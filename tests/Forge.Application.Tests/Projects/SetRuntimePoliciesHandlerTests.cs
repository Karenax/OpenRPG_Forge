using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Lifecycle;
using Forge.Domain.Rules;
using Forge.Domain.RuntimePolicies;

namespace Forge.Application.Tests.Projects;

public sealed class SetRuntimePoliciesHandlerTests
{
    [Fact]
    public void Module_and_area_policies_support_dry_run_apply_and_stable_patches()
    {
        var context = new PlaceEntityTestContext();
        var clock = new FixedClock(PlaceEntityTestContext.Timestamp);
        var moduleDefaults = context.State.Project.Module.RuntimePolicies;
        var global = new HeartbeatScheduleDefinition(Guid.NewGuid(), 1, "World clock", HeartbeatScope.Module,
            true, 2_000, 15, false, "1.0", "OnGlobalHeartbeat", "test");
        var moduleProfile = new ModuleRuntimePolicyProfile(moduleDefaults.Id, 1, [global], moduleDefaults.Rest,
            moduleDefaults.SequenceAbort, "test");
        var moduleCommand = new SetModuleRuntimePoliciesCommand(CommandId.From(Guid.NewGuid()),
            context.State.Revision, moduleProfile, true);
        var moduleHandler = new SetModuleRuntimePoliciesHandler(clock);
        var preview = moduleHandler.Handle(context.State, moduleCommand);
        Assert.True(preview.Succeeded);
        Assert.Null(preview.NewRevision);
        Assert.Equal("module/runtimePolicies", preview.Patch!.Path);
        var applied = moduleHandler.Handle(context.State, moduleCommand with { DryRun = false });
        Assert.True(applied.Succeeded);
        Assert.Single(applied.State.Project.Module.RuntimePolicies.Heartbeats);

        var area = applied.State.Project.Areas.Single();
        var areaSchedule = new HeartbeatScheduleDefinition(Guid.NewGuid(), 1, "Area ecology", HeartbeatScope.Area,
            true, 3_000, 10, true, "1.0", "OnAreaHeartbeat", "test");
        var areaProfile = new AreaRuntimePolicyProfile(area.Id.Value, 1, [areaSchedule], area.RuntimePolicies.Rest,
            "test");
        var areaHandler = new SetAreaRuntimePoliciesHandler(clock);
        var areaResult = areaHandler.Handle(applied.State, new SetAreaRuntimePoliciesCommand(
            CommandId.From(Guid.NewGuid()), applied.State.Revision, area.Id, areaProfile, false));
        Assert.True(areaResult.Succeeded);
        Assert.Single(areaResult.State.Project.Areas.Single().RuntimePolicies.Heartbeats);
        Assert.Contains(area.Id.ToString(), areaResult.Patch!.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void Area_policy_rejects_an_owner_identity_mismatch()
    {
        var context = new PlaceEntityTestContext();
        var area = context.State.Project.Areas.Single();
        var invalid = new AreaRuntimePolicyProfile(Guid.NewGuid(), 1, [], area.RuntimePolicies.Rest, "test");
        var result = new SetAreaRuntimePoliciesHandler(new FixedClock(PlaceEntityTestContext.Timestamp)).Handle(
            context.State, new SetAreaRuntimePoliciesCommand(CommandId.From(Guid.NewGuid()), context.State.Revision,
                area.Id, invalid, false));
        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, item => item.Code == "runtime.policy.area_identity");
    }

    [Fact]
    public void Combined_policy_command_is_one_atomic_undoable_transaction()
    {
        var context = new PlaceEntityTestContext();
        var area = context.State.Project.Areas.Single();
        var moduleDefaults = context.State.Project.Module.RuntimePolicies;
        var global = new HeartbeatScheduleDefinition(Guid.NewGuid(), 1, "World clock", HeartbeatScope.Module,
            true, 2_000, 12, false, "1.0", "OnGlobalHeartbeat", "test");
        var local = new HeartbeatScheduleDefinition(Guid.NewGuid(), 1, "Area ecology", HeartbeatScope.Area,
            true, 1_000, 8, true, "1.0", "OnAreaHeartbeat", "test");
        var module = new ModuleRuntimePolicyProfile(moduleDefaults.Id, 1, [global], moduleDefaults.Rest,
            moduleDefaults.SequenceAbort, "test");
        var areaProfile = new AreaRuntimePolicyProfile(area.RuntimePolicies.Id, 1, [local],
            area.RuntimePolicies.Rest, "test");
        var handler = new SetRuntimePoliciesHandler(new FixedClock(PlaceEntityTestContext.Timestamp));
        var command = new SetRuntimePoliciesCommand(CommandId.From(Guid.NewGuid()), context.State.Revision,
            module, area.Id, areaProfile, false);
        var result = handler.Handle(context.State, command);
        Assert.True(result.Succeeded);
        Assert.Equal("runtimePolicies", result.Patch!.Path);
        Assert.Single(result.State.Project.Module.RuntimePolicies.Heartbeats);
        Assert.Single(result.State.Project.Areas.Single().RuntimePolicies.Heartbeats);

        var history = new ProjectCommandHistory(context.State);
        history.Record(result);
        Assert.True(history.Undo().Succeeded);
        Assert.Empty(history.State.Project.Module.RuntimePolicies.Heartbeats);
        Assert.Empty(history.State.Project.Areas.Single().RuntimePolicies.Heartbeats);
        Assert.True(history.Redo().Succeeded);
        Assert.Single(history.State.Project.Module.RuntimePolicies.Heartbeats);
        Assert.Single(history.State.Project.Areas.Single().RuntimePolicies.Heartbeats);
    }

    [Fact]
    public void Ux0FEventProfilesUseOneDryRunnableUndoableCommandAndPreserveOtherPolicies()
    {
        var context = new PlaceEntityTestContext();
        var initial = context.State.Project.Module.RuntimePolicies;
        var lifecycleBindings = initial.LifecycleEvents.Bindings.Select(binding =>
            binding.Event == RuntimeLifecycleEventKind.ProjectLoaded && binding.Authority == RuntimeAuthority.Client
                ? binding with
                {
                    Handler = "OnClientProjectReady",
                    ScriptPath = "scripts/client-project-ready.forge",
                    RequiredAtRuntime = true,
                    Provenance = "test:ux0-f",
                }
                : binding).ToArray();
        var lifecycle = new ProjectLifecycleEventProfile(initial.LifecycleEvents.Id,
            initial.LifecycleEvents.SchemaVersion, "test:ux0-f", true, false, lifecycleBindings);
        var gameplayBindings = initial.GameplayEvents.Bindings.Select(binding => binding.EventName == "life.dead"
            ? binding with
            {
                Handler = "OnHeroDefeated",
                ScriptPath = "scripts/life-events.forge",
                RequiredAtRuntime = true,
                Provenance = "test:ux0-f",
            }
            : binding).ToArray();
        var gameplay = new ProjectGameplayEventProfile(initial.GameplayEvents.Id,
            initial.GameplayEvents.SchemaVersion, "test:ux0-f", true, false, true, false,
            initial.GameplayEvents.ReplicateEquipmentAndProgression, gameplayBindings);
        var configured = new ModuleRuntimePolicyProfile(initial.Id, initial.SchemaVersion,
            initial.Heartbeats, initial.Rest, initial.SequenceAbort, "test:ux0-f", initial.Cinematics,
            initial.Gameplay, initial.Rules, initial.Extensions, gameplay, initial.World, lifecycle);
        var handler = new SetModuleRuntimePoliciesHandler(new FixedClock(PlaceEntityTestContext.Timestamp));
        var command = new SetModuleRuntimePoliciesCommand(CommandId.From(Guid.NewGuid()),
            context.State.Revision, configured, true);

        var preview = handler.Handle(context.State, command);

        Assert.True(preview.Succeeded);
        Assert.False(preview.Audit.Applied);
        Assert.Same(context.State, preview.State);
        Assert.Contains("8 project lifecycle-event", preview.Patch!.Summary, StringComparison.Ordinal);
        Assert.Contains("14 project gameplay-event", preview.Patch.Summary, StringComparison.Ordinal);

        var applied = handler.Handle(context.State, command with { DryRun = false });
        Assert.True(applied.Succeeded);
        Assert.Equal("scripts/client-project-ready.forge", applied.State.Project.Module.RuntimePolicies
            .LifecycleEvents.Bindings.Single(binding =>
                binding.Event == RuntimeLifecycleEventKind.ProjectLoaded &&
                binding.Authority == RuntimeAuthority.Client).ScriptPath);
        Assert.Equal("scripts/life-events.forge", applied.State.Project.Module.RuntimePolicies.GameplayEvents
            .Bindings.Single(binding => binding.EventName == "life.dead").ScriptPath);
        Assert.False(applied.State.Project.Module.RuntimePolicies.GameplayEvents.AutoRespawn);
        Assert.Same(initial.Extensions, applied.State.Project.Module.RuntimePolicies.Extensions);
        Assert.Same(initial.Rules, applied.State.Project.Module.RuntimePolicies.Rules);
        Assert.Same(initial.World, applied.State.Project.Module.RuntimePolicies.World);

        var history = new ProjectCommandHistory(context.State);
        history.Record(applied);
        Assert.True(history.Undo().Succeeded);
        Assert.Null(history.State.Project.Module.RuntimePolicies.LifecycleEvents.Bindings.Single(binding =>
            binding.Event == RuntimeLifecycleEventKind.ProjectLoaded &&
            binding.Authority == RuntimeAuthority.Client).ScriptPath);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal("scripts/life-events.forge", history.State.Project.Module.RuntimePolicies.GameplayEvents
            .Bindings.Single(binding => binding.EventName == "life.dead").ScriptPath);
    }

    [Fact]
    public void Ux0GGlobalEventsUseOneDryRunnableUndoableCommandAndPreserveNeighborPolicies()
    {
        const string scriptPath = "scripts/global-events.forge";
        var context = new PlaceEntityTestContext();
        var initial = context.State.Project.Module.RuntimePolicies;
        var gameplayBindings = initial.GameplayEvents.Bindings.Select(binding =>
            binding.EventName is "equipment.changed" or "progression.changed"
                ? binding with
                {
                    Handler = "main",
                    ScriptPath = scriptPath,
                    RequiredAtRuntime = true,
                    Provenance = "test:ux0-g",
                }
                : binding).ToArray();
        var gameplay = new ProjectGameplayEventProfile(initial.GameplayEvents.Id,
            initial.GameplayEvents.SchemaVersion, "test:ux0-g", true, false,
            initial.GameplayEvents.UseDownedState, initial.GameplayEvents.AutoRespawn, false, gameplayBindings);
        var restBindings = initial.Rest.Bindings.Select(binding => binding with
        {
            Handler = "main",
            ScriptPath = scriptPath,
            RequiredAtRuntime = binding.EventName == "rest.completed",
            Provenance = "test:ux0-g",
        }).ToArray();
        var rest = new RestLifecycleProfile(initial.Rest.Id, initial.Rest.SchemaVersion, true,
            PartyRestPolicy.WholeParty, 12, restBindings, "test:ux0-g");
        var heartbeat = new HeartbeatScheduleDefinition(Guid.NewGuid(), 1, "World simulation",
            HeartbeatScope.Module, true, 1_500, 25, true, "1.0", "main", "test:ux0-g",
            scriptPath, true);
        var custom = new CustomEventSchema(Guid.NewGuid(), 1, "test:ux0-g", "module", "portal_opened", 1,
            new Dictionary<string, string> { ["message"] = "string" }, false,
            "1.0", "main", EventAuthorityScope.Server, scriptPath, true);
        var world = new ProjectWorldProfile(initial.World.Id, initial.World.SchemaVersion, "test:ux0-g", true,
            initial.World.RequiredAtRuntime, initial.World.CustomEvents.Append(custom).ToArray(), initial.World.Clock);
        var configured = new ModuleRuntimePolicyProfile(initial.Id, initial.SchemaVersion,
            initial.Heartbeats.Append(heartbeat).ToArray(), rest, initial.SequenceAbort, "test:ux0-g",
            initial.Cinematics, initial.Gameplay, initial.Rules, initial.Extensions, gameplay, world,
            initial.LifecycleEvents);
        var handler = new SetModuleRuntimePoliciesHandler(new FixedClock(PlaceEntityTestContext.Timestamp));
        var command = new SetModuleRuntimePoliciesCommand(CommandId.From(Guid.NewGuid()),
            context.State.Revision, configured, true);

        var preview = handler.Handle(context.State, command);
        Assert.True(preview.Succeeded);
        Assert.Same(context.State, preview.State);
        Assert.Contains("1 global heartbeat", preview.Patch!.Summary, StringComparison.Ordinal);
        Assert.Contains("4 rest-event", preview.Patch.Summary, StringComparison.Ordinal);
        Assert.Contains("1 custom world event", preview.Patch.Summary, StringComparison.Ordinal);

        var applied = handler.Handle(context.State, command with { DryRun = false });
        Assert.True(applied.Succeeded);
        var policies = applied.State.Project.Module.RuntimePolicies;
        Assert.Equal(scriptPath, Assert.Single(policies.Heartbeats).ScriptPath);
        Assert.All(policies.Rest.Bindings, binding => Assert.Equal(scriptPath, binding.ScriptPath));
        Assert.Equal(scriptPath, Assert.Single(policies.World.CustomEvents).ScriptPath);
        Assert.False(policies.GameplayEvents.ReplicateEquipmentAndProgression);
        Assert.Same(initial.Extensions, policies.Extensions);
        Assert.Same(initial.Rules, policies.Rules);
        Assert.Same(initial.LifecycleEvents, policies.LifecycleEvents);

        var history = new ProjectCommandHistory(context.State);
        history.Record(applied);
        Assert.True(history.Undo().Succeeded);
        Assert.Empty(history.State.Project.Module.RuntimePolicies.Heartbeats);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(scriptPath, history.State.Project.Module.RuntimePolicies.World.CustomEvents.Single().ScriptPath);
    }

    [Fact]
    public void Ux0HWorldTransitionsUseOneDryRunnableUndoableCommandAndPreserveStartupFlow()
    {
        const string scriptPath = "scripts/sequence-abort.forge";
        var context = new PlaceEntityTestContext();
        var initial = context.State.Project.Module.RuntimePolicies;
        var area = context.State.Project.Areas.Single();
        var abort = new SequenceAbortPolicy(initial.SequenceAbort.Id, initial.SequenceAbort.SchemaVersion, true,
            initial.SequenceAbort.Binding with
            {
                Handler = "main",
                ScriptPath = scriptPath,
                RequiredAtRuntime = true,
                Provenance = "test:ux0-h",
            }, SequenceAbortRestoreFlags.All, 32, "test:ux0-h");
        var clock = new WorldClockProfile(initial.World.Clock.Id, initial.World.Clock.SchemaVersion,
            "test:ux0-h", 30, 10, 20, 50, 2.5, false, 95,
            new Dictionary<string, int> { [area.Id.ToString()] = 25 },
            new Dictionary<WorldAstronomicalEventKind, int>
            {
                [WorldAstronomicalEventKind.Dawn] = 125,
                [WorldAstronomicalEventKind.Midnight] = 0,
            });
        var world = new ProjectWorldProfile(initial.World.Id, initial.World.SchemaVersion, "test:ux0-h",
            true, false, initial.World.CustomEvents, clock);
        var progression = new ProgressionPolicy(initial.Rules.Progression.Id,
            initial.Rules.Progression.SchemaVersion, initial.Rules.Progression.RulesetId,
            [new(1, 0), new(2, 120), new(3, 360), new(4, 840)], 1.2, .9,
            new Dictionary<string, double>
            {
                ["combat"] = 1.1,
                ["exploration"] = .8,
                ["quest"] = 1.4,
            }, true, "test:ux0-h");
        var rules = new ModuleRulesProfile(initial.Rules.Id, initial.Rules.SchemaVersion, progression,
            initial.Rules.Difficulties, initial.Rules.ActiveDifficultyKey, initial.Rules.Mutators,
            "test:ux0-h", initial.Rules.Plugins);
        var configured = new ModuleRuntimePolicyProfile(initial.Id, initial.SchemaVersion,
            initial.Heartbeats, initial.Rest, abort, "test:ux0-h", initial.Cinematics, initial.Gameplay,
            rules, initial.Extensions, initial.GameplayEvents, world, initial.LifecycleEvents);
        var handler = new SetModuleRuntimePoliciesHandler(new FixedClock(PlaceEntityTestContext.Timestamp));
        var command = new SetModuleRuntimePoliciesCommand(CommandId.From(Guid.NewGuid()),
            context.State.Revision, configured, true);

        var preview = handler.Handle(context.State, command);
        Assert.True(preview.Succeeded);
        Assert.Same(context.State, preview.State);
        Assert.Contains("calendar 30x10", preview.Patch!.Summary, StringComparison.Ordinal);
        Assert.Contains("4 progression point", preview.Patch.Summary, StringComparison.Ordinal);

        var applied = handler.Handle(context.State, command with { DryRun = false });
        Assert.True(applied.Succeeded);
        var policies = applied.State.Project.Module.RuntimePolicies;
        Assert.Equal(scriptPath, policies.SequenceAbort.Binding.ScriptPath);
        Assert.Equal(30, policies.World.Clock.DaysPerMonth);
        Assert.Equal(25, policies.World.Clock.AreaTimeZoneOffsets[area.Id.ToString()]);
        Assert.Equal(840, policies.Rules.Progression.Curve[^1].RequiredExperience);
        Assert.Same(initial.Cinematics, policies.Cinematics);
        Assert.Same(initial.Gameplay, policies.Gameplay);
        Assert.Same(initial.Extensions, policies.Extensions);

        var history = new ProjectCommandHistory(context.State);
        history.Record(applied);
        Assert.True(history.Undo().Succeeded);
        Assert.Null(history.State.Project.Module.RuntimePolicies.SequenceAbort.Binding.ScriptPath);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(30, history.State.Project.Module.RuntimePolicies.World.Clock.DaysPerMonth);
    }
}
