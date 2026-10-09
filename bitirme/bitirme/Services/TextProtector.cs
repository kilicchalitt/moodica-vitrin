using System;
using Microsoft.AspNetCore.DataProtection;

namespace bitirme.Services
{
    /// <summary>
    /// Serbest metni Data Protection ile şifreler (<c>p1:</c> ön ekli; anket ve 2FA anahtarıyla aynı yöntem). Her kullanım kendi
    /// amacıyla (purpose) ayrılır; biri diğerinin şifresini çözemez. Düz metin kayıt okunmaz.
    /// </summary>
    public abstract class TextProtector
    {
        public const string ProtectedPrefix = "p1:";

        private readonly IDataProtector _protector;

        protected TextProtector(IDataProtectionProvider dataProtection, string purpose)
        {
            _protector = dataProtection.CreateProtector(purpose);
        }

        /// <summary>Boş metin saklanmaz (null döner).</summary>
        public string? Protect(string? text) =>
            string.IsNullOrWhiteSpace(text) ? null : ProtectedPrefix + _protector.Protect(text.Trim());

        public string? Unprotect(string? stored)
        {
            if (string.IsNullOrEmpty(stored))
            {
                return null;
            }

            if (!stored.StartsWith(ProtectedPrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Kayıt şifreli değil; düz metin sağlık verisi okunmaz.");
            }

            return _protector.Unprotect(stored[ProtectedPrefix.Length..]);
        }
    }

    /// <summary>
    /// Uzmanın özel seans notu (ISSUE-22). Özel not ilk günden şifreli saklanır; denetleyiciler yalnızca gerektiğinde çözer.
    /// </summary>
    public sealed class SessionNoteProtector : TextProtector
    {
        public const string ProtectorPurpose = "Moodica.SessionPrivateNote.v1";

        public SessionNoteProtector(IDataProtectionProvider dataProtection) : base(dataProtection, ProtectorPurpose)
        {
        }
    }

    /// <summary>
    /// Randevudaki serbest metinler: danışanın şikayeti, danışana görünen seans notu ve iptal gerekçesi (ISSUE-22, Faz 8).
    /// Veritabanına yazılırken şifrelenir, okunurken çözülür (<c>AppointmentTextEncryptionInterceptor</c>).
    /// </summary>
    public sealed class AppointmentTextProtector : TextProtector
    {
        public const string ProtectorPurpose = "Moodica.AppointmentText.v1";

        public AppointmentTextProtector(IDataProtectionProvider dataProtection) : base(dataProtection, ProtectorPurpose)
        {
        }
    }

    /// <summary>Danışanın seans sonrası geri bildirim yorumu (DECISION-33); sağlık bilgisi içerebileceği için şifreli.</summary>
    public sealed class FeedbackCommentProtector : TextProtector
    {
        public const string ProtectorPurpose = "Moodica.SessionFeedbackComment.v1";

        public FeedbackCommentProtector(IDataProtectionProvider dataProtection) : base(dataProtection, ProtectorPurpose)
        {
        }
    }
}
