using Forge.Domain.Areas;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal static class StudioUx2EnvBControls
{
    internal static LineEdit Text(string placeholder = "")
    {
        var edit = new LineEdit
        {
            PlaceholderText = placeholder,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        StudioStyle.ApplyLineEdit(edit);
        return edit;
    }

    internal static TextEdit Lines(string placeholder, float height = 86)
    {
        var edit = new TextEdit
        {
            PlaceholderText = placeholder,
            CustomMinimumSize = new Vector2(0, height),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            WrapMode = TextEdit.LineWrappingMode.Boundary,
        };
        edit.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.Surface, StudioStyle.Border, 5));
        edit.AddThemeStyleboxOverride("focus", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Accent, 5, 2));
        edit.AddThemeColorOverride("font_color", StudioStyle.Text);
        edit.AddThemeColorOverride("font_placeholder_color", StudioStyle.Muted);
        edit.AddThemeColorOverride("caret_color", StudioStyle.Accent);
        edit.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        return edit;
    }

    internal static SpinBox Number(
        double minimum,
        double maximum,
        double arrowStep,
        bool allowGreater = false,
        bool allowLesser = false)
    {
        var value = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Step = 0,
            CustomArrowStep = arrowStep,
            CustomArrowRound = false,
            AllowGreater = allowGreater,
            AllowLesser = allowLesser,
            UpdateOnTextChanged = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        StudioStyle.ApplySpinBox(value);
        return value;
    }

    internal static OptionButton Options(params string[] values)
    {
        var option = new OptionButton
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FitToLongestItem = false,
        };
        StudioStyle.ApplyOptionButton(option);
        foreach (var value in values) option.AddItem(value);
        return option;
    }

    internal static CheckBox Check(string text)
    {
        var check = new CheckBox { Text = text };
        check.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        check.AddThemeColorOverride("font_color", StudioStyle.Text);
        check.AddThemeColorOverride("font_hover_color", StudioStyle.Text);
        check.AddThemeColorOverride("font_pressed_color", StudioStyle.Text);
        return check;
    }

    internal static VBoxContainer Field(string label, Control control, int minimumWidth = 118)
    {
        var field = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(minimumWidth, 0),
        };
        field.AddThemeConstantOverride("separation", 3);
        field.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        field.AddChild(control);
        return field;
    }

    internal static PanelContainer Card(string name, string title, string help, Control content)
    {
        var panel = StudioStyle.Panel(name, StudioStyle.Surface, 8, 1);
        var margin = StudioStyle.Margin(10, 8, 10, 8);
        panel.AddChild(margin);
        var root = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 6);
        root.AddChild(StudioStyle.Label(title, 12));
        root.AddChild(StudioStyle.Label(help, 9, StudioStyle.Muted, wrap: true));
        root.AddChild(content);
        margin.AddChild(root);
        return panel;
    }

    internal static IReadOnlyDictionary<string, string> ParseOverrides(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in (text ?? string.Empty).Replace("\r", string.Empty).Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            var separator = trimmed.IndexOf('=');
            if (separator <= 0)
                throw new ArgumentException("Each override line requires a key and a value separator.");
            var key = trimmed[..separator].Trim();
            if (!values.TryAdd(key, trimmed[(separator + 1)..].Trim()))
                throw new ArgumentException("Override keys must be unique.");
        }
        return values;
    }

    internal static string FormatOverrides(IReadOnlyDictionary<string, string> values) => string.Join(
        '\n',
        values.OrderBy(item => item.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => $"{item.Key}={item.Value}"));
}

internal sealed class StudioUx2EnvBTransformEditor : VBoxContainer
{
    private readonly SpinBox[] _position;
    private readonly SpinBox[] _rotation;
    private readonly SpinBox[] _scale;
    private bool _updating;

    internal event Action? Changed;

    internal StudioUx2EnvBTransformDraft Value => new(
        new Vector3Value((float)_position[0].Value, (float)_position[1].Value, (float)_position[2].Value),
        new QuaternionValue((float)_rotation[0].Value, (float)_rotation[1].Value,
            (float)_rotation[2].Value, (float)_rotation[3].Value),
        new Vector3Value((float)_scale[0].Value, (float)_scale[1].Value, (float)_scale[2].Value));

    internal StudioUx2EnvBTransformEditor(StudioUx2EnvBTransformDraft value)
    {
        AddThemeConstantOverride("separation", 4);
        var grid = new GridContainer { Columns = 5, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 5);
        grid.AddThemeConstantOverride("v_separation", 3);
        AddChild(grid);
        _position = [Axis(-1_000_000, 1_000_000, .1), Axis(-1_000_000, 1_000_000, .1), Axis(-1_000_000, 1_000_000, .1)];
        _rotation = [Axis(-1, 1, .01), Axis(-1, 1, .01), Axis(-1, 1, .01), Axis(-1, 1, .01)];
        _scale = [Axis(.0001, 1_000_000, .1), Axis(.0001, 1_000_000, .1), Axis(.0001, 1_000_000, .1)];
        AddVectorRow(grid, "Position", _position, ["X", "Y", "Z"]);
        AddVectorRow(grid, "Rotation", _rotation, ["X", "Y", "Z", "W"]);
        AddVectorRow(grid, "Échelle", _scale, ["X", "Y", "Z"]);
        SetValue(value);
        foreach (var control in _position.Concat(_rotation).Concat(_scale))
            control.ValueChanged += _ => { if (!_updating) Changed?.Invoke(); };
    }

    internal void SetEditingEnabled(bool enabled)
    {
        foreach (var control in _position.Concat(_rotation).Concat(_scale)) control.Editable = enabled;
    }

    private void SetValue(StudioUx2EnvBTransformDraft value)
    {
        _updating = true;
        _position[0].Value = value.Position.X;
        _position[1].Value = value.Position.Y;
        _position[2].Value = value.Position.Z;
        _rotation[0].Value = value.Rotation.X;
        _rotation[1].Value = value.Rotation.Y;
        _rotation[2].Value = value.Rotation.Z;
        _rotation[3].Value = value.Rotation.W;
        _scale[0].Value = value.Scale.X;
        _scale[1].Value = value.Scale.Y;
        _scale[2].Value = value.Scale.Z;
        _updating = false;
    }

    private static SpinBox Axis(double minimum, double maximum, double step) =>
        StudioUx2EnvBControls.Number(minimum, maximum, step, allowGreater: true, allowLesser: true);

    private static void AddVectorRow(
        GridContainer grid,
        string title,
        IReadOnlyList<SpinBox> values,
        IReadOnlyList<string> axes)
    {
        grid.AddChild(StudioStyle.Label(title, 9, StudioStyle.Muted));
        for (var index = 0; index < 4; index++)
        {
            if (index < values.Count)
                grid.AddChild(StudioUx2EnvBControls.Field(axes[index], values[index], 78));
            else
                grid.AddChild(new Control());
        }
    }
}

internal interface IStudioUx2EnvBValueRow<T>
{
    Control Control { get; }
    T Value { get; }
    event Action? Changed;
    event Action? RemoveRequested;
    void SetEditingEnabled(bool enabled);
}

internal sealed class StudioUx2EnvBCollectionEditor<T> : VBoxContainer
{
    private readonly VBoxContainer _rows;
    private readonly Button _add;
    private readonly Func<T, IStudioUx2EnvBValueRow<T>> _rowFactory;
    private readonly Func<T> _newFactory;
    private readonly List<IStudioUx2EnvBValueRow<T>> _items = [];
    private bool _editingEnabled;

    internal event Action? Changed;

    internal IReadOnlyList<T> Values => _items.Select(item => item.Value).ToArray();

    internal StudioUx2EnvBCollectionEditor(
        string addLabel,
        Func<T, IStudioUx2EnvBValueRow<T>> rowFactory,
        Func<T> newFactory)
    {
        _rowFactory = rowFactory;
        _newFactory = newFactory;
        AddThemeConstantOverride("separation", 6);
        _rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 6);
        AddChild(_rows);
        _add = StudioStyle.Button(addLabel, compact: true);
        _add.Pressed += () =>
        {
            AddRow(_newFactory());
            Changed?.Invoke();
        };
        AddChild(_add);
    }

    internal void SetValues(IReadOnlyList<T> values)
    {
        foreach (var item in _items) item.Control.QueueFree();
        _items.Clear();
        foreach (var value in values) AddRow(value);
    }

    internal void SetEditingEnabled(bool enabled)
    {
        _editingEnabled = enabled;
        _add.Disabled = !enabled;
        foreach (var item in _items) item.SetEditingEnabled(enabled);
    }

    private void AddRow(T value)
    {
        var row = _rowFactory(value);
        row.Changed += () => Changed?.Invoke();
        row.RemoveRequested += () =>
        {
            _items.Remove(row);
            row.Control.QueueFree();
            Changed?.Invoke();
        };
        row.SetEditingEnabled(_editingEnabled);
        _items.Add(row);
        _rows.AddChild(row.Control);
    }
}

internal abstract class StudioUx2EnvBRowBase<T> : PanelContainer, IStudioUx2EnvBValueRow<T>
{
    private readonly Button _remove;
    protected readonly VBoxContainer Root;

    public Control Control => this;
    public abstract T Value { get; }
    public event Action? Changed;
    public event Action? RemoveRequested;

    protected StudioUx2EnvBRowBase(string title)
    {
        AddThemeStyleboxOverride("panel", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        var margin = StudioStyle.Margin(8, 6, 8, 6);
        AddChild(margin);
        Root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        Root.AddThemeConstantOverride("separation", 5);
        margin.AddChild(Root);
        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label(title, 10, StudioStyle.Accent));
        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(spacer);
        _remove = StudioStyle.Button("Retirer", compact: true);
        _remove.Pressed += () => RemoveRequested?.Invoke();
        heading.AddChild(_remove);
        Root.AddChild(heading);
    }

    protected void Watch(LineEdit control) => control.TextChanged += _ => Changed?.Invoke();
    protected void Watch(TextEdit control) => control.TextChanged += () => Changed?.Invoke();
    protected void Watch(SpinBox control) => control.ValueChanged += _ => Changed?.Invoke();
    protected void Watch(OptionButton control) => control.ItemSelected += _ => Changed?.Invoke();
    protected void Watch(CheckBox control) => control.Toggled += _ => Changed?.Invoke();
    protected void Watch(StudioUx2EnvBTransformEditor control) => control.Changed += () => Changed?.Invoke();

    public virtual void SetEditingEnabled(bool enabled) => _remove.Disabled = !enabled;
}

internal sealed class StudioUx2EnvBCurveRow : StudioUx2EnvBRowBase<StudioUx2EnvBCurveDraft>
{
    private readonly SpinBox _hour;
    private readonly SpinBox _sun;
    private readonly SpinBox _moon;
    private readonly SpinBox _elevation;

    public override StudioUx2EnvBCurveDraft Value => new(
        (float)_hour.Value,
        (float)_sun.Value,
        (float)_moon.Value,
        (float)_elevation.Value);

    internal StudioUx2EnvBCurveRow(StudioUx2EnvBCurveDraft value) : base("Point de courbe")
    {
        var row = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        Root.AddChild(row);
        _hour = StudioUx2EnvBControls.Number(0, 24, .25);
        _sun = StudioUx2EnvBControls.Number(0, 100, .01, allowGreater: true);
        _moon = StudioUx2EnvBControls.Number(0, 100, .01, allowGreater: true);
        _elevation = StudioUx2EnvBControls.Number(-90, 90, .25);
        row.AddChild(StudioUx2EnvBControls.Field("Heure", _hour));
        row.AddChild(StudioUx2EnvBControls.Field("Soleil", _sun));
        row.AddChild(StudioUx2EnvBControls.Field("Lune", _moon));
        row.AddChild(StudioUx2EnvBControls.Field("Altitude solaire (°)", _elevation));
        _hour.Value = value.Hour;
        _sun.Value = value.SunIntensity;
        _moon.Value = value.MoonIntensity;
        _elevation.Value = value.SunElevationDegrees;
        Watch(_hour); Watch(_sun); Watch(_moon); Watch(_elevation);
    }

    public override void SetEditingEnabled(bool enabled)
    {
        base.SetEditingEnabled(enabled);
        _hour.Editable = enabled; _sun.Editable = enabled; _moon.Editable = enabled; _elevation.Editable = enabled;
    }
}

internal sealed class StudioUx2EnvBEventRow : StudioUx2EnvBRowBase<StudioUx2EnvBEventDraft>
{
    private readonly Guid _id;
    private readonly SpinBox _hour;
    private readonly LineEdit _name;
    private readonly LineEdit _script;
    private readonly CheckBox _required;

    public override StudioUx2EnvBEventDraft Value => new(
        _id,
        (float)_hour.Value,
        _name.Text,
        _script.Text,
        _required.ButtonPressed);

    internal StudioUx2EnvBEventRow(StudioUx2EnvBEventDraft value) : base("Événement horaire")
    {
        _id = value.Id;
        var row = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        Root.AddChild(row);
        _hour = StudioUx2EnvBControls.Number(0, 23.9999, .25);
        _name = StudioUx2EnvBControls.Text("Nom humain");
        _script = StudioUx2EnvBControls.Text("scripts/environnement/aube.forge");
        _required = StudioUx2EnvBControls.Check("Requis au runtime");
        row.AddChild(StudioUx2EnvBControls.Field("Heure", _hour));
        row.AddChild(StudioUx2EnvBControls.Field("Événement", _name, 180));
        row.AddChild(StudioUx2EnvBControls.Field("ForgeScript relatif", _script, 260));
        row.AddChild(_required);
        _hour.Value = value.Hour; _name.Text = value.EventName; _script.Text = value.ForgeScriptPath;
        _required.ButtonPressed = value.Required;
        Watch(_hour); Watch(_name); Watch(_script); Watch(_required);
    }

    public override void SetEditingEnabled(bool enabled)
    {
        base.SetEditingEnabled(enabled);
        _hour.Editable = enabled; _name.Editable = enabled; _script.Editable = enabled; _required.Disabled = !enabled;
    }
}

internal sealed class StudioUx2EnvBProbeRow : StudioUx2EnvBRowBase<StudioUx2EnvBProbeDraft>
{
    private readonly Guid _id;
    private readonly Forge.Domain.Blueprints.BlueprintId? _blueprintId;
    private readonly string _blueprintLabel;
    private readonly LineEdit _name;
    private readonly LineEdit _asset;
    private readonly SpinBox _radius;
    private readonly CheckBox _realtime;
    private readonly StudioUx2EnvBTransformEditor _transform;
    private readonly TextEdit _overrides;

    public override StudioUx2EnvBProbeDraft Value => new(
        _id, _name.Text, _transform.Value, _asset.Text, _blueprintId, _blueprintLabel,
        (float)_radius.Value, _realtime.ButtonPressed, StudioUx2EnvBControls.ParseOverrides(_overrides.Text));

    internal StudioUx2EnvBProbeRow(StudioUx2EnvBProbeDraft value) : base("Probe de réflexion")
    {
        _id = value.Id; _blueprintId = value.BlueprintId; _blueprintLabel = value.BlueprintLabel;
        var row = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        Root.AddChild(row);
        _name = StudioUx2EnvBControls.Text("Nom humain");
        _asset = StudioUx2EnvBControls.Text("Ressource de réflexion");
        _radius = StudioUx2EnvBControls.Number(.0001, 1_000_000, .1, allowGreater: true);
        _realtime = StudioUx2EnvBControls.Check("Temps réel");
        row.AddChild(StudioUx2EnvBControls.Field("Nom", _name, 190));
        row.AddChild(StudioUx2EnvBControls.Field("Ressource", _asset, 250));
        row.AddChild(StudioUx2EnvBControls.Field("Rayon (m)", _radius));
        row.AddChild(_realtime);
        Root.AddChild(StudioStyle.Label($"Blueprint préservé : {_blueprintLabel}", 9, StudioStyle.Muted, wrap: true));
        _transform = new StudioUx2EnvBTransformEditor(value.Transform);
        Root.AddChild(StudioUx2EnvBControls.Field("Transform complet", _transform));
        _overrides = StudioUx2EnvBControls.Lines("Une surcharge par ligne : clé=valeur", 64);
        Root.AddChild(StudioUx2EnvBControls.Field("Surcharges typées", _overrides));
        _name.Text = value.Name; _asset.Text = value.AssetId; _radius.Value = value.RadiusMeters;
        _realtime.ButtonPressed = value.Realtime; _overrides.Text = StudioUx2EnvBControls.FormatOverrides(value.Overrides);
        Watch(_name); Watch(_asset); Watch(_radius); Watch(_realtime); Watch(_transform); Watch(_overrides);
    }

    public override void SetEditingEnabled(bool enabled)
    {
        base.SetEditingEnabled(enabled);
        _name.Editable = enabled; _asset.Editable = enabled; _radius.Editable = enabled;
        _realtime.Disabled = !enabled; _transform.SetEditingEnabled(enabled); _overrides.Editable = enabled;
    }
}

internal sealed class StudioUx2EnvBLightingVolumeRow : StudioUx2EnvBRowBase<StudioUx2EnvBLightingVolumeDraft>
{
    private readonly Guid _id;
    private readonly LineEdit _name;
    private readonly LineEdit _preset;
    private readonly OptionButton _fog;
    private readonly SpinBox _blend;
    private readonly StudioUx2EnvBTransformEditor _transform;
    private readonly TextEdit _overrides;

    public override StudioUx2EnvBLightingVolumeDraft Value => new(
        _id, _name.Text, _transform.Value, _preset.Text, (FogMode)_fog.Selected,
        (float)_blend.Value, StudioUx2EnvBControls.ParseOverrides(_overrides.Text));

    internal StudioUx2EnvBLightingVolumeRow(StudioUx2EnvBLightingVolumeDraft value) : base("Volume local de lumière")
    {
        _id = value.Id;
        var row = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        Root.AddChild(row);
        _name = StudioUx2EnvBControls.Text("Nom humain");
        _preset = StudioUx2EnvBControls.Text("Preset local");
        _fog = StudioUx2EnvBControls.Options("Sans brouillard", "Distance", "Volumétrique");
        _blend = StudioUx2EnvBControls.Number(0, 1_000_000, .1, allowGreater: true);
        row.AddChild(StudioUx2EnvBControls.Field("Nom", _name, 190));
        row.AddChild(StudioUx2EnvBControls.Field("Preset", _preset, 180));
        row.AddChild(StudioUx2EnvBControls.Field("Brouillard local", _fog, 170));
        row.AddChild(StudioUx2EnvBControls.Field("Transition (m)", _blend));
        _transform = new StudioUx2EnvBTransformEditor(value.Transform);
        Root.AddChild(StudioUx2EnvBControls.Field("Transform complet", _transform));
        _overrides = StudioUx2EnvBControls.Lines("Une surcharge par ligne : clé=valeur", 64);
        Root.AddChild(StudioUx2EnvBControls.Field("Surcharges typées", _overrides));
        _name.Text = value.Name; _preset.Text = value.LightingPresetKey;
        _fog.Select((int)value.FogMode); _blend.Value = value.BlendDistanceMeters;
        _overrides.Text = StudioUx2EnvBControls.FormatOverrides(value.Overrides);
        Watch(_name); Watch(_preset); Watch(_fog); Watch(_blend); Watch(_transform); Watch(_overrides);
    }

    public override void SetEditingEnabled(bool enabled)
    {
        base.SetEditingEnabled(enabled);
        _name.Editable = enabled; _preset.Editable = enabled; _fog.Disabled = !enabled;
        _blend.Editable = enabled; _transform.SetEditingEnabled(enabled); _overrides.Editable = enabled;
    }
}

internal sealed class StudioUx2EnvBFogVolumeRow : StudioUx2EnvBRowBase<StudioUx2EnvBFogVolumeDraft>
{
    private readonly Guid _id;
    private readonly LineEdit _name;
    private readonly OptionButton _mode;
    private readonly SpinBox _density;
    private readonly SpinBox _falloff;
    private readonly LineEdit _color;
    private readonly SpinBox _blend;
    private readonly StudioUx2EnvBTransformEditor _transform;
    private readonly TextEdit _overrides;

    public override StudioUx2EnvBFogVolumeDraft Value => new(
        _id, _name.Text, _transform.Value, (FogMode)_mode.Selected, (float)_density.Value,
        (float)_falloff.Value, _color.Text, (float)_blend.Value,
        StudioUx2EnvBControls.ParseOverrides(_overrides.Text));

    internal StudioUx2EnvBFogVolumeRow(StudioUx2EnvBFogVolumeDraft value) : base("Volume local de brouillard")
    {
        _id = value.Id;
        var row = new GridContainer { Columns = 5, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        Root.AddChild(row);
        _name = StudioUx2EnvBControls.Text("Nom humain");
        _mode = StudioUx2EnvBControls.Options("Désactivé", "Distance", "Volumétrique");
        _density = StudioUx2EnvBControls.Number(0, 1, .01);
        _falloff = StudioUx2EnvBControls.Number(0, 1_000_000, .1, allowGreater: true);
        _color = StudioUx2EnvBControls.Text("#b8c4d8");
        _blend = StudioUx2EnvBControls.Number(0, 1_000_000, .1, allowGreater: true);
        row.AddChild(StudioUx2EnvBControls.Field("Nom", _name, 170));
        row.AddChild(StudioUx2EnvBControls.Field("Mode", _mode, 150));
        row.AddChild(StudioUx2EnvBControls.Field("Densité", _density));
        row.AddChild(StudioUx2EnvBControls.Field("Décroissance hauteur", _falloff));
        row.AddChild(StudioUx2EnvBControls.Field("Couleur", _color));
        Root.AddChild(StudioUx2EnvBControls.Field("Transition (m)", _blend));
        _transform = new StudioUx2EnvBTransformEditor(value.Transform);
        Root.AddChild(StudioUx2EnvBControls.Field("Transform complet", _transform));
        _overrides = StudioUx2EnvBControls.Lines("Une surcharge par ligne : clé=valeur", 64);
        Root.AddChild(StudioUx2EnvBControls.Field("Surcharges typées", _overrides));
        _name.Text = value.Name; _mode.Select((int)value.Mode); _density.Value = value.Density;
        _falloff.Value = value.HeightFalloff; _color.Text = value.Color; _blend.Value = value.BlendDistanceMeters;
        _overrides.Text = StudioUx2EnvBControls.FormatOverrides(value.Overrides);
        Watch(_name); Watch(_mode); Watch(_density); Watch(_falloff); Watch(_color); Watch(_blend);
        Watch(_transform); Watch(_overrides);
    }

    public override void SetEditingEnabled(bool enabled)
    {
        base.SetEditingEnabled(enabled);
        _name.Editable = enabled; _mode.Disabled = !enabled; _density.Editable = enabled;
        _falloff.Editable = enabled; _color.Editable = enabled; _blend.Editable = enabled;
        _transform.SetEditingEnabled(enabled); _overrides.Editable = enabled;
    }
}

internal sealed class StudioUx2EnvBQualityRow : StudioUx2EnvBRowBase<StudioUx2EnvBQualityDraft>
{
    private readonly LineEdit _platform;
    private readonly OptionButton _tier;

    public override StudioUx2EnvBQualityDraft Value => new(_platform.Text, (AreaFogQualityTier)_tier.Selected);

    internal StudioUx2EnvBQualityRow(StudioUx2EnvBQualityDraft value) : base("Profil qualité")
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        Root.AddChild(row);
        _platform = StudioUx2EnvBControls.Text("desktop");
        _tier = StudioUx2EnvBControls.Options("Bas", "Moyen", "Élevé", "Ultra");
        row.AddChild(StudioUx2EnvBControls.Field("Plateforme", _platform, 220));
        row.AddChild(StudioUx2EnvBControls.Field("Qualité", _tier, 180));
        _platform.Text = value.Platform; _tier.Select((int)value.Tier);
        Watch(_platform); Watch(_tier);
    }

    public override void SetEditingEnabled(bool enabled)
    {
        base.SetEditingEnabled(enabled);
        _platform.Editable = enabled; _tier.Disabled = !enabled;
    }
}

internal sealed class StudioAreaEnvironmentFoundationOverlayUx2EnvB : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx2EnvBEvidenceSourceSnapshot? _evidenceSourceSnapshot;
    private string? _evidenceTempRoot;
    private bool _evidenceUsesRealModuleData;
    private bool _evidenceUsesCanonicalFallback;
    private readonly PanelContainer _dialog;
    private readonly OptionButton _area;
    private readonly Label _selection;
    private readonly TabContainer _tabs;
    private readonly OptionButton _classification;
    private readonly TextEdit _tags;
    private readonly LineEdit _classificationKey;
    private readonly LineEdit _extensionPack;
    private readonly CheckBox[] _capabilities;
    private readonly OptionButton _dayNightMode;
    private readonly SpinBox _dayLength;
    private readonly SpinBox _ambientIntensity;
    private readonly SpinBox _directionalIntensity;
    private readonly SpinBox _temperature;
    private readonly CheckBox _hdr;
    private readonly CheckBox _shadows;
    private readonly LineEdit _lightingPreset;
    private readonly LineEdit _ambientColor;
    private readonly LineEdit _directionalColor;
    private readonly CheckBox _simpleControls;
    private readonly StudioUx2EnvBCollectionEditor<StudioUx2EnvBCurveDraft> _curves;
    private readonly StudioUx2EnvBCollectionEditor<StudioUx2EnvBEventDraft> _events;
    private readonly StudioUx2EnvBCollectionEditor<StudioUx2EnvBProbeDraft> _probes;
    private readonly StudioUx2EnvBCollectionEditor<StudioUx2EnvBLightingVolumeDraft> _lightingVolumes;
    private readonly OptionButton _fogMode;
    private readonly SpinBox _fogDensity;
    private readonly SpinBox _fogStart;
    private readonly SpinBox _fogEnd;
    private readonly SpinBox _fogHeight;
    private readonly LineEdit _fogDayColor;
    private readonly LineEdit _fogNightColor;
    private readonly CheckBox _fogNoiseEnabled;
    private readonly LineEdit _fogNoiseAsset;
    private readonly SpinBox _fogNoiseScale;
    private readonly SpinBox _fogNoiseSpeedX;
    private readonly SpinBox _fogNoiseSpeedZ;
    private readonly SpinBox _fogNoiseContrast;
    private readonly StudioUx2EnvBCollectionEditor<StudioUx2EnvBFogVolumeDraft> _fogVolumes;
    private readonly StudioUx2EnvBCollectionEditor<StudioUx2EnvBQualityDraft> _qualityProfiles;
    private readonly CheckBox _requiredAtRuntime;
    private readonly CheckBox _requireRuntimeMetadata;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;
    private readonly Button _close;
    private IReadOnlyList<StudioUx2EnvBAreaChoice> _choices = [];
    private StudioUx2EnvBDraft? _draft;
    private StudioUx2EnvBPreview? _lastPreview;
    private bool _updating;
    private bool _applied;
    private bool _canonicalPreviewReady;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? AreaEnvironmentChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _choices.Count;
    internal bool HasExplicitSelection => _draft is not null && _area.Selected > 0;
    internal bool CanonicalPreviewReady => _canonicalPreviewReady && _lastPreview is
        { Succeeded: true, CanonicalDryRun: true, TemporalSimulation: true };
    internal bool ShowsCompleteCollections => _draft is not null &&
        _curves.Values.Count == _draft.Curves.Count && _events.Values.Count == _draft.Events.Count &&
        _probes.Values.Count == _draft.Probes.Count && _lightingVolumes.Values.Count == _draft.LightingVolumes.Count &&
        _fogVolumes.Values.Count == _draft.FogVolumes.Count &&
        _qualityProfiles.Values.Count == _draft.FogQualityProfiles.Count;
    internal bool ShowsStructuredDiagnostics => _lastPreview?.Groups.Count == 4;
    internal bool ShowsHumanSurface
    {
        get
        {
            var tabTitles = Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle);
            var text = string.Join(' ', DescendantLabels(this).Select(label => label.Text).Concat(tabTitles));
            return text.Contains("Classification", StringComparison.Ordinal) &&
                   text.Contains("Cycle", StringComparison.Ordinal) &&
                   text.Contains("Éclairage", StringComparison.Ordinal) &&
                   text.Contains("Brouillard", StringComparison.Ordinal) && !ContainsUuid(text);
        }
    }
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                          !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealModuleSource => UsesIsolatedEvidence && _evidenceUsesRealModuleData;
    internal bool UsesCanonicalFallbackEvidence => UsesIsolatedEvidence && _evidenceUsesCanonicalFallback;
    internal bool EvidenceSourceUntouched => SourceSnapshotMatches();
    internal bool EvidenceResourcesReleased => _evidenceSession is null && _evidenceTempRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);

    internal StudioAreaEnvironmentFoundationOverlayUx2EnvB(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(0.015f, 0.02f, 0.027f, .96f);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _dialog = StudioStyle.Panel("Ux2EnvBEnvironmentDialog", StudioStyle.Surface, 12, 1);
        AddChild(_dialog);
        var margin = StudioStyle.Margin(14, 12, 14, 12);
        _dialog.AddChild(margin);
        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(heading);
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        headingText.AddChild(StudioStyle.Label("Fondations d’environnement", 18));
        headingText.AddChild(StudioStyle.Label(
            "Classification, cycle, lumière physique et brouillard · aperçu sec canonique avant toute application.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        _close = StudioStyle.Button("Fermer", compact: true);
        _close.Pressed += Close;
        heading.AddChild(_close);

        _area = StudioUx2EnvBControls.Options();
        _area.ItemSelected += SelectArea;
        root.AddChild(StudioUx2EnvBControls.Field("Zone à modifier · sélection explicite", _area, 420));
        _selection = StudioStyle.Label("Aucune zone sélectionnée.", 10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddChild(_tabs);

        _classification = StudioUx2EnvBControls.Options(
            "Intérieure", "Extérieure", "Naturelle", "Artificielle", "Souterraine");
        _tags = StudioUx2EnvBControls.Lines("Une étiquette par ligne", 76);
        _classificationKey = StudioUx2EnvBControls.Text("clé-sans-espace");
        _extensionPack = StudioUx2EnvBControls.Text("Pack optionnel");
        _capabilities =
        [
            StudioUx2EnvBControls.Check("Rendu"),
            StudioUx2EnvBControls.Check("Audio"),
            StudioUx2EnvBControls.Check("Navigation"),
            StudioUx2EnvBControls.Check("Météo"),
            StudioUx2EnvBControls.Check("Caméra"),
            StudioUx2EnvBControls.Check("Gameplay"),
        ];
        BuildClassificationTab();

        _dayNightMode = StudioUx2EnvBControls.Options("Horloge", "Jour permanent", "Nuit permanente");
        _dayLength = StudioUx2EnvBControls.Number(.0001, 1_000_000, .25, allowGreater: true);
        _curves = new StudioUx2EnvBCollectionEditor<StudioUx2EnvBCurveDraft>(
            "Ajouter un point de courbe",
            value => new StudioUx2EnvBCurveRow(value),
            () => new StudioUx2EnvBCurveDraft(12, 1, 0, 60));
        _events = new StudioUx2EnvBCollectionEditor<StudioUx2EnvBEventDraft>(
            "Ajouter un événement horaire",
            value => new StudioUx2EnvBEventRow(value),
            () => new StudioUx2EnvBEventDraft(Guid.NewGuid(), 12, "Nouvel événement",
                "scripts/environment/event.forge", false));
        BuildCycleTab();

        _ambientIntensity = StudioUx2EnvBControls.Number(0, 1_000_000, .01, allowGreater: true);
        _directionalIntensity = StudioUx2EnvBControls.Number(0, 1_000_000, .01, allowGreater: true);
        _temperature = StudioUx2EnvBControls.Number(1_000, 20_000, 25);
        _hdr = StudioUx2EnvBControls.Check("HDR");
        _shadows = StudioUx2EnvBControls.Check("Ombres");
        _lightingPreset = StudioUx2EnvBControls.Text("Preset physique");
        _ambientColor = StudioUx2EnvBControls.Text("#596273");
        _directionalColor = StudioUx2EnvBControls.Text("#fff1d6");
        _simpleControls = StudioUx2EnvBControls.Check("Commandes simples");
        _probes = new StudioUx2EnvBCollectionEditor<StudioUx2EnvBProbeDraft>(
            "Ajouter une probe",
            value => new StudioUx2EnvBProbeRow(value),
            () => new StudioUx2EnvBProbeDraft(Guid.NewGuid(), "Nouvelle probe",
                new StudioUx2EnvBTransformDraft(Vector3Value.Zero, QuaternionValue.Identity, Vector3Value.One),
                "builtin://openrpg/environment/reflection-neutral", null, "Aucun blueprint associé",
                10, false, new Dictionary<string, string>(StringComparer.Ordinal)));
        _lightingVolumes = new StudioUx2EnvBCollectionEditor<StudioUx2EnvBLightingVolumeDraft>(
            "Ajouter un volume de lumière",
            value => new StudioUx2EnvBLightingVolumeRow(value),
            () => new StudioUx2EnvBLightingVolumeDraft(Guid.NewGuid(), "Nouveau volume",
                new StudioUx2EnvBTransformDraft(Vector3Value.Zero, QuaternionValue.Identity, Vector3Value.One),
                "physical-neutral", FogMode.Disabled, 2,
                new Dictionary<string, string>(StringComparer.Ordinal)));
        BuildLightingTab();

        _fogMode = StudioUx2EnvBControls.Options("Désactivé", "Distance", "Volumétrique");
        _fogDensity = StudioUx2EnvBControls.Number(0, 1, .01);
        _fogStart = StudioUx2EnvBControls.Number(0, 1_000_000, .1, allowGreater: true);
        _fogEnd = StudioUx2EnvBControls.Number(0, 1_000_000, .1, allowGreater: true);
        _fogHeight = StudioUx2EnvBControls.Number(-1_000_000, 1_000_000, .1, true, true);
        _fogDayColor = StudioUx2EnvBControls.Text("#b8c4d8");
        _fogNightColor = StudioUx2EnvBControls.Text("#25324a");
        _fogNoiseEnabled = StudioUx2EnvBControls.Check("Bruit procédural actif");
        _fogNoiseAsset = StudioUx2EnvBControls.Text("Ressource de bruit");
        _fogNoiseScale = StudioUx2EnvBControls.Number(.0001, 1_000_000, .1, true);
        _fogNoiseSpeedX = StudioUx2EnvBControls.Number(-1_000_000, 1_000_000, .01, true, true);
        _fogNoiseSpeedZ = StudioUx2EnvBControls.Number(-1_000_000, 1_000_000, .01, true, true);
        _fogNoiseContrast = StudioUx2EnvBControls.Number(0, 4, .01);
        _fogVolumes = new StudioUx2EnvBCollectionEditor<StudioUx2EnvBFogVolumeDraft>(
            "Ajouter un volume de brouillard",
            value => new StudioUx2EnvBFogVolumeRow(value),
            () => new StudioUx2EnvBFogVolumeDraft(Guid.NewGuid(), "Nouveau volume",
                new StudioUx2EnvBTransformDraft(Vector3Value.Zero, QuaternionValue.Identity, Vector3Value.One),
                FogMode.Distance, .1f, .5f, "#b8c4d8", 2,
                new Dictionary<string, string>(StringComparer.Ordinal)));
        _qualityProfiles = new StudioUx2EnvBCollectionEditor<StudioUx2EnvBQualityDraft>(
            "Ajouter un profil qualité",
            value => new StudioUx2EnvBQualityRow(value),
            () => new StudioUx2EnvBQualityDraft("nouvelle-plateforme", AreaFogQualityTier.Medium));
        _requiredAtRuntime = StudioUx2EnvBControls.Check("Profil authoring requis au runtime");
        _requireRuntimeMetadata = StudioUx2EnvBControls.Check("Bloquer le build si les métadonnées manquent");
        BuildFogTab();

        _diff = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _diagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        BuildReviewTab();

        _summary = StudioStyle.Label(
            "Sélectionnez une zone puis lancez l’aperçu sec · aucun changement appliqué.",
            10, StudioStyle.Muted, wrap: true);
        _summary.CustomMinimumSize = new Vector2(0, 46);
        root.AddChild(_summary);

        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 6);
        root.AddChild(actions);
        _preview = StudioStyle.Button("Prévisualiser", compact: true);
        _apply = StudioStyle.Button("Appliquer", compact: true);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        {
            button.CustomMinimumSize = new Vector2(116, 34);
            actions.AddChild(button);
        }
        _preview.Pressed += Preview;
        _apply.Pressed += Apply;
        _undo.Pressed += Undo;
        _redo.Pressed += Redo;
        _save.Pressed += Save;
        var actionNote = StudioStyle.Label(
            "L’aperçu est une simulation bornée : aucun événement ni rendu live n’est exécuté.",
            9, StudioStyle.Muted, wrap: true);
        actionNote.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        actionNote.CustomMinimumSize = new Vector2(260, 34);
        actions.AddChild(actionNote);
        _status = StudioStyle.Label("Prêt · sélection explicite requise", 9, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        ConnectInvalidation();
        SetEditingEnabled(false);
        ResetControls();
    }

    internal void Open()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null) ReleaseIsolatedEvidenceSession();
        _session = _sourceSession;
        Visible = true;
        UpdateDialogBounds();
        RefreshChoices();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceTempRoot is null) return;
        if (Visible || !ReferenceEquals(_session, _sourceSession))
            _session.CancelAreaEnvironmentUx2EnvB();
        Visible = false;
        _draft = null;
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _applied = false;
        ReleaseIsolatedEvidenceSession();
        _session = _sourceSession;
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        var approval = _sourceSession.CaptureAreaEnvironmentApprovalUx2EnvB();
        _session = _sourceSession;
        Visible = true;
        UpdateDialogBounds();
        RefreshChoices();
        _sourceSession.RestoreAreaEnvironmentApprovalUx2EnvB(approval);
    }

    internal bool PrepareEvidence(string mode)
    {
        var sourceApproval = _sourceSession.CaptureAreaEnvironmentApprovalUx2EnvB();
        Close();
        _sourceSession.RestoreAreaEnvironmentApprovalUx2EnvB(sourceApproval);
        var sourceProject = _sourceSession.Project;
        if (sourceProject is null || _sourceSession.History is null || sourceProject.Areas.Count == 0)
            return false;
        _evidenceSourceSnapshot = CaptureSourceSnapshot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux2-env-b-{Guid.NewGuid():N}");
        var evidenceSession = new StudioSessionController();
        try
        {
            var realArea = sourceProject.Areas.FirstOrDefault(area =>
                area.Environment.Authoring.Lighting.Curves.Count >= 2 &&
                area.Environment.Authoring.Fog.QualityProfiles.Count >= 1);
            _evidenceUsesRealModuleData = realArea is not null;
            _evidenceUsesCanonicalFallback = realArea is null;
            var evidenceProject = realArea is null
                ? StudioSessionController.CreateUx2EnvBEvidenceProject(sourceProject)
                : sourceProject;
            var evidenceAreaId = realArea?.Id ?? evidenceProject.Areas[0].Id;
            var created = ProjectSession.Create(tempRoot, evidenceProject);
            if (!created.Succeeded || !evidenceSession.Open(tempRoot).Succeeded)
            {
                evidenceSession.Close();
                StudioSessionController.DeleteUx2EnvBTemporaryWorkspace(tempRoot);
                _evidenceSourceSnapshot = null;
                _evidenceUsesRealModuleData = false;
                _evidenceUsesCanonicalFallback = false;
                return false;
            }
            _evidenceSession = evidenceSession;
            _evidenceTempRoot = tempRoot;
            _session = evidenceSession;
            Visible = true;
            UpdateDialogBounds();
            RefreshChoices();
            var evidenceChoiceIndex = _choices.ToList().FindIndex(choice => choice.AreaId == evidenceAreaId);
            if (evidenceChoiceIndex < 0)
            {
                Close();
                return false;
            }
            _area.Select(evidenceChoiceIndex + 1);
            LoadChoice(evidenceChoiceIndex);
            if (!ConfigureEvidenceDraft())
            {
                Close();
                return false;
            }
            Preview();
            var ready = CanonicalPreviewReady && HasExplicitSelection && ShowsCompleteCollections &&
                        ShowsStructuredDiagnostics && ShowsHumanSurface && UsesIsolatedEvidence &&
                        (UsesRealModuleSource ^ UsesCanonicalFallbackEvidence) && EvidenceSourceUntouched;
            if (!ready)
            {
                Close();
                return false;
            }
            return true;
        }
        catch (Exception exception)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_ENV_B_EVIDENCE_FAILED type={exception.GetType().Name} message={exception.Message}");
            try { evidenceSession.Close(); }
            catch (Exception releaseException)
            {
                GD.PrintErr(
                    $"OPENRPG_FORGE_STUDIO_UX2_ENV_B_EVIDENCE_RELEASE_FAILED type={releaseException.GetType().Name} message={releaseException.Message}");
            }
            StudioSessionController.DeleteUx2EnvBTemporaryWorkspace(tempRoot);
            _evidenceSession = null;
            _evidenceTempRoot = null;
            _evidenceSourceSnapshot = null;
            _evidenceUsesRealModuleData = false;
            _evidenceUsesCanonicalFallback = false;
            _session = _sourceSession;
            Visible = false;
            return false;
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        var sourceSnapshot = CaptureSourceSnapshot();
        if (!PrepareEvidence("ux2-env-b-area-environment") || !CanonicalPreviewReady || !UsesIsolatedEvidence)
        {
            Close();
            return false;
        }
        var isolated = _session;
        var approved = _lastPreview is { Succeeded: true, CanApply: true };
        if (!approved)
        {
            Close();
            return false;
        }
        Apply();
        var applied = _applied && isolated.Project is not null;
        var appliedJson = applied ? ProjectJsonSerializer.Serialize(isolated.Project!) : string.Empty;
        Undo();
        var undone = !_applied;
        Redo();
        var redone = _applied && isolated.Project is not null &&
                     string.Equals(appliedJson, ProjectJsonSerializer.Serialize(isolated.Project!),
                         StringComparison.Ordinal);
        Save();
        var reopen = new StudioSessionController();
        var roundTrip = false;
        try
        {
            var opened = _evidenceTempRoot is not null && reopen.Open(_evidenceTempRoot).Succeeded;
            roundTrip = opened && reopen.Project is not null &&
                        string.Equals(appliedJson, ProjectJsonSerializer.Serialize(reopen.Project!),
                            StringComparison.Ordinal);
        }
        finally
        {
            reopen.Close();
        }
        Close();
        return applied && undone && redone && roundTrip && SourceSnapshotMatches(sourceSnapshot) &&
               EvidenceResourcesReleased && SourceSessionRestored;
    }

    private void BuildClassificationTab()
    {
        var tab = ScrollTab("Classification");
        var identity = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        identity.AddChild(StudioUx2EnvBControls.Field("Famille intégrée", _classification, 240));
        identity.AddChild(StudioUx2EnvBControls.Field("Étiquettes", _tags, 320));
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvBClassification", "Type de zone",
            "Le type intégré reste cohérent avec la clé extensible ; aucune autre zone n’est reclassée.", identity));
        var extension = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        extension.AddChild(StudioUx2EnvBControls.Field("Clé sans espace", _classificationKey, 260));
        extension.AddChild(StudioUx2EnvBControls.Field("Pack d’extension", _extensionPack, 260));
        var capabilities = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        capabilities.AddThemeConstantOverride("separation", 12);
        foreach (var capability in _capabilities) capabilities.AddChild(capability);
        var extensionRoot = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        extensionRoot.AddChild(extension);
        extensionRoot.AddChild(StudioStyle.Label("Sous-systèmes pilotés", 9, StudioStyle.Muted));
        extensionRoot.AddChild(capabilities);
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvBCapabilities", "Classification extensible",
            "Au moins une capacité doit rester active. Le pack est une provenance, pas une activation automatique.",
            extensionRoot));
    }

    private void BuildCycleTab()
    {
        var tab = ScrollTab("Cycle jour/nuit");
        var baseRow = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        baseRow.AddChild(StudioUx2EnvBControls.Field("Mode", _dayNightMode, 220));
        baseRow.AddChild(StudioUx2EnvBControls.Field("Durée du jour (minutes)", _dayLength, 220));
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvBCycle", "Horloge de zone",
            "La durée alimente l’aperçu temporel ; elle ne démarre aucun scheduler live.", baseRow));
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvBCurves", "Courbes soleil et lune",
            "Tous les points sont chargés, éditables et réordonnés seulement par l’auteur.", _curves));
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvBEvents", "Événements horaires ForgeScript",
            "Les chemins restent relatifs. L’aperçu liste les événements mais n’exécute aucun script.", _events));
    }

    private void BuildLightingTab()
    {
        var tab = ScrollTab("Éclairage");
        var physical = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        physical.AddChild(StudioUx2EnvBControls.Field("Ambiance", _ambientIntensity));
        physical.AddChild(StudioUx2EnvBControls.Field("Directionnelle", _directionalIntensity));
        physical.AddChild(StudioUx2EnvBControls.Field("Température (K)", _temperature));
        var flags = new HBoxContainer(); flags.AddChild(_hdr); flags.AddChild(_shadows); flags.AddChild(_simpleControls);
        physical.AddChild(StudioUx2EnvBControls.Field("Rendu", flags, 250));
        physical.AddChild(StudioUx2EnvBControls.Field("Preset", _lightingPreset, 220));
        physical.AddChild(StudioUx2EnvBControls.Field("Couleur ambiante", _ambientColor));
        physical.AddChild(StudioUx2EnvBControls.Field("Couleur directionnelle", _directionalColor));
        physical.AddChild(new Control());
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvBPhysicalLight", "Lumière physique",
            "Intensités, température, couleurs, HDR, ombres et niveau de contrôle restent explicites.", physical));
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvBProbes", "Probes de réflexion",
            "Transforms complets, ressources, rayon, mode temps réel et surcharges sont conservés.", _probes));
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvBLightVolumes", "Overrides locaux par volume",
            "Chaque volume garde son transform, son preset, son brouillard, son blend et ses surcharges.",
            _lightingVolumes));
    }

    private void BuildFogTab()
    {
        var tab = ScrollTab("Brouillard");
        var global = new GridContainer { Columns = 5, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        global.AddChild(StudioUx2EnvBControls.Field("Mode", _fogMode, 160));
        global.AddChild(StudioUx2EnvBControls.Field("Densité", _fogDensity));
        global.AddChild(StudioUx2EnvBControls.Field("Début (m)", _fogStart));
        global.AddChild(StudioUx2EnvBControls.Field("Fin (m)", _fogEnd));
        global.AddChild(StudioUx2EnvBControls.Field("Hauteur", _fogHeight));
        global.AddChild(StudioUx2EnvBControls.Field("Couleur jour", _fogDayColor));
        global.AddChild(StudioUx2EnvBControls.Field("Couleur nuit", _fogNightColor));
        global.AddChild(_requiredAtRuntime);
        global.AddChild(_requireRuntimeMetadata);
        global.AddChild(new Control());
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvBFog", "Profil global et exigence runtime",
            "La garde de build reste séparée du mode visuel ; aucun fallback n’est présenté comme une preuve runtime.",
            global));
        var noise = new GridContainer { Columns = 5, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        noise.AddChild(StudioUx2EnvBControls.Field("Ressource", _fogNoiseAsset, 220));
        noise.AddChild(StudioUx2EnvBControls.Field("Échelle (m)", _fogNoiseScale));
        noise.AddChild(StudioUx2EnvBControls.Field("Vitesse X", _fogNoiseSpeedX));
        noise.AddChild(StudioUx2EnvBControls.Field("Vitesse Z", _fogNoiseSpeedZ));
        noise.AddChild(StudioUx2EnvBControls.Field("Contraste", _fogNoiseContrast));
        var noiseRoot = new VBoxContainer(); noiseRoot.AddChild(_fogNoiseEnabled); noiseRoot.AddChild(noise);
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvBFogNoise", "Bruit procédural",
            "La ressource et les paramètres sont enregistrés, jamais exécutés par cet aperçu.", noiseRoot));
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvBFogVolumes", "Volumes locaux",
            "Tous les volumes conservent leur transform, mode, densité, couleur, blend et surcharges.", _fogVolumes));
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvBQuality", "Profils qualité",
            "Chaque plateforme garde un niveau explicite ; au moins un profil est obligatoire.", _qualityProfiles));
    }

    private void BuildReviewTab()
    {
        var tab = ScrollTab("Revue");
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvBDiff", "Diff sémantique",
            "Classification, cycle, lumière et brouillard sont résumés sans chemins internes.", _diff));
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvBDiagnostics", "Validation canonique",
            "Les erreurs restent groupées par intention humaine avant toute application.", _diagnostics));
    }

    private VBoxContainer ScrollTab(string title)
    {
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(900, 0),
        };
        content.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(content);
        _tabs.AddChild(scroll);
        _tabs.SetTabTitle(_tabs.GetTabCount() - 1, title);
        return content;
    }

    private void RefreshChoices()
    {
        _updating = true;
        _choices = _session.AreaEnvironmentChoicesUx2EnvB;
        _area.Clear();
        _area.AddItem("Choisir explicitement une zone…");
        foreach (var choice in _choices) _area.AddItem(choice.Label);
        _area.Select(0);
        _updating = false;
        ClearSelection();
    }

    private void SelectArea(long index)
    {
        if (_updating) return;
        var choiceIndex = (int)index - 1;
        if (choiceIndex < 0 || choiceIndex >= _choices.Count) ClearSelection();
        else LoadChoice(choiceIndex);
    }

    private void ClearSelection()
    {
        _draft = null; _lastPreview = null; _applied = false; _canonicalPreviewReady = false;
        _session.CancelAreaEnvironmentUx2EnvB();
        ResetControls();
        _selection.Text = "Choisissez explicitement une zone ; aucune fiche n’est sélectionnée automatiquement.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
        _summary.Text = "Sélectionnez une zone puis lancez l’aperçu sec · aucun changement appliqué.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        SetEditingEnabled(false);
        UpdateTransactionButtons();
        SetStatus("Prêt · sélection explicite requise", false);
    }

    private void LoadChoice(int choiceIndex)
    {
        var choice = _choices[choiceIndex];
        _session.CancelAreaEnvironmentUx2EnvB();
        _draft = _session.CreateAreaEnvironmentDraftUx2EnvB(choice.AreaId);
        _lastPreview = null; _applied = false; _canonicalPreviewReady = false;
        if (_draft is null)
        {
            ResetControls();
            _selection.Text = "La zone choisie a changé. Rechargez la page Environnement.";
            _selection.AddThemeColorOverride("font_color", StudioStyle.Danger);
            SetEditingEnabled(false);
            UpdateTransactionButtons();
            SetStatus("Zone indisponible · aucune mutation", true);
            return;
        }
        Populate(_draft);
        _selection.Text = choice.Label + (choice.HasAuthoredEnvironment
            ? " · métadonnées authoring présentes"
            : " · profil migré, première application explicitement authorée");
        _selection.AddThemeColorOverride("font_color", StudioStyle.Accent);
        _summary.Text = "Profil complet chargé · voisins et sous-profils non affichés préservés · aperçu sec obligatoire.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        SetEditingEnabled(true);
        UpdateTransactionButtons();
        SetStatus("Brouillon prêt · aperçu sec requis", false);
    }

    private void Populate(StudioUx2EnvBDraft draft)
    {
        _updating = true;
        _classification.Select((int)draft.Classification);
        _tags.Text = string.Join('\n', draft.Tags);
        _classificationKey.Text = draft.ClassificationKey;
        _extensionPack.Text = draft.ExtensionPack;
        var values = new[] { draft.DrivesRendering, draft.DrivesAudio, draft.DrivesNavigation,
            draft.DrivesWeather, draft.DrivesCamera, draft.DrivesGameplay };
        for (var index = 0; index < _capabilities.Length; index++) _capabilities[index].ButtonPressed = values[index];
        _dayNightMode.Select((int)draft.DayNightMode); _dayLength.Value = draft.DayLengthMinutes;
        _ambientIntensity.Value = draft.AmbientIntensity; _directionalIntensity.Value = draft.DirectionalIntensity;
        _temperature.Value = draft.ColorTemperatureKelvin; _hdr.ButtonPressed = draft.Hdr;
        _shadows.ButtonPressed = draft.Shadows; _lightingPreset.Text = draft.LightingPresetKey;
        _ambientColor.Text = draft.AmbientColor; _directionalColor.Text = draft.DirectionalColor;
        _simpleControls.ButtonPressed = draft.SimpleControls;
        _curves.SetValues(draft.Curves); _events.SetValues(draft.Events); _probes.SetValues(draft.Probes);
        _lightingVolumes.SetValues(draft.LightingVolumes);
        _fogMode.Select((int)draft.FogMode); _fogDensity.Value = draft.FogDensity;
        _fogStart.Value = draft.FogStartDistance; _fogEnd.Value = draft.FogEndDistance; _fogHeight.Value = draft.FogHeight;
        _fogDayColor.Text = draft.FogDayColor; _fogNightColor.Text = draft.FogNightColor;
        _fogNoiseEnabled.ButtonPressed = draft.FogNoise is not null;
        _fogNoiseAsset.Text = draft.FogNoise?.AssetId ?? string.Empty;
        _fogNoiseScale.Value = draft.FogNoise?.ScaleMeters ?? 1;
        _fogNoiseSpeedX.Value = draft.FogNoise?.SpeedX ?? 0;
        _fogNoiseSpeedZ.Value = draft.FogNoise?.SpeedZ ?? 0;
        _fogNoiseContrast.Value = draft.FogNoise?.Contrast ?? 1;
        _fogVolumes.SetValues(draft.FogVolumes); _qualityProfiles.SetValues(draft.FogQualityProfiles);
        _requiredAtRuntime.ButtonPressed = draft.RequiredAtRuntime;
        _requireRuntimeMetadata.ButtonPressed = draft.RequireAuthoringRuntimeMetadata;
        _updating = false;
        RenderDiff(null); RenderDiagnostics([]);
    }

    private StudioUx2EnvBDraft? ReadDraft()
    {
        if (_draft is null) return null;
        var tags = _tags.Text.Replace("\r", string.Empty).Split('\n')
            .Select(item => item.Trim()).Where(item => item.Length > 0).ToArray();
        return _draft with
        {
            Classification = (AreaEnvironmentClass)_classification.Selected,
            Tags = tags,
            ClassificationKey = _classificationKey.Text,
            ExtensionPack = _extensionPack.Text,
            DrivesRendering = _capabilities[0].ButtonPressed,
            DrivesAudio = _capabilities[1].ButtonPressed,
            DrivesNavigation = _capabilities[2].ButtonPressed,
            DrivesWeather = _capabilities[3].ButtonPressed,
            DrivesCamera = _capabilities[4].ButtonPressed,
            DrivesGameplay = _capabilities[5].ButtonPressed,
            DayNightMode = (DayNightMode)_dayNightMode.Selected,
            DayLengthMinutes = (float)_dayLength.Value,
            AmbientIntensity = (float)_ambientIntensity.Value,
            DirectionalIntensity = (float)_directionalIntensity.Value,
            ColorTemperatureKelvin = (float)_temperature.Value,
            Hdr = _hdr.ButtonPressed,
            Shadows = _shadows.ButtonPressed,
            LightingPresetKey = _lightingPreset.Text,
            AmbientColor = _ambientColor.Text,
            DirectionalColor = _directionalColor.Text,
            SimpleControls = _simpleControls.ButtonPressed,
            Curves = _curves.Values,
            Events = _events.Values,
            Probes = _probes.Values,
            LightingVolumes = _lightingVolumes.Values,
            FogMode = (FogMode)_fogMode.Selected,
            FogDensity = (float)_fogDensity.Value,
            FogStartDistance = (float)_fogStart.Value,
            FogEndDistance = (float)_fogEnd.Value,
            FogHeight = (float)_fogHeight.Value,
            FogDayColor = _fogDayColor.Text,
            FogNightColor = _fogNightColor.Text,
            FogNoise = _fogNoiseEnabled.ButtonPressed
                ? new StudioUx2EnvBFogNoiseDraft(_fogNoiseAsset.Text, (float)_fogNoiseScale.Value,
                    (float)_fogNoiseSpeedX.Value, (float)_fogNoiseSpeedZ.Value, (float)_fogNoiseContrast.Value)
                : null,
            FogVolumes = _fogVolumes.Values,
            FogQualityProfiles = _qualityProfiles.Values,
            RequiredAtRuntime = _requiredAtRuntime.ButtonPressed,
            RequireAuthoringRuntimeMetadata = _requireRuntimeMetadata.ButtonPressed,
        };
    }

    private bool ConfigureEvidenceDraft()
    {
        var current = ReadDraft();
        if (current is null || current.Curves.Count < 2 || current.FogQualityProfiles.Count < 1)
            return false;
        var configured = current with
        {
            ClassificationKey = "natural-canopy-evidence",
            DayLengthMinutes = 39.375f,
            AmbientIntensity = .45625f,
            DirectionalIntensity = 1.2375f,
            FogDensity = .14375f,
        };
        Populate(configured);
        InvalidatePreview();
        return true;
    }

    private void Preview()
    {
        StudioUx2EnvBDraft? draft;
        try
        {
            draft = ReadDraft();
        }
        catch (ArgumentException)
        {
            var diagnostic = new StudioUx2EnvBDiagnostic(
                "studio.area_environment.transform_invalid",
                "environment/authoring/volumes",
                "Un transform ou une surcharge contient une valeur invalide ou ambiguë.");
            var groups = new[]
            {
                new StudioUx2EnvBDiagnosticGroup("classification", "Classification et capacités", "conforme", []),
                new StudioUx2EnvBDiagnosticGroup("cycle", "Cycle, courbes et événements", "conforme", []),
                new StudioUx2EnvBDiagnosticGroup("lighting", "Éclairage physique et probes", "à corriger", [diagnostic]),
                new StudioUx2EnvBDiagnosticGroup("fog", "Brouillard et qualité", "à corriger", [diagnostic]),
            };
            _lastPreview = new StudioUx2EnvBPreview(false, diagnostic.Message, string.Empty, false,
                new StudioUx2EnvBSemanticDiff([], [], [], []), groups, false, false, false);
            _canonicalPreviewReady = false;
            _apply.Disabled = true;
            RenderDiff(_lastPreview);
            RenderDiagnostics(groups);
            _summary.Text = diagnostic.Message;
            _summary.AddThemeColorOverride("font_color", StudioStyle.Danger);
            SetStatus(diagnostic.Message, true);
            return;
        }
        if (draft is null) return;
        var result = _session.PreviewAreaEnvironmentUx2EnvB(draft);
        _lastPreview = result;
        _canonicalPreviewReady = result.Succeeded && result.CanonicalDryRun;
        _apply.Disabled = !result.CanApply || !result.Diff.HasChanges || !_canonicalPreviewReady;
        RenderDiff(result); RenderDiagnostics(result.Groups);
        _summary.Text = string.IsNullOrWhiteSpace(result.Summary) ? result.Message : result.Message + "\n" + result.Summary;
        _summary.AddThemeColorOverride("font_color", result.Succeeded ? StudioStyle.Success : StudioStyle.Danger);
        SetStatus(result.Message, !result.Succeeded);
        UpdateTransactionButtons(keepApplyState: true);
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedAreaEnvironmentUx2EnvB();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            _apply.Disabled = true; _canonicalPreviewReady = false; return;
        }
        _applied = true; _apply.Disabled = true; _preview.Disabled = true; SetEditingEnabled(false);
        UpdateTransactionButtons();
        _summary.Text = "Appliqué · seul l’environnement de la zone choisie a changé. Audio, rendu avancé, ciel, vent, météo, eau, réverbération, navigation et contenu restent préservés.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Success);
        AreaEnvironmentChanged?.Invoke();
    }

    private void Undo()
    {
        var result = _session.Undo(); SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        _applied = false; UpdateTransactionButtons();
        _summary.Text = "Annulé · la révision précédente de l’environnement est restaurée.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Accent);
        AreaEnvironmentChanged?.Invoke();
    }

    private void Redo()
    {
        var result = _session.Redo(); SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        _applied = true; UpdateTransactionButtons();
        _summary.Text = "Rétabli · l’environnement approuvé est de nouveau présent.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Success);
        AreaEnvironmentChanged?.Invoke();
    }

    private void Save()
    {
        var result = _session.Save(); SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _save.Disabled = true;
            _summary.Text += "\nProjet enregistré · la réouverture relira le même agrégat canonique.";
        }
    }

    private void ConnectInvalidation()
    {
        _classification.ItemSelected += _ => InvalidatePreview(); _tags.TextChanged += InvalidatePreview;
        _classificationKey.TextChanged += _ => InvalidatePreview(); _extensionPack.TextChanged += _ => InvalidatePreview();
        foreach (var capability in _capabilities) capability.Toggled += _ => InvalidatePreview();
        _dayNightMode.ItemSelected += _ => InvalidatePreview(); _dayLength.ValueChanged += _ => InvalidatePreview();
        _ambientIntensity.ValueChanged += _ => InvalidatePreview(); _directionalIntensity.ValueChanged += _ => InvalidatePreview();
        _temperature.ValueChanged += _ => InvalidatePreview(); _hdr.Toggled += _ => InvalidatePreview();
        _shadows.Toggled += _ => InvalidatePreview(); _lightingPreset.TextChanged += _ => InvalidatePreview();
        _ambientColor.TextChanged += _ => InvalidatePreview(); _directionalColor.TextChanged += _ => InvalidatePreview();
        _simpleControls.Toggled += _ => InvalidatePreview(); _curves.Changed += InvalidatePreview;
        _events.Changed += InvalidatePreview; _probes.Changed += InvalidatePreview;
        _lightingVolumes.Changed += InvalidatePreview; _fogMode.ItemSelected += _ => InvalidatePreview();
        _fogDensity.ValueChanged += _ => InvalidatePreview(); _fogStart.ValueChanged += _ => InvalidatePreview();
        _fogEnd.ValueChanged += _ => InvalidatePreview(); _fogHeight.ValueChanged += _ => InvalidatePreview();
        _fogDayColor.TextChanged += _ => InvalidatePreview(); _fogNightColor.TextChanged += _ => InvalidatePreview();
        _fogNoiseEnabled.Toggled += _ => InvalidatePreview(); _fogNoiseAsset.TextChanged += _ => InvalidatePreview();
        _fogNoiseScale.ValueChanged += _ => InvalidatePreview(); _fogNoiseSpeedX.ValueChanged += _ => InvalidatePreview();
        _fogNoiseSpeedZ.ValueChanged += _ => InvalidatePreview(); _fogNoiseContrast.ValueChanged += _ => InvalidatePreview();
        _fogVolumes.Changed += InvalidatePreview; _qualityProfiles.Changed += InvalidatePreview;
        _requiredAtRuntime.Toggled += _ => InvalidatePreview(); _requireRuntimeMetadata.Toggled += _ => InvalidatePreview();
    }

    private void InvalidatePreview()
    {
        if (_updating || _draft is null) return;
        _session.CancelAreaEnvironmentUx2EnvB(); _lastPreview = null; _canonicalPreviewReady = false; _applied = false;
        _apply.Disabled = true; _save.Disabled = true;
        _summary.Text = "Brouillon modifié · relancez l’aperçu sec avant application.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Warning);
        RenderDiff(null); RenderDiagnostics([]); UpdateTransactionButtons(keepApplyState: true);
        SetStatus("Aperçu précédent effacé · validation à relancer", false);
    }

    private void ResetControls()
    {
        _updating = true;
        _classification.Select(0); _tags.Text = string.Empty; _classificationKey.Text = string.Empty;
        _extensionPack.Text = string.Empty; foreach (var capability in _capabilities) capability.ButtonPressed = false;
        _dayNightMode.Select(0); _dayLength.Value = 24; _ambientIntensity.Value = 0; _directionalIntensity.Value = 0;
        _temperature.Value = 6_500; _hdr.ButtonPressed = false; _shadows.ButtonPressed = false;
        _lightingPreset.Text = string.Empty; _ambientColor.Text = "#596273"; _directionalColor.Text = "#fff1d6";
        _simpleControls.ButtonPressed = true; _curves.SetValues([]); _events.SetValues([]); _probes.SetValues([]);
        _lightingVolumes.SetValues([]); _fogMode.Select(0); _fogDensity.Value = 0; _fogStart.Value = 0;
        _fogEnd.Value = 0; _fogHeight.Value = 0; _fogDayColor.Text = "#b8c4d8"; _fogNightColor.Text = "#25324a";
        _fogNoiseEnabled.ButtonPressed = false; _fogNoiseAsset.Text = string.Empty; _fogNoiseScale.Value = 1;
        _fogNoiseSpeedX.Value = 0; _fogNoiseSpeedZ.Value = 0; _fogNoiseContrast.Value = 1;
        _fogVolumes.SetValues([]); _qualityProfiles.SetValues([]); _requiredAtRuntime.ButtonPressed = false;
        _requireRuntimeMetadata.ButtonPressed = false; _updating = false;
        RenderDiff(null); RenderDiagnostics([]);
    }

    private void SetEditingEnabled(bool enabled)
    {
        _classification.Disabled = !enabled; _tags.Editable = enabled; _classificationKey.Editable = enabled;
        _extensionPack.Editable = enabled; foreach (var capability in _capabilities) capability.Disabled = !enabled;
        _dayNightMode.Disabled = !enabled; _dayLength.Editable = enabled; _ambientIntensity.Editable = enabled;
        _directionalIntensity.Editable = enabled; _temperature.Editable = enabled; _hdr.Disabled = !enabled;
        _shadows.Disabled = !enabled; _lightingPreset.Editable = enabled; _ambientColor.Editable = enabled;
        _directionalColor.Editable = enabled; _simpleControls.Disabled = !enabled;
        _curves.SetEditingEnabled(enabled); _events.SetEditingEnabled(enabled); _probes.SetEditingEnabled(enabled);
        _lightingVolumes.SetEditingEnabled(enabled); _fogMode.Disabled = !enabled; _fogDensity.Editable = enabled;
        _fogStart.Editable = enabled; _fogEnd.Editable = enabled; _fogHeight.Editable = enabled;
        _fogDayColor.Editable = enabled; _fogNightColor.Editable = enabled; _fogNoiseEnabled.Disabled = !enabled;
        _fogNoiseAsset.Editable = enabled; _fogNoiseScale.Editable = enabled; _fogNoiseSpeedX.Editable = enabled;
        _fogNoiseSpeedZ.Editable = enabled; _fogNoiseContrast.Editable = enabled;
        _fogVolumes.SetEditingEnabled(enabled); _qualityProfiles.SetEditingEnabled(enabled);
        _requiredAtRuntime.Disabled = !enabled; _requireRuntimeMetadata.Disabled = !enabled;
        _preview.Disabled = !enabled;
    }

    private void UpdateTransactionButtons(bool keepApplyState = false)
    {
        var history = _session.History;
        if (!keepApplyState) _apply.Disabled = true;
        _undo.Disabled = !_applied || history?.CanUndo != true;
        _redo.Disabled = _applied || history?.CanRedo != true;
        _save.Disabled = !_applied || !_session.IsDirty;
    }

    private void RenderDiff(StudioUx2EnvBPreview? preview)
    {
        ClearChildren(_diff);
        if (preview is null)
        {
            _diff.AddChild(StudioStyle.Label("Aucun aperçu frais.", 10, StudioStyle.Muted)); return;
        }
        if (!preview.Diff.HasChanges)
        {
            _diff.AddChild(StudioStyle.Label("Aucun changement sémantique.", 10, StudioStyle.Muted)); return;
        }
        AddDiffSection("Classification", preview.Diff.Classification);
        AddDiffSection("Cycle", preview.Diff.Cycle);
        AddDiffSection("Éclairage", preview.Diff.Lighting);
        AddDiffSection("Brouillard", preview.Diff.Fog);
    }

    private void AddDiffSection(string title, IReadOnlyList<string> entries)
    {
        if (entries.Count == 0) return;
        _diff.AddChild(StudioStyle.Label(title, 10, StudioStyle.Accent));
        foreach (var entry in entries) _diff.AddChild(StudioStyle.Label("• " + entry, 10, StudioStyle.Text, wrap: true));
    }

    private void RenderDiagnostics(IReadOnlyList<StudioUx2EnvBDiagnosticGroup> groups)
    {
        ClearChildren(_diagnostics);
        if (groups.Count == 0)
        {
            _diagnostics.AddChild(StudioStyle.Label("Les diagnostics apparaîtront après l’aperçu sec.",
                10, StudioStyle.Muted, wrap: true));
            return;
        }
        foreach (var group in groups)
        {
            var color = group.Diagnostics.Count == 0 ? StudioStyle.Success : StudioStyle.Danger;
            _diagnostics.AddChild(StudioStyle.Label($"{group.Title} · {group.State}", 10, color));
            foreach (var item in group.Diagnostics)
                _diagnostics.AddChild(StudioStyle.Label("• " + item.Message, 10,
                    item.Blocking ? StudioStyle.Danger : StudioStyle.Warning, wrap: true));
        }
    }

    private static void ClearChildren(Node root)
    {
        foreach (var child in root.GetChildren()) child.QueueFree();
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(message, error);
    }

    private StudioUx2EnvBEvidenceSourceSnapshot CaptureSourceSnapshot()
    {
        var history = _sourceSession.History ?? throw new InvalidOperationException("No source history.");
        var project = _sourceSession.Project ?? throw new InvalidOperationException("No source project.");
        return new StudioUx2EnvBEvidenceSourceSnapshot(
            project, history, history.State.Revision, history.Journal.ToArray(), _sourceSession.IsDirty,
            history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(),
            _sourceSession.SelectedEntityId, _sourceSession.CaptureAreaEnvironmentApprovalUx2EnvB());
    }

    private bool SourceSnapshotMatches(StudioUx2EnvBEvidenceSourceSnapshot? snapshot = null)
    {
        snapshot ??= _evidenceSourceSnapshot;
        if (snapshot is null) return false;
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) && ReferenceEquals(snapshot.History, history) &&
               history is not null && history.State.Revision == snapshot.Revision &&
               history.Journal.SequenceEqual(snapshot.Journal) && history.CanUndo == snapshot.CanUndo &&
               history.CanRedo == snapshot.CanRedo && _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureAreaEnvironmentApprovalUx2EnvB() == snapshot.Approval;
    }

    private bool ReleaseIsolatedEvidenceSession()
    {
        var evidenceSession = _evidenceSession; var tempRoot = _evidenceTempRoot;
        _evidenceSession = null; _evidenceTempRoot = null; _evidenceSourceSnapshot = null; _session = _sourceSession;
        _evidenceUsesRealModuleData = false;
        _evidenceUsesCanonicalFallback = false;
        var released = true;
        try { evidenceSession?.Close(); }
        catch (Exception exception)
        {
            released = false;
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_B_RELEASE_FAILED type={exception.GetType().Name} message={exception.Message}");
        }
        var rootReleased = StudioSessionController.DeleteUx2EnvBTemporaryWorkspace(tempRoot);
        if (!rootReleased) _evidenceTempRoot = tempRoot;
        return released && rootReleased;
    }

    private void UpdateDialogBounds()
    {
        var viewport = GetViewportRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0) return;
        var scale = viewport.X >= 3_200 ? 1.7f : viewport.X >= 2_300 ? 1.23f : 1f;
        var width = Math.Clamp((viewport.X - 52f) / scale, 1_040f, 1_560f);
        var height = Math.Clamp((viewport.Y - 42f) / scale, 740f, 1_040f);
        _dialog.Scale = Vector2.One * scale;
        _dialog.CustomMaximumSize = new Vector2(width, height);
        _dialog.Size = new Vector2(width, height);
        _dialog.Position = new Vector2((viewport.X - width * scale) / 2f,
            Math.Max(18f, (viewport.Y - height * scale) * .45f));
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && Visible) UpdateDialogBounds();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Close(); GetViewport().SetInputAsHandled();
        }
    }

    public override void _ExitTree()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null) Close();
    }

    private static IEnumerable<Label> DescendantLabels(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Label label) yield return label;
            foreach (var descendant in DescendantLabels(child)) yield return descendant;
        }
    }

    private static bool ContainsUuid(string text) => System.Text.RegularExpressions.Regex.IsMatch(
        text,
        @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase |
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);
}
