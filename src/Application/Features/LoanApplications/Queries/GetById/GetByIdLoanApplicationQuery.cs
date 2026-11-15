using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using Application.Features.LoanApplications.Constants;
using Application.Features.LoanApplications.Profiles;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Application.Features.LoanApplications.Rules;
using Application.Services.LoanCalculation;
using Application.Services.CustomerNaming;
using Application.Services.Repositories;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.LoanApplications.Queries.GetById;

public class GetByIdLoanApplicationQuery : IRequest<GetByIdLoanApplicationResponse>, ISecuredRequest
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.StaffAndCustomer;

    public Guid Id { get; set; }
}

public class GetByIdLoanApplicationQueryHandler
    : IRequestHandler<GetByIdLoanApplicationQuery, GetByIdLoanApplicationResponse>
{
    private readonly ILoanApplicationRepository _applications;
    private readonly LoanApplicationBusinessRules _rules;
    private readonly CustomerAccessPolicy _access;

    public GetByIdLoanApplicationQueryHandler(
        ILoanApplicationRepository applications, LoanApplicationBusinessRules rules, CustomerAccessPolicy access)
    {
        _applications = applications;
        _rules = rules;
        _access = access;
    }

    public async Task<GetByIdLoanApplicationResponse> Handle(
        GetByIdLoanApplicationQuery request, CancellationToken cancellationToken)
    {
        LoanApplication? application = await _applications.GetAsync(
            a => a.Id == request.Id,
            include: q => q.Include(a => a.Customer).Include(a => a.LoanSubType).ThenInclude(s => s!.LoanType),
            enableTracking: false,
            cancellationToken: cancellationToken);
        _rules.ApplicationShouldExist(application);
        if (!_access.CanAccess(application!.CustomerId))
            throw new NotFoundException(LoanApplicationsMessages.ApplicationNotFound);

        // The approved amount is what the customer will actually pay back.
        LoanCalculation calculation = LoanCalculator.Calculate(
            application!.ApprovedAmount ?? application.RequestedAmount, application.AnnualInterestRate, application.TermInMonths);

        GetByIdLoanApplicationResponse response = LoanApplicationMapper.ToGetByIdResponse(application);
        response.CustomerName = CustomerDisplayName.Of(application.Customer);
        response.MonthlyPayment = calculation.MonthlyPayment;
        response.TotalRepayment = calculation.TotalRepayment;
        response.TotalInterest = calculation.TotalInterest;
        return response;
    }
}
