using System.Text;
using System.Text.Json;
using Forge.Domain.Areas;

namespace Forge.ModuleFormat.Saves;

public static class RuntimeSaveStore
{
    public const string FileMissingCode = "save.file_missing";
    public const string FileInvalidCode = "save.file_invalid";
    public const string SaveFailedCode = "save.write_failed";
    public const string ModuleMismatchCode = "save.module_mismatch";
    public const string ContentMismatchCode = "save.content_mismatch";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
    };

    public static RuntimeSaveOperationResult Save(string path, RuntimeSaveDocument save)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(save);
        var diagnostics = RuntimeSaveValidator.Validate(save);
        if (diagnostics.Count > 0)
        {
            return new RuntimeSaveOperationResult(false, null, diagnostics);
        }

        string fullPath;
        try
        {
            fullPath = ValidatePath(path);
            var directory = Path.GetDirectoryName(fullPath)!;
            Directory.CreateDirectory(directory);
            var temporary = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                var json = JsonSerializer.Serialize(save, JsonOptions);
                using (var stream = new FileStream(
                           temporary,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None,
                           4096,
                           FileOptions.WriteThrough))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }

                File.Move(temporary, fullPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }

            return new RuntimeSaveOperationResult(true, save, []);
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return Failure(SaveFailedCode, exception.Message, path);
        }
    }

    public static RuntimeSaveOperationResult Load(
        string path,
        string expectedModuleId,
        string expectedContentSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedModuleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedContentSha256);
        string fullPath;
        try
        {
            fullPath = ValidatePath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return Failure(FileInvalidCode, exception.Message, path);
        }

        if (!File.Exists(fullPath))
        {
            return Failure(FileMissingCode, "The runtime save file does not exist.", fullPath);
        }

        RuntimeSaveDocument? save;
        try
        {
            save = JsonSerializer.Deserialize<RuntimeSaveDocument>(File.ReadAllText(fullPath), JsonOptions);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return Failure(FileInvalidCode, exception.Message, fullPath);
        }

        if (save is null)
        {
            return Failure(FileInvalidCode, "The runtime save file is empty.", fullPath);
        }

        save = Migrate(save);

        var diagnostics = RuntimeSaveValidator.Validate(save).ToList();
        if (!string.Equals(save.ModuleId, expectedModuleId, StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new RuntimeSaveDiagnostic(
                ModuleMismatchCode,
                $"Save module '{save.ModuleId}' does not match loaded module '{expectedModuleId}'.",
                "moduleId"));
        }

        if (!string.Equals(save.ModuleContentSha256, expectedContentSha256, StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new RuntimeSaveDiagnostic(
                ContentMismatchCode,
                "The save was created for a different module content hash.",
                "moduleContentSha256"));
        }

        return diagnostics.Count == 0
            ? new RuntimeSaveOperationResult(true, save, [])
            : new RuntimeSaveOperationResult(false, null, diagnostics);
    }

    private static string ValidatePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!string.Equals(Path.GetExtension(fullPath), ".orpsave", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Runtime save files must use the .orpsave extension.", nameof(path));
        }

        return fullPath;
    }

    internal static RuntimeSaveDocument Migrate(RuntimeSaveDocument save)
    {
        if (string.Equals(save.FormatVersion, RuntimeSaveDocument.CurrentFormatVersion, StringComparison.Ordinal))
        {
            return save with
            {
                EntitySnapshots = save.EntitySnapshots ?? [],
                QuestStates = save.QuestStates ?? [],
                MapExploration = MigrateMapExploration(save.MapExploration),
            };
        }

        var supportedLegacyVersion =
            string.Equals(save.FormatVersion, RuntimeSaveDocument.PreMapExplorationFormatVersion,
                StringComparison.Ordinal) ||
            string.Equals(save.FormatVersion, RuntimeSaveDocument.PreQuestCollectionFormatVersion,
                StringComparison.Ordinal) ||
            string.Equals(save.FormatVersion, RuntimeSaveDocument.PreInventoryStacksFormatVersion,
                StringComparison.Ordinal) ||
            string.Equals(save.FormatVersion, RuntimeSaveDocument.PreWorldClockFormatVersion,
                StringComparison.Ordinal) ||
            string.Equals(save.FormatVersion, RuntimeSaveDocument.PreRuntimeExtensionsFormatVersion,
                StringComparison.Ordinal) ||
            string.Equals(save.FormatVersion, RuntimeSaveDocument.PreRulesFormatVersion,
                StringComparison.Ordinal);
        if (!supportedLegacyVersion)
            return save;

        var questStates = save.QuestStates ?? [];
        var trackedQuestId = save.TrackedQuestId;
        if (questStates.Count == 0 && save.QuestId is not null && save.QuestStateId is not null)
        {
            var accepted = save.Variables
                .Where(item => string.Equals(item.Key, "quest.accepted", StringComparison.Ordinal))
                .Select(item => bool.TryParse(item.Value, out var value) && value)
                .SingleOrDefault();
            questStates = [new RuntimeQuestSaveState(save.QuestId, save.QuestStateId, accepted)];
            trackedQuestId = accepted ? save.QuestId : null;
        }

        return save with
        {
            FormatVersion = RuntimeSaveDocument.CurrentFormatVersion,
            EntitySnapshots = save.EntitySnapshots ?? [],
            QuestStates = questStates,
            TrackedQuestId = trackedQuestId,
            MapExploration = MigrateMapExploration(save.MapExploration),
        };
    }

    private static RuntimeMapExplorationSave? MigrateMapExploration(RuntimeMapExplorationSave? exploration)
    {
        if (exploration is null) return null;
        var cells = exploration.RevealedCells
            .Select(key => AreaMapCell.TryParse(key, out var cell, out var legacy)
                ? legacy ? cell.LegacyKey : cell.Key
                : key)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var pins = exploration.RevealedPins
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return exploration with { RevealedCells = cells, RevealedPins = pins };
    }

    private static RuntimeSaveOperationResult Failure(string code, string message, string? path) =>
        new(false, null, [new RuntimeSaveDiagnostic(code, message, path)]);
}
