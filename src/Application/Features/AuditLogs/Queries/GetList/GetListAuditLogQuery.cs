using Application.Features.AuditLogs.Profiles;
using Application.Services.Repositories;
using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using BankingSystemCore.Repositories;
using Domain.Entities;
using MediatR;

namespace Application.Features.AuditLogs.Queries.GetList;

/// <summary>Reads the audit trail, newest first. Bank staff only.</summary>
public class GetListAuditLogQuery : IRequest<Paginate<GetListAuditLogListItemDto>>, ISecuredRequest
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.StaffOnly;

    /// <summary>Zero-based page index.</summary>
    public int Index { get; set; } = 0;

    public int Size { get; set; } = 10;

    /// <summary>Only what this user did.</summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>Only what was done to this customer, application or user.</summary>
    public Guid? TargetId { get; set; }

    /// <summary>Only actions that start with this text, e.g. "LoanApplication" or "Auth.Login".</summary>
    public string? Action { get; set; }

    public bool? Succeeded { get; set; }

    /// <summary>From this moment on (UTC), inclusive.</summary>
    public DateTime? From { get; set; }

    /// <summary>Until this moment (UTC), exclusive.</summary>
    public DateTime? To { get; set; }
}

public class GetListAuditLogQueryHandler : IRequestHandler<GetListAuditLogQuery, Paginate<GetListAuditLogListItemDto>>
{
    private readonly IAuditLogRepository _logs;

    public GetListAuditLogQueryHandler(IAuditLogRepository logs)
    {
        _logs = logs;
    }

    public async Task<Paginate<GetListAuditLogListItemDto>> Handle(GetListAuditLogQuery request, CancellationToken cancellationToken)
    {
        Guid? actor = request.ActorUserId;
        Guid? target = request.TargetId;
        string? action = request.Action?.Trim();
        bool? succeeded = request.Succeeded;
        DateTime? from = request.From;
        DateTime? to = request.To;

        Paginate<AuditLog> page = await _logs.GetListAsync(
            predicate: l => (actor == null || l.ActorUserId == actor)
                            && (target == null || l.TargetId == target)
                            && (action == null || action == "" || l.Action.StartsWith(action))
                            && (succeeded == null || l.Succeeded == succeeded)
                            && (from == null || l.CreateDate >= from)
                            && (to == null || l.CreateDate < to),
            orderBy: q => q.OrderByDescending(l => l.CreateDate),
            index: request.Index,
            size: request.Size,
            enableTracking: false,
            cancellationToken: cancellationToken);

        return AuditLogMapper.ToListResponse(page);
    }
}
