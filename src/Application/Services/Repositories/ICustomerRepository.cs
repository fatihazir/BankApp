using BankingSystemCore.Repositories;
using Domain.Entities;

namespace Application.Services.Repositories;

public interface ICustomerRepository : IAsyncRepository<Customer, Guid>
{
}
