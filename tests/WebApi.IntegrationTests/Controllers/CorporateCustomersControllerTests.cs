using System.Net;
using System.Text.Json;
using Application.Features.CorporateCustomers.Commands.Create;
using Application.Features.CorporateCustomers.Commands.Delete;
using Application.Features.CorporateCustomers.Commands.Update;
using Application.Features.CorporateCustomers.Queries.GetList;
using BankingSystemCore.Repositories;
using WebApi.IntegrationTests.Infrastructure;

namespace WebApi.IntegrationTests.Controllers;

[Collection(DatabaseCollection.Name)]
public class CorporateCustomersControllerTests(SqlServerFixture database) : ApiTestBase(database)
{
    private const string BaseUrl = "/api/corporate-customers";

    private static CreateCorporateCustomerCommand NewCustomer(string taxNumber = "1234567890") => new()
    {
        Email = $"c{taxNumber}@bank.test",
        Password = CustomerPassword,
        PhoneNumber = "2125551234",
        Address = "Plaza 1",
        City = "Istanbul",
        Country = "Turkey",
        CompanyName = "Acme Teknoloji",
        TaxNumber = taxNumber,
        TaxOffice = "Mecidiyekoy",
        FoundationDate = new DateOnly(2010, 5, 1),
        EmployeeCount = 50,
        AnnualRevenue = 12_500_000.75m,
        AuthorizedPersonName = "Fatih Veli"
    };

    private async Task<CreatedCorporateCustomerResponse> CreateAsync(string taxNumber = "1234567890")
    {
        var response = await PostAsync(BaseUrl, NewCustomer(taxNumber));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await ReadAsync<CreatedCorporateCustomerResponse>(response);
    }

    [Fact]
    public async Task Post_Returns201_WithLocationHeader_AndBody()
    {
        var response = await PostAsync(BaseUrl, NewCustomer());

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await ReadAsync<CreatedCorporateCustomerResponse>(response);
        created.CustomerNumber.ShouldStartWith("COR-");
        response.Headers.Location!.ToString().ShouldEndWith($"{BaseUrl}/{created.Id}");
    }

    [Fact]
    public async Task Get_ById_Returns200_WithStoredValues()
    {
        var created = await CreateAsync();

        var response = await Client.GetAsync($"{BaseUrl}/{created.Id}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await ReadJsonAsync(response);
        json.GetProperty("companyName").GetString().ShouldBe("Acme Teknoloji");
        json.GetProperty("foundationDate").GetString().ShouldBe("2010-05-01");
        json.GetProperty("annualRevenue").GetDecimal().ShouldBe(12_500_000.75m);
    }

    [Fact]
    public async Task Get_List_ReturnsRequestedPage()
    {
        await CreateAsync("1111111111");
        await CreateAsync("2222222222");
        await CreateAsync("3333333333");

        var response = await Client.GetAsync($"{BaseUrl}?index=0&size=2", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await ReadAsync<Paginate<GetListCorporateCustomerListItemDto>>(response);
        page.Count.ShouldBe(3);
        page.Pages.ShouldBe(2);
        page.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Get_List_WithInvalidPageSize_Returns400()
    {
        var response = await Client.GetAsync($"{BaseUrl}?size=0", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("type").GetString().ShouldBe("urn:problem:validation");
    }

    [Fact]
    public async Task Put_UpdatesCustomer_AndTheRouteIdWinsOverTheBodyId()
    {
        var created = await CreateAsync();
        var update = new UpdateCorporateCustomerCommand
        {
            Id = Guid.NewGuid(), // ignored: the route id is used
            PhoneNumber = "2125551234", Address = "Plaza 2", City = "Izmir", Country = "Turkey",
            IsActive = true, CompanyName = "Acme Holding", TaxNumber = "1234567890", TaxOffice = "Mecidiyekoy",
            FoundationDate = new DateOnly(2010, 5, 1), EmployeeCount = 60, AnnualRevenue = 1m, AuthorizedPersonName = "Jane Doe"
        };

        var response = await PutAsync($"{BaseUrl}/{created.Id}", update);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await ReadAsync<UpdatedCorporateCustomerResponse>(response);
        updated.Id.ShouldBe(created.Id);
        updated.CompanyName.ShouldBe("Acme Holding");
        updated.CustomerNumber.ShouldBe(created.CustomerNumber);
    }

    [Fact]
    public async Task Put_ToAnotherCustomersTaxNumber_Returns400()
    {
        var first = await CreateAsync("1111111111");
        await CreateAsync("2222222222");
        var update = new UpdateCorporateCustomerCommand
        {
            PhoneNumber = "1", Address = "a", City = "a", Country = "a", IsActive = true,
            CompanyName = "x", TaxNumber = "2222222222", TaxOffice = "x", FoundationDate = new DateOnly(2010, 5, 1),
            AuthorizedPersonName = "x"
        };

        var response = await PutAsync($"{BaseUrl}/{first.Id}", update);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("type").GetString().ShouldBe("urn:problem:business");
    }

    [Fact]
    public async Task Put_Unknown_Returns404()
    {
        var update = new UpdateCorporateCustomerCommand
        {
            PhoneNumber = "1", Address = "a", City = "a", Country = "a", IsActive = true,
            CompanyName = "x", TaxNumber = "1234567890", TaxOffice = "x", FoundationDate = new DateOnly(2010, 5, 1),
            AuthorizedPersonName = "x"
        };

        var response = await PutAsync($"{BaseUrl}/{Guid.NewGuid()}", update);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_Returns200_ThenGetReturns404()
    {
        var created = await CreateAsync();

        var deleted = await Client.DeleteAsync($"{BaseUrl}/{created.Id}", Ct);
        deleted.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<DeletedCorporateCustomerResponse>(deleted)).DeleteDate.ShouldNotBeNull();

        (await Client.GetAsync($"{BaseUrl}/{created.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Client.GetAsync(BaseUrl, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_Unknown_Returns404_AsProblemJson()
    {
        var response = await Client.DeleteAsync($"{BaseUrl}/{Guid.NewGuid()}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        (await ReadJsonAsync(response)).GetProperty("type").GetString().ShouldBe("urn:problem:not-found");
    }

    [Fact]
    public async Task Post_WithDuplicateTaxNumber_Returns400_BusinessProblem()
    {
        await CreateAsync();

        var response = await PostAsync(BaseUrl, NewCustomer());

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var json = await ReadJsonAsync(response);
        json.GetProperty("type").GetString().ShouldBe("urn:problem:business");
        json.GetProperty("detail").GetString()!.ShouldContain("tax number");
    }

    [Fact]
    public async Task Post_WithFutureFoundationDate_Returns400()
    {
        var command = NewCustomer();
        command.FoundationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));

        var response = await PostAsync(BaseUrl, command);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("type").GetString().ShouldBe("urn:problem:business");
    }

    [Fact]
    public async Task Post_WithInvalidBody_Returns400_ValidationProblem_WithErrorsPerProperty()
    {
        var response = await PostAsync(BaseUrl, new { email = "x" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var json = await ReadJsonAsync(response);
        json.GetProperty("type").GetString().ShouldBe("urn:problem:validation");
        var properties = json.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("property").GetString()).ToList();
        properties.ShouldContain("Email");
        properties.ShouldContain("CompanyName");
        properties.ShouldContain("TaxNumber");
    }

    [Fact]
    public async Task OpenApiDocument_ContainsPrefilledExamples_ForCreateAndUpdate()
    {
        var doc = await ReadJsonAsync(await Client.GetAsync("/openapi/v1.json", Ct));

        JsonElement Example(string path, string verb) => doc.GetProperty("paths").GetProperty(path).GetProperty(verb)
            .GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("example");

        Example(BaseUrl, "post").GetProperty("taxNumber").GetString().ShouldBe("1234567890");
        Example(BaseUrl + "/{id}", "put").GetProperty("isActive").GetBoolean().ShouldBeTrue();
    }
}
