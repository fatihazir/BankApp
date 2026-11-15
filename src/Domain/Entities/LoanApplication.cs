using BankingSystemCore.Repositories;

namespace Domain.Entities;

/// <summary>
/// A customer's request for a loan of a given sub type. Works for individual and corporate customers,
/// because it points at the shared <see cref="Customer"/> base.
/// </summary>
public class LoanApplication : Entity<Guid>
{
    /// <summary>Unique, human-readable number shown to the customer.</summary>
    public string ApplicationNumber { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    /// <summary>Null when the navigation was not loaded (no Include).</summary>
    public Customer? Customer { get; set; }

    public Guid LoanSubTypeId { get; set; }

    /// <summary>Null when the navigation was not loaded (no Include).</summary>
    public LoanSubType? LoanSubType { get; set; }

    public decimal RequestedAmount { get; set; }

    public int TermInMonths { get; set; }

    /// <summary>Interest rate of the sub type at submission time. Later rate changes do not affect it.</summary>
    public decimal AnnualInterestRate { get; set; }

    public string? Purpose { get; set; }

    public LoanApplicationStatus Status { get; set; }

    public DateTime? DecisionDate { get; set; }

    /// <summary>Why the application was approved, rejected or cancelled.</summary>
    public string? DecisionReason { get; set; }

    public decimal? ApprovedAmount { get; set; }
}
