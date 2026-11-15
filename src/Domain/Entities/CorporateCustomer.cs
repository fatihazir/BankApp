namespace Domain.Entities;

/// <summary>
/// A legal entity (company) customer.
/// </summary>
public class CorporateCustomer : Customer
{
    public string CompanyName { get; set; } = string.Empty;

    public string TaxNumber { get; set; } = string.Empty;

    public string TaxOffice { get; set; } = string.Empty;

    public string? TradeRegistryNumber { get; set; }

    public DateOnly FoundationDate { get; set; }

    public string? Sector { get; set; }

    public int EmployeeCount { get; set; }

    public decimal AnnualRevenue { get; set; }

    public string AuthorizedPersonName { get; set; } = string.Empty;

    public string? AuthorizedPersonTitle { get; set; }

    public string? AuthorizedPersonNationalId { get; set; }
}
