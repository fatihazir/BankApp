using Application.Services.Repositories;
using BankingSystemCore.Repositories;
using Domain.Entities;
using Persistence.Contexts;

namespace Persistence.Repositories;

public class LoanSubTypeRepository : EfRepositoryBase<LoanSubType, Guid, BankAppDbContext>, ILoanSubTypeRepository
{
    public LoanSubTypeRepository(BankAppDbContext context) : base(context)
    {
    }
}
