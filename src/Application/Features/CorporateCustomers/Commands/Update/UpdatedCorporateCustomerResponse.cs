namespace Application.Features.CorporateCustomers.Commands.Update;

public class UpdatedCorporateCustomerResponse
{
    public Guid Id { get; set; }
    public string CustomerNumber { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public DateTime? UpdateDate { get; set; }
}
