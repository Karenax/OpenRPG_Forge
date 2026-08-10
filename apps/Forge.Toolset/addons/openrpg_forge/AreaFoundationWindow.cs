#if TOOLS
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

    public string PreviewText => _preview?.Text ?? string.Empty;
    public int DiagnosticCount => _diagnostics?.ItemCount ?? 0;

    public void Initialize(
        Func<ProjectState?> state,
        Func<AreaId?> active,
        Func<AreaId, AreaFoundationProfile, bool> apply,
        Action<Window> brand)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _active = active ?? throw new ArgumentNullException(nameof(active));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _brand = brand ?? throw new ArgumentNullException(nameof(brand));
        Title = "Fondation de zone et kits modulaires · OpenRPG Forge";
        MinSize = new Vector2I(1280, 840);
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
            OffsetRight = 1256,
            OffsetBottom = 816,
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
            _draft.Volumes, _draft.Dependencies);
        _tileSummary!.Text = $"{definitions.Length} définition(s) · {placements.Length} placement(s) · sockets/règles/groupes/pinceau prêts";
    }

    private void Preview()
    {
        var area = ActiveArea();
        if (area is null || _draft is null) return;
        _draft = new(_draft.Id, 1, "author:area-foundation-ui", ReadCreation(),
            new(_draft.TileKit.Id, 1, "author:area-foundation-ui", (float)_grid!.Value,
                _draft.TileKit.Definitions, _draft.TileKit.ConnectionRules, _draft.TileKit.Placements,
                _draft.TileKit.Groups, _draft.TileKit.Brushes), ReadLifecycle(), _draft.Volumes, _draft.Dependencies);
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
            CustomMinimumSize = new Vector2(385, 0),
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

    private static OptionButton Choice(Container parent, string label)
    {
        parent.AddChild(new Label { Text = label });
        var choice = new OptionButton();
        parent.AddChild(choice);
        return choice;
    }
}
#endif
