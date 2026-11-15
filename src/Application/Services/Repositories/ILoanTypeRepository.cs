using BankingSystemCore.Repositories;
using Domain.Entities;

namespace Application.Services.Repositories;

public interface ILoanTypeRepository : IAsyncRepository<LoanType, Guid>
{
}
