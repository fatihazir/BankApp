using Application.Features.LoanApplications.Commands.Create;
using Application.Features.LoanApplications.Queries.Calculate;
using Application.Features.LoanSubTypes.Commands.Create;
using Application.Features.LoanTypes.Commands.Create;
using Application.Features.LoanTypes.Queries.GetList;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Persistence.EntityConfigurations;
using Persistence.IntegrationTests.Infrastructure;

namespace Persistence.IntegrationTests.Loans;

[Collection(DatabaseCollection.Name)]
public class LoanTypeCreationTests(SqlServerFixture database) : IntegrationTestBase(database)
{
    private static CreateLoanSubTypeCommand NewSubType(Guid loanTypeId, string name = "Education Loan") => new()
    {
        LoanTypeId = loanTypeId, Name = name, MinAmount = 10_000, MaxAmount = 500_000,
        MinTermInMonths = 6, MaxTermInMonths = 48, AnnualInterestRate = 39.5m
    };

    [Fact]
    public async Task CreatedLoanType_AppearsInTheList_WithoutSubTypesYet()
    {
        var created = await Send(new CreateLoanTypeCommand { Name = "Special Loan", Segment = CustomerSegment.Individual });

        created.IsActive.ShouldBeTrue();
        created.CreateDate.ShouldNotBe(default);
        var listed = (await Send(new GetListLoanTypeQuery { Segment = CustomerSegment.Individual })).Items;
        listed.Select(t => t.Name).ShouldContain("Special Loan");
        listed.Single(t => t.Name == "Special Loan").SubTypes.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoanType_WithADuplicateName_IsRejected_EvenInAnotherSegment()
    {
        await Should.ThrowAsync<BusinessException>(
            () => Send(new CreateLoanTypeCommand { Name = "Individual Loan", Segment = CustomerSegment.Corporate }));
    }

    [Fact]
    public async Task LoanType_WithInvalidData_FailsValidation() =>
        await Should.ThrowAsync<ValidationException>(() => Send(new CreateLoanTypeCommand { Name = "" }));

    [Fact]
    public async Task CreatedSubType_AppearsUnderItsType_AndCanBeAppliedFor()
    {
        var type = await Send(new CreateLoanTypeCommand { Name = "Special Loan", Segment = CustomerSegment.Individual });
        var subType = await Send(NewSubType(type.Id));
        subType.LoanTypeId.ShouldBe(type.Id);
        subType.IsActive.ShouldBeTrue();

        var listed = (await Send(new GetListLoanTypeQuery { Segment = CustomerSegment.Individual })).Items.Single(t => t.Id == type.Id);
        listed.SubTypes.Single().Name.ShouldBe("Education Loan");
        listed.SubTypes.Single().AnnualInterestRate.ShouldBe(39.5m);

        Guid customerId = (await Send(TestData.IndividualCreate())).Id;
        var application = await Send(new CreateLoanApplicationCommand
        {
            CustomerId = customerId, LoanSubTypeId = subType.Id, RequestedAmount = 100_000, TermInMonths = 12
        });
        application.AnnualInterestRate.ShouldBe(39.5m);

        var calculation = await Send(new CalculateLoanQuery { LoanSubTypeId = subType.Id, Amount = 100_000, TermInMonths = 12 });
        calculation.MonthlyPayment.ShouldBe(application.MonthlyPayment);
    }

    [Fact]
    public async Task SubTypeOfACorporateType_IsNotAvailableToIndividuals()
    {
        var type = await Send(new CreateLoanTypeCommand { Name = "Corporate Special", Segment = CustomerSegment.Corporate });
        var subType = await Send(NewSubType(type.Id));
        Guid individualId = (await Send(TestData.IndividualCreate())).Id;

        await Should.ThrowAsync<BusinessException>(() => Send(new CreateLoanApplicationCommand
        {
            CustomerId = individualId, LoanSubTypeId = subType.Id, RequestedAmount = 100_000, TermInMonths = 12
        }));
    }

    [Fact]
    public async Task SubType_NameMustBeUniqueWithinTheType_ButCanRepeatInAnotherType()
    {
        await Send(NewSubType(LoanSeedData.IndividualLoanTypeId, "Education Loan"));

        await Should.ThrowAsync<BusinessException>(() => Send(NewSubType(LoanSeedData.IndividualLoanTypeId, "Education Loan")));
        await Send(NewSubType(LoanSeedData.CorporateLoanTypeId, "Education Loan"));
    }

    [Fact]
    public async Task SubType_UnderAnUnknownType_IsNotFound() =>
        await Should.ThrowAsync<NotFoundException>(() => Send(NewSubType(Guid.NewGuid())));

    [Fact]
    public async Task SubType_UnderAnInactiveType_IsRejected()
    {
        await UsingDbContext(async db =>
        {
            (await db.LoanTypes.FindAsync([LoanSeedData.CorporateLoanTypeId], TestContext.Current.CancellationToken))!.IsActive = false;
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        await Should.ThrowAsync<BusinessException>(() => Send(NewSubType(LoanSeedData.CorporateLoanTypeId)));
    }

    [Fact]
    public async Task SubType_WithInconsistentLimits_FailsValidation()
    {
        var command = NewSubType(LoanSeedData.IndividualLoanTypeId);
        command.MaxAmount = command.MinAmount - 1;

        await Should.ThrowAsync<ValidationException>(() => Send(command));
    }

    [Fact]
    public async Task SubTypeValues_ArePersistedExactly()
    {
        var created = await Send(NewSubType(LoanSeedData.IndividualLoanTypeId));

        var stored = await UsingDbContext(db => db.LoanSubTypes.AsNoTracking().SingleAsync(s => s.Id == created.Id, TestContext.Current.CancellationToken));

        (stored.MinAmount, stored.MaxAmount, stored.MinTermInMonths, stored.MaxTermInMonths, stored.AnnualInterestRate)
            .ShouldBe((10_000m, 500_000m, 6, 48, 39.5m));
    }
}
