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
            var translations = Translations.Load(environment.ContentRootPath);
            SiteText.Use(translations);
            _localizer = new JsonStringLocalizer(translations);
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

    /// <summary>
    /// Kod içinden çeviri (denetleyici mesajları, servis hata metinleri, e-postalar): <c>SiteText.T("Türkçe metin")</c>.
    /// O anki dil İngilizceyse karşılığı, değilse Türkçesi döner; uygulama başlamadan (birim testleri) Türkçe döner.
    /// </summary>
    public static class SiteText
    {
        private static Translations? _translations;

        internal static void Use(Translations translations) => _translations = translations;

        public static string T(string turkish, params object[] arguments)
        {
            var text = SiteCultures.IsEnglish && _translations?.English.TryGetValue(turkish, out var english) == true ? english! : turkish;
            return arguments.Length == 0 ? text : string.Format(SiteCultures.CurrentCulture, text, arguments);
        }

        /// <summary>
        /// Çevrilmeden taşınan metin anahtarı: başka birine giden canlı bildirim gibi, çevirinin alıcının dilinde sonra
        /// yapılacağı yerlerde kullanılır. Metni olduğu gibi döner; <c>LocalizationCoverageTests</c> karşılığını yine denetler.
        /// </summary>
        public static string Key(string turkish) => turkish;
    }
}
