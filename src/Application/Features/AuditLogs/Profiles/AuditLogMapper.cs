using Application.Features.AuditLogs.Queries.GetList;
using BankingSystemCore.Repositories;
using Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Application.Features.AuditLogs.Profiles;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class AuditLogMapper
{
    public static partial GetListAuditLogListItemDto ToListItem(AuditLog entity);

    public static partial Paginate<GetListAuditLogListItemDto> ToListResponse(Paginate<AuditLog> page);
}
