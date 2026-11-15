using Application.Services.Repositories;
using BankingSystemCore.Repositories;
using Domain.Entities;
using Persistence.Contexts;

namespace Persistence.Repositories;

public class UserSessionRepository : EfRepositoryBase<UserSession, Guid, BankAppDbContext>, IUserSessionRepository
{
    public UserSessionRepository(BankAppDbContext context) : base(context)
    {
    }
}
