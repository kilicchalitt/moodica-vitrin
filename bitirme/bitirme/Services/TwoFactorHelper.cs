using System.Text;
using QRCoder;

namespace bitirme.Services
{
    /// <summary>
    /// Kimlik doğrulayıcı uygulamaları (Google/Microsoft Authenticator vb.) için yardımcılar.
    /// QR kod sunucuda üretilir; gizli anahtar hiçbir dış servise gönderilmez.
    /// </summary>
    public static class TwoFactorHelper
    {
        public const string Issuer = "Moodica";

        /// <summary>Anahtarı elle girilebilmesi için 4'lü gruplar halinde, küçük harfle gösterir.</summary>
        public static string FormatKey(string key)
        {
            var result = new StringBuilder();
            for (var i = 0; i < key.Length; i += 4)
            {
                if (result.Length > 0)
                {
                    result.Append(' ');
                }

                result.Append(key.AsSpan(i, Math.Min(4, key.Length - i)));
            }

            return result.ToString().ToLowerInvariant();
        }

        /// <summary>Uygulamaların QR koddan okuduğu otpauth bağlantısı (RFC 6238 / Key Uri Format).</summary>
        public static string BuildAuthenticatorUri(string accountName, string key) =>
            $"otpauth://totp/{Uri.EscapeDataString(Issuer)}:{Uri.EscapeDataString(accountName)}" +
            $"?secret={key}&issuer={Uri.EscapeDataString(Issuer)}&digits=6";

        /// <summary>Kullanıcının yazdığı koddan boşluk ve tireleri atar ("123 456" → "123456").</summary>
        public static string NormalizeCode(string? code) =>
            new string((code ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());

        public static string QrCodeDataUri(string text)
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
            var png = new PngByteQRCode(data).GetGraphic(5);
            return "data:image/png;base64," + Convert.ToBase64String(png);
        }
    }
}
