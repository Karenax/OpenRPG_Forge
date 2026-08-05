using System.Text.Json;
using Forge.Domain.RuntimeExtensions;
using Forge.Scripting;
using Godot;

namespace Forge.Runtime;

public partial class RuntimeDeclarativeUi : CanvasLayer
{
    public event Action<ScriptUiEvent>? EventRaised;

    private ForgeScriptJsonService? _json;
    private ForgeScriptUiEventService? _events;
    private IReadOnlyDictionary<string, SafeJsonHandle> _jsonHandles = new Dictionary<string, SafeJsonHandle>();
    private ModernRuntimeToolingProfile? _tooling;
    private Func<string, string>? _localize;

    public void Configure(ModuleRuntimeExtensionProfile profile, ForgeScriptJsonService json,
        ForgeScriptUiEventService events, IReadOnlyDictionary<string, SafeJsonHandle> jsonHandles,
        Func<string, string>? localize = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _json = json ?? throw new ArgumentNullException(nameof(json));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _jsonHandles = jsonHandles ?? throw new ArgumentNullException(nameof(jsonHandles));
        _tooling = profile.ModernTooling;
        _localize = localize;
        Layer = 30;
        var dock = new VBoxContainer
        {
            Name = "ScriptableUiDock",
            OffsetLeft = 24,
            OffsetTop = 80,
            CustomMinimumSize = new Vector2(360, 0),
        };
        dock.AddThemeConstantOverride("separation", 10);
        AddChild(dock);
        if (_tooling.UiToolkit.ResponsiveLayout)
        {
            dock.AnchorLeft = 0.68f;
            dock.AnchorRight = 0.98f;
            dock.OffsetLeft = 0;
            dock.OffsetRight = 0;
            dock.OffsetTop = 24;
        }
        foreach (var document in profile.UiDocuments)
            dock.AddChild(BuildDocument(document));
    }

    private Control BuildDocument(ScriptableUiDocument document)
    {
        var panel = new PanelContainer { Name = $"UiDocument_{document.Key}" };
        panel.AddThemeStyleboxOverride("panel", PanelStyle());
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 7);
        panel.AddChild(root);
        var heading = new Label { Text = Localize(document.Title), ThemeTypeVariation = "HeaderSmall" };
        heading.AddThemeColorOverride("font_color", new Color("ff9a5c"));
        root.AddChild(heading);
        var containers = new Dictionary<string, Container>(StringComparer.Ordinal);
        var controls = new Dictionary<string, Control>(StringComparer.Ordinal);
        foreach (var widget in document.Widgets)
        {
            var control = BuildWidget(document, widget);
            var parent = widget.ParentKey is not null && containers.TryGetValue(widget.ParentKey, out var found)
                ? found : root;
            parent.AddChild(control);
            controls[widget.Key] = control;
            if (control is Container container) containers[widget.Key] = container;
        }
        if (_tooling?.UiToolkit.DeveloperInspector == true)
        {
            var inspector = new Label
            {
                Name = "ScriptableUiDeveloperInspector",
                Text = $"NUI {_tooling.UiToolkit.ToolkitVersion} · {document.Widgets.Count} widget(s)",
                TooltipText = "Inspecteur développeur runtime en lecture seule",
            };
            inspector.AddThemeColorOverride("font_color", new Color("9b8ab5"));
            root.AddChild(inspector);
        }
        return panel;
    }

    private Control BuildWidget(ScriptableUiDocument document, ScriptableUiWidget widget)
    {
        var text = widget.Kind == ScriptableUiWidgetKind.CheckBox ? Localize(widget.Text) : ResolveText(widget);
        Control control = widget.Kind switch
        {
            ScriptableUiWidgetKind.Panel => new VBoxContainer(),
            ScriptableUiWidgetKind.Form => new GridContainer { Columns = 2 },
            ScriptableUiWidgetKind.Label => new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart },
            ScriptableUiWidgetKind.Button => new Button { Text = text, FocusMode = widget.Interactive ? Control.FocusModeEnum.All : Control.FocusModeEnum.None },
            ScriptableUiWidgetKind.CheckBox => new CheckBox { Text = text, ButtonPressed = ResolveBool(widget), Disabled = !widget.Interactive },
            ScriptableUiWidgetKind.Slider => new HSlider { Value = ResolveNumber(widget), Editable = widget.Interactive },
            ScriptableUiWidgetKind.Progress => new ProgressBar { Value = ResolveNumber(widget), ShowPercentage = true },
            ScriptableUiWidgetKind.TextInput => new LineEdit { Text = text, Editable = widget.Interactive },
            ScriptableUiWidgetKind.List => BuildList(widget),
            ScriptableUiWidgetKind.Canvas => new ColorRect { Color = new Color("241a31"), CustomMinimumSize = new Vector2(220, 120), MouseFilter = Control.MouseFilterEnum.Ignore },
            _ => throw new ArgumentOutOfRangeException(nameof(widget.Kind)),
        };
        control.Name = $"UiWidget_{widget.Key}";
        control.TooltipText = widget.Text;
        if (_tooling?.UiToolkit.Accessibility == true)
        {
            control.SetMeta("accessible_label", text);
            control.SetMeta("focus_order", widget.FocusOrder);
        }
        if (control is Button button)
            button.Pressed += () => Publish(document, widget, ScriptableUiEventKind.Pressed, JsonSerializer.SerializeToElement(new { }));
        if (control is LineEdit input)
            input.TextSubmitted += value => Publish(document, widget, ScriptableUiEventKind.Submitted,
                JsonSerializer.SerializeToElement(new { value }));
        if (control is CheckBox checkBox)
            checkBox.Toggled += value => Publish(document, widget, ScriptableUiEventKind.Toggled,
                JsonSerializer.SerializeToElement(new { value }));
        if (control is Slider slider)
            slider.ValueChanged += value => Publish(document, widget, ScriptableUiEventKind.ValueChanged,
                JsonSerializer.SerializeToElement(new { value }));
        return control;
    }

    private Control BuildList(ScriptableUiWidget widget)
    {
        var list = new VBoxContainer();
        if (widget.BindingKey is null || _json is null) return list;
        var split = widget.BindingKey.Split(':', 2);
        if (split.Length != 2 || !_jsonHandles.TryGetValue(split[0], out var handle)) return list;
        try
        {
            var value = _json.Read(handle, split[1]);
            if (value.ValueKind == JsonValueKind.Array)
                foreach (var item in value.EnumerateArray().Take(100))
                    list.AddChild(new Label { Text = item.ValueKind == JsonValueKind.String ? item.GetString() : item.GetRawText() });
        }
        catch (InvalidOperationException) { }
        return list;
    }

    private string ResolveText(ScriptableUiWidget widget)
    {
        if (widget.BindingKey is null || _json is null) return Localize(widget.Text);
        var split = widget.BindingKey.Split(':', 2);
        if (split.Length != 2 || !_jsonHandles.TryGetValue(split[0], out var handle)) return Localize(widget.Text);
        try
        {
            var value = _json.Read(handle, split[1]);
            return value.ValueKind == JsonValueKind.String ? value.GetString() ?? Localize(widget.Text) : value.GetRawText();
        }
        catch (InvalidOperationException) { return Localize(widget.Text); }
    }

    private string Localize(string value) => _tooling?.UiToolkit.Localization == true && value.StartsWith('@') && _localize is not null
        ? _localize(value[1..]) : value;

    private double ResolveNumber(ScriptableUiWidget widget)
    {
        if (widget.BindingKey is null || _json is null) return 0;
        var split = widget.BindingKey.Split(':', 2);
        if (split.Length != 2 || !_jsonHandles.TryGetValue(split[0], out var handle)) return 0;
        try
        {
            var value = _json.Read(handle, split[1]);
            return value.ValueKind == JsonValueKind.Number ? value.GetDouble() : 0;
        }
        catch (InvalidOperationException) { return 0; }
    }

    private bool ResolveBool(ScriptableUiWidget widget)
    {
        if (widget.BindingKey is null || _json is null) return false;
        var split = widget.BindingKey.Split(':', 2);
        if (split.Length != 2 || !_jsonHandles.TryGetValue(split[0], out var handle)) return false;
        try
        {
            var value = _json.Read(handle, split[1]);
            return value.ValueKind == JsonValueKind.True;
        }
        catch (InvalidOperationException) { return false; }
    }

    private void Publish(ScriptableUiDocument document, ScriptableUiWidget widget,
        ScriptableUiEventKind kind, JsonElement payload)
    {
        if (_events is null) return;
        var binding = document.EventBindings.SingleOrDefault(item => item.WidgetKey == widget.Key && item.Event == kind);
        if (binding is null) return;
        try
        {
            EventRaised?.Invoke(_events.Publish(new ScriptUiHandle(document.Id, document.Key), widget.Key,
                kind.ToString().ToLowerInvariant(), payload, binding.InputContext, binding.Permission));
        }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException)
        {
            GD.PrintErr($"OPENRPG_FORGE_RUNTIME_UI_EVENT_REJECTED code={exception.Message} widget={widget.Key}");
        }
    }

    private static StyleBoxFlat PanelStyle()
    {
        var style = new StyleBoxFlat { BgColor = new Color("171221e8"), BorderColor = new Color("65478a") };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(10);
        style.ContentMarginLeft = style.ContentMarginRight = 14;
        style.ContentMarginTop = style.ContentMarginBottom = 12;
        return style;
    }
}
