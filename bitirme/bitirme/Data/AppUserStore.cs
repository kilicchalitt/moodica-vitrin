using bitirme.Models;
using bitirme.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace bitirme.Data
{
    /// <summary>
    /// Identity kullanıcı deposu; 2FA sırlarını veritabanında açık saklamaz (ISSUE-31):
    /// authenticator anahtarı Data Protection ile şifrelenir, kurtarma kodlarının yalnızca özeti tutulur.
    /// Bu sürümden önce yazılmış düz kayıtlar okunabilir ve açılışta <see cref="ProtectLegacyTwoFactorDataAsync"/> ile dönüştürülür.
    /// </summary>
    public class AppUserStore : UserStore<User, IdentityRole<int>, AppDbContext, int>
    {
        // Identity'nin UserStoreBase içinde kullandığı sabitlerle aynı olmalıdır.
        public const string InternalLoginProvider = "[AspNetUserStore]";
        public const string AuthenticatorKeyTokenName = "AuthenticatorKey";
        public const string RecoveryCodeTokenName = "RecoveryCodes";
        public const string ProtectedPrefix = "p1:";
        public const string ProtectorPurpose = "Moodica.Identity.AuthenticatorKey.v1";

        private readonly IDataProtector _protector;

        public AppUserStore(AppDbContext context, IDataProtectionProvider dataProtection, IdentityErrorDescriber? describer = null)
            : base(context, describer)
        {
            _protector = dataProtection.CreateProtector(ProtectorPurpose);
        }

        public override Task SetAuthenticatorKeyAsync(User user, string key, CancellationToken cancellationToken) =>
            SetTokenAsync(user, InternalLoginProvider, AuthenticatorKeyTokenName, ProtectedPrefix + _protector.Protect(key), cancellationToken);

        public override async Task<string?> GetAuthenticatorKeyAsync(User user, CancellationToken cancellationToken)
        {
            var stored = await GetTokenAsync(user, InternalLoginProvider, AuthenticatorKeyTokenName, cancellationToken);
            return stored is not null && stored.StartsWith(ProtectedPrefix, StringComparison.Ordinal)
                ? _protector.Unprotect(stored[ProtectedPrefix.Length..])
                : stored;
        }

        public override Task ReplaceCodesAsync(User user, IEnumerable<string> recoveryCodes, CancellationToken cancellationToken) =>
            SetTokenAsync(user, InternalLoginProvider, RecoveryCodeTokenName,
                string.Join(";", recoveryCodes.Select(RecoveryCodeHasher.Hash)), cancellationToken);

        public override async Task<bool> RedeemCodeAsync(User user, string code, CancellationToken cancellationToken)
        {
            var stored = await GetTokenAsync(user, InternalLoginProvider, RecoveryCodeTokenName, cancellationToken) ?? string.Empty;
            var codes = stored.Split(';', StringSplitOptions.RemoveEmptyEntries);
            var match = codes.FirstOrDefault(c => RecoveryCodeHasher.Matches(c, code));
            if (match == null)
            {
                return false;
            }

            await SetTokenAsync(user, InternalLoginProvider, RecoveryCodeTokenName,
                string.Join(";", codes.Where(c => c != match)), cancellationToken);
            return true;
        }

        /// <summary>
        /// Açılışta bir kez: düz saklanmış authenticator anahtarlarını şifreler, düz kurtarma kodlarını özetler.
        /// Değiştirilen kayıt sayısını döner.
        /// </summary>
        public static async Task<int> ProtectLegacyTwoFactorDataAsync(AppDbContext context, IDataProtectionProvider dataProtection)
        {
            var protector = dataProtection.CreateProtector(ProtectorPurpose);
            var tokens = await context.UserTokens
                .Where(t => t.LoginProvider == InternalLoginProvider &&
                            (t.Name == AuthenticatorKeyTokenName || t.Name == RecoveryCodeTokenName))
                .ToListAsync();

            var changed = 0;
            foreach (var token in tokens.Where(t => !string.IsNullOrEmpty(t.Value)))
            {
                if (token.Name == AuthenticatorKeyTokenName && !token.Value!.StartsWith(ProtectedPrefix, StringComparison.Ordinal))
                {
                    token.Value = ProtectedPrefix + protector.Protect(token.Value);
                    changed++;
                }
                else if (token.Name == RecoveryCodeTokenName && token.Value!.Split(';').Any(c => c.Length > 0 && !RecoveryCodeHasher.IsHashed(c)))
                {
                    token.Value = string.Join(";", token.Value.Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(c => RecoveryCodeHasher.IsHashed(c) ? c : RecoveryCodeHasher.Hash(c)));
                    changed++;
                }
            }

            if (changed > 0)
            {
                await context.SaveChangesAsync();
            }

            return changed;
        }
    }
}
