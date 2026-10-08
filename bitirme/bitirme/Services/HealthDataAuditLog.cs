using bitirme.Data;
using bitirme.Models;
using Microsoft.EntityFrameworkCore;

namespace bitirme.Services
{
    /// <summary>Kaydedilecek tek bir sağlık verisi erişimi.</summary>
    public readonly record struct HealthDataAccess(int SubjectUserId, int? AppointmentId, HealthDataKind Kind, HealthDataAccessAction Action);

    /// <summary>
    /// Hangi erişimlerin kaydedileceğine karar verir. Veritabanından bağımsızdır ve birim testlerle doğrulanır.
    /// </summary>
    public static class HealthDataAuditRules
    {
        /// <summary>
        /// Aynı kişinin aynı veriyi bu süre içinde yeniden görüntülemesi tekrar kaydedilmez; panel SignalR ile sık
        /// yenilendiği için her yenilemede satır yazılması kaydı okunmaz hâle getirirdi. Yazma her zaman kaydedilir.
        /// </summary>
        public static readonly TimeSpan ViewDeduplicationWindow = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Kaydedilecek erişimler: kişinin kendi verisine erişimi kaydedilmez; görüntülemeler pencere içinde zaten
        /// kaydedilmişse ve aynı istekte tekrar ediyorsa atlanır.
        /// </summary>
        public static IReadOnlyList<HealthDataAccess> SelectToRecord(
            int actorUserId,
            IEnumerable<HealthDataAccess> accesses,
            IReadOnlySet<HealthDataAccess> recentlyRecordedViews)
        {
            var result = new List<HealthDataAccess>();
            var seenViews = new HashSet<HealthDataAccess>();

            foreach (var access in accesses)
            {
                if (access.SubjectUserId == actorUserId)
                {
                    continue;
                }

                if (access.Action == HealthDataAccessAction.Viewed &&
                    (recentlyRecordedViews.Contains(access) || !seenViews.Add(access)))
                {
                    continue;
                }

                result.Add(access);
            }

            return result;
        }
    }

    /// <summary>
    /// Sağlık verisi erişimlerini HealthDataAccessLogs tablosuna yazar (TASK-32). Kayıtta verinin içeriği değil,
    /// yalnızca kimin kimin hangi verisine eriştiği tutulur.
    /// </summary>
    public class HealthDataAuditLog
    {
        private readonly AppDbContext _context;

        private readonly TimeProvider _time;

        public HealthDataAuditLog(AppDbContext context, TimeProvider time)
        {
            _time = time;
            _context = context;
        }

        public Task RecordAsync(int actorUserId, HealthDataAccess access) => RecordAsync(actorUserId, [access]);

        public async Task RecordAsync(int actorUserId, IReadOnlyCollection<HealthDataAccess> accesses)
        {
            if (accesses.Count == 0)
            {
                return;
            }

            var now = _time.UtcNow();
            var since = now - HealthDataAuditRules.ViewDeduplicationWindow;
            var subjectIds = accesses.Select(a => a.SubjectUserId).Distinct().ToList();
            var recentViews = await _context.HealthDataAccessLogs
                .Where(l => l.ActorUserId == actorUserId && l.Action == HealthDataAccessAction.Viewed &&
                            l.CreatedAt >= since && subjectIds.Contains(l.SubjectUserId))
                .Select(l => new { l.SubjectUserId, l.AppointmentId, l.Kind, l.Action })
                .ToListAsync();

            var recentSet = recentViews
                .Select(l => new HealthDataAccess(l.SubjectUserId, l.AppointmentId, l.Kind, l.Action))
                .ToHashSet();

            var toRecord = HealthDataAuditRules.SelectToRecord(actorUserId, accesses, recentSet);
            if (toRecord.Count == 0)
            {
                return;
            }

            _context.HealthDataAccessLogs.AddRange(toRecord.Select(a => new HealthDataAccessLog
            {
                ActorUserId = actorUserId,
                SubjectUserId = a.SubjectUserId,
                AppointmentId = a.AppointmentId,
                Kind = a.Kind,
                Action = a.Action,
                CreatedAt = now
            }));
            await _context.SaveChangesAsync();
        }
    }
}
