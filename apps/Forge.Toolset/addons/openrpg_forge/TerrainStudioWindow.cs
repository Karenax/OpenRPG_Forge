#if TOOLS
using Forge.Application.Assets;
using Forge.Application.Areas;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Spatial;
using Godot;

namespace Forge.Toolset.Editor;

public partial class TerrainStudioWindow : Window
{
    private static readonly int[] SampleOptions = [9, 17, 33, 65, 129];
    private Func<ProjectState?>? _state;
    private Func<AreaId?>? _active;
    private Func<AreaId, AreaWorldProfile, bool>? _apply;
    private Func<AreaId, TerrainBrushStroke, bool>? _brush;
    private Action<Window>? _brand;
    private AreaWorldProfile? _current;
    private readonly List<Guid> _brushMaterialIds = [];

    private OptionButton? _provider;
    private OptionButton? _samples;
    private SpinBox? _heightScale;
    private SpinBox? _seed;
    private LineEdit? _soilColor;
    private LineEdit? _grassColor;
    private LineEdit? _rockColor;
    private SpinBox? _meadowDensity;
    private SpinBox? _rockSlope;
    private OptionButton? _meadowScatter;
    private OptionButton? _rockScatter;
    private readonly List<string> _meadowScatterResources = [];
    private readonly List<string> _rockScatterResources = [];
    private OptionButton? _brushOperation;
    private SpinBox? _brushX;
    private SpinBox? _brushZ;
    private SpinBox? _brushRadius;
    private SpinBox? _brushStrength;
    private SpinBox? _brushTargetHeight;
    private OptionButton? _brushMaterial;
    private Label? _brushPreview;
    private CheckBox? _road;
    private CheckBox? _river;
    private CheckBox? _bridge;
    private SpinBox? _featureWidth;
    private CheckBox? _volumeEnabled;
    private CheckBox? _flightMode;
    private CheckBox? _swimMode;
    private CheckBox? _spaceMode;
    private SpinBox? _cellSize;
    private SpinBox? _volumeHeight;
    private SpinBox? _avoidance;
    private CheckBox? _debugVolumes;
    private CheckBox? _flightRoute;
    private SpinBox? _routeAltitude;
    private SpinBox? _routeBank;
    private OptionButton? _obstaclePolicy;
    private SpinBox? _cacheBudget;
    private SpinBox? _concurrentLoads;
    private SpinBox? _chunkRadius;
    private SpinBox? _targetMilliseconds;
    private LineEdit? _resources;
    private Label? _status;

    internal string Summary => _current is null
        ? "unavailable"
        : $"{_current.Terrain.Provider}:{_current.Terrain.SamplesX}:{_current.Biomes.Count}:{_current.Features.Count}:" +
          $"{_current.VolumetricNavigation.Modes}:{_current.FlightRoutes.Count}:{_current.Loading.CacheBudgetMegabytes}";

    public void Initialize(Func<ProjectState?> state, Func<AreaId?> active,
        Func<AreaId, AreaWorldProfile, bool> apply, Func<AreaId, TerrainBrushStroke, bool> brush,
        Action<Window> brand)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _active = active ?? throw new ArgumentNullException(nameof(active));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _brush = brush ?? throw new ArgumentNullException(nameof(brush));
        _brand = brand ?? throw new ArgumentNullException(nameof(brand));
        Title = "Terrain & volumes · OpenRPG Forge";
        MinSize = new Vector2I(1180, 760);
        Size = new Vector2I(1280, 820);
        Transient = true;
        Visible = false;
        Build();
    }

    public void Open()
    {
        Refresh();
        PopupCentered(Size);
        _brand?.Invoke(this);
    }

    internal bool ConfigureForSmokeTest(AreaWorldProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _current = profile;
        Populate(profile);
        return true;
    }

    internal bool ApplyForSmokeTest() => Save();

    internal bool ApplyBrushForSmokeTest(TerrainBrushOperation operation, float worldX, float worldZ,
        float radius, float strength, float targetHeight = 0, Guid materialLayerId = default)
    {
        _brushOperation!.Selected = (int)operation;
        _brushX!.Value = worldX;
        _brushZ!.Value = worldZ;
        _brushRadius!.Value = radius;
        _brushStrength!.Value = strength;
        _brushTargetHeight!.Value = targetHeight;
        var materialIndex = _brushMaterialIds.IndexOf(materialLayerId);
        _brushMaterial!.Selected = materialIndex < 0 ? 0 : materialIndex;
        return ApplyBrush();
    }

    private void Build()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 20);
        }
        AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);
        var header = new Label { Text = "TERRAIN & VOLUMES 3D", ThemeTypeVariation = "HeaderSmall" };
        header.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(header);
        root.AddChild(new Label
        {
            Text = "Le terrain X/Z, l’altitude Y, les biomes, les routes et le vol restent dans le document canonique annulable.",
        });

        var tabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        root.AddChild(tabs);
        BuildTerrainTab(tabs);
        BuildSculptTab(tabs);
        BuildFeatureTab(tabs);
        BuildNavigationTab(tabs);
        BuildStreamingTab(tabs);

        _status = new Label { Text = "Prêt · prévisualisez le relief et les volumes avant application." };
        _status.AddThemeColorOverride("font_color", new Color("9bd9bd"));
        root.AddChild(_status);
        var actions = new HBoxContainer();
        actions.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var apply = new Button { Text = "Appliquer au monde", CustomMinimumSize = new Vector2(190, 40) };
        apply.Pressed += () => Save();
        actions.AddChild(apply);
        var close = new Button { Text = "Fermer", CustomMinimumSize = new Vector2(110, 40) };
        close.Pressed += Hide;
        actions.AddChild(close);
        root.AddChild(actions);
    }

    private void BuildSculptTab(TabContainer tabs)
    {
        var columns = Tab(tabs, "Sculpture");
        var brush = Card(columns, "BROSSE CANONIQUE");
        _brushOperation = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _brushOperation.AddItem("Lever", (int)TerrainBrushOperation.Raise);
        _brushOperation.AddItem("Abaisser", (int)TerrainBrushOperation.Lower);
        _brushOperation.AddItem("Aplanir vers Y", (int)TerrainBrushOperation.Flatten);
        _brushOperation.AddItem("Lisser", (int)TerrainBrushOperation.Smooth);
        _brushOperation.AddItem("Peindre un matériau / biome", (int)TerrainBrushOperation.PaintMaterial);
        _brushOperation.AddItem("Ouvrir un trou", (int)TerrainBrushOperation.OpenHole);
        _brushOperation.AddItem("Refermer un trou", (int)TerrainBrushOperation.CloseHole);
        brush.AddChild(FieldRow("Opération", _brushOperation));
        _brushX = Spin(-4096, 4096, 0, .5);
        _brushZ = Spin(-4096, 4096, 0, .5);
        brush.AddChild(FieldRow("Centre X", _brushX));
        brush.AddChild(FieldRow("Centre Z", _brushZ));

        var influence = Card(columns, "INFLUENCE");
        _brushRadius = Spin(.25, 4096, 8, .25);
        _brushStrength = Spin(.01, 1, .15, .01);
        _brushTargetHeight = Spin(-512, 512, 0, .25);
        _brushMaterial = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        influence.AddChild(FieldRow("Rayon (m)", _brushRadius));
        influence.AddChild(FieldRow("Intensité", _brushStrength));
        influence.AddChild(FieldRow("Cible Y", _brushTargetHeight));
        influence.AddChild(FieldRow("Matériau", _brushMaterial));
        influence.AddChild(new Label
        {
            Text = "Le relief utilise un falloff radial. Les trous modifient les cellules canoniques et peuvent accueillir une grotte ou un surplomb séparé.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        var preview = Card(columns, "PRÉVISUALISATION & HISTORIQUE");
        _brushPreview = new Label
        {
            Text = "Aucun trait prévisualisé.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        preview.AddChild(_brushPreview);
        var previewButton = new Button { Text = "Prévisualiser le trait" };
        previewButton.Pressed += PreviewBrush;
        preview.AddChild(previewButton);
        var applyButton = new Button { Text = "Appliquer le trait", CustomMinimumSize = new Vector2(180, 40) };
        applyButton.AddThemeColorOverride("font_color", Colors.White);
        applyButton.Pressed += () => ApplyBrush();
        preview.AddChild(applyButton);
        preview.AddChild(new Label
        {
            Text = "Chaque application crée une commande indépendante : Annuler/Rétablir restaure exactement le terrain précédent.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
    }

    private void BuildTerrainTab(TabContainer tabs)
    {
        var columns = Tab(tabs, "Terrain & biomes");
        var terrain = Card(columns, "SOURCE DE TERRAIN");
        _provider = EnumOption<TerrainProviderKind>();
        terrain.AddChild(FieldRow("Provider", _provider));
        _samples = new OptionButton();
        foreach (var value in SampleOptions) _samples.AddItem($"{value} × {value}");
        terrain.AddChild(FieldRow("Résolution", _samples));
        _heightScale = Spin(0, 512, 8, .25);
        terrain.AddChild(FieldRow("Amplitude Y", _heightScale));
        _seed = Spin(int.MinValue, int.MaxValue, 2026, 1);
        terrain.AddChild(FieldRow("Graine", _seed));
        terrain.AddChild(new Label
        {
            Text = "Heightmap : relief déterministe. Flat : sol plan. Voxel/Mesh conservent leurs références externes et un fallback inspectable.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        var materials = Card(columns, "MATÉRIAUX PBR");
        _soilColor = Field("Terre · #70533a");
        _grassColor = Field("Prairie · #5f8345");
        _rockColor = Field("Roche · #777b82");
        materials.AddChild(FieldRow("Terre", _soilColor));
        materials.AddChild(FieldRow("Prairie", _grassColor));
        materials.AddChild(FieldRow("Roche", _rockColor));
        materials.AddChild(new Label { Text = "Les ressources GLB/matériaux pourront remplacer ces couleurs sans changer le relief." });

        var biomes = Card(columns, "RECETTES DE BIOME");
        _meadowDensity = Spin(0, 1, .45, .05);
        _rockSlope = Spin(0, 90, 32, 1);
        _meadowScatter = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _rockScatter = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        biomes.AddChild(FieldRow("Densité prairie", _meadowDensity));
        biomes.AddChild(FieldRow("Roche dès (°)", _rockSlope));
        biomes.AddChild(FieldRow("Asset prairie", _meadowScatter));
        biomes.AddChild(FieldRow("Asset roche", _rockScatter));
        biomes.AddChild(new Label
        {
            Text = "Les couches échantillonnent altitude, pente, humidité et exclusions. La brosse Matériau/biome force localement la recette et sa dispersion, sans dupliquer le terrain.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
    }

    private void PreviewBrush()
    {
        if (_current is null || _brushPreview is null) return;
        try
        {
            var result = TerrainBrushEditor.Apply(_current.Terrain, BuildBrushStroke(),
                Guid.Parse("d1400000-0000-4000-8000-000000000000"));
            _brushPreview.Text = BrushSummary(result);
        }
        catch (ArgumentException exception)
        {
            _brushPreview.Text = $"✕ {exception.Message}";
        }
    }

    private bool ApplyBrush()
    {
        var areaId = _active?.Invoke();
        if (areaId is null || _current is null || _brushPreview is null) return false;
        try
        {
            var stroke = BuildBrushStroke();
            var preview = TerrainBrushEditor.Apply(_current.Terrain, stroke,
                Guid.Parse("d1400000-0000-4000-8000-000000000000"));
            if (preview.ChangedSamples == 0 && preview.ChangedHoleCells == 0 && preview.ChangedMaterialCells == 0)
            {
                _brushPreview.Text = "✕ La brosse ne touche aucun échantillon ou cellule modifiable.";
                return false;
            }

            var applied = _brush?.Invoke(areaId, stroke) == true;
            if (applied)
            {
                Refresh();
                _brushPreview.Text = $"✓ {BrushSummary(preview)} · commande annulable appliquée";
            }
            else
            {
                _brushPreview.Text = "✕ Trait refusé par la validation du projet.";
            }

            return applied;
        }
        catch (ArgumentException exception)
        {
            _brushPreview.Text = $"✕ {exception.Message}";
            return false;
        }
    }

    private TerrainBrushStroke BuildBrushStroke() => new(
        (TerrainBrushOperation)_brushOperation!.GetSelectedId(),
        (float)_brushX!.Value,
        (float)_brushZ!.Value,
        (float)_brushRadius!.Value,
        (float)_brushStrength!.Value,
        (float)_brushTargetHeight!.Value,
        _brushMaterialIds.Count == 0 ? Guid.Empty : _brushMaterialIds[_brushMaterial!.Selected]);

    private static string BrushSummary(TerrainBrushEditResult result) =>
        $"Prévisualisation · {result.ChangedSamples} échantillon(s) · {result.ChangedHoleCells} cellule(s) de trou · " +
        $"{result.ChangedMaterialCells} cellule(s) de matériau";

    private void BuildFeatureTab(TabContainer tabs)
    {
        var columns = Tab(tabs, "Routes & features");
        var feature = Card(columns, "FEATURES PARAMÉTRIQUES");
        _road = Check("Route traversante", false);
        _river = Check("Rivière traversante", false);
        _bridge = Check("Pont à l’intersection", true);
        feature.AddChild(_road);
        feature.AddChild(_river);
        feature.AddChild(_bridge);
        _featureWidth = Spin(.25, 128, 3, .25);
        feature.AddChild(FieldRow("Largeur", _featureWidth));

        var compatibility = Card(columns, "COMPATIBILITÉ");
        compatibility.AddChild(new Label
        {
            Text = "Route et rivière utilisent des splines canoniques. Un croisement qui exige un pont est refusé avant mutation si le pont manque.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        compatibility.AddChild(new Label
        {
            Text = "Les futures falaises, murs, bâtiments et détails réutiliseront le même contrat de points, largeur, matériau, biome et provenance.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
    }

    private void BuildNavigationTab(TabContainer tabs)
    {
        var columns = Tab(tabs, "Navigation 3D");
        var volume = Card(columns, "GRILLE VOLUMÉTRIQUE");
        _volumeEnabled = Check("Activer navigation 3D", false);
        _flightMode = Check("Vol", true);
        _swimMode = Check("Nage", false);
        _spaceMode = Check("Espace", false);
        volume.AddChild(_volumeEnabled);
        volume.AddChild(_flightMode);
        volume.AddChild(_swimMode);
        volume.AddChild(_spaceMode);
        _cellSize = Spin(.5, 64, 4, .5);
        _volumeHeight = Spin(1, 4096, 24, 1);
        _avoidance = Spin(0, 64, 1, .25);
        volume.AddChild(FieldRow("Cellule", _cellSize));
        volume.AddChild(FieldRow("Hauteur du volume", _volumeHeight));
        volume.AddChild(FieldRow("Évitement", _avoidance));
        _debugVolumes = Check("Afficher les volumes", false);
        volume.AddChild(_debugVolumes);

        var route = Card(columns, "ROUTE AÉRIENNE");
        _flightRoute = Check("Route de démonstration", false);
        route.AddChild(_flightRoute);
        _routeAltitude = Spin(-4096, 4096, 8, .5);
        _routeBank = Spin(-89, 89, 12, 1);
        route.AddChild(FieldRow("Altitude Y", _routeAltitude));
        route.AddChild(FieldRow("Banking", _routeBank));
        _obstaclePolicy = EnumOption<FlightObstaclePolicy>();
        route.AddChild(FieldRow("Obstacles", _obstaclePolicy));
        route.AddChild(new Label { Text = "L’overlay cyan rend la route et son altitude visibles dans le viewport." });
    }

    private void BuildStreamingTab(TabContainer tabs)
    {
        var columns = Tab(tabs, "Streaming & budgets");
        var streaming = Card(columns, "CHARGEMENT ASYNCHRONE");
        _cacheBudget = Spin(16, 65536, 512, 16);
        _concurrentLoads = Spin(1, 32, 4, 1);
        _chunkRadius = Spin(0, 32, 2, 1);
        _targetMilliseconds = Spin(1, 600000, 2000, 50);
        streaming.AddChild(FieldRow("Cache (Mo)", _cacheBudget));
        streaming.AddChild(FieldRow("Chargements simultanés", _concurrentLoads));
        streaming.AddChild(FieldRow("Rayon de chunks", _chunkRadius));
        streaming.AddChild(FieldRow("Cible (ms)", _targetMilliseconds));
        _resources = Field("Ressources relatives, séparées par des virgules");
        streaming.AddChild(_resources);

        var evidence = Card(columns, "MESURES & CONTRÔLE");
        evidence.AddChild(new Label
        {
            Text = "Le coordinator partage cache, préchargement des dépendances, progression, annulation et résultat chronométré. Le runtime publie les compteurs de reconstruction.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
    }

    private void Refresh()
    {
        var id = _active?.Invoke();
        var area = _state?.Invoke()?.Project.Areas.SingleOrDefault(item => item.Id == id);
        if (area is null) return;
        _current = area.World;
        Populate(_current);
    }

    private void Populate(AreaWorldProfile profile)
    {
        _provider!.Selected = (int)profile.Terrain.Provider;
        var sampleIndex = Array.IndexOf(SampleOptions, profile.Terrain.SamplesX);
        _samples!.Selected = sampleIndex < 0 ? 1 : sampleIndex;
        _heightScale!.Value = profile.Terrain.HeightScale;
        _seed!.Value = profile.Terrain.Seed;
        _soilColor!.Text = profile.Terrain.MaterialLayers.ElementAtOrDefault(0)?.AlbedoColor ?? "#70533a";
        _grassColor!.Text = profile.Terrain.MaterialLayers.ElementAtOrDefault(1)?.AlbedoColor ?? "#5f8345";
        _rockColor!.Text = profile.Terrain.MaterialLayers.ElementAtOrDefault(2)?.AlbedoColor ?? "#777b82";
        _brushMaterial!.Clear();
        _brushMaterialIds.Clear();
        _brushMaterial.AddItem("Automatique · recettes de biome");
        _brushMaterialIds.Add(Guid.Empty);
        foreach (var material in profile.Terrain.MaterialLayers)
        {
            _brushMaterial.AddItem(material.Name);
            _brushMaterialIds.Add(material.Id);
        }
        _meadowDensity!.Value = profile.Biomes.FirstOrDefault(item => item.Tags.Contains("meadow"))?.ScatterDensity ?? .45;
        _rockSlope!.Value = profile.Biomes.FirstOrDefault(item => item.Tags.Contains("rock"))?.MinimumSlopeDegrees ?? 32;
        PopulateScatterResourceOptions(
            _meadowScatter!,
            _meadowScatterResources,
            profile.Biomes.FirstOrDefault(item => item.Tags.Contains("meadow"))?.ScatterResource ?? string.Empty);
        PopulateScatterResourceOptions(
            _rockScatter!,
            _rockScatterResources,
            profile.Biomes.FirstOrDefault(item => item.Tags.Contains("rock"))?.ScatterResource ?? string.Empty);
        _road!.ButtonPressed = profile.Features.Any(item => item.Kind == TerrainFeatureKind.Road);
        _river!.ButtonPressed = profile.Features.Any(item => item.Kind == TerrainFeatureKind.River);
        _bridge!.ButtonPressed = profile.Features.Any(item => item.Kind == TerrainFeatureKind.Bridge);
        _featureWidth!.Value = profile.Features.FirstOrDefault()?.Width ?? 3;
        _volumeEnabled!.ButtonPressed = profile.VolumetricNavigation.Enabled;
        _flightMode!.ButtonPressed = profile.VolumetricNavigation.Modes.HasFlag(VolumetricNavigationMode.Flight);
        _swimMode!.ButtonPressed = profile.VolumetricNavigation.Modes.HasFlag(VolumetricNavigationMode.Swim);
        _spaceMode!.ButtonPressed = profile.VolumetricNavigation.Modes.HasFlag(VolumetricNavigationMode.Space);
        _cellSize!.Value = profile.VolumetricNavigation.CellSize;
        _volumeHeight!.Value = profile.VolumetricNavigation.Size.Y;
        _avoidance!.Value = profile.VolumetricNavigation.AvoidanceRadius;
        _debugVolumes!.ButtonPressed = profile.VolumetricNavigation.DebugVisible;
        var route = profile.FlightRoutes.FirstOrDefault();
        _flightRoute!.ButtonPressed = route is not null;
        _routeAltitude!.Value = route?.Nodes.FirstOrDefault()?.Position.Y ?? 8;
        _routeBank!.Value = route?.Nodes.FirstOrDefault()?.BankDegrees ?? 12;
        _obstaclePolicy!.Selected = (int)(route?.ObstaclePolicy ?? FlightObstaclePolicy.Avoid);
        _cacheBudget!.Value = profile.Loading.CacheBudgetMegabytes;
        _concurrentLoads!.Value = profile.Loading.MaximumConcurrentLoads;
        _chunkRadius!.Value = profile.Loading.ChunkRadius;
        _targetMilliseconds!.Value = profile.Loading.TargetLoadMilliseconds;
        _resources!.Text = string.Join(", ", profile.Loading.RequiredResources);
    }

    private bool Save()
    {
        var areaId = _active?.Invoke();
        var area = _state?.Invoke()?.Project.Areas.SingleOrDefault(item => item.Id == areaId);
        if (areaId is null || area is null || _current is null) return false;
        try
        {
            var provider = (TerrainProviderKind)_provider!.Selected;
            var samples = SampleOptions[_samples!.Selected];
            var seed = (int)_seed!.Value;
            var heightScale = (float)_heightScale!.Value;
            var heights = _current.Terrain.Provider == provider && _current.Terrain.SamplesX == samples &&
                          _current.Terrain.SamplesZ == samples && _current.Terrain.Seed == seed
                ? _current.Terrain.Heights
                : GenerateHeights(provider, samples, seed);
            var oldMaterials = _current.Terrain.MaterialLayers;
            var soil = new TerrainMaterialLayer(oldMaterials.ElementAtOrDefault(0)?.Id ?? Guid.NewGuid(), "Terre",
                ColorValue(_soilColor!.Text, "#70533a"), oldMaterials.ElementAtOrDefault(0)?.MaterialResource ?? string.Empty, 4);
            var grass = new TerrainMaterialLayer(oldMaterials.ElementAtOrDefault(1)?.Id ?? Guid.NewGuid(), "Prairie",
                ColorValue(_grassColor!.Text, "#5f8345"), oldMaterials.ElementAtOrDefault(1)?.MaterialResource ?? string.Empty, 5);
            var rock = new TerrainMaterialLayer(oldMaterials.ElementAtOrDefault(2)?.Id ?? Guid.NewGuid(), "Roche",
                ColorValue(_rockColor!.Text, "#777b82"), oldMaterials.ElementAtOrDefault(2)?.MaterialResource ?? string.Empty, 3);
            IReadOnlyList<TerrainMaterialLayer> materials = provider == TerrainProviderKind.None ? [] : [soil, grass, rock];
            var holes = _current.Terrain.SamplesX == samples ? _current.Terrain.Holes : [];
            var materialPaint = _current.Terrain.SamplesX == samples && _current.Terrain.SamplesZ == samples
                ? _current.Terrain.MaterialPaint
                : [];
            var terrain = new TerrainDefinition(_current.Terrain.Id, TerrainDefinition.CurrentSchemaVersion,
                provider, area.Dimensions.Width, area.Dimensions.Depth, samples, samples, Math.Min(32, samples - 1),
                seed, provider == TerrainProviderKind.Flat || provider == TerrainProviderKind.None ? 0 : heightScale,
                heights, materials, holes, _current.Terrain.HeightmapResource, _current.Terrain.VoxelResource,
                _current.Terrain.MeshResource, "toolset:terrain-studio", materialPaint);
            IReadOnlyList<BiomeLayerDefinition> biomes = provider == TerrainProviderKind.None
                ? []
                :
                [
                    new(_current.Biomes.FirstOrDefault(item => item.Tags.Contains("meadow"))?.Id ?? Guid.NewGuid(),
                        "Prairie", grass.Id, -512, 512, 0, Math.Max(0, (float)_rockSlope!.Value), 0, 1,
                        (float)_meadowDensity!.Value, SelectedScatterResource(_meadowScatter!, _meadowScatterResources),
                        ["meadow", "temperate", "ground-cover"], ["water", "road"], 20,
                        "toolset:terrain-studio"),
                    new(_current.Biomes.FirstOrDefault(item => item.Tags.Contains("rock"))?.Id ?? Guid.NewGuid(),
                        "Roche", rock.Id, -512, 512, Math.Max(0, (float)_rockSlope.Value), 90, 0, 1,
                        .08f, SelectedScatterResource(_rockScatter!, _rockScatterResources),
                        ["rock", "cliff"], ["water"], 30, "toolset:terrain-studio"),
                ];
            var features = BuildFeatures(area, _current);
            var modes = VolumetricNavigationMode.None;
            if (_flightMode!.ButtonPressed) modes |= VolumetricNavigationMode.Flight;
            if (_swimMode!.ButtonPressed) modes |= VolumetricNavigationMode.Swim;
            if (_spaceMode!.ButtonPressed) modes |= VolumetricNavigationMode.Space;
            var enabled = _volumeEnabled!.ButtonPressed;
            if (enabled && modes == VolumetricNavigationMode.None) modes = VolumetricNavigationMode.Flight;
            var volume = new VolumetricNavigationProfile(_current.VolumetricNavigation.Id, 1, enabled, modes,
                (float)_cellSize!.Value, new Vector3Value(0, (float)_volumeHeight!.Value / 2, 0),
                new Vector3Value(area.Dimensions.Width, (float)_volumeHeight.Value, area.Dimensions.Depth),
                (float)_avoidance!.Value, _current.VolumetricNavigation.Obstacles,
                _current.VolumetricNavigation.CostVolumes, _debugVolumes!.ButtonPressed, "toolset:terrain-studio");
            var routes = BuildRoutes(area, _current, enabled);
            var loading = new AreaLoadingProfile(_current.Loading.Id, 1, true, true, true,
                (int)_cacheBudget!.Value, (int)_concurrentLoads!.Value, (int)_chunkRadius!.Value,
                (int)_targetMilliseconds!.Value,
                _resources!.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                "toolset:terrain-studio");
            var profile = new AreaWorldProfile(_current.Id, 1, terrain, biomes, features, volume, routes, loading,
                "toolset:terrain-studio");
            var applied = _apply?.Invoke(areaId, profile) == true;
            if (applied) _current = profile;
            _status!.Text = applied
                ? $"✓ Monde appliqué · {samples}² · {features.Count} feature(s) · {modes}"
                : "✕ Profil refusé · vérifiez les croisements, limites et volumes";
            return applied;
        }
        catch (ArgumentException exception)
        {
            _status!.Text = $"✕ {exception.Message}";
            return false;
        }
    }

    private IReadOnlyList<TerrainFeatureDefinition> BuildFeatures(AreaDefinition area, AreaWorldProfile current)
    {
        var width = (float)_featureWidth!.Value;
        var halfX = area.Dimensions.Width * .42f;
        var halfZ = area.Dimensions.Depth * .42f;
        var features = new List<TerrainFeatureDefinition>();
        if (_road!.ButtonPressed)
        {
            features.Add(new(current.Features.FirstOrDefault(item => item.Kind == TerrainFeatureKind.Road)?.Id ?? Guid.NewGuid(),
                1, "Route principale", TerrainFeatureKind.Road,
                [new Vector3Value(-halfX, .05f, 0), new Vector3Value(halfX, .05f, 0)], width, .08f, 0,
                _river!.ButtonPressed ? TerrainFeatureCrossingPolicy.RequiresBridge : TerrainFeatureCrossingPolicy.Allow,
                [], string.Empty, "toolset:terrain-studio"));
        }
        if (_river!.ButtonPressed)
        {
            features.Add(new(current.Features.FirstOrDefault(item => item.Kind == TerrainFeatureKind.River)?.Id ?? Guid.NewGuid(),
                1, "Rivière principale", TerrainFeatureKind.River,
                [new Vector3Value(0, 0, -halfZ), new Vector3Value(0, 0, halfZ)], width * 1.35f, .06f, 0,
                _road!.ButtonPressed ? TerrainFeatureCrossingPolicy.RequiresBridge : TerrainFeatureCrossingPolicy.Allow,
                [], string.Empty, "toolset:terrain-studio"));
        }
        if (_bridge!.ButtonPressed && _road.ButtonPressed && _river.ButtonPressed)
        {
            features.Add(new(current.Features.FirstOrDefault(item => item.Kind == TerrainFeatureKind.Bridge)?.Id ?? Guid.NewGuid(),
                1, "Pont principal", TerrainFeatureKind.Bridge,
                [new Vector3Value(-width, .2f, -width), new Vector3Value(width, .2f, width)], width * 1.2f, .35f, 0,
                TerrainFeatureCrossingPolicy.Allow, [], string.Empty, "toolset:terrain-studio"));
        }
        return features;
    }

    private IReadOnlyList<FlightRouteDefinition> BuildRoutes(AreaDefinition area, AreaWorldProfile current, bool volumeEnabled)
    {
        if (!_flightRoute!.ButtonPressed || !volumeEnabled) return [];
        var existing = current.FlightRoutes.FirstOrDefault();
        var altitude = (float)_routeAltitude!.Value;
        var bank = (float)_routeBank!.Value;
        var x = area.Dimensions.Width * .35f;
        var z = area.Dimensions.Depth * .35f;
        return
        [
            new(existing?.Id ?? Guid.NewGuid(), 1, "Route aérienne principale",
                [
                    new(existing?.Nodes.ElementAtOrDefault(0)?.Id ?? Guid.NewGuid(), "Entrée", new Vector3Value(-x, altitude, -z), 12, bank),
                    new(existing?.Nodes.ElementAtOrDefault(1)?.Id ?? Guid.NewGuid(), "Sortie", new Vector3Value(x, altitude, z), 12, -bank),
                ], FlightRouteCurve.CatmullRom, (FlightObstaclePolicy)_obstaclePolicy!.Selected, false, true,
                "toolset:terrain-studio"),
        ];
    }

    private static IReadOnlyList<float> GenerateHeights(TerrainProviderKind provider, int samples, int seed)
    {
        var values = new float[samples * samples];
        if (provider is TerrainProviderKind.None or TerrainProviderKind.Flat) return values;
        var phase = (Math.Abs((long)seed) % 10000) / 10000f * MathF.Tau;
        for (var z = 0; z < samples; z++)
        {
            for (var x = 0; x < samples; x++)
            {
                var nx = (x / (samples - 1f)) - .5f;
                var nz = (z / (samples - 1f)) - .5f;
                values[(z * samples) + x] = Math.Clamp(
                    (MathF.Sin((nx * 5.2f) + phase) + MathF.Cos((nz * 4.3f) - phase * .5f)) * .12f +
                    MathF.Sin((nx + nz) * 8f + phase) * .04f, -1, 1);
            }
        }
        return values;
    }

    private static string ColorValue(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private void PopulateScatterResourceOptions(OptionButton button, List<string> resources, string selectedResource)
    {
        button.Clear();
        resources.Clear();
        button.AddItem("Aucun asset · dispersion désactivée");
        resources.Add(string.Empty);

        var blueprints = _state?.Invoke()?.Project.Blueprints ?? [];
        foreach (var blueprint in blueprints.OrderBy(candidate => candidate.DisplayName, StringComparer.CurrentCulture))
        {
            if (!AssetBlueprintFactory.TryGetAssetId(blueprint, out var assetId) || assetId is null) continue;
            var resource = $"asset:{assetId}";
            if (resources.Contains(resource, StringComparer.Ordinal)) continue;
            button.AddItem($"{blueprint.DisplayName} · installé");
            resources.Add(resource);
        }

        var selectedIndex = resources.FindIndex(candidate =>
            string.Equals(candidate, selectedResource, StringComparison.Ordinal));
        if (selectedIndex < 0 && !string.IsNullOrWhiteSpace(selectedResource))
        {
            button.AddItem($"Manquant · {selectedResource}");
            resources.Add(selectedResource);
            selectedIndex = resources.Count - 1;
        }

        button.Selected = Math.Max(0, selectedIndex);
    }

    private static string SelectedScatterResource(OptionButton button, IReadOnlyList<string> resources)
    {
        var selected = button.Selected;
        return selected >= 0 && selected < resources.Count ? resources[selected] : string.Empty;
    }

    private static HBoxContainer Tab(TabContainer tabs, string name)
    {
        var scroll = new ScrollContainer
        {
            Name = name,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        tabs.AddChild(scroll);
        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 14);
        scroll.AddChild(row);
        return row;
    }

    private static VBoxContainer Card(Container parent, string title)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(350, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("15111f"),
            BorderColor = new Color("4f3b67"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 9,
            CornerRadiusTopRight = 9,
            CornerRadiusBottomLeft = 9,
            CornerRadiusBottomRight = 9,
            ContentMarginLeft = 14,
            ContentMarginTop = 12,
            ContentMarginRight = 14,
            ContentMarginBottom = 12,
        });
        parent.AddChild(panel);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        var heading = new Label { Text = title };
        heading.AddThemeColorOverride("font_color", new Color("ffb06d"));
        content.AddChild(heading);
        panel.AddChild(content);
        return content;
    }

    private static OptionButton EnumOption<T>() where T : struct, Enum
    {
        var button = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var value in Enum.GetValues<T>()) button.AddItem(value.ToString());
        return button;
    }

    private static LineEdit Field(string hint) => new() { PlaceholderText = hint, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };

    private static SpinBox Spin(double min, double max, double value, double step) => new()
    {
        MinValue = min,
        MaxValue = max,
        Value = value,
        Step = step,
        CustomMinimumSize = new Vector2(120, 0),
    };

    private static CheckBox Check(string text, bool value) => new() { Text = text, ButtonPressed = value };

    private static HBoxContainer FieldRow(string text, Control control)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = text, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        row.AddChild(control);
        return row;
    }
}
#endif
