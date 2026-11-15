using Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Persistence.EntityConfigurations;
using Persistence.IntegrationTests.Infrastructure;

namespace Persistence.IntegrationTests.Security;

/// <summary>
/// User &lt;- ApplicationUser &lt;- Customer &lt;- IndividualCustomer / CorporateCustomer: one customer is one row in
/// every table of its chain, and the database refuses wrong data even when the application code is bypassed.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class UserSchemaTests(SqlServerFixture database) : IntegrationTestBase(database)
{
    private Task<int> Count(string table) => UsingDbContext(db =>
        db.Database.SqlQueryRaw<int>($"SELECT COUNT(*) AS [Value] FROM [{table}]").SingleAsync(TestContext.Current.CancellationToken));

    private Task<List<string>> ColumnsOf(string table) => UsingDbContext(db =>
        db.Database.SqlQueryRaw<string>("SELECT COLUMN_NAME AS [Value] FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = {0}", table)
            .ToListAsync(TestContext.Current.CancellationToken));

    private async Task SaveAsync(params ApplicationUser[] users) => await UsingDbContext(async db =>
    {
        db.ApplicationUsers.AddRange(users);
        return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    });

    private static ApplicationUser Staff(string email = "staff@bank.test") => new()
    {
        Email = email, PasswordHash = "hash", Role = UserRole.BankStaff, CreateDate = DateTime.UtcNow
    };

    // ---- one customer, one row per level ----

    [Fact]
    public async Task AnIndividualCustomer_IsOneRowInEveryTableOfItsChain()
    {
        await Send(TestData.IndividualCreate());

        (await Count("Users"), await Count("ApplicationUsers"), await Count("Customers"), await Count("IndividualCustomers"), await Count("CorporateCustomers"))
            .ShouldBe((1, 1, 1, 1, 0));
    }

    [Fact]
    public async Task ACorporateCustomer_IsOneRowInEveryTableOfItsChain()
    {
        await Send(TestData.CorporateCreate());

        (await Count("Users"), await Count("ApplicationUsers"), await Count("Customers"), await Count("IndividualCustomers"), await Count("CorporateCustomers"))
            .ShouldBe((1, 1, 1, 0, 1));
    }

    [Fact]
    public async Task AStaffUser_StopsAtTheApplicationUsersTable()
    {
        await SaveAsync(Staff());

        (await Count("Users"), await Count("ApplicationUsers"), await Count("Customers")).ShouldBe((1, 1, 0));
    }

    [Fact]
    public async Task EveryLevelHoldsItsOwnColumns_AndTheUsersTableHoldsLoginInformationOnly()
    {
        var users = await ColumnsOf("Users");
        var applicationUsers = await ColumnsOf("ApplicationUsers");
        var customers = await ColumnsOf("Customers");

        // Users: only what the login needs.
        users.ShouldContain("Email");
        users.ShouldContain("PasswordHash");
        users.ShouldNotContain("Role");
        users.ShouldNotContain("BannedDate");
        users.ShouldNotContain("CustomerNumber");
        users.ShouldNotContain("PhoneNumber");
        users.ShouldNotContain("FirstName");

        // ApplicationUsers: what this application adds to a user.
        applicationUsers.ShouldBe(["BanReason", "BannedDate", "Id", "Role"], ignoreOrder: true);

        // Customers: customer data, nothing about the login.
        customers.ShouldContain("CustomerNumber");
        customers.ShouldNotContain("Email");
        customers.ShouldNotContain("PasswordHash");
        customers.ShouldNotContain("Role");
    }

    [Fact]
    public async Task TheCustomerIdIsTheUserId()
    {
        Guid customerId = (await Send(TestData.IndividualCreate())).Id;

        var user = await UsingDbContext(db => Task.FromResult(db.Users.AsNoTracking().Single()));

        user.Id.ShouldBe(customerId);
    }

    [Fact]
    public async Task ACustomerIsAlwaysSavedWithTheCustomerRole()
    {
        await Send(TestData.IndividualCreate());

        var user = await UsingDbContext(db => Task.FromResult(db.ApplicationUsers.AsNoTracking().Single()));

        user.Role.ShouldBe(UserRole.Customer);
        user.ShouldBeOfType<IndividualCustomer>();
    }

    [Fact]
    public async Task ACustomerThatCannotBeCompleted_LeavesNoRowInAnyTable()
    {
        var invalid = new IndividualCustomer
        {
            Email = "broken@bank.test", PasswordHash = "hash", CustomerNumber = "IND-X", NationalId = TestData.NationalId(1),
            FirstName = new string('x', 500), // longer than the column: the last insert of the chain fails
            CreateDate = DateTime.UtcNow
        };

        await Should.ThrowAsync<DbUpdateException>(() => UsingDbContext(async db =>
        {
            db.IndividualCustomers.Add(invalid);
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }));

        (await Count("Users"), await Count("ApplicationUsers"), await Count("Customers"), await Count("IndividualCustomers")).ShouldBe((0, 0, 0, 0));
    }

    // ---- constraints ----

    [Fact]
    public async Task TheEmail_IsUniqueAcrossAllUsers() =>
        await Should.ThrowAsync<DbUpdateException>(() => SaveAsync(Staff("same@bank.test"), Staff("same@bank.test")));

    [Fact]
    public async Task AStaffEmail_CannotBeTakenByACustomer_EvenIfTheApplicationRulesAreBypassed()
    {
        await SaveAsync(Staff(TestData.Email(1)));

        await Should.ThrowAsync<DbUpdateException>(() => UsingDbContext(async db =>
        {
            db.IndividualCustomers.Add(new IndividualCustomer
            {
                Email = TestData.Email(1), PasswordHash = "hash", CustomerNumber = "IND-1", NationalId = TestData.NationalId(1),
                FirstName = "A", LastName = "B", CreateDate = DateTime.UtcNow
            });
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }));
    }

    [Fact]
    public async Task ManyStaffUsers_AreAllowed()
    {
        await SaveAsync(Staff("a@bank.test"), Staff("b@bank.test"), Staff("c@bank.test"));

        (await Count("Users")).ShouldBe(3);
    }

    [Fact]
    public async Task ARowOfALowerLevel_CannotExistWithoutTheRowsAbove()
    {
        // Straight SQL, no application code: a Customers row whose id is no ApplicationUser.
        await Should.ThrowAsync<SqlException>(() => UsingDbContext(db => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Customers (Id, CustomerNumber, PhoneNumber, Address, City, Country) VALUES (NEWID(), 'X', '1', 'a', 'a', 'a')",
            TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task ACustomerWithSessionsOrApplications_CannotBeDeletedFromTheDatabase()
    {
        Guid customerId = (await Send(TestData.IndividualCreate())).Id;
        await Send(new Application.Features.Auth.Commands.Login.LoginCommand { Email = TestData.Email(1), Password = TestData.Password });

        await Should.ThrowAsync<DbUpdateException>(() => UsingDbContext(async db =>
        {
            db.Users.Remove((await db.Users.FindAsync([customerId], TestContext.Current.CancellationToken))!);
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }));
    }

    [Fact]
    public async Task ASessionNeedsAnExistingUser_AndItsRefreshTokenHashIsUnique()
    {
        await Should.ThrowAsync<DbUpdateException>(() => UsingDbContext(async db =>
        {
            db.UserSessions.Add(new UserSession { UserId = Guid.NewGuid(), RefreshTokenHash = new string('A', 64), RefreshTokenExpiresAt = DateTime.UtcNow, CreateDate = DateTime.UtcNow });
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }));

        await SaveAsync(Staff());
        Guid userId = (await UsingDbContext(db => Task.FromResult(db.Users.Single()))).Id;
        UserSession Session() => new() { UserId = userId, RefreshTokenHash = new string('B', 64), RefreshTokenExpiresAt = DateTime.UtcNow, CreateDate = DateTime.UtcNow };

        await UsingDbContext(async db => { db.UserSessions.Add(Session()); return await db.SaveChangesAsync(TestContext.Current.CancellationToken); });
        await Should.ThrowAsync<DbUpdateException>(() => UsingDbContext(async db => { db.UserSessions.Add(Session()); return await db.SaveChangesAsync(TestContext.Current.CancellationToken); }));
    }

    // ---- concurrency ----

    [Fact]
    public async Task AWriteBasedOnStaleData_IsRefused_SoItCannotUndoABan()
    {
        Guid id = (await Send(TestData.IndividualCreate())).Id;
        CurrentUser.AsStaff();

        // A login reads the user while it is still active ...
        using var staleScope = ScopedDb(out var stale);
        var staleUser = await stale.ApplicationUsers.SingleAsync(TestContext.Current.CancellationToken);

        // ... a ban lands in between ...
        await Send(new Application.Features.Users.Commands.Ban.BanUserCommand { Id = id, Reason = "Fraud" });

        // ... and then the login tries to save what it read.
        staleUser.LastLoginDate = DateTime.UtcNow;
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync(TestContext.Current.CancellationToken));

        (await UsingDbContext(db => Task.FromResult(db.ApplicationUsers.AsNoTracking().Single()))).IsActive.ShouldBeFalse();
    }
}
