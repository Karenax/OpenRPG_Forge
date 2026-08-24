using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioPlaceableVerbEditor : VBoxContainer
{
    private sealed class VerbRow(
        StudioPlaceableVerbDraft source,
        PanelContainer card,
        Label title,
        LineEdit label,
        LineEdit eventName,
        LineEdit condition,
        Button remove)
    {
        internal StudioPlaceableVerbDraft Source { get; } = source;
        internal PanelContainer Card { get; } = card;
        internal Label Title { get; } = title;
        internal LineEdit Label { get; } = label;
        internal LineEdit EventName { get; } = eventName;
        internal LineEdit Condition { get; } = condition;
        internal Button Remove { get; } = remove;
    }

    private readonly VBoxContainer _rowsHost;
    private readonly Label _empty;
    private readonly Button _add;
    private readonly List<VerbRow> _rows = [];
    private bool _loading;

    internal event Action? Changed;

    internal int VerbCount => _rows.Count;

    internal string HumanText => string.Join(' ', _rows.SelectMany(row => new[]
    {
        row.Label.Text,
        row.EventName.Text,
        row.Condition.Text,
    }));

    internal StudioPlaceableVerbEditor()
    {
        AddThemeConstantOverride("separation", 6);
        _empty = StudioStyle.Label(
            "État vide · ajoutez au moins une action proposée au joueur.",
            10,
            StudioStyle.Warning,
            wrap: true);
        AddChild(_empty);
        _rowsHost = new VBoxContainer();
        _rowsHost.AddThemeConstantOverride("separation", 6);
        AddChild(_rowsHost);
        _add = StudioStyle.Button("Ajouter une action", compact: true);
        _add.TooltipText = "Ajouter un verbe utilisable sans exposer son identifiant interne";
        _add.Pressed += () =>
        {
            AddRow(new(Guid.NewGuid(), "Nouvelle action", "placeable-used", "true", null, null));
            Changed?.Invoke();
        };
        AddChild(_add);
    }

    internal void Load(IReadOnlyList<StudioPlaceableVerbDraft> verbs)
    {
        _loading = true;
        foreach (var row in _rows)
        {
            _rowsHost.RemoveChild(row.Card);
            row.Card.QueueFree();
        }
        _rows.Clear();
        foreach (var verb in verbs)
        {
            AddRow(verb);
        }
        UpdateRows();
        _loading = false;
    }

    internal IReadOnlyList<StudioPlaceableVerbDraft> Capture() => _rows.Select(row => row.Source with
    {
        Label = row.Label.Text,
        EventName = row.EventName.Text,
        Condition = row.Condition.Text,
    }).ToArray();

    internal void FocusFirstAction() => _rows.FirstOrDefault()?.Label.GrabFocus();

    private void AddRow(StudioPlaceableVerbDraft source)
    {
        var card = StudioStyle.Panel("PlaceableVerb", StudioStyle.SurfaceRaised, 6);
        _rowsHost.AddChild(card);
        var margin = StudioStyle.Margin(10, 8, 10, 8);
        card.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 4);
        margin.AddChild(column);
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 6);
        column.AddChild(header);
        var title = StudioStyle.Label(string.Empty, 10, StudioStyle.Accent);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);
        var remove = StudioStyle.Button("Retirer", compact: true);
        remove.TooltipText = "Retirer cette action du brouillon";
        header.AddChild(remove);
        var label = AddLine(column, "Action proposée", "Examiner");
        var eventName = AddLine(column, "Événement", "placeable-inspected");
        var condition = AddLine(column, "Condition", "player.can.interact");
        label.Text = source.Label;
        eventName.Text = source.EventName;
        condition.Text = source.Condition;
        var row = new VerbRow(source, card, title, label, eventName, condition, remove);
        _rows.Add(row);
        foreach (var line in new[] { label, eventName, condition })
        {
            line.TextChanged += _ =>
            {
                if (!_loading)
                {
                    Changed?.Invoke();
                }
            };
        }
        remove.Pressed += () =>
        {
            _rows.Remove(row);
            _rowsHost.RemoveChild(card);
            card.QueueFree();
            UpdateRows();
            Changed?.Invoke();
        };
        UpdateRows();
    }

    private void UpdateRows()
    {
        _empty.Visible = _rows.Count == 0;
        for (var index = 0; index < _rows.Count; index++)
        {
            _rows[index].Title.Text = $"ACTION {index + 1}";
        }
    }

    private static LineEdit AddLine(VBoxContainer parent, string label, string placeholder)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var line = new LineEdit { PlaceholderText = placeholder, ClearButtonEnabled = false };
        StudioStyle.ApplyLineEdit(line);
        parent.AddChild(line);
        return line;
    }
}
