using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using Application.Features.LoanApplications.Profiles;
using Application.Features.LoanApplications.Rules;
using Application.Services.Repositories;
using Domain.Entities;
using MediatR;

namespace Application.Features.LoanApplications.Commands.Reject;

public class RejectLoanApplicationCommand : IRequest<LoanApplicationStatusResponse>, ISecuredRequest
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.StaffOnly;

    public Guid Id { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class RejectLoanApplicationCommandHandler : IRequestHandler<RejectLoanApplicationCommand, LoanApplicationStatusResponse>
{
    private readonly ILoanApplicationRepository _applications;
    private readonly LoanApplicationBusinessRules _rules;

    public RejectLoanApplicationCommandHandler(ILoanApplicationRepository applications, LoanApplicationBusinessRules rules)
    {
        _applications = applications;
        _rules = rules;
    }

    public async Task<LoanApplicationStatusResponse> Handle(
        RejectLoanApplicationCommand request, CancellationToken cancellationToken)
    {
        LoanApplication? application = await _applications.GetAsync(
            a => a.Id == request.Id, cancellationToken: cancellationToken);
        _rules.ApplicationShouldExist(application);
        _rules.StatusMustBe(application!, LoanApplicationStatus.Submitted, LoanApplicationStatus.UnderReview);

        application!.Status = LoanApplicationStatus.Rejected;
        application.DecisionDate = DateTime.UtcNow;
        application.DecisionReason = request.Reason;

        await _applications.UpdateAsync(application!, cancellationToken);

        return LoanApplicationMapper.ToStatusResponse(application!);
    }
}
