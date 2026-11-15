using Application.Services.Repositories;
using BankingSystemCore.Repositories;
using Domain.Entities;
using Persistence.Contexts;

namespace Persistence.Repositories;

public class ApplicationUserRepository : EfRepositoryBase<ApplicationUser, Guid, BankAppDbContext>, IApplicationUserRepository
{
    public ApplicationUserRepository(BankAppDbContext context, TimeProvider time) : base(context, time)
    {
    }
}
