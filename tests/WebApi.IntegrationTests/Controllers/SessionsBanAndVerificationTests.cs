using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Features.IndividualCustomers.Commands.Create;
using Domain.Entities;
using Persistence.EntityConfigurations;
using WebApi.IntegrationTests.Infrastructure;

namespace WebApi.IntegrationTests.Controllers;

/// <summary>Refresh tokens, logout, instant revocation (ban, delete) and e-mail verification over HTTP.</summary>
[Collection(DatabaseCollection.Name)]
public class SessionsBanAndVerificationTests(SqlServerFixture database) : ApiTestBase(database)
{
    private sealed record Customer(Guid Id, string Email);

    private async Task<Customer> NewCustomerAsync(int seed = 1)
    {
        string email = $"customer{seed}@bank.test";
        var response = await PostAsync("/api/v0/individual-customers", new CreateIndividualCustomerCommand
        {
            Email = email, Password = CustomerPassword, PhoneNumber = "5551112233", Address = "Street", City = "Istanbul", Country = "Turkey",
            FirstName = "Customer", LastName = seed.ToString(), NationalId = TestData.NationalId(seed), DateOfBirth = new DateOnly(1990, 1, 1),
            EmploymentStatus = EmploymentStatus.Employed, MonthlyIncome = 1000
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return new Customer((await ReadJsonAsync(response)).GetProperty("id").GetGuid(), email);
    }

    private async Task<JsonElement> LoginJsonAsync(string email, string password = CustomerPassword)
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/v0/auth/login", new { email, password }, Json, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await ReadJsonAsync(response);
    }

    private static string Access(JsonElement login) => login.GetProperty("accessToken").GetString()!;

    private static string Refresh(JsonElement login) => login.GetProperty("refreshToken").GetString()!;

    private async Task<HttpStatusCode> StatusWith(string accessToken, string url = "/api/v0/auth/me")
    {
        using var client = ClientFor(accessToken);
        return (await client.GetAsync(url, Ct)).StatusCode;
    }

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        AnonymousClient.PostAsJsonAsync("/api/v0/auth/refresh", new { refreshToken }, Json, Ct);

    // ---- login returns a token pair ----

    [Fact]
    public async Task Login_ReturnsAShortAccessToken_AndARefreshTokenThatLivesThirtyMinutes()
    {
        var customer = await NewCustomerAsync();

        var login = await LoginJsonAsync(customer.Email);

        Refresh(login).ShouldNotBeNullOrEmpty();
        var accessLifetime = login.GetProperty("expiresAt").GetDateTime() - DateTime.UtcNow;
        var refreshLifetime = login.GetProperty("refreshTokenExpiresAt").GetDateTime() - DateTime.UtcNow;
        accessLifetime.ShouldBeInRange(TimeSpan.FromMinutes(9), TimeSpan.FromMinutes(10));
        refreshLifetime.ShouldBeInRange(TimeSpan.FromMinutes(29), TimeSpan.FromMinutes(30));
        login.GetRawText().ShouldNotContain("PBKDF2");
    }

    [Fact]
    public async Task Refresh_GivesANewPair_ThatWorks_AndTheOldRefreshTokenIsUsedUp()
    {
        var customer = await NewCustomerAsync();
        var login = await LoginJsonAsync(customer.Email);

        var refreshed = await RefreshAsync(Refresh(login));

        refreshed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var pair = await ReadJsonAsync(refreshed);
        Refresh(pair).ShouldNotBe(Refresh(login));
        (await StatusWith(Access(pair))).ShouldBe(HttpStatusCode.OK);

        var again = await RefreshAsync(Refresh(login)); // the old one
        again.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ReadJsonAsync(again)).GetProperty("type").GetString().ShouldBe("urn:problem:authentication");
    }

    [Fact]
    public async Task UsingAnOldRefreshToken_KillsTheSession_EvenTheAccessTokenAndTheNewestRefreshToken()
    {
        var customer = await NewCustomerAsync();
        var login = await LoginJsonAsync(customer.Email);
        var pair = await ReadJsonAsync(await RefreshAsync(Refresh(login)));

        (await RefreshAsync(Refresh(login))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized); // somebody replays the old token

        (await StatusWith(Access(pair))).ShouldBe(HttpStatusCode.Unauthorized);               // the access token is dead at once
        (await RefreshAsync(Refresh(pair))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized); // and so is the newest refresh token
        (await StatusWith(Access(await LoginJsonAsync(customer.Email)))).ShouldBe(HttpStatusCode.OK); // a fresh login still works
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    public async Task Refresh_WithAnUnknownToken_Returns401_OrValidationError(string token)
    {
        var response = await RefreshAsync(token);

        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Refresh_DoesNotNeedAnAccessToken_AndAnAccessTokenIsNotARefreshToken()
    {
        var customer = await NewCustomerAsync();
        var login = await LoginJsonAsync(customer.Email);

        // An access token is not a refresh token: it is refused (too long for one, so by validation, otherwise as unknown).
        (await RefreshAsync(Access(login))).StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
    }

    // ---- logout ----

    [Fact]
    public async Task Logout_Returns204_AndTheSameAccessTokenFailsOnTheNextRequest()
    {
        var customer = await NewCustomerAsync();
        var login = await LoginJsonAsync(customer.Email);
        (await StatusWith(Access(login))).ShouldBe(HttpStatusCode.OK);

        using var client = ClientFor(Access(login));
        (await client.PostAsync("/api/v0/auth/logout", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var next = await client.GetAsync("/api/v0/auth/me", Ct);
        next.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await RefreshAsync(Refresh(login))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_EndsOnlyThatSession_NotTheOtherDevices()
    {
        var customer = await NewCustomerAsync();
        var phone = await LoginJsonAsync(customer.Email);
        var laptop = await LoginJsonAsync(customer.Email);

        using var phoneClient = ClientFor(Access(phone));
        await phoneClient.PostAsync("/api/v0/auth/logout", null, Ct);

        (await StatusWith(Access(phone))).ShouldBe(HttpStatusCode.Unauthorized);
        (await StatusWith(Access(laptop))).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_WithoutAToken_Returns401() =>
        (await AnonymousClient.PostAsync("/api/v0/auth/logout", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

    // ---- ban: immediate ----

    [Fact]
    public async Task Ban_MakesTheNextRequestWithTheSameTokenFail_WithoutWaitingForItToExpire()
    {
        var customer = await NewCustomerAsync();
        var login = await LoginJsonAsync(customer.Email);
        (await StatusWith(Access(login), $"/api/v0/individual-customers/{customer.Id}")).ShouldBe(HttpStatusCode.OK);

        var ban = await PostAsync($"/api/v0/users/{customer.Id}/ban", new { reason = "Suspicious transfers" });
        ban.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadJsonAsync(ban)).GetProperty("revokedSessions").GetInt32().ShouldBe(1);

        // The very next request with the very same, still unexpired token:
        var after = await ClientFor(Access(login)).GetAsync($"/api/v0/individual-customers/{customer.Id}", Ct);
        after.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ReadJsonAsync(after)).GetProperty("type").GetString().ShouldBe("urn:problem:authentication");
    }

    [Fact]
    public async Task ABannedUser_CannotLoginOrRefresh_AndEveryDeviceIsLoggedOut()
    {
        var customer = await NewCustomerAsync();
        var phone = await LoginJsonAsync(customer.Email);
        var laptop = await LoginJsonAsync(customer.Email);

        await PostAsync($"/api/v0/users/{customer.Id}/ban", new { reason = "x" });

        (await StatusWith(Access(phone))).ShouldBe(HttpStatusCode.Unauthorized);
        (await StatusWith(Access(laptop))).ShouldBe(HttpStatusCode.Unauthorized);
        (await RefreshAsync(Refresh(phone))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await AnonymousClient.PostAsJsonAsync("/api/v0/auth/login", new { email = customer.Email, password = CustomerPassword }, Json, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Unban_AllowsANewLogin_ButTheOldTokensStayDead()
    {
        var customer = await NewCustomerAsync();
        var before = await LoginJsonAsync(customer.Email);
        await PostAsync($"/api/v0/users/{customer.Id}/ban", new { reason = "x" });

        (await Client.PostAsync($"/api/v0/users/{customer.Id}/unban", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await StatusWith(Access(before))).ShouldBe(HttpStatusCode.Unauthorized);
        (await RefreshAsync(Refresh(before))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await StatusWith(Access(await LoginJsonAsync(customer.Email)))).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ban_DoesNotTouchOtherUsers()
    {
        var banned = await NewCustomerAsync(1);
        var other = await NewCustomerAsync(2);
        var otherLogin = await LoginJsonAsync(other.Email);

        await PostAsync($"/api/v0/users/{banned.Id}/ban", new { reason = "x" });

        (await StatusWith(Access(otherLogin))).ShouldBe(HttpStatusCode.OK);
        (await StatusWith(await LoginAsync(StaffEmail, StaffPassword))).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ABannedStaffMember_IsLockedOutAtOnce_LikeAnyoneElse()
    {
        await PostAsync("/api/v0/users/staff", new { email = "second@bank.test", password = "Second-Passw0rd-1" });
        var second = await LoginJsonAsync("second@bank.test", "Second-Passw0rd-1");
        using var secondClient = ClientFor(Access(second));
        (await secondClient.GetAsync("/api/v0/individual-customers", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        Guid secondId = (await ReadJsonAsync(await Client.GetAsync("/api/v0/users?role=BankStaff&size=50", Ct)))
            .GetProperty("items").EnumerateArray().Single(u => u.GetProperty("email").GetString() == "second@bank.test").GetProperty("id").GetGuid();

        await PostAsync($"/api/v0/users/{secondId}/ban", new { reason = "Left the bank" });

        (await secondClient.GetAsync("/api/v0/individual-customers", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ban_Rules_NoSelfBan_NoDoubleBan_UnknownUser_ReasonRequired()
    {
        var customer = await NewCustomerAsync();
        Guid staffId = (await ReadJsonAsync(await Client.GetAsync("/api/v0/auth/me", Ct))).GetProperty("userId").GetGuid();

        (await PostAsync($"/api/v0/users/{staffId}/ban", new { reason = "x" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await PostAsync($"/api/v0/users/{Guid.NewGuid()}/ban", new { reason = "x" })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await PostAsync($"/api/v0/users/{customer.Id}/ban", new { reason = "" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await PostAsync($"/api/v0/users/{customer.Id}/ban", new { reason = "x" })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PostAsync($"/api/v0/users/{customer.Id}/ban", new { reason = "again" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Client.PostAsync($"/api/v0/users/{Guid.NewGuid()}/unban", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Client.PostAsync($"/api/v0/users/{staffId}/unban", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest); // not banned
    }

    [Fact]
    public async Task OnlyStaff_CanBanUnbanAndListUsers()
    {
        var customer = await NewCustomerAsync();
        using var client = ClientFor(Access(await LoginJsonAsync(customer.Email)));

        (await client.PostAsJsonAsync($"/api/v0/users/{customer.Id}/ban", new { reason = "x" }, Json, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PostAsync($"/api/v0/users/{customer.Id}/unban", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/v0/users", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await AnonymousClient.GetAsync("/api/v0/users", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- deleting a customer ----

    [Fact]
    public async Task DeletingACustomer_MakesTheirTokenFailAtOnce_AndTheyCannotLoginAgain()
    {
        var customer = await NewCustomerAsync();
        var login = await LoginJsonAsync(customer.Email);

        (await Client.DeleteAsync($"/api/v0/individual-customers/{customer.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await StatusWith(Access(login))).ShouldBe(HttpStatusCode.Unauthorized);
        (await RefreshAsync(Refresh(login))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await AnonymousClient.PostAsJsonAsync("/api/v0/auth/login", new { email = customer.Email, password = CustomerPassword }, Json, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- the user list ----

    [Fact]
    public async Task TheUserList_ShowsStaffAndCustomers_WithBanInformation_AndNoSecrets()
    {
        var customer = await NewCustomerAsync();
        await PostAsync($"/api/v0/users/{customer.Id}/ban", new { reason = "Fraud" });

        var response = await Client.GetAsync("/api/v0/users?size=50", Ct);
        var page = await ReadJsonAsync(response);

        page.GetProperty("count").GetInt32().ShouldBe(2);
        var banned = page.GetProperty("items").EnumerateArray().Single(u => u.GetProperty("email").GetString() == customer.Email);
        (banned.GetProperty("isActive").GetBoolean(), banned.GetProperty("banReason").GetString(), banned.GetProperty("role").GetString())
            .ShouldBe((false, "Fraud", "Customer"));
        banned.GetProperty("customerId").GetGuid().ShouldBe(customer.Id);
        banned.GetProperty("customerNumber").GetString()!.ShouldStartWith("IND-");

        string raw = await response.Content.ReadAsStringAsync(Ct);
        raw.ShouldNotContain("passwordHash", Case.Insensitive);
        raw.ShouldNotContain("PBKDF2");
        raw.ShouldNotContain("rowVersion", Case.Insensitive);
        raw.ShouldNotContain("refreshToken", Case.Insensitive);
        raw.ShouldNotContain("TokenHash", Case.Insensitive);
    }

    [Fact]
    public async Task TheUserList_IsPagedAndFiltered()
    {
        await NewCustomerAsync(1);
        await NewCustomerAsync(2);

        var staff = await ReadJsonAsync(await Client.GetAsync("/api/v0/users?role=BankStaff", Ct));
        var customers = await ReadJsonAsync(await Client.GetAsync("/api/v0/users?role=Customer&size=1", Ct));

        staff.GetProperty("count").GetInt32().ShouldBe(1);
        (customers.GetProperty("count").GetInt32(), customers.GetProperty("pages").GetInt32(), customers.GetProperty("items").GetArrayLength()).ShouldBe((2, 2, 1));
        (await Client.GetAsync("/api/v0/users?size=0", Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Client.GetAsync("/api/v0/users?role=99", Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- e-mail verification ----

    [Fact]
    public async Task ANewCustomer_GetsAnEmail_AndItsTokenVerifiesTheAddress_Once()
    {
        var customer = await NewCustomerAsync();
        Emails.Sent.ShouldHaveSingleItem().To.ShouldBe(customer.Email);
        string token = Emails.TokenSentTo(customer.Email);
        using var client = ClientFor(await LoginAsync(customer.Email, CustomerPassword));
        (await ReadJsonAsync(await client.GetAsync("/api/v0/auth/me", Ct))).GetProperty("emailVerified").GetBoolean().ShouldBeFalse();

        var verify = await AnonymousClient.PostAsJsonAsync("/api/v0/auth/verify-email", new { email = customer.Email, token }, Json, Ct);

        verify.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadJsonAsync(await client.GetAsync("/api/v0/auth/me", Ct))).GetProperty("emailVerified").GetBoolean().ShouldBeTrue();

        var again = await AnonymousClient.PostAsJsonAsync("/api/v0/auth/verify-email", new { email = customer.Email, token }, Json, Ct);
        again.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(again)).GetProperty("type").GetString().ShouldBe("urn:problem:business");
    }

    [Fact]
    public async Task AWrongOrForeignToken_VerifiesNothing_AndGivesTheSameAnswerAsAnUnknownAddress()
    {
        var first = await NewCustomerAsync(1);
        var second = await NewCustomerAsync(2);
        string secondsToken = Emails.TokenSentTo(second.Email);

        var wrong = await AnonymousClient.PostAsJsonAsync("/api/v0/auth/verify-email", new { email = first.Email, token = "wrong" }, Json, Ct);
        var foreign = await AnonymousClient.PostAsJsonAsync("/api/v0/auth/verify-email", new { email = first.Email, token = secondsToken }, Json, Ct);
        var unknown = await AnonymousClient.PostAsJsonAsync("/api/v0/auth/verify-email", new { email = "nobody@bank.test", token = "x" }, Json, Ct);

        new[] { wrong, foreign, unknown }.ShouldAllBe(r => r.StatusCode == HttpStatusCode.BadRequest);
        var messages = new[] { wrong, foreign, unknown }.Select(r => ReadJsonAsync(r).Result.GetProperty("detail").GetString()).Distinct();
        messages.Count().ShouldBe(1);
    }

    [Fact]
    public async Task ASignedInUser_CanAskForANewEmail_WhichReplacesTheOldToken()
    {
        var customer = await NewCustomerAsync();
        string oldToken = Emails.TokenSentTo(customer.Email);
        using var client = ClientFor(await LoginAsync(customer.Email, CustomerPassword));

        var resend = await client.PostAsync("/api/v0/auth/send-verification-email", null, Ct);

        resend.StatusCode.ShouldBe(HttpStatusCode.OK);
        Emails.Sent.Count.ShouldBe(2);
        (await AnonymousClient.PostAsJsonAsync("/api/v0/auth/verify-email", new { email = customer.Email, token = oldToken }, Json, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await AnonymousClient.PostAsJsonAsync("/api/v0/auth/verify-email", new { email = customer.Email, token = Emails.TokenSentTo(customer.Email) }, Json, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostAsync("/api/v0/auth/send-verification-email", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest); // already verified
        (await AnonymousClient.PostAsync("/api/v0/auth/send-verification-email", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TheEmail_DoesNotContainThePassword_AndTheLinkPointsAtTheConfiguredPage()
    {
        var customer = await NewCustomerAsync();

        string body = Emails.Sent.Single().Body;

        body.ShouldNotContain(CustomerPassword);
        body.ShouldContain("/verify-email?email=");
        body.ShouldContain(Uri.EscapeDataString(customer.Email));
    }

    // ---- swagger ----

    [Fact]
    public async Task TheSwaggerDocument_LocksTheSessionEndpoints_AndLeavesLoginRefreshAndVerifyOpen()
    {
        var doc = await ReadJsonAsync(await AnonymousClient.GetAsync("/openapi/v0.json", Ct));
        bool Locked(string path, string verb) => doc.GetProperty("paths").GetProperty(path).GetProperty(verb).TryGetProperty("security", out _);

        Locked("/api/v0/auth/logout", "post").ShouldBeTrue();
        Locked("/api/v0/auth/send-verification-email", "post").ShouldBeTrue();
        Locked("/api/v0/users", "get").ShouldBeTrue();
        Locked("/api/v0/users/{id}/ban", "post").ShouldBeTrue();
        Locked("/api/v0/auth/login", "post").ShouldBeFalse();
        Locked("/api/v0/auth/refresh", "post").ShouldBeFalse();
        Locked("/api/v0/auth/verify-email", "post").ShouldBeFalse();
        doc.GetProperty("paths").TryGetProperty("/api/v0/auth/register-customer", out _).ShouldBeFalse(); // a customer is created with its login
    }

    [Fact]
    public async Task TheSwaggerExamples_OfTheCustomerAndUserEndpoints_AreValidRequests()
    {
        var doc = await ReadJsonAsync(await AnonymousClient.GetAsync("/openapi/v0.json", Ct));
        StringContent Example(string path) => new(
            doc.GetProperty("paths").GetProperty(path).GetProperty("post").GetProperty("requestBody").GetProperty("content")
                .GetProperty("application/json").GetProperty("example").GetRawText(),
            System.Text.Encoding.UTF8, "application/json");

        (await Client.PostAsync("/api/v0/individual-customers", Example("/api/v0/individual-customers"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await Client.PostAsync("/api/v0/corporate-customers", Example("/api/v0/corporate-customers"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await Client.PostAsync("/api/v0/users/staff", Example("/api/v0/users/staff"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Created);
    }
}
