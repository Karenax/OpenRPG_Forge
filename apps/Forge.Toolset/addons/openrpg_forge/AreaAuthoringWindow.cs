#if TOOLS
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Godot;
namespace Forge.Toolset.Editor;

public partial class AreaAuthoringWindow : Window
{
    private Func<ProjectState?>? _state; private Func<AreaId?>? _active; private Func<AreaId, AreaAuthoringProfile, bool>? _apply; private Action<Window>? _brand; private AreaAuthoringProfile? _draft;
    private LineEdit? _eventName; private LineEdit? _eventSchema; private LineEdit? _eventScript; private CheckBox? _fog; private SpinBox? _floors; private LineEdit? _pin; private SpinBox? _pinX; private SpinBox? _pinZ; private LineEdit? _layer; private LineEdit? _owner; private CheckBox? _visible; private CheckBox? _locked; private ItemList? _summary; private Label? _status;
    public void Initialize(Func<ProjectState?> state, Func<AreaId?> active, Func<AreaId, AreaAuthoringProfile, bool> apply, Action<Window> brand) { _state = state; _active = active; _apply = apply; _brand = brand; Title = "Carte, événements & calques · OpenRPG Forge"; MinSize = new(1050, 650); Size = MinSize; Transient = true; Visible = false; Build(); }
    public void Open() { Refresh(); PopupCentered(MinSize); _brand?.Invoke(this); }
    private void Build()
    {
        var root = new VBoxContainer { OffsetLeft = 20, OffsetTop = 18, OffsetRight = 1030, OffsetBottom = 630 }; AddChild(root); var h = new Label { Text = "CARTE, ÉVÉNEMENTS & CALQUES", ThemeTypeVariation = "HeaderSmall" }; h.AddThemeColorOverride("font_color", new("ff9858")); root.AddChild(h); var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; root.AddChild(body);
        var ev = Col(body, "ÉVÉNEMENT NAMESPACÉ"); _eventName = Field("module.area.event"); ev.AddChild(_eventName); _eventSchema = Field("schema://events/v1"); ev.AddChild(_eventSchema); _eventScript = Field("scripts/event.forge"); ev.AddChild(_eventScript); var addEvent = new Button { Text = "+ Ajouter événement" }; addEvent.Pressed += AddEvent; ev.AddChild(addEvent);
        var map = Col(body, "MINIMAP & BROUILLARD"); _fog = new CheckBox { Text = "Brouillard de guerre", ButtonPressed = true }; map.AddChild(_fog); _floors = Spin(1, 32, 1); map.AddChild(_floors); _pin = Field("Nom du repère"); map.AddChild(_pin); _pinX = Spin(-4096, 4096, 0); map.AddChild(_pinX); _pinZ = Spin(-4096, 4096, 0); map.AddChild(_pinZ); var addPin = new Button { Text = "+ Ajouter repère" }; addPin.Pressed += AddPin; map.AddChild(addPin);
        var layers = Col(body, "CALQUE IMBRIQUÉ"); _layer = Field("Nom du calque"); layers.AddChild(_layer); _owner = Field("Propriétaire"); layers.AddChild(_owner); _visible = new CheckBox { Text = "Visible", ButtonPressed = true }; layers.AddChild(_visible); _locked = new CheckBox { Text = "Verrouillé" }; layers.AddChild(_locked); var addLayer = new Button { Text = "+ Ajouter calque" }; addLayer.Pressed += AddLayer; layers.AddChild(addLayer);
        _summary = new ItemList { CustomMinimumSize = new(300, 0), SizeFlagsVertical = Control.SizeFlags.ExpandFill }; body.AddChild(_summary); _status = new Label(); root.AddChild(_status); var save = new Button { Text = "Appliquer la configuration" }; save.Pressed += Save; root.AddChild(save);
    }
    private void Refresh() { var id = _active?.Invoke(); _draft = _state?.Invoke()?.Project.Areas.SingleOrDefault(a => a.Id == id)?.Authoring; Render(); }
    private void Render() { if (_draft is null) return; _fog!.ButtonPressed = _draft.Map.FogOfWar; _floors!.Value = _draft.Map.FloorCount; _summary!.Clear(); foreach (var e in _draft.UserEvents) _summary.AddItem($"Événement · {e.NamespacedName}"); foreach (var p in _draft.Map.Pins) _summary.AddItem($"Repère · {p.Label} · étage {p.Floor}"); foreach (var l in _draft.Layers) _summary.AddItem($"Calque · {l.Name} · {l.Owner}{(l.Locked ? " · verrouillé" : "")}"); }
    private void AddEvent() { if (_draft is null) return; try { _draft = new(1, [.. _draft.UserEvents, new AreaUserEvent(Guid.NewGuid(), _eventName!.Text, _eventSchema!.Text, _eventScript!.Text)], _draft.Map, _draft.Layers); Render(); } catch (ArgumentException e) { _status!.Text = e.Message; } }
    private void AddPin() { if (_draft is null || string.IsNullOrWhiteSpace(_pin!.Text)) return; var map = new AreaMapProfile(true, _fog!.ButtonPressed, "orthographic", (int)_floors!.Value, [.. _draft.Map.Pins, new AreaMapPin(Guid.NewGuid(), _pin.Text.Trim(), (float)_pinX!.Value, (float)_pinZ!.Value, 0, false)]); _draft = new(1, _draft.UserEvents, map, _draft.Layers); Render(); }
    private void AddLayer() { if (_draft is null || string.IsNullOrWhiteSpace(_layer!.Text) || string.IsNullOrWhiteSpace(_owner!.Text)) return; _draft = new(1, _draft.UserEvents, _draft.Map, [.. _draft.Layers, new AreaEditorLayer(Guid.NewGuid(), _layer.Text, null, _visible!.ButtonPressed, _locked!.ButtonPressed, _owner.Text, [])]); Render(); }
    private void Save() { var id = _active?.Invoke(); if (id is null || _draft is null) return; _status!.Text = _apply?.Invoke(id, _draft) == true ? "✓ Configuration appliquée et historisée" : "✕ Configuration refusée"; }
    private static VBoxContainer Col(Container p, string title) { var c = new VBoxContainer { CustomMinimumSize = new(235, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; c.AddChild(new Label { Text = title }); p.AddChild(c); return c; }
    private static LineEdit Field(string hint) => new() { PlaceholderText = hint }; private static SpinBox Spin(double min, double max, double value) => new() { MinValue = min, MaxValue = max, Value = value };
}
#endif
