using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace bitirme.Services
{
    /// <summary>
    /// Görüntülü seans için STUN/TURN ayarları ("Turn" bölümü; DECISION-31). Anahtarlar yalnızca User Secrets /
    /// ortam değişkeninde tutulur (DECISION-08). Sağlayıcı boşsa veya anahtar eksikse yalnızca STUN kullanılır.
    /// </summary>
    public sealed class TurnOptions
    {
        public const string SectionName = "Turn";
        public const string Cloudflare = "Cloudflare";
        public const string Coturn = "Coturn";

        /// <summary>"Cloudflare" (geliştirme/demo), "Coturn" (yayın, yurt içi sunucu) veya boş.</summary>
        public string? Provider { get; set; }

        public string[] StunUrls { get; set; } = ["stun:stun.cloudflare.com:3478"];

        public string? CloudflareKeyId { get; set; }
        public string? CloudflareApiToken { get; set; }

        /// <summary>coturn adresleri (ör. "turn:turn.ornek.com.tr:3478?transport=udp", "turns:turn.ornek.com.tr:443?transport=tcp").</summary>
        public string[] CoturnUrls { get; set; } = [];

        /// <summary>coturn <c>static-auth-secret</c> (<c>use-auth-secret</c> kısa ömürlü kimlik bilgisi).</summary>
        public string? CoturnSecret { get; set; }

        /// <summary>
        /// Görüşme yalnızca TURN rölesi üzerinden kurulur (tarayıcıda <c>iceTransportPolicy: "relay"</c>). TURN denemesi ve
        /// sorun ayıklama içindir; TURN kimlik bilgisi yoksa yok sayılır (yoksa hiç bağlantı kurulamaz). Varsayılan kapalı.
        /// </summary>
        public bool ForceRelay { get; set; }
    }

    /// <summary>Tarayıcının <c>RTCIceServer</c> biçimi.</summary>
    public sealed record IceServer(
        [property: JsonPropertyName("urls")] IReadOnlyList<string> Urls,
        [property: JsonPropertyName("username"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Username = null,
        [property: JsonPropertyName("credential"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Credential = null);

    /// <summary>TURN kimlik bilgisi kuralları; veritabanından ve ağdan bağımsız, birim testli.</summary>
    public static class TurnCredentialRules
    {
        /// <summary>Oda kapandıktan sonra da süren bir görüşmenin rölesi kopmasın diye eklenen pay.</summary>
        public static readonly TimeSpan Margin = TimeSpan.FromMinutes(15);

        /// <summary>Kimlik bilgisi odanın kapanışından <see cref="Margin"/> sonrasına kadar geçerlidir (en az <see cref="Margin"/>).</summary>
        public static TimeSpan Lifetime(DateTime sessionStart, DateTime now)
        {
            var untilClose = AppointmentRules.MeetingClosesAt(sessionStart) - now;
            return (untilClose > TimeSpan.Zero ? untilClose : TimeSpan.Zero) + Margin;
        }

        /// <summary>
        /// coturn TURN REST API (<c>use-auth-secret</c>): kullanıcı adı "bitiş zamanı:etiket", parola
        /// HMAC-SHA1(anahtar, kullanıcı adı) Base64. Etiket rastgele; randevu veya kişi bilgisi taşımaz.
        /// </summary>
        public static (string Username, string Credential) Coturn(string secret, DateTimeOffset expiresAt, string label)
        {
            var username = $"{expiresAt.ToUnixTimeSeconds()}:{label}";
            using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(secret));
            return (username, Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(username))));
        }
    }

    /// <summary>Görüşme sayfasına verilecek ICE sunucularını üretir (DECISION-31).</summary>
    public sealed class IceServerProvider
    {
        public const string CloudflareEndpoint = "https://rtc.live.cloudflare.com/v1/turn/keys/{0}/credentials/generate-ice-servers";

        private readonly HttpClient _http;
        private readonly TurnOptions _options;
        private readonly TimeProvider _time;
        private readonly ILogger<IceServerProvider> _logger;

        public IceServerProvider(HttpClient http, IOptions<TurnOptions> options, TimeProvider time, ILogger<IceServerProvider> logger)
        {
            _http = http;
            _options = options.Value;
            _time = time;
            _logger = logger;
        }

        public async Task<IReadOnlyList<IceServer>> GetAsync(TimeSpan lifetime, CancellationToken cancellationToken = default)
        {
            if (string.Equals(_options.Provider, TurnOptions.Cloudflare, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(_options.CloudflareKeyId) && !string.IsNullOrWhiteSpace(_options.CloudflareApiToken))
            {
                var servers = await GetCloudflareAsync(lifetime, cancellationToken);
                if (servers != null)
                {
                    return servers;
                }
            }
            // Boş adres (ör. ortam değişkeni verilmemiş ikinci adres) tarayıcıda RTCPeerConnection'ı bozar; ayıklanır.
            else if (string.Equals(_options.Provider, TurnOptions.Coturn, StringComparison.OrdinalIgnoreCase) &&
                     !string.IsNullOrWhiteSpace(_options.CoturnSecret) && CoturnUrls() is { Length: > 0 } coturnUrls)
            {
                var (username, credential) = TurnCredentialRules.Coturn(_options.CoturnSecret,
                    _time.GetUtcNow() + lifetime, Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant());
                return [.. StunOnly(), new IceServer(coturnUrls, username, credential)];
            }

            return StunOnly();
        }

        /// <summary>Tarayıcının <c>iceTransportPolicy</c> değeri: "relay" yalnızca ForceRelay açık ve TURN sunucusu verildiyse.</summary>
        public string TransportPolicy(IReadOnlyList<IceServer> servers) =>
            _options.ForceRelay && servers.Any(s => s.Credential != null) ? "relay" : "all";

        private string[] CoturnUrls() => _options.CoturnUrls.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).ToArray();

        private IReadOnlyList<IceServer> StunOnly() =>
            _options.StunUrls.Length == 0 ? [] : [new IceServer(_options.StunUrls)];

        // Cloudflare kısa ömürlü kimlik bilgisi üretir; hata olursa görüşme STUN ile denenir (aynı ağda yine çalışır).
        private async Task<IReadOnlyList<IceServer>?> GetCloudflareAsync(TimeSpan lifetime, CancellationToken cancellationToken)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, string.Format(CloudflareEndpoint, Uri.EscapeDataString(_options.CloudflareKeyId!)))
                {
                    Content = JsonContent.Create(new { ttl = (int)lifetime.TotalSeconds })
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.CloudflareApiToken);

                using var response = await _http.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Cloudflare TURN kimlik bilgisi alınamadı: HTTP {StatusCode}; yalnızca STUN kullanılıyor.", (int)response.StatusCode);
                    return null;
                }

                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                var servers = ParseCloudflare(document.RootElement);
                if (servers.Count == 0)
                {
                    _logger.LogWarning("Cloudflare TURN yanıtında ICE sunucusu yok; yalnızca STUN kullanılıyor.");
                    return null;
                }

                return servers;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogWarning("Cloudflare TURN isteği başarısız ({Error}); yalnızca STUN kullanılıyor.", ex.GetType().Name);
                return null;
            }
        }

        /// <summary>Cloudflare yanıtı: <c>{"iceServers":[{"urls":[...]|"...","username":"...","credential":"..."}]}</c>.</summary>
        public static IReadOnlyList<IceServer> ParseCloudflare(JsonElement root)
        {
            if (!root.TryGetProperty("iceServers", out var list) || list.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var servers = new List<IceServer>();
            foreach (var item in list.EnumerateArray())
            {
                if (!item.TryGetProperty("urls", out var urlsElement))
                {
                    continue;
                }

                var urls = urlsElement.ValueKind == JsonValueKind.Array
                    ? urlsElement.EnumerateArray().Select(u => u.GetString()).OfType<string>().ToList()
                    : urlsElement.GetString() is { } single ? [single] : [];
                if (urls.Count == 0)
                {
                    continue;
                }

                servers.Add(new IceServer(urls,
                    item.TryGetProperty("username", out var username) ? username.GetString() : null,
                    item.TryGetProperty("credential", out var credential) ? credential.GetString() : null));
            }

            return servers;
        }
    }
}
