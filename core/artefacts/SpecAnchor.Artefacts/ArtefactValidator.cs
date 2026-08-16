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

    /// <summary>
    /// Validates one ledger-term entry (YAML or JSON) against the schema, the index
    /// identifiers and the rule catalog. Empty result = the entry may enter the glossary.
    /// </summary>
    /// <param name="documentText">The ledger-term entry, YAML or JSON.</param>
    /// <param name="ledgerSchemaPath">Path to ledger-term.schema.v1.json.</param>
    /// <param name="csharpIndex">C# index supplying known identifiers.</param>
    /// <param name="sqlIndex">SQL index supplying known table/column/object names.</param>
    /// <param name="ruleStatements">Statements of the context's rule catalog.</param>
    /// <returns>Findings; empty when the entry is valid.</returns>
    public static IReadOnlyList<Finding> ValidateLedgerTerm(
        string documentText,
        string ledgerSchemaPath,
        CSharpIndex csharpIndex,
        SqlIndex sqlIndex,
        IReadOnlyList<string> ruleStatements)
    {
        if (!TryParse(documentText, out var node, out var parseError))
        {
            return [new Finding("error", "SA0001", "$", $"document does not parse: {parseError}")];
        }

        var findings = new List<Finding>();
        findings.AddRange(EvaluateSchema(node, ledgerSchemaPath));
        if (findings.Count > 0)
        {
            return findings;
        }

        var known = KnownIdentifiers(csharpIndex, sqlIndex);
        var aliases = node!["aliases_in_code"]!.AsArray();
        for (var i = 0; i < aliases.Count; i++)
        {
            var alias = aliases[i]!.GetValue<string>();
            if (!known.Contains(alias))
            {
                findings.Add(new Finding("error", "SA0201", $"$.aliases_in_code[{i}]",
                    $"alias '{alias}' resolves to no identifier known to the indexes — " +
                    "a glossary term with no anchor in code is speculation, not vocabulary"));
            }
        }

        var term = node["term"]!.GetValue<string>();
        if (!ruleStatements.Any(s => s.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            findings.Add(new Finding("error", "SA0202", "$.term",
                $"term '{term}' appears in no rule statement — the glossary serves the catalog"));
        }

        return findings;
    }

    /// <summary>
    /// Validates one char-test record (YAML or JSON) against the schema and the rule
    /// catalog, including arithmetic consistency of the recorded result.
    /// </summary>
    /// <param name="documentText">The char-test record, YAML or JSON.</param>
    /// <param name="charTestSchemaPath">Path to char-test.schema.v1.json.</param>
    /// <param name="knownRuleIds">rule_ids present in the catalog.</param>
    /// <returns>Findings; empty when the record is valid.</returns>
    public static IReadOnlyList<Finding> ValidateCharTest(
        string documentText,
        string charTestSchemaPath,
        IReadOnlyList<string> knownRuleIds)
    {
        if (!TryParse(documentText, out var node, out var parseError))
        {
            return [new Finding("error", "SA0001", "$", $"document does not parse: {parseError}")];
        }

        var findings = new List<Finding>();
        findings.AddRange(EvaluateSchema(node, charTestSchemaPath));
        if (findings.Count > 0)
        {
            return findings;
        }

        var ruleId = node!["rule_id"]!.GetValue<string>();
        if (!knownRuleIds.Contains(ruleId, StringComparer.Ordinal))
        {
            findings.Add(new Finding("error", "SA0301", "$.rule_id",
                $"rule '{ruleId}' is not in the catalog — a test must prove an existing rule"));
        }

        var sampleSize = AsInt(node["sample_size"]!);
        var passed = AsInt(node["result"]!["passed"]!);
        var failed = AsInt(node["result"]!["failed"]!);
        if (passed + failed != sampleSize)
        {
            findings.Add(new Finding("error", "SA0302", "$.result",
                $"passed ({passed}) + failed ({failed}) != sample_size ({sampleSize}) — " +
                "an arithmetic hole here means the run report cannot be trusted"));
        }

        return findings;
    }

    private static HashSet<string> KnownIdentifiers(CSharpIndex csharpIndex, SqlIndex sqlIndex)
    {
        var known = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in csharpIndex.Types)
        {
            known.Add(SimpleName(type.FullName));
            foreach (var member in type.Members)
            {
                var display = member.Name;
                var parenIndex = display.IndexOf('(', StringComparison.Ordinal);
                if (parenIndex >= 0)
                {
                    display = display[..parenIndex];
                }

                known.Add(SimpleName(display));
            }
        }

        foreach (var table in sqlIndex.Tables)
        {
            known.Add(SimpleName(table.Name));
            foreach (var column in table.Columns)
            {
                known.Add(column.Name);
            }
        }

        foreach (var procedure in sqlIndex.Procedures)
        {
            known.Add(SimpleName(procedure.Name));
        }

        foreach (var trigger in sqlIndex.Triggers)
        {
            known.Add(SimpleName(trigger.Name));
        }

        return known;
    }

    private static string SimpleName(string qualified)
    {
        var lastDot = qualified.LastIndexOf('.');
        return lastDot >= 0 ? qualified[(lastDot + 1)..] : qualified;
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
