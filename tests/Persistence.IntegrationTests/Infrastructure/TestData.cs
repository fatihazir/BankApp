using Application.Features.CorporateCustomers.Commands.Create;
using Application.Features.IndividualCustomers.Commands.Create;
using Domain.Entities;

namespace Persistence.IntegrationTests.Infrastructure;

public static class TestData
{
    /// <summary>The password every test customer is created with.</summary>
    public const string Password = "Customer-Passw0rd1";

    /// <summary>Every customer is a user, and the e-mail is the login name, so it must be unique per customer.</summary>
    public static string Email(int seed) => $"customer{seed}@bank.test";

    public static string CompanyEmail(int seed) => $"company{seed}@bank.test";

    /// <summary>Builds a checksum-valid 11-digit national ID from a seed (0..99999999).</summary>
    public static string NationalId(int seed)
    {
        int[] d = (100_000_000 + seed).ToString().Select(c => c - '0').ToArray();
        int odd = d[0] + d[2] + d[4] + d[6] + d[8];
        int even = d[1] + d[3] + d[5] + d[7];
        int tenth = ((odd * 7 - even) % 10 + 10) % 10;
        int eleventh = (d.Sum() + tenth) % 10;
        return string.Concat(d) + tenth + eleventh;
    }

    public static string TaxNumber(int seed) => (1_000_000_000L + seed).ToString();

    public static CreateIndividualCustomerCommand IndividualCreate(int seed = 1) => new()
    {
        Email = Email(seed),
        Password = Password,
        PhoneNumber = "5551112233",
        Address = "Street 1",
        City = "Istanbul",
        Country = "TR",
        FirstName = "Ali",
        LastName = "Veli",
        NationalId = NationalId(seed),
        DateOfBirth = new DateOnly(1990, 1, 1),
        EmploymentStatus = EmploymentStatus.Employed,
        MonthlyIncome = 50000m
    };

    public static CreateCorporateCustomerCommand CorporateCreate(int seed = 1) => new()
    {
        Email = CompanyEmail(seed),
        Password = Password,
        PhoneNumber = "2121112233",
        Address = "Plaza 1",
        City = "Istanbul",
        Country = "TR",
        CompanyName = "Acme",
        TaxNumber = TaxNumber(seed),
        TaxOffice = "Central Tax Office",
        FoundationDate = new DateOnly(2010, 5, 1),
        EmployeeCount = 50,
        AnnualRevenue = 12_500_000.75m,
        AuthorizedPersonName = "John Doe"
    };
}
