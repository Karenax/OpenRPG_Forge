using Forge.Application.Projects;
using Forge.Domain.Factions;
using Forge.Domain.WorldFeatures;

namespace Forge.Application.WorldFeatures;

/// <summary>Typed read-only projection for encounter lifecycle authoring and ForgeScript hosts.</summary>
public sealed class EncounterLifecycleScriptService
{
    public sealed record RespawnView(
        double DelaySeconds,
        int MaximumCycles,
        string Condition,
        bool ResetWhenEmpty,
        bool Persistent,
        double AntiFarmMultiplier);

    public sealed record FactionView(Guid Id, string Slug, string DisplayName, string Provenance);

    public sealed record SchedulerView(
        bool HeartbeatEnabled,
        double IntervalSeconds,
        int BudgetPerMinute,
        string CustomEventNamespace);

    public sealed record Handle(
        long Revision,
        Guid EncounterId,
        string Provenance,
        RespawnView? Respawn,
        FactionView? SpawnFaction,
        SchedulerView? Scheduler,
        IReadOnlyList<string> Diagnostics);

    public Handle Get(ProjectState state, Guid encounterId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var encounter = state.Project.Module.WorldFeatures.Encounters.SingleOrDefault(item => item.Id == encounterId)
            ?? throw new KeyNotFoundException($"Encounter world profile '{encounterId:D}' was not found.");
        var factions = state.Project.Module.Factions;
        var diagnostics = EncounterLifecycleRuntimeService.Validate(encounter, factions);
        var faction = encounter.SpawnFactionId is null
            ? null
            : factions.Factions.SingleOrDefault(item =>
                string.Equals(item.Slug, encounter.SpawnFactionId, StringComparison.Ordinal) && !item.Archived);

        return new(state.Revision.Value, encounter.Id, encounter.Provenance,
            encounter.Respawn is { } respawn
                ? new(respawn.DelaySeconds, respawn.MaximumCycles, respawn.Condition, respawn.ResetWhenEmpty,
                    respawn.Persistent, respawn.AntiFarmMultiplier)
                : null,
            faction is null ? null : ToView(faction),
            encounter.Scheduler is { } scheduler
                ? new(scheduler.HeartbeatEnabled, scheduler.IntervalSeconds, scheduler.BudgetPerMinute,
                    scheduler.CustomEventNamespace)
                : null,
            diagnostics.Select(item => $"{item.Code}:{item.Path}").ToArray());
    }

    private static FactionView ToView(FactionDefinition faction) =>
        new(faction.Id, faction.Slug, faction.DisplayName, faction.Provenance);
}
