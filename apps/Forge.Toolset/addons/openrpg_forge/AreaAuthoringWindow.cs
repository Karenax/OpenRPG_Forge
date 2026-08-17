#if TOOLS
using Forge.Application.Areas;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;
using Godot;

namespace Forge.Toolset.Editor;

public partial class AreaAuthoringWindow : Window
{
    private Func<ProjectState?>? _state;
    private Func<AreaId?>? _active;
    private Func<AreaId, AreaAuthoringProfile, bool>? _apply;
    private Action<IReadOnlyList<EntityId>>? _selectEntities;
    private Action<Window>? _brand;
    private AreaAuthoringProfile? _draft;
    private TabContainer? _tabs;
    private LineEdit? _eventName;
    private LineEdit? _eventSchema;
    private LineEdit? _eventScript;
    private LineEdit? _eventSubscriptions;
    private CheckBox? _fog;
    private CheckBox? _map2D;
    private CheckBox? _map3D;
    private OptionButton? _explorationScope;
    private SpinBox? _explorationCell;
    private SpinBox? _floors;
    private LineEdit? _pin;
    private SpinBox? _pinX;
    private SpinBox? _pinZ;
    private LineEdit? _snapshotName;
    private LineEdit? _snapshotTrigger;
    private SpinBox? _snapshotTransition;
    private LineEdit? _roomName;
    private LineEdit? _occluderAsset;
    private SpinBox? _cellSize;
    private SpinBox? _streamingDistance;
    private LineEdit? _lodDistances;
    private SpinBox? _lodTriangles;
    private SpinBox? _poppingTolerance;
    private LineEdit? _lodAsset;
    private CheckBox? _runtimeRequired;
    private LineEdit? _layer;
    private LineEdit? _owner;
    private CheckBox? _visible;
    private CheckBox? _locked;
    private CheckBox? _layerExport;
    private CheckBox? _layerRuntimeRequired;
    private OptionButton? _layerParent;
    private OptionButton? _layerEntity;
    private Tree? _layerTree;
    private Label? _layerPreview;
    private Guid? _draggedLayerId;
    private ItemList? _summary;
    private Label? _status;
    private string? _capturePath;
    private double _captureDelay;
    private bool _captureOpened;
    private bool _captureR056;

    public void Initialize(
        Func<ProjectState?> state,
        Func<AreaId?> active,
        Func<AreaId, AreaAuthoringProfile, bool> apply,
        Action<IReadOnlyList<EntityId>> selectEntities,
        Action<Window> brand)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _active = active ?? throw new ArgumentNullException(nameof(active));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _selectEntities = selectEntities ?? throw new ArgumentNullException(nameof(selectEntities));
        _brand = brand ?? throw new ArgumentNullException(nameof(brand));
        Title = "Expérience de zone · OpenRPG Forge";
        MinSize = new(1240, 760);
        Size = new(1320, 820);
        Transient = true;
        Visible = false;
        Build();
        var capture = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--r055-area-experience-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r056-area-layers-ui-capture=", StringComparison.Ordinal));
        if (capture is not null)
        {
            _captureR056 = capture.StartsWith("--r056-area-layers-ui-capture=", StringComparison.Ordinal);
            var prefix = _captureR056
                ? "--r056-area-layers-ui-capture="
                : "--r055-area-experience-ui-capture=";
            _capturePath = capture[prefix.Length..];
            SetProcess(true);
        }
    }

    public override void _Process(double delta)
    {
        if (_capturePath is null) return;
        _captureDelay += delta;
        if (!_captureOpened && _captureDelay >= 1)
        {
            Open();
            _captureOpened = true;
            _tabs!.CurrentTab = _captureR056 ? 3 : 2;
            if (_captureR056) PrepareR056ForSmokeTest();
            else PrepareR055ForSmokeTest();
            return;
        }
        if (!_captureOpened || _captureDelay < 2.5) return;
        var destination = Path.GetFullPath(_capturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(destination);
        var captureMarker = _captureR056
            ? "OPENRPG_FORGE_R056_AREA_LAYERS_UI_CAPTURE"
            : "OPENRPG_FORGE_R055_AREA_EXPERIENCE_UI_CAPTURE";
        GD.Print($"{captureMarker}_{(error == Error.Ok ? "PASS" : "FAILED")} " +
                 $"path={destination} size={image.GetWidth()}x{image.GetHeight()}");
        _capturePath = null;
        SetProcess(false);
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    public void Open()
    {
        Refresh();
        PopupCentered(Size);
        _brand?.Invoke(this);
    }

    internal bool ApplyForSmokeTest() => Save();

    internal bool PrepareR055ForSmokeTest()
    {
        var id = _active?.Invoke();
        var area = _state?.Invoke()?.Project.Areas.SingleOrDefault(item => item.Id == id);
        if (area is null) return false;
        var roomA = new AreaVisibilityRoomProfile(Guid.Parse("a0551000-0000-4000-8000-000000000001"),
            "Crypte", TransformValue.Identity, false, 96, new Dictionary<string, string>());
        var roomB = new AreaVisibilityRoomProfile(Guid.Parse("a0551000-0000-4000-8000-000000000002"),
            "Sanctuaire", new(new(16, 0, 0), QuaternionValue.Identity, new(12, 8, 12)), false, 96,
            new Dictionary<string, string>());
        var visibility = new AreaVisibilityAuthoringProfile([roomA, roomB],
            [new(Guid.Parse("a0551000-0000-4000-8000-000000000003"), "Arche du sanctuaire", roomA.Id,
                roomB.Id, new(new(8, 2, 0), QuaternionValue.Identity, new(3, 4, .5f)),
                AreaVisibilityPortalMode.Conditional, "quest.ashes.entered", true)],
            [new(Guid.Parse("a0551000-0000-4000-8000-000000000004"), "Mur d'occlusion",
                new(new(4, 2, 0), QuaternionValue.Identity, new(8, 4, .5f)), false,
                "asset:crypt-wall", new Dictionary<string, string>())], true, true, true, 16, 128);
        var lod = new AreaLodAuthoringProfile(AreaLodSourceMode.Hybrid, [20, 50, 100],
            1_500_000, 96, 24, .15f, true, true,
            [new(Guid.Parse("a0551000-0000-4000-8000-000000000005"), "asset:crypt-column",
                AreaLodSourceMode.Generated, [1, .5f, .2f], [12000, 6000, 2400], .2f,
                "author:r055-smoke")]);
        var experience = new AreaExperienceAuthoringProfile(
            Guid.Parse("a0551000-0000-4000-8000-000000000006"), 1, "author:r055-smoke",
            [new(Guid.Parse("a0551000-0000-4000-8000-000000000007"), "Combat crypte",
                new Dictionary<string, float> { ["music"] = .8f, ["ambience"] = .55f }, .75f,
                $"area.{area.Slug}.combat", "author:r055-smoke")], visibility, lod, true);
        var eventName = $"area.{area.Slug}.alarm";
        var map = new AreaMapProfile(true, true, "hybrid-2d-3d", 2,
            [new(Guid.Parse("a0551000-0000-4000-8000-000000000008"), "Sanctuaire", 16, 0, 0, false)],
            Guid.Parse("a0551000-0000-4000-8000-000000000009"), 1, "author:r055-smoke", true, true,
            AreaMapExplorationScope.Party, 8, "area.map.reveal");
        _draft = new(AreaAuthoringProfile.CurrentSchemaVersion,
            [new(Guid.Parse("a0551000-0000-4000-8000-000000000010"), eventName,
                "schema://area/alarm/v1", "scripts/ash_warden_death.forge", 1,
                ["main"], true, "author:r055-smoke", AreaUserEvent.CurrentSchemaVersion,
                new Dictionary<string, string> { ["payload"] = "string" },
                AreaUserEvent.CurrentForgeScriptApiVersion)],
            map, area.Authoring.Layers, experience);
        Populate(_draft);
        _status!.Text = "✓ R055 prêt · aperçu 2D/3D, cellules, portails, occluder et LOD mesurable";
        return true;
    }

    internal bool PrepareR056ForSmokeTest()
    {
        var area = CurrentArea();
        if (area is null) return false;
        var root = Guid.Parse("a0561000-0000-4000-8000-000000000001");
        var child = Guid.Parse("a0561000-0000-4000-8000-000000000002");
        var privateLayer = Guid.Parse("a0561000-0000-4000-8000-000000000003");
        var entityIds = area.Entities.Take(2).Select(item => item.Id).ToArray();
        _draft = new(area.Authoring.SchemaVersion, area.Authoring.UserEvents, area.Authoring.Map,
        [
            new(root, "Architecture", null, true, false, "level-design", [], provenance: "author:r056-smoke"),
            new(child, "Rencontre", root, true, false, "gameplay", entityIds,
                provenance: "author:r056-smoke"),
            new(privateLayer, "Notes privées", root, false, true, "gm", [],
                provenance: "author:r056-smoke", exportEnabled: false),
        ], area.Authoring.Experience, true);
        Populate(_draft);
        var selected = _layerTree!.GetRoot()?.GetFirstChild()?.GetFirstChild();
        if (selected is not null) _layerTree.SetSelected(selected, 0);
        _status!.Text = "✓ R056 prêt · arbre imbriqué, ownership, sélection et export partiel";
        return true;
    }

    private void Build()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 20);
        AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);
        root.AddChild(new Label { Text = "EXPÉRIENCE DE ZONE", ThemeTypeVariation = "HeaderSmall" });
        root.AddChild(new Label
        {
            Text = "Audio adaptatif, événements ForgeScript, carte explorée, visibilité et LOD dans une transaction canonique.",
        });
        _tabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        root.AddChild(_tabs);
        BuildEventsMapTab(_tabs);
        BuildAudioLayersTab(_tabs);
        BuildVisibilityLodTab(_tabs);
        BuildLayersTab(_tabs);
        _status = new Label { Text = "Sélectionnez une zone pour commencer." };
        root.AddChild(_status);
        var actions = new HBoxContainer();
        actions.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var save = new Button { Text = "Appliquer la configuration", CustomMinimumSize = new(240, 38) };
        save.Pressed += () => Save();
        actions.AddChild(save);
        root.AddChild(actions);
    }

    private void BuildEventsMapTab(TabContainer tabs)
    {
        var body = Tab(tabs, "Événements & carte");
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 16);
        body.AddChild(columns);
        var events = Column(columns, "ÉVÉNEMENT DE ZONE");
        _eventName = Field("area.zone.alarme");
        events.AddChild(Labeled("Nom namespacé", _eventName));
        _eventSchema = Field("schema://area/event/v1");
        events.AddChild(Labeled("Schéma de payload", _eventSchema));
        _eventScript = Field("scripts/area_event.forge");
        events.AddChild(Labeled("Script ForgeScript", _eventScript));
        _eventSubscriptions = Field("main");
        events.AddChild(Labeled("Abonnements", _eventSubscriptions));
        var addEvent = new Button { Text = "+ Ajouter l'événement" };
        addEvent.Pressed += AddEvent;
        events.AddChild(addEvent);

        var map = Column(columns, "CARTE & EXPLORATION");
        _fog = new CheckBox { Text = "Brouillard de guerre", ButtonPressed = true };
        map.AddChild(_fog);
        _map2D = new CheckBox { Text = "Générer la carte 2D", ButtonPressed = true };
        map.AddChild(_map2D);
        _map3D = new CheckBox { Text = "Générer la carte 3D", ButtonPressed = true };
        map.AddChild(_map3D);
        _explorationScope = new OptionButton();
        foreach (var value in Enum.GetValues<AreaMapExplorationScope>()) _explorationScope.AddItem(value.ToString());
        _explorationScope.Selected = (int)AreaMapExplorationScope.Party;
        map.AddChild(Labeled("Exploration", _explorationScope));
        _explorationCell = Spin(1, 256, 8, 1);
        map.AddChild(Labeled("Taille cellule", _explorationCell));
        _floors = Spin(1, 32, 1, 1);
        map.AddChild(Labeled("Étages", _floors));

        var pins = Column(columns, "REPÈRES & CALQUES");
        _pin = Field("Nom du repère");
        pins.AddChild(Labeled("Repère", _pin));
        _pinX = Spin(-4096, 4096, 0, 1);
        pins.AddChild(Labeled("Position X", _pinX));
        _pinZ = Spin(-4096, 4096, 0, 1);
        pins.AddChild(Labeled("Position Z", _pinZ));
        var addPin = new Button { Text = "+ Ajouter le repère" };
        addPin.Pressed += AddPin;
        pins.AddChild(addPin);
    }

    private void BuildAudioLayersTab(TabContainer tabs)
    {
        var body = Tab(tabs, "Audio adaptatif");
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 20);
        body.AddChild(columns);
        var authoring = Column(columns, "SNAPSHOT DE MIXAGE");
        _snapshotName = Field("Exploration nocturne");
        authoring.AddChild(Labeled("Nom", _snapshotName));
        _snapshotTrigger = Field("area.zone.night");
        authoring.AddChild(Labeled("Événement déclencheur", _snapshotTrigger));
        _snapshotTransition = Spin(0, 60, 1, .1);
        authoring.AddChild(Labeled("Transition (s)", _snapshotTransition));
        var add = new Button { Text = "+ Ajouter snapshot" };
        add.Pressed += AddSnapshot;
        authoring.AddChild(add);
        authoring.AddChild(new Label
        {
            Text = "Les couches jour/nuit/combat, ambiances et zones de réverbération utilisent le profil environnement existant.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        _summary = new ItemList { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        columns.AddChild(_summary);
    }

    private void BuildVisibilityLodTab(TabContainer tabs)
    {
        var body = Tab(tabs, "Visibilité & LOD R055");
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 16);
        body.AddChild(columns);
        var visibility = Column(columns, "ROOMS, PORTAILS & OCCLUDERS");
        _roomName = Field("Salle principale");
        visibility.AddChild(Labeled("Nom de room", _roomName));
        _occluderAsset = Field("asset:wall-occluder");
        visibility.AddChild(Labeled("Mesh occluder", _occluderAsset));
        _cellSize = Spin(1, 512, 16, 1);
        visibility.AddChild(Labeled("Taille cellule", _cellSize));
        _streamingDistance = Spin(8, 4096, 128, 8);
        visibility.AddChild(Labeled("Distance streaming", _streamingDistance));
        var applyVisibility = new Button { Text = "Préparer rooms + portail + occluder" };
        applyVisibility.Pressed += ApplyVisibilityDraft;
        visibility.AddChild(applyVisibility);

        var lod = Column(columns, "POLITIQUE LOD / HLOD");
        _lodDistances = Field("20, 50, 100");
        lod.AddChild(Labeled("Seuils distance", _lodDistances));
        _lodTriangles = Spin(1_000, 20_000_000, 1_500_000, 10_000);
        lod.AddChild(Labeled("Budget triangles", _lodTriangles));
        _poppingTolerance = Spin(0, 1, .15, .01);
        lod.AddChild(Labeled("Tolérance popping", _poppingTolerance));
        _lodAsset = Field("asset:architecture-column");
        lod.AddChild(Labeled("Override asset", _lodAsset));
        var applyLod = new Button { Text = "Appliquer la politique mesurable" };
        applyLod.Pressed += ApplyLodDraft;
        lod.AddChild(applyLod);
        _runtimeRequired = new CheckBox { Text = "Métadonnées runtime requises" };
        lod.AddChild(_runtimeRequired);

        var preview = Column(columns, "APERÇU & GOUVERNANCE");
        preview.AddChild(new Label
        {
            Text = "Occlusion GPU et visibilité par cellules\nRooms et portails conditionnels\nStreaming par distance\nLOD importé/généré/hybride\nBudgets, cross-fade et test de popping\nFallback optionnel ou diagnostic requis",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        preview.AddChild(new Label
        {
            Text = "Même commande : UI · ForgeScript · MCP\nDry-run · diff sémantique · undo/redo\nPersistance et préflight avant package",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
    }

    private void BuildLayersTab(TabContainer tabs)
    {
        var body = Tab(tabs, "Calques & groupes R056");
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 16);
        body.AddChild(columns);
        var authoring = Column(columns, "ORGANISATION");
        _layer = Field("Architecture");
        authoring.AddChild(Labeled("Nom du calque", _layer));
        _owner = Field("level-design");
        authoring.AddChild(Labeled("Propriétaire", _owner));
        _layerParent = new OptionButton();
        authoring.AddChild(Labeled("Parent", _layerParent));
        _layerEntity = new OptionButton();
        authoring.AddChild(Labeled("Entité assignée", _layerEntity));
        _visible = new CheckBox { Text = "Visible", ButtonPressed = true };
        authoring.AddChild(_visible);
        _locked = new CheckBox { Text = "Verrouillé" };
        authoring.AddChild(_locked);
        _layerExport = new CheckBox { Text = "Inclure dans l’export partiel", ButtonPressed = true };
        authoring.AddChild(_layerExport);
        _layerRuntimeRequired = new CheckBox { Text = "Métadonnées de calques requises au runtime" };
        authoring.AddChild(_layerRuntimeRequired);
        var add = new Button { Text = "+ Ajouter au groupe" };
        add.Pressed += AddLayer;
        authoring.AddChild(add);

        var treeColumn = Column(columns, "ARBRE · GLISSER POUR RÉIMBRIQUER");
        _layerTree = new Tree
        {
            Columns = 1,
            HideRoot = true,
            CustomMinimumSize = new(520, 300),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _layerTree.GuiInput += OnLayerTreeInput;
        treeColumn.AddChild(_layerTree);
        var actions = new HBoxContainer();
        var select = new Button { Text = "Sélectionner le groupe" };
        select.Pressed += SelectLayerGroup;
        actions.AddChild(select);
        var export = new Button { Text = "Prévisualiser l’export partiel" };
        export.Pressed += PreviewLayerExport;
        actions.AddChild(export);
        treeColumn.AddChild(actions);
        _layerPreview = new Label
        {
            Text = "État vide · ajoutez un calque pour prévisualiser la sélection et l’export.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        treeColumn.AddChild(_layerPreview);
    }

    private void Refresh()
    {
        var id = _active?.Invoke();
        _draft = _state?.Invoke()?.Project.Areas.SingleOrDefault(area => area.Id == id)?.Authoring;
        if (_draft is null)
        {
            _status!.Text = "État vide · aucune zone active";
            return;
        }
        Populate(_draft);
    }

    private void Populate(AreaAuthoringProfile value)
    {
        _draft = value;
        _fog!.ButtonPressed = value.Map.FogOfWar;
        _map2D!.ButtonPressed = value.Map.Generate2D;
        _map3D!.ButtonPressed = value.Map.Generate3D;
        _explorationScope!.Selected = (int)value.Map.ExplorationScope;
        _explorationCell!.Value = value.Map.ExplorationCellSize;
        _floors!.Value = value.Map.FloorCount;
        _cellSize!.Value = value.Experience.Visibility.CellSize;
        _streamingDistance!.Value = value.Experience.Visibility.StreamingDistance;
        _lodDistances!.Text = string.Join(", ", value.Experience.Lod.DistanceThresholds.Select(item => $"{item:0.##}"));
        _lodTriangles!.Value = value.Experience.Lod.MaximumVisibleTriangles;
        _poppingTolerance!.Value = value.Experience.Lod.PoppingTolerance;
        _runtimeRequired!.ButtonPressed = value.Experience.RequireRuntimeMetadata;
        _layerRuntimeRequired!.ButtonPressed = value.RequireLayerRuntimeMetadata;
        _summary!.Clear();
        foreach (var snapshot in value.Experience.AudioSnapshots) _summary.AddItem($"Mix · {snapshot.Name}");
        foreach (var item in value.UserEvents) _summary.AddItem($"Événement · {item.NamespacedName}");
        foreach (var item in value.Map.Pins) _summary.AddItem($"Repère · {item.Label} · étage {item.Floor}");
        foreach (var item in value.Experience.Visibility.Rooms) _summary.AddItem($"Room · {item.Name}");
        foreach (var item in value.Experience.Visibility.Occluders) _summary.AddItem($"Occluder · {item.Name}");
        foreach (var item in value.Experience.Lod.AssetOverrides) _summary.AddItem($"LOD · {item.AssetId}");
        PopulateLayers(value);
    }

    private void AddEvent()
    {
        if (_draft is null) return;
        try
        {
            var item = new AreaUserEvent(Guid.NewGuid(), _eventName!.Text, _eventSchema!.Text, _eventScript!.Text, 1,
                Split(_eventSubscriptions!.Text), true, "author:toolset", AreaUserEvent.CurrentSchemaVersion,
                new Dictionary<string, string> { ["payload"] = "string" },
                AreaUserEvent.CurrentForgeScriptApiVersion);
            _draft = new(1, [.. _draft.UserEvents, item], _draft.Map, _draft.Layers, _draft.Experience);
            Populate(_draft);
        }
        catch (ArgumentException exception)
        {
            _status!.Text = $"Erreur · {exception.Message}";
        }
    }

    private void AddPin()
    {
        if (_draft is null || string.IsNullOrWhiteSpace(_pin!.Text)) return;
        var source = _draft.Map;
        var map = new AreaMapProfile(true, _fog!.ButtonPressed, source.Projection, (int)_floors!.Value,
            [.. source.Pins, new AreaMapPin(Guid.NewGuid(), _pin.Text.Trim(), (float)_pinX!.Value,
                (float)_pinZ!.Value, 0, false)], source.Id, source.SchemaVersion, "author:toolset",
            _map2D!.ButtonPressed, _map3D!.ButtonPressed, (AreaMapExplorationScope)_explorationScope!.Selected,
            (float)_explorationCell!.Value, source.RevealApi);
        _draft = new(1, _draft.UserEvents, map, _draft.Layers, _draft.Experience);
        Populate(_draft);
    }

    private void AddLayer()
    {
        if (_draft is null || string.IsNullOrWhiteSpace(_layer!.Text) || string.IsNullOrWhiteSpace(_owner!.Text)) return;
        var parentId = _layerParent!.Selected <= 0
            ? (Guid?)null
            : Guid.Parse(_layerParent.GetItemMetadata(_layerParent.Selected).AsString());
        var entityIds = _layerEntity!.Selected <= 0
            ? Array.Empty<EntityId>()
            : [EntityId.Parse(_layerEntity.GetItemMetadata(_layerEntity.Selected).AsString())];
        _draft = new(1, _draft.UserEvents, _draft.Map,
            [.. _draft.Layers, new AreaEditorLayer(Guid.NewGuid(), _layer.Text, parentId,
                _visible!.ButtonPressed, _locked!.ButtonPressed, _owner.Text, entityIds,
                provenance: "author:toolset", exportEnabled: _layerExport!.ButtonPressed)], _draft.Experience,
            _layerRuntimeRequired!.ButtonPressed);
        Populate(_draft);
    }

    private void PopulateLayers(AreaAuthoringProfile value)
    {
        _layerParent!.Clear();
        _layerParent.AddItem("Racine");
        foreach (var layer in AreaLayerHierarchy.Resolve(value))
        {
            _layerParent.AddItem($"{new string('·', layer.Depth)} {layer.Name}");
            _layerParent.SetItemMetadata(_layerParent.ItemCount - 1, layer.LayerId.ToString());
        }
        _layerEntity!.Clear();
        _layerEntity.AddItem("Aucune");
        var area = CurrentArea();
        if (area is not null)
            foreach (var entity in area.Entities.OrderBy(item => item.Id.Value))
            {
                _layerEntity.AddItem(entity.Id.ToString());
                _layerEntity.SetItemMetadata(_layerEntity.ItemCount - 1, entity.Id.ToString());
            }

        _layerTree!.Clear();
        var root = _layerTree.CreateItem();
        var byParent = value.Layers.GroupBy(item => item.ParentId ?? Guid.Empty).ToDictionary(group => group.Key,
            group => group.OrderBy(item => item.Name, StringComparer.Ordinal).ToArray());
        AddLayerItems(root, Guid.Empty, byParent,
            AreaLayerHierarchy.Resolve(value).ToDictionary(item => item.LayerId));
        _layerPreview!.Text = value.Layers.Count == 0
            ? "État vide · ajoutez un calque pour prévisualiser la sélection et l’export."
            : $"{value.Layers.Count} calque(s) · glisser-déposer pour changer de parent · verrouillage hérité";
    }

    private static void AddLayerItems(TreeItem parent, Guid parentId,
        IReadOnlyDictionary<Guid, AreaEditorLayer[]> byParent,
        IReadOnlyDictionary<Guid, AreaLayerEffectiveState> effective)
    {
        if (!byParent.TryGetValue(parentId, out var children)) return;
        foreach (var layer in children)
        {
            var state = effective[layer.Id];
            var item = parent.CreateChild();
            var visibility = state.Visible ? "◉" : "○";
            var locked = state.Locked ? "🔒" : string.Empty;
            var export = state.ExportEnabled ? "export" : "privé";
            item.SetText(0, $"{visibility} {layer.Name} · {layer.Owner} · {export} {locked}".Trim());
            item.SetMetadata(0, layer.Id.ToString());
            AddLayerItems(item, layer.Id, byParent, effective);
        }
    }

    private void OnLayerTreeInput(InputEvent input)
    {
        if (_draft is null || input is not InputEventMouseButton mouse || mouse.ButtonIndex != MouseButton.Left)
            return;
        var item = _layerTree!.GetItemAtPosition(mouse.Position);
        if (mouse.Pressed)
        {
            _draggedLayerId = TryLayerId(item);
            return;
        }
        var target = TryLayerId(item);
        if (_draggedLayerId is not { } source || target == source) return;
        var result = AreaLayerAuthoringService.Reparent(_draft, source, target);
        _draggedLayerId = null;
        if (!result.Succeeded)
        {
            _status!.Text = $"Erreur · {string.Join(", ", result.Diagnostics)}";
            return;
        }
        _draft = result.Profile;
        Populate(_draft);
        _status!.Text = "✓ Hiérarchie prévisualisée · appliquer pour historiser";
    }

    private void SelectLayerGroup()
    {
        var area = CurrentArea();
        var layerId = TryLayerId(_layerTree?.GetSelected());
        if (area is null || _draft is null || layerId is null) return;
        area = area.WithAuthoring(_draft);
        var selection = AreaLayerAuthoringService.SelectGroup(area, layerId.Value);
        _selectEntities?.Invoke(selection.EntityIds);
        _layerPreview!.Text = $"Sélection · {selection.LayerIds.Count} calque(s), " +
                              $"{selection.EntityIds.Count} entité(s) visibles · overlay viewport synchronisé";
    }

    private void PreviewLayerExport()
    {
        var area = CurrentArea();
        var layerId = TryLayerId(_layerTree?.GetSelected());
        if (area is null || _draft is null || layerId is null) return;
        area = area.WithAuthoring(_draft);
        var export = AreaLayerAuthoringService.PreviewPartialExport(area, [layerId.Value]);
        _layerPreview!.Text = $"Aperçu sans écriture · {export.Layers.Count} calque(s), " +
                              $"{export.Entities.Count} entité(s), {export.BlueprintDependencies.Count} blueprint(s)";
    }

    private AreaDefinition? CurrentArea()
    {
        var id = _active?.Invoke();
        return _state?.Invoke()?.Project.Areas.SingleOrDefault(item => item.Id == id);
    }

    private static Guid? TryLayerId(TreeItem? item) => item is null ||
        !Guid.TryParse(item.GetMetadata(0).AsString(), out var id) ? null : id;

    private void AddSnapshot()
    {
        if (_draft is null || string.IsNullOrWhiteSpace(_snapshotName!.Text)) return;
        var snapshot = new AreaAudioSnapshotProfile(Guid.NewGuid(), _snapshotName.Text.Trim(),
            new Dictionary<string, float> { ["music"] = .8f, ["ambience"] = .6f },
            (float)_snapshotTransition!.Value, _snapshotTrigger!.Text.Trim(), "author:toolset");
        var source = _draft.Experience;
        var experience = new AreaExperienceAuthoringProfile(source.Id, source.SchemaVersion, "author:toolset",
            [.. source.AudioSnapshots, snapshot], source.Visibility, source.Lod,
            _runtimeRequired!.ButtonPressed);
        _draft = new(1, _draft.UserEvents, _draft.Map, _draft.Layers, experience);
        Populate(_draft);
    }

    private void ApplyVisibilityDraft()
    {
        if (_draft is null || string.IsNullOrWhiteSpace(_roomName!.Text)) return;
        var roomA = new AreaVisibilityRoomProfile(Guid.NewGuid(), _roomName.Text.Trim(), TransformValue.Identity,
            false, (float)_streamingDistance!.Value, new Dictionary<string, string>());
        var roomB = new AreaVisibilityRoomProfile(Guid.NewGuid(), $"{_roomName.Text.Trim()} · annexe",
            new(new(16, 0, 0), QuaternionValue.Identity, new(12, 8, 12)), false,
            (float)_streamingDistance.Value, new Dictionary<string, string>());
        var visibility = new AreaVisibilityAuthoringProfile([roomA, roomB],
            [new(Guid.NewGuid(), "Portail de visibilité", roomA.Id, roomB.Id,
                new(new(8, 2, 0), QuaternionValue.Identity, new(3, 4, .5f)),
                AreaVisibilityPortalMode.Open, string.Empty, true)],
            [new(Guid.NewGuid(), "Occluder auteur", new(new(4, 2, 0), QuaternionValue.Identity,
                new(8, 4, .5f)), false, _occluderAsset!.Text.Trim(), new Dictionary<string, string>())],
            true, true, true, (float)_cellSize!.Value, (float)_streamingDistance.Value);
        var source = _draft.Experience;
        var experience = new AreaExperienceAuthoringProfile(source.Id, source.SchemaVersion, "author:toolset",
            source.AudioSnapshots, visibility, source.Lod, _runtimeRequired!.ButtonPressed);
        _draft = new(1, _draft.UserEvents, _draft.Map, _draft.Layers, experience);
        Populate(_draft);
    }

    private void ApplyLodDraft()
    {
        if (_draft is null) return;
        try
        {
            var distances = _lodDistances!.Text.Split(',', StringSplitOptions.TrimEntries |
                StringSplitOptions.RemoveEmptyEntries).Select(value => float.Parse(value,
                System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var overrides = string.IsNullOrWhiteSpace(_lodAsset!.Text)
                ? Array.Empty<AreaLodAssetOverride>()
                : [new AreaLodAssetOverride(Guid.NewGuid(), _lodAsset.Text.Trim(), AreaLodSourceMode.Hybrid,
                    [1, .5f, .2f], [12000, 6000, 2400], .2f, "author:toolset")];
            var lod = new AreaLodAuthoringProfile(AreaLodSourceMode.Hybrid, distances,
                (int)_lodTriangles!.Value, 128, 32, (float)_poppingTolerance!.Value, true, true, overrides);
            var source = _draft.Experience;
            var experience = new AreaExperienceAuthoringProfile(source.Id, source.SchemaVersion, "author:toolset",
                source.AudioSnapshots, source.Visibility, lod, _runtimeRequired!.ButtonPressed);
            _draft = new(1, _draft.UserEvents, _draft.Map, _draft.Layers, experience);
            Populate(_draft);
        }
        catch (FormatException exception)
        {
            _status!.Text = $"Erreur LOD · {exception.Message}";
        }
    }

    private bool Save()
    {
        var id = _active?.Invoke();
        if (id is null || _draft is null) return false;
        var succeeded = _apply?.Invoke(id, _draft) == true;
        _status!.Text = succeeded
            ? "✓ Configuration appliquée et historisée"
            : "✕ Configuration refusée · consultez le diagnostic localisé";
        return succeeded;
    }

    private static VBoxContainer Tab(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer { Name = title };
        tabs.AddChild(scroll);
        var body = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(body);
        return body;
    }

    private static VBoxContainer Column(Container parent, string title)
    {
        var column = new VBoxContainer
        {
            CustomMinimumSize = new(300, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        column.AddChild(new Label { Text = title });
        parent.AddChild(column);
        return column;
    }

    private static Control Labeled(string label, Control field)
    {
        var box = new VBoxContainer();
        box.AddChild(new Label { Text = label });
        box.AddChild(field);
        return box;
    }

    private static LineEdit Field(string hint) => new() { PlaceholderText = hint };

    private static SpinBox Spin(double min, double max, double value, double step) =>
        new() { MinValue = min, MaxValue = max, Value = value, Step = step };

    private static IReadOnlyList<string> Split(string value) => value.Split(',',
        StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
#endif
