using Application.Features.LoanTypes.Constants;
using Application.Services.Repositories;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Domain.Entities;

namespace Application.Features.LoanTypes.Rules;

public class LoanTypeBusinessRules
{
    private readonly ILoanTypeRepository _loanTypes;

    public LoanTypeBusinessRules(ILoanTypeRepository loanTypes)
    {
        _loanTypes = loanTypes;
    }

    public void LoanTypeShouldExist(LoanType? loanType)
    {
        if (loanType is null)
            throw new NotFoundException(LoanTypesMessages.LoanTypeNotFound);
    }

    public void LoanTypeMustBeActive(LoanType loanType)
    {
        if (!loanType.IsActive)
            throw new BusinessException(LoanTypesMessages.LoanTypeInactive);
    }

    public async Task NameCannotBeDuplicated(string name, CancellationToken cancellationToken = default)
    {
        // Soft-deleted rows are included because the database unique index covers them too.
        bool exists = await _loanTypes.AnyAsync(t => t.Name == name, withDeleted: true, cancellationToken: cancellationToken);

        if (exists)
            throw new BusinessException(LoanTypesMessages.LoanTypeNameExists);
    }
}
