namespace BankingSystemCore.Application.Pipelines.Auditing;

/// <summary>Stores audit entries. The application decides where (a table, a log stream, a queue).</summary>
public interface IAuditWriter
{
    Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
