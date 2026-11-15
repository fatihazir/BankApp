using Application.Features.Auth.Constants;
using Application.Services.Repositories;
using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using BankingSystemCore.Security.CurrentUser;
using Domain.Entities;
using MediatR;

namespace Application.Features.Users.Commands.Ban;

/// <summary>
/// Bans a user: they cannot log in, and every session they have stops working immediately.
/// </summary>
public class BanUserCommand : IRequest<BannedUserResponse>, ISecuredRequest
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.StaffOnly;

    public Guid Id { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class BanUserCommandHandler : IRequestHandler<BanUserCommand, BannedUserResponse>
{
    private readonly IApplicationUserRepository _users;
    private readonly UserSessionService _sessions;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _time;

    public BanUserCommandHandler(IApplicationUserRepository users, UserSessionService sessions, ICurrentUser currentUser, TimeProvider time)
    {
        _users = users;
        _sessions = sessions;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<BannedUserResponse> Handle(BanUserCommand request, CancellationToken cancellationToken)
    {
        ApplicationUser user = await _users.GetAsync(u => u.Id == request.Id, cancellationToken: cancellationToken)
            ?? throw new NotFoundException(AuthMessages.UserNotFound);

        if (_currentUser.Id == user.Id)
            throw new BusinessException(AuthMessages.CannotBanYourself);
        if (user.BannedDate is not null)
            throw new BusinessException(AuthMessages.AlreadyBanned);

        DateTime now = _time.GetUtcNow().UtcDateTime;

        // First the user: from this moment every request with one of their tokens is refused.
        user.IsActive = false;
        user.BannedDate = now;
        user.BanReason = request.Reason.Trim();
        await _users.UpdateAsync(user, cancellationToken);

        // Then end their sessions, so refresh tokens stop working too.
        int revoked = await _sessions.RevokeAllAsync(user.Id, "User banned", cancellationToken);

        return new BannedUserResponse
        {
            UserId = user.Id, Email = user.Email, IsActive = user.IsActive, BannedDate = now, BanReason = user.BanReason, RevokedSessions = revoked
        };
    }
}
