using Forge.Domain.Spatial;

namespace Forge.Domain.WorldFeatures;

public enum EncounterNetworkAuthority
{
    Local,
    Host,
    Server,
}

public enum EncounterWaveCompletionPolicy
{
    AllDefeated,
    ObjectiveCompleted,
    TimerElapsed,
    Script,
}

public sealed record EncounterScenarioProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    IReadOnlyList<EncounterScenarioProfile.SpawnAnchorPolicy> SpawnAnchors,
    EncounterSpawnDistribution Distribution,
    EncounterScenarioProfile.ActivatorPolicy Activator,
    EncounterScenarioProfile.LifecyclePolicy Lifecycle,
    IReadOnlyList<EncounterScenarioProfile.PhasePolicy> Phases,
    IReadOnlyDictionary<string, string> Overrides)
{
    public const int CurrentSchemaVersion = 1;

    public sealed record SpawnAnchorPolicy(
        Guid Id,
        string AnchorId,
        TransformValue Transform,
        int Capacity,
        bool Navigable,
        EncounterWorldProfile.VisibilityMode Visibility,
        bool IsFallback,
        double Weight);

    public sealed record ActivatorPolicy(
        Guid Id,
        bool PlayersOnly,
        bool IgnoreSummons,
        bool IgnoreCompanions,
        bool IgnoreGameMasters,
        bool IgnoreSpectators,
        EncounterNetworkAuthority RequiredAuthority,
        IReadOnlyList<string> RequiredTags);

    public sealed record LifecyclePolicy(
        Guid Id,
        string ForgeScriptApiVersion,
        EventBinding Entered,
        EventBinding Exited,
        EventBinding Exhausted);

    public sealed record EventBinding(Guid Id, string Binding, string? ScriptId);

    public sealed record PhasePolicy(
        Guid Id,
        int Order,
        string DisplayName,
        IReadOnlyList<string> EntryConditions,
        IReadOnlyList<WavePolicy> Waves,
        IReadOnlyList<Guid> ReinforcementEncounterIds);

    public sealed record WavePolicy(
        Guid Id,
        int Order,
        IReadOnlyList<Guid> GroupIds,
        double DelaySeconds,
        EncounterWaveCompletionPolicy Completion,
        string? CompletionTargetId,
        Guid? QuestId,
        string? ObjectiveId,
        string? VoiceCueId,
        Guid? NextWaveId);
}
