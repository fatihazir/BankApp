namespace Application.Services.Security;

/// <summary>Bound from the "Security:EmailVerification" configuration section.</summary>
public class EmailVerificationOptions
{
    public const string SectionName = "Security:EmailVerification";

    /// <summary>
    /// When true, users must verify their e-mail address before they can log in. Off until a real e-mail
    /// provider is configured, otherwise nobody could ever verify.
    /// </summary>
    public bool RequireVerifiedEmail { get; set; }

    public int TokenLifetimeHours { get; set; } = 24;

    /// <summary>
    /// Link put into the e-mail. <c>{email}</c> and <c>{token}</c> are replaced. Point it at the page of the
    /// front-end that calls <c>POST /api/v0/auth/verify-email</c>.
    /// </summary>
    public string LinkTemplate { get; set; } = "http://localhost:5043/verify-email?email={email}&token={token}";
}
