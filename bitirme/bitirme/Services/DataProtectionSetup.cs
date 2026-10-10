using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using bitirme.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace bitirme.Services
{
    /// <summary>
    /// Data Protection: oturum çerezleri, antiforgery ve şifreli alanların (2FA anahtarı, anket, notlar) anahtarları
    /// veritabanında kalıcı (ISSUE-29). Yayında anahtarların kendisi bir sertifikayla (PFX) şifrelenir: sertifika veritabanının
    /// dışında, sunucu diskinde durur; veritabanı dökümü veya yedeği tek başına anahtarları vermez (Faz 9).
    /// Sertifika kaybolursa şifreli veri okunamaz: <c>deploy/yedekle.sh</c> sertifikayı ve şifresini ayrıca şifreli yedekler.
    /// Geliştirme ve testlerde sertifika gerekmez (anahtarlar düz XML, önceki gibi).
    /// </summary>
    public static class DataProtectionSetup
    {
        public const string CertificatePathKey = "DataProtection:CertificatePath";
        public const string CertificatePasswordKey = "DataProtection:CertificatePassword";

        public static IServiceCollection AddMoodicaDataProtection(this IServiceCollection services, IConfiguration configuration,
            IHostEnvironment environment)
        {
            var builder = services.AddDataProtection()
                .SetApplicationName("Moodica")
                .PersistKeysToDbContext<AppDbContext>();

            var path = configuration[CertificatePathKey];
            if (!string.IsNullOrWhiteSpace(path))
            {
                // Yeni anahtarlar bu sertifikayla şifrelenir; aynı sertifika çözmek için de kaydedilir.
                builder.ProtectKeysWithCertificate(LoadCertificate(path, configuration[CertificatePasswordKey]));
            }
            else if (RequiresCertificate(environment))
            {
                throw new InvalidOperationException(
                    $"Yayında Data Protection anahtarları şifreli olmalı: {CertificatePathKey} ve {CertificatePasswordKey} verilmeli " +
                    "(deploy/kur.sh sertifikayı üretir; deploy/README.md).");
            }

            return services;
        }

        /// <summary>Geliştirme ve entegrasyon testleri dışında (staging, production) sertifika zorunlu.</summary>
        public static bool RequiresCertificate(IHostEnvironment environment) =>
            !environment.IsDevelopment() && !environment.IsEnvironment("Testing");

        public static X509Certificate2 LoadCertificate(string path, string? password)
        {
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"Data Protection sertifikası bulunamadı: {path}");
            }

            X509Certificate2 certificate;
            try
            {
                // Özel anahtar diske/kullanıcı profiline yazılmadan bellekte tutulur.
                certificate = X509CertificateLoader.LoadPkcs12FromFile(path, password, X509KeyStorageFlags.EphemeralKeySet);
            }
            catch (CryptographicException ex)
            {
                throw new InvalidOperationException($"Data Protection sertifikası açılamadı (şifre yanlış veya dosya bozuk): {path}", ex);
            }

            if (!certificate.HasPrivateKey)
            {
                throw new InvalidOperationException($"Data Protection sertifikasında özel anahtar yok: {path}");
            }

            return certificate;
        }
    }
}
