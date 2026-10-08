using bitirme.Data;
using bitirme.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace bitirme.Services.Email
{
    /// <summary>Arka plan servisini yeni e-posta geldiğinde beklemeden uyandırır.</summary>
    public sealed class EmailOutboxSignal
    {
        private readonly SemaphoreSlim _signal = new(0);

        public void Notify()
        {
            if (_signal.CurrentCount == 0)
            {
                _signal.Release();
            }
        }

        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) => _signal.WaitAsync(timeout, cancellationToken);
    }

    /// <summary>
    /// Uygulamanın e-posta kapısı: e-postayı gönderilmek üzere giden kutusuna yazar (istek SMTP'yi beklemez; ISSUE-25).
    /// </summary>
    public class OutboxEmailService : IEmailService
    {
        private readonly AppDbContext _context;
        private readonly TimeProvider _time;
        private readonly EmailOutboxSignal _signal;

        public OutboxEmailService(AppDbContext context, TimeProvider time, EmailOutboxSignal signal)
        {
            _context = context;
            _time = time;
            _signal = signal;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string message)
        {
            var now = _time.UtcNow();
            _context.OutboxEmails.Add(new OutboxEmail
            {
                To = toEmail,
                Subject = subject,
                HtmlBody = message,
                Status = OutboxStatus.Pending,
                CreatedAt = now,
                NextAttemptAt = now
            });
            await _context.SaveChangesAsync();
            _signal.Notify();
        }
    }

    /// <summary>Tek bir e-postayı SMTP ile gönderir.</summary>
    public interface ISmtpSender
    {
        /// <summary>Gönderen adresi ve şifre ayarlı mı? Değilse e-postalar kutuda bekler (geliştirme).</summary>
        bool IsConfigured { get; }

        Task SendAsync(OutboxEmail email, CancellationToken cancellationToken);
    }

    /// <summary>
    /// MailKit ile SMTP gönderimi (System.Net.Mail.SmtpClient yerine; ISSUE-25). Ayarlar appsettings'ten, şifre User
    /// Secrets / ortam değişkeninden (DECISION-08).
    /// </summary>
    public class MailKitSmtpSender : ISmtpSender
    {
        private readonly IConfiguration _configuration;

        public MailKitSmtpSender(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private string? SenderEmail => _configuration["EmailSettings:SenderEmail"];
        private string? Password => _configuration["EmailSettings:Password"];

        public bool IsConfigured => !string.IsNullOrWhiteSpace(SenderEmail) && !string.IsNullOrWhiteSpace(Password);

        public async Task SendAsync(OutboxEmail email, CancellationToken cancellationToken)
        {
            if (SenderEmail is not { Length: > 0 } sender || Password is not { Length: > 0 } password)
            {
                throw new InvalidOperationException("E-posta ayarları eksik (EmailSettings:SenderEmail / EmailSettings:Password).");
            }

            var server = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
            var port = int.TryParse(_configuration["EmailSettings:Port"], out var p) ? p : 587;

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Moodica", sender));
            message.To.Add(MailboxAddress.Parse(email.To));
            message.Subject = email.Subject;
            message.Body = new BodyBuilder { HtmlBody = email.HtmlBody }.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(server, port, SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);
            await client.AuthenticateAsync(sender, password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
    }

    /// <summary>
    /// Giden kutusunu işler: zamanı gelen e-postaları gönderir, hatada <see cref="EmailRules.RetryDelay"/> ile yeniden
    /// dener, <see cref="EmailRules.MaxAttempts"/> sonra vazgeçer; saklama süresi dolanları siler.
    /// </summary>
    public class EmailOutboxProcessor
    {
        private readonly AppDbContext _context;
        private readonly ISmtpSender _sender;
        private readonly TimeProvider _time;
        private readonly ILogger<EmailOutboxProcessor> _logger;

        public EmailOutboxProcessor(AppDbContext context, ISmtpSender sender, TimeProvider time, ILogger<EmailOutboxProcessor> logger)
        {
            _context = context;
            _sender = sender;
            _time = time;
            _logger = logger;
        }

        /// <summary>Zamanı gelen en fazla <see cref="EmailRules.BatchSize"/> e-postayı gönderir; gönderilen sayıyı döner.</summary>
        public async Task<int> ProcessDueAsync(CancellationToken cancellationToken = default)
        {
            if (!_sender.IsConfigured)
            {
                return 0;
            }

            var now = _time.UtcNow();
            var due = await _context.OutboxEmails
                .Where(e => e.Status == OutboxStatus.Pending && e.NextAttemptAt <= now)
                .OrderBy(e => e.Id)
                .Take(EmailRules.BatchSize)
                .ToListAsync(cancellationToken);

            var sent = 0;
            foreach (var email in due)
            {
                if (EmailRules.IsReservedAddress(email.To))
                {
                    email.Status = OutboxStatus.Failed;
                    email.LastError = "Ayrılmış (deneme) alan adı; gönderilmedi.";
                    await _context.SaveChangesAsync(cancellationToken);
                    continue;
                }

                email.Attempts++;
                try
                {
                    await _sender.SendAsync(email, cancellationToken);
                    email.Status = OutboxStatus.Sent;
                    email.SentAt = _time.UtcNow();
                    email.LastError = null;
                    sent++;
                    _logger.LogInformation("E-posta gönderildi. Kutu no {Id}, konu: {Subject}", email.Id, email.Subject);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    email.LastError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                    if (email.Attempts >= EmailRules.MaxAttempts)
                    {
                        email.Status = OutboxStatus.Failed;
                        _logger.LogError(ex, "E-posta {Attempts} denemede gönderilemedi, vazgeçildi. Kutu no {Id}", email.Attempts, email.Id);
                    }
                    else
                    {
                        email.NextAttemptAt = _time.UtcNow() + EmailRules.RetryDelay(email.Attempts);
                        _logger.LogWarning(ex, "E-posta gönderilemedi, yeniden denenecek. Kutu no {Id}", email.Id);
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);
            }

            return sent;
        }

        /// <summary>Saklama süresi dolan (gönderilmiş, vazgeçilmiş ya da bekleyen) kayıtları siler.</summary>
        public Task<int> PurgeAsync(CancellationToken cancellationToken = default)
        {
            var cutoff = _time.UtcNow() - EmailRules.Retention;
            return _context.OutboxEmails.Where(e => e.CreatedAt < cutoff).ExecuteDeleteAsync(cancellationToken);
        }
    }
}
