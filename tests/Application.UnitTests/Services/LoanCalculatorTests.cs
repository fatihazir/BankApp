using Application.Services.LoanCalculation;

namespace Application.UnitTests.Services;

public class LoanCalculatorTests
{
    [Fact]
    public void KnownCase_100000_At12Percent_For12Months()
    {
        // Standard annuity example: 1% per month.
        var result = LoanCalculator.Calculate(100_000m, 12m, 12);

        result.MonthlyPayment.ShouldBe(8_884.88m);
        result.TotalRepayment.ShouldBe(106_618.56m, 0.05m); // 12 payments, last one absorbs rounding
        result.TotalInterest.ShouldBe(6_618.56m, 0.05m);
    }

    [Fact]
    public void KnownCase_FirstInstallment_SplitsInterestAndPrincipal()
    {
        var first = LoanCalculator.Calculate(100_000m, 12m, 12).Schedule[0];

        first.Interest.ShouldBe(1_000.00m);     // 1% of 100,000
        first.Principal.ShouldBe(7_884.88m);
        first.RemainingBalance.ShouldBe(92_115.12m);
    }

    [Fact]
    public void KnownCase_SecondInstallment_UsesTheReducedBalance()
    {
        var second = LoanCalculator.Calculate(100_000m, 12m, 12).Schedule[1];

        second.Interest.ShouldBe(921.15m);      // 1% of 92,115.12
        second.Principal.ShouldBe(7_963.73m);
    }

    [Fact]
    public void ZeroInterest_SplitsThePrincipalEvenly()
    {
        var result = LoanCalculator.Calculate(12_000m, 0m, 12);

        result.MonthlyPayment.ShouldBe(1_000m);
        result.TotalInterest.ShouldBe(0m);
        result.TotalRepayment.ShouldBe(12_000m);
        result.Schedule.ShouldAllBe(i => i.Interest == 0m);
    }

    [Fact]
    public void ZeroInterest_WhenNotDivisible_LastInstallmentAbsorbsTheRounding()
    {
        var result = LoanCalculator.Calculate(100m, 0m, 3);

        result.MonthlyPayment.ShouldBe(33.33m);
        result.Schedule[2].Payment.ShouldBe(33.34m);
        result.TotalRepayment.ShouldBe(100m);
    }

    [Fact]
    public void SingleMonth_PaysPrincipalPlusOneMonthOfInterest()
    {
        var result = LoanCalculator.Calculate(1_000m, 24m, 1);

        result.Schedule.Count.ShouldBe(1);
        result.MonthlyPayment.ShouldBe(1_020m);
        result.TotalInterest.ShouldBe(20m);
    }

    [Fact]
    public void HigherRate_MeansHigherPayment()
    {
        LoanCalculator.Calculate(100_000m, 48m, 24).MonthlyPayment
            .ShouldBeGreaterThan(LoanCalculator.Calculate(100_000m, 36m, 24).MonthlyPayment);
    }

    [Fact]
    public void LongerTerm_MeansLowerPayment_ButMoreTotalInterest()
    {
        var shortTerm = LoanCalculator.Calculate(100_000m, 40m, 12);
        var longTerm = LoanCalculator.Calculate(100_000m, 40m, 36);

        longTerm.MonthlyPayment.ShouldBeLessThan(shortTerm.MonthlyPayment);
        longTerm.TotalInterest.ShouldBeGreaterThan(shortTerm.TotalInterest);
    }

    [Theory]
    [InlineData(5_000, 48.00, 3)]
    [InlineData(250_000, 48.00, 36)]
    [InlineData(1_000_000, 36.00, 240)]
    [InlineData(50_000_000, 42.00, 36)]
    [InlineData(200_000_000, 38.00, 120)]
    [InlineData(12_345.67, 17.35, 7)]
    [InlineData(999.99, 0, 5)]
    [InlineData(1_000_000, 100, 60)]
    public void Invariants_HoldForAnyLoan(double amount, double annualRate, int months)
    {
        decimal principal = (decimal)amount;

        var result = LoanCalculator.Calculate(principal, (decimal)annualRate, months);

        // one row per month, numbered 1..n
        result.Schedule.Count.ShouldBe(months);
        result.Schedule.Select(i => i.Number).ShouldBe(Enumerable.Range(1, months));

        // the loan is repaid completely, with no cent left over or missing
        result.Schedule.Sum(i => i.Principal).ShouldBe(principal);
        result.Schedule[^1].RemainingBalance.ShouldBe(0m);

        // totals are consistent with the rows
        result.TotalRepayment.ShouldBe(result.Schedule.Sum(i => i.Payment));
        result.TotalInterest.ShouldBe(result.Schedule.Sum(i => i.Interest));
        (result.TotalRepayment - result.TotalInterest).ShouldBe(principal);

        // every row adds up, balances only go down, nothing is negative
        decimal previousBalance = principal;
        foreach (var row in result.Schedule)
        {
            row.Payment.ShouldBe(row.Principal + row.Interest);
            row.RemainingBalance.ShouldBe(previousBalance - row.Principal);
            row.RemainingBalance.ShouldBeLessThan(previousBalance);
            row.Principal.ShouldBeGreaterThan(0m);
            row.Interest.ShouldBeGreaterThanOrEqualTo(0m);
            row.RemainingBalance.ShouldBeGreaterThanOrEqualTo(0m);
            previousBalance = row.RemainingBalance;
        }

        // All installments but the last equal the regular payment. The last one absorbs the accumulated cent
        // rounding, which stays far below 0.1% of the payment even for long, expensive loans.
        result.Schedule.Take(months - 1).ShouldAllBe(i => i.Payment == result.MonthlyPayment);
        Math.Abs(result.Schedule[^1].Payment - result.MonthlyPayment).ShouldBeLessThan(result.MonthlyPayment * 0.001m);

        // two decimals everywhere
        result.Schedule.ShouldAllBe(i => decimal.Round(i.Payment, 2) == i.Payment && decimal.Round(i.Interest, 2) == i.Interest);
    }

    [Theory]
    [InlineData(0, 10, 12)]
    [InlineData(-1, 10, 12)]
    [InlineData(1000, -1, 12)]
    [InlineData(1000, 10, 0)]
    [InlineData(1000, 10, -3)]
    public void InvalidInput_Throws(double amount, double rate, int months) =>
        Should.Throw<ArgumentOutOfRangeException>(() => LoanCalculator.Calculate((decimal)amount, (decimal)rate, months));
}
