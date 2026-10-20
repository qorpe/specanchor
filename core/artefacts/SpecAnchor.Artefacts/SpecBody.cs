using System.Text.RegularExpressions;

namespace SpecAnchor.Artefacts;

/// <summary>
/// The two conventions a spec body carries, read the same way by the validator and the
/// gate: an unresolved question is an <c>[OPEN</c> bracket, and a clause a test can cite
/// is declared by a <c>[Cn]</c> marker — on an EARS line, before a Gherkin scenario, or in
/// a DMN table row. Plain text matching, no interpretation.
/// </summary>
public static partial class SpecBody
{
    /// <summary>Counts the unresolved [OPEN — ...] brackets in a spec body.</summary>
    /// <param name="body">The spec body.</param>
    /// <returns>The number of occurrences of "[OPEN".</returns>
    public static int CountOpenBrackets(string body)
    {
        var count = 0;
        for (var at = body.IndexOf("[OPEN", StringComparison.Ordinal);
             at >= 0;
             at = body.IndexOf("[OPEN", at + 1, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    /// <summary>The clause ids a body declares, in order of appearance, duplicates kept.</summary>
    /// <param name="body">The spec body.</param>
    /// <returns>Clause ids such as "C1".</returns>
    public static IReadOnlyList<string> Clauses(string body) =>
        ClauseMarker().Matches(body).Select(m => m.Groups[1].Value).ToList();

    [GeneratedRegex(@"\[(C[0-9]+)\]")]
    private static partial Regex ClauseMarker();
}
