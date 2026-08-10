using System.Text.RegularExpressions;

namespace Forge.Scripting;

public enum ForgeScriptSourceDistributionPolicy
{
    AuthoringOnly,
    SourceAndBytecode,
    SourceOnly,
}

public enum ForgeScriptEventAuthority
{
    Local,
    Owner,
    Server,
}

public sealed record ForgeScriptEventContract(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string EventName,
    string PayloadType,
    bool RequiredAtRuntime);

public sealed record ForgeScriptEventBinding(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string ComponentId,
    string EventName,
    string Handler,
    string PayloadType,
    int Priority,
    string? EnableCondition,
    ForgeScriptEventAuthority Authority,
    bool Enabled);

public sealed record ForgeScriptSourceDocument(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string RelativePath,
    string Source,
    string ApiVersion,
    ForgeScriptSourceDistributionPolicy Distribution,
    bool RequiredAtRuntime,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<ForgeScriptEventBinding> EventBindings,
    IReadOnlySet<string>? RequiredCapabilities = null)
{
    public const int CurrentSchemaVersion = 1;
    public const string Language = "AngelScript";
    public const string DialectVersion = "2.38";
}

public sealed record ForgeScriptSourceDiagnostic(
    string Code,
    string Path,
    Guid? SubjectId,
    string Severity,
    string Message);

public sealed record ForgeScriptSourceRequirement(Guid SubjectId, string RelativePath, bool RequiredAtRuntime);

public static partial class ForgeScriptSourceValidator
{

    public static IReadOnlyList<ForgeScriptSourceDiagnostic> Validate(
        ForgeScriptSourceDocument document,
        IReadOnlyList<ForgeScriptEventContract> eventContracts,
        IReadOnlySet<string>? availableSourcePaths = null,
        int maximumSourceBytes = 524_288)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(eventContracts);
        var diagnostics = new List<ForgeScriptSourceDiagnostic>();
        Add(document.Id == Guid.Empty, "script.source.uuid_required", "script", document.Id,
            "A stable source UUID is required.");
        Add(document.SchemaVersion != ForgeScriptSourceDocument.CurrentSchemaVersion,
            "script.source.schema_unsupported", "script/schemaVersion", document.Id,
            "ForgeScript source schema v1 is required.");
        Add(string.IsNullOrWhiteSpace(document.Provenance), "script.source.provenance_required",
            "script/provenance", document.Id, "Source provenance is required.");
        Add(!IsSafeSourcePath(document.RelativePath), "script.source.path_invalid",
            "script/relativePath", document.Id, "The canonical source path must be a safe relative .as path.");
        Add(string.IsNullOrWhiteSpace(document.Source), "script.source.empty", "script/source", document.Id,
            "Canonical ForgeScript source cannot be empty.");
        Add(System.Text.Encoding.UTF8.GetByteCount(document.Source ?? string.Empty) > maximumSourceBytes,
            "script.source.quota_exceeded", "script/source", document.Id,
            "Canonical ForgeScript source exceeds the configured byte quota.");
        Add(!ForgeScriptApiCompatibility.IsSupported(document.ApiVersion),
            "script.source.api_unsupported", "script/apiVersion", document.Id,
            "The declared ForgeScript API version is unsupported.");
        var negotiation = ForgeScriptApiCompatibility.Negotiate(document.ApiVersion, document.RequiredCapabilities);
        Add(negotiation.MissingCapabilities.Count > 0, negotiation.DiagnosticCode,
            "script/requiredCapabilities", document.Id,
            $"Missing ForgeScript capabilities: {string.Join(", ", negotiation.MissingCapabilities)}.");
        Add(!Enum.IsDefined(document.Distribution), "script.source.distribution_invalid",
            "script/distribution", document.Id, "The source distribution policy is invalid.");

        var normalizedDependencies = document.Dependencies.Select(NormalizePath).ToArray();
        Add(normalizedDependencies.Any(path => !IsSafeSourcePath(path)) ||
            normalizedDependencies.Distinct(StringComparer.Ordinal).Count() != normalizedDependencies.Length ||
            normalizedDependencies.Contains(NormalizePath(document.RelativePath), StringComparer.Ordinal),
            "script.source.dependencies_invalid", "script/dependencies", document.Id,
            "Dependencies must be unique, safe .as paths and cannot reference the source itself.");
        if (availableSourcePaths is not null)
        {
            foreach (var missing in normalizedDependencies.Where(path => !availableSourcePaths.Contains(path)))
                diagnostics.Add(new("script.source.dependency_missing", $"script/dependencies/{missing}", document.Id,
                    "error", $"Required source dependency '{missing}' is missing."));
        }

        var contractByName = eventContracts
            .Where(contract => !string.IsNullOrWhiteSpace(contract.EventName))
            .GroupBy(contract => contract.EventName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var bindingIds = new HashSet<Guid>();
        for (var index = 0; index < document.EventBindings.Count; index++)
        {
            var binding = document.EventBindings[index];
            var path = $"script/eventBindings/{index}";
            Add(binding.Id == Guid.Empty || !bindingIds.Add(binding.Id), "script.binding.uuid_invalid",
                path, binding.Id, "Every event binding needs a unique stable UUID.");
            Add(binding.SchemaVersion != 1 || string.IsNullOrWhiteSpace(binding.Provenance),
                "script.binding.schema_invalid", path, binding.Id,
                "Event binding schema v1 and provenance are required.");
            Add(!IdentifierPattern().IsMatch(binding.ComponentId ?? string.Empty),
                "script.binding.component_invalid", $"{path}/componentId", binding.Id,
                "A canonical component identifier is required.");
            Add(!HandlerPattern().IsMatch(binding.Handler ?? string.Empty),
                "script.binding.handler_invalid", $"{path}/handler", binding.Id,
                "The handler must be a valid function identifier.");
            Add(binding.Priority is < -10_000 or > 10_000, "script.binding.priority_invalid",
                $"{path}/priority", binding.Id, "Handler priority must be between -10000 and 10000.");
            Add(!Enum.IsDefined(binding.Authority), "script.binding.authority_invalid",
                $"{path}/authority", binding.Id, "Event authority must be explicit.");
            if (!contractByName.TryGetValue(binding.EventName, out var contract))
            {
                diagnostics.Add(new("script.binding.event_missing", $"{path}/eventName", binding.Id, "error",
                    $"Event contract '{binding.EventName}' is missing."));
                continue;
            }
            Add(contract.Id == Guid.Empty || contract.SchemaVersion != 1 || string.IsNullOrWhiteSpace(contract.Provenance),
                "script.event_contract.invalid", $"events/{contract.EventName}", contract.Id,
                "Event contracts require UUID, schema v1 and provenance.");
            Add(!string.Equals(binding.PayloadType, contract.PayloadType, StringComparison.Ordinal),
                "script.binding.payload_mismatch", $"{path}/payloadType", binding.Id,
                $"Handler payload must be '{contract.PayloadType}'.");
            if (binding.Enabled)
            {
                var declaration = EntryDeclaration(binding.Handler!, binding.PayloadType);
                Add(!ContainsDeclaration(document.Source!, declaration), "script.binding.entry_missing",
                    $"{path}/handler", binding.Id, $"Typed entry function '{declaration}' is required.");
            }
        }

        Add(document.EventBindings.GroupBy(binding => new
        {
            binding.ComponentId,
            binding.EventName,
            binding.Handler,
            binding.Priority,
        }).Any(group => group.Count() > 1), "script.binding.duplicate", "script/eventBindings", document.Id,
            "Duplicate handler registrations at the same priority are not allowed.");
        diagnostics.AddRange(ForgeScriptCapabilityManifest.ValidateSource(document.Source ?? string.Empty,
            document.Id == Guid.Empty ? null : document.Id));
        return diagnostics.OrderBy(item => item.Path, StringComparer.Ordinal)
            .ThenBy(item => item.Code, StringComparer.Ordinal).ToArray();

        void Add(bool condition, string code, string path, Guid subjectId, string message)
        {
            if (condition) diagnostics.Add(new(code, path, subjectId == Guid.Empty ? null : subjectId, "error", message));
        }
    }

    public static IReadOnlyList<ForgeScriptEventBinding> ResolveHandlers(
        ForgeScriptSourceDocument document,
        string componentId,
        string eventName,
        ForgeScriptEventAuthority authority,
        Func<string, bool>? conditionEvaluator = null) => document.EventBindings
        .Where(binding => binding.Enabled && binding.Authority == authority &&
                          string.Equals(binding.ComponentId, componentId, StringComparison.Ordinal) &&
                          string.Equals(binding.EventName, eventName, StringComparison.Ordinal) &&
                          (string.IsNullOrWhiteSpace(binding.EnableCondition) ||
                           conditionEvaluator?.Invoke(binding.EnableCondition) == true))
        .OrderByDescending(binding => binding.Priority)
        .ThenBy(binding => binding.Id)
        .ToArray();

    public static IReadOnlyList<ForgeScriptSourceDiagnostic> ValidateRequirements(
        IEnumerable<ForgeScriptSourceRequirement> requirements,
        IEnumerable<ForgeScriptSourceDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(documents);
        var paths = documents.Select(document => NormalizePath(document.RelativePath)).ToHashSet(StringComparer.Ordinal);
        return requirements.Where(requirement => requirement.RequiredAtRuntime &&
                    !paths.Contains(NormalizePath(requirement.RelativePath)))
            .OrderBy(requirement => requirement.RelativePath, StringComparer.Ordinal)
            .Select(requirement => new ForgeScriptSourceDiagnostic("script.source.required_missing",
                $"script/references/{NormalizePath(requirement.RelativePath)}", requirement.SubjectId, "error",
                $"Required ForgeScript source '{requirement.RelativePath}' is missing."))
            .ToArray();
    }

    public static string EntryDeclaration(string handler, string payloadType) =>
        $"void {handler}(const {payloadType} &in e)";

    public static bool IsSafeSourcePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var normalized = NormalizePath(path);
        return !Path.IsPathRooted(normalized) && normalized.EndsWith(".as", StringComparison.OrdinalIgnoreCase) &&
               normalized.Split('/').All(segment => segment is not ("" or "." or ".."));
    }

    private static bool ContainsDeclaration(string source, string declaration) =>
        Regex.IsMatch(source, Regex.Escape(declaration).Replace("\\ ", "\\s+", StringComparison.Ordinal) + @"\s*\{",
            RegexOptions.CultureInvariant);

    private static string NormalizePath(string? path) => (path ?? string.Empty).Replace('\\', '/').Trim('/');

    [GeneratedRegex("^[a-z][a-z0-9_.-]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierPattern();

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex HandlerPattern();
}
