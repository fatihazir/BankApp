using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using Application.Features.LoanApplications.Profiles;
using Application.Features.LoanApplications.Rules;
using Application.Services.Repositories;
using Domain.Entities;
using MediatR;

namespace Application.Features.LoanApplications.Commands.Approve;

public class ApproveLoanApplicationCommand : IRequest<LoanApplicationStatusResponse>, ISecuredRequest
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.StaffOnly;

    public Guid Id { get; set; }
    public decimal ApprovedAmount { get; set; }
    public string? Reason { get; set; }
}

public class ApproveLoanApplicationCommandHandler : IRequestHandler<ApproveLoanApplicationCommand, LoanApplicationStatusResponse>
{
    private readonly ILoanApplicationRepository _applications;
    private readonly LoanApplicationBusinessRules _rules;

    public ApproveLoanApplicationCommandHandler(ILoanApplicationRepository applications, LoanApplicationBusinessRules rules)
    {
        _applications = applications;
        _rules = rules;
    }

    public async Task<LoanApplicationStatusResponse> Handle(
        ApproveLoanApplicationCommand request, CancellationToken cancellationToken)
    {
        LoanApplication? application = await _applications.GetAsync(
            a => a.Id == request.Id, cancellationToken: cancellationToken);
        _rules.ApplicationShouldExist(application);
        _rules.StatusMustBe(application!, LoanApplicationStatus.UnderReview);
        _rules.ApprovedAmountMustBeValid(application!, request.ApprovedAmount);
        application!.Status = LoanApplicationStatus.Approved;
        application.DecisionDate = DateTime.UtcNow;
        application.ApprovedAmount = request.ApprovedAmount;
        application.DecisionReason = request.Reason;

        await _applications.UpdateAsync(application!, cancellationToken);

        return LoanApplicationMapper.ToStatusResponse(application!);
    }
}
