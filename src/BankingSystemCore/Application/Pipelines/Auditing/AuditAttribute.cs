namespace BankingSystemCore.Application.Pipelines.Auditing;

/// <summary>
/// Put it on a command that changes state. When the command succeeds, <see cref="AuditBehavior{TRequest,TResponse}"/>
/// writes who did it, what it was and to what. A test fails when a command has neither this attribute nor a place
/// on the list of commands that are not audited on purpose.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AuditAttribute : Attribute
{
    public AuditAttribute(string action, string? targetType = null)
    {
        Action = action;
        TargetType = targetType;
    }

    /// <summary>What happened, e.g. "LoanApplication.Approve".</summary>
    public string Action { get; }

    /// <summary>What it happened to, e.g. "LoanApplication". The id of it is the Id of the request or of the response.</summary>
    public string? TargetType { get; }
}
