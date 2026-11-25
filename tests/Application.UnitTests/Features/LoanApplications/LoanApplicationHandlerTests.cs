using System.Linq.Expressions;
using Application.Features.LoanApplications.Commands.Approve;
using Application.Features.LoanApplications.Commands.Cancel;
using Application.Features.LoanApplications.Commands.Create;
using Application.Features.LoanApplications.Commands.Reject;
using Application.Features.LoanApplications.Commands.StartReview;
using Application.Features.LoanApplications.Rules;
using Application.Services.Repositories;
using Application.UnitTests.Support;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Domain.Entities;
using Microsoft.EntityFrameworkCore.Query;
using NSubstitute;

namespace Application.UnitTests.Features.LoanApplications;

public class LoanApplicationHandlerTests
{
    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
    private readonly ILoanSubTypeRepository _subTypes = Substitute.For<ILoanSubTypeRepository>();
    private readonly ILoanApplicationRepository _applications = Substitute.For<ILoanApplicationRepository>();
    private readonly LoanApplicationBusinessRules _rules;
    private readonly TestClock _clock = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public LoanApplicationHandlerTests()
    {
        _rules = new LoanApplicationBusinessRules(_applications);
    }

    // ---- stubs ----

    private void CustomerIs(Customer? customer) =>
        _customers.GetAsync(
                Arg.Any<Expression<Func<Customer, bool>>>(),
                Arg.Any<Func<IQueryable<Customer>, IIncludableQueryable<Customer, object?>>?>(),
                Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(customer);

    private void SubTypeIs(LoanSubType? subType) =>
        _subTypes.GetAsync(
                Arg.Any<Expression<Func<LoanSubType, bool>>>(),
                Arg.Any<Func<IQueryable<LoanSubType>, IIncludableQueryable<LoanSubType, object?>>?>(),
                Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(subType);

    private void ApplicationIs(LoanApplication? application) =>
        _applications.GetAsync(
                Arg.Any<Expression<Func<LoanApplication, bool>>>(),
                Arg.Any<Func<IQueryable<LoanApplication>, IIncludableQueryable<LoanApplication, object?>>?>(),
                Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(application);

    private static LoanSubType PersonalLoan() => new()
    {
        Id = Guid.NewGuid(), Name = "Personal Loan", MinAmount = 5_000, MaxAmount = 100_000,
        MinTermInMonths = 3, MaxTermInMonths = 60, AnnualInterestRate = 48m, IsActive = true,
        LoanType = new LoanType { Segment = CustomerSegment.Individual, IsActive = true }
    };

    private static LoanApplication ApplicationIn(LoanApplicationStatus status) => new()
    {
        Id = Guid.NewGuid(), ApplicationNumber = "LA-1", RequestedAmount = 50_000, Status = status
    };

    private CreateLoanApplicationCommandHandler CreateHandler(FakeCurrentUser? user = null) =>
        new(_customers, _subTypes, _applications, _rules, (user ?? FakeCurrentUser.Staff()).Policy(), _clock);

    private static CreateLoanApplicationCommand Command(decimal amount = 20_000, int term = 24) => new()
    {
        CustomerId = Guid.NewGuid(), LoanSubTypeId = Guid.NewGuid(), RequestedAmount = amount, TermInMonths = term
    };

    // ---- create ----

    [Fact]
    public async Task Create_Valid_SavesASubmittedApplication_WithTheRateOfTheSubType()
    {
        CustomerIs(new IndividualCustomer { IsActive = true });
        SubTypeIs(PersonalLoan());

        var response = await CreateHandler().Handle(Command(), Ct);

        response.Status.ShouldBe(LoanApplicationStatus.Submitted);
        response.ApplicationNumber.ShouldStartWith("LA-");
        response.AnnualInterestRate.ShouldBe(48m);
        await _applications.Received(1).AddAsync(
            Arg.Is<LoanApplication>(a => a.Status == LoanApplicationStatus.Submitted
                                         && a.AnnualInterestRate == 48m
                                         && a.RequestedAmount == 20_000
                                         && a.TermInMonths == 24
                                         && a.ApplicationNumber.StartsWith("LA-")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_TheApplicationNumber_CarriesTheDateOfTheClock()
    {
        CustomerIs(new IndividualCustomer { IsActive = true });
        SubTypeIs(PersonalLoan());

        var response = await CreateHandler().Handle(Command(), Ct);
        response.ApplicationNumber.ShouldStartWith("LA-20261115-");

        _clock.Advance(TimeSpan.FromDays(1));
        (await CreateHandler().Handle(Command(), Ct)).ApplicationNumber.ShouldStartWith("LA-20261116-");
    }

    [Fact]
    public async Task Create_GeneratesDifferentApplicationNumbers()
    {
        CustomerIs(new IndividualCustomer { IsActive = true });
        SubTypeIs(PersonalLoan());

        var first = await CreateHandler().Handle(Command(), Ct);
        var second = await CreateHandler().Handle(Command(), Ct);

        first.ApplicationNumber.ShouldNotBe(second.ApplicationNumber);
    }

    [Fact]
    public async Task Create_IndividualCustomerForCorporateLoan_IsRejected_AndNothingIsSaved()
    {
        CustomerIs(new IndividualCustomer { IsActive = true });
        var corporateLoan = PersonalLoan();
        corporateLoan.LoanType!.Segment = CustomerSegment.Corporate;
        SubTypeIs(corporateLoan);

        await Should.ThrowAsync<BusinessException>(() => CreateHandler().Handle(Command(), Ct));

        await _applications.DidNotReceiveWithAnyArgs().AddAsync(default!, Ct);
    }

    [Fact]
    public async Task Create_UnknownCustomer_IsNotFound()
    {
        CustomerIs(null);
        SubTypeIs(PersonalLoan());

        await Should.ThrowAsync<NotFoundException>(() => CreateHandler().Handle(Command(), Ct));
    }

    [Fact]
    public async Task Create_InactiveCustomer_IsRejected()
    {
        CustomerIs(new IndividualCustomer { IsActive = false });
        SubTypeIs(PersonalLoan());

        await Should.ThrowAsync<BusinessException>(() => CreateHandler().Handle(Command(), Ct));
    }

    [Fact]
    public async Task Create_UnknownSubType_IsNotFound()
    {
        CustomerIs(new IndividualCustomer { IsActive = true });
        SubTypeIs(null);

        await Should.ThrowAsync<NotFoundException>(() => CreateHandler().Handle(Command(), Ct));
    }

    [Theory]
    [InlineData(1_000, 24)]     // amount below minimum
    [InlineData(500_000, 24)]   // amount above maximum
    [InlineData(20_000, 1)]     // term below minimum
    [InlineData(20_000, 120)]   // term above maximum
    public async Task Create_OutsideTheLimitsOfTheSubType_IsRejected(decimal amount, int term)
    {
        CustomerIs(new IndividualCustomer { IsActive = true });
        SubTypeIs(PersonalLoan());

        await Should.ThrowAsync<BusinessException>(() => CreateHandler().Handle(Command(amount, term), Ct));

        await _applications.DidNotReceiveWithAnyArgs().AddAsync(default!, Ct);
    }

    [Fact]
    public async Task Create_WhenAnOpenApplicationExists_IsRejected()
    {
        CustomerIs(new IndividualCustomer { IsActive = true });
        SubTypeIs(PersonalLoan());
        _applications.AnyAsync(Arg.Any<Expression<Func<LoanApplication, bool>>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await Should.ThrowAsync<BusinessException>(() => CreateHandler().Handle(Command(), Ct));
    }

    // ---- start review ----

    [Fact]
    public async Task StartReview_MovesSubmittedToUnderReview()
    {
        ApplicationIs(ApplicationIn(LoanApplicationStatus.Submitted));
        var handler = new StartReviewLoanApplicationCommandHandler(_applications, _rules);

        var response = await handler.Handle(new StartReviewLoanApplicationCommand { Id = Guid.NewGuid() }, Ct);

        response.Status.ShouldBe(LoanApplicationStatus.UnderReview);
        await _applications.Received(1).UpdateAsync(
            Arg.Is<LoanApplication>(a => a.Status == LoanApplicationStatus.UnderReview), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(LoanApplicationStatus.UnderReview)]
    [InlineData(LoanApplicationStatus.Approved)]
    [InlineData(LoanApplicationStatus.Rejected)]
    [InlineData(LoanApplicationStatus.Cancelled)]
    public async Task StartReview_FromAnyOtherStatus_IsRejected(LoanApplicationStatus status)
    {
        ApplicationIs(ApplicationIn(status));
        var handler = new StartReviewLoanApplicationCommandHandler(_applications, _rules);

        await Should.ThrowAsync<BusinessException>(
            () => handler.Handle(new StartReviewLoanApplicationCommand { Id = Guid.NewGuid() }, Ct));

        await _applications.DidNotReceiveWithAnyArgs().UpdateAsync(default!, Ct);
    }

    // ---- approve ----

    [Fact]
    public async Task Approve_UnderReview_RecordsTheDecision()
    {
        ApplicationIs(ApplicationIn(LoanApplicationStatus.UnderReview));
        var handler = new ApproveLoanApplicationCommandHandler(_applications, _rules, _clock);

        var response = await handler.Handle(
            new ApproveLoanApplicationCommand { Id = Guid.NewGuid(), ApprovedAmount = 40_000, Reason = "Good profile" }, Ct);

        response.Status.ShouldBe(LoanApplicationStatus.Approved);
        response.ApprovedAmount.ShouldBe(40_000);
        response.DecisionReason.ShouldBe("Good profile");
        response.DecisionDate.ShouldBe(_clock.Now.UtcDateTime); // the decision is stamped by the injected clock
    }

    [Fact]
    public async Task Approve_BeforeReview_IsRejected()
    {
        ApplicationIs(ApplicationIn(LoanApplicationStatus.Submitted));
        var handler = new ApproveLoanApplicationCommandHandler(_applications, _rules, _clock);

        await Should.ThrowAsync<BusinessException>(
            () => handler.Handle(new ApproveLoanApplicationCommand { Id = Guid.NewGuid(), ApprovedAmount = 10_000 }, Ct));
    }

    [Fact]
    public async Task Approve_MoreThanRequested_IsRejected()
    {
        ApplicationIs(ApplicationIn(LoanApplicationStatus.UnderReview));
        var handler = new ApproveLoanApplicationCommandHandler(_applications, _rules, _clock);

        await Should.ThrowAsync<BusinessException>(
            () => handler.Handle(new ApproveLoanApplicationCommand { Id = Guid.NewGuid(), ApprovedAmount = 50_000.01m }, Ct));

        await _applications.DidNotReceiveWithAnyArgs().UpdateAsync(default!, Ct);
    }

    // ---- reject ----

    [Theory]
    [InlineData(LoanApplicationStatus.Submitted)]
    [InlineData(LoanApplicationStatus.UnderReview)]
    public async Task Reject_OpenApplication_RecordsTheReason(LoanApplicationStatus status)
    {
        ApplicationIs(ApplicationIn(status));
        var handler = new RejectLoanApplicationCommandHandler(_applications, _rules, _clock);

        var response = await handler.Handle(new RejectLoanApplicationCommand { Id = Guid.NewGuid(), Reason = "Too risky" }, Ct);

        response.Status.ShouldBe(LoanApplicationStatus.Rejected);
        response.DecisionReason.ShouldBe("Too risky");
        response.DecisionDate.ShouldBe(_clock.Now.UtcDateTime); // the decision is stamped by the injected clock
        response.ApprovedAmount.ShouldBeNull();
    }

    [Fact]
    public async Task Reject_AlreadyApproved_IsRejected()
    {
        ApplicationIs(ApplicationIn(LoanApplicationStatus.Approved));
        var handler = new RejectLoanApplicationCommandHandler(_applications, _rules, _clock);

        await Should.ThrowAsync<BusinessException>(
            () => handler.Handle(new RejectLoanApplicationCommand { Id = Guid.NewGuid(), Reason = "x" }, Ct));
    }

    // ---- cancel ----

    [Theory]
    [InlineData(LoanApplicationStatus.Submitted)]
    [InlineData(LoanApplicationStatus.UnderReview)]
    public async Task Cancel_OpenApplication_IsCancelled(LoanApplicationStatus status)
    {
        ApplicationIs(ApplicationIn(status));
        var handler = new CancelLoanApplicationCommandHandler(_applications, _rules, FakeCurrentUser.Staff().Policy(), _clock);

        var response = await handler.Handle(new CancelLoanApplicationCommand { Id = Guid.NewGuid() }, Ct);

        response.Status.ShouldBe(LoanApplicationStatus.Cancelled);
    }

    [Theory]
    [InlineData(LoanApplicationStatus.Approved)]
    [InlineData(LoanApplicationStatus.Rejected)]
    [InlineData(LoanApplicationStatus.Cancelled)]
    public async Task Cancel_ClosedApplication_IsRejected(LoanApplicationStatus status)
    {
        ApplicationIs(ApplicationIn(status));
        var handler = new CancelLoanApplicationCommandHandler(_applications, _rules, FakeCurrentUser.Staff().Policy(), _clock);

        await Should.ThrowAsync<BusinessException>(
            () => handler.Handle(new CancelLoanApplicationCommand { Id = Guid.NewGuid() }, Ct));
    }

    [Fact]
    public async Task StatusChanges_OnAnUnknownApplication_AreNotFound()
    {
        ApplicationIs(null);

        await Should.ThrowAsync<NotFoundException>(() =>
            new CancelLoanApplicationCommandHandler(_applications, _rules, FakeCurrentUser.Staff().Policy(), _clock).Handle(new CancelLoanApplicationCommand { Id = Guid.NewGuid() }, Ct));
        await Should.ThrowAsync<NotFoundException>(() =>
            new ApproveLoanApplicationCommandHandler(_applications, _rules, _clock).Handle(new ApproveLoanApplicationCommand { Id = Guid.NewGuid(), ApprovedAmount = 1 }, Ct));
    }
}
