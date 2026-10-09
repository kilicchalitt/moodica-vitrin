using bitirme.Models;

namespace bitirme.Services
{
    /// <summary>
    /// Aydınlatma ve açık rıza metinlerinin güncel sürümleri ve hangi kullanıcı türünün hangilerini onaylaması
    /// gerektiği. Metin değiştirildiğinde sürümü burada artırılır; kullanıcılar bir sonraki isteklerinde yeni sürümü
    /// onaylamaya yönlendirilir. Metinler taslaktır, avukat onayı Faz 8'de (TASK-32).
    /// </summary>
    public static class ConsentCatalog
    {
        public const int MaxVersionLength = 20;

        private static readonly IReadOnlyDictionary<ConsentDocument, string> CurrentVersions = new Dictionary<ConsentDocument, string>
        {
            // 2026-10-07: aydınlatma metinlerine görüntülü görüşme bağlantı (STUN/TURN) sağlayıcısına aktarım eklendi.
            // 2026-10-07b: seans geri bildirimi (veri türü ve hizmet kalitesi amacı, DECISION-33) eklendi.
            // 2026-10-08: danışan metinlerine isteğe bağlı ilerleme ölçümleri (PHQ-9/GAD-7 tekrarı, DECISION-33) eklendi.
            [ConsentDocument.PatientPrivacyNotice] = "2026-10-08-taslak",
            [ConsentDocument.HealthDataExplicitConsent] = "2026-10-08-taslak",
            // 2026-10-08: uzman metnine danışanın ihtiyaç analizi özeti ve ilerleme ölçümleri eklendi.
            [ConsentDocument.ExpertPrivacyNotice] = "2026-10-08-taslak"
        };

        private static readonly ConsentDocument[] PatientDocuments =
            [ConsentDocument.PatientPrivacyNotice, ConsentDocument.HealthDataExplicitConsent];

        private static readonly ConsentDocument[] ExpertDocuments =
            [ConsentDocument.ExpertPrivacyNotice];

        public static string CurrentVersion(ConsentDocument document) => CurrentVersions[document];

        /// <summary>Kullanıcı türünün onaylaması gereken metinler. Yönetici personeldir, bu akışa girmez.</summary>
        public static IReadOnlyList<ConsentDocument> RequiredFor(UserRole role) => role switch
        {
            UserRole.Patient => PatientDocuments,
            UserRole.Doctor => ExpertDocuments,
            _ => []
        };

        /// <summary>Identity rol adından rıza akışına giren kullanıcı türü; yönetici ve bilinmeyen roller için null.</summary>
        public static UserRole? RoleFromName(string? roleName) => roleName switch
        {
            AppRoles.Patient => UserRole.Patient,
            AppRoles.Doctor => UserRole.Doctor,
            _ => null
        };

        /// <summary>Kullanıcı türü için güncel sürümü henüz onaylanmamış metinler.</summary>
        public static IReadOnlyList<ConsentDocument> Missing(UserRole role, IEnumerable<(ConsentDocument Document, string Version)> accepted)
        {
            var acceptedSet = accepted.ToHashSet();
            return RequiredFor(role)
                .Where(document => !acceptedSet.Contains((document, CurrentVersion(document))))
                .ToList();
        }

        /// <summary>Geri alınabilen metin: yalnızca sağlık verisi açık rızası (aydınlatma metinleri bilgilendirmedir; DECISION-35).</summary>
        public static bool IsWithdrawable(ConsentDocument document) => document == ConsentDocument.HealthDataExplicitConsent;

        /// <summary>
        /// Kayıtlardan (eskiden yeniye; onaylar ve geri almalar) güncel sürümü onaylanmamış metinler. En son kaydı geri alma olan
        /// açık rıza eksik sayılmaz: kişi bilerek geri aldı, onay ekranına zorlanmaz; sağlık verisi işleyen işlemler kapalı kalır.
        /// </summary>
        public static IReadOnlyList<ConsentDocument> MissingFromRecords(UserRole role,
            IReadOnlyCollection<(ConsentDocument Document, string Version, bool Withdrawn)> recordsOldestFirst)
        {
            var accepted = recordsOldestFirst.Where(r => !r.Withdrawn).Select(r => (r.Document, r.Version));
            return Missing(role, accepted)
                .Where(document => !IsWithdrawn(recordsOldestFirst, document))
                .ToList();
        }

        /// <summary>Metnin en son kaydı geri alma mı?</summary>
        public static bool IsWithdrawn(IEnumerable<(ConsentDocument Document, string Version, bool Withdrawn)> recordsOldestFirst,
            ConsentDocument document) =>
            IsWithdrawable(document) && recordsOldestFirst.LastOrDefault(r => r.Document == document).Withdrawn;

        public static string Title(ConsentDocument document) => document switch
        {
            ConsentDocument.PatientPrivacyNotice => "Danışan Aydınlatma Metni",
            ConsentDocument.HealthDataExplicitConsent => "Sağlık Verileri Açık Rıza Metni",
            ConsentDocument.ExpertPrivacyNotice => "Uzman Aydınlatma Metni",
            _ => document.ToString()
        };
    }
}
