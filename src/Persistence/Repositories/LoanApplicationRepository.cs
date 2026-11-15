using Application.Services.Repositories;
using BankingSystemCore.Repositories;
using Domain.Entities;
using Persistence.Contexts;

namespace Persistence.Repositories;

public class LoanApplicationRepository : EfRepositoryBase<LoanApplication, Guid, BankAppDbContext>, ILoanApplicationRepository
{
    public LoanApplicationRepository(BankAppDbContext context, TimeProvider time) : base(context, time)
    {
    }
}
