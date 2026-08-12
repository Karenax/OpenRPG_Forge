using System.Text.RegularExpressions;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Validation;

namespace Forge.Domain.Lifecycle;

public sealed record ProjectGameplayEventProfile
{
    public const int CurrentSchemaVersion = 1;
    public static readonly IReadOnlyList<string> RequiredEventNames =
    [
        "ability.activated",
        "equipment.changed",
        "inventory.changed",
        "item.used",
        "life.dead",
        "life.downed",
        "life.dying",
        "life.respawned",
        "life.respawnrequested",
        "player.disconnected",
        "player.joining",
        "player.leaving",
        "player.ready",
        "progression.changed",
    ];

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public bool Enabled { get; }
    public bool RequiredAtRuntime { get; }
    public bool UseDownedState { get; }
    public bool AutoRespawn { get; }
    public bool ReplicateEquipmentAndProgression { get; }
    public IReadOnlyList<EventBinding> Bindings { get; }

    public ProjectGameplayEventProfile(
        Guid id,
        int schemaVersion,
        string provenance,
        bool enabled,
        bool requiredAtRuntime,
        bool useDownedState,
        bool autoRespawn,
        bool replicateEquipmentAndProgression,
        IReadOnlyList<EventBinding>? bindings)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = (provenance ?? string.Empty).Trim();
        Enabled = enabled;
        RequiredAtRuntime = requiredAtRuntime;
        UseDownedState = useDownedState;
        AutoRespawn = autoRespawn;
        ReplicateEquipmentAndProgression = replicateEquipmentAndProgression;
        Bindings = (bindings ?? [])
            .OrderBy(binding => binding.EventName, StringComparer.Ordinal)
            .ThenBy(binding => binding.Id)
            .ToArray();
    }

    public static ProjectGameplayEventProfile CreateDefault(Guid moduleId) => new(
        moduleId,
        CurrentSchemaVersion,
        "migration-default:r051",
        true,
        false,
        true,
        true,
        true,
        RequiredEventNames.Select(eventName => new EventBinding(
            RuntimePolicyIdFactory.Create(moduleId, $"project-event:{eventName}"),
            1,
            "migration-default:r051",
            "1.0",
            eventName,
            HandlerName(eventName),
            EventAuthorityScope.Server)).ToArray());

    private static string HandlerName(string eventName) => "On" + string.Concat(eventName
        .Split('.', StringSplitOptions.RemoveEmptyEntries)
        .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}

public static partial class ProjectGameplayEventProfileValidator
{
    public const string IdentityInvalidCode = "project.events.identity_invalid";
    public const string SchemaUnsupportedCode = "project.events.schema_unsupported";
    public const string BindingSetInvalidCode = "project.events.binding_set_invalid";
    public const string BindingIdentityInvalidCode = "project.events.binding_identity_invalid";
    public const string BindingContractInvalidCode = "project.events.binding_contract_invalid";
    public const string RequiredDisabledCode = "project.events.required_disabled";
    public const string MissingRequiredCode = "project.events.required_missing";

    private static readonly IReadOnlySet<string> Required =
        ProjectGameplayEventProfile.RequiredEventNames.ToHashSet(StringComparer.Ordinal);
    private static readonly Regex HandlerPattern = HandlerRegex();
    private static readonly Regex ApiVersionPattern = ApiVersionRegex();

    public static IReadOnlyList<DomainDiagnostic> Validate(ProjectGameplayEventProfile profile,
        Guid? expectedModuleId = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        const string root = "module/runtimePolicies/gameplayEvents";
        var diagnostics = new List<DomainDiagnostic>();
        if (profile.SchemaVersion != ProjectGameplayEventProfile.CurrentSchemaVersion)
            diagnostics.Add(new(SchemaUnsupportedCode, "Project gameplay-event profile schema is unsupported.",
                $"{root}/schemaVersion"));
        if (profile.Id == Guid.Empty || (expectedModuleId.HasValue && profile.Id != expectedModuleId.Value) ||
            string.IsNullOrWhiteSpace(profile.Provenance))
            diagnostics.Add(new(IdentityInvalidCode,
                "Project gameplay-event profile requires the module UUID and provenance.", $"{root}/id"));
        if (profile.RequiredAtRuntime && !profile.Enabled)
            diagnostics.Add(new(RequiredDisabledCode,
                "A runtime-required project gameplay-event profile cannot be disabled.", $"{root}/enabled"));

        var names = profile.Bindings.Select(binding => binding.EventName).ToArray();
        if (names.Length != Required.Count || names.Distinct(StringComparer.Ordinal).Count() != names.Length ||
            !Required.SetEquals(names))
            diagnostics.Add(new(BindingSetInvalidCode,
                "Project gameplay-event bindings must contain every supported event exactly once.",
                $"{root}/bindings"));
        if (profile.Bindings.Any(binding => binding.Id == Guid.Empty) ||
            profile.Bindings.Select(binding => binding.Id).Distinct().Count() != profile.Bindings.Count)
            diagnostics.Add(new(BindingIdentityInvalidCode,
                "Project gameplay-event bindings require unique non-empty UUIDs.", $"{root}/bindings"));

        foreach (var binding in profile.Bindings)
        {
            if (binding.SchemaVersion != 1 || string.IsNullOrWhiteSpace(binding.Provenance) ||
                !ApiVersionPattern.IsMatch(binding.ForgeScriptApiVersion ?? string.Empty) ||
                !HandlerPattern.IsMatch(binding.Handler ?? string.Empty) ||
                binding.Authority != EventAuthorityScope.Server)
                diagnostics.Add(new(BindingContractInvalidCode,
                    "Project gameplay-event binding requires schema v1, provenance, supported API, safe handler and server authority.",
                    $"{root}/bindings/{binding.Id:D}"));
        }

        return diagnostics;
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{1,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex HandlerRegex();

    [GeneratedRegex("^[0-9]+\\.[0-9]+(?:\\.[0-9]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex ApiVersionRegex();
}

public sealed record ProjectGameplayEventRuntimeHandle(
    Guid Id,
    int SchemaVersion,
    bool UseDownedState,
    bool AutoRespawn,
    bool ReplicateEquipmentAndProgression,
    IReadOnlyDictionary<string, EventBinding> Bindings);

public sealed record ProjectGameplayEventProfileResolution(
    ProjectGameplayEventRuntimeHandle? Handle,
    IReadOnlyList<DomainDiagnostic> Diagnostics)
{
    public bool Available => Handle is not null && Diagnostics.Count == 0;
}

public static class ProjectGameplayEventProfileRuntime
{
    public static ProjectGameplayEventProfileResolution Resolve(
        ProjectGameplayEventProfile? profile,
        bool required)
    {
        if (profile is null)
            return required
                ? new(null, [new DomainDiagnostic(ProjectGameplayEventProfileValidator.MissingRequiredCode,
                    "Required project gameplay-event configuration is unavailable.",
                    "module/runtimePolicies/gameplayEvents")])
                : new(null, []);
        var diagnostics = ProjectGameplayEventProfileValidator.Validate(profile);
        if (!profile.Enabled)
            return required
                ? new(null, diagnostics.Append(new DomainDiagnostic(
                    ProjectGameplayEventProfileValidator.MissingRequiredCode,
                    "Required project gameplay-event configuration is disabled.",
                    "module/runtimePolicies/gameplayEvents/enabled")).ToArray())
                : new(null, diagnostics);
        return new(new ProjectGameplayEventRuntimeHandle(
            profile.Id,
            profile.SchemaVersion,
            profile.UseDownedState,
            profile.AutoRespawn,
            profile.ReplicateEquipmentAndProgression,
            profile.Bindings.ToDictionary(binding => binding.EventName, StringComparer.Ordinal)), diagnostics);
    }
}
