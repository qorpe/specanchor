namespace FactoringApp.Assignment;

public sealed class AssignmentService
{
    private readonly List<string> _log = new();

    public void RegisterAssignment(int invoiceId, int supplierId, DateTime notificationDate)
    {
        _log.Add($"INSERT INTO TemlikKayit (InvoiceId, SupplierId, IhbarTarihi) VALUES ({invoiceId}, {supplierId}, '{notificationDate:yyyy-MM-dd}')");
    }

    public IReadOnlyList<string> PendingWrites => _log;
}
