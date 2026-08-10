using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Domain.Lifecycle;

public sealed class RuntimeLifecycleCoordinator
{
    public const string ProjectAlreadyLoadedCode = "runtime.lifecycle.project_already_loaded";
    public const string ProjectMissingCode = "runtime.lifecycle.project_missing";
    public const string InitialAreaAlreadyEnteredCode = "runtime.lifecycle.initial_area_already_entered";
    public const string AreaMissingCode = "runtime.lifecycle.area_missing";
    public const string ActorMissingCode = "runtime.lifecycle.actor_missing";
    public const string WorldAlreadyStartedCode = "runtime.lifecycle.world_already_started";
    public const string WorldNotStartedCode = "runtime.lifecycle.world_not_started";
    public const string SameAreaCode = "runtime.lifecycle.same_area";

    private readonly IRuntimeLifecycleEventIdSource _eventIds;
    private readonly List<Action<RuntimeLifecycleEvent>> _observers;
    private ProjectDefinition? _project;
    private AreaId? _currentAreaId;
    private RuntimeAuthority _authority;
    private long _sequence;
    private bool _worldStarted;

    public RuntimeLifecycleCoordinator(
        IRuntimeLifecycleEventIdSource? eventIds = null,
        IReadOnlyList<Action<RuntimeLifecycleEvent>>? observers = null)
    {
        _eventIds = eventIds ?? new RandomRuntimeLifecycleEventIdSource();
        _observers = (observers ?? []).ToList();
    }

    public AreaId? CurrentAreaId => _currentAreaId;

    public long Sequence => _sequence;

    public RuntimeLifecycleDispatchResult LoadProject(
        ProjectDefinition project,
        RuntimeAuthority authority,
        RuntimeLifecycleCause cause,
        bool dryRun = false)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (_project is not null)
        {
            return Failure(ProjectAlreadyLoadedCode, "A project is already loaded in this lifecycle coordinator.", "project.json");
        }

        var @event = Create(
            project,
            RuntimeLifecycleEventKind.ProjectLoaded,
            null,
            null,
            null,
            null,
            cause,
            authority,
            _sequence + 1);
        if (!dryRun)
        {
            _project = project;
            _authority = authority;
            Publish(@event);
        }

        return Success([@event], !dryRun);
    }

    public RuntimeLifecycleDispatchResult EnterInitialArea(
        AreaId areaId,
        EntityId? actorEntityId,
        RuntimeLifecycleCause cause,
        bool dryRun = false)
    {
        if (_project is null)
        {
            return Failure(ProjectMissingCode, "Load a project before entering its initial area.", "project.json");
        }

        if (_currentAreaId is not null)
        {
            return Failure(InitialAreaAlreadyEnteredCode, "The initial area has already been entered.", $"areas/{_currentAreaId}");
        }

        var validation = ValidateAreaAndActor(_project, areaId, actorEntityId);
        if (validation is not null)
        {
            return validation;
        }

        var @event = Create(
            _project,
            RuntimeLifecycleEventKind.AreaEntered,
            null,
            areaId,
            actorEntityId,
            null,
            cause,
            _authority,
            _sequence + 1);
        if (!dryRun)
        {
            _currentAreaId = areaId;
            Publish(@event);
        }

        return Success([@event], !dryRun);
    }

    public RuntimeLifecycleDispatchResult StartWorld(
        RuntimeLifecycleCause cause,
        bool dryRun = false)
    {
        if (_project is null || _currentAreaId is null)
        {
            return Failure(ProjectMissingCode, "Load a project and enter its initial area before starting the world.", "project.json");
        }

        if (_worldStarted)
        {
            return Failure(WorldAlreadyStartedCode, "The world has already started.", "project.json");
        }

        var @event = Create(
            _project,
            RuntimeLifecycleEventKind.WorldStarted,
            null,
            _currentAreaId,
            null,
            null,
            cause,
            _authority,
            _sequence + 1);
        if (!dryRun)
        {
            _worldStarted = true;
            Publish(@event);
        }

        return Success([@event], !dryRun);
    }

    public RuntimeLifecycleDispatchResult Transition(
        AreaId destinationAreaId,
        EntityId? actorEntityId,
        PortalId? transitionId,
        RuntimeLifecycleCause cause,
        bool dryRun = false)
    {
        if (_project is null || _currentAreaId is null || !_worldStarted)
        {
            return Failure(WorldNotStartedCode, "Start the world before transitioning between areas.", "runtime/lifecycle");
        }

        if (_currentAreaId == destinationAreaId)
        {
            return Failure(SameAreaCode, "An area transition must change the active area.", $"areas/{destinationAreaId}");
        }

        var validation = ValidateTransition(_project, _currentAreaId, destinationAreaId, actorEntityId);
        if (validation is not null)
        {
            return validation;
        }

        var previous = _currentAreaId;
        var exited = Create(
            _project,
            RuntimeLifecycleEventKind.AreaExited,
            previous,
            destinationAreaId,
            actorEntityId,
            transitionId,
            cause,
            _authority,
            _sequence + 1);
        var entered = Create(
            _project,
            RuntimeLifecycleEventKind.AreaEntered,
            previous,
            destinationAreaId,
            actorEntityId,
            transitionId,
            cause,
            _authority,
            _sequence + 2);
        if (!dryRun)
        {
            Publish(exited);
            _currentAreaId = destinationAreaId;
            Publish(entered);
        }

        return Success([exited, entered], !dryRun);
    }

    private RuntimeLifecycleDispatchResult? ValidateAreaAndActor(
        ProjectDefinition project,
        AreaId areaId,
        EntityId? actorEntityId)
    {
        var area = project.Areas.FirstOrDefault(candidate => candidate.Id == areaId);
        if (area is null)
        {
            return Failure(AreaMissingCode, $"Area '{areaId}' does not exist.", $"areas/{areaId}");
        }

        if (actorEntityId is not null && !area.Entities.Any(entity => entity.Id == actorEntityId))
        {
            return Failure(
                ActorMissingCode,
                $"Actor '{actorEntityId}' does not exist in area '{areaId}'.",
                $"areas/{areaId}/entities/{actorEntityId}");
        }

        return null;
    }

    private RuntimeLifecycleDispatchResult? ValidateTransition(
        ProjectDefinition project,
        AreaId currentAreaId,
        AreaId destinationAreaId,
        EntityId? actorEntityId)
    {
        var destination = project.Areas.FirstOrDefault(area => area.Id == destinationAreaId);
        if (destination is null)
        {
            return Failure(
                AreaMissingCode,
                $"Area '{destinationAreaId}' does not exist.",
                $"areas/{destinationAreaId}");
        }

        if (actorEntityId is null)
        {
            return null;
        }

        var current = project.Areas.Single(area => area.Id == currentAreaId);
        return current.Entities.Any(entity => entity.Id == actorEntityId)
            ? null
            : Failure(
                ActorMissingCode,
                $"Actor '{actorEntityId}' does not exist in current area '{currentAreaId}'.",
                $"areas/{currentAreaId}/entities/{actorEntityId}");
    }

    private RuntimeLifecycleEvent Create(
        ProjectDefinition project,
        RuntimeLifecycleEventKind kind,
        AreaId? previousAreaId,
        AreaId? currentAreaId,
        EntityId? actorEntityId,
        PortalId? transitionId,
        RuntimeLifecycleCause cause,
        RuntimeAuthority authority,
        long sequence) =>
        CreateWithBinding(project, kind, previousAreaId, currentAreaId, actorEntityId, transitionId, cause,
            authority, sequence);

    private RuntimeLifecycleEvent CreateWithBinding(
        ProjectDefinition project,
        RuntimeLifecycleEventKind kind,
        AreaId? previousAreaId,
        AreaId? currentAreaId,
        EntityId? actorEntityId,
        PortalId? transitionId,
        RuntimeLifecycleCause cause,
        RuntimeAuthority authority,
        long sequence)
    {
        var binding = kind switch
        {
            RuntimeLifecycleEventKind.AreaEntered when currentAreaId is not null =>
                project.Areas.Single(area => area.Id == currentAreaId).Foundation.Lifecycle.Entered,
            RuntimeLifecycleEventKind.AreaExited when previousAreaId is not null =>
                project.Areas.Single(area => area.Id == previousAreaId).Foundation.Lifecycle.Exited,
            _ => null,
        };
        return new(
            _eventIds.Next(),
            RuntimeLifecycleEvent.CurrentSchemaVersion,
            sequence,
            kind,
            project.Id,
            project.Module.Id,
            previousAreaId,
            currentAreaId,
            actorEntityId,
            transitionId,
            cause,
            authority,
            "runtime-core",
            binding?.ScriptPath,
            binding?.Required ?? false);
    }

    private void Publish(RuntimeLifecycleEvent @event)
    {
        _sequence = @event.Sequence;
        foreach (var observer in _observers)
        {
            observer(@event);
        }
    }

    private static RuntimeLifecycleDispatchResult Success(
        IReadOnlyList<RuntimeLifecycleEvent> events,
        bool applied) =>
        new(true, events, [], applied);

    private static RuntimeLifecycleDispatchResult Failure(string code, string message, string path) =>
        new(false, [], [new DomainDiagnostic(code, message, path)], false);
}
