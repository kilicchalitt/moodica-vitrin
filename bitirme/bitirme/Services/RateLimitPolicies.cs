using System;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using bitirme.Services.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace bitirme.Services
{
    /// <summary>
    /// İstek sınırlama (Faz 8): şifre deneme, e-posta gönderen hesap işlemleri, kayıt ve şifreli veri yazan gönderimler
    /// sabit zaman penceresiyle sınırlanır. Anonim işlemler IP adresine, gönderimler kullanıcıya göre sayılır. Hesap başına
    /// kilit (5 hatada 15 dk) ayrıca Identity'dedir; bu sınır çok hesaba yayılan denemeleri ve e-posta bombardımanını keser.
    /// <c>RateLimiting:Enabled=false</c> sınırları kaldırır (entegrasyon testleri tek adresten çok sayıda giriş yapar).
    /// Ters vekil arkasında gerçek IP <see cref="ReverseProxySettings"/> ile gelir (yalnızca güvenilen vekilden).
    /// </summary>
    public static class RateLimitPolicies
    {
        /// <summary>Şifre/kod denemesi: giriş, 2FA girişi, yönetici girişi, şifre sıfırlama.</summary>
        public const string SignIn = "giris";

        /// <summary>E-posta gönderen hesap işlemleri: şifremi unuttum, doğrulama e-postasını yeniden gönderme.</summary>
        public const string AccountEmail = "hesap-epostasi";

        /// <summary>Danışan ve uzman kaydı.</summary>
        public const string Register = "kayit";

        /// <summary>Şifreli sağlık verisi yazan gönderimler: ihtiyaç analizi anketi, ilerleme ölçümü.</summary>
        public const string Submission = "gonderim";

        /// <summary>Telefon doğrulama kodu gönderme (ücretli mesaj; kullanıcı başına).</summary>
        public const string PhoneCodeSend = "telefon-kodu";

        /// <summary>Telefon doğrulama kodunu girme (tahmin denemesi; kullanıcı başına).</summary>
        public const string PhoneCodeVerify = "telefon-dogrulama";

        public static string RejectedMessage => SiteText.T("Çok fazla deneme yapıldı. Lütfen birkaç dakika sonra tekrar deneyin.");

        public static IServiceCollection AddMoodicaRateLimiting(this IServiceCollection services, IConfiguration configuration)
        {
            var enabled = configuration.GetValue("RateLimiting:Enabled", true);

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = WriteRejectionAsync;

                AddPolicy(options, SignIn, enabled, ClientAddress, permits: 10, TimeSpan.FromMinutes(1));
                AddPolicy(options, AccountEmail, enabled, ClientAddress, permits: 5, TimeSpan.FromMinutes(15));
                AddPolicy(options, Register, enabled, ClientAddress, permits: 5, TimeSpan.FromHours(1));
                AddPolicy(options, Submission, enabled, UserOrAddress, permits: 10, TimeSpan.FromHours(1));
                AddPolicy(options, PhoneCodeSend, enabled, UserOrAddress, permits: 3, TimeSpan.FromMinutes(15));
                AddPolicy(options, PhoneCodeVerify, enabled, UserOrAddress, permits: 10, TimeSpan.FromMinutes(15));
            });

            return services;
        }

        private static void AddPolicy(RateLimiterOptions options, string name, bool enabled, Func<HttpContext, string> partitionKey,
            int permits, TimeSpan window)
        {
            options.AddPolicy(name, context => enabled
                ? RateLimitPartition.GetFixedWindowLimiter(partitionKey(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permits,
                    Window = window,
                    QueueLimit = 0
                })
                : RateLimitPartition.GetNoLimiter(string.Empty));
        }

        private static string ClientAddress(HttpContext context) =>
            "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "bilinmiyor");

        private static string UserOrAddress(HttpContext context) =>
            context.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } userId ? "kullanici:" + userId : ClientAddress(context);

        private static async System.Threading.Tasks.ValueTask WriteRejectionAsync(OnRejectedContext rejected, System.Threading.CancellationToken cancellationToken)
        {
            var response = rejected.HttpContext.Response;
            if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            }

            var request = rejected.HttpContext.Request;
            var wantsJson = request.HasJsonContentType()
                || request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);
            if (wantsJson)
            {
                // Anket istemcisi "errors", panel betikleri "message" alanını gösterir.
                await response.WriteAsJsonAsync(new { success = false, message = RejectedMessage, errors = new[] { RejectedMessage } }, cancellationToken);
                return;
            }

            response.ContentType = "text/html; charset=utf-8";
            await response.WriteAsync(
                $"<!DOCTYPE html><html lang=\"{SiteCultures.Current}\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
                $"<title>{WebUtility.HtmlEncode(SiteText.T("Çok fazla deneme"))} - Moodica</title></head>" +
                "<body style=\"margin:0;background:#F8F6EF;font-family:system-ui,sans-serif;color:#1C2733\">" +
                "<div style=\"max-width:32rem;margin:4rem auto;padding:2rem;background:#fff;border:1px solid #E3DED0;border-radius:16px\">" +
                "<p style=\"margin:0 0 1rem;font-family:Georgia,serif;font-size:1.4rem;color:#1460B0\">Moodica</p>" +
                $"<h1 style=\"margin:0 0 .75rem;font-family:Georgia,serif;font-weight:normal;font-size:1.6rem;color:#0A2E57\">{WebUtility.HtmlEncode(SiteText.T("Çok fazla deneme"))}</h1>" +
                $"<p>{WebUtility.HtmlEncode(RejectedMessage)}</p>" +
                $"<p><a href=\"/\" style=\"color:#1460B0;font-weight:600\">{WebUtility.HtmlEncode(SiteText.T("Ana sayfaya dön"))}</a></p></div>" +
                "</body></html>", cancellationToken);
        }
    }
}
