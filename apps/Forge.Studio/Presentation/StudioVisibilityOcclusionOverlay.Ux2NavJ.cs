using Forge.Domain.Areas;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;
using System.Text.RegularExpressions;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioVisibilityOcclusionOverlayUx2NavJ : Control
{
    private const string ProtectedNameUx2NavJ = "Nom existant protégé";
    private const string ProtectedConditionUx2NavJ = "Condition existante protégée";
    private const string ProtectedAssetUx2NavJ = "Asset existant protégé";
    private static readonly StudioUx2NavJItemKind[] ItemKinds = Enum.GetValues<StudioUx2NavJItemKind>();
    private static readonly AreaVisibilityPortalMode[] PortalModes = Enum.GetValues<AreaVisibilityPortalMode>();

    private readonly StudioSessionController _sourceSession;
    private readonly StudioWorldViewport _worldViewport;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceTempRoot;
    private StudioUx2NavJEvidenceSourceSnapshot? _evidenceSourceSnapshot;
    private bool _lastEvidenceSourceUntouched = true;
    private bool _lastEvidenceResourcesReleased = true;
    private bool _lastSourceRestored = true;
    private bool _lastUiTransaction;
    private bool _updating;
    private bool _explicitAreaSelection;
    private Guid? _selectedItemId;
    private StudioUx2NavJDraft? _draft;
    private StudioUx2NavJPreview? _lastPreview;
    private IReadOnlyList<StudioUx2NavJAreaChoice> _areaChoices = [];
    private IReadOnlyList<Guid> _roomChoiceIds = [];

    private readonly PanelContainer _dialog;
    private readonly OptionButton _area;
    private readonly OptionButton _kind;
    private readonly OptionButton _item;
    private readonly Label _selection;
    private readonly VBoxContainer _editor;
    private readonly CheckBox _occlusionCulling;
    private readonly CheckBox _distanceStreaming;
    private readonly CheckBox _cellVisualizer;
    private readonly SpinBox _cellSize;
    private readonly SpinBox _streamingDistance;
    private readonly Button _addItem;
    private readonly Button _removeItem;
    private readonly VBoxContainer _itemFields;
    private readonly Label _itemKind;
    private readonly LineEdit _name;
    private readonly SpinBox _positionX;
    private readonly SpinBox _positionY;
    private readonly SpinBox _positionZ;
    private readonly SpinBox _scaleX;
    private readonly SpinBox _scaleY;
    private readonly SpinBox _scaleZ;
    private readonly VBoxContainer _roomFields;
    private readonly CheckBox _roomExterior;
    private readonly SpinBox _roomStreaming;
    private readonly VBoxContainer _portalFields;
    private readonly OptionButton _portalSource;
    private readonly OptionButton _portalDestination;
    private readonly OptionButton _portalMode;
    private readonly LineEdit _portalCondition;
    private readonly CheckBox _portalTwoWay;
    private readonly VBoxContainer _occluderFields;
    private readonly CheckBox _occluderDynamic;
    private readonly LineEdit _occluderAsset;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly VBoxContainer _runtime;
    private readonly Label _summary;
    private readonly Label _boundary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? VisibilityChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _areaChoices.Count;
    internal bool HasExplicitSelection => _explicitAreaSelection && _selectedItemId is not null;
    internal bool CanonicalPreviewReady => _lastPreview?.Succeeded == true;
    internal bool ShowsViewportOverlay => _lastPreview?.Overlay is { } overlay &&
                                           overlay.Markers.Count + overlay.Segments.Count > 0;
    internal bool ShowsStructuredDiagnostics => _lastPreview is { Succeeded: true } &&
                                                _lastPreview.Diagnostics.Count == 0;
    internal bool ShowsBoundedRuntimeClaim => _lastPreview?.Runtime?.Boundary.Contains(
        "aucun culling GPU", StringComparison.OrdinalIgnoreCase) == true;
    internal bool ShowsHumanSurface => _selection.Text.Contains("sélectionné", StringComparison.OrdinalIgnoreCase) &&
                                       !_selection.Text.Contains("0000-", StringComparison.Ordinal);
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                          !ReferenceEquals(_session, _sourceSession);
    internal bool EvidenceSourceUntouched => _lastEvidenceSourceUntouched;
    internal bool EvidenceResourcesReleased => _lastEvidenceResourcesReleased;
    internal bool SourceSessionRestored => _lastSourceRestored;
    internal bool LastUiTransaction => _lastUiTransaction;

    internal StudioVisibilityOcclusionOverlayUx2NavJ(StudioSessionController session,
        StudioWorldViewport worldViewport)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = session;
        _worldViewport = worldViewport ?? throw new ArgumentNullException(nameof(worldViewport));
        Name = "StudioVisibilityOcclusionOverlayUx2NavJ";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;

        var veil = new ColorRect { Color = new Color(0.02f, 0.015f, 0.03f, .72f) };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(veil);
        _dialog = StudioStyle.Panel("VisibilityOcclusionDialogUx2NavJ", StudioStyle.Surface, 12, 1);
        AddChild(_dialog);
        var margin = StudioStyle.Margin(14, 12, 14, 12);
        _dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 7);
        margin.AddChild(root);

        var header = RowUx2NavJ();
        root.AddChild(header);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("NAVIGATION · OCCLUSION ET VISIBILITÉ", 10,
            StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Volumes, passages et écrans d’occlusion", 19));
        heading.AddChild(StudioStyle.Label(
            "ORF-AREA-033 · configure les rooms, portails conditionnels, occluders, cellules et streaming. " +
            "La politique LOD/HLOD reste protégée et appartient à la surface Carte & LOD.",
            9, StudioStyle.Muted, wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.TooltipText = "Fermer sans appliquer le brouillon · Échap";
        close.Pressed += Close;
        header.AddChild(close);

        var selector = RowUx2NavJ();
        root.AddChild(selector);
        _area = StudioUx2NavCControls.Options();
        _area.ItemSelected += SelectArea;
        selector.AddChild(StudioUx2NavCControls.Field("Zone", _area, 210));
        _kind = StudioUx2NavCControls.Options("Volumes", "Passages", "Écrans");
        _kind.ItemSelected += SelectKind;
        selector.AddChild(StudioUx2NavCControls.Field("Collection", _kind, 130));
        _item = StudioUx2NavCControls.Options();
        _item.ItemSelected += SelectItem;
        selector.AddChild(StudioUx2NavCControls.Field("Élément nommé", _item, 230));
        _addItem = StudioStyle.Button("Ajouter", compact: true);
        _addItem.Pressed += AddItem;
        selector.AddChild(_addItem);
        _removeItem = StudioStyle.Button("Retirer", compact: true);
        _removeItem.Pressed += RemoveItem;
        selector.AddChild(_removeItem);
        _selection = StudioStyle.Label(
            "Choisissez explicitement une zone ; aucune valeur n’est chargée automatiquement.",
            10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        var bodyHost = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 430),
            ClipContents = true,
        };
        root.AddChild(bodyHost);
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true,
        };
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bodyHost.AddChild(scroll);
        _editor = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _editor.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_editor);

        var global = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var options = RowUx2NavJ();
        _occlusionCulling = StudioUx2NavCControls.Check("Activer l’occlusion culling");
        _distanceStreaming = StudioUx2NavCControls.Check("Streaming par distance");
        _cellVisualizer = StudioUx2NavCControls.Check("Afficher les cellules");
        options.AddChild(_occlusionCulling);
        options.AddChild(_distanceStreaming);
        options.AddChild(_cellVisualizer);
        global.AddChild(options);
        var grid = RowUx2NavJ();
        _cellSize = StudioUx2NavCControls.Number(1, 512, 1);
        _streamingDistance = StudioUx2NavCControls.Number(1, float.MaxValue, 8);
        grid.AddChild(StudioUx2NavCControls.Field("Taille cellule (m)", _cellSize));
        grid.AddChild(StudioUx2NavCControls.Field("Distance streaming (m)", _streamingDistance));
        global.AddChild(grid);
        _editor.AddChild(StudioUx2NavCControls.Card("VisibilityPolicyUx2NavJ", "Politique de zone",
            "Ces options alimentent le plan canonique ; aucun culling GPU n’est déclenché dans l’aperçu.",
            global));

        var itemContent = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _itemKind = StudioStyle.Label("Aucun élément sélectionné", 10, StudioStyle.Accent);
        itemContent.AddChild(_itemKind);
        _itemFields = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _itemFields.AddThemeConstantOverride("separation", 6);
        itemContent.AddChild(_itemFields);
        _name = StudioUx2NavCControls.Text("Nom humain");
        _itemFields.AddChild(StudioUx2NavCControls.Field("Nom", _name));
        var position = RowUx2NavJ();
        _positionX = NumberUx2NavJ();
        _positionY = NumberUx2NavJ();
        _positionZ = NumberUx2NavJ();
        position.AddChild(StudioUx2NavCControls.Field("Position X", _positionX));
        position.AddChild(StudioUx2NavCControls.Field("Position Y", _positionY));
        position.AddChild(StudioUx2NavCControls.Field("Position Z", _positionZ));
        _itemFields.AddChild(position);
        var scale = RowUx2NavJ();
        _scaleX = PositiveNumberUx2NavJ();
        _scaleY = PositiveNumberUx2NavJ();
        _scaleZ = PositiveNumberUx2NavJ();
        scale.AddChild(StudioUx2NavCControls.Field("Largeur X", _scaleX));
        scale.AddChild(StudioUx2NavCControls.Field("Hauteur Y", _scaleY));
        scale.AddChild(StudioUx2NavCControls.Field("Profondeur Z", _scaleZ));
        _itemFields.AddChild(scale);
        _itemFields.AddChild(StudioStyle.Label(
            "L’orientation quaternion et les overrides inconnus restent préservés exactement.",
            9, StudioStyle.Muted, wrap: true));

        _roomFields = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var roomRow = RowUx2NavJ();
        _roomExterior = StudioUx2NavCControls.Check("Volume extérieur");
        _roomStreaming = StudioUx2NavCControls.Number(0, float.MaxValue, 8);
        roomRow.AddChild(_roomExterior);
        roomRow.AddChild(StudioUx2NavCControls.Field("Distance propre (m)", _roomStreaming));
        _roomFields.AddChild(roomRow);
        _itemFields.AddChild(_roomFields);

        _portalFields = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var portalRooms = RowUx2NavJ();
        _portalSource = StudioUx2NavCControls.Options();
        _portalDestination = StudioUx2NavCControls.Options();
        portalRooms.AddChild(StudioUx2NavCControls.Field("Depuis", _portalSource));
        portalRooms.AddChild(StudioUx2NavCControls.Field("Vers", _portalDestination));
        _portalFields.AddChild(portalRooms);
        var portalPolicy = RowUx2NavJ();
        _portalMode = StudioUx2NavCControls.Options("Ouvert", "Fermé", "Conditionnel");
        _portalCondition = StudioUx2NavCControls.Text("ex. quête.cristal.ouverte");
        _portalTwoWay = StudioUx2NavCControls.Check("Double sens");
        portalPolicy.AddChild(StudioUx2NavCControls.Field("État", _portalMode));
        portalPolicy.AddChild(StudioUx2NavCControls.Field("Condition nommée", _portalCondition, 220));
        portalPolicy.AddChild(_portalTwoWay);
        _portalFields.AddChild(portalPolicy);
        _itemFields.AddChild(_portalFields);

        _occluderFields = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var occluderRow = RowUx2NavJ();
        _occluderDynamic = StudioUx2NavCControls.Check("Écran dynamique");
        _occluderAsset = StudioUx2NavCControls.Text("Asset de maillage facultatif");
        occluderRow.AddChild(_occluderDynamic);
        occluderRow.AddChild(StudioUx2NavCControls.Field("Asset de maillage", _occluderAsset, 260));
        _occluderFields.AddChild(occluderRow);
        _itemFields.AddChild(_occluderFields);
        _editor.AddChild(StudioUx2NavCControls.Card("VisibilityElementUx2NavJ", "Élément sélectionné",
            "L’ajout, le retrait ou la modification restent dans le brouillon jusqu’à l’application.",
            itemContent));

        var previewColumns = RowUx2NavJ();
        _diff = PreviewColumnUx2NavJ(previewColumns, "DIFF SÉMANTIQUE");
        _diagnostics = PreviewColumnUx2NavJ(previewColumns, "DIAGNOSTICS");
        _runtime = PreviewColumnUx2NavJ(previewColumns, "PLAN RUNTIME BORNÉ");
        _summary = StudioStyle.Label("Aucun aperçu calculé.", 10, StudioStyle.Text, wrap: true);
        var previewContent = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        previewContent.AddChild(previewColumns);
        previewContent.AddChild(_summary);
        _boundary = StudioStyle.Label(
            "Limite : aucun culling GPU, mesh HLOD, streaming réel, réseau ou préchargement n’est exécuté.",
            9, StudioStyle.Warning, wrap: true);
        previewContent.AddChild(_boundary);
        _editor.AddChild(StudioUx2NavCControls.Card("VisibilityPreviewUx2NavJ", "Aperçu avant application",
            "Le viewport montre un overlay de diagnostic ; le document reste inchangé.", previewContent));

        var footer = RowUx2NavJ();
        _status = StudioStyle.Label("Prêt · aucune mutation.", 9, StudioStyle.Muted, wrap: true);
        _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        footer.AddChild(_status);
        _preview = StudioStyle.Button("Prévisualiser", compact: true);
        _preview.Pressed += Preview;
        footer.AddChild(_preview);
        _apply = StudioStyle.Button("Appliquer", compact: true);
        _apply.Pressed += Apply;
        footer.AddChild(_apply);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _undo.Pressed += Undo;
        footer.AddChild(_undo);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.Pressed += Redo;
        footer.AddChild(_redo);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        _save.Pressed += Save;
        footer.AddChild(_save);
        root.AddChild(footer);

        WireDraftSignals();
        Resized += UpdateDialogBounds;
        TreeExiting += Close;
        ClearSelection();
    }

    internal void Open()
    {
        ReleaseEvidenceWorkspace();
        _session = _sourceSession;
        _sourceSession.CancelVisibilityApprovalUx2NavJ();
        Visible = true;
        MouseFilter = MouseFilterEnum.Stop;
        PopulateAreas();
        ClearSelection();
        UpdateDialogBounds();
        SetStatus("Choisissez explicitement une zone ; aucune fiche n’est chargée automatiquement.");
    }

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null) return;
        if (_evidenceSession is not null) _evidenceSession.CancelVisibilityApprovalUx2NavJ();
        else _sourceSession.CancelVisibilityApprovalUx2NavJ();
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        _worldViewport.ClearNavigationOverlayUx2NavB();
        _draft = null;
        _lastPreview = null;
        _selectedItemId = null;
        _explicitAreaSelection = false;
        ReleaseEvidenceWorkspace();
        RestoreSourceViewport();
    }

    internal bool PrepareEvidence(string mode)
    {
        if (!string.Equals(mode, "ux2-nav-j", StringComparison.OrdinalIgnoreCase) ||
            _sourceSession.Project is null || _sourceSession.History is null) return false;
        ReleaseEvidenceWorkspace();
        _evidenceSourceSnapshot = CaptureSourceSnapshot();
        _lastEvidenceSourceUntouched = true;
        _lastEvidenceResourcesReleased = false;
        _lastSourceRestored = false;
        var project = _sourceSession.CreateUx2NavJEvidenceProject();
        if (project is null) return false;
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-nav-j-evidence-" + Guid.NewGuid().ToString("N"));
        try
        {
            var created = ProjectSession.Create(tempRoot, project);
            if (!created.Succeeded)
            {
                _lastEvidenceResourcesReleased =
                    StudioSessionController.DeleteUx2NavJTemporaryWorkspace(tempRoot);
                return false;
            }
            _evidenceTempRoot = tempRoot;
            var evidence = new StudioSessionController();
            if (!evidence.Open(tempRoot).Succeeded)
            {
                evidence.Close();
                _lastEvidenceResourcesReleased =
                    StudioSessionController.DeleteUx2NavJTemporaryWorkspace(tempRoot);
                _evidenceTempRoot = null;
                return false;
            }
            _evidenceSession = evidence;
            _session = evidence;
            Visible = true;
            MouseFilter = MouseFilterEnum.Stop;
            PopulateAreas();
            var areaIndex = _areaChoices.ToList().FindIndex(item => item.IsEvidence);
            if (areaIndex < 0) { Close(); return false; }
            _area.Selected = areaIndex + 1;
            SelectArea(areaIndex + 1);
            _kind.Selected = Array.IndexOf(ItemKinds, StudioUx2NavJItemKind.Occluder);
            SelectKind(_kind.Selected);
            if (_draft?.Occluders.Count is not > 0) { Close(); return false; }
            _item.Selected = 1;
            SelectItem(1);
            _positionX.Value += 1;
            _occluderDynamic.ButtonPressed = true;
            _cellVisualizer.ButtonPressed = true;
            DraftChanged();
            Preview();
            UpdateDialogBounds();
            return HasExplicitSelection && CanonicalPreviewReady && ShowsViewportOverlay &&
                   ShowsStructuredDiagnostics && ShowsBoundedRuntimeClaim && UsesIsolatedEvidence &&
                   EvidenceSourceUntouched && ShowsHumanSurface;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or InvalidOperationException)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_NAV_J_EVIDENCE_FAILED type={exception.GetType().Name} message={exception.Message}");
            Close();
            return false;
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        _lastUiTransaction = false;
        try
        {
            if (!PrepareEvidence("ux2-nav-j")) { Close(); return false; }
            var countBeforeUi = _draft!.Occluders.Count;
            _addItem.EmitSignal(Button.SignalName.Pressed);
            var addedThroughUi = _draft?.Occluders.Count == countBeforeUi + 1 &&
                                 _selectedItemId is not null;
            _removeItem.EmitSignal(Button.SignalName.Pressed);
            var removedThroughUi = _draft?.Occluders.Count == countBeforeUi;
            _item.Selected = 1;
            SelectItem(1);
            _positionZ.Value += 2;
            _preview.EmitSignal(Button.SignalName.Pressed);
            var dryRun = CanonicalPreviewReady && !_apply.Disabled;
            var projectBeforeApply = _session.Project;
            _apply.EmitSignal(Button.SignalName.Pressed);
            var appliedProject = _session.Project;
            var applied = appliedProject is not null && !ReferenceEquals(projectBeforeApply, appliedProject);
            _undo.EmitSignal(Button.SignalName.Pressed);
            var undone = ReferenceEquals(projectBeforeApply, _session.Project);
            _redo.EmitSignal(Button.SignalName.Pressed);
            var redone = ReferenceEquals(appliedProject, _session.Project);
            _save.EmitSignal(Button.SignalName.Pressed);
            var saved = !_session.IsDirty;
            _lastUiTransaction = addedThroughUi && removedThroughUi && dryRun && applied && undone &&
                                 redone && saved;
        }
        catch (Exception exception)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_NAV_J_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}");
            _lastUiTransaction = false;
        }
        finally
        {
            Close();
        }
        return _lastUiTransaction && EvidenceResourcesReleased && SourceSessionRestored &&
               _lastEvidenceSourceUntouched;
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        var approval = _sourceSession.CaptureVisibilityApprovalUx2NavJ();
        Open();
        _sourceSession.RestoreVisibilityApprovalUx2NavJ(approval);
    }

    private void PopulateAreas()
    {
        _updating = true;
        _areaChoices = _session.VisibilityAreaChoicesUx2NavJ;
        _area.Clear();
        _area.AddItem("Choisir une zone…");
        foreach (var choice in _areaChoices) _area.AddItem(choice.Label);
        _area.Selected = 0;
        _updating = false;
    }

    private void ClearSelection()
    {
        _updating = true;
        _item.Clear();
        _item.AddItem("Choisir un élément nommé…");
        _item.Selected = 0;
        _kind.Selected = 0;
        _updating = false;
        _draft = null;
        _lastPreview = null;
        _selectedItemId = null;
        _explicitAreaSelection = false;
        _selection.Text = "Choisissez explicitement une zone ; aucune valeur n’est chargée automatiquement.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
        SetEditorEnabled(false);
        RenderPreview(null);
        UpdateActionState();
    }

    private void SelectArea(long index)
    {
        if (_updating) return;
        _session.CancelVisibilityApprovalUx2NavJ();
        _worldViewport.ClearNavigationOverlayUx2NavB();
        _lastPreview = null;
        _selectedItemId = null;
        _explicitAreaSelection = false;
        _draft = index > 0 && index <= _areaChoices.Count
            ? _session.CreateVisibilityDraftUx2NavJ(_areaChoices[(int)index - 1].AreaId)
            : null;
        if (_draft is null) { ClearSelection(); return; }
        _explicitAreaSelection = true;
        LoadGlobalDraft(_draft);
        PopulateItems();
        SetEditorEnabled(true);
        BindViewportToArea(_session, _draft.AreaId);
        _selection.Text = $"{_draft.AreaDisplayName} · zone sélectionnée explicitement · choisissez un élément nommé";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Success);
        SetStatus("Profil chargé · sélectionnez un élément ou modifiez la politique, puis prévisualisez.");
        UpdateActionState();
    }

    private void SelectKind(long index)
    {
        if (_updating || _draft is null) return;
        SaveSelectedItem();
        _selectedItemId = null;
        PopulateItems();
        LoadSelectedItem();
        InvalidatePreview();
    }

    private void SelectItem(long index)
    {
        if (_updating || _draft is null) return;
        SaveSelectedItem();
        var ids = CurrentItemIds();
        _selectedItemId = index > 0 && index <= ids.Count ? ids[(int)index - 1] : null;
        LoadSelectedItem();
        InvalidatePreview();
        if (_selectedItemId is not null)
        {
            _selection.Text = $"{_draft.AreaDisplayName} · {CurrentKindLabel().ToLowerInvariant()} sélectionné explicitement · non appliqué";
            _selection.AddThemeColorOverride("font_color", StudioStyle.Success);
        }
    }

    private void AddItem()
    {
        if (_draft is null) return;
        SaveControlsIntoDraft();
        switch (CurrentKind())
        {
            case StudioUx2NavJItemKind.Room:
            {
                var item = _session.CreateNewVisibilityRoomDraftUx2NavJ(_draft);
                _draft = _draft with { Rooms = _draft.Rooms.Concat([item]).ToArray() };
                _selectedItemId = item.Id;
                break;
            }
            case StudioUx2NavJItemKind.Portal:
            {
                var item = _session.CreateNewVisibilityPortalDraftUx2NavJ(_draft);
                if (item is null)
                {
                    SetStatus("Ajoutez au moins deux volumes avant de créer un passage.", true);
                    return;
                }
                _draft = _draft with { Portals = _draft.Portals.Concat([item]).ToArray() };
                _selectedItemId = item.Id;
                break;
            }
            case StudioUx2NavJItemKind.Occluder:
            {
                var item = _session.CreateNewOccluderDraftUx2NavJ(_draft);
                _draft = _draft with { Occluders = _draft.Occluders.Concat([item]).ToArray() };
                _selectedItemId = item.Id;
                break;
            }
        }
        PopulateItems();
        LoadSelectedItem();
        InvalidatePreview();
        SetStatus("Élément ajouté au brouillon · aucun document modifié.");
    }

    private void RemoveItem()
    {
        if (_draft is null || _selectedItemId is null) return;
        var id = _selectedItemId.Value;
        switch (CurrentKind())
        {
            case StudioUx2NavJItemKind.Room:
                if (_draft.Portals.Any(item => item.SourceRoomId == id || item.DestinationRoomId == id))
                {
                    SetStatus("Retirez ou redirigez d’abord les passages reliés à ce volume.", true);
                    return;
                }
                _draft = _draft with { Rooms = _draft.Rooms.Where(item => item.Id != id).ToArray() };
                break;
            case StudioUx2NavJItemKind.Portal:
                _draft = _draft with { Portals = _draft.Portals.Where(item => item.Id != id).ToArray() };
                break;
            case StudioUx2NavJItemKind.Occluder:
                _draft = _draft with { Occluders = _draft.Occluders.Where(item => item.Id != id).ToArray() };
                break;
        }
        _selectedItemId = null;
        PopulateItems();
        LoadSelectedItem();
        InvalidatePreview();
        SetStatus("Élément retiré du brouillon · aperçu sec requis.");
    }

    private void LoadGlobalDraft(StudioUx2NavJDraft draft)
    {
        _updating = true;
        _occlusionCulling.ButtonPressed = draft.OcclusionCulling;
        _distanceStreaming.ButtonPressed = draft.DistanceStreaming;
        _cellVisualizer.ButtonPressed = draft.CellVisualizer;
        _cellSize.Value = draft.CellSize;
        _streamingDistance.Value = draft.StreamingDistance;
        _updating = false;
    }

    private void PopulateItems()
    {
        _updating = true;
        _item.Clear();
        _item.AddItem("Choisir un élément nommé…");
        if (_draft is not null)
        {
            foreach (var label in CurrentItemLabels()) _item.AddItem(label);
            var ids = CurrentItemIds();
            var index = _selectedItemId is null ? -1 : ids.ToList().FindIndex(item => item == _selectedItemId);
            _item.Selected = index + 1;
            if (index < 0) _selectedItemId = null;
        }
        _updating = false;
        UpdateActionState();
    }

    private IReadOnlyList<Guid> CurrentItemIds() => _draft is null ? [] : CurrentKind() switch
    {
        StudioUx2NavJItemKind.Room => _draft.Rooms.Select(item => item.Id).ToArray(),
        StudioUx2NavJItemKind.Portal => _draft.Portals.Select(item => item.Id).ToArray(),
        StudioUx2NavJItemKind.Occluder => _draft.Occluders.Select(item => item.Id).ToArray(),
        _ => [],
    };

    private IReadOnlyList<string> CurrentItemLabels()
    {
        if (_draft is null) return [];
        return CurrentKind() switch
        {
            StudioUx2NavJItemKind.Room => _draft.Rooms.Select((item, index) =>
                $"Volume {index + 1} · {HumanTextUx2NavJ(item.Name, "Nom protégé")}").ToArray(),
            StudioUx2NavJItemKind.Portal => _draft.Portals.Select((item, index) =>
                $"Passage {index + 1} · {HumanTextUx2NavJ(item.Name, "Nom protégé")}").ToArray(),
            StudioUx2NavJItemKind.Occluder => _draft.Occluders.Select((item, index) =>
                $"Écran {index + 1} · {HumanTextUx2NavJ(item.Name, "Nom protégé")}").ToArray(),
            _ => [],
        };
    }

    private void LoadSelectedItem()
    {
        _updating = true;
        _itemFields.Visible = _selectedItemId is not null;
        _removeItem.Disabled = _selectedItemId is null;
        _roomFields.Visible = CurrentKind() == StudioUx2NavJItemKind.Room;
        _portalFields.Visible = CurrentKind() == StudioUx2NavJItemKind.Portal;
        _occluderFields.Visible = CurrentKind() == StudioUx2NavJItemKind.Occluder;
        _itemKind.Text = _selectedItemId is null ? "Aucun élément sélectionné" : CurrentKindLabel();
        if (_draft is not null && _selectedItemId is { } id)
        {
            switch (CurrentKind())
            {
                case StudioUx2NavJItemKind.Room:
                {
                    var item = _draft.Rooms.Single(candidate => candidate.Id == id);
                    LoadCommon(item.NameIsProtected, item.Name, item.Transform);
                    _roomExterior.ButtonPressed = item.Exterior;
                    _roomStreaming.Value = item.StreamingDistance;
                    break;
                }
                case StudioUx2NavJItemKind.Portal:
                {
                    var item = _draft.Portals.Single(candidate => candidate.Id == id);
                    LoadCommon(item.NameIsProtected, item.Name, item.Transform);
                    PopulatePortalRooms(item.SourceRoomId, item.DestinationRoomId);
                    _portalMode.Selected = Math.Max(0, Array.IndexOf(PortalModes, item.Mode));
                    _portalCondition.Text = item.ConditionIsProtected
                        ? ProtectedConditionUx2NavJ : item.Condition;
                    _portalTwoWay.ButtonPressed = item.TwoWay;
                    break;
                }
                case StudioUx2NavJItemKind.Occluder:
                {
                    var item = _draft.Occluders.Single(candidate => candidate.Id == id);
                    LoadCommon(item.NameIsProtected, item.Name, item.Transform);
                    _occluderDynamic.ButtonPressed = item.Dynamic;
                    _occluderAsset.Text = item.MeshAssetIsProtected
                        ? ProtectedAssetUx2NavJ : item.MeshAssetId;
                    break;
                }
            }
        }
        _updating = false;
        UpdateActionState();
    }

    private void LoadCommon(bool protectedName, string name, TransformValue transform)
    {
        _name.Text = protectedName ? ProtectedNameUx2NavJ : name;
        _positionX.Value = transform.Position.X;
        _positionY.Value = transform.Position.Y;
        _positionZ.Value = transform.Position.Z;
        _scaleX.Value = transform.Scale.X;
        _scaleY.Value = transform.Scale.Y;
        _scaleZ.Value = transform.Scale.Z;
    }

    private void PopulatePortalRooms(Guid source, Guid destination)
    {
        _portalSource.Clear();
        _portalDestination.Clear();
        _roomChoiceIds = _draft?.Rooms.Select(item => item.Id).ToArray() ?? [];
        var rooms = _draft?.Rooms ?? [];
        var duplicates = rooms.GroupBy(item => HumanTextUx2NavJ(item.Name, "Volume protégé"),
                StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(),
                StringComparer.CurrentCultureIgnoreCase);
        for (var index = 0; index < rooms.Count; index++)
        {
            var room = rooms[index];
            var label = HumanTextUx2NavJ(room.Name, "Volume protégé");
            if (duplicates[label] > 1) label += $" · volume {index + 1}";
            _portalSource.AddItem(label);
            _portalDestination.AddItem(label);
        }
        _portalSource.Selected = Math.Max(0, _roomChoiceIds.ToList().FindIndex(item => item == source));
        _portalDestination.Selected = Math.Max(0,
            _roomChoiceIds.ToList().FindIndex(item => item == destination));
    }

    private void SaveControlsIntoDraft()
    {
        if (_draft is null) return;
        SaveSelectedItem();
        _draft = _draft with
        {
            OcclusionCulling = _occlusionCulling.ButtonPressed,
            DistanceStreaming = _distanceStreaming.ButtonPressed,
            CellVisualizer = _cellVisualizer.ButtonPressed,
            CellSize = _cellSize.Value,
            StreamingDistance = _streamingDistance.Value,
        };
    }

    private void SaveSelectedItem()
    {
        if (_draft is null || _selectedItemId is null) return;
        var id = _selectedItemId.Value;
        switch (CurrentKind())
        {
            case StudioUx2NavJItemKind.Room:
            {
                var items = _draft.Rooms.ToArray();
                var index = items.ToList().FindIndex(item => item.Id == id);
                if (index < 0) return;
                var current = items[index];
                items[index] = current with
                {
                    Name = ResolveName(current.NameIsProtected, current.Name),
                    NameIsProtected = ResolveNameProtected(current.NameIsProtected),
                    Transform = TransformFromControls(current.Transform),
                    Exterior = _roomExterior.ButtonPressed,
                    StreamingDistance = _roomStreaming.Value,
                };
                _draft = _draft with { Rooms = items };
                break;
            }
            case StudioUx2NavJItemKind.Portal:
            {
                var items = _draft.Portals.ToArray();
                var index = items.ToList().FindIndex(item => item.Id == id);
                if (index < 0) return;
                var current = items[index];
                var source = _portalSource.Selected >= 0 && _portalSource.Selected < _roomChoiceIds.Count
                    ? _roomChoiceIds[_portalSource.Selected] : current.SourceRoomId;
                var destination = _portalDestination.Selected >= 0 &&
                                  _portalDestination.Selected < _roomChoiceIds.Count
                    ? _roomChoiceIds[_portalDestination.Selected] : current.DestinationRoomId;
                items[index] = current with
                {
                    Name = ResolveName(current.NameIsProtected, current.Name),
                    NameIsProtected = ResolveNameProtected(current.NameIsProtected),
                    Transform = TransformFromControls(current.Transform),
                    SourceRoomId = source,
                    DestinationRoomId = destination,
                    Mode = _portalMode.Selected >= 0 && _portalMode.Selected < PortalModes.Length
                        ? PortalModes[_portalMode.Selected] : current.Mode,
                    Condition = current.ConditionIsProtected &&
                                string.Equals(_portalCondition.Text, ProtectedConditionUx2NavJ,
                                    StringComparison.Ordinal)
                        ? current.Condition : _portalCondition.Text,
                    ConditionIsProtected = current.ConditionIsProtected &&
                                           string.Equals(_portalCondition.Text,
                                               ProtectedConditionUx2NavJ, StringComparison.Ordinal),
                    TwoWay = _portalTwoWay.ButtonPressed,
                };
                _draft = _draft with { Portals = items };
                break;
            }
            case StudioUx2NavJItemKind.Occluder:
            {
                var items = _draft.Occluders.ToArray();
                var index = items.ToList().FindIndex(item => item.Id == id);
                if (index < 0) return;
                var current = items[index];
                items[index] = current with
                {
                    Name = ResolveName(current.NameIsProtected, current.Name),
                    NameIsProtected = ResolveNameProtected(current.NameIsProtected),
                    Transform = TransformFromControls(current.Transform),
                    Dynamic = _occluderDynamic.ButtonPressed,
                    MeshAssetId = current.MeshAssetIsProtected &&
                                  string.Equals(_occluderAsset.Text, ProtectedAssetUx2NavJ,
                                      StringComparison.Ordinal)
                        ? current.MeshAssetId : _occluderAsset.Text,
                    MeshAssetIsProtected = current.MeshAssetIsProtected &&
                                           string.Equals(_occluderAsset.Text,
                                               ProtectedAssetUx2NavJ, StringComparison.Ordinal),
                };
                _draft = _draft with { Occluders = items };
                break;
            }
        }
    }

    private string ResolveName(bool protectedName, string original) => protectedName &&
        string.Equals(_name.Text, ProtectedNameUx2NavJ, StringComparison.Ordinal) ? original : _name.Text;

    private bool ResolveNameProtected(bool protectedName) => protectedName &&
        string.Equals(_name.Text, ProtectedNameUx2NavJ, StringComparison.Ordinal);

    private TransformValue TransformFromControls(TransformValue source) => new(
        new Vector3Value((float)_positionX.Value, (float)_positionY.Value, (float)_positionZ.Value),
        source.Rotation,
        new Vector3Value((float)_scaleX.Value, (float)_scaleY.Value, (float)_scaleZ.Value));

    private void DraftChanged()
    {
        if (_updating || _draft is null) return;
        SaveControlsIntoDraft();
        InvalidatePreview();
    }

    private void InvalidatePreview()
    {
        _session.CancelVisibilityApprovalUx2NavJ();
        _lastPreview = null;
        _worldViewport.ClearNavigationOverlayUx2NavB();
        RenderPreview(null);
        UpdateActionState();
    }

    private void Preview()
    {
        if (_draft is null) { SetStatus("Choisissez d’abord une zone.", true); return; }
        SaveControlsIntoDraft();
        _lastPreview = _session.PreviewVisibilityUx2NavJ(_draft);
        RenderPreview(_lastPreview);
        SetStatus(_lastPreview.Message, !_lastPreview.Succeeded);
        UpdateActionState();
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedVisibilityUx2NavJ();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            VisibilityChanged?.Invoke();
            ReloadSelectedArea();
        }
        UpdateActionState();
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) { VisibilityChanged?.Invoke(); ReloadSelectedArea(); }
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) { VisibilityChanged?.Invoke(); ReloadSelectedArea(); }
    }

    private void Save()
    {
        var result = _session.Save();
        SetStatus(result.Message, !result.Succeeded);
        UpdateActionState();
    }

    private void ReloadSelectedArea()
    {
        if (_area.Selected <= 0 || _area.Selected > _areaChoices.Count) { ClearSelection(); return; }
        var areaId = _areaChoices[_area.Selected - 1].AreaId;
        _draft = _session.CreateVisibilityDraftUx2NavJ(areaId);
        if (_draft is null) { ClearSelection(); return; }
        _selectedItemId = null;
        _lastPreview = null;
        _explicitAreaSelection = true;
        LoadGlobalDraft(_draft);
        PopulateItems();
        LoadSelectedItem();
        RenderPreview(null);
        BindViewportToArea(_session, areaId);
    }

    private void RenderPreview(StudioUx2NavJPreview? preview)
    {
        ClearChildrenUx2NavJ(_diff);
        ClearChildrenUx2NavJ(_diagnostics);
        ClearChildrenUx2NavJ(_runtime);
        if (preview is null)
        {
            _diff.AddChild(StudioStyle.Label("Prévisualisez pour voir les changements.", 9,
                StudioStyle.Muted, wrap: true));
            _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic calculé.", 9,
                StudioStyle.Muted, wrap: true));
            _runtime.AddChild(StudioStyle.Label("Aucun plan canonique calculé.", 9,
                StudioStyle.Muted, wrap: true));
            _summary.Text = "Aucun aperçu calculé.";
            _boundary.Text =
                "Limite : aucun culling GPU, mesh HLOD, streaming réel, réseau ou préchargement n’est exécuté.";
            return;
        }
        foreach (var line in preview.Diff.DefaultIfEmpty("Aucune différence sémantique."))
            _diff.AddChild(StudioStyle.Label("• " + line, 9, StudioStyle.Text, wrap: true));
        foreach (var item in preview.Diagnostics)
            _diagnostics.AddChild(StudioStyle.Label($"{item.Code}\n{item.Path}\n{item.Message}", 9,
                StudioStyle.Warning, wrap: true));
        if (preview.Diagnostics.Count == 0)
            _diagnostics.AddChild(StudioStyle.Label("✓ Profil et références valides.", 9,
                StudioStyle.Success, wrap: true));
        if (preview.Runtime is { } runtime)
        {
            _runtime.AddChild(StudioStyle.Label(
                $"Volumes {runtime.Rooms} · passages ouverts {runtime.OpenPortals} · fermés {runtime.ClosedPortals} · conditionnels {runtime.ConditionalPortals}",
                9, StudioStyle.Text, wrap: true));
            _runtime.AddChild(StudioStyle.Label(
                $"Écrans {runtime.Occluders} · dynamiques {runtime.DynamicOccluders} · handle typé {(runtime.TypedRuntimeHandle ? "prêt" : "absent")}",
                9, StudioStyle.Text, wrap: true));
            _runtime.AddChild(StudioStyle.Label(runtime.PerformanceWithinBudget
                    ? "✓ Budget auteur respecté."
                    : "Budget auteur dépassé · " + string.Join(" · ", runtime.PerformanceDiagnostics),
                9, runtime.PerformanceWithinBudget ? StudioStyle.Success : StudioStyle.Warning, wrap: true));
            _boundary.Text = runtime.Boundary;
        }
        _summary.Text = preview.Summary;
        if (preview.Succeeded) _worldViewport.ShowNavigationOverlayUx2NavB(preview.Overlay);
        else _worldViewport.ClearNavigationOverlayUx2NavB();
    }

    private void WireDraftSignals()
    {
        foreach (var check in new[]
                 {
                     _occlusionCulling, _distanceStreaming, _cellVisualizer, _roomExterior,
                     _portalTwoWay, _occluderDynamic,
                 }) check.Toggled += _ => DraftChanged();
        foreach (var number in new[]
                 {
                     _cellSize, _streamingDistance, _positionX, _positionY, _positionZ,
                     _scaleX, _scaleY, _scaleZ, _roomStreaming,
                 }) number.ValueChanged += _ => DraftChanged();
        foreach (var text in new[] { _name, _portalCondition, _occluderAsset })
            text.TextChanged += _ => DraftChanged();
        _portalSource.ItemSelected += _ => DraftChanged();
        _portalDestination.ItemSelected += _ => DraftChanged();
        _portalMode.ItemSelected += _ => DraftChanged();
    }

    private void SetEditorEnabled(bool enabled)
    {
        _editor.Visible = enabled;
        _kind.Disabled = !enabled;
        _item.Disabled = !enabled;
        _addItem.Disabled = !enabled;
        _preview.Disabled = !enabled;
        foreach (var check in new[] { _occlusionCulling, _distanceStreaming, _cellVisualizer })
            check.Disabled = !enabled;
        _cellSize.Editable = enabled;
        _streamingDistance.Editable = enabled;
        UpdateActionState();
    }

    private void UpdateActionState()
    {
        _apply.Disabled = _lastPreview?.CanApply != true;
        _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo;
        _save.Disabled = _session.Project is null || !_session.IsDirty;
        _removeItem.Disabled = _selectedItemId is null;
    }

    private StudioUx2NavJItemKind CurrentKind()
    {
        var index = Math.Clamp(_kind.Selected, 0, ItemKinds.Length - 1);
        return ItemKinds[index];
    }

    private string CurrentKindLabel() => CurrentKind() switch
    {
        StudioUx2NavJItemKind.Room => "Volume de visibilité",
        StudioUx2NavJItemKind.Portal => "Passage de visibilité",
        StudioUx2NavJItemKind.Occluder => "Écran d’occlusion",
        _ => "Élément",
    };

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(message, error);
    }

    private StudioUx2NavJEvidenceSourceSnapshot CaptureSourceSnapshot()
    {
        var history = _sourceSession.History!;
        return new(_sourceSession.Project!, history, history.State.Revision, history.Journal.ToArray(),
            _sourceSession.IsDirty, history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureVisibilityApprovalUx2NavJ());
    }

    private bool SourceMatchesSnapshot(StudioUx2NavJEvidenceSourceSnapshot snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.IsDirty && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo && _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureVisibilityApprovalUx2NavJ() == snapshot.Approval;
    }

    private void ReleaseEvidenceWorkspace()
    {
        if (_evidenceSession is null && _evidenceTempRoot is null)
        {
            _session = _sourceSession;
            _lastSourceRestored = true;
            return;
        }
        try { _evidenceSession?.Close(); }
        finally
        {
            _evidenceSession = null;
            var root = _evidenceTempRoot;
            _evidenceTempRoot = null;
            _session = _sourceSession;
            _lastEvidenceResourcesReleased = StudioSessionController.DeleteUx2NavJTemporaryWorkspace(root);
            _lastEvidenceSourceUntouched = _evidenceSourceSnapshot is null ||
                                           SourceMatchesSnapshot(_evidenceSourceSnapshot);
            _lastSourceRestored = true;
        }
    }

    private void BindViewportToArea(StudioSessionController session, AreaId areaId)
    {
        try { _worldViewport.RebuildArea(session, areaId, readOnly: true); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException)
        {
            GD.PushWarning($"OPENRPG_FORGE_STUDIO_UX2_NAV_J_VIEWPORT_FALLBACK {exception.Message}");
        }
    }

    private void RestoreSourceViewport()
    {
        try
        {
            if (_sourceSession.ActiveAreaId is { } activeArea)
            {
                _worldViewport.RebuildArea(_sourceSession, activeArea, readOnly: false, frameArea: false);
                _worldViewport.ApplyCameraProfile(_sourceSession.CameraProfile);
            }
            else _worldViewport.ShowNoArea(_sourceSession);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException)
        {
            GD.PushWarning($"OPENRPG_FORGE_STUDIO_UX2_NAV_J_VIEWPORT_RESTORE_FAILED {exception.Message}");
        }
    }

    private void UpdateDialogBounds()
    {
        var viewport = GetViewportRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0) return;
        var scale = viewport.X >= 3_200 ? 1.5f : viewport.X >= 2_300 ? 1.15f : 1f;
        var width = Math.Clamp((viewport.X * .49f) / scale, 780f, 1_000f);
        var height = Math.Clamp((viewport.Y - 30f) / scale, 700f, 1_180f);
        _dialog.Scale = Vector2.One * scale;
        _dialog.CustomMaximumSize = new Vector2(width, height);
        _dialog.Size = new Vector2(width, height);
        _dialog.Position = new Vector2(viewport.X - width * scale - 15f,
            Math.Max(15f, (viewport.Y - height * scale) / 2f));
    }

    private static HBoxContainer RowUx2NavJ()
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 6);
        return row;
    }

    private static SpinBox NumberUx2NavJ()
    {
        var result = StudioUx2NavCControls.Number(-float.MaxValue, float.MaxValue, .25);
        result.AllowGreater = false;
        result.AllowLesser = false;
        return result;
    }

    private static SpinBox PositiveNumberUx2NavJ()
    {
        var result = StudioUx2NavCControls.Number(float.Epsilon, float.MaxValue, .25);
        result.AllowGreater = false;
        result.AllowLesser = false;
        return result;
    }

    private static VBoxContainer PreviewColumnUx2NavJ(Container parent, string title)
    {
        var panel = StudioStyle.Panel("PreviewColumnUx2NavJ" + title.GetHashCode(),
            StudioStyle.SurfaceRaised, 7, 1);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var margin = StudioStyle.Margin(8, 7, 8, 7);
        panel.AddChild(margin);
        var box = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(190, 90),
        };
        box.AddThemeConstantOverride("separation", 4);
        box.AddChild(StudioStyle.Label(title, 9, StudioStyle.Accent));
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 3);
        box.AddChild(content);
        margin.AddChild(box);
        parent.AddChild(panel);
        return content;
    }

    private static void ClearChildrenUx2NavJ(Node node)
    {
        foreach (var child in node.GetChildren()) child.QueueFree();
    }

    private static string HumanTextUx2NavJ(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) || Guid.TryParse(value.Trim(), out _) || Regex.IsMatch(value,
            "(?i)(?<![0-9a-f])[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}(?![0-9a-f])")
            ? fallback : value.Trim();
}
