#if TOOLS
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Godot;

namespace Forge.Toolset.Editor;

public partial class AreaEnvironmentWindow : Window
{
    private Func<ProjectState?>? _state; private Func<AreaId?>? _active; private Func<AreaId, AreaEnvironmentProfile, bool>? _apply; private Action<Window>? _brand;
    private OptionButton? _classification; private LineEdit? _tags; private OptionButton? _dayNight; private SpinBox? _dayLength; private SpinBox? _ambient; private SpinBox? _sun; private SpinBox? _temperature;
    private OptionButton? _fogMode; private SpinBox? _fogDensity; private SpinBox? _fogStart; private SpinBox? _fogEnd; private LineEdit? _dayMusic; private LineEdit? _nightMusic; private LineEdit? _combatMusic; private LineEdit? _ambience; private LineEdit? _reverb; private Label? _status;

    public void Initialize(Func<ProjectState?> state, Func<AreaId?> active, Func<AreaId, AreaEnvironmentProfile, bool> apply, Action<Window> brand)
    { _state = state; _active = active; _apply = apply; _brand = brand; Title = "Environnement de zone · OpenRPG Forge"; MinSize = new(980, 690); Size = MinSize; Transient = true; Visible = false; Build(); }
    public void Open() { Refresh(); PopupCentered(MinSize); _brand?.Invoke(this); }
    private void Build()
    {
        var root = new VBoxContainer { OffsetLeft = 22, OffsetTop = 18, OffsetRight = 958, OffsetBottom = 670 }; AddChild(root);
        var h = new Label { Text = "ENVIRONNEMENT DE ZONE", ThemeTypeVariation = "HeaderSmall" }; h.AddThemeColorOverride("font_color", new("ff9858")); root.AddChild(h);
        root.AddChild(new Label { Text = "Classification, cycle, éclairage HDR, brouillard et mixage audio dans un profil canonique." });
        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; root.AddChild(body);
        var general = Col(body, "CLASSIFICATION & LUMIÈRE"); _classification = EnumOption<AreaEnvironmentClass>(); general.AddChild(_classification); _tags = Field("Tags : cave, naturel…"); general.AddChild(_tags); _dayNight = EnumOption<DayNightMode>(); general.AddChild(_dayNight);
        _dayLength = Spin(1, 1440, 24, 1); general.AddChild(Row("Durée du jour (min)", _dayLength)); _ambient = Spin(0, 20, .35, .05); general.AddChild(Row("Ambiante", _ambient)); _sun = Spin(0, 50, 1, .05); general.AddChild(Row("Directionnelle", _sun)); _temperature = Spin(1000, 20000, 6500, 100); general.AddChild(Row("Température K", _temperature));
        var fog = Col(body, "BROUILLARD"); _fogMode = EnumOption<FogMode>(); fog.AddChild(_fogMode); _fogDensity = Spin(0, 1, 0, .01); fog.AddChild(Row("Densité", _fogDensity)); _fogStart = Spin(0, 10000, 20, 1); fog.AddChild(Row("Début", _fogStart)); _fogEnd = Spin(0, 10000, 100, 1); fog.AddChild(Row("Fin", _fogEnd));
        var audio = Col(body, "AUDIO ADAPTATIF"); _dayMusic = Field("Musique jour"); audio.AddChild(_dayMusic); _nightMusic = Field("Musique nuit"); audio.AddChild(_nightMusic); _combatMusic = Field("Musique combat"); audio.AddChild(_combatMusic); _ambience = Field("Ambiance"); audio.AddChild(_ambience); _reverb = Field("Réverbération"); audio.AddChild(_reverb);
        _status = new Label(); root.AddChild(_status); var buttons = new HBoxContainer(); buttons.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }); var save = new Button { Text = "Appliquer" }; save.Pressed += Save; buttons.AddChild(save); var close = new Button { Text = "Fermer" }; close.Pressed += Hide; buttons.AddChild(close); root.AddChild(buttons);
    }
    private void Refresh()
    {
        var id = _active?.Invoke(); var area = _state?.Invoke()?.Project.Areas.SingleOrDefault(item => item.Id == id); if (area is null) return; var p = area.Environment;
        _classification!.Selected = (int)p.Classification; _tags!.Text = string.Join(", ", p.Tags); _dayNight!.Selected = (int)p.Lighting.Mode; _dayLength!.Value = p.Lighting.DayLengthMinutes; _ambient!.Value = p.Lighting.AmbientIntensity; _sun!.Value = p.Lighting.DirectionalIntensity; _temperature!.Value = p.Lighting.ColorTemperatureKelvin; _fogMode!.Selected = (int)p.Fog.Mode; _fogDensity!.Value = p.Fog.Density; _fogStart!.Value = p.Fog.StartDistance; _fogEnd!.Value = p.Fog.EndDistance; _dayMusic!.Text = p.Audio.DayMusic; _nightMusic!.Text = p.Audio.NightMusic; _combatMusic!.Text = p.Audio.CombatMusic; _ambience!.Text = p.Audio.Ambience; _reverb!.Text = p.Audio.ReverbPreset;
    }
    private void Save()
    {
        var id = _active?.Invoke(); if (id is null) return; try { var profile = new AreaEnvironmentProfile(1, (AreaEnvironmentClass)_classification!.Selected, _tags!.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries), new((DayNightMode)_dayNight!.Selected, (float)_dayLength!.Value, (float)_ambient!.Value, (float)_sun!.Value, (float)_temperature!.Value, true, true), new((FogMode)_fogMode!.Selected, (float)_fogDensity!.Value, (float)_fogStart!.Value, (float)_fogEnd!.Value, 0), new(_dayMusic!.Text, _nightMusic!.Text, _combatMusic!.Text, _ambience!.Text, string.IsNullOrWhiteSpace(_reverb!.Text) ? "neutral" : _reverb.Text, 2)); _status!.Text = _apply?.Invoke(id, profile) == true ? "✓ Profil appliqué et historisé" : "✕ Profil refusé"; } catch (ArgumentException e) { _status!.Text = $"✕ {e.Message}"; }
    }
    private static VBoxContainer Col(Container p, string title) { var c = new VBoxContainer { CustomMinimumSize = new(300, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; c.AddChild(new Label { Text = title }); p.AddChild(c); return c; }
    private static OptionButton EnumOption<T>() where T : struct, Enum { var b = new OptionButton(); foreach (var v in Enum.GetValues<T>()) b.AddItem(v.ToString()); return b; }
    private static LineEdit Field(string hint) => new() { PlaceholderText = hint };
    private static SpinBox Spin(double min, double max, double value, double step) => new() { MinValue = min, MaxValue = max, Value = value, Step = step };
    private static HBoxContainer Row(string text, Control c) { var r = new HBoxContainer(); r.AddChild(new Label { Text = text, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }); r.AddChild(c); return r; }
}
#endif
