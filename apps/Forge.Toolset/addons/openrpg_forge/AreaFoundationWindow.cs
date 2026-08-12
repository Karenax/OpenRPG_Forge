#if TOOLS
using Forge.Application.Areas;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;
using Godot;

namespace Forge.Toolset.Editor;

public partial class AreaFoundationWindow : Window
{
    private Func<ProjectState?>? _state;
    private Func<AreaId?>? _active;
    private Func<AreaId, AreaFoundationProfile, bool>? _apply;
    private Func<AreaId, AreaTileEditRequest, bool>? _editTiles;
    private Action<Window>? _brand;
    private AreaFoundationProfile? _draft;
    private readonly List<BlueprintId> _blueprints = [];
    private readonly List<EntityId> _entities = [];

    private LineEdit? _kit;
    private LineEdit? _terrain;
    private LineEdit? _lighting;
    private LineEdit? _navigation;
    private LineEdit? _streaming;
    private LineEdit? _biome;
    private SpinBox? _seed;
    private SpinBox? _budget;
    private SpinBox? _grid;
    private OptionButton? _blueprint;
    private OptionButton? _entity;
    private OptionButton? _rotationMode;
    private SpinBox? _rotation;
    private OptionButton? _mirror;
    private LineEdit? _variant;
    private LineEdit? _entered;
    private LineEdit? _exited;
    private CheckBox? _enteredRequired;
    private CheckBox? _exitedRequired;
    private Label? _documentSummary;
    private Label? _tileSummary;
    private Label? _preview;
    private ItemList? _diagnostics;
    private OptionButton? _tileOperation;
    private CheckBox? _preserveAttachments;
    private LineEdit? _replacementKit;
    private LineEdit? _groupParameters;
    private SpinBox? _groupWidth;
    private SpinBox? _groupDepth;
    private SpinBox? _groupHeight;
    private SpinBox? _autoFitSeed;
    private CheckBox? _manualLock;
    private LineEdit? _tileState;
    private LineEdit? _tileAnimation;
    private LineEdit? _tileMaterial;
    private LineEdit? _tileLightColor;
    private SpinBox? _tileLightIntensity;
    private CheckBox? _tileCollision;
    private CheckBox? _tileNavigation;
    private OptionButton? _advancedResizeAnchor;
    private CheckBox? _migrateResizeLosses;
    private CheckBox? _requireTileEditing;
    private Label? _advancedSummary;

    public string PreviewText => _preview?.Text ?? string.Empty;
    public int DiagnosticCount => _diagnostics?.ItemCount ?? 0;

    public void Initialize(
        Func<ProjectState?> state,
        Func<AreaId?> active,
        Func<AreaId, AreaFoundationProfile, bool> apply,
        Func<AreaId, AreaTileEditRequest, bool> editTiles,
        Action<Window> brand)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _active = active ?? throw new ArgumentNullException(nameof(active));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _editTiles = editTiles ?? throw new ArgumentNullException(nameof(editTiles));
        _brand = brand ?? throw new ArgumentNullException(nameof(brand));
        Title = "Fondation de zone et kits modulaires · OpenRPG Forge";
        MinSize = new Vector2I(1600, 900);
        Size = MinSize;
        Transient = true;
        Exclusive = false;
        Visible = false;
        Build();
        CloseRequested += Hide;
    }

    public void Open()
    {
        Refresh();
        PopupCentered(MinSize);
        _brand?.Invoke(this);
    }

    public bool PrepareForSmokeTest()
    {
        Refresh();
        if (_draft is null || _blueprints.Count == 0 || _entities.Count == 0) return false;
        _kit!.Text = "crypt-modular-kit";
        _terrain!.Text = "flat-stone";
        _lighting!.Text = "torchlit-interior";
        _navigation!.Text = "walk-medium";
        _streaming!.Text = "room-chunks";
        _biome!.Text = "underground-crypt";
        _seed!.Value = 2026;
        _budget!.Value = 768;
        _grid!.Value = 2;
        _rotationMode!.Selected = (int)ModularTileRotationMode.Orthogonal;
        _rotation!.Value = 90;
        _mirror!.Selected = (int)ModularTileMirrorMode.X;
        _variant!.Text = "weathered";
        _entered!.Text = "scripts/ash_warden_phase_two.forge";
        _exited!.Text = string.Empty;
        _enteredRequired!.ButtonPressed = false;
        AddOrReplaceTile();
        Preview();
        return DiagnosticCount == 0 && PreviewText.Contains("0 diagnostic", StringComparison.Ordinal);
    }

    public bool ApplyForSmokeTest()
    {
        var before = _state?.Invoke()?.Revision;
        Apply();
        var after = _state?.Invoke()?.Revision;
        return before is not null && after is not null && after.Value.Value > before.Value.Value;
    }

    public bool PrepareR053ForSmokeTest()
    {
        if (!PrepareForSmokeTest()) return false;
        _tileOperation!.Selected = (int)AreaTileEditOperationKind.Replace;
        _preserveAttachments!.ButtonPressed = true;
        _replacementKit!.Text = "crypt-modular-remap";
        _groupParameters!.Text = "width:int=2,theme:string=crypt";
        _groupWidth!.Value = 2;
        _groupDepth!.Value = 1;
        _groupHeight!.Value = 3;
        _autoFitSeed!.Value = 53053;
        _manualLock!.ButtonPressed = true;
        _tileState!.Text = "torchlit";
        _tileAnimation!.Text = "animations/torch-loop";
        _tileMaterial!.Text = "floor=wet-stone";
        _tileLightColor!.Text = "#ff9858";
        _tileLightIntensity!.Value = 2;
        _tileCollision!.ButtonPressed = true;
        _tileNavigation!.ButtonPressed = true;
        _advancedResizeAnchor!.Selected = (int)AreaResizeAnchor.NorthWest;
        _migrateResizeLosses!.ButtonPressed = true;
        _requireTileEditing!.ButtonPressed = true;
        ConfigureAdvancedDraft();
        Preview();
        return _draft?.TileKit.Editing is not null && DiagnosticCount == 0;
    }

    public void ShowAdvancedForCapture() => _advancedSummary?.GrabFocus();

    private void Build()
    {
        var background = new PanelContainer { AnchorRight = 1, AnchorBottom = 1 };
        background.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("0d0916"),
            BorderColor = new Color("5a3b78"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
        });
        AddChild(background);
        var root = new VBoxContainer
        {
            OffsetLeft = 24,
            OffsetTop = 20,
            OffsetRight = 1576,
            OffsetBottom = 876,
        };
        root.AddThemeConstantOverride("separation", 12);
        AddChild(root);
        var title = new Label { Text = "FONDATION DE ZONE & KITS MODULAIRES", ThemeTypeVariation = "HeaderSmall" };
        title.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(title);
        root.AddChild(new Label
        {
            Text = "Configurer le document canonique, prévisualiser les raccords et lier les événements sans éditer de JSON.",
        });

        var summaries = new HBoxContainer();
        summaries.AddThemeConstantOverride("separation", 10);
        root.AddChild(summaries);
        _documentSummary = Summary(summaries, "DOCUMENT INDÉPENDANT");
        _tileSummary = Summary(summaries, "KIT MODULAIRE");
        var lifecycleSummary = Summary(summaries, "CYCLE DE VIE");
        lifecycleSummary.Text = "Bindings optionnels · API ForgeScript 1.0.0";

        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12);
        root.AddChild(body);

        var wizard = Column(body, "ASSISTANT DE ZONE");
        _kit = Field(wizard, "Kit", "forge-generic");
        _terrain = Field(wizard, "Terrain", "flat");
        _lighting = Field(wizard, "Éclairage", "interior-neutral");
        _navigation = Field(wizard, "Navigation", "walk-medium");
        _streaming = Field(wizard, "Streaming", "single-area");
        _biome = Field(wizard, "Biome", "interior");
        _seed = Spin(wizard, "Graine", int.MinValue, int.MaxValue, 2026);
        _budget = Spin(wizard, "Budget runtime (MiB)", 32, 65536, 512);

        var tile = Column(body, "TUILE · SOCKETS · VARIANTE");
        _grid = Spin(tile, "Grille (m)", 0.25, 64, 1, 0.25);
        _blueprint = Choice(tile, "Blueprint");
        _entity = Choice(tile, "Instance liée");
        _rotationMode = Choice(tile, "Rotation");
        foreach (var mode in Enum.GetValues<ModularTileRotationMode>()) _rotationMode.AddItem(mode.ToString());
        _rotation = Spin(tile, "Angle", -360, 360, 0, 1);
        _mirror = Choice(tile, "Symétrie");
        foreach (var mode in Enum.GetValues<ModularTileMirrorMode>()) _mirror.AddItem(mode.ToString());
        _variant = Field(tile, "Variante sémantique", "default");
        var addTile = new Button { Text = "+ Ajouter / mettre à jour la tuile" };
        addTile.Pressed += AddOrReplaceTile;
        tile.AddChild(addTile);
        var raccord = new Button { Text = "Prévisualiser les raccords" };
        raccord.Pressed += Preview;
        tile.AddChild(raccord);

        var lifecycle = Column(body, "ENTRÉE / SORTIE & VALIDATION");
        _entered = Field(lifecycle, "Script area.entered", "scripts/area_entered.forge");
        _enteredRequired = new CheckBox { Text = "Entrée requise avant build" };
        lifecycle.AddChild(_enteredRequired);
        _exited = Field(lifecycle, "Script area.exited", "scripts/area_exited.forge");
        _exitedRequired = new CheckBox { Text = "Sortie requise avant build" };
        lifecycle.AddChild(_exitedRequired);
        _preview = new Label
        {
            Text = "État vide · sélectionnez une zone pour calculer l’aperçu.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 72),
        };
        lifecycle.AddChild(_preview);
        _diagnostics = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 130) };
        lifecycle.AddChild(_diagnostics);
        var preview = new Button { Text = "Prévisualiser · 0 écriture" };
        preview.Pressed += Preview;
        lifecycle.AddChild(preview);
        var apply = new Button { Text = "Appliquer par transaction" };
        apply.AddThemeColorOverride("font_color", Colors.White);
        apply.Pressed += Apply;
        lifecycle.AddChild(apply);

        var advanced = Column(body, "ÉDITION AVANCÉE R053");
        _advancedSummary = new Label
        {
            Text = "État vide · configurez groupes, outils, auto-tiling et propriétés.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        advanced.AddChild(_advancedSummary);
        _tileOperation = Choice(advanced, "Outil");
        foreach (var operation in Enum.GetValues<AreaTileEditOperationKind>())
            _tileOperation.AddItem(operation.ToString());
        _preserveAttachments = new CheckBox { Text = "Préserver les objets attachés", ButtonPressed = true };
        advanced.AddChild(_preserveAttachments);
        _replacementKit = Field(advanced, "Kit de remap", "forge-generic-remap");
        _groupParameters = Field(advanced, "Paramètres du groupe", "width:int=2");
        var footprint = new GridContainer { Columns = 3 };
        advanced.AddChild(footprint);
        _groupWidth = CompactSpin(footprint, "Empreinte X", 1, 1024, 1);
        _groupDepth = CompactSpin(footprint, "Empreinte Z", 1, 1024, 1);
        _groupHeight = CompactSpin(footprint, "Hauteur", .1, 1024, 3, .1);
        _autoFitSeed = Spin(advanced, "Seed auto-tiling", int.MinValue, int.MaxValue, 2026);
        _manualLock = new CheckBox { Text = "Verrouillage manuel", ButtonPressed = true };
        advanced.AddChild(_manualLock);
        _tileState = Field(advanced, "État de tuile", "default");
        _tileAnimation = Field(advanced, "Animation en boucle", "animations/idle-loop");
        _tileMaterial = Field(advanced, "Matériaux", "surface=default");
        _tileLightColor = Field(advanced, "Lumière", "#ff9858");
        _tileLightIntensity = Spin(advanced, "Intensité", 0, 100000, 1, .1);
        var collisionNavigation = new HBoxContainer();
        _tileCollision = new CheckBox { Text = "Collision", ButtonPressed = true };
        _tileNavigation = new CheckBox { Text = "Navigation", ButtonPressed = true };
        collisionNavigation.AddChild(_tileCollision);
        collisionNavigation.AddChild(_tileNavigation);
        advanced.AddChild(collisionNavigation);
        _advancedResizeAnchor = Choice(advanced, "Origine redimensionnement");
        foreach (var anchor in Enum.GetValues<AreaResizeAnchor>()) _advancedResizeAnchor.AddItem(anchor.ToString());
        _migrateResizeLosses = new CheckBox { Text = "Repositionner les pertes", ButtonPressed = true };
        advanced.AddChild(_migrateResizeLosses);
        _requireTileEditing = new CheckBox { Text = "Métadonnées requises au runtime" };
        advanced.AddChild(_requireTileEditing);
        var advancedPreview = new Button { Text = "Prévisualiser le contrat avancé" };
        advancedPreview.Pressed += () => { ConfigureAdvancedDraft(); Preview(); };
        advanced.AddChild(advancedPreview);
        var advancedApply = new Button { Text = "Appliquer config & outil" };
        advancedApply.Pressed += ApplyAdvancedOperation;
        advanced.AddChild(advancedApply);
    }

    private void Refresh()
    {
        var state = _state?.Invoke();
        var areaId = _active?.Invoke();
        var area = state?.Project.Areas.SingleOrDefault(item => item.Id == areaId);
        _blueprints.Clear();
        _entities.Clear();
        _blueprint?.Clear();
        _entity?.Clear();
        if (state is null || area is null)
        {
            _draft = null;
            _documentSummary!.Text = "Aucune zone active";
            _tileSummary!.Text = "État vide · aucun kit";
            _preview!.Text = "État vide · ouvrez ou créez une zone.";
            return;
        }

        _draft = area.Foundation;
        foreach (var blueprint in state.Project.Blueprints.OrderBy(item => item.Slug, StringComparer.Ordinal))
        {
            _blueprints.Add(blueprint.Id);
            _blueprint!.AddItem($"{blueprint.Slug} · {blueprint.Kind}");
        }
        foreach (var entity in area.Entities.OrderBy(item => item.Id.Value))
        {
            _entities.Add(entity.Id);
            _entity!.AddItem(entity.Id.ToString());
        }
        LoadDraft(area);
    }

    private void LoadDraft(AreaDefinition area)
    {
        var profile = area.Foundation;
        _kit!.Text = profile.Creation.KitKey;
        _terrain!.Text = profile.Creation.TerrainProfile;
        _lighting!.Text = profile.Creation.LightingProfile;
        _navigation!.Text = profile.Creation.NavigationProfile;
        _streaming!.Text = profile.Creation.StreamingProfile;
        _biome!.Text = profile.Creation.Biome;
        _seed!.Value = profile.Creation.Seed;
        _budget!.Value = profile.Creation.RuntimeBudgetMegabytes;
        _grid!.Value = profile.TileKit.GridSizeMeters;
        _entered!.Text = profile.Lifecycle.Entered?.ScriptPath ?? string.Empty;
        _exited!.Text = profile.Lifecycle.Exited?.ScriptPath ?? string.Empty;
        _enteredRequired!.ButtonPressed = profile.Lifecycle.Entered?.Required ?? false;
        _exitedRequired!.ButtonPressed = profile.Lifecycle.Exited?.Required ?? false;
        _documentSummary!.Text = $"{area.DisplayName} · UUID {profile.Id:D} · schéma v{profile.SchemaVersion}";
        _tileSummary!.Text = $"{profile.TileKit.Definitions.Count} définition(s) · {profile.TileKit.Placements.Count} placement(s) · " +
                             $"{profile.TileKit.ConnectionRules.Count} règle(s) · grille {profile.TileKit.GridSizeMeters:0.##} m";
        _preview!.Text = "Prêt · prévisualisez avant application.";
        _diagnostics!.Clear();
        var editing = profile.TileKit.Editing;
        _advancedSummary!.Text = editing is null
            ? "État vide · aucune configuration avancée."
            : $"{editing.Groups.Count} groupe(s) paramétrique(s) · {editing.Properties.Count} propriété(s) · " +
              $"{editing.AutoFit.ManualLockedPlacementIds.Count} verrou(s) · seed {editing.AutoFit.Seed}";
        _tileOperation!.Selected = (int)(editing?.Tools.Operation ?? AreaTileEditOperationKind.SelectByType);
        _preserveAttachments!.ButtonPressed = editing?.Tools.PreserveAttachedObjects ?? true;
        _replacementKit!.Text = editing?.Tools.ReplacementKitKey ?? string.Empty;
        _autoFitSeed!.Value = editing?.AutoFit.Seed ?? profile.Creation.Seed;
        _manualLock!.ButtonPressed = editing?.AutoFit.ManualLockedPlacementIds.Count > 0;
        _advancedResizeAnchor!.Selected = Array.IndexOf(Enum.GetNames<AreaResizeAnchor>(),
            editing?.Resize.Anchor ?? AreaResizeAnchor.Center.ToString());
        _migrateResizeLosses!.ButtonPressed = editing?.Resize.MigrateInstances ?? true;
        _requireTileEditing!.ButtonPressed = profile.RequireTileEditingRuntimeMetadata;
    }

    private void AddOrReplaceTile()
    {
        if (_draft is null || _blueprint is null || _entity is null || _blueprint.Selected < 0 ||
            _entity.Selected < 0 || _blueprint.Selected >= _blueprints.Count || _entity.Selected >= _entities.Count)
        {
            _preview!.Text = "État vide · un blueprint et une instance de la zone sont requis.";
            return;
        }

        var definitionId = Guid.NewGuid();
        var placementId = Guid.NewGuid();
        var socket = new ModularTileSocket(Guid.NewGuid(), "edge", "modular-edge", TransformValue.Identity,
            ModularTileSocketDirection.East);
        var variantKey = string.IsNullOrWhiteSpace(_variant!.Text) ? "default" : _variant.Text.Trim();
        var variant = new ModularTileVariant(Guid.NewGuid(), variantKey, variantKey, _blueprints[_blueprint.Selected]);
        var mode = (ModularTileRotationMode)_rotationMode!.Selected;
        var mirror = (ModularTileMirrorMode)_mirror!.Selected;
        var definition = new ModularTileDefinition(definitionId, $"tile-{_draft.TileKit.Definitions.Count + 1}",
            _blueprints[_blueprint.Selected], mode, true, true, [socket], [variant]);
        var placement = new ModularTilePlacement(placementId, definitionId, _entities[_entity.Selected],
            _draft.TileKit.Placements.Count, 0, (float)_rotation!.Value, mirror, variantKey, null);
        var definitions = _draft.TileKit.Definitions.Append(definition).ToArray();
        var placements = _draft.TileKit.Placements.Append(placement).ToArray();
        var rules = _draft.TileKit.ConnectionRules.Count == 0
            ? [new ModularTileConnectionRule(Guid.NewGuid(), "modular-edge", "modular-edge", true)]
            : _draft.TileKit.ConnectionRules;
        var group = new ModularTileGroup(Guid.NewGuid(), "Groupe principal", placements.Select(item => item.Id).ToArray());
        var brush = new ModularTileBrush(Guid.NewGuid(), "Pinceau principal", definitions.Select(item => item.Id).ToArray(),
            _draft.Creation.Seed);
        var kit = new ModularTileKitProfile(_draft.TileKit.Id, 1, "author:area-foundation-ui", (float)_grid!.Value,
            definitions, rules, placements, [group], [brush]);
        _draft = new(_draft.Id, 1, "author:area-foundation-ui", ReadCreation(), kit, ReadLifecycle(),
            _draft.Volumes, _draft.Dependencies, _draft.RequireTileEditingRuntimeMetadata);
        _tileSummary!.Text = $"{definitions.Length} définition(s) · {placements.Length} placement(s) · sockets/règles/groupes/pinceau prêts";
    }

    private void Preview()
    {
        var area = ActiveArea();
        if (area is null || _draft is null) return;
        _draft = new(_draft.Id, 1, "author:area-foundation-ui", ReadCreation(),
            new(_draft.TileKit.Id, 1, "author:area-foundation-ui", (float)_grid!.Value,
                _draft.TileKit.Definitions, _draft.TileKit.ConnectionRules, _draft.TileKit.Placements,
                _draft.TileKit.Groups, _draft.TileKit.Brushes, _draft.TileKit.Editing), ReadLifecycle(),
            _draft.Volumes, _draft.Dependencies, _draft.RequireTileEditingRuntimeMetadata);
        var diagnostics = AreaFoundationValidator.Validate(area, _draft);
        _diagnostics!.Clear();
        foreach (var diagnostic in diagnostics) _diagnostics.AddItem($"{diagnostic.Code} · {diagnostic.Path}");
        var connection = _draft.TileKit.Placements.Count == 0
            ? null
            : ModularTileConnectionPreviewService.Preview(_draft.TileKit, _draft.TileKit.Placements[^1].Id);
        _preview!.Text = $"Aperçu · {diagnostics.Count} diagnostic(s) · " +
                         $"{connection?.CompatibleSocketCount ?? 0} socket(s) raccordé(s) · " +
                         $"{connection?.OpenSocketCount ?? 0} ouvert(s) · 0 écriture";
    }

    private void Apply()
    {
        Preview();
        var area = ActiveArea();
        if (area is null || _draft is null || DiagnosticCount > 0) return;
        _preview!.Text = _apply?.Invoke(area.Id, _draft) == true
            ? "✓ Fondation appliquée par commande · undo/redo disponible"
            : "✕ Fondation refusée · consultez les diagnostics";
        Refresh();
    }

    private void ApplyAdvancedOperation()
    {
        ConfigureAdvancedDraft();
        Apply();
        var area = ActiveArea();
        var placement = area?.Foundation.TileKit.Placements.LastOrDefault();
        if (area is null || placement is null || area.Foundation.TileKit.Editing is not { } editing) return;
        var request = new AreaTileEditRequest(editing.Tools.Operation, [placement.Id],
            editing.Tools.ReplacementDefinitionId, editing.Tools.ReplacementKitKey,
            editing.Tools.PreserveAttachedObjects, editing.Tools.SelectionType);
        _advancedSummary!.Text = _editTiles?.Invoke(area.Id, request) == true
            ? "✓ Config et outil appliqués par la même commande canonique"
            : "✕ Outil refusé · consultez les diagnostics";
        Refresh();
    }

    private void ConfigureAdvancedDraft()
    {
        if (_draft is null || _draft.TileKit.Placements.Count == 0 || _draft.TileKit.Groups.Count == 0) return;
        var kit = _draft.TileKit;
        var placement = kit.Placements[^1];
        var group = kit.Groups[^1];
        var definition = kit.Definitions.Single(item => item.Id == placement.DefinitionId);
        var parameters = ParsePairs(_groupParameters!.Text, includeType: true)
            .Select(item => new EditableTileGroupParameter(item.Key, item.Type, item.Value)).ToArray();
        var groupProfile = new EditableTileGroupProfile(Guid.NewGuid(), group.Id, parameters,
            new((int)_groupWidth!.Value, (int)_groupDepth!.Value, (float)_groupHeight!.Value),
            [new(Guid.NewGuid(), "entry", definition.Sockets.FirstOrDefault()?.Type ?? "modular-edge",
                0, 0, ModularTileSocketDirection.East)],
            [new("default", group.PlacementIds.Select(id => kit.Placements.Single(p => p.Id == id).DefinitionId)
                .Distinct().ToArray())], true);
        var operation = (AreaTileEditOperationKind)_tileOperation!.Selected;
        var tools = new AreaTileToolProfile(operation, definition.Id, _replacementKit!.Text.Trim(), definition.Id,
            definition.Key, _preserveAttachments!.ButtonPressed);
        var autoFit = new AreaTileAutoFitProfile((int)_autoFitSeed!.Value,
            _manualLock!.ButtonPressed ? [placement.Id] : [], true);
        var material = ParsePairs(_tileMaterial!.Text, includeType: false)
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        var property = new TileInstancePropertyProfile(Guid.NewGuid(), placement.Id, _tileState!.Text.Trim(),
            _tileAnimation!.Text.Trim(), true, material,
            new(true, _tileLightColor!.Text.Trim(), (float)_tileLightIntensity!.Value, 8),
            _tileCollision!.ButtonPressed, _tileNavigation!.ButtonPressed,
            definition.Sockets.Select(item => item.Name).ToArray(),
            new Dictionary<string, string> { ["state"] = _tileState.Text.Trim() });
        var resize = new AreaTileResizeProfile(1, 1, 4096, 4096,
            ((AreaResizeAnchor)_advancedResizeAnchor!.Selected).ToString(),
            _migrateResizeLosses!.ButtonPressed, []);
        var editing = new AreaTileEditingProfile(kit.Editing?.Id ?? Guid.NewGuid(), 1,
            "author:area-tile-editing-ui", [groupProfile], tools, autoFit, [property], resize);
        var updatedKit = new ModularTileKitProfile(kit.Id, kit.SchemaVersion, "author:area-tile-editing-ui",
            kit.GridSizeMeters, kit.Definitions, kit.ConnectionRules, kit.Placements, kit.Groups, kit.Brushes,
            editing);
        _draft = new(_draft.Id, _draft.SchemaVersion, "author:area-tile-editing-ui", ReadCreation(), updatedKit,
            ReadLifecycle(), _draft.Volumes, _draft.Dependencies, _requireTileEditing!.ButtonPressed);
        _advancedSummary!.Text = "Aperçu prêt · groupe paramétrique, outil, auto-tiling, propriétés et resize · 0 écriture";
    }

    private static IReadOnlyList<(string Key, string Type, string Value)> ParsePairs(string source, bool includeType)
    {
        return source.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token =>
            {
                var assignment = token.Split('=', 2, StringSplitOptions.TrimEntries);
                var declaration = assignment[0].Split(':', 2, StringSplitOptions.TrimEntries);
                return (Key: declaration[0],
                    Type: includeType && declaration.Length > 1 ? declaration[1] : "string",
                    Value: assignment.Length > 1 ? assignment[1] : string.Empty);
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Key)).ToArray();
    }

    private AreaDefinition? ActiveArea()
    {
        var id = _active?.Invoke();
        return _state?.Invoke()?.Project.Areas.SingleOrDefault(item => item.Id == id);
    }

    private AreaCreationSettings ReadCreation() => new("playable", _kit!.Text.Trim(), _terrain!.Text.Trim(),
        _lighting!.Text.Trim(), _navigation!.Text.Trim(), _streaming!.Text.Trim(), _biome!.Text.Trim(),
        (int)_seed!.Value, (int)_budget!.Value);

    private AreaLifecycleBindingProfile ReadLifecycle() => new(
        Binding("area.entered", _entered!.Text, _enteredRequired!.ButtonPressed, _draft?.Lifecycle.Entered?.Id),
        Binding("area.exited", _exited!.Text, _exitedRequired!.ButtonPressed, _draft?.Lifecycle.Exited?.Id));

    private static AreaLifecycleScriptBinding? Binding(string eventName, string path, bool required, Guid? existingId)
    {
        if (string.IsNullOrWhiteSpace(path) && !required) return null;
        return new(existingId ?? Guid.NewGuid(), eventName, path.Trim(), "1.0.0", required,
            "author:area-foundation-ui");
    }

    private static VBoxContainer Column(Container parent, string title)
    {
        var column = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(370, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        column.AddThemeConstantOverride("separation", 7);
        var heading = new Label { Text = title };
        heading.AddThemeColorOverride("font_color", new Color("ffb07c"));
        column.AddChild(heading);
        parent.AddChild(column);
        return column;
    }

    private static Label Summary(Container parent, string title)
    {
        var card = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        card.AddChild(new Label { Text = title });
        var value = new Label { Text = "État vide", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        card.AddChild(value);
        parent.AddChild(card);
        return value;
    }

    private static LineEdit Field(Container parent, string label, string value)
    {
        parent.AddChild(new Label { Text = label });
        var field = new LineEdit { Text = value };
        parent.AddChild(field);
        return field;
    }

    private static SpinBox Spin(Container parent, string label, double min, double max, double value, double step = 1)
    {
        parent.AddChild(new Label { Text = label });
        var field = new SpinBox { MinValue = min, MaxValue = max, Value = value, Step = step };
        parent.AddChild(field);
        return field;
    }

    private static SpinBox CompactSpin(Container parent, string label, double min, double max, double value,
        double step = 1)
    {
        var column = new VBoxContainer();
        column.AddChild(new Label { Text = label });
        var field = new SpinBox { MinValue = min, MaxValue = max, Value = value, Step = step };
        column.AddChild(field);
        parent.AddChild(column);
        return field;
    }

    private static OptionButton Choice(Container parent, string label)
    {
        parent.AddChild(new Label { Text = label });
        var choice = new OptionButton();
        parent.AddChild(choice);
        return choice;
    }
}
#endif
