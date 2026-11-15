namespace BankingSystemCore.Application.Pipelines.Auditing;

/// <summary>One line of the audit trail: who did what to what, from where, and whether it worked.</summary>
public sealed class AuditEntry
{
    public Guid? ActorUserId { get; init; }
    public string? ActorEmail { get; init; }
    public string? ActorRole { get; init; }
    public required string Action { get; init; }
    public string? TargetType { get; init; }
    public Guid? TargetId { get; init; }
    public string? Detail { get; init; }
    public bool Succeeded { get; init; } = true;
    public string? IpAddress { get; init; }
    public string? TraceId { get; init; }
}
