using Forge.Application.Gameplay;
using Forge.Application.Lifecycle;
using Forge.Application.Rules;
using Forge.Domain.Areas;
using Forge.Domain.Gameplay;
using Forge.Domain.Lifecycle;
using Forge.Domain.Modules;
using Forge.Domain.Rules;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Lifecycle;

public sealed class RuntimePolicyServicesTests
{
    [Fact]
    public void Heartbeats_are_budgeted_suspended_and_backlog_bounded()
    {
        var schedule = new HeartbeatScheduleDefinition(Guid.NewGuid(), 1, "Global simulation",
            HeartbeatScope.Module, true, 500, 20, true, "1.0", "OnGlobalHeartbeat", "test");
        var scheduler = new HeartbeatScheduler();
        var result = scheduler.Advance([schedule], 2_500, true);
        Assert.Equal(HeartbeatScheduler.MaximumCatchUpDispatches, result.Dispatches.Count);
        Assert.All(result.Dispatches, item => Assert.Equal(20, item.BudgetMilliseconds));
        Assert.Contains(result.Diagnostics, item => item.Code == HeartbeatScheduler.AggressiveIntervalCode);
        Assert.Contains(result.Diagnostics, item => item.Code == HeartbeatScheduler.BacklogTruncatedCode);

        var areaSchedule = new HeartbeatScheduleDefinition(Guid.NewGuid(), 1, "Area ecology",
            HeartbeatScope.Area, true, 1_000, 10, true, "1.0", "OnAreaHeartbeat", "test");
        Assert.Empty(scheduler.Advance([areaSchedule], 5_000, false, AreaId.From(Guid.NewGuid())).Dispatches);
    }

    [Fact]
    public void Ux0GHeartbeatDispatchCarriesResolvedScriptContract()
    {
        var schedule = new HeartbeatScheduleDefinition(Guid.NewGuid(), 1, "World simulation",
            HeartbeatScope.Module, true, 1_500, 25, true, "1.0", "main", "test:ux0-g",
            "scripts/world-simulation.forge", true);

        var dispatch = Assert.Single(new HeartbeatScheduler().Advance([schedule], 1_500, true).Dispatches);

        Assert.Equal("scripts/world-simulation.forge", dispatch.ScriptPath);
        Assert.Equal("main", dispatch.Handler);
        Assert.True(dispatch.RequiredAtRuntime);
        Assert.Equal(25, dispatch.BudgetMilliseconds);
    }

    [Fact]
    public void Rest_lifecycle_applies_volume_security_completion_and_cooldown()
    {
        var moduleId = ModuleId.From(Guid.NewGuid());
        var defaults = ModuleRuntimePolicyProfile.CreateDefault(moduleId);
        var lifecycle = new RestLifecycleProfile(defaults.Rest.Id, 1, true, PartyRestPolicy.WholeParty, 8,
            defaults.Rest.Bindings, "test");
        var blocked = new RestPolicyVolume(Guid.NewGuid(), "Danger", Vector3Value.Zero,
            new Vector3Value(4, 4, 4), RestPermissionMode.Forbidden, "rest.denied.danger", string.Empty,
            30, 0, 10, "test");
        var policy = new AreaRestPolicy(Guid.NewGuid(), 1, RestPermissionMode.Allowed, string.Empty, string.Empty,
            30, 3, [blocked], "test");
        var areaId = AreaId.From(Guid.NewGuid());
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var sequence = 0;
        var coordinator = new RestRuntimeCoordinator(() => new Guid(++sequence, 0, 0, new byte[8]));

        var denied = coordinator.Request("hero", "party", areaId, Vector3Value.Zero, lifecycle, policy, true,
            _ => true, 10, now);
        Assert.False(denied.Accepted);
        Assert.Equal(RestRuntimeCoordinator.ForbiddenCode, denied.DiagnosticCode);
        Assert.Equal([RestLifecycleStage.Requested, RestLifecycleStage.Interrupted], denied.Events.Select(item => item.Stage));

        var started = coordinator.Request("hero", "party", areaId, new Vector3Value(6, 0, 0), lifecycle, policy,
            true, _ => true, 12, now);
        Assert.True(started.Accepted);
        Assert.Equal([RestLifecycleStage.Requested, RestLifecycleStage.Started], started.Events.Select(item => item.Stage));
        var completed = coordinator.Complete(started.Session!, 14, now.AddSeconds(8));
        Assert.True(completed.Accepted);
        Assert.Equal(RestLifecycleStage.Completed, Assert.Single(completed.Events).Stage);
        var cooldown = coordinator.Request("hero", "party", areaId, new Vector3Value(6, 0, 0), lifecycle, policy,
            true, _ => true, 15, now.AddSeconds(9));
        Assert.Equal(RestRuntimeCoordinator.CooldownCode, cooldown.DiagnosticCode);
    }

    [Fact]
    public void Sequence_abort_restores_camera_control_invulnerability_locks_and_narrative_transaction()
    {
        var policy = ModuleRuntimePolicyProfile.CreateDefault(ModuleId.From(Guid.NewGuid())).SequenceAbort;
        var exploration = new ControlContext(Guid.NewGuid(), 1, "test", "exploration", false, "follow",
            new HashSet<string> { "move" });
        var cinematic = new ControlContext(Guid.NewGuid(), 1, "test", "cinematic", true, "rail",
            new HashSet<string> { "skip" });
        var snapshot = new SequenceRuntimeState("camera.player", [exploration], new HashSet<string>(),
            new HashSet<string>(), 20, null, false);
        var current = new SequenceRuntimeState("camera.cutscene", [exploration, cinematic],
            new HashSet<string> { "hero" }, new HashSet<string> { "input", "dialogue" }, 24, "tx-24", true);
        var result = SequenceAbortCoordinator.Abort(policy, current, snapshot);
        Assert.True(result.Accepted);
        Assert.Equal(snapshot with { SequenceActive = false }, result.State);
        Assert.Equal(2, result.ReleasedLocks);
        Assert.True(result.CameraRestored);
        Assert.True(result.NarrativeRolledBack);
        Assert.Equal("OnSequenceAborted", result.EventHandler);
    }

    [Fact]
    public void Ux0HWorldTransitionsResolveAbortCalendarAndDataDrivenProgression()
    {
        var moduleId = ModuleId.From(Guid.NewGuid());
        var defaults = ModuleRuntimePolicyProfile.CreateDefault(moduleId);
        var abort = new SequenceAbortPolicy(defaults.SequenceAbort.Id, 1, true,
            defaults.SequenceAbort.Binding with
            {
                Handler = "main",
                ScriptPath = "scripts/sequence-abort.forge",
                RequiredAtRuntime = true,
            }, SequenceAbortRestoreFlags.All, 16, "test:ux0-h");
        var snapshot = new SequenceRuntimeState("camera.player", [], new HashSet<string>(),
            new HashSet<string>(), 4, "tx-4", false);
        var active = snapshot with
        {
            CameraId = "camera.cinematic",
            NarrativeRevision = 8,
            SequenceActive = true,
        };
        var aborted = SequenceAbortCoordinator.Abort(abort, active, snapshot);

        var clock = new WorldClockProfile(Guid.NewGuid(), 1, "test:ux0-h", 30, 10, 20, 50,
            2.5, false, 95, new Dictionary<string, int> { ["zone"] = 25 },
            new Dictionary<WorldAstronomicalEventKind, int>
            {
                [WorldAstronomicalEventKind.Dawn] = 125,
            });
        var advanced = CustomWorldEventBus.AdvanceClock(clock,
            CustomWorldEventBus.CreateInitialSnapshot(clock), 2, "zone");
        var progression = new ProgressionPolicy(Guid.NewGuid(), 1, "forge.generic",
            [new(1, 0), new(2, 120), new(3, 360)], 1.2, .9,
            new Dictionary<string, double> { ["quest"] = 1.4 }, true, "test:ux0-h");
        var resolved = RulesRuntimeService.ResolveProgression(progression, 150, "quest", 2, 0);

        Assert.True(aborted.Accepted);
        Assert.True(aborted.CameraRestored);
        Assert.Equal("scripts/sequence-abort.forge", abort.Binding.ScriptPath);
        Assert.Equal(100, advanced.Snapshot.TotalMinutes);
        Assert.Contains(WorldAstronomicalEventKind.Dawn, advanced.CrossedEvents);
        Assert.Equal(113.4, resolved.AwardedExperience, 6);
        Assert.Equal(1, resolved.Level);
    }
}
