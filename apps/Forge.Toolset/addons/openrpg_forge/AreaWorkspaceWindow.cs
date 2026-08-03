#if TOOLS
using Forge.Application.Areas;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Validation;
using Godot;

namespace Forge.Toolset.Editor;

public partial class AreaWorkspaceWindow : Window
{
    private readonly List<AreaId> _visibleAreaIds = [];
    private readonly List<EntityId> _sourceEntityIds = [];
    private readonly List<AreaId> _destinationAreaIds = [];
    private readonly List<EntityId> _destinationEntityIds = [];
    private readonly AreaKind[] _areaKinds = Enum.GetValues<AreaKind>();
    private readonly AreaTemplateKind[] _templates = Enum.GetValues<AreaTemplateKind>();

    private Func<ProjectState?>? _getState;
    private Func<AreaId?>? _getActiveAreaId;
    private Func<AreaId, bool>? _activateArea;
    private Func<string, AreaKind, AreaDimensions, AreaTemplateKind, AreaId?>? _createArea;
    private Func<AreaId, PortalLink, bool>? _upsertPortal;
    private Func<AreaId, string, AreaCloneMode, ExternalPortalPolicy, AreaId?>? _duplicateArea;
    private Func<AreaId, AreaDimensions, AreaResizeAnchor, bool>? _resizeArea;
    private Action? _openNavigation;
    private Action<Window>? _brandWindow;

    private ItemList? _areaList;
    private Label? _areaSummary;
    private Label? _activeAreaSummary;
    private LineEdit? _areaName;
    private OptionButton? _areaKind;
    private SpinBox? _areaWidth;
    private SpinBox? _areaDepth;
    private OptionButton? _areaTemplate;
    private OptionButton? _sourceEntity;
    private OptionButton? _destinationArea;
    private OptionButton? _destinationEntity;
    private LineEdit? _portalName;
    private CheckBox? _bidirectional;
    private CheckBox? _preload;
    private ItemList? _validationList;
    private Label? _validationSummary;
    private Label? _status;
    private LineEdit? _cloneName;
    private OptionButton? _cloneMode;
    private CheckBox? _preserveExternalPortals;
    private Button? _applyCloneButton;
    private AreaId? _selectedAreaId;
    private AreaId? _cloneSourceAreaId;
    private SpinBox? _resizeWidth;
    private SpinBox? _resizeDepth;
    private OptionButton? _resizeAnchor;

    public int AreaCount => _visibleAreaIds.Count;

    public int ValidationCount => _validationList?.ItemCount ?? 0;

    public void Initialize(
        Func<ProjectState?> getState,
        Func<AreaId?> getActiveAreaId,
        Func<AreaId, bool> activateArea,
        Func<string, AreaKind, AreaDimensions, AreaTemplateKind, AreaId?> createArea,
        Func<AreaId, PortalLink, bool> upsertPortal,
        Func<AreaId, string, AreaCloneMode, ExternalPortalPolicy, AreaId?> duplicateArea,
        Func<AreaId, AreaDimensions, AreaResizeAnchor, bool> resizeArea,
        Action openNavigation,
        Action<Window> brandWindow)
    {
        _getState = getState ?? throw new ArgumentNullException(nameof(getState));
        _getActiveAreaId = getActiveAreaId ?? throw new ArgumentNullException(nameof(getActiveAreaId));
        _activateArea = activateArea ?? throw new ArgumentNullException(nameof(activateArea));
        _createArea = createArea ?? throw new ArgumentNullException(nameof(createArea));
        _upsertPortal = upsertPortal ?? throw new ArgumentNullException(nameof(upsertPortal));
        _duplicateArea = duplicateArea ?? throw new ArgumentNullException(nameof(duplicateArea));
        _resizeArea = resizeArea ?? throw new ArgumentNullException(nameof(resizeArea));
        _openNavigation = openNavigation ?? throw new ArgumentNullException(nameof(openNavigation));
        _brandWindow = brandWindow ?? throw new ArgumentNullException(nameof(brandWindow));

        Title = "Zones · OpenRPG Forge";
        MinSize = new Vector2I(1180, 760);
        Size = new Vector2I(1180, 760);
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
        PopupCentered(new Vector2I(1180, 760));
        _brandWindow?.Invoke(this);
    }

    public void Refresh(AreaId? preferredAreaId = null)
    {
        var state = _getState?.Invoke();
        if (state is null || _areaList is null)
        {
            return;
        }

        var activeAreaId = preferredAreaId ?? _getActiveAreaId?.Invoke();
        _visibleAreaIds.Clear();
        _areaList.Clear();
        foreach (var area in state.Project.Areas.OrderBy(area => area.DisplayName, StringComparer.Ordinal))
        {
            _visibleAreaIds.Add(area.Id);
            var active = area.Id == activeAreaId ? "◆ " : string.Empty;
            _areaList.AddItem(
                $"{active}{area.DisplayName}\n{FormatKind(area.Kind)} · {area.Dimensions.Width} × {area.Dimensions.Depth} · " +
                $"{area.Entities.Count} entité(s) · {area.Portals.Count} transition(s)");
        }

        _areaSummary!.Text = $"{state.Project.Areas.Count} zone(s) · documents indépendants au prochain enregistrement";
        if (activeAreaId is not null)
        {
            var performance = AreaPerformanceAnalyzer.Analyze(state.Project, activeAreaId);
            _areaSummary.Text += $" · budget {(performance.IsWithinBudget ? "OK" : "DÉPASSÉ")} · " +
                $"{performance.UniqueAssetCount} assets · {performance.OccupiedOcclusionSectors} secteurs · " +
                $"LOD {performance.NearLodCount}/{performance.MidLodCount}/{performance.FarLodCount}";
        }
        var selectedIndex = activeAreaId is null ? -1 : _visibleAreaIds.FindIndex(id => id == activeAreaId);
        if (selectedIndex < 0 && _visibleAreaIds.Count > 0)
        {
            selectedIndex = 0;
        }

        if (selectedIndex >= 0)
        {
            _areaList.Select(selectedIndex);
            _selectedAreaId = _visibleAreaIds[selectedIndex];
        }

        RefreshActiveArea(state);
        RefreshValidation(state);
    }

    public bool SelectAreaForSmokeTest(AreaId areaId)
    {
        Refresh(areaId);
        var index = _visibleAreaIds.FindIndex(id => id == areaId);
        if (index < 0)
        {
            return false;
        }

        SelectArea(index);
        return _getActiveAreaId?.Invoke() == areaId;
    }

    private void BuildUi()
    {
        var background = new PanelContainer { AnchorRight = 1, AnchorBottom = 1 };
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

        var heading = new Label { Text = "ZONES & TRANSITIONS", ThemeTypeVariation = "HeaderSmall" };
        heading.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(heading);
        root.AddChild(new Label
        {
            Text = "Composez le monde par documents indépendants, basculez la zone active et reliez ses points d’ancrage.",
        });

        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 14);
        root.AddChild(body);

        var catalog = AddCard(body, new Vector2(330, 0));
        catalog.AddChild(new Label { Text = "MONDE DU MODULE" });
        _areaSummary = MutedLabel("Aucune zone");
        catalog.AddChild(_areaSummary);
        _areaList = new ItemList
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            AllowReselect = true,
        };
        catalog.AddChild(_areaList);
        catalog.AddChild(MutedLabel("◆ indique la zone affichée et éditée dans le workspace Monde."));
        catalog.AddChild(new HSeparator());
        catalog.AddChild(new Label { Text = "COPIE DE ZONE" });
        _cloneName = new LineEdit { PlaceholderText = "Nom de la copie" };
        _cloneName.TextChanged += _ => ResetAreaClonePreview();
        catalog.AddChild(_cloneName);
        _cloneMode = new OptionButton();
        _cloneMode.AddItem("Clone complet");
        _cloneMode.AddItem("Gabarit vide");
        _cloneMode.ItemSelected += _ => ResetAreaClonePreview();
        catalog.AddChild(_cloneMode);
        _preserveExternalPortals = new CheckBox
        {
            Text = "Conserver les transitions externes",
            ButtonPressed = true,
        };
        _preserveExternalPortals.Toggled += _ => ResetAreaClonePreview();
        catalog.AddChild(_preserveExternalPortals);
        var previewCloneButton = new Button { Text = "Prévisualiser la copie" };
        previewCloneButton.Pressed += PreviewAreaClone;
        catalog.AddChild(previewCloneButton);
        _applyCloneButton = new Button
        {
            Text = "Créer la copie",
            Disabled = true,
        };
        _applyCloneButton.AddThemeStyleboxOverride("normal", Card("38264f", "8060aa", 1, 8));
        _applyCloneButton.Pressed += ApplyAreaClone;
        catalog.AddChild(_applyCloneButton);

        var create = AddCard(body, new Vector2(330, 0));
        create.AddChild(new Label { Text = "NOUVELLE ZONE" });
        create.AddChild(new Label { Text = "Nom affiché" });
        _areaName = new LineEdit { PlaceholderText = "Ex. Lisière de la forêt" };
        create.AddChild(_areaName);
        create.AddChild(new Label { Text = "Environnement" });
        _areaKind = new OptionButton();
        foreach (var kind in _areaKinds)
        {
            _areaKind.AddItem(FormatKind(kind));
        }
        create.AddChild(_areaKind);
        var dimensions = new HBoxContainer();
        dimensions.AddThemeConstantOverride("separation", 8);
        create.AddChild(dimensions);
        var widthColumn = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        widthColumn.AddChild(new Label { Text = "Largeur" });
        _areaWidth = DimensionSpinBox();
        widthColumn.AddChild(_areaWidth);
        dimensions.AddChild(widthColumn);
        var depthColumn = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        depthColumn.AddChild(new Label { Text = "Profondeur" });
        _areaDepth = DimensionSpinBox();
        depthColumn.AddChild(_areaDepth);
        dimensions.AddChild(depthColumn);
        create.AddChild(new Label { Text = "Gabarit" });
        _areaTemplate = new OptionButton();
        foreach (var template in _templates)
        {
            _areaTemplate.AddItem(FormatTemplate(template));
        }
        _areaTemplate.Selected = Array.IndexOf(_templates, AreaTemplateKind.Playable);
        create.AddChild(_areaTemplate);
        create.AddChild(MutedLabel(
            "Le gabarit jouable ajoute un point d’apparition si le module possède un blueprint PlayerSpawn."));
        var createButton = new Button { Text = "+ Créer la zone", CustomMinimumSize = new Vector2(0, 42) };
        createButton.AddThemeColorOverride("font_color", Colors.White);
        createButton.AddThemeStyleboxOverride("normal", Card("d84f32", "ff8b63", 1, 8));
        createButton.Pressed += CreateArea;
        create.AddChild(createButton);

        var transitions = AddCard(body, new Vector2(410, 0), expand: true);
        transitions.AddChild(new Label { Text = "TRANSITION DEPUIS LA ZONE ACTIVE" });
        _activeAreaSummary = new Label { Text = "Aucune zone active", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _activeAreaSummary.AddThemeColorOverride("font_color", new Color("ffb07c"));
        transitions.AddChild(_activeAreaSummary);
        var resizeRow = new HBoxContainer(); transitions.AddChild(resizeRow);
        _resizeWidth = DimensionSpinBox(); _resizeDepth = DimensionSpinBox();
        _resizeAnchor = new OptionButton(); foreach (var anchor in Enum.GetValues<AreaResizeAnchor>()) _resizeAnchor.AddItem(anchor.ToString());
        resizeRow.AddChild(_resizeWidth); resizeRow.AddChild(_resizeDepth); resizeRow.AddChild(_resizeAnchor);
        var resizeButton = new Button { Text = "Prévisualiser & redimensionner" }; resizeButton.Pressed += ResizeActiveArea; transitions.AddChild(resizeButton);
        transitions.AddChild(new Label { Text = "Point d’ancrage source" });
        _sourceEntity = new OptionButton();
        transitions.AddChild(_sourceEntity);
        transitions.AddChild(new Label { Text = "Zone de destination" });
        _destinationArea = new OptionButton();
        transitions.AddChild(_destinationArea);
        transitions.AddChild(new Label { Text = "Point d’arrivée" });
        _destinationEntity = new OptionButton();
        transitions.AddChild(_destinationEntity);
        transitions.AddChild(new Label { Text = "Nom de la transition" });
        _portalName = new LineEdit { PlaceholderText = "Ex. Vers les ruines" };
        transitions.AddChild(_portalName);
        _bidirectional = new CheckBox { Text = "Liaison bidirectionnelle", ButtonPressed = true };
        transitions.AddChild(_bidirectional);
        _preload = new CheckBox { Text = "Précharger la destination" };
        transitions.AddChild(_preload);
        var portalButton = new Button { Text = "Relier les zones", CustomMinimumSize = new Vector2(0, 42) };
        portalButton.AddThemeStyleboxOverride("normal", Card("38264f", "8060aa", 1, 8));
        portalButton.Pressed += CreatePortal;
        transitions.AddChild(portalButton);

        var validation = new PanelContainer { CustomMinimumSize = new Vector2(0, 150) };
        validation.AddThemeStyleboxOverride("panel", Card("100d1b", "47365e", 1, 10));
        root.AddChild(validation);
        var validationMargin = new MarginContainer();
        validationMargin.AddThemeConstantOverride("margin_left", 14);
        validationMargin.AddThemeConstantOverride("margin_top", 10);
        validationMargin.AddThemeConstantOverride("margin_right", 14);
        validationMargin.AddThemeConstantOverride("margin_bottom", 10);
        validation.AddChild(validationMargin);
        var validationRow = new HBoxContainer();
        validationRow.AddThemeConstantOverride("separation", 12);
        validationMargin.AddChild(validationRow);
        var validationHeading = new VBoxContainer { CustomMinimumSize = new Vector2(260, 0) };
        validationHeading.AddChild(new Label { Text = "VALIDATION DES ZONES" });
        _validationSummary = MutedLabel("Validation en attente");
        validationHeading.AddChild(_validationSummary);
        var navigationButton = new Button { Text = "Navigation & géométrie…", CustomMinimumSize = new Vector2(0, 38) };
        navigationButton.AddThemeStyleboxOverride("normal", Card("38264f", "8060aa", 1, 8));
        navigationButton.Pressed += () => _openNavigation?.Invoke();
        validationHeading.AddChild(navigationButton);
        validationRow.AddChild(validationHeading);
        _validationList = new ItemList { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        validationRow.AddChild(_validationList);

        _status = new Label { Text = "Prêt." };
        _status.AddThemeColorOverride("font_color", new Color("9ee6b3"));
        root.AddChild(_status);
    }

    private void ConnectSignals()
    {
        CloseRequested += Hide;
        _areaList!.ItemSelected += SelectArea;
        _destinationArea!.ItemSelected += _ => RefreshDestinationEntities();
    }

    private void ResizeActiveArea()
    {
        var areaId = _getActiveAreaId?.Invoke();
        if (areaId is null) return;
        var dimensions = new AreaDimensions((int)_resizeWidth!.Value, (int)_resizeDepth!.Value);
        var anchor = (AreaResizeAnchor)_resizeAnchor!.Selected;
        _status!.Text = _resizeArea?.Invoke(areaId, dimensions, anchor) == true
            ? "✓ Zone redimensionnée par une transaction atomique" : "✕ Redimensionnement refusé";
        Refresh(areaId);
    }

    private void SelectArea(long index)
    {
        var state = _getState?.Invoke();
        if (state is null || index < 0 || index >= _visibleAreaIds.Count)
        {
            return;
        }

        var areaId = _visibleAreaIds[(int)index];
        _selectedAreaId = areaId;
        ResetAreaClonePreview();
        if (_activateArea?.Invoke(areaId) == true)
        {
            var area = state.Project.Areas.Single(candidate => candidate.Id == areaId);
            SetStatus($"Zone active : « {area.DisplayName} ».", false);
        }
        Refresh(areaId);
    }

    private void CreateArea()
    {
        if (_areaName is null || _areaKind is null || _areaWidth is null || _areaDepth is null ||
            _areaTemplate is null || _areaKind.Selected < 0 || _areaTemplate.Selected < 0)
        {
            return;
        }

        var name = _areaName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            SetStatus("Donnez un nom à la nouvelle zone.", true);
            return;
        }

        try
        {
            var dimensions = new AreaDimensions((int)_areaWidth.Value, (int)_areaDepth.Value);
            var areaId = _createArea?.Invoke(
                name,
                _areaKinds[_areaKind.Selected],
                dimensions,
                _templates[_areaTemplate.Selected]);
            if (areaId is null)
            {
                SetStatus("La création de la zone a été refusée. Consultez les diagnostics du projet.", true);
                return;
            }

            _areaName.Text = string.Empty;
            SetStatus($"Zone « {name} » créée et activée.", false);
            Refresh(areaId);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            SetStatus(exception.Message, true);
        }
    }

    private void CreatePortal()
    {
        var state = _getState?.Invoke();
        var sourceAreaId = _getActiveAreaId?.Invoke();
        if (state is null || sourceAreaId is null || _sourceEntity is null || _destinationArea is null ||
            _destinationEntity is null || _portalName is null || _bidirectional is null || _preload is null ||
            _sourceEntity.Selected < 0 || _sourceEntity.Selected >= _sourceEntityIds.Count ||
            _destinationArea.Selected < 0 || _destinationArea.Selected >= _destinationAreaIds.Count ||
            _destinationEntity.Selected < 0 || _destinationEntity.Selected >= _destinationEntityIds.Count)
        {
            SetStatus("Choisissez une source et une destination valides.", true);
            return;
        }

        var destinationAreaId = _destinationAreaIds[_destinationArea.Selected];
        var displayName = _portalName.Text.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            var destination = state.Project.Areas.Single(area => area.Id == destinationAreaId);
            displayName = $"Vers {destination.DisplayName}";
        }

        var portal = new PortalLink(
            PortalId.From(Guid.NewGuid()),
            displayName,
            _sourceEntityIds[_sourceEntity.Selected],
            destinationAreaId,
            _destinationEntityIds[_destinationEntity.Selected],
            _bidirectional.ButtonPressed,
            _preload.ButtonPressed);
        if (_upsertPortal?.Invoke(sourceAreaId, portal) != true)
        {
            SetStatus("La transition a été refusée. Vérifiez ses références.", true);
            return;
        }

        _portalName.Text = string.Empty;
        SetStatus($"Transition « {displayName} » créée.", false);
        Refresh(sourceAreaId);
    }

    private void PreviewAreaClone()
    {
        var state = _getState?.Invoke();
        if (state is null || _selectedAreaId is null || _cloneMode is null ||
            _preserveExternalPortals is null || _cloneName is null || _applyCloneButton is null)
        {
            SetStatus("Sélectionnez une zone à copier.", true);
            return;
        }

        var source = state.Project.Areas.Single(area => area.Id == _selectedAreaId);
        var mode = _cloneMode.Selected == 1 ? AreaCloneMode.Template : AreaCloneMode.Full;
        var externalPolicy = _preserveExternalPortals.ButtonPressed
            ? ExternalPortalPolicy.Preserve
            : ExternalPortalPolicy.Drop;
        var preview = AreaDuplicationPreview.Create(source, mode, externalPolicy);
        if (string.IsNullOrWhiteSpace(_cloneName.Text))
        {
            _cloneName.Text = $"{source.DisplayName} — Copie";
        }

        _cloneSourceAreaId = source.Id;
        _applyCloneButton.Disabled = false;
        SetStatus(
            $"Aperçu · {preview.EntityCount} entité(s), {preview.InternalPortalCount} transition(s) interne(s), " +
            $"{preview.ExternalPortalCount} externe(s) · nouveaux UUID à l’application.",
            false);
    }

    private void ApplyAreaClone()
    {
        if (_cloneSourceAreaId is null || _cloneName is null || _cloneMode is null ||
            _preserveExternalPortals is null)
        {
            SetStatus("Prévisualisez d’abord la copie de zone.", true);
            return;
        }

        var name = _cloneName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            SetStatus("Donnez un nom à la copie de zone.", true);
            return;
        }

        var mode = _cloneMode.Selected == 1 ? AreaCloneMode.Template : AreaCloneMode.Full;
        var externalPolicy = _preserveExternalPortals.ButtonPressed
            ? ExternalPortalPolicy.Preserve
            : ExternalPortalPolicy.Drop;
        var cloneId = _duplicateArea?.Invoke(_cloneSourceAreaId, name, mode, externalPolicy);
        if (cloneId is null)
        {
            SetStatus("La copie a été refusée. Consultez les diagnostics du projet.", true);
            return;
        }

        SetStatus($"Zone « {name} » créée depuis l’aperçu.", false);
        ResetAreaClonePreview();
        Refresh(cloneId);
    }

    private void ResetAreaClonePreview()
    {
        _cloneSourceAreaId = null;
        if (_applyCloneButton is not null)
        {
            _applyCloneButton.Disabled = true;
        }
    }

    private void RefreshActiveArea(ProjectState state)
    {
        var activeId = _getActiveAreaId?.Invoke();
        var activeArea = state.Project.Areas.FirstOrDefault(area => area.Id == activeId) ?? state.Project.Areas.FirstOrDefault();
        _sourceEntityIds.Clear();
        _sourceEntity!.Clear();
        _destinationAreaIds.Clear();
        _destinationArea!.Clear();
        if (activeArea is null)
        {
            _activeAreaSummary!.Text = "Aucune zone active";
            _destinationEntityIds.Clear();
            _destinationEntity!.Clear();
            return;
        }

        _activeAreaSummary!.Text =
            $"{activeArea.DisplayName} · {FormatKind(activeArea.Kind)} · document v{activeArea.SchemaVersion}";
        _resizeWidth!.Value = activeArea.Dimensions.Width;
        _resizeDepth!.Value = activeArea.Dimensions.Depth;
        foreach (var entity in activeArea.Entities)
        {
            _sourceEntityIds.Add(entity.Id);
            _sourceEntity.AddItem(FormatEntity(state, entity));
        }

        foreach (var area in state.Project.Areas.OrderBy(area => area.DisplayName, StringComparer.Ordinal))
        {
            _destinationAreaIds.Add(area.Id);
            _destinationArea.AddItem(area.DisplayName);
        }

        var preferredDestination = _destinationAreaIds.FindIndex(id => id != activeArea.Id);
        _destinationArea.Selected = preferredDestination >= 0 ? preferredDestination : 0;
        RefreshDestinationEntities();
    }

    private void RefreshDestinationEntities()
    {
        var state = _getState?.Invoke();
        _destinationEntityIds.Clear();
        _destinationEntity!.Clear();
        if (state is null || _destinationArea!.Selected < 0 ||
            _destinationArea.Selected >= _destinationAreaIds.Count)
        {
            return;
        }

        var destination = state.Project.Areas.Single(area => area.Id == _destinationAreaIds[_destinationArea.Selected]);
        foreach (var entity in destination.Entities)
        {
            _destinationEntityIds.Add(entity.Id);
            _destinationEntity.AddItem(FormatEntity(state, entity));
        }
    }

    private void RefreshValidation(ProjectState state)
    {
        var diagnostics = ProjectValidator.Validate(state.Project)
            .Where(item => item.Path.StartsWith("areas/", StringComparison.Ordinal) ||
                item.Code.StartsWith("area.", StringComparison.Ordinal))
            .ToList();
        foreach (var area in state.Project.Areas)
        {
            diagnostics.AddRange(AreaGeometryValidator.Validate(state.Project, area));
        }

        var activeArea = state.Project.Areas.FirstOrDefault(area => area.Id == _getActiveAreaId?.Invoke());
        if (activeArea is not null)
        {
            try
            {
                diagnostics.AddRange(AreaNavigationValidator.AnalyzeAll(state.Project, activeArea)
                    .SelectMany(analysis => analysis.Diagnostics));
            }
            catch (InvalidOperationException exception)
            {
                diagnostics.Add(new DomainDiagnostic(
                    "area.navigation.bake_too_dense",
                    exception.Message,
                    $"areas/{activeArea.Id}/navigation/cellSize"));
            }
        }

        var distinctDiagnostics = diagnostics.Distinct().ToArray();
        _validationList!.Clear();
        if (distinctDiagnostics.Length == 0)
        {
            _validationSummary!.Text = "✓ Références, géométrie et navigation valides";
            _validationSummary.AddThemeColorOverride("font_color", new Color("83d6ae"));
            _validationList.AddItem("Aucune erreur de zone pour la révision courante.");
            _validationList.SetItemSelectable(0, false);
            return;
        }

        _validationSummary!.Text = $"{distinctDiagnostics.Length} problème(s) à corriger";
        _validationSummary.AddThemeColorOverride("font_color", new Color("ff8c8c"));
        foreach (var diagnostic in distinctDiagnostics)
        {
            _validationList.AddItem($"{diagnostic.Code} · {diagnostic.Message}");
            _validationList.SetItemTooltip(_validationList.ItemCount - 1, diagnostic.Path);
        }
    }

    private static string FormatEntity(ProjectState state, EntityInstance entity)
    {
        var blueprint = state.Project.Blueprints.FirstOrDefault(item => item.Id == entity.BlueprintId);
        return $"{blueprint?.DisplayName ?? "Blueprint manquant"} · {entity.Id.ToString()[..8]}";
    }

    private static SpinBox DimensionSpinBox() => new()
    {
        MinValue = AreaDimensions.MinimumSize,
        MaxValue = AreaDimensions.MaximumSize,
        Step = 4,
        Value = 64,
        CustomMinimumSize = new Vector2(0, 38),
    };

    private static Label MutedLabel(string text)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeColorOverride("font_color", new Color("a899b9"));
        return label;
    }

    private static VBoxContainer AddCard(HBoxContainer parent, Vector2 minimumSize, bool expand = false)
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

    private void SetStatus(string text, bool error)
    {
        _status!.Text = text;
        _status.AddThemeColorOverride("font_color", error ? new Color("ff8c8c") : new Color("9ee6b3"));
    }

    private static string FormatKind(AreaKind kind) => kind switch
    {
        AreaKind.Interior => "Intérieur",
        AreaKind.Exterior => "Extérieur",
        _ => kind.ToString(),
    };

    private static string FormatTemplate(AreaTemplateKind template) => template switch
    {
        AreaTemplateKind.Empty => "Vide",
        AreaTemplateKind.Playable => "Jouable · point d’apparition",
        _ => template.ToString(),
    };
}
#endif
