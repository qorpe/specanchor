namespace FactoringApp.Accounting;

public sealed class CarryOverService
{
    public decimal Devir(decimal closingBalance)
    {
        return closingBalance < 0 ? 0m : closingBalance;
    }
}
