using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace BankingSystemCore.Security.Tokens;

/// <summary>
/// Random, single-purpose secrets such as refresh tokens and e-mail verification tokens.
/// Only the hash is stored, so a database leak does not leak usable tokens.
/// </summary>
public static class SecureToken
{
    /// <summary>A URL-safe random token with 256 bits of randomness.</summary>
    public static string Create() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>SHA-256 as hex. Enough for high-entropy random tokens (passwords need <c>IPasswordHasher</c>).</summary>
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Constant-time comparison of a token with a stored hash.</summary>
    public static bool Matches(string token, string? storedHash) =>
        storedHash is not null
        && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(token)), Encoding.ASCII.GetBytes(storedHash));
}
