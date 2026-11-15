using BankingSystemCore.Repositories;

namespace BankingSystemCore.Security.Users;

/// <summary>
/// Someone who can log in: nothing but sign-in information. It knows nothing about roles, customers or any other
/// part of an application; an application derives its own user type from it (see <c>ApplicationUser</c>).
/// </summary>
public abstract class User : Entity<Guid>
{
    /// <summary>The login name. Trimmed and lower-cased, so it is unique regardless of the casing typed.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Salted password hash. The password itself is never stored.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>False blocks logins and makes every token of the user stop working at once.</summary>
    public bool IsActive { get; set; } = true;

    // ---- protection against guessing passwords ----

    /// <summary>Wrong passwords since the last successful login.</summary>
    public int FailedLoginCount { get; set; }

    /// <summary>While in the future, logins are refused.</summary>
    public DateTime? LockoutEnd { get; set; }

    public DateTime? LastLoginDate { get; set; }

    // ---- e-mail verification ----

    public bool EmailVerified { get; set; }

    public DateTime? EmailVerifiedDate { get; set; }

    /// <summary>SHA-256 of the verification token that was e-mailed. The token itself is never stored.</summary>
    public string? EmailVerificationTokenHash { get; set; }

    public DateTime? EmailVerificationTokenExpiresAt { get; set; }

    /// <summary>
    /// Concurrency token: a write based on stale data (e.g. a login that read the user just before a ban) fails
    /// instead of overwriting the newer values.
    /// </summary>
    public byte[] RowVersion { get; set; } = [];
}
