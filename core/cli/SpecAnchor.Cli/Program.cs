using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using SpecAnchor.Gates;
using SpecAnchor.Index.CSharp;
using SpecAnchor.Index.Matrix;
using SpecAnchor.Index.Sql;

const string Usage = """
    specanchor — spec-anchored legacy modernization toolchain

    usage:
      specanchor index --src <dir> --sql <dir> --out <dir>
          build the deterministic indexes and the table access matrix
      specanchor gate  --discovery <dir> --src <dir> --sql <dir> --schemas <dir> [--changed <file>]
          run all catalog gates locally; --changed (newline-separated file list)
          enables the touch gate
      specanchor mcp
          serve the index-query and gate tools over stdio (Model Context Protocol)

    exit codes: 0 clean · 1 findings · 2 usage or I/O error
    """;

var options = ParseOptions(args.Skip(1).ToArray());

try
{
    switch (args.FirstOrDefault())
    {
        case "index":
        {
            if (!options.TryGetValue("--src", out var src) ||
                !options.TryGetValue("--sql", out var sql) ||
                !options.TryGetValue("--out", out var outDir))
            {
                return Fail();
            }

            var csharpIndex = CSharpIndexer.IndexDirectory(src);
            var sqlIndex = SqlIndexer.IndexDirectory(sql);
            var matrix = TableAccessMatrixBuilder.Build(csharpIndex, sqlIndex);

            Directory.CreateDirectory(outDir);
            var json = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(Path.Combine(outDir, "csharp-index.json"), IndexSerializer.ToJson(csharpIndex));
            File.WriteAllText(Path.Combine(outDir, "sql-index.json"), JsonSerializer.Serialize(sqlIndex, json));
            File.WriteAllText(Path.Combine(outDir, "matrix.json"), JsonSerializer.Serialize(matrix, json));
            Console.WriteLine($"specanchor: index written to {outDir} " +
                $"({csharpIndex.Types.Count} types, {sqlIndex.Procedures.Count} procedures, " +
                $"{matrix.Entries.Count} matrix rows, " +
                $"coverage {matrix.Coverage.CallSitesResolved}/{matrix.Coverage.CallSitesTotal})");
            return 0;
        }

        case "gate":
        {
            if (!options.TryGetValue("--discovery", out var discovery) ||
                !options.TryGetValue("--src", out var src) ||
                !options.TryGetValue("--sql", out var sql) ||
                !options.TryGetValue("--schemas", out var schemas))
            {
                return Fail();
            }

            IReadOnlyList<string>? changed = null;
            if (options.TryGetValue("--changed", out var changedFile))
            {
                changed = File.ReadAllLines(changedFile)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();
            }

            var report = GateRunner.Run(new GateInput(
                discovery, schemas,
                CSharpIndexer.IndexDirectory(src),
                SqlIndexer.IndexDirectory(sql),
                changed));

            foreach (var finding in report.Findings)
            {
                Console.WriteLine(
                    $"{finding.Finding.Severity.ToUpperInvariant()}  {finding.Gate}  " +
                    $"{finding.File} at {finding.Finding.Path}: {finding.Finding.Message}");
            }

            Console.WriteLine(report.IsClean
                ? "specanchor gate: clean — all gates green"
                : $"specanchor gate: {report.Findings.Count} finding(s)");
            return report.ExitCode;
        }

        case "mcp":
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Logging.ClearProviders();
            builder.Services.AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly();
            await builder.Build().RunAsync();
            return 0;
        }

        default:
            return Fail();
    }
}
catch (IOException ex)
{
    Console.Error.WriteLine($"specanchor: {ex.Message}");
    return 2;
}

static int Fail()
{
    Console.Error.WriteLine(Usage);
    return 2;
}

static Dictionary<string, string> ParseOptions(string[] rest)
{
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 0; i + 1 < rest.Length; i += 2)
    {
        if (rest[i].StartsWith("--", StringComparison.Ordinal))
        {
            options[rest[i]] = rest[i + 1];
        }
    }

    return options;
}
