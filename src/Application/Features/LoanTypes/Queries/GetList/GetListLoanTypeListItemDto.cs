using Domain.Entities;

namespace Application.Features.LoanTypes.Queries.GetList;

public class GetListLoanTypeListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CustomerSegment Segment { get; set; }
    public IList<LoanSubTypeDto> SubTypes { get; set; } = new List<LoanSubTypeDto>();
}

public class LoanSubTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public int MinTermInMonths { get; set; }
    public int MaxTermInMonths { get; set; }
    public decimal AnnualInterestRate { get; set; }
}
