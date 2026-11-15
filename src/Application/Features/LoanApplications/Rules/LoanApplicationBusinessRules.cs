using System.Globalization;
using Application.Features.LoanApplications.Constants;
using Application.Services.Repositories;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Domain.Entities;

namespace Application.Features.LoanApplications.Rules;

public class LoanApplicationBusinessRules
{
    private readonly ILoanApplicationRepository _applications;

    public LoanApplicationBusinessRules(ILoanApplicationRepository applications)
    {
        _applications = applications;
    }

    public void ApplicationShouldExist(LoanApplication? application)
    {
        if (application is null)
            throw new NotFoundException(LoanApplicationsMessages.ApplicationNotFound);
    }

    public void CustomerShouldExist(Customer? customer)
    {
        if (customer is null)
            throw new NotFoundException(LoanApplicationsMessages.CustomerNotFound);
    }

    public void CustomerMustBeActive(Customer customer)
    {
        if (!customer.IsActive)
            throw new BusinessException(LoanApplicationsMessages.CustomerInactive);
    }

    public void LoanSubTypeShouldExist(LoanSubType? subType)
    {
        if (subType is null)
            throw new NotFoundException(LoanApplicationsMessages.LoanSubTypeNotFound);
    }

    public void LoanSubTypeMustBeActive(LoanSubType subType)
    {
        LoanType loanType = subType.LoanType
            ?? throw new InvalidOperationException("The LoanType of the sub type must be loaded (use Include).");

        if (!subType.IsActive || !loanType.IsActive)
            throw new BusinessException(LoanApplicationsMessages.LoanSubTypeInactive);
    }

    /// <summary>Individual customers can only use individual loan types, corporate customers only corporate ones.</summary>
    public void CustomerSegmentMustMatchLoanType(Customer customer, LoanType loanType)
    {
        if (SegmentOf(customer) != loanType.Segment)
            throw new BusinessException(LoanApplicationsMessages.SegmentMismatch);
    }

    public void AmountMustBeWithinLimits(LoanSubType subType, decimal amount)
    {
        if (amount < subType.MinAmount || amount > subType.MaxAmount)
            throw new BusinessException(string.Format(CultureInfo.InvariantCulture,
                LoanApplicationsMessages.AmountOutOfRange, subType.MinAmount, subType.MaxAmount));
    }

    public void TermMustBeWithinLimits(LoanSubType subType, int termInMonths)
    {
        if (termInMonths < subType.MinTermInMonths || termInMonths > subType.MaxTermInMonths)
            throw new BusinessException(string.Format(CultureInfo.InvariantCulture,
                LoanApplicationsMessages.TermOutOfRange, subType.MinTermInMonths, subType.MaxTermInMonths));
    }

    public async Task CustomerCannotHaveOpenApplicationForSameSubType(
        Guid customerId, Guid loanSubTypeId, CancellationToken cancellationToken = default)
    {
        bool exists = await _applications.AnyAsync(
            a => a.CustomerId == customerId
                 && a.LoanSubTypeId == loanSubTypeId
                 && (a.Status == LoanApplicationStatus.Submitted || a.Status == LoanApplicationStatus.UnderReview),
            cancellationToken: cancellationToken);

        if (exists)
            throw new BusinessException(LoanApplicationsMessages.OpenApplicationExists);
    }

    /// <summary>Guards the status flow: Submitted -> UnderReview -> Approved/Rejected, or Cancelled while open.</summary>
    public void StatusMustBe(LoanApplication application, params LoanApplicationStatus[] allowed)
    {
        if (!allowed.Contains(application.Status))
            throw new BusinessException(string.Format(CultureInfo.InvariantCulture,
                LoanApplicationsMessages.InvalidStatus, application.Status));
    }

    public void ApprovedAmountMustBeValid(LoanApplication application, decimal approvedAmount)
    {
        if (approvedAmount <= 0 || approvedAmount > application.RequestedAmount)
            throw new BusinessException(LoanApplicationsMessages.ApprovedAmountInvalid);
    }

    public static CustomerSegment SegmentOf(Customer customer) => customer switch
    {
        IndividualCustomer => CustomerSegment.Individual,
        CorporateCustomer => CustomerSegment.Corporate,
        _ => throw new InvalidOperationException($"Unknown customer type {customer.GetType().Name}.")
    };
}
