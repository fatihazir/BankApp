using BankingSystemCore.Repositories;
using Domain.Entities;

namespace Application.Services.Repositories;

public interface IUserSessionRepository : IAsyncRepository<UserSession, Guid>
{
    /// <summary>
    /// Permanently deletes up to <paramref name="batchSize"/> sessions that ended before the given time: revoked
    /// before it, or with a refresh token that expired before it. Returns how many rows were deleted.
    /// </summary>
    Task<int> DeleteEndedBeforeAsync(DateTime before, int batchSize, CancellationToken cancellationToken = default);
}
