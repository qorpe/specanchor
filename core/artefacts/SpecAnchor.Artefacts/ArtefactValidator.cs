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

    /// <summary>
    /// Validates one spec (YAML or JSON) against the schema and the rule catalog: every
    /// rule_id resolves, open_brackets equals the brackets actually in the body, the body
    /// declares its clauses exactly once each, and the spec carries at least as many open
    /// brackets as its undecided rules carry open questions — a spec may not answer what no
    /// domain expert has decided. Clause citation by tests is a catalog-wide check and lives
    /// in the gate runner.
    /// </summary>
    /// <param name="documentText">The spec, YAML or JSON.</param>
    /// <param name="specSchemaPath">Path to spec.schema.v1.json.</param>
    /// <param name="catalogRules">The rules in the catalog.</param>
    /// <returns>Findings; empty when the spec may enter the catalog.</returns>
    public static IReadOnlyList<Finding> ValidateSpec(
        string documentText,
        string specSchemaPath,
        IReadOnlyList<CatalogRule> catalogRules)
    {
        if (!TryParse(documentText, out var node, out var parseError))
        {
            return [new Finding("error", "SA0001", "$", $"document does not parse: {parseError}")];
        }

        var findings = new List<Finding>();
        findings.AddRange(EvaluateSchema(node, specSchemaPath));
        if (findings.Count > 0)
        {
            return findings;
        }

        var ruleIds = node!["rule_ids"]!.AsArray();
        var undecidedQuestions = 0;
        for (var i = 0; i < ruleIds.Count; i++)
        {
            var ruleId = ruleIds[i]!.GetValue<string>();
            var rule = catalogRules.FirstOrDefault(r => r.RuleId == ruleId);
            if (rule is null)
            {
                findings.Add(new Finding("error", "SA0501", $"$.rule_ids[{i}]",
                    $"rule '{ruleId}' is not in the catalog — a spec must be derived from an existing rule"));
            }
            else if (rule.Disposition is null)
            {
                undecidedQuestions += rule.OpenQuestions;
            }
        }

        var body = node["body"]!.GetValue<string>();
        var declared = AsInt(node["open_brackets"]!);
        var actual = SpecBody.CountOpenBrackets(body);
        if (declared != actual)
        {
            findings.Add(new Finding("error", "SA0502", "$.open_brackets",
                $"open_brackets says {declared} but the body carries {actual} [OPEN bracket(s) — " +
                "the count is what Definition of Ready reads, so it must be the real one"));
        }

        if (actual < undecidedQuestions)
        {
            findings.Add(new Finding("error", "SA0503", "$.body",
                $"the body carries {actual} open bracket(s) but its rules carry {undecidedQuestions} open " +
                "question(s) no domain expert has decided (disposition null) — the spec answers what nobody " +
                "decided; keep each one as an [OPEN — ...] bracket until the rule's disposition is set"));
        }

        var clauses = SpecBody.Clauses(body);
        if (clauses.Count == 0)
        {
            findings.Add(new Finding("error", "SA0504", "$.body",
                "the body declares no clause — mark each criterion, scenario or table row with [Cn] " +
                "so a test can cite it; an uncitable spec cannot be proven"));
        }

        foreach (var duplicate in clauses.GroupBy(c => c, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            findings.Add(new Finding("error", "SA0505", $"$.body[{duplicate.Key}]",
                $"clause {duplicate.Key} is declared {duplicate.Count()} times — a citation of it would be ambiguous"));
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

    private static bool TryParse(string text, out JsonNode? node, out string? error) =>
        ArtefactDocument.TryParse(text, out node, out error);

    // Lazy, not a bare GetOrAdd factory: GetOrAdd may run the factory twice under
    // contention, and JsonSchema.FromText registers the schema's $id globally — the second
    // registration throws. Two gate runs in parallel (tests, an MCP client) hit exactly that.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<JsonSchema>> SchemaCache = new();

    private static IEnumerable<Finding> EvaluateSchema(JsonNode? node, string schemaPath)
    {
        var schema = SchemaCache.GetOrAdd(Path.GetFullPath(schemaPath),
            p => new Lazy<JsonSchema>(() => JsonSchema.FromText(File.ReadAllText(p)))).Value;
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

/// <summary>What the spec checks need to know about one rule in the catalog.</summary>
/// <param name="RuleId">The rule's id.</param>
/// <param name="Disposition">keep, change or retire; null while no domain expert has decided.</param>
/// <param name="OpenQuestions">How many open_questions the rule carries.</param>
public sealed record CatalogRule(string RuleId, string? Disposition, int OpenQuestions);
