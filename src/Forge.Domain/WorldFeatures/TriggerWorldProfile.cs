using Forge.Domain.Areas;
using Forge.Domain.Blueprints;

namespace Forge.Domain.WorldFeatures;

public sealed record TriggerWorldProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    BlueprintId BlueprintId,
    AreaId AreaId,
    TriggerWorldProfile.DiscoveryPolicy? Discovery,
    TriggerWorldProfile.InteractionPolicy? Interaction,
    TriggerWorldProfile.SchedulerPolicy? Scheduler,
    string CustomEventNamespace,
    TriggerWorldProfile.AccessPolicy? Access,
    TriggerWorldProfile.RestPolicy? Rest,
    TriggerWorldProfile.CluePolicy? Clue,
    TriggerWorldProfile.NarrativeCuePolicy? Narrative,
    TriggerWorldProfile.DirectorPolicy? Director)
{
    public enum DiscoveryScope { Player, Party, World }

    public sealed record DiscoveryPolicy(
        IReadOnlyList<string> Senses,
        int Difficulty,
        Guid RevealTargetId,
        string FeedbackId,
        bool Permanent,
        DiscoveryScope Scope);

    public sealed record InteractionPolicy(
        string Verb,
        string Icon,
        string Outline,
        string PromptKey,
        string ScriptId,
        bool Visible,
        string Permission);

    public sealed record SchedulerPolicy(
        double IntervalSeconds,
        int BudgetPerMinute,
        bool WhileOccupiedOnly,
        bool AggregateOccupants);

    public sealed record AccessPolicy(string CredentialId, bool Consume, string DeniedFeedbackKey);

    public sealed record RestPolicy(
        bool Safe,
        string DangerQuery,
        bool WholePartyRequired,
        bool AllowCampSetup,
        string AllowedMessageKey,
        string DeniedMessageKey);

    public sealed record CluePolicy(
        string SkillId,
        int Difficulty,
        string TraceId,
        double DirectionDegrees,
        double AgeHours,
        string? FalseClueId,
        Guid? QuestId);

    public sealed record NarrativeCuePolicy(
        IReadOnlyList<BlueprintId> Participants,
        int Priority,
        bool Once,
        double CooldownSeconds,
        string MultiplayerPolicy,
        Guid DialogueId);

    public sealed record DirectorPolicy(
        bool AllowManualEnter,
        bool AllowManualExit,
        bool AllowManualFire,
        bool RequireTarget,
        bool Audit);
}
