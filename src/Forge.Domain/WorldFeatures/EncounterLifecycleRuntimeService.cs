using Forge.Domain.Factions;
using Forge.Domain.Validation;

namespace Forge.Domain.WorldFeatures;

public sealed record EncounterRespawnRuntimeContext(
    int CompletedCycles,
    double ElapsedSinceExhaustionSeconds,
    bool AreaHasNoEligibleActivators,
    bool RestoredAfterAreaUnload,
    IReadOnlySet<string> ActiveConditions,
    bool Networked,
    bool IsAuthoritativeServer);

public sealed record EncounterRespawnRuntimeResolution(
    bool Respawn,
    int EffectiveCompletedCycles,
    int NextCycle,
    double RewardMultiplier,
    string? SpawnFactionSlug,
    string Reason,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public sealed record EncounterScheduledEventRuntimeContext(
    string EventName,
    double ElapsedSeconds,
    int DispatchesThisMinute,
    bool Networked,
    bool IsAuthoritativeServer);

public sealed record EncounterScheduledEventRuntimeResolution(
    bool Dispatch,
    string? EventName,
    string Reason,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

/// <summary>
/// Resolves encounter lifecycle policies without spawning actors, assigning factions, dispatching scripts,
/// granting rewards or mutating runtime state. The caller remains responsible for those authoritative effects.
/// </summary>
public static class EncounterLifecycleRuntimeService
{
    public const string RespawnInvalidCode = "world.encounter.respawn_invalid";
    public const string FactionMissingCode = "world.encounter.spawn_faction_missing";
    public const string SchedulerInvalidCode = "world.encounter.scheduler_invalid";
    public const string EventInvalidCode = "world.encounter.custom_event_invalid";
    public const string AuthorityDeniedCode = "world.encounter.authority_denied";

    public static EncounterRespawnRuntimeResolution ResolveRespawn(
        EncounterWorldProfile profile,
        FactionCatalog factions,
        EncounterRespawnRuntimeContext context)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(factions);
        ArgumentNullException.ThrowIfNull(context);

        var root = $"module/worldFeatures/encounters/{profile.Id}";
        var diagnostics = ValidateRespawn(profile.Respawn, $"{root}/respawn").ToList();
        var faction = ResolveFaction(profile.SpawnFactionId, factions, $"{root}/spawnFactionId", diagnostics);
        if (context.CompletedCycles < 0 || !double.IsFinite(context.ElapsedSinceExhaustionSeconds) ||
            context.ElapsedSinceExhaustionSeconds < 0)
        {
            diagnostics.Add(new(RespawnInvalidCode,
                "Respawn runtime context requires non-negative cycles and finite elapsed time.",
                $"{root}/runtimeContext"));
        }

        if (diagnostics.Count > 0)
            return new(false, Math.Max(0, context.CompletedCycles), Math.Max(0, context.CompletedCycles),
                profile.Respawn?.AntiFarmMultiplier ?? 1, faction?.Slug, "invalid", diagnostics);
        if (profile.Respawn is not { } policy)
            return new(false, context.CompletedCycles, context.CompletedCycles, 1, faction?.Slug,
                "respawn-optional-fallback", []);
        if (context.Networked && !context.IsAuthoritativeServer)
            return new(false, context.CompletedCycles, context.CompletedCycles, policy.AntiFarmMultiplier,
                faction?.Slug, "authority-denied",
                [new(AuthorityDeniedCode, "Only the authoritative server can approve networked respawn.", root)]);

        var effectiveCycles = context.CompletedCycles;
        if ((!policy.Persistent && context.RestoredAfterAreaUnload) ||
            (policy.ResetWhenEmpty && context.AreaHasNoEligibleActivators))
            effectiveCycles = 0;
        if (!context.ActiveConditions.Contains(policy.Condition))
            return new(false, effectiveCycles, effectiveCycles, policy.AntiFarmMultiplier, faction?.Slug,
                "condition", []);
        if (context.ElapsedSinceExhaustionSeconds < policy.DelaySeconds)
            return new(false, effectiveCycles, effectiveCycles, policy.AntiFarmMultiplier, faction?.Slug,
                "delay", []);
        if (effectiveCycles >= policy.MaximumCycles)
            return new(false, effectiveCycles, effectiveCycles, policy.AntiFarmMultiplier, faction?.Slug,
                "cycle-cap", []);

        return new(true, effectiveCycles, checked(effectiveCycles + 1), policy.AntiFarmMultiplier,
            faction?.Slug, "respawn-approved", []);
    }

    public static EncounterScheduledEventRuntimeResolution ResolveScheduledEvent(
        EncounterWorldProfile profile,
        EncounterScheduledEventRuntimeContext context)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(context);

        var root = $"module/worldFeatures/encounters/{profile.Id}";
        var diagnostics = ValidateScheduler(profile.Scheduler, $"{root}/scheduler").ToList();
        if (!double.IsFinite(context.ElapsedSeconds) || context.ElapsedSeconds < 0 ||
            context.DispatchesThisMinute < 0)
            diagnostics.Add(new(SchedulerInvalidCode,
                "Scheduler runtime context requires finite elapsed time and a non-negative dispatch count.",
                $"{root}/runtimeContext"));
        if (diagnostics.Count > 0)
            return new(false, null, "invalid", diagnostics);
        if (profile.Scheduler is not { } scheduler)
            return new(false, null, "scheduler-optional-fallback", []);
        if (context.Networked && !context.IsAuthoritativeServer)
            return new(false, null, "authority-denied",
                [new(AuthorityDeniedCode, "Only the authoritative server can approve networked encounter events.", root)]);
        if (scheduler.BudgetPerMinute <= context.DispatchesThisMinute)
            return new(false, null, "budget", []);

        if (string.Equals(context.EventName, "heartbeat", StringComparison.Ordinal))
        {
            if (!scheduler.HeartbeatEnabled)
                return new(false, null, "heartbeat-disabled", []);
            if (context.ElapsedSeconds < scheduler.IntervalSeconds)
                return new(false, null, "cadence", []);
            return new(true, "heartbeat", "heartbeat-approved", []);
        }

        var prefix = $"custom:{scheduler.CustomEventNamespace}:";
        if (!ValidCustomEvent(context.EventName, prefix))
            return new(false, null, "namespace",
                [new(EventInvalidCode,
                    "Custom encounter events must use the configured namespace and non-empty safe segments.",
                    $"{root}/scheduler/customEventNamespace")]);
        return new(true, context.EventName, "custom-approved", []);
    }

    public static IReadOnlyList<DomainDiagnostic> Validate(
        EncounterWorldProfile profile,
        FactionCatalog factions)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(factions);
        var root = $"module/worldFeatures/encounters/{profile.Id}";
        var diagnostics = ValidateRespawn(profile.Respawn, $"{root}/respawn").ToList();
        diagnostics.AddRange(ValidateScheduler(profile.Scheduler, $"{root}/scheduler"));
        ResolveFaction(profile.SpawnFactionId, factions, $"{root}/spawnFactionId", diagnostics);
        return diagnostics
            .DistinctBy(item => (item.Code, item.Path))
            .OrderBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.Path, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<DomainDiagnostic> ValidateRespawn(
        EncounterWorldProfile.RespawnPolicy? policy,
        string path)
    {
        if (policy is null) return [];
        return !double.IsFinite(policy.DelaySeconds) || policy.DelaySeconds < 0 ||
               policy.MaximumCycles < 0 || !SafeIdentifier(policy.Condition) ||
               !double.IsFinite(policy.AntiFarmMultiplier) || policy.AntiFarmMultiplier is < 0 or > 1
            ? [new(RespawnInvalidCode,
                "Respawn requires finite delay, bounded cycles, a safe condition and an anti-farm multiplier from zero to one.",
                path)]
            : [];
    }

    private static IReadOnlyList<DomainDiagnostic> ValidateScheduler(
        EncounterWorldProfile.SchedulerPolicy? policy,
        string path)
    {
        if (policy is null) return [];
        return !double.IsFinite(policy.IntervalSeconds) || policy.IntervalSeconds <= 0 ||
               policy.BudgetPerMinute < 1 || !SafeNamespace(policy.CustomEventNamespace)
            ? [new(SchedulerInvalidCode,
                "Encounter scheduler requires finite cadence, a positive budget and a safe custom namespace.", path)]
            : [];
    }

    private static FactionDefinition? ResolveFaction(
        string? factionSlug,
        FactionCatalog factions,
        string path,
        ICollection<DomainDiagnostic> diagnostics)
    {
        if (factionSlug is null) return null;
        var resolved = factions.Factions.SingleOrDefault(item =>
            string.Equals(item.Slug, factionSlug, StringComparison.Ordinal));
        if (!SafeIdentifier(factionSlug) || resolved is null || resolved.Archived)
            diagnostics.Add(new(FactionMissingCode,
                "Spawn faction must reference an active authored faction slug.", path));
        return resolved is { Archived: false } ? resolved : null;
    }

    private static bool ValidCustomEvent(string value, string prefix)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var suffix = value[prefix.Length..];
        return suffix.Length > 0 && suffix.Split(':').All(SafeSegment);
    }

    private static bool SafeNamespace(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Split('.').All(SafeSegment);

    private static bool SafeIdentifier(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Split(['.', '-', '_', ':', '/']).All(SafeSegment);

    private static bool SafeSegment(string value) =>
        value.Length > 0 && value.All(character => char.IsLetterOrDigit(character));
}
