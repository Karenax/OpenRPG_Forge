using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioPlaceableLocalizationEditor : VBoxContainer
{
    private sealed class LocalizationRow(
        PanelContainer card,
        Label title,
        LineEdit locale,
        LineEdit name,
        LineEdit prompt,
        Button remove)
    {
        internal PanelContainer Card { get; } = card;
        internal Label Title { get; } = title;
        internal LineEdit Locale { get; } = locale;
        internal LineEdit Name { get; } = name;
        internal LineEdit Prompt { get; } = prompt;
        internal Button Remove { get; } = remove;
    }

    private readonly VBoxContainer _rowsHost;
    private readonly Label _empty;
    private readonly Button _add;
    private readonly List<LocalizationRow> _rows = [];
    private bool _loading;

    internal event Action? Changed;

    internal int LocalizationCount => _rows.Count;

    internal string HumanText => string.Join(' ', _rows.SelectMany(row => new[]
    {
        row.Locale.Text,
        row.Name.Text,
        row.Prompt.Text,
    }));

    internal StudioPlaceableLocalizationEditor()
    {
        AddThemeConstantOverride("separation", 6);
        _empty = StudioStyle.Label(
            "Aucune traduction supplémentaire · le français principal reste disponible ci-dessus.",
            10,
            StudioStyle.Muted,
            wrap: true);
        AddChild(_empty);
        _rowsHost = new VBoxContainer();
        _rowsHost.AddThemeConstantOverride("separation", 6);
        AddChild(_rowsHost);
        _add = StudioStyle.Button("Ajouter une langue", compact: true);
        _add.TooltipText = "Ajouter un nom et une invitation localisés sans exposer d’identifiant interne";
        _add.Pressed += () =>
        {
            AddRow(new("en-US", "English name", "Examine"));
            Changed?.Invoke();
        };
        AddChild(_add);
    }

    internal void Load(IReadOnlyList<StudioPlaceableLocalizedTextDraft> texts)
    {
        _loading = true;
        foreach (var row in _rows)
        {
            _rowsHost.RemoveChild(row.Card);
            row.Card.QueueFree();
        }
        _rows.Clear();
        foreach (var text in texts)
        {
            AddRow(text);
        }
        UpdateRows();
        _loading = false;
    }

    internal IReadOnlyList<StudioPlaceableLocalizedTextDraft> Capture() => _rows
        .Select(row => new StudioPlaceableLocalizedTextDraft(row.Locale.Text, row.Name.Text, row.Prompt.Text))
        .ToArray();

    internal void SetEvidenceValues(IReadOnlyList<StudioPlaceableLocalizedTextDraft> texts) => Load(texts);

    internal void FocusFirstLocalization()
    {
        Control target = _rows.FirstOrDefault() is { } row ? row.Locale : _add;
        target.GrabFocus();
    }

    private void AddRow(StudioPlaceableLocalizedTextDraft source)
    {
        var card = StudioStyle.Panel("PlaceableLocalization", StudioStyle.SurfaceRaised, 6);
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
        remove.TooltipText = "Retirer cette traduction du brouillon";
        header.AddChild(remove);
        var locale = AddLine(column, "Langue", "en-US");
        var name = AddLine(column, "Nom affiché", "Watcher statue");
        var prompt = AddLine(column, "Invitation", "Examine the statue");
        locale.Text = source.Locale;
        name.Text = source.Name;
        prompt.Text = source.Prompt;
        var row = new LocalizationRow(card, title, locale, name, prompt, remove);
        _rows.Add(row);
        foreach (var line in new[] { locale, name, prompt })
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
            _rows[index].Title.Text = $"TRADUCTION {index + 1}";
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
