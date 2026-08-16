using System.Text.Json.Nodes;
using SpecAnchor.Artefacts;
using SpecAnchor.Index.CSharp;
using SpecAnchor.Index.Sql;

namespace SpecAnchor.Gates;

/// <summary>
/// Runs the catalog gates over a discovery folder. A gate first seen red in CI breeds
/// resentment; the same gate seen locally in two seconds becomes habit — this runner is
/// what `specanchor gate` executes, and CI runs exactly the same code, so local and CI
/// can never disagree.
/// Layout expected under the discovery root, at any depth: rules/*.yaml|yml|json,
/// terms/*.yaml|yml|json, tests/*.yaml|yml|json — one artefact per file.
/// </summary>
public static class GateRunner
{
    /// <summary>Runs all gates and returns the aggregated report.</summary>
    /// <param name="input">Paths, indexes and (optionally) the changed-file set for the touch gate.</param>
    /// <returns>The gate report; exit code 0 when clean, 1 when any gate is red.</returns>
    public static GateReport Run(GateInput input)
    {
        var findings = new List<GateFinding>();
        var rules = LoadArtefacts(input.DiscoveryRoot, "rules", findings);
        var terms = LoadArtefacts(input.DiscoveryRoot, "terms", findings);
        var tests = LoadArtefacts(input.DiscoveryRoot, "tests", findings);

        var aliases = terms
            .SelectMany(t => t.Node["aliases_in_code"]?.AsArray() ?? [])
            .Select(n => n!.GetValue<string>())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var statements = rules
            .Select(r => r.Node["statement"]?.GetValue<string>() ?? string.Empty)
            .ToList();
        var ruleIds = rules
            .Select(r => r.Node["rule_id"]?.GetValue<string>() ?? string.Empty)
            .ToList();
        var testIds = tests
            .Select(t => t.Node["test_id"]?.GetValue<string>() ?? string.Empty)
            .ToHashSet(StringComparer.Ordinal);

        var ruleSchema = Path.Combine(input.SchemasDirectory, "rule.schema.v1.json");
        var ledgerSchema = Path.Combine(input.SchemasDirectory, "ledger-term.schema.v1.json");
        var charTestSchema = Path.Combine(input.SchemasDirectory, "char-test.schema.v1.json");

        foreach (var rule in rules)
        {
            foreach (var finding in ArtefactValidator.ValidateRule(
                rule.Text, ruleSchema, input.CSharpIndex, input.SqlIndex, aliases))
            {
                findings.Add(new GateFinding(GateFor(finding.Code), rule.File, finding));
            }

            if (rule.Node["confidence"]?.GetValue<string>() == "evidenced")
            {
                var evidence = rule.Node["evidence"]?.GetValue<string>() ?? string.Empty;
                if (!testIds.Contains(evidence))
                {
                    findings.Add(new GateFinding("evidence", rule.File, new Finding(
                        "error", "SA0105", "$.evidence",
                        $"evidenced rule cites '{evidence}' but no such test record exists — " +
                        "evidence must name a test that actually ran")));
                }
            }
        }

        foreach (var term in terms)
        {
            foreach (var finding in ArtefactValidator.ValidateLedgerTerm(
                term.Text, ledgerSchema, input.CSharpIndex, input.SqlIndex, statements))
            {
                findings.Add(new GateFinding("ledger", term.File, finding));
            }
        }

        foreach (var test in tests)
        {
            foreach (var finding in ArtefactValidator.ValidateCharTest(test.Text, charTestSchema, ruleIds))
            {
                findings.Add(new GateFinding("char-test", test.File, finding));
            }
        }

        if (input.ChangedFiles is not null)
        {
            findings.AddRange(TouchGate.Check(input.ChangedFiles, TouchSubjects(rules, tests)));
        }

        return new GateReport(findings
            .OrderBy(f => f.File, StringComparer.Ordinal)
            .ThenBy(f => f.Finding.Code, StringComparer.Ordinal)
            .ToList());
    }

    private static string GateFor(string code) => code switch
    {
        "SA0103" => "statement-quality",
        "SA0101" or "SA0102" or "SA0104" => "source-ref",
        _ => "schema",
    };

    private static List<(string File, string Text, JsonNode Node)> LoadArtefacts(
        string discoveryRoot, string folderName, List<GateFinding> findings)
    {
        var results = new List<(string, string, JsonNode)>();
        if (!Directory.Exists(discoveryRoot))
        {
            return results;
        }

        var files = Directory.EnumerateFiles(discoveryRoot, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".yaml", StringComparison.Ordinal) ||
                         f.EndsWith(".yml", StringComparison.Ordinal) ||
                         f.EndsWith(".json", StringComparison.Ordinal)) &&
                        Path.GetFileName(Path.GetDirectoryName(f)!) == folderName)
            .OrderBy(f => f, StringComparer.Ordinal);

        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(discoveryRoot, file);
            var text = File.ReadAllText(file);
            if (ArtefactDocument.TryParse(text, out var node, out var error) && node is not null)
            {
                results.Add((relative, text, node));
            }
            else
            {
                findings.Add(new GateFinding("schema", relative,
                    new Finding("error", "SA0001", "$", $"document does not parse: {error}")));
            }
        }

        return results;
    }

    private static List<TouchSubject> TouchSubjects(
        List<(string File, string Text, JsonNode Node)> rules,
        List<(string File, string Text, JsonNode Node)> tests)
    {
        var testByRule = tests
            .Where(t => t.Node["rule_id"] is not null)
            .GroupBy(t => t.Node["rule_id"]!.GetValue<string>(), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().File, StringComparer.Ordinal);

        var subjects = new List<TouchSubject>();
        foreach (var rule in rules)
        {
            var sourceFiles = (rule.Node["source_ref"]?.AsArray() ?? [])
                .Select(r => r!["file"]?.GetValue<string>())
                .Where(f => f is not null)
                .Select(f => f!)
                .ToList();
            var ruleId = rule.Node["rule_id"]?.GetValue<string>() ?? string.Empty;
            subjects.Add(new TouchSubject(
                rule.File,
                testByRule.GetValueOrDefault(ruleId),
                sourceFiles));
        }

        return subjects;
    }
}

/// <summary>Input to a gate run.</summary>
/// <param name="DiscoveryRoot">The discovery folder holding rules/, terms/ and tests/.</param>
/// <param name="SchemasDirectory">Directory containing the artefact schemas.</param>
/// <param name="CSharpIndex">The C# index of the system under discovery.</param>
/// <param name="SqlIndex">The SQL index of the system under discovery.</param>
/// <param name="ChangedFiles">PR-changed files; enables the touch gate when present.</param>
public sealed record GateInput(
    string DiscoveryRoot,
    string SchemasDirectory,
    CSharpIndex CSharpIndex,
    SqlIndex SqlIndex,
    IReadOnlyList<string>? ChangedFiles = null);

/// <summary>The aggregated result of a gate run.</summary>
/// <param name="Findings">All findings across all gates, sorted.</param>
public sealed record GateReport(IReadOnlyList<GateFinding> Findings)
{
    /// <summary>True when no gate produced a finding.</summary>
    public bool IsClean => Findings.Count == 0;

    /// <summary>Exit-code contract: 0 clean, 1 findings (2 is reserved for usage errors).</summary>
    public int ExitCode => IsClean ? 0 : 1;
}

/// <summary>One finding attributed to a gate and a file.</summary>
/// <param name="Gate">source-ref, statement-quality, schema, ledger, char-test, evidence or touch.</param>
/// <param name="File">The artefact file, relative to the discovery root.</param>
/// <param name="Finding">The underlying validator finding.</param>
public sealed record GateFinding(string Gate, string File, Finding Finding);
