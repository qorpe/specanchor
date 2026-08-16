namespace SpecAnchor.Parity;

/// <summary>
/// The comparison contract. Naive equality is forbidden: demanding a byte-exact
/// match produces a gate that gets bypassed within two weeks. The known-differences
/// register is what makes parity honest — a silent difference is a defect, a
/// recorded one is a decision.
/// </summary>
/// <param name="ExcludedFields">Fields normalised away entirely (timestamps, sequences, generated ids).</param>
/// <param name="Tolerances">Per-field numeric tolerances.</param>
/// <param name="Rounding">Expected rounding mode per field group; informational for classification.</param>
/// <param name="KnownDifferences">Accepted, business-signed deviations.</param>
public sealed record ComparisonPolicy(
    IReadOnlyList<string> ExcludedFields,
    IReadOnlyList<Tolerance> Tolerances,
    IReadOnlyDictionary<string, string> Rounding,
    IReadOnlyList<KnownDifference> KnownDifferences);

/// <summary>A numeric tolerance for one field.</summary>
/// <param name="Field">Field the tolerance applies to.</param>
/// <param name="Type">absolute or relative.</param>
/// <param name="Value">Tolerance value; relative is a fraction of the legacy value.</param>
public sealed record Tolerance(string Field, string Type, decimal Value);

/// <summary>One accepted deviation from legacy behaviour — signed and dated.</summary>
/// <param name="Id">KD-nnnn.</param>
/// <param name="Description">What differs and why it was accepted.</param>
/// <param name="AcceptedBy">The business owner who signed the deviation.</param>
/// <param name="Date">Acceptance date (ISO).</param>
/// <param name="RuleId">The rule the deviation belongs to.</param>
/// <param name="Field">The output field the acceptance covers; required for mechanical application.</param>
public sealed record KnownDifference(
    string Id,
    string Description,
    string AcceptedBy,
    string Date,
    string RuleId,
    string? Field);

/// <summary>One record of output from one side of the parallel run.</summary>
/// <param name="RecordId">Join key across legacy and new (e.g. invoice id).</param>
/// <param name="Fields">Field name to value, values as invariant strings.</param>
public sealed record ParityRecord(string RecordId, IReadOnlyDictionary<string, string> Fields);

/// <summary>The parity report — what goes on screen in the sprint review.</summary>
/// <param name="SampleSize">Records compared.</param>
/// <param name="Passed">Records with no unaccepted mismatch.</param>
/// <param name="Failed">Records with at least one unaccepted mismatch.</param>
/// <param name="Failures">Every unaccepted mismatch, classified by cause.</param>
/// <param name="KnownDifferenceHits">Mismatches covered by the register — visible, never silent.</param>
public sealed record ParityReport(
    int SampleSize,
    int Passed,
    int Failed,
    IReadOnlyList<ParityFailure> Failures,
    IReadOnlyList<KnownDifferenceHit> KnownDifferenceHits);

/// <summary>One unaccepted mismatch.</summary>
/// <param name="RecordId">The record the mismatch occurred in.</param>
/// <param name="Field">The differing field; empty for missing records.</param>
/// <param name="Legacy">Legacy-side value.</param>
/// <param name="New">New-side value.</param>
/// <param name="Classification">rounding | value-mismatch | missing-record.</param>
public sealed record ParityFailure(
    string RecordId,
    string Field,
    string Legacy,
    string New,
    string Classification);

/// <summary>A mismatch accepted by the known-differences register.</summary>
/// <param name="RecordId">The record the deviation occurred in.</param>
/// <param name="Field">The covered field.</param>
/// <param name="KnownDifferenceId">The KD entry that covers it.</param>
public sealed record KnownDifferenceHit(string RecordId, string Field, string KnownDifferenceId);
