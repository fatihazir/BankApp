namespace BankingSystemCore.Security.Hashing;

/// <summary>Bound from the "Security:PasswordHashing" configuration section.</summary>
public class PasswordHashingOptions
{
    public const string SectionName = "Security:PasswordHashing";

    /// <summary>PBKDF2-HMAC-SHA256 iterations (OWASP recommends at least 600,000). Lower only in tests.</summary>
    public int Iterations { get; set; } = 600_000;
}
