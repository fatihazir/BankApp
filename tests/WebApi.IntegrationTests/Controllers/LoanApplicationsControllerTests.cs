using System.Net;
using System.Text.Json;
using Application.Features.CorporateCustomers.Commands.Create;
using Application.Features.IndividualCustomers.Commands.Create;
using Application.Features.LoanApplications.Commands;
using Application.Features.LoanApplications.Commands.Approve;
using Application.Features.LoanApplications.Commands.Create;
using Application.Features.LoanApplications.Commands.Reject;
using Application.Features.LoanApplications.Queries.GetById;
using Application.Features.LoanApplications.Queries.GetList;
using BankingSystemCore.Repositories;
using Domain.Entities;
using Persistence.EntityConfigurations;
using WebApi.IntegrationTests.Infrastructure;

namespace WebApi.IntegrationTests.Controllers;

[Collection(DatabaseCollection.Name)]
public class LoanApplicationsControllerTests(SqlServerFixture database) : ApiTestBase(database)
{
    private const string Url = "/api/v0/loan-applications";

    private async Task<Guid> NewIndividualAsync(string nationalId = "10000000146", string firstName = "Fatih")
    {
        var response = await PostAsync("/api/v0/individual-customers", new CreateIndividualCustomerCommand
        {
            Email = $"{nationalId}@bank.test", Password = CustomerPassword, PhoneNumber = "5551112233", Address = "Street", City = "Istanbul", Country = "Turkey",
            FirstName = firstName, LastName = "Yilmaz", NationalId = nationalId, DateOfBirth = new DateOnly(1990, 1, 1),
            EmploymentStatus = EmploymentStatus.Employed, MonthlyIncome = 50_000
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> NewCorporateAsync()
    {
        var response = await PostAsync("/api/v0/corporate-customers", new CreateCorporateCustomerCommand
        {
            Email = "company@bank.test", Password = CustomerPassword, PhoneNumber = "2125551234", Address = "Plaza", City = "Istanbul", Country = "Turkey",
            CompanyName = "Acme", TaxNumber = "1234567890", TaxOffice = "Central Tax Office", FoundationDate = new DateOnly(2010, 1, 1),
            EmployeeCount = 10, AnnualRevenue = 1_000_000, AuthorizedPersonName = "Boss"
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private static CreateLoanApplicationCommand Apply(Guid customerId, Guid subTypeId, decimal amount = 100_000, int term = 24) => new()
    {
        CustomerId = customerId, LoanSubTypeId = subTypeId, RequestedAmount = amount, TermInMonths = term
    };

    private async Task<CreatedLoanApplicationResponse> SubmitAsync(Guid customerId, Guid? subTypeId = null)
    {
        var response = await PostAsync(Url, Apply(customerId, subTypeId ?? LoanSeedData.PersonalLoanId));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await ReadAsync<CreatedLoanApplicationResponse>(response);
    }

    // ---- create ----

    [Fact]
    public async Task Post_Returns201_WithLocation_AndTheCalculation()
    {
        Guid customerId = await NewIndividualAsync();

        var response = await PostAsync(Url, Apply(customerId, LoanSeedData.PersonalLoanId));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await ReadAsync<CreatedLoanApplicationResponse>(response);
        created.ApplicationNumber.ShouldStartWith("LA-");
        created.Status.ShouldBe(LoanApplicationStatus.Submitted);
        created.MonthlyPayment.ShouldBeGreaterThan(0m);
        response.Headers.Location!.ToString().ShouldEndWith($"{Url}/{created.Id}");
        (await ReadJsonAsync(await Client.GetAsync(response.Headers.Location, Ct))).GetProperty("status").GetString().ShouldBe("Submitted");
    }

    [Fact]
    public async Task Post_IndividualForACorporateLoan_Returns400_BusinessProblem()
    {
        Guid customerId = await NewIndividualAsync();

        var response = await PostAsync(Url, Apply(customerId, LoanSeedData.WorkingCapitalLoanId, 1_000_000, 12));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("type").GetString().ShouldBe("urn:problem:business");
    }

    [Fact]
    public async Task Post_CorporateForACorporateLoan_IsAccepted()
    {
        Guid customerId = await NewCorporateAsync();

        var response = await PostAsync(Url, Apply(customerId, LoanSeedData.WorkingCapitalLoanId, 1_000_000, 12));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_UnknownCustomer_Returns404()
    {
        var response = await PostAsync(Url, Apply(Guid.NewGuid(), LoanSeedData.PersonalLoanId));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReadJsonAsync(response)).GetProperty("type").GetString().ShouldBe("urn:problem:not-found");
    }

    [Fact]
    public async Task Post_AmountOutsideTheLimits_Returns400_WithTheLimitsInTheMessage()
    {
        Guid customerId = await NewIndividualAsync();

        var response = await PostAsync(Url, Apply(customerId, LoanSeedData.PersonalLoanId, 1, 12));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("detail").GetString()!.ShouldContain("5000");
    }

    [Fact]
    public async Task Post_WithEmptyBody_Returns400_ValidationProblem()
    {
        var response = await PostAsync(Url, new { });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var json = await ReadJsonAsync(response);
        json.GetProperty("type").GetString().ShouldBe("urn:problem:validation");
        var properties = json.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("property").GetString()).ToList();
        properties.ShouldContain("CustomerId");
        properties.ShouldContain("LoanSubTypeId");
        properties.ShouldContain("RequestedAmount");
    }

    [Fact]
    public async Task Post_SecondOpenApplicationForTheSameSubType_Returns400()
    {
        Guid customerId = await NewIndividualAsync();
        await SubmitAsync(customerId);

        (await PostAsync(Url, Apply(customerId, LoanSeedData.PersonalLoanId))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- decision flow over HTTP ----

    [Fact]
    public async Task FullFlow_Submit_StartReview_Approve_ThenGetShowsTheDecision()
    {
        Guid customerId = await NewIndividualAsync();
        var created = await SubmitAsync(customerId);

        var review = await Client.PostAsync($"{Url}/{created.Id}/start-review", null, Ct);
        review.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<LoanApplicationStatusResponse>(review)).Status.ShouldBe(LoanApplicationStatus.UnderReview);

        var approve = await PostAsync($"{Url}/{created.Id}/approve", new { approvedAmount = 80_000, reason = "Good profile" });
        approve.StatusCode.ShouldBe(HttpStatusCode.OK);
        var decision = await ReadAsync<LoanApplicationStatusResponse>(approve);
        (decision.Status, decision.ApprovedAmount, decision.DecisionReason).ShouldBe((LoanApplicationStatus.Approved, 80_000m, "Good profile"));

        var stored = await ReadAsync<GetByIdLoanApplicationResponse>(await Client.GetAsync($"{Url}/{created.Id}", Ct));
        stored.Status.ShouldBe(LoanApplicationStatus.Approved);
        stored.CustomerName.ShouldBe("Fatih Yilmaz");
        stored.LoanTypeName.ShouldBe("Individual Loan");
        stored.LoanSubTypeName.ShouldBe("Personal Loan");
    }

    [Fact]
    public async Task Approve_WithoutReview_Returns400()
    {
        var created = await SubmitAsync(await NewIndividualAsync());

        var response = await PostAsync($"{Url}/{created.Id}/approve", new { approvedAmount = 10_000 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("type").GetString().ShouldBe("urn:problem:business");
    }

    [Fact]
    public async Task Reject_NeedsAReason_AndRecordsIt()
    {
        var created = await SubmitAsync(await NewIndividualAsync());

        var withoutReason = await PostAsync($"{Url}/{created.Id}/reject", new { reason = "" });
        withoutReason.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(withoutReason)).GetProperty("type").GetString().ShouldBe("urn:problem:validation");

        var rejected = await PostAsync($"{Url}/{created.Id}/reject", new RejectLoanApplicationCommand { Reason = "Debt ratio too high" });
        rejected.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<LoanApplicationStatusResponse>(rejected)).DecisionReason.ShouldBe("Debt ratio too high");
    }

    [Fact]
    public async Task Cancel_WorksWithoutABody_AndWithAReason()
    {
        Guid customerId = await NewIndividualAsync();
        var first = await SubmitAsync(customerId, LoanSeedData.PersonalLoanId);
        var second = await SubmitAsync(customerId, LoanSeedData.AutoLoanId);

        var withoutBody = await Client.PostAsync($"{Url}/{first.Id}/cancel", null, Ct);
        withoutBody.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<LoanApplicationStatusResponse>(withoutBody)).Status.ShouldBe(LoanApplicationStatus.Cancelled);

        var withReason = await PostAsync($"{Url}/{second.Id}/cancel", new { reason = "Changed my mind" });
        (await ReadAsync<LoanApplicationStatusResponse>(withReason)).DecisionReason.ShouldBe("Changed my mind");
    }

    [Fact]
    public async Task ClosedApplication_CannotBeChangedAgain_Returns400()
    {
        var created = await SubmitAsync(await NewIndividualAsync());
        await Client.PostAsync($"{Url}/{created.Id}/cancel", null, Ct);

        (await Client.PostAsync($"{Url}/{created.Id}/start-review", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Client.PostAsync($"{Url}/{created.Id}/cancel", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task StatusEndpoints_OnAnUnknownApplication_Return404()
    {
        Guid unknown = Guid.NewGuid();

        (await Client.PostAsync($"{Url}/{unknown}/start-review", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await PostAsync($"{Url}/{unknown}/approve", new { approvedAmount = 1 })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await PostAsync($"{Url}/{unknown}/reject", new { reason = "x" })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Client.PostAsync($"{Url}/{unknown}/cancel", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Client.GetAsync($"{Url}/{unknown}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- list ----

    [Fact]
    public async Task Get_List_IsPaged_FilteredByCustomer_AndShowsCustomerAndLoanType()
    {
        Guid fatih = await NewIndividualAsync("10000000146", "Fatih");
        Guid sevval = await NewIndividualAsync("12345678950", "Sevval");
        await SubmitAsync(fatih, LoanSeedData.PersonalLoanId);
        await SubmitAsync(fatih, LoanSeedData.AutoLoanId);
        await SubmitAsync(fatih, LoanSeedData.MortgageLoanId);
        await SubmitAsync(sevval, LoanSeedData.PersonalLoanId);

        var page = await ReadAsync<Paginate<GetListLoanApplicationListItemDto>>(
            await Client.GetAsync($"{Url}?customerId={fatih}&index=0&size=2", Ct));

        (page.Count, page.Pages, page.Items.Count, page.HasNext, page.HasPrevious).ShouldBe((3, 2, 2, true, false));
        page.Items.ShouldAllBe(i => i.CustomerId == fatih && i.CustomerName == "Fatih Yilmaz");
        page.Items.ShouldAllBe(i => i.LoanTypeName == "Individual Loan" && i.CustomerNumber.StartsWith("IND-"));

        var all = await ReadAsync<Paginate<GetListLoanApplicationListItemDto>>(await Client.GetAsync($"{Url}?size=50", Ct));
        all.Count.ShouldBe(4);
    }

    [Fact]
    public async Task Get_List_FiltersByStatus_UsingTheEnumName()
    {
        Guid customerId = await NewIndividualAsync();
        var created = await SubmitAsync(customerId);
        await PostAsync($"{Url}/{created.Id}/reject", new { reason = "No" });
        await SubmitAsync(customerId, LoanSeedData.AutoLoanId);

        var rejected = await ReadAsync<Paginate<GetListLoanApplicationListItemDto>>(await Client.GetAsync($"{Url}?status=Rejected", Ct));
        var submitted = await ReadAsync<Paginate<GetListLoanApplicationListItemDto>>(await Client.GetAsync($"{Url}?status=Submitted", Ct));

        (rejected.Count, submitted.Count).ShouldBe((1, 1));
    }

    [Theory]
    [InlineData("size=0")]
    [InlineData("size=101")]
    [InlineData("index=-1")]
    [InlineData("status=99")]
    public async Task Get_List_WithInvalidParameters_Returns400(string query) =>
        (await Client.GetAsync($"{Url}?{query}", Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

    // ---- calculation ----

    [Fact]
    public async Task Calculate_ReturnsThePaymentPlan_AndMatchesTheApplicationCalculation()
    {
        var response = await Client.GetAsync(
            $"{Url}/calculate?loanSubTypeId={LoanSeedData.PersonalLoanId}&amount=100000&termInMonths=12", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await ReadJsonAsync(response);
        json.GetProperty("annualInterestRate").GetDecimal().ShouldBe(48.00m);
        json.GetProperty("schedule").GetArrayLength().ShouldBe(12);
        json.GetProperty("schedule")[11].GetProperty("remainingBalance").GetDecimal().ShouldBe(0m);

        var application = await SubmitWith(100_000, 12);
        application.MonthlyPayment.ShouldBe(json.GetProperty("monthlyPayment").GetDecimal());

        async Task<CreatedLoanApplicationResponse> SubmitWith(decimal amount, int term)
        {
            Guid customerId = await NewIndividualAsync();
            var created = await PostAsync(Url, Apply(customerId, LoanSeedData.PersonalLoanId, amount, term));
            return await ReadAsync<CreatedLoanApplicationResponse>(created);
        }
    }

    [Theory]
    [InlineData("amount=100000&termInMonths=12", HttpStatusCode.BadRequest)]                       // no sub type id
    [InlineData("loanSubTypeId=00000000-0000-0000-0000-000000000099&amount=100000&termInMonths=12", HttpStatusCode.NotFound)]
    public async Task Calculate_WithBadInput_ReturnsAProblem(string query, HttpStatusCode expected) =>
        (await Client.GetAsync($"{Url}/calculate?{query}", Ct)).StatusCode.ShouldBe(expected);

    [Fact]
    public async Task Calculate_OutsideTheLimits_Returns400()
    {
        var response = await Client.GetAsync(
            $"{Url}/calculate?loanSubTypeId={LoanSeedData.PersonalLoanId}&amount=1&termInMonths=12", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- swagger ----

    [Fact]
    public async Task SwaggerExamples_ForLoanApplications_AreValidRequests_OnceTheCustomerIdIsFilledIn()
    {
        var doc = await ReadJsonAsync(await Client.GetAsync("/openapi/v0.json", Ct));
        JsonElement Example(string path) => doc.GetProperty("paths").GetProperty(path).GetProperty("post")
            .GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("example");

        Guid customerId = await NewIndividualAsync();
        var create = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Example(Url).GetRawText())!;
        create["customerId"] = JsonSerializer.SerializeToElement(customerId);

        var created = await PostAsync(Url, create);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        Guid id = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();

        (await Client.PostAsync($"{Url}/{id}/start-review", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var approve = await Client.PostAsync($"{Url}/{id}/approve",
            new StringContent(Example(Url + "/{id}/approve").GetRawText(), System.Text.Encoding.UTF8, "application/json"), Ct);
        approve.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
