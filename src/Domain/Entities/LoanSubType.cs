using BankingSystemCore.Repositories;

namespace Domain.Entities;

/// <summary>
/// A concrete loan product under a <see cref="LoanType"/> (e.g. "Mortgage", "Working Capital").
/// Carries the limits and the interest rate that apply to applications for it.
/// </summary>
public class LoanSubType : Entity<Guid>
{
    public Guid LoanTypeId { get; set; }

    /// <summary>Null when the navigation was not loaded (no Include).</summary>
    public LoanType? LoanType { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal MinAmount { get; set; }

    public decimal MaxAmount { get; set; }

    public int MinTermInMonths { get; set; }

    public int MaxTermInMonths { get; set; }

    /// <summary>Annual interest rate in percent (e.g. 42.50).</summary>
    public decimal AnnualInterestRate { get; set; }

    public bool IsActive { get; set; } = true;
}
