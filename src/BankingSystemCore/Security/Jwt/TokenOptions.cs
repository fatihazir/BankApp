namespace BankingSystemCore.Security.Jwt;

/// <summary>Settings of the access tokens. Bound from the "Security:Jwt" configuration section.</summary>
public class TokenOptions
{
    public const string SectionName = "Security:Jwt";

    /// <summary>The 256-bit minimum keeps HMAC-SHA256 signatures strong. Never commit a real key.</summary>
    public const int MinimumKeyLengthInBytes = 32;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    /// <summary>Secret used to sign tokens. At least 32 bytes.</summary>
    public string SecurityKey { get; set; } = string.Empty;

    /// <summary>Short on purpose: a new one is obtained with the refresh token.</summary>
    public int AccessTokenExpirationMinutes { get; set; } = 10;

    /// <summary>How long a refresh token lives. Every refresh issues a new one, so an active session keeps going.</summary>
    public int RefreshTokenExpirationMinutes { get; set; } = 30;

    public bool IsValid(out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(Issuer)) error = "Security:Jwt:Issuer is required.";
        else if (string.IsNullOrWhiteSpace(Audience)) error = "Security:Jwt:Audience is required.";
        else if (System.Text.Encoding.UTF8.GetByteCount(SecurityKey ?? string.Empty) < MinimumKeyLengthInBytes)
            error = $"Security:Jwt:SecurityKey must be at least {MinimumKeyLengthInBytes} bytes long.";
        else if (AccessTokenExpirationMinutes is < 1 or > 1440)
            error = "Security:Jwt:AccessTokenExpirationMinutes must be between 1 and 1440.";
        else if (RefreshTokenExpirationMinutes is < 1 or > 43200)
            error = "Security:Jwt:RefreshTokenExpirationMinutes must be between 1 and 43200.";

        return error.Length == 0;
    }
}
