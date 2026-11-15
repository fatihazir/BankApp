using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Features.CorporateCustomers.Commands.Create;
using Application.Features.IndividualCustomers.Commands.Create;
using BankingSystemCore.Security.Jwt;
using Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Persistence.EntityConfigurations;
using WebApi.IntegrationTests.Infrastructure;

namespace WebApi.IntegrationTests.Controllers;

[Collection(DatabaseCollection.Name)]
public class AuthenticationAndAuthorizationTests(SqlServerFixture database) : ApiTestBase(database)
{
    // ---- helpers ----

    private sealed record Person(Guid Id, string Number, string NationalId, string Email);

    private async Task<Person> NewPersonAsync(int seed, string firstName)
    {
        string nationalId = TestData.NationalId(seed);
        var response = await PostAsync("/api/v0/individual-customers", new CreateIndividualCustomerCommand
        {
            Email = $"{firstName}@x.com", Password = CustomerPassword, PhoneNumber = "5551112233", Address = "Street", City = "Istanbul", Country = "Turkey",
            FirstName = firstName, LastName = "Test", NationalId = nationalId, DateOfBirth = new DateOnly(1990, 1, 1),
            EmploymentStatus = EmploymentStatus.Employed, MonthlyIncome = 1000
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var json = await ReadJsonAsync(response);
        return new Person(json.GetProperty("id").GetGuid(), json.GetProperty("customerNumber").GetString()!, nationalId, $"{firstName}@x.com");
    }

    private async Task<HttpClient> LoginAsCustomerAsync(Person person) => await CustomerClientAsync(person.Email);

    private async Task<Guid> ApplyAsync(HttpClient client, Guid customerId, Guid? subTypeId = null)
    {
        var response = await client.PostAsJsonAsync("/api/v0/loan-applications", new
        {
            customerId, loanSubTypeId = subTypeId ?? LoanSeedData.PersonalLoanId, requestedAmount = 100_000, termInMonths = 24
        }, Json, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string type)
    {
        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var json = await ReadJsonAsync(response);
        json.GetProperty("type").GetString().ShouldBe(type);
        json.GetProperty("traceId").GetString().ShouldNotBeNullOrEmpty();
    }

    // ---- login ----

    [Fact]
    public async Task Login_ReturnsAToken_ThatWorksOnMe()
    {
        var login = await AnonymousClient.PostAsJsonAsync("/api/v0/auth/login", new { email = StaffEmail, password = StaffPassword }, Json, Ct);

        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadJsonAsync(login);
        body.GetProperty("tokenType").GetString().ShouldBe("Bearer");
        body.GetProperty("role").GetString().ShouldBe("BankStaff");
        body.GetProperty("expiresAt").GetDateTime().ShouldBeGreaterThan(DateTime.UtcNow);
        body.GetRawText().ShouldNotContain("PBKDF2");

        using var client = ClientFor(body.GetProperty("accessToken").GetString()!);
        var me = await ReadJsonAsync(await client.GetAsync("/api/v0/auth/me", Ct));
        me.GetProperty("email").GetString().ShouldBe(StaffEmail);
        me.GetProperty("roles")[0].GetString().ShouldBe("BankStaff");
        me.GetProperty("emailVerified").GetBoolean().ShouldBeTrue(); // staff accounts are trusted
    }

    [Fact]
    public async Task Login_WithAWrongPasswordOrUnknownEmail_Returns401_WithTheSameMessage()
    {
        var wrong = await AnonymousClient.PostAsJsonAsync("/api/v0/auth/login", new { email = StaffEmail, password = "Wrong-Password-1" }, Json, Ct);
        var unknown = await AnonymousClient.PostAsJsonAsync("/api/v0/auth/login", new { email = "nobody@bank.test", password = "Wrong-Password-1" }, Json, Ct);

        await AssertProblem(wrong, HttpStatusCode.Unauthorized, "urn:problem:authentication");
        await AssertProblem(unknown, HttpStatusCode.Unauthorized, "urn:problem:authentication");
        (await ReadJsonAsync(wrong)).GetProperty("detail").GetString()
            .ShouldBe((await ReadJsonAsync(unknown)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Login_WithBadInput_Returns400_Validation()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/v0/auth/login", new { email = "not-an-email", password = "" }, Json, Ct);

        await AssertProblem(response, HttpStatusCode.BadRequest, "urn:problem:validation");
    }

    [Fact]
    public async Task Login_LocksTheAccountAfterFiveWrongPasswords_EvenForTheRightPassword()
    {
        for (int i = 0; i < 5; i++)
            (await AnonymousClient.PostAsJsonAsync("/api/v0/auth/login", new { email = StaffEmail, password = "Wrong-Password-1" }, Json, Ct))
                .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var locked = await AnonymousClient.PostAsJsonAsync("/api/v0/auth/login", new { email = StaffEmail, password = StaffPassword }, Json, Ct);

        await AssertProblem(locked, HttpStatusCode.Unauthorized, "urn:problem:authentication");
        (await ReadJsonAsync(locked)).GetProperty("detail").GetString()!.ShouldContain("locked");
    }

    // ---- no token / bad token ----

    [Theory]
    [InlineData("GET", "/api/v0/individual-customers")]
    [InlineData("GET", "/api/v0/corporate-customers")]
    [InlineData("POST", "/api/v0/individual-customers")]
    [InlineData("GET", "/api/v0/loan-types")]
    [InlineData("POST", "/api/v0/loan-types")]
    [InlineData("GET", "/api/v0/loan-applications")]
    [InlineData("POST", "/api/v0/loan-applications")]
    [InlineData("GET", "/api/v0/loan-applications/calculate")]
    [InlineData("POST", "/api/v0/users/staff")]
    [InlineData("GET", "/api/v0/auth/me")]
    public async Task EveryProtectedEndpoint_Returns401_WithoutAToken(string method, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method == "POST") request.Content = JsonContent.Create(new { });

        var response = await AnonymousClient.SendAsync(request, Ct);

        await AssertProblem(response, HttpStatusCode.Unauthorized, "urn:problem:authentication");
    }

    [Fact]
    public async Task EveryEndpointWithAnIdInTheRoute_Returns401_WithoutAToken()
    {
        Guid id = Guid.NewGuid();

        foreach (var (method, url) in new[]
        {
            ("GET", $"/api/v0/individual-customers/{id}"), ("PUT", $"/api/v0/individual-customers/{id}"), ("DELETE", $"/api/v0/individual-customers/{id}"),
            ("GET", $"/api/v0/corporate-customers/{id}"), ("PUT", $"/api/v0/corporate-customers/{id}"), ("DELETE", $"/api/v0/corporate-customers/{id}"),
            ("GET", $"/api/v0/loan-applications/{id}"), ("POST", $"/api/v0/loan-applications/{id}/approve"), ("POST", $"/api/v0/loan-applications/{id}/reject"),
            ("POST", $"/api/v0/loan-applications/{id}/start-review"), ("POST", $"/api/v0/loan-applications/{id}/cancel"),
            ("POST", $"/api/v0/loan-types/{id}/sub-types")
        })
        {
            var request = new HttpRequestMessage(new HttpMethod(method), url);
            if (method is "POST" or "PUT") request.Content = JsonContent.Create(new { });
            (await AnonymousClient.SendAsync(request, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized, $"{method} {url}");
        }
    }

    [Fact]
    public async Task OnlyLoginRefreshAndVerification_AreOpenToAnonymousCallers()
    {
        (await AnonymousClient.PostAsJsonAsync("/api/v0/auth/login", new { email = "a@b.com", password = "x" }, Json, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized); // reached the handler
        (await AnonymousClient.PostAsJsonAsync("/api/v0/auth/refresh", new { refreshToken = "x" }, Json, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);   // reached the handler
        (await AnonymousClient.PostAsJsonAsync("/api/v0/auth/verify-email", new { }, Json, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);                 // reached validation
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("a.b.c")]
    [InlineData("")]
    public async Task AMalformedToken_Returns401(string token)
    {
        using var client = ClientFor(token.Length == 0 ? "x" : token);

        await AssertProblem(await client.GetAsync("/api/v0/auth/me", Ct), HttpStatusCode.Unauthorized, "urn:problem:authentication");
    }

    [Fact]
    public async Task ATokenWithTamperedPayload_Returns401()
    {
        string[] parts = (await IssueTokenAsync("Customer")).Split('.');
        string payload = System.Text.Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[1])).Replace("Customer", "BankStaff");
        parts[1] = Base64UrlEncoder.Encode(payload);

        using var client = ClientFor(string.Join('.', parts));

        (await client.GetAsync("/api/v0/individual-customers", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ATokenSignedWithAnotherKey_Returns401()
    {
        var foreign = new JwtHelper(Options.Create(new TokenOptions
        {
            Issuer = TestIssuer, Audience = TestAudience, SecurityKey = new string('x', 40), AccessTokenExpirationMinutes = 30
        })).CreateToken(new TokenUser(Guid.NewGuid(), "evil@bank.test", ["BankStaff"]));

        using var client = ClientFor(foreign.Token);

        (await client.GetAsync("/api/v0/individual-customers", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AnExpiredToken_Returns401()
    {
        var twoHoursAgo = new FixedTime(DateTimeOffset.UtcNow.AddHours(-2));
        var expired = new JwtHelper(Options.Create(new TokenOptions
        {
            Issuer = TestIssuer, Audience = TestAudience, SecurityKey = TestKey, AccessTokenExpirationMinutes = 30
        }), twoHoursAgo).CreateToken(new TokenUser(Guid.NewGuid(), "old@bank.test", ["BankStaff"]));

        using var client = ClientFor(expired.Token);

        await AssertProblem(await client.GetAsync("/api/v0/individual-customers", Ct), HttpStatusCode.Unauthorized, "urn:problem:authentication");
    }

    [Theory]
    [InlineData("other-issuer", TestAudience)]
    [InlineData(TestIssuer, "other-audience")]
    public async Task ATokenForAnotherIssuerOrAudience_Returns401(string issuer, string audience)
    {
        var token = new JwtHelper(Options.Create(new TokenOptions
        {
            Issuer = issuer, Audience = audience, SecurityKey = TestKey, AccessTokenExpirationMinutes = 30
        })).CreateToken(new TokenUser(Guid.NewGuid(), "x@bank.test", ["BankStaff"]));

        using var client = ClientFor(token.Token);

        (await client.GetAsync("/api/v0/individual-customers", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AnUnsignedToken_AlgNone_Returns401()
    {
        string header = Base64UrlEncoder.Encode("""{"alg":"none","typ":"JWT"}""");
        string payload = Base64UrlEncoder.Encode($$"""{"sub":"{{Guid.NewGuid()}}","role":"BankStaff","iss":"{{TestIssuer}}","aud":"{{TestAudience}}","exp":9999999999}""");

        using var client = ClientFor($"{header}.{payload}.");

        (await client.GetAsync("/api/v0/individual-customers", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // ---- roles ----

    [Fact]
    public async Task ACustomer_GetsForbidden403_OnStaffOnlyEndpoints()
    {
        var fatih = await NewPersonAsync(1, "Fatih");
        using var customer = await LoginAsCustomerAsync(fatih);

        foreach (var (method, url) in new[]
        {
            ("GET", "/api/v0/individual-customers"), ("GET", "/api/v0/corporate-customers"), ("POST", "/api/v0/individual-customers"),
            ("POST", "/api/v0/corporate-customers"), ("DELETE", $"/api/v0/individual-customers/{fatih.Id}"),
            ("POST", "/api/v0/loan-types"), ("POST", $"/api/v0/loan-types/{LoanSeedData.IndividualLoanTypeId}/sub-types"),
            ("POST", "/api/v0/users/staff"), ("POST", $"/api/v0/loan-applications/{Guid.NewGuid()}/approve")
        })
        {
            var request = new HttpRequestMessage(new HttpMethod(method), url);
            if (method == "POST") request.Content = JsonContent.Create(new { });

            var response = await customer.SendAsync(request, Ct);

            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, $"{method} {url}");
        }
    }

    [Fact]
    public async Task Forbidden_IsAProblemJson_OfTheAuthorizationType()
    {
        var fatih = await NewPersonAsync(1, "Fatih");
        using var customer = await LoginAsCustomerAsync(fatih);

        await AssertProblem(await customer.GetAsync("/api/v0/individual-customers", Ct), HttpStatusCode.Forbidden, "urn:problem:authorization");
    }

    [Fact]
    public async Task ACustomer_CannotApproveOrRejectTheirOwnApplication()
    {
        var fatih = await NewPersonAsync(1, "Fatih");
        using var customer = await LoginAsCustomerAsync(fatih);
        Guid applicationId = await ApplyAsync(customer, fatih.Id);

        (await customer.PostAsync($"/api/v0/loan-applications/{applicationId}/start-review", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await customer.PostAsJsonAsync($"/api/v0/loan-applications/{applicationId}/approve", new { approvedAmount = 1 }, Json, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await customer.PostAsJsonAsync($"/api/v0/loan-applications/{applicationId}/reject", new { reason = "x" }, Json, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var stored = await ReadJsonAsync(await Client.GetAsync($"/api/v0/loan-applications/{applicationId}", Ct));
        stored.GetProperty("status").GetString().ShouldBe("Submitted");
    }

    [Fact]
    public async Task AnyLoggedInUser_CanBrowseLoanTypes_AndCalculate()
    {
        var fatih = await NewPersonAsync(1, "Fatih");
        using var customer = await LoginAsCustomerAsync(fatih);

        (await customer.GetAsync("/api/v0/loan-types", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await customer.GetAsync($"/api/v0/loan-applications/calculate?loanSubTypeId={LoanSeedData.PersonalLoanId}&amount=50000&termInMonths=12", Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ---- a customer's own data over HTTP ----

    [Fact]
    public async Task ACustomer_ReadsAndUpdatesOnlyTheirOwnRecord()
    {
        var fatih = await NewPersonAsync(1, "Fatih");
        var sevval = await NewPersonAsync(2, "Sevval");
        using var customer = await LoginAsCustomerAsync(fatih);

        (await customer.GetAsync($"/api/v0/individual-customers/{fatih.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var other = await customer.GetAsync($"/api/v0/individual-customers/{sevval.Id}", Ct);
        await AssertProblem(other, HttpStatusCode.NotFound, "urn:problem:not-found");

        var missing = await customer.GetAsync($"/api/v0/individual-customers/{Guid.NewGuid()}", Ct);
        (await ReadJsonAsync(other)).GetProperty("detail").GetString()
            .ShouldBe((await ReadJsonAsync(missing)).GetProperty("detail").GetString()); // indistinguishable

        var update = new
        {
            email = "x@y.com", phoneNumber = "1", address = "a", city = "a", country = "a", isActive = true, firstName = "Hacked",
            lastName = "x", nationalId = sevval.NationalId, dateOfBirth = "1990-01-01", employmentStatus = "Employed", monthlyIncome = 1
        };
        (await customer.PutAsJsonAsync($"/api/v0/individual-customers/{sevval.Id}", update, Json, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReadJsonAsync(await Client.GetAsync($"/api/v0/individual-customers/{sevval.Id}", Ct))).GetProperty("firstName").GetString().ShouldBe("Sevval");
    }

    [Fact]
    public async Task ACustomer_AppliesOnlyForThemselves_AndSeesOnlyTheirApplications()
    {
        var fatih = await NewPersonAsync(1, "Fatih");
        var sevval = await NewPersonAsync(2, "Sevval");
        using var fatihClient = await LoginAsCustomerAsync(fatih);
        using var sevvalClient = await LoginAsCustomerAsync(sevval);

        Guid fatihApplication = await ApplyAsync(fatihClient, fatih.Id);
        Guid sevvalApplication = await ApplyAsync(sevvalClient, sevval.Id);

        var forOther = await fatihClient.PostAsJsonAsync("/api/v0/loan-applications",
            new { customerId = sevval.Id, loanSubTypeId = LoanSeedData.AutoLoanId, requestedAmount = 100_000, termInMonths = 24 }, Json, Ct);
        await AssertProblem(forOther, HttpStatusCode.Forbidden, "urn:problem:authorization");

        var list = await ReadJsonAsync(await fatihClient.GetAsync("/api/v0/loan-applications?size=50", Ct));
        list.GetProperty("count").GetInt32().ShouldBe(1);
        list.GetProperty("items")[0].GetProperty("id").GetGuid().ShouldBe(fatihApplication);

        (await fatihClient.GetAsync($"/api/v0/loan-applications?customerId={sevval.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await fatihClient.GetAsync($"/api/v0/loan-applications/{sevvalApplication}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await fatihClient.PostAsync($"/api/v0/loan-applications/{sevvalApplication}/cancel", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await fatihClient.PostAsync($"/api/v0/loan-applications/{fatihApplication}/cancel", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Staff_SeesEverythingAndDecides()
    {
        var fatih = await NewPersonAsync(1, "Fatih");
        var sevval = await NewPersonAsync(2, "Sevval");
        using var fatihClient = await LoginAsCustomerAsync(fatih);
        using var sevvalClient = await LoginAsCustomerAsync(sevval);
        Guid fatihApplication = await ApplyAsync(fatihClient, fatih.Id);
        await ApplyAsync(sevvalClient, sevval.Id);

        var all = await ReadJsonAsync(await Client.GetAsync("/api/v0/loan-applications?size=50", Ct));
        all.GetProperty("count").GetInt32().ShouldBe(2);

        (await Client.PostAsync($"/api/v0/loan-applications/{fatihApplication}/start-review", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Client.PostAsJsonAsync($"/api/v0/loan-applications/{fatihApplication}/approve", new { approvedAmount = 90_000 }, Json, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var seenByCustomer = await ReadJsonAsync(await fatihClient.GetAsync($"/api/v0/loan-applications/{fatihApplication}", Ct));
        seenByCustomer.GetProperty("status").GetString().ShouldBe("Approved");
        seenByCustomer.GetProperty("approvedAmount").GetDecimal().ShouldBe(90_000m);
    }

    [Fact]
    public async Task ACustomerRoleTokenOfAUserWhoOwnsNothing_SeesNothing()
    {
        var fatih = await NewPersonAsync(1, "Fatih");
        using var odd = ClientFor(await IssueTokenAsync("Customer"));

        (await odd.GetAsync($"/api/v0/individual-customers/{fatih.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        // A customer-role token of a user who owns nothing sees nothing (the list is filtered to that user).
        var applications = await ReadJsonAsync(await odd.GetAsync("/api/v0/loan-applications", Ct));
        applications.GetProperty("count").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task ATokenWithAnUnknownRole_IsForbiddenEverywhereItIsNotOpenToAnyUser()
    {
        using var client = ClientFor(await IssueTokenAsync("Admin"));

        (await client.GetAsync("/api/v0/individual-customers", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/v0/loan-applications", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- a customer is created together with their login ----

    [Fact]
    public async Task ACustomerCreatedByStaff_CanLoginRightAway_AndSeesOnlyTheirOwnData()
    {
        var fatih = await NewPersonAsync(1, "Fatih");
        var sevval = await NewPersonAsync(2, "Sevval");

        string token = await LoginAsync(fatih.Email, CustomerPassword);
        using var customer = ClientFor(token);

        var me = await ReadJsonAsync(await customer.GetAsync("/api/v0/auth/me", Ct));
        me.GetProperty("customerId").GetGuid().ShouldBe(fatih.Id);
        me.GetProperty("userId").GetGuid().ShouldBe(fatih.Id); // the customer is the user
        me.GetProperty("customerName").GetString().ShouldBe("Fatih Test");
        (await customer.GetAsync($"/api/v0/individual-customers/{fatih.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await customer.GetAsync($"/api/v0/individual-customers/{sevval.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreatingACustomer_NeedsALoginName_AndAStrongPassword_AndTheRoleCannotBeChosen()
    {
        var weak = await PostAsync("/api/v0/individual-customers", new
        {
            email = "x@bank.test", password = "weak", firstName = "A", lastName = "B", nationalId = TestData.NationalId(5),
            dateOfBirth = "1990-01-01", employmentStatus = "Employed", phoneNumber = "1", address = "a", city = "a", country = "a"
        });
        await AssertProblem(weak, HttpStatusCode.BadRequest, "urn:problem:validation");
        (await ReadJsonAsync(weak)).GetProperty("errors").EnumerateArray()
            .Select(e => e.GetProperty("property").GetString()).ShouldContain("Password");

        // A "role" in the body is simply ignored: the new customer is a Customer, never staff.
        var sneaky = await PostAsync("/api/v0/individual-customers", new
        {
            email = "sneaky@bank.test", password = CustomerPassword, role = "BankStaff", firstName = "A", lastName = "B",
            nationalId = TestData.NationalId(6), dateOfBirth = "1990-01-01", employmentStatus = "Employed",
            phoneNumber = "1", address = "a", city = "a", country = "a"
        });
        sneaky.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var client = ClientFor(await LoginAsync("sneaky@bank.test", CustomerPassword));
        (await ReadJsonAsync(await client.GetAsync("/api/v0/auth/me", Ct))).GetProperty("roles")[0].GetString().ShouldBe("Customer");
        (await client.GetAsync("/api/v0/individual-customers", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TheLoginName_MustBeFreeAmongAllUsers()
    {
        await PostAsync("/api/v0/users/staff", new { email = "taken@bank.test", password = "Second-Passw0rd-1" });

        var response = await PostAsync("/api/v0/individual-customers", new
        {
            email = "TAKEN@bank.test", password = CustomerPassword, firstName = "A", lastName = "B", nationalId = TestData.NationalId(7),
            dateOfBirth = "1990-01-01", employmentStatus = "Employed", phoneNumber = "1", address = "a", city = "a", country = "a"
        });

        await AssertProblem(response, HttpStatusCode.BadRequest, "urn:problem:business");
    }

    [Fact]
    public async Task ACustomer_CannotCreateCustomers()
    {
        var fatih = await NewPersonAsync(1, "Fatih");
        using var customer = await LoginAsCustomerAsync(fatih);

        var response = await customer.PostAsJsonAsync("/api/v0/individual-customers", new { }, Json, Ct);

        await AssertProblem(response, HttpStatusCode.Forbidden, "urn:problem:authorization");
    }

    [Fact]
    public async Task ACorporateCustomer_LogsInAndSeesOnlyItself()
    {
        var response = await PostAsync("/api/v0/corporate-customers", new CreateCorporateCustomerCommand
        {
            Email = "acme@bank.test", Password = CustomerPassword, PhoneNumber = "2125551234", Address = "Plaza", City = "Istanbul",
            Country = "Turkey", CompanyName = "Acme", TaxNumber = "1234567890", TaxOffice = "Kadikoy",
            FoundationDate = new DateOnly(2010, 1, 1), AuthorizedPersonName = "Boss"
        });
        Guid companyId = (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
        var fatih = await NewPersonAsync(1, "Fatih");

        using var company = await CustomerClientAsync("acme@bank.test");

        (await company.GetAsync($"/api/v0/corporate-customers/{companyId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await company.GetAsync($"/api/v0/individual-customers/{fatih.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await company.GetAsync($"/api/v0/corporate-customers/{Guid.NewGuid()}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- staff accounts ----

    [Fact]
    public async Task Staff_CanCreateAnotherStaffAccount_ThatCanLogin()
    {
        var created = await PostAsync("/api/v0/users/staff", new { email = "second.staff@bank.test", password = "Second-Passw0rd-1" });

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        string token = await LoginAsync("second.staff@bank.test", "Second-Passw0rd-1");
        using var second = ClientFor(token);
        (await second.GetAsync("/api/v0/individual-customers", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreatingStaff_WithAWeakPasswordOrADuplicateEmail_Returns400()
    {
        (await PostAsync("/api/v0/users/staff", new { email = "x@bank.test", password = "weak" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await PostAsync("/api/v0/users/staff", new { email = StaffEmail, password = "Second-Passw0rd-1" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- swagger ----

    [Fact]
    public async Task OpenApiDocument_DescribesTheBearerScheme_AndLocksOnlyTheProtectedOperations()
    {
        var doc = await ReadJsonAsync(await AnonymousClient.GetAsync("/openapi/v0.json", Ct));

        var scheme = doc.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        (scheme.GetProperty("type").GetString(), scheme.GetProperty("scheme").GetString(), scheme.GetProperty("bearerFormat").GetString())
            .ShouldBe(("http", "bearer", "JWT"));

        JsonElement Operation(string path, string verb) => doc.GetProperty("paths").GetProperty(path).GetProperty(verb);

        Operation("/api/v0/individual-customers", "get").TryGetProperty("security", out var secured).ShouldBeTrue();
        secured.GetRawText().ShouldContain("Bearer");
        Operation("/api/v0/auth/me", "get").TryGetProperty("security", out _).ShouldBeTrue();
        Operation("/api/v0/auth/login", "post").TryGetProperty("security", out _).ShouldBeFalse();
        Operation("/api/v0/auth/refresh", "post").TryGetProperty("security", out _).ShouldBeFalse();
        Operation("/api/v0/auth/verify-email", "post").TryGetProperty("security", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task SwaggerUi_AndTheDocument_AreReachableWithoutLoggingIn()
    {
        (await AnonymousClient.GetAsync("/swagger/index.html", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await AnonymousClient.GetAsync("/openapi/v0.json", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
