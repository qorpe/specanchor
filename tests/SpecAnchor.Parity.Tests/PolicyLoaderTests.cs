using SpecAnchor.Parity;
using Xunit;

namespace SpecAnchor.Parity.Tests;

/// <summary>Policy loading edges: absent sections, rounding map, JSON input.</summary>
public sealed class PolicyLoaderTests
{
    [Fact]
    public void A_minimal_document_yields_empty_sections_not_nulls()
    {
        var policy = PolicyLoader.Load("schemaVersion: 1");
        Assert.Empty(policy.ExcludedFields);
        Assert.Empty(policy.Tolerances);
        Assert.Empty(policy.Rounding);
        Assert.Empty(policy.KnownDifferences);
    }

    [Fact]
    public void The_rounding_map_is_loaded_with_its_values()
    {
        var policy = PolicyLoader.Load("""
            rounding: { commission: half-up, interest: bankers }
            """);
        Assert.Equal("half-up", policy.Rounding["commission"]);
        Assert.Equal("bankers", policy.Rounding["interest"]);
    }

    [Fact]
    public void Tolerances_and_known_differences_load_all_fields()
    {
        var policy = PolicyLoader.Load("""
            tolerances:
              - { field: commission, type: absolute, value: 0.02 }
            known_differences:
              - id: KD-0007
                description: rounding delta
                accepted_by: F. Yilmaz
                date: 2026-08-12
                rule_id: RULE-0042
                field: commission
            """);

        var tolerance = Assert.Single(policy.Tolerances);
        Assert.Equal(("commission", "absolute", 0.02m), (tolerance.Field, tolerance.Type, tolerance.Value));

        var kd = Assert.Single(policy.KnownDifferences);
        Assert.Equal("KD-0007", kd.Id);
        Assert.Equal("F. Yilmaz", kd.AcceptedBy);
        Assert.Equal("2026-08-12", kd.Date);
        Assert.Equal("RULE-0042", kd.RuleId);
        Assert.Equal("commission", kd.Field);
        Assert.Equal("rounding delta", kd.Description);
    }

    [Fact]
    public void Json_documents_load_the_same_as_yaml()
    {
        var policy = PolicyLoader.Load("""
            { "excluded_fields": ["calculatedAt"], "rounding": { "commission": "half-up" } }
            """);
        Assert.Equal("calculatedAt", Assert.Single(policy.ExcludedFields));
        Assert.Equal("half-up", policy.Rounding["commission"]);
    }
}
