using System.Net;
using System.Text.Json;
using Application.Features.IndividualCustomers.Commands.Create;
using Application.Features.IndividualCustomers.Commands.Delete;
using Application.Features.IndividualCustomers.Commands.Update;
using Application.Features.IndividualCustomers.Queries.GetById;
using Application.Features.IndividualCustomers.Queries.GetList;
using BankingSystemCore.Repositories;
using Domain.Entities;
using WebApi.IntegrationTests.Infrastructure;

namespace WebApi.IntegrationTests.Controllers;

[Collection(DatabaseCollection.Name)]
public class IndividualCustomersControllerTests(SqlServerFixture database) : ApiTestBase(database)
{
    private const string BaseUrl = "/api/individual-customers";

    private static CreateIndividualCustomerCommand NewCustomer(string nationalId = "10000000146") => new()
    {
        Email = $"c{nationalId}@bank.test",
        Password = CustomerPassword,
        PhoneNumber = "5551112233",
        Address = "Street 1",
        City = "Istanbul",
        Country = "Turkey",
        FirstName = "Ali",
        LastName = "Veli",
        NationalId = nationalId,
        DateOfBirth = new DateOnly(1990, 1, 15),
        EmploymentStatus = EmploymentStatus.Employed,
        MonthlyIncome = 75000m
    };

    private async Task<CreatedIndividualCustomerResponse> CreateAsync(string nationalId = "10000000146")
    {
        var response = await PostAsync(BaseUrl, NewCustomer(nationalId));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await ReadAsync<CreatedIndividualCustomerResponse>(response);
    }

    [Fact]
    public async Task Post_Returns201_WithLocationHeader_AndBody()
    {
        var response = await PostAsync(BaseUrl, NewCustomer());

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await ReadAsync<CreatedIndividualCustomerResponse>(response);
        created.CustomerNumber.ShouldStartWith("IND-");
        response.Headers.Location!.ToString().ShouldEndWith($"{BaseUrl}/{created.Id}");
    }

    [Fact]
    public async Task SameSignUpEightTimesAtOnce_GivesOneCreated_AndNeverAServerError()
    {
        // Every request passes the "is it taken?" check before any of them saves, so the unique index decides.
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => PostAsync(BaseUrl, NewCustomer())));

        var statuses = responses.Select(r => r.StatusCode).ToList();
        statuses.Count(s => s == HttpStatusCode.Created).ShouldBe(1);
        statuses.ShouldAllBe(s => s == HttpStatusCode.Created || s == HttpStatusCode.BadRequest || s == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Get_ById_Returns200_WithStoredValues_AndEnumAsText()
    {
        var created = await CreateAsync();

        var response = await Client.GetAsync($"{BaseUrl}/{created.Id}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await ReadJsonAsync(response);
        json.GetProperty("firstName").GetString().ShouldBe("Ali");
        json.GetProperty("dateOfBirth").GetString().ShouldBe("1990-01-15");
        json.GetProperty("employmentStatus").GetString().ShouldBe("Employed");
    }

    [Fact]
    public async Task Get_List_ReturnsRequestedPage()
    {
        await CreateAsync("10000000146");
        await CreateAsync("12345678950");
        await CreateAsync("23456789138");

        var response = await Client.GetAsync($"{BaseUrl}?index=0&size=2", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await ReadAsync<Paginate<GetListIndividualCustomerListItemDto>>(response);
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
        var update = new UpdateIndividualCustomerCommand
        {
            Id = Guid.NewGuid(), // ignored: the route id is used
            PhoneNumber = "5551112233", Address = "Street 2", City = "Ankara", Country = "Turkey",
            IsActive = true, FirstName = "Ayse", LastName = "Veli", NationalId = "10000000146",
            DateOfBirth = new DateOnly(1990, 1, 15), EmploymentStatus = EmploymentStatus.SelfEmployed, MonthlyIncome = 1m
        };

        var response = await PutAsync($"{BaseUrl}/{created.Id}", update);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await ReadAsync<UpdatedIndividualCustomerResponse>(response);
        updated.Id.ShouldBe(created.Id);
        updated.FirstName.ShouldBe("Ayse");
    }

    [Fact]
    public async Task Delete_Returns200_ThenGetReturns404()
    {
        var created = await CreateAsync();

        var deleted = await Client.DeleteAsync($"{BaseUrl}/{created.Id}", Ct);
        deleted.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<DeletedIndividualCustomerResponse>(deleted)).DeleteDate.ShouldNotBeNull();

        var get = await Client.GetAsync($"{BaseUrl}/{created.Id}", Ct);
        get.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_Unknown_Returns404_AsProblemJson()
    {
        var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var json = await ReadJsonAsync(response);
        json.GetProperty("type").GetString().ShouldBe("urn:problem:not-found");
        json.GetProperty("traceId").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Post_WithDuplicateNationalId_Returns400_BusinessProblem()
    {
        await CreateAsync();

        var response = await PostAsync(BaseUrl, NewCustomer());

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var json = await ReadJsonAsync(response);
        json.GetProperty("type").GetString().ShouldBe("urn:problem:business");
        json.GetProperty("detail").GetString()!.ShouldContain("national ID");
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
        properties.ShouldContain("FirstName");
        properties.ShouldContain("NationalId");
    }

    [Fact]
    public async Task Post_WithUnknownEnumValue_Returns400_WithoutLeakingInternalTypeNames()
    {
        var content = new StringContent("""{"employmentStatus":"Nope"}""", System.Text.Encoding.UTF8, "application/json");

        var response = await Client.PostAsync(BaseUrl, content, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync(Ct);
        body.ShouldContain("urn:problem:validation");
        body.ShouldNotContain("Domain.Entities");
    }

    [Fact]
    public async Task OpenApiDocument_ContainsPrefilledExamples_ForCreateAndUpdate()
    {
        var doc = await ReadJsonAsync(await Client.GetAsync("/openapi/v1.json", Ct));

        JsonElement Example(string path, string verb) => doc.GetProperty("paths").GetProperty(path).GetProperty(verb)
            .GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("example");

        Example(BaseUrl, "post").GetProperty("nationalId").GetString().ShouldBe("10000000146");
        Example(BaseUrl + "/{id}", "put").GetProperty("isActive").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Root_RedirectsToSwagger_AndSwaggerUiIsServed()
    {
        var root = await Client.GetAsync("/", Ct);
        root.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        root.Headers.Location!.ToString().ShouldBe("/swagger");

        (await Client.GetAsync("/swagger/index.html", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
