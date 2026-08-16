using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SpecAnchor.Index.Sql;

/// <summary>
/// Builds the deterministic T-SQL index of a script directory. Parsing is a full
/// ScriptDom AST (TSql160), never regex; files are read in ordinal order so the same
/// tree always produces a byte-identical index. Parse errors become blind spots.
/// </summary>
public static class SqlIndexer
{
    /// <summary>
    /// Indexes every .sql file under <paramref name="sqlRoot"/>. A file whose
    /// statements contain no CREATE is inventoried as a loose script (job step).
    /// </summary>
    /// <param name="sqlRoot">Directory containing the T-SQL scripts.</param>
    /// <returns>The index artefact.</returns>
    public static SqlIndex IndexDirectory(string sqlRoot)
    {
        var root = Path.GetFullPath(sqlRoot);
        var files = Directory.EnumerateFiles(root, "*.sql", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        var tables = new List<TableEntry>();
        var procedures = new List<ProcedureEntry>();
        var triggers = new List<TriggerEntry>();
        var scripts = new List<ScriptEntry>();
        var blindSpots = new List<SqlBlindSpot>();
        var parser = new TSql160Parser(initialQuotedIdentifiers: true);

        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(root, file);
            using var reader = new StreamReader(file);
            var fragment = parser.Parse(reader, out var errors);

            foreach (var error in errors)
            {
                blindSpots.Add(new SqlBlindSpot(relative, error.Line, $"SQL{error.Number}: {error.Message}"));
            }

            if (fragment is not TSqlScript script)
            {
                continue;
            }

            var looseAccumulator = new BodyAccumulator();
            var sawCreate = false;

            foreach (var statement in script.Batches.SelectMany(b => b.Statements))
            {
                switch (statement)
                {
                    case CreateTableStatement table:
                        sawCreate = true;
                        tables.Add(ToTableEntry(table, relative));
                        break;
                    case ProcedureStatementBody procedure:
                        sawCreate = true;
                        procedures.Add(ToProcedureEntry(procedure, relative));
                        break;
                    case TriggerStatementBody trigger:
                        sawCreate = true;
                        triggers.Add(ToTriggerEntry(trigger, relative));
                        break;
                    default:
                        statement.Accept(looseAccumulator);
                        break;
                }
            }

            if (!sawCreate && (looseAccumulator.Reads.Count > 0 || looseAccumulator.Writes.Count > 0 ||
                               looseAccumulator.Executes.Count > 0 || looseAccumulator.Branches > 0))
            {
                scripts.Add(new ScriptEntry(
                    relative,
                    looseAccumulator.Branches,
                    Sorted(looseAccumulator.Reads),
                    Sorted(looseAccumulator.Writes),
                    Sorted(looseAccumulator.Executes)));
            }
        }

        return new SqlIndex(
            Mode: blindSpots.Count == 0 ? "ast" : "ast-with-errors",
            Tables: tables.OrderBy(t => t.Name, StringComparer.Ordinal).ToList(),
            Procedures: procedures.OrderBy(p => p.Name, StringComparer.Ordinal).ToList(),
            Triggers: triggers.OrderBy(t => t.Name, StringComparer.Ordinal).ToList(),
            Scripts: scripts.OrderBy(s => s.File, StringComparer.Ordinal).ToList(),
            BlindSpots: blindSpots.OrderBy(b => b.File, StringComparer.Ordinal).ThenBy(b => b.Line).ToList());
    }

    private static TableEntry ToTableEntry(CreateTableStatement table, string file)
    {
        var columns = table.Definition.ColumnDefinitions
            .Select(c => new ColumnEntry(c.ColumnIdentifier.Value, TokenText(c.DataType)))
            .ToList();

        var foreignKeys = new SortedSet<string>(StringComparer.Ordinal);
        var constraints = table.Definition.TableConstraints
            .Concat(table.Definition.ColumnDefinitions.SelectMany(c => c.Constraints));
        foreach (var constraint in constraints.OfType<ForeignKeyConstraintDefinition>())
        {
            foreignKeys.Add(NameOf(constraint.ReferenceTableName));
        }

        return new TableEntry(
            NameOf(table.SchemaObjectName), file,
            table.StartLine, EndLine(table),
            columns, foreignKeys.ToList());
    }

    private static ProcedureEntry ToProcedureEntry(ProcedureStatementBody procedure, string file)
    {
        var accumulator = new BodyAccumulator();
        procedure.StatementList?.Accept(accumulator);

        var parameters = procedure.Parameters
            .Select(p => new ColumnEntry(p.VariableName.Value, TokenText(p.DataType)))
            .ToList();

        return new ProcedureEntry(
            NameOf(procedure.ProcedureReference.Name), file,
            procedure.StartLine, EndLine(procedure),
            parameters, accumulator.Branches,
            Sorted(accumulator.Reads), Sorted(accumulator.Writes), Sorted(accumulator.Executes));
    }

    private static TriggerEntry ToTriggerEntry(TriggerStatementBody trigger, string file)
    {
        var accumulator = new BodyAccumulator();
        trigger.StatementList?.Accept(accumulator);

        var events = trigger.TriggerActions
            .Select(a => a.TriggerActionType.ToString().ToUpperInvariant())
            .OrderBy(e => e, StringComparer.Ordinal)
            .ToList();

        return new TriggerEntry(
            NameOf(trigger.Name),
            NameOf(trigger.TriggerObject.Name),
            trigger.TriggerType.ToString().ToUpperInvariant(),
            events, file,
            trigger.StartLine, EndLine(trigger),
            accumulator.Branches,
            Sorted(accumulator.Reads), Sorted(accumulator.Writes));
    }

    private static string NameOf(SchemaObjectName name) =>
        string.Join(".", name.Identifiers.Select(i => i.Value));

    private static int EndLine(TSqlFragment fragment) =>
        fragment.ScriptTokenStream[fragment.LastTokenIndex].Line;

    private static string TokenText(TSqlFragment fragment) =>
        string.Concat(Enumerable.Range(fragment.FirstTokenIndex, fragment.LastTokenIndex - fragment.FirstTokenIndex + 1)
            .Select(i => fragment.ScriptTokenStream[i].Text));

    private static IReadOnlyList<string> Sorted(IEnumerable<string> values) =>
        values.Distinct(StringComparer.Ordinal).OrderBy(v => v, StringComparer.Ordinal).ToList();

    /// <summary>
    /// Walks one statement body collecting reads, writes, branches and EXECs.
    /// A NamedTableReference is a read unless it is the target of an INSERT, UPDATE,
    /// DELETE or MERGE — targets are recorded before children are visited, so the
    /// same node is never double-counted.
    /// </summary>
    private sealed class BodyAccumulator : TSqlFragmentVisitor
    {
        private readonly HashSet<TSqlFragment> _writeTargets = new();

        public HashSet<string> Reads { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Writes { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Executes { get; } = new(StringComparer.Ordinal);

        public int Branches { get; private set; }

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

        public override void ExplicitVisit(MergeSpecification node)
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

        public override void ExplicitVisit(IfStatement node)
        {
            Branches++;
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(WhileStatement node)
        {
            Branches++;
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(SearchedCaseExpression node)
        {
            Branches++;
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(SimpleCaseExpression node)
        {
            Branches++;
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(ExecuteStatement node)
        {
            if (node.ExecuteSpecification.ExecutableEntity is ExecutableProcedureReference procedure &&
                procedure.ProcedureReference?.ProcedureReference?.Name is { } name)
            {
                Executes.Add(string.Join(".", name.Identifiers.Select(i => i.Value)));
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
