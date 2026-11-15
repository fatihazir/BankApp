using System.Linq.Expressions;
using Application.Features.LoanApplications.Rules;
using Application.Services.Repositories;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Domain.Entities;
using NSubstitute;

namespace Application.UnitTests.Features.LoanApplications;

public class LoanApplicationBusinessRulesTests
{
    private readonly ILoanApplicationRepository _repository = Substitute.For<ILoanApplicationRepository>();
    private readonly LoanApplicationBusinessRules _rules;

    public LoanApplicationBusinessRulesTests()
    {
        _rules = new LoanApplicationBusinessRules(_repository);
    }

    private static LoanSubType SubType() => new()
    {
        MinAmount = 5_000, MaxAmount = 100_000, MinTermInMonths = 3, MaxTermInMonths = 60, IsActive = true,
        LoanType = new LoanType { Segment = CustomerSegment.Individual, IsActive = true }
    };

    private sealed class UnknownCustomer : Customer;

    // ---- existence ----

    [Fact]
    public void ApplicationShouldExist_ThrowsNotFound_WhenNull() =>
        Should.Throw<NotFoundException>(() => _rules.ApplicationShouldExist(null));

    [Fact]
    public void CustomerShouldExist_ThrowsNotFound_WhenNull() =>
        Should.Throw<NotFoundException>(() => _rules.CustomerShouldExist(null));

    [Fact]
    public void LoanSubTypeShouldExist_ThrowsNotFound_WhenNull() =>
        Should.Throw<NotFoundException>(() => _rules.LoanSubTypeShouldExist(null));

    [Fact]
    public void ExistingEntities_PassTheExistenceRules()
    {
        Should.NotThrow(() => _rules.ApplicationShouldExist(new LoanApplication()));
        Should.NotThrow(() => _rules.CustomerShouldExist(new IndividualCustomer()));
        Should.NotThrow(() => _rules.LoanSubTypeShouldExist(SubType()));
    }

    // ---- active ----

    [Fact]
    public void CustomerMustBeActive_Throws_ForInactiveCustomer() =>
        Should.Throw<BusinessException>(() => _rules.CustomerMustBeActive(new IndividualCustomer { IsActive = false }));

    [Fact]
    public void CustomerMustBeActive_Accepts_ActiveCustomer() =>
        Should.NotThrow(() => _rules.CustomerMustBeActive(new IndividualCustomer { IsActive = true }));

    [Fact]
    public void LoanSubTypeMustBeActive_Throws_WhenSubTypeIsInactive()
    {
        var subType = SubType();
        subType.IsActive = false;
        Should.Throw<BusinessException>(() => _rules.LoanSubTypeMustBeActive(subType));
    }

    [Fact]
    public void LoanSubTypeMustBeActive_Throws_WhenParentLoanTypeIsInactive()
    {
        var subType = SubType();
        subType.LoanType!.IsActive = false;
        Should.Throw<BusinessException>(() => _rules.LoanSubTypeMustBeActive(subType));
    }

    [Fact]
    public void LoanSubTypeMustBeActive_Throws_WhenLoanTypeWasNotLoaded()
    {
        var subType = SubType();
        subType.LoanType = null;
        Should.Throw<InvalidOperationException>(() => _rules.LoanSubTypeMustBeActive(subType));
    }

    [Fact]
    public void LoanSubTypeMustBeActive_Accepts_ActiveSubTypeAndType() =>
        Should.NotThrow(() => _rules.LoanSubTypeMustBeActive(SubType()));

    // ---- segment ----

    [Fact]
    public void Segment_IndividualCustomer_CanUseIndividualLoanType() =>
        Should.NotThrow(() => _rules.CustomerSegmentMustMatchLoanType(
            new IndividualCustomer(), new LoanType { Segment = CustomerSegment.Individual }));

    [Fact]
    public void Segment_CorporateCustomer_CanUseCorporateLoanType() =>
        Should.NotThrow(() => _rules.CustomerSegmentMustMatchLoanType(
            new CorporateCustomer(), new LoanType { Segment = CustomerSegment.Corporate }));

    [Fact]
    public void Segment_IndividualCustomer_CannotUseCorporateLoanType() =>
        Should.Throw<BusinessException>(() => _rules.CustomerSegmentMustMatchLoanType(
            new IndividualCustomer(), new LoanType { Segment = CustomerSegment.Corporate }));

    [Fact]
    public void Segment_CorporateCustomer_CannotUseIndividualLoanType() =>
        Should.Throw<BusinessException>(() => _rules.CustomerSegmentMustMatchLoanType(
            new CorporateCustomer(), new LoanType { Segment = CustomerSegment.Individual }));

    [Fact]
    public void SegmentOf_UnknownCustomerType_Throws() =>
        Should.Throw<InvalidOperationException>(() => LoanApplicationBusinessRules.SegmentOf(new UnknownCustomer()));

    // ---- limits (inclusive) ----

    [Theory]
    [InlineData(5_000)]
    [InlineData(50_000)]
    [InlineData(100_000)]
    public void Amount_WithinLimits_IsAccepted(decimal amount) =>
        Should.NotThrow(() => _rules.AmountMustBeWithinLimits(SubType(), amount));

    [Theory]
    [InlineData(4_999.99)]
    [InlineData(100_000.01)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Amount_OutsideLimits_IsRejected(decimal amount) =>
        Should.Throw<BusinessException>(() => _rules.AmountMustBeWithinLimits(SubType(), amount));

    [Theory]
    [InlineData(3)]
    [InlineData(24)]
    [InlineData(60)]
    public void Term_WithinLimits_IsAccepted(int months) =>
        Should.NotThrow(() => _rules.TermMustBeWithinLimits(SubType(), months));

    [Theory]
    [InlineData(2)]
    [InlineData(61)]
    [InlineData(0)]
    public void Term_OutsideLimits_IsRejected(int months) =>
        Should.Throw<BusinessException>(() => _rules.TermMustBeWithinLimits(SubType(), months));

    [Fact]
    public void LimitMessages_ContainTheLimits()
    {
        Should.Throw<BusinessException>(() => _rules.AmountMustBeWithinLimits(SubType(), 1)).Message.ShouldContain("5000");
        Should.Throw<BusinessException>(() => _rules.TermMustBeWithinLimits(SubType(), 1)).Message.ShouldContain("60");
    }

    // ---- open application ----

    [Fact]
    public async Task OpenApplication_Throws_WhenOneExists()
    {
        _repository.AnyAsync(Arg.Any<Expression<Func<LoanApplication, bool>>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await Should.ThrowAsync<BusinessException>(() =>
            _rules.CustomerCannotHaveOpenApplicationForSameSubType(Guid.NewGuid(), Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OpenApplication_Predicate_OnlyMatchesSameCustomerSameSubType_InOpenStatuses()
    {
        Expression<Func<LoanApplication, bool>>? predicate = null;
        _repository.AnyAsync(Arg.Do<Expression<Func<LoanApplication, bool>>?>(p => predicate = p), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(false);
        Guid customer = Guid.NewGuid(), subType = Guid.NewGuid();

        await _rules.CustomerCannotHaveOpenApplicationForSameSubType(customer, subType, TestContext.Current.CancellationToken);

        Func<LoanApplication, bool> matches = predicate!.Compile();
        LoanApplication App(Guid c, Guid s, LoanApplicationStatus st) => new() { CustomerId = c, LoanSubTypeId = s, Status = st };

        matches(App(customer, subType, LoanApplicationStatus.Submitted)).ShouldBeTrue();
        matches(App(customer, subType, LoanApplicationStatus.UnderReview)).ShouldBeTrue();
        matches(App(customer, subType, LoanApplicationStatus.Approved)).ShouldBeFalse();
        matches(App(customer, subType, LoanApplicationStatus.Rejected)).ShouldBeFalse();
        matches(App(customer, subType, LoanApplicationStatus.Cancelled)).ShouldBeFalse();
        matches(App(Guid.NewGuid(), subType, LoanApplicationStatus.Submitted)).ShouldBeFalse();
        matches(App(customer, Guid.NewGuid(), LoanApplicationStatus.Submitted)).ShouldBeFalse();
    }

    // ---- status flow ----

    [Theory]
    [InlineData(LoanApplicationStatus.Submitted, LoanApplicationStatus.Submitted, true)]
    [InlineData(LoanApplicationStatus.UnderReview, LoanApplicationStatus.Submitted, false)]
    [InlineData(LoanApplicationStatus.Approved, LoanApplicationStatus.UnderReview, false)]
    [InlineData(LoanApplicationStatus.UnderReview, LoanApplicationStatus.UnderReview, true)]
    public void StatusMustBe_ChecksTheCurrentStatus(LoanApplicationStatus current, LoanApplicationStatus required, bool allowed)
    {
        var application = new LoanApplication { Status = current };

        if (allowed) Should.NotThrow(() => _rules.StatusMustBe(application, required));
        else Should.Throw<BusinessException>(() => _rules.StatusMustBe(application, required));
    }

    [Theory]
    [InlineData(LoanApplicationStatus.Approved)]
    [InlineData(LoanApplicationStatus.Rejected)]
    [InlineData(LoanApplicationStatus.Cancelled)]
    public void ClosedApplications_CannotBeChangedAnymore(LoanApplicationStatus closed)
    {
        var application = new LoanApplication { Status = closed };

        Should.Throw<BusinessException>(() => _rules.StatusMustBe(
            application, LoanApplicationStatus.Submitted, LoanApplicationStatus.UnderReview));
    }

    // ---- approved amount ----

    [Theory]
    [InlineData(1)]
    [InlineData(10_000)]
    [InlineData(50_000)]
    public void ApprovedAmount_UpToRequested_IsAccepted(decimal approved) =>
        Should.NotThrow(() => _rules.ApprovedAmountMustBeValid(new LoanApplication { RequestedAmount = 50_000 }, approved));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(50_000.01)]
    public void ApprovedAmount_ZeroNegativeOrAboveRequested_IsRejected(decimal approved) =>
        Should.Throw<BusinessException>(() => _rules.ApprovedAmountMustBeValid(new LoanApplication { RequestedAmount = 50_000 }, approved));
}
