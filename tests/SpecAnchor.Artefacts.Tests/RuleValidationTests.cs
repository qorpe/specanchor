using SpecAnchor.Artefacts;
using SpecAnchor.Index.CSharp;
using SpecAnchor.Index.Sql;
using Xunit;

namespace SpecAnchor.Artefacts.Tests;

/// <summary>
/// The self-validation contract: a rule without a resolvable source reference is
/// rejected, not flagged. These tests pin that behaviour against the rig indexes.
/// </summary>
public sealed class ValidatorFixture
{
    public ValidatorFixture()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "rig")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        var repoRoot = dir ?? throw new InvalidOperationException("Repository root with rig/ not found.");
        RuleSchemaPath = Path.Combine(repoRoot, "core", "schemas", "rule.schema.v1.json");
        CSharp = CSharpIndexer.IndexDirectory(Path.Combine(repoRoot, "rig", "legacy-factoring", "src"));
        Sql = SqlIndexer.IndexDirectory(Path.Combine(repoRoot, "rig", "legacy-factoring", "sql"));
    }

    public string RuleSchemaPath { get; }

    public CSharpIndex CSharp { get; }

    public SqlIndex Sql { get; }

    public static readonly IReadOnlyList<string> Aliases =
        ["CommissionCalculator", "contractType", "MinCommission", "TemlikKayit"];
}

public sealed class RuleValidationTests : IClassFixture<ValidatorFixture>
{
    private const string ValidRule = """
        rule_id: RULE-0042
        version: 1
        schemaVersion: 1
        context: Factoring.Pricing
        statement: >
          For a domestic recourse factoring transaction, commission is the invoice
          amount multiplied by the contract rate and never below the contract minimum.
        source_ref:
          - file: FactoringApp/Pricing/CommissionCalculator.cs
            line_start: 5
            line_end: 15
          - object: dbo.usp_CalculateCommission
            kind: procedure
        confidence: inferred
        open_questions:
          - The minimum is bypassed for one contract type. No documentation explains why.
        disposition: null
        """;

    private readonly ValidatorFixture _fixture;

    public RuleValidationTests(ValidatorFixture fixture) => _fixture = fixture;

    private IReadOnlyList<Finding> Validate(string document) =>
        ArtefactValidator.ValidateRule(document, _fixture.RuleSchemaPath,
            _fixture.CSharp, _fixture.Sql, ValidatorFixture.Aliases);

    [Fact]
    public void A_well_formed_rule_with_resolvable_refs_passes_clean()
    {
        Assert.Empty(Validate(ValidRule));
    }

    [Fact]
    public void A_rule_pointing_at_an_unknown_file_is_rejected()
    {
        var doc = ValidRule.Replace("FactoringApp/Pricing/CommissionCalculator.cs",
            "FactoringApp/Pricing/DoesNotExist.cs");
        Assert.Contains(Validate(doc), f => f.Code == "SA0101");
    }

    [Fact]
    public void A_rule_whose_lines_overlap_no_symbol_is_rejected()
    {
        var doc = ValidRule.Replace("line_start: 5", "line_start: 900")
            .Replace("line_end: 15", "line_end: 950");
        Assert.Contains(Validate(doc), f => f.Code == "SA0102");
    }

    [Fact]
    public void A_rule_naming_an_unknown_procedure_is_rejected()
    {
        var doc = ValidRule.Replace("dbo.usp_CalculateCommission", "dbo.usp_Ghost");
        Assert.Contains(Validate(doc), f => f.Code == "SA0104");
    }

    [Fact]
    public void A_statement_containing_a_code_identifier_fails_statement_quality()
    {
        var doc = ValidRule.Replace(
            "never below the contract minimum.",
            "never below MinCommission when contractType is not three.");
        var findings = Validate(doc);
        Assert.Contains(findings, f => f.Code == "SA0103" && f.Message.Contains("MinCommission"));
        Assert.Contains(findings, f => f.Code == "SA0103" && f.Message.Contains("contractType"));
    }

    [Fact]
    public void Evidenced_without_a_named_test_violates_the_schema()
    {
        var doc = ValidRule.Replace("confidence: inferred", "confidence: evidenced");
        Assert.Contains(Validate(doc), f => f.Code == "SA0002");
    }

    [Fact]
    public void A_rule_without_any_source_ref_violates_the_schema()
    {
        var doc = """
            rule_id: RULE-0099
            version: 1
            schemaVersion: 1
            context: Factoring.Pricing
            statement: Invented rules do not enter the catalog.
            source_ref: []
            confidence: inferred
            """;
        Assert.Contains(Validate(doc), f => f.Code == "SA0002");
    }

    [Fact]
    public void Json_input_is_accepted_as_well_as_yaml()
    {
        var doc = """
            {
              "rule_id": "RULE-0043", "version": 1, "schemaVersion": 1,
              "context": "Factoring.Assignment",
              "statement": "An assignment takes legal effect when notification reaches the debtor.",
              "source_ref": [ { "file": "FactoringApp/Assignment/AssignmentService.cs", "line_start": 3, "line_end": 14 } ],
              "confidence": "inferred"
            }
            """;
        Assert.Empty(Validate(doc));
    }
}
