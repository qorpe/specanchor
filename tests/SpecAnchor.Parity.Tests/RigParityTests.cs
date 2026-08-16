using System.Globalization;
using FactoringApp.Pricing;
using SpecAnchor.Parity;
using Xunit;

namespace SpecAnchor.Parity.Tests;

/// <summary>
/// The parity harness proven against the rig's Trap A: the NEW side runs the rig's
/// actual CommissionCalculator (banker's rounding); the LEGACY side reproduces
/// usp_CalculateCommission's semantics (T-SQL ROUND = half away from zero, same
/// minimum-commission logic). Invoice 1004 (4010 x 0.0125 = 50.125) must surface as
/// a rounding-class difference — a finding to route to the register, not a defect.
/// Live execution against a running SQL Server is engagement glue (REVISIONS #13).
/// </summary>
public sealed class RigParityTests
{
    private static readonly (string Id, decimal Amount, decimal Rate, decimal Min, int ContractType)[] Samples =
    [
        ("1001", 10_000m, 0.0125m, 150m, 1),
        ("1002", 4_000m, 0.0125m, 150m, 1),
        ("1003", 4_000m, 0.0125m, 150m, 3),
        ("1004", 4_010m, 0.0125m, 10m, 1),
        ("1005", 12_345m, 0.0100m, 100m, 2),
    ];

    private const string BasePolicy = """
        schemaVersion: 1
        excluded_fields: [calculatedAt]
        tolerances: []
        rounding: { commission: half-up }
        known_differences: []
        """;

    private static ParityRecord LegacyRecord(string id, decimal amount, decimal rate, decimal min, int contractType)
    {
        var commission = Math.Round(amount * rate, 2, MidpointRounding.AwayFromZero);
        if (commission < min && contractType != 3)
        {
            commission = min;
        }

        return Record(id, commission, "2026-08-16T01:00:00");
    }

    private static ParityRecord NewRecord(string id, decimal amount, decimal rate, decimal min, int contractType)
    {
        var commission = new CommissionCalculator().Calculate(amount, rate, min, contractType);
        return Record(id, commission, "2026-08-16T02:00:00");
    }

    private static ParityRecord Record(string id, decimal commission, string calculatedAt) =>
        new(id, new Dictionary<string, string>
        {
            ["commission"] = commission.ToString("F2", CultureInfo.InvariantCulture),
            ["status"] = "created",
            ["calculatedAt"] = calculatedAt,
        });

    private static (List<ParityRecord> Legacy, List<ParityRecord> New) BuildSides()
    {
        var legacy = Samples.Select(s => LegacyRecord(s.Id, s.Amount, s.Rate, s.Min, s.ContractType)).ToList();
        var @new = Samples.Select(s => NewRecord(s.Id, s.Amount, s.Rate, s.Min, s.ContractType)).ToList();
        return (legacy, @new);
    }

    [Fact]
    public void Trap_A_surfaces_as_exactly_one_rounding_classified_failure()
    {
        var (legacy, @new) = BuildSides();
        var report = ParityComparator.Compare(legacy, @new, PolicyLoader.Load(BasePolicy));

        Assert.Equal(5, report.SampleSize);
        Assert.Equal(4, report.Passed);
        Assert.Equal(1, report.Failed);
        var failure = Assert.Single(report.Failures);
        Assert.Equal("1004", failure.RecordId);
        Assert.Equal("commission", failure.Field);
        Assert.Equal("50.13", failure.Legacy);
        Assert.Equal("50.12", failure.New);
        Assert.Equal("rounding", failure.Classification);
    }

    [Fact]
    public void The_excluded_timestamp_differs_everywhere_yet_never_fails()
    {
        var (legacy, @new) = BuildSides();
        var report = ParityComparator.Compare(legacy, @new, PolicyLoader.Load(BasePolicy));
        Assert.DoesNotContain(report.Failures, f => f.Field == "calculatedAt");
    }

    [Fact]
    public void A_signed_KD_entry_turns_the_failure_into_a_visible_acceptance()
    {
        var policy = PolicyLoader.Load(BasePolicy.Replace("known_differences: []", """
            known_differences:
              - id: KD-0007
                description: Rounding — legacy half-up, new banker's; 1 kurus delta.
                accepted_by: F. Yilmaz
                date: 2026-08-12
                rule_id: RULE-0042
                field: commission
            """));

        var (legacy, @new) = BuildSides();
        var report = ParityComparator.Compare(legacy, @new, policy);

        Assert.Equal(0, report.Failed);
        Assert.Equal(5, report.Passed);
        var hit = Assert.Single(report.KnownDifferenceHits);
        Assert.Equal("1004", hit.RecordId);
        Assert.Equal("KD-0007", hit.KnownDifferenceId);
    }

    [Fact]
    public void An_absolute_tolerance_also_absorbs_the_rounding_gap()
    {
        var policy = PolicyLoader.Load(BasePolicy.Replace("tolerances: []", """
            tolerances:
              - { field: commission, type: absolute, value: 0.02 }
            """));

        var (legacy, @new) = BuildSides();
        var report = ParityComparator.Compare(legacy, @new, policy);
        Assert.Equal(0, report.Failed);
        Assert.Empty(report.KnownDifferenceHits);
    }

    [Fact]
    public void A_record_missing_on_the_new_side_is_a_missing_record_failure()
    {
        var (legacy, @new) = BuildSides();
        @new.RemoveAll(r => r.RecordId == "1005");
        var report = ParityComparator.Compare(legacy, @new, PolicyLoader.Load(BasePolicy));

        Assert.Contains(report.Failures, f => f.RecordId == "1005" && f.Classification == "missing-record");
        Assert.Equal(2, report.Failed);
    }

    [Fact]
    public void A_genuine_logic_difference_classifies_as_value_mismatch_not_rounding()
    {
        var (legacy, @new) = BuildSides();
        var broken = @new.Select(r => r.RecordId != "1001" ? r : r with
        {
            Fields = new Dictionary<string, string>(r.Fields) { ["commission"] = "999.99" },
        }).ToList();

        var report = ParityComparator.Compare(legacy, broken, PolicyLoader.Load(BasePolicy));
        Assert.Contains(report.Failures,
            f => f.RecordId == "1001" && f.Classification == "value-mismatch");
    }

    [Fact]
    public void The_report_is_deterministic_two_runs_serialize_byte_identical()
    {
        var (legacy, @new) = BuildSides();
        var policy = PolicyLoader.Load(BasePolicy);
        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(ParityComparator.Compare(legacy, @new, policy)),
            System.Text.Json.JsonSerializer.Serialize(ParityComparator.Compare(legacy, @new, policy)));
    }
}
