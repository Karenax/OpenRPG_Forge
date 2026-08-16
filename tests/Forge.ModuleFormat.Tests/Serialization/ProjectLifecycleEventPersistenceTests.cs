using System.Text.Json.Nodes;
using Forge.Domain.Lifecycle;
using Forge.Domain.Projects;
using Forge.Domain.RuntimePolicies;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class ProjectLifecycleEventPersistenceTests
{
    [Fact]
    public void Ux0FProjectRoundTripPreservesLifecycleAndGameplayScriptBindings()
    {
        var source = TestProjectFactory.CreateValid();
        var policies = source.Module.RuntimePolicies;
        var lifecycleBindings = policies.LifecycleEvents.Bindings.Select(binding =>
            binding.Event == RuntimeLifecycleEventKind.ProjectLoaded && binding.Authority == RuntimeAuthority.Server
                ? binding with
                {
                    Handler = "OnServerProjectReady",
                    ScriptPath = "scripts/server-project-ready.forge",
                    RequiredAtRuntime = true,
                    Provenance = "test:ux0-f",
                }
                : binding).ToArray();
        var lifecycle = new ProjectLifecycleEventProfile(policies.LifecycleEvents.Id,
            policies.LifecycleEvents.SchemaVersion, "test:ux0-f", true, false, lifecycleBindings);
        var gameplayBindings = policies.GameplayEvents.Bindings.Select(binding => binding.EventName == "item.used"
            ? binding with
            {
                Handler = "OnItemConsumed",
                ScriptPath = "scripts/item-events.forge",
                RequiredAtRuntime = true,
                Provenance = "test:ux0-f",
            }
            : binding).ToArray();
        var gameplay = new ProjectGameplayEventProfile(policies.GameplayEvents.Id,
            policies.GameplayEvents.SchemaVersion, "test:ux0-f", true, false,
            policies.GameplayEvents.UseDownedState, policies.GameplayEvents.AutoRespawn,
            policies.GameplayEvents.ReplicateEquipmentAndProgression, gameplayBindings);
        var updatedPolicies = Copy(policies, lifecycle, gameplay);
        var project = new ProjectDefinition(source.Id, source.Title,
            source.Module.WithRuntimePolicies(updatedPolicies), source.Blueprints, source.Areas,
            source.Dialogues, source.Quests);

        var json = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(json).Module.RuntimePolicies;

        Assert.Contains($"\"formatVersion\": \"{ProjectJsonSerializer.CurrentFormatVersion}\"", json,
            StringComparison.Ordinal);
        var restoredLifecycle = restored.LifecycleEvents.Bindings.Single(binding =>
            binding.Event == RuntimeLifecycleEventKind.ProjectLoaded && binding.Authority == RuntimeAuthority.Server);
        Assert.Equal("scripts/server-project-ready.forge", restoredLifecycle.ScriptPath);
        Assert.Equal("OnServerProjectReady", restoredLifecycle.Handler);
        Assert.True(restoredLifecycle.RequiredAtRuntime);
        var restoredGameplay = restored.GameplayEvents.Bindings.Single(binding => binding.EventName == "item.used");
        Assert.Equal("scripts/item-events.forge", restoredGameplay.ScriptPath);
        Assert.Equal("OnItemConsumed", restoredGameplay.Handler);
        Assert.True(restoredGameplay.RequiredAtRuntime);
    }

    [Fact]
    public void Ux0FPreviousProjectFormatMigratesMissingScriptConfigurationToStableDefaults()
    {
        var source = TestProjectFactory.CreateValid();
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(source))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.PreProjectLifecycleEventsFormatVersion;
        var policies = document["project"]!["module"]!["runtimePolicies"]!.AsObject();
        policies.Remove("lifecycleEvents");
        foreach (var binding in policies["gameplayEvents"]!["bindings"]!.AsArray())
        {
            binding!.AsObject().Remove("scriptPath");
            binding.AsObject().Remove("requiredAtRuntime");
        }

        var first = ProjectJsonSerializer.Deserialize(document.ToJsonString()).Module.RuntimePolicies;
        var second = ProjectJsonSerializer.Deserialize(document.ToJsonString()).Module.RuntimePolicies;

        Assert.Equal(8, first.LifecycleEvents.Bindings.Count);
        Assert.Equal(first.LifecycleEvents.Bindings.Select(binding => binding.Id),
            second.LifecycleEvents.Bindings.Select(binding => binding.Id));
        Assert.All(first.LifecycleEvents.Bindings, binding => Assert.Null(binding.ScriptPath));
        Assert.All(first.GameplayEvents.Bindings, binding =>
        {
            Assert.Null(binding.ScriptPath);
            Assert.False(binding.RequiredAtRuntime);
        });
    }

    private static ModuleRuntimePolicyProfile Copy(
        ModuleRuntimePolicyProfile source,
        ProjectLifecycleEventProfile lifecycle,
        ProjectGameplayEventProfile gameplay) =>
        new(source.Id, source.SchemaVersion, source.Heartbeats, source.Rest, source.SequenceAbort,
            "test:ux0-f", source.Cinematics, source.Gameplay, source.Rules, source.Extensions,
            gameplay, source.World, lifecycle);
}
