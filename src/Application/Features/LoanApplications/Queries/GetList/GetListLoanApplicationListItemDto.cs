using Domain.Entities;

namespace Application.Features.LoanApplications.Queries.GetList;

public class GetListLoanApplicationListItemDto
{
    public Guid Id { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string LoanTypeName { get; set; } = string.Empty;
    public CustomerSegment Segment { get; set; }
    public string LoanSubTypeName { get; set; } = string.Empty;
    public decimal RequestedAmount { get; set; }
    public int TermInMonths { get; set; }
    public LoanApplicationStatus Status { get; set; }
    public DateTime CreateDate { get; set; }
}
