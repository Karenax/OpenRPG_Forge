using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.Domain.Areas;

public enum PlayFromHereStartMode
{
    PlayerSpawn,
    Camera,
    Waypoint,
}

public enum PlaytestTargetKind
{
    World,
    SelectedQuest,
    Dialogue,
}

public sealed record PlaytestQuestInjection(
    Guid Id,
    QuestId QuestId,
    QuestStateId StateId,
    IReadOnlyDictionary<string, string> Variables);

public sealed record AreaPlaytestProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public bool Enabled { get; }
    public bool RequiredAtRuntime { get; }
    public PlayFromHereStartMode StartMode { get; }
    public EntityId? SpawnEntityId { get; }
    public TransformValue CameraTransform { get; }
    public Guid? WaypointInstanceId { get; }
    public BlueprintId? PlayerBlueprintId { get; }
    public string TestProfile { get; }
    public IReadOnlyList<PlaytestQuestInjection> QuestInjections { get; }
    public PlaytestTargetKind TargetKind { get; }
    public Guid? TargetId { get; }
    public bool Headless { get; }

    public AreaPlaytestProfile(
        Guid id,
        int schemaVersion,
        string? provenance,
        bool enabled,
        bool requiredAtRuntime,
        PlayFromHereStartMode startMode,
        EntityId? spawnEntityId,
        TransformValue? cameraTransform,
        Guid? waypointInstanceId,
        BlueprintId? playerBlueprintId,
        string? testProfile,
        IReadOnlyList<PlaytestQuestInjection>? questInjections,
        PlaytestTargetKind targetKind = PlaytestTargetKind.World,
        Guid? targetId = null,
        bool headless = false)
    {
        Id = id;
        SchemaVersion = schemaVersion == 0 ? CurrentSchemaVersion : schemaVersion;
        Provenance = string.IsNullOrWhiteSpace(provenance) ? "migration-default" : provenance.Trim();
        Enabled = enabled;
        RequiredAtRuntime = requiredAtRuntime;
        StartMode = startMode;
        SpawnEntityId = spawnEntityId;
        CameraTransform = cameraTransform ?? TransformValue.Identity;
        WaypointInstanceId = waypointInstanceId;
        PlayerBlueprintId = playerBlueprintId;
        TestProfile = string.IsNullOrWhiteSpace(testProfile) ? "default" : testProfile.Trim();
        QuestInjections = (questInjections ?? []).OrderBy(item => item.QuestId.ToString(), StringComparer.Ordinal)
            .ThenBy(item => item.Id).ToArray();
        TargetKind = targetKind;
        TargetId = targetId;
        Headless = headless;
    }

    public static AreaPlaytestProfile CreateDefault(AreaId areaId) => new(
        Stable(areaId), CurrentSchemaVersion, "migration-default", false, false,
        PlayFromHereStartMode.PlayerSpawn, null, TransformValue.Identity, null, null, "default", []);

    private static Guid Stable(AreaId areaId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:{areaId}:play-from-here"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }
}

public static class AreaPlaytestProfileValidator
{
    public const string IdentityCode = "area.playtest.identity_invalid";
    public const string SchemaCode = "area.playtest.schema_unsupported";
    public const string StartCode = "area.playtest.start_invalid";
    public const string CharacterCode = "area.playtest.character_invalid";
    public const string QuestCode = "area.playtest.quest_invalid";
    public const string TargetCode = "area.playtest.target_invalid";

    public static IReadOnlyList<DomainDiagnostic> Validate(
        ProjectDefinition project,
        AreaDefinition area,
        AreaPlaytestProfile profile)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(profile);
        if (!profile.Enabled) return [];
        var root = $"areas/{area.Id}/playtest";
        var diagnostics = new List<DomainDiagnostic>();
        if (profile.Id == Guid.Empty || string.IsNullOrWhiteSpace(profile.Provenance) ||
            string.IsNullOrWhiteSpace(profile.TestProfile))
            diagnostics.Add(new(IdentityCode, "Playtest identity, provenance and test profile are required.", root));
        if (profile.SchemaVersion != AreaPlaytestProfile.CurrentSchemaVersion)
            diagnostics.Add(new(SchemaCode, "Playtest profile schema version is unsupported.", $"{root}/schemaVersion"));

        var blueprint = profile.PlayerBlueprintId is null
            ? null
            : project.Blueprints.SingleOrDefault(item => item.Id == profile.PlayerBlueprintId);
        if (blueprint is null || blueprint.Kind != BlueprintKind.PlayerSpawn)
            diagnostics.Add(new(CharacterCode, "Playtest character must reference a PlayerSpawn blueprint.",
                $"{root}/playerBlueprintId"));

        var startValid = profile.StartMode switch
        {
            PlayFromHereStartMode.PlayerSpawn => profile.SpawnEntityId is not null &&
                area.Entities.Any(item => item.Id == profile.SpawnEntityId && item.BlueprintId == profile.PlayerBlueprintId),
            PlayFromHereStartMode.Camera => IsFinite(profile.CameraTransform),
            PlayFromHereStartMode.Waypoint => profile.WaypointInstanceId is not null &&
                area.Navigation.Anchors.Instances.Any(item => item.Id == profile.WaypointInstanceId),
            _ => false,
        };
        if (!startValid)
            diagnostics.Add(new(StartCode, "Playtest start must resolve to a spawn, finite camera or area waypoint.",
                $"{root}/start"));

        if (profile.QuestInjections.Select(item => item.Id).Any(item => item == Guid.Empty) ||
            profile.QuestInjections.Select(item => item.Id).Distinct().Count() != profile.QuestInjections.Count ||
            profile.QuestInjections.Select(item => item.QuestId).Distinct().Count() != profile.QuestInjections.Count ||
            profile.QuestInjections.Any(injection => project.Quests.SingleOrDefault(item => item.Id == injection.QuestId)
                is not { } quest || quest.States.All(item => item.Id != injection.StateId)))
            diagnostics.Add(new(QuestCode, "Playtest quest injections must reference unique existing quest states.",
                $"{root}/questInjections"));
        var targetValid = profile.TargetKind switch
        {
            PlaytestTargetKind.World => profile.TargetId is null,
            PlaytestTargetKind.SelectedQuest => profile.TargetId is { } questId &&
                project.Quests.Any(quest => quest.Id.Value == questId) &&
                profile.QuestInjections.Any(injection => injection.QuestId.Value == questId),
            PlaytestTargetKind.Dialogue => profile.TargetId is { } dialogueId &&
                project.Dialogues.Any(dialogue => dialogue.Id.Value == dialogueId),
            _ => false,
        };
        if (!targetValid)
            diagnostics.Add(new(TargetCode,
                "Playtest target must resolve to the world, an injected quest or an existing dialogue.",
                $"{root}/target"));
        return diagnostics;
    }

    private static bool IsFinite(TransformValue value) =>
        float.IsFinite(value.Position.X) && float.IsFinite(value.Position.Y) && float.IsFinite(value.Position.Z) &&
        float.IsFinite(value.Rotation.X) && float.IsFinite(value.Rotation.Y) &&
        float.IsFinite(value.Rotation.Z) && float.IsFinite(value.Rotation.W) &&
        float.IsFinite(value.Scale.X) && float.IsFinite(value.Scale.Y) && float.IsFinite(value.Scale.Z);
}
