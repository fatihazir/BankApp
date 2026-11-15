using BankingSystemCore.Repositories;
using BankingSystemCore.Security.Users;

namespace Domain.Entities;

/// <summary>
/// One login of a user (one device or browser). Its id is the "sid" claim of the access tokens, so revoking the
/// session invalidates those tokens immediately. It also holds the current refresh token (as a hash).
/// </summary>
public class UserSession : Entity<Guid>
{
    public Guid UserId { get; set; }

    /// <summary>Null when the navigation was not loaded (no Include).</summary>
    public User? User { get; set; }

    /// <summary>SHA-256 of the refresh token that is valid now.</summary>
    public string RefreshTokenHash { get; set; } = string.Empty;

    public DateTime RefreshTokenExpiresAt { get; set; }

    /// <summary>
    /// Hash of the refresh token that was replaced by the last refresh. Presenting it again means a token was
    /// copied or stolen, so the whole session is revoked.
    /// </summary>
    public string? PreviousRefreshTokenHash { get; set; }

    public DateTime? LastRefreshDate { get; set; }

    /// <summary>Set when the session ended: logout, ban, refresh token reuse. Null while the session is alive.</summary>
    public DateTime? RevokedDate { get; set; }

    public string? RevokeReason { get; set; }

    /// <summary>Concurrency token: two refreshes with the same token cannot both win.</summary>
    public byte[] RowVersion { get; set; } = [];
}
