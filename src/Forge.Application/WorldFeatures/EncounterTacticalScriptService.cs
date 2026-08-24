using Forge.Application.Projects;
using Forge.Domain.WorldFeatures;

namespace Forge.Application.WorldFeatures;

/// <summary>Typed read-only projection for encounter tactical policy and ForgeScript hosts.</summary>
public sealed class EncounterTacticalScriptService
{
    public sealed record ReinforcementView(
        int MinimumAlertLevel,
        string FactionSlug,
        string? FactionDisplayName,
        string? FactionProvenance,
        Guid RouteId,
        int RoutePointCount,
        string? RouteProvenance,
        int MaximumActors,
        double DelaySeconds,
        string CancelCondition);

    public sealed record VisibilityView(
        string Mode,
        double MinimumCameraDistance,
        bool RequireLineOfSightBlocked,
        string? SpawnEffectId);

    public sealed record CleanupView(
        double DeadAfterSeconds,
        double EscapedAfterSeconds,
        double AbandonedAfterSeconds,
        bool CleanupOnAreaUnload,
        bool PreservePersistentActors);

    public sealed record Handle(
        long Revision,
        Guid EncounterId,
        string Provenance,
        ReinforcementView? Reinforcement,
        VisibilityView Visibility,
        CleanupView Cleanup,
        IReadOnlyList<string> Diagnostics,
        string RuntimeBoundary);

    public Handle Get(ProjectState state, Guid encounterId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var project = state.Project;
        var encounter = project.Module.WorldFeatures.Encounters.SingleOrDefault(item => item.Id == encounterId)
            ?? throw new KeyNotFoundException($"Encounter world profile '{encounterId:D}' was not found.");
        var area = project.Areas.SingleOrDefault(item => item.Id == encounter.AreaId)
            ?? throw new KeyNotFoundException($"Encounter area '{encounter.AreaId}' was not found.");
        var diagnostics = EncounterTacticalRuntimeService.Validate(encounter, project.Module.Factions,
            area.Navigation.Waypoints);
        var policy = encounter.Reinforcements;
        var faction = policy is null
            ? null
            : project.Module.Factions.Factions.SingleOrDefault(item =>
                string.Equals(item.Slug, policy.FactionId, StringComparison.Ordinal) && !item.Archived);
        var route = policy is null
            ? null
            : area.Navigation.Waypoints.PatrolRoutes.SingleOrDefault(item => item.Id == policy.RouteId);

        return new(state.Revision.Value, encounter.Id, encounter.Provenance,
            policy is null
                ? null
                : new(policy.MinimumAlertLevel, policy.FactionId, faction?.DisplayName, faction?.Provenance,
                    policy.RouteId, route?.Points.Count ?? 0, route?.Provenance,
                    policy.MaximumActors, policy.DelaySeconds, policy.CancelCondition),
            new(encounter.Visibility.Mode.ToString(), encounter.Visibility.MinimumCameraDistance,
                encounter.Visibility.RequireLineOfSightBlocked,
                string.IsNullOrWhiteSpace(encounter.Visibility.SpawnEffectId)
                    ? null
                    : encounter.Visibility.SpawnEffectId),
            new(encounter.Cleanup.DeadAfterSeconds, encounter.Cleanup.EscapedAfterSeconds,
                encounter.Cleanup.AbandonedAfterSeconds, encounter.Cleanup.CleanupOnAreaUnload,
                encounter.Cleanup.PreservePersistentActors),
            diagnostics.Select(item => $"{item.Code}:{item.Path}").ToArray(),
            "Policy projection only: actor spawn, camera/LOS sampling, cleanup effects and network transport remain runtime-adapter responsibilities.");
    }
}
