using SpecAnchor.Index.CSharp;
using Xunit;

namespace SpecAnchor.Index.CSharp.Tests;

/// <summary>
/// Acceptance tests against the fake legacy rig. The rig's planted traps
/// (rig/legacy-factoring/TRAPS.md) are the answer key: an index run that misses
/// a trap is a failing run.
/// </summary>
public sealed class RigIndexFixture
{
    public RigIndexFixture()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "rig")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        RepoRoot = dir ?? throw new InvalidOperationException("Repository root with rig/ not found.");
        Index = CSharpIndexer.IndexDirectory(Path.Combine(RepoRoot, "rig", "legacy-factoring", "src"));
    }

    public string RepoRoot { get; }

    public CSharpIndex Index { get; }
}

public sealed class RigIndexTests : IClassFixture<RigIndexFixture>
{
    private readonly RigIndexFixture _fixture;

    public RigIndexTests(RigIndexFixture fixture) => _fixture = fixture;

    [Fact]
    public void Rig_compiles_clean_so_mode_is_semantic_and_no_blind_spots()
    {
        Assert.Empty(_fixture.Index.BlindSpots);
        Assert.Equal("semantic", _fixture.Index.Mode);
    }

    [Fact]
    public void Trap_C_LegacyRebateCalculator_is_a_dead_code_candidate()
    {
        Assert.Contains(_fixture.Index.DeadCodeCandidates,
            c => c.Contains("LegacyRebateCalculator", StringComparison.Ordinal));
    }

    [Fact]
    public void CommissionCalculator_is_referenced_and_therefore_not_dead()
    {
        Assert.DoesNotContain(_fixture.Index.DeadCodeCandidates,
            c => c.Contains("CommissionCalculator", StringComparison.Ordinal));
    }

    [Fact]
    public void Trap_D_alias_edge_TransferHelper_to_AssignmentService_is_in_the_call_graph()
    {
        Assert.Contains(_fixture.Index.CallGraph, e =>
            e.Caller.Contains("TransferHelper.RegisterTransfer", StringComparison.Ordinal) &&
            e.Callee.Contains("AssignmentService.RegisterAssignment", StringComparison.Ordinal));
    }

    [Fact]
    public void Type_spans_resolve_to_real_files_and_lines()
    {
        var entry = Assert.Single(_fixture.Index.Types,
            t => t.FullName == "FactoringApp.Pricing.CommissionCalculator");
        Assert.EndsWith("CommissionCalculator.cs", entry.File, StringComparison.Ordinal);
        Assert.True(entry.LineStart >= 1);
        Assert.True(entry.LineEnd > entry.LineStart);

        var absolute = Path.Combine(_fixture.RepoRoot, "rig", "legacy-factoring", "src", entry.File);
        Assert.True(File.Exists(absolute), $"source_ref must resolve: {absolute}");
    }

    [Fact]
    public void Calculate_carries_its_decision_points_as_complexity()
    {
        var type = Assert.Single(_fixture.Index.Types,
            t => t.FullName == "FactoringApp.Pricing.CommissionCalculator");
        var member = Assert.Single(type.Members, m => m.Name.Contains(".Calculate(", StringComparison.Ordinal));
        Assert.True(member.CyclomaticComplexity >= 3,
            $"if + && should yield >= 3, got {member.CyclomaticComplexity}");
    }

    [Fact]
    public void Namespace_map_covers_the_three_rig_areas()
    {
        var names = _fixture.Index.Namespaces.Select(n => n.Name).ToList();
        Assert.Contains("FactoringApp.Pricing", names);
        Assert.Contains("FactoringApp.Assignment", names);
        Assert.Contains("FactoringApp.Accounting", names);
    }

    [Fact]
    public void Index_is_deterministic_two_runs_serialize_byte_identical()
    {
        var again = CSharpIndexer.IndexDirectory(
            Path.Combine(_fixture.RepoRoot, "rig", "legacy-factoring", "src"));
        Assert.Equal(IndexSerializer.ToJson(_fixture.Index), IndexSerializer.ToJson(again));
    }
}
