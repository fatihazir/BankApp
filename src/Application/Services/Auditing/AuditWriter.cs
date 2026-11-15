using Application.Services.Repositories;
using BankingSystemCore.Application.Pipelines.Auditing;
using Domain.Entities;

namespace Application.Services.Auditing;

/// <summary>Keeps the audit trail in the AuditLogs table.</summary>
public class AuditWriter : IAuditWriter
{
    private readonly IAuditLogRepository _logs;

    public AuditWriter(IAuditLogRepository logs)
    {
        _logs = logs;
    }

    public async Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        await _logs.AddAsync(new AuditLog
        {
            ActorUserId = entry.ActorUserId,
            ActorEmail = entry.ActorEmail,
            ActorRole = entry.ActorRole,
            Action = entry.Action,
            TargetType = entry.TargetType,
            TargetId = entry.TargetId,
            Detail = entry.Detail,
            Succeeded = entry.Succeeded,
            IpAddress = entry.IpAddress,
            TraceId = entry.TraceId
        }, cancellationToken);
    }
}
