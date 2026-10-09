using bitirme.Data;
using bitirme.Models;
using Microsoft.EntityFrameworkCore;

namespace bitirme.Services
{
    /// <summary>
    /// Aydınlatma/rıza onaylarını ConsentRecords tablosuna yazar ve kullanıcının güncel sürümünü henüz
    /// onaylamadığı metinleri bulur (TASK-32).
    /// </summary>
    public class ConsentService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ConsentService> _logger;

        public ConsentService(AppDbContext context, ILogger<ConsentService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>Metinlerin güncel sürümlerinin onayını kaydeder.</summary>
        public async Task RecordAsync(int userId, IEnumerable<ConsentDocument> documents)
        {
            var records = documents
                .Distinct()
                .Select(document => new ConsentRecord
                {
                    UserId = userId,
                    Document = document,
                    Version = ConsentCatalog.CurrentVersion(document)
                })
                .ToList();

            _context.ConsentRecords.AddRange(records);
            await _context.SaveChangesAsync();

            foreach (var record in records)
            {
                _logger.LogInformation("Rıza onayı: kullanıcı {UserId}, {Document} sürüm {Version}", userId, record.Document, record.Version);
            }
        }

        /// <summary>Kullanıcının güncel sürümünü onaylamadığı zorunlu metinler.</summary>
        public async Task<IReadOnlyList<ConsentDocument>> GetMissingAsync(int userId, UserRole role)
        {
            var required = ConsentCatalog.RequiredFor(role);
            if (required.Count == 0)
            {
                return [];
            }

            var records = await _context.ConsentRecords
                .Where(c => c.UserId == userId && required.Contains(c.Document))
                .OrderBy(c => c.Id)
                .Select(c => new { c.Document, c.Version, c.Withdrawn })
                .ToListAsync();

            return ConsentCatalog.MissingFromRecords(role, records.Select(c => (c.Document, c.Version, c.Withdrawn)).ToList());
        }

        /// <summary>Açık rızanın geri alındığını kaydeder (yeni satır; önceki onaylar silinmez — DECISION-35).</summary>
        public async Task WithdrawAsync(int userId, ConsentDocument document)
        {
            if (!ConsentCatalog.IsWithdrawable(document))
            {
                throw new InvalidOperationException($"{document} geri alınabilen bir rıza değil.");
            }

            _context.ConsentRecords.Add(new ConsentRecord
            {
                UserId = userId,
                Document = document,
                Version = ConsentCatalog.CurrentVersion(document),
                Withdrawn = true
            });
            await _context.SaveChangesAsync();
            _logger.LogInformation("Rıza geri alındı: kullanıcı {UserId}, {Document}", userId, document);
        }

        /// <summary>Danışan sağlık verisi açık rızasını geri almış mı (en son kaydı geri alma mı)?</summary>
        public async Task<bool> HasWithdrawnHealthConsentAsync(int patientId) =>
            (await WithdrawnHealthConsentAmongAsync([patientId])).Count > 0;

        /// <summary>Verilen danışanlardan sağlık verisi açık rızasını geri almış olanlar.</summary>
        public async Task<IReadOnlySet<int>> WithdrawnHealthConsentAmongAsync(IEnumerable<int> patientIds)
        {
            var ids = patientIds.Distinct().ToList();
            var latest = await _context.ConsentRecords
                .Where(c => ids.Contains(c.UserId) && c.Document == ConsentDocument.HealthDataExplicitConsent)
                .GroupBy(c => c.UserId)
                .Select(g => g.OrderByDescending(c => c.Id).Select(c => new { c.UserId, c.Withdrawn }).First())
                .ToListAsync();
            return latest.Where(l => l.Withdrawn).Select(l => l.UserId).ToHashSet();
        }
    }
}
