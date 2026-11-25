using BankingSystemCore.Application.Pipelines.Auditing;
using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using Application.Features.LoanApplications.Profiles;
using Application.Features.LoanApplications.Rules;
using Application.Services.LoanCalculation;
using Application.Services.Repositories;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.LoanApplications.Commands.Create;

[Audit("LoanApplication.Create", "LoanApplication")]
public class CreateLoanApplicationCommand : IRequest<CreatedLoanApplicationResponse>, ISecuredRequest
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.StaffAndCustomer;

    public Guid CustomerId { get; set; }
    public Guid LoanSubTypeId { get; set; }
    public decimal RequestedAmount { get; set; }
    public int TermInMonths { get; set; }
    public string? Purpose { get; set; }
}

public class CreateLoanApplicationCommandHandler
    : IRequestHandler<CreateLoanApplicationCommand, CreatedLoanApplicationResponse>
{
    private readonly ICustomerRepository _customers;
    private readonly ILoanSubTypeRepository _subTypes;
    private readonly ILoanApplicationRepository _applications;
    private readonly LoanApplicationBusinessRules _rules;
    private readonly CustomerAccessPolicy _access;
    private readonly TimeProvider _time;

    public CreateLoanApplicationCommandHandler(
        ICustomerRepository customers,
        ILoanSubTypeRepository subTypes,
        ILoanApplicationRepository applications,
        LoanApplicationBusinessRules rules,
        CustomerAccessPolicy access,
        TimeProvider time)
    {
        _customers = customers;
        _subTypes = subTypes;
        _applications = applications;
        _rules = rules;
        _access = access;
        _time = time;
    }

    public async Task<CreatedLoanApplicationResponse> Handle(
        CreateLoanApplicationCommand request, CancellationToken cancellationToken)
    {
        // A customer can only apply for themselves; staff can apply on behalf of anyone.
        _access.EnsureCanAccess(request.CustomerId);

        Customer? customer = await _customers.GetAsync(
            c => c.Id == request.CustomerId, enableTracking: false, cancellationToken: cancellationToken);
        _rules.CustomerShouldExist(customer);
        _rules.CustomerMustBeActive(customer!);

        LoanSubType? subType = await _subTypes.GetAsync(
            s => s.Id == request.LoanSubTypeId,
            include: q => q.Include(s => s.LoanType),
            enableTracking: false,
            cancellationToken: cancellationToken);
        _rules.LoanSubTypeShouldExist(subType);
        _rules.LoanSubTypeMustBeActive(subType!);

        // LoanType is loaded: the activity rule above fails if it is missing.
        _rules.CustomerSegmentMustMatchLoanType(customer!, subType!.LoanType!);
        _rules.AmountMustBeWithinLimits(subType, request.RequestedAmount);
        _rules.TermMustBeWithinLimits(subType, request.TermInMonths);
        await _rules.CustomerCannotHaveOpenApplicationForSameSubType(
            request.CustomerId, request.LoanSubTypeId, cancellationToken);

        LoanApplication application = LoanApplicationMapper.ToEntity(request);
        application.ApplicationNumber = NewApplicationNumber();
        application.Status = LoanApplicationStatus.Submitted;
        application.AnnualInterestRate = subType.AnnualInterestRate;

        await _applications.AddAsync(application, cancellationToken);

        LoanCalculation calculation = LoanCalculator.Calculate(
            application.RequestedAmount, application.AnnualInterestRate, application.TermInMonths);

        CreatedLoanApplicationResponse response = LoanApplicationMapper.ToCreatedResponse(application);
        response.MonthlyPayment = calculation.MonthlyPayment;
        response.TotalRepayment = calculation.TotalRepayment;
        response.TotalInterest = calculation.TotalInterest;
        return response;
    }

    private string NewApplicationNumber() =>
        $"LA-{_time.GetUtcNow():yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
}
