using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Persistence.EntityConfigurations;
using Persistence.IntegrationTests.Infrastructure;

namespace Persistence.IntegrationTests.Loans;

/// <summary>The database must protect the data even when the business rules are bypassed.</summary>
[Collection(DatabaseCollection.Name)]
public class LoanSchemaTests(SqlServerFixture database) : IntegrationTestBase(database)
{
    private static LoanApplication NewApplication(Guid customerId, string number = "LA-TEST-1") => new()
    {
        ApplicationNumber = number, CustomerId = customerId, LoanSubTypeId = LoanSeedData.PersonalLoanId,
        RequestedAmount = 1_000, TermInMonths = 12, AnnualInterestRate = 48m, Status = LoanApplicationStatus.Submitted,
        CreateDate = DateTime.UtcNow
    };

    [Fact]
    public async Task ApplicationNumber_IsUniqueInTheDatabase()
    {
        Guid customerId = (await Send(TestData.IndividualCreate())).Id;
        await UsingDbContext(async db =>
        {
            db.LoanApplications.Add(NewApplication(customerId));
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        await Should.ThrowAsync<DbUpdateException>(() => UsingDbContext(async db =>
        {
            db.LoanApplications.Add(NewApplication(customerId));
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }));
    }

    [Fact]
    public async Task Application_CannotPointAtAnUnknownCustomerOrSubType()
    {
        await Should.ThrowAsync<DbUpdateException>(() => UsingDbContext(async db =>
        {
            db.LoanApplications.Add(NewApplication(Guid.NewGuid()));
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }));
    }

    [Fact]
    public async Task Customer_WithApplications_CannotBeDeletedFromTheDatabase()
    {
        Guid customerId = (await Send(TestData.CorporateCreate())).Id;
        await UsingDbContext(async db =>
        {
            var application = NewApplication(customerId);
            application.LoanSubTypeId = LoanSeedData.WorkingCapitalLoanId;
            db.LoanApplications.Add(application);
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        await Should.ThrowAsync<DbUpdateException>(() => UsingDbContext(async db =>
        {
            db.CorporateCustomers.Remove((await db.CorporateCustomers.FindAsync([customerId], TestContext.Current.CancellationToken))!);
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }));
    }

    [Fact]
    public async Task LoanType_WithSubTypes_CannotBeDeleted()
    {
        await Should.ThrowAsync<DbUpdateException>(() => UsingDbContext(async db =>
        {
            db.LoanTypes.Remove((await db.LoanTypes.FindAsync([LoanSeedData.IndividualLoanTypeId], TestContext.Current.CancellationToken))!);
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }));
    }

    [Fact]
    public async Task SubTypeName_IsUniqueWithinALoanType()
    {
        await Should.ThrowAsync<DbUpdateException>(() => UsingDbContext(async db =>
        {
            db.LoanSubTypes.Add(new LoanSubType
            {
                LoanTypeId = LoanSeedData.IndividualLoanTypeId, Name = "Personal Loan", CreateDate = DateTime.UtcNow
            });
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }));
    }

    [Fact]
    public async Task Decimals_KeepTheirPrecision()
    {
        Guid customerId = (await Send(TestData.IndividualCreate())).Id;
        var application = NewApplication(customerId);
        application.RequestedAmount = 12_345.67m;
        application.ApprovedAmount = 9_876.54m;
        application.AnnualInterestRate = 42.75m;
        await UsingDbContext(async db =>
        {
            db.LoanApplications.Add(application);
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        var stored = await UsingDbContext(db => db.LoanApplications.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken));

        (stored.RequestedAmount, stored.ApprovedAmount, stored.AnnualInterestRate).ShouldBe((12_345.67m, 9_876.54m, 42.75m));
    }

    [Fact]
    public async Task TwoDecisionsOnTheSameApplication_CannotBothWin()
    {
        Guid customerId = (await Send(TestData.IndividualCreate())).Id;
        Guid id = (await Send(new Application.Features.LoanApplications.Commands.Create.CreateLoanApplicationCommand
        {
            CustomerId = customerId, LoanSubTypeId = LoanSeedData.PersonalLoanId, RequestedAmount = 10_000, TermInMonths = 12
        })).Id;

        // Two staff members open the same application before either one saves.
        using var scopeA = ScopedDb(out var dbA);
        using var scopeB = ScopedDb(out var dbB);
        var forApprover = await dbA.LoanApplications.SingleAsync(a => a.Id == id, TestContext.Current.CancellationToken);
        var forRejecter = await dbB.LoanApplications.SingleAsync(a => a.Id == id, TestContext.Current.CancellationToken);

        forApprover.Status = LoanApplicationStatus.Approved;
        await dbA.SaveChangesAsync(TestContext.Current.CancellationToken);
        forRejecter.Status = LoanApplicationStatus.Rejected;

        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync(TestContext.Current.CancellationToken));
    }
}
