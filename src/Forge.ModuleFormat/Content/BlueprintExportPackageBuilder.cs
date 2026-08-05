using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Content;

public static class BlueprintExportPackageBuilder
{
    public const int CurrentSchemaVersion = 1;
    private static readonly DateTimeOffset DeterministicTimestamp = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static BlueprintExportResult Build(
        ProjectDefinition project,
        IReadOnlyList<BlueprintId> selectedBlueprintIds,
        string outputPath,
        bool includePreviews = true)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(selectedBlueprintIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var selected = project.Blueprints
            .Where(blueprint => selectedBlueprintIds.Contains(blueprint.Id))
            .OrderBy(blueprint => blueprint.Id.Value)
            .ToArray();
        if (selected.Length == 0 || selected.Length != selectedBlueprintIds.Distinct().Count())
        {
            throw new ArgumentException("Every selected blueprint ID must exist exactly once in the project.", nameof(selectedBlueprintIds));
        }

        var dependencies = selected
            .Where(blueprint => blueprint.PaletteOrigin == BlueprintPaletteOrigin.InstalledPack)
            .Select(blueprint => blueprint.Provenance.Source)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var licenses = selected.Select(blueprint => blueprint.Provenance.LicenseSpdx)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var previews = includePreviews
            ? selected.Select(blueprint => $"previews/{blueprint.Slug}.txt").ToArray()
            : [];
        var manifest = new BlueprintExportManifest(
            CurrentSchemaVersion,
            "openrpg-forge-blueprint-pack",
            selected.Select(blueprint => blueprint.Id.ToString()).ToArray(),
            dependencies,
            licenses,
            ["blueprint-schema-v1"],
            previews);

        var fullOutput = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutput)!);
        var temporary = fullOutput + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false, Encoding.UTF8))
            {
                AddJson(archive, "manifest.json", manifest);
                foreach (var blueprint in selected)
                {
                    AddJson(archive, $"blueprints/{blueprint.Id}.json", blueprint);
                    if (includePreviews)
                    {
                        AddText(
                            archive,
                            $"previews/{blueprint.Slug}.txt",
                            $"{blueprint.DisplayName}\nKind: {blueprint.Kind}\nSource: {blueprint.Provenance.Source}\n");
                    }
                }

                AddJson(archive, "licenses/index.json", licenses);
                AddJson(archive, "migrations/blueprint-schema-v1.json", new { schemaVersion = 1, strategy = "identity" });
            }

            File.Move(temporary, fullOutput, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }

        var info = new FileInfo(fullOutput);
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fullOutput))).ToLowerInvariant();
        return new BlueprintExportResult(fullOutput, hash, info.Length, selected.Length, dependencies.Length, previews.Length);
    }

    private static void AddJson<T>(ZipArchive archive, string path, T value) =>
        AddText(archive, path, JsonSerializer.Serialize(value, ProjectJsonSerializer.CreateOptions()));

    private static void AddText(ZipArchive archive, string path, string value)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        entry.LastWriteTime = DeterministicTimestamp;
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(value.Replace("\r\n", "\n", StringComparison.Ordinal));
    }
}
