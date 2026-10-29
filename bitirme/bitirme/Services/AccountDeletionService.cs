using System;
using System.Net;
using System.Threading.Tasks;
using bitirme.Models;
using bitirme.Services.Localization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace bitirme.Services
{
    /// <summary>
    /// Kullanıcının kendi hesabını silmesi (Faz 8, DECISION-35: "kimlik silinsin, kayıtlar saklansın"). Yaklaşan randevular
    /// iptal edilir, kimlik bilgileri (ad, e-posta, telefon, doğum tarihi, uzman profili ve fotoğrafı) anonimleştirilir, şifre ve
    /// 2FA kaldırılır, hesap süresiz kilitlenir. Randevu, sağlık, rıza ve erişim kayıtları saklama süresince şifreli kalır
    /// (süre avukatla, <c>HUKUK.md</c> H-13). Uzmanın mesleki belgeleri doğrulama kaydı olarak saklanır (H-13'te sorulur).
    /// Yönetici hesapları bu yolla silinmez.
    /// </summary>
    public sealed class AccountDeletionService
    {
        public const string DeletedFirstName = "Silinmiş";
        public const string DeletedLastName = "Kullanıcı";
        public const string PatientDeletedReason = "Danışan hesabını sildiği için randevu iptal edildi.";

        private readonly UserManager<User> _userManager;
        private readonly AppointmentStatusService _status;
        private readonly IEmailService _email;
        private readonly IFileStorage _storage;
        private readonly TimeProvider _time;
        private readonly ILogger<AccountDeletionService> _logger;

        public AccountDeletionService(UserManager<User> userManager, AppointmentStatusService status,
            IEmailService email, IFileStorage storage, TimeProvider time, ILogger<AccountDeletionService> logger)
        {
            _userManager = userManager;
            _status = status;
            _email = email;
            _storage = storage;
            _time = time;
            _logger = logger;
        }

        /// <summary>Silinmiş hesabın e-posta/kullanıcı adı: benzersiz, gönderilmeyen ayrılmış alan adında (.invalid).</summary>
        public static string DeletedEmail(int userId) => $"silinmis-{userId}@silinmis.invalid";

        public static string? GetBlocker(User user) => user switch
        {
            Admin => SiteText.T("Yönetici hesapları bu sayfadan silinmez."),
            { DeletedAt: not null } => SiteText.T("Hesap zaten silinmiş."),
            _ => null
        };

        public async Task DeleteAsync(User user)
        {
            if (GetBlocker(user) is { } blocker)
            {
                throw new InvalidOperationException(blocker);
            }

            // Önce randevular: iptal e-postaları gerçek ad ve adreslerle gider.
            var cancelled = user is Doctor
                ? await _status.CancelUpcomingForInactiveExpertAsync(user.Id)
                : await _status.CancelUpcomingForPatientAsync(user.Id, PatientDeletedReason);

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                await _email.SendEmailAsync(user.Email, "Moodica hesabınız silindi",
                    EmailLayout.Wrap($"Merhaba {WebUtility.HtmlEncode(user.FirstName)},",
                    "<p style='margin:0 0 12px;'>Talebiniz üzerine Moodica hesabınız silindi; ad, e-posta ve telefon bilgileriniz kaldırıldı ve artık giriş yapılamaz. " +
                    "Randevu ve sağlık kayıtlarınız yasal saklama süresi boyunca şifreli olarak tutulur, süre dolunca silinir.</p>" +
                    // İade e-postası işçi çalıştığında adres anonimleştiği için gidemez; iade bilgisi burada verilir.
                    (user is Patient && cancelled > 0
                        ? $"<p style='margin:0 0 12px;'>Yaklaşan {cancelled} randevunuz iptal edildi; ödemesi alınmış randevuların ücretinin tamamı, ödeme yaptığınız karta birkaç iş günü içinde iade edilir.</p>"
                        : string.Empty) +
                    EmailLayout.Note("Bu işlemi siz yapmadıysanız bu e-postayı yanıtlayarak bize bildirin.")));
            }

            var photo = (user as Doctor)?.PhotoFileName;

            user.FirstName = DeletedFirstName;
            user.LastName = DeletedLastName;
            user.UserName = DeletedEmail(user.Id);
            user.Email = DeletedEmail(user.Id);
            user.EmailConfirmed = false;
            user.PhoneNumber = null;
            user.PhoneNumberConfirmed = false;
            user.PendingPhoneNumber = null;
            user.TwoFactorEnabled = false;
            user.LockoutEnabled = true;
            user.LockoutEnd = AccountStatus.SuspendedUntil;
            user.DeletedAt = _time.UtcNow();
            switch (user)
            {
                case Patient patient:
                    patient.DateOfBirth = null;
                    break;
                case Doctor doctor:
                    doctor.Title = string.Empty;
                    doctor.Expertise = string.Empty;
                    doctor.Biography = string.Empty;
                    doctor.DiplomaNo = string.Empty;
                    doctor.Areas = [];
                    doctor.Methods = [];
                    doctor.AcceptsNewClients = false;
                    doctor.PhotoFileName = null;
                    break;
            }

            var updated = await _userManager.UpdateAsync(user); // normalleştirilmiş ad/e-posta ve güvenlik damgası da yenilenir
            if (!updated.Succeeded)
            {
                throw new InvalidOperationException("Hesap silinemedi: " + string.Join(" ", updated.Errors));
            }

            await _userManager.RemovePasswordAsync(user);
            await _userManager.RemoveAuthenticationTokenAsync(user, "[AspNetUserStore]", "AuthenticatorKey");
            await _userManager.RemoveAuthenticationTokenAsync(user, "[AspNetUserStore]", "RecoveryCodes");
            await _userManager.UpdateSecurityStampAsync(user);

            if (photo != null)
            {
                _storage.Delete(Doctor.PhotoFolder, photo);
            }

            _logger.LogInformation("Hesap silindi: kullanıcı {UserId}, iptal edilen randevu {Count}", user.Id, cancelled);
        }
    }
}
