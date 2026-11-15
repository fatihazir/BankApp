using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using Application.Features.LoanSubTypes.Profiles;
using Application.Features.LoanSubTypes.Rules;
using Application.Features.LoanTypes.Rules;
using Application.Services.Repositories;
using Domain.Entities;
using MediatR;

namespace Application.Features.LoanSubTypes.Commands.Create;

public class CreateLoanSubTypeCommand : IRequest<CreatedLoanSubTypeResponse>, ISecuredRequest
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.StaffOnly;

    public Guid LoanTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public int MinTermInMonths { get; set; }
    public int MaxTermInMonths { get; set; }

    /// <summary>Annual interest rate in percent (e.g. 42.50).</summary>
    public decimal AnnualInterestRate { get; set; }
}

public class CreateLoanSubTypeCommandHandler : IRequestHandler<CreateLoanSubTypeCommand, CreatedLoanSubTypeResponse>
{
    private readonly ILoanTypeRepository _loanTypes;
    private readonly ILoanSubTypeRepository _subTypes;
    private readonly LoanTypeBusinessRules _loanTypeRules;
    private readonly LoanSubTypeBusinessRules _rules;

    public CreateLoanSubTypeCommandHandler(
        ILoanTypeRepository loanTypes,
        ILoanSubTypeRepository subTypes,
        LoanTypeBusinessRules loanTypeRules,
        LoanSubTypeBusinessRules rules)
    {
        _loanTypes = loanTypes;
        _subTypes = subTypes;
        _loanTypeRules = loanTypeRules;
        _rules = rules;
    }

    public async Task<CreatedLoanSubTypeResponse> Handle(CreateLoanSubTypeCommand request, CancellationToken cancellationToken)
    {
        LoanType? loanType = await _loanTypes.GetAsync(
            t => t.Id == request.LoanTypeId, enableTracking: false, cancellationToken: cancellationToken);
        _loanTypeRules.LoanTypeShouldExist(loanType);
        _loanTypeRules.LoanTypeMustBeActive(loanType!);

        string name = request.Name.Trim();
        await _rules.NameCannotBeDuplicatedWithinLoanType(request.LoanTypeId, name, cancellationToken);

        LoanSubType subType = LoanSubTypeMapper.ToEntity(request);
        subType.Name = name;
        subType.IsActive = true;

        await _subTypes.AddAsync(subType, cancellationToken);

        return LoanSubTypeMapper.ToCreatedResponse(subType);
    }
}
