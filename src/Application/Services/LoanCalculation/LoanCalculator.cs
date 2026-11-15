namespace Application.Services.LoanCalculation;

/// <summary>
/// Equal-installment (annuity) loan calculation. Pure and stateless.
/// Interest is calculated monthly on the remaining balance (annual rate / 12). Taxes and fees are not included.
/// </summary>
public static class LoanCalculator
{
    private const int Decimals = 2;

    public static LoanCalculation Calculate(decimal principal, decimal annualInterestRatePercent, int termInMonths)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(principal, 0m);
        ArgumentOutOfRangeException.ThrowIfLessThan(annualInterestRatePercent, 0m);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(termInMonths, 0);

        decimal monthlyRate = annualInterestRatePercent / 12m / 100m;
        decimal monthlyPayment = MonthlyPayment(principal, monthlyRate, termInMonths);

        var schedule = new List<InstallmentPlanItem>(termInMonths);
        decimal balance = principal;
        decimal totalRepayment = 0m;

        for (int number = 1; number <= termInMonths; number++)
        {
            decimal interest = Round(balance * monthlyRate);
            decimal principalPart = number == termInMonths ? balance : monthlyPayment - interest;

            // The last installment absorbs the rounding differences so the balance ends at exactly zero.
            decimal payment = principalPart + interest;
            balance -= principalPart;
            totalRepayment += payment;

            schedule.Add(new InstallmentPlanItem(number, payment, principalPart, interest, balance));
        }

        return new LoanCalculation(monthlyPayment, totalRepayment, totalRepayment - principal, schedule);
    }

    private static decimal MonthlyPayment(decimal principal, decimal monthlyRate, int termInMonths)
    {
        if (monthlyRate == 0m)
            return Round(principal / termInMonths);

        decimal growth = 1m;
        for (int i = 0; i < termInMonths; i++)
            growth *= 1m + monthlyRate;

        return Round(principal * monthlyRate * growth / (growth - 1m));
    }

    private static decimal Round(decimal value) => Math.Round(value, Decimals, MidpointRounding.AwayFromZero);
}

public sealed record LoanCalculation(
    decimal MonthlyPayment,
    decimal TotalRepayment,
    decimal TotalInterest,
    IReadOnlyList<InstallmentPlanItem> Schedule);

public sealed record InstallmentPlanItem(
    int Number,
    decimal Payment,
    decimal Principal,
    decimal Interest,
    decimal RemainingBalance);
