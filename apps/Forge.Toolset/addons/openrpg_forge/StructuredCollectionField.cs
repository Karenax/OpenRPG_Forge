#if TOOLS
using Godot;

namespace Forge.Toolset.Editor;

/// <summary>
/// Presents legacy delimiter-backed authoring rows as a normal list and typed inspector.
/// The backing control remains the compatibility boundary for existing parsers but is never
/// exposed to creators. Extra legacy columns are retained and become editable generic fields.
/// </summary>
internal sealed partial class StructuredCollectionField : VBoxContainer
{
    internal sealed record Column(string Label, string Placeholder = "", string DefaultValue = "",
        IReadOnlyList<string>? Options = null);

    private readonly TextEdit _backing;
    private readonly string _itemName;
    private readonly IReadOnlyList<Column> _declaredColumns;
    private readonly List<string[]> _rows = [];
    private readonly List<Control> _fieldControls = [];
    private readonly List<Button> _editButtons = [];
    private readonly ItemList _list = new();
    private readonly GridContainer _form = new() { Columns = 2 };
    private readonly Label _summary = new();
    private readonly VBoxContainer _editor = new();
    private readonly Button _toggle = new()
    {
        Text = "Modifier…",
        ToggleMode = true,
        CustomMinimumSize = new Vector2(112, 34),
    };
    private bool _syncing;
    private int _selectedIndex = -1;
    private bool _editable = true;

    private StructuredCollectionField(TextEdit backing, string itemName, IReadOnlyList<Column> columns)
    {
        _backing = backing;
        _itemName = itemName;
        _declaredColumns = columns;
        _editable = backing.Editable;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 8);
        Build();
        Reload();
        _backing.TextChanged += HandleBackingChanged;
    }

    internal static StructuredCollectionField Attach(TextEdit backing, string itemName, params Column[] columns)
    {
        ArgumentNullException.ThrowIfNull(backing);
        if (backing.GetParent() is not Container parent)
            throw new InvalidOperationException("Le champ structuré doit appartenir à un conteneur.");
        var index = backing.GetIndex();
        backing.Visible = false;
        var field = new StructuredCollectionField(backing, itemName, columns);
        parent.AddChild(field);
        parent.MoveChild(field, index + 1);
        return field;
    }

    internal int Count => _rows.Count;

    internal void SetEditable(bool editable)
    {
        _editable = editable;
        _backing.Editable = editable;
        foreach (var button in _editButtons) button.Disabled = !editable;
        foreach (var control in _fieldControls)
        {
            if (control is LineEdit edit) edit.Editable = editable;
            else if (control is OptionButton option) option.Disabled = !editable;
        }
    }

    private void Build()
    {
        var header = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 34),
        };
        header.AddThemeConstantOverride("separation", 8);
        AddChild(header);
        _toggle.Toggled += SetExpanded;
        _toggle.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        _toggle.TooltipText = $"Afficher ou replier les détails de {_itemName}";
        header.AddChild(_toggle);
        _summary.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _summary.AddThemeColorOverride("font_color", new Color("c7b9dc"));
        header.AddChild(_summary);

        _editor.Visible = false;
        _editor.AddThemeConstantOverride("separation", 8);
        AddChild(_editor);
        var split = new HSplitContainer { CustomMinimumSize = new Vector2(0, 210), SplitOffsets = [310] };
        _editor.AddChild(split);
        var left = new VBoxContainer();
        left.AddThemeConstantOverride("separation", 6);
        split.AddChild(left);
        _list.SizeFlagsVertical = SizeFlags.ExpandFill;
        _list.ItemSelected += SelectRow;
        left.AddChild(_list);
        var toolbar = new HBoxContainer();
        toolbar.AddThemeConstantOverride("separation", 4);
        left.AddChild(toolbar);
        AddButton(toolbar, "+", AddRow, $"Ajouter {_itemName}");
        AddButton(toolbar, "⧉", DuplicateRow, $"Dupliquer {_itemName}");
        AddButton(toolbar, "−", RemoveRow, $"Supprimer {_itemName}");
        AddButton(toolbar, "↑", () => MoveRow(-1), "Monter");
        AddButton(toolbar, "↓", () => MoveRow(1), "Descendre");

        var scroll = new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        split.AddChild(scroll);
        _form.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _form.AddThemeConstantOverride("h_separation", 10);
        _form.AddThemeConstantOverride("v_separation", 7);
        scroll.AddChild(_form);
    }

    private void AddButton(Container parent, string text, Action pressed, string tooltip)
    {
        var button = new Button
        {
            Text = text,
            TooltipText = tooltip,
            CustomMinimumSize = new Vector2(38, 32),
            Disabled = !_editable,
        };
        button.Pressed += pressed;
        _editButtons.Add(button);
        parent.AddChild(button);
    }

    private void HandleBackingChanged()
    {
        if (!_syncing) Reload();
    }

    private void Reload()
    {
        var previous = Math.Clamp(_selectedIndex, -1, Math.Max(-1, _rows.Count - 1));
        _rows.Clear();
        foreach (var line in _backing.Text.Split(['\r', '\n'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            _rows.Add(line.Split('|', StringSplitOptions.TrimEntries));
        }
        RefreshList();
        if (_rows.Count == 0) SetExpanded(true);
        if (_rows.Count > 0) SelectRow(Math.Clamp(previous, 0, _rows.Count - 1));
        else SelectRow(-1);
    }

    private void RefreshList()
    {
        _list.Clear();
        for (var index = 0; index < _rows.Count; index++)
        {
            var name = _rows[index].FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            _list.AddItem(string.IsNullOrWhiteSpace(name) ? $"{_itemName} {index + 1}" : name);
        }
        _summary.Text = _rows.Count == 0
            ? $"Aucun {_itemName} · utilisez + pour en créer un"
            : $"{_rows.Count} {_itemName}{(_rows.Count > 1 ? "s" : string.Empty)} · cliquez Modifier pour détailler";
    }

    private void SetExpanded(bool expanded)
    {
        _toggle.SetPressedNoSignal(expanded);
        _editor.Visible = expanded;
        _toggle.Text = expanded ? "Replier" : "Modifier…";
    }

    private void SelectRow(long index) => SelectRow((int)index);

    private void SelectRow(int index)
    {
        _selectedIndex = index >= 0 && index < _rows.Count ? index : -1;
        if (_selectedIndex >= 0) _list.Select(_selectedIndex);
        RebuildForm();
    }

    private void RebuildForm()
    {
        foreach (var child in _form.GetChildren()) child.QueueFree();
        _fieldControls.Clear();
        if (_selectedIndex < 0) return;
        var row = _rows[_selectedIndex];
        var count = Math.Max(_declaredColumns.Count, row.Length);
        if (row.Length < count) Array.Resize(ref row, count);
        _rows[_selectedIndex] = row;
        for (var index = 0; index < count; index++)
        {
            var column = index < _declaredColumns.Count
                ? _declaredColumns[index]
                : new Column($"Champ supplémentaire {index + 1}");
            _form.AddChild(new Label { Text = column.Label, TooltipText = column.Placeholder });
            Control input;
            if (column.Options is { Count: > 0 })
            {
                var options = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                options.Disabled = !_editable;
                foreach (var option in column.Options) options.AddItem(option);
                var selected = column.Options.ToList().FindIndex(value =>
                    string.Equals(value, row[index], StringComparison.OrdinalIgnoreCase));
                options.Selected = Math.Max(0, selected);
                options.ItemSelected += _ => UpdateSelectedRow();
                input = options;
            }
            else
            {
                var edit = new LineEdit
                {
                    Text = row[index] ?? string.Empty,
                    PlaceholderText = column.Placeholder,
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    Editable = _editable,
                };
                edit.TextChanged += _ => UpdateSelectedRow();
                input = edit;
            }
            _fieldControls.Add(input);
            _form.AddChild(input);
        }
    }

    private void UpdateSelectedRow()
    {
        if (_syncing || !_editable || _selectedIndex < 0) return;
        var row = _rows[_selectedIndex];
        for (var index = 0; index < _fieldControls.Count; index++)
        {
            row[index] = _fieldControls[index] switch
            {
                LineEdit edit => edit.Text.Trim(),
                OptionButton option when option.ItemCount > 0 => option.GetItemText(option.Selected),
                _ => row[index],
            };
        }
        Commit();
        RefreshList();
        _list.Select(_selectedIndex);
    }

    private void AddRow()
    {
        if (!_editable) return;
        var row = _declaredColumns.Select(column => column.DefaultValue).ToArray();
        EnsureFreshIdentity(row);
        _rows.Add(row);
        Commit();
        RefreshList();
        SelectRow(_rows.Count - 1);
    }

    private void DuplicateRow()
    {
        if (!_editable || _selectedIndex < 0) return;
        var copy = _rows[_selectedIndex].ToArray();
        EnsureFreshIdentity(copy);
        _rows.Insert(_selectedIndex + 1, copy);
        Commit();
        RefreshList();
        SelectRow(_selectedIndex + 1);
    }

    private void RemoveRow()
    {
        if (!_editable || _selectedIndex < 0) return;
        var next = Math.Min(_selectedIndex, _rows.Count - 2);
        _rows.RemoveAt(_selectedIndex);
        Commit();
        RefreshList();
        SelectRow(next);
    }

    private void MoveRow(int offset)
    {
        if (!_editable || _selectedIndex < 0) return;
        var target = _selectedIndex + offset;
        if (target < 0 || target >= _rows.Count) return;
        (_rows[target], _rows[_selectedIndex]) = (_rows[_selectedIndex], _rows[target]);
        Commit();
        RefreshList();
        SelectRow(target);
    }

    private void Commit()
    {
        _syncing = true;
        try
        {
            _backing.Text = string.Join('\n', _rows.Select(row => string.Join('|', row)));
        }
        finally
        {
            _syncing = false;
        }
    }

    private void EnsureFreshIdentity(string[] row)
    {
        if (row.Length == 0 || _declaredColumns.Count == 0 ||
            !_declaredColumns[0].Label.Equals("Identifiant", StringComparison.OrdinalIgnoreCase)) return;
        if (!Guid.TryParse(row[0], out _)) return;
        row[0] = Guid.NewGuid().ToString("D");
    }
}
#endif
