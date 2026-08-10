using System.Text.Json;

namespace Forge.ModuleFormat.Playtesting;

public sealed record PlaytestQuestLaunchState(
    string QuestId,
    string StateId,
    IReadOnlyDictionary<string, string> Variables);

public sealed record PlaytestLaunchManifest(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string AreaId,
    string SpawnEntityId,
    string StartMode,
    string TestProfile,
    IReadOnlyList<PlaytestQuestLaunchState> QuestStates,
    string TargetKind = "World",
    string? TargetId = null,
    bool Headless = false)
{
    public const int CurrentSchemaVersion = 1;
}

public static class PlaytestLaunchManifestStore
{
    public const string FileName = "playtest.launch.json";
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static string Serialize(PlaytestLaunchManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return JsonSerializer.Serialize(manifest, Options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    public static PlaytestLaunchManifest Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var manifest = JsonSerializer.Deserialize<PlaytestLaunchManifest>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException("Playtest launch manifest is empty.");
        if (manifest.Id == Guid.Empty || manifest.SchemaVersion != PlaytestLaunchManifest.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(manifest.Provenance) || !Guid.TryParse(manifest.AreaId, out _) ||
            !Guid.TryParse(manifest.SpawnEntityId, out _) || string.IsNullOrWhiteSpace(manifest.TestProfile) ||
            manifest.QuestStates.Any(item => !Guid.TryParse(item.QuestId, out _) || !Guid.TryParse(item.StateId, out _)) ||
            !Enum.TryParse<Forge.Domain.Areas.PlaytestTargetKind>(manifest.TargetKind, out var targetKind) ||
            (targetKind == Forge.Domain.Areas.PlaytestTargetKind.World && manifest.TargetId is not null) ||
            (targetKind != Forge.Domain.Areas.PlaytestTargetKind.World && !Guid.TryParse(manifest.TargetId, out _)))
            throw new InvalidDataException("Playtest launch manifest is invalid.");
        return manifest;
    }
}
