using SpecAnchor.Gates;
using Xunit;

namespace SpecAnchor.Gates.Tests;

/// <summary>The narrow scaffold: red acceptance test + skeleton, nothing more.</summary>
public sealed class ScaffolderTests
{
    private const string Card = """
        rule_id: RULE-0042
        version: 1
        schemaVersion: 1
        context: Factoring.Pricing
        statement: Commission is never below the contract minimum.
        source_ref:
          - file: FactoringApp/Pricing/CommissionCalculator.cs
            line_start: 5
            line_end: 15
        confidence: inferred
        open_questions:
          - Why is the minimum bypassed for one contract type?
        """;

    [Fact]
    public void Scaffold_produces_a_red_acceptance_test_and_a_skeleton()
    {
        var files = Scaffolder.Scaffold(Card);

        Assert.Equal(2, files.Count);
        var acceptance = Assert.Single(files, f => f.FileName == "RULE-0042.Acceptance.cs");
        Assert.Contains("Assert.Fail", acceptance.Content);
        Assert.Contains("RULE-0042", acceptance.Content);
        Assert.Contains("Open questions on the card: 1", acceptance.Content);
        Assert.Contains("namespace Discovery.Factoring.Pricing.Acceptance;", acceptance.Content);

        var skeleton = Assert.Single(files, f => f.FileName == "RULE-0042.Skeleton.cs");
        Assert.Contains("NotImplementedException", skeleton.Content);
    }

    [Fact]
    public void A_card_without_a_rule_id_is_refused()
    {
        Assert.Throws<InvalidOperationException>(() => Scaffolder.Scaffold("version: 1"));
    }

    [Fact]
    public void Scaffold_output_is_deterministic()
    {
        Assert.Equal(Scaffolder.Scaffold(Card), Scaffolder.Scaffold(Card));
    }
}
