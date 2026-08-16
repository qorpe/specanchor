using SpecAnchor.Artefacts;

namespace SpecAnchor.Gates;

/// <summary>
/// The touch gate: when a PR changes code a rule's source_ref points at, the rule and
/// its characterization test must change in the same PR — otherwise the catalog is
/// silently rotting while the code moves on. Pure function; the CLI supplies the
/// changed-file list from `git diff --name-only`.
/// </summary>
public static class TouchGate
{
    /// <summary>Checks every rule's source files against the changed set.</summary>
    /// <param name="changedFiles">Files changed in the PR, repo-relative.</param>
    /// <param name="subjects">Rules with their test file and referenced source files.</param>
    /// <returns>SA0401 findings for silently-rotting rules; empty when clean.</returns>
    public static IReadOnlyList<GateFinding> Check(
        IReadOnlyList<string> changedFiles,
        IReadOnlyList<TouchSubject> subjects)
    {
        var findings = new List<GateFinding>();
        foreach (var subject in subjects)
        {
            var touchedSource = subject.SourceRefFiles
                .FirstOrDefault(src => changedFiles.Any(c => PathsMatch(c, src)));
            if (touchedSource is null)
            {
                continue;
            }

            var ruleChanged = changedFiles.Any(c => PathsMatch(c, subject.RuleFile));
            var testChanged = subject.TestFile is not null &&
                              changedFiles.Any(c => PathsMatch(c, subject.TestFile));
            if (!ruleChanged && !testChanged)
            {
                findings.Add(new GateFinding("touch", subject.RuleFile, new Finding(
                    "error", "SA0401", "$.source_ref",
                    $"'{touchedSource}' changed in this PR but neither the rule nor its test did — " +
                    "update the rule and its characterization test, or record why behaviour is unchanged")));
            }
        }

        return findings;
    }

    private static bool PathsMatch(string a, string b)
    {
        var na = a.Replace('\\', '/');
        var nb = b.Replace('\\', '/');
        return na.EndsWith(nb, StringComparison.Ordinal) || nb.EndsWith(na, StringComparison.Ordinal);
    }
}

/// <summary>One rule as seen by the touch gate.</summary>
/// <param name="RuleFile">The rule card's file, relative to the discovery root.</param>
/// <param name="TestFile">The characterization test's file, when one exists.</param>
/// <param name="SourceRefFiles">Source files the rule's source_ref entries point at.</param>
public sealed record TouchSubject(
    string RuleFile,
    string? TestFile,
    IReadOnlyList<string> SourceRefFiles);
