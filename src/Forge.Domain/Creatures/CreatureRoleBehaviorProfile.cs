namespace Forge.Domain.Creatures;

public enum AmbientResumeMode
{
    Resume,
    Restart,
    Stop,
}

public sealed record CreatureRoleBehaviorProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public bool RequiredAtRuntime { get; }
    public RoleInformation Role { get; }
    public AmbientBehaviorGraph Ambient { get; }
    public ProgressionTemplate AutoLevel { get; }
    public IReadOnlyDictionary<string, string> Overrides { get; }

    public CreatureRoleBehaviorProfile(
        Guid id,
        int schemaVersion,
        string provenance,
        bool requiredAtRuntime,
        RoleInformation role,
        AmbientBehaviorGraph ambient,
        ProgressionTemplate autoLevel,
        IReadOnlyDictionary<string, string>? overrides)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(ambient);
        ArgumentNullException.ThrowIfNull(autoLevel);
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance?.Trim() ?? string.Empty;
        RequiredAtRuntime = requiredAtRuntime;
        Role = role.Normalize();
        Ambient = ambient.Normalize();
        AutoLevel = autoLevel.Normalize();
        Overrides = (overrides ?? new Dictionary<string, string>())
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(
                item => CreaturePresentationProfile.NormalizeIdentifier(item.Key),
                item => item.Value?.Trim() ?? string.Empty,
                StringComparer.Ordinal);
    }

    public static CreatureRoleBehaviorProfile CreateDefault(Guid id, string provenance) => new(
        id,
        CurrentSchemaVersion,
        provenance,
        requiredAtRuntime: false,
        new RoleInformation(
            "forge.generic",
            new Dictionary<string, string> { ["occupation"] = "wanderer" },
            ["civilian"]),
        new AmbientBehaviorGraph(
            "ambient.standard",
            [new AmbientIdleSet(Guid.NewGuid(), "idle.standard", ["idle"], 1, 3, 8)],
            [],
            [],
            new AmbientInterruptionPolicy(true, true, true, AmbientResumeMode.Resume, .5)),
        new ProgressionTemplate(
            Guid.NewGuid(),
            "progression.manual",
            1,
            1,
            ApplyOnSave: false,
            ManualLock: true,
            [],
            [],
            []),
        new Dictionary<string, string>());

    public sealed record RoleInformation(
        string RulesetTemplateId,
        IReadOnlyDictionary<string, string> LoreFields,
        IReadOnlyList<string> RoleTags)
    {
        internal RoleInformation Normalize() => this with
        {
            RulesetTemplateId = CreaturePresentationProfile.NormalizeIdentifier(RulesetTemplateId),
            LoreFields = (LoreFields ?? new Dictionary<string, string>())
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .ToDictionary(
                    item => CreaturePresentationProfile.NormalizeIdentifier(item.Key),
                    item => item.Value?.Trim() ?? string.Empty,
                    StringComparer.Ordinal),
            RoleTags = (RoleTags ?? []).Select(CreaturePresentationProfile.NormalizeIdentifier)
                .Order(StringComparer.Ordinal).ToArray(),
        };
    }

    public sealed record AmbientIdleSet(
        Guid Id,
        string SetId,
        IReadOnlyList<string> AnimationIds,
        double Weight,
        double MinimumSeconds,
        double MaximumSeconds)
    {
        internal AmbientIdleSet Normalize() => this with
        {
            SetId = CreaturePresentationProfile.NormalizeIdentifier(SetId),
            AnimationIds = (AnimationIds ?? []).Select(CreaturePresentationProfile.NormalizeIdentifier)
                .Order(StringComparer.Ordinal).ToArray(),
        };
    }

    public sealed record AmbientScheduleEntry(
        Guid Id,
        string ScheduleId,
        int StartMinute,
        int EndMinute,
        string IdleSetId,
        string? WaypointId,
        string? ConversationId)
    {
        internal AmbientScheduleEntry Normalize() => this with
        {
            ScheduleId = CreaturePresentationProfile.NormalizeIdentifier(ScheduleId),
            IdleSetId = CreaturePresentationProfile.NormalizeIdentifier(IdleSetId),
            WaypointId = NormalizeOptional(WaypointId),
            ConversationId = NormalizeOptional(ConversationId),
        };
    }

    public sealed record AmbientInteraction(
        Guid Id,
        string InteractionId,
        string TargetId,
        string AnimationId,
        double CooldownSeconds)
    {
        internal AmbientInteraction Normalize() => this with
        {
            InteractionId = CreaturePresentationProfile.NormalizeIdentifier(InteractionId),
            TargetId = CreaturePresentationProfile.NormalizeIdentifier(TargetId),
            AnimationId = CreaturePresentationProfile.NormalizeIdentifier(AnimationId),
        };
    }

    public sealed record AmbientInterruptionPolicy(
        bool InterruptOnCombat,
        bool InterruptOnConversation,
        bool InterruptOnMovement,
        AmbientResumeMode ResumeMode,
        double ResumeDelaySeconds);

    public sealed record AmbientBehaviorGraph(
        string GraphId,
        IReadOnlyList<AmbientIdleSet> IdleSets,
        IReadOnlyList<AmbientScheduleEntry> Schedules,
        IReadOnlyList<AmbientInteraction> Interactions,
        AmbientInterruptionPolicy Interruption)
    {
        internal AmbientBehaviorGraph Normalize() => this with
        {
            GraphId = CreaturePresentationProfile.NormalizeIdentifier(GraphId),
            IdleSets = (IdleSets ?? []).Select(item => item.Normalize())
                .OrderBy(item => item.SetId, StringComparer.Ordinal).ToArray(),
            Schedules = (Schedules ?? []).Select(item => item.Normalize())
                .OrderBy(item => item.StartMinute).ThenBy(item => item.ScheduleId, StringComparer.Ordinal).ToArray(),
            Interactions = (Interactions ?? []).Select(item => item.Normalize())
                .OrderBy(item => item.InteractionId, StringComparer.Ordinal).ToArray(),
        };
    }

    public sealed record TrackAllocation(string TrackId, int Weight)
    {
        internal TrackAllocation Normalize() => this with
        {
            TrackId = CreaturePresentationProfile.NormalizeIdentifier(TrackId),
        };
    }

    public sealed record SkillAllocation(string SkillId, int RankEveryLevels, int MaximumRank)
    {
        internal SkillAllocation Normalize() => this with
        {
            SkillId = CreaturePresentationProfile.NormalizeIdentifier(SkillId),
        };
    }

    public sealed record TalentUnlock(string TalentId, int UnlockLevel)
    {
        internal TalentUnlock Normalize() => this with
        {
            TalentId = CreaturePresentationProfile.NormalizeIdentifier(TalentId),
        };
    }

    public sealed record ProgressionTemplate(
        Guid Id,
        string TemplateId,
        int Seed,
        int TargetLevel,
        bool ApplyOnSave,
        bool ManualLock,
        IReadOnlyList<TrackAllocation> Tracks,
        IReadOnlyList<SkillAllocation> Skills,
        IReadOnlyList<TalentUnlock> Talents)
    {
        internal ProgressionTemplate Normalize() => this with
        {
            TemplateId = CreaturePresentationProfile.NormalizeIdentifier(TemplateId),
            Tracks = (Tracks ?? []).Select(item => item.Normalize())
                .OrderBy(item => item.TrackId, StringComparer.Ordinal).ToArray(),
            Skills = (Skills ?? []).Select(item => item.Normalize())
                .OrderBy(item => item.SkillId, StringComparer.Ordinal).ToArray(),
            Talents = (Talents ?? []).Select(item => item.Normalize())
                .OrderBy(item => item.TalentId, StringComparer.Ordinal).ToArray(),
        };
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value)
        ? null
        : CreaturePresentationProfile.NormalizeIdentifier(value);
}
