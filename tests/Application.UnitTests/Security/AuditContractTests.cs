using Application.Features.Auth.Commands.Login;
using Application.Features.Auth.Commands.Refresh;
using Application.Features.Auth.Commands.SendEmailVerification;
using Application.Features.Auth.Commands.VerifyEmail;
using BankingSystemCore.Application.Pipelines.Auditing;
using MediatR;

namespace Application.UnitTests.Security;

/// <summary>
/// Guards against a command that changes state and leaves no trace. Every command is audited by its
/// <see cref="AuditAttribute"/> unless it is on the short list below, and a command on that list says why.
/// </summary>
public class AuditContractTests
{
    /// <summary>Commands that are not audited by the attribute. Adding to this list is a decision.</summary>
    private static readonly Type[] NotAuditedByAttribute =
    [
        typeof(LoginCommand),                  // writes its own entries (success, failure, lockout), see SecurityAudit
        typeof(RefreshTokenCommand),           // writes its own entry when a token is reused
        typeof(VerifyEmailCommand),            // nobody is logged in and the caller can send anything
        typeof(SendEmailVerificationCommand)   // sends a mail, changes nothing a person decided
    ];

    private static readonly Type[] Commands = typeof(LoginCommand).Assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IBaseRequest).IsAssignableFrom(t) && t.Name.EndsWith("Command"))
        .ToArray();

    [Fact]
    public void ThereAreCommandsToCheck() => Commands.Length.ShouldBeGreaterThan(20);

    [Fact]
    public void EveryCommand_IsAudited_UnlessItIsOnTheList()
    {
        var missing = Commands
            .Where(t => !NotAuditedByAttribute.Contains(t) && !Attribute.IsDefined(t, typeof(AuditAttribute), inherit: false))
            .Select(t => t.Name)
            .ToList();

        missing.ShouldBeEmpty("These commands change state but are not audited. Put [Audit(...)] on them: " + string.Join(", ", missing));
    }

    [Fact]
    public void QueriesAreNeverAudited() =>
        typeof(LoginCommand).Assembly.GetTypes()
            .Where(t => t.Name.EndsWith("Query") && Attribute.IsDefined(t, typeof(AuditAttribute), inherit: false))
            .ShouldBeEmpty();

    [Fact]
    public void TheActionNames_AreUnique()
    {
        var actions = Commands
            .Select(t => ((AuditAttribute?)Attribute.GetCustomAttribute(t, typeof(AuditAttribute), inherit: false))?.Action)
            .Where(a => a is not null)
            .ToList();

        actions.Distinct().Count().ShouldBe(actions.Count);
    }

    [Fact]
    public void AnAuditNote_NeverComesFromASecretField()
    {
        // The note is built by the request itself. Requests that carry a password must not offer one.
        var withPassword = Commands.Where(t => t.GetProperty("Password") is not null);

        withPassword.ShouldAllBe(t => !typeof(IAuditDetail).IsAssignableFrom(t));
    }
}
