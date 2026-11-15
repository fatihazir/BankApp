using BankingSystemCore.Application.Pipelines.Auditing;
using Domain.Entities;

namespace Application.Features.Users.Commands.CreateStaff;

public class CreatedStaffUserResponse : IAuditTarget
{
    Guid? IAuditTarget.AuditTargetId => UserId;

    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
}
