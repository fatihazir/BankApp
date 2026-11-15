using System.Net;
using System.Text.Json;
using Application.Features.LoanTypes.Queries.GetList;
using BankingSystemCore.Repositories;
using Persistence.EntityConfigurations;
using WebApi.IntegrationTests.Infrastructure;

namespace WebApi.IntegrationTests.Controllers;

[Collection(DatabaseCollection.Name)]
public class LoanTypesControllerTests(SqlServerFixture database) : ApiTestBase(database)
{
    private const string Url = "/api/v0/loan-types";

    [Fact]
    public async Task Get_ReturnsThePagedSeededTypes_WithTheirSubTypes()
    {
        var response = await Client.GetAsync(Url, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await ReadAsync<Paginate<GetListLoanTypeListItemDto>>(response);
        (page.Count, page.Pages, page.Items.Count).ShouldBe((2, 1, 2));
        page.Items.Sum(t => t.SubTypes.Count).ShouldBe(6);
    }

    [Theory]
    [InlineData("Individual", "Individual Loan")]
    [InlineData("Corporate", "Corporate Loan")]
    public async Task Get_FiltersBySegment(string segment, string expectedName)
    {
        var page = await ReadAsync<Paginate<GetListLoanTypeListItemDto>>(await Client.GetAsync($"{Url}?segment={segment}", Ct));

        page.Items.Single().Name.ShouldBe(expectedName);
    }

    [Theory]
    [InlineData("segment=99")]
    [InlineData("size=0")]
    [InlineData("index=-1")]
    public async Task Get_WithInvalidParameters_Returns400(string query) =>
        (await Client.GetAsync($"{Url}?{query}", Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

    [Fact]
    public async Task Post_CreatesALoanType_Returns201()
    {
        var response = await PostAsync(Url, new { name = "Special Loan", description = "Campaign", segment = "Individual" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var json = await ReadJsonAsync(response);
        json.GetProperty("name").GetString().ShouldBe("Special Loan");
        json.GetProperty("segment").GetString().ShouldBe("Individual");
        json.GetProperty("isActive").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Post_DuplicateName_Returns400_AndInvalidBodyReturnsValidationProblem()
    {
        var duplicate = await PostAsync(Url, new { name = "Individual Loan", segment = "Corporate" });
        duplicate.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(duplicate)).GetProperty("type").GetString().ShouldBe("urn:problem:business");

        var invalid = await PostAsync(Url, new { name = "", segment = "Individual" });
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(invalid)).GetProperty("type").GetString().ShouldBe("urn:problem:validation");
    }

    [Fact]
    public async Task PostSubType_AddsAProduct_ThatShowsUpInTheList()
    {
        var response = await PostAsync($"{Url}/{LoanSeedData.IndividualLoanTypeId}/sub-types", new
        {
            name = "Education Loan", description = "Education", minAmount = 10_000, maxAmount = 500_000,
            minTermInMonths = 6, maxTermInMonths = 48, annualInterestRate = 39.5
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var page = await ReadAsync<Paginate<GetListLoanTypeListItemDto>>(await Client.GetAsync($"{Url}?segment=Individual", Ct));
        page.Items.Single().SubTypes.Select(s => s.Name).ShouldContain("Education Loan");
    }

    [Fact]
    public async Task PostSubType_Problems_AreReportedClearly()
    {
        var body = new { name = "X", minAmount = 10_000, maxAmount = 500_000, minTermInMonths = 6, maxTermInMonths = 48, annualInterestRate = 39.5 };

        (await PostAsync($"{Url}/{Guid.NewGuid()}/sub-types", body)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var inconsistent = await PostAsync($"{Url}/{LoanSeedData.IndividualLoanTypeId}/sub-types",
            new { name = "X", minAmount = 500_000, maxAmount = 10_000, minTermInMonths = 6, maxTermInMonths = 48, annualInterestRate = 39.5 });
        inconsistent.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await PostAsync($"{Url}/{LoanSeedData.IndividualLoanTypeId}/sub-types", body)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await PostAsync($"{Url}/{LoanSeedData.IndividualLoanTypeId}/sub-types", body)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SwaggerExamples_ForLoanTypes_AreAccepted()
    {
        var doc = await ReadJsonAsync(await Client.GetAsync("/openapi/v0.json", Ct));
        JsonElement Example(string path) => doc.GetProperty("paths").GetProperty(path).GetProperty("post")
            .GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("example");

        var type = await Client.PostAsync(Url,
            new StringContent(Example(Url).GetRawText(), System.Text.Encoding.UTF8, "application/json"), Ct);
        type.StatusCode.ShouldBe(HttpStatusCode.Created);
        Guid typeId = (await ReadJsonAsync(type)).GetProperty("id").GetGuid();

        var subType = await Client.PostAsync($"{Url}/{typeId}/sub-types",
            new StringContent(Example(Url + "/{loanTypeId}/sub-types").GetRawText(), System.Text.Encoding.UTF8, "application/json"), Ct);
        subType.StatusCode.ShouldBe(HttpStatusCode.Created);
    }
}
