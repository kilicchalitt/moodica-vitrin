using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using bitirme.Models;
using bitirme.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace bitirme.Data
{
    /// <summary>
    /// Randevudaki serbest sağlık metinlerini (şikayet, danışana görünen not, iptal gerekçesi) veritabanına şifreli yazar ve
    /// okurken çözer (ISSUE-22, Faz 8). Uygulama kodu varlıkta hep düz metinle çalışır; şifreli değer yalnızca kaydetme
    /// sırasında veritabanına gider, kayıttan sonra varlığa yeniden düz metin konur. Varlık yerine alan seçen sorgular
    /// (<c>Select(a =&gt; a.Note)</c>) ve toplu güncellemeler (<c>ExecuteUpdate</c>) bu sınıftan geçmez: şifreli değeri döner/yazmaz.
    /// </summary>
    public sealed class AppointmentTextEncryptionInterceptor : SaveChangesInterceptor, IMaterializationInterceptor
    {
        /// <summary>Şifrelenen randevu alanları.</summary>
        public static readonly IReadOnlyList<string> ProtectedProperties =
        [
            nameof(Appointment.PatientComplaint),
            nameof(Appointment.Note),
            nameof(Appointment.CancellationReason)
        ];

        private readonly AppointmentTextProtector _protector;

        // Kaydetme sırasında şifreli değerle değiştirilen alanların düz metni; kayıttan (ya da hatadan) sonra geri konur.
        private readonly ConditionalWeakTable<DbContext, List<(PropertyEntry Property, string? Plain)>> _pending = new();

        public AppointmentTextEncryptionInterceptor(AppointmentTextProtector protector)
        {
            _protector = protector;
        }

        /// <summary>
        /// Şifreleme öncesinden kalan düz metin randevu alanlarını bir kez şifreler (açılışta çalışır); şifrelenen randevu sayısını döner.
        /// </summary>
        public static async Task<int> ProtectLegacyAsync(AppDbContext context, AppointmentTextProtector protector)
        {
            const string prefix = TextProtector.ProtectedPrefix;
            var legacy = await context.Appointments
                .Where(a => (a.PatientComplaint != null && !a.PatientComplaint.StartsWith(prefix))
                    || (a.Note != null && !a.Note.StartsWith(prefix))
                    || (a.CancellationReason != null && !a.CancellationReason.StartsWith(prefix)))
                .Select(a => new { a.Id, a.PatientComplaint, a.Note, a.CancellationReason })
                .ToListAsync();

            string? Protect(string? stored) =>
                stored == null || stored.StartsWith(prefix, StringComparison.Ordinal) ? stored : protector.Protect(stored);

            foreach (var row in legacy)
            {
                var complaint = Protect(row.PatientComplaint);
                var note = Protect(row.Note);
                var reason = Protect(row.CancellationReason);
                await context.Appointments
                    .Where(a => a.Id == row.Id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(a => a.PatientComplaint, complaint)
                        .SetProperty(a => a.Note, note)
                        .SetProperty(a => a.CancellationReason, reason));
            }

            return legacy.Count;
        }

        public object InitializedInstance(MaterializationInterceptionData materializationData, object entity)
        {
            if (entity is Appointment appointment)
            {
                appointment.PatientComplaint = _protector.Unprotect(appointment.PatientComplaint);
                appointment.Note = _protector.Unprotect(appointment.Note);
                appointment.CancellationReason = _protector.Unprotect(appointment.CancellationReason);
            }

            return entity;
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Encrypt(eventData.Context);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Encrypt(eventData.Context);
            return ValueTask.FromResult(result);
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            Restore(eventData.Context, accepted: true);
            return result;
        }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            Restore(eventData.Context, accepted: true);
            return ValueTask.FromResult(result);
        }

        public override void SaveChangesFailed(DbContextErrorEventData eventData) => Restore(eventData.Context, accepted: false);

        public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            Restore(eventData.Context, accepted: false);
            return Task.CompletedTask;
        }

        private void Encrypt(DbContext? context)
        {
            if (context == null)
            {
                return;
            }

            // Kaydetme değişiklikleri bu çağrıdan sonra algılar; hangi alanın yazılacağını bilmek için önce burada algılanır.
            context.ChangeTracker.DetectChanges();

            var pending = new List<(PropertyEntry, string?)>();
            foreach (var entry in context.ChangeTracker.Entries<Appointment>())
            {
                if (entry.State is not (EntityState.Added or EntityState.Modified))
                {
                    continue;
                }

                foreach (var name in ProtectedProperties)
                {
                    var property = entry.Property(name);
                    if (entry.State == EntityState.Modified && !property.IsModified)
                    {
                        continue;
                    }

                    var plain = (string?)property.CurrentValue;
                    pending.Add((property, plain));
                    property.CurrentValue = _protector.Protect(plain);
                }
            }

            _pending.AddOrUpdate(context, pending);
        }

        private void Restore(DbContext? context, bool accepted)
        {
            if (context == null || !_pending.TryGetValue(context, out var pending))
            {
                return;
            }

            _pending.Remove(context);
            foreach (var (property, plain) in pending)
            {
                if (property.EntityEntry.State == EntityState.Detached)
                {
                    continue;
                }

                property.CurrentValue = plain;
                if (accepted)
                {
                    // Kayıt kabul edildi: veritabanındaki değer bu düz metnin şifrelisi, alan değişmemiş sayılır.
                    property.OriginalValue = plain;
                    property.IsModified = false;
                }
            }
        }
    }
}
