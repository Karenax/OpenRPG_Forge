using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Modules;
using Forge.Domain.Projects;

namespace Forge.Domain.Lifecycle;

public sealed record RuntimeLifecycleEvent
{
    public const int CurrentSchemaVersion = 1;
    public const string CurrentForgeScriptApiVersion = "1.0.0";

    public RuntimeLifecycleEventId Id { get; }

    public int SchemaVersion { get; }

    public long Sequence { get; }

    public RuntimeLifecycleEventKind Kind { get; }

    public string Binding { get; }

    public string ForgeScriptApiVersion { get; }

    public ProjectId ProjectId { get; }

    public ModuleId ModuleId { get; }

    public AreaId? PreviousAreaId { get; }

    public AreaId? CurrentAreaId { get; }

    public EntityId? ActorEntityId { get; }

    public PortalId? TransitionId { get; }

    public RuntimeLifecycleCause Cause { get; }

    public RuntimeAuthority Authority { get; }

    public string Provenance { get; }

    public string DocumentPath { get; }

    public string? ScriptPath { get; }

    public bool ScriptRequired { get; }

    public RuntimeLifecycleEvent(
        RuntimeLifecycleEventId id,
        int schemaVersion,
        long sequence,
        RuntimeLifecycleEventKind kind,
        ProjectId projectId,
        ModuleId moduleId,
        AreaId? previousAreaId,
        AreaId? currentAreaId,
        EntityId? actorEntityId,
        PortalId? transitionId,
        RuntimeLifecycleCause cause,
        RuntimeAuthority authority,
        string provenance = "runtime-core",
        string? scriptPath = null,
        bool scriptRequired = false)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(projectId);
        ArgumentNullException.ThrowIfNull(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }

        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence));
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Sequence = sequence;
        Kind = kind;
        Binding = BindingFor(kind);
        ForgeScriptApiVersion = CurrentForgeScriptApiVersion;
        ProjectId = projectId;
        ModuleId = moduleId;
        PreviousAreaId = previousAreaId;
        CurrentAreaId = currentAreaId;
        ActorEntityId = actorEntityId;
        TransitionId = transitionId;
        Cause = cause;
        Authority = authority;
        Provenance = provenance.Trim();
        ScriptPath = string.IsNullOrWhiteSpace(scriptPath) ? null : scriptPath.Trim();
        ScriptRequired = scriptRequired;
        DocumentPath = kind switch
        {
            RuntimeLifecycleEventKind.ProjectLoaded or RuntimeLifecycleEventKind.WorldStarted => "project.json",
            RuntimeLifecycleEventKind.AreaEntered => $"areas/{currentAreaId}",
            RuntimeLifecycleEventKind.AreaExited => $"areas/{previousAreaId}",
            _ => "runtime/lifecycle",
        };
    }

    public static string BindingFor(RuntimeLifecycleEventKind kind) => kind switch
    {
        RuntimeLifecycleEventKind.ProjectLoaded => "project.loaded",
        RuntimeLifecycleEventKind.WorldStarted => "world.started",
        RuntimeLifecycleEventKind.AreaEntered => "area.entered",
        RuntimeLifecycleEventKind.AreaExited => "area.exited",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
