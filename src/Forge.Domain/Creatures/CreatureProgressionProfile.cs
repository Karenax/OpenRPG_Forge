namespace Forge.Domain.Creatures;

public enum AbilityLoadoutMode
{
    Learned,
    Prepared,
    Deck,
    Cooldown,
    Charges,
    Resource,
}

public sealed record CreatureProgressionProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }

    public int SchemaVersion { get; }

    public string Provenance { get; }

    public string RulesetId { get; }

    public bool AllowMultipleTracks { get; }

    public bool RequiredAtRuntime { get; }

    public IReadOnlyList<ProgressionTrack> Tracks { get; }

    public IReadOnlyList<DispositionAxis> DispositionAxes { get; }

    public IReadOnlyList<SkillProgression> Skills { get; }

    public IReadOnlyList<TalentProgression> Talents { get; }

    public AbilityLoadout Loadout { get; }

    public IReadOnlyDictionary<string, string> Overrides { get; }

    public CreatureProgressionProfile(
        Guid id,
        int schemaVersion,
        string provenance,
        string rulesetId,
        bool allowMultipleTracks,
        bool requiredAtRuntime,
        IReadOnlyList<ProgressionTrack>? tracks,
        IReadOnlyList<DispositionAxis>? dispositionAxes,
        IReadOnlyList<SkillProgression>? skills,
        IReadOnlyList<TalentProgression>? talents,
        AbilityLoadout loadout,
        IReadOnlyDictionary<string, string>? overrides)
    {
        ArgumentNullException.ThrowIfNull(loadout);
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance?.Trim() ?? string.Empty;
        RulesetId = CreaturePresentationProfile.NormalizeIdentifier(rulesetId);
        AllowMultipleTracks = allowMultipleTracks;
        RequiredAtRuntime = requiredAtRuntime;
        Tracks = (tracks ?? []).Select(item => item.Normalize())
            .OrderBy(item => item.TrackId, StringComparer.Ordinal).ToArray();
        DispositionAxes = (dispositionAxes ?? []).Select(item => item.Normalize())
            .OrderBy(item => item.AxisId, StringComparer.Ordinal).ToArray();
        Skills = (skills ?? []).Select(item => item.Normalize())
            .OrderBy(item => item.SkillId, StringComparer.Ordinal).ToArray();
        Talents = (talents ?? []).Select(item => item.Normalize())
            .OrderBy(item => item.TalentId, StringComparer.Ordinal).ToArray();
        Loadout = loadout.Normalize();
        Overrides = (overrides ?? new Dictionary<string, string>())
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(
                item => CreaturePresentationProfile.NormalizeIdentifier(item.Key),
                item => item.Value?.Trim() ?? string.Empty,
                StringComparer.Ordinal);
    }

    public static CreatureProgressionProfile CreateDefault(Guid id, string provenance) => new(
        id,
        CurrentSchemaVersion,
        provenance,
        "forge.generic",
        allowMultipleTracks: true,
        requiredAtRuntime: false,
        [new ProgressionTrack("adventurer", "Aventurier", 1, 20, 0, [])],
        [],
        [new SkillProgression("awareness", "Vigilance", "insight", 0, 10, 0, false, false, 10)],
        [],
        new AbilityLoadout("standard", AbilityLoadoutMode.Learned, 4, [], []),
        new Dictionary<string, string>());

    public sealed record ProgressionTrack(
        string TrackId,
        string DisplayName,
        int Level,
        int MaximumLevel,
        double Experience,
        IReadOnlyList<string> GrantedTalentIds)
    {
        internal ProgressionTrack Normalize() => this with
        {
            TrackId = CreaturePresentationProfile.NormalizeIdentifier(TrackId),
            DisplayName = DisplayName?.Trim() ?? string.Empty,
            GrantedTalentIds = (GrantedTalentIds ?? []).Select(CreaturePresentationProfile.NormalizeIdentifier)
                .Order(StringComparer.Ordinal).ToArray(),
        };
    }

    public sealed record DispositionAxis(
        string AxisId,
        string NegativeLabel,
        string PositiveLabel,
        double Value)
    {
        internal DispositionAxis Normalize() => this with
        {
            AxisId = CreaturePresentationProfile.NormalizeIdentifier(AxisId),
            NegativeLabel = NegativeLabel?.Trim() ?? string.Empty,
            PositiveLabel = PositiveLabel?.Trim() ?? string.Empty,
        };
    }

    public sealed record SkillProgression(
        string SkillId,
        string DisplayName,
        string GoverningAttributeId,
        int Rank,
        int MaximumRank,
        int TrainingPoints,
        bool Trained,
        bool Expertise,
        double DefaultDifficulty)
    {
        internal SkillProgression Normalize() => this with
        {
            SkillId = CreaturePresentationProfile.NormalizeIdentifier(SkillId),
            DisplayName = DisplayName?.Trim() ?? string.Empty,
            GoverningAttributeId = CreaturePresentationProfile.NormalizeIdentifier(GoverningAttributeId),
        };
    }

    public sealed record TalentPrerequisite(string Kind, string ReferenceId, int MinimumRank)
    {
        internal TalentPrerequisite Normalize() => this with
        {
            Kind = CreaturePresentationProfile.NormalizeIdentifier(Kind),
            ReferenceId = CreaturePresentationProfile.NormalizeIdentifier(ReferenceId),
        };
    }

    public sealed record TalentProgression(
        string TalentId,
        string DisplayName,
        int Rank,
        int MaximumRank,
        bool Passive,
        IReadOnlyList<TalentPrerequisite> Prerequisites,
        IReadOnlyList<string> GrantedAbilityIds,
        IReadOnlyList<string> HookIds)
    {
        internal TalentProgression Normalize() => this with
        {
            TalentId = CreaturePresentationProfile.NormalizeIdentifier(TalentId),
            DisplayName = DisplayName?.Trim() ?? string.Empty,
            Prerequisites = (Prerequisites ?? []).Select(item => item.Normalize())
                .OrderBy(item => item.Kind, StringComparer.Ordinal)
                .ThenBy(item => item.ReferenceId, StringComparer.Ordinal).ToArray(),
            GrantedAbilityIds = (GrantedAbilityIds ?? []).Select(CreaturePresentationProfile.NormalizeIdentifier)
                .Order(StringComparer.Ordinal).ToArray(),
            HookIds = (HookIds ?? []).Select(CreaturePresentationProfile.NormalizeIdentifier)
                .Order(StringComparer.Ordinal).ToArray(),
        };
    }

    public sealed record PreparedAbility(
        string AbilityId,
        int Rank,
        int Charges,
        double CooldownSeconds,
        string? ResourceId,
        int ResourceCost,
        IReadOnlyList<string> Modifiers)
    {
        internal PreparedAbility Normalize() => this with
        {
            AbilityId = CreaturePresentationProfile.NormalizeIdentifier(AbilityId),
            ResourceId = string.IsNullOrWhiteSpace(ResourceId)
                ? null
                : CreaturePresentationProfile.NormalizeIdentifier(ResourceId),
            Modifiers = (Modifiers ?? []).Select(CreaturePresentationProfile.NormalizeIdentifier)
                .Order(StringComparer.Ordinal).ToArray(),
        };
    }

    public sealed record AbilityLoadout(
        string ProviderId,
        AbilityLoadoutMode Mode,
        int Capacity,
        IReadOnlyList<string> LearnedAbilityIds,
        IReadOnlyList<PreparedAbility> PreparedAbilities)
    {
        internal AbilityLoadout Normalize() => this with
        {
            ProviderId = CreaturePresentationProfile.NormalizeIdentifier(ProviderId),
            LearnedAbilityIds = (LearnedAbilityIds ?? []).Select(CreaturePresentationProfile.NormalizeIdentifier)
                .Order(StringComparer.Ordinal).ToArray(),
            PreparedAbilities = (PreparedAbilities ?? []).Select(item => item.Normalize())
                .OrderBy(item => item.AbilityId, StringComparer.Ordinal).ToArray(),
        };
    }
}
