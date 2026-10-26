using System.Collections.Frozen;
using System.Text.Json;
using Microsoft.Extensions.Localization;

namespace bitirme.Services.Localization
{
    /// <summary>
    /// Çeviri sözlüğü (DECISION-49): anahtar sitedeki Türkçe metnin kendisidir, İngilizcesi <c>Resources/en.json</c>'dadır.
    /// Türkçe istekte metin olduğu gibi döner; İngilizcede karşılığı yoksa yine Türkçesi döner (sayfa bozulmaz, eksik
    /// çeviri <c>LocalizationCoverageTests</c> ile yakalanır). Kaynak türünden bağımsız tek sözlük kullanılır.
    /// </summary>
    public sealed class JsonStringLocalizerFactory : IStringLocalizerFactory
    {
        private readonly JsonStringLocalizer _localizer;

        public JsonStringLocalizerFactory(IWebHostEnvironment environment)
        {
            _localizer = new JsonStringLocalizer(Translations.Load(environment.ContentRootPath));
        }

        public IStringLocalizer Create(Type resourceSource) => _localizer;

        public IStringLocalizer Create(string baseName, string location) => _localizer;
    }

    public sealed class JsonStringLocalizer(Translations translations) : IStringLocalizer
    {
        public LocalizedString this[string name] => Get(name, null);

        public LocalizedString this[string name, params object[] arguments] => Get(name, arguments);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            translations.English.Select(pair => new LocalizedString(pair.Key, pair.Value));

        private LocalizedString Get(string name, object[]? arguments)
        {
            var found = true;
            var text = name;
            if (SiteCultures.IsEnglish)
            {
                found = translations.English.TryGetValue(name, out var english);
                text = english ?? name;
            }

            if (arguments is { Length: > 0 })
            {
                text = string.Format(System.Globalization.CultureInfo.CurrentCulture, text, arguments);
            }

            return new LocalizedString(name, text, resourceNotFound: !found);
        }
    }

    /// <summary>İngilizce karşılıklar; uygulama boyunca bir kez okunur.</summary>
    public sealed class Translations
    {
        public FrozenDictionary<string, string> English { get; }

        public Translations(IDictionary<string, string> english)
        {
            English = english.ToFrozenDictionary(StringComparer.Ordinal);
        }

        public static Translations Load(string contentRoot)
        {
            var path = Path.Combine(contentRoot, "Resources", "en.json");
            using var stream = File.OpenRead(path);
            var english = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
                ?? throw new InvalidOperationException("Resources/en.json okunamadı.");
            return new Translations(english);
        }
    }
}
