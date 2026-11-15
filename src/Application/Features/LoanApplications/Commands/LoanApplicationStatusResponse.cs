using Domain.Entities;

namespace Application.Features.LoanApplications.Commands;

/// <summary>Result of every status change (start review, approve, reject, cancel).</summary>
public class LoanApplicationStatusResponse
{
    public Guid Id { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public LoanApplicationStatus Status { get; set; }
    public DateTime? DecisionDate { get; set; }
    public string? DecisionReason { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public DateTime? UpdateDate { get; set; }
}
