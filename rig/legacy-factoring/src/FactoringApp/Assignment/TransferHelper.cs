namespace FactoringApp.Assignment;

public static class TransferHelper
{
    public static void RegisterTransfer(AssignmentService service, int invoiceId, int supplierId)
    {
        service.RegisterAssignment(invoiceId, supplierId, DateTime.Today);
    }
}
