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

    public void Configure(ModuleRuntimeExtensionProfile profile, ForgeScriptJsonService json,
        ForgeScriptUiEventService events, IReadOnlyDictionary<string, SafeJsonHandle> jsonHandles)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _json = json ?? throw new ArgumentNullException(nameof(json));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _jsonHandles = jsonHandles ?? throw new ArgumentNullException(nameof(jsonHandles));
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
        var heading = new Label { Text = document.Title, ThemeTypeVariation = "HeaderSmall" };
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
        return panel;
    }

    private Control BuildWidget(ScriptableUiDocument document, ScriptableUiWidget widget)
    {
        var text = ResolveText(widget);
        Control control = widget.Kind switch
        {
            ScriptableUiWidgetKind.Panel => new VBoxContainer(),
            ScriptableUiWidgetKind.Label => new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart },
            ScriptableUiWidgetKind.Button => new Button { Text = text, FocusMode = widget.Interactive ? Control.FocusModeEnum.All : Control.FocusModeEnum.None },
            ScriptableUiWidgetKind.Progress => new ProgressBar { Value = ResolveNumber(widget), ShowPercentage = true },
            ScriptableUiWidgetKind.TextInput => new LineEdit { Text = text, Editable = widget.Interactive },
            ScriptableUiWidgetKind.List => new VBoxContainer(),
            _ => throw new ArgumentOutOfRangeException(nameof(widget.Kind)),
        };
        control.Name = $"UiWidget_{widget.Key}";
        control.TooltipText = widget.Text;
        if (control is Button button)
            button.Pressed += () => Publish(document, widget, ScriptableUiEventKind.Pressed, JsonSerializer.SerializeToElement(new { }));
        if (control is LineEdit input)
            input.TextSubmitted += value => Publish(document, widget, ScriptableUiEventKind.Submitted,
                JsonSerializer.SerializeToElement(new { value }));
        return control;
    }

    private string ResolveText(ScriptableUiWidget widget)
    {
        if (widget.BindingKey is null || _json is null) return widget.Text;
        var split = widget.BindingKey.Split(':', 2);
        if (split.Length != 2 || !_jsonHandles.TryGetValue(split[0], out var handle)) return widget.Text;
        try
        {
            var value = _json.Read(handle, split[1]);
            return value.ValueKind == JsonValueKind.String ? value.GetString() ?? widget.Text : value.GetRawText();
        }
        catch (InvalidOperationException) { return widget.Text; }
    }

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
