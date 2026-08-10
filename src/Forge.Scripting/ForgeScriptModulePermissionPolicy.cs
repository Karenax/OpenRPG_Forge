using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Forge.Scripting;

public sealed record ForgeScriptKitCapabilityGrant(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string KitId,
    IReadOnlySet<string> Capabilities);

public sealed record ForgeScriptPermissionEventBinding(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string EventName,
    string Handler,
    string KitId,
    IReadOnlySet<string> Capabilities,
    ForgeScriptEventAuthority Authority,
    bool Enabled);

public sealed record ForgeScriptModulePermissionPolicy(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string ApiVersion,
    Guid ModuleId,
    bool DenyByDefault,
    bool ServerEnforced,
    bool RequireSignedDistribution,
    bool AllowUnsignedDevelopment,
    bool RequiredAtRuntime,
    IReadOnlySet<string> AllowedCapabilities,
    IReadOnlyList<ForgeScriptKitCapabilityGrant> KitGrants,
    IReadOnlyList<ForgeScriptPermissionEventBinding> EventBindings)
{
    public const int CurrentSchemaVersion = 1;
    public const string RelativePath = "scripts/forge-script-permissions.json";

    public static ForgeScriptModulePermissionPolicy CreateDefault(Guid moduleId)
    {
        if (moduleId == Guid.Empty)
            throw new ArgumentException("A module UUID is required.", nameof(moduleId));
        var capabilities = new HashSet<string>(["world", "utilities", "determinism"], StringComparer.Ordinal);
        return new(
            Stable(moduleId, "policy"), CurrentSchemaVersion, "forge:default", ForgeScriptApiCompatibility.Current,
            moduleId, true, true, true, true, true, capabilities,
            [new(Stable(moduleId, "kit:core"), 1, "forge:default", "core", capabilities)],
            [new(Stable(moduleId, "binding:module.start:main"), 1, "forge:default", "module.start", "main",
                "core", capabilities, ForgeScriptEventAuthority.Server, true)]);
    }

    private static Guid Stable(Guid owner, string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:{owner:D}:script-permission:{key}"));
        bytes[7] = (byte)((bytes[7] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }
}

public sealed record ForgeScriptModulePermissionDiagnostic(
    string Code,
    string Path,
    Guid? SubjectId,
    string Severity,
    string Message);

public static partial class ForgeScriptModulePermissionValidator
{
    public static IReadOnlyList<ForgeScriptModulePermissionDiagnostic> Validate(
        ForgeScriptModulePermissionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var diagnostics = new List<ForgeScriptModulePermissionDiagnostic>();
        var known = ForgeScriptCapabilityManifest.Capabilities.Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
        Add(policy.Id == Guid.Empty, "script.permission.uuid_required", "scriptPermissions/id", policy.Id,
            "A stable policy UUID is required.");
        Add(policy.ModuleId == Guid.Empty, "script.permission.module_required", "scriptPermissions/moduleId",
            policy.ModuleId, "A stable module UUID is required.");
        Add(policy.SchemaVersion != ForgeScriptModulePermissionPolicy.CurrentSchemaVersion,
            "script.permission.schema_unsupported", "scriptPermissions/schemaVersion", policy.Id,
            "Module permission schema v1 is required.");
        Add(string.IsNullOrWhiteSpace(policy.Provenance), "script.permission.provenance_required",
            "scriptPermissions/provenance", policy.Id, "Permission provenance is required.");
        Add(!ForgeScriptApiCompatibility.IsSupported(policy.ApiVersion), "script.permission.api_unsupported",
            "scriptPermissions/apiVersion", policy.Id, $"ForgeScript API '{policy.ApiVersion}' is unsupported.");
        Add(!policy.DenyByDefault, "script.permission.deny_default_required", "scriptPermissions/denyByDefault",
            policy.Id, "Module permissions must deny undeclared capabilities by default.");
        Add(!policy.ServerEnforced, "script.permission.server_enforcement_required",
            "scriptPermissions/serverEnforced", policy.Id, "Module permissions must be enforced by the server.");
        Add(!policy.RequireSignedDistribution, "script.permission.signature_required",
            "scriptPermissions/requireSignedDistribution", policy.Id,
            "Distributed module scripts require a trusted package signature.");

        var allowed = policy.AllowedCapabilities ?? new HashSet<string>(StringComparer.Ordinal);
        Add(allowed.Count == 0 || allowed.Any(capability => !known.Contains(capability)),
            "script.permission.capability_invalid", "scriptPermissions/allowedCapabilities", policy.Id,
            "At least one known ForgeScript capability must be explicitly allowed.");

        var grantIds = new HashSet<Guid>();
        var kitIds = new HashSet<string>(StringComparer.Ordinal);
        var grants = policy.KitGrants ?? [];
        foreach (var (grant, index) in grants.Select((value, index) => (value, index)))
        {
            var path = $"scriptPermissions/kitGrants/{index}";
            Add(grant.Id == Guid.Empty || !grantIds.Add(grant.Id), "script.permission.kit_uuid_invalid", path,
                grant.Id, "Every kit grant needs a unique stable UUID.");
            Add(grant.SchemaVersion != 1 || string.IsNullOrWhiteSpace(grant.Provenance),
                "script.permission.kit_schema_invalid", path, grant.Id,
                "Kit grants require schema v1 and provenance.");
            Add(!KeyPattern().IsMatch(grant.KitId ?? string.Empty) ||
                (!string.IsNullOrEmpty(grant.KitId) && !kitIds.Add(grant.KitId)),
                "script.permission.kit_id_invalid", $"{path}/kitId", grant.Id,
                "Kit IDs must be unique canonical identifiers.");
            var grantCapabilities = grant.Capabilities ?? new HashSet<string>(StringComparer.Ordinal);
            Add(grantCapabilities.Count == 0 || grantCapabilities.Any(capability =>
                    !known.Contains(capability) || !allowed.Contains(capability)),
                "script.permission.kit_capability_invalid", $"{path}/capabilities", grant.Id,
                "Kit capabilities must be known non-empty subsets of the module allowlist.");
        }

        var bindingIds = new HashSet<Guid>();
        var bindings = policy.EventBindings ?? [];
        foreach (var (binding, index) in bindings.Select((value, index) => (value, index)))
        {
            var path = $"scriptPermissions/eventBindings/{index}";
            Add(binding.Id == Guid.Empty || !bindingIds.Add(binding.Id),
                "script.permission.binding_uuid_invalid", path, binding.Id,
                "Every permission binding needs a unique stable UUID.");
            Add(binding.SchemaVersion != 1 || string.IsNullOrWhiteSpace(binding.Provenance),
                "script.permission.binding_schema_invalid", path, binding.Id,
                "Permission bindings require schema v1 and provenance.");
            Add(!EventPattern().IsMatch(binding.EventName ?? string.Empty) ||
                !HandlerPattern().IsMatch(binding.Handler ?? string.Empty),
                "script.permission.binding_target_invalid", path, binding.Id,
                "Binding event and handler identifiers must be canonical.");
            Add(!kitIds.Contains(binding.KitId), "script.permission.binding_kit_missing", $"{path}/kitId",
                binding.Id, $"Permission binding references unknown kit '{binding.KitId}'.");
            var kit = grants.FirstOrDefault(item => string.Equals(item.KitId, binding.KitId,
                StringComparison.Ordinal));
            var bindingCapabilities = binding.Capabilities ?? new HashSet<string>(StringComparer.Ordinal);
            Add(bindingCapabilities.Count == 0 || bindingCapabilities.Any(capability =>
                    !known.Contains(capability) || !allowed.Contains(capability) ||
                    kit is null || kit.Capabilities is null || !kit.Capabilities.Contains(capability)),
                "script.permission.binding_capability_invalid", $"{path}/capabilities", binding.Id,
                "Binding capabilities must be known non-empty subsets of the selected kit grant.");
            Add(binding.Authority != ForgeScriptEventAuthority.Server,
                "script.permission.binding_authority_invalid", $"{path}/authority", binding.Id,
                "Module permission bindings require server authority.");
        }
        Add(bindings.Count == 0, "script.permission.binding_required",
            "scriptPermissions/eventBindings", policy.Id, "At least one event permission binding is required.");
        return diagnostics.OrderBy(item => item.Path, StringComparer.Ordinal)
            .ThenBy(item => item.Code, StringComparer.Ordinal).ToArray();

        void Add(bool condition, string code, string path, Guid subjectId, string message)
        {
            if (condition)
                diagnostics.Add(new(code, path, subjectId == Guid.Empty ? null : subjectId, "error", message));
        }
    }

    [GeneratedRegex("^[a-z][a-z0-9_.-]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();

    [GeneratedRegex("^[a-z][a-z0-9_.-]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex EventPattern();

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex HandlerPattern();
}

public sealed record ForgeScriptModulePermissionCommand(
    Guid TransactionId,
    long ExpectedRevision,
    ForgeScriptModulePermissionPolicy Policy,
    string Reason);

public sealed record ForgeScriptModulePermissionMutation(
    bool Succeeded,
    ForgeScriptModulePermissionPolicy Policy,
    long Revision,
    string SemanticDiff,
    IReadOnlyList<ForgeScriptModulePermissionDiagnostic> Diagnostics);

public sealed class ForgeScriptModulePermissionHistory(ForgeScriptModulePermissionPolicy initial)
{
    private readonly Stack<ForgeScriptModulePermissionPolicy> _undo = [];
    private readonly Stack<ForgeScriptModulePermissionPolicy> _redo = [];

    public ForgeScriptModulePermissionPolicy Policy { get; private set; } = initial;
    public long Revision { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public ForgeScriptModulePermissionMutation Preview(ForgeScriptModulePermissionCommand command) =>
        Evaluate(command);

    public ForgeScriptModulePermissionMutation Apply(ForgeScriptModulePermissionCommand command)
    {
        var result = Evaluate(command);
        if (!result.Succeeded)
            return result;
        _undo.Push(Policy);
        _redo.Clear();
        Policy = command.Policy;
        Revision++;
        return result with { Policy = Policy, Revision = Revision };
    }

    public ForgeScriptModulePermissionMutation Undo()
    {
        if (!CanUndo)
            return Failure("script.permission.undo_empty", "scriptPermissions/history", "Nothing to undo.");
        _redo.Push(Policy);
        Policy = _undo.Pop();
        Revision++;
        return new(true, Policy, Revision, "undo:script-module-permissions", []);
    }

    public ForgeScriptModulePermissionMutation Redo()
    {
        if (!CanRedo)
            return Failure("script.permission.redo_empty", "scriptPermissions/history", "Nothing to redo.");
        _undo.Push(Policy);
        Policy = _redo.Pop();
        Revision++;
        return new(true, Policy, Revision, "redo:script-module-permissions", []);
    }

    private ForgeScriptModulePermissionMutation Evaluate(ForgeScriptModulePermissionCommand command)
    {
        if (command.TransactionId == Guid.Empty || command.ExpectedRevision != Revision ||
            string.IsNullOrWhiteSpace(command.Reason))
            return Failure("script.permission.command_invalid", "scriptPermissions/command",
                "Transaction identity, current revision and reason are required.");
        var diagnostics = ForgeScriptModulePermissionValidator.Validate(command.Policy);
        return diagnostics.Count > 0
            ? new(false, Policy, Revision, "no-change", diagnostics)
            : new(true, command.Policy, Revision,
                $"script-permissions:{Policy.Id:D}|capabilities:{string.Join(',', Policy.AllowedCapabilities.Order())}" +
                $"->{string.Join(',', command.Policy.AllowedCapabilities.Order())}|signed:" +
                $"{Policy.RequireSignedDistribution}->{command.Policy.RequireSignedDistribution}", []);
    }

    private ForgeScriptModulePermissionMutation Failure(string code, string path, string message) =>
        new(false, Policy, Revision, "no-change", [new(code, path, Policy.Id, "error", message)]);
}

public sealed class ForgeScriptModulePermissionStore(string workspaceRoot)
{
    private readonly string _workspaceRoot = System.IO.Path.GetFullPath(workspaceRoot ??
        throw new ArgumentNullException(nameof(workspaceRoot)));
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
    };

    public string Path => System.IO.Path.Combine(_workspaceRoot,
        ForgeScriptModulePermissionPolicy.RelativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));

    public void Save(ForgeScriptModulePermissionPolicy policy)
    {
        var diagnostics = ForgeScriptModulePermissionValidator.Validate(policy);
        if (diagnostics.Count > 0)
            throw new InvalidOperationException(string.Join(";", diagnostics.Select(item => item.Code)));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var temporary = $"{Path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(ToDocument(policy), Options) + Environment.NewLine,
                new UTF8Encoding(false));
            File.Move(temporary, Path, true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    public ForgeScriptModulePermissionPolicy Load()
    {
        var document = JsonSerializer.Deserialize<PolicyDocument>(File.ReadAllText(Path), Options)
            ?? throw new InvalidOperationException("script.permission.document_invalid");
        var policy = FromDocument(document);
        var diagnostics = ForgeScriptModulePermissionValidator.Validate(policy);
        if (diagnostics.Count > 0)
            throw new InvalidOperationException(string.Join(";", diagnostics.Select(item => item.Code)));
        return policy;
    }

    private static PolicyDocument ToDocument(ForgeScriptModulePermissionPolicy policy) => new(
        policy.Id, policy.SchemaVersion, policy.Provenance, policy.ApiVersion, policy.ModuleId,
        policy.DenyByDefault, policy.ServerEnforced, policy.RequireSignedDistribution,
        policy.AllowUnsignedDevelopment, policy.RequiredAtRuntime, policy.AllowedCapabilities.Order().ToArray(),
        policy.KitGrants.Select(item => new GrantDocument(item.Id, item.SchemaVersion, item.Provenance, item.KitId,
            item.Capabilities.Order().ToArray())).ToArray(),
        policy.EventBindings.Select(item => new BindingDocument(item.Id, item.SchemaVersion, item.Provenance,
            item.EventName, item.Handler, item.KitId, item.Capabilities.Order().ToArray(), item.Authority,
            item.Enabled)).ToArray());

    private static ForgeScriptModulePermissionPolicy FromDocument(PolicyDocument document) => new(
        document.Id, document.SchemaVersion, document.Provenance, document.ApiVersion, document.ModuleId,
        document.DenyByDefault, document.ServerEnforced, document.RequireSignedDistribution,
        document.AllowUnsignedDevelopment, document.RequiredAtRuntime,
        (document.AllowedCapabilities ?? []).ToHashSet(StringComparer.Ordinal),
        (document.KitGrants ?? []).Select(item => new ForgeScriptKitCapabilityGrant(item.Id, item.SchemaVersion,
            item.Provenance, item.KitId, (item.Capabilities ?? []).ToHashSet(StringComparer.Ordinal))).ToArray(),
        (document.EventBindings ?? []).Select(item => new ForgeScriptPermissionEventBinding(item.Id, item.SchemaVersion,
            item.Provenance, item.EventName, item.Handler, item.KitId,
            (item.Capabilities ?? []).ToHashSet(StringComparer.Ordinal), item.Authority, item.Enabled)).ToArray());

    private sealed record PolicyDocument(Guid Id, int SchemaVersion, string Provenance, string ApiVersion,
        Guid ModuleId, bool DenyByDefault, bool ServerEnforced, bool RequireSignedDistribution,
        bool AllowUnsignedDevelopment, bool RequiredAtRuntime, IReadOnlyList<string> AllowedCapabilities,
        IReadOnlyList<GrantDocument> KitGrants, IReadOnlyList<BindingDocument> EventBindings);

    private sealed record GrantDocument(Guid Id, int SchemaVersion, string Provenance, string KitId,
        IReadOnlyList<string> Capabilities);

    private sealed record BindingDocument(Guid Id, int SchemaVersion, string Provenance, string EventName,
        string Handler, string KitId, IReadOnlyList<string> Capabilities, ForgeScriptEventAuthority Authority,
        bool Enabled);
}
