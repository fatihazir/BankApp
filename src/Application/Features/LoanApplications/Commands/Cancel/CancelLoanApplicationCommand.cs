using BankingSystemCore.Application.Pipelines.Auditing;
using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using Application.Features.LoanApplications.Constants;
using Application.Features.LoanApplications.Profiles;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Application.Features.LoanApplications.Rules;
using Application.Services.Repositories;
using Domain.Entities;
using MediatR;

namespace Application.Features.LoanApplications.Commands.Cancel;

[Audit("LoanApplication.Cancel", "LoanApplication")]
public class CancelLoanApplicationCommand : IRequest<LoanApplicationStatusResponse>, ISecuredRequest, IAuditDetail
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.StaffAndCustomer;

    string? IAuditDetail.AuditDetail => Reason;

    public Guid Id { get; set; }
    public string? Reason { get; set; }
}

public class CancelLoanApplicationCommandHandler : IRequestHandler<CancelLoanApplicationCommand, LoanApplicationStatusResponse>
{
    private readonly ILoanApplicationRepository _applications;
    private readonly LoanApplicationBusinessRules _rules;
    private readonly CustomerAccessPolicy _access;
    private readonly TimeProvider _time;

    public CancelLoanApplicationCommandHandler(
        ILoanApplicationRepository applications, LoanApplicationBusinessRules rules, CustomerAccessPolicy access, TimeProvider time)
    {
        _applications = applications;
        _rules = rules;
        _access = access;
        _time = time;
    }

    public async Task<LoanApplicationStatusResponse> Handle(
        CancelLoanApplicationCommand request, CancellationToken cancellationToken)
    {
        LoanApplication? application = await _applications.GetAsync(
            a => a.Id == request.Id, cancellationToken: cancellationToken);
        _rules.ApplicationShouldExist(application);
        if (!_access.CanAccess(application!.CustomerId))
            throw new NotFoundException(LoanApplicationsMessages.ApplicationNotFound);
        _rules.StatusMustBe(application!, LoanApplicationStatus.Submitted, LoanApplicationStatus.UnderReview);

        application!.Status = LoanApplicationStatus.Cancelled;
        application.DecisionDate = _time.GetUtcNow().UtcDateTime;
        application.DecisionReason = request.Reason;

        await _applications.UpdateAsync(application!, cancellationToken);

        return LoanApplicationMapper.ToStatusResponse(application!);
    }
}
