using Forge.Domain.Areas;
using Forge.Domain.Factions;
using Forge.Domain.WorldFeatures;

namespace Forge.Domain.Tests.WorldFeatures;

public sealed class EncounterLifecycleRuntimeServiceTests
{
    private static readonly Guid EncounterId = Guid.Parse("a2081000-0000-4000-8000-000000000001");
    private static readonly Guid FactionId = Guid.Parse("a2081000-0000-4000-8000-000000000002");

    [Fact]
    public void RespawnConsumesConditionResetPersistenceAntiFarmFactionAndAuthority()
    {
        var profile = Profile();
        var factions = Factions();
        var approved = EncounterLifecycleRuntimeService.ResolveRespawn(profile, factions,
            new(2, 31, false, false, new HashSet<string> { "area.occupied" }, true, true));
        var reset = EncounterLifecycleRuntimeService.ResolveRespawn(profile, factions,
            new(3, 31, true, false, new HashSet<string> { "area.occupied" }, true, true));
        var persisted = EncounterLifecycleRuntimeService.ResolveRespawn(profile, factions,
            new(2, 31, false, true, new HashSet<string> { "area.occupied" }, true, true));
        var denied = EncounterLifecycleRuntimeService.ResolveRespawn(profile, factions,
            new(0, 31, false, false, new HashSet<string> { "area.occupied" }, true, false));

        Assert.True(approved.Respawn);
        Assert.Equal(3, approved.NextCycle);
        Assert.Equal(.45, approved.RewardMultiplier);
        Assert.Equal("ash-watch", approved.SpawnFactionSlug);
        Assert.True(reset.Respawn);
        Assert.Equal(0, reset.EffectiveCompletedCycles);
        Assert.Equal(2, persisted.EffectiveCompletedCycles);
        Assert.False(denied.Respawn);
        Assert.Contains(denied.Diagnostics, item => item.Code == EncounterLifecycleRuntimeService.AuthorityDeniedCode);
    }

    [Fact]
    public void NonPersistentRespawnResetsAfterUnloadAndHonorsConditionDelayAndCycleCap()
    {
        var profile = Profile() with { Respawn = Profile().Respawn! with { Persistent = false } };
        var reset = EncounterLifecycleRuntimeService.ResolveRespawn(profile, Factions(),
            new(3, 31, false, true, new HashSet<string> { "area.occupied" }, false, false));
        var condition = EncounterLifecycleRuntimeService.ResolveRespawn(profile, Factions(),
            new(0, 31, false, false, new HashSet<string>(), false, false));
        var delay = EncounterLifecycleRuntimeService.ResolveRespawn(profile, Factions(),
            new(0, 29, false, false, new HashSet<string> { "area.occupied" }, false, false));
        var cap = EncounterLifecycleRuntimeService.ResolveRespawn(profile, Factions(),
            new(3, 31, false, false, new HashSet<string> { "area.occupied" }, false, false));

        Assert.True(reset.Respawn);
        Assert.Equal(0, reset.EffectiveCompletedCycles);
        Assert.Equal("condition", condition.Reason);
        Assert.Equal("delay", delay.Reason);
        Assert.Equal("cycle-cap", cap.Reason);
    }

    [Fact]
    public void SchedulerApprovesBoundedHeartbeatAndNamespacedCustomPolicyOnly()
    {
        var profile = Profile();
        var heartbeat = EncounterLifecycleRuntimeService.ResolveScheduledEvent(profile,
            new("heartbeat", 5, 0, true, true));
        var cadence = EncounterLifecycleRuntimeService.ResolveScheduledEvent(profile,
            new("heartbeat", 4.9, 0, false, false));
        var custom = EncounterLifecycleRuntimeService.ResolveScheduledEvent(profile,
            new("custom:crypt.encounter:alarm", 0, 1, false, false));
        var wrongNamespace = EncounterLifecycleRuntimeService.ResolveScheduledEvent(profile,
            new("custom:other:alarm", 0, 1, false, false));
        var budget = EncounterLifecycleRuntimeService.ResolveScheduledEvent(profile,
            new("custom:crypt.encounter:alarm", 0, 12, false, false));

        Assert.True(heartbeat.Dispatch);
        Assert.Equal("cadence", cadence.Reason);
        Assert.True(custom.Dispatch);
        Assert.Equal("namespace", wrongNamespace.Reason);
        Assert.Contains(wrongNamespace.Diagnostics, item => item.Code == EncounterLifecycleRuntimeService.EventInvalidCode);
        Assert.Equal("budget", budget.Reason);
    }

    [Fact]
    public void OptionalPoliciesFallbackAndInvalidPoliciesReturnStructuredDiagnostics()
    {
        var optional = Profile() with { Respawn = null, SpawnFactionId = null, Scheduler = null };
        var respawnFallback = EncounterLifecycleRuntimeService.ResolveRespawn(optional, Factions(),
            new(0, 0, false, false, new HashSet<string>(), false, false));
        var schedulerFallback = EncounterLifecycleRuntimeService.ResolveScheduledEvent(optional,
            new("heartbeat", 0, 0, false, false));
        var invalid = Profile() with
        {
            Respawn = Profile().Respawn! with { Condition = string.Empty },
            SpawnFactionId = "missing-faction",
            Scheduler = Profile().Scheduler! with { CustomEventNamespace = "custom::" },
        };
        var diagnostics = EncounterLifecycleRuntimeService.Validate(invalid, Factions());

        Assert.Equal("respawn-optional-fallback", respawnFallback.Reason);
        Assert.Equal("scheduler-optional-fallback", schedulerFallback.Reason);
        Assert.Contains(diagnostics, item => item.Code == EncounterLifecycleRuntimeService.RespawnInvalidCode &&
                                             item.Path.EndsWith("/respawn", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == EncounterLifecycleRuntimeService.FactionMissingCode &&
                                             item.Path.EndsWith("/spawnFactionId", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == EncounterLifecycleRuntimeService.SchedulerInvalidCode &&
                                             item.Path.EndsWith("/scheduler", StringComparison.Ordinal));
    }

    private static FactionCatalog Factions() => new(
        [new(FactionId, 1, "test:ux2-lw-i", "ash-watch", "Veilleurs des cendres", false, [], ["guard"])],
        [], [], []);

    private static EncounterWorldProfile Profile() => new(
        EncounterId, 1, "test:ux2-lw-i", AreaId.From(Guid.Parse("a2081000-0000-4000-8000-000000000003")),
        new(30, 3, "area.occupied", true, true, .45), "ash-watch",
        new(true, 5, 12, "crypt.encounter"), null,
        new(EncounterWorldProfile.VisibilityMode.InViewAllowed, 0, false, string.Empty),
        new(30, 30, 120, true, true));
}
