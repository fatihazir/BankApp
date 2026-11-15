using Application.Services.Repositories;
using BankingSystemCore.Repositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Persistence.Contexts;

namespace Persistence.Repositories;

public class UserSessionRepository : EfRepositoryBase<UserSession, Guid, BankAppDbContext>, IUserSessionRepository
{
    public UserSessionRepository(BankAppDbContext context, TimeProvider time) : base(context, time)
    {
    }

    public async Task<int> DeleteEndedBeforeAsync(DateTime before, int batchSize, CancellationToken cancellationToken = default)
    {
        // Pick the batch first: a delete cannot be limited with Take in one statement.
        List<Guid> ids = await Context.UserSessions
            .Where(s => (s.RevokedDate != null && s.RevokedDate < before) || s.RefreshTokenExpiresAt < before)
            .OrderBy(s => s.RefreshTokenExpiresAt)
            .Select(s => s.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
            return 0;

        return await Context.UserSessions.Where(s => ids.Contains(s.Id)).ExecuteDeleteAsync(cancellationToken);
    }
}
