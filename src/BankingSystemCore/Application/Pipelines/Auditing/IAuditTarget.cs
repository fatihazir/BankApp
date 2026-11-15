namespace BankingSystemCore.Application.Pipelines.Auditing;

/// <summary>
/// A response that names what the request created when its id is not called "Id" (a user id, say).
/// Implement it explicitly, so it stays out of JSON and Swagger.
/// </summary>
public interface IAuditTarget
{
    Guid? AuditTargetId { get; }
}
