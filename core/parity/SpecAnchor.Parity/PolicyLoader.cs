using System.Text.Json.Nodes;
using YamlDotNet.RepresentationModel;
using Yaml2JsonNode;

namespace SpecAnchor.Parity;

/// <summary>
/// Loads a comparison policy from YAML or JSON. The document should validate against
/// comparison-policy.schema.v1.json; the loader is tolerant only of absent optional
/// sections, never of unknown shapes.
/// </summary>
public static class PolicyLoader
{
    /// <summary>Parses the policy document.</summary>
    /// <param name="documentText">The policy, YAML or JSON.</param>
    /// <returns>The parsed policy.</returns>
    public static ComparisonPolicy Load(string documentText)
    {
        var trimmed = documentText.TrimStart();
        JsonNode root;
        if (trimmed.StartsWith('{'))
        {
            root = JsonNode.Parse(documentText)!;
        }
        else
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(documentText));
            root = stream.Documents[0].RootNode.ToJsonNode()!;
        }

        var excluded = (root["excluded_fields"]?.AsArray() ?? [])
            .Select(n => n!.GetValue<string>())
            .ToList();

        var tolerances = (root["tolerances"]?.AsArray() ?? [])
            .Select(n => new Tolerance(
                n!["field"]!.GetValue<string>(),
                n["type"]!.GetValue<string>(),
                AsDecimal(n["value"]!)))
            .ToList();

        var rounding = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root["rounding"] is JsonObject roundingNode)
        {
            foreach (var pair in roundingNode)
            {
                rounding[pair.Key] = pair.Value!.GetValue<string>();
            }
        }

        var knownDifferences = (root["known_differences"]?.AsArray() ?? [])
            .Select(n => new KnownDifference(
                n!["id"]!.GetValue<string>(),
                n["description"]!.GetValue<string>(),
                n["accepted_by"]!.GetValue<string>(),
                n["date"]!.GetValue<string>(),
                n["rule_id"]!.GetValue<string>(),
                n["field"]?.GetValue<string>()))
            .ToList();

        return new ComparisonPolicy(excluded, tolerances, rounding, knownDifferences);
    }

    private static decimal AsDecimal(JsonNode node) =>
        node.AsValue().TryGetValue<decimal>(out var d) ? d : node.GetValue<int>();
}
