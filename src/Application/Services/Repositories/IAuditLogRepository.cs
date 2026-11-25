using BankingSystemCore.Repositories;
using Domain.Entities;

namespace Application.Services.Repositories;

public interface IAuditLogRepository : IAsyncRepository<AuditLog, Guid>
{
}
