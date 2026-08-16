namespace SpecAnchor.Index.Matrix;

/// <summary>
/// The table read/write matrix — which accessor touches which table, joined across
/// both indexes. Data-level coupling is invisible in code and the most dangerous of
/// the three coupling layers; this artefact is where it becomes visible.
/// </summary>
/// <param name="Entries">Accessor × table × access rows, sorted.</param>
/// <param name="Coverage">Honest accounting of SQL-in-strings resolution.</param>
public sealed record TableAccessMatrix(
    IReadOnlyList<TableAccess> Entries,
    DataAccessCoverage Coverage);

/// <summary>One accessor touching one table.</summary>
/// <param name="Accessor">Member, procedure, trigger or script name.</param>
/// <param name="AccessorKind">csharp, procedure, trigger or script.</param>
/// <param name="Table">The table touched.</param>
/// <param name="Access">read or write.</param>
/// <param name="File">Source file of the accessor, relative to its indexed root.</param>
/// <param name="Line">1-based line of the evidence (literal or declaration).</param>
public sealed record TableAccess(
    string Accessor,
    string AccessorKind,
    string Table,
    string Access,
    string File,
    int Line);

/// <summary>
/// The data_access_coverage artefact (schema v1): declared honestly, never claimed.
/// An unresolved call site in a calculation path is itself a finding worth reporting.
/// </summary>
/// <param name="CallSitesTotal">String literals that look like SQL.</param>
/// <param name="CallSitesResolved">Of those, how many parsed to a usable AST.</param>
/// <param name="ByTechnology">Per-technology totals; v1 resolves string-sql only.</param>
/// <param name="Unresolved">SQL-looking literals that did not parse, with the reason.</param>
/// <param name="RuntimeOnly">Statements seen only at runtime capture; empty until capture exists.</param>
public sealed record DataAccessCoverage(
    int CallSitesTotal,
    int CallSitesResolved,
    IReadOnlyList<TechnologyCoverage> ByTechnology,
    IReadOnlyList<UnresolvedSite> Unresolved,
    IReadOnlyList<string> RuntimeOnly);

/// <summary>Coverage split for one data access technology.</summary>
/// <param name="Technology">Technology key from the data-access-coverage schema.</param>
/// <param name="Total">Call sites seen.</param>
/// <param name="Resolved">Call sites resolved to tables.</param>
public sealed record TechnologyCoverage(string Technology, int Total, int Resolved);

/// <summary>A call site the matrix could not resolve, reported instead of skipped.</summary>
/// <param name="File">File containing the literal.</param>
/// <param name="Line">1-based line of the literal.</param>
/// <param name="Reason">Why resolution failed.</param>
public sealed record UnresolvedSite(string File, int Line, string Reason);
