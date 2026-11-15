using BankingSystemCore.Application.Pipelines.Auditing;
using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using Application.Features.LoanTypes.Profiles;
using Application.Features.LoanTypes.Rules;
using Application.Services.Repositories;
using Domain.Entities;
using MediatR;

namespace Application.Features.LoanTypes.Commands.Create;

[Audit("LoanType.Create", "LoanType")]
public class CreateLoanTypeCommand : IRequest<CreatedLoanTypeResponse>, ISecuredRequest
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.StaffOnly;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CustomerSegment Segment { get; set; }
}

public class CreateLoanTypeCommandHandler : IRequestHandler<CreateLoanTypeCommand, CreatedLoanTypeResponse>
{
    private readonly ILoanTypeRepository _loanTypes;
    private readonly LoanTypeBusinessRules _rules;

    public CreateLoanTypeCommandHandler(ILoanTypeRepository loanTypes, LoanTypeBusinessRules rules)
    {
        _loanTypes = loanTypes;
        _rules = rules;
    }

    public async Task<CreatedLoanTypeResponse> Handle(CreateLoanTypeCommand request, CancellationToken cancellationToken)
    {
        string name = request.Name.Trim();
        await _rules.NameCannotBeDuplicated(name, cancellationToken);

        LoanType loanType = LoanTypeMapper.ToEntity(request);
        loanType.Name = name;
        loanType.IsActive = true;

        await _loanTypes.AddAsync(loanType, cancellationToken);

        return LoanTypeMapper.ToCreatedResponse(loanType);
    }
}
