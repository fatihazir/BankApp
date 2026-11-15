using Application.Services.Repositories;
using BankingSystemCore.Repositories;
using Domain.Entities;

namespace Application.Services.Security;

/// <summary>Ends sessions in bulk, e.g. when a user is banned or deleted.</summary>
public class UserSessionService
{
    private const int BatchSize = 200;

    private readonly IUserSessionRepository _sessions;
    private readonly TimeProvider _time;

    public UserSessionService(IUserSessionRepository sessions, TimeProvider time)
    {
        _sessions = sessions;
        _time = time;
    }

    /// <summary>Revokes every live session of the user. Their access and refresh tokens stop working at once.</summary>
    public async Task<int> RevokeAllAsync(Guid userId, string reason, CancellationToken cancellationToken = default)
    {
        DateTime now = _time.GetUtcNow().UtcDateTime;
        int revoked = 0;
        Paginate<UserSession> batch;

        do
        {
            // Revoked sessions drop out of the filter, so the first page is always the next batch.
            batch = await _sessions.GetListAsync(
                predicate: s => s.UserId == userId && s.RevokedDate == null,
                index: 0, size: BatchSize, cancellationToken: cancellationToken);

            foreach (UserSession session in batch.Items)
            {
                session.RevokedDate = now;
                session.RevokeReason = reason;
            }

            if (batch.Items.Count > 0)
                await _sessions.UpdateRangeAsync(batch.Items, cancellationToken);
            revoked += batch.Items.Count;
        }
        while (batch.Items.Count == BatchSize);

        return revoked;
    }
}
