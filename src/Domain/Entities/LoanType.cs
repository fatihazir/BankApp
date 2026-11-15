using BankingSystemCore.Repositories;

namespace Domain.Entities;

/// <summary>
/// Top level loan category (e.g. "Individual Loan", "Corporate Loan"). Each category belongs to one customer segment.
/// </summary>
public class LoanType : Entity<Guid>
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Only customers of this segment can apply for loans under this type.</summary>
    public CustomerSegment Segment { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<LoanSubType> SubTypes { get; set; } = new List<LoanSubType>();
}
