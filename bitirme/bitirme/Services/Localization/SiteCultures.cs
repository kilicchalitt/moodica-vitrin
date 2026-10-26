using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace bitirme.Services.Localization
{
    /// <summary>
    /// Sitenin dilleri (DECISION-49): Türkçe (varsayılan) ve İngilizce. Seçim dil çerezinde tutulur; oturum açmış kullanıcının
    /// tercihi hesabında da saklanır (<see cref="Models.User.Language"/>) ve e-postalar o dilde gönderilir.
    /// İngilizce biçimler (tarih, sayı) en-GB'dir.
    /// </summary>
    public static class SiteCultures
    {
        public const string Turkish = "tr";
        public const string English = "en";

        public static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");
        public static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-GB");

        public static readonly CultureInfo[] Supported = [TurkishCulture, EnglishCulture];

        /// <summary>Dil kodu ("tr"/"en"); bilinmeyen değer Türkçe sayılır.</summary>
        public static string Normalize(string? language) =>
            string.Equals(language, English, StringComparison.OrdinalIgnoreCase) ? English : Turkish;

        public static CultureInfo CultureFor(string? language) =>
            Normalize(language) == English ? EnglishCulture : TurkishCulture;

        /// <summary>Şu anki istek dili ("tr"/"en").</summary>
        public static string Current => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == English ? English : Turkish;

        public static bool IsEnglish => Current == English;

        /// <summary>Biçimlendirme kültürü: İngilizcede en-GB, aksi hâlde (istek dışı iş parçacıkları dahil) tr-TR.</summary>
        public static CultureInfo CurrentCulture => IsEnglish ? EnglishCulture : TurkishCulture;

        /// <summary>Dil çerezinin değeri (ASP.NET Core istek dili çerezi biçimi).</summary>
        public static string CookieValue(string language)
        {
            var culture = CultureFor(language);
            return CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture, culture));
        }

        /// <summary>Kod bloğu süresince dili değiştirir (ör. alıcının dilinde e-posta hazırlamak için).</summary>
        public static IDisposable Use(string? language) => new CultureScope(CultureFor(language));

        private sealed class CultureScope : IDisposable
        {
            private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
            private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

            public CultureScope(CultureInfo culture)
            {
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
            }

            public void Dispose()
            {
                CultureInfo.CurrentCulture = _culture;
                CultureInfo.CurrentUICulture = _uiCulture;
            }
        }
    }
}
