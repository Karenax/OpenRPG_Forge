using Forge.Domain.Areas;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal static class StudioUx2AControls
{
    internal static LineEdit Text(string placeholder = "")
    {
        var edit = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        StudioStyle.ApplyLineEdit(edit);
        return edit;
    }

    internal static SpinBox Number(double minimum, double maximum, double step = 1)
    {
        var value = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Step = step,
            AllowGreater = false,
            AllowLesser = false,
            UpdateOnTextChanged = true,
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
        foreach (var value in values)
        {
            option.AddItem(value);
        }
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

    internal static VBoxContainer Field(string label, Control control, int minimumWidth = 120)
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
        root.AddThemeConstantOverride("separation", 5);
        root.AddChild(StudioStyle.Label(title, 12));
        root.AddChild(StudioStyle.Label(help, 9, StudioStyle.Muted, wrap: true));
        root.AddChild(content);
        margin.AddChild(root);
        return panel;
    }
}

internal sealed partial class StudioUx2AHeartbeatRow : PanelContainer
{
    private readonly Guid _id;
    private readonly LineEdit _name;
    private readonly CheckBox _enabled;
    private readonly SpinBox _interval;
    private readonly SpinBox _budget;
    private readonly CheckBox _suspend;
    private readonly LineEdit _api;
    private readonly LineEdit _handler;
    private readonly OptionButton _script;
    private readonly CheckBox _required;
    private readonly Button _remove;
    private readonly List<string?> _scriptValues = [];
    private bool _updating;

    internal event Action? Changed;
    internal event Action<StudioUx2AHeartbeatRow>? RemoveRequested;

    internal StudioUx2AHeartbeatDraft Value => new(
        _id,
        _name.Text,
        _enabled.ButtonPressed,
        (int)_interval.Value,
        (int)_budget.Value,
        _suspend.ButtonPressed,
        _api.Text,
        _handler.Text,
        _script.Selected >= 0 && _script.Selected < _scriptValues.Count
            ? _scriptValues[_script.Selected]
            : null,
        _required.ButtonPressed);

    internal StudioUx2AHeartbeatRow(
        StudioUx2AHeartbeatDraft value,
        IReadOnlyList<StudioEventScriptChoice> scripts)
    {
        _id = value.Id;
        AddThemeStyleboxOverride("panel", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        var margin = StudioStyle.Margin(8, 6, 8, 6);
        AddChild(margin);
        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 5);
        margin.AddChild(root);

        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddThemeConstantOverride("separation", 6);
        root.AddChild(heading);
        _name = StudioUx2AControls.Text("Nom du rythme");
        heading.AddChild(StudioUx2AControls.Field("Nom humain", _name, 220));
        _enabled = StudioUx2AControls.Check("Actif");
        heading.AddChild(_enabled);
        _suspend = StudioUx2AControls.Check("Suspendu hors observation");
        heading.AddChild(_suspend);
        _required = StudioUx2AControls.Check("Script requis au runtime");
        heading.AddChild(_required);
        _remove = StudioStyle.Button("Retirer", compact: true);
        _remove.TooltipText = "Retirer explicitement ce rythme du brouillon.";
        _remove.Pressed += () => RemoveRequested?.Invoke(this);
        heading.AddChild(_remove);

        var timing = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        timing.AddThemeConstantOverride("h_separation", 7);
        timing.AddThemeConstantOverride("v_separation", 5);
        root.AddChild(timing);
        _interval = StudioUx2AControls.Number(0, 86_400_000);
        _budget = StudioUx2AControls.Number(0, 1_000);
        _api = StudioUx2AControls.Text("Ex. 1.0");
        _handler = StudioUx2AControls.Text("Ex. main");
        timing.AddChild(StudioUx2AControls.Field("Cadence (ms)", _interval));
        timing.AddChild(StudioUx2AControls.Field("Budget (ms)", _budget));
        timing.AddChild(StudioUx2AControls.Field("API ForgeScript", _api));
        timing.AddChild(StudioUx2AControls.Field("Point d’entrée", _handler));

        _script = StudioUx2AControls.Options();
        PopulateScripts(scripts, value.ScriptPath);
        root.AddChild(StudioUx2AControls.Field(
            "Script réel du projet · aucun handler n’est exécuté par cet aperçu",
            _script,
            320));

        _updating = true;
        _name.Text = value.Name;
        _enabled.ButtonPressed = value.Enabled;
        _interval.Value = value.IntervalMilliseconds;
        _budget.Value = value.BudgetMilliseconds;
        _suspend.ButtonPressed = value.SuspendWhenInactive;
        _api.Text = value.ForgeScriptApiVersion;
        _handler.Text = value.Handler;
        _required.ButtonPressed = value.RequiredAtRuntime;
        _updating = false;
        ConnectChanges();
    }

    internal void SetEditingEnabled(bool enabled)
    {
        _name.Editable = enabled;
        _enabled.Disabled = !enabled;
        _interval.Editable = enabled;
        _budget.Editable = enabled;
        _suspend.Disabled = !enabled;
        _api.Editable = enabled;
        _handler.Editable = enabled;
        _script.Disabled = !enabled;
        _required.Disabled = !enabled;
        _remove.Disabled = !enabled;
    }

    private void PopulateScripts(IReadOnlyList<StudioEventScriptChoice> scripts, string? current)
    {
        _script.Clear();
        _scriptValues.Clear();
        _script.AddItem("Aucun script");
        _scriptValues.Add(null);
        foreach (var item in scripts)
        {
            _script.AddItem($"{item.DisplayName} · source : projet/scripts");
            _scriptValues.Add(item.RelativePath);
        }
        var selected = current is null
            ? 0
            : _scriptValues.FindIndex(item => string.Equals(item, current, StringComparison.Ordinal));
        if (selected < 0)
        {
            _script.AddItem("Script hérité indisponible · conservé tant qu’il n’est pas remplacé");
            _scriptValues.Add(current);
            selected = _scriptValues.Count - 1;
        }
        _script.Select(selected);
    }

    private void ConnectChanges()
    {
        void Notify()
        {
            if (!_updating)
            {
                Changed?.Invoke();
            }
        }
        _name.TextChanged += _ => Notify();
        _enabled.Toggled += _ => Notify();
        _interval.ValueChanged += _ => Notify();
        _budget.ValueChanged += _ => Notify();
        _suspend.Toggled += _ => Notify();
        _api.TextChanged += _ => Notify();
        _handler.TextChanged += _ => Notify();
        _script.ItemSelected += _ => Notify();
        _required.Toggled += _ => Notify();
    }
}

internal sealed partial class StudioUx2AHeartbeatEditor : VBoxContainer
{
    private readonly VBoxContainer _rows;
    private readonly Button _add;
    private readonly List<StudioUx2AHeartbeatRow> _editors = [];
    private IReadOnlyList<StudioEventScriptChoice> _scripts = [];
    private bool _editingEnabled;

    internal event Action? Changed;
    internal IReadOnlyList<StudioUx2AHeartbeatDraft> Values => _editors.Select(item => item.Value).ToArray();
    internal bool ShowsCompleteCollection => _editors.Count == Values.Count;

    internal StudioUx2AHeartbeatEditor()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 5);
        _rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 5);
        AddChild(_rows);
        _add = StudioStyle.Button("Ajouter un rythme", compact: true);
        _add.TooltipText =
            "Ajoute un brouillon vide après une action explicite ; aucun rythme n’est créé automatiquement.";
        _add.Pressed += () =>
        {
            AddRow(new StudioUx2AHeartbeatDraft(
                Guid.NewGuid(), string.Empty, false, 1_000, 10, true, "1.0", "main", null, false));
            Changed?.Invoke();
        };
        AddChild(_add);
    }

    internal void SetValues(
        IReadOnlyList<StudioUx2AHeartbeatDraft> values,
        IReadOnlyList<StudioEventScriptChoice> scripts)
    {
        foreach (var editor in _editors)
        {
            editor.QueueFree();
        }
        _editors.Clear();
        _scripts = scripts;
        foreach (var value in values)
        {
            AddRow(value);
        }
    }

    internal void SetEditingEnabled(bool enabled)
    {
        _editingEnabled = enabled;
        _add.Disabled = !enabled;
        foreach (var editor in _editors)
        {
            editor.SetEditingEnabled(enabled);
        }
    }

    private void AddRow(StudioUx2AHeartbeatDraft value)
    {
        var editor = new StudioUx2AHeartbeatRow(value, _scripts);
        editor.Changed += () => Changed?.Invoke();
        editor.RemoveRequested += RemoveRow;
        editor.SetEditingEnabled(_editingEnabled);
        _editors.Add(editor);
        _rows.AddChild(editor);
    }

    private void RemoveRow(StudioUx2AHeartbeatRow editor)
    {
        if (!_editingEnabled || !_editors.Remove(editor))
        {
            return;
        }
        editor.QueueFree();
        Changed?.Invoke();
    }
}

internal sealed partial class StudioUx2APerceptionRow : PanelContainer
{
    private readonly Guid _id;
    private readonly LineEdit _sense;
    private readonly SpinBox _multiplier;
    private readonly SpinBox _additive;
    private readonly LineEdit _condition;
    private readonly Button _remove;

    internal event Action? Changed;
    internal event Action<StudioUx2APerceptionRow>? RemoveRequested;

    internal StudioUx2APerceptionModifierDraft Value => new(
        _id,
        _sense.Text,
        _multiplier.Value,
        _additive.Value,
        _condition.Text);

    internal StudioUx2APerceptionRow(StudioUx2APerceptionModifierDraft value)
    {
        _id = value.Id;
        AddThemeStyleboxOverride("panel", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        var margin = StudioStyle.Margin(8, 6, 8, 6);
        AddChild(margin);
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 6);
        margin.AddChild(row);
        _sense = StudioUx2AControls.Text("Ex. vision, hearing");
        _sense.Text = value.SenseId;
        row.AddChild(StudioUx2AControls.Field("Sens", _sense, 150));
        _multiplier = StudioUx2AControls.Number(0, 10, .05);
        _multiplier.Value = value.Multiplier;
        row.AddChild(StudioUx2AControls.Field("Portée ×", _multiplier, 110));
        _additive = StudioUx2AControls.Number(-10, 10, .05);
        _additive.Value = value.AdditiveModifier;
        row.AddChild(StudioUx2AControls.Field("Confiance +/−", _additive, 110));
        _condition = StudioUx2AControls.Text("Toujours si vide");
        _condition.Text = value.ConditionTag;
        row.AddChild(StudioUx2AControls.Field("Condition", _condition, 160));
        _remove = StudioStyle.Button("Retirer", compact: true);
        _remove.Pressed += () => RemoveRequested?.Invoke(this);
        row.AddChild(_remove);

        _sense.TextChanged += _ => Changed?.Invoke();
        _multiplier.ValueChanged += _ => Changed?.Invoke();
        _additive.ValueChanged += _ => Changed?.Invoke();
        _condition.TextChanged += _ => Changed?.Invoke();
    }

    internal void SetEditingEnabled(bool enabled)
    {
        _sense.Editable = enabled;
        _multiplier.Editable = enabled;
        _additive.Editable = enabled;
        _condition.Editable = enabled;
        _remove.Disabled = !enabled;
    }
}

internal sealed partial class StudioUx2APerceptionEditor : VBoxContainer
{
    private readonly VBoxContainer _rows;
    private readonly Button _add;
    private readonly List<StudioUx2APerceptionRow> _editors = [];
    private bool _editingEnabled;

    internal event Action? Changed;
    internal IReadOnlyList<StudioUx2APerceptionModifierDraft> Values =>
        _editors.Select(item => item.Value).ToArray();
    internal bool ShowsCompleteCollection => _editors.Count == Values.Count;

    internal StudioUx2APerceptionEditor()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 5);
        _rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 5);
        AddChild(_rows);
        _add = StudioStyle.Button("Ajouter un modificateur", compact: true);
        _add.TooltipText =
            "Ajoute une ligne vide après une action explicite ; la zone n’est jamais enrichie automatiquement.";
        _add.Pressed += () =>
        {
            AddRow(new StudioUx2APerceptionModifierDraft(Guid.NewGuid(), string.Empty, 1, 0, string.Empty));
            Changed?.Invoke();
        };
        AddChild(_add);
    }

    internal void SetValues(IReadOnlyList<StudioUx2APerceptionModifierDraft> values)
    {
        foreach (var editor in _editors)
        {
            editor.QueueFree();
        }
        _editors.Clear();
        foreach (var value in values)
        {
            AddRow(value);
        }
    }

    internal void SetEditingEnabled(bool enabled)
    {
        _editingEnabled = enabled;
        _add.Disabled = !enabled;
        foreach (var editor in _editors)
        {
            editor.SetEditingEnabled(enabled);
        }
    }

    private void AddRow(StudioUx2APerceptionModifierDraft value)
    {
        var editor = new StudioUx2APerceptionRow(value);
        editor.Changed += () => Changed?.Invoke();
        editor.RemoveRequested += RemoveRow;
        editor.SetEditingEnabled(_editingEnabled);
        _editors.Add(editor);
        _rows.AddChild(editor);
    }

    private void RemoveRow(StudioUx2APerceptionRow editor)
    {
        if (!_editingEnabled || !_editors.Remove(editor))
        {
            return;
        }
        editor.QueueFree();
        Changed?.Invoke();
    }
}

internal sealed partial class StudioUx2ARestVolumeRow : PanelContainer
{
    private readonly Guid _id;
    private readonly LineEdit _name;
    private readonly SpinBox _centerX;
    private readonly SpinBox _centerY;
    private readonly SpinBox _centerZ;
    private readonly SpinBox _sizeX;
    private readonly SpinBox _sizeY;
    private readonly SpinBox _sizeZ;
    private readonly OptionButton _mode;
    private readonly LineEdit _reason;
    private readonly LineEdit _condition;
    private readonly SpinBox _cooldown;
    private readonly SpinBox _secureRadius;
    private readonly SpinBox _priority;
    private readonly Button _remove;

    internal event Action? Changed;
    internal event Action<StudioUx2ARestVolumeRow>? RemoveRequested;

    internal StudioUx2ARestVolumeDraft Value => new(
        _id,
        _name.Text,
        new Vector3Value((float)_centerX.Value, (float)_centerY.Value, (float)_centerZ.Value),
        new Vector3Value((float)_sizeX.Value, (float)_sizeY.Value, (float)_sizeZ.Value),
        (RestPermissionMode)_mode.Selected,
        _reason.Text,
        _condition.Text,
        (int)_cooldown.Value,
        (float)_secureRadius.Value,
        (int)_priority.Value);

    internal StudioUx2ARestVolumeRow(StudioUx2ARestVolumeDraft value)
    {
        _id = value.Id;
        AddThemeStyleboxOverride("panel", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        var margin = StudioStyle.Margin(8, 6, 8, 6);
        AddChild(margin);
        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 5);
        margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddThemeConstantOverride("separation", 6);
        root.AddChild(header);
        _name = StudioUx2AControls.Text("Nom du volume");
        _name.Text = value.Name;
        header.AddChild(StudioUx2AControls.Field("Nom humain", _name, 240));
        _mode = StudioUx2AControls.Options("Autorisé", "Interdit", "Conditionnel");
        _mode.Select((int)value.Mode);
        header.AddChild(StudioUx2AControls.Field("Règle prioritaire", _mode, 150));
        _priority = StudioUx2AControls.Number(-10_000, 10_000);
        _priority.Value = value.Priority;
        header.AddChild(StudioUx2AControls.Field("Priorité", _priority, 100));
        _remove = StudioStyle.Button("Retirer", compact: true);
        _remove.Pressed += () => RemoveRequested?.Invoke(this);
        header.AddChild(_remove);

        var geometry = new GridContainer { Columns = 6, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        geometry.AddThemeConstantOverride("h_separation", 5);
        root.AddChild(geometry);
        _centerX = Coordinate(value.Center.X);
        _centerY = Coordinate(value.Center.Y);
        _centerZ = Coordinate(value.Center.Z);
        _sizeX = SizeCoordinate(value.Size.X);
        _sizeY = SizeCoordinate(value.Size.Y);
        _sizeZ = SizeCoordinate(value.Size.Z);
        geometry.AddChild(StudioUx2AControls.Field("Centre X", _centerX, 88));
        geometry.AddChild(StudioUx2AControls.Field("Centre Y", _centerY, 88));
        geometry.AddChild(StudioUx2AControls.Field("Centre Z", _centerZ, 88));
        geometry.AddChild(StudioUx2AControls.Field("Largeur", _sizeX, 88));
        geometry.AddChild(StudioUx2AControls.Field("Hauteur", _sizeY, 88));
        geometry.AddChild(StudioUx2AControls.Field("Profondeur", _sizeZ, 88));

        var rules = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rules.AddThemeConstantOverride("h_separation", 6);
        root.AddChild(rules);
        _reason = StudioUx2AControls.Text("Message joueur si refusé");
        _reason.Text = value.DenialReason;
        _condition = StudioUx2AControls.Text("Condition si nécessaire");
        _condition.Text = value.Condition;
        _cooldown = StudioUx2AControls.Number(0, 86_400);
        _cooldown.Value = value.CooldownSeconds;
        _secureRadius = StudioUx2AControls.Number(0, 512, .25);
        _secureRadius.Value = value.RequiredSecureRadius;
        rules.AddChild(StudioUx2AControls.Field("Explication joueur", _reason, 240));
        rules.AddChild(StudioUx2AControls.Field("Condition", _condition, 180));
        rules.AddChild(StudioUx2AControls.Field("Recharge (s)", _cooldown, 110));
        rules.AddChild(StudioUx2AControls.Field("Rayon sécurisé", _secureRadius, 110));
        ConnectChanges();
    }

    internal void SetEditingEnabled(bool enabled)
    {
        _name.Editable = enabled;
        _mode.Disabled = !enabled;
        _reason.Editable = enabled;
        _condition.Editable = enabled;
        foreach (var spin in new[]
                 {
                     _centerX, _centerY, _centerZ, _sizeX, _sizeY, _sizeZ, _cooldown, _secureRadius, _priority,
                 })
        {
            spin.Editable = enabled;
        }
        _remove.Disabled = !enabled;
    }

    private static SpinBox Coordinate(float value)
    {
        var spin = StudioUx2AControls.Number(-32_768, 32_768, .25);
        spin.Value = value;
        return spin;
    }

    private static SpinBox SizeCoordinate(float value)
    {
        var spin = StudioUx2AControls.Number(0, 65_536, .25);
        spin.Value = value;
        return spin;
    }

    private void ConnectChanges()
    {
        _name.TextChanged += _ => Changed?.Invoke();
        _mode.ItemSelected += _ => Changed?.Invoke();
        _reason.TextChanged += _ => Changed?.Invoke();
        _condition.TextChanged += _ => Changed?.Invoke();
        foreach (var spin in new[]
                 {
                     _centerX, _centerY, _centerZ, _sizeX, _sizeY, _sizeZ, _cooldown, _secureRadius, _priority,
                 })
        {
            spin.ValueChanged += _ => Changed?.Invoke();
        }
    }
}

internal sealed partial class StudioUx2ARestVolumeEditor : VBoxContainer
{
    private readonly VBoxContainer _rows;
    private readonly Button _add;
    private readonly List<StudioUx2ARestVolumeRow> _editors = [];
    private bool _editingEnabled;

    internal event Action? Changed;
    internal IReadOnlyList<StudioUx2ARestVolumeDraft> Values => _editors.Select(item => item.Value).ToArray();
    internal bool ShowsCompleteCollection => _editors.Count == Values.Count;

    internal StudioUx2ARestVolumeEditor()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 5);
        _rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 5);
        AddChild(_rows);
        _add = StudioStyle.Button("Ajouter un volume", compact: true);
        _add.TooltipText =
            "Ajoute un volume vide après une action explicite ; sa géométrie doit être renseignée avant l’aperçu.";
        _add.Pressed += () =>
        {
            AddRow(new StudioUx2ARestVolumeDraft(
                Guid.NewGuid(), string.Empty, Vector3Value.Zero, Vector3Value.Zero, RestPermissionMode.Allowed,
                string.Empty, string.Empty, 0, 0, 0));
            Changed?.Invoke();
        };
        AddChild(_add);
    }

    internal void SetValues(IReadOnlyList<StudioUx2ARestVolumeDraft> values)
    {
        foreach (var editor in _editors)
        {
            editor.QueueFree();
        }
        _editors.Clear();
        foreach (var value in values)
        {
            AddRow(value);
        }
    }

    internal void SetEditingEnabled(bool enabled)
    {
        _editingEnabled = enabled;
        _add.Disabled = !enabled;
        foreach (var editor in _editors)
        {
            editor.SetEditingEnabled(enabled);
        }
    }

    private void AddRow(StudioUx2ARestVolumeDraft value)
    {
        var editor = new StudioUx2ARestVolumeRow(value);
        editor.Changed += () => Changed?.Invoke();
        editor.RemoveRequested += RemoveRow;
        editor.SetEditingEnabled(_editingEnabled);
        _editors.Add(editor);
        _rows.AddChild(editor);
    }

    private void RemoveRow(StudioUx2ARestVolumeRow editor)
    {
        if (!_editingEnabled || !_editors.Remove(editor))
        {
            return;
        }
        editor.QueueFree();
        Changed?.Invoke();
    }
}

internal sealed partial class StudioLivingWorldPoliciesOverlay : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx2AEvidenceSourceSnapshot? _evidenceSourceSnapshot;
    private string? _evidenceTempRoot;
    private bool _lastEvidenceSourceUntouched = true;
    private bool _lastEvidenceResourcesReleased = true;
    private bool _lastSourceRestored = true;
    private bool _lastEvidencePreviewDryRun;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _area;
    private readonly Label _selection;
    private readonly StudioUx2AHeartbeatEditor _heartbeats;
    private readonly LineEdit _perceptionRuleset;
    private readonly StudioUx2APerceptionEditor _perception;
    private readonly Label _projectRestState;
    private readonly OptionButton _restMode;
    private readonly LineEdit _restReason;
    private readonly LineEdit _restCondition;
    private readonly SpinBox _restCooldown;
    private readonly SpinBox _restSecureRadius;
    private readonly StudioUx2ARestVolumeEditor _restVolumes;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    private IReadOnlyList<StudioUx2AAreaChoice> _choices = [];
    private StudioUx2ADraft? _draft;
    private StudioUx2APreview? _lastPreview;
    private bool _updating;
    private bool _applied;
    private bool _canonicalPreviewReady;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? AreaPoliciesChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _choices.Count;
    internal bool HasExplicitSelection => _draft is not null && _area.Selected > 0;
    internal bool CanonicalPreviewReady => _canonicalPreviewReady;
    internal bool ShowsCompleteCollections => _draft is not null &&
                                               _heartbeats.ShowsCompleteCollection &&
                                               _perception.ShowsCompleteCollection &&
                                               _restVolumes.ShowsCompleteCollection &&
                                               _heartbeats.Values.Count == _draft.Heartbeats.Count &&
                                               _perception.Values.Count == _draft.PerceptionModifiers.Count &&
                                               _restVolumes.Values.Count == _draft.RestVolumes.Count;
    internal bool ShowsHumanPerceptionDiff
    {
        get
        {
            var preview = _lastPreview;
            return preview is not null && preview.Diff.Perception.Count > 0 &&
                   preview.Diff.Perception.All(item => !ContainsUuid(item));
        }
    }
    internal bool ShowsDormantRest => _lastPreview?.ProjectRestDormant == true &&
                                      _projectRestState.Text.Contains("dormant", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsStructuredDiagnostics => _lastPreview?.Groups is { Count: 4 } groups &&
                                                 groups.Select(item => item.Key).Distinct().Count() == 4 &&
                                                 groups.SelectMany(item => item.Diagnostics).All(item =>
                                                     !string.IsNullOrWhiteSpace(item.Code) &&
                                                     !string.IsNullOrWhiteSpace(item.Path));
    internal bool UsesIsolatedEvidence =>
        _evidenceSession is not null && ReferenceEquals(_session, _evidenceSession);
    internal bool EvidenceSourceUntouched => _evidenceSourceSnapshot is { } snapshot
        ? SourceMatchesSnapshot(snapshot)
        : _lastEvidenceSourceUntouched;
    internal bool EvidenceResourcesReleased =>
        _evidenceSession is null && _evidenceTempRoot is null && _lastEvidenceResourcesReleased;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession) && _lastSourceRestored;

    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantLabels(_dialog).Select(item => item.Text));
            return HasExplicitSelection &&
                   text.Contains("Heartbeat", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Perception", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Repos", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuid(text);
        }
    }

    internal StudioLivingWorldPoliciesOverlay(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = _sourceSession;
        Name = "StudioLivingWorldPoliciesOverlay";
        Visible = false;
        ZIndex = 460;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect
        {
            Color = new Color(0, 0, 0, .82f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        _dialog = StudioStyle.Panel("LivingWorldPoliciesDialog", StudioStyle.Chrome, 12, 2);
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
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddThemeConstantOverride("separation", 8);
        root.AddChild(header);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("MONDE VIVANT · POLITIQUES DE ZONE", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Rythmes, perception et repos", 20));
        heading.AddChild(StudioStyle.Label(
            "AREA-023 / 025 / 026 · aperçu sec canonique · aucune exécution de script, simulation moteur, " +
            "navigation, entrée viewport ni diffusion réseau",
            10,
            StudioStyle.Muted,
            wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.TooltipText = "Fermer sans appliquer le brouillon · Échap";
        close.Pressed += Close;
        header.AddChild(close);

        var selector = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selector.AddThemeConstantOverride("separation", 8);
        root.AddChild(selector);
        selector.AddChild(StudioStyle.Label("Zone à configurer", 10, StudioStyle.Muted));
        _area = StudioUx2AControls.Options();
        _area.ItemSelected += SelectArea;
        selector.AddChild(_area);
        _selection = StudioStyle.Label(
            "Choisissez explicitement une zone ; aucune fiche n’est sélectionnée automatiquement.",
            11,
            StudioStyle.Warning,
            wrap: true);
        root.AddChild(_selection);

        var bodyHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 470),
            CustomMaximumSize = new Vector2(-1, 760),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            PropagateMaximumSize = true,
        };
        root.AddChild(bodyHost);
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true,
        };
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bodyHost.AddChild(scroll);
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 9);
        scroll.AddChild(content);

        _heartbeats = new StudioUx2AHeartbeatEditor();
        content.AddChild(StudioUx2AControls.Card(
            "AreaHeartbeatPolicies",
            "Heartbeat de zone",
            "Collection complète, cadence et budget. Les scripts proposés proviennent du projet ouvert ; " +
            "l’aperçu calcule seulement la politique du planificateur.",
            _heartbeats));

        var perceptionContent = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        perceptionContent.AddThemeConstantOverride("separation", 5);
        _perceptionRuleset = StudioUx2AControls.Text("Identifiant de règles nommé");
        perceptionContent.AddChild(StudioUx2AControls.Field("Profil de règles de perception", _perceptionRuleset));
        _perception = new StudioUx2APerceptionEditor();
        perceptionContent.AddChild(_perception);
        content.AddChild(StudioUx2AControls.Card(
            "AreaPerceptionPolicies",
            "Modificateurs de perception",
            "Diff humain explicite sur la portée et la confiance. Aucun effet visuel, gizmo ou rendu moteur n’est simulé.",
            perceptionContent));

        var restContent = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        restContent.AddThemeConstantOverride("separation", 5);
        _projectRestState = StudioStyle.Label(
            "État du cycle de repos du projet : non évalué",
            10,
            StudioStyle.Warning,
            wrap: true);
        restContent.AddChild(_projectRestState);
        var restPolicy = new GridContainer { Columns = 5, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        restPolicy.AddThemeConstantOverride("h_separation", 6);
        restContent.AddChild(restPolicy);
        _restMode = StudioUx2AControls.Options("Autorisé", "Interdit", "Conditionnel");
        _restReason = StudioUx2AControls.Text("Message joueur si refusé");
        _restCondition = StudioUx2AControls.Text("Condition si nécessaire");
        _restCooldown = StudioUx2AControls.Number(0, 86_400);
        _restSecureRadius = StudioUx2AControls.Number(0, 512, .25);
        restPolicy.AddChild(StudioUx2AControls.Field("Règle par défaut", _restMode, 130));
        restPolicy.AddChild(StudioUx2AControls.Field("Explication joueur", _restReason, 220));
        restPolicy.AddChild(StudioUx2AControls.Field("Condition", _restCondition, 180));
        restPolicy.AddChild(StudioUx2AControls.Field("Recharge (s)", _restCooldown, 105));
        restPolicy.AddChild(StudioUx2AControls.Field("Rayon sécurisé", _restSecureRadius, 105));
        _restVolumes = new StudioUx2ARestVolumeEditor();
        restContent.AddChild(_restVolumes);
        content.AddChild(StudioUx2AControls.Card(
            "AreaRestPolicies",
            "Interdiction et volumes de repos",
            "Les volumes sont des politiques spatiales prioritaires. Une configuration dormante reste dormante : " +
            "cet outil ne réactive jamais le cycle de repos du module.",
            restContent));

        _diff = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _diff.AddThemeConstantOverride("separation", 4);
        content.AddChild(StudioUx2AControls.Card(
            "AreaPolicyHumanDiff",
            "Diff humain avant application",
            "Les changements de perception restent lisibles avant la commande canonique.",
            _diff));
        _diagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _diagnostics.AddThemeConstantOverride("separation", 4);
        content.AddChild(StudioUx2AControls.Card(
            "AreaPolicyDiagnostics",
            "Diagnostics localisés",
            "Codes et chemins canoniques sont conservés en interne ; cette vue les regroupe en actions humaines.",
            _diagnostics));

        _summary = StudioStyle.Label(
            "Sélectionnez une zone puis lancez l’aperçu sec · aucun changement appliqué.",
            11,
            StudioStyle.Muted,
            wrap: true);
        root.AddChild(_summary);
        _status = StudioStyle.Label("Prêt · sélection explicite requise", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        var footer = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footer.AddThemeConstantOverride("separation", 7);
        root.AddChild(footer);
        _preview = StudioStyle.Button("Aperçu sec", primary: true);
        _preview.TooltipText = "Valider et calculer les trois politiques sans modifier le projet.";
        _preview.Pressed += Preview;
        footer.AddChild(_preview);
        _apply = StudioStyle.Button("Appliquer", primary: true);
        _apply.TooltipText = "Disponible seulement après un aperçu sec réussi et encore frais.";
        _apply.Pressed += Apply;
        footer.AddChild(_apply);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _undo.Pressed += Undo;
        footer.AddChild(_undo);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.Pressed += Redo;
        footer.AddChild(_redo);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        _save.Pressed += Save;
        footer.AddChild(_save);
        footer.AddChild(StudioStyle.Label(
            "Une seule commande de politiques de zone ; module, autres zones, cinématiques, combat et expérience préservés.",
            10,
            StudioStyle.Muted,
            wrap: true));

        ConnectInvalidation();
        ResetControls();
        SetEditingEnabled(false);
        Resized += UpdateDialogBounds;
    }

    internal void Open()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null)
        {
            Close();
        }
        _session = _sourceSession;
        OpenBoundSession();
    }

    private void OpenBoundSession()
    {
        _session.CancelAreaPoliciesUx2A();
        _choices = _session.AreaPolicyChoicesUx2A;
        _area.Clear();
        _area.AddItem("Choisissez explicitement une zone…");
        foreach (var choice in _choices)
        {
            _area.AddItem(choice.Label);
        }
        _area.Select(0);
        _draft = null;
        _lastPreview = null;
        _applied = false;
        _canonicalPreviewReady = false;
        ResetControls();
        _selection.Text = _choices.Count == 0
            ? "Aucune zone n’est disponible dans ce projet."
            : "Choisissez explicitement une zone ; aucune fiche n’est sélectionnée automatiquement.";
        _selection.AddThemeColorOverride(
            "font_color",
            _choices.Count == 0 ? StudioStyle.Danger : StudioStyle.Warning);
        _summary.Text = "Sélectionnez une zone puis lancez l’aperçu sec · aucun changement appliqué.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        SetStatus("Prêt · sélection explicite requise", error: false);
        RenderDiff(null);
        RenderDiagnostics([]);
        SetEditingEnabled(false);
        UpdateTransactionButtons();
        Visible = true;
        MoveToFront();
        UpdateDialogBounds();
        _area.GrabFocus();
    }

    internal void Close()
    {
        var sourceUntouched = _evidenceSourceSnapshot is not { } snapshot || SourceMatchesSnapshot(snapshot);
        _session.CancelAreaPoliciesUx2A();
        Visible = false;
        _draft = null;
        _lastPreview = null;
        _applied = false;
        _canonicalPreviewReady = false;
        ResetControls();
        var released = ReleaseIsolatedEvidenceSession();
        _lastEvidenceSourceUntouched = sourceUntouched;
        _lastEvidenceResourcesReleased = released;
        _lastSourceRestored = ReferenceEquals(_session, _sourceSession);
    }

    internal bool PrepareEvidence(string mode)
    {
        _ = mode;
        if ((_evidenceSession is not null || _evidenceTempRoot is not null) &&
            !ReleaseIsolatedEvidenceSession())
        {
            return false;
        }
        if (!TryOpenIsolatedEvidenceSession())
        {
            return false;
        }
        var choiceIndex = _choices.ToList().FindIndex(item => item.IsEvidence);
        if (choiceIndex < 0)
        {
            Close();
            return false;
        }
        _area.Select(choiceIndex + 1);
        LoadChoice(choiceIndex);
        if (_draft is null || !ConfigureEvidenceDraft())
        {
            Close();
            return false;
        }

        var history = _session.History;
        var project = _session.Project;
        var revision = history?.State.Revision;
        var journal = history?.Journal.ToArray() ?? [];
        var canUndo = history?.CanUndo;
        var canRedo = history?.CanRedo;
        var dirty = _session.IsDirty;
        Preview();
        _lastEvidencePreviewDryRun = project is not null &&
                                     ReferenceEquals(project, _session.Project) &&
                                     ReferenceEquals(history, _session.History) &&
                                     history?.State.Revision == revision &&
                                     (history?.Journal ?? []).SequenceEqual(journal) &&
                                     history?.CanUndo == canUndo && history?.CanRedo == canRedo &&
                                     _session.IsDirty == dirty;
        _selection.Text += " · session de preuve isolée";
        _summary.Text = "PREUVE ISOLÉE · le projet source reste strictement inchangé.\n" + _summary.Text;
        SetStatus("Preuve isolée prête · fermeture = suppression de la session temporaire", error: false);
        return _lastEvidencePreviewDryRun && !_apply.Disabled && HasExplicitSelection &&
               CanonicalPreviewReady && ShowsCompleteCollections && ShowsHumanPerceptionDiff &&
               ShowsDormantRest && ShowsStructuredDiagnostics && ShowsHumanSurface &&
               UsesIsolatedEvidence && EvidenceSourceUntouched;
    }

    internal bool RunUiTransactionEvidence()
    {
        var sourceSnapshot = CaptureSourceSnapshot();
        if (sourceSnapshot is null)
        {
            return false;
        }
        StudioSessionController? reopened = null;
        try
        {
            var ready = PrepareEvidence("ux2-a-area-living-world");
            var transactionSession = _session;
            var originalProject = transactionSession.Project;
            var targetId = _draft?.AreaId;
            var sourceArea = targetId is { } areaId
                ? originalProject?.Areas.SingleOrDefault(item => item.Id == areaId)
                : null;
            var expectedDraft = ReadDraft();
            var dryRun = ready && _lastEvidencePreviewDryRun && originalProject is not null &&
                         sourceArea is not null && expectedDraft is not null &&
                         UsesIsolatedEvidence && SourceMatchesSnapshot(sourceSnapshot);
            Apply();
            var appliedProject = transactionSession.Project;
            var appliedArea = targetId is { } appliedAreaId
                ? appliedProject?.Areas.SingleOrDefault(item => item.Id == appliedAreaId)
                : null;
            var applied = dryRun && _applied && appliedArea is not null &&
                          !ReferenceEquals(originalProject, appliedProject) &&
                          ReferenceEquals(sourceArea!.RuntimePolicies.Cinematics,
                              appliedArea.RuntimePolicies.Cinematics) &&
                           ReferenceEquals(sourceArea!.RuntimePolicies.Rules.Combat,
                               appliedArea.RuntimePolicies.Rules.Combat) &&
                           ReferenceEquals(sourceArea!.RuntimePolicies.Experience,
                               appliedArea.RuntimePolicies.Experience);
            var expectedAreaJson = appliedArea is null ? null : AreaJsonSerializer.Serialize(appliedArea);
            if (applied)
            {
                Undo();
            }
            var undone = applied && ReferenceEquals(originalProject, transactionSession.Project);
            if (undone)
            {
                Redo();
            }
            var redone = undone && ReferenceEquals(appliedProject, transactionSession.Project);
            var saved = redone && transactionSession.Save().Succeeded;
            var persisted = false;
            if (saved && transactionSession.Session is { } workspace && targetId is { } persistedAreaId &&
                expectedAreaJson is not null)
            {
                reopened = new StudioSessionController();
                if (reopened.Open(workspace.RootDirectory, persistedAreaId.ToString()).Succeeded)
                {
                    var restored = reopened.Project?.Areas.SingleOrDefault(item => item.Id == persistedAreaId);
                    persisted = restored is not null && string.Equals(
                        expectedAreaJson,
                        AreaJsonSerializer.Serialize(restored),
                        StringComparison.Ordinal);
                }
            }
            var sourceUntouched = SourceMatchesSnapshot(sourceSnapshot);
            reopened?.Close();
            reopened = null;
            Close();
            return dryRun && applied && undone && redone && saved && persisted && sourceUntouched &&
                   EvidenceResourcesReleased && SourceSessionRestored;
        }
        catch (Exception exception)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2A_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}");
            return false;
        }
        finally
        {
            reopened?.Close();
            if (Visible || _evidenceSession is not null || _evidenceTempRoot is not null)
            {
                Close();
            }
        }
    }

    private bool TryOpenIsolatedEvidenceSession()
    {
        var snapshot = CaptureSourceSnapshot();
        var evidenceProject = _sourceSession.CreateUx2AEvidenceProject();
        if (snapshot is null || evidenceProject is null)
        {
            return false;
        }
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "openrpg-forge-studio-ux2a-evidence-" + Guid.NewGuid().ToString("N"));
        var evidenceSession = new StudioSessionController();
        try
        {
            var created = ProjectSession.Create(tempRoot, evidenceProject);
            var opened = created.Succeeded &&
                         StudioSessionController.WriteUx2AEvidenceScript(tempRoot) &&
                         evidenceSession.Open(tempRoot).Succeeded;
            if (!opened)
            {
                evidenceSession.Close();
                _lastEvidenceResourcesReleased =
                    StudioSessionController.DeleteUx2ATemporaryWorkspace(tempRoot);
                _evidenceTempRoot = _lastEvidenceResourcesReleased ? null : tempRoot;
                _session = _sourceSession;
                _lastSourceRestored = true;
                return false;
            }
            _evidenceSourceSnapshot = snapshot;
            _evidenceSession = evidenceSession;
            _evidenceTempRoot = tempRoot;
            _session = evidenceSession;
            _lastEvidenceResourcesReleased = false;
            _lastSourceRestored = false;
            _lastEvidencePreviewDryRun = false;
            OpenBoundSession();
            var ready = _choices.Any(item => item.IsEvidence) && SourceMatchesSnapshot(snapshot);
            if (!ready)
            {
                Close();
            }
            return ready;
        }
        catch (Exception exception)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2A_EVIDENCE_FAILED type={exception.GetType().Name} message={exception.Message}");
            evidenceSession.Close();
            var released = StudioSessionController.DeleteUx2ATemporaryWorkspace(tempRoot);
            _evidenceSourceSnapshot = null;
            _evidenceSession = null;
            _evidenceTempRoot = released ? null : tempRoot;
            _session = _sourceSession;
            _lastEvidenceResourcesReleased = released;
            _lastSourceRestored = true;
            return false;
        }
    }

    private StudioUx2AEvidenceSourceSnapshot? CaptureSourceSnapshot()
    {
        var project = _sourceSession.Project;
        var history = _sourceSession.History;
        if (project is null || history is null)
        {
            return null;
        }
        return new StudioUx2AEvidenceSourceSnapshot(
            project,
            history,
            history.State.Revision,
            history.Journal.ToArray(),
            _sourceSession.IsDirty,
            history.CanUndo,
            history.CanRedo,
            _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(),
            _sourceSession.SelectedEntityId,
            _sourceSession.CaptureAreaPolicyApprovalUx2A());
    }

    private bool SourceMatchesSnapshot(StudioUx2AEvidenceSourceSnapshot snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) &&
               history is not null && history.State.Revision == snapshot.Revision &&
               history.Journal.SequenceEqual(snapshot.Journal) &&
               history.CanUndo == snapshot.CanUndo && history.CanRedo == snapshot.CanRedo &&
               _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureAreaPolicyApprovalUx2A() == snapshot.Approval;
    }

    private bool ReleaseIsolatedEvidenceSession()
    {
        var evidenceSession = _evidenceSession;
        var tempRoot = _evidenceTempRoot;
        _evidenceSession = null;
        _evidenceTempRoot = null;
        _evidenceSourceSnapshot = null;
        _session = _sourceSession;
        var released = true;
        try
        {
            evidenceSession?.Close();
        }
        catch (Exception exception)
        {
            released = false;
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2A_EVIDENCE_RELEASE_FAILED type={exception.GetType().Name} message={exception.Message}");
        }
        var rootReleased = StudioSessionController.DeleteUx2ATemporaryWorkspace(tempRoot);
        if (!rootReleased)
        {
            _evidenceTempRoot = tempRoot;
        }
        return released && rootReleased;
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _ExitTree()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null)
        {
            Close();
        }
    }

    private void SelectArea(long index)
    {
        if (_updating)
        {
            return;
        }
        var choiceIndex = (int)index - 1;
        if (choiceIndex < 0 || choiceIndex >= _choices.Count)
        {
            ClearSelection();
            return;
        }
        LoadChoice(choiceIndex);
    }

    private void ClearSelection()
    {
        _draft = null;
        _lastPreview = null;
        _applied = false;
        _canonicalPreviewReady = false;
        _session.CancelAreaPoliciesUx2A();
        ResetControls();
        _selection.Text = "Choisissez explicitement une zone ; aucune fiche n’est sélectionnée automatiquement.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
        _summary.Text = "Sélectionnez une zone puis lancez l’aperçu sec · aucun changement appliqué.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        SetStatus("Prêt · sélection explicite requise", error: false);
        RenderDiff(null);
        RenderDiagnostics([]);
        SetEditingEnabled(false);
        UpdateTransactionButtons();
    }

    private void LoadChoice(int choiceIndex)
    {
        var choice = _choices[choiceIndex];
        _session.CancelAreaPoliciesUx2A();
        _draft = _session.CreateAreaPolicyDraftUx2A(choice.AreaId);
        _lastPreview = null;
        _applied = false;
        _canonicalPreviewReady = false;
        if (_draft is null)
        {
            ResetControls();
            _selection.Text = "La zone choisie a changé. Rechargez la page Monde vivant.";
            _selection.AddThemeColorOverride("font_color", StudioStyle.Danger);
            _summary.Text = "Aucune politique n’a été synthétisée et aucune mutation n’est disponible.";
            _summary.AddThemeColorOverride("font_color", StudioStyle.Danger);
            RenderDiff(null);
            RenderDiagnostics([]);
            SetEditingEnabled(false);
            UpdateTransactionButtons();
            SetStatus("Zone indisponible · aucune mutation", error: true);
            return;
        }

        Populate(_draft);
        _selection.Text = choice.Label;
        _selection.AddThemeColorOverride("font_color", StudioStyle.Accent);
        _summary.Text = "Collections réelles chargées · aucun ajout automatique · aperçu sec obligatoire.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        RenderDiff(null);
        RenderDiagnostics([]);
        SetEditingEnabled(true);
        UpdateTransactionButtons();
        SetStatus("Brouillon prêt · aperçu sec requis", error: false);
    }

    private void Populate(StudioUx2ADraft draft)
    {
        _updating = true;
        _heartbeats.SetValues(draft.Heartbeats, _session.FindForgeScripts());
        _perceptionRuleset.Text = draft.PerceptionRulesetId;
        _perception.SetValues(draft.PerceptionModifiers);
        _restMode.Select((int)draft.RestMode);
        _restReason.Text = draft.RestDenialReason;
        _restCondition.Text = draft.RestCondition;
        _restCooldown.Value = draft.RestCooldownSeconds;
        _restSecureRadius.Value = draft.RestRequiredSecureRadius;
        _restVolumes.SetValues(draft.RestVolumes);
        _projectRestState.Text = draft.ProjectRestEnabled
            ? "Cycle de repos du projet actif · cette fiche modifie seulement la règle de cette zone."
            : "Cycle de repos du projet dormant · la règle de zone est conservée sans activation implicite.";
        _projectRestState.AddThemeColorOverride(
            "font_color",
            draft.ProjectRestEnabled ? StudioStyle.Accent : StudioStyle.Warning);
        _updating = false;
    }

    private StudioUx2ADraft? ReadDraft()
    {
        if (_draft is null)
        {
            return null;
        }
        return _draft with
        {
            Heartbeats = _heartbeats.Values,
            PerceptionRulesetId = _perceptionRuleset.Text,
            PerceptionModifiers = _perception.Values,
            RestMode = (RestPermissionMode)_restMode.Selected,
            RestDenialReason = _restReason.Text,
            RestCondition = _restCondition.Text,
            RestCooldownSeconds = (int)_restCooldown.Value,
            RestRequiredSecureRadius = (float)_restSecureRadius.Value,
            RestVolumes = _restVolumes.Values,
        };
    }

    private bool ConfigureEvidenceDraft()
    {
        var current = ReadDraft();
        if (current is null || current.Heartbeats.Count < 2 || current.PerceptionModifiers.Count < 3 ||
            current.RestVolumes.Count < 2)
        {
            return false;
        }
        var configured = current with
        {
            Heartbeats = current.Heartbeats.Select((item, index) => index == 0
                ? item with { IntervalMilliseconds = 1_850, BudgetMilliseconds = 30 }
                : item).ToArray(),
            PerceptionRulesetId = "forge.weather-aware",
            PerceptionModifiers = current.PerceptionModifiers.Select(item =>
                string.Equals(item.SenseId, "vision", StringComparison.Ordinal) &&
                string.Equals(item.ConditionTag, "fog", StringComparison.Ordinal)
                    ? item with { Multiplier = .4, AdditiveModifier = -.2 }
                    : item).ToArray(),
            RestMode = RestPermissionMode.Forbidden,
            RestDenialReason = "La clairière reste dangereuse pendant l’alerte.",
            RestCondition = string.Empty,
            RestCooldownSeconds = 75,
        };
        Populate(configured);
        InvalidatePreview();
        return true;
    }

    private void Preview()
    {
        var draft = ReadDraft();
        if (draft is null)
        {
            return;
        }
        var result = _session.PreviewAreaPoliciesUx2A(draft);
        _lastPreview = result;
        _canonicalPreviewReady = result.Succeeded;
        _apply.Disabled = !result.CanApply || !result.Diff.HasChanges || !_canonicalPreviewReady;
        RenderDiff(result);
        RenderDiagnostics(result.Groups);
        _summary.Text = string.IsNullOrWhiteSpace(result.Summary)
            ? result.Message
            : result.Message + "\n" + result.Summary;
        _summary.AddThemeColorOverride(
            "font_color",
            result.Succeeded ? StudioStyle.Success : StudioStyle.Danger);
        if (result.Succeeded && !result.Diff.HasChanges)
        {
            SetStatus("Aperçu sec conforme · aucun changement à appliquer", error: false);
        }
        else
        {
            SetStatus(result.Message, error: !result.Succeeded);
        }
        UpdateTransactionButtons(keepApplyState: true);
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedAreaPoliciesUx2A();
        SetStatus(result.Message, error: !result.Succeeded);
        if (!result.Succeeded)
        {
            _apply.Disabled = true;
            _canonicalPreviewReady = false;
            return;
        }
        _applied = true;
        _apply.Disabled = true;
        _preview.Disabled = true;
        SetEditingEnabled(false);
        UpdateTransactionButtons();
        _summary.Text =
            "Appliqué · seules les politiques de la zone choisie ont changé. Module, autres zones, cinématiques, " +
            "combat, expérience, navigation et contenu spatial restent préservés.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Success);
        AreaPoliciesChanged?.Invoke();
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(result.Message, error: !result.Succeeded);
        if (!result.Succeeded)
        {
            return;
        }
        _applied = false;
        UpdateTransactionButtons();
        _summary.Text = "Annulé · la révision précédente de la zone est restaurée.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Accent);
        AreaPoliciesChanged?.Invoke();
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(result.Message, error: !result.Succeeded);
        if (!result.Succeeded)
        {
            return;
        }
        _applied = true;
        UpdateTransactionButtons();
        _summary.Text = "Rétabli · les politiques approuvées sont de nouveau présentes dans la zone.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Success);
        AreaPoliciesChanged?.Invoke();
    }

    private void Save()
    {
        var result = _session.Save();
        SetStatus(result.Message, error: !result.Succeeded);
        if (result.Succeeded)
        {
            _save.Disabled = true;
            _summary.Text += "\nProjet enregistré · la réouverture relira les mêmes politiques canoniques.";
        }
    }

    private void ConnectInvalidation()
    {
        _heartbeats.Changed += InvalidatePreview;
        _perceptionRuleset.TextChanged += _ => InvalidatePreview();
        _perception.Changed += InvalidatePreview;
        _restMode.ItemSelected += _ => InvalidatePreview();
        _restReason.TextChanged += _ => InvalidatePreview();
        _restCondition.TextChanged += _ => InvalidatePreview();
        _restCooldown.ValueChanged += _ => InvalidatePreview();
        _restSecureRadius.ValueChanged += _ => InvalidatePreview();
        _restVolumes.Changed += InvalidatePreview;
    }

    private void InvalidatePreview()
    {
        if (_updating || _draft is null)
        {
            return;
        }
        _session.CancelAreaPoliciesUx2A();
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _applied = false;
        _apply.Disabled = true;
        _save.Disabled = true;
        _summary.Text = "Brouillon modifié · relancez l’aperçu sec avant application.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Warning);
        SetStatus("Aperçu précédent effacé · validation à relancer", error: false);
        RenderDiff(null);
        RenderDiagnostics([]);
        UpdateTransactionButtons(keepApplyState: true);
    }

    private void ResetControls()
    {
        _updating = true;
        _heartbeats.SetValues([], []);
        _perceptionRuleset.Text = string.Empty;
        _perception.SetValues([]);
        _restMode.Select(0);
        _restReason.Text = string.Empty;
        _restCondition.Text = string.Empty;
        _restCooldown.Value = 0;
        _restSecureRadius.Value = 0;
        _restVolumes.SetValues([]);
        _projectRestState.Text = "État du cycle de repos du projet : non évalué";
        _projectRestState.AddThemeColorOverride("font_color", StudioStyle.Warning);
        _updating = false;
        _lastPreview = null;
        RenderDiff(null);
        RenderDiagnostics([]);
    }

    private void SetEditingEnabled(bool enabled)
    {
        _heartbeats.SetEditingEnabled(enabled);
        _perceptionRuleset.Editable = enabled;
        _perception.SetEditingEnabled(enabled);
        _restMode.Disabled = !enabled;
        _restReason.Editable = enabled;
        _restCondition.Editable = enabled;
        _restCooldown.Editable = enabled;
        _restSecureRadius.Editable = enabled;
        _restVolumes.SetEditingEnabled(enabled);
        _preview.Disabled = !enabled;
    }

    private void UpdateTransactionButtons(bool keepApplyState = false)
    {
        var history = _session.History;
        if (!keepApplyState)
        {
            _apply.Disabled = true;
        }
        _undo.Disabled = !_applied || history?.CanUndo != true;
        _redo.Disabled = _applied || history?.CanRedo != true;
        _save.Disabled = !_applied || !_session.IsDirty;
    }

    private void RenderDiff(StudioUx2APreview? preview)
    {
        foreach (var child in _diff.GetChildren())
        {
            child.QueueFree();
        }
        if (preview is null)
        {
            _diff.AddChild(StudioStyle.Label(
                "Aucun aperçu frais · les changements ne sont pas encore approuvés.",
                10,
                StudioStyle.Muted,
                wrap: true));
            return;
        }
        if (!preview.Diff.HasChanges)
        {
            _diff.AddChild(StudioStyle.Label(
                "Aucun changement sémantique.",
                10,
                StudioStyle.Muted,
                wrap: true));
            return;
        }
        AddDiffSection("Heartbeat", preview.Diff.Heartbeats);
        AddDiffSection("Perception", preview.Diff.Perception);
        AddDiffSection("Repos", preview.Diff.Rest);
    }

    private void AddDiffSection(string title, IReadOnlyList<string> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }
        _diff.AddChild(StudioStyle.Label(title, 10, StudioStyle.Accent));
        foreach (var entry in entries)
        {
            _diff.AddChild(StudioStyle.Label("• " + entry, 10, StudioStyle.Text, wrap: true));
        }
    }

    private void RenderDiagnostics(IReadOnlyList<StudioUx2ADiagnosticGroup> groups)
    {
        foreach (var child in _diagnostics.GetChildren())
        {
            child.QueueFree();
        }
        if (groups.Count == 0)
        {
            _diagnostics.AddChild(StudioStyle.Label(
                "Les diagnostics apparaîtront ici après l’aperçu sec.",
                10,
                StudioStyle.Muted,
                wrap: true));
            return;
        }
        foreach (var group in groups)
        {
            var color = group.Diagnostics.Count == 0 ? StudioStyle.Success : StudioStyle.Danger;
            _diagnostics.AddChild(StudioStyle.Label(
                $"{group.Title} · {group.State}",
                10,
                color,
                wrap: true));
            foreach (var diagnostic in group.Diagnostics)
            {
                _diagnostics.AddChild(StudioStyle.Label(
                    "• " + diagnostic.Message,
                    10,
                    diagnostic.Blocking ? StudioStyle.Danger : StudioStyle.Warning,
                    wrap: true));
            }
        }
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(message, error);
    }

    private void UpdateDialogBounds()
    {
        var viewport = GetViewportRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0)
        {
            return;
        }
        var scale = viewport.X >= 3_200 ? 1.72f : viewport.X >= 2_300 ? 1.24f : 1f;
        var width = Math.Clamp((viewport.X - 52f) / scale, 960f, 1_520f);
        var height = Math.Clamp((viewport.Y - 42f) / scale, 720f, 1_020f);
        var renderedWidth = width * scale;
        var renderedHeight = height * scale;
        _dialog.Scale = Vector2.One * scale;
        _dialog.CustomMaximumSize = new Vector2(width, height);
        _dialog.Position = new Vector2(
            (viewport.X - renderedWidth) / 2f,
            Math.Max(20f, (viewport.Y - renderedHeight) * .45f));
        _dialog.Size = new Vector2(width, height);
    }

    private static IEnumerable<Label> DescendantLabels(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Label label)
            {
                yield return label;
            }
            foreach (var descendant in DescendantLabels(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool ContainsUuid(string text) => System.Text.RegularExpressions.Regex.IsMatch(
        text,
        @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase |
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);
}
