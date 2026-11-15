using BankingSystemCore.Repositories;
using Domain.Entities;

namespace Application.Services.Repositories;

public interface ILoanSubTypeRepository : IAsyncRepository<LoanSubType, Guid>
{
}
