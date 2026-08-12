using Forge.Application.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;

namespace Forge.Application.WorldFeatures;

public sealed class EncounterScenarioScriptService
{
    public sealed record SpawnAnchorView(
        Guid Id,
        string AnchorId,
        TransformValue Transform,
        int Capacity,
        bool Navigable,
        string Visibility,
        bool IsFallback,
        double Weight);

    public sealed record ActivatorView(
        Guid Id,
        bool PlayersOnly,
        bool IgnoreSummons,
        bool IgnoreCompanions,
        bool IgnoreGameMasters,
        bool IgnoreSpectators,
        string RequiredAuthority,
        IReadOnlyList<string> RequiredTags);

    public sealed record LifecycleView(
        Guid Id,
        string ForgeScriptApiVersion,
        string EnteredBinding,
        string? EnteredScriptId,
        string ExitedBinding,
        string? ExitedScriptId,
        string ExhaustedBinding,
        string? ExhaustedScriptId);

    public sealed record WaveView(
        Guid Id,
        int Order,
        IReadOnlyList<Guid> GroupIds,
        double DelaySeconds,
        string Completion,
        string? CompletionTargetId,
        Guid? QuestId,
        string? ObjectiveId,
        string? VoiceCueId,
        Guid? NextWaveId);

    public sealed record PhaseView(
        Guid Id,
        int Order,
        string DisplayName,
        IReadOnlyList<string> EntryConditions,
        IReadOnlyList<WaveView> Waves,
        IReadOnlyList<Guid> ReinforcementEncounterIds);

    public sealed record Handle(
        int SchemaVersion,
        long Revision,
        Guid EncounterId,
        string Provenance,
        string Distribution,
        IReadOnlyList<SpawnAnchorView> SpawnAnchors,
        ActivatorView Activator,
        LifecycleView Lifecycle,
        IReadOnlyList<PhaseView> Phases,
        bool RequiredAtRuntime,
        IReadOnlyDictionary<string, string> Overrides);

    public Handle Get(ProjectState state, Guid encounterId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var encounter = state.Project.Module.WorldFeatures.Encounters.SingleOrDefault(item => item.Id == encounterId)
            ?? throw new KeyNotFoundException($"Encounter world profile '{encounterId:D}' was not found.");
        var resolution = EncounterScenarioProfileValidator.Resolve(encounter);
        var profile = resolution.Profile ?? throw new InvalidOperationException(
            resolution.Diagnostics.FirstOrDefault()?.Code ?? "WORLD_ENCOUNTER_SCENARIO_MISSING");
        if (!resolution.Available)
            throw new InvalidOperationException(string.Join('|', resolution.Diagnostics.Select(item => item.Code)));

        return new(profile.SchemaVersion, state.Revision.Value, encounter.Id, profile.Provenance,
            profile.Distribution.ToString(), profile.SpawnAnchors.Select(item => new SpawnAnchorView(item.Id,
                item.AnchorId, item.Transform, item.Capacity, item.Navigable, item.Visibility.ToString(),
                item.IsFallback, item.Weight)).ToArray(),
            new(profile.Activator.Id, profile.Activator.PlayersOnly, profile.Activator.IgnoreSummons,
                profile.Activator.IgnoreCompanions, profile.Activator.IgnoreGameMasters,
                profile.Activator.IgnoreSpectators, profile.Activator.RequiredAuthority.ToString(),
                profile.Activator.RequiredTags),
            new(profile.Lifecycle.Id, profile.Lifecycle.ForgeScriptApiVersion,
                profile.Lifecycle.Entered.Binding, profile.Lifecycle.Entered.ScriptId,
                profile.Lifecycle.Exited.Binding, profile.Lifecycle.Exited.ScriptId,
                profile.Lifecycle.Exhausted.Binding, profile.Lifecycle.Exhausted.ScriptId),
            profile.Phases.Select(phase => new PhaseView(phase.Id, phase.Order, phase.DisplayName,
                phase.EntryConditions, phase.Waves.Select(wave => new WaveView(wave.Id, wave.Order,
                    wave.GroupIds, wave.DelaySeconds, wave.Completion.ToString(), wave.CompletionTargetId,
                    wave.QuestId, wave.ObjectiveId, wave.VoiceCueId, wave.NextWaveId)).ToArray(),
                phase.ReinforcementEncounterIds)).ToArray(), encounter.ScenarioRequiredAtRuntime, profile.Overrides);
    }
}
