namespace Application.Features.LoanSubTypes.Commands.Create;

public class CreatedLoanSubTypeResponse
{
    public Guid Id { get; set; }
    public Guid LoanTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public int MinTermInMonths { get; set; }
    public int MaxTermInMonths { get; set; }
    public decimal AnnualInterestRate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreateDate { get; set; }
}
