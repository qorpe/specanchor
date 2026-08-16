using SpecAnchor.Index.CSharp;
using SpecAnchor.Index.Matrix;
using SpecAnchor.Index.Sql;
using Xunit;

namespace SpecAnchor.Index.Matrix.Tests;

/// <summary>
/// Acceptance tests for the combined table read/write matrix against the rig.
/// The cross-language link is the point: the same TemlikKayit table must be
/// reachable from a C# literal and from the schema inventory.
/// </summary>
public sealed class RigMatrixFixture
{
    public RigMatrixFixture()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "rig")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        var repoRoot = dir ?? throw new InvalidOperationException("Repository root with rig/ not found.");
        CSharp = CSharpIndexer.IndexDirectory(Path.Combine(repoRoot, "rig", "legacy-factoring", "src"));
        Sql = SqlIndexer.IndexDirectory(Path.Combine(repoRoot, "rig", "legacy-factoring", "sql"));
        Matrix = TableAccessMatrixBuilder.Build(CSharp, Sql);
    }

    public CSharpIndex CSharp { get; }

    public SqlIndex Sql { get; }

    public TableAccessMatrix Matrix { get; }
}

public sealed class RigMatrixTests : IClassFixture<RigMatrixFixture>
{
    private readonly RigMatrixFixture _fixture;

    public RigMatrixTests(RigMatrixFixture fixture) => _fixture = fixture;

    [Fact]
    public void Trap_D_cross_language_link_csharp_literal_writes_TemlikKayit()
    {
        var entry = Assert.Single(_fixture.Matrix.Entries,
            e => e.AccessorKind == "csharp" && e.Table == "TemlikKayit" && e.Access == "write");
        Assert.Contains("AssignmentService.RegisterAssignment", entry.Accessor, StringComparison.Ordinal);
        Assert.EndsWith("AssignmentService.cs", entry.File, StringComparison.Ordinal);
    }

    [Fact]
    public void Procedure_writes_CommissionResult_and_reads_Invoice_and_Contract()
    {
        Assert.Contains(_fixture.Matrix.Entries, e =>
            e.Accessor == "dbo.usp_CalculateCommission" && e.Table == "CommissionResult" && e.Access == "write");
        Assert.Contains(_fixture.Matrix.Entries, e =>
            e.Accessor == "dbo.usp_CalculateCommission" && e.Table == "Invoice" && e.Access == "read");
        Assert.Contains(_fixture.Matrix.Entries, e =>
            e.Accessor == "dbo.usp_CalculateCommission" && e.Table == "Contract" && e.Access == "read");
    }

    [Fact]
    public void Trigger_reads_are_present_and_pseudo_tables_are_filtered()
    {
        Assert.Contains(_fixture.Matrix.Entries, e =>
            e.AccessorKind == "trigger" && e.Table == "Customer" && e.Access == "read");
        Assert.DoesNotContain(_fixture.Matrix.Entries, e =>
            string.Equals(e.Table, "inserted", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Coverage_is_honest_one_sql_literal_seen_one_resolved()
    {
        var coverage = _fixture.Matrix.Coverage;
        Assert.Equal(1, coverage.CallSitesTotal);
        Assert.Equal(1, coverage.CallSitesResolved);
        Assert.Empty(coverage.Unresolved);
        var tech = Assert.Single(coverage.ByTechnology);
        Assert.Equal("string-sql", tech.Technology);
    }

    [Fact]
    public void Matrix_is_deterministic_two_builds_serialize_byte_identical()
    {
        var again = TableAccessMatrixBuilder.Build(_fixture.CSharp, _fixture.Sql);
        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(_fixture.Matrix),
            System.Text.Json.JsonSerializer.Serialize(again));
    }
}
