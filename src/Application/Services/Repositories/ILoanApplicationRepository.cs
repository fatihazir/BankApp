using BankingSystemCore.Repositories;
using Domain.Entities;

namespace Application.Services.Repositories;

public interface ILoanApplicationRepository : IAsyncRepository<LoanApplication, Guid>
{
}
