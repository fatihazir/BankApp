using Domain.Entities;

namespace Application.Services.CustomerNaming;

public static class CustomerDisplayName
{
    /// <summary>Person name for individual customers, company name for corporate customers.</summary>
    public static string Of(Customer? customer) => customer switch
    {
        IndividualCustomer individual => $"{individual.FirstName} {individual.LastName}".Trim(),
        CorporateCustomer corporate => corporate.CompanyName,
        _ => string.Empty
    };
}
