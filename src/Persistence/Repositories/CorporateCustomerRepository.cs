using Application.Services.Repositories;
using BankingSystemCore.Repositories;
using Domain.Entities;
using Persistence.Contexts;

namespace Persistence.Repositories;

public class CorporateCustomerRepository
    : EfRepositoryBase<CorporateCustomer, Guid, BankAppDbContext>, ICorporateCustomerRepository
{
    public CorporateCustomerRepository(BankAppDbContext context, TimeProvider time) : base(context, time)
    {
    }
}
