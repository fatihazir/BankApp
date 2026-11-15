using BankingSystemCore.Repositories;
using Domain.Entities;

namespace Application.Services.Repositories;

public interface IUserSessionRepository : IAsyncRepository<UserSession, Guid>
{
}
