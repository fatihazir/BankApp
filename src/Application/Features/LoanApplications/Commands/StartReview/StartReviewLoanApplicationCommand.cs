using BankingSystemCore.Application.Pipelines.Auditing;
using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using Application.Features.LoanApplications.Profiles;
using Application.Features.LoanApplications.Rules;
using Application.Services.Repositories;
using Domain.Entities;
using MediatR;

namespace Application.Features.LoanApplications.Commands.StartReview;

[Audit("LoanApplication.StartReview", "LoanApplication")]
public class StartReviewLoanApplicationCommand : IRequest<LoanApplicationStatusResponse>, ISecuredRequest
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.StaffOnly;

    public Guid Id { get; set; }
}

public class StartReviewLoanApplicationCommandHandler : IRequestHandler<StartReviewLoanApplicationCommand, LoanApplicationStatusResponse>
{
    private readonly ILoanApplicationRepository _applications;
    private readonly LoanApplicationBusinessRules _rules;

    public StartReviewLoanApplicationCommandHandler(ILoanApplicationRepository applications, LoanApplicationBusinessRules rules)
    {
        _applications = applications;
        _rules = rules;
    }

    public async Task<LoanApplicationStatusResponse> Handle(
        StartReviewLoanApplicationCommand request, CancellationToken cancellationToken)
    {
        LoanApplication? application = await _applications.GetAsync(
            a => a.Id == request.Id, cancellationToken: cancellationToken);
        _rules.ApplicationShouldExist(application);
        _rules.StatusMustBe(application!, LoanApplicationStatus.Submitted);

        application!.Status = LoanApplicationStatus.UnderReview;

        await _applications.UpdateAsync(application!, cancellationToken);

        return LoanApplicationMapper.ToStatusResponse(application!);
    }
}
