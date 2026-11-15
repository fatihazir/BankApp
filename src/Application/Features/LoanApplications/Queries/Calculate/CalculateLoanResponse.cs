namespace Application.Features.LoanApplications.Queries.Calculate;

public class CalculateLoanResponse
{
    public Guid LoanSubTypeId { get; set; }
    public string LoanSubTypeName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int TermInMonths { get; set; }
    public decimal AnnualInterestRate { get; set; }
    public decimal MonthlyPayment { get; set; }
    public decimal TotalRepayment { get; set; }
    public decimal TotalInterest { get; set; }
    public IList<InstallmentDto> Schedule { get; set; } = new List<InstallmentDto>();
}

public class InstallmentDto
{
    public int Number { get; set; }
    public decimal Payment { get; set; }
    public decimal Principal { get; set; }
    public decimal Interest { get; set; }
    public decimal RemainingBalance { get; set; }
}
