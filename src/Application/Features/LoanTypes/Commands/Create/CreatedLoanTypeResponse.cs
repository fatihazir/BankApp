using Domain.Entities;

namespace Application.Features.LoanTypes.Commands.Create;

public class CreatedLoanTypeResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CustomerSegment Segment { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreateDate { get; set; }
}
