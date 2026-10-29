using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using bitirme.Data;
using bitirme.Models;
using bitirme.Services.Localization;
using Microsoft.EntityFrameworkCore;

namespace bitirme.Services
{
    /// <summary>Kişinin verisine başkasının erişimi ya da hesabıyla ilgili bir yönetim işlemi (kim, ne, ne zaman).</summary>
    public sealed record DataAccessEntry(DateTime AtUtc, string Who, string What, DateTime? AppointmentUtc);

    /// <summary>
    /// "Verime kim erişti" (Faz 8, KVKK md. 11): kişinin sağlık verisine kendisi dışında erişenler
    /// (<c>HealthDataAccessLogs</c>) ve hesabıyla ilgili yönetici işlemleri (<c>AdminActionLogs</c>), en yeniden eskiye.
    /// Yöneticiler kişi adıyla değil "Moodica yönetimi" olarak gösterilir; kayıtların içeriği (gerekçe, dosya adı) gösterilmez.
    /// Uzmanın özel klinik notuna erişimler danışana gösterilmez: notun varlığı danışana açılmaz (DECISION-32; avukata
    /// soruldu, <c>HUKUK.md</c> H-14).
    /// </summary>
    public sealed class DataAccessReport
    {
        public const int MaxEntries = 200;
        public const string PlatformName = "Moodica yönetimi";

        private readonly AppDbContext _context;

        public DataAccessReport(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>En yeni <paramref name="limit"/> kayıt (sayfada 200; veri indirmede tamamı).</summary>
        public async Task<IReadOnlyList<DataAccessEntry>> ForUserAsync(int userId, int limit = MaxEntries)
        {
            var health = await _context.HealthDataAccessLogs
                .AsNoTracking()
                .Where(l => l.SubjectUserId == userId && l.ActorUserId != userId && l.Kind != HealthDataKind.PrivateSessionNote)
                .OrderByDescending(l => l.CreatedAt)
                .Take(limit)
                .Select(l => new
                {
                    l.CreatedAt,
                    l.Kind,
                    l.Action,
                    IsAdmin = l.ActorUser is Admin,
                    Title = l.ActorUser is Doctor ? ((Doctor)l.ActorUser).Title : null,
                    l.ActorUser!.FirstName,
                    l.ActorUser.LastName,
                    AppointmentDate = l.Appointment != null ? l.Appointment.AppointmentDate : (DateTime?)null
                })
                .ToListAsync();

            var admin = await _context.AdminActionLogs
                .AsNoTracking()
                .Where(l => l.TargetUserId == userId)
                .OrderByDescending(l => l.CreatedAt)
                .Take(limit)
                .Select(l => new { l.CreatedAt, l.Action })
                .ToListAsync();

            return health
                .Select(h => new DataAccessEntry(h.CreatedAt,
                    h.IsAdmin ? PlatformName : ActorName(h.Title, h.FirstName, h.LastName),
                    Describe(h.Kind, h.Action),
                    h.AppointmentDate))
                .Concat(admin.Select(a => new DataAccessEntry(a.CreatedAt, PlatformName, Describe(a.Action), null)))
                .OrderByDescending(e => e.AtUtc)
                .Take(limit)
                .ToList();
        }

        public static string ActorName(string? title, string? firstName, string? lastName) =>
            string.Join(" ", new[] { title, firstName, lastName }.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

        public static string Describe(HealthDataKind kind, HealthDataAccessAction action)
        {
            var data = kind switch
            {
                HealthDataKind.Complaint => SiteText.T("Randevu şikayetinizi"),
                HealthDataKind.SessionNote => "Seans notunuzu",
                HealthDataKind.NeedsAssessment => SiteText.T("İhtiyaç analizi anketinizi"),
                HealthDataKind.ProgressCheck => SiteText.T("İlerleme ölçümlerinizi"),
                _ => SiteText.T("Sağlık verinizi")
            };
            return action == HealthDataAccessAction.Written ? SiteText.T("{0} yazdı veya güncelledi", data) : SiteText.T("{0} görüntüledi", data);
        }

        public static string Describe(AdminActionType action) => action switch
        {
            AdminActionType.DoctorApproved => SiteText.T("Uzman başvurunuzu onayladı"),
            AdminActionType.DoctorRejected => SiteText.T("Uzman başvurunuzu reddetti"),
            AdminActionType.UserSuspended => SiteText.T("Hesabınızı askıya aldı"),
            AdminActionType.UserReactivated => SiteText.T("Hesabınızı yeniden açtı"),
            AdminActionType.DocumentViewed => SiteText.T("Mesleki belgenizi görüntüledi"),
            AdminActionType.FeedbackCommentViewed => SiteText.T("Seans geri bildirimi yorumunuzu görüntüledi"),
            AdminActionType.TwoFactorReset => SiteText.T("İki aşamalı doğrulama kurtarma talebinizi onayladı (2FA sıfırlandı)"),
            AdminActionType.TwoFactorResetRejected => SiteText.T("İki aşamalı doğrulama kurtarma talebinizi reddetti"),
            AdminActionType.NoShowRefunded => SiteText.T("\"Uzman gelmedi\" bildiriminizi inceleyip seans ücretinizi iade etti"),
            AdminActionType.RefundRetried => SiteText.T("Ödeme kuruluşunun reddettiği iadenizi yeniden denemeye aldı"),
            _ => SiteText.T("Hesabınızla ilgili bir işlem yaptı")
        };
    }
}
