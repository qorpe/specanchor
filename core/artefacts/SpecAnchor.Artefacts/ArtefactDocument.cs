using System.Text.Json.Nodes;
using YamlDotNet.RepresentationModel;
using Yaml2JsonNode;

namespace SpecAnchor.Artefacts;

/// <summary>
/// Parses artefact documents. YAML and JSON are both accepted; the parsed form is
/// always a JSON node so every downstream check works on one shape.
/// </summary>
public static class ArtefactDocument
{
    /// <summary>Parses YAML or JSON text into a JSON node.</summary>
    /// <param name="text">The document text.</param>
    /// <param name="node">The parsed node on success.</param>
    /// <param name="error">The parse error on failure.</param>
    /// <returns>True when the document parsed.</returns>
    public static bool TryParse(string text, out JsonNode? node, out string? error)
    {
        node = null;
        error = null;
        try
        {
            var trimmed = text.TrimStart();
            if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
            {
                node = JsonNode.Parse(text);
                return node is not null;
            }

            var stream = new YamlStream();
            stream.Load(new StringReader(text));
            node = stream.Documents[0].RootNode.ToJsonNode();
            return node is not null;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
