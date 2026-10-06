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

            var accepted = await _context.ConsentRecords
                .Where(c => c.UserId == userId && required.Contains(c.Document))
                .Select(c => new { c.Document, c.Version })
                .Distinct()
                .ToListAsync();

            return ConsentCatalog.Missing(role, accepted.Select(c => (c.Document, c.Version)));
        }
    }
}
