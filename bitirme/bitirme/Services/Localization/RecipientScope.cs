using bitirme.Models;

namespace bitirme.Services.Localization
{
    /// <summary>
    /// Başka birine giden metin (e-posta, canlı bildirim) o kişinin dilinde ve saat diliminde hazırlansın diye kod bloğu
    /// süresince dili ve gösterim dilimini alıcınınkine çevirir (DECISION-49, I-8). Alıcı bilinmiyorsa Türkçe ve Türkiye saati.
    /// </summary>
    public static class RecipientScope
    {
        public static IDisposable Use(User? recipient) => Use(recipient?.Language, recipient?.TimeZoneId);

        public static IDisposable Use(string? language, string? timeZoneId) =>
            new Both(SiteCultures.Use(language), DisplayTimeZone.Use(timeZoneId));

        private sealed class Both(IDisposable culture, IDisposable zone) : IDisposable
        {
            public void Dispose()
            {
                zone.Dispose();
                culture.Dispose();
            }
        }
    }
}
