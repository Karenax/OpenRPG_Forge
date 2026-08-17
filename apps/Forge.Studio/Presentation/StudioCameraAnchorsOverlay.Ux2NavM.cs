using System.Globalization;
using Forge.Domain.Sequences;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioCameraAnchorsOverlayUx2NavM : Control
{
    private readonly StudioSessionController _sourceSession;
    private readonly StudioWorldViewport _worldViewport;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceTempRoot;
    private StudioUx2NavMEvidenceSourceSnapshot? _evidenceSnapshot;
    private readonly PanelContainer _dialog;
    private readonly OptionButton _area;
    private readonly OptionButton _anchor;
    private readonly Label _selection;
    private readonly LineEdit _name;
    private readonly LineEdit[] _position;
    private readonly LineEdit[] _rotation;
    private readonly LineEdit[] _scale;
    private readonly LineEdit[] _lookAt;
    private readonly OptionButton _framing;
    private readonly LineEdit _fov;
    private readonly LineEdit _duration;
    private readonly LineEdit _safeArea;
    private readonly LineEdit _tags;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _cameraPose;
    private readonly Label _lookAtPreview;
    private readonly Label _framingPreview;
    private readonly Label _lensPreview;
    private readonly Label _timingPreview;
    private readonly Label _safeAreaPreview;
    private readonly Label _runtimeConsumption;
    private readonly Label _boundary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;
    private IReadOnlyList<StudioUx2NavMAreaChoice> _areaChoices = [];
    private IReadOnlyList<StudioUx2NavMAnchorChoice> _anchorChoices = [];
    private readonly List<StudioUx2NavMAreaChoice?> _areaEntries = [];
    private readonly List<StudioUx2NavMAnchorChoice?> _anchorEntries = [];
    private StudioUx2NavMDraft? _draft;
    private StudioUx2NavMPreview? _lastPreview;
    private bool _updating;
    private bool _canonicalPreviewReady;
    private bool _lastEvidenceSourceUntouched;
    private bool _lastEvidenceResourcesReleased;
    private bool _lastSourceRestored = true;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? CameraAnchorChanged;

    internal bool IsOpen => Visible;
    internal int AreaChoiceCount => _areaChoices.Count;
    internal int AnchorChoiceCount => _anchorChoices.Count;
    internal bool HasExplicitSelection => _draft is not null && _area.Selected > 0 && _anchor.Selected > 0;
    internal bool CanonicalPreviewReady => _canonicalPreviewReady && _lastPreview?.Succeeded == true;
    internal bool ShowsViewportOverlay => _lastPreview?.Framing is not null &&
                                         _worldViewport.NavigationOverlayPrimitiveCountUx2NavB >= 3;
    internal bool ShowsFramingPreview => _lastPreview?.Framing is not null &&
                                         !_cameraPose.Text.Contains("non évaluée", StringComparison.OrdinalIgnoreCase) &&
                                         !_lookAtPreview.Text.Contains("non évaluée", StringComparison.OrdinalIgnoreCase) &&
                                         !_framingPreview.Text.Contains("non évalué", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsHonestRuntimeBoundary =>
        _runtimeConsumption.Text.Contains("position", StringComparison.OrdinalIgnoreCase) &&
        _runtimeConsumption.Text.Contains("champ de vision", StringComparison.OrdinalIgnoreCase) &&
        _runtimeConsumption.Text.Contains("look-at", StringComparison.OrdinalIgnoreCase) &&
        _boundary.Text.Contains("aucune caméra live", StringComparison.OrdinalIgnoreCase) &&
        _boundary.Text.Contains("séquence", StringComparison.OrdinalIgnoreCase) &&
        _boundary.Text.Contains("réseau", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsStructuredDiagnostics => _lastPreview is not null &&
        _lastPreview.Diagnostics.All(item => !string.IsNullOrWhiteSpace(item.Code) &&
                                             !string.IsNullOrWhiteSpace(item.Path));
    internal bool UsesCanonicalFallback => _draft is not null &&
                                           string.Equals(_draft.Provenance, "studio:ux2-nav-m:evidence",
                                               StringComparison.Ordinal);
    internal bool UsesRealModuleAnchor => _draft is not null && !UsesCanonicalFallback;
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && ReferenceEquals(_session, _evidenceSession);
    internal bool EvidenceSourceUntouched => _evidenceSnapshot is { } snapshot
        ? SourceMatchesSnapshot(snapshot)
        : _lastEvidenceSourceUntouched;
    internal bool EvidenceResourcesReleased => _evidenceSession is null && _evidenceTempRoot is null &&
                                               _lastEvidenceResourcesReleased;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession) && _lastSourceRestored;
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantLabels(_dialog).Select(item => item.Text));
            return text.Contains("Ancres caméra", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Position et cible", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Rotation et échelle", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Cadrage et lentille", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Cadre sûr", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu de cadrage", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsTechnicalIdentifier(text);
        }
    }

    internal StudioCameraAnchorsOverlayUx2NavM(
        StudioSessionController session,
        StudioWorldViewport worldViewport)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _worldViewport = worldViewport ?? throw new ArgumentNullException(nameof(worldViewport));
        _session = _sourceSession;
        Name = "StudioCameraAnchorsOverlayUx2NavM";
        Visible = false;
        ZIndex = 492;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect { Color = new(0, 0, 0, .82f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        _dialog = StudioStyle.Panel("CameraAnchorsDialog", StudioStyle.Chrome, 12, 2);
        _dialog.ClipContents = true;
        _dialog.PropagateMaximumSize = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(18, 14, 18, 14);
        margin.PropagateMaximumSize = true;
        _dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            PropagateMaximumSize = true,
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(header);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("NAVIGATION · CAMÉRA", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Ancres caméra", 20));
        heading.AddChild(StudioStyle.Label(
            "WPT-013 · transform, regard, cadrage, FOV, durée et cadre sûr · overlay géométrique sans caméra live",
            10, StudioStyle.Muted, wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);

        var selectors = Grid(2);
        _area = Option();
        _anchor = Option();
        _area.ItemSelected += index => SelectArea((int)index);
        _anchor.ItemSelected += index => SelectAnchor((int)index);
        selectors.AddChild(Field("Zone", _area));
        selectors.AddChild(Field("Ancre caméra", _anchor));
        root.AddChild(selectors);
        _selection = StudioStyle.Label("Choisissez explicitement une zone puis une ancre caméra.",
            11, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        var bodyHost = new Control
        {
            CustomMinimumSize = new(0, 500),
            CustomMaximumSize = new(-1, 770),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            PropagateMaximumSize = true,
        };
        root.AddChild(bodyHost);
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true,
        };
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bodyHost.AddChild(scroll);
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 9);
        scroll.AddChild(content);

        _name = Line("Plan d’ouverture");
        var identity = Grid(1);
        identity.AddChild(Field("Nom humain", _name));
        content.AddChild(Card("CameraAnchorIdentity", "Ancre sélectionnée",
            "Le nom est unique dans la zone. L’identité interne et la provenance restent protégées.", identity));

        _position = VectorLines("0", "6", "0");
        _lookAt = VectorLines("0", "1.5", "0");
        var spatial = Grid(2);
        spatial.AddChild(VectorField("Position caméra X / Y / Z", _position));
        spatial.AddChild(VectorField("Cible regardée X / Y / Z", _lookAt));
        content.AddChild(Card("CameraAnchorSpatial", "Position et cible",
            "L’overlay navigation affiche la caméra, la cible et la ligne de visée dans la vraie zone.", spatial));

        _rotation = [Line("0"), Line("0"), Line("0"), Line("1")];
        _scale = VectorLines("1", "1", "1");
        var transform = Grid(2);
        transform.AddChild(VectorField("Rotation quaternion X / Y / Z / W", _rotation));
        transform.AddChild(VectorField("Échelle X / Y / Z", _scale));
        content.AddChild(Card("CameraAnchorTransform", "Rotation et échelle",
            "Le transform complet est conservé sans normalisation silencieuse. Le runtime actuel oriente la caméra avec look-at.",
            transform));

        _framing = Option("Plan large", "Plan en pied", "Plan moyen", "Gros plan", "Très gros plan");
        _fov = NumberLine("55");
        _duration = NumberLine("2000");
        _safeArea = NumberLine("0.08");
        _tags = Line("camera, opening");
        var lens = Grid(2);
        lens.AddChild(Field("Cadrage", _framing));
        lens.AddChild(Field("Champ de vision vertical (°)", _fov));
        lens.AddChild(Field("Durée suggérée (ms)", _duration));
        lens.AddChild(Field("Marge du cadre sûr (0 à 0,45)", _safeArea));
        lens.AddChild(Field("Tags", _tags));
        content.AddChild(Card("CameraAnchorLens", "Cadrage et lentille",
            "Le cadre sûr réserve les bords pour sous-titres et interfaces. Les valeurs brutes ne sont jamais clampées.",
            lens));

        _diff = new();
        _diagnostics = new();
        _cameraPose = PreviewLabel("Position caméra non évaluée.");
        _lookAtPreview = PreviewLabel("Cible non évaluée.");
        _framingPreview = PreviewLabel("Cadrage non évalué.");
        _lensPreview = PreviewLabel("Lentille non évaluée.");
        _timingPreview = PreviewLabel("Durée non évaluée.");
        _safeAreaPreview = PreviewLabel("Cadre sûr non évalué.");
        _runtimeConsumption = PreviewLabel(
            "Consommation runtime non évaluée · aucune caméra n’est déplacée.");
        _boundary = StudioStyle.Label(
            "Aperçu géométrique seulement : aucune caméra live, séquence, contrôle joueur, interpolation, rendu ou réseau n’est exécuté.",
            10, StudioStyle.Warning, wrap: true);
        var previewBody = new VBoxContainer();
        previewBody.AddChild(_diff);
        previewBody.AddChild(_diagnostics);
        previewBody.AddChild(_cameraPose);
        previewBody.AddChild(_lookAtPreview);
        previewBody.AddChild(_framingPreview);
        previewBody.AddChild(_lensPreview);
        previewBody.AddChild(_timingPreview);
        previewBody.AddChild(_safeAreaPreview);
        previewBody.AddChild(_runtimeConsumption);
        previewBody.AddChild(_boundary);
        content.AddChild(Card("CameraAnchorPreview", "Aperçu de cadrage",
            "La commande module/zone est validée à sec avant de dessiner l’overlay et avant toute application.",
            previewBody));

        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _preview = StudioStyle.Button("Prévisualiser", compact: true);
        _apply = StudioStyle.Button("Appliquer", compact: true);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
            button.CustomMinimumSize = new(118, 34);
        _preview.Pressed += Preview;
        _apply.Pressed += Apply;
        _undo.Pressed += Undo;
        _redo.Pressed += Redo;
        _save.Pressed += Save;
        actions.AddChild(_preview);
        actions.AddChild(_apply);
        actions.AddChild(_undo);
        actions.AddChild(_redo);
        actions.AddChild(_save);
        root.AddChild(actions);
        _status = StudioStyle.Label("Choisissez une zone puis une ancre.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        _name.TextChanged += _ => DraftChanged();
        foreach (var line in _position.Concat(_rotation).Concat(_scale).Concat(_lookAt))
            line.TextChanged += _ => DraftChanged();
        _framing.ItemSelected += _ => DraftChanged();
        _fov.TextChanged += _ => DraftChanged();
        _duration.TextChanged += _ => DraftChanged();
        _safeArea.TextChanged += _ => DraftChanged();
        _tags.TextChanged += _ => DraftChanged();
        Resized += FitDialog;
        ResetPreview("Choisissez une zone puis une ancre.");
    }

    internal void Open()
    {
        if (_evidenceSession is not null) ReleaseEvidence();
        _session = _sourceSession;
        _session.CancelCameraAnchorPreviewUx2NavM();
        Visible = true;
        Reload(clearSelection: true);
        FitDialog();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceTempRoot is null) return;
        _worldViewport.ClearNavigationOverlayUx2NavB();
        if (_evidenceSession is not null) ReleaseEvidence();
        else _session.CancelCameraAnchorPreviewUx2NavM();
        _draft = null;
        _lastPreview = null;
        _canonicalPreviewReady = false;
        Visible = false;
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) ReleaseEvidence();
        _session = _sourceSession;
        Visible = true;
        Reload(clearSelection: true);
        FitDialog();
    }

    internal bool PrepareEvidence(string mode)
    {
        if (_sourceSession.Project is null || _sourceSession.History is null) return false;
        if (_evidenceSession is not null) ReleaseEvidence();
        var project = _sourceSession.CreateUx2NavMEvidenceProject();
        if (project is null) return false;
        _lastEvidenceSourceUntouched = false;
        _lastEvidenceResourcesReleased = false;
        _lastSourceRestored = false;
        var sourceSnapshot = CaptureSourceSnapshot();
        _evidenceSnapshot = sourceSnapshot;
        var root = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-nav-m-evidence-" + Guid.NewGuid().ToString("N"));
        _evidenceTempRoot = root;
        try
        {
            if (!ProjectSession.Create(root, project).Succeeded)
            {
                ReleaseEvidence();
                return false;
            }
            var evidence = new StudioSessionController();
            if (!evidence.Open(root).Succeeded)
            {
                evidence.Close();
                ReleaseEvidence();
                return false;
            }
            _evidenceSession = evidence;
            _session = evidence;
            var area = project.Areas.FirstOrDefault(item =>
                item.RuntimePolicies.Cinematics.CameraAnchors.Count > 0);
            if (area is null || !evidence.SelectArea(area.Id))
            {
                ReleaseEvidence();
                return false;
            }
            Visible = true;
            FitDialog();
            Reload(clearSelection: true);
            var areaIndex = _areaEntries.FindIndex(item => item?.AreaId == area.Id);
            if (areaIndex <= 0)
            {
                ReleaseEvidence();
                return false;
            }
            _area.Select(areaIndex);
            SelectArea(areaIndex);
            var anchorIndex = _anchorEntries.FindIndex(item => item is not null);
            if (anchorIndex <= 1)
            {
                ReleaseEvidence();
                return false;
            }
            _anchor.Select(anchorIndex);
            SelectAnchor(anchorIndex);
            if (_draft is null)
            {
                ReleaseEvidence();
                return false;
            }
            _draft = _draft with
            {
                FieldOfViewDegrees = _draft.FieldOfViewDegrees <= 177.5
                    ? _draft.FieldOfViewDegrees + 1.5
                    : _draft.FieldOfViewDegrees - 1.5,
                DefaultDurationMilliseconds = _draft.DefaultDurationMilliseconds <= 86_399_750
                    ? _draft.DefaultDurationMilliseconds + 250
                    : _draft.DefaultDurationMilliseconds - 250,
                SafeAreaMargin = _draft.SafeAreaMargin <= .44
                    ? _draft.SafeAreaMargin + .01
                    : Math.Max(0, _draft.SafeAreaMargin - .01),
                Tags = _draft.Tags.Append("review").Distinct(StringComparer.Ordinal).ToArray(),
            };
            PopulateDraft();
            Preview();
            _lastEvidenceSourceUntouched = SourceMatchesSnapshot(sourceSnapshot);
            SetStatus($"Preuve isolée prête · {HumanMode(mode)} · source intacte", false);
            var preview = _lastPreview;
            return preview is { Succeeded: true, CanApply: true, Framing: not null } && ShowsViewportOverlay &&
                   _lastEvidenceSourceUntouched;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            SetStatus($"Préparation de preuve refusée · {exception.Message}", true);
            ReleaseEvidence();
            return false;
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        StudioSessionController? reopened = null;
        try
        {
            if (!PrepareEvidence("transaction UI") || _draft is null || _evidenceTempRoot is null) return false;
            var evidenceRoot = _evidenceTempRoot;
            var before = ProjectJsonSerializer.Serialize(_session.Project!);
            var applied = _session.ApplyApprovedCameraAnchorUx2NavM();
            if (!applied.Succeeded) return false;
            var after = ProjectJsonSerializer.Serialize(_session.Project!);
            var undo = _session.Undo();
            var undone = ProjectJsonSerializer.Serialize(_session.Project!);
            var redo = _session.Redo();
            var redone = ProjectJsonSerializer.Serialize(_session.Project!);
            var saved = _session.Save();
            reopened = new();
            var opened = saved.Succeeded && reopened.Open(evidenceRoot).Succeeded;
            var persisted = opened ? ProjectJsonSerializer.Serialize(reopened.Project!) : string.Empty;
            return before != after && undo.Succeeded && redo.Succeeded && undone == before && redone == after &&
                   persisted == after;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_NAV_M_UI_TRANSACTION_FAILED {exception.Message}");
            return false;
        }
        finally
        {
            reopened?.Close();
            Close();
        }
    }

    private void Reload(bool clearSelection)
    {
        _updating = true;
        _areaChoices = _session.CameraAnchorAreaChoicesUx2NavM;
        _area.Clear();
        _areaEntries.Clear();
        _area.AddItem("Choisissez explicitement une zone…");
        _areaEntries.Add(null);
        foreach (var choice in _areaChoices)
        {
            _area.AddItem(choice.Label);
            _areaEntries.Add(choice);
        }
        FillAnchors(null);
        if (clearSelection)
        {
            _area.Select(0);
            _draft = null;
            _selection.Text = "Choisissez explicitement une zone puis une ancre caméra.";
            ResetPreview("Choisissez une zone puis une ancre.");
        }
        _updating = false;
        UpdateActions();
    }

    private void FillAnchors(Forge.Domain.Areas.AreaId? areaId)
    {
        _anchorChoices = _session.CameraAnchorChoicesUx2NavM(areaId);
        _anchor.Clear();
        _anchorEntries.Clear();
        _anchor.AddItem("Choisissez explicitement une ancre…");
        _anchorEntries.Add(null);
        _anchor.AddItem("Créer une nouvelle ancre caméra…");
        _anchorEntries.Add(null);
        foreach (var choice in _anchorChoices)
        {
            _anchor.AddItem(choice.Label);
            _anchorEntries.Add(choice);
        }
        _anchor.Select(0);
    }

    private void SelectArea(int index)
    {
        if (_updating) return;
        _session.CancelCameraAnchorPreviewUx2NavM();
        _worldViewport.ClearNavigationOverlayUx2NavB();
        _draft = null;
        _lastPreview = null;
        _canonicalPreviewReady = false;
        var choice = index >= 0 && index < _areaEntries.Count ? _areaEntries[index] : null;
        _updating = true;
        FillAnchors(choice?.AreaId);
        _updating = false;
        if (choice is null)
        {
            _selection.Text = "Aucune zone sélectionnée.";
            ResetPreview("Choisissez une zone.");
            return;
        }
        if (!_session.SelectArea(choice.AreaId))
        {
            _selection.Text = "La zone n’est plus disponible. Rechargez la liste.";
            ResetPreview("Sélection périmée.");
            return;
        }
        _selection.Text = $"Zone choisie · {choice.Label} · choisissez maintenant une ancre.";
        ResetPreview("Choisissez une ancre existante ou créez-en une nouvelle.");
    }

    private void SelectAnchor(int index)
    {
        if (_updating) return;
        _session.CancelCameraAnchorPreviewUx2NavM();
        _worldViewport.ClearNavigationOverlayUx2NavB();
        _lastPreview = null;
        _canonicalPreviewReady = false;
        var areaChoice = _area.Selected >= 0 && _area.Selected < _areaEntries.Count
            ? _areaEntries[_area.Selected]
            : null;
        if (areaChoice is null || index <= 0)
        {
            _draft = null;
            _selection.Text = areaChoice is null
                ? "Choisissez d’abord une zone."
                : "Aucune ancre sélectionnée.";
            ResetPreview("Choisissez une ancre.");
            return;
        }
        if (index == 1)
        {
            _draft = _session.CreateNewCameraAnchorDraftUx2NavM(areaChoice.AreaId);
            if (_draft is null)
            {
                _selection.Text = "La création de l’ancre n’est plus disponible.";
                ResetPreview("Sélection périmée.");
                return;
            }
            _selection.Text = $"Nouvelle ancre · {areaChoice.Label} · aucune donnée existante remplacée.";
        }
        else
        {
            var choice = index < _anchorEntries.Count ? _anchorEntries[index] : null;
            _draft = choice is null ? null :
                _session.CreateCameraAnchorDraftUx2NavM(areaChoice.AreaId, choice.AnchorId);
            if (_draft is null)
            {
                _selection.Text = "L’ancre n’est plus disponible. Rechargez la liste.";
                ResetPreview("Sélection périmée.");
                return;
            }
            _selection.Text = $"Ancre choisie · {choice!.Label}";
        }
        PopulateDraft();
        ResetPreview("Ajustez l’ancre puis prévisualisez son cadrage.");
    }

    private void PopulateDraft()
    {
        if (_draft is null) return;
        _updating = true;
        _name.Text = _draft.Name;
        SetVector(_position, _draft.PositionX, _draft.PositionY, _draft.PositionZ);
        SetVector(_rotation, _draft.RotationX, _draft.RotationY, _draft.RotationZ, _draft.RotationW);
        SetVector(_scale, _draft.ScaleX, _draft.ScaleY, _draft.ScaleZ);
        SetVector(_lookAt, _draft.LookAtX, _draft.LookAtY, _draft.LookAtZ);
        _framing.Select((int)_draft.Framing);
        _fov.Text = Invariant(_draft.FieldOfViewDegrees);
        _duration.Text = _draft.DefaultDurationMilliseconds.ToString(CultureInfo.InvariantCulture);
        _safeArea.Text = Invariant(_draft.SafeAreaMargin);
        _tags.Text = string.Join(", ", _draft.Tags);
        _updating = false;
    }

    private bool TryReadDraft(out StudioUx2NavMDraft? draft)
    {
        draft = null;
        if (_draft is null) return false;
        var position = ReadVector(_position);
        var rotation = ReadVector(_rotation);
        var scale = ReadVector(_scale);
        var lookAt = ReadVector(_lookAt);
        if (!double.TryParse(_fov.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var fov))
            fov = double.NaN;
        if (!int.TryParse(_duration.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var duration))
            duration = 0;
        if (!double.TryParse(_safeArea.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var safeArea))
            safeArea = double.NaN;
        var tags = _tags.Text.Split([',', ';'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        draft = _draft with
        {
            Name = _name.Text,
            PositionX = position[0], PositionY = position[1], PositionZ = position[2],
            RotationX = rotation[0], RotationY = rotation[1], RotationZ = rotation[2],
            RotationW = rotation[3],
            ScaleX = scale[0], ScaleY = scale[1], ScaleZ = scale[2],
            LookAtX = lookAt[0], LookAtY = lookAt[1], LookAtZ = lookAt[2],
            Framing = (CameraFraming)_framing.Selected,
            FieldOfViewDegrees = fov,
            DefaultDurationMilliseconds = duration,
            SafeAreaMargin = safeArea,
            Tags = tags,
        };
        return true;
    }

    private void Preview()
    {
        if (!TryReadDraft(out var draft) || draft is null) return;
        _draft = draft;
        var preview = _session.PreviewCameraAnchorUx2NavM(draft);
        _lastPreview = preview;
        _canonicalPreviewReady = preview.Succeeded;
        RenderPreview(preview);
        SetStatus(preview.Message, !preview.Succeeded);
    }

    private void RenderPreview(StudioUx2NavMPreview preview)
    {
        Clear(_diff);
        if (preview.Diff.Count == 0)
            _diff.AddChild(StudioStyle.Label("Aucun changement à afficher.", 10, StudioStyle.Muted));
        foreach (var row in preview.Diff)
        {
            var color = row.Changed ? StudioStyle.Accent : StudioStyle.Muted;
            _diff.AddChild(StudioStyle.Label($"{row.Section} · avant : {row.Before}",
                10, StudioStyle.Muted, wrap: true));
            _diff.AddChild(StudioStyle.Label($"{row.Section} · après : {row.After}", 10, color, wrap: true));
        }
        Clear(_diagnostics);
        if (preview.Diagnostics.Count == 0)
            _diagnostics.AddChild(StudioStyle.Label("Validation canonique conforme.", 10, StudioStyle.Success));
        foreach (var item in preview.Diagnostics)
            _diagnostics.AddChild(StudioStyle.Label($"{DiagnosticSection(item.Path)} · {item.Message}",
                10, StudioStyle.Danger, wrap: true));
        if (preview.Framing is not { } framing)
        {
            _worldViewport.ClearNavigationOverlayUx2NavB();
            ResetFramingLabels();
            UpdateActions();
            return;
        }
        _cameraPose.Text = framing.CameraPose;
        _lookAtPreview.Text = framing.LookAt;
        _framingPreview.Text = $"Cadrage · {framing.Framing}";
        _lensPreview.Text = $"Lentille · {framing.Lens}";
        _timingPreview.Text = $"Durée · {framing.Timing}";
        _safeAreaPreview.Text = $"Cadre sûr · {framing.SafeArea}";
        _runtimeConsumption.Text = framing.RuntimeConsumption;
        _boundary.Text = framing.Boundary;
        _worldViewport.ShowNavigationOverlayUx2NavB(framing.Overlay);
        UpdateActions();
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedCameraAnchorUx2NavM();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            UpdateActions();
            return;
        }
        var areaId = _draft?.AreaId;
        var anchorId = _draft?.AnchorId;
        CameraAnchorChanged?.Invoke();
        ReloadAndReselect(areaId, anchorId);
    }

    private void Undo()
    {
        var areaId = _draft?.AreaId;
        var anchorId = _draft?.AnchorId;
        var result = _session.Undo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            CameraAnchorChanged?.Invoke();
            ReloadAndReselect(areaId, anchorId);
        }
    }

    private void Redo()
    {
        var areaId = _draft?.AreaId;
        var anchorId = _draft?.AnchorId;
        var result = _session.Redo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            CameraAnchorChanged?.Invoke();
            ReloadAndReselect(areaId, anchorId);
        }
    }

    private void Save()
    {
        var result = _session.Save();
        SetStatus(result.Message, !result.Succeeded);
        UpdateActions();
    }

    private void ReloadAndReselect(Forge.Domain.Areas.AreaId? areaId, Guid? anchorId)
    {
        _worldViewport.ClearNavigationOverlayUx2NavB();
        Reload(clearSelection: true);
        if (areaId is null) return;
        var areaIndex = _areaEntries.FindIndex(item => item?.AreaId == areaId);
        if (areaIndex <= 0) return;
        _area.Select(areaIndex);
        SelectArea(areaIndex);
        if (anchorId is null) return;
        var anchorIndex = _anchorEntries.FindIndex(item => item?.AnchorId == anchorId);
        if (anchorIndex <= 1) return;
        _anchor.Select(anchorIndex);
        SelectAnchor(anchorIndex);
    }

    private void DraftChanged()
    {
        if (_updating || _draft is null) return;
        _session.CancelCameraAnchorPreviewUx2NavM();
        _worldViewport.ClearNavigationOverlayUx2NavB();
        _canonicalPreviewReady = false;
        _lastPreview = null;
        Clear(_diff);
        _diff.AddChild(StudioStyle.Label(
            "Aperçu précédent invalidé. Prévisualisez les nouvelles valeurs.",
            10, StudioStyle.Warning, wrap: true));
        Clear(_diagnostics);
        _diagnostics.AddChild(StudioStyle.Label("Validation en attente.", 10, StudioStyle.Muted));
        ResetFramingLabels();
        SetStatus("Aperçu invalidé par la modification.", false);
        UpdateActions();
    }

    private void ResetPreview(string message)
    {
        _lastPreview = null;
        _canonicalPreviewReady = false;
        Clear(_diff);
        _diff.AddChild(StudioStyle.Label("Prévisualisez pour afficher le diff humain.",
            10, StudioStyle.Muted));
        Clear(_diagnostics);
        _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic affiché.", 10, StudioStyle.Muted));
        ResetFramingLabels();
        SetStatus(message, false);
        UpdateActions();
    }

    private void ResetFramingLabels()
    {
        _cameraPose.Text = "Position caméra non évaluée.";
        _lookAtPreview.Text = "Cible non évaluée.";
        _framingPreview.Text = "Cadrage non évalué.";
        _lensPreview.Text = "Lentille non évaluée.";
        _timingPreview.Text = "Durée non évaluée.";
        _safeAreaPreview.Text = "Cadre sûr non évalué.";
        _runtimeConsumption.Text = "Consommation runtime non évaluée · aucune caméra n’est déplacée.";
        _boundary.Text =
            "Aperçu géométrique seulement : aucune caméra live, séquence, contrôle joueur, interpolation, rendu ou réseau n’est exécuté.";
    }

    private void UpdateActions()
    {
        _preview.Disabled = _draft is null;
        _apply.Disabled = _lastPreview?.CanApply != true;
        _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo;
        _save.Disabled = _session.Project is null;
    }

    private StudioUx2NavMEvidenceSourceSnapshot CaptureSourceSnapshot()
    {
        var history = _sourceSession.History ?? throw new InvalidOperationException("Historique source absent.");
        return new(_sourceSession.Project ?? throw new InvalidOperationException("Projet source absent."),
            history, history.State.Revision, history.Journal.ToArray(), _sourceSession.IsDirty,
            history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureCameraAnchorApprovalUx2NavM());
    }

    private bool SourceMatchesSnapshot(StudioUx2NavMEvidenceSourceSnapshot snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.IsDirty && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo && _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureCameraAnchorApprovalUx2NavM() == snapshot.Approval;
    }

    private void ReleaseEvidence()
    {
        var snapshot = _evidenceSnapshot;
        _worldViewport.ClearNavigationOverlayUx2NavB();
        _evidenceSession?.CancelCameraAnchorPreviewUx2NavM();
        _evidenceSession?.Close();
        _evidenceSession = null;
        var root = _evidenceTempRoot;
        _evidenceTempRoot = null;
        _session = _sourceSession;
        _lastSourceRestored = true;
        _lastEvidenceSourceUntouched = snapshot is not null && SourceMatchesSnapshot(snapshot);
        _evidenceSnapshot = null;
        var released = true;
        if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
        {
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) { released = false; }
            catch (UnauthorizedAccessException) { released = false; }
        }
        _lastEvidenceResourcesReleased = released && (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root));
    }

    private static OptionButton Option(params string[] labels)
    {
        var option = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, FitToLongestItem = false };
        StudioStyle.ApplyOptionButton(option);
        foreach (var label in labels) option.AddItem(label);
        return option;
    }

    private static LineEdit Line(string placeholder)
    {
        var line = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyLineEdit(line);
        return line;
    }

    private static LineEdit NumberLine(string placeholder)
    {
        var line = Line(placeholder);
        line.TooltipText = "Valeur brute sans clamp ni arrondi implicite ; la validation canonique décide.";
        return line;
    }

    private static LineEdit[] VectorLines(params string[] placeholders) =>
        placeholders.Select(NumberLine).ToArray();

    private static GridContainer Grid(int columns)
    {
        var grid = new GridContainer { Columns = columns, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 6);
        return grid;
    }

    private static VBoxContainer Field(string label, Control control)
    {
        var field = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        field.AddThemeConstantOverride("separation", 3);
        field.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted, wrap: true));
        field.AddChild(control);
        return field;
    }

    private static VBoxContainer VectorField(string label, IReadOnlyList<LineEdit> lines)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var line in lines)
        {
            line.CustomMinimumSize = new(80, StudioStyle.ControlHeight);
            row.AddChild(line);
        }
        return Field(label, row);
    }

    private static PanelContainer Card(string name, string title, string help, Control body)
    {
        var card = StudioStyle.Panel(name, StudioStyle.Surface, 8);
        var margin = StudioStyle.Margin(12, 10, 12, 10);
        card.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 5);
        margin.AddChild(root);
        root.AddChild(StudioStyle.Label(title, 13));
        root.AddChild(StudioStyle.Label(help, 10, StudioStyle.Muted, wrap: true));
        root.AddChild(body);
        return card;
    }

    private static Label PreviewLabel(string text) => StudioStyle.Label(text, 10, StudioStyle.Muted, wrap: true);

    private static double[] ReadVector(IReadOnlyList<LineEdit> lines) => lines.Select(item =>
        double.TryParse(item.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : double.NaN).ToArray();

    private static void SetVector(IReadOnlyList<LineEdit> lines, params double[] values)
    {
        for (var index = 0; index < lines.Count; index++) lines[index].Text = Invariant(values[index]);
    }

    private static string Invariant(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string DiagnosticSection(string path) =>
        path.Contains("transform", StringComparison.Ordinal) ? "Transform" :
        path.Contains("cameraAnchors", StringComparison.Ordinal) ? "Ancre caméra" :
        path.Contains("cinematics", StringComparison.Ordinal) ? "Cinématiques" : "Projet";

    private static string HumanMode(string mode) => mode.Replace('-', ' ').Replace('_', ' ');

    private void FitDialog()
    {
        if (!Visible) return;
        var viewport = GetViewportRect().Size;
        var width = Mathf.Clamp(viewport.X - 48, 920, 1400);
        var height = Mathf.Clamp(viewport.Y - 36, 700, 1040);
        _dialog.Position = new((viewport.X - width) / 2, (viewport.Y - height) / 2);
        _dialog.Size = new(width, height);
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(message, error);
    }

    private static void Clear(Node node)
    {
        foreach (var child in node.GetChildren()) child.Free();
    }

    private static IEnumerable<Label> DescendantLabels(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Label label) yield return label;
            foreach (var nested in DescendantLabels(child)) yield return nested;
        }
    }

    private static bool ContainsTechnicalIdentifier(string text) =>
        text.Split([' ', '·', '/', ':', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Any(token => Guid.TryParse(token, out _));
}
