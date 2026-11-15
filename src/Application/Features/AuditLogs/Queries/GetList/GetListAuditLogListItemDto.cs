namespace Application.Features.AuditLogs.Queries.GetList;

public class GetListAuditLogListItemDto
{
    public Guid Id { get; set; }

    /// <summary>When it happened (UTC).</summary>
    public DateTime CreateDate { get; set; }

    public Guid? ActorUserId { get; set; }
    public string? ActorEmail { get; set; }
    public string? ActorRole { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? TargetType { get; set; }
    public Guid? TargetId { get; set; }
    public string? Detail { get; set; }
    public bool Succeeded { get; set; }
    public string? IpAddress { get; set; }
    public string? TraceId { get; set; }
}
