using System.Text.Json.Nodes;
using Json.Schema;
using SpecAnchor.Index.CSharp;
using SpecAnchor.Index.Sql;
using YamlDotNet.RepresentationModel;
using Yaml2JsonNode;

namespace SpecAnchor.Artefacts;

/// <summary>
/// The deterministic self-validation engine every skill calls before returning.
/// A skill that produces without validating is not part of this toolset: a rule card
/// that fails here is REJECTED — removed from the catalog and reported — not flagged.
/// The same checks later back the source-ref and statement-quality gates, so skill
/// output and gate enforcement can never drift apart.
/// </summary>
public static class ArtefactValidator
{
    /// <summary>
    /// Validates one rule card (YAML or JSON) against the rule schema, the indexes and
    /// the glossary. Empty result = the card may enter the catalog.
    /// </summary>
    /// <param name="documentText">The rule card, YAML or JSON.</param>
    /// <param name="ruleSchemaPath">Path to rule.schema.v1.json.</param>
    /// <param name="csharpIndex">C# index used to resolve file source_refs.</param>
    /// <param name="sqlIndex">SQL index used to resolve object source_refs.</param>
    /// <param name="aliasesInCode">Glossary aliases_in_code; none may appear in the statement.</param>
    /// <returns>Findings, most severe first; empty when the card is valid.</returns>
    public static IReadOnlyList<Finding> ValidateRule(
        string documentText,
        string ruleSchemaPath,
        CSharpIndex csharpIndex,
        SqlIndex sqlIndex,
        IReadOnlyList<string> aliasesInCode)
    {
        if (TryParse(documentText, out var node, out var parseError))
        {
            var findings = new List<Finding>();
            findings.AddRange(EvaluateSchema(node, ruleSchemaPath));
            if (findings.Count == 0)
            {
                findings.AddRange(ResolveSourceRefs(node!, csharpIndex, sqlIndex));
                findings.AddRange(CheckStatementQuality(node!, aliasesInCode));
            }

            return findings;
        }

        return [new Finding("error", "SA0001", "$", $"document does not parse: {parseError}")];
    }

    private static bool TryParse(string text, out JsonNode? node, out string? error)
    {
        node = null;
        error = null;
        try
        {
            var trimmed = text.TrimStart();
            if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
            {
                node = JsonNode.Parse(text);
                return node is not null;
            }

            var stream = new YamlStream();
            stream.Load(new StringReader(text));
            node = stream.Documents[0].RootNode.ToJsonNode();
            return node is not null;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, JsonSchema> SchemaCache = new();

    private static IEnumerable<Finding> EvaluateSchema(JsonNode? node, string schemaPath)
    {
        var schema = SchemaCache.GetOrAdd(Path.GetFullPath(schemaPath),
            p => JsonSchema.FromText(File.ReadAllText(p)));
        var element = System.Text.Json.JsonSerializer.SerializeToElement(node);
        var result = schema.Evaluate(element, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (result.IsValid)
        {
            yield break;
        }

        foreach (var detail in result.Details ?? [])
        {
            if (detail.Errors is null)
            {
                continue;
            }

            foreach (var kvp in detail.Errors)
            {
                yield return new Finding("error", "SA0002",
                    detail.InstanceLocation.ToString(), $"{kvp.Key}: {kvp.Value}");
            }
        }
    }

    private static IEnumerable<Finding> ResolveSourceRefs(
        JsonNode rule, CSharpIndex csharpIndex, SqlIndex sqlIndex)
    {
        var knownFiles = KnownCSharpFiles(csharpIndex);
        var refs = rule["source_ref"]!.AsArray();
        for (var i = 0; i < refs.Count; i++)
        {
            var reference = refs[i]!;
            var path = $"$.source_ref[{i}]";
            if (reference["file"] is { } fileNode)
            {
                var file = fileNode.GetValue<string>();
                if (!knownFiles.Contains(file))
                {
                    yield return new Finding("error", "SA0101", path,
                        $"file '{file}' is not known to the C# index — the reference does not resolve");
                    continue;
                }

                var lineStart = AsInt(reference["line_start"]!);
                var lineEnd = AsInt(reference["line_end"]!);
                var overlaps = csharpIndex.Types.Any(t =>
                    t.File == file &&
                    (Overlaps(lineStart, lineEnd, t.LineStart, t.LineEnd) ||
                     t.Members.Any(m => Overlaps(lineStart, lineEnd, m.LineStart, m.LineEnd))));
                if (!overlaps)
                {
                    yield return new Finding("error", "SA0102", path,
                        $"lines {lineStart}-{lineEnd} of '{file}' do not overlap any declared symbol");
                }
            }
            else if (reference["object"] is { } objectNode)
            {
                var name = objectNode.GetValue<string>();
                var kind = reference["kind"]!.GetValue<string>();
                var resolves = kind switch
                {
                    "procedure" => sqlIndex.Procedures.Any(p => p.Name == name),
                    "trigger" => sqlIndex.Triggers.Any(t => t.Name == name),
                    "job" => sqlIndex.Scripts.Any(s => s.File.Contains(name, StringComparison.Ordinal)),
                    _ => false,
                };
                if (!resolves)
                {
                    yield return new Finding("error", "SA0104", path,
                        $"{kind} '{name}' is not known to the SQL index — the reference does not resolve");
                }
            }
        }
    }

    private static IEnumerable<Finding> CheckStatementQuality(
        JsonNode rule, IReadOnlyList<string> aliasesInCode)
    {
        var statement = rule["statement"]!.GetValue<string>();
        foreach (var alias in aliasesInCode.Where(a => a.Length >= 3))
        {
            if (statement.Contains(alias, StringComparison.Ordinal))
            {
                yield return new Finding("error", "SA0103", "$.statement",
                    $"statement contains code identifier '{alias}' — a rule sentence must use domain language " +
                    "(the domain expert must be able to say 'no, that is wrong')");
            }
        }
    }

    private static HashSet<string> KnownCSharpFiles(CSharpIndex index)
    {
        var files = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in index.Types)
        {
            files.Add(type.File);
            foreach (var member in type.Members)
            {
                files.Add(member.File);
            }
        }

        return files;
    }

    private static int AsInt(JsonNode node) =>
        node.AsValue().TryGetValue<int>(out var i) ? i : (int)node.GetValue<decimal>();

    private static bool Overlaps(int aStart, int aEnd, int bStart, int bEnd) =>
        aStart <= bEnd && bStart <= aEnd;
}

/// <summary>One validation finding.</summary>
/// <param name="Severity">error or warning.</param>
/// <param name="Code">Stable finding code (SA0xxx).</param>
/// <param name="Path">JSON path of the offending value.</param>
/// <param name="Message">What is wrong and what the fix must teach.</param>
public sealed record Finding(string Severity, string Code, string Path, string Message);
