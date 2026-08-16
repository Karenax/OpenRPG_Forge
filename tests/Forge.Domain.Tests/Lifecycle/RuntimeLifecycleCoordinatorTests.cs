using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Lifecycle;
using Forge.Domain.Projects;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Spatial;

namespace Forge.Domain.Tests.Lifecycle;

public sealed class RuntimeLifecycleCoordinatorTests
{
    private static readonly AreaId FirstAreaId =
        AreaId.From(Guid.Parse("d1000000-0000-4000-8000-000000000001"));
    private static readonly AreaId SecondAreaId =
        AreaId.From(Guid.Parse("d1000000-0000-4000-8000-000000000002"));
    private static readonly EntityId ActorId =
        EntityId.From(Guid.Parse("d2000000-0000-4000-8000-000000000001"));

    [Fact]
    public void StartupPublishesDeterministicProjectAreaWorldOrder()
    {
        var observed = new List<RuntimeLifecycleEvent>();
        var coordinator = new RuntimeLifecycleCoordinator(
            new SequentialEventIds(),
            [observed.Add]);
        var project = CreateProject();

        var loaded = coordinator.LoadProject(project, RuntimeAuthority.SinglePlayer, RuntimeLifecycleCause.Startup);
        var entered = coordinator.EnterInitialArea(FirstAreaId, ActorId, RuntimeLifecycleCause.Startup);
        var started = coordinator.StartWorld(RuntimeLifecycleCause.Startup);

        Assert.True(loaded.Succeeded && entered.Succeeded && started.Succeeded);
        Assert.Equal(
            [
                RuntimeLifecycleEventKind.ProjectLoaded,
                RuntimeLifecycleEventKind.AreaEntered,
                RuntimeLifecycleEventKind.WorldStarted,
            ],
            observed.Select(@event => @event.Kind));
        Assert.Equal([1L, 2L, 3L], observed.Select(@event => @event.Sequence));
        Assert.Equal(["project.loaded", "area.entered", "world.started"], observed.Select(@event => @event.Binding));
        Assert.All(observed, @event => Assert.Equal(RuntimeAuthority.SinglePlayer, @event.Authority));
        Assert.Equal(RuntimeLifecycleEvent.CurrentForgeScriptApiVersion, observed[0].ForgeScriptApiVersion);
        Assert.Equal(ActorId, observed[1].ActorEntityId);
    }

    [Fact]
    public void TransitionPublishesExitBeforeEnterWithSharedContext()
    {
        var observed = new List<RuntimeLifecycleEvent>();
        var coordinator = Started(CreateProject(), observed);
        var portalId = PortalId.From(Guid.Parse("d3000000-0000-4000-8000-000000000001"));

        var result = coordinator.Transition(
            SecondAreaId,
            ActorId,
            portalId,
            RuntimeLifecycleCause.PortalTransition);

        Assert.True(result.Succeeded);
        Assert.Equal([RuntimeLifecycleEventKind.AreaExited, RuntimeLifecycleEventKind.AreaEntered], result.Events.Select(@event => @event.Kind));
        Assert.All(result.Events, @event =>
        {
            Assert.Equal(FirstAreaId, @event.PreviousAreaId);
            Assert.Equal(SecondAreaId, @event.CurrentAreaId);
            Assert.Equal(ActorId, @event.ActorEntityId);
            Assert.Equal(portalId, @event.TransitionId);
            Assert.Equal(RuntimeLifecycleCause.PortalTransition, @event.Cause);
        });
        Assert.Equal(SecondAreaId, coordinator.CurrentAreaId);
        Assert.Equal([4L, 5L], result.Events.Select(@event => @event.Sequence));
    }

    [Fact]
    public void Transition_resolves_area_specific_exit_and_enter_forgescript_bindings()
    {
        var source = CreateProject();
        var first = source.Areas.Single(item => item.Id == FirstAreaId);
        var second = source.Areas.Single(item => item.Id == SecondAreaId);
        first = first.WithFoundation(WithBindings(first, null,
            new(Guid.Parse("a0285000-0000-4000-8000-000000000001"), "area.exited",
                "scripts/first_exited.forge", "1.0.0", true, "test:r028")));
        second = second.WithFoundation(WithBindings(second,
            new(Guid.Parse("a0285000-0000-4000-8000-000000000002"), "area.entered",
                "scripts/second_entered.forge", "1.0.0", false, "test:r028"), null));
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, source.Blueprints,
            [first, second], source.Dialogues, source.Quests);
        var coordinator = Started(project, []);

        var result = coordinator.Transition(SecondAreaId, ActorId, null,
            RuntimeLifecycleCause.PortalTransition);

        Assert.Equal("scripts/first_exited.forge", result.Events[0].ScriptPath);
        Assert.True(result.Events[0].ScriptRequired);
        Assert.Equal("scripts/second_entered.forge", result.Events[1].ScriptPath);
        Assert.False(result.Events[1].ScriptRequired);
    }

    [Fact]
    public void DryRunPreviewsEventWithoutPublishingOrAdvancingSequence()
    {
        var observed = new List<RuntimeLifecycleEvent>();
        var coordinator = new RuntimeLifecycleCoordinator(new SequentialEventIds(), [observed.Add]);
        var project = CreateProject();

        var preview = coordinator.LoadProject(
            project,
            RuntimeAuthority.Editor,
            RuntimeLifecycleCause.Playtest,
            dryRun: true);

        Assert.True(preview.Succeeded);
        Assert.False(preview.Applied);
        Assert.Empty(observed);
        Assert.Equal(0, coordinator.Sequence);
        Assert.Null(coordinator.CurrentAreaId);
        Assert.True(coordinator.LoadProject(project, RuntimeAuthority.Editor, RuntimeLifecycleCause.Playtest).Succeeded);
    }

    [Theory]
    [InlineData(RuntimeAuthority.Editor)]
    [InlineData(RuntimeAuthority.Client)]
    [InlineData(RuntimeAuthority.Server)]
    public void Project_and_world_events_preserve_the_requested_runtime_authority(RuntimeAuthority authority)
    {
        var observed = new List<RuntimeLifecycleEvent>();
        var coordinator = new RuntimeLifecycleCoordinator(new SequentialEventIds(), [observed.Add]);
        var project = CreateProject();

        Assert.True(coordinator.LoadProject(project, authority, RuntimeLifecycleCause.Playtest).Succeeded);
        Assert.True(coordinator.EnterInitialArea(FirstAreaId, ActorId, RuntimeLifecycleCause.Playtest).Succeeded);
        Assert.True(coordinator.StartWorld(RuntimeLifecycleCause.Playtest).Succeeded);

        Assert.Equal(["project.loaded", "area.entered", "world.started"],
            observed.Select(item => item.Binding));
        Assert.All(observed, item => Assert.Equal(authority, item.Authority));
    }

    [Fact]
    public void Ux0FProjectAndWorldEventsResolveTheConfiguredScriptForTheActiveContext()
    {
        var source = CreateProject();
        var policies = source.Module.RuntimePolicies;
        var bindings = policies.LifecycleEvents.Bindings.Select(binding =>
        {
            if (binding.Authority != RuntimeAuthority.Server)
                return binding;
            return binding.Event switch
            {
                RuntimeLifecycleEventKind.ProjectLoaded => binding with
                {
                    Handler = "OnServerProjectReady",
                    ScriptPath = "scripts/server-project-ready.forge",
                    RequiredAtRuntime = true,
                    Provenance = "test:ux0-f",
                },
                RuntimeLifecycleEventKind.WorldStarted => binding with
                {
                    Handler = "OnServerWorldReady",
                    ScriptPath = "scripts/server-world-ready.forge",
                    RequiredAtRuntime = false,
                    Provenance = "test:ux0-f",
                },
                _ => binding,
            };
        }).ToArray();
        var lifecycle = new ProjectLifecycleEventProfile(policies.LifecycleEvents.Id,
            policies.LifecycleEvents.SchemaVersion, "test:ux0-f", true, false, bindings);
        var updatedPolicies = new ModuleRuntimePolicyProfile(policies.Id, policies.SchemaVersion,
            policies.Heartbeats, policies.Rest, policies.SequenceAbort, "test:ux0-f", policies.Cinematics,
            policies.Gameplay, policies.Rules, policies.Extensions, policies.GameplayEvents, policies.World,
            lifecycle);
        var project = new ProjectDefinition(source.Id, source.Title,
            source.Module.WithRuntimePolicies(updatedPolicies), source.Blueprints, source.Areas,
            source.Dialogues, source.Quests);
        var observed = new List<RuntimeLifecycleEvent>();
        var coordinator = new RuntimeLifecycleCoordinator(new SequentialEventIds(), [observed.Add]);

        Assert.True(coordinator.LoadProject(project, RuntimeAuthority.Server, RuntimeLifecycleCause.Startup).Succeeded);
        Assert.True(coordinator.EnterInitialArea(FirstAreaId, ActorId, RuntimeLifecycleCause.Startup).Succeeded);
        Assert.True(coordinator.StartWorld(RuntimeLifecycleCause.Startup).Succeeded);

        Assert.Equal("scripts/server-project-ready.forge", observed[0].ScriptPath);
        Assert.Equal("OnServerProjectReady", observed[0].ScriptHandler);
        Assert.True(observed[0].ScriptRequired);
        Assert.Equal("scripts/server-world-ready.forge", observed[2].ScriptPath);
        Assert.Equal("OnServerWorldReady", observed[2].ScriptHandler);
        Assert.False(observed[2].ScriptRequired);
        Assert.Null(observed[1].ScriptHandler);
    }

    [Fact]
    public void InvalidOrderAndReferencesReturnStableDiagnostics()
    {
        var project = CreateProject();
        var coordinator = new RuntimeLifecycleCoordinator(new SequentialEventIds());
        var beforeLoad = coordinator.StartWorld(RuntimeLifecycleCause.Startup);
        coordinator.LoadProject(project, RuntimeAuthority.Server, RuntimeLifecycleCause.Startup);
        var missingArea = coordinator.EnterInitialArea(
            AreaId.From(Guid.Parse("d1000000-0000-4000-8000-000000000099")),
            null,
            RuntimeLifecycleCause.Startup);
        var missingActor = coordinator.EnterInitialArea(
            FirstAreaId,
            EntityId.From(Guid.Parse("d2000000-0000-4000-8000-000000000099")),
            RuntimeLifecycleCause.Startup);

        Assert.Equal(RuntimeLifecycleCoordinator.ProjectMissingCode, Assert.Single(beforeLoad.Diagnostics).Code);
        Assert.Equal(RuntimeLifecycleCoordinator.AreaMissingCode, Assert.Single(missingArea.Diagnostics).Code);
        Assert.Equal(RuntimeLifecycleCoordinator.ActorMissingCode, Assert.Single(missingActor.Diagnostics).Code);
    }

    [Fact]
    public void TransitionRejectsAnActorThatIsNotInTheCurrentArea()
    {
        var project = CreateProject();
        var coordinator = Started(project, []);

        var result = coordinator.Transition(
            SecondAreaId,
            EntityId.From(Guid.Parse("d2000000-0000-4000-8000-000000000099")),
            null,
            RuntimeLifecycleCause.ScriptedTransition);

        Assert.False(result.Succeeded);
        Assert.Equal(RuntimeLifecycleCoordinator.ActorMissingCode, Assert.Single(result.Diagnostics).Code);
        Assert.Equal(FirstAreaId, coordinator.CurrentAreaId);
        Assert.Equal(3, coordinator.Sequence);
    }

    private static RuntimeLifecycleCoordinator Started(
        ProjectDefinition project,
        List<RuntimeLifecycleEvent> observed)
    {
        var coordinator = new RuntimeLifecycleCoordinator(new SequentialEventIds(), [observed.Add]);
        coordinator.LoadProject(project, RuntimeAuthority.Server, RuntimeLifecycleCause.Startup);
        coordinator.EnterInitialArea(FirstAreaId, ActorId, RuntimeLifecycleCause.Startup);
        coordinator.StartWorld(RuntimeLifecycleCause.Startup);
        observed.Clear();
        return coordinator;
    }

    private static ProjectDefinition CreateProject()
    {
        var blueprintId = BlueprintId.From(Guid.Parse("d4000000-0000-4000-8000-000000000001"));
        var blueprint = new BlueprintDefinition(blueprintId, "player", "Player", BlueprintKind.PlayerSpawn);
        var actor = new EntityInstance(ActorId, blueprintId, TransformValue.Identity);
        return new ProjectDefinition(
            ProjectId.From(Guid.Parse("d5000000-0000-4000-8000-000000000001")),
            "Lifecycle project",
            [blueprint],
            [
                new AreaDefinition(FirstAreaId, "first", "First", [actor]),
                new AreaDefinition(SecondAreaId, "second", "Second", []),
            ]);
    }

    private static AreaFoundationProfile WithBindings(AreaDefinition area,
        AreaLifecycleScriptBinding? entered, AreaLifecycleScriptBinding? exited)
    {
        var source = area.Foundation;
        return new(source.Id, source.SchemaVersion, "test:r028", source.Creation, source.TileKit,
            new(entered, exited), source.Volumes, source.Dependencies);
    }

    private sealed class SequentialEventIds : IRuntimeLifecycleEventIdSource
    {
        private int _value;

        public RuntimeLifecycleEventId Next()
        {
            _value++;
            return RuntimeLifecycleEventId.From(Guid.Parse($"d6000000-0000-4000-8000-{_value:000000000000}"));
        }
    }
}
