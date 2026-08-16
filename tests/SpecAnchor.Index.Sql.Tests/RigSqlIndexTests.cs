using SpecAnchor.Index.Sql;
using Xunit;

namespace SpecAnchor.Index.Sql.Tests;

/// <summary>
/// Acceptance tests against the rig's SQL side. The bonus traps in
/// rig/legacy-factoring/TRAPS.md apply: rules hiding in the trigger and the job
/// must be reachable from this index, or the run fails.
/// </summary>
public sealed class RigSqlIndexFixture
{
    public RigSqlIndexFixture()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "rig")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        RepoRoot = dir ?? throw new InvalidOperationException("Repository root with rig/ not found.");
        SqlRoot = Path.Combine(RepoRoot, "rig", "legacy-factoring", "sql");
        Index = SqlIndexer.IndexDirectory(SqlRoot);
    }

    public string RepoRoot { get; }

    public string SqlRoot { get; }

    public SqlIndex Index { get; }
}

public sealed class RigSqlIndexTests : IClassFixture<RigSqlIndexFixture>
{
    private readonly RigSqlIndexFixture _fixture;

    public RigSqlIndexTests(RigSqlIndexFixture fixture) => _fixture = fixture;

    [Fact]
    public void All_rig_scripts_parse_clean()
    {
        Assert.Empty(_fixture.Index.BlindSpots);
        Assert.Equal("ast", _fixture.Index.Mode);
    }

    [Fact]
    public void Schema_inventory_contains_the_five_tables_with_temlik_columns()
    {
        Assert.Equal(5, _fixture.Index.Tables.Count);
        var temlik = Assert.Single(_fixture.Index.Tables, t => t.Name == "TemlikKayit");
        Assert.Contains(temlik.Columns, c => c.Name == "IhbarTarihi");
        Assert.Contains("Invoice", temlik.ForeignKeysTo);
    }

    [Fact]
    public void Commission_procedure_branches_reads_and_writes_are_captured()
    {
        var proc = Assert.Single(_fixture.Index.Procedures);
        Assert.Equal("dbo.usp_CalculateCommission", proc.Name);
        Assert.True(proc.BranchCount >= 1, $"the minimum-commission IF must count, got {proc.BranchCount}");
        Assert.Contains("Invoice", proc.Reads);
        Assert.Contains("Contract", proc.Reads);
        Assert.Contains("CommissionResult", proc.Writes);
        Assert.DoesNotContain("CommissionResult", proc.Reads);
        Assert.Contains(proc.Parameters, p => p.Name == "@InvoiceId");
    }

    [Fact]
    public void Bonus_trap_the_risk_limit_rule_in_the_trigger_is_reachable()
    {
        var trigger = Assert.Single(_fixture.Index.Triggers);
        Assert.Equal("dbo.trg_Invoice_RiskLimit", trigger.Name);
        Assert.Equal("Invoice", trigger.Table);
        Assert.Equal("AFTER", trigger.Timing);
        Assert.Contains("INSERT", trigger.Events);
        Assert.Contains("Customer", trigger.Reads);
        Assert.Contains("Contract", trigger.Reads);
        Assert.True(trigger.BranchCount >= 1, "the IF EXISTS guard is the rule");
    }

    [Fact]
    public void Bonus_trap_the_job_script_and_what_it_executes_are_inventoried()
    {
        var job = Assert.Single(_fixture.Index.Scripts);
        Assert.EndsWith("004_job_NightlyCommissionRecalc.sql", job.File, StringComparison.Ordinal);
        Assert.Contains("dbo.usp_CalculateCommission", job.ExecutesProcedures);
        Assert.Contains("Invoice", job.Reads);
        Assert.True(job.BranchCount >= 1, "the WHILE loop must count");
    }

    [Fact]
    public void Source_refs_resolve_procedure_spans_point_into_the_real_file()
    {
        var proc = Assert.Single(_fixture.Index.Procedures);
        var absolute = Path.Combine(_fixture.SqlRoot, proc.File);
        Assert.True(File.Exists(absolute), $"source_ref must resolve: {absolute}");
        Assert.True(proc.LineStart >= 1);
        Assert.True(proc.LineEnd > proc.LineStart);
    }

    [Fact]
    public void Index_is_deterministic_two_runs_serialize_byte_identical()
    {
        var again = SqlIndexer.IndexDirectory(_fixture.SqlRoot);
        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(_fixture.Index),
            System.Text.Json.JsonSerializer.Serialize(again));
    }
}
