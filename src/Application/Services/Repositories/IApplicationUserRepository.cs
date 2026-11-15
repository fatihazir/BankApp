using BankingSystemCore.Repositories;
using Domain.Entities;

namespace Application.Services.Repositories;

public interface IApplicationUserRepository : IAsyncRepository<ApplicationUser, Guid>
{
}
