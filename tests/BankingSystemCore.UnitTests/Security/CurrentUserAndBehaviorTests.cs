using System.Security.Claims;
using BankingSystemCore.Application.Pipelines.Authorization;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using BankingSystemCore.Security;
using BankingSystemCore.Security.CurrentUser;
using BankingSystemCore.Security.Hashing;
using BankingSystemCore.Security.Jwt;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BankingSystemCore.UnitTests.Security;

public class CurrentUserAndBehaviorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---- HttpCurrentUser ----

    private static HttpCurrentUser UserWith(params Claim[] claims)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: claims.Length == 0 ? null : "Bearer"))
        };
        return new HttpCurrentUser(new HttpContextAccessor { HttpContext = context });
    }

    [Fact]
    public void AuthenticatedUser_ExposesIdEmailRolesAndClaims()
    {
        Guid id = Guid.NewGuid();
        var user = UserWith(new Claim("sub", id.ToString()), new Claim("email", "a@b.com"), new Claim("role", "Customer"), new Claim("role", "BankStaff"), new Claim("customerId", "42"));

        user.IsAuthenticated.ShouldBeTrue();
        user.Id.ShouldBe(id);
        user.Email.ShouldBe("a@b.com");
        user.Roles.ShouldBe(["Customer", "BankStaff"], ignoreOrder: true);
        user.IsInRole("BankStaff").ShouldBeTrue();
        user.IsInRole("Admin").ShouldBeFalse();
        user.FindClaim("customerId").ShouldBe("42");
        user.FindClaim("missing").ShouldBeNull();
    }

    [Fact]
    public void Roles_AreAlsoReadFromTheMicrosoftRoleClaimType() =>
        UserWith(new Claim(ClaimTypes.Role, "BankStaff")).IsInRole("BankStaff").ShouldBeTrue();

    [Fact]
    public void RoleNames_AreCaseSensitive() =>
        UserWith(new Claim("role", "bankstaff")).IsInRole("BankStaff").ShouldBeFalse();

    [Fact]
    public void NoToken_MeansNotAuthenticated_WithNoRolesAndNoClaims()
    {
        var user = UserWith();

        user.IsAuthenticated.ShouldBeFalse();
        user.Id.ShouldBeNull();
        user.Roles.ShouldBeEmpty();
        user.IsInRole("Customer").ShouldBeFalse();
        user.FindClaim("sub").ShouldBeNull();
    }

    [Fact]
    public void ClaimsOfAnUnauthenticatedPrincipal_AreIgnored()
    {
        // A principal that has claims but was never authenticated must not be trusted.
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("role", "BankStaff")])) };
        var user = new HttpCurrentUser(new HttpContextAccessor { HttpContext = context });

        user.IsAuthenticated.ShouldBeFalse();
        user.FindClaim("role").ShouldBeNull();
    }

    [Fact]
    public void WithoutAnHttpContext_NobodyIsLoggedIn() =>
        new HttpCurrentUser(new HttpContextAccessor()).IsAuthenticated.ShouldBeFalse();

    [Fact]
    public void TheSessionId_IsReadFromTheSidClaim()
    {
        Guid sessionId = Guid.NewGuid();

        UserWith(new Claim("sid", sessionId.ToString())).SessionId.ShouldBe(sessionId);
        UserWith(new Claim("sub", Guid.NewGuid().ToString())).SessionId.ShouldBeNull();
        UserWith(new Claim("sid", "not-a-guid")).SessionId.ShouldBeNull();
    }

    [Fact]
    public void ASubjectThatIsNotAGuid_GivesNoId() => UserWith(new Claim("sub", "not-a-guid")).Id.ShouldBeNull();

    // ---- AuthorizationBehavior ----

    private sealed record Secured(params string[] AllowedRoles) : IRequest<string>, ISecuredRequest
    {
        public IReadOnlyCollection<string> Roles => AllowedRoles;
    }

    private sealed record Open : IRequest<string>;

    /// <summary>A command without a result: it implements the non-generic IRequest.</summary>
    private sealed record SecuredCommand(params string[] AllowedRoles) : IRequest, ISecuredRequest
    {
        public IReadOnlyCollection<string> Roles => AllowedRoles;
    }

    private sealed class Handler : IRequestHandler<Secured, string>, IRequestHandler<Open, string>, IRequestHandler<SecuredCommand>
    {
        public Task Handle(SecuredCommand request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<string> Handle(Secured request, CancellationToken cancellationToken) => Task.FromResult("done");
        public Task<string> Handle(Open request, CancellationToken cancellationToken) => Task.FromResult("done");
    }

    private static IMediator MediatorFor(ICurrentUser user)
    {
        var services = new ServiceCollection();
        services.AddSingleton(user);
        services.AddMediatR(c =>
        {
            c.RegisterServicesFromAssemblyContaining<Handler>();
            c.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
        });
        return services.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    private static HttpCurrentUser Anonymous => UserWith();
    private static HttpCurrentUser Customer => UserWith(new Claim("role", "Customer"));
    private static HttpCurrentUser Staff => UserWith(new Claim("role", "BankStaff"));

    [Fact]
    public async Task NotLoggedIn_IsAuthenticationException_401() =>
        await Should.ThrowAsync<AuthenticationException>(() => MediatorFor(Anonymous).Send(new Secured("Customer"), Ct));

    [Fact]
    public async Task NotLoggedIn_IsRefusedEvenWhenAnyRoleIsAllowed() =>
        await Should.ThrowAsync<AuthenticationException>(() => MediatorFor(Anonymous).Send(new Secured(), Ct));

    [Fact]
    public async Task ACommandWithoutAResult_IsProtectedToo()
    {
        await Should.ThrowAsync<AuthenticationException>(() => MediatorFor(Anonymous).Send(new SecuredCommand("Customer"), Ct));
        await Should.ThrowAsync<AuthorizationException>(() => MediatorFor(Customer).Send(new SecuredCommand("BankStaff"), Ct));
        await Should.NotThrowAsync(() => MediatorFor(Staff).Send(new SecuredCommand("BankStaff"), Ct));
    }

    [Fact]
    public async Task WrongRole_IsAuthorizationException_403() =>
        await Should.ThrowAsync<AuthorizationException>(() => MediatorFor(Customer).Send(new Secured("BankStaff"), Ct));

    [Fact]
    public async Task OneMatchingRoleIsEnough()
    {
        (await MediatorFor(Customer).Send(new Secured("BankStaff", "Customer"), Ct)).ShouldBe("done");
        (await MediatorFor(Staff).Send(new Secured("BankStaff", "Customer"), Ct)).ShouldBe("done");
    }

    [Fact]
    public async Task NoRolesListed_MeansAnyLoggedInUser()
    {
        (await MediatorFor(Customer).Send(new Secured(), Ct)).ShouldBe("done");
        (await MediatorFor(Staff).Send(new Secured(), Ct)).ShouldBe("done");
    }

    [Fact]
    public async Task RequestsThatAreNotSecured_RunWithoutAnyLogin() =>
        (await MediatorFor(Anonymous).Send(new Open(), Ct)).ShouldBe("done");

    [Fact]
    public async Task TheHandler_IsNotReached_WhenAuthorizationFails()
    {
        bool reached = false;
        var behavior = new AuthorizationBehavior<Secured, string>(Customer);

        await Should.ThrowAsync<AuthorizationException>(() => behavior.Handle(
            new Secured("BankStaff"), _ => { reached = true; return Task.FromResult("done"); }, Ct));

        reached.ShouldBeFalse();
    }

    // ---- registration ----

    private static ServiceProvider Services(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSecurityServices(configuration);
        return services.BuildServiceProvider();
    }

    private static readonly Dictionary<string, string?> ValidSettings = new()
    {
        ["Security:Jwt:Issuer"] = "i", ["Security:Jwt:Audience"] = "a", ["Security:Jwt:SecurityKey"] = new string('k', 40)
    };

    [Fact]
    public void Registration_ProvidesTheSecurityServices()
    {
        using var provider = Services(ValidSettings);
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ITokenHelper>().ShouldBeOfType<JwtHelper>();
        scope.ServiceProvider.GetRequiredService<IPasswordHasher>().ShouldBeOfType<Pbkdf2PasswordHasher>();
        scope.ServiceProvider.GetRequiredService<ICurrentUser>().ShouldBeOfType<HttpCurrentUser>();
        scope.ServiceProvider.GetRequiredService<BankingSystemCore.Mailing.IEmailSender>().ShouldBeOfType<BankingSystemCore.Mailing.LoggingEmailSender>();
        scope.ServiceProvider.GetRequiredService<TimeProvider>().ShouldBe(TimeProvider.System);
    }

    [Fact]
    public void Registration_WithAWeakKey_FailsWhenTheOptionsAreFirstUsed()
    {
        var settings = new Dictionary<string, string?>(ValidSettings) { ["Security:Jwt:SecurityKey"] = "short" };
        using var provider = Services(settings);

        Should.Throw<Microsoft.Extensions.Options.OptionsValidationException>(
            () => provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<TokenOptions>>().Value);
    }

    [Fact]
    public void Registration_WithoutAKey_Fails()
    {
        using var provider = Services(new Dictionary<string, string?>());

        Should.Throw<Microsoft.Extensions.Options.OptionsValidationException>(
            () => provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<TokenOptions>>().Value);
    }

    [Fact]
    public void ARealEmailSender_RegisteredAfterwards_ReplacesTheLoggingOne()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(ValidSettings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSecurityServices(configuration);
        services.AddSingleton<BankingSystemCore.Mailing.IEmailSender, FakeSmtpSender>();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<BankingSystemCore.Mailing.IEmailSender>().ShouldBeOfType<FakeSmtpSender>();
    }

    private sealed class FakeSmtpSender : BankingSystemCore.Mailing.IEmailSender
    {
        public Task SendAsync(BankingSystemCore.Mailing.EmailMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public void PasswordHashingCost_CanBeConfigured()
    {
        var settings = new Dictionary<string, string?>(ValidSettings) { ["Security:PasswordHashing:Iterations"] = "123" };
        using var provider = Services(settings);

        provider.GetRequiredService<IPasswordHasher>().Hash("x").ShouldStartWith("PBKDF2-SHA256$123$");
    }
}
