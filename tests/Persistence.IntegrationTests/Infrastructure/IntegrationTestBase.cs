using Application;
using BankingSystemCore.Mailing;
using BankingSystemCore.Security;
using BankingSystemCore.Security.CurrentUser;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Persistence.Contexts;

namespace Persistence.IntegrationTests.Infrastructure;

/// <summary>
/// Builds the same DI container the WebApi will use, pointed at the test database.
/// The database is emptied before every test.
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private readonly SqlServerFixture _database;
    private readonly ServiceProvider _services;

    /// <summary>Who is logged in. Staff unless the test says otherwise.</summary>
    protected TestCurrentUser CurrentUser { get; } = new();

    /// <summary>The e-mails the application "sent" during the test.</summary>
    protected CapturingEmailSender Emails { get; } = new();

    /// <summary>The clock of the application. It stands still until the test moves it.</summary>
    protected TestClock Clock { get; } = new();

    protected IntegrationTestBase(SqlServerFixture database, Dictionary<string, string?>? extraSettings = null)
    {
        _database = database;

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:BankAppDb"] = database.ConnectionString,
                ["Security:Jwt:Issuer"] = "test-issuer",
                ["Security:Jwt:Audience"] = "test-audience",
                ["Security:Jwt:SecurityKey"] = new string('k', 40),
                ["Security:PasswordHashing:Iterations"] = "10" // fast hashing in tests only
            })
            .AddInMemoryCollection(extraSettings ?? new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(configuration);
        services.AddApplicationServices();
        services.AddPersistenceServices(configuration);
        services.AddSecurityServices(configuration);

        // There is no HTTP request in these tests, so the logged-in user is a test double.
        services.RemoveAll<ICurrentUser>();
        services.AddSingleton<ICurrentUser>(CurrentUser);

        // The application reads time from this clock only, so a test can move it.
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(Clock);

        // No mail server in tests: the e-mails are kept so the tests can read the links.
        services.RemoveAll<IEmailSender>();
        services.AddSingleton<IEmailSender>(Emails);

        // Fails fast if any registration is missing or has a wrong lifetime.
        _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    public async ValueTask InitializeAsync() => await _database.ResetAsync();

    public ValueTask DisposeAsync()
    {
        _services.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>Sends a request through the full MediatR pipeline (validation + handler) in its own scope.</summary>
    protected async Task<TResponse> Send<TResponse>(IRequest<TResponse> request)
    {
        using IServiceScope scope = _services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(request, TestContext.Current.CancellationToken);
    }

    /// <summary>A database context of its own (a separate "request"), to play two requests against each other.</summary>
    protected IServiceScope ScopedDb(out BankAppDbContext db)
    {
        IServiceScope scope = _services.CreateScope();
        db = scope.ServiceProvider.GetRequiredService<BankAppDbContext>();
        return scope;
    }

    /// <summary>Gives the test access to a service, e.g. the session validator that the API asks on every request.</summary>
    protected T Resolve<T>(IServiceScope scope) where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

    protected IServiceScope NewScope() => _services.CreateScope();

    /// <summary>The same for a command that returns nothing.</summary>
    protected async Task Send(IRequest request)
    {
        using IServiceScope scope = _services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Runs an action against the database directly, bypassing the application layer.</summary>
    protected async Task<T> UsingDbContext<T>(Func<BankAppDbContext, Task<T>> action)
    {
        using IServiceScope scope = _services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<BankAppDbContext>());
    }
}
