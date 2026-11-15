using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Services.Repositories;
using BankingSystemCore.Security.Tokens;
using BankingSystemCore.Security.Hashing;
using BankingSystemCore.Security.Jwt;
using Domain.Entities;
using BankingSystemCore.Mailing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace WebApi.IntegrationTests.Infrastructure;

/// <summary>
/// Starts the real WebApi in memory against the test database. The database is emptied before every test.
/// <see cref="Client"/> is logged in as bank staff; use <see cref="AnonymousClient"/> or <see cref="ClientFor"/> for others.
/// </summary>
public abstract class ApiTestBase : IAsyncLifetime
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public const string StaffEmail = "staff@bank.test";
    public const string StaffPassword = "Staff-Passw0rd-1";
    public const string TestKey = "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk"; // 40 chars
    public const string TestIssuer = "test-issuer";
    public const string TestAudience = "test-audience";

    private readonly SqlServerFixture _database;
    private readonly WebApplicationFactory<Program> _factory;

    /// <summary>Logged in as bank staff.</summary>
    protected HttpClient Client { get; }

    /// <summary>The e-mails the API "sent": there is no mail server, so tests read the links from here.</summary>
    protected CapturingEmailSender Emails { get; } = new();

    /// <summary>No token at all.</summary>
    protected HttpClient AnonymousClient { get; }

    protected IServiceProvider Services => _factory.Services;

    protected ApiTestBase(SqlServerFixture database, Dictionary<string, string?>? extraSettings = null)
    {
        _database = database;
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            // UseSetting is visible to code that reads builder.Configuration in Program.cs.
            builder.UseSetting("ConnectionStrings:BankAppDb", database.ConnectionString);
            builder.UseSetting("Security:Jwt:Issuer", TestIssuer);
            builder.UseSetting("Security:Jwt:Audience", TestAudience);
            builder.UseSetting("Security:Jwt:SecurityKey", TestKey);
            builder.UseSetting("Security:PasswordHashing:Iterations", "10"); // fast hashing in tests only
            // Every test logs in from the same address, so the rate limit is off unless a test asks for it.
            builder.UseSetting("RateLimiting:Enabled", "false");
            // The cleanup worker would touch the database while a test resets it; one test switches it on.
            builder.UseSetting("Security:SessionCleanup:Enabled", "false");
            foreach ((string key, string? value) in extraSettings ?? [])
                builder.UseSetting(key, value);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(Emails);
            });
        });
        Client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AnonymousClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    public async ValueTask InitializeAsync()
    {
        await _database.ResetAsync();

        // The reset removed every user, so create the staff account the tests work with.
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IApplicationUserRepository>().AddAsync(new ApplicationUser
            {
                Email = StaffEmail,
                PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash(StaffPassword),
                Role = UserRole.BankStaff,
                IsActive = true,
                EmailVerified = true
            }, Ct);
        }

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(StaffEmail, StaffPassword));
    }

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        AnonymousClient.Dispose();
        _factory.Dispose();
        return ValueTask.CompletedTask;
    }

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected Task<HttpResponseMessage> PostAsync<T>(string url, T body) => Client.PostAsJsonAsync(url, body, Json, Ct);

    protected Task<HttpResponseMessage> PutAsync<T>(string url, T body) => Client.PutAsJsonAsync(url, body, Json, Ct);

    // The body is read as text, so a test can read the same response more than once.
    protected async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync(Ct), Json)!;

    protected async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;

    /// <summary>Logs in over HTTP and returns the access token.</summary>
    protected async Task<string> LoginAsync(string email, string password)
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/v0/auth/login", new { email, password }, Json, Ct);
        response.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(response)).GetProperty("accessToken").GetString()!;
    }

    /// <summary>A client that sends the given token.</summary>
    protected HttpClient ClientFor(string token)
    {
        HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public const string CustomerPassword = "Customer-Passw0rd-1";

    /// <summary>A client logged in as a customer. The customer has to exist already (staff create customers).</summary>
    protected async Task<HttpClient> CustomerClientAsync(string email, string password = CustomerPassword) =>
        ClientFor(await LoginAsync(email, password));

    /// <summary>
    /// A token for tests that need a role or a claim a login cannot produce (an unknown role, say). It is a real one:
    /// signed with the real key and tied to a real session of a real user, because the API checks the session.
    /// </summary>
    protected async Task<string> IssueTokenAsync(string role)
    {
        using IServiceScope scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IApplicationUserRepository>();
        var sessions = scope.ServiceProvider.GetRequiredService<IUserSessionRepository>();

        var user = await users.AddAsync(new ApplicationUser
        {
            Email = $"issued-{Guid.NewGuid():N}@bank.test", PasswordHash = "not-a-real-hash", Role = UserRole.BankStaff, IsActive = true
        }, Ct);
        var session = await sessions.AddAsync(new UserSession
        {
            UserId = user.Id, RefreshTokenHash = BankingSystemCore.Security.Tokens.SecureToken.Hash(Guid.NewGuid().ToString()),
            RefreshTokenExpiresAt = DateTime.UtcNow.AddMinutes(30)
        }, Ct);

        return scope.ServiceProvider.GetRequiredService<ITokenHelper>()
            .CreateToken(new TokenUser(user.Id, user.Email, [role], SessionId: session.Id)).Token;
    }
}
