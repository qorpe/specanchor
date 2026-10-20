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
/// terms/*.yaml|yml|json, tests/*.yaml|yml|json, specs/*.yaml|yml|json — one artefact per file.
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
        var specs = LoadArtefacts(input.DiscoveryRoot, "specs", findings);

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
        var specSchema = Path.Combine(input.SchemasDirectory, "spec.schema.v1.json");

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

        var readiness = CheckSpecs(rules, tests, specs, specSchema, findings);

        if (input.ChangedFiles is not null)
        {
            findings.AddRange(TouchGate.Check(input.ChangedFiles, TouchSubjects(rules, tests)));
        }

        return new GateReport(findings
            .OrderBy(f => f.File, StringComparer.Ordinal)
            .ThenBy(f => f.Finding.Code, StringComparer.Ordinal)
            .ThenBy(f => f.Finding.Path, StringComparer.Ordinal)
            .ToList())
        {
            Readiness = readiness,
        };
    }

    /// <summary>
    /// The spec gate: rule → spec → test. Each spec is validated against the catalog, every
    /// clause it declares must be cited by a test, and every clause a test cites must be
    /// declared by a spec. Specs that pass are then measured against Definition of Ready.
    /// </summary>
    private static List<SpecReadiness> CheckSpecs(
        List<(string File, string Text, JsonNode Node)> rules,
        List<(string File, string Text, JsonNode Node)> tests,
        List<(string File, string Text, JsonNode Node)> specs,
        string specSchema,
        List<GateFinding> findings)
    {
        var catalogRules = rules
            .Select(r => new CatalogRule(
                Text(r.Node["rule_id"]) ?? string.Empty,
                Text(r.Node["disposition"]),
                r.Node["open_questions"] is JsonArray questions ? questions.Count : 0))
            .ToList();

        var cited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var test in tests)
        {
            foreach (var clause in Clauses(test.Node))
            {
                cited.Add(clause);
            }
        }

        var declared = new HashSet<string>(StringComparer.Ordinal);
        var valid = new List<(string File, JsonNode Node)>();
        foreach (var spec in specs)
        {
            var specFindings = ArtefactValidator.ValidateSpec(spec.Text, specSchema, catalogRules);
            findings.AddRange(specFindings.Select(f => new GateFinding("spec", spec.File, f)));

            // Read tolerantly: a spec that failed its schema is already rejected above and
            // must not crash the run that reports it.
            var specId = Text(spec.Node["spec_id"]) ?? string.Empty;
            var body = Text(spec.Node["body"]) ?? string.Empty;
            var uncited = false;
            foreach (var clause in SpecBody.Clauses(body).Distinct(StringComparer.Ordinal))
            {
                declared.Add($"{specId}.{clause}");
                if (!cited.Contains($"{specId}.{clause}"))
                {
                    uncited = true;
                    findings.Add(new GateFinding("spec", spec.File, new Finding(
                        "error", "SA0506", $"$.body[{clause}]",
                        $"clause {specId}.{clause} is cited by no test — a clause nothing proves is a " +
                        "claim, not a specification; cite it from the test that proves it (spec_clauses)")));
                }
            }

            if (specFindings.Count == 0 && !uncited)
            {
                valid.Add((spec.File, spec.Node));
            }
        }

        foreach (var test in tests)
        {
            var clauses = Clauses(test.Node);
            for (var i = 0; i < clauses.Count; i++)
            {
                var clause = clauses[i];
                if (!declared.Contains(clause))
                {
                    findings.Add(new GateFinding("char-test", test.File, new Finding(
                        "error", "SA0303", $"$.spec_clauses[{i}]",
                        $"clause '{clause}' is declared by no spec in the catalog — the citation does not resolve")));
                }
            }
        }

        return valid.Select(s => Readiness(s.File, s.Node, catalogRules)).ToList();
    }

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static List<string> Clauses(JsonNode test) =>
        test["spec_clauses"] is JsonArray clauses
            ? clauses.Select(Text).Where(c => c is not null).Select(c => c!).ToList()
            : [];

    /// <summary>Definition of Ready (team-operating-agreement §2), for a spec that passed every gate.</summary>
    private static SpecReadiness Readiness(string file, JsonNode spec, IReadOnlyList<CatalogRule> catalogRules)
    {
        var reasons = new List<string>();
        // A spec that reached here passed SA0502, so its declared count is the body's count.
        var openBrackets = SpecBody.CountOpenBrackets(spec["body"]!.GetValue<string>());
        if (openBrackets > 0)
        {
            reasons.Add($"{openBrackets} open bracket(s) in the body");
        }

        if (string.IsNullOrWhiteSpace(spec["approved_by"]?.GetValue<string>()))
        {
            reasons.Add("no approved_by — the domain expert has not approved it");
        }

        foreach (var ruleId in spec["rule_ids"]!.AsArray().Select(r => r!.GetValue<string>()))
        {
            if (catalogRules.First(r => r.RuleId == ruleId).Disposition is null)
            {
                reasons.Add($"{ruleId} has no disposition");
            }
        }

        return new SpecReadiness(file, spec["spec_id"]!.GetValue<string>(), reasons.Count == 0, reasons);
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
/// <param name="DiscoveryRoot">The discovery folder holding rules/, terms/, tests/ and specs/.</param>
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
    /// <summary>
    /// Definition of Ready, per spec that passed every gate. Reported beside the findings,
    /// never among them: a spec that is not ready yet is a correct artefact in a state,
    /// not a defect, and does not change the exit code.
    /// </summary>
    public IReadOnlyList<SpecReadiness> Readiness { get; init; } = [];

    /// <summary>True when no gate produced a finding.</summary>
    public bool IsClean => Findings.Count == 0;

    /// <summary>Exit-code contract: 0 clean, 1 findings (2 is reserved for usage errors).</summary>
    public int ExitCode => IsClean ? 0 : 1;
}

/// <summary>One finding attributed to a gate and a file.</summary>
/// <param name="Gate">source-ref, statement-quality, schema, ledger, char-test, evidence, spec or touch.</param>
/// <param name="File">The artefact file, relative to the discovery root.</param>
/// <param name="Finding">The underlying validator finding.</param>
public sealed record GateFinding(string Gate, string File, Finding Finding);

/// <summary>Whether one spec meets the Definition of Ready (team-operating-agreement §2).</summary>
/// <param name="File">The spec file, relative to the discovery root.</param>
/// <param name="SpecId">The spec's id.</param>
/// <param name="Ready">True when nothing below stands between the spec and a sprint.</param>
/// <param name="Reasons">Each unmet Definition of Ready condition; empty when ready.</param>
public sealed record SpecReadiness(string File, string SpecId, bool Ready, IReadOnlyList<string> Reasons);
