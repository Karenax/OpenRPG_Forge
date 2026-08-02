using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Lifecycle;
using Forge.Domain.Projects;
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
