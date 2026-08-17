using System.Text.Json.Nodes;
using Forge.Domain.Lifecycle;
using Forge.Domain.Projects;
using Forge.Domain.Rules;
using Forge.Domain.RuntimePolicies;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class ProjectGlobalEventPersistenceTests
{
    [Fact]
    public void Ux0GProjectRoundTripPreservesRestHeartbeatAndUserEventScriptBindings()
    {
        const string scriptPath = "scripts/global-events.forge";
        var source = TestProjectFactory.CreateValid();
        var policies = source.Module.RuntimePolicies;
        var restBindings = policies.Rest.Bindings.Select(binding => binding with
        {
            Handler = "main",
            ScriptPath = scriptPath,
            RequiredAtRuntime = binding.EventName == "rest.completed",
            Provenance = "test:ux0-g",
        }).ToArray();
        var rest = new RestLifecycleProfile(policies.Rest.Id, policies.Rest.SchemaVersion, true,
            PartyRestPolicy.WholeParty, 12, restBindings, "test:ux0-g");
        var heartbeat = new HeartbeatScheduleDefinition(Guid.NewGuid(), 1, "World simulation",
            HeartbeatScope.Module, true, 1_500, 25, true, "1.0", "main", "test:ux0-g",
            scriptPath, true);
        var custom = new CustomEventSchema(Guid.NewGuid(), 1, "test:ux0-g", "module", "portal_opened", 1,
            new Dictionary<string, string> { ["message"] = "string" }, false,
            "1.0", "main", EventAuthorityScope.Server, scriptPath, true);
        var world = new ProjectWorldProfile(policies.World.Id, policies.World.SchemaVersion, "test:ux0-g", true,
            false, [custom], policies.World.Clock);
        var configured = Copy(policies, [heartbeat], rest, world);
        var project = new ProjectDefinition(source.Id, source.Title,
            source.Module.WithRuntimePolicies(configured), source.Blueprints, source.Areas,
            source.Dialogues, source.Quests);

        var json = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(json).Module.RuntimePolicies;

        Assert.Contains("\"formatVersion\": \"0.6.37\"", json, StringComparison.Ordinal);
        var restoredHeartbeat = Assert.Single(restored.Heartbeats);
        Assert.Equal(scriptPath, restoredHeartbeat.ScriptPath);
        Assert.True(restoredHeartbeat.RequiredAtRuntime);
        Assert.All(restored.Rest.Bindings, binding => Assert.Equal(scriptPath, binding.ScriptPath));
        Assert.True(restored.Rest.Bindings.Single(binding => binding.EventName == "rest.completed")
            .RequiredAtRuntime);
        var restoredCustom = Assert.Single(restored.World.CustomEvents);
        Assert.Equal(scriptPath, restoredCustom.ScriptPath);
        Assert.True(restoredCustom.RequiredAtRuntime);
        Assert.Equal("string", restoredCustom.Fields["message"]);
    }

    [Fact]
    public void Ux0GPreviousProjectFormatDefaultsMissingGlobalScriptConfiguration()
    {
        var source = TestProjectFactory.CreateValid();
        var policies = source.Module.RuntimePolicies;
        var heartbeat = new HeartbeatScheduleDefinition(Guid.NewGuid(), 1, "World simulation",
            HeartbeatScope.Module, true, 1_500, 25, true, "1.0", "main", "test:ux0-g");
        var custom = new CustomEventSchema(Guid.NewGuid(), 1, "test:ux0-g", "module", "portal_opened", 1,
            new Dictionary<string, string> { ["message"] = "string" }, false);
        var world = new ProjectWorldProfile(policies.World.Id, policies.World.SchemaVersion, "test:ux0-g", true,
            false, [custom], policies.World.Clock);
        var configured = Copy(policies, [heartbeat], policies.Rest, world);
        var project = new ProjectDefinition(source.Id, source.Title,
            source.Module.WithRuntimePolicies(configured), source.Blueprints, source.Areas,
            source.Dialogues, source.Quests);
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(project))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.PreGlobalEventScriptsFormatVersion;
        var runtime = document["project"]!["module"]!["runtimePolicies"]!;
        RemoveScriptProperties(runtime["heartbeats"]!.AsArray());
        RemoveScriptProperties(runtime["rest"]!["bindings"]!.AsArray());
        RemoveScriptProperties(runtime["world"]!["customEvents"]!.AsArray());

        var restored = ProjectJsonSerializer.Deserialize(document.ToJsonString()).Module.RuntimePolicies;

        Assert.Null(Assert.Single(restored.Heartbeats).ScriptPath);
        Assert.False(restored.Heartbeats[0].RequiredAtRuntime);
        Assert.All(restored.Rest.Bindings, binding =>
        {
            Assert.Null(binding.ScriptPath);
            Assert.False(binding.RequiredAtRuntime);
        });
        Assert.Null(Assert.Single(restored.World.CustomEvents).ScriptPath);
        Assert.False(restored.World.CustomEvents[0].RequiredAtRuntime);
    }

    [Fact]
    public void Ux0HProjectRoundTripPreservesAbortCalendarAndProgressionWithoutMovingStartupFlow()
    {
        const string scriptPath = "scripts/sequence-abort.forge";
        var source = TestProjectFactory.CreateValid();
        var policies = source.Module.RuntimePolicies;
        var area = source.Areas.Single();
        var abort = new SequenceAbortPolicy(policies.SequenceAbort.Id, policies.SequenceAbort.SchemaVersion,
            true, policies.SequenceAbort.Binding with
            {
                Handler = "main",
                ScriptPath = scriptPath,
                RequiredAtRuntime = true,
                Provenance = "test:ux0-h",
            }, SequenceAbortRestoreFlags.All, 32, "test:ux0-h");
        var clock = new WorldClockProfile(policies.World.Clock.Id, policies.World.Clock.SchemaVersion,
            "test:ux0-h", 30, 10, 20, 50, 2.5, false, 95,
            new Dictionary<string, int> { [area.Id.ToString()] = 25 },
            new Dictionary<WorldAstronomicalEventKind, int>
            {
                [WorldAstronomicalEventKind.Dawn] = 125,
                [WorldAstronomicalEventKind.Midnight] = 0,
            });
        var world = new ProjectWorldProfile(policies.World.Id, policies.World.SchemaVersion, "test:ux0-h",
            true, false, policies.World.CustomEvents, clock);
        var progression = new ProgressionPolicy(policies.Rules.Progression.Id,
            policies.Rules.Progression.SchemaVersion, policies.Rules.Progression.RulesetId,
            [new(1, 0), new(2, 120), new(3, 360), new(4, 840)], 1.2, .9,
            new Dictionary<string, double>
            {
                ["combat"] = 1.1,
                ["exploration"] = .8,
                ["quest"] = 1.4,
            }, true, "test:ux0-h");
        var rules = new ModuleRulesProfile(policies.Rules.Id, policies.Rules.SchemaVersion, progression,
            policies.Rules.Difficulties, policies.Rules.ActiveDifficultyKey, policies.Rules.Mutators,
            "test:ux0-h", policies.Rules.Plugins);
        var configured = new ModuleRuntimePolicyProfile(policies.Id, policies.SchemaVersion,
            policies.Heartbeats, policies.Rest, abort, "test:ux0-h", policies.Cinematics,
            policies.Gameplay, rules, policies.Extensions, policies.GameplayEvents, world,
            policies.LifecycleEvents);
        var project = new ProjectDefinition(source.Id, source.Title,
            source.Module.WithRuntimePolicies(configured), source.Blueprints, source.Areas,
            source.Dialogues, source.Quests);

        var json = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(json).Module.RuntimePolicies;

        Assert.Contains("\"formatVersion\": \"0.6.37\"", json, StringComparison.Ordinal);
        Assert.Equal(scriptPath, restored.SequenceAbort.Binding.ScriptPath);
        Assert.True(restored.SequenceAbort.Binding.RequiredAtRuntime);
        Assert.Equal(32, restored.SequenceAbort.MaximumNarrativeRollbackSteps);
        Assert.Equal(30, restored.World.Clock.DaysPerMonth);
        Assert.Equal(25, restored.World.Clock.AreaTimeZoneOffsets[area.Id.ToString()]);
        Assert.Equal(125, restored.World.Clock.AstronomicalMinutes[WorldAstronomicalEventKind.Dawn]);
        Assert.Equal(840, restored.Rules.Progression.Curve[^1].RequiredExperience);
        Assert.Equal(1.4, restored.Rules.Progression.SourceMultipliers["quest"]);
        Assert.Equal(policies.Cinematics.Startup, restored.Cinematics.Startup);
    }

    private static void RemoveScriptProperties(JsonArray entries)
    {
        foreach (var entry in entries)
        {
            entry!.AsObject().Remove("scriptPath");
            entry.AsObject().Remove("requiredAtRuntime");
        }
    }

    private static ModuleRuntimePolicyProfile Copy(
        ModuleRuntimePolicyProfile source,
        IReadOnlyList<HeartbeatScheduleDefinition> heartbeats,
        RestLifecycleProfile rest,
        ProjectWorldProfile world) =>
        new(source.Id, source.SchemaVersion, heartbeats, rest, source.SequenceAbort,
            "test:ux0-g", source.Cinematics, source.Gameplay, source.Rules, source.Extensions,
            source.GameplayEvents, world, source.LifecycleEvents);
}
