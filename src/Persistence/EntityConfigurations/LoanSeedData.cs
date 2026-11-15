using Domain.Entities;

namespace Persistence.EntityConfigurations;

/// <summary>
/// Initial loan types and sub types, inserted by the migration. The limits and rates are starting values
/// and can be changed in the database. Ids and dates are fixed because seed data must be deterministic.
/// </summary>
public static class LoanSeedData
{
    private static readonly DateTime SeededAt = new(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc);

    public static readonly Guid IndividualLoanTypeId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    public static readonly Guid CorporateLoanTypeId = Guid.Parse("a0000000-0000-0000-0000-000000000002");

    public static readonly Guid PersonalLoanId = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    public static readonly Guid MortgageLoanId = Guid.Parse("b0000000-0000-0000-0000-000000000002");
    public static readonly Guid AutoLoanId = Guid.Parse("b0000000-0000-0000-0000-000000000003");
    public static readonly Guid WorkingCapitalLoanId = Guid.Parse("b0000000-0000-0000-0000-000000000004");
    public static readonly Guid InvestmentLoanId = Guid.Parse("b0000000-0000-0000-0000-000000000005");
    public static readonly Guid CommercialLoanId = Guid.Parse("b0000000-0000-0000-0000-000000000006");

    public static LoanType[] LoanTypes() =>
    [
        new() { Id = IndividualLoanTypeId, Name = "Individual Loan", Description = "Loans for individual customers.", Segment = CustomerSegment.Individual, IsActive = true, CreateDate = SeededAt },
        new() { Id = CorporateLoanTypeId, Name = "Corporate Loan", Description = "Loans for corporate customers.", Segment = CustomerSegment.Corporate, IsActive = true, CreateDate = SeededAt }
    ];

    public static LoanSubType[] LoanSubTypes() =>
    [
        SubType(PersonalLoanId, IndividualLoanTypeId, "Personal Loan", "General purpose loan.", 5_000, 1_000_000, 3, 60, 48.00m),
        SubType(MortgageLoanId, IndividualLoanTypeId, "Mortgage Loan", "Loan for buying a home.", 100_000, 10_000_000, 12, 240, 36.00m),
        SubType(AutoLoanId, IndividualLoanTypeId, "Auto Loan", "Loan for buying a vehicle.", 50_000, 3_000_000, 6, 60, 40.00m),
        SubType(WorkingCapitalLoanId, CorporateLoanTypeId, "Working Capital Loan", "Short term financing of daily operations.", 100_000, 50_000_000, 3, 36, 42.00m),
        SubType(InvestmentLoanId, CorporateLoanTypeId, "Investment Loan", "Long term financing of equipment and facilities.", 500_000, 200_000_000, 12, 120, 38.00m),
        SubType(CommercialLoanId, CorporateLoanTypeId, "Commercial Loan", "General commercial financing.", 50_000, 20_000_000, 3, 60, 44.00m)
    ];

    private static LoanSubType SubType(Guid id, Guid loanTypeId, string name, string description,
        decimal minAmount, decimal maxAmount, int minTerm, int maxTerm, decimal rate) => new()
    {
        Id = id,
        LoanTypeId = loanTypeId,
        Name = name,
        Description = description,
        MinAmount = minAmount,
        MaxAmount = maxAmount,
        MinTermInMonths = minTerm,
        MaxTermInMonths = maxTerm,
        AnnualInterestRate = rate,
        IsActive = true,
        CreateDate = SeededAt
    };
}
