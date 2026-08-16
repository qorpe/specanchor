using SpecAnchor.Parity;
using Xunit;

namespace SpecAnchor.Parity.Tests;

/// <summary>
/// Edge behaviour of the comparator, written to kill the mutants the rig scenario
/// does not reach: relative tolerances, both directions of missing records, field
/// union across sides, ordering, and non-numeric mismatches.
/// </summary>
public sealed class ComparatorEdgeTests
{
    private static readonly ComparisonPolicy Empty =
        new([], [], new Dictionary<string, string>(), []);

    private static ParityRecord R(string id, params (string Field, string Value)[] fields) =>
        new(id, fields.ToDictionary(f => f.Field, f => f.Value));

    [Fact]
    public void A_record_missing_on_the_legacy_side_reports_the_direction_correctly()
    {
        var report = ParityComparator.Compare(
            [R("A", ("x", "1"))],
            [R("A", ("x", "1")), R("B", ("x", "1"))],
            Empty);

        var failure = Assert.Single(report.Failures);
        Assert.Equal("B", failure.RecordId);
        Assert.Equal("<missing>", failure.Legacy);
        Assert.Equal("<present>", failure.New);
        Assert.Equal("missing-record", failure.Classification);
    }

    [Fact]
    public void A_record_missing_on_the_new_side_reports_the_direction_correctly()
    {
        var report = ParityComparator.Compare(
            [R("A", ("x", "1")), R("B", ("x", "1"))],
            [R("A", ("x", "1"))],
            Empty);

        var failure = Assert.Single(report.Failures);
        Assert.Equal("<present>", failure.Legacy);
        Assert.Equal("<missing>", failure.New);
    }

    [Fact]
    public void A_field_present_on_only_one_side_is_compared_not_skipped()
    {
        var report = ParityComparator.Compare(
            [R("A", ("x", "1"), ("extra", "7"))],
            [R("A", ("x", "1"))],
            Empty);

        var failure = Assert.Single(report.Failures);
        Assert.Equal("extra", failure.Field);
        Assert.Equal("7", failure.Legacy);
        Assert.Equal("<missing>", failure.New);
    }

    [Fact]
    public void Failures_are_ordered_by_record_id_then_field()
    {
        var report = ParityComparator.Compare(
            [R("B", ("b", "1"), ("a", "1")), R("A", ("z", "1"))],
            [R("B", ("b", "2"), ("a", "2")), R("A", ("z", "9"))],
            Empty);

        Assert.Equal(["A", "B", "B"], report.Failures.Select(f => f.RecordId).ToArray());
        Assert.Equal(["z", "a", "b"], report.Failures.Select(f => f.Field).ToArray());
    }

    [Fact]
    public void A_relative_tolerance_passes_within_and_fails_beyond_the_fraction()
    {
        var policy = new ComparisonPolicy([], [new Tolerance("x", "relative", 0.10m)],
            new Dictionary<string, string>(), []);

        var within = ParityComparator.Compare([R("A", ("x", "100"))], [R("A", ("x", "110"))], policy);
        Assert.Equal(0, within.Failed);

        var beyond = ParityComparator.Compare([R("A", ("x", "100"))], [R("A", ("x", "111"))], policy);
        Assert.Equal(1, beyond.Failed);
    }

    [Fact]
    public void A_relative_tolerance_uses_the_magnitude_of_the_legacy_value()
    {
        var policy = new ComparisonPolicy([], [new Tolerance("x", "relative", 0.10m)],
            new Dictionary<string, string>(), []);

        var report = ParityComparator.Compare([R("A", ("x", "-100"))], [R("A", ("x", "-109"))], policy);
        Assert.Equal(0, report.Failed);
    }

    [Fact]
    public void An_absolute_tolerance_passes_exactly_at_the_limit_and_fails_just_beyond()
    {
        var policy = new ComparisonPolicy([], [new Tolerance("x", "absolute", 0.02m)],
            new Dictionary<string, string>(), []);

        var atLimit = ParityComparator.Compare([R("A", ("x", "1.00"))], [R("A", ("x", "1.02"))], policy);
        Assert.Equal(0, atLimit.Failed);

        var beyond = ParityComparator.Compare([R("A", ("x", "1.00"))], [R("A", ("x", "1.03"))], policy);
        Assert.Equal(1, beyond.Failed);
    }

    [Fact]
    public void A_non_numeric_mismatch_classifies_as_value_mismatch()
    {
        var report = ParityComparator.Compare(
            [R("A", ("status", "collected"))],
            [R("A", ("status", "overdue"))],
            Empty);

        Assert.Equal("value-mismatch", Assert.Single(report.Failures).Classification);
    }

    [Fact]
    public void A_numeric_gap_just_above_the_rounding_threshold_is_not_rounding()
    {
        var report = ParityComparator.Compare(
            [R("A", ("x", "1.00"))],
            [R("A", ("x", "1.02"))],
            Empty);

        Assert.Equal("value-mismatch", Assert.Single(report.Failures).Classification);
    }

    [Fact]
    public void A_KD_hit_still_counts_the_record_as_passed()
    {
        var policy = new ComparisonPolicy([], [], new Dictionary<string, string>(),
            [new KnownDifference("KD-0001", "d", "who", "2026-01-01", "RULE-0001", "x")]);

        var report = ParityComparator.Compare([R("A", ("x", "1"))], [R("A", ("x", "2"))], policy);
        Assert.Equal(1, report.Passed);
        Assert.Equal(0, report.Failed);
        Assert.Single(report.KnownDifferenceHits);
    }
}
