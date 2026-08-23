using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioTriggerStringListEditor : VBoxContainer
{
    private readonly ItemList _items;
    private readonly LineEdit _value;
    private readonly Button _save;
    private readonly Button _remove;
    private readonly List<string> _values = [];
    private int _selected = -1;

    internal event Action? Changed;

    internal IReadOnlyList<string> Values => _values.ToArray();

    internal StudioTriggerStringListEditor(string emptyText, string placeholder)
    {
        AddThemeConstantOverride("separation", 5);
        _items = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 82),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
            TooltipText = emptyText,
        };
        _items.AddThemeStyleboxOverride("panel", StudioStyle.Box(StudioStyle.Surface, StudioStyle.Border, 6));
        AddChild(_items);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 5);
        AddChild(row);
        _value = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyLineEdit(_value);
        row.AddChild(_value);
        _save = StudioStyle.Button("Ajouter", compact: true);
        _save.Pressed += SaveValue;
        row.AddChild(_save);
        _remove = StudioStyle.Button("Retirer", compact: true);
        _remove.Pressed += RemoveValue;
        row.AddChild(_remove);
        _items.ItemSelected += index =>
        {
            _selected = (int)index;
            if (_selected >= 0 && _selected < _values.Count)
            {
                _value.Text = _values[_selected];
                _save.Text = "Modifier";
            }
        };
    }

    internal void SetValues(IEnumerable<string> values)
    {
        _values.Clear();
        _values.AddRange(values);
        Refresh();
    }

    internal void SetEditingEnabled(bool enabled)
    {
        _value.Editable = enabled;
        _save.Disabled = !enabled;
        _remove.Disabled = !enabled;
        _items.MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
    }

    private void SaveValue()
    {
        var value = _value.Text.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }
        if (_selected >= 0 && _selected < _values.Count)
        {
            _values[_selected] = value;
        }
        else
        {
            _values.Add(value);
        }
        Refresh();
        Changed?.Invoke();
    }

    private void RemoveValue()
    {
        if (_selected < 0 || _selected >= _values.Count)
        {
            return;
        }
        _values.RemoveAt(_selected);
        Refresh();
        Changed?.Invoke();
    }

    private void Refresh()
    {
        _selected = -1;
        _items.Clear();
        foreach (var value in _values)
        {
            _items.AddItem(value);
        }
        _value.Clear();
        _save.Text = "Ajouter";
    }
}

internal sealed partial class StudioTriggerVertexListEditor : VBoxContainer
{
    private readonly ItemList _items;
    private readonly SpinBox _x;
    private readonly SpinBox _z;
    private readonly Button _save;
    private readonly Button _remove;
    private readonly List<Vector3Value> _values = [];
    private int _selected = -1;

    internal event Action? Changed;

    internal IReadOnlyList<Vector3Value> Values => _values.ToArray();

    internal StudioTriggerVertexListEditor(string emptyText)
    {
        AddThemeConstantOverride("separation", 5);
        _items = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 112),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
            TooltipText = emptyText,
        };
        _items.AddThemeStyleboxOverride("panel", StudioStyle.Box(StudioStyle.Surface, StudioStyle.Border, 6));
        AddChild(_items);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 5);
        AddChild(row);
        row.AddChild(StudioStyle.Label("X", 10, StudioStyle.Muted));
        _x = Coordinate();
        row.AddChild(_x);
        row.AddChild(StudioStyle.Label("Z", 10, StudioStyle.Muted));
        _z = Coordinate();
        row.AddChild(_z);
        _save = StudioStyle.Button("Ajouter", compact: true);
        _save.Pressed += SaveValue;
        row.AddChild(_save);
        _remove = StudioStyle.Button("Retirer", compact: true);
        _remove.Pressed += RemoveValue;
        row.AddChild(_remove);
        _items.ItemSelected += index =>
        {
            _selected = (int)index;
            if (_selected >= 0 && _selected < _values.Count)
            {
                _x.Value = _values[_selected].X;
                _z.Value = _values[_selected].Z;
                _save.Text = "Modifier";
            }
        };
    }

    internal void SetValues(IEnumerable<Vector3Value> values)
    {
        _values.Clear();
        _values.AddRange(values);
        Refresh();
    }

    internal void SetEditingEnabled(bool enabled)
    {
        _x.Editable = enabled;
        _z.Editable = enabled;
        _save.Disabled = !enabled;
        _remove.Disabled = !enabled;
        _items.MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
    }

    private void SaveValue()
    {
        var value = new Vector3Value((float)_x.Value, 0, (float)_z.Value);
        if (_selected >= 0 && _selected < _values.Count)
        {
            value = new Vector3Value(value.X, _values[_selected].Y, value.Z);
            _values[_selected] = value;
        }
        else
        {
            _values.Add(value);
        }
        Refresh();
        Changed?.Invoke();
    }

    private void RemoveValue()
    {
        if (_selected < 0 || _selected >= _values.Count)
        {
            return;
        }
        _values.RemoveAt(_selected);
        Refresh();
        Changed?.Invoke();
    }

    private void Refresh()
    {
        _selected = -1;
        _items.Clear();
        for (var index = 0; index < _values.Count; index++)
        {
            var value = _values[index];
            _items.AddItem(FormattableString.Invariant(
                $"Sommet {index + 1} · X {value.X:0.##} · Z {value.Z:0.##}"));
        }
        _x.Value = 0;
        _z.Value = 0;
        _save.Text = "Ajouter";
    }

    private static SpinBox Coordinate()
    {
        var value = new SpinBox
        {
            MinValue = -1_000_000,
            MaxValue = 1_000_000,
            Step = .05,
            CustomMinimumSize = new Vector2(118, 0),
            AllowGreater = false,
            AllowLesser = false,
        };
        StudioStyle.ApplySpinBox(value);
        return value;
    }
}

internal sealed partial class StudioTriggerHoleEditor : VBoxContainer
{
    private readonly OptionButton _holes;
    private readonly StudioTriggerVertexListEditor _vertices;
    private readonly Button _addHole;
    private readonly Button _removeHole;
    private readonly List<IReadOnlyList<Vector3Value>> _values = [];
    private int _selected = -1;
    private bool _updating;

    internal event Action? Changed;

    internal IReadOnlyList<IReadOnlyList<Vector3Value>> Values => _values
        .Select(item => (IReadOnlyList<Vector3Value>)item.ToArray())
        .ToArray();

    internal StudioTriggerHoleEditor()
    {
        AddThemeConstantOverride("separation", 5);
        var toolbar = new HBoxContainer();
        toolbar.AddThemeConstantOverride("separation", 5);
        AddChild(toolbar);
        _holes = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_holes);
        _holes.ItemSelected += index => SelectHole((int)index);
        toolbar.AddChild(_holes);
        _addHole = StudioStyle.Button("Ajouter un trou", compact: true);
        _addHole.Pressed += AddHole;
        toolbar.AddChild(_addHole);
        _removeHole = StudioStyle.Button("Retirer le trou", compact: true);
        _removeHole.Pressed += RemoveHole;
        toolbar.AddChild(_removeHole);
        _vertices = new StudioTriggerVertexListEditor(
            "Chaque trou exige au moins trois sommets et ne doit pas se croiser.");
        _vertices.Changed += UpdateSelectedHole;
        AddChild(_vertices);
    }

    internal void SetValues(IEnumerable<IReadOnlyList<Vector3Value>> values)
    {
        _updating = true;
        _values.Clear();
        _values.AddRange(values.Select(item => (IReadOnlyList<Vector3Value>)item.ToArray()));
        RefreshChoices();
        _updating = false;
    }

    internal void SetEditingEnabled(bool enabled)
    {
        _holes.Disabled = !enabled || _values.Count == 0;
        _addHole.Disabled = !enabled;
        _removeHole.Disabled = !enabled || _values.Count == 0;
        _vertices.SetEditingEnabled(enabled && _values.Count > 0);
    }

    private void AddHole()
    {
        _values.Add(
        [
            new Vector3Value(-.5f, 0, -.4f),
            new Vector3Value(.5f, 0, -.4f),
            new Vector3Value(0, 0, .6f),
        ]);
        RefreshChoices(_values.Count - 1);
        Changed?.Invoke();
    }

    private void RemoveHole()
    {
        if (_selected < 0 || _selected >= _values.Count)
        {
            return;
        }
        _values.RemoveAt(_selected);
        RefreshChoices(Math.Min(_selected, _values.Count - 1));
        Changed?.Invoke();
    }

    private void SelectHole(int index)
    {
        if (_updating)
        {
            return;
        }
        _selected = index >= 0 && index < _values.Count ? index : -1;
        _vertices.SetValues(_selected >= 0 ? _values[_selected] : []);
    }

    private void UpdateSelectedHole()
    {
        if (_updating || _selected < 0 || _selected >= _values.Count)
        {
            return;
        }
        _values[_selected] = _vertices.Values;
        RefreshChoices(_selected);
        Changed?.Invoke();
    }

    private void RefreshChoices(int requested = 0)
    {
        _updating = true;
        _holes.Clear();
        for (var index = 0; index < _values.Count; index++)
        {
            _holes.AddItem($"Trou {index + 1} · {_values[index].Count} sommet(s)");
        }
        _selected = _values.Count == 0 ? -1 : Math.Clamp(requested, 0, _values.Count - 1);
        if (_selected >= 0)
        {
            _holes.Select(_selected);
            _vertices.SetValues(_values[_selected]);
        }
        else
        {
            _vertices.SetValues([]);
        }
        _holes.Disabled = _values.Count == 0;
        _removeHole.Disabled = _values.Count == 0;
        _vertices.SetEditingEnabled(_values.Count > 0);
        _updating = false;
    }
}

internal sealed partial class StudioTriggerActionListEditor : VBoxContainer
{
    private readonly ItemList _items;
    private readonly LineEdit _kind;
    private readonly LineEdit _target;
    private readonly Button _save;
    private readonly Button _remove;
    private readonly List<StudioTriggerActionDraft> _values = [];
    private int _selected = -1;

    internal event Action? Changed;

    internal IReadOnlyList<StudioTriggerActionDraft> Values => _values.ToArray();

    internal StudioTriggerActionListEditor()
    {
        AddThemeConstantOverride("separation", 5);
        _items = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 100),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        _items.AddThemeStyleboxOverride("panel", StudioStyle.Box(StudioStyle.Surface, StudioStyle.Border, 6));
        AddChild(_items);

        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(grid);
        grid.AddChild(StudioStyle.Label("Type d’action", 10, StudioStyle.Muted));
        _kind = Text("Ex. start-encounter");
        grid.AddChild(_kind);
        grid.AddChild(StudioStyle.Label("Cible métier", 10, StudioStyle.Muted));
        _target = Text("Ex. encounter:guardian");
        grid.AddChild(_target);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 5);
        AddChild(row);
        _save = StudioStyle.Button("Ajouter l’action", compact: true);
        _save.Pressed += SaveValue;
        row.AddChild(_save);
        _remove = StudioStyle.Button("Retirer", compact: true);
        _remove.Pressed += RemoveValue;
        row.AddChild(_remove);
        _items.ItemSelected += index =>
        {
            _selected = (int)index;
            if (_selected >= 0 && _selected < _values.Count)
            {
                _kind.Text = _values[_selected].Kind;
                _target.Text = _values[_selected].TargetId;
                _save.Text = "Modifier";
            }
        };
    }

    internal void SetValues(IEnumerable<StudioTriggerActionDraft> values)
    {
        _values.Clear();
        _values.AddRange(values);
        Refresh();
    }

    internal void SetEditingEnabled(bool enabled)
    {
        _kind.Editable = enabled;
        _target.Editable = enabled;
        _save.Disabled = !enabled;
        _remove.Disabled = !enabled;
        _items.MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
    }

    private void SaveValue()
    {
        var kind = _kind.Text.Trim();
        var target = _target.Text.Trim();
        if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(target))
        {
            return;
        }
        if (_selected >= 0 && _selected < _values.Count)
        {
            _values[_selected] = _values[_selected] with { Kind = kind, TargetId = target };
        }
        else
        {
            _values.Add(new StudioTriggerActionDraft(
                Guid.NewGuid(),
                kind,
                target,
                new Dictionary<string, string>(StringComparer.Ordinal)));
        }
        Refresh();
        Changed?.Invoke();
    }

    private void RemoveValue()
    {
        if (_selected < 0 || _selected >= _values.Count)
        {
            return;
        }
        _values.RemoveAt(_selected);
        Refresh();
        Changed?.Invoke();
    }

    private void Refresh()
    {
        _selected = -1;
        _items.Clear();
        foreach (var value in _values)
        {
            _items.AddItem($"{value.Kind} → {value.TargetId}");
        }
        _kind.Clear();
        _target.Clear();
        _save.Text = "Ajouter l’action";
    }

    private static LineEdit Text(string placeholder)
    {
        var value = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyLineEdit(value);
        return value;
    }
}

internal sealed partial class StudioTriggerContourPreview : Control
{
    private IReadOnlyList<Vector3Value> _vertices = [];
    private IReadOnlyList<IReadOnlyList<Vector3Value>> _holes = [];

    internal int VertexCount => _vertices.Count;

    internal int HoleCount => _holes.Count;

    internal bool UsesUniformProjection => true;

    internal StudioTriggerContourPreview()
    {
        CustomMinimumSize = new Vector2(300, 240);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    internal void SetContours(
        IReadOnlyList<Vector3Value> vertices,
        IReadOnlyList<IReadOnlyList<Vector3Value>> holes)
    {
        _vertices = vertices.ToArray();
        _holes = holes.Select(item => (IReadOnlyList<Vector3Value>)item.ToArray()).ToArray();
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), StudioStyle.Surface, filled: true);
        if (_vertices.Count < 2)
        {
            return;
        }
        var all = _vertices.Concat(_holes.SelectMany(item => item)).ToArray();
        var minX = all.Min(item => item.X);
        var maxX = all.Max(item => item.X);
        var minZ = all.Min(item => item.Z);
        var maxZ = all.Max(item => item.Z);
        var rangeX = Math.Max(.1f, maxX - minX);
        var rangeZ = Math.Max(.1f, maxZ - minZ);
        const float padding = 24;
        var availableWidth = MathF.Max(1, Size.X - padding * 2);
        var availableHeight = MathF.Max(1, Size.Y - padding * 2);
        var uniformScale = MathF.Min(availableWidth / rangeX, availableHeight / rangeZ);
        var renderedWidth = rangeX * uniformScale;
        var renderedHeight = rangeZ * uniformScale;
        var left = (Size.X - renderedWidth) * .5f;
        var top = (Size.Y - renderedHeight) * .5f;
        Vector2 Map(Vector3Value value) => new(
            left + (value.X - minX) * uniformScale,
            top + (maxZ - value.Z) * uniformScale);

        DrawContour(_vertices, StudioStyle.Accent, 3, Map);
        foreach (var hole in _holes)
        {
            DrawContour(hole, StudioStyle.Warning, 2, Map);
        }
    }

    private void DrawContour(
        IReadOnlyList<Vector3Value> values,
        Color color,
        float width,
        Func<Vector3Value, Vector2> map)
    {
        if (values.Count < 2)
        {
            return;
        }
        for (var index = 0; index < values.Count; index++)
        {
            var start = map(values[index]);
            var end = map(values[(index + 1) % values.Count]);
            DrawLine(start, end, color, width, antialiased: true);
            DrawCircle(start, 4.5f, color, filled: true, width: -1, antialiased: true);
        }
    }
}

internal sealed partial class StudioTriggerFoundationOverlay : Control
{
    private static readonly TriggerWorldProfile.ShapeKind[] ShapeKinds =
        Enum.GetValues<TriggerWorldProfile.ShapeKind>();
    private static readonly TriggerWorldProfile.FirePolicy[] FirePolicies =
        Enum.GetValues<TriggerWorldProfile.FirePolicy>();

    private readonly StudioSessionController _session;
    private readonly PanelContainer _dialog;
    private readonly OptionButton _trigger;
    private readonly Label _selection;
    private readonly Button _volumeTab;
    private readonly Button _policyTab;
    private readonly Button _eventsTab;
    private readonly VBoxContainer _volumePanel;
    private readonly VBoxContainer _policyPanel;
    private readonly VBoxContainer _eventsPanel;
    private readonly LineEdit _slug;
    private readonly OptionButton _shape;
    private readonly StudioTriggerVertexListEditor _vertices;
    private readonly StudioTriggerHoleEditor _holes;
    private readonly StudioTriggerContourPreview _contourPreview;
    private readonly SpinBox _height;
    private readonly SpinBox _snap;
    private readonly SpinBox _sizeX;
    private readonly SpinBox _sizeY;
    private readonly SpinBox _sizeZ;
    private readonly CheckBox _visualize;
    private readonly SpinBox _positionX;
    private readonly SpinBox _positionY;
    private readonly SpinBox _positionZ;
    private readonly SpinBox _rotationX;
    private readonly SpinBox _rotationY;
    private readonly SpinBox _rotationZ;
    private readonly SpinBox _scaleX;
    private readonly SpinBox _scaleY;
    private readonly SpinBox _scaleZ;
    private readonly StudioTriggerStringListEditor _entityKinds;
    private readonly StudioTriggerStringListEditor _requiredTags;
    private readonly StudioTriggerStringListEditor _excludedTags;
    private readonly StudioTriggerStringListEditor _layers;
    private readonly StudioTriggerStringListEditor _conditions;
    private readonly SpinBox _cooldown;
    private readonly OptionButton _fire;
    private readonly StudioTriggerActionListEditor _actions;
    private readonly LineEdit _enterApi;
    private readonly LineEdit _enterBinding;
    private readonly LineEdit _enterBefore;
    private readonly LineEdit _enterAfter;
    private readonly LineEdit _exitApi;
    private readonly LineEdit _exitBinding;
    private readonly LineEdit _exitBefore;
    private readonly LineEdit _exitAfter;
    private readonly CheckBox _requireRuntime;
    private readonly Label _summary;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Label _status;
    private IReadOnlyList<StudioTriggerChoice> _choices = [];
    private StudioTriggerFoundationDraft? _draft;
    private bool _updating;
    private bool _applied;
    private int _section;

    internal event Action<string, bool>? StatusChanged;

    internal event Action? TriggerChanged;

    internal bool IsOpen => Visible;

    internal int SectionCount => 3;

    internal int VertexCount => _contourPreview.VertexCount;

    internal int HoleCount => _contourPreview.HoleCount;

    internal bool UsesUniformProjection => _contourPreview.UsesUniformProjection;

    internal bool IsExtrudedPolygonSurface =>
        _draft?.ShapeKind == TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D && _shape.Disabled;

    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', _selection.Text, _summary.Text, _status.Text,
                _volumeTab.Text, _policyTab.Text, _eventsTab.Text);
            return _draft is not null && SectionCount == 3 &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains(".json", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal StudioTriggerFoundationOverlay(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Name = "StudioTriggerFoundationOverlay";
        Visible = false;
        ZIndex = 448;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect { Color = new Color(0, 0, 0, .8f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        _dialog = StudioStyle.Panel("TriggerFoundationDialog", StudioStyle.Chrome, 12, 2);
        _dialog.ClipContents = true;
        _dialog.PropagateMaximumSize = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(18, 14, 18, 14);
        margin.PropagateMaximumSize = true;
        _dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            PropagateMaximumSize = true,
        };
        root.AddThemeConstantOverride("separation", 9);
        margin.AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        root.AddChild(header);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("DÉCLENCHEUR", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Volume, activation et événements", 20));
        heading.AddChild(StudioStyle.Label(
            "Polygone extrudé uniquement · empreinte X/Z 2D à échelle uniforme · portail et réglages avancés préservés",
            10,
            StudioStyle.Muted,
            wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.TooltipText = "Fermer sans appliquer le brouillon · Échap";
        close.Pressed += Close;
        header.AddChild(close);

        var selector = new HBoxContainer();
        selector.AddThemeConstantOverride("separation", 8);
        root.AddChild(selector);
        selector.AddChild(StudioStyle.Label("Déclencheur à configurer", 10, StudioStyle.Muted));
        _trigger = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_trigger);
        _trigger.ItemSelected += SelectTrigger;
        selector.AddChild(_trigger);
        _selection = StudioStyle.Label(
            "Choisissez explicitement un blueprint de déclencheur.",
            11,
            StudioStyle.Warning,
            wrap: true);
        root.AddChild(_selection);

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 6);
        root.AddChild(tabs);
        _volumeTab = Tab("Volume & transform");
        _volumeTab.Pressed += () => ShowSection(0);
        tabs.AddChild(_volumeTab);
        _policyTab = Tab("Activation & actions");
        _policyTab.Pressed += () => ShowSection(1);
        tabs.AddChild(_policyTab);
        _eventsTab = Tab("OnEnter & OnExit");
        _eventsTab.Pressed += () => ShowSection(2);
        tabs.AddChild(_eventsTab);

        var bodyHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 300),
            CustomMaximumSize = new Vector2(-1, 520),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            PropagateMaximumSize = true,
        };
        root.AddChild(bodyHost);
        var body = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true,
        };
        body.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bodyHost.AddChild(body);
        var panels = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddChild(panels);

        _volumePanel = PanelColumn();
        panels.AddChild(_volumePanel);
        _volumePanel.AddChild(StudioStyle.Label("Contour du volume", 17));
        _volumePanel.AddChild(StudioStyle.Label(
            "Cette fiche édite uniquement un polygone extrudé. Les sommets et les trous se règlent par lignes nommées. L’aperçu ci-dessous est une empreinte X/Z 2D à échelle uniforme ; aucun gizmo 3D n’est revendiqué par ce lot.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var volumeSplit = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        volumeSplit.AddThemeConstantOverride("separation", 10);
        _volumePanel.AddChild(volumeSplit);
        var volumeFields = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        volumeFields.CustomMinimumSize = new Vector2(570, 0);
        volumeFields.AddThemeConstantOverride("separation", 8);
        volumeSplit.AddChild(volumeFields);
        var identityGrid = Grid();
        volumeFields.AddChild(identityGrid);
        _slug = AddField(identityGrid, "Nom court", Text("seuil-des-brumes"));
        _shape = AddField(identityGrid, "Forme", Option(ShapeKinds.Select(HumanizeShape)));
        _height = AddField(identityGrid, "Hauteur", Number(.1, 10_000, .1, " m"));
        _snap = AddField(identityGrid, "Pas de grille", Number(0, 10_000, .05, " m"));
        _sizeX = AddField(identityGrid, "Dimension X", Number(.1, 1_000_000, .1, " m"));
        _sizeY = AddField(identityGrid, "Dimension Y", Number(.1, 1_000_000, .1, " m"));
        _sizeZ = AddField(identityGrid, "Dimension Z", Number(.1, 1_000_000, .1, " m"));
        _visualize = AddField(identityGrid, "Repère d’auteur", Check("Afficher le volume dans l’éditeur"));
        volumeFields.AddChild(StudioStyle.SectionLabel("SOMMETS DU CONTOUR EXTÉRIEUR"));
        _vertices = new StudioTriggerVertexListEditor("Le contour extérieur exige au moins trois sommets.");
        _vertices.Changed += InvalidatePreview;
        volumeFields.AddChild(_vertices);
        volumeFields.AddChild(StudioStyle.SectionLabel("TROUS ET CONTOURS INTÉRIEURS"));
        _holes = new StudioTriggerHoleEditor();
        _holes.Changed += InvalidatePreview;
        volumeFields.AddChild(_holes);
        volumeFields.AddChild(StudioStyle.SectionLabel("TRANSFORMATION"));
        var transformGrid = Grid();
        volumeFields.AddChild(transformGrid);
        _positionX = AddField(transformGrid, "Position X", Coordinate());
        _positionY = AddField(transformGrid, "Position Y", Coordinate());
        _positionZ = AddField(transformGrid, "Position Z", Coordinate());
        _rotationX = AddField(transformGrid, "Rotation X", Number(-360, 360, .5, "°"));
        _rotationY = AddField(transformGrid, "Rotation Y", Number(-360, 360, .5, "°"));
        _rotationZ = AddField(transformGrid, "Rotation Z", Number(-360, 360, .5, "°"));
        _scaleX = AddField(transformGrid, "Échelle X", Number(.02, 100, .05));
        _scaleY = AddField(transformGrid, "Échelle Y", Number(.02, 100, .05));
        _scaleZ = AddField(transformGrid, "Échelle Z", Number(.02, 100, .05));
        var previewPanel = StudioStyle.Panel("TriggerContourPreview", StudioStyle.SurfaceRaised, 8, 1);
        previewPanel.CustomMinimumSize = new Vector2(330, 0);
        previewPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        volumeSplit.AddChild(previewPanel);
        var previewMargin = StudioStyle.Margin(10, 10, 10, 10);
        previewPanel.AddChild(previewMargin);
        var previewColumn = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        previewColumn.AddThemeConstantOverride("separation", 6);
        previewMargin.AddChild(previewColumn);
        previewColumn.AddChild(StudioStyle.SectionLabel("APERÇU 2D DU CONTOUR"));
        previewColumn.AddChild(StudioStyle.Label(
            "Orange : contour extérieur · jaune : trous · points : sommets",
            10,
            StudioStyle.Muted,
            wrap: true));
        _contourPreview = new StudioTriggerContourPreview
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        previewColumn.AddChild(_contourPreview);

        _policyPanel = PanelColumn();
        panels.AddChild(_policyPanel);
        _policyPanel.AddChild(StudioStyle.Label("Activation du déclencheur", 17));
        _policyPanel.AddChild(StudioStyle.Label(
            "Chaque collection se modifie par entrée nommée. Les références de validation déjà présentes restent intactes.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var policyColumns = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        policyColumns.AddThemeConstantOverride("separation", 10);
        _policyPanel.AddChild(policyColumns);
        var filters = PanelColumn();
        filters.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        policyColumns.AddChild(filters);
        filters.AddChild(StudioStyle.SectionLabel("QUAND · UNE ENTITÉ ENTRE OU SORT"));
        filters.AddChild(StudioStyle.Label(
            "Choisissez qui peut activer ce volume avant de préciser les conditions et les effets.",
            10,
            StudioStyle.Muted,
            wrap: true));
        filters.AddChild(StudioStyle.SectionLabel("TYPES D’ENTITÉ"));
        _entityKinds = new StudioTriggerStringListEditor("Au moins un type est requis.", "creature");
        filters.AddChild(_entityKinds);
        filters.AddChild(StudioStyle.SectionLabel("TAGS REQUIS"));
        _requiredTags = new StudioTriggerStringListEditor("Aucun tag requis.", "player");
        filters.AddChild(_requiredTags);
        filters.AddChild(StudioStyle.SectionLabel("TAGS EXCLUS"));
        _excludedTags = new StudioTriggerStringListEditor("Aucun tag exclu.", "undead");
        filters.AddChild(_excludedTags);
        filters.AddChild(StudioStyle.SectionLabel("CALQUES"));
        _layers = new StudioTriggerStringListEditor("Tous les calques si la liste est vide.", "characters");
        filters.AddChild(_layers);
        var behavior = PanelColumn();
        behavior.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        policyColumns.AddChild(behavior);
        behavior.AddChild(StudioStyle.SectionLabel("SI · CONDITIONS"));
        _conditions = new StudioTriggerStringListEditor("Aucune condition supplémentaire.", "area.ready");
        behavior.AddChild(_conditions);
        var policyGrid = Grid();
        behavior.AddChild(policyGrid);
        _cooldown = AddField(policyGrid, "Délai", Number(0, 86_400, .25, " s"));
        _fire = AddField(policyGrid, "Déclenchement", Option(FirePolicies.Select(HumanizeFire)));
        behavior.AddChild(StudioStyle.SectionLabel("ALORS · ACTIONS TYPÉES"));
        _actions = new StudioTriggerActionListEditor();
        behavior.AddChild(_actions);

        foreach (var editor in new[] { _entityKinds, _requiredTags, _excludedTags, _layers, _conditions })
        {
            editor.Changed += InvalidatePreview;
        }
        _actions.Changed += InvalidatePreview;

        _eventsPanel = PanelColumn();
        panels.AddChild(_eventsPanel);
        _eventsPanel.AddChild(StudioStyle.Label("Événements typés du volume", 17));
        _eventsPanel.AddChild(StudioStyle.Label(
            "Les liaisons VolumeEntered et VolumeExited sont canoniques. Les scripts restent facultatifs et passent par ForgeScript.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var eventColumns = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        eventColumns.AddThemeConstantOverride("separation", 10);
        _eventsPanel.AddChild(eventColumns);
        var enterPanel = EventPanel("ENTRÉE");
        eventColumns.AddChild(enterPanel.Panel);
        _enterApi = enterPanel.Api;
        _enterBinding = enterPanel.Binding;
        _enterBefore = enterPanel.Before;
        _enterAfter = enterPanel.After;
        var exitPanel = EventPanel("SORTIE");
        eventColumns.AddChild(exitPanel.Panel);
        _exitApi = exitPanel.Api;
        _exitBinding = exitPanel.Binding;
        _exitBefore = exitPanel.Before;
        _exitAfter = exitPanel.After;
        _requireRuntime = Check("Exiger cette fondation au runtime");
        _eventsPanel.AddChild(_requireRuntime);
        _eventsPanel.AddChild(StudioStyle.Label(
            "Le portail de transition, le contexte de validation et AdvancedProfile sont volontairement hors de cette fiche et sont préservés.",
            10,
            StudioStyle.Warning,
            wrap: true));

        _summary = StudioStyle.Label(
            "Sélectionnez un déclencheur puis prévisualisez · aucun changement appliqué.",
            10,
            StudioStyle.Muted,
            wrap: true);
        root.AddChild(_summary);
        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 7);
        root.AddChild(footer);
        _preview = StudioStyle.Button("1 · Prévisualiser", compact: true);
        _preview.Pressed += Preview;
        footer.AddChild(_preview);
        _apply = StudioStyle.Button("2 · Appliquer", primary: true, compact: true);
        _apply.Disabled = true;
        _apply.Pressed += Apply;
        footer.AddChild(_apply);
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _undo = StudioStyle.Button("Annuler l’opération", compact: true);
        _undo.Disabled = true;
        _undo.Pressed += Undo;
        footer.AddChild(_undo);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.Disabled = true;
        _redo.Pressed += Redo;
        footer.AddChild(_redo);
        _status = StudioStyle.Label("Prêt · sélection explicite requise", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        ConnectInvalidation();
        Resized += UpdateDialogBounds;
        UpdateDialogBounds();
        ShowSection(0);
        SetEditingEnabled(false);
    }

    internal void Open()
    {
        _session.CancelTriggerFoundationUx1Y();
        _choices = _session.TriggerChoicesUx1Y;
        _trigger.Clear();
        _trigger.AddItem("Choisissez explicitement un déclencheur…");
        foreach (var choice in _choices)
        {
            _trigger.AddItem(choice.Label);
        }
        _trigger.Select(0);
        _draft = null;
        _applied = false;
        _selection.Text = _choices.Count == 0
            ? "Aucun blueprint de déclencheur n’est disponible dans ce module."
            : "Choisissez explicitement un blueprint de déclencheur.";
        _selection.AddThemeColorOverride("font_color", _choices.Count == 0 ? StudioStyle.Danger : StudioStyle.Warning);
        _summary.Text = "Sélectionnez un déclencheur puis prévisualisez · aucun changement appliqué.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        _status.Text = "Prêt · sélection explicite requise";
        SetEditingEnabled(false);
        _undo.Disabled = true;
        _redo.Disabled = true;
        Visible = true;
        MoveToFront();
        ShowSection(0);
        UpdateDialogBounds();
        _trigger.GrabFocus();
    }

    internal void Close()
    {
        _session.CancelTriggerFoundationUx1Y();
        Visible = false;
        _draft = null;
        _applied = false;
    }

    internal bool PrepareEvidence(string mode)
    {
        Open();
        if (_choices.Count == 0)
        {
            return false;
        }

        var polygonChoiceIndex = -1;
        for (var index = 0; index < _choices.Count; index++)
        {
            var candidate = _session.CreateTriggerFoundationDraftUx1Y(_choices[index].BlueprintId);
            if (candidate?.ShapeKind == TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D)
            {
                polygonChoiceIndex = index;
                break;
            }
        }
        if (polygonChoiceIndex < 0)
        {
            return false;
        }

        _trigger.Select(polygonChoiceIndex + 1);
        LoadChoice(polygonChoiceIndex);
        if (_draft?.ShapeKind != TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D)
        {
            return false;
        }
        if (_vertices.Values.Count < 3)
        {
            _vertices.SetValues(
            [
                new Vector3Value(-2, 0, -2),
                new Vector3Value(2, 0, -2),
                new Vector3Value(2, 0, 2),
                new Vector3Value(-2, 0, 2),
            ]);
        }
        if (_holes.Values.Count == 0)
        {
            _holes.SetValues(
            [
                [new Vector3Value(-.5f, 0, -.4f), new(.5f, 0, -.4f), new(0, 0, .6f)],
            ]);
        }
        _height.Value = Math.Max(1, _height.Value);
        _snap.Value = _snap.Value <= 0 ? .25 : _snap.Value;
        ShowSection(mode.Contains("events", StringComparison.OrdinalIgnoreCase) ? 2 : 0);
        Preview();
        return !_apply.Disabled && _contourPreview.VertexCount >= 3 && _contourPreview.HoleCount >= 1;
    }

    internal bool RunUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        if (originalProject is null || originalHistory is null)
        {
            return false;
        }

        var originalDirty = _session.IsDirty;
        var originalRevision = originalHistory.State.Revision;
        var originalJournal = originalHistory.Journal.ToArray();
        var originalCanUndo = originalHistory.CanUndo;
        var originalCanRedo = originalHistory.CanRedo;
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "openrpg-forge-studio-ux1y-ui-" + Guid.NewGuid().ToString("N"));
        var isolatedSession = new StudioSessionController();
        StudioTriggerFoundationOverlay? isolatedOverlay = null;
        try
        {
            var created = ProjectSession.Create(tempRoot, originalProject);
            if (!created.Succeeded ||
                !isolatedSession.Open(tempRoot, _session.ActiveAreaId?.ToString()).Succeeded)
            {
                GD.PrintErr(
                    $"OPENRPG_FORGE_STUDIO_UX1Y_UI_TRANSACTION_FAILED stage=isolated-open created={created.Succeeded.ToString().ToLowerInvariant()}");
                return false;
            }

            isolatedOverlay = new StudioTriggerFoundationOverlay(isolatedSession);
            AddChild(isolatedOverlay);
            var isolatedEvidence = isolatedOverlay.RunBoundUiTransactionEvidence();
            var currentHistory = _session.History;
            var sourceUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                  _session.IsDirty == originalDirty &&
                                  currentHistory is not null &&
                                  ReferenceEquals(originalHistory, currentHistory) &&
                                  currentHistory.State.Revision == originalRevision &&
                                  currentHistory.CanUndo == originalCanUndo &&
                                  currentHistory.CanRedo == originalCanRedo &&
                                  currentHistory.Journal.SequenceEqual(originalJournal);
            var valid = isolatedEvidence && sourceUntouched;
            if (!valid)
            {
                GD.PrintErr(
                    $"OPENRPG_FORGE_STUDIO_UX1Y_UI_TRANSACTION_FAILED stage=isolated-transaction " +
                    $"isolated={isolatedEvidence.ToString().ToLowerInvariant()} source_untouched={sourceUntouched.ToString().ToLowerInvariant()}");
            }
            return valid;
        }
        catch (Exception exception)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1Y_UI_TRANSACTION_FAILED stage=exception type={exception.GetType().Name} message={exception.Message}");
            return false;
        }
        finally
        {
            if (isolatedOverlay is not null && GodotObject.IsInstanceValid(isolatedOverlay))
            {
                isolatedOverlay.Close();
                isolatedOverlay.GetParent()?.RemoveChild(isolatedOverlay);
                isolatedOverlay.Free();
            }
            isolatedSession.Close();
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private bool RunBoundUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalDirty = _session.IsDirty;
        var ready = PrepareEvidence("ux1-y-foundation");
        var dryRun = ready && ReferenceEquals(originalProject, _session.Project) &&
                     _session.IsDirty == originalDirty;
        Apply();
        var appliedProject = _session.Project;
        var applied = dryRun && _applied && !ReferenceEquals(originalProject, appliedProject);
        if (!applied)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1Y_BOUND_UI_TRANSACTION_FAILED stage=apply ready={ready.ToString().ToLowerInvariant()} " +
                $"dry_run={dryRun.ToString().ToLowerInvariant()} applied_flag={_applied.ToString().ToLowerInvariant()} " +
                $"project_changed={(!ReferenceEquals(originalProject, appliedProject)).ToString().ToLowerInvariant()}");
            Close();
            return false;
        }
        Undo();
        var undone = applied && ReferenceEquals(originalProject, _session.Project) &&
                     _session.IsDirty == originalDirty;
        if (undone)
        {
            Redo();
        }
        var redone = undone && ReferenceEquals(appliedProject, _session.Project);
        if (redone)
        {
            Undo();
        }
        var restored = redone && ReferenceEquals(originalProject, _session.Project) &&
                       _session.IsDirty == originalDirty;
        Close();
        var valid = dryRun && applied && undone && redone && restored;
        if (!valid)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1Y_BOUND_UI_TRANSACTION_FAILED ready={ready.ToString().ToLowerInvariant()} " +
                $"dry_run={dryRun.ToString().ToLowerInvariant()} applied={applied.ToString().ToLowerInvariant()} " +
                $"undone={undone.ToString().ToLowerInvariant()} redone={redone.ToString().ToLowerInvariant()} " +
                $"restored={restored.ToString().ToLowerInvariant()}");
        }
        return valid;
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    private void SelectTrigger(long index)
    {
        if (_updating)
        {
            return;
        }
        if (index <= 0)
        {
            _draft = null;
            SetEditingEnabled(false);
            return;
        }
        LoadChoice((int)index - 1);
    }

    private void LoadChoice(int index)
    {
        if (index < 0 || index >= _choices.Count)
        {
            return;
        }
        _session.CancelTriggerFoundationUx1Y();
        _draft = _session.CreateTriggerFoundationDraftUx1Y(_choices[index].BlueprintId);
        if (_draft is null)
        {
            _selection.Text = "Cette cible est ambiguë ou n’existe plus. Rechargez le module avant de continuer.";
            _selection.AddThemeColorOverride("font_color", StudioStyle.Danger);
            SetEditingEnabled(false);
            return;
        }
        Populate(_draft);
        if (_draft.ShapeKind != TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D)
        {
            _selection.Text =
                $"{_choices[index].Label} · {HumanizeShape(_draft.ShapeKind)} préservée sans conversion";
            _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
            _summary.Text =
                "Cette fiche UX1-Y est limitée aux polygones extrudés. Utilisez une future surface de volume 3D pour modifier cette forme.";
            _summary.AddThemeColorOverride("font_color", StudioStyle.Warning);
            SetEditingEnabled(false);
            SetStatus("Volume non polygonal conservé en lecture seule · aucune conversion appliquée", error: false);
            return;
        }
        _selection.Text = _choices[index].HasFoundation
            ? $"{_choices[index].Label} · fondation existante chargée"
            : $"{_choices[index].Label} · nouvelle fondation préparée";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Success);
        SetEditingEnabled(true);
        SetStatus("Déclencheur chargé · prévisualisez avant d’appliquer", error: false);
    }

    private void Populate(StudioTriggerFoundationDraft draft)
    {
        _updating = true;
        _slug.Text = draft.Slug;
        _shape.Select(Math.Max(0, Array.IndexOf(ShapeKinds, draft.ShapeKind)));
        _height.Value = draft.Height;
        _snap.Value = draft.SnapIncrement;
        _sizeX.Value = draft.Size.X;
        _sizeY.Value = draft.Size.Y;
        _sizeZ.Value = draft.Size.Z;
        _visualize.ButtonPressed = draft.VisualizeInEditor;
        _vertices.SetValues(draft.Vertices);
        _holes.SetValues(draft.Holes);
        _contourPreview.SetContours(draft.Vertices, draft.Holes);
        _positionX.Value = draft.Position.X;
        _positionY.Value = draft.Position.Y;
        _positionZ.Value = draft.Position.Z;
        _rotationX.Value = draft.RotationDegrees.X;
        _rotationY.Value = draft.RotationDegrees.Y;
        _rotationZ.Value = draft.RotationDegrees.Z;
        _scaleX.Value = draft.Scale.X;
        _scaleY.Value = draft.Scale.Y;
        _scaleZ.Value = draft.Scale.Z;
        _entityKinds.SetValues(draft.EntityKinds);
        _requiredTags.SetValues(draft.RequiredTags);
        _excludedTags.SetValues(draft.ExcludedTags);
        _layers.SetValues(draft.Layers);
        _conditions.SetValues(draft.Conditions);
        _cooldown.Value = draft.CooldownSeconds;
        _fire.Select(Math.Max(0, Array.IndexOf(FirePolicies, draft.FirePolicy)));
        _actions.SetValues(draft.Actions);
        _enterApi.Text = draft.OnEnter.ForgeScriptApiVersion;
        _enterBinding.Text = draft.OnEnter.EventBinding;
        _enterBefore.Text = draft.OnEnter.BeforeScriptId ?? string.Empty;
        _enterAfter.Text = draft.OnEnter.AfterScriptId ?? string.Empty;
        _exitApi.Text = draft.OnExit.ForgeScriptApiVersion;
        _exitBinding.Text = draft.OnExit.EventBinding;
        _exitBefore.Text = draft.OnExit.BeforeScriptId ?? string.Empty;
        _exitAfter.Text = draft.OnExit.AfterScriptId ?? string.Empty;
        _requireRuntime.ButtonPressed = draft.RequireRuntime;
        _summary.Text = "Fondation chargée · portail, validation et Advanced préservés · aperçu à calculer.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        _updating = false;
        InvalidatePreview();
    }

    private StudioTriggerFoundationDraft ReadDraft()
    {
        var draft = _draft ?? throw new InvalidOperationException("Aucun déclencheur n’est sélectionné.");
        return draft with
        {
            Slug = _slug.Text,
            ShapeKind = draft.ShapeKind,
            Vertices = _vertices.Values,
            Holes = _holes.Values,
            Size = new Vector3Value((float)_sizeX.Value, (float)_sizeY.Value, (float)_sizeZ.Value),
            Height = _height.Value,
            SnapIncrement = _snap.Value,
            VisualizeInEditor = _visualize.ButtonPressed,
            EntityKinds = _entityKinds.Values,
            RequiredTags = _requiredTags.Values,
            ExcludedTags = _excludedTags.Values,
            Layers = _layers.Values,
            Conditions = _conditions.Values,
            CooldownSeconds = _cooldown.Value,
            FirePolicy = FirePolicies[Math.Clamp(_fire.Selected, 0, FirePolicies.Length - 1)],
            Actions = _actions.Values,
            Position = new Vector3Value((float)_positionX.Value, (float)_positionY.Value, (float)_positionZ.Value),
            RotationDegrees = new Vector3Value(
                (float)_rotationX.Value,
                (float)_rotationY.Value,
                (float)_rotationZ.Value),
            Scale = new Vector3Value((float)_scaleX.Value, (float)_scaleY.Value, (float)_scaleZ.Value),
            OnEnter = draft.OnEnter with
            {
                ForgeScriptApiVersion = _enterApi.Text,
                EventBinding = _enterBinding.Text,
                BeforeScriptId = EmptyToNull(_enterBefore.Text),
                AfterScriptId = EmptyToNull(_enterAfter.Text),
            },
            OnExit = draft.OnExit with
            {
                ForgeScriptApiVersion = _exitApi.Text,
                EventBinding = _exitBinding.Text,
                BeforeScriptId = EmptyToNull(_exitBefore.Text),
                AfterScriptId = EmptyToNull(_exitAfter.Text),
            },
            RequireRuntime = _requireRuntime.ButtonPressed,
        };
    }

    private static string? EmptyToNull(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void Preview()
    {
        if (_draft is null)
        {
            SetStatus("Choisissez d’abord un déclencheur.", error: true);
            return;
        }
        var result = _session.PreviewTriggerFoundationUx1Y(ReadDraft());
        _apply.Disabled = !result.CanApply;
        _summary.Text = result.Succeeded ? result.Summary : result.Message;
        _summary.AddThemeColorOverride("font_color", result.Succeeded ? StudioStyle.Success : StudioStyle.Danger);
        if (result.SnappedVertices is not null && result.SnappedHoles is not null)
        {
            _contourPreview.SetContours(result.SnappedVertices, result.SnappedHoles);
        }
        SetStatus(result.Message, error: !result.Succeeded);
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedTriggerFoundationUx1Y();
        SetStatus(result.Message, error: !result.Succeeded);
        if (!result.Succeeded)
        {
            _apply.Disabled = true;
            return;
        }
        _applied = true;
        _apply.Disabled = true;
        _preview.Disabled = true;
        _undo.Disabled = false;
        _redo.Disabled = true;
        SetEditingEnabled(false);
        TriggerChanged?.Invoke();
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(result.Message, error: !result.Succeeded);
        if (result.Succeeded)
        {
            _undo.Disabled = true;
            _redo.Disabled = false;
            TriggerChanged?.Invoke();
        }
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(result.Message, error: !result.Succeeded);
        if (result.Succeeded)
        {
            _undo.Disabled = false;
            _redo.Disabled = true;
            TriggerChanged?.Invoke();
        }
    }

    private void ShowSection(int section)
    {
        _section = Math.Clamp(section, 0, 2);
        _volumePanel.Visible = _section == 0;
        _policyPanel.Visible = _section == 1;
        _eventsPanel.Visible = _section == 2;
        ApplyTabStyle(_volumeTab, _section == 0);
        ApplyTabStyle(_policyTab, _section == 1);
        ApplyTabStyle(_eventsTab, _section == 2);
    }

    private void ConnectInvalidation()
    {
        foreach (var spin in new[]
                 {
                     _height, _snap, _sizeX, _sizeY, _sizeZ,
                     _positionX, _positionY, _positionZ,
                     _rotationX, _rotationY, _rotationZ,
                     _scaleX, _scaleY, _scaleZ, _cooldown,
                 })
        {
            spin.ValueChanged += _ => InvalidatePreview();
        }
        foreach (var edit in new[]
                 {
                     _slug, _enterApi, _enterBinding, _enterBefore, _enterAfter,
                     _exitApi, _exitBinding, _exitBefore, _exitAfter,
                 })
        {
            edit.TextChanged += _ => InvalidatePreview();
        }
        _shape.ItemSelected += _ => InvalidatePreview();
        _fire.ItemSelected += _ => InvalidatePreview();
        _visualize.Toggled += _ => InvalidatePreview();
        _requireRuntime.Toggled += _ => InvalidatePreview();
    }

    private void InvalidatePreview()
    {
        if (_updating || _draft is null || _applied)
        {
            return;
        }
        _session.CancelTriggerFoundationUx1Y();
        _apply.Disabled = true;
        _preview.Disabled = false;
        _summary.Text = "Aperçu à recalculer · aucun changement appliqué.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        _contourPreview.SetContours(_vertices.Values, _holes.Values);
    }

    private void SetEditingEnabled(bool enabled)
    {
        foreach (var edit in new[]
                 {
                     _slug, _enterApi, _enterBinding, _enterBefore, _enterAfter,
                     _exitApi, _exitBinding, _exitBefore, _exitAfter,
                 })
        {
            edit.Editable = enabled;
        }
        foreach (var spin in new[]
                 {
                     _height, _snap, _sizeX, _sizeY, _sizeZ,
                     _positionX, _positionY, _positionZ,
                     _rotationX, _rotationY, _rotationZ,
                     _scaleX, _scaleY, _scaleZ, _cooldown,
                 })
        {
            spin.Editable = enabled;
        }
        _shape.Disabled = true;
        _fire.Disabled = !enabled;
        _visualize.Disabled = !enabled;
        _requireRuntime.Disabled = !enabled;
        _vertices.SetEditingEnabled(enabled);
        _holes.SetEditingEnabled(enabled);
        _entityKinds.SetEditingEnabled(enabled);
        _requiredTags.SetEditingEnabled(enabled);
        _excludedTags.SetEditingEnabled(enabled);
        _layers.SetEditingEnabled(enabled);
        _conditions.SetEditingEnabled(enabled);
        _actions.SetEditingEnabled(enabled);
        _volumeTab.Disabled = !enabled;
        _policyTab.Disabled = !enabled;
        _eventsTab.Disabled = !enabled;
        _preview.Disabled = !enabled;
        if (!enabled)
        {
            _apply.Disabled = true;
        }
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Success);
        StatusChanged?.Invoke(message, error);
    }

    private void UpdateDialogBounds()
    {
        var scale = Size.X >= 3200 ? 1.72f : Size.X >= 2300 ? 1.24f : 1f;
        var width = Math.Clamp((Size.X - 56) / scale, 900, 1160);
        var height = Math.Clamp((Size.Y - 72) / scale, 680, 850);
        var renderedWidth = width * scale;
        var renderedHeight = height * scale;
        _dialog.Scale = Vector2.One * scale;
        _dialog.CustomMaximumSize = new Vector2(width, height);
        _dialog.Position = new Vector2(
            (Size.X - renderedWidth) * .5f,
            Math.Max(24, (Size.Y - renderedHeight) * .45f));
        _dialog.Size = new Vector2(width, height);
    }

    private static VBoxContainer PanelColumn()
    {
        var panel = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.AddThemeConstantOverride("separation", 8);
        return panel;
    }

    private static Button Tab(string text)
    {
        var button = StudioStyle.Button(text, compact: true);
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return button;
    }

    private static void ApplyTabStyle(Button button, bool selected)
    {
        button.AddThemeStyleboxOverride(
            "normal",
            StudioStyle.Box(
                selected ? StudioStyle.Selection : StudioStyle.SurfaceRaised,
                selected ? StudioStyle.Success : StudioStyle.Border,
                6,
                selected ? 2 : 1));
    }

    private static GridContainer Grid() => new()
    {
        Columns = 2,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
    };

    private static T AddField<T>(GridContainer grid, string label, T control) where T : Control
    {
        var title = StudioStyle.Label(label, 10, StudioStyle.Muted);
        title.CustomMinimumSize = new Vector2(160, 0);
        title.VerticalAlignment = VerticalAlignment.Center;
        grid.AddChild(title);
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        grid.AddChild(control);
        return control;
    }

    private static LineEdit Text(string placeholder)
    {
        var value = new LineEdit { PlaceholderText = placeholder };
        StudioStyle.ApplyLineEdit(value);
        return value;
    }

    private static OptionButton Option(IEnumerable<string> labels)
    {
        var value = new OptionButton();
        StudioStyle.ApplyOptionButton(value);
        foreach (var label in labels)
        {
            value.AddItem(label);
        }
        return value;
    }

    private static SpinBox Number(double min, double max, double step, string suffix = "")
    {
        var value = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Suffix = suffix,
            AllowGreater = false,
            AllowLesser = false,
        };
        StudioStyle.ApplySpinBox(value);
        return value;
    }

    private static SpinBox Coordinate() => Number(-1_000_000, 1_000_000, .05, " m");

    private static CheckBox Check(string text)
    {
        var value = new CheckBox { Text = text, FocusMode = FocusModeEnum.All };
        value.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        value.AddThemeColorOverride("font_color", StudioStyle.Text);
        return value;
    }

    private static (PanelContainer Panel, LineEdit Api, LineEdit Binding, LineEdit Before, LineEdit After)
        EventPanel(string title)
    {
        var panel = StudioStyle.Panel($"Trigger{title}Event", StudioStyle.SurfaceRaised, 8, 1);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var margin = StudioStyle.Margin(10, 10, 10, 10);
        panel.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 7);
        margin.AddChild(column);
        column.AddChild(StudioStyle.SectionLabel(title));
        var grid = Grid();
        column.AddChild(grid);
        var api = AddField(grid, "API ForgeScript", Text("1.0.0"));
        var binding = AddField(grid, "Liaison canonique", Text(string.Empty));
        binding.Editable = false;
        binding.TooltipText = "Liaison typée imposée par le contrat runtime.";
        var before = AddField(grid, "Script avant", Text("Facultatif"));
        var after = AddField(grid, "Script après", Text("Facultatif"));
        return (panel, api, binding, before, after);
    }

    private static string HumanizeShape(TriggerWorldProfile.ShapeKind value) => value switch
    {
        TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D => "Polygone extrudé",
        TriggerWorldProfile.ShapeKind.Box3D => "Boîte 3D",
        TriggerWorldProfile.ShapeKind.Sphere3D => "Sphère 3D",
        TriggerWorldProfile.ShapeKind.Capsule3D => "Capsule 3D",
        TriggerWorldProfile.ShapeKind.ConvexHull3D => "Volume convexe 3D",
        _ => "Volume",
    };

    private static string HumanizeFire(TriggerWorldProfile.FirePolicy value) => value switch
    {
        TriggerWorldProfile.FirePolicy.Once => "Une seule fois",
        TriggerWorldProfile.FirePolicy.OncePerEntity => "Une fois par entité",
        TriggerWorldProfile.FirePolicy.Repeat => "Répétable",
        TriggerWorldProfile.FirePolicy.WhileInside => "Tant que présent",
        _ => "Politique définie",
    };
}
