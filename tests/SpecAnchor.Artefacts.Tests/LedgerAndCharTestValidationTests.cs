using SpecAnchor.Artefacts;
using Xunit;

namespace SpecAnchor.Artefacts.Tests;

/// <summary>
/// Ledger-term and char-test halves of the self-validation contract, pinned
/// against the rig indexes. Trap D's answer (one concept, two names, one entry
/// with both aliases) must validate clean; speculation must not.
/// </summary>
public sealed class LedgerAndCharTestValidationTests : IClassFixture<ValidatorFixture>
{
    private const string ValidTerm = """
        term: Temlik
        context: Factoring.Assignment
        definition: >
          Transfer of a receivable from the supplier to the factor, taking legal
          effect when notification reaches the debtor.
        aliases_in_code: [AssignmentService, RegisterAssignment, RegisterTransfer, TemlikKayit, IhbarTarihi]
        not_to_be_confused_with: >
          Devir (Factoring.Accounting) — carrying a balance to the next period.
          Same Turkish word, different concept, different context.
        source_ref: ["FactoringApp/Assignment/AssignmentService.cs:8"]
        status: proposed
        schemaVersion: 1
        """;

    private const string ValidCharTest = """
        test_id: CHAR-0042
        rule_id: RULE-0042
        target: legacy
        method: replay
        sample_size: 240
        result: { passed: 238, failed: 2 }
        failures: >
          Both failures are the records where the minimum commission was bypassed.
          This is the behaviour described in RULE-0042.open_questions.
        tolerance: comparison-policy v1
        schemaVersion: 1
        """;

    private static readonly IReadOnlyList<string> RuleStatements =
    [
        "For a domestic recourse factoring transaction, commission is never below the contract minimum.",
        "A Temlik takes legal effect when notification reaches the debtor.",
    ];

    private readonly ValidatorFixture _fixture;

    public LedgerAndCharTestValidationTests(ValidatorFixture fixture) => _fixture = fixture;

    private string LedgerSchemaPath =>
        Path.Combine(Path.GetDirectoryName(_fixture.RuleSchemaPath)!, "ledger-term.schema.v1.json");

    private string CharTestSchemaPath =>
        Path.Combine(Path.GetDirectoryName(_fixture.RuleSchemaPath)!, "char-test.schema.v1.json");

    [Fact]
    public void Trap_D_entry_with_both_aliases_and_the_false_friend_note_passes_clean()
    {
        Assert.Empty(ArtefactValidator.ValidateLedgerTerm(
            ValidTerm, LedgerSchemaPath, _fixture.CSharp, _fixture.Sql, RuleStatements));
    }

    [Fact]
    public void An_alias_that_resolves_to_nothing_is_rejected()
    {
        var doc = ValidTerm.Replace("TemlikKayit,", "TemlikKayit, GhostService,");
        Assert.Contains(
            ArtefactValidator.ValidateLedgerTerm(doc, LedgerSchemaPath, _fixture.CSharp, _fixture.Sql, RuleStatements),
            f => f.Code == "SA0201" && f.Message.Contains("GhostService"));
    }

    [Fact]
    public void A_term_no_rule_uses_is_rejected()
    {
        var statements = new[] { "A statement that never mentions the concept." };
        Assert.Contains(
            ArtefactValidator.ValidateLedgerTerm(ValidTerm, LedgerSchemaPath, _fixture.CSharp, _fixture.Sql, statements),
            f => f.Code == "SA0202");
    }

    [Fact]
    public void A_ledger_entry_without_aliases_violates_the_schema()
    {
        var doc = ValidTerm.Replace(
            "aliases_in_code: [AssignmentService, RegisterAssignment, RegisterTransfer, TemlikKayit, IhbarTarihi]",
            "aliases_in_code: []");
        Assert.Contains(
            ArtefactValidator.ValidateLedgerTerm(doc, LedgerSchemaPath, _fixture.CSharp, _fixture.Sql, RuleStatements),
            f => f.Code == "SA0002");
    }

    [Fact]
    public void A_recorded_run_with_honest_arithmetic_passes_clean()
    {
        Assert.Empty(ArtefactValidator.ValidateCharTest(
            ValidCharTest, CharTestSchemaPath, ["RULE-0042"]));
    }

    [Fact]
    public void A_test_proving_a_nonexistent_rule_is_rejected()
    {
        Assert.Contains(
            ArtefactValidator.ValidateCharTest(ValidCharTest, CharTestSchemaPath, ["RULE-0001"]),
            f => f.Code == "SA0301");
    }

    [Fact]
    public void A_result_that_does_not_add_up_is_rejected()
    {
        var doc = ValidCharTest.Replace("{ passed: 238, failed: 2 }", "{ passed: 238, failed: 1 }");
        Assert.Contains(
            ArtefactValidator.ValidateCharTest(doc, CharTestSchemaPath, ["RULE-0042"]),
            f => f.Code == "SA0302");
    }

    [Fact]
    public void A_char_test_against_an_unknown_target_violates_the_schema()
    {
        var doc = ValidCharTest.Replace("target: legacy", "target: staging");
        Assert.Contains(
            ArtefactValidator.ValidateCharTest(doc, CharTestSchemaPath, ["RULE-0042"]),
            f => f.Code == "SA0002");
    }
}
