using Domain.Entities;

namespace Application.Features.Users.Commands.CreateStaff;

public class CreatedStaffUserResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
}
