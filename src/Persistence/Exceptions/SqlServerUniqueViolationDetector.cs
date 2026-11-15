using BankingSystemCore.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Persistence.Exceptions;

/// <summary>SQL Server reports a duplicate value in a unique index (2601) or a unique/primary key constraint (2627).</summary>
public sealed class SqlServerUniqueViolationDetector : IUniqueViolationDetector
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation };
}
