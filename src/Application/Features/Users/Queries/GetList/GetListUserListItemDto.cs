using Domain.Entities;

namespace Application.Features.Users.Queries.GetList;

/// <summary>Never contains the password hash or any token.</summary>
public class GetListUserListItemDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerNumber { get; set; }
    public bool IsActive { get; set; }
    public bool EmailVerified { get; set; }
    public DateTime? BannedDate { get; set; }
    public string? BanReason { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public DateTime? LastLoginDate { get; set; }
    public DateTime CreateDate { get; set; }
}
