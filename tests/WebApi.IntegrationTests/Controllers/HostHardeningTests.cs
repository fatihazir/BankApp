using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WebApi.IntegrationTests.Infrastructure;

namespace WebApi.IntegrationTests.Controllers;

/// <summary>Security headers, health, API versions: the same in every environment.</summary>
[Collection(DatabaseCollection.Name)]
public class HostHardeningTests(SqlServerFixture database) : ApiTestBase(database)
{
    // ---- security headers ----

    [Fact]
    public async Task EveryResponse_CarriesTheSecurityHeaders_EvenAnError()
    {
        var unauthorized = await AnonymousClient.GetAsync("/api/v0/auth/me", Ct);
        var notFound = await AnonymousClient.GetAsync("/api/v0/nothing-here", Ct);
        var ok = await Client.GetAsync("/api/v0/auth/me", Ct);

        unauthorized.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        notFound.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        foreach (var response in new[] { unauthorized, notFound, ok })
        {
            response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
            response.Headers.GetValues("X-Frame-Options").ShouldBe(["DENY"]);
            response.Headers.GetValues("Referrer-Policy").ShouldBe(["no-referrer"]);
            response.Headers.GetValues("Content-Security-Policy").Single().ShouldContain("default-src 'none'");
            response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task TheSwaggerPage_NeedsScripts_SoItHasNoContentSecurityPolicy_ButTheOtherHeaders()
    {
        var response = await AnonymousClient.GetAsync("/swagger/index.html", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("Content-Security-Policy").ShouldBeFalse();
        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
    }

    // ---- HSTS and proxies (the middleware only acts on HTTPS outside development, so the settings are checked) ----

    [Fact]
    public void Hsts_AsksBrowsersToUseHttpsForAYear()
    {
        var hsts = Services.GetRequiredService<IOptions<HstsOptions>>().Value;

        hsts.MaxAge.ShouldBe(TimeSpan.FromDays(365));
        hsts.IncludeSubDomains.ShouldBeTrue();
    }

    [Fact]
    public void ForwardedHeaders_AreReadForTheClientAddressAndTheScheme()
    {
        var forwarded = Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        forwarded.ForwardedHeaders.ShouldBe(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost);
    }

    // ---- health ----

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task TheHealthEndpoints_AnswerWithoutALogin_AndOnlyWithAStatus(string url)
    {
        var response = await AnonymousClient.GetAsync(url, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldBe("Healthy");
    }
}

/// <summary>The limit on the anonymous endpoints, switched on with small numbers.</summary>
[Collection(DatabaseCollection.Name)]
public class RateLimitTests(SqlServerFixture database) : ApiTestBase(database, new()
{
    ["RateLimiting:Enabled"] = "true",
    ["RateLimiting:AnonymousAuth:PermitLimit"] = "5",
    ["RateLimiting:AnonymousAuth:WindowSeconds"] = "60"
})
{
    [Fact]
    public async Task Login_IsLimitedPerAddress_WithAProblemAnd_RetryAfter()
    {
        // The test setup used one login already.
        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage? refused = null;
        for (int i = 0; i < 10 && refused is null; i++)
        {
            var response = await AnonymousClient.PostAsJsonAsync("/api/v0/auth/login", new { email = StaffEmail, password = "Wrong-Password-1" }, Json, Ct);
            statuses.Add(response.StatusCode);
            if (response.StatusCode == HttpStatusCode.TooManyRequests) refused = response;
        }

        refused.ShouldNotBeNull();
        statuses.Count(s => s != HttpStatusCode.TooManyRequests).ShouldBe(4); // 5 allowed in total, 1 used by the setup
        refused.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        (await ReadJsonAsync(refused)).GetProperty("type").GetString().ShouldBe("urn:problem:rate-limit");
        refused.Headers.RetryAfter.ShouldNotBeNull();
    }

    [Fact]
    public async Task RefreshAndVerifyEmail_AreLimitedToo_ButLoggedInEndpointsAreNot()
    {
        for (int i = 0; i < 5; i++) await AnonymousClient.PostAsJsonAsync("/api/v0/auth/refresh", new { refreshToken = "x" }, Json, Ct);
        for (int i = 0; i < 5; i++) await AnonymousClient.PostAsJsonAsync("/api/v0/auth/verify-email", new { email = "a@b.com", token = "x" }, Json, Ct);

        (await AnonymousClient.PostAsJsonAsync("/api/v0/auth/refresh", new { refreshToken = "x" }, Json, Ct)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await AnonymousClient.PostAsJsonAsync("/api/v0/auth/verify-email", new { email = "a@b.com", token = "x" }, Json, Ct)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        for (int i = 0; i < 10; i++)
            (await Client.GetAsync("/api/v0/auth/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

/// <summary>CORS is closed unless an origin is listed.</summary>
[Collection(DatabaseCollection.Name)]
public class CorsTests(SqlServerFixture database) : ApiTestBase(database, new() { ["Cors:AllowedOrigins:0"] = "https://app.example.com" })
{
    private async Task<HttpResponseMessage> PreflightFrom(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v0/auth/login");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");
        return await AnonymousClient.SendAsync(request, Ct);
    }

    [Fact]
    public async Task AListedOrigin_MayCallTheApi()
    {
        var response = await PreflightFrom("https://app.example.com");

        response.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe(["https://app.example.com"]);
        response.Headers.GetValues("Access-Control-Allow-Headers").Single().ShouldContain("Content-Type", Case.Insensitive);
    }

    [Fact]
    public async Task AnyOtherOrigin_GetsNoPermission()
    {
        var response = await PreflightFrom("https://evil.example.com");

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }
}

/// <summary>With nothing configured the API answers no cross-origin request.</summary>
[Collection(DatabaseCollection.Name)]
public class CorsClosedByDefaultTests(SqlServerFixture database) : ApiTestBase(database)
{
    [Fact]
    public async Task WithoutConfiguration_NoOriginIsAllowed()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v0/auth/login");
        request.Headers.Add("Origin", "https://app.example.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await AnonymousClient.SendAsync(request, Ct);

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }
}

/// <summary>The cleanup worker, switched on with a one second interval.</summary>
[Collection(DatabaseCollection.Name)]
public class SessionCleanupWorkerTests(SqlServerFixture database) : ApiTestBase(database, new()
{
    ["Security:SessionCleanup:Enabled"] = "true",
    ["Security:SessionCleanup:Interval"] = "00:00:01"
})
{
    [Fact]
    public async Task TheWorker_DeletesOldEndedSessions_ButKeepsTheLiveOnes()
    {
        Guid oldId, liveId;
        using (var scope = Services.CreateScope())
        {
            var sessions = scope.ServiceProvider.GetRequiredService<Application.Services.Repositories.IUserSessionRepository>();
            var users = scope.ServiceProvider.GetRequiredService<Application.Services.Repositories.IApplicationUserRepository>();
            Guid staffId = (await users.GetAsync(u => u.Email == StaffEmail, cancellationToken: Ct))!.Id;

            oldId = (await sessions.AddAsync(new Domain.Entities.UserSession
            {
                UserId = staffId, RefreshTokenHash = BankingSystemCore.Security.Tokens.SecureToken.Hash("old"),
                RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(-30), RevokedDate = DateTime.UtcNow.AddDays(-30)
            }, Ct)).Id;
            liveId = (await sessions.GetAsync(s => s.UserId == staffId && s.RevokedDate == null, cancellationToken: Ct))!.Id;
        }

        // The worker runs every second; give it a few turns.
        bool gone = false;
        for (int i = 0; i < 20 && !gone; i++)
        {
            await Task.Delay(500, Ct);
            using var scope = Services.CreateScope();
            var sessions = scope.ServiceProvider.GetRequiredService<Application.Services.Repositories.IUserSessionRepository>();
            gone = await sessions.GetAsync(s => s.Id == oldId, cancellationToken: Ct) is null;
        }

        gone.ShouldBeTrue();
        using var check = Services.CreateScope();
        (await check.ServiceProvider.GetRequiredService<Application.Services.Repositories.IUserSessionRepository>()
            .GetAsync(s => s.Id == liveId, cancellationToken: Ct)).ShouldNotBeNull();
    }
}
