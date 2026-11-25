using BankingSystemCore.Application.Pipelines.Auditing;
using Application.Services.Repositories;
using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using BankingSystemCore.Security.CurrentUser;
using Domain.Entities;
using MediatR;

namespace Application.Features.Auth.Commands.Logout;

/// <summary>Ends the session of the current token. The access token stops working at once.</summary>
[Audit("Auth.Logout", "UserSession")]
public class LogoutCommand : IRequest, ISecuredRequest
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.AnyAuthenticated;
}

public class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserSessionRepository _sessions;
    private readonly TimeProvider _time;

    public LogoutCommandHandler(ICurrentUser currentUser, IUserSessionRepository sessions, TimeProvider time)
    {
        _currentUser = currentUser;
        _sessions = sessions;
        _time = time;
    }

    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.SessionId is not { } sessionId)
            return;

        UserSession? session = await _sessions.GetAsync(
            s => s.Id == sessionId && s.RevokedDate == null, cancellationToken: cancellationToken);
        if (session is null)
            return; // already ended: logging out twice is fine

        session.RevokedDate = _time.GetUtcNow().UtcDateTime;
        session.RevokeReason = "Logout";
        await _sessions.UpdateAsync(session, cancellationToken);
    }
}
