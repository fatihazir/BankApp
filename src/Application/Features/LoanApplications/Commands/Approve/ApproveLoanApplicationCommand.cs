using BankingSystemCore.Application.Pipelines.Auditing;
using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using Application.Features.LoanApplications.Profiles;
using Application.Features.LoanApplications.Rules;
using Application.Services.Repositories;
using Domain.Entities;
using MediatR;

namespace Application.Features.LoanApplications.Commands.Approve;

[Audit("LoanApplication.Approve", "LoanApplication")]
public class ApproveLoanApplicationCommand : IRequest<LoanApplicationStatusResponse>, ISecuredRequest, IAuditDetail
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.StaffOnly;

    string? IAuditDetail.AuditDetail => $"Approved amount {ApprovedAmount}" + (string.IsNullOrWhiteSpace(Reason) ? "" : $". {Reason}");

    public Guid Id { get; set; }
    public decimal ApprovedAmount { get; set; }
    public string? Reason { get; set; }
}

public class ApproveLoanApplicationCommandHandler : IRequestHandler<ApproveLoanApplicationCommand, LoanApplicationStatusResponse>
{
    private readonly ILoanApplicationRepository _applications;
    private readonly LoanApplicationBusinessRules _rules;
    private readonly TimeProvider _time;

    public ApproveLoanApplicationCommandHandler(ILoanApplicationRepository applications, LoanApplicationBusinessRules rules, TimeProvider time)
    {
        _applications = applications;
        _rules = rules;
        _time = time;
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
        application.DecisionDate = _time.GetUtcNow().UtcDateTime;
        application.ApprovedAmount = request.ApprovedAmount;
        application.DecisionReason = request.Reason;

        await _applications.UpdateAsync(application!, cancellationToken);

        return LoanApplicationMapper.ToStatusResponse(application!);
    }
}
