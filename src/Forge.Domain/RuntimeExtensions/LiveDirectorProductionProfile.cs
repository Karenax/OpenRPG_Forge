using Forge.Domain.RuntimePolicies;

namespace Forge.Domain.RuntimeExtensions;

public sealed record LiveDirectorProductionProfile
{
    public const int CurrentSchemaVersion = 1;

    public sealed record ForcedRestSettings(Guid Id, bool Enabled, bool AllowRulesOverride,
        bool EmitCompletedEvent, int MaximumTargets);

    public sealed record LevelAdjustmentSettings(Guid Id, bool Enabled, int MaximumDelta,
        bool RequireRebuild, bool RequireRollbackSnapshot);

    public sealed record SavedGroup(Guid Id, string Key, string Query, int Hotkey, int MaximumMembers);

    public sealed record AppearanceOverrideSettings(Guid Id, bool Enabled, float MinimumScale,
        float MaximumScale, int MaximumDurationSeconds, bool ValidateAssets);

    public sealed record SessionRecordingSettings(Guid Id, bool Enabled, bool RecordCommands,
        bool RecordEvents, bool AllowBookmarks, bool RedactPrivateData, string ExportFormat,
        int MaximumMinutes);

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public ForcedRestSettings ForcedRest { get; }
    public LevelAdjustmentSettings LevelAdjustment { get; }
    public IReadOnlyList<SavedGroup> SavedGroups { get; }
    public AppearanceOverrideSettings AppearanceOverrides { get; }
    public SessionRecordingSettings SessionRecording { get; }
    public string Provenance { get; }

    public LiveDirectorProductionProfile(Guid id, int schemaVersion, ForcedRestSettings forcedRest,
        LevelAdjustmentSettings levelAdjustment, IReadOnlyList<SavedGroup> savedGroups,
        AppearanceOverrideSettings appearanceOverrides, SessionRecordingSettings sessionRecording,
        string provenance)
    {
        ArgumentNullException.ThrowIfNull(forcedRest);
        ArgumentNullException.ThrowIfNull(levelAdjustment);
        ArgumentNullException.ThrowIfNull(savedGroups);
        ArgumentNullException.ThrowIfNull(appearanceOverrides);
        ArgumentNullException.ThrowIfNull(sessionRecording);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || forcedRest.Id == Guid.Empty ||
            forcedRest.MaximumTargets is < 1 or > 256 || levelAdjustment.Id == Guid.Empty ||
            levelAdjustment.MaximumDelta is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(id), "Live Director production policies require schema v1 and bounded operations.");
        var groups = savedGroups.OrderBy(item => item.Hotkey).ThenBy(item => item.Key, StringComparer.Ordinal).ToArray();
        if (groups.Any(item => item.Id == Guid.Empty || item.Hotkey is < 1 or > 12 ||
                item.MaximumMembers is < 1 or > 512 || string.IsNullOrWhiteSpace(item.Query)) ||
            groups.Select(item => item.Id).Distinct().Count() != groups.Length ||
            groups.Select(item => item.Hotkey).Distinct().Count() != groups.Length ||
            groups.Select(item => Normalize(item.Key)).Distinct(StringComparer.Ordinal).Count() != groups.Length)
            throw new ArgumentException("Saved Live Director groups require unique IDs, keys and hotkeys.", nameof(savedGroups));
        if (appearanceOverrides.Id == Guid.Empty || !float.IsFinite(appearanceOverrides.MinimumScale) ||
            !float.IsFinite(appearanceOverrides.MaximumScale) || appearanceOverrides.MinimumScale is < .1f or > 10f ||
            appearanceOverrides.MaximumScale < appearanceOverrides.MinimumScale || appearanceOverrides.MaximumScale > 10f ||
            appearanceOverrides.MaximumDurationSeconds is < 1 or > 86_400)
            throw new ArgumentException("Appearance overrides require bounded scale and duration.", nameof(appearanceOverrides));
        if (sessionRecording.Id == Guid.Empty || sessionRecording.ExportFormat is not ("jsonl" or "orp-replay") ||
            sessionRecording.MaximumMinutes is < 1 or > 1_440)
            throw new ArgumentException("Session recording requires a supported format and bounded duration.", nameof(sessionRecording));
        var ids = groups.Select(item => item.Id).Append(id).Append(forcedRest.Id).Append(levelAdjustment.Id)
            .Append(appearanceOverrides.Id).Append(sessionRecording.Id).ToArray();
        if (ids.Distinct().Count() != ids.Length)
            throw new ArgumentException("Live Director production UUIDs must be unique.");

        Id = id;
        SchemaVersion = schemaVersion;
        ForcedRest = forcedRest;
        LevelAdjustment = levelAdjustment;
        SavedGroups = groups.Select(item => item with { Key = Normalize(item.Key), Query = item.Query.Trim() }).ToArray();
        AppearanceOverrides = appearanceOverrides;
        SessionRecording = sessionRecording with { ExportFormat = sessionRecording.ExportFormat.Trim().ToLowerInvariant() };
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }

    public static LiveDirectorProductionProfile CreateDefault(Guid ownerId)
    {
        Guid Stable(string key) => RuntimePolicyIdFactory.Create(ownerId, $"post-launch:director:{key}");
        return new LiveDirectorProductionProfile(Stable("root"), 1,
            new ForcedRestSettings(Stable("forced-rest"), false, false, true, 32),
            new LevelAdjustmentSettings(Stable("level-adjustment"), false, 5, true, true), [],
            new AppearanceOverrideSettings(Stable("appearance"), false, .5f, 2f, 3_600, true),
            new SessionRecordingSettings(Stable("recording"), false, true, true, true, true, "orp-replay", 240),
            "migration-default");
    }

    public IReadOnlyList<Guid> AllIds() => SavedGroups.Select(item => item.Id).Append(Id).Append(ForcedRest.Id)
        .Append(LevelAdjustment.Id).Append(AppearanceOverrides.Id).Append(SessionRecording.Id).ToArray();

    private static string Normalize(string value) => DataDrivenRuleSchema.NormalizeKey(value, nameof(value));
}
