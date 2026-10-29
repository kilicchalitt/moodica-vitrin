namespace bitirme.Services
{
    /// <summary>E-postanın vurgu tonu: başlığın üstündeki ince şerit ve başlık rengi.</summary>
    public enum EmailTone
    {
        Info,
        Success,
        Danger
    }

    /// <summary>
    /// Bütün e-postaların ortak görünümü (T2, DECISION-46): krem zemin, beyaz kart, mavi "Moodica" başlığı, serif başlık,
    /// tonuna göre renkli şerit, alt bilgide acil durum notu. Metinler o anki dildedir (alıcının dili, <c>RecipientScope</c>). E-posta programları dış stil ve betik yüklemediği için stiller
    /// satır içinde, düzen tablo ile; görsel ve dış kaynak yok (izleme pikseli de yok). <paramref name="bodyHtml"/> çağıranın
    /// kodladığı HTML'dir; kullanıcıdan gelen değerler çağıran tarafta HtmlEncode edilir.
    /// </summary>
    public static class EmailLayout
    {
        public const string Blue = "#1460B0";
        private const string Ink = "#1C2733";
        private const string Muted = "#4B5664";
        private const string Sand = "#F8F6EF";
        private const string Line = "#E3DED0";

        public static string ToneColor(EmailTone tone) => tone switch
        {
            EmailTone.Success => "#23704F",
            EmailTone.Danger => "#B3261E",
            _ => Blue
        };

        public static string Wrap(string title, string bodyHtml, EmailTone tone = EmailTone.Info)
        {
            var accent = ToneColor(tone);
            return
                $"<div style='margin:0; padding:24px 12px; background:{Sand};'>" +
                "<table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='max-width:560px; margin:0 auto; border-collapse:collapse;'>" +
                $"<tr><td style='padding:0 4px 16px; font-family:Georgia,\"Times New Roman\",serif; font-size:24px; color:{Blue};'>Moodica</td></tr>" +
                $"<tr><td style='background:#FFFFFF; border:1px solid {Line}; border-top:4px solid {accent}; border-radius:16px; padding:28px 28px 24px; " +
                $"font-family:\"Segoe UI\",Arial,Helvetica,sans-serif; font-size:15px; line-height:1.6; color:{Ink};'>" +
                $"<h1 style='margin:0 0 16px; font-family:Georgia,\"Times New Roman\",serif; font-weight:normal; font-size:24px; line-height:1.25; color:{accent};'>{title}</h1>" +
                bodyHtml +
                $"<p style='margin:24px 0 0; color:{Muted};'>{Localization.SiteText.T("Sağlıklı günler dileriz,<br>Moodica")}</p>" +
                "</td></tr>" +
                $"<tr><td style='padding:16px 4px 0; font-family:\"Segoe UI\",Arial,Helvetica,sans-serif; font-size:12px; line-height:1.5; color:{Muted};'>" +
                Localization.SiteText.T("Moodica acil durum hizmeti değildir. Kendinizi ya da başkasını tehlikede hissediyorsanız hemen 112'yi arayın.") +
                "</td></tr>" +
                "</table></div>";
        }

        /// <summary>Hap biçimli ana düğme (bağlantı). <paramref name="href"/> ve <paramref name="text"/> kodlanmış olmalı.</summary>
        public static string Button(string href, string text) =>
            $"<p style='margin:24px 0;'><a href='{href}' style='display:inline-block; background:{Blue}; color:#FFFFFF; padding:12px 24px; " +
            $"border-radius:999px; text-decoration:none; font-weight:bold;'>{text}</a></p>";

        /// <summary>Küçük, soluk açıklama paragrafı (kodlanmış HTML).</summary>
        public static string Note(string html) => $"<p style='margin:12px 0 0; font-size:13px; color:{Muted};'>{html}</p>";
    }
}
