#if TOOLS
using Forge.Application.Projects;
using Forge.Domain.RuntimeExtensions;
using Godot;

namespace Forge.Toolset.Editor;

public partial class RuntimeExtensionsWindow : Window
{
    private Func<ProjectState?>? _state;
    private Func<ModuleRuntimeExtensionProfile, bool>? _apply;
    private Action<Window>? _brand;
    private ModuleRuntimeExtensionProfile? _profile;
    private TextEdit? _rules;
    private TextEdit? _uiDocuments;
    private TextEdit? _widgets;
    private TextEdit? _events;
    private TextEdit? _json;
    private LineEdit? _components;
    private SpinBox? _maxReferences;
    private SpinBox? _maxInventory;
    private SpinBox? _maxBlackboard;
    private CheckBox? _restoreReferences;
    private TextEdit? _modernTooling;
    private Label? _summary;
    private Label? _status;

    internal string Summary => _profile is null ? "unavailable" :
        $"rules={_profile.RuleSchemas.Count};ui={_profile.UiDocuments.Count};json={_profile.JsonDocuments.Count};" +
        $"components={_profile.SnapshotPolicy.Components.Count};toolkit={_profile.ModernTooling.UiToolkit.ToolkitVersion}";

    public void Initialize(Func<ProjectState?> state, Func<ModuleRuntimeExtensionProfile, bool> apply,
        Action<Window> brand)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _brand = brand ?? throw new ArgumentNullException(nameof(brand));
        Title = "Extensions runtime & UI · OpenRPG Forge";
        MinSize = new Vector2I(1280, 820);
        Size = new Vector2I(1480, 900);
        Transient = true;
        Visible = false;
        Build();
    }

    public void Open()
    {
        var state = _state?.Invoke();
        if (state is null) { _status!.Text = "Aucun projet ouvert."; return; }
        _profile = state.Project.Module.RuntimePolicies.Extensions;
        Populate();
        PopupCentered(Size);
        _brand?.Invoke(this);
    }

    internal bool ConfigureForSmokeTest(ModuleRuntimeExtensionProfile profile)
    {
        _profile = profile;
        Populate();
        return true;
    }

    internal bool ApplyForSmokeTest() => Save();

    private void Build()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 18);
        AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 9); margin.AddChild(root);
        var title = new Label { Text = "RÈGLES DATA-DRIVEN, UI SCRIPTABLE & ÉTAT", ThemeTypeVariation = "HeaderSmall" };
        title.AddThemeColorOverride("font_color", new Color("ff9858")); root.AddChild(title);
        root.AddChild(new Label
        {
            Text = "Éditez les documents canoniques par lignes tabulées. Prévisualisation, validation, sauvegarde et undo utilisent la même commande de domaine.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        _summary = new Label(); _summary.AddThemeColorOverride("font_color", new Color("c7b3dc")); root.AddChild(_summary);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; root.AddChild(scroll);
        var columns = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 10); scroll.AddChild(columns);

        var data = Card(columns, "RÈGLES & JSON");
        data.AddChild(Hint("Règles : type ⇥ clé ⇥ requis ⇥ schéma JSON ⇥ valeurs JSON"));
        _rules = Editor(210); data.AddChild(_rules);
        data.AddChild(Hint("JSON runtime : clé ⇥ mutable ⇥ octets ⇥ profondeur ⇥ API ⇥ schéma ⇥ valeur"));
        _json = Editor(210); data.AddChild(_json);

        var ui = Card(columns, "UI DÉCLARATIVE & ÉVÉNEMENTS");
        ui.AddChild(Hint("Documents : clé ⇥ titre ⇥ thème ⇥ requis"));
        _uiDocuments = Editor(120); ui.AddChild(_uiDocuments);
        ui.AddChild(Hint("Widgets : document ⇥ clé ⇥ type ⇥ parent ⇥ texte ⇥ binding ⇥ interactif ⇥ focus ⇥ permission"));
        _widgets = Editor(190); ui.AddChild(_widgets);
        ui.AddChild(Hint("Événements : document ⇥ widget ⇥ type ⇥ commande ⇥ contexte ⇥ permission"));
        _events = Editor(150); ui.AddChild(_events);

        var state = Card(columns, "SNAPSHOTS D’ENTITÉS");
        state.AddChild(Hint("Composants conservés (séparés par des virgules)"));
        _components = new LineEdit(); state.AddChild(_components);
        _maxReferences = Spin(0, 4096, 256, 1); state.AddChild(Row("Références max.", _maxReferences));
        _maxInventory = Spin(0, 4096, 512, 1); state.AddChild(Row("Inventaire max.", _maxInventory));
        _maxBlackboard = Spin(64, 1_048_576, 65_536, 64); state.AddChild(Row("Blackboard octets", _maxBlackboard));
        _restoreReferences = new CheckBox { Text = "Restaurer les références résolues", ButtonPressed = true };
        state.AddChild(_restoreReferences);
        state.AddChild(Hint("Politiques modernes : copy, aoe, nui, compiler, docs (lignes tabulées)"));
        _modernTooling = Editor(210); state.AddChild(_modernTooling);
        state.AddChild(Hint("Les chemins hôte restent inaccessibles. Les JSON sont validés et bornés avant le runtime."));

        var footer = new HBoxContainer();
        _status = new Label { Text = "Prêt · aucune mutation.", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _status.AddThemeColorOverride("font_color", new Color("9bd9bd")); footer.AddChild(_status);
        var apply = new Button { Text = "Prévisualiser et appliquer", CustomMinimumSize = new Vector2(230, 40) };
        apply.Pressed += () => Save(); footer.AddChild(apply);
        var close = new Button { Text = "Fermer", CustomMinimumSize = new Vector2(110, 40) };
        close.Pressed += Hide; footer.AddChild(close); root.AddChild(footer);
    }

    private void Populate()
    {
        if (_profile is null) return;
        _rules!.Text = string.Join("\n", _profile.RuleSchemas.Select(item =>
            Join(item.Kind, item.Key, item.Required, item.JsonSchema, item.DefaultsJson)));
        _json!.Text = string.Join("\n", _profile.JsonDocuments.Select(item =>
            Join(item.Key, item.Mutable, item.MaxBytes, item.MaxDepth, item.ForgeScriptApiVersion,
                item.JsonSchema, item.InitialValue)));
        _uiDocuments!.Text = string.Join("\n", _profile.UiDocuments.Select(item =>
            Join(item.Key, item.Title, item.ThemeKey, item.Required)));
        _widgets!.Text = string.Join("\n", _profile.UiDocuments.SelectMany(document => document.Widgets.Select(item =>
            Join(document.Key, item.Key, item.Kind, item.ParentKey ?? "-", item.Text, item.BindingKey ?? "-",
                item.Interactive, item.FocusOrder, item.Permission))));
        _events!.Text = string.Join("\n", _profile.UiDocuments.SelectMany(document => document.EventBindings.Select(item =>
            Join(document.Key, item.WidgetKey, item.Event, item.Command, item.InputContext, item.Permission))));
        _components!.Text = string.Join(", ", _profile.SnapshotPolicy.Components);
        _maxReferences!.Value = _profile.SnapshotPolicy.MaxReferences;
        _maxInventory!.Value = _profile.SnapshotPolicy.MaxInventoryItems;
        _maxBlackboard!.Value = _profile.SnapshotPolicy.MaxBlackboardBytes;
        _restoreReferences!.ButtonPressed = _profile.SnapshotPolicy.RestoreReferences;
        var modern = _profile.ModernTooling;
        _modernTooling!.Text = string.Join("\n", new[]
        {
            Join("copy", modern.RuntimeCopy.MaxCopiesPerTransaction, modern.RuntimeCopy.RequireServerAuthority,
                modern.RuntimeCopy.CopyOwnership, modern.RuntimeCopy.ReferenceMode, string.Join(',', modern.RuntimeCopy.StateComponents)),
            Join("aoe", string.Join(',', modern.AreaEffectIndicator.Shapes), modern.AreaEffectIndicator.MaxRange,
                modern.AreaEffectIndicator.MaxHeightDelta, modern.AreaEffectIndicator.EvaluateCover,
                modern.AreaEffectIndicator.RequireServerConfirmation, modern.AreaEffectIndicator.ColorMode,
                modern.AreaEffectIndicator.ValidColor, modern.AreaEffectIndicator.InvalidColor),
            Join("nui", modern.UiToolkit.ToolkitVersion, modern.UiToolkit.ResponsiveLayout, modern.UiToolkit.Forms,
                modern.UiToolkit.Lists, modern.UiToolkit.Canvas, modern.UiToolkit.Accessibility,
                modern.UiToolkit.Localization, modern.UiToolkit.DeveloperInspector),
            Join("compiler", modern.Compiler.Incremental, modern.Compiler.StableDiagnostics,
                modern.Compiler.PerformanceTelemetry, modern.Compiler.MaxParallelism, modern.Compiler.MaxSourceBytes,
                string.Join(',', modern.Compiler.ConformanceSuites)),
            Join("docs", modern.Documentation.OutputDirectory, string.Join(',', modern.Documentation.Catalogs),
                modern.Documentation.IncludeMcpResources),
        });
        _summary!.Text = $"Schéma v{_profile.SchemaVersion} · {Summary} · API ForgeScript 1.0";
        _status!.Text = "Prêt · documents canoniques chargés.";
    }

    private bool Save()
    {
        if (_profile is null) return false;
        try
        {
            var rules = ParseRules();
            var json = ParseJson();
            var ui = ParseUi();
            var components = _components!.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var source = _profile.SnapshotPolicy;
            var snapshot = new EntityStateSnapshotPolicy(source.Id, 1, components,
                (int)_maxReferences!.Value, (int)_maxInventory!.Value, (int)_maxBlackboard!.Value,
                _restoreReferences!.ButtonPressed, "forge-ui");
            var profile = new ModuleRuntimeExtensionProfile(_profile.Id, 1, rules, ui, json, snapshot, "forge-ui",
                ParseModernTooling());
            var succeeded = _apply?.Invoke(profile) == true;
            _status!.Text = succeeded ? "✓ Extensions validées, persistantes et annulables." :
                "✕ Application refusée · consultez les diagnostics.";
            if (succeeded) { _profile = profile; Populate(); }
            return succeeded;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or System.Text.Json.JsonException)
        {
            _status!.Text = $"✕ {exception.Message}";
            return false;
        }
    }

    private ModernRuntimeToolingProfile ParseModernTooling()
    {
        var rows = Lines(_modernTooling!).ToDictionary(parts => Key(parts[0]), StringComparer.Ordinal);
        var source = _profile!.ModernTooling;
        string[] Row(string key, int count)
        {
            if (!rows.TryGetValue(key, out var row)) throw new FormatException($"Politique '{key}' absente.");
            Require(row, count, key); return row;
        }
        var copy = Row("copy", 6); var aoe = Row("aoe", 9); var nui = Row("nui", 9);
        var compiler = Row("compiler", 7); var docs = Row("docs", 4);
        return new ModernRuntimeToolingProfile(source.Id, 1,
            new RuntimeObjectCopyPolicy(source.RuntimeCopy.Id, 1, Int(copy[1]), Bool(copy[2]), Bool(copy[3]),
                EnumValue<RuntimeReferenceCopyMode>(copy[4]), Csv(copy[5]), "forge-ui"),
            new AreaEffectIndicatorProfile(source.AreaEffectIndicator.Id, 1,
                Csv(aoe[1]).Select(EnumValue<AreaEffectShape>).ToArray(), Float(aoe[2]), Float(aoe[3]),
                Bool(aoe[4]), Bool(aoe[5]), EnumValue<TargetIndicatorColorMode>(aoe[6]), aoe[7], aoe[8], "forge-ui"),
            new ScriptableUiToolkitProfile(source.UiToolkit.Id, 1, nui[1], Bool(nui[2]), Bool(nui[3]),
                Bool(nui[4]), Bool(nui[5]), Bool(nui[6]), Bool(nui[7]), Bool(nui[8]), "forge-ui"),
            new ForgeScriptCompilerProfile(source.Compiler.Id, 1, Bool(compiler[1]), Bool(compiler[2]),
                Bool(compiler[3]), Int(compiler[4]), Int(compiler[5]), Csv(compiler[6]), "forge-ui"),
            new GeneratedDocumentationProfile(source.Documentation.Id, 1, docs[1], Csv(docs[2]),
                Bool(docs[3]), "forge-ui"), "forge-ui");
    }

    private IReadOnlyList<DataDrivenRuleSchema> ParseRules()
    {
        var ids = _profile!.RuleSchemas.ToDictionary(item => item.Key, item => item.Id, StringComparer.Ordinal);
        return Lines(_rules!).Select(parts =>
        {
            Require(parts, 5, "règle");
            return new DataDrivenRuleSchema(ids.GetValueOrDefault(Key(parts[1]), Guid.NewGuid()), 1, parts[1],
                EnumValue<RuleSchemaKind>(parts[0]), parts[3], parts[4], Bool(parts[2]), "forge-ui");
        }).ToArray();
    }

    private IReadOnlyList<RuntimeJsonDocumentDefinition> ParseJson()
    {
        var ids = _profile!.JsonDocuments.ToDictionary(item => item.Key, item => item.Id, StringComparer.Ordinal);
        return Lines(_json!).Select(parts =>
        {
            Require(parts, 7, "JSON runtime");
            return new RuntimeJsonDocumentDefinition(ids.GetValueOrDefault(Key(parts[0]), Guid.NewGuid()), 1,
                parts[0], parts[5], parts[6], Int(parts[2]), Int(parts[3]), Bool(parts[1]), parts[4], "forge-ui");
        }).ToArray();
    }

    private IReadOnlyList<ScriptableUiDocument> ParseUi()
    {
        var documentIds = _profile!.UiDocuments.ToDictionary(item => item.Key, item => item.Id, StringComparer.Ordinal);
        var widgetIds = _profile.UiDocuments.SelectMany(document => document.Widgets.Select(widget =>
            (Key: $"{document.Key}:{widget.Key}", widget.Id))).ToDictionary(item => item.Key, item => item.Id, StringComparer.Ordinal);
        var eventIds = _profile.UiDocuments.SelectMany(document => document.EventBindings.Select(binding =>
            (Key: $"{document.Key}:{binding.WidgetKey}:{binding.Event}", binding.Id)))
            .ToDictionary(item => item.Key, item => item.Id, StringComparer.OrdinalIgnoreCase);
        var widgets = Lines(_widgets!).Select(parts =>
        {
            Require(parts, 9, "widget");
            var documentKey = Key(parts[0]); var widgetKey = Key(parts[1]);
            return (Document: documentKey, Widget: new ScriptableUiWidget(
                widgetIds.GetValueOrDefault($"{documentKey}:{widgetKey}", Guid.NewGuid()), widgetKey,
                EnumValue<ScriptableUiWidgetKind>(parts[2]), Optional(parts[3]), parts[4], Optional(parts[5]),
                Bool(parts[6]), Int(parts[7]), parts[8]));
        }).ToArray();
        var events = Lines(_events!).Select(parts =>
        {
            Require(parts, 6, "événement UI");
            var documentKey = Key(parts[0]); var widgetKey = Key(parts[1]);
            var kind = EnumValue<ScriptableUiEventKind>(parts[2]);
            return (Document: documentKey, Binding: new ScriptableUiEventBinding(
                eventIds.GetValueOrDefault($"{documentKey}:{widgetKey}:{kind}", Guid.NewGuid()), widgetKey,
                kind, parts[3], "1.0", parts[4], parts[5]));
        }).ToArray();
        return Lines(_uiDocuments!).Select(parts =>
        {
            Require(parts, 4, "document UI");
            var key = Key(parts[0]);
            return new ScriptableUiDocument(documentIds.GetValueOrDefault(key, Guid.NewGuid()), 1, key,
                parts[1], parts[2], Bool(parts[3]), widgets.Where(item => item.Document == key).Select(item => item.Widget).ToArray(),
                events.Where(item => item.Document == key).Select(item => item.Binding).ToArray(), "forge-ui");
        }).ToArray();
    }

    private static IEnumerable<string[]> Lines(TextEdit editor) => editor.Text
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(line => !line.StartsWith('#'))
        .Select(line => line.Split('\t').Select(item => item.Trim()).ToArray());
    private static void Require(string[] parts, int count, string kind)
    { if (parts.Length != count) throw new FormatException($"Ligne {kind} invalide : {parts.Length}/{count} colonnes."); }
    private static T EnumValue<T>(string value) where T : struct, Enum =>
        Enum.TryParse<T>(value, true, out var parsed) ? parsed : throw new FormatException($"Valeur {typeof(T).Name} invalide : {value}");
    private static bool Bool(string value) => value.Trim().ToLowerInvariant() switch
    { "true" or "oui" or "yes" or "1" => true, "false" or "non" or "no" or "0" => false, _ => throw new FormatException($"Booléen invalide : {value}") };
    private static int Int(string value) => int.TryParse(value, out var parsed) ? parsed : throw new FormatException($"Entier invalide : {value}");
    private static float Float(string value) => float.TryParse(value, System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : throw new FormatException($"Nombre invalide : {value}");
    private static IReadOnlyList<string> Csv(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static string Key(string value) => value.Trim().ToLowerInvariant();
    private static string? Optional(string value) => value is "-" or "" ? null : value;
    private static string Join(params object?[] values) => string.Join('\t', values.Select(value => value?.ToString() ?? "-"));
    private static TextEdit Editor(float height) => new() { CustomMinimumSize = new Vector2(420, height), WrapMode = TextEdit.LineWrappingMode.None };
    private static Label Hint(string text) => new() { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color("bcaacb") };
    private static SpinBox Spin(double min, double max, double value, double step) => new() { MinValue = min, MaxValue = max, Value = value, Step = step };
    private static HBoxContainer Row(string label, Control control)
    { var row = new HBoxContainer(); row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(150, 0) }); control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(control); return row; }
    private static VBoxContainer Card(Container parent, string title)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", Style()); parent.AddChild(panel);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) }; box.AddThemeConstantOverride("separation", 6); panel.AddChild(box);
        var heading = new Label { Text = title }; heading.AddThemeColorOverride("font_color", new Color("ffb06f")); box.AddChild(heading); return box;
    }
    private static StyleBoxFlat Style()
    { var style = new StyleBoxFlat { BgColor = new Color("171221"), BorderColor = new Color("513a6d") }; style.SetBorderWidthAll(1); style.SetCornerRadiusAll(10); style.ContentMarginLeft = style.ContentMarginRight = 12; style.ContentMarginTop = style.ContentMarginBottom = 10; return style; }
}
#endif
