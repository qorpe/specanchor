using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using SpecAnchor.Gates;
using SpecAnchor.Index.CSharp;
using SpecAnchor.Index.Matrix;
using SpecAnchor.Index.Sql;

namespace SpecAnchor.Cli;

/// <summary>
/// The MCP surface: the agent queries the deterministic index instead of reading the
/// raw repository, and never re-derives what the engine can answer exactly. Every tool
/// is a pure function over the indexes — no state, no model, same input same output.
/// </summary>
[McpServerToolType]
public static class IndexTools
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    [McpServerTool]
    [Description("Summary of the deterministic index: namespaces, type/procedure counts, blind spots and data-access coverage.")]
    public static string IndexSummary(
        [Description("Directory containing the C# sources")] string srcDir,
        [Description("Directory containing the T-SQL scripts")] string sqlDir)
    {
        var cs = CSharpIndexer.IndexDirectory(srcDir);
        var sql = SqlIndexer.IndexDirectory(sqlDir);
        var matrix = TableAccessMatrixBuilder.Build(cs, sql);
        return JsonSerializer.Serialize(new
        {
            cs.Mode,
            cs.Namespaces,
            TypeCount = cs.Types.Count,
            ProcedureCount = sql.Procedures.Count,
            TriggerCount = sql.Triggers.Count,
            JobScriptCount = sql.Scripts.Count,
            CSharpBlindSpots = cs.BlindSpots,
            SqlBlindSpots = sql.BlindSpots,
            matrix.Coverage,
        }, Json);
    }

    [McpServerTool]
    [Description("Callers of a member: every call-graph edge whose callee contains the given name.")]
    public static string WhoCalls(
        [Description("Directory containing the C# sources")] string srcDir,
        [Description("Member name or fragment, e.g. RegisterAssignment")] string memberName)
    {
        var cs = CSharpIndexer.IndexDirectory(srcDir);
        var edges = cs.CallGraph
            .Where(e => e.Callee.Contains(memberName, StringComparison.Ordinal))
            .ToList();
        return JsonSerializer.Serialize(edges, Json);
    }

    [McpServerTool]
    [Description("Who touches a table: every accessor (C# member, procedure, trigger, job script) reading or writing it, with file:line evidence.")]
    public static string TableAccess(
        [Description("Directory containing the C# sources")] string srcDir,
        [Description("Directory containing the T-SQL scripts")] string sqlDir,
        [Description("Table name, e.g. TemlikKayit")] string table)
    {
        var matrix = TableAccessMatrixBuilder.Build(
            CSharpIndexer.IndexDirectory(srcDir),
            SqlIndexer.IndexDirectory(sqlDir));
        return JsonSerializer.Serialize(
            matrix.Entries.Where(e => e.Table.Contains(table, StringComparison.Ordinal)).ToList(), Json);
    }

    [McpServerTool]
    [Description("Dead-code candidates: publicly visible symbols with no inbound reference. Candidates, not verdicts.")]
    public static string DeadCode(
        [Description("Directory containing the C# sources")] string srcDir)
    {
        return JsonSerializer.Serialize(CSharpIndexer.IndexDirectory(srcDir).DeadCodeCandidates, Json);
    }

    [McpServerTool]
    [Description("Details of a SQL object (procedure or trigger): parameters, branch count, table reads/writes, EXEC edges.")]
    public static string SqlObject(
        [Description("Directory containing the T-SQL scripts")] string sqlDir,
        [Description("Object name or fragment, e.g. usp_CalculateCommission")] string name)
    {
        var sql = SqlIndexer.IndexDirectory(sqlDir);
        return JsonSerializer.Serialize(new
        {
            Procedures = sql.Procedures.Where(p => p.Name.Contains(name, StringComparison.Ordinal)).ToList(),
            Triggers = sql.Triggers.Where(t => t.Name.Contains(name, StringComparison.Ordinal)).ToList(),
        }, Json);
    }

    [McpServerTool]
    [Description("Run all catalog gates over a discovery folder; returns the findings. The skill's self-validation and CI run this same code.")]
    public static string Gate(
        [Description("Discovery folder holding rules/, terms/ and tests/")] string discoveryDir,
        [Description("Directory containing the C# sources")] string srcDir,
        [Description("Directory containing the T-SQL scripts")] string sqlDir,
        [Description("Directory containing the artefact schemas")] string schemasDir)
    {
        var report = GateRunner.Run(new GateInput(
            discoveryDir, schemasDir,
            CSharpIndexer.IndexDirectory(srcDir),
            SqlIndexer.IndexDirectory(sqlDir)));
        return JsonSerializer.Serialize(report, Json);
    }
}
