using Application.Services.Repositories;
using BankingSystemCore.Repositories;
using Domain.Entities;
using Persistence.Contexts;

namespace Persistence.Repositories;

public class LoanTypeRepository : EfRepositoryBase<LoanType, Guid, BankAppDbContext>, ILoanTypeRepository
{
    public LoanTypeRepository(BankAppDbContext context, TimeProvider time) : base(context, time)
    {
    }
}
