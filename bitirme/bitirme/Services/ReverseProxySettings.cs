using System;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace bitirme.Services
{
    /// <summary>
    /// Ters vekil (nginx vb.) arkasında gerçek istemci adresi ve şeması (Faz 9). Vekil HTTPS'i sonlandırıp isteği
    /// uygulamaya iletince bağlantının adresi vekilin adresidir: istek sınırları (Faz 8) bütün kullanıcıları tek adres sayar,
    /// HTTPS yönlendirmesi döngüye girer. <c>ReverseProxy:Enabled=true</c> iken <c>X-Forwarded-For</c> ve
    /// <c>X-Forwarded-Proto</c> yalnızca güvenilen vekilden (yerel döngü adresi + <c>KnownProxies</c> + <c>KnownNetworks</c>)
    /// gelirse kabul edilir; başka bir adresten gelen başlık yok sayılır (sahte adresle sınırları aşmak engellenir).
    /// Varsayılan kapalı: vekilsiz çalışan uygulamada başlıklara hiç güvenilmez.
    /// </summary>
    public static class ReverseProxySettings
    {
        public const string SectionName = "ReverseProxy";

        public static bool IsEnabled(IConfiguration configuration) =>
            configuration.GetValue($"{SectionName}:Enabled", false);

        public static IServiceCollection AddMoodicaForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
        {
            if (!IsEnabled(configuration))
            {
                return services;
            }

            var proxies = configuration.GetSection($"{SectionName}:KnownProxies").Get<string[]>() ?? [];
            var networks = configuration.GetSection($"{SectionName}:KnownNetworks").Get<string[]>() ?? [];
            services.Configure<ForwardedHeadersOptions>(options => Apply(options, proxies, networks));
            return services;
        }

        /// <summary>
        /// İletilen başlık ayarları: yalnızca istemci adresi ve şema (Host başlığına güvenilmez), tek vekil katmanı.
        /// Varsayılan güvenilenler (127.0.0.0/8, ::1) korunur. Hatalı adres açılışta hata verir (sessizce yok sayılmaz).
        /// </summary>
        public static void Apply(ForwardedHeadersOptions options, string[] knownProxies, string[] knownNetworks)
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;

            foreach (var proxy in knownProxies)
            {
                if (!IPAddress.TryParse(proxy.Trim(), out var address))
                {
                    throw new InvalidOperationException($"{SectionName}:KnownProxies içinde geçersiz IP adresi: '{proxy}'.");
                }

                options.KnownProxies.Add(address);
            }

            foreach (var network in knownNetworks)
            {
                if (!System.Net.IPNetwork.TryParse(network.Trim(), out var parsed))
                {
                    throw new InvalidOperationException($"{SectionName}:KnownNetworks içinde geçersiz ağ (ör. 172.18.0.0/16): '{network}'.");
                }

                options.KnownIPNetworks.Add(parsed);
            }
        }

        /// <summary>Ardışık düzenin en başında çağrılır: HSTS, HTTPS yönlendirmesi, loglama ve istek sınırları gerçek adresi görür.</summary>
        public static WebApplication UseMoodicaForwardedHeaders(this WebApplication app)
        {
            if (IsEnabled(app.Configuration))
            {
                app.UseForwardedHeaders();
            }

            return app;
        }
    }
}
