namespace FactoringApp.Pricing;

public sealed class CommissionCalculator
{
    public decimal Calculate(decimal invoiceAmount, decimal commissionRate, decimal minCommission, int contractType)
    {
        var commission = Math.Round(invoiceAmount * commissionRate, 2);

        if (commission < minCommission && contractType != 3)
        {
            commission = minCommission;
        }

        return commission;
    }
}
