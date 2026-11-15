using System.Linq.Expressions;
using Application.Services.Repositories;
using Application.Services.Security;
using BankingSystemCore.Mailing;
using BankingSystemCore.Security.Hashing;
using BankingSystemCore.Security.Jwt;
using Domain.Entities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;

namespace Application.UnitTests.Support;

/// <summary>The pieces every authentication test needs, wired with a fake clock and fake repositories.</summary>
public sealed class AuthFixture
{
    public const string Password = "Correct-Horse-9";

    public static readonly TokenOptions TokenSettings = new()
    {
        Issuer = "test-issuer", Audience = "test-audience", SecurityKey = new string('k', 40),
        AccessTokenExpirationMinutes = 10, RefreshTokenExpirationMinutes = 30
    };

    public sealed class MutableTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    public IApplicationUserRepository Users { get; } = Substitute.For<IApplicationUserRepository>();
    public IUserSessionRepository Sessions { get; } = Substitute.For<IUserSessionRepository>();
    public IEmailSender Sender { get; } = Substitute.For<IEmailSender>();
    public IPasswordHasher Hasher { get; } = new Pbkdf2PasswordHasher(Options.Create(new PasswordHashingOptions { Iterations = 10 }));
    public MutableTime Time { get; } = new(new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero));
    public EmailVerificationOptions EmailOptions { get; } = new();
    public AuthTokenIssuer Issuer { get; }
    public EmailVerificationService Verification { get; }

    /// <summary>The session the issuer created last (the fake repository gives it an id, like the database would).</summary>
    public UserSession? LastCreatedSession { get; private set; }

    public AuthFixture()
    {
        Issuer = new AuthTokenIssuer(Sessions, new JwtHelper(Options.Create(TokenSettings), Time), Options.Create(TokenSettings), Time);
        Verification = new EmailVerificationService(Sender, Options.Create(EmailOptions), Time);

        Sessions.AddAsync(Arg.Any<UserSession>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var session = call.Arg<UserSession>();
            session.Id = Guid.NewGuid();
            LastCreatedSession = session;
            return session;
        });
    }

    public DateTime Utc => Time.Now.UtcDateTime;

    public IOptions<EmailVerificationOptions> EmailOptionsAccessor => Options.Create(EmailOptions);

    public ApplicationUser UserIs(ApplicationUser? user)
    {
        Users.GetAsync(
                Arg.Any<Expression<Func<ApplicationUser, bool>>>(),
                Arg.Any<Func<IQueryable<ApplicationUser>, IIncludableQueryable<ApplicationUser, object?>>?>(),
                Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(user);
        return user!;
    }

    public UserSession SessionIs(UserSession? session)
    {
        Sessions.GetAsync(
                Arg.Any<Expression<Func<UserSession, bool>>>(),
                Arg.Any<Func<IQueryable<UserSession>, IIncludableQueryable<UserSession, object?>>?>(),
                Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(session);
        return session!;
    }

    public ApplicationUser Staff(bool active = true) => new()
    {
        Id = Guid.NewGuid(), Email = "staff@bank.test", PasswordHash = Hasher.Hash(Password),
        Role = UserRole.BankStaff, IsActive = active, EmailVerified = true
    };

    public IndividualCustomer Customer() => new()
    {
        Id = Guid.NewGuid(), Email = "customer@bank.test", PasswordHash = Hasher.Hash(Password), IsActive = true
    };

    /// <summary>The API's validation rules, except the lifetime (the fake clock is in 2026).</summary>
    public static TokenValidationParameters ValidationParameters()
    {
        var parameters = JwtHelper.CreateValidationParameters(TokenSettings);
        parameters.ValidateLifetime = false;
        return parameters;
    }
}
