using System.Text.Json;
using Forge.Domain.RuntimeExtensions;

namespace Forge.Application.RuntimeExtensions;

public sealed record RuntimeExtensionDiagnostic(string Code, string Message, string Path, Guid? SubjectId);
public sealed record RuleSchemaHandle(Guid Id, int SchemaVersion, string Key, RuleSchemaKind Kind,
    JsonElement Defaults, bool Required);
public sealed record ScriptableUiHandle(Guid Id, int SchemaVersion, string Key, string ThemeKey,
    IReadOnlyList<string> WidgetKeys, IReadOnlyList<string> Commands, bool Required);
public sealed record RuntimeJsonHandle(Guid Id, int SchemaVersion, string Key, int MaxBytes, int MaxDepth,
    bool Mutable, string ForgeScriptApiVersion);

public static class RuntimeExtensionRuntimeService
{
    public const string RequiredRuleMissingCode = "runtime.extension.required_rule_missing";
    public const string RequiredUiEmptyCode = "runtime.extension.required_ui_empty";

    public static RuleSchemaHandle? GetRule(ModuleRuntimeExtensionProfile profile, string key,
        bool requiredByConsumer, out RuntimeExtensionDiagnostic? diagnostic)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var normalized = key.Trim().ToLowerInvariant();
        var rule = profile.RuleSchemas.SingleOrDefault(item => item.Key == normalized);
        if (rule is null)
        {
            diagnostic = requiredByConsumer
                ? new RuntimeExtensionDiagnostic(RequiredRuleMissingCode,
                    $"Required data-driven rule '{normalized}' is unavailable.",
                    $"module/runtimePolicies/extensions/ruleSchemas/{normalized}", null)
                : null;
            return null;
        }
        diagnostic = null;
        using var document = JsonDocument.Parse(rule.DefaultsJson);
        return new RuleSchemaHandle(rule.Id, rule.SchemaVersion, rule.Key, rule.Kind,
            document.RootElement.Clone(), rule.Required);
    }

    public static IReadOnlyList<ScriptableUiHandle> GetUiDocuments(ModuleRuntimeExtensionProfile profile) =>
        profile.UiDocuments.Select(document => new ScriptableUiHandle(document.Id, document.SchemaVersion,
            document.Key, document.ThemeKey, document.Widgets.Select(widget => widget.Key).ToArray(),
            document.EventBindings.Select(binding => binding.Command).Distinct(StringComparer.Ordinal).ToArray(),
            document.Required)).ToArray();

    public static IReadOnlyList<RuntimeJsonHandle> GetJsonDocuments(ModuleRuntimeExtensionProfile profile) =>
        profile.JsonDocuments.Select(document => new RuntimeJsonHandle(document.Id, document.SchemaVersion,
            document.Key, document.MaxBytes, document.MaxDepth, document.Mutable,
            document.ForgeScriptApiVersion)).ToArray();

    public static IReadOnlyList<RuntimeExtensionDiagnostic> ValidateRequired(ModuleRuntimeExtensionProfile profile) =>
        profile.UiDocuments.Where(document => document.Required && document.Widgets.Count == 0)
            .Select(document => new RuntimeExtensionDiagnostic(RequiredUiEmptyCode,
                $"Required UI document '{document.Key}' has no widgets.",
                $"module/runtimePolicies/extensions/uiDocuments/{document.Id:D}/widgets", document.Id))
            .ToArray();
}
