using System.Globalization;

namespace SpecAnchor.Parity;

/// <summary>
/// Compares legacy and new output record sets under a comparison policy. Expect a
/// large share of early failures to be decimal rounding differences between C# and
/// T-SQL, not logic: the comparator classifies them so they can be routed to the
/// known-differences register instead of being reported as defects.
/// </summary>
public static class ParityComparator
{
    /// <summary>Smallest currency unit; a numeric gap at or below it classifies as rounding.</summary>
    private const decimal RoundingThreshold = 0.01m;

    /// <summary>Runs the comparison and produces the parity report.</summary>
    /// <param name="legacy">Output records from the legacy side.</param>
    /// <param name="new">Output records from the new side.</param>
    /// <param name="policy">The comparison contract to apply.</param>
    /// <returns>The report, deterministic for identical inputs.</returns>
    public static ParityReport Compare(
        IReadOnlyList<ParityRecord> legacy,
        IReadOnlyList<ParityRecord> @new,
        ComparisonPolicy policy)
    {
        var legacyById = legacy.ToDictionary(r => r.RecordId, StringComparer.Ordinal);
        var newById = @new.ToDictionary(r => r.RecordId, StringComparer.Ordinal);
        var allIds = legacyById.Keys.Union(newById.Keys, StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var failures = new List<ParityFailure>();
        var hits = new List<KnownDifferenceHit>();
        var failedRecords = new HashSet<string>(StringComparer.Ordinal);

        foreach (var id in allIds)
        {
            var hasLegacy = legacyById.TryGetValue(id, out var legacyRecord);
            var hasNew = newById.TryGetValue(id, out var newRecord);
            if (!hasLegacy || !hasNew)
            {
                failures.Add(new ParityFailure(id, string.Empty,
                    hasLegacy ? "<present>" : "<missing>",
                    hasNew ? "<present>" : "<missing>",
                    "missing-record"));
                failedRecords.Add(id);
                continue;
            }

            var fields = legacyRecord!.Fields.Keys.Union(newRecord!.Fields.Keys, StringComparer.Ordinal)
                .Where(f => !policy.ExcludedFields.Contains(f, StringComparer.Ordinal))
                .OrderBy(f => f, StringComparer.Ordinal);

            foreach (var field in fields)
            {
                var legacyValue = legacyRecord.Fields.GetValueOrDefault(field, "<missing>");
                var newValue = newRecord.Fields.GetValueOrDefault(field, "<missing>");
                if (Matches(field, legacyValue, newValue, policy))
                {
                    continue;
                }

                var covering = policy.KnownDifferences.FirstOrDefault(kd => kd.Field == field);
                if (covering is not null)
                {
                    hits.Add(new KnownDifferenceHit(id, field, covering.Id));
                    continue;
                }

                failures.Add(new ParityFailure(id, field, legacyValue, newValue, Classify(legacyValue, newValue)));
                failedRecords.Add(id);
            }
        }

        return new ParityReport(
            SampleSize: allIds.Count,
            Passed: allIds.Count - failedRecords.Count,
            Failed: failedRecords.Count,
            Failures: failures,
            KnownDifferenceHits: hits);
    }

    private static bool Matches(string field, string legacyValue, string newValue, ComparisonPolicy policy)
    {
        if (string.Equals(legacyValue, newValue, StringComparison.Ordinal))
        {
            return true;
        }

        if (TryDecimal(legacyValue, out var a) && TryDecimal(newValue, out var b))
        {
            if (a == b)
            {
                return true;
            }

            var tolerance = policy.Tolerances.FirstOrDefault(t => t.Field == field);
            if (tolerance is not null)
            {
                var limit = tolerance.Type == "relative" ? Math.Abs(a) * tolerance.Value : tolerance.Value;
                return Math.Abs(a - b) <= limit;
            }
        }

        return false;
    }

    private static string Classify(string legacyValue, string newValue)
    {
        if (TryDecimal(legacyValue, out var a) && TryDecimal(newValue, out var b) &&
            Math.Abs(a - b) <= RoundingThreshold)
        {
            return "rounding";
        }

        return "value-mismatch";
    }

    private static bool TryDecimal(string value, out decimal result) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
}
