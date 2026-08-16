using SpecAnchor.Gates;
using SpecAnchor.Index.CSharp;
using SpecAnchor.Index.Sql;
using Xunit;

namespace SpecAnchor.Gates.Tests;

/// <summary>
/// Gate runner acceptance: the rig's committed discovery sample must be green
/// (that same run is CI's `specanchor gate` step), and every gate must go red
/// for the mutation that targets it.
/// </summary>
public sealed class GateFixture
{
    public GateFixture()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "rig")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        RepoRoot = dir ?? throw new InvalidOperationException("Repository root with rig/ not found.");
        SchemasDir = Path.Combine(RepoRoot, "core", "schemas");
        DiscoveryDir = Path.Combine(RepoRoot, "rig", "legacy-factoring", "discovery");
        CSharp = CSharpIndexer.IndexDirectory(Path.Combine(RepoRoot, "rig", "legacy-factoring", "src"));
        Sql = SqlIndexer.IndexDirectory(Path.Combine(RepoRoot, "rig", "legacy-factoring", "sql"));
    }

    public string RepoRoot { get; }

    public string SchemasDir { get; }

    public string DiscoveryDir { get; }

    public CSharpIndex CSharp { get; }

    public SqlIndex Sql { get; }

    /// <summary>Copies the rig discovery sample into a temp dir and applies one mutation.</summary>
    public string MutatedDiscovery(string relativeFile, Func<string, string> mutate)
    {
        var temp = Path.Combine(Path.GetTempPath(), "specanchor-gates-" + Guid.NewGuid().ToString("N"));
        foreach (var file in Directory.EnumerateFiles(DiscoveryDir, "*.*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(temp, Path.GetRelativePath(DiscoveryDir, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }

        var path = Path.Combine(temp, relativeFile);
        File.WriteAllText(path, mutate(File.ReadAllText(path)));
        return temp;
    }
}

public sealed class GateRunnerTests : IClassFixture<GateFixture>
{
    private readonly GateFixture _fixture;

    public GateRunnerTests(GateFixture fixture) => _fixture = fixture;

    private GateReport Run(string discovery, IReadOnlyList<string>? changed = null) =>
        GateRunner.Run(new GateInput(discovery, _fixture.SchemasDir, _fixture.CSharp, _fixture.Sql, changed));

    [Fact]
    public void The_rig_discovery_sample_is_green_end_to_end()
    {
        var report = Run(_fixture.DiscoveryDir);
        Assert.True(report.IsClean, string.Join("\n", report.Findings.Select(f => $"{f.Gate} {f.File}: {f.Finding.Message}")));
        Assert.Equal(0, report.ExitCode);
    }

    [Fact]
    public void Source_ref_gate_goes_red_when_a_rule_points_at_a_ghost_file()
    {
        var discovery = _fixture.MutatedDiscovery(
            Path.Combine("rules", "RULE-0042.yaml"),
            t => t.Replace("FactoringApp/Pricing/CommissionCalculator.cs", "FactoringApp/Ghost.cs"));
        var report = Run(discovery);
        Assert.Contains(report.Findings, f => f.Gate == "source-ref" && f.Finding.Code == "SA0101");
        Assert.Equal(1, report.ExitCode);
    }

    [Fact]
    public void Statement_quality_gate_goes_red_when_a_code_identifier_leaks_into_the_sentence()
    {
        var discovery = _fixture.MutatedDiscovery(
            Path.Combine("rules", "RULE-0043.yaml"),
            t => t.Replace("when the notification reaches the debtor.",
                "when RegisterAssignment is called."));
        var report = Run(discovery);
        Assert.Contains(report.Findings, f => f.Gate == "statement-quality" && f.Finding.Code == "SA0103");
    }

    [Fact]
    public void Evidence_gate_goes_red_when_an_evidenced_rule_cites_a_nonexistent_test()
    {
        var discovery = _fixture.MutatedDiscovery(
            Path.Combine("rules", "RULE-0042.yaml"),
            t => t.Replace("evidence: CHAR-0042", "evidence: CHAR-9999"));
        var report = Run(discovery);
        Assert.Contains(report.Findings, f => f.Gate == "evidence" && f.Finding.Code == "SA0105");
    }

    [Fact]
    public void Ledger_gate_goes_red_when_an_alias_resolves_to_nothing()
    {
        var discovery = _fixture.MutatedDiscovery(
            Path.Combine("terms", "temlik.yaml"),
            t => t.Replace("TemlikKayit,", "TemlikKayit, GhostService,"));
        var report = Run(discovery);
        Assert.Contains(report.Findings, f => f.Gate == "ledger" && f.Finding.Code == "SA0201");
    }

    [Fact]
    public void Char_test_gate_goes_red_when_the_arithmetic_does_not_add_up()
    {
        var discovery = _fixture.MutatedDiscovery(
            Path.Combine("tests", "CHAR-0042.yaml"),
            t => t.Replace("{ passed: 238, failed: 2 }", "{ passed: 238, failed: 1 }"));
        var report = Run(discovery);
        Assert.Contains(report.Findings, f => f.Gate == "char-test" && f.Finding.Code == "SA0302");
    }

    [Fact]
    public void Touch_gate_goes_red_when_referenced_code_changes_but_the_rule_does_not()
    {
        var report = Run(_fixture.DiscoveryDir,
            ["rig/legacy-factoring/src/FactoringApp/Pricing/CommissionCalculator.cs"]);
        Assert.Contains(report.Findings, f => f.Gate == "touch" && f.Finding.Code == "SA0401");
    }

    [Fact]
    public void Touch_gate_stays_green_when_the_rule_changed_alongside_the_code()
    {
        var report = Run(_fixture.DiscoveryDir,
        [
            "rig/legacy-factoring/src/FactoringApp/Pricing/CommissionCalculator.cs",
            "rules/RULE-0042.yaml",
        ]);
        Assert.DoesNotContain(report.Findings, f => f.Gate == "touch");
    }
}
