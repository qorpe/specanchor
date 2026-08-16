using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SpecAnchor.Index.CSharp;

/// <summary>
/// Builds the deterministic, symbol-level index of a C# source tree.
/// Detection is semantic (a real Roslyn compilation over the sources), not textual;
/// files are read in ordinal order so the same tree always produces a byte-identical index.
/// </summary>
public static class CSharpIndexer
{
    /// <summary>
    /// Indexes every .cs file under <paramref name="sourceRoot"/> (bin/, obj/ and .git/ excluded).
    /// Compilation errors do not abort the run: they are reported as blind spots and the
    /// mode is downgraded to "semantic-with-errors".
    /// </summary>
    /// <param name="sourceRoot">Directory containing the sources to index.</param>
    /// <returns>The index artefact.</returns>
    public static CSharpIndex IndexDirectory(string sourceRoot)
    {
        var root = Path.GetFullPath(sourceRoot);
        var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsExcluded(root, f))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        var sourceTrees = files
            .Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), path: Path.GetRelativePath(root, f)))
            .ToList();
        var allTrees = sourceTrees
            .Append(CSharpSyntaxTree.ParseText(DefaultGlobalUsings))
            .ToList();

        var references = TrustedPlatformAssemblies()
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();

        var compilation = CSharpCompilation.Create(
            "specanchor-index",
            allTrees,
            references,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));

        // CS5001 (no entry point) is an artifact of compiling as ConsoleApplication so that
        // top-level statements bind; a library-only tree is not a coverage gap.
        var blindSpots = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error && d.Id != "CS5001")
            .Select(ToBlindSpot)
            .OrderBy(b => b.File, StringComparer.Ordinal).ThenBy(b => b.Line)
            .ToList();

        var types = new List<TypeEntry>();
        var callEdges = new HashSet<CallEdge>();
        var referencedFromOtherType = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var declaredTypes = new List<INamedTypeSymbol>();
        var declaredMembers = new List<(ISymbol Symbol, bool IsPublic)>();

        foreach (var tree in sourceTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            CollectDeclarations(tree, model, types, declaredTypes, declaredMembers);
            CollectReferences(tree, model, callEdges, referencedFromOtherType);
        }

        var entryPoint = compilation.GetEntryPoint(CancellationToken.None);
        var deadCandidates = FindDeadCodeCandidates(declaredTypes, declaredMembers, referencedFromOtherType, entryPoint);

        var namespaces = declaredTypes
            .GroupBy(t => t.ContainingNamespace.IsGlobalNamespace ? "<global>" : t.ContainingNamespace.ToDisplayString())
            .Select(g => new NamespaceEntry(g.Key, g.Count()))
            .OrderBy(n => n.Name, StringComparer.Ordinal)
            .ToList();

        return new CSharpIndex(
            Mode: blindSpots.Count == 0 ? "semantic" : "semantic-with-errors",
            Namespaces: namespaces,
            Types: types.OrderBy(t => t.FullName, StringComparer.Ordinal).ToList(),
            CallGraph: callEdges.OrderBy(e => e.Caller, StringComparer.Ordinal).ThenBy(e => e.Callee, StringComparer.Ordinal).ToList(),
            DeadCodeCandidates: deadCandidates,
            BlindSpots: blindSpots);
    }

    /// <summary>
    /// Mirrors the SDK's ImplicitUsings so sources written for modern csproj files bind
    /// under the ad-hoc compilation. Legacy sources with explicit usings are unaffected.
    /// </summary>
    private const string DefaultGlobalUsings = """
        global using global::System;
        global using global::System.Collections.Generic;
        global using global::System.IO;
        global using global::System.Linq;
        global using global::System.Net.Http;
        global using global::System.Threading;
        global using global::System.Threading.Tasks;
        """;

    private static bool IsExcluded(string root, string file)
    {
        var relative = Path.GetRelativePath(root, file);
        var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(s => s is "bin" or "obj" or ".git");
    }

    private static IEnumerable<string> TrustedPlatformAssemblies()
    {
        var tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty;
        return tpa.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
    }

    private static BlindSpot ToBlindSpot(Diagnostic diagnostic)
    {
        var span = diagnostic.Location.GetLineSpan();
        return new BlindSpot(
            File: span.Path ?? string.Empty,
            Line: span.IsValid ? span.StartLinePosition.Line + 1 : 0,
            Reason: $"{diagnostic.Id}: {diagnostic.GetMessage()}");
    }

    private static void CollectDeclarations(
        SyntaxTree tree,
        SemanticModel model,
        List<TypeEntry> types,
        List<INamedTypeSymbol> declaredTypes,
        List<(ISymbol Symbol, bool IsPublic)> declaredMembers)
    {
        foreach (var typeNode in tree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
        {
            if (model.GetDeclaredSymbol(typeNode) is not INamedTypeSymbol typeSymbol)
            {
                continue;
            }

            declaredTypes.Add(typeSymbol);

            var members = new List<MemberEntry>();
            foreach (var memberNode in typeNode.DescendantNodes().OfType<MemberDeclarationSyntax>())
            {
                var (kind, include) = memberNode switch
                {
                    MethodDeclarationSyntax => ("Method", true),
                    ConstructorDeclarationSyntax => ("Constructor", true),
                    PropertyDeclarationSyntax => ("Property", true),
                    _ => ("", false),
                };
                if (!include || model.GetDeclaredSymbol(memberNode) is not ISymbol memberSymbol)
                {
                    continue;
                }

                declaredMembers.Add((memberSymbol, memberSymbol.DeclaredAccessibility == Accessibility.Public));
                var memberSpan = tree.GetLineSpan(memberNode.Span);
                members.Add(new MemberEntry(
                    Name: memberSymbol.ToDisplayString(),
                    Kind: kind,
                    File: tree.FilePath,
                    LineStart: memberSpan.StartLinePosition.Line + 1,
                    LineEnd: memberSpan.EndLinePosition.Line + 1,
                    CyclomaticComplexity: CyclomaticComplexity(memberNode)));
            }

            var span = tree.GetLineSpan(typeNode.Span);
            types.Add(new TypeEntry(
                FullName: typeSymbol.ToDisplayString(),
                Kind: typeSymbol.TypeKind.ToString(),
                File: tree.FilePath,
                LineStart: span.StartLinePosition.Line + 1,
                LineEnd: span.EndLinePosition.Line + 1,
                Members: members.OrderBy(m => m.LineStart).ToList()));
        }
    }

    private static void CollectReferences(
        SyntaxTree tree,
        SemanticModel model,
        HashSet<CallEdge> callEdges,
        HashSet<ISymbol> referencedFromOtherType)
    {
        foreach (var node in tree.GetRoot().DescendantNodes())
        {
            if (node is InvocationExpressionSyntax or BaseObjectCreationExpressionSyntax)
            {
                if (model.GetSymbolInfo(node).Symbol is IMethodSymbol callee &&
                    callee.OriginalDefinition.Locations.Any(l => l.IsInSource) &&
                    model.GetEnclosingSymbol(node.SpanStart) is { } caller)
                {
                    callEdges.Add(new CallEdge(caller.ToDisplayString(), callee.OriginalDefinition.ToDisplayString()));
                }
            }

            if (node is IdentifierNameSyntax or GenericNameSyntax)
            {
                var symbol = model.GetSymbolInfo(node).Symbol?.OriginalDefinition;
                if (symbol is null || !symbol.Locations.Any(l => l.IsInSource))
                {
                    continue;
                }

                var referencingType = model.GetEnclosingSymbol(node.SpanStart) is { } enclosing
                    ? enclosing as INamedTypeSymbol ?? enclosing.ContainingType
                    : null;
                var referencedType = symbol as INamedTypeSymbol ?? symbol.ContainingType;
                if (referencingType is null || referencedType is null ||
                    SymbolEqualityComparer.Default.Equals(referencingType, referencedType))
                {
                    continue;
                }

                referencedFromOtherType.Add(symbol);
                referencedFromOtherType.Add(referencedType);
            }
        }
    }

    private static List<string> FindDeadCodeCandidates(
        List<INamedTypeSymbol> declaredTypes,
        List<(ISymbol Symbol, bool IsPublic)> declaredMembers,
        HashSet<ISymbol> referencedFromOtherType,
        IMethodSymbol? entryPoint)
    {
        var entryType = entryPoint?.ContainingType;
        var candidates = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var type in declaredTypes)
        {
            if (type.DeclaredAccessibility != Accessibility.Public ||
                SymbolEqualityComparer.Default.Equals(type, entryType))
            {
                continue;
            }

            if (!referencedFromOtherType.Contains(type))
            {
                candidates.Add(type.ToDisplayString());
            }
        }

        foreach (var (member, isPublic) in declaredMembers)
        {
            if (!isPublic ||
                SymbolEqualityComparer.Default.Equals(member.ContainingType, entryType) ||
                member is IMethodSymbol { IsOverride: true })
            {
                continue;
            }

            if (!referencedFromOtherType.Contains(member))
            {
                candidates.Add(member.ToDisplayString());
            }
        }

        return candidates.ToList();
    }

    private static int CyclomaticComplexity(SyntaxNode member)
    {
        var count = 1;
        foreach (var node in member.DescendantNodes())
        {
            switch (node)
            {
                case IfStatementSyntax:
                case WhileStatementSyntax:
                case ForStatementSyntax:
                case ForEachStatementSyntax:
                case CaseSwitchLabelSyntax:
                case CasePatternSwitchLabelSyntax:
                case SwitchExpressionArmSyntax:
                case ConditionalExpressionSyntax:
                case CatchClauseSyntax:
                    count++;
                    break;
                case BinaryExpressionSyntax binary when
                    binary.IsKind(SyntaxKind.LogicalAndExpression) ||
                    binary.IsKind(SyntaxKind.LogicalOrExpression) ||
                    binary.IsKind(SyntaxKind.CoalesceExpression):
                    count++;
                    break;
            }
        }

        return count;
    }
}
