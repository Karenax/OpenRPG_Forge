#if TOOLS
using Forge.Application.Blueprints;
using Forge.Application.Creator;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Godot;

namespace Forge.Toolset.Editor;

public partial class BlueprintCatalogWindow : Window
{
    private readonly List<BlueprintId> _visibleBlueprintIds = [];
    private readonly BlueprintKind[] _kinds = Enum.GetValues<BlueprintKind>();

    private Func<ProjectState?>? _getState;
    private Func<BlueprintId, string, string, BlueprintKind, bool>? _upsertBlueprint;
    private Func<BlueprintId, string, string, bool>? _createFromSelection;
    private Action<CreatorSelection>? _publishSelection;
    private Action<Window>? _brandWindow;
    private BlueprintId? _draftBlueprintId;

    private LineEdit? _search;
    private ItemList? _blueprintList;
    private Label? _catalogSummary;
    private LineEdit? _displayName;
    private LineEdit? _slug;
    private OptionButton? _kind;
    private Label? _identity;
    private Label? _inheritance;
    private ItemList? _referenceList;
    private Label? _referenceSummary;
    private Label? _status;
    private Button? _createFromSelectionButton;

    public int VisibleBlueprintCount => _visibleBlueprintIds.Count;

    public int ReferenceCount => _referenceList?.ItemCount ?? 0;

    public void Initialize(
        Func<ProjectState?> getState,
        Func<BlueprintId, string, string, BlueprintKind, bool> upsertBlueprint,
        Func<BlueprintId, string, string, bool> createFromSelection,
        Action<CreatorSelection> publishSelection,
        Action<Window> brandWindow)
    {
        ArgumentNullException.ThrowIfNull(getState);
        ArgumentNullException.ThrowIfNull(upsertBlueprint);
        ArgumentNullException.ThrowIfNull(createFromSelection);
        ArgumentNullException.ThrowIfNull(publishSelection);
        ArgumentNullException.ThrowIfNull(brandWindow);
        _getState = getState;
        _upsertBlueprint = upsertBlueprint;
        _createFromSelection = createFromSelection;
        _publishSelection = publishSelection;
        _brandWindow = brandWindow;

        Title = "Blueprints · OpenRPG Forge";
        MinSize = new Vector2I(1040, 700);
        Size = new Vector2I(1040, 700);
        Transient = true;
        Exclusive = false;
        Unresizable = true;
        Visible = false;
        BuildUi();
        ConnectSignals();
    }

    public void Open()
    {
        Refresh();
        Exclusive = true;
        PopupCentered(new Vector2I(1040, 700));
        _brandWindow?.Invoke(this);
    }

    public void Refresh(BlueprintId? preferredBlueprintId = null)
    {
        var state = _getState?.Invoke();
        if (state is null || _blueprintList is null)
        {
            return;
        }

        var keepId = preferredBlueprintId ?? _draftBlueprintId;
        var query = _search?.Text.Trim() ?? string.Empty;
        _visibleBlueprintIds.Clear();
        _blueprintList.Clear();
        foreach (var blueprint in state.Project.Blueprints
                     .Where(blueprint => Matches(blueprint, query))
                     .OrderBy(blueprint => blueprint.DisplayName, StringComparer.Ordinal))
        {
            _visibleBlueprintIds.Add(blueprint.Id);
            _blueprintList.AddItem($"{blueprint.DisplayName}\n{FormatKind(blueprint.Kind)} · {blueprint.Slug}");
        }

        _catalogSummary!.Text = $"{_visibleBlueprintIds.Count} affiché(s) · {state.Project.Blueprints.Count} au total";
        var selectedIndex = keepId is null ? -1 : _visibleBlueprintIds.FindIndex(id => id == keepId);
        if (selectedIndex >= 0)
        {
            _blueprintList.Select(selectedIndex);
            LoadBlueprint(state, keepId!);
        }
        else if (_visibleBlueprintIds.Count > 0)
        {
            _blueprintList.Select(0);
            LoadBlueprint(state, _visibleBlueprintIds[0]);
        }
        else
        {
            ClearEditor();
        }
    }

    public bool SelectBlueprintForSmokeTest(BlueprintId blueprintId)
    {
        Refresh(blueprintId);
        var index = _visibleBlueprintIds.FindIndex(id => id == blueprintId);
        if (index < 0)
        {
            return false;
        }

        SelectBlueprint(index);
        return true;
    }

    private void BuildUi()
    {
        var background = new PanelContainer
        {
            AnchorRight = 1,
            AnchorBottom = 1,
        };
        background.AddThemeStyleboxOverride("panel", Card("15111f", "62458a", 1, 0));
        AddChild(background);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 22);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_right", 22);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        background.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        var titleRow = new HBoxContainer();
        titleRow.AddThemeConstantOverride("separation", 12);
        root.AddChild(titleRow);
        var titleColumn = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        titleRow.AddChild(titleColumn);
        var title = new Label { Text = "BLUEPRINTS", ThemeTypeVariation = "HeaderSmall" };
        title.AddThemeColorOverride("font_color", new Color("ff9858"));
        titleColumn.AddChild(title);
        titleColumn.AddChild(new Label
        {
            Text = "Créez des définitions réutilisables, dérivez une instance et inspectez tous ses usages.",
        });
        var newButton = new Button { Text = "+ Nouveau blueprint", CustomMinimumSize = new Vector2(190, 40) };
        newButton.AddThemeStyleboxOverride("normal", Card("38264f", "8060aa", 1, 8));
        newButton.Pressed += CreateNew;
        titleRow.AddChild(newButton);

        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 14);
        root.AddChild(body);

        var catalogCard = AddCard(body, new Vector2(310, 0));
        catalogCard.AddChild(new Label { Text = "PALETTE DU PROJET" });
        _search = new LineEdit { PlaceholderText = "Rechercher par nom, slug ou type…" };
        catalogCard.AddChild(_search);
        _catalogSummary = new Label();
        _catalogSummary.AddThemeColorOverride("font_color", new Color("a899b9"));
        catalogCard.AddChild(_catalogSummary);
        _blueprintList = new ItemList
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            AllowReselect = true,
        };
        catalogCard.AddChild(_blueprintList);

        var editorCard = AddCard(body, new Vector2(350, 0), expand: true);
        editorCard.AddChild(new Label { Text = "DÉFINITION" });
        _identity = new Label { Text = "Nouvelle définition" };
        _identity.AddThemeColorOverride("font_color", new Color("ffb07c"));
        editorCard.AddChild(_identity);
        editorCard.AddChild(new Label { Text = "Nom affiché" });
        _displayName = new LineEdit { PlaceholderText = "Ex. Colonne de la crypte" };
        editorCard.AddChild(_displayName);
        editorCard.AddChild(new Label { Text = "Slug stable" });
        _slug = new LineEdit { PlaceholderText = "colonne-de-la-crypte" };
        editorCard.AddChild(_slug);
        editorCard.AddChild(new Label { Text = "Type" });
        _kind = new OptionButton();
        foreach (var kind in _kinds)
        {
            _kind.AddItem(FormatKind(kind));
        }
        editorCard.AddChild(_kind);
        _inheritance = new Label
        {
            Text = "Les instances héritent du blueprint et conservent leurs propriétés personnalisées.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _inheritance.AddThemeColorOverride("font_color", new Color("b9aac8"));
        editorCard.AddChild(_inheritance);
        var spacer = new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        editorCard.AddChild(spacer);
        _createFromSelectionButton = new Button
        {
            Text = "Créer depuis l’instance sélectionnée",
            CustomMinimumSize = new Vector2(0, 42),
        };
        editorCard.AddChild(_createFromSelectionButton);
        var saveButton = new Button { Text = "Enregistrer la définition", CustomMinimumSize = new Vector2(0, 42) };
        saveButton.AddThemeColorOverride("font_color", Colors.White);
        saveButton.AddThemeStyleboxOverride("normal", Card("d84f32", "ff8b63", 1, 8));
        saveButton.Pressed += Save;
        editorCard.AddChild(saveButton);

        var referenceCard = AddCard(body, new Vector2(290, 0));
        referenceCard.AddChild(new Label { Text = "RÉFÉRENCES INVERSÉES" });
        _referenceSummary = new Label { Text = "Aucun usage" };
        _referenceSummary.AddThemeColorOverride("font_color", new Color("ffb07c"));
        referenceCard.AddChild(_referenceSummary);
        _referenceList = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        referenceCard.AddChild(_referenceList);
        var help = new Label
        {
            Text = "Les entités et locuteurs de dialogues sont recalculés depuis le projet canonique.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        help.AddThemeColorOverride("font_color", new Color("a899b9"));
        referenceCard.AddChild(help);

        _status = new Label { Text = "Prêt." };
        _status.AddThemeColorOverride("font_color", new Color("9ee6b3"));
        root.AddChild(_status);
    }

    private VBoxContainer AddCard(HBoxContainer parent, Vector2 minimumSize, bool expand = false)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = minimumSize,
            SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkBegin,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", Card("211a2d", "47365e", 1, 10));
        parent.AddChild(panel);
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        panel.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        margin.AddChild(column);
        return column;
    }

    private static StyleBoxFlat Card(string background, string border, int borderWidth, int radius) => new()
    {
        BgColor = new Color(background),
        BorderColor = new Color(border),
        BorderWidthLeft = borderWidth,
        BorderWidthTop = borderWidth,
        BorderWidthRight = borderWidth,
        BorderWidthBottom = borderWidth,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
    };

    private void ConnectSignals()
    {
        CloseRequested += Hide;
        _search!.TextChanged += _ => Refresh();
        _blueprintList!.ItemSelected += SelectBlueprint;
        _createFromSelectionButton!.Pressed += CreateFromSelection;
    }

    private void SelectBlueprint(long index)
    {
        var state = _getState?.Invoke();
        if (state is null || index < 0 || index >= _visibleBlueprintIds.Count)
        {
            return;
        }

        LoadBlueprint(state, _visibleBlueprintIds[(int)index]);
    }

    private void LoadBlueprint(ProjectState state, BlueprintId blueprintId)
    {
        var blueprint = state.Project.Blueprints.Single(candidate => candidate.Id == blueprintId);
        _draftBlueprintId = blueprint.Id;
        _identity!.Text = $"{blueprint.Id.ToString()[..8]} · {FormatKind(blueprint.Kind)}";
        _displayName!.Text = blueprint.DisplayName;
        _slug!.Text = blueprint.Slug;
        _kind!.Selected = Array.IndexOf(_kinds, blueprint.Kind);
        RefreshReferences(state, blueprint);
        _publishSelection?.Invoke(new CreatorSelection(
            CreatorSelectionKind.Blueprint,
            blueprint.Id.ToString(),
            blueprint.DisplayName,
            $"blueprints/{blueprint.Id}"));
        SetStatus($"Blueprint « {blueprint.DisplayName} » chargé.", false);
    }

    private void RefreshReferences(ProjectState state, BlueprintDefinition blueprint)
    {
        var references = BlueprintReferenceIndex.Build(state.Project).Find(blueprint.Id);
        _referenceList!.Clear();
        foreach (var reference in references)
        {
            _referenceList.AddItem($"{FormatReferenceKind(reference.Kind)}\n{reference.DisplayName}");
            _referenceList.SetItemTooltip(_referenceList.ItemCount - 1, reference.DocumentPath);
        }

        _referenceSummary!.Text = references.Count == 0
            ? "Aucun usage · suppression sans dépendance"
            : $"{references.Count} usage(s) dans le projet";
    }

    private void CreateNew()
    {
        _draftBlueprintId = BlueprintId.From(Guid.NewGuid());
        _identity!.Text = "Nouvelle définition · non enregistrée";
        _displayName!.Text = "Nouveau blueprint";
        _slug!.Text = $"nouveau-blueprint-{(_getState?.Invoke()?.Project.Blueprints.Count ?? 0) + 1}";
        _kind!.Selected = 0;
        _referenceList!.Clear();
        _referenceSummary!.Text = "Aucun usage";
        SetStatus("Définition prête. Ajustez son nom, son slug et son type.", false);
    }

    private void ClearEditor()
    {
        _draftBlueprintId = null;
        _identity!.Text = "Aucun blueprint visible";
        _displayName!.Text = string.Empty;
        _slug!.Text = string.Empty;
        _referenceList!.Clear();
        _referenceSummary!.Text = "Aucun usage";
    }

    private void Save()
    {
        var blueprintId = _draftBlueprintId ?? BlueprintId.From(Guid.NewGuid());
        if (!ValidateDraft(out var displayName, out var slug, out var kind))
        {
            return;
        }

        if (_upsertBlueprint?.Invoke(blueprintId, slug, displayName, kind) == true)
        {
            _draftBlueprintId = blueprintId;
            Refresh(blueprintId);
            SetStatus($"Blueprint « {displayName} » enregistré par commande transactionnelle.", false);
        }
        else
        {
            SetStatus("Le blueprint n’a pas pu être enregistré. Consultez les diagnostics.", true);
        }
    }

    private void CreateFromSelection()
    {
        if (!ValidateDraft(out var displayName, out var slug, out _))
        {
            return;
        }

        var blueprintId = BlueprintId.From(Guid.NewGuid());
        if (_createFromSelection?.Invoke(blueprintId, slug, displayName) == true)
        {
            _draftBlueprintId = blueprintId;
            Refresh(blueprintId);
            SetStatus("Blueprint dérivé et instance retargetée sans perdre ses overrides.", false);
        }
        else
        {
            SetStatus("Sélectionnez une instance dans le Monde ou l’Explorateur avant de dériver.", true);
        }
    }

    private bool ValidateDraft(out string displayName, out string slug, out BlueprintKind kind)
    {
        displayName = _displayName!.Text.Trim();
        slug = _slug!.Text.Trim();
        kind = _kinds[Math.Clamp(_kind!.Selected, 0, _kinds.Length - 1)];
        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(slug))
        {
            SetStatus("Le nom et le slug sont obligatoires.", true);
            return false;
        }

        if (!string.Equals(slug, SlugGenerator.FromDisplayName(slug, "blueprint"), StringComparison.Ordinal))
        {
            SetStatus("Le slug doit être canonique : minuscules, chiffres et tirets.", true);
            return false;
        }

        return true;
    }

    private void SetStatus(string message, bool error)
    {
        _status!.Text = message;
        _status.AddThemeColorOverride("font_color", new Color(error ? "ff8c8c" : "9ee6b3"));
    }

    private static bool Matches(BlueprintDefinition blueprint, string query) =>
        string.IsNullOrWhiteSpace(query) ||
        blueprint.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        blueprint.Slug.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        FormatKind(blueprint.Kind).Contains(query, StringComparison.OrdinalIgnoreCase);

    private static string FormatKind(BlueprintKind kind) => kind switch
    {
        BlueprintKind.Placeable => "Décor plaçable",
        BlueprintKind.Door => "Porte",
        BlueprintKind.Creature => "Créature",
        BlueprintKind.PlayerSpawn => "Point de départ",
        BlueprintKind.Trigger => "Déclencheur",
        BlueprintKind.Item => "Objet",
        _ => kind.ToString(),
    };

    private static string FormatReferenceKind(BlueprintReferenceKind kind) => kind switch
    {
        BlueprintReferenceKind.EntityInstance => "INSTANCE",
        BlueprintReferenceKind.DialogueSpeaker => "DIALOGUE",
        _ => kind.ToString().ToUpperInvariant(),
    };
}
#endif
