using Application.Features.LoanSubTypes.Constants;
using Application.Services.Repositories;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;

namespace Application.Features.LoanSubTypes.Rules;

public class LoanSubTypeBusinessRules
{
    private readonly ILoanSubTypeRepository _subTypes;

    public LoanSubTypeBusinessRules(ILoanSubTypeRepository subTypes)
    {
        _subTypes = subTypes;
    }

    public async Task NameCannotBeDuplicatedWithinLoanType(Guid loanTypeId, string name, CancellationToken cancellationToken = default)
    {
        // Soft-deleted rows are included because the database unique index covers them too.
        bool exists = await _subTypes.AnyAsync(
            s => s.LoanTypeId == loanTypeId && s.Name == name, withDeleted: true, cancellationToken: cancellationToken);

        if (exists)
            throw new BusinessException(LoanSubTypesMessages.NameExists);
    }
}
