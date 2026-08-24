using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioTileGridCanvasUx2B : Control
{
    private const float CellSize = 42f;
    private IReadOnlyList<StudioUx2BPlacementChoice> _placements = [];
    private readonly HashSet<Guid> _selected = [];
    private int _targetX;
    private int _targetZ;

    internal event Action<IReadOnlyList<Guid>>? SelectionChanged;
    internal event Action<int, int>? TargetChanged;

    internal bool DirectInputExercised { get; private set; }
    internal IReadOnlyList<Guid> SelectedIds => _selected.OrderBy(item => item).ToArray();
    internal int TargetX => _targetX;
    internal int TargetZ => _targetZ;

    internal StudioTileGridCanvasUx2B()
    {
        Name = "TerrainTileGridUx2B";
        CustomMinimumSize = new Vector2(540, 460);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        MouseDefaultCursorShape = CursorShape.Cross;
        FocusMode = FocusModeEnum.All;
        MouseFilter = MouseFilterEnum.Stop;
    }

    internal void SetPlacements(IReadOnlyList<StudioUx2BPlacementChoice> placements)
    {
        _placements = placements ?? [];
        _selected.RemoveWhere(id => _placements.All(item => item.PlacementId != id));
        QueueRedraw();
    }

    internal void SetSelection(IEnumerable<Guid> placementIds)
    {
        _selected.Clear();
        foreach (var id in placementIds.Where(id => _placements.Any(item => item.PlacementId == id)))
        {
            _selected.Add(id);
        }
        QueueRedraw();
    }

    internal void SetTarget(int gridX, int gridZ)
    {
        _targetX = gridX;
        _targetZ = gridZ;
        QueueRedraw();
    }

    internal bool RunDirectInputEvidence()
    {
        var cell = CellCenter(_placements.FirstOrDefault()?.GridX ?? 0, _placements.FirstOrDefault()?.GridZ ?? 0);
        _GuiInput(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = true,
            Position = cell,
        });
        return DirectInputExercised;
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), StudioStyle.Background, filled: true);
        var center = Size / 2f;
        for (var index = -6; index <= 6; index++)
        {
            var x = center.X + index * CellSize;
            var z = center.Y + index * CellSize;
            DrawLine(new Vector2(x, 0), new Vector2(x, Size.Y), StudioStyle.Border, 1);
            DrawLine(new Vector2(0, z), new Vector2(Size.X, z), StudioStyle.Border, 1);
        }

        var target = CellRect(_targetX, _targetZ).Grow(-3);
        DrawRect(target, new Color(StudioStyle.Accent, .22f), filled: true);
        DrawRect(target, StudioStyle.Accent, filled: false, width: 2);
        foreach (var placement in _placements)
        {
            var rect = CellRect(placement.GridX, placement.GridZ).Grow(-5);
            var selected = _selected.Contains(placement.PlacementId);
            DrawRect(rect, selected ? StudioStyle.Selection : StudioStyle.SurfaceRaised, filled: true);
            DrawRect(rect, selected ? StudioStyle.Success : StudioStyle.Muted, filled: false,
                width: selected ? 3 : 1);
            DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(7, 23),
                $"{placement.GridX},{placement.GridZ}", HorizontalAlignment.Left, -1, 11,
                selected ? StudioStyle.Success : StudioStyle.Text);
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mouse)
        {
            return;
        }

        DirectInputExercised = true;
        var (gridX, gridZ) = GridFromPosition(mouse.Position);
        var placement = _placements.LastOrDefault(item => item.GridX == gridX && item.GridZ == gridZ);
        if (placement is not null)
        {
            if (!_selected.Add(placement.PlacementId)) _selected.Remove(placement.PlacementId);
            SelectionChanged?.Invoke(SelectedIds);
        }
        else
        {
            _targetX = gridX;
            _targetZ = gridZ;
            TargetChanged?.Invoke(gridX, gridZ);
        }
        QueueRedraw();
        AcceptEvent();
    }

    private Rect2 CellRect(int gridX, int gridZ) =>
        new(CellCenter(gridX, gridZ) - new Vector2(CellSize / 2, CellSize / 2), new(CellSize, CellSize));

    private Vector2 CellCenter(int gridX, int gridZ) =>
        Size / 2f + new Vector2(gridX * CellSize + CellSize / 2, gridZ * CellSize + CellSize / 2);

    private (int X, int Z) GridFromPosition(Vector2 position)
    {
        var relative = position - Size / 2f;
        return ((int)MathF.Floor(relative.X / CellSize), (int)MathF.Floor(relative.Y / CellSize));
    }
}

internal sealed partial class StudioTerrainTilesWorkspaceUx2B : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private readonly PanelContainer _dialog;
    private readonly OptionButton _area;
    private readonly OptionButton _operation;
    private readonly OptionButton _definition;
    private readonly SpinBox _gridX;
    private readonly SpinBox _gridZ;
    private readonly SpinBox _rotation;
    private readonly OptionButton _mirror;
    private readonly LineEdit _variant;
    private readonly LineEdit _groupName;
    private readonly CheckBox _preserveAttachments;
    private readonly StudioTileGridCanvasUx2B _grid;
    private readonly Label _selection;
    private readonly Label _summary;
    private readonly Label _diagnostics;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;
    private IReadOnlyList<StudioUx2BAreaChoice> _areaChoices = [];
    private IReadOnlyList<StudioUx2BDefinitionChoice> _definitionChoices = [];
    private IReadOnlyList<StudioUx2BPlacementChoice> _placementChoices = [];
    private StudioUx2BTileDraft? _draft;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private bool _updating;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? TerrainChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _draft is not null && _area.Selected > 0;
    internal bool CanonicalPreviewReady { get; private set; }
    internal bool ShowsCompleteKit => _definitionChoices.Count >= 2 && _placementChoices.Count >= 2;
    internal bool ShowsDirectGrid => _grid.Visible && _grid.CustomMinimumSize.X >= 500;
    internal bool DirectGridInputExercised => _grid.DirectInputExercised;
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && ReferenceEquals(_session, _evidenceSession);
    internal bool EvidenceResourcesReleased => _evidenceSession is null && _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', _summary.Text, _diagnostics.Text, _status.Text, _selection.Text,
                _area.GetItemText(Math.Max(0, _area.Selected)),
                _operation.GetItemText(Math.Max(0, _operation.Selected)));
            return text.Contains("Aperçu", StringComparison.CurrentCultureIgnoreCase) &&
                   text.Contains("tuile", StringComparison.CurrentCultureIgnoreCase) &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains(".json", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal StudioTerrainTilesWorkspaceUx2B(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = session;
        Name = "StudioTerrainTilesWorkspaceUx2B";
        Visible = false;
        ZIndex = 470;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect { Color = new Color(0, 0, 0, .78f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        _dialog = StudioStyle.Panel("TerrainTilesDialogUx2B", StudioStyle.Chrome, 12, 2);
        _dialog.AnchorLeft = .025f;
        _dialog.AnchorTop = .035f;
        _dialog.AnchorRight = .975f;
        _dialog.AnchorBottom = .965f;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(16, 12, 16, 12);
        _dialog.AddChild(margin);
        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 9);
        margin.AddChild(root);

        var header = new HBoxContainer();
        var title = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddChild(StudioStyle.Label("CONSTRUCTION MODULAIRE · UX2", 10, StudioStyle.Accent));
        title.AddChild(StudioStyle.Label("Tuiles, groupes et raccords", 20));
        title.AddChild(StudioStyle.Label(
            "Grille directe · sélection humaine · aperçu sec obligatoire · aucune simulation réseau",
            10, StudioStyle.Muted));
        header.AddChild(title);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);
        root.AddChild(header);

        var body = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 9);
        root.AddChild(body);

        var leftPanel = StudioStyle.Panel("TerrainTileContextUx2B", StudioStyle.Surface, 8);
        leftPanel.CustomMinimumSize = new Vector2(270, 0);
        body.AddChild(leftPanel);
        var leftMargin = StudioStyle.Margin(11, 10, 11, 10);
        leftPanel.AddChild(leftMargin);
        var left = new VBoxContainer();
        left.AddThemeConstantOverride("separation", 7);
        leftMargin.AddChild(left);
        left.AddChild(StudioStyle.SectionLabel("ZONE ET OUTIL"));
        _area = new OptionButton();
        StudioStyle.ApplyOptionButton(_area);
        _area.ItemSelected += index => SelectArea((int)index);
        left.AddChild(_area);
        _operation = new OptionButton();
        StudioStyle.ApplyOptionButton(_operation);
        foreach (var label in new[]
                 {
                     "Placer une tuile", "Former un groupe", "Effacer", "Remplacer",
                     "Rotation et variante", "Ajustement compatible",
                 })
        {
            _operation.AddItem(label);
        }
        _operation.ItemSelected += index => ChangeOperation((int)index);
        left.AddChild(_operation);
        left.AddChild(StudioStyle.Label(
            "Cliquez une case vide pour choisir la cible. Cliquez une ou plusieurs tuiles pour les sélectionner.",
            10, StudioStyle.Success, wrap: true));
        _selection = StudioStyle.Label("Aucune tuile sélectionnée.", 11, StudioStyle.Muted, wrap: true);
        left.AddChild(_selection);
        var legend = StudioStyle.Panel("TerrainLegendUx2B", StudioStyle.Selection, 7);
        legend.AddChild(StudioStyle.Label(
            "Vert : sélection · orange : cellule cible · les objets attachés sont conservés par défaut.",
            10, StudioStyle.Text, wrap: true));
        left.AddChild(legend);

        var gridPanel = StudioStyle.Panel("TerrainGridPanelUx2B", StudioStyle.Background, 8);
        gridPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        gridPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        body.AddChild(gridPanel);
        var gridMargin = StudioStyle.Margin(6, 6, 6, 6);
        gridPanel.AddChild(gridMargin);
        _grid = new StudioTileGridCanvasUx2B();
        _grid.SelectionChanged += ids =>
        {
            if (_draft is null) return;
            _draft = _draft with { SelectedPlacementIds = ids };
            _selection.Text = ids.Count == 0 ? "Aucune tuile sélectionnée." : $"{ids.Count} tuile(s) sélectionnée(s).";
            InvalidatePreview();
        };
        _grid.TargetChanged += OnGridTargetChanged;
        gridMargin.AddChild(_grid);

        var rightPanel = StudioStyle.Panel("TerrainTileInspectorUx2B", StudioStyle.Surface, 8);
        rightPanel.CustomMinimumSize = new Vector2(355, 0);
        body.AddChild(rightPanel);
        var rightMargin = StudioStyle.Margin(12, 10, 12, 10);
        rightPanel.AddChild(rightMargin);
        var rightScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        rightMargin.AddChild(rightScroll);
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 7);
        rightScroll.AddChild(right);
        right.AddChild(StudioStyle.SectionLabel("PARAMÈTRES HUMAINS"));
        _definition = Field(right, "Définition", new OptionButton());
        StudioStyle.ApplyOptionButton(_definition);
        _gridX = Field(right, "Case X", IntegerBox(-4096, 4096));
        _gridZ = Field(right, "Case Z", IntegerBox(-4096, 4096));
        _rotation = Field(right, "Rotation", new SpinBox
        {
            MinValue = -3600,
            MaxValue = 3600,
            Step = 0,
            CustomArrowStep = 45,
            Suffix = "°",
        });
        StudioStyle.ApplySpinBox(_rotation);
        _mirror = Field(right, "Miroir", new OptionButton());
        StudioStyle.ApplyOptionButton(_mirror);
        foreach (var item in new[] { "Aucun", "Horizontal", "Vertical" }) _mirror.AddItem(item);
        _variant = Field(right, "Variante", new LineEdit { PlaceholderText = "Base ou nom de variante" });
        StudioStyle.ApplyLineEdit(_variant);
        _groupName = Field(right, "Nom du groupe", new LineEdit { PlaceholderText = "Ex. Galerie nord" });
        StudioStyle.ApplyLineEdit(_groupName);
        _preserveAttachments = new CheckBox { Text = "Conserver les objets attachés", ButtonPressed = true };
        right.AddChild(_preserveAttachments);
        foreach (var control in new Control[] { _definition, _gridX, _gridZ, _rotation, _mirror, _variant, _groupName,
                     _preserveAttachments })
        {
            switch (control)
            {
                case OptionButton option: option.ItemSelected += _ => InvalidatePreview(); break;
                case SpinBox spin: spin.ValueChanged += _ => InvalidatePreview(); break;
                case LineEdit edit: edit.TextChanged += _ => InvalidatePreview(); break;
                case CheckBox check: check.Toggled += _ => InvalidatePreview(); break;
            }
        }
        right.AddChild(StudioStyle.SectionLabel("APERÇU CANONIQUE"));
        _summary = StudioStyle.Label("Aucun aperçu. Choisissez une zone puis une opération.", 11,
            StudioStyle.Muted, wrap: true);
        right.AddChild(_summary);
        _diagnostics = StudioStyle.Label(
            "Le validateur vérifie les cases, modèles, raccords, rotations, variantes et groupes.",
            10, StudioStyle.Muted, wrap: true);
        right.AddChild(_diagnostics);

        _status = StudioStyle.Label("Sélectionnez explicitement une zone.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        root.AddChild(StudioStyle.Label(
            "L’aperçu n’écrit rien. Appliquer produit une seule commande de géométrie annulable.",
            10, StudioStyle.Muted, wrap: true));
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        _preview = StudioStyle.Button("Prévisualiser", primary: true);
        _preview.Pressed += Preview;
        _apply = StudioStyle.Button("Appliquer");
        _apply.Pressed += Apply;
        _undo = StudioStyle.Button("Annuler");
        _undo.Pressed += Undo;
        _redo = StudioStyle.Button("Rétablir");
        _redo.Pressed += Redo;
        _save = StudioStyle.Button("Enregistrer");
        _save.Pressed += Save;
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        {
            button.CustomMinimumSize = new Vector2(118, 34);
            actions.AddChild(button);
        }
        root.AddChild(actions);
        SetProcessUnhandledKeyInput(true);
        UpdateActions();
    }

    internal void Open()
    {
        CloseEvidenceSession();
        _session = _sourceSession;
        Visible = true;
        LoadAreas();
        _status.Text = "Sélectionnez explicitement une zone ; aucune première zone n’est choisie automatiquement.";
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null) return;
        Visible = false;
        _session.CancelTerrainApprovalUx2B();
        _draft = null;
        CanonicalPreviewReady = false;
        CloseEvidenceSession();
    }

    internal bool PrepareEvidence(string mode)
    {
        CloseEvidenceSession();
        var sourceProject = _sourceSession.Project;
        var evidenceProject = _sourceSession.CreateTerrainEvidenceProjectUx2B();
        if (sourceProject is null || evidenceProject is null) return false;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux2b-" + Guid.NewGuid().ToString("N"));
        var created = ProjectSession.Create(root, evidenceProject);
        if (!created.Succeeded)
        {
            StudioSessionController.DeleteTerrainTemporaryWorkspaceUx2B(root);
            return false;
        }
        var controller = new StudioSessionController();
        if (!controller.Open(root).Succeeded)
        {
            controller.Close();
            StudioSessionController.DeleteTerrainTemporaryWorkspaceUx2B(root);
            return false;
        }
        _evidenceRoot = root;
        _evidenceSession = controller;
        _session = controller;
        Visible = true;
        LoadAreas();
        var target = _areaChoices.FindIndex(item =>
            controller.Project?.Areas.Single(area => area.Id == item.AreaId).Slug == "atelier-tuiles");
        if (target < 0) return FailEvidence();
        _area.Select(target + 1);
        SelectArea(target + 1);
        _grid.RunDirectInputEvidence();
        if (_draft is null || _definitionChoices.Count < 2 || _placementChoices.Count < 2) return FailEvidence();
        _operation.Select((int)StudioUx2BTileOperation.Transform);
        ChangeOperation((int)StudioUx2BTileOperation.Transform);
        _grid.SetSelection([_placementChoices[0].PlacementId]);
        _draft = _draft with { SelectedPlacementIds = [_placementChoices[0].PlacementId] };
        _rotation.Value = 90;
        _mirror.Select(1);
        _variant.Text = "humide";
        Preview();
        return CanonicalPreviewReady && UsesIsolatedEvidence && ShowsCompleteKit && DirectGridInputExercised;
    }

    internal bool RunUiTransactionEvidence()
    {
        var sourceProject = _sourceSession.Project;
        var sourceHistory = _sourceSession.History;
        var sourceRevision = sourceHistory?.State.Revision;
        var sourceJournal = sourceHistory?.Journal.ToArray() ?? [];
        var sourceDirty = _sourceSession.IsDirty;
        var sourceApproval = _sourceSession.CaptureTerrainApprovalUx2B();
        try
        {
            if (!PrepareEvidence("ux2-b-terrain-tiles") || _evidenceSession is null ||
                !ReferenceEquals(_session, _evidenceSession) || !CanonicalPreviewReady)
            {
                return false;
            }
            var beforeRevision = _session.History?.State.Revision;
            Apply();
            var applied = _session.History?.State.Revision != beforeRevision && _session.CanUndo;
            Undo();
            var undone = _session.CanRedo;
            Redo();
            var redone = _session.CanUndo;
            Undo();
            return applied && undone && redone && _grid.DirectInputExercised;
        }
        finally
        {
            Close();
            _sourceSession.RestoreTerrainApprovalUx2B(sourceApproval);
            var history = _sourceSession.History;
            if (!ReferenceEquals(sourceProject, _sourceSession.Project) ||
                !ReferenceEquals(sourceHistory, history) || history?.State.Revision != sourceRevision ||
                !(history?.Journal ?? []).SequenceEqual(sourceJournal) || _sourceSession.IsDirty != sourceDirty)
            {
                GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2B_SOURCE_CHANGED");
            }
        }
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        _session = _sourceSession;
        Visible = true;
        LoadAreas();
    }

    private void LoadAreas()
    {
        _updating = true;
        _area.Clear();
        _area.AddItem("Choisir une zone…");
        _areaChoices = _session.TerrainAreaChoicesUx2B;
        foreach (var choice in _areaChoices) _area.AddItem(choice.Label);
        _area.Select(0);
        ResetArea();
        _updating = false;
    }

    private void OnGridTargetChanged(int x, int z)
    {
        _gridX.Value = x;
        _gridZ.Value = z;
        InvalidatePreview();
    }

    private void SelectArea(int index)
    {
        if (_updating) return;
        _session.CancelTerrainApprovalUx2B();
        CanonicalPreviewReady = false;
        if (index <= 0 || index > _areaChoices.Count)
        {
            ResetArea();
            return;
        }
        var choice = _areaChoices[index - 1];
        _draft = _session.CreateTerrainDraftUx2B(choice.AreaId);
        _definitionChoices = _session.TerrainDefinitionChoicesUx2B(choice.AreaId);
        _placementChoices = _session.TerrainPlacementChoicesUx2B(choice.AreaId);
        _definition.Clear();
        foreach (var definition in _definitionChoices) _definition.AddItem(definition.Label);
        if (_definitionChoices.Count > 0) _definition.Select(0);
        _grid.SetPlacements(_placementChoices);
        _grid.SetSelection([]);
        _grid.SetTarget(0, 0);
        _gridX.Value = 0;
        _gridZ.Value = 0;
        _rotation.Value = 0;
        _mirror.Select(0);
        _variant.Text = string.Empty;
        _groupName.Text = "Nouveau groupe";
        _selection.Text = "Aucune tuile sélectionnée.";
        _summary.Text = choice.HasTileKit
            ? "Zone chargée. Choisissez l’outil et la cible, puis prévisualisez."
            : "Cette zone ne possède aucune définition de tuile. Configurez d’abord un kit de construction.";
        _diagnostics.Text = "Aucune commande préparée.";
        _status.Text = choice.HasTileKit
            ? "Grille prête · les données cachées du kit restent préservées."
            : "État vide honnête · aucun kit synthétique n’est ajouté au projet.";
        UpdateActions();
    }

    private void ChangeOperation(int index)
    {
        if (_draft is null) return;
        _draft = _draft with { Operation = (StudioUx2BTileOperation)index };
        InvalidatePreview();
    }

    private void Preview()
    {
        var draft = ReadDraft();
        if (draft is null)
        {
            SetStatus("Choisissez une zone possédant un kit de tuiles.", true);
            return;
        }
        var result = _session.PreviewTerrainUx2B(draft);
        CanonicalPreviewReady = result.Succeeded && result.CanApply;
        _summary.Text = result.Succeeded
            ? result.Summary + "\n" + string.Join("\n", result.Changes.Select(item => "• " + item))
            : "Aperçu refusé · aucun changement préparé.";
        _diagnostics.Text = result.Diagnostics.Count > 0
            ? string.Join("\n", result.Diagnostics.Select(item => "• " + item))
            : result.Notices.Count > 0
                ? string.Join("\n", result.Notices.Select(item => "• " + item))
                : "Validation canonique réussie · aucun diagnostic bloquant.";
        SetStatus(result.Message, !result.Succeeded);
        UpdateActions();
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedTerrainUx2B();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            CanonicalPreviewReady = false;
            ReloadCurrentArea();
            TerrainChanged?.Invoke();
        }
        UpdateActions();
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(result.Message, !result.Succeeded);
        ReloadCurrentArea();
        TerrainChanged?.Invoke();
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(result.Message, !result.Succeeded);
        ReloadCurrentArea();
        TerrainChanged?.Invoke();
    }

    private void Save()
    {
        var result = _session.Save();
        SetStatus(result.Message, !result.Succeeded);
        UpdateActions();
    }

    private StudioUx2BTileDraft? ReadDraft()
    {
        if (_draft is null || _definitionChoices.Count == 0) return null;
        var definitionIndex = Math.Clamp(_definition.Selected, 0, _definitionChoices.Count - 1);
        return _draft with
        {
            Operation = (StudioUx2BTileOperation)_operation.Selected,
            SelectedPlacementIds = _grid.SelectedIds,
            DefinitionId = _definitionChoices[definitionIndex].DefinitionId,
            GridX = (int)_gridX.Value,
            GridZ = (int)_gridZ.Value,
            RotationDegrees = (float)_rotation.Value,
            Mirror = (ModularTileMirrorMode)_mirror.Selected,
            VariantKey = _variant.Text.Trim(),
            GroupName = _groupName.Text,
            PreserveAttachedObjects = _preserveAttachments.ButtonPressed,
        };
    }

    private void ReloadCurrentArea()
    {
        if (_draft is null) return;
        var areaId = _draft.AreaId;
        var index = _areaChoices.FindIndex(item => item.AreaId == areaId);
        LoadAreas();
        if (index >= 0)
        {
            _area.Select(index + 1);
            SelectArea(index + 1);
        }
    }

    private void ResetArea()
    {
        _draft = null;
        _definitionChoices = [];
        _placementChoices = [];
        _definition.Clear();
        _grid.SetPlacements([]);
        _grid.SetSelection([]);
        _summary.Text = "Aucun aperçu. Choisissez une zone puis une opération.";
        _diagnostics.Text = "Les zones sans kit restent vides ; aucune donnée n’est inventée.";
        _selection.Text = "Aucune tuile sélectionnée.";
        CanonicalPreviewReady = false;
        UpdateActions();
    }

    private void InvalidatePreview()
    {
        if (_updating) return;
        _session.CancelTerrainApprovalUx2B();
        CanonicalPreviewReady = false;
        _summary.Text = "Paramètres modifiés · prévisualisez de nouveau avant d’appliquer.";
        UpdateActions();
    }

    private void UpdateActions()
    {
        _preview.Disabled = _draft is null || _definitionChoices.Count == 0;
        _apply.Disabled = !CanonicalPreviewReady;
        _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo;
        _save.Disabled = !_session.IsDirty;
    }

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Success);
        StatusChanged?.Invoke(message, error);
    }

    private bool FailEvidence()
    {
        Close();
        return false;
    }

    private void CloseEvidenceSession()
    {
        var controller = _evidenceSession;
        var root = _evidenceRoot;
        _evidenceSession = null;
        _evidenceRoot = null;
        if (controller is not null) controller.Close();
        if (root is not null && !StudioSessionController.DeleteTerrainTemporaryWorkspaceUx2B(root))
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2B_TEMP_CLEANUP_FAILED");
        _session = _sourceSession;
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.Escape)
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    private static SpinBox IntegerBox(int min, int max)
    {
        var box = new SpinBox { MinValue = min, MaxValue = max, Step = 1 };
        StudioStyle.ApplySpinBox(box);
        return box;
    }

    private static T Field<T>(VBoxContainer parent, string label, T control) where T : Control
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        parent.AddChild(control);
        return control;
    }
}

internal static class StudioUx2BListExtensions
{
    internal static int FindIndex<T>(this IReadOnlyList<T> source, Func<T, bool> predicate)
    {
        for (var index = 0; index < source.Count; index++)
            if (predicate(source[index])) return index;
        return -1;
    }
}
