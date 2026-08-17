using System.Text.RegularExpressions;
using Forge.Domain.Lifecycle;
using Forge.Domain.Validation;

namespace Forge.Domain.Areas;

public static partial class AreaUserEventValidator
{
    public const string IdentityInvalidCode = "area.user_event.identity_invalid";
    public const string SchemaUnsupportedCode = "area.user_event.schema_unsupported";
    public const string SchemaUpgradeRequiredCode = "area.user_event.schema_upgrade_required";
    public const string NameInvalidCode = "area.user_event.name_invalid";
    public const string PayloadSchemaInvalidCode = "area.user_event.payload_schema_invalid";
    public const string PayloadFieldsInvalidCode = "area.user_event.payload_fields_invalid";
    public const string ScriptInvalidCode = "area.user_event.script_invalid";
    public const string ApiUnsupportedCode = "area.user_event.api_unsupported";
    public const string SubscriptionsInvalidCode = "area.user_event.subscriptions_invalid";

    private static readonly IReadOnlySet<string> SupportedPayloadTypes =
        new HashSet<string>(["bool", "int", "long", "float", "string", "guid"], StringComparer.Ordinal);

    public static IReadOnlyList<DomainDiagnostic> Validate(AreaDefinition area)
    {
        ArgumentNullException.ThrowIfNull(area);
        var diagnostics = new List<DomainDiagnostic>();
        foreach (var binding in area.Authoring.UserEvents)
            diagnostics.AddRange(Validate(area, binding, requireExecutable: false));
        return diagnostics.OrderBy(item => item.Path, StringComparer.Ordinal)
            .ThenBy(item => item.Code, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<DomainDiagnostic> ValidateExecutable(AreaDefinition area)
    {
        ArgumentNullException.ThrowIfNull(area);
        var diagnostics = new List<DomainDiagnostic>();
        foreach (var binding in area.Authoring.UserEvents)
            diagnostics.AddRange(Validate(area, binding, requireExecutable: true));
        return diagnostics.OrderBy(item => item.Path, StringComparer.Ordinal)
            .ThenBy(item => item.Code, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<DomainDiagnostic> Validate(
        AreaDefinition area,
        AreaUserEvent binding,
        bool requireExecutable)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(binding);
        var path = $"areas/{area.Id}/authoring/userEvents/{binding.Id:D}";
        var diagnostics = new List<DomainDiagnostic>();

        Add(binding.Id == Guid.Empty || string.IsNullOrWhiteSpace(binding.Provenance),
            IdentityInvalidCode, "Area user events require a stable UUID and provenance.", path);
        Add(binding.SchemaVersion is not (AreaUserEvent.LegacySchemaVersion or AreaUserEvent.CurrentSchemaVersion),
            SchemaUnsupportedCode, "Area user-event schema version is unsupported.", $"{path}/schemaVersion");
        if (requireExecutable && binding.SchemaVersion == AreaUserEvent.LegacySchemaVersion)
            Add(true, SchemaUpgradeRequiredCode,
                "Legacy area user events must declare a typed payload before runtime dispatch.",
                $"{path}/schemaVersion");

        if (binding.SchemaVersion == AreaUserEvent.CurrentSchemaVersion)
        {
            var areaPrefix = $"area.{area.Slug}.";
            Add(!binding.NamespacedName.StartsWith(areaPrefix, StringComparison.Ordinal) ||
                binding.NamespacedName.Length <= areaPrefix.Length ||
                binding.NamespacedName.Split('.').Any(segment => !EventSegmentPattern().IsMatch(segment)),
                NameInvalidCode, $"Area user-event names must use the '{areaPrefix}' namespace.",
                $"{path}/namespacedName");
            Add(!IsPayloadSchemaIdentifier(binding.PayloadSchema), PayloadSchemaInvalidCode,
                "Payload schema must be an absolute schema://area/... identifier.", $"{path}/payloadSchema");
            Add(binding.ApiVersion < 1, ApiUnsupportedCode,
                "Payload API version must be positive.", $"{path}/apiVersion");
            Add(!string.Equals(binding.ForgeScriptApiVersion, AreaUserEvent.CurrentForgeScriptApiVersion,
                    StringComparison.Ordinal), ApiUnsupportedCode,
                $"ForgeScript API '{binding.ForgeScriptApiVersion}' is unsupported.",
                $"{path}/forgeScriptApiVersion");
            Add(!ProjectLifecycleEventProfileValidator.IsSafeScriptPath(binding.ScriptPath), ScriptInvalidCode,
                "Area user-event script must be a safe relative scripts/*.forge path.", $"{path}/scriptPath");
            Add(binding.PayloadFields.Count is < 1 or > 32 || binding.PayloadFields.Any(field =>
                    !PayloadFieldPattern().IsMatch(field.Key) || !SupportedPayloadTypes.Contains(field.Value)),
                PayloadFieldsInvalidCode,
                "Typed payloads require 1 to 32 uniquely named bool/int/long/float/string/guid fields.",
                $"{path}/payloadFields");
            Add(binding.Subscriptions.Count is < 1 or > 16 ||
                binding.Subscriptions.Any(handler => !HandlerPattern().IsMatch(handler)),
                SubscriptionsInvalidCode,
                "ForgeScript subscriptions require 1 to 16 unique handler identifiers.",
                $"{path}/subscriptions");
        }
        else if (binding.SchemaVersion == AreaUserEvent.LegacySchemaVersion)
        {
            Add(string.IsNullOrWhiteSpace(binding.NamespacedName) ||
                !binding.NamespacedName.Contains('.', StringComparison.Ordinal), NameInvalidCode,
                "Legacy area user events require a namespaced event name.", $"{path}/namespacedName");
            Add(string.IsNullOrWhiteSpace(binding.PayloadSchema), PayloadSchemaInvalidCode,
                "Legacy area user events require a payload schema identifier.", $"{path}/payloadSchema");
            Add(binding.ApiVersion < 1, ApiUnsupportedCode,
                "Legacy payload API version must be positive.", $"{path}/apiVersion");
            Add(!ProjectLifecycleEventProfileValidator.IsSafeScriptPath(binding.ScriptPath), ScriptInvalidCode,
                "Legacy area user-event script must be a safe relative scripts/*.forge path.",
                $"{path}/scriptPath");
        }

        return diagnostics;

        void Add(bool condition, string code, string message, string diagnosticPath)
        {
            if (condition) diagnostics.Add(new(code, message, diagnosticPath));
        }
    }

    public static bool IsPayloadSchemaIdentifier(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        string.Equals(uri.Scheme, "schema", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(uri.Host, "area", StringComparison.OrdinalIgnoreCase) &&
        uri.AbsolutePath.Length > 1 && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);

    public static bool IsSupportedPayloadType(string? value) =>
        value is not null && SupportedPayloadTypes.Contains(value);

    [GeneratedRegex("^[a-z][a-z0-9_-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex EventSegmentPattern();

    [GeneratedRegex("^[a-z][a-z0-9_]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex PayloadFieldPattern();

    [GeneratedRegex("^(?:main|[A-Za-z_][A-Za-z0-9_]{1,127})$", RegexOptions.CultureInvariant)]
    private static partial Regex HandlerPattern();
}
