using Application.Services.Repositories;

namespace Application.Services.Security;

public class SessionValidator : ISessionValidator
{
    private readonly IUserSessionRepository _sessions;

    public SessionValidator(IUserSessionRepository sessions)
    {
        _sessions = sessions;
    }

    /// <summary>One query: the session must not be revoked and its user must still exist and be active (not banned, not deleted).</summary>
    public Task<bool> IsActiveAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        _sessions.AnyAsync(s => s.Id == sessionId && s.RevokedDate == null && s.User!.IsActive && s.User.DeleteDate == null, cancellationToken: cancellationToken);
}
