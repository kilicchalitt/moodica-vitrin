using System;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace bitirme.Services
{
    /// <summary>
    /// Tarayıcı güvenlik başlıkları (Faz 8): içerik güvenlik politikası (CSP), çerçeveye alınmama, MIME koklamama,
    /// yönlendiren bilgisi ve tarayıcı izinleri. Satır içi betikler yalnızca bu isteğe özel nonce ile çalışır
    /// (<c>&lt;script nonce="@Context.CspNonce()"&gt;</c>); satır içi olay işleyicisi (onclick vb.) çalışmaz.
    /// Dış betik, stil ve yazı tipi kaynağı yok: Bootstrap, SweetAlert2, SignalR, Font Awesome ve yazı tipleri
    /// sunucumuzdan (wwwroot/lib) verilir; ziyaretçinin IP'si CDN'lere gitmez, CDN'deki dosya değişse etkilenmeyiz (ISSUE-53).
    /// Satır içi stil (style özniteliği, SweetAlert2'nin eklediği stil) izinli; stil enjeksiyonu betik çalıştıramaz.
    /// </summary>
    public static class SecurityHeaders
    {
        private const string NonceKey = "Moodica.CspNonce";

        /// <summary>Bu isteğin CSP nonce değeri (satır içi betik etiketine yazılır).</summary>
        public static string CspNonce(this HttpContext context) =>
            context.Items[NonceKey] as string ?? throw new InvalidOperationException("Güvenlik başlıkları ara katmanı çalışmadı.");

        public static IApplicationBuilder UseMoodicaSecurityHeaders(this IApplicationBuilder app, IWebHostEnvironment environment)
        {
            // Geliştirmede Visual Studio'nun tarayıcı yenileme/Browser Link betikleri başka bir yerel bağlantı noktasından gelir.
            var developmentSources = environment.IsDevelopment() ? " http://localhost:* ws://localhost:* wss://localhost:*" : string.Empty;

            return app.Use(async (context, next) =>
            {
                // Onaltılık: CSP'de geçerli ve Razor özniteliğe yazarken kodlamaz (base64'teki +, /, = kodlanıyordu).
                var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
                context.Items[NonceKey] = nonce;

                var headers = context.Response.Headers;
                headers.ContentSecurityPolicy =
                    "default-src 'self'; " +
                    $"script-src 'self' 'nonce-{nonce}'{developmentSources}; " +
                    "style-src 'self' 'unsafe-inline'; " +
                    "font-src 'self' data:; " +
                    "img-src 'self' data: blob:; " +
                    "media-src 'self' blob:; " +
                    $"connect-src 'self'{developmentSources}; " +
                    // form-action yönlendirmeyi de kapsar: "Ödemeye geç" formu iyzico'nun ödeme sayfasına yönlenir (Faz 6 B).
                    "object-src 'none'; base-uri 'self'; form-action 'self' https://sandbox-cpp.iyzipay.com https://cpp.iyzipay.com; frame-ancestors 'none'";
                headers.XFrameOptions = "DENY";
                headers.XContentTypeOptions = "nosniff";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["Permissions-Policy"] = "camera=(self), microphone=(self), display-capture=(), geolocation=(), payment=(), usb=()";
                headers["Cross-Origin-Opener-Policy"] = "same-origin";

                await next();
            });
        }
    }
}
