namespace Application.Features.Users.Commands.Unban;

public class UnbannedUserResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
