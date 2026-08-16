using SpecAnchor.Cli;
using Xunit;

namespace SpecAnchor.Cli.Tests;

/// <summary>
/// The MCP tools are pure functions over the indexes — tested directly against the
/// rig, the same way an agent would call them over stdio.
/// </summary>
public sealed class IndexToolsFixture
{
    public IndexToolsFixture()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "rig")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        var repoRoot = dir ?? throw new InvalidOperationException("Repository root with rig/ not found.");
        Src = Path.Combine(repoRoot, "rig", "legacy-factoring", "src");
        Sql = Path.Combine(repoRoot, "rig", "legacy-factoring", "sql");
        Discovery = Path.Combine(repoRoot, "rig", "legacy-factoring", "discovery");
        Schemas = Path.Combine(repoRoot, "core", "schemas");
    }

    public string Src { get; }

    public string Sql { get; }

    public string Discovery { get; }

    public string Schemas { get; }
}

public sealed class IndexToolsTests : IClassFixture<IndexToolsFixture>
{
    private readonly IndexToolsFixture _fixture;

    public IndexToolsTests(IndexToolsFixture fixture) => _fixture = fixture;

    [Fact]
    public void WhoCalls_answers_the_alias_question_exactly()
    {
        var json = IndexTools.WhoCalls(_fixture.Src, "RegisterAssignment");
        Assert.Contains("TransferHelper.RegisterTransfer", json);
    }

    [Fact]
    public void TableAccess_reaches_the_cross_language_link()
    {
        var json = IndexTools.TableAccess(_fixture.Src, _fixture.Sql, "TemlikKayit");
        Assert.Contains("AssignmentService.RegisterAssignment", json);
        Assert.Contains("\"write\"", json);
    }

    [Fact]
    public void DeadCode_lists_the_rebate_calculator()
    {
        Assert.Contains("LegacyRebateCalculator", IndexTools.DeadCode(_fixture.Src));
    }

    [Fact]
    public void SqlObject_exposes_the_procedure_branches_and_writes()
    {
        var json = IndexTools.SqlObject(_fixture.Sql, "usp_CalculateCommission");
        Assert.Contains("CommissionResult", json);
        Assert.Contains("\"BranchCount\": 1", json);
    }

    [Fact]
    public void Gate_tool_reports_the_rig_discovery_sample_clean()
    {
        var json = IndexTools.Gate(_fixture.Discovery, _fixture.Src, _fixture.Sql, _fixture.Schemas);
        Assert.Contains("\"Findings\": []", json);
    }

    [Fact]
    public void IndexSummary_carries_the_honest_coverage_line()
    {
        var json = IndexTools.IndexSummary(_fixture.Src, _fixture.Sql);
        Assert.Contains("\"CallSitesTotal\": 1", json);
        Assert.Contains("\"CallSitesResolved\": 1", json);
    }
}
