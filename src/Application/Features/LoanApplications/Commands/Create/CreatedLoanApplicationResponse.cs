using Domain.Entities;

namespace Application.Features.LoanApplications.Commands.Create;

public class CreatedLoanApplicationResponse
{
    public Guid Id { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid LoanSubTypeId { get; set; }
    public decimal RequestedAmount { get; set; }
    public int TermInMonths { get; set; }
    public decimal AnnualInterestRate { get; set; }

    /// <summary>Calculated from the requested amount, term and rate.</summary>
    public decimal MonthlyPayment { get; set; }
    public decimal TotalRepayment { get; set; }
    public decimal TotalInterest { get; set; }

    public LoanApplicationStatus Status { get; set; }
    public DateTime CreateDate { get; set; }
}
