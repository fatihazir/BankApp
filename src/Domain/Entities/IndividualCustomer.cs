namespace Domain.Entities;

/// <summary>
/// A natural person customer.
/// </summary>
public class IndividualCustomer : Customer
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    /// <summary>National identity number (11 digits).</summary>
    public string NationalId { get; set; } = string.Empty;

    public DateOnly DateOfBirth { get; set; }

    public string? MotherName { get; set; }

    public EmploymentStatus EmploymentStatus { get; set; }

    public string? Occupation { get; set; }

    public string? EmployerName { get; set; }

    public decimal MonthlyIncome { get; set; }
}
