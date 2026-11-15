using System.Net;
using System.Net.Http.Json;
using Application.Features.IndividualCustomers.Commands.Create;
using Domain.Entities;
using Persistence.EntityConfigurations;
using WebApi.IntegrationTests.Infrastructure;

namespace WebApi.IntegrationTests.Controllers;

[Collection(DatabaseCollection.Name)]
public class AuditLogsControllerTests(SqlServerFixture database) : ApiTestBase(database)
{
    private const string Url = "/api/v0/audit-logs";

    private static CreateIndividualCustomerCommand NewCustomer() => new()
    {
        Email = "fatih@bank.test", Password = CustomerPassword, PhoneNumber = "5551112233", Address = "Street 1", City = "Istanbul",
        Country = "Turkey", FirstName = "Fatih", LastName = "Test", NationalId = "10000000146",
        DateOfBirth = new DateOnly(1990, 1, 15), EmploymentStatus = EmploymentStatus.Employed, MonthlyIncome = 75000m
    };

    private async Task<(Guid CustomerId, Guid ApplicationId)> StaffDecidesAsync()
    {
        var created = await PostAsync("/api/v0/individual-customers", NewCustomer());
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        Guid customerId = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();

        var application = await PostAsync("/api/v0/loan-applications",
            new { customerId, loanSubTypeId = LoanSeedData.PersonalLoanId, requestedAmount = 50_000, termInMonths = 12 });
        Guid applicationId = (await ReadJsonAsync(application)).GetProperty("id").GetGuid();

        (await Client.PostAsync($"/api/v0/loan-applications/{applicationId}/start-review", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PostAsync($"/api/v0/loan-applications/{applicationId}/approve", new { approvedAmount = 40_000, reason = "Good profile" }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        return (customerId, applicationId);
    }

    [Fact]
    public async Task StaffCanSeeWhoApprovedWhat_FromWhere()
    {
        var (_, applicationId) = await StaffDecidesAsync();

        var response = await Client.GetAsync($"{Url}?action=LoanApplication.Approve", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await ReadJsonAsync(response);
        page.GetProperty("count").GetInt32().ShouldBe(1);
        var entry = page.GetProperty("items")[0];
        entry.GetProperty("actorEmail").GetString().ShouldBe(StaffEmail);
        entry.GetProperty("actorRole").GetString().ShouldBe("BankStaff");
        entry.GetProperty("targetType").GetString().ShouldBe("LoanApplication");
        entry.GetProperty("targetId").GetGuid().ShouldBe(applicationId);
        entry.GetProperty("detail").GetString().ShouldBe("Approved amount 40000. Good profile");
        entry.GetProperty("succeeded").GetBoolean().ShouldBeTrue();
        entry.GetProperty("traceId").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task TheTrail_ShowsLoginsAndRefusals_ButNeverAPassword()
    {
        await StaffDecidesAsync();
        await AnonymousClient.PostAsJsonAsync("/api/v0/auth/login", new { email = "fatih@bank.test", password = "Wrong-Password-1" }, Json, Ct);

        var all = await Client.GetAsync($"{Url}?size=100", Ct);

        string text = await all.Content.ReadAsStringAsync(Ct);
        text.ShouldContain("Auth.Login.Succeeded"); // the staff member of the test setup
        text.ShouldContain("Auth.Login.Failed");
        text.ShouldContain("InvalidPassword");
        text.ShouldNotContain("Wrong-Password-1");
        text.ShouldNotContain(CustomerPassword);
        text.ShouldNotContain(StaffPassword);
        text.ShouldNotContain("PBKDF2");
        text.ShouldNotContain("10000000146"); // national ID
    }

    [Fact]
    public async Task FiltersAndPaging_Work()
    {
        var (customerId, _) = await StaffDecidesAsync();

        var ofCustomer = await ReadJsonAsync(await Client.GetAsync($"{Url}?targetId={customerId}", Ct));
        var failures = await ReadJsonAsync(await Client.GetAsync($"{Url}?succeeded=false", Ct));
        var firstPage = await ReadJsonAsync(await Client.GetAsync($"{Url}?index=0&size=2", Ct));

        ofCustomer.GetProperty("count").GetInt32().ShouldBe(1); // the customer was created
        failures.GetProperty("count").GetInt32().ShouldBe(0);
        firstPage.GetProperty("items").GetArrayLength().ShouldBe(2);
        firstPage.GetProperty("hasNext").GetBoolean().ShouldBeTrue();
    }

    [Theory]
    [InlineData("size=0")]
    [InlineData("size=101")]
    [InlineData("index=-1")]
    [InlineData("from=2026-02-01&to=2026-01-01")]
    public async Task BadParameters_Return400(string query) =>
        (await Client.GetAsync($"{Url}?{query}", Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

    [Fact]
    public async Task OnlyStaffMayRead()
    {
        await PostAsync("/api/v0/individual-customers", NewCustomer());
        using var customer = await CustomerClientAsync("fatih@bank.test");

        (await customer.GetAsync(Url, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await AnonymousClient.GetAsync(Url, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
