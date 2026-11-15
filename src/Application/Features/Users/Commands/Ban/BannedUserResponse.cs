namespace Application.Features.Users.Commands.Ban;

public class BannedUserResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? BannedDate { get; set; }
    public string? BanReason { get; set; }

    /// <summary>How many logins were ended by the ban.</summary>
    public int RevokedSessions { get; set; }
}
