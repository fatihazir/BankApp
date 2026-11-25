using Application.Features.CorporateCustomers.Commands.Create;
using Application.Features.IndividualCustomers.Commands.Delete;
using Application.Features.IndividualCustomers.Commands.Update;
using Application.Features.LoanApplications.Commands.Approve;
using Application.Features.LoanApplications.Commands.Cancel;
using Application.Features.LoanApplications.Commands.Create;
using Application.Features.LoanApplications.Commands.Reject;
using Application.Features.LoanApplications.Commands.StartReview;
using Application.Features.LoanApplications.Queries.GetById;
using Application.Features.LoanApplications.Queries.GetList;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Domain.Entities;
using Persistence.EntityConfigurations;
using Persistence.IntegrationTests.Infrastructure;

namespace Persistence.IntegrationTests.Loans;

[Collection(DatabaseCollection.Name)]
public class LoanApplicationTests(SqlServerFixture database) : IntegrationTestBase(database)
{
    private async Task<Guid> NewIndividualAsync(int seed = 1) => (await Send(TestData.IndividualCreate(seed))).Id;

    private async Task<Guid> NewCorporateAsync(int seed = 1) => (await Send(TestData.CorporateCreate(seed))).Id;

    private static CreateLoanApplicationCommand Apply(Guid customerId, Guid subTypeId, decimal amount = 100_000, int term = 24) => new()
    {
        CustomerId = customerId, LoanSubTypeId = subTypeId, RequestedAmount = amount, TermInMonths = term, Purpose = "Test"
    };

    private async Task<Guid> SubmitAsync(Guid? customerId = null, Guid? subTypeId = null)
    {
        Guid customer = customerId ?? await NewIndividualAsync();
        return (await Send(Apply(customer, subTypeId ?? LoanSeedData.PersonalLoanId))).Id;
    }

    // ---- creating applications ----

    [Fact]
    public async Task Individual_CanApplyForAnIndividualLoan()
    {
        Guid customerId = await NewIndividualAsync();

        var created = await Send(Apply(customerId, LoanSeedData.PersonalLoanId, 100_000, 24));

        created.Status.ShouldBe(LoanApplicationStatus.Submitted);
        created.ApplicationNumber.ShouldStartWith("LA-");
        created.AnnualInterestRate.ShouldBe(48.00m);
        created.CustomerId.ShouldBe(customerId);
    }

    [Fact]
    public async Task Corporate_CanApplyForACorporateLoan()
    {
        Guid customerId = await NewCorporateAsync();

        var created = await Send(Apply(customerId, LoanSeedData.WorkingCapitalLoanId, 1_000_000, 12));

        created.Status.ShouldBe(LoanApplicationStatus.Submitted);
        created.AnnualInterestRate.ShouldBe(42.00m);
    }

    [Fact]
    public async Task Individual_CannotApplyForACorporateLoan()
    {
        Guid customerId = await NewIndividualAsync();

        await Should.ThrowAsync<BusinessException>(() => Send(Apply(customerId, LoanSeedData.WorkingCapitalLoanId, 1_000_000, 12)));
    }

    [Fact]
    public async Task Corporate_CannotApplyForAnIndividualLoan()
    {
        Guid customerId = await NewCorporateAsync();

        await Should.ThrowAsync<BusinessException>(() => Send(Apply(customerId, LoanSeedData.PersonalLoanId)));
    }

    [Fact]
    public async Task EveryIndividualSubType_AcceptsIndividuals_AndEveryCorporateSubType_AcceptsCorporates()
    {
        Guid individual = await NewIndividualAsync();
        Guid corporate = await NewCorporateAsync();

        await Send(Apply(individual, LoanSeedData.PersonalLoanId, 10_000, 12));
        await Send(Apply(individual, LoanSeedData.MortgageLoanId, 500_000, 120));
        await Send(Apply(individual, LoanSeedData.AutoLoanId, 100_000, 36));
        await Send(Apply(corporate, LoanSeedData.WorkingCapitalLoanId, 1_000_000, 12));
        await Send(Apply(corporate, LoanSeedData.InvestmentLoanId, 5_000_000, 60));
        await Send(Apply(corporate, LoanSeedData.CommercialLoanId, 1_000_000, 24));

        (await Send(new GetListLoanApplicationQuery { Size = 50 })).Count.ShouldBe(6);
    }

    [Fact]
    public async Task SoftDeletedCustomer_CannotApply()
    {
        Guid customerId = await NewIndividualAsync();
        await Send(new DeleteIndividualCustomerCommand { Id = customerId });

        await Should.ThrowAsync<NotFoundException>(() => Send(Apply(customerId, LoanSeedData.PersonalLoanId)));
    }

    [Fact]
    public async Task InactiveCustomer_CannotApply()
    {
        Guid customerId = await NewIndividualAsync();
        await Send(new UpdateIndividualCustomerCommand
        {
            Id = customerId, PhoneNumber = "1", Address = "a", City = "a", Country = "a", IsActive = false,
            FirstName = "Ali", LastName = "Veli", NationalId = TestData.NationalId(1), DateOfBirth = new DateOnly(1990, 1, 1),
            EmploymentStatus = EmploymentStatus.Employed
        });

        await Should.ThrowAsync<BusinessException>(() => Send(Apply(customerId, LoanSeedData.PersonalLoanId)));
    }

    [Fact]
    public async Task SecondOpenApplicationForTheSameSubType_IsRejected_ButAnotherSubTypeIsFine()
    {
        Guid customerId = await NewIndividualAsync();
        await Send(Apply(customerId, LoanSeedData.PersonalLoanId));

        await Should.ThrowAsync<BusinessException>(() => Send(Apply(customerId, LoanSeedData.PersonalLoanId)));
        await Send(Apply(customerId, LoanSeedData.AutoLoanId, 100_000, 24));
    }

    [Fact]
    public async Task AfterTheFirstApplicationIsClosed_ANewOneIsAllowed()
    {
        Guid customerId = await NewIndividualAsync();
        Guid first = await SubmitAsync(customerId);
        await Send(new RejectLoanApplicationCommand { Id = first, Reason = "No" });

        (await Send(Apply(customerId, LoanSeedData.PersonalLoanId))).Status.ShouldBe(LoanApplicationStatus.Submitted);
    }

    [Fact]
    public async Task TwoDifferentCustomers_CanApplyForTheSameSubType()
    {
        await SubmitAsync(await NewIndividualAsync(1));
        await SubmitAsync(await NewIndividualAsync(2));

        (await Send(new GetListLoanApplicationQuery())).Count.ShouldBe(2);
    }

    [Fact]
    public async Task TheInterestRateOfTheApplication_IsASnapshot()
    {
        Guid customerId = await NewIndividualAsync();
        Guid applicationId = (await Send(Apply(customerId, LoanSeedData.PersonalLoanId))).Id;

        async Task SetRate(decimal rate) => await UsingDbContext(async db =>
        {
            var subType = await db.LoanSubTypes.FindAsync([LoanSeedData.PersonalLoanId], TestContext.Current.CancellationToken);
            subType!.AnnualInterestRate = rate;
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        await SetRate(99.99m);

        (await Send(new GetByIdLoanApplicationQuery { Id = applicationId })).AnnualInterestRate.ShouldBe(48.00m);
    }

    // ---- decision flow ----

    [Fact]
    public async Task FullFlow_Submit_Review_Approve()
    {
        Guid id = await SubmitAsync();

        var review = await Send(new StartReviewLoanApplicationCommand { Id = id });
        review.Status.ShouldBe(LoanApplicationStatus.UnderReview);

        var approved = await Send(new ApproveLoanApplicationCommand { Id = id, ApprovedAmount = 80_000, Reason = "OK" });
        approved.Status.ShouldBe(LoanApplicationStatus.Approved);
        approved.ApprovedAmount.ShouldBe(80_000);
        approved.DecisionDate.ShouldNotBeNull();
        approved.UpdateDate.ShouldNotBeNull();

        var stored = await Send(new GetByIdLoanApplicationQuery { Id = id });
        stored.Status.ShouldBe(LoanApplicationStatus.Approved);
        stored.ApprovedAmount.ShouldBe(80_000);
        stored.DecisionReason.ShouldBe("OK");
        stored.RequestedAmount.ShouldBe(100_000);
    }

    [Fact]
    public async Task Approve_BeforeReview_IsRejected_AndTheStatusStaysSubmitted()
    {
        Guid id = await SubmitAsync();

        await Should.ThrowAsync<BusinessException>(() => Send(new ApproveLoanApplicationCommand { Id = id, ApprovedAmount = 10_000 }));

        (await Send(new GetByIdLoanApplicationQuery { Id = id })).Status.ShouldBe(LoanApplicationStatus.Submitted);
    }

    [Fact]
    public async Task Reject_RecordsTheReason()
    {
        Guid id = await SubmitAsync();

        var rejected = await Send(new RejectLoanApplicationCommand { Id = id, Reason = "Debt ratio too high" });

        rejected.Status.ShouldBe(LoanApplicationStatus.Rejected);
        (await Send(new GetByIdLoanApplicationQuery { Id = id })).DecisionReason.ShouldBe("Debt ratio too high");
    }

    // ---- reading ----

    [Fact]
    public async Task GetById_ReturnsNamesOfCustomerAndLoanType()
    {
        Guid customerId = await NewCorporateAsync();
        Guid id = (await Send(Apply(customerId, LoanSeedData.InvestmentLoanId, 5_000_000, 60))).Id;

        var application = await Send(new GetByIdLoanApplicationQuery { Id = id });

        application.CustomerNumber.ShouldStartWith("COR-");
        application.LoanSubTypeName.ShouldBe("Investment Loan");
        application.LoanTypeName.ShouldBe("Corporate Loan");
        application.Segment.ShouldBe(CustomerSegment.Corporate);
        application.TermInMonths.ShouldBe(60);
    }

    [Fact]
    public async Task GetById_Unknown_IsNotFound() =>
        await Should.ThrowAsync<NotFoundException>(() => Send(new GetByIdLoanApplicationQuery { Id = Guid.NewGuid() }));

    [Fact]
    public async Task GetList_FiltersByCustomerAndStatus_AndPages()
    {
        Guid ali = await NewIndividualAsync(1);
        Guid veli = await NewIndividualAsync(2);
        Guid aliPersonal = await SubmitAsync(ali, LoanSeedData.PersonalLoanId);
        await Send(Apply(ali, LoanSeedData.AutoLoanId, 100_000, 24));
        await SubmitAsync(veli, LoanSeedData.PersonalLoanId);
        await Send(new RejectLoanApplicationCommand { Id = aliPersonal, Reason = "No" });

        (await Send(new GetListLoanApplicationQuery { Size = 50 })).Count.ShouldBe(3);
        (await Send(new GetListLoanApplicationQuery { CustomerId = ali })).Count.ShouldBe(2);
        (await Send(new GetListLoanApplicationQuery { Status = LoanApplicationStatus.Rejected })).Count.ShouldBe(1);
        (await Send(new GetListLoanApplicationQuery { CustomerId = veli, Status = LoanApplicationStatus.Rejected })).Count.ShouldBe(0);

        var firstPage = await Send(new GetListLoanApplicationQuery { Size = 2 });
        firstPage.Items.Count.ShouldBe(2);
        firstPage.Pages.ShouldBe(2);
        firstPage.HasNext.ShouldBeTrue();
        firstPage.Items.ShouldAllBe(i => i.LoanSubTypeName != "");
    }

    [Fact]
    public async Task GetList_IncludesTheCustomerAndTheLoanType()
    {
        Guid person = await NewIndividualAsync(1);
        Guid company = await NewCorporateAsync(1);
        await Send(Apply(person, LoanSeedData.MortgageLoanId, 500_000, 120));
        await Send(Apply(company, LoanSeedData.InvestmentLoanId, 5_000_000, 60));

        var items = (await Send(new GetListLoanApplicationQuery { Size = 10 })).Items;

        var individual = items.Single(i => i.CustomerId == person);
        individual.CustomerName.ShouldBe("Ali Veli");
        individual.CustomerNumber.ShouldStartWith("IND-");
        individual.LoanTypeName.ShouldBe("Individual Loan");
        individual.Segment.ShouldBe(CustomerSegment.Individual);
        individual.LoanSubTypeName.ShouldBe("Mortgage Loan");

        var corporate = items.Single(i => i.CustomerId == company);
        corporate.CustomerName.ShouldBe("Acme");
        corporate.CustomerNumber.ShouldStartWith("COR-");
        corporate.LoanTypeName.ShouldBe("Corporate Loan");
        corporate.Segment.ShouldBe(CustomerSegment.Corporate);
    }

    [Fact]
    public async Task GetList_CustomerFilterAndPaging_WorkTogether()
    {
        Guid customerId = await NewIndividualAsync(1);
        Guid other = await NewIndividualAsync(2);
        await Send(Apply(customerId, LoanSeedData.PersonalLoanId));
        await Send(Apply(customerId, LoanSeedData.AutoLoanId, 100_000, 24));
        await Send(Apply(customerId, LoanSeedData.MortgageLoanId, 500_000, 120));
        await Send(Apply(other, LoanSeedData.PersonalLoanId));

        var page1 = await Send(new GetListLoanApplicationQuery { CustomerId = customerId, Index = 0, Size = 2 });
        var page2 = await Send(new GetListLoanApplicationQuery { CustomerId = customerId, Index = 1, Size = 2 });

        (page1.Count, page1.Pages, page1.Items.Count).ShouldBe((3, 2, 2));
        page2.Items.Count.ShouldBe(1);
        page1.Items.Concat(page2.Items).Select(i => i.Id).Distinct().Count().ShouldBe(3);
        page1.Items.Concat(page2.Items).ShouldAllBe(i => i.CustomerId == customerId);
    }

    [Fact]
    public async Task GetById_IncludesTheCustomerName_AndTheCalculatedPayment()
    {
        Guid customerId = await NewIndividualAsync();
        Guid id = (await Send(Apply(customerId, LoanSeedData.PersonalLoanId, 100_000, 12))).Id;

        var application = await Send(new GetByIdLoanApplicationQuery { Id = id });

        application.CustomerName.ShouldBe("Ali Veli");
        application.MonthlyPayment.ShouldBe(Application.Services.LoanCalculation.LoanCalculator.Calculate(100_000m, 48m, 12).MonthlyPayment);
        application.TotalRepayment.ShouldBeGreaterThan(100_000m);
        (application.TotalRepayment - application.TotalInterest).ShouldBe(100_000m);
    }

    [Fact]
    public async Task Created_ResponseContainsTheCalculation_AndGetByIdUsesTheApprovedAmountAfterApproval()
    {
        Guid customerId = await NewIndividualAsync();
        var created = await Send(Apply(customerId, LoanSeedData.PersonalLoanId, 100_000, 12));
        created.MonthlyPayment.ShouldBeGreaterThan(0m);
        created.TotalRepayment.ShouldBeGreaterThan(created.TotalInterest);

        await Send(new StartReviewLoanApplicationCommand { Id = created.Id });
        await Send(new ApproveLoanApplicationCommand { Id = created.Id, ApprovedAmount = 50_000 });

        var approved = await Send(new GetByIdLoanApplicationQuery { Id = created.Id });
        approved.MonthlyPayment.ShouldBeLessThan(created.MonthlyPayment);
        (approved.TotalRepayment - approved.TotalInterest).ShouldBe(50_000m);
    }

    [Fact]
    public async Task Calculate_ReturnsPaymentAndSchedule_WithoutCreatingAnApplication()
    {
        var result = await Send(new Application.Features.LoanApplications.Queries.Calculate.CalculateLoanQuery
        {
            LoanSubTypeId = LoanSeedData.PersonalLoanId, Amount = 120_000, TermInMonths = 24
        });

        result.AnnualInterestRate.ShouldBe(48.00m);
        result.Schedule.Count.ShouldBe(24);
        result.Schedule[^1].RemainingBalance.ShouldBe(0m);
        (result.TotalRepayment - result.TotalInterest).ShouldBe(120_000m);
        (await Send(new GetListLoanApplicationQuery())).Count.ShouldBe(0);
    }

    [Fact]
    public async Task GetList_ReturnsNewestFirst()
    {
        Guid customerId = await NewIndividualAsync();
        await Send(Apply(customerId, LoanSeedData.PersonalLoanId));
        Clock.Advance(TimeSpan.FromSeconds(1));
        Guid newest = (await Send(Apply(customerId, LoanSeedData.AutoLoanId, 100_000, 24))).Id;

        (await Send(new GetListLoanApplicationQuery())).Items[0].Id.ShouldBe(newest);
    }
}
