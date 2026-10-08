using bitirme.Data;
using bitirme.Models;
using Microsoft.EntityFrameworkCore;

namespace bitirme.Services.Email
{
    /// <summary>
    /// Seans hatırlatmaları (DECISION-30): onaylı randevu için 24 saat ve 1 saat kala danışana ve uzmana e-posta.
    /// Gönderilen hatırlatma randevuda işaretlenir; aynı hatırlatma iki kez gitmez.
    /// </summary>
    public class AppointmentReminderService
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _email;
        private readonly TimeProvider _time;

        public AppointmentReminderService(AppDbContext context, IEmailService email, TimeProvider time)
        {
            _context = context;
            _email = email;
            _time = time;
        }

        /// <summary>Zamanı gelen hatırlatmaları giden kutusuna ekler; eklenen randevu sayısını döner.</summary>
        public async Task<int> EnqueueDueAsync(CancellationToken cancellationToken = default)
        {
            var now = _time.UtcNow();
            var horizon = now.AddHours(24);
            var candidates = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .Where(a => a.Status == AppointmentStatus.Approved && a.AppointmentDate > now && a.AppointmentDate <= horizon &&
                            (a.ReminderDayBeforeSentAt == null || a.ReminderHourBeforeSentAt == null))
                .ToListAsync(cancellationToken);

            var count = 0;
            foreach (var appointment in candidates)
            {
                var due = EmailRules.DueReminder(appointment.AppointmentDate, now,
                    appointment.ReminderDayBeforeSentAt != null, appointment.ReminderHourBeforeSentAt != null);
                if (due is not { } kind)
                {
                    continue;
                }

                // Önce işaretlenir: e-posta kuyruğa girmeden işlem yarıda kalırsa hatırlatma tekrar denenmez (çift gönderim yerine).
                if (kind == ReminderKind.DayBefore)
                {
                    appointment.ReminderDayBeforeSentAt = now;
                }
                else
                {
                    appointment.ReminderHourBeforeSentAt = now;
                }

                await _context.SaveChangesAsync(cancellationToken);

                foreach (var (to, message) in new[]
                         {
                             (appointment.Patient?.Email, AppointmentEmails.ReminderForPatient(appointment, kind)),
                             (appointment.Doctor?.Email, AppointmentEmails.ReminderForDoctor(appointment, kind))
                         })
                {
                    if (!string.IsNullOrWhiteSpace(to))
                    {
                        await _email.SendEmailAsync(to, message.Subject, message.HtmlBody);
                    }
                }

                count++;
            }

            return count;
        }
    }

    /// <summary>
    /// Arka plan e-posta işçisi: belirli aralıkla (ya da yeni e-posta gelince hemen) hatırlatmaları kuyruğa ekler, giden
    /// kutusunu gönderir ve saatte bir eski kayıtları siler. <c>Email:BackgroundWorker</c> false ise çalışmaz (testler).
    /// </summary>
    public class EmailBackgroundWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly EmailOutboxSignal _signal;
        private readonly ILogger<EmailBackgroundWorker> _logger;
        private readonly TimeSpan _interval;
        private readonly bool _enabled;
        private DateTime _lastPurge = DateTime.MinValue;

        public EmailBackgroundWorker(IServiceScopeFactory scopes, EmailOutboxSignal signal, IConfiguration configuration, ILogger<EmailBackgroundWorker> logger)
        {
            _scopes = scopes;
            _signal = signal;
            _logger = logger;
            _interval = TimeSpan.FromSeconds(configuration.GetValue("Email:PollSeconds", 30));
            _enabled = configuration.GetValue("Email:BackgroundWorker", true);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_enabled)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<AppointmentReminderService>().EnqueueDueAsync(stoppingToken);
                    await scope.ServiceProvider.GetRequiredService<EmailOutboxProcessor>().ProcessDueAsync(stoppingToken);

                    if (DateTime.UtcNow - _lastPurge > TimeSpan.FromHours(1))
                    {
                        var purged = await scope.ServiceProvider.GetRequiredService<EmailOutboxProcessor>().PurgeAsync(stoppingToken);
                        _lastPurge = DateTime.UtcNow;
                        if (purged > 0)
                        {
                            _logger.LogInformation("Giden kutusundan {Count} eski e-posta silindi.", purged);
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "E-posta işçisinde hata; bir sonraki turda yeniden denenecek.");
                }

                try
                {
                    await _signal.WaitAsync(_interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
