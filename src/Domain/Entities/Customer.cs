namespace Domain.Entities;

/// <summary>
/// Base class for all customer types. A customer is also a user who can log in, so adding a customer creates
/// the rows of every level at once: Users, ApplicationUsers, Customers and the customer type table.
/// The customer id is the user id.
/// </summary>
public abstract class Customer : ApplicationUser
{
    protected Customer()
    {
        Role = UserRole.Customer;
    }

    /// <summary>Unique, human-readable customer number assigned by the bank.</summary>
    public string CustomerNumber { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string? PostalCode { get; set; }

    /// <summary>Credit score used in loan evaluation. Null until a score is obtained.</summary>
    public int? CreditScore { get; set; }
}
