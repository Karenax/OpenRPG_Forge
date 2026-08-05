using System.Text.Json;
using Forge.Domain.Modules;
using Forge.Domain.RuntimePolicies;

namespace Forge.Domain.RuntimeExtensions;

public enum RuleSchemaKind
{
    Class,
    Progression,
    Weapon,
    Vfx,
    Casting,
}

public enum ScriptableUiWidgetKind
{
    Panel,
    Form,
    Label,
    Button,
    CheckBox,
    Slider,
    Progress,
    TextInput,
    List,
    Canvas,
}

public enum ScriptableUiEventKind
{
    Pressed,
    ValueChanged,
    Submitted,
    FocusEntered,
    FocusExited,
    Toggled,
}

public sealed record DataDrivenRuleSchema
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Key { get; }
    public RuleSchemaKind Kind { get; }
    public string JsonSchema { get; }
    public string DefaultsJson { get; }
    public bool Required { get; }
    public string Provenance { get; }

    public DataDrivenRuleSchema(Guid id, int schemaVersion, string key, RuleSchemaKind kind,
        string jsonSchema, string defaultsJson, bool required, string provenance)
    {
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion)
            throw new ArgumentOutOfRangeException(nameof(id), "A rule schema requires a stable UUID and schema v1.");
        Id = id;
        SchemaVersion = schemaVersion;
        Key = NormalizeKey(key, nameof(key));
        Kind = kind;
        JsonSchema = RequireJsonObject(jsonSchema, nameof(jsonSchema));
        DefaultsJson = RequireJson(defaultsJson, nameof(defaultsJson));
        Required = required;
        Provenance = RequireText(provenance, nameof(provenance));
    }

    internal static string NormalizeKey(string value, string parameter)
    {
        var normalized = RequireText(value, parameter).ToLowerInvariant();
        if (normalized.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')))
            throw new ArgumentException("Keys may contain only ASCII letters, digits, dots, dashes and underscores.", parameter);
        return normalized;
    }

    internal static string RequireJson(string value, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameter);
        using var document = JsonDocument.Parse(value);
        return document.RootElement.GetRawText();
    }

    internal static string RequireJsonObject(string value, string parameter)
    {
        var canonical = RequireJson(value, parameter);
        using var document = JsonDocument.Parse(canonical);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("A JSON schema must be an object.", parameter);
        return canonical;
    }

    internal static string RequireText(string value, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameter);
        return value.Trim();
    }
}

public sealed record ScriptableUiWidget
{
    public Guid Id { get; }
    public string Key { get; }
    public ScriptableUiWidgetKind Kind { get; }
    public string? ParentKey { get; }
    public string Text { get; }
    public string? BindingKey { get; }
    public bool Interactive { get; }
    public int FocusOrder { get; }
    public string Permission { get; }

    public ScriptableUiWidget(Guid id, string key, ScriptableUiWidgetKind kind, string? parentKey,
        string text, string? bindingKey, bool interactive, int focusOrder, string permission)
    {
        if (id == Guid.Empty || focusOrder < 0) throw new ArgumentOutOfRangeException(nameof(id));
        Id = id;
        Key = DataDrivenRuleSchema.NormalizeKey(key, nameof(key));
        Kind = kind;
        ParentKey = string.IsNullOrWhiteSpace(parentKey) ? null :
            DataDrivenRuleSchema.NormalizeKey(parentKey, nameof(parentKey));
        Text = text?.Trim() ?? string.Empty;
        BindingKey = string.IsNullOrWhiteSpace(bindingKey) ? null : bindingKey.Trim();
        if (BindingKey is { Length: > 256 } || BindingKey?.Any(char.IsControl) == true)
            throw new ArgumentException("UI bindings must be bounded printable paths.", nameof(bindingKey));
        Interactive = interactive;
        FocusOrder = focusOrder;
        Permission = DataDrivenRuleSchema.NormalizeKey(permission, nameof(permission));
    }
}

public sealed record ScriptableUiEventBinding
{
    public Guid Id { get; }
    public string WidgetKey { get; }
    public ScriptableUiEventKind Event { get; }
    public string Command { get; }
    public string ForgeScriptApiVersion { get; }
    public string InputContext { get; }
    public string Permission { get; }

    public ScriptableUiEventBinding(Guid id, string widgetKey, ScriptableUiEventKind @event, string command,
        string forgeScriptApiVersion, string inputContext, string permission)
    {
        if (id == Guid.Empty) throw new ArgumentOutOfRangeException(nameof(id));
        if (!string.Equals(forgeScriptApiVersion, "1.0", StringComparison.Ordinal))
            throw new ArgumentOutOfRangeException(nameof(forgeScriptApiVersion), "ForgeScript UI bindings require API 1.0.");
        Id = id;
        WidgetKey = DataDrivenRuleSchema.NormalizeKey(widgetKey, nameof(widgetKey));
        Event = @event;
        Command = DataDrivenRuleSchema.RequireText(command, nameof(command));
        ForgeScriptApiVersion = forgeScriptApiVersion;
        InputContext = DataDrivenRuleSchema.NormalizeKey(inputContext, nameof(inputContext));
        Permission = DataDrivenRuleSchema.NormalizeKey(permission, nameof(permission));
    }
}

public sealed record ScriptableUiDocument
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Key { get; }
    public string Title { get; }
    public string ThemeKey { get; }
    public bool Required { get; }
    public IReadOnlyList<ScriptableUiWidget> Widgets { get; }
    public IReadOnlyList<ScriptableUiEventBinding> EventBindings { get; }
    public string Provenance { get; }

    public ScriptableUiDocument(Guid id, int schemaVersion, string key, string title, string themeKey,
        bool required, IReadOnlyList<ScriptableUiWidget> widgets,
        IReadOnlyList<ScriptableUiEventBinding> eventBindings, string provenance)
    {
        ArgumentNullException.ThrowIfNull(widgets);
        ArgumentNullException.ThrowIfNull(eventBindings);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion)
            throw new ArgumentOutOfRangeException(nameof(id));
        var orderedWidgets = widgets.OrderBy(item => item.FocusOrder).ThenBy(item => item.Key, StringComparer.Ordinal).ToArray();
        var orderedEvents = eventBindings.OrderBy(item => item.WidgetKey, StringComparer.Ordinal).ThenBy(item => item.Id).ToArray();
        if (orderedWidgets.Select(item => item.Id).Distinct().Count() != orderedWidgets.Length ||
            orderedWidgets.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() != orderedWidgets.Length ||
            orderedEvents.Select(item => item.Id).Distinct().Count() != orderedEvents.Length)
            throw new ArgumentException("UI document UUIDs and widget keys must be unique.");
        var keys = orderedWidgets.Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
        if (orderedWidgets.Any(item => item.ParentKey is not null && !keys.Contains(item.ParentKey)) ||
            orderedEvents.Any(item => !keys.Contains(item.WidgetKey)))
            throw new ArgumentException("UI parents and event bindings must resolve to widgets in the document.");
        Id = id;
        SchemaVersion = schemaVersion;
        Key = DataDrivenRuleSchema.NormalizeKey(key, nameof(key));
        Title = DataDrivenRuleSchema.RequireText(title, nameof(title));
        ThemeKey = DataDrivenRuleSchema.NormalizeKey(themeKey, nameof(themeKey));
        Required = required;
        Widgets = orderedWidgets;
        EventBindings = orderedEvents;
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }
}

public sealed record RuntimeJsonDocumentDefinition
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Key { get; }
    public string JsonSchema { get; }
    public string InitialValue { get; }
    public int MaxBytes { get; }
    public int MaxDepth { get; }
    public bool Mutable { get; }
    public string ForgeScriptApiVersion { get; }
    public string Provenance { get; }

    public RuntimeJsonDocumentDefinition(Guid id, int schemaVersion, string key, string jsonSchema,
        string initialValue, int maxBytes, int maxDepth, bool mutable, string forgeScriptApiVersion,
        string provenance)
    {
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || maxBytes is < 64 or > 1_048_576 ||
            maxDepth is < 1 or > 64 || !string.Equals(forgeScriptApiVersion, "1.0", StringComparison.Ordinal))
            throw new ArgumentOutOfRangeException(nameof(id), "Runtime JSON requires schema v1, API 1.0 and bounded quotas.");
        var canonical = DataDrivenRuleSchema.RequireJson(initialValue, nameof(initialValue));
        if (System.Text.Encoding.UTF8.GetByteCount(canonical) > maxBytes)
            throw new ArgumentOutOfRangeException(nameof(initialValue), "The initial JSON value exceeds its byte quota.");
        using (var parsed = JsonDocument.Parse(canonical, new JsonDocumentOptions { MaxDepth = maxDepth })) { }
        Id = id;
        SchemaVersion = schemaVersion;
        Key = DataDrivenRuleSchema.NormalizeKey(key, nameof(key));
        JsonSchema = DataDrivenRuleSchema.RequireJsonObject(jsonSchema, nameof(jsonSchema));
        InitialValue = canonical;
        MaxBytes = maxBytes;
        MaxDepth = maxDepth;
        Mutable = mutable;
        ForgeScriptApiVersion = forgeScriptApiVersion;
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }
}

public sealed record EntityStateSnapshotPolicy
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public IReadOnlyList<string> Components { get; }
    public int MaxReferences { get; }
    public int MaxInventoryItems { get; }
    public int MaxBlackboardBytes { get; }
    public bool RestoreReferences { get; }
    public string Provenance { get; }

    public EntityStateSnapshotPolicy(Guid id, int schemaVersion, IReadOnlyList<string> components,
        int maxReferences, int maxInventoryItems, int maxBlackboardBytes, bool restoreReferences,
        string provenance)
    {
        ArgumentNullException.ThrowIfNull(components);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || maxReferences is < 0 or > 4096 ||
            maxInventoryItems is < 0 or > 4096 || maxBlackboardBytes is < 64 or > 1_048_576)
            throw new ArgumentOutOfRangeException(nameof(id), "Entity snapshots require schema v1 and bounded quotas.");
        Id = id;
        SchemaVersion = schemaVersion;
        Components = components.Select(item => DataDrivenRuleSchema.NormalizeKey(item, nameof(components)))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        MaxReferences = maxReferences;
        MaxInventoryItems = maxInventoryItems;
        MaxBlackboardBytes = maxBlackboardBytes;
        RestoreReferences = restoreReferences;
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }
}

public sealed record ModuleRuntimeExtensionProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public IReadOnlyList<DataDrivenRuleSchema> RuleSchemas { get; }
    public IReadOnlyList<ScriptableUiDocument> UiDocuments { get; }
    public IReadOnlyList<RuntimeJsonDocumentDefinition> JsonDocuments { get; }
    public EntityStateSnapshotPolicy SnapshotPolicy { get; }
    public ModernRuntimeToolingProfile ModernTooling { get; }
    public OperationalReadinessProfile OperationalReadiness { get; }
    public string Provenance { get; }

    public ModuleRuntimeExtensionProfile(Guid id, int schemaVersion, IReadOnlyList<DataDrivenRuleSchema> ruleSchemas,
        IReadOnlyList<ScriptableUiDocument> uiDocuments, IReadOnlyList<RuntimeJsonDocumentDefinition> jsonDocuments,
        EntityStateSnapshotPolicy snapshotPolicy, string provenance, ModernRuntimeToolingProfile? modernTooling = null,
        OperationalReadinessProfile? operationalReadiness = null)
    {
        ArgumentNullException.ThrowIfNull(ruleSchemas);
        ArgumentNullException.ThrowIfNull(uiDocuments);
        ArgumentNullException.ThrowIfNull(jsonDocuments);
        ArgumentNullException.ThrowIfNull(snapshotPolicy);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion)
            throw new ArgumentOutOfRangeException(nameof(id));
        RuleSchemas = Unique(ruleSchemas, item => item.Id, item => item.Key, nameof(ruleSchemas));
        UiDocuments = Unique(uiDocuments, item => item.Id, item => item.Key, nameof(uiDocuments));
        JsonDocuments = Unique(jsonDocuments, item => item.Id, item => item.Key, nameof(jsonDocuments));
        var modern = modernTooling ?? ModernRuntimeToolingProfile.CreateDefault(id);
        var readiness = operationalReadiness ?? OperationalReadinessProfile.CreateDefault(id);
        var ids = RuleSchemas.Select(item => item.Id).Concat(UiDocuments.Select(item => item.Id))
            .Concat(JsonDocuments.Select(item => item.Id)).Append(id).Append(snapshotPolicy.Id)
            .Append(modern.Id).Append(modern.RuntimeCopy.Id).Append(modern.AreaEffectIndicator.Id)
            .Append(modern.UiToolkit.Id).Append(modern.Compiler.Id).Append(modern.Documentation.Id)
            .Append(readiness.Id).Append(readiness.CrashArtifacts.Id).Append(readiness.ProjectHealth.Id)
            .Append(readiness.Recovery.Id).Append(readiness.ContentSynchronization.Id)
            .Concat(readiness.TaskManifests.Select(item => item.Id)).ToArray();
        if (ids.Distinct().Count() != ids.Length)
            throw new ArgumentException("Runtime extension UUIDs must be unique across the profile.");
        Id = id;
        SchemaVersion = schemaVersion;
        SnapshotPolicy = snapshotPolicy;
        ModernTooling = modern;
        OperationalReadiness = readiness;
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }

    public static ModuleRuntimeExtensionProfile CreateDefault(ModuleId moduleId)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        return new ModuleRuntimeExtensionProfile(moduleId.Value, CurrentSchemaVersion, [], [], [],
            new EntityStateSnapshotPolicy(RuntimePolicyIdFactory.Create(moduleId.Value, "entity-snapshot"), 1,
                ["transform", "combat", "inventory", "blackboard"],
                256, 512, 65_536, true, "migration-default"), "migration-default");
    }

    private static IReadOnlyList<T> Unique<T>(IReadOnlyList<T> values, Func<T, Guid> id,
        Func<T, string> key, string parameter)
    {
        var ordered = values.OrderBy(key, StringComparer.Ordinal).ToArray();
        if (ordered.Select(id).Distinct().Count() != ordered.Length ||
            ordered.Select(key).Distinct(StringComparer.Ordinal).Count() != ordered.Length)
            throw new ArgumentException("Runtime extension UUIDs and keys must be unique.", parameter);
        return ordered;
    }
}
