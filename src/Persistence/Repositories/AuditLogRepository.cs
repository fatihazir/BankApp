using Application.Services.Repositories;
using BankingSystemCore.Repositories;
using Domain.Entities;
using Persistence.Contexts;

namespace Persistence.Repositories;

public class AuditLogRepository : EfRepositoryBase<AuditLog, Guid, BankAppDbContext>, IAuditLogRepository
{
    public AuditLogRepository(BankAppDbContext context, TimeProvider time) : base(context, time)
    {
    }
}
