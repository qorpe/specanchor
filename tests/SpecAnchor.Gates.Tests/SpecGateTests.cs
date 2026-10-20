using SpecAnchor.Gates;
using Xunit;

namespace SpecAnchor.Gates.Tests;

/// <summary>
/// The spec gate closes rule → spec → test. Trap E (rig/legacy-factoring/TRAPS.md) is the
/// acceptance case: a spec that quietly answers RULE-0042's undecided open questions, and
/// whose clauses no test cites, must be REJECTED — while SPEC-0001, which keeps both
/// questions as open brackets, passes every gate and is reported NOT READY for a sprint.
/// </summary>
public sealed class SpecGateTests : IClassFixture<GateFixture>
{
    private static readonly string Spec0001 = Path.Combine("specs", "SPEC-0001.yaml");
    private static readonly string Spec0002 = Path.Combine("specs", "SPEC-0002.yaml");
    private static readonly string Char0042 = Path.Combine("tests", "CHAR-0042.yaml");
    private static readonly string Rule0042 = Path.Combine("rules", "RULE-0042.yaml");

    private readonly GateFixture _fixture;

    public SpecGateTests(GateFixture fixture) => _fixture = fixture;

    private GateReport Run(string discovery) =>
        GateRunner.Run(new GateInput(discovery, _fixture.SchemasDir, _fixture.CSharp, _fixture.Sql));

    private string DiscoveryWithTrapE()
    {
        var discovery = _fixture.CopiedDiscovery();
        File.Copy(Path.Combine(_fixture.TrapSpecimensDir, "SPEC-0002.yaml"), Path.Combine(discovery, Spec0002));
        return discovery;
    }

    private static string Describe(GateReport report) =>
        string.Join("\n", report.Findings.Select(f => $"{f.Gate} {f.File} {f.Finding.Code}: {f.Finding.Message}"));

    [Fact]
    public void Trap_E_a_spec_that_resolves_an_undecided_open_question_is_rejected()
    {
        var report = Run(DiscoveryWithTrapE());

        Assert.Contains(report.Findings, f =>
            f.Gate == "spec" && f.File == Spec0002 && f.Finding.Code == "SA0503");
        Assert.Equal(1, report.ExitCode);
    }

    [Fact]
    public void Trap_E_every_clause_no_test_cites_is_rejected()
    {
        var report = Run(DiscoveryWithTrapE());

        var uncited = report.Findings
            .Where(f => f.Gate == "spec" && f.File == Spec0002 && f.Finding.Code == "SA0506")
            .Select(f => f.Finding.Path)
            .ToList();
        Assert.Equal(["$.body[C1]", "$.body[C2]", "$.body[C3]"], uncited);
    }

    [Fact]
    public void Trap_E_a_rejected_spec_is_not_reported_as_merely_not_ready()
    {
        var report = Run(DiscoveryWithTrapE());

        Assert.DoesNotContain(report.Readiness, r => r.SpecId == "SPEC-0002");
        Assert.DoesNotContain(report.Findings, f => f.File == Spec0001);
    }

    [Fact]
    public void Trap_E_the_gate_answers_the_same_way_every_run()
    {
        var discovery = DiscoveryWithTrapE();
        Assert.Equal(Describe(Run(discovery)), Describe(Run(discovery)));
    }

    [Fact]
    public void SPEC_0001_passes_every_gate_and_is_reported_not_ready_with_every_reason()
    {
        var report = Run(_fixture.DiscoveryDir);

        Assert.True(report.IsClean, Describe(report));
        var readiness = Assert.Single(report.Readiness);
        Assert.Equal("SPEC-0001", readiness.SpecId);
        Assert.Equal(Spec0001, readiness.File);
        Assert.False(readiness.Ready);
        Assert.Equal(3, readiness.Reasons.Count);
        Assert.Contains(readiness.Reasons, r => r.Contains("2 open bracket", StringComparison.Ordinal));
        Assert.Contains(readiness.Reasons, r => r.Contains("approved_by", StringComparison.Ordinal));
        Assert.Contains(readiness.Reasons, r => r.Contains("RULE-0042", StringComparison.Ordinal));
    }

    [Fact]
    public void Putting_the_fault_back_into_SPEC_0001_resolving_contract_type_3_quietly_is_rejected()
    {
        var discovery = _fixture.MutatedDiscovery(Spec0001, t => t
            .Replace("""
                the system shall [OPEN — pending expert decision: the
                  legacy system skips the minimum and nobody asked so far could explain why].
                """, "the system shall not apply the minimum commission.")
            .Replace("open_brackets: 2", "open_brackets: 1"));

        var report = Run(discovery);

        Assert.Contains(report.Findings, f => f.File == Spec0001 && f.Finding.Code == "SA0503");
        Assert.DoesNotContain(report.Findings, f => f.Finding.Code == "SA0502");
    }

    [Fact]
    public void A_spec_that_miscounts_its_own_open_brackets_is_rejected()
    {
        var discovery = _fixture.MutatedDiscovery(Spec0001, t => t.Replace("open_brackets: 2", "open_brackets: 0"));

        var report = Run(discovery);

        Assert.Contains(report.Findings, f => f.File == Spec0001 && f.Finding.Code == "SA0502");
    }

    [Fact]
    public void A_spec_whose_rule_id_resolves_to_nothing_is_rejected()
    {
        var discovery = _fixture.MutatedDiscovery(Spec0001, t => t.Replace("rule_ids: [RULE-0042]", "rule_ids: [RULE-0999]"));

        var report = Run(discovery);

        Assert.Contains(report.Findings, f => f.File == Spec0001 && f.Finding.Code == "SA0501");
    }

    [Fact]
    public void A_spec_that_declares_no_clause_is_rejected()
    {
        var discovery = _fixture.MutatedDiscovery(Spec0001, t => t
            .Replace("[C1] ", string.Empty).Replace("[C2] ", string.Empty).Replace("[C3] ", string.Empty));

        var report = Run(discovery);

        Assert.Contains(report.Findings, f => f.File == Spec0001 && f.Finding.Code == "SA0504");
    }

    [Fact]
    public void A_spec_that_declares_the_same_clause_twice_is_rejected()
    {
        var discovery = _fixture.MutatedDiscovery(Spec0001, t => t.Replace("[C3] ", "[C2] "));

        var report = Run(discovery);

        Assert.Contains(report.Findings, f => f.File == Spec0001 && f.Finding.Code == "SA0505");
    }

    [Fact]
    public void Dropping_one_clause_citation_from_the_char_test_rejects_that_clause()
    {
        var discovery = _fixture.MutatedDiscovery(Char0042, t => t.Replace(", SPEC-0001.C3", string.Empty));

        var report = Run(discovery);

        var finding = Assert.Single(report.Findings);
        Assert.Equal(("spec", Spec0001, "SA0506", "$.body[C3]"),
            (finding.Gate, finding.File, finding.Finding.Code, finding.Finding.Path));
    }

    [Fact]
    public void A_char_test_that_cites_a_clause_no_spec_declares_is_rejected()
    {
        var discovery = _fixture.MutatedDiscovery(Char0042, t => t.Replace("SPEC-0001.C3]", "SPEC-0001.C3, SPEC-0001.C9]"));

        var report = Run(discovery);

        Assert.Contains(report.Findings, f =>
            f.Gate == "char-test" && f.File == Char0042 && f.Finding.Code == "SA0303");
    }

    [Fact]
    public void A_spec_that_breaks_its_schema_is_rejected_without_crashing_the_run()
    {
        var discovery = _fixture.MutatedDiscovery(Spec0001, t => t.Replace("body: |", "body: 5\nnotes: |"));

        var report = Run(discovery);

        Assert.Contains(report.Findings, f => f.Gate == "spec" && f.File == Spec0001 && f.Finding.Code == "SA0002");
        Assert.Empty(report.Readiness);
    }

    [Fact]
    public void A_spec_becomes_ready_only_when_brackets_approval_and_disposition_are_all_in_place()
    {
        var discovery = _fixture.CopiedDiscovery();
        GateFixture.Mutate(discovery, Rule0042, t => t
            .Replace("disposition: null", "disposition: keep\ndecided_by: Pricing domain expert"));
        GateFixture.Mutate(discovery, Spec0001, t => t
            .Replace("""
                rounded to two
                  decimals [OPEN — rounding mode: the application rounds half to even, the procedure
                  half away from zero; pending expert decision].
                """, "rounded to two decimals half away from zero.")
            .Replace("""
                the system shall [OPEN — pending expert decision: the
                  legacy system skips the minimum and nobody asked so far could explain why].
                """, "the system shall not apply the minimum commission.")
            .Replace("open_brackets: 2", "open_brackets: 0\napproved_by: Pricing domain expert"));

        var report = Run(discovery);

        Assert.True(report.IsClean, Describe(report));
        var readiness = Assert.Single(report.Readiness);
        Assert.True(readiness.Ready, string.Join("; ", readiness.Reasons));
        Assert.Empty(readiness.Reasons);
    }
}
