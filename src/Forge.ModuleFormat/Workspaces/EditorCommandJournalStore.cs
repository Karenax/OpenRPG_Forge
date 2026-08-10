using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Application.Commands;

namespace Forge.ModuleFormat.Workspaces;

public static class EditorCommandJournalStore
{
    public const string RelativePath = ".forge/command-history.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static void Save(string workspaceRoot, IReadOnlyList<CommandHistoryJournalEntry> entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(entries);
        Validate(entries);
        var path = Path.Combine(Path.GetFullPath(workspaceRoot), RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(entries, Options) + "\n", new UTF8Encoding(false));
        File.Move(temporary, path, true);
    }

    public static IReadOnlyList<CommandHistoryJournalEntry> Load(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var path = Path.Combine(Path.GetFullPath(workspaceRoot), RelativePath);
        if (!File.Exists(path)) return [];
        var entries = JsonSerializer.Deserialize<CommandHistoryJournalEntry[]>(
            File.ReadAllText(path, Encoding.UTF8), Options) ?? [];
        Validate(entries);
        return entries;
    }

    private static void Validate(IReadOnlyList<CommandHistoryJournalEntry> entries)
    {
        if (entries.Any(entry => entry.Id == Guid.Empty ||
                                 entry.SchemaVersion != CommandHistoryJournalEntry.CurrentSchemaVersion ||
                                 string.IsNullOrWhiteSpace(entry.Provenance) ||
                                 string.IsNullOrWhiteSpace(entry.CommandName)))
            throw new InvalidDataException("Editor command journal contains an invalid envelope.");
        if (entries.Select(entry => entry.Id).Distinct().Count() != entries.Count)
            throw new InvalidDataException("Editor command journal contains duplicate command UUIDs.");
    }
}
