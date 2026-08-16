using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpecAnchor.Index.CSharp;

/// <summary>
/// Serializes the index artefact. Output is deterministic because every list in
/// <see cref="CSharpIndex"/> is sorted at build time; serializing the same index
/// twice produces byte-identical JSON.
/// </summary>
public static class IndexSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>Serializes the index to indented, camelCase JSON.</summary>
    /// <param name="index">The index to serialize.</param>
    /// <returns>The JSON document as a string.</returns>
    public static string ToJson(CSharpIndex index) => JsonSerializer.Serialize(index, Options);
}
