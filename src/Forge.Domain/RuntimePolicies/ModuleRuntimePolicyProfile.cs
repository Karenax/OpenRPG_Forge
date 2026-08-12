using Forge.Domain.Lifecycle;
using Forge.Domain.Gameplay;
using Forge.Domain.Modules;
using Forge.Domain.Sequences;
using Forge.Domain.Rules;
using Forge.Domain.RuntimeExtensions;

namespace Forge.Domain.RuntimePolicies;

public sealed record ModuleRuntimePolicyProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public IReadOnlyList<HeartbeatScheduleDefinition> Heartbeats { get; }
    public RestLifecycleProfile Rest { get; }
    public SequenceAbortPolicy SequenceAbort { get; }
    public ModuleCinematicProfile Cinematics { get; }
    public ModuleGameplayControlProfile Gameplay { get; }
    public ModuleRulesProfile Rules { get; }
    public ModuleRuntimeExtensionProfile Extensions { get; }
    public ProjectGameplayEventProfile GameplayEvents { get; }
    public ProjectWorldProfile World { get; }
    public string Provenance { get; }

    public ModuleRuntimePolicyProfile(Guid id, int schemaVersion,
        IReadOnlyList<HeartbeatScheduleDefinition> heartbeats, RestLifecycleProfile rest,
        SequenceAbortPolicy sequenceAbort, string provenance, ModuleCinematicProfile? cinematics = null,
        ModuleGameplayControlProfile? gameplay = null, ModuleRulesProfile? rules = null,
        ModuleRuntimeExtensionProfile? extensions = null, ProjectGameplayEventProfile? gameplayEvents = null,
        ProjectWorldProfile? world = null)
    {
        ArgumentNullException.ThrowIfNull(heartbeats);
        ArgumentNullException.ThrowIfNull(rest);
        ArgumentNullException.ThrowIfNull(sequenceAbort);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var schedules = heartbeats.OrderBy(item => item.Id).ToArray();
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion ||
            schedules.Select(item => item.Id).Distinct().Count() != schedules.Length ||
            schedules.Any(item => item.Scope != HeartbeatScope.Module))
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Module runtime policy profile is invalid.");
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Heartbeats = schedules;
        Rest = rest;
        SequenceAbort = sequenceAbort;
        Cinematics = cinematics ?? ModuleCinematicProfile.CreateDefault(ModuleId.From(id));
        Gameplay = gameplay ?? ModuleGameplayControlProfile.CreateDefault(ModuleId.From(id));
        Rules = rules ?? ModuleRulesProfile.CreateDefault(ModuleId.From(id));
        Extensions = extensions ?? ModuleRuntimeExtensionProfile.CreateDefault(ModuleId.From(id));
        GameplayEvents = gameplayEvents ?? ProjectGameplayEventProfile.CreateDefault(id);
        World = world ?? ProjectWorldProfile.CreateDefault(id);
        Provenance = provenance.Trim();
    }

    public static ModuleRuntimePolicyProfile CreateDefault(ModuleId moduleId)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        var restId = RuntimePolicyIdFactory.Create(moduleId.Value, "rest");
        var bindings = RestLifecycleProfile.RequiredEvents.Order(StringComparer.Ordinal).Select(eventName =>
            new EventBinding(RuntimePolicyIdFactory.Create(moduleId.Value, $"rest:{eventName}"), 1,
                "migration-default", "1.0", eventName, HandlerName(eventName), EventAuthorityScope.Server)).ToArray();
        var rest = new RestLifecycleProfile(restId, RestLifecycleProfile.CurrentSchemaVersion, false,
            PartyRestPolicy.Individual, 8, bindings, "migration-default");
        var abortBinding = new EventBinding(RuntimePolicyIdFactory.Create(moduleId.Value, "sequence:aborted"), 1,
            "migration-default", "1.0", "sequence.aborted", "OnSequenceAborted", EventAuthorityScope.Server);
        var abort = new SequenceAbortPolicy(RuntimePolicyIdFactory.Create(moduleId.Value, "sequence-abort"),
            SequenceAbortPolicy.CurrentSchemaVersion, true, abortBinding, SequenceAbortRestoreFlags.All, 64,
            "migration-default");
        return new ModuleRuntimePolicyProfile(moduleId.Value, CurrentSchemaVersion, [], rest, abort,
            "migration-default", ModuleCinematicProfile.CreateDefault(moduleId),
            ModuleGameplayControlProfile.CreateDefault(moduleId), ModuleRulesProfile.CreateDefault(moduleId));
    }

    private static string HandlerName(string eventName) => eventName switch
    {
        "rest.requested" => "OnRestRequested",
        "rest.started" => "OnRestStarted",
        "rest.interrupted" => "OnRestInterrupted",
        "rest.completed" => "OnRestCompleted",
        _ => throw new ArgumentOutOfRangeException(nameof(eventName)),
    };
}
