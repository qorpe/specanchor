using Microsoft.SqlServer.TransactSql.ScriptDom;
using SpecAnchor.Index.CSharp;
using SpecAnchor.Index.Sql;

namespace SpecAnchor.Index.Matrix;

/// <summary>
/// Joins the C# and SQL indexes into the table read/write matrix. SQL-side accessors
/// come straight from the SQL index; C#-side accessors come from string literals that
/// parse as T-SQL DML (raw SQL in strings — the Dapper/ADO layer of a legacy system).
/// Literals that look like SQL but do not parse are reported as unresolved, never dropped.
/// </summary>
public static class TableAccessMatrixBuilder
{
    private static readonly string[] SqlMarkers =
        ["INSERT INTO", "SELECT ", "UPDATE ", "DELETE FROM", "MERGE "];

    private static readonly HashSet<string> PseudoTables =
        new(StringComparer.OrdinalIgnoreCase) { "inserted", "deleted" };

    /// <summary>Builds the matrix and its coverage artefact from the two indexes.</summary>
    /// <param name="csharp">The C# index (supplies string literals and their members).</param>
    /// <param name="sql">The SQL index (supplies procedure/trigger/script accesses).</param>
    /// <returns>The combined matrix.</returns>
    public static TableAccessMatrix Build(CSharpIndex csharp, SqlIndex sql)
    {
        var entries = new List<TableAccess>();

        foreach (var procedure in sql.Procedures)
        {
            AddSqlSide(entries, procedure.Name, "procedure", procedure.File, procedure.LineStart,
                procedure.Reads, procedure.Writes);
        }

        foreach (var trigger in sql.Triggers)
        {
            AddSqlSide(entries, trigger.Name, "trigger", trigger.File, trigger.LineStart,
                trigger.Reads, trigger.Writes);
        }

        foreach (var script in sql.Scripts)
        {
            AddSqlSide(entries, script.File, "script", script.File, 1,
                script.Reads, script.Writes);
        }

        var total = 0;
        var resolved = 0;
        var unresolved = new List<UnresolvedSite>();
        var parser = new TSql160Parser(initialQuotedIdentifiers: true);

        foreach (var literal in csharp.StringLiterals)
        {
            if (!LooksLikeSql(literal.Value))
            {
                continue;
            }

            total++;
            var (reads, writes, error) = ParseLiteral(parser, literal.Value);
            if (error is not null || (reads.Count == 0 && writes.Count == 0))
            {
                unresolved.Add(new UnresolvedSite(literal.File, literal.Line,
                    error ?? "parsed, but no table reference found"));
                continue;
            }

            resolved++;
            foreach (var table in reads)
            {
                entries.Add(new TableAccess(literal.ContainingMember, "csharp", table, "read",
                    literal.File, literal.Line));
            }

            foreach (var table in writes)
            {
                entries.Add(new TableAccess(literal.ContainingMember, "csharp", table, "write",
                    literal.File, literal.Line));
            }
        }

        var coverage = new DataAccessCoverage(
            CallSitesTotal: total,
            CallSitesResolved: resolved,
            ByTechnology: [new TechnologyCoverage("string-sql", total, resolved)],
            Unresolved: unresolved.OrderBy(u => u.File, StringComparer.Ordinal).ThenBy(u => u.Line).ToList(),
            RuntimeOnly: []);

        return new TableAccessMatrix(
            entries
                .Distinct()
                .OrderBy(e => e.Accessor, StringComparer.Ordinal)
                .ThenBy(e => e.Table, StringComparer.Ordinal)
                .ThenBy(e => e.Access, StringComparer.Ordinal)
                .ToList(),
            coverage);
    }

    private static void AddSqlSide(
        List<TableAccess> entries,
        string accessor,
        string kind,
        string file,
        int line,
        IEnumerable<string> reads,
        IEnumerable<string> writes)
    {
        foreach (var table in reads.Where(t => !PseudoTables.Contains(t)))
        {
            entries.Add(new TableAccess(accessor, kind, table, "read", file, line));
        }

        foreach (var table in writes.Where(t => !PseudoTables.Contains(t)))
        {
            entries.Add(new TableAccess(accessor, kind, table, "write", file, line));
        }
    }

    private static bool LooksLikeSql(string value)
    {
        return SqlMarkers.Any(m => value.Contains(m, StringComparison.OrdinalIgnoreCase));
    }

    private static (IReadOnlyList<string> Reads, IReadOnlyList<string> Writes, string? Error) ParseLiteral(
        TSql160Parser parser,
        string sqlText)
    {
        using var reader = new StringReader(sqlText);
        var fragment = parser.Parse(reader, out var errors);
        if (errors.Count > 0)
        {
            var first = errors[0];
            return ([], [], $"SQL{first.Number}: {first.Message}");
        }

        var visitor = new TableReferenceVisitor();
        fragment.Accept(visitor);
        return (
            visitor.Reads.OrderBy(t => t, StringComparer.Ordinal).ToList(),
            visitor.Writes.OrderBy(t => t, StringComparer.Ordinal).ToList(),
            null);
    }

    private sealed class TableReferenceVisitor : TSqlFragmentVisitor
    {
        private readonly HashSet<TSqlFragment> _writeTargets = new();

        public HashSet<string> Reads { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Writes { get; } = new(StringComparer.Ordinal);

        public override void ExplicitVisit(InsertSpecification node)
        {
            RecordWrite(node.Target);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(UpdateSpecification node)
        {
            RecordWrite(node.Target);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(DeleteSpecification node)
        {
            RecordWrite(node.Target);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(NamedTableReference node)
        {
            if (!_writeTargets.Contains(node))
            {
                Reads.Add(string.Join(".", node.SchemaObject.Identifiers.Select(i => i.Value)));
            }

            base.ExplicitVisit(node);
        }

        private void RecordWrite(TableReference target)
        {
            if (target is NamedTableReference named)
            {
                _writeTargets.Add(named);
                Writes.Add(string.Join(".", named.SchemaObject.Identifiers.Select(i => i.Value)));
            }
        }
    }
}
