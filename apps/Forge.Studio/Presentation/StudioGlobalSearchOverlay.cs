using Forge.Application.Creator;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed record StudioGlobalSearchEntry(
    string Key,
    string Group,
    string Title,
    string Subtitle,
    int Score,
    string? PageId = null,
    string? ActionId = null,
    CreatorSearchDocument? Document = null);

internal sealed partial class StudioGlobalSearchOverlay : Control
{
    private readonly PanelContainer _dialog;
    private readonly LineEdit _query;
    private readonly ItemList _results;
    private readonly Label _summary;
    private readonly Label _detailTitle;
    private readonly Label _detailPath;
    private readonly Label _detailBody;
    private readonly VBoxContainer _referenceList;
    private readonly Button _rename;
    private readonly List<StudioGlobalSearchEntry> _visibleEntries = [];

    internal event Action<string>? QueryChanged;

    internal event Action<int>? EntrySelected;

    internal event Action<int>? EntryActivated;

    internal event Action<int>? RenameRequested;

    internal event Action? CloseRequested;

    internal bool IsOpen => Visible;

    internal int ResultCount => _visibleEntries.Count;

    internal StudioGlobalSearchOverlay()
    {
        Name = "StudioGlobalSearchOverlay";
        Visible = false;
        ZIndex = 500;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var backdrop = new ColorRect
        {
            Color = new Color(0, 0, 0, .66f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        backdrop.GuiInput += BackdropInput;
        AddChild(backdrop);

        _dialog = StudioStyle.Panel("GlobalSearchDialog", StudioStyle.Chrome, 10, 2);
        _dialog.ZIndex = 1;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(16, 14, 16, 14);
        _dialog.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 9);
        margin.AddChild(root);

        var heading = new HBoxContainer();
        heading.AddChild(StudioStyle.Label("RECHERCHE GLOBALE", 12, StudioStyle.Accent));
        heading.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        heading.AddChild(StudioStyle.Label("Ctrl+K · Échap pour fermer", 10, StudioStyle.Muted));
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        root.AddChild(heading);

        _query = new LineEdit
        {
            PlaceholderText = "Page, ressource, objet ou action…",
            ClearButtonEnabled = true,
        };
        StudioStyle.ApplyLineEdit(_query);
        _query.TextChanged += value => QueryChanged?.Invoke(value);
        _query.TextSubmitted += _ => ActivateCurrent();
        _query.GuiInput += QueryInput;
        root.AddChild(_query);

        _summary = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted);
        root.AddChild(_summary);

        var body = new HSplitContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SplitOffsets = [500] };
        root.AddChild(body);
        _results = new ItemList
        {
            AllowReselect = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(440, 300),
        };
        _results.ItemSelected += index => EntrySelected?.Invoke((int)index);
        _results.ItemActivated += index => EntryActivated?.Invoke((int)index);
        body.AddChild(_results);

        var detailsPanel = StudioStyle.Panel("GlobalSearchDetails", StudioStyle.Surface, 8);
        detailsPanel.CustomMinimumSize = new Vector2(300, 0);
        body.AddChild(detailsPanel);
        var detailsMargin = StudioStyle.Margin(12, 12, 12, 12);
        detailsPanel.AddChild(detailsMargin);
        var details = new VBoxContainer();
        details.AddThemeConstantOverride("separation", 7);
        detailsMargin.AddChild(details);
        details.AddChild(StudioStyle.SectionLabel("CONTEXTE ET UTILISATIONS"));
        _detailTitle = StudioStyle.Label("Commencez à écrire", 16, StudioStyle.Text, wrap: true);
        details.AddChild(_detailTitle);
        _detailPath = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted, wrap: true);
        details.AddChild(_detailPath);
        _detailBody = StudioStyle.Label(
            "La recherche couvre les 37 pages, les actions du shell et le contenu canonique du module.",
            11,
            StudioStyle.Muted,
            wrap: true);
        details.AddChild(_detailBody);
        details.AddChild(new HSeparator());
        _referenceList = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        _referenceList.AddThemeConstantOverride("separation", 5);
        details.AddChild(_referenceList);
        _rename = StudioStyle.Button("Renommer en sécurité…", primary: true, compact: true);
        _rename.Disabled = true;
        _rename.TooltipText = "Prévisualiser le nom, l’adresse et les références avant toute modification.";
        _rename.Pressed += RequestRename;
        details.AddChild(_rename);

        Resized += UpdateDialogBounds;
        UpdateDialogBounds();
    }

    internal void Open(string initialQuery = "")
    {
        Visible = true;
        _query.Text = initialQuery;
        QueryChanged?.Invoke(initialQuery);
        _query.GrabFocus();
        _query.CaretColumn = _query.Text.Length;
    }

    internal void Close()
    {
        if (!Visible)
        {
            return;
        }

        Visible = false;
        CloseRequested?.Invoke();
    }

    internal void SetResults(IReadOnlyList<StudioGlobalSearchEntry> entries)
    {
        SetRenameAvailable(false);
        _visibleEntries.Clear();
        _visibleEntries.AddRange(entries);
        _results.Clear();
        foreach (var entry in entries)
        {
            _results.AddItem($"{entry.Group}  ·  {entry.Title}");
            _results.SetItemTooltip(_results.ItemCount - 1, entry.Subtitle);
        }

        _summary.Text = entries.Count == 0
            ? "Aucun résultat · essayez un nom, un tag, un contenu ou une action"
            : $"{entries.Count} résultat(s) · Entrée pour ouvrir";
        if (entries.Count > 0)
        {
            _results.Select(0);
            EntrySelected?.Invoke(0);
        }
        else
        {
            ShowDetails("Aucun résultat", string.Empty,
                "La recherche est tolérante aux accents et couvre les noms, tags, contenus et actions.", []);
        }
    }

    internal StudioGlobalSearchEntry? EntryAt(int index) =>
        index >= 0 && index < _visibleEntries.Count ? _visibleEntries[index] : null;

    internal void SetRenameAvailable(bool available)
    {
        _rename.Disabled = !available;
        _rename.Text = available ? "Renommer en sécurité…" : "Renommage indisponible";
    }

    internal void ShowDetails(
        string title,
        string path,
        string body,
        IReadOnlyList<string> references)
    {
        _detailTitle.Text = title;
        _detailPath.Text = path;
        _detailBody.Text = body;
        ClearChildren(_referenceList);
        _referenceList.AddChild(StudioStyle.SectionLabel(
            references.Count == 0 ? "AUCUNE UTILISATION INVERSE" : $"{references.Count} UTILISATION(S)"));
        foreach (var reference in references.Take(8))
        {
            var card = StudioStyle.Panel("SearchReference", StudioStyle.SurfaceRaised, 5);
            card.AddChild(StudioStyle.Label(reference, 10, StudioStyle.Text, wrap: true));
            _referenceList.AddChild(card);
        }
        if (references.Count > 8)
        {
            _referenceList.AddChild(StudioStyle.Label(
                $"+ {references.Count - 8} autre(s) utilisation(s)", 10, StudioStyle.Muted));
        }
    }

    private void ActivateCurrent()
    {
        var selected = _results.GetSelectedItems();
        var index = selected.Length > 0 ? selected[0] : 0;
        if (index >= 0 && index < _visibleEntries.Count)
        {
            EntryActivated?.Invoke(index);
        }
    }

    private void RequestRename()
    {
        var selected = _results.GetSelectedItems();
        if (selected.Length > 0)
        {
            RenameRequested?.Invoke(selected[0]);
        }
    }

    private void QueryInput(InputEvent input)
    {
        if (input is not InputEventKey key || !key.Pressed || key.Echo)
        {
            return;
        }

        if (key.Keycode == Key.Down || key.Keycode == Key.Up)
        {
            if (_visibleEntries.Count == 0)
            {
                return;
            }

            var selected = _results.GetSelectedItems();
            var current = selected.Length == 0 ? 0 : selected[0];
            var next = key.Keycode == Key.Down
                ? Math.Min(current + 1, _visibleEntries.Count - 1)
                : Math.Max(current - 1, 0);
            _results.Select(next);
            _results.EnsureCurrentIsVisible();
            EntrySelected?.Invoke(next);
            AcceptEvent();
        }
        else if (key.Keycode == Key.Escape)
        {
            Close();
            AcceptEvent();
        }
    }

    private void BackdropInput(InputEvent input)
    {
        if (input is InputEventMouseButton { Pressed: true })
        {
            Close();
        }
    }

    private void UpdateDialogBounds()
    {
        var width = Math.Clamp(Size.X - 48, 720, 980);
        var height = Math.Clamp(Size.Y - 112, 460, 680);
        _dialog.Position = new Vector2((Size.X - width) * .5f, Math.Max(58, (Size.Y - height) * .34f));
        _dialog.Size = new Vector2(width, height);
    }

    private static void ClearChildren(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }
}
