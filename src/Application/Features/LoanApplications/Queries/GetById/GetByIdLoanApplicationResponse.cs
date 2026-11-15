using Domain.Entities;

namespace Application.Features.LoanApplications.Queries.GetById;

public class GetByIdLoanApplicationResponse
{
    public Guid Id { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }
    public string CustomerNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;

    public Guid LoanSubTypeId { get; set; }
    public string LoanSubTypeName { get; set; } = string.Empty;
    public string LoanTypeName { get; set; } = string.Empty;
    public CustomerSegment Segment { get; set; }

    public decimal RequestedAmount { get; set; }
    public int TermInMonths { get; set; }
    public decimal AnnualInterestRate { get; set; }

    /// <summary>Calculated from the approved amount when approved, otherwise from the requested amount.</summary>
    public decimal MonthlyPayment { get; set; }
    public decimal TotalRepayment { get; set; }
    public decimal TotalInterest { get; set; }

    public string? Purpose { get; set; }

    public LoanApplicationStatus Status { get; set; }
    public DateTime? DecisionDate { get; set; }
    public string? DecisionReason { get; set; }
    public decimal? ApprovedAmount { get; set; }

    public DateTime CreateDate { get; set; }
    public DateTime? UpdateDate { get; set; }
}
