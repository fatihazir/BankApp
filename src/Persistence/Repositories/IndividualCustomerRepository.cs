using Application.Services.Repositories;
using BankingSystemCore.Repositories;
using Domain.Entities;
using Persistence.Contexts;

namespace Persistence.Repositories;

public class IndividualCustomerRepository
    : EfRepositoryBase<IndividualCustomer, Guid, BankAppDbContext>, IIndividualCustomerRepository
{
    public IndividualCustomerRepository(BankAppDbContext context) : base(context)
    {
    }
}
