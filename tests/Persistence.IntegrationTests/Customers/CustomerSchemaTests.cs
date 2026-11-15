using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Persistence.IntegrationTests.Infrastructure;

namespace Persistence.IntegrationTests.Customers;

[Collection(DatabaseCollection.Name)]
public class CustomerSchemaTests(SqlServerFixture database) : IntegrationTestBase(database)
{
    [Fact]
    public async Task TheDatabase_HasTheCustomerLoanAndUserTables()
    {
        var tables = await UsingDbContext(db => db.Database
            .SqlQueryRaw<string>("SELECT TABLE_NAME AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME <> '__EFMigrationsHistory' ORDER BY 1")
            .ToListAsync(TestContext.Current.CancellationToken));

        tables.ShouldBe(
            ["ApplicationUsers", "CorporateCustomers", "Customers", "IndividualCustomers", "LoanApplications", "LoanSubTypes", "LoanTypes", "UserSessions", "Users"],
            ignoreOrder: true);
    }

    [Fact]
    public async Task UniqueIndex_OnNationalId_IsEnforcedByTheDatabase()
    {
        // Bypasses the business rules on purpose: the database must still protect the data.
        Func<Task> insertTwice = () => UsingDbContext(async db =>
        {
            db.IndividualCustomers.AddRange(NewIndividual("IND-1"), NewIndividual("IND-2"));
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        await Should.ThrowAsync<DbUpdateException>(insertTwice);
    }

    [Fact]
    public async Task UniqueIndex_OnCustomerNumber_IsEnforcedAcrossBothCustomerTypes()
    {
        Func<Task> insertTwice = () => UsingDbContext(async db =>
        {
            db.IndividualCustomers.Add(NewIndividual("SAME-NUMBER", TestData.NationalId(1)));
            db.CorporateCustomers.Add(new CorporateCustomer { CustomerNumber = "SAME-NUMBER", TaxNumber = TestData.TaxNumber(1) });
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        await Should.ThrowAsync<DbUpdateException>(insertTwice);
    }

    [Fact]
    public async Task TheDetector_RecognisesADuplicateValue_ButNotAnotherDatabaseError()
    {
        using var scope = NewScope();
        var detector = Resolve<BankingSystemCore.Repositories.IUniqueViolationDetector>(scope);

        var duplicate = await Should.ThrowAsync<DbUpdateException>(() => UsingDbContext(async db =>
        {
            db.IndividualCustomers.AddRange(NewIndividual("IND-1"), NewIndividual("IND-2"));
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }));
        var tooLong = await Should.ThrowAsync<DbUpdateException>(() => UsingDbContext(async db =>
        {
            var customer = NewIndividual("IND-3");
            customer.FirstName = new string('x', 500); // longer than the column
            db.IndividualCustomers.Add(customer);
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }));

        detector.IsUniqueViolation(duplicate).ShouldBeTrue();
        detector.IsUniqueViolation(tooLong).ShouldBeFalse();
    }

    private static IndividualCustomer NewIndividual(string customerNumber, string? nationalId = null) => new()
    {
        CustomerNumber = customerNumber,
        NationalId = nationalId ?? TestData.NationalId(1),
        FirstName = "Ali",
        LastName = "Veli",
        CreateDate = DateTime.UtcNow
    };
}
