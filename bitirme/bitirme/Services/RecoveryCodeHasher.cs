using System.Security.Cryptography;
using System.Text;

namespace bitirme.Services
{
    /// <summary>
    /// 2FA kurtarma kodlarının veritabanında saklanan özeti (ISSUE-31). Kodlar rastgele ve tek kullanımlık olduğundan
    /// tuzsuz SHA-256 yeterlidir; veritabanı sızsa bile kodun kendisi okunamaz.
    /// </summary>
    public static class RecoveryCodeHasher
    {
        public const string Prefix = "h1:";

        public static string Hash(string code) =>
            Prefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim().ToUpperInvariant())));

        public static bool IsHashed(string stored) => stored.StartsWith(Prefix, StringComparison.Ordinal);

        /// <summary>Saklanan değer (özet veya eski düz kod) girilen koda karşılık geliyor mu?</summary>
        public static bool Matches(string stored, string code) =>
            IsHashed(stored)
                ? CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(stored), Encoding.ASCII.GetBytes(Hash(code)))
                : string.Equals(stored, code.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
