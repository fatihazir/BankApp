using Microsoft.EntityFrameworkCore;

namespace BankingSystemCore.Repositories;

/// <summary>
/// Tells whether a failed save was caused by a unique index or key (two requests created the same value at once).
/// How a database reports that is provider specific, so the provider's project implements this, not the kernel.
/// </summary>
public interface IUniqueViolationDetector
{
    bool IsUniqueViolation(DbUpdateException exception);
}
