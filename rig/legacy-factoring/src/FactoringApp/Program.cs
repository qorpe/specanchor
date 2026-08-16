using FactoringApp.Accounting;
using FactoringApp.Assignment;
using FactoringApp.Pricing;

var calculator = new CommissionCalculator();

var samples = new (int InvoiceId, decimal Amount, decimal Rate, decimal Min, int ContractType)[]
{
    (1001, 10_000m, 0.0125m, 150m, 1),
    (1002, 4_000m, 0.0125m, 150m, 1),
    (1003, 4_000m, 0.0125m, 150m, 3),
    (1004, 4_010m, 0.0125m, 10m, 1),
    (1005, 12_345m, 0.0100m, 100m, 2),
};

Console.WriteLine("InvoiceId  Amount     Rate    ContractType  Commission");
foreach (var s in samples)
{
    var commission = calculator.Calculate(s.Amount, s.Rate, s.Min, s.ContractType);
    Console.WriteLine($"{s.InvoiceId}      {s.Amount,9:F2}  {s.Rate:F4}  {s.ContractType}             {commission,8:F2}");
}

var assignments = new AssignmentService();
assignments.RegisterAssignment(1001, 42, DateTime.Today);
TransferHelper.RegisterTransfer(assignments, 1002, 42);
Console.WriteLine($"\nAssignment writes queued: {assignments.PendingWrites.Count}");

var carryOver = new CarryOverService();
Console.WriteLine($"Period carry-over (Devir) for -120.00: {carryOver.Devir(-120m):F2}");
