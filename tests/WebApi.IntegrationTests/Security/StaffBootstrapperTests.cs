using Application.Services.Repositories;
using BankingSystemCore.Security.Hashing;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Persistence.Contexts;
using WebApi.Infrastructure;
using WebApi.IntegrationTests.Infrastructure;

namespace WebApi.IntegrationTests.Security;

[Collection(DatabaseCollection.Name)]
public class StaffBootstrapperTests(SqlServerFixture database) : ApiTestBase(database)
{
    private static IConfiguration Config(string? email, string? password) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Security:BootstrapStaff:Email"] = email,
            ["Security:BootstrapStaff:Password"] = password
        })
        .Build();

    private async Task<List<ApplicationUser>> StaffUsers()
    {
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<BankAppDbContext>().ApplicationUsers
            .Where(u => u.Role == UserRole.BankStaff).AsNoTracking().ToListAsync(Ct);
    }

    private async Task RemoveAllUsers()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankAppDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM UserSessions; DELETE FROM ApplicationUsers; DELETE FROM Users;", Ct);
    }

    [Fact]
    public async Task CreatesTheFirstStaffUser_WhenNoneExists_AndItCanLogin()
    {
        await RemoveAllUsers();

        await StaffBootstrapper.EnsureAsync(Services, Config(" First@Bank.test ", "First-Passw0rd-1"), Ct);

        var staff = (await StaffUsers()).ShouldHaveSingleItem();
        staff.Email.ShouldBe("first@bank.test");
        staff.PasswordHash.ShouldNotContain("First-Passw0rd-1");
        (await LoginAsync("first@bank.test", "First-Passw0rd-1")).ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task DoesNothing_WhenAStaffUserExists_EvenWithDifferentSettings()
    {
        await StaffBootstrapper.EnsureAsync(Services, Config("another@bank.test", "Another-Passw0rd-1"), Ct);
        await StaffBootstrapper.EnsureAsync(Services, Config("another@bank.test", "Another-Passw0rd-1"), Ct);

        (await StaffUsers()).Select(u => u.Email).ShouldBe([StaffEmail]);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("a@bank.test", null)]
    [InlineData(null, "Some-Passw0rd-1")]
    [InlineData("", "Some-Passw0rd-1")]
    [InlineData("a@bank.test", "  ")]
    public async Task DoesNothing_WhenTheSettingsAreIncomplete(string? email, string? password)
    {
        await RemoveAllUsers();

        await StaffBootstrapper.EnsureAsync(Services, Config(email, password), Ct);

        (await StaffUsers()).ShouldBeEmpty();
    }

    [Fact]
    public async Task RunningItTwice_CreatesOnlyOneUser()
    {
        await RemoveAllUsers();
        var config = Config("once@bank.test", "Once-Passw0rd-1");

        await StaffBootstrapper.EnsureAsync(Services, config, Ct);
        await StaffBootstrapper.EnsureAsync(Services, config, Ct);

        (await StaffUsers()).Count.ShouldBe(1);
    }
}
