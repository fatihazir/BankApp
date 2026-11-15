using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using Application.Features.LoanApplications.Rules;
using Application.Services.LoanCalculation;
using Application.Services.Repositories;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.LoanApplications.Queries.Calculate;

/// <summary>Calculates the installments of a loan before (or without) applying for it.</summary>
public class CalculateLoanQuery : IRequest<CalculateLoanResponse>, ISecuredRequest
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.AnyAuthenticated;

    public Guid LoanSubTypeId { get; set; }
    public decimal Amount { get; set; }
    public int TermInMonths { get; set; }
}

public class CalculateLoanQueryHandler : IRequestHandler<CalculateLoanQuery, CalculateLoanResponse>
{
    private readonly ILoanSubTypeRepository _subTypes;
    private readonly LoanApplicationBusinessRules _rules;

    public CalculateLoanQueryHandler(ILoanSubTypeRepository subTypes, LoanApplicationBusinessRules rules)
    {
        _subTypes = subTypes;
        _rules = rules;
    }

    public async Task<CalculateLoanResponse> Handle(CalculateLoanQuery request, CancellationToken cancellationToken)
    {
        LoanSubType? subType = await _subTypes.GetAsync(
            s => s.Id == request.LoanSubTypeId,
            include: q => q.Include(s => s.LoanType),
            enableTracking: false,
            cancellationToken: cancellationToken);
        _rules.LoanSubTypeShouldExist(subType);
        _rules.LoanSubTypeMustBeActive(subType!);
        _rules.AmountMustBeWithinLimits(subType!, request.Amount);
        _rules.TermMustBeWithinLimits(subType!, request.TermInMonths);

        LoanCalculation result = LoanCalculator.Calculate(request.Amount, subType!.AnnualInterestRate, request.TermInMonths);

        return new CalculateLoanResponse
        {
            LoanSubTypeId = subType.Id,
            LoanSubTypeName = subType.Name,
            Amount = request.Amount,
            TermInMonths = request.TermInMonths,
            AnnualInterestRate = subType.AnnualInterestRate,
            MonthlyPayment = result.MonthlyPayment,
            TotalRepayment = result.TotalRepayment,
            TotalInterest = result.TotalInterest,
            Schedule = result.Schedule
                .Select(i => new InstallmentDto
                {
                    Number = i.Number,
                    Payment = i.Payment,
                    Principal = i.Principal,
                    Interest = i.Interest,
                    RemainingBalance = i.RemainingBalance
                })
                .ToList()
        };
    }
}
