using System.Globalization;
using System.Xml.Linq;

namespace bitirme.Services.Payments
{
    /// <summary>Bir TL tutarın yaklaşık döviz karşılığı (yalnızca bilgi; ödeme TL alınır).</summary>
    public sealed record ApproximateAmount(string Currency, string Symbol, decimal Amount);

    /// <summary>
    /// İngilizcede ücretlerin yanında yaklaşık £/€/$ karşılığı (DECISION-49). Kur, Türkiye Cumhuriyet Merkez Bankası'nın
    /// herkese açık günlük kur dosyasından (döviz satış) alınır, 6 saat bellekte tutulur. İstekte kişisel veri gönderilmez.
    /// Kur alınamazsa karşılık gösterilmez (sayfa bozulmaz); test ortamında dışarı istek atılmaz.
    /// </summary>
    public sealed class ExchangeRates(HttpClient http, IHostEnvironment environment, TimeProvider time, ILogger<ExchangeRates> logger)
    {
        public const string Source = "https://www.tcmb.gov.tr/kurlar/today.xml";
        private static readonly (string Code, string Symbol)[] Currencies = [("GBP", "£"), ("EUR", "€"), ("USD", "$")];
        private static readonly TimeSpan CacheFor = TimeSpan.FromHours(6);
        private static readonly SemaphoreSlim Gate = new(1, 1);
        private static IReadOnlyDictionary<string, decimal>? _rates;
        private static DateTimeOffset _fetchedAt;

        /// <summary>Tutarın yaklaşık karşılıkları (GBP, EUR, USD); kur yoksa boş.</summary>
        public async Task<IReadOnlyList<ApproximateAmount>> ApproximateAsync(decimal amountTry, CancellationToken cancellationToken = default)
        {
            var rates = await GetRatesAsync(cancellationToken);
            return Convert(amountTry, rates);
        }

        /// <summary>Saf hesap (birim testli): TL tutar / kur, en yakın tam sayıya yuvarlanır.</summary>
        public static IReadOnlyList<ApproximateAmount> Convert(decimal amountTry, IReadOnlyDictionary<string, decimal>? rates) =>
            rates == null
                ? []
                : Currencies
                    .Where(c => rates.TryGetValue(c.Code, out var rate) && rate > 0)
                    .Select(c => new ApproximateAmount(c.Code, c.Symbol, Math.Round(amountTry / rates[c.Code], 0, MidpointRounding.AwayFromZero)))
                    .ToList();

        /// <summary>TCMB kur dosyasını okur: Currency[@Kod]/ForexSelling ve Unit (birim) → 1 birim dövizin TL karşılığı.</summary>
        public static IReadOnlyDictionary<string, decimal> Parse(string xml)
        {
            var result = new Dictionary<string, decimal>();
            foreach (var currency in XDocument.Parse(xml).Descendants("Currency"))
            {
                var code = (string?)currency.Attribute("Kod") ?? (string?)currency.Attribute("CurrencyCode");
                if (code == null || Currencies.All(c => c.Code != code))
                {
                    continue;
                }

                if (decimal.TryParse((string?)currency.Element("ForexSelling"), NumberStyles.Number, CultureInfo.InvariantCulture, out var selling) &&
                    int.TryParse((string?)currency.Element("Unit"), out var unit) && unit > 0 && selling > 0)
                {
                    result[code] = selling / unit;
                }
            }

            return result;
        }

        private async Task<IReadOnlyDictionary<string, decimal>?> GetRatesAsync(CancellationToken cancellationToken)
        {
            if (environment.IsEnvironment("Testing"))
            {
                return null;
            }

            if (_rates != null && time.GetUtcNow() - _fetchedAt < CacheFor)
            {
                return _rates;
            }

            await Gate.WaitAsync(cancellationToken);
            try
            {
                if (_rates != null && time.GetUtcNow() - _fetchedAt < CacheFor)
                {
                    return _rates;
                }

                var xml = await http.GetStringAsync(Source, cancellationToken);
                var parsed = Parse(xml);
                if (parsed.Count > 0)
                {
                    _rates = parsed;
                    _fetchedAt = time.GetUtcNow();
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Xml.XmlException)
            {
                // Eski kur varsa o kullanılır; hiç yoksa karşılık gösterilmez. Bir saat sonra yeniden denenir.
                logger.LogWarning("TCMB kuru alınamadı: {Error}", ex.Message);
                _fetchedAt = time.GetUtcNow() - CacheFor + TimeSpan.FromHours(1);
            }
            finally
            {
                Gate.Release();
            }

            return _rates;
        }
    }
}
