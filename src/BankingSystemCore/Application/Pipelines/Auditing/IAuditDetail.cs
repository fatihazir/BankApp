namespace BankingSystemCore.Application.Pipelines.Auditing;

/// <summary>
/// A request that adds a short, safe note to its audit entry (a reason, an amount).
/// Never return a password, a token or a national ID. Implement it explicitly, so it stays out of JSON and Swagger.
/// </summary>
public interface IAuditDetail
{
    string? AuditDetail { get; }
}
