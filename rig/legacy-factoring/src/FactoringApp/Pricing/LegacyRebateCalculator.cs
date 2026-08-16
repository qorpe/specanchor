namespace FactoringApp.Pricing;

public sealed class LegacyRebateCalculator
{
    public decimal CalculateYearEndRebate(decimal totalCommissionPaid, int transactionCount)
    {
        if (transactionCount < 50)
        {
            return 0m;
        }

        var rate = transactionCount >= 200 ? 0.05m : 0.02m;
        return Math.Round(totalCommissionPaid * rate, 2);
    }
}
