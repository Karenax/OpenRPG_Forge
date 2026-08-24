using System.Text.RegularExpressions;
using Forge.Domain.Modules;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Validation;

namespace Forge.Domain.Lifecycle;

public sealed record ProjectLifecycleScriptBinding(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string ForgeScriptApiVersion,
    RuntimeLifecycleEventKind Event,
    RuntimeAuthority Authority,
    string Handler,
    string? ScriptPath,
    bool RequiredAtRuntime);

public sealed record ProjectLifecycleEventProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public bool Enabled { get; }
    public bool RequiredAtRuntime { get; }
    public IReadOnlyList<ProjectLifecycleScriptBinding> Bindings { get; }

    public ProjectLifecycleEventProfile(
        Guid id,
        int schemaVersion,
        string provenance,
        bool enabled,
        bool requiredAtRuntime,
        IReadOnlyList<ProjectLifecycleScriptBinding>? bindings)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = (provenance ?? string.Empty).Trim();
        Enabled = enabled;
        RequiredAtRuntime = requiredAtRuntime;
        Bindings = (bindings ?? [])
            .OrderBy(binding => binding.Event)
            .ThenBy(binding => binding.Authority)
            .ThenBy(binding => binding.Id)
            .ToArray();
    }

    public static ProjectLifecycleEventProfile CreateDefault(ModuleId moduleId)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        var events = new[]
        {
            RuntimeLifecycleEventKind.ProjectLoaded,
            RuntimeLifecycleEventKind.WorldStarted,
        };
        var bindings = events.SelectMany(@event => Enum.GetValues<RuntimeAuthority>().Select(authority =>
            new ProjectLifecycleScriptBinding(
                RuntimePolicyIdFactory.Create(moduleId.Value,
                    $"project-lifecycle:{@event.ToString().ToLowerInvariant()}:{authority.ToString().ToLowerInvariant()}"),
                1,
                "migration-default:ux0-f",
                RuntimeLifecycleEvent.CurrentForgeScriptApiVersion,
                @event,
                authority,
                DefaultHandler(@event, authority),
                null,
                false))).ToArray();
        return new ProjectLifecycleEventProfile(
            moduleId.Value,
            CurrentSchemaVersion,
            "migration-default:ux0-f",
            true,
            false,
            bindings);
    }

    private static string DefaultHandler(RuntimeLifecycleEventKind @event, RuntimeAuthority authority) =>
        $"On{@event}{authority}";
}

public static partial class ProjectLifecycleEventProfileValidator
{
    public const string IdentityInvalidCode = "project.lifecycle.identity_invalid";
    public const string SchemaUnsupportedCode = "project.lifecycle.schema_unsupported";
    public const string BindingSetInvalidCode = "project.lifecycle.binding_set_invalid";
    public const string BindingIdentityInvalidCode = "project.lifecycle.binding_identity_invalid";
    public const string BindingContractInvalidCode = "project.lifecycle.binding_contract_invalid";
    public const string RequiredDisabledCode = "project.lifecycle.required_disabled";
    public const string MissingRequiredCode = "project.lifecycle.required_missing";

    private static readonly Regex HandlerPattern = HandlerRegex();
    private static readonly Regex ApiVersionPattern = ApiVersionRegex();

    public static IReadOnlyList<DomainDiagnostic> Validate(
        ProjectLifecycleEventProfile profile,
        Guid? expectedModuleId = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        const string root = "module/runtimePolicies/lifecycleEvents";
        var diagnostics = new List<DomainDiagnostic>();
        if (profile.SchemaVersion != ProjectLifecycleEventProfile.CurrentSchemaVersion)
            diagnostics.Add(new(SchemaUnsupportedCode,
                "Project lifecycle-event profile schema is unsupported.", $"{root}/schemaVersion"));
        if (profile.Id == Guid.Empty || expectedModuleId.HasValue && profile.Id != expectedModuleId.Value ||
            string.IsNullOrWhiteSpace(profile.Provenance))
            diagnostics.Add(new(IdentityInvalidCode,
                "Project lifecycle-event profile requires the module UUID and provenance.", $"{root}/id"));
        if (profile.RequiredAtRuntime && !profile.Enabled)
            diagnostics.Add(new(RequiredDisabledCode,
                "A runtime-required project lifecycle-event profile cannot be disabled.", $"{root}/enabled"));

        var expected = new HashSet<(RuntimeLifecycleEventKind Event, RuntimeAuthority Authority)>(
            new[] { RuntimeLifecycleEventKind.ProjectLoaded, RuntimeLifecycleEventKind.WorldStarted }
                .SelectMany(@event => Enum.GetValues<RuntimeAuthority>()
                    .Select(authority => (@event, authority))));
        var actual = profile.Bindings.Select(binding => (binding.Event, binding.Authority)).ToArray();
        if (actual.Length != expected.Count || actual.Distinct().Count() != actual.Length ||
            !expected.SetEquals(actual))
            diagnostics.Add(new(BindingSetInvalidCode,
                "Lifecycle bindings must contain ProjectLoaded and WorldStarted for every runtime context exactly once.",
                $"{root}/bindings"));
        if (profile.Bindings.Any(binding => binding.Id == Guid.Empty) ||
            profile.Bindings.Select(binding => binding.Id).Distinct().Count() != profile.Bindings.Count)
            diagnostics.Add(new(BindingIdentityInvalidCode,
                "Project lifecycle-event bindings require unique non-empty UUIDs.", $"{root}/bindings"));

        foreach (var binding in profile.Bindings)
        {
            if (binding.SchemaVersion != 1 || string.IsNullOrWhiteSpace(binding.Provenance) ||
                !ApiVersionPattern.IsMatch(binding.ForgeScriptApiVersion ?? string.Empty) ||
                !HandlerPattern.IsMatch(binding.Handler ?? string.Empty) ||
                binding.RequiredAtRuntime && string.IsNullOrWhiteSpace(binding.ScriptPath) ||
                !string.IsNullOrWhiteSpace(binding.ScriptPath) && !IsSafeScriptPath(binding.ScriptPath))
                diagnostics.Add(new(BindingContractInvalidCode,
                    "Lifecycle binding requires schema v1, provenance, supported API, safe handler and relative .forge script.",
                    $"{root}/bindings/{binding.Id:D}"));
        }
        return diagnostics;
    }

    public static bool IsSafeScriptPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        var segments = normalized.Split('/');
        return normalized.StartsWith("scripts/", StringComparison.Ordinal) &&
               normalized.EndsWith(".forge", StringComparison.OrdinalIgnoreCase) &&
               !normalized.Contains(':') &&
               segments.All(segment => segment.Length > 0 && segment is not "." and not "..") &&
               !Path.IsPathRooted(path);
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{1,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex HandlerRegex();

    [GeneratedRegex("^[0-9]+\\.[0-9]+(?:\\.[0-9]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex ApiVersionRegex();
}

public sealed record ProjectLifecycleBindingResolution(
    ProjectLifecycleScriptBinding? Binding,
    IReadOnlyList<DomainDiagnostic> Diagnostics)
{
    public bool Available => Binding is not null && Diagnostics.Count == 0;
}

public static class ProjectLifecycleEventProfileRuntime
{
    public static ProjectLifecycleBindingResolution Resolve(
        ProjectLifecycleEventProfile? profile,
        RuntimeLifecycleEventKind @event,
        RuntimeAuthority authority,
        bool required)
    {
        const string root = "module/runtimePolicies/lifecycleEvents";
        if (profile is null)
            return required
                ? new(null, [new DomainDiagnostic(ProjectLifecycleEventProfileValidator.MissingRequiredCode,
                    "Required project lifecycle-event configuration is unavailable.", root)])
                : new(null, []);
        var diagnostics = ProjectLifecycleEventProfileValidator.Validate(profile);
        if (diagnostics.Count > 0)
            return new(null, diagnostics);
        if (!profile.Enabled)
            return required || profile.RequiredAtRuntime
                ? new(null, [new DomainDiagnostic(ProjectLifecycleEventProfileValidator.MissingRequiredCode,
                    "Required project lifecycle-event configuration is disabled.", $"{root}/enabled")])
                : new(null, []);
        var binding = profile.Bindings.SingleOrDefault(item => item.Event == @event && item.Authority == authority);
        if (binding is null)
            return new(null, [new DomainDiagnostic(ProjectLifecycleEventProfileValidator.BindingSetInvalidCode,
                "The requested lifecycle binding is unavailable.", $"{root}/bindings")]);
        return new(binding, []);
    }
}
