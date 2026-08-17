using Forge.Domain.Factions;
using Forge.Domain.Navigation;
using Forge.Domain.Validation;

namespace Forge.Domain.WorldFeatures;

public sealed record EncounterVisibilityRuntimeContext(
    double CameraDistance,
    bool VisibleToCamera,
    bool LineOfSightBlocked);

public sealed record EncounterVisibilityRuntimeResolution(
    bool SpawnAllowed,
    string? SpawnEffectId,
    string Reason,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public sealed record EncounterReinforcementRuntimeContext(
    int AlertLevel,
    double ElapsedSinceAlertSeconds,
    int ActiveReinforcementActors,
    IReadOnlySet<string> ActiveConditions,
    EncounterVisibilityRuntimeContext Visibility,
    bool Networked,
    bool IsAuthoritativeServer);

public sealed record EncounterReinforcementRuntimeResolution(
    bool Reinforce,
    string? FactionSlug,
    Guid? RouteId,
    int RemainingActorSlots,
    string? SpawnEffectId,
    string Reason,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public enum EncounterActorLifecycleState
{
    Active,
    Dead,
    Escaped,
    Abandoned,
}

public sealed record EncounterCleanupRuntimeContext(
    EncounterActorLifecycleState State,
    double ElapsedInStateSeconds,
    bool AreaUnloading,
    bool PersistentActor,
    bool Networked,
    bool IsAuthoritativeServer);

public sealed record EncounterCleanupRuntimeResolution(
    bool Eligible,
    bool Preserve,
    string Reason,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

/// <summary>
/// Resolves tactical encounter policy without spawning actors, querying a camera or physics world,
/// following a route, deleting entities or transporting commands over a network. Runtime adapters
/// provide observed camera/LOS/lifecycle facts and remain responsible for every authoritative effect.
/// </summary>
public static class EncounterTacticalRuntimeService
{
    public const string ReinforcementInvalidCode = "world.encounter.reinforcement_invalid";
    public const string ReinforcementFactionMissingCode = "world.encounter.reinforcement_faction_missing";
    public const string ReinforcementRouteMissingCode = "world.encounter.reinforcement_route_missing";
    public const string VisibilityInvalidCode = "world.encounter.visibility_invalid";
    public const string CleanupInvalidCode = "world.encounter.cleanup_invalid";
    public const string RuntimeContextInvalidCode = "world.encounter.tactical_context_invalid";
    public const string AuthorityDeniedCode = "world.encounter.tactical_authority_denied";

    public static IReadOnlyList<DomainDiagnostic> Validate(
        EncounterWorldProfile profile,
        FactionCatalog factions,
        WaypointAuthoringProfile waypoints)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(factions);
        ArgumentNullException.ThrowIfNull(waypoints);

        var root = $"module/worldFeatures/encounters/{profile.Id}";
        var diagnostics = ValidateReinforcement(profile, factions, waypoints, root).ToList();
        diagnostics.AddRange(ValidateVisibility(profile.Visibility, $"{root}/visibility"));
        diagnostics.AddRange(ValidateCleanup(profile.Cleanup, $"{root}/cleanup"));
        return Distinct(diagnostics);
    }

    public static EncounterVisibilityRuntimeResolution ResolveVisibility(
        EncounterWorldProfile profile,
        EncounterVisibilityRuntimeContext context)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(context);

        var path = $"module/worldFeatures/encounters/{profile.Id}/visibility";
        var diagnostics = ValidateVisibility(profile.Visibility, path).ToList();
        if (!double.IsFinite(context.CameraDistance) || context.CameraDistance < 0)
            diagnostics.Add(new(RuntimeContextInvalidCode,
                "Visibility runtime context requires a finite non-negative camera distance.",
                $"{path}/runtimeContext/cameraDistance"));
        if (diagnostics.Count > 0)
            return new(false, null, "invalid", Distinct(diagnostics));

        var policy = profile.Visibility;
        if (context.CameraDistance < policy.MinimumCameraDistance)
            return new(false, null, "camera-distance", []);
        if (policy.Mode != EncounterWorldProfile.VisibilityMode.InViewAllowed && context.VisibleToCamera)
            return new(false, null, "visible-to-camera", []);
        if (policy.RequireLineOfSightBlocked && !context.LineOfSightBlocked)
            return new(false, null, "line-of-sight-open", []);

        return new(true,
            string.IsNullOrWhiteSpace(policy.SpawnEffectId) ? null : policy.SpawnEffectId,
            "visibility-approved", []);
    }

    public static EncounterReinforcementRuntimeResolution ResolveReinforcement(
        EncounterWorldProfile profile,
        FactionCatalog factions,
        WaypointAuthoringProfile waypoints,
        EncounterReinforcementRuntimeContext context)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(factions);
        ArgumentNullException.ThrowIfNull(waypoints);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.ActiveConditions);
        ArgumentNullException.ThrowIfNull(context.Visibility);

        var root = $"module/worldFeatures/encounters/{profile.Id}";
        var diagnostics = ValidateReinforcement(profile, factions, waypoints, root).ToList();
        if (context.AlertLevel < 0 || !double.IsFinite(context.ElapsedSinceAlertSeconds) ||
            context.ElapsedSinceAlertSeconds < 0 || context.ActiveReinforcementActors < 0)
            diagnostics.Add(new(RuntimeContextInvalidCode,
                "Reinforcement runtime context requires non-negative alert, elapsed time and actor count.",
                $"{root}/reinforcements/runtimeContext"));
        if (diagnostics.Count > 0)
            return new(false, null, null, 0, null, "invalid", Distinct(diagnostics));
        if (profile.Reinforcements is not { } policy)
            return new(false, null, null, 0, null, "reinforcement-optional-fallback", []);
        if (context.Networked && !context.IsAuthoritativeServer)
            return new(false, policy.FactionId, policy.RouteId,
                Math.Max(0, policy.MaximumActors - context.ActiveReinforcementActors), null,
                "authority-denied",
                [new(AuthorityDeniedCode,
                    "Only the authoritative server can approve networked encounter reinforcement.", root)]);
        if (context.ActiveConditions.Contains(policy.CancelCondition))
            return new(false, policy.FactionId, policy.RouteId,
                Math.Max(0, policy.MaximumActors - context.ActiveReinforcementActors), null,
                "cancel-condition", []);

        var visibility = ResolveVisibility(profile, context.Visibility);
        if (visibility.Diagnostics.Count > 0)
            return new(false, policy.FactionId, policy.RouteId,
                Math.Max(0, policy.MaximumActors - context.ActiveReinforcementActors), null,
                "visibility-invalid", visibility.Diagnostics);
        if (!visibility.SpawnAllowed)
            return new(false, policy.FactionId, policy.RouteId,
                Math.Max(0, policy.MaximumActors - context.ActiveReinforcementActors), null,
                visibility.Reason, []);
        if (context.AlertLevel < policy.MinimumAlertLevel)
            return new(false, policy.FactionId, policy.RouteId,
                Math.Max(0, policy.MaximumActors - context.ActiveReinforcementActors), null,
                "alert-threshold", []);
        if (context.ElapsedSinceAlertSeconds < policy.DelaySeconds)
            return new(false, policy.FactionId, policy.RouteId,
                Math.Max(0, policy.MaximumActors - context.ActiveReinforcementActors), null,
                "reinforcement-delay", []);
        if (context.ActiveReinforcementActors >= policy.MaximumActors)
            return new(false, policy.FactionId, policy.RouteId, 0, null, "actor-cap", []);

        return new(true, policy.FactionId, policy.RouteId,
            policy.MaximumActors - context.ActiveReinforcementActors,
            visibility.SpawnEffectId, "reinforcement-approved", []);
    }

    public static EncounterCleanupRuntimeResolution ResolveCleanup(
        EncounterWorldProfile profile,
        EncounterCleanupRuntimeContext context)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(context);

        var path = $"module/worldFeatures/encounters/{profile.Id}/cleanup";
        var diagnostics = ValidateCleanup(profile.Cleanup, path).ToList();
        if (!Enum.IsDefined(context.State) || !double.IsFinite(context.ElapsedInStateSeconds) ||
            context.ElapsedInStateSeconds < 0)
            diagnostics.Add(new(RuntimeContextInvalidCode,
                "Cleanup runtime context requires a known lifecycle state and finite non-negative elapsed time.",
                $"{path}/runtimeContext"));
        if (diagnostics.Count > 0)
            return new(false, false, "invalid", Distinct(diagnostics));
        if (context.Networked && !context.IsAuthoritativeServer)
            return new(false, false, "authority-denied",
                [new(AuthorityDeniedCode,
                    "Only the authoritative server can approve networked encounter cleanup.", path)]);

        var policy = profile.Cleanup;
        if (context.PersistentActor && policy.PreservePersistentActors)
            return new(false, true, "persistent-actor-preserved", []);
        if (context.AreaUnloading)
            return policy.CleanupOnAreaUnload
                ? new(true, false, "area-unload", [])
                : new(false, false, "area-unload-disabled", []);

        var delay = context.State switch
        {
            EncounterActorLifecycleState.Dead => policy.DeadAfterSeconds,
            EncounterActorLifecycleState.Escaped => policy.EscapedAfterSeconds,
            EncounterActorLifecycleState.Abandoned => policy.AbandonedAfterSeconds,
            _ => double.PositiveInfinity,
        };
        if (context.State == EncounterActorLifecycleState.Active)
            return new(false, false, "actor-active", []);
        return context.ElapsedInStateSeconds >= delay
            ? new(true, false, $"{context.State.ToString().ToLowerInvariant()}-eligible", [])
            : new(false, false, "cleanup-delay", []);
    }

    private static IReadOnlyList<DomainDiagnostic> ValidateReinforcement(
        EncounterWorldProfile profile,
        FactionCatalog factions,
        WaypointAuthoringProfile waypoints,
        string root)
    {
        if (profile.Reinforcements is not { } policy) return [];
        var path = $"{root}/reinforcements";
        var diagnostics = new List<DomainDiagnostic>();
        if (policy.MinimumAlertLevel < 0 || policy.RouteId == Guid.Empty || policy.MaximumActors < 1 ||
            !double.IsFinite(policy.DelaySeconds) || policy.DelaySeconds < 0 ||
            !SafeIdentifier(policy.CancelCondition))
            diagnostics.Add(new(ReinforcementInvalidCode,
                "Reinforcement requires a non-negative alert threshold, route, actor cap, finite delay and safe cancel condition.",
                path));
        if (!SafeIdentifier(policy.FactionId) || factions.Factions.All(item =>
                !string.Equals(item.Slug, policy.FactionId, StringComparison.Ordinal) || item.Archived))
            diagnostics.Add(new(ReinforcementFactionMissingCode,
                "Reinforcement faction must reference an active authored faction slug.",
                $"{path}/factionId"));
        if (waypoints.AreaId != profile.AreaId ||
            waypoints.PatrolRoutes.All(item => item.Id != policy.RouteId || item.Points.Count == 0))
            diagnostics.Add(new(ReinforcementRouteMissingCode,
                "Reinforcement route must reference a non-empty authored patrol route in the encounter area.",
                $"{path}/routeId"));
        return diagnostics;
    }

    private static IReadOnlyList<DomainDiagnostic> ValidateVisibility(
        EncounterWorldProfile.VisibilityPolicy policy,
        string path)
    {
        var effectValid = string.IsNullOrWhiteSpace(policy.SpawnEffectId)
            ? policy.Mode != EncounterWorldProfile.VisibilityMode.Effect
            : SafeIdentifier(policy.SpawnEffectId);
        return !Enum.IsDefined(policy.Mode) || !double.IsFinite(policy.MinimumCameraDistance) ||
               policy.MinimumCameraDistance < 0 || !effectValid
            ? [new(VisibilityInvalidCode,
                "Visibility requires a known mode, finite camera distance and a safe effect for effect mode.", path)]
            : [];
    }

    private static IReadOnlyList<DomainDiagnostic> ValidateCleanup(
        EncounterWorldProfile.CleanupPolicy policy,
        string path) =>
        !double.IsFinite(policy.DeadAfterSeconds) || policy.DeadAfterSeconds < 0 ||
        !double.IsFinite(policy.EscapedAfterSeconds) || policy.EscapedAfterSeconds < 0 ||
        !double.IsFinite(policy.AbandonedAfterSeconds) || policy.AbandonedAfterSeconds < 0
            ? [new(CleanupInvalidCode,
                "Cleanup requires finite non-negative delays for dead, escaped and abandoned actors.", path)]
            : [];

    private static IReadOnlyList<DomainDiagnostic> Distinct(IEnumerable<DomainDiagnostic> diagnostics) =>
        diagnostics.DistinctBy(item => (item.Code, item.Path))
            .OrderBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.Path, StringComparer.Ordinal)
            .ToArray();

    private static bool SafeIdentifier(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Split(['.', '-', '_', ':', '/']).All(segment =>
            segment.Length > 0 && segment.All(character => char.IsLetterOrDigit(character)));
}
