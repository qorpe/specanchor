namespace SpecAnchor.Index.CSharp;

/// <summary>
/// The deterministic C# index. Built by <see cref="CSharpIndexer"/> with no model,
/// no embeddings and no vector search: same input, same output. Agents read this
/// instead of the raw repository.
/// </summary>
/// <param name="Mode">
/// "semantic" when the ad-hoc compilation produced no errors; "semantic-with-errors"
/// when it did — in that case <paramref name="BlindSpots"/> lists what could not be
/// resolved instead of skipping it silently.
/// </param>
/// <param name="Namespaces">Namespace map with the number of declared types in each.</param>
/// <param name="Types">Type and member inventory with file and line spans.</param>
/// <param name="CallGraph">Caller → callee edges between members declared in source.</param>
/// <param name="DeadCodeCandidates">
/// Publicly visible symbols declared in source with no inbound reference from any other
/// type. Candidates, not verdicts: a human or a later pass confirms.
/// </param>
/// <param name="StringLiterals">
/// String literals (interpolation holes replaced with @p0, @p1, …) with their containing
/// member. Raw SQL hiding in strings is resolved from these by the table access matrix.
/// </param>
/// <param name="BlindSpots">Compilation errors reported honestly as coverage gaps.</param>
public sealed record CSharpIndex(
    string Mode,
    IReadOnlyList<NamespaceEntry> Namespaces,
    IReadOnlyList<TypeEntry> Types,
    IReadOnlyList<CallEdge> CallGraph,
    IReadOnlyList<string> DeadCodeCandidates,
    IReadOnlyList<LiteralEntry> StringLiterals,
    IReadOnlyList<BlindSpot> BlindSpots);

/// <summary>One string literal and where it lives.</summary>
/// <param name="Value">Literal text; interpolation holes appear as @p0, @p1, …</param>
/// <param name="ContainingMember">Display name of the enclosing member.</param>
/// <param name="File">Path of the file, relative to the indexed root.</param>
/// <param name="Line">1-based line of the literal.</param>
public sealed record LiteralEntry(string Value, string ContainingMember, string File, int Line);

/// <summary>One namespace and how many types it declares.</summary>
/// <param name="Name">Namespace display name; "&lt;global&gt;" for the global namespace.</param>
/// <param name="TypeCount">Number of types declared in the namespace within the indexed sources.</param>
public sealed record NamespaceEntry(string Name, int TypeCount);

/// <summary>One type declared in the indexed sources, with a resolvable source span.</summary>
/// <param name="FullName">Fully qualified display name.</param>
/// <param name="Kind">Roslyn type kind: Class, Struct, Interface, Enum, Delegate.</param>
/// <param name="File">Path of the declaring file, relative to the indexed root.</param>
/// <param name="LineStart">1-based first line of the declaration.</param>
/// <param name="LineEnd">1-based last line of the declaration.</param>
/// <param name="Members">Declared methods, constructors and properties.</param>
public sealed record TypeEntry(
    string FullName,
    string Kind,
    string File,
    int LineStart,
    int LineEnd,
    IReadOnlyList<MemberEntry> Members);

/// <summary>One member declared on a type, with its span, complexity and decision surface.</summary>
/// <param name="Name">Member display name including parameters.</param>
/// <param name="Kind">Method, Constructor or Property.</param>
/// <param name="File">Path of the declaring file, relative to the indexed root.</param>
/// <param name="LineStart">1-based first line of the declaration.</param>
/// <param name="LineEnd">1-based last line of the declaration.</param>
/// <param name="CyclomaticComplexity">1 plus the number of decision points in the body.</param>
/// <param name="BranchConditions">
/// The member's decision surface: every if/while/ternary/switch condition as written,
/// in syntax order (whitespace collapsed, capped per entry). This is what lets the
/// rule extractor read a calculation's branching without ever opening the source file —
/// the C# counterpart of a procedure's parsed AST.
/// </param>
public sealed record MemberEntry(
    string Name,
    string Kind,
    string File,
    int LineStart,
    int LineEnd,
    int CyclomaticComplexity,
    IReadOnlyList<string> BranchConditions);

/// <summary>A caller → callee edge between two members declared in source.</summary>
/// <param name="Caller">Display name of the invoking member.</param>
/// <param name="Callee">Display name of the invoked member or constructor.</param>
public sealed record CallEdge(string Caller, string Callee);

/// <summary>A location the index could not resolve, reported instead of skipped.</summary>
/// <param name="File">Path of the file, relative to the indexed root; empty when unknown.</param>
/// <param name="Line">1-based line of the problem; 0 when unknown.</param>
/// <param name="Reason">Compiler diagnostic id and message.</param>
public sealed record BlindSpot(string File, int Line, string Reason);
