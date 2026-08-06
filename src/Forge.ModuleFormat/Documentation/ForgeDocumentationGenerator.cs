using System.Text;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Saves;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Scripting;

namespace Forge.ModuleFormat.Documentation;

public sealed record GeneratedDocumentationFile(string RelativePath, string Content);
public sealed record DocumentationGenerationResult(bool Current, IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<GeneratedDocumentationFile> Files);

public static class ForgeDocumentationGenerator
{
    private static readonly IReadOnlyList<string> CliCommands =
    [
        "assets list|install-pack|uninstall-pack|report|delivery-report|credits|prepare|roundtrip|finalize-roundtrip",
        "content batch|export-blueprints|merge-table",
        "docs generate|check", "package export|validate|load|analyze|compliance|keygen|sign|verify-signature",
        "package delta|apply-delta|bundle|spatial|diagnose", "workspace migrate", "author-original",
        "author-forgotten-forest",
        "readiness validate|health|crash|recovery|sync-plan", "quality run", "performance run", "governance run",
    ];

    public static DocumentationGenerationResult Generate(string repositoryRoot, string outputDirectory, bool checkOnly)
    {
        var root = Path.GetFullPath(repositoryRoot);
        var output = ResolveOutput(root, outputDirectory);
        var rows = ReadCoverage(Path.Combine(root, "tracking", "project", "AURORA_PARITY_COVERAGE.csv"));
        var files = Build(rows);
        var changed = files.Where(file =>
        {
            var path = Path.Combine(output, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            return !File.Exists(path) || !string.Equals(Normalize(File.ReadAllText(path)), file.Content, StringComparison.Ordinal);
        }).Select(file => file.RelativePath).ToArray();
        if (!checkOnly)
        {
            Directory.CreateDirectory(output);
            foreach (var file in files)
            {
                var path = Path.Combine(output, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, file.Content, new UTF8Encoding(false));
            }
        }
        return new DocumentationGenerationResult(changed.Length == 0, changed, files);
    }

    private static IReadOnlyList<GeneratedDocumentationFile> Build(IReadOnlyList<CoverageRow> rows)
    {
        var api = ForgeScriptQualityService.GenerateDocumentation(ForgeScriptApiCatalog.Registrations, "1.0");
        return
        [
            GeneratedFile("feature-catalog.md", FeatureCatalog(rows)),
            GeneratedFile("forgescript-api.md", api.Markdown + "\n## Ressources MCP\n\n" +
                string.Join('\n', api.McpResources.Select(item => $"- `{item}`")) + "\n"),
            GeneratedFile("format-schemas.md", Schemas()),
            GeneratedFile("cli-reference.md", "# Référence CLI OpenRPG Forge\n\n" +
                string.Join("\n", CliCommands.Select(item => $"- `forge {item}`")) + "\n"),
            GeneratedFile("package-format.md", PackageFormat()),
            GeneratedFile("coverage.md", Coverage(rows)),
        ];
    }

    private static string FeatureCatalog(IReadOnlyList<CoverageRow> rows)
    {
        var lines = new List<string> { "# Catalogue de fonctionnalités OpenRPG Forge", "", "Fichier généré. Source : `tracking/project/AURORA_PARITY_COVERAGE.csv`.", "", "| ID | Fonctionnalité | Priorité | État | Gate |", "|---|---|---:|---|---|" };
        lines.AddRange(rows.OrderBy(row => row.Id, StringComparer.Ordinal).Select(row =>
            $"| {row.Id} | {Escape(row.Name)} | {row.Priority} | {row.Status} | {row.Gate} |"));
        return string.Join('\n', lines) + "\n";
    }

    private static string Coverage(IReadOnlyList<CoverageRow> rows)
    {
        var lines = new List<string> { "# Couverture Aurora", "", "Fichier généré depuis le tracker canonique.", "", $"Total : **{rows.Count}** exigences.", "", "| État | Nombre |", "|---|---:|" };
        lines.AddRange(rows.GroupBy(row => row.Status).OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"| {group.Key} | {group.Count()} |"));
        lines.AddRange(["", "| Gate | Nombre |", "|---|---:|"]);
        lines.AddRange(rows.GroupBy(row => row.Gate).OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"| {group.Key} | {group.Count()} |"));
        return string.Join('\n', lines) + "\n";
    }

    private static string Schemas() => $"""
        # Formats canoniques

        Fichier généré depuis les constantes de sérialisation.

        | Document | Version courante |
        |---|---|
        | Projet agrégé | {ProjectJsonSerializer.CurrentFormatVersion} |
        | Workspace scindé | {ProjectSession.CurrentProjectFormatVersion} |
        | Zone | {AreaJsonSerializer.CurrentFormatVersion} |
        | Sauvegarde runtime | {RuntimeSaveDocument.CurrentFormatVersion} |
        | Manifeste `.orpmod` | {ModuleManifest.CurrentFormatVersion} |

        Les migrations acceptées sont testées par `Forge.ModuleFormat.Tests`; toute écriture produit la version courante.
        """.Replace("\r\n", "\n") + "\n";

    private static string PackageFormat() => $"""
        # Paquets OpenRPG Forge

        Fichier généré depuis le contrat `ModuleManifest`.

        - Format : `{ModuleManifest.CurrentFormatVersion}`
        - Extension : `.orpmod`
        - Identité : UUID stable, slug, version et manifeste canonique
        - Intégrité : SHA-256 par entrée et validation avant chargement
        - Gouvernance : licences, provenance, dépendances et profils de distribution
        - Sécurité : chemins relatifs normalisés, quotas et refus des traversées
        """.Replace("\r\n", "\n") + "\n";

    private static IReadOnlyList<CoverageRow> ReadCoverage(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Aurora coverage tracker is missing.", path);
        var lines = File.ReadAllLines(path, Encoding.UTF8);
        return lines.Skip(1).Where(line => !string.IsNullOrWhiteSpace(line)).Select(line =>
        {
            var fields = ParseCsv(line);
            if (fields.Count < 12) throw new FormatException("Aurora coverage row is incomplete.");
            return new CoverageRow(fields[0], fields[2], fields[3], fields[5], fields[10]);
        }).ToArray();
    }

    private static IReadOnlyList<string> ParseCsv(string line)
    {
        var fields = new List<string>(); var value = new StringBuilder(); var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"') { value.Append('"'); index++; }
                else quoted = !quoted;
            }
            else if (character == ';' && !quoted) { fields.Add(value.ToString()); value.Clear(); }
            else value.Append(character);
        }
        fields.Add(value.ToString());
        if (quoted) throw new FormatException("Aurora coverage CSV contains an unterminated quoted field.");
        return fields;
    }

    private static string ResolveOutput(string root, string relative)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relative);
        var output = Path.GetFullPath(Path.Combine(root, relative));
        if (!output.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Documentation output must remain inside the repository.", nameof(relative));
        return output;
    }

    private static GeneratedDocumentationFile GeneratedFile(string path, string content) => new(path, Normalize(content));
    private static string Normalize(string content) => content.Replace("\r\n", "\n").TrimEnd() + "\n";
    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);
    private sealed record CoverageRow(string Id, string Name, string Priority, string Status, string Gate);
}
