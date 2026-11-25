using BankingSystemCore.Application.Pipelines.Auditing;
using Application.Features.Auth.Constants;
using Application.Services.Repositories;
using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Domain.Entities;
using MediatR;

namespace Application.Features.Users.Commands.Unban;

/// <summary>Lifts a ban. Old sessions stay ended: the user has to log in again.</summary>
[Audit("User.Unban", "User")]
public class UnbanUserCommand : IRequest<UnbannedUserResponse>, ISecuredRequest
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.StaffOnly;

    public Guid Id { get; set; }
}

public class UnbanUserCommandHandler : IRequestHandler<UnbanUserCommand, UnbannedUserResponse>
{
    private readonly IApplicationUserRepository _users;

    public UnbanUserCommandHandler(IApplicationUserRepository users)
    {
        _users = users;
    }

    public async Task<UnbannedUserResponse> Handle(UnbanUserCommand request, CancellationToken cancellationToken)
    {
        ApplicationUser user = await _users.GetAsync(u => u.Id == request.Id, cancellationToken: cancellationToken)
            ?? throw new NotFoundException(AuthMessages.UserNotFound);

        if (user.BannedDate is null)
            throw new BusinessException("The user is not banned.");

        user.IsActive = true;
        user.BannedDate = null;
        user.BanReason = null;
        user.FailedLoginCount = 0;
        user.LockoutEnd = null;
        await _users.UpdateAsync(user, cancellationToken);

        return new UnbannedUserResponse { UserId = user.Id, Email = user.Email, IsActive = true };
    }
}
