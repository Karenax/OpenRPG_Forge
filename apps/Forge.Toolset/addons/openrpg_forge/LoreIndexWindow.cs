#if TOOLS
using Forge.Application.Assistant;
using Forge.Application.Projects;
using Forge.Domain.Assistant;
using Godot;

namespace Forge.Toolset.Editor;

public partial class LoreIndexWindow : Window
{
    private Func<ProjectState?>? _getProject;
    private Func<LoreIndexState?>? _getLore;
    private Func<LoreIndexDefinition, Guid, Guid, bool, bool>? _apply;
    private Func<bool>? _undo;
    private Func<bool>? _redo;
    private Action<Window>? _brandWindow;
    private LoreIndexDefinition? _draft;
    private LoreIndexDefinition? _approved;
    private Guid _selectedEntryId;
    private Guid _approvedTransactionId;
    private Guid _approvedPlanId;
    private LoreEntryKind[] _kinds = Enum.GetValues<LoreEntryKind>();

    private CheckButton? _enabled;
    private SpinBox? _contextBudget;
    private ItemList? _entries;
    private OptionButton? _kind;
    private LineEdit? _key;
    private LineEdit? _title;
    private TextEdit? _content;
    private LineEdit? _sources;
    private LineEdit? _related;
    private LineEdit? _tags;
    private LineEdit? _query;
    private RichTextLabel? _preview;
    private Label? _status;
    private Button? _applyButton;
    private bool _suppressSignals;

    public int EntryCount => _getLore?.Invoke()?.Index.Entries.Count ?? 0;
    public string LastContextHash { get; private set; } = string.Empty;
    public int LastContextSourceCount { get; private set; }

    public void Initialize(
        Func<ProjectState?> getProject,
        Func<LoreIndexState?> getLore,
        Func<LoreIndexDefinition, Guid, Guid, bool, bool> apply,
        Func<bool> undo,
        Func<bool> redo,
        Action<Window> brandWindow)
    {
        _getProject = getProject ?? throw new ArgumentNullException(nameof(getProject));
        _getLore = getLore ?? throw new ArgumentNullException(nameof(getLore));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _undo = undo ?? throw new ArgumentNullException(nameof(undo));
        _redo = redo ?? throw new ArgumentNullException(nameof(redo));
        _brandWindow = brandWindow ?? throw new ArgumentNullException(nameof(brandWindow));
        Title = "Index de lore · OpenRPG Forge";
        MinSize = new Vector2I(1240, 780);
        Size = MinSize;
        Transient = true;
        Exclusive = false;
        Unresizable = true;
        Visible = false;
        BuildUi();
    }

    public void Open()
    {
        Refresh();
        Exclusive = true;
        PopupCentered(Size);
        _brandWindow?.Invoke(this);
    }

    public void Refresh()
    {
        var project = _getProject?.Invoke();
        var lore = _getLore?.Invoke();
        if (project is null || lore is null)
        {
            SetStatus("Aucun projet Forge ouvert.", true);
            return;
        }
        _draft = lore.Index;
        _approved = null;
        _approvedPlanId = Guid.Empty;
        _approvedTransactionId = Guid.Empty;
        LastContextHash = string.Empty;
        LastContextSourceCount = 0;
        _suppressSignals = true;
        _enabled!.ButtonPressed = _draft.Enabled;
        _contextBudget!.Value = _draft.MaximumContextCharacters;
        PopulateEntries();
        if (_draft.Entries.Count > 0)
        {
            _entries!.Select(0);
            LoadEntry(_draft.Entries[0]);
        }
        else
        {
            NewEntry();
        }
        _query!.Text = project.Project.Title;
        _preview!.Text = "Sélectionnez ou ajoutez une entrée, puis prévisualisez l’index.";
        _suppressSignals = false;
        _applyButton!.Disabled = true;
        SetStatus($"Index chargé · révision {lore.Revision} · {_draft.Entries.Count} entrée(s).", false);
    }

    public bool ConfigureForSmokeTest()
    {
        Refresh();
        var project = _getProject!.Invoke()!.Project;
        _selectedEntryId = Guid.Parse("b1230000-0000-4000-8000-000000000099");
        _kind!.Selected = Array.IndexOf(_kinds, LoreEntryKind.Terminology);
        _key!.Text = "braise-memorielle";
        _title!.Text = "Braise mémorielle";
        _content!.Text = "Énergie canonique qui relie la Crypte, ses sanctuaires et les souvenirs du monde.";
        _sources!.Text = $"project:{project.Id}";
        _related!.Text = string.Empty;
        _tags!.Text = "braise, canon, terminologie";
        _query!.Text = "braise crypte";
        return StageEntry();
    }

    public bool PreviewForSmokeTest() => Preview();

    public bool ApplyForSmokeTest()
    {
        Apply();
        return _approved is null;
    }

    private void BuildUi()
    {
        var background = new PanelContainer { AnchorRight = 1, AnchorBottom = 1 };
        background.AddThemeStyleboxOverride("panel", Card("100d1b", "62458a", 1, 0));
        AddChild(background);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 20);
        background.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);
        var header = new Label { Text = "INDEX DE LORE GOUVERNÉ", ThemeTypeVariation = "HeaderSmall" };
        header.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(header);
        root.AddChild(new Label
        {
            Text = "Bible du monde, factions, personnages, chronologie et terminologie · sources canoniques toujours visibles.",
        });

        var settings = new HBoxContainer();
        settings.AddThemeConstantOverride("separation", 12);
        root.AddChild(settings);
        _enabled = new CheckButton { Text = "Activer l’index pour le contexte Assistant" };
        settings.AddChild(_enabled);
        settings.AddChild(new Label { Text = "Budget de contexte" });
        _contextBudget = new SpinBox { MinValue = 512, MaxValue = 100_000, Step = 256, CustomMinimumSize = new Vector2(150, 0) };
        settings.AddChild(_contextBudget);

        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12);
        root.AddChild(body);

        var listCard = AddCard(body, 280);
        listCard.AddChild(Section("ENTRÉES CANONIQUES"));
        _entries = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 420) };
        _entries.ItemSelected += index => LoadEntry(_draft!.Entries[(int)index]);
        listCard.AddChild(_entries);
        var listActions = new HBoxContainer();
        listActions.AddThemeConstantOverride("separation", 6);
        listCard.AddChild(listActions);
        var add = new Button { Text = "+ Nouvelle" };
        add.Pressed += NewEntry;
        listActions.AddChild(add);
        var remove = new Button { Text = "Retirer" };
        remove.Pressed += RemoveEntry;
        listActions.AddChild(remove);

        var editCard = AddCard(body, 430, true);
        editCard.AddChild(Section("IDENTITÉ, CONTENU & SOURCES"));
        _kind = new OptionButton();
        foreach (var kind in _kinds) _kind.AddItem(FormatKind(kind));
        editCard.AddChild(Field("Type", _kind));
        _key = AddLine(editCard, "Clé stable", "serment-de-braise");
        _title = AddLine(editCard, "Titre", "Serment de braise");
        editCard.AddChild(FieldLabel("CONTENU CANONIQUE"));
        _content = new TextEdit { CustomMinimumSize = new Vector2(0, 170), WrapMode = TextEdit.LineWrappingMode.Boundary };
        editCard.AddChild(_content);
        _sources = AddLine(editCard, "IDs sources séparés par virgule", "project:uuid, dialogue:uuid, manual:world-bible");
        _related = AddLine(editCard, "UUID liés séparés par virgule", "uuid, uuid");
        _tags = AddLine(editCard, "Tags", "canon, faction, braise");
        var stage = new Button { Text = "Mettre à jour le brouillon", CustomMinimumSize = new Vector2(0, 38) };
        stage.Pressed += () => StageEntry();
        editCard.AddChild(stage);

        var retrievalCard = AddCard(body, 390, true);
        retrievalCard.AddChild(Section("RECHERCHE & GRAPHE DE CONTEXTE"));
        _query = AddLine(retrievalCard, "Requête", "braise elyra chronologie");
        retrievalCard.AddChild(FieldLabel("PLAN, DIFF & SOURCES RETROUVÉES"));
        _preview = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 420),
        };
        _preview.AddThemeStyleboxOverride("normal", Card("0b0911", "3d3150", 1, 7));
        retrievalCard.AddChild(_preview);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 8);
        root.AddChild(footer);
        _status = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        footer.AddChild(_status);
        var undo = new Button { Text = "Annuler" };
        undo.Pressed += () => { if (_undo?.Invoke() == true) Refresh(); };
        footer.AddChild(undo);
        var redo = new Button { Text = "Rétablir" };
        redo.Pressed += () => { if (_redo?.Invoke() == true) Refresh(); };
        footer.AddChild(redo);
        var preview = new Button { Text = "Prévisualiser", CustomMinimumSize = new Vector2(140, 42) };
        preview.Pressed += () => Preview();
        footer.AddChild(preview);
        _applyButton = new Button { Text = "Appliquer l’index", Disabled = true, CustomMinimumSize = new Vector2(165, 42) };
        _applyButton.AddThemeColorOverride("font_color", Colors.White);
        _applyButton.AddThemeStyleboxOverride("normal", Card("d84f32", "ff8b63", 1, 8));
        _applyButton.Pressed += Apply;
        footer.AddChild(_applyButton);
        var close = new Button { Text = "Fermer" };
        close.Pressed += Hide;
        footer.AddChild(close);
        CloseRequested += Hide;

        _enabled.Toggled += _ => InvalidatePreview();
        _contextBudget.ValueChanged += _ => InvalidatePreview();
        _query.TextChanged += _ => InvalidatePreview();
    }

    private void PopulateEntries()
    {
        _entries!.Clear();
        foreach (var entry in _draft!.Entries)
        {
            _entries.AddItem($"{FormatKind(entry.Kind)} · {entry.Title}");
        }
    }

    private void NewEntry()
    {
        _selectedEntryId = Guid.Empty;
        _kind!.Selected = 0;
        _key!.Text = string.Empty;
        _title!.Text = string.Empty;
        _content!.Text = string.Empty;
        var project = _getProject?.Invoke()?.Project;
        _sources!.Text = project is null ? "manual:world-bible" : $"project:{project.Id}";
        _related!.Text = string.Empty;
        _tags!.Text = string.Empty;
        InvalidatePreview();
    }

    private void LoadEntry(LoreEntryDefinition entry)
    {
        _selectedEntryId = entry.Id;
        _kind!.Selected = Array.IndexOf(_kinds, entry.Kind);
        _key!.Text = entry.Key;
        _title!.Text = entry.Title;
        _content!.Text = entry.Content;
        _sources!.Text = string.Join(", ", entry.SourceIds);
        _related!.Text = string.Join(", ", entry.RelatedEntryIds);
        _tags!.Text = string.Join(", ", entry.Tags);
        InvalidatePreview();
    }

    private bool StageEntry()
    {
        try
        {
            var entry = new LoreEntryDefinition(
                _selectedEntryId == Guid.Empty ? Guid.NewGuid() : _selectedEntryId,
                1,
                _kinds[_kind!.Selected],
                _key!.Text,
                _title!.Text,
                _content!.Text,
                Split(_sources!.Text),
                Split(_related!.Text).Select(Guid.Parse).ToArray(),
                Split(_tags!.Text),
                "creator:g12-b");
            var entries = _draft!.Entries.Where(item => item.Id != entry.Id).Append(entry).ToArray();
            var candidate = BuildIndex(entries);
            var diagnostics = LoreIndexValidator.Validate(candidate);
            if (diagnostics.Count > 0)
            {
                SetStatus($"{diagnostics[0].Code} · {diagnostics[0].Message}", true);
                return false;
            }
            _draft = candidate;
            _selectedEntryId = entry.Id;
            PopulateEntries();
            var selected = Array.FindIndex(_draft.Entries.ToArray(), item => item.Id == entry.Id);
            if (selected >= 0) _entries!.Select(selected);
            InvalidatePreview();
            SetStatus("✓ Brouillon de lore mis à jour · aucun fichier modifié.", false);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            SetStatus($"assistant.lore.editor_invalid · {exception.Message}", true);
            return false;
        }
    }

    private void RemoveEntry()
    {
        if (_selectedEntryId == Guid.Empty || _draft is null) return;
        var entries = _draft.Entries
            .Where(entry => entry.Id != _selectedEntryId)
            .Select(entry => entry.RelatedEntryIds.Contains(_selectedEntryId)
                ? new LoreEntryDefinition(
                    entry.Id,
                    entry.SchemaVersion,
                    entry.Kind,
                    entry.Key,
                    entry.Title,
                    entry.Content,
                    entry.SourceIds,
                    entry.RelatedEntryIds.Where(id => id != _selectedEntryId).ToArray(),
                    entry.Tags,
                    entry.Provenance)
                : entry)
            .ToArray();
        _draft = BuildIndex(entries);
        PopulateEntries();
        NewEntry();
        SetStatus("Entrée retirée du brouillon · appliquez le plan pour persister.", false);
    }

    private bool Preview()
    {
        var project = _getProject?.Invoke();
        if (project is null || _draft is null) return false;
        _draft = BuildIndex(_draft.Entries);
        var diagnostics = LoreIndexProjectValidator.Validate(_draft, project.Project);
        if (diagnostics.Count > 0)
        {
            SetStatus($"{diagnostics[0].Code} · {diagnostics[0].Message}", true);
            return false;
        }
        var transactionId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        if (_apply?.Invoke(_draft, transactionId, planId, true) != true)
        {
            SetStatus("Le plan de lore a été refusé par la commande de domaine.", true);
            return false;
        }
        var packet = LoreContextAssembler.Assemble(_draft, project.Project, _query!.Text.Trim());
        _approved = _draft;
        _approvedTransactionId = transactionId;
        _approvedPlanId = planId;
        LastContextHash = packet.ContentSha256;
        LastContextSourceCount = packet.SourceIds.Count;
        _applyButton!.Disabled = false;
        var previousCount = _getLore!.Invoke()!.Index.Entries.Count;
        _preview!.Text =
            $"[color=#83d6ae]PLAN {planId:D}[/color]\n" +
            $"Transaction : {transactionId:D}\n" +
            $"Diff : assistant/lore-index · {previousCount} → {_draft.Entries.Count} entrée(s)\n" +
            $"Contexte : {packet.EntryCount} entrée(s) · {packet.SourceIds.Count} source(s) · " +
            $"SHA-256 {packet.ContentSha256}\n" +
            $"Tronqué : {packet.Truncated}\n\n{packet.Content}";
        SetStatus("✓ Plan et contexte prévisualisés · aucune mutation canonique.", false);
        return true;
    }

    private void Apply()
    {
        if (_approved is null || _approvedPlanId == Guid.Empty || _approvedTransactionId == Guid.Empty) return;
        if (_apply?.Invoke(_approved, _approvedTransactionId, _approvedPlanId, false) != true)
        {
            SetStatus("Application refusée : plan, transaction, révision ou persistance invalide.", true);
            return;
        }
        _approved = null;
        _approvedPlanId = Guid.Empty;
        _approvedTransactionId = Guid.Empty;
        Refresh();
        SetStatus("✓ Index de lore appliqué · annulation et rétablissement disponibles.", false);
    }

    private LoreIndexDefinition BuildIndex(IReadOnlyList<LoreEntryDefinition> entries) =>
        new(
            _draft!.Id,
            1,
            _enabled!.ButtonPressed,
            (int)_contextBudget!.Value,
            entries,
            "creator:g12-b");

    private void InvalidatePreview()
    {
        if (_suppressSignals || _applyButton is null) return;
        _approved = null;
        _approvedPlanId = Guid.Empty;
        _approvedTransactionId = Guid.Empty;
        LastContextHash = string.Empty;
        LastContextSourceCount = 0;
        _applyButton.Disabled = true;
    }

    private void SetStatus(string text, bool error)
    {
        if (_status is null) return;
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", new Color(error ? "ff8c8c" : "83d6ae"));
    }

    private static string[] Split(string value) =>
        (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string FormatKind(LoreEntryKind kind) => kind switch
    {
        LoreEntryKind.WorldBible => "Bible du monde",
        LoreEntryKind.Faction => "Faction",
        LoreEntryKind.Character => "Personnage",
        LoreEntryKind.TimelineEvent => "Chronologie",
        LoreEntryKind.Terminology => "Terminologie",
        _ => kind.ToString(),
    };

    private static VBoxContainer AddCard(Container parent, float width, bool expand = false)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(width, 0),
            SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", Card("171221", "47365f", 1, 10));
        parent.AddChild(panel);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 13);
        panel.AddChild(margin);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        margin.AddChild(content);
        return content;
    }

    private static Label Section(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", new Color("ffb276"));
        return label;
    }

    private static Label FieldLabel(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", new Color("9f8dbd"));
        return label;
    }

    private static Control Field(string label, Control control)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 3);
        column.AddChild(FieldLabel(label.ToUpperInvariant()));
        column.AddChild(control);
        return column;
    }

    private static LineEdit AddLine(Container parent, string label, string placeholder)
    {
        var line = new LineEdit { PlaceholderText = placeholder };
        parent.AddChild(Field(label, line));
        return line;
    }

    private static StyleBoxFlat Card(string background, string border, int width, int radius) => new()
    {
        BgColor = new Color(background),
        BorderColor = new Color(border),
        BorderWidthLeft = width,
        BorderWidthTop = width,
        BorderWidthRight = width,
        BorderWidthBottom = width,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
    };
}
#endif
