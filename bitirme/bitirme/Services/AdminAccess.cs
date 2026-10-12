using System.Net;

namespace bitirme.Services
{
    /// <summary>
    /// Yönetim panelinin ağ kısıtı (Faz 9; `YOL_HARITASI.md` "Yönetim panelinin ayrı alt alan adı veya IP kısıtıyla korunması").
    /// <c>AdminAccess:AllowedNetworks</c> boşsa kısıt yoktur (varsayılan). Doluysa <c>/Yonetim</c> altındaki bütün adresler yalnızca
    /// bu ağlardan (CIDR ya da tek adres) açılır; diğerlerine 404 döner (panelin varlığı da belli olmaz). İstemci adresi ters
    /// vekilden güvenle alınır (<see cref="ReverseProxySettings"/>; middleware iletilen başlıklardan sonra çalışır).
    /// </summary>
    public static class AdminAccess
    {
        public const string SectionName = "AdminAccess";
        public static readonly PathString AdminPath = new("/Yonetim");

        /// <summary>Ayardaki ağları ayrıştırır; hatalı değer açılışta hata verir (yanlış ayarla panel açık kalmasın).</summary>
        public static IReadOnlyList<IPNetwork> ParseNetworks(IEnumerable<string>? values)
        {
            var networks = new List<IPNetwork>();
            foreach (var raw in values ?? [])
            {
                var value = raw.Trim();
                if (value.Length == 0)
                {
                    continue;
                }

                if (IPAddress.TryParse(value, out var single))
                {
                    networks.Add(new IPNetwork(single, single.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128));
                }
                else if (IPNetwork.TryParse(value, out var network))
                {
                    networks.Add(network);
                }
                else
                {
                    throw new InvalidOperationException($"{SectionName}:AllowedNetworks içinde geçersiz adres veya ağ: '{value}'.");
                }
            }

            return networks;
        }

        /// <summary>İzinli mi? Liste boşsa her adres; IPv4'e eşlenmiş IPv6 adresleri IPv4 olarak değerlendirilir.</summary>
        public static bool IsAllowed(IReadOnlyList<IPNetwork> networks, IPAddress? address)
        {
            if (networks.Count == 0)
            {
                return true;
            }

            if (address == null)
            {
                return false;
            }

            var normalized = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
            return networks.Any(n => n.Contains(normalized));
        }

        public static IApplicationBuilder UseMoodicaAdminAccess(this IApplicationBuilder app, IConfiguration configuration)
        {
            // Liste ya da (ortam değişkeni için) virgülle ayrılmış tek değer: AdminAccess__AllowedNetworks=85.1.2.3,10.8.0.0/24
            var key = $"{SectionName}:AllowedNetworks";
            var networks = ParseNetworks(configuration.GetSection(key).Get<string[]>() ?? configuration[key]?.Split(','));
            if (networks.Count == 0)
            {
                return app;
            }

            return app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments(AdminPath, StringComparison.OrdinalIgnoreCase) &&
                    !IsAllowed(networks, context.Connection.RemoteIpAddress))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                await next();
            });
        }
    }
}
