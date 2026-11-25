using BankingSystemCore.Repositories;

namespace Domain.Entities;

/// <summary>
/// One line of the audit trail. It is never updated or deleted by the application. CreateDate is the moment it happened.
/// The actor and the target are plain ids (no foreign keys): the record must outlive a deleted user or customer.
/// </summary>
public class AuditLog : Entity<Guid>
{
    public Guid? ActorUserId { get; set; }

    /// <summary>The e-mail of the actor at that moment.</summary>
    public string? ActorEmail { get; set; }

    public string? ActorRole { get; set; }

    /// <summary>What happened, e.g. "LoanApplication.Approve".</summary>
    public string Action { get; set; } = string.Empty;

    public string? TargetType { get; set; }

    public Guid? TargetId { get; set; }

    /// <summary>A short note: a reason, an amount. Never a secret.</summary>
    public string? Detail { get; set; }

    /// <summary>False for a refused or failed attempt that is worth keeping (a wrong password, a reused token).</summary>
    public bool Succeeded { get; set; } = true;

    public string? IpAddress { get; set; }

    /// <summary>Ties the entry to the log lines and the error response of the same request.</summary>
    public string? TraceId { get; set; }
}
