using Application.Services.Repositories;
using BankingSystemCore.Repositories;
using Domain.Entities;
using Persistence.Contexts;

namespace Persistence.Repositories;

public class CustomerRepository : EfRepositoryBase<Customer, Guid, BankAppDbContext>, ICustomerRepository
{
    public CustomerRepository(BankAppDbContext context, TimeProvider time) : base(context, time)
    {
    }
}
