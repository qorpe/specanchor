namespace SpecAnchor.Index.Sql;

/// <summary>
/// The deterministic T-SQL index built by <see cref="SqlIndexer"/> from a script
/// directory. Stored procedure and trigger bodies are parsed to a full AST — in a
/// legacy factoring system a large share of the business logic lives here, and an
/// extraction that reads only application code looks competent and is wrong.
/// </summary>
/// <param name="Mode">"ast" when every file parsed cleanly; "ast-with-errors" otherwise.</param>
/// <param name="Tables">Table inventory with columns and foreign keys.</param>
/// <param name="Procedures">Stored procedures with branches, reads, writes and calls.</param>
/// <param name="Triggers">Triggers with their table, timing, events and body analysis.</param>
/// <param name="Scripts">Loose scripts (typically exported job steps) with what they execute.</param>
/// <param name="BlindSpots">Parse errors reported honestly instead of skipped.</param>
public sealed record SqlIndex(
    string Mode,
    IReadOnlyList<TableEntry> Tables,
    IReadOnlyList<ProcedureEntry> Procedures,
    IReadOnlyList<TriggerEntry> Triggers,
    IReadOnlyList<ScriptEntry> Scripts,
    IReadOnlyList<SqlBlindSpot> BlindSpots);

/// <summary>One table with its columns and outgoing foreign keys.</summary>
/// <param name="Name">Table name including schema when written.</param>
/// <param name="File">Declaring script file, relative to the indexed root.</param>
/// <param name="LineStart">1-based first line of the CREATE TABLE statement.</param>
/// <param name="LineEnd">1-based last line of the statement.</param>
/// <param name="Columns">Column names with their declared types.</param>
/// <param name="ForeignKeysTo">Names of tables referenced by foreign keys.</param>
public sealed record TableEntry(
    string Name,
    string File,
    int LineStart,
    int LineEnd,
    IReadOnlyList<ColumnEntry> Columns,
    IReadOnlyList<string> ForeignKeysTo);

/// <summary>One column: name and declared type text.</summary>
/// <param name="Name">Column name.</param>
/// <param name="Type">Declared type exactly as written, e.g. DECIMAL(18,2).</param>
public sealed record ColumnEntry(string Name, string Type);

/// <summary>One stored procedure with its body analysis.</summary>
/// <param name="Name">Procedure name including schema when written.</param>
/// <param name="File">Declaring script file, relative to the indexed root.</param>
/// <param name="LineStart">1-based first line of the statement.</param>
/// <param name="LineEnd">1-based last line of the statement.</param>
/// <param name="Parameters">Parameter names with their declared types.</param>
/// <param name="BranchCount">IF / WHILE / CASE decision points in the body.</param>
/// <param name="Reads">Tables the body reads (includes inserted/deleted pseudo-tables).</param>
/// <param name="Writes">Tables the body inserts into, updates or deletes from.</param>
/// <param name="ExecutesProcedures">Procedures the body EXECs.</param>
public sealed record ProcedureEntry(
    string Name,
    string File,
    int LineStart,
    int LineEnd,
    IReadOnlyList<ColumnEntry> Parameters,
    int BranchCount,
    IReadOnlyList<string> Reads,
    IReadOnlyList<string> Writes,
    IReadOnlyList<string> ExecutesProcedures);

/// <summary>One trigger — a classic hiding place for business rules.</summary>
/// <param name="Name">Trigger name.</param>
/// <param name="Table">The table the trigger is attached to.</param>
/// <param name="Timing">AFTER, INSTEAD OF or FOR.</param>
/// <param name="Events">INSERT / UPDATE / DELETE actions the trigger fires on.</param>
/// <param name="File">Declaring script file, relative to the indexed root.</param>
/// <param name="LineStart">1-based first line of the statement.</param>
/// <param name="LineEnd">1-based last line of the statement.</param>
/// <param name="BranchCount">Decision points in the body.</param>
/// <param name="Reads">Tables the body reads.</param>
/// <param name="Writes">Tables the body writes.</param>
public sealed record TriggerEntry(
    string Name,
    string Table,
    string Timing,
    IReadOnlyList<string> Events,
    string File,
    int LineStart,
    int LineEnd,
    int BranchCount,
    IReadOnlyList<string> Reads,
    IReadOnlyList<string> Writes);

/// <summary>
/// A script file with no CREATE statement — typically an exported job step. Scheduling
/// semantics live here, not in application code.
/// </summary>
/// <param name="File">Script file, relative to the indexed root.</param>
/// <param name="BranchCount">Decision points including WHILE loops.</param>
/// <param name="Reads">Tables the script reads.</param>
/// <param name="Writes">Tables the script writes.</param>
/// <param name="ExecutesProcedures">Procedures the script EXECs.</param>
public sealed record ScriptEntry(
    string File,
    int BranchCount,
    IReadOnlyList<string> Reads,
    IReadOnlyList<string> Writes,
    IReadOnlyList<string> ExecutesProcedures);

/// <summary>A location the parser could not resolve, reported instead of skipped.</summary>
/// <param name="File">Script file, relative to the indexed root.</param>
/// <param name="Line">1-based line of the parse error.</param>
/// <param name="Reason">Parser error number and message.</param>
public sealed record SqlBlindSpot(string File, int Line, string Reason);
