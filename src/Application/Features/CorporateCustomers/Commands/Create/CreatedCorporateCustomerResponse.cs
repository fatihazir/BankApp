namespace Application.Features.CorporateCustomers.Commands.Create;

public class CreatedCorporateCustomerResponse
{
    public Guid Id { get; set; }
    public string CustomerNumber { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string TaxNumber { get; set; } = string.Empty;
    public DateTime CreateDate { get; set; }
}
