using System.Globalization;
using System.Text.RegularExpressions;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioRuntimeOperationsWorkbenchUx6Y : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private SourceSnapshotUx6Y? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;
    private bool _updating;
    private bool _explicitSelection;

    private readonly OptionButton _areaChoice;
    private readonly Label _selection;
    private readonly TabContainer _tabs;
    private readonly OptionButton _weatherState;
    private readonly CheckBox _serverAuthority;
    private readonly LineEdit _seed;
    private readonly CheckBox _persistWeather;
    private readonly CheckBox _replicateWeather;
    private readonly CheckBox _asyncStreaming;
    private readonly CheckBox _prefetchDependencies;
    private readonly CheckBox _cancellation;
    private readonly LineEdit _cacheBudget;
    private readonly LineEdit _concurrency;
    private readonly LineEdit _chunkRadius;
    private readonly LineEdit _targetMilliseconds;
    private readonly TextEdit _requiredResources;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _weatherResults;
    private readonly VBoxContainer _loadingResults;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _previewButton;
    private readonly Button _applyButton;
    private readonly Button _undoButton;
    private readonly Button _redoButton;
    private readonly Button _saveButton;

    private IReadOnlyList<StudioUx6YAreaChoice> _areas = [];
    private IReadOnlyList<StudioUx6YWeatherStateChoice> _states = [];
    private StudioUx6YDraft? _draft;
    private StudioUx6YPreview? _preview;
    private bool _previewReady;
    private string? _previewFingerprint;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? RuntimeOperationsChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _explicitSelection && _draft is not null && _areaChoice.Selected > 0;
    internal bool PreviewReady => _previewReady && _preview is
    {
        Succeeded: true, CanApply: true, WeatherDryRun: true, LoadingDryRun: true,
        WeatherRuntimeResolved: true, StreamingRuntimeResolved: true, RuntimeClaimsBounded: true,
    };
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && !ReferenceEquals(_session, _sourceSession);
    internal bool EvidenceSourceUntouched => SourceMatchesUx6Y();
    internal bool EvidenceResourcesReleased => _evidenceReleased && _evidenceSession is null && _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool ShowsTwoWorkflows => _tabs.GetTabCount() == 2 &&
                                        _tabs.GetTabTitle(0) == "Météo runtime" &&
                                        _tabs.GetTabTitle(1) == "Streaming de zone";
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx6Y(this)
                .Concat(Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle)));
            return text.Contains("Opérations runtime de zone", StringComparison.Ordinal) &&
                   text.Contains("Aperçu sec canonique", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Météo runtime", StringComparison.Ordinal) &&
                   text.Contains("Streaming de zone", StringComparison.Ordinal) &&
                   text.Contains("aucun réseau", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuidUx6Y(text);
        }
    }

    internal StudioRuntimeOperationsWorkbenchUx6Y(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.012f, .018f, .027f, .975f);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        var dialog = StudioStyle.Panel("Ux6YRuntimeOperationsDialog", StudioStyle.Surface, 12, 1);
        dialog.ClipContents = true;
        dialog.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        dialog.OffsetLeft = 18;
        dialog.OffsetTop = 14;
        dialog.OffsetRight = -18;
        dialog.OffsetBottom = -14;
        AddChild(dialog);
        var margin = StudioStyle.Margin(14, 12, 14, 12);
        dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        headingText.AddChild(StudioStyle.Label("Opérations runtime de zone", 18));
        headingText.AddChild(StudioStyle.Label(
            "Météo autoritaire et streaming amélioré · aperçu sec canonique · deux commandes annulables.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        root.AddChild(heading);

        _areaChoice = StudioUx2EnvBControls.Options();
        _areaChoice.ItemSelected += SelectAreaUx6Y;
        root.AddChild(FieldUx6Y("Zone · sélection explicite", _areaChoice, 680));
        _selection = StudioStyle.Label("Choisissez explicitement une zone avant toute prévisualisation.",
            10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        var tabsHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 300),
            CustomMaximumSize = new Vector2(-1, 560),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            PropagateMaximumSize = true,
        };
        root.AddChild(tabsHost);
        _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        tabsHost.AddChild(_tabs);

        var weatherTab = ScrollTabUx6Y("Météo runtime", out var weatherBody);
        _tabs.AddChild(weatherTab);
        _weatherState = StudioUx2EnvBControls.Options();
        weatherBody.AddChild(FieldUx6Y("État météo initial", _weatherState));
        var weatherFlags = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _serverAuthority = StudioUx2EnvBControls.Check("Serveur autoritaire");
        _persistWeather = StudioUx2EnvBControls.Check("Persister le snapshot");
        _replicateWeather = StudioUx2EnvBControls.Check("Répliquer l’état");
        weatherFlags.AddChild(_serverAuthority);
        weatherFlags.AddChild(_persistWeather);
        weatherFlags.AddChild(_replicateWeather);
        weatherBody.AddChild(weatherFlags);
        _seed = IntegerUx6Y("2026");
        weatherBody.AddChild(StudioUx2EnvBControls.Card("Ux6YWeatherPolicy", "Déterminisme et autorité",
            "Les états, transitions, effets et ressources restent la propriété d’UX2‑ENV‑C ; cette surface ne modifie que leur politique runtime.",
            FieldUx6Y("Seed déterministe", _seed)));
        weatherBody.AddChild(StudioStyle.Label(
            "L’aperçu exécute la machine déterministe et construit l’événement weather.changed sur une copie. " +
            "Il ne publie aucun paquet réseau et n’exécute aucun ForgeScript.", 10, StudioStyle.Muted, wrap: true));

        var loadingTab = ScrollTabUx6Y("Streaming de zone", out var loadingBody);
        _tabs.AddChild(loadingTab);
        var loadingFlags = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _asyncStreaming = StudioUx2EnvBControls.Check("Streaming asynchrone");
        _prefetchDependencies = StudioUx2EnvBControls.Check("Précharger les dépendances");
        _cancellation = StudioUx2EnvBControls.Check("Propager l’annulation");
        loadingFlags.AddChild(_asyncStreaming);
        loadingFlags.AddChild(_prefetchDependencies);
        loadingFlags.AddChild(_cancellation);
        loadingBody.AddChild(loadingFlags);
        var loadingGrid = GridUx6Y(4);
        _cacheBudget = IntegerUx6Y("512");
        _concurrency = IntegerUx6Y("4");
        _chunkRadius = IntegerUx6Y("2");
        _targetMilliseconds = IntegerUx6Y("2000");
        loadingGrid.AddChild(FieldUx6Y("Cache (Mio)", _cacheBudget));
        loadingGrid.AddChild(FieldUx6Y("Chargements simultanés", _concurrency));
        loadingGrid.AddChild(FieldUx6Y("Rayon de chunks", _chunkRadius));
        loadingGrid.AddChild(FieldUx6Y("Cible (ms)", _targetMilliseconds));
        loadingBody.AddChild(StudioUx2EnvBControls.Card("Ux6YStreamingBudget", "Concurrence, cache et benchmark",
            "Les nombres sont lus exactement, sans arrondi ni clamp silencieux.", loadingGrid));
        _requiredResources = TextAreaUx6Y("", 88);
        loadingBody.AddChild(FieldUx6Y("Ressources requises · une ressource relative sûre par ligne",
            _requiredResources));
        loadingBody.AddChild(StudioStyle.Label(
            "L’aperçu utilise un chargeur mémoire déterministe pour exercer dépendances, progression, cache, annulation et gate de temps. " +
            "Aucune ressource ni scène Godot n’est chargée.", 10, StudioStyle.Muted, wrap: true));

        var review = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 150),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _diff = ReviewColumnUx6Y(review, "DIFF HUMAIN");
        _weatherResults = ReviewColumnUx6Y(review, "MÉTÉO COPIÉE");
        _loadingResults = ReviewColumnUx6Y(review, "STREAMING COPIÉ");
        _diagnostics = ReviewColumnUx6Y(review, "DIAGNOSTICS");
        root.AddChild(review);
        _summary = StudioStyle.Label("Sélectionnez une zone, puis préparez un aperçu sec.", 10,
            StudioStyle.Muted, wrap: true);
        root.AddChild(_summary);

        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _previewButton = StudioStyle.Button("Aperçu sec", primary: true);
        _applyButton = StudioStyle.Button("Appliquer");
        _undoButton = StudioStyle.Button("Annuler");
        _redoButton = StudioStyle.Button("Rétablir");
        _saveButton = StudioStyle.Button("Enregistrer");
        foreach (var button in new[] { _previewButton, _applyButton, _undoButton, _redoButton, _saveButton })
        {
            button.CustomMinimumSize = new Vector2(button == _previewButton ? 132 : 108, 34);
            actions.AddChild(button);
        }
        actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _status = StudioStyle.Label("Aucune mutation appliquée.", 10, StudioStyle.Muted, wrap: true);
        _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        actions.AddChild(_status);
        root.AddChild(actions);

        _previewButton.Pressed += PreviewUx6Y;
        _applyButton.Pressed += ApplyUx6Y;
        _undoButton.Pressed += UndoUx6Y;
        _redoButton.Pressed += RedoUx6Y;
        _saveButton.Pressed += SaveUx6Y;
        WireChangesUx6Y();
        ResetControlsUx6Y();
    }

    internal void Open() => OpenBoundUx6Y(_sourceSession, evidence: false, cancelApproval: false);

    internal void RestoreSourceSurfaceAfterSmoke() =>
        OpenBoundUx6Y(_sourceSession, evidence: false, cancelApproval: false);

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null) return;
        if (Visible) _session.CancelRuntimeOperationsApprovalUx6Y();
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidenceUx6Y();
        _session = _sourceSession;
        ResetControlsUx6Y();
    }

    internal void CloseEvidence()
    {
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidenceUx6Y();
        _session = _sourceSession;
        ResetControlsUx6Y(keepReleaseState: true);
    }

    internal bool PrepareEvidence(string mode)
    {
        if (!mode.StartsWith("ux6-y", StringComparison.OrdinalIgnoreCase)) return false;
        ReleaseEvidenceUx6Y();
        _sourceSnapshot = CaptureSourceUx6Y();
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux6-y-{Guid.NewGuid():N}");
        try
        {
            var source = _sourceSession.Project;
            if (source is null || !ProjectSession.Create(root, source).Succeeded) return false;
            var isolated = new StudioSessionController();
            if (!isolated.Open(root).Succeeded)
            {
                isolated.Close();
                StudioSessionController.DeleteTemporaryUx6Y(root);
                return false;
            }
            _evidenceSession = isolated;
            _evidenceRoot = root;
            _evidenceReleased = false;
            OpenBoundUx6Y(isolated, evidence: true, cancelApproval: true);
            _areaChoice.Select(1);
            SelectAreaUx6Y(1);
            if (_draft is null) return false;
            _draft = StudioSessionController.ChangedRuntimeOperationsDraftUx6Y(_draft);
            PopulateDraftUx6Y();
            _preview = _session.PreviewRuntimeOperationsUx6Y(_draft);
            RenderPreviewUx6Y();
            _tabs.CurrentTab = mode.EndsWith("-area-loading", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            var ready = PreviewReady;
            var workflows = ShowsTwoWorkflows;
            var human = ShowsHumanSurface;
            var sourceMatches = SourceMatchesUx6Y();
            if (!ready || !workflows || !human || !sourceMatches)
                GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_Y_UI_PREPARE_FAILED " +
                            $"preview={ready.ToString().ToLowerInvariant()} " +
                            $"workflows={workflows.ToString().ToLowerInvariant()} " +
                            $"human={human.ToString().ToLowerInvariant()} " +
                            $"source={sourceMatches.ToString().ToLowerInvariant()}");
            return ready && workflows && human && sourceMatches;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_Y_EVIDENCE_FAILED type={exception.GetType().Name}");
            return false;
        }
        finally
        {
            if (!PreviewReady || !ShowsTwoWorkflows || !SourceMatchesUx6Y())
            {
                if (_evidenceSession is not null) ReleaseEvidenceUx6Y();
                else StudioSessionController.DeleteTemporaryUx6Y(root);
            }
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        var sourceApproval = _sourceSession.CaptureRuntimeOperationsApprovalUx6Y();
        try
        {
            var ready = PrepareEvidence("ux6-y-runtime-operations");
            if (!ready || _evidenceSession is null || !ReferenceEquals(_session, _evidenceSession) ||
                !PreviewReady || _draft is null || string.IsNullOrWhiteSpace(_evidenceRoot)) return false;
            var applied = _session.ApplyApprovedRuntimeOperationsUx6Y(_draft);
            var undo = applied.Succeeded ? _session.UndoRuntimeOperationsUx6Y() : new(false, "non exécuté");
            var redo = undo.Succeeded ? _session.RedoRuntimeOperationsUx6Y() : new(false, "non exécuté");
            var saved = redo.Succeeded ? _session.SaveRuntimeOperationsUx6Y() : new(false, "non exécuté");
            var projectJson = _session.Project is null ? string.Empty : ProjectJsonSerializer.Serialize(_session.Project);
            var reopened = new StudioSessionController();
            var roundTrip = saved.Succeeded && reopened.Open(_evidenceRoot).Succeeded &&
                            reopened.Project is not null &&
                            ProjectJsonSerializer.Serialize(reopened.Project) == projectJson;
            reopened.Close();
            return applied.Succeeded && undo.Succeeded && redo.Succeeded && saved.Succeeded && roundTrip &&
                   SourceMatchesUx6Y();
        }
        finally
        {
            CloseEvidence();
            _sourceSession.RestoreRuntimeOperationsApprovalUx6Y(sourceApproval);
        }
    }

    private void OpenBoundUx6Y(StudioSessionController session, bool evidence, bool cancelApproval)
    {
        _session = session;
        if (cancelApproval) _session.CancelRuntimeOperationsApprovalUx6Y();
        Visible = true;
        _areas = _session.AreaChoicesUx6Y;
        _updating = true;
        _areaChoice.Clear();
        _areaChoice.AddItem("Choisir explicitement une zone…");
        foreach (var area in _areas) _areaChoice.AddItem(area.Label);
        _areaChoice.Select(0);
        _updating = false;
        _explicitSelection = false;
        _draft = null;
        _selection.Text = evidence
            ? "Session de preuve isolée · choisissez la zone copiée."
            : "Choisissez explicitement une zone avant toute prévisualisation.";
        ResetPreviewUx6Y();
        UpdateButtonsUx6Y();
    }

    private void SelectAreaUx6Y(long index)
    {
        if (_updating) return;
        _explicitSelection = index > 0 && index <= _areas.Count;
        var areaId = _explicitSelection ? _areas[(int)index - 1].Id : null;
        _draft = _session.CreateRuntimeOperationsDraftUx6Y(areaId, _explicitSelection);
        if (_draft is null)
        {
            _selection.Text = "Sélection explicite requise.";
            ResetPreviewUx6Y();
            return;
        }
        _selection.Text = UsesIsolatedEvidence
            ? "Session de preuve isolée · zone copiée · source Crypte des Cendres strictement inchangée."
            : "Zone sélectionnée · les états météo et la présentation UX1‑C restent protégés.";
        PopulateDraftUx6Y();
        ResetPreviewUx6Y();
    }

    private void PopulateDraftUx6Y()
    {
        if (_draft is null) return;
        _updating = true;
        _states = _session.WeatherStateChoicesUx6Y(_draft.AreaId);
        _weatherState.Clear();
        _weatherState.AddItem("Choisir un état météo initial…");
        foreach (var state in _states) _weatherState.AddItem($"{state.Label} · {HumanWeatherKindUx6Y(state.Kind)}");
        var stateIndex = _states.ToList().FindIndex(item => item.Id == _draft.Weather.InitialStateId);
        _weatherState.Select(stateIndex + 1);
        _serverAuthority.ButtonPressed = _draft.Weather.ServerAuthoritative;
        _seed.Text = _draft.Weather.DeterministicSeed.ToString(CultureInfo.InvariantCulture);
        _persistWeather.ButtonPressed = _draft.Weather.PersistState;
        _replicateWeather.ButtonPressed = _draft.Weather.ReplicateState;
        _asyncStreaming.ButtonPressed = _draft.Loading.AsyncStreaming;
        _prefetchDependencies.ButtonPressed = _draft.Loading.PrefetchDependencies;
        _cancellation.ButtonPressed = _draft.Loading.CancellationEnabled;
        _cacheBudget.Text = _draft.Loading.CacheBudgetMegabytes.ToString(CultureInfo.InvariantCulture);
        _concurrency.Text = _draft.Loading.MaximumConcurrentLoads.ToString(CultureInfo.InvariantCulture);
        _chunkRadius.Text = _draft.Loading.ChunkRadius.ToString(CultureInfo.InvariantCulture);
        _targetMilliseconds.Text = _draft.Loading.TargetLoadMilliseconds.ToString(CultureInfo.InvariantCulture);
        _requiredResources.Text = string.Join('\n', _draft.Loading.RequiredResources);
        _updating = false;
    }

    private void PreviewUx6Y()
    {
        if (!TryReadDraftUx6Y(out var draft)) return;
        _draft = draft;
        _preview = _session.PreviewRuntimeOperationsUx6Y(draft!);
        RenderPreviewUx6Y();
    }

    private void ApplyUx6Y()
    {
        if (!PreviewReady || !TryReadDraftUx6Y(out var current) || current is null ||
            _previewFingerprint != _session.RuntimeOperationsDraftFingerprintUx6Y(current))
        {
            SetLocalStatusUx6Y("Le brouillon a changé · relancez l’aperçu sec.", true);
            ResetPreviewUx6Y();
            return;
        }
        var result = _session.ApplyApprovedRuntimeOperationsUx6Y(current);
        SetLocalStatusUx6Y(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            RuntimeOperationsChanged?.Invoke();
            _draft = _session.CreateRuntimeOperationsDraftUx6Y(current.AreaId, true);
            PopulateDraftUx6Y();
        }
        ResetPreviewUx6Y();
    }

    private void UndoUx6Y() => HandleActionUx6Y(_session.UndoRuntimeOperationsUx6Y());
    private void RedoUx6Y() => HandleActionUx6Y(_session.RedoRuntimeOperationsUx6Y());
    private void SaveUx6Y() => HandleActionUx6Y(_session.SaveRuntimeOperationsUx6Y(), reload: false);

    private void HandleActionUx6Y(StudioUx6YActionResult result, bool reload = true)
    {
        SetLocalStatusUx6Y(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            RuntimeOperationsChanged?.Invoke();
            if (reload && _draft is not null)
            {
                _draft = _session.CreateRuntimeOperationsDraftUx6Y(_draft.AreaId, true);
                PopulateDraftUx6Y();
            }
        }
        ResetPreviewUx6Y();
    }

    private bool TryReadDraftUx6Y(out StudioUx6YDraft? draft)
    {
        draft = null;
        if (_draft is null || _weatherState.Selected <= 0 || _weatherState.Selected > _states.Count)
        {
            SetLocalStatusUx6Y("Choisissez explicitement une zone et son état météo initial.", true);
            return false;
        }
        if (!TryParseIntUx6Y(_seed.Text, out var seed) ||
            !TryParseIntUx6Y(_cacheBudget.Text, out var cache) ||
            !TryParseIntUx6Y(_concurrency.Text, out var concurrency) ||
            !TryParseIntUx6Y(_chunkRadius.Text, out var radius) ||
            !TryParseIntUx6Y(_targetMilliseconds.Text, out var target))
        {
            SetLocalStatusUx6Y("Seed, cache, concurrence, rayon et cible doivent être des entiers exacts.", true);
            return false;
        }
        draft = _draft with
        {
            Weather = _draft.Weather with
            {
                InitialStateId = _states[_weatherState.Selected - 1].Id,
                ServerAuthoritative = _serverAuthority.ButtonPressed,
                DeterministicSeed = seed,
                PersistState = _persistWeather.ButtonPressed,
                ReplicateState = _replicateWeather.ButtonPressed,
            },
            Loading = _draft.Loading with
            {
                AsyncStreaming = _asyncStreaming.ButtonPressed,
                PrefetchDependencies = _prefetchDependencies.ButtonPressed,
                CancellationEnabled = _cancellation.ButtonPressed,
                CacheBudgetMegabytes = cache,
                MaximumConcurrentLoads = concurrency,
                ChunkRadius = radius,
                TargetLoadMilliseconds = target,
                RequiredResources = SplitLinesUx6Y(_requiredResources.Text),
            },
        };
        return true;
    }

    private void RenderPreviewUx6Y()
    {
        ClearUx6Y(_diff);
        ClearUx6Y(_weatherResults);
        ClearUx6Y(_loadingResults);
        ClearUx6Y(_diagnostics);
        if (_preview is null)
        {
            ResetPreviewUx6Y();
            return;
        }
        foreach (var item in _preview.Diff) AddReviewUx6Y(_diff, item);
        foreach (var item in _preview.WeatherResults) AddReviewUx6Y(_weatherResults, item);
        foreach (var item in _preview.LoadingResults) AddReviewUx6Y(_loadingResults, item);
        foreach (var item in _preview.Diagnostics)
            _diagnostics.AddChild(StudioStyle.Label($"{HumanUx6Y(item.Code)} · {HumanUx6Y(item.Message)}",
                10, StudioStyle.Danger, wrap: true));
        if (_preview.Diff.Count == 0) AddReviewUx6Y(_diff, "Aucune différence.", StudioStyle.Muted);
        if (_preview.Diagnostics.Count == 0)
            AddReviewUx6Y(_diagnostics, "Aucun diagnostic bloquant.", StudioStyle.Success);
        _summary.Text = _preview.Message;
        _previewReady = _preview.Succeeded && _preview.CanApply && _preview.WeatherDryRun &&
                        _preview.LoadingDryRun && _preview.WeatherRuntimeResolved &&
                        _preview.StreamingRuntimeResolved && _preview.RuntimeClaimsBounded && _draft is not null;
        _previewFingerprint = _previewReady ? _session.RuntimeOperationsDraftFingerprintUx6Y(_draft!) : null;
        SetLocalStatusUx6Y(_preview.Message, !_preview.Succeeded);
        UpdateButtonsUx6Y();
    }

    private void WireChangesUx6Y()
    {
        _weatherState.ItemSelected += _ => InvalidatePreviewUx6Y();
        foreach (var check in new[] { _serverAuthority, _persistWeather, _replicateWeather, _asyncStreaming,
                     _prefetchDependencies, _cancellation })
            check.Toggled += _ => InvalidatePreviewUx6Y();
        foreach (var edit in new[] { _seed, _cacheBudget, _concurrency, _chunkRadius, _targetMilliseconds })
            edit.TextChanged += _ => InvalidatePreviewUx6Y();
        _requiredResources.TextChanged += InvalidatePreviewUx6Y;
    }

    private void InvalidatePreviewUx6Y()
    {
        if (_updating) return;
        _session.CancelRuntimeOperationsApprovalUx6Y();
        _previewReady = false;
        _previewFingerprint = null;
        _applyButton.Disabled = true;
        _summary.Text = "Brouillon modifié · relancez l’aperçu sec canonique.";
    }

    private void ResetPreviewUx6Y()
    {
        _preview = null;
        _previewReady = false;
        _previewFingerprint = null;
        ClearUx6Y(_diff);
        ClearUx6Y(_weatherResults);
        ClearUx6Y(_loadingResults);
        ClearUx6Y(_diagnostics);
        AddReviewUx6Y(_diff, "Aucun aperçu préparé.", StudioStyle.Muted);
        AddReviewUx6Y(_weatherResults, "Machine météo non exécutée.", StudioStyle.Muted);
        AddReviewUx6Y(_loadingResults, "Streaming mémoire non exécuté.", StudioStyle.Muted);
        AddReviewUx6Y(_diagnostics, "Aucun diagnostic.", StudioStyle.Muted);
        _summary.Text = _draft is null
            ? "Sélectionnez une zone, puis préparez un aperçu sec."
            : "Zone chargée · modifiez un onglet, puis prévisualisez.";
        UpdateButtonsUx6Y();
    }

    private void ResetControlsUx6Y(bool keepReleaseState = false)
    {
        _updating = true;
        _areaChoice.Clear();
        _areaChoice.AddItem("Choisir explicitement une zone…");
        _weatherState.Clear();
        _weatherState.AddItem("Choisir un état météo initial…");
        _requiredResources.Text = string.Empty;
        _updating = false;
        _areas = [];
        _states = [];
        _draft = null;
        _explicitSelection = false;
        if (!keepReleaseState) _evidenceReleased = _evidenceSession is null;
        ResetPreviewUx6Y();
    }

    private void UpdateButtonsUx6Y()
    {
        var selected = HasExplicitSelection;
        _previewButton.Disabled = !selected;
        _applyButton.Disabled = !PreviewReady;
        _undoButton.Disabled = _session.History?.CanUndo != true;
        _redoButton.Disabled = _session.History?.CanRedo != true;
        _saveButton.Disabled = !_session.IsDirty;
    }

    private void SetLocalStatusUx6Y(string message, bool error)
    {
        _status.Text = message;
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Success;
        StatusChanged?.Invoke(message, error);
    }

    private SourceSnapshotUx6Y CaptureSourceUx6Y()
    {
        var history = _sourceSession.History;
        return new(_sourceSession.Project, history, history?.State.Revision,
            history?.Journal.ToArray() ?? [], _sourceSession.IsDirty, history?.CanUndo, history?.CanRedo,
            _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(),
            _sourceSession.SelectedEntityId, _sourceSession.CaptureRuntimeOperationsApprovalUx6Y());
    }

    private bool SourceMatchesUx6Y()
    {
        if (_sourceSnapshot is null) return true;
        var snapshot = _sourceSnapshot;
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.Dirty && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo && _sourceSession.ActiveAreaId == snapshot.ActiveArea &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.Selection) &&
               _sourceSession.SelectedEntityId == snapshot.ActiveSelection &&
               _sourceSession.CaptureRuntimeOperationsApprovalUx6Y() == snapshot.Approval;
    }

    private void ReleaseEvidenceUx6Y()
    {
        _evidenceSession?.Close();
        _evidenceSession = null;
        if (!string.IsNullOrWhiteSpace(_evidenceRoot))
            _evidenceReleased = StudioSessionController.DeleteTemporaryUx6Y(_evidenceRoot);
        else
            _evidenceReleased = true;
        _evidenceRoot = null;
        _session = _sourceSession;
    }

    private static ScrollContainer ScrollTabUx6Y(string title, out VBoxContainer body)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(body);
        return scroll;
    }

    private static GridContainer GridUx6Y(int columns) => new()
    {
        Columns = columns,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
    };

    private static VBoxContainer FieldUx6Y(string label, Control control, float minimumWidth = 0)
    {
        var field = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        if (minimumWidth > 0) field.CustomMinimumSize = new Vector2(minimumWidth, 0);
        field.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted, wrap: true));
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        field.AddChild(control);
        return field;
    }

    private static LineEdit IntegerUx6Y(string value)
    {
        var edit = StudioUx2EnvBControls.Text(value);
        edit.PlaceholderText = "Entier exact";
        return edit;
    }

    private static TextEdit TextAreaUx6Y(string value, float height) => new()
    {
        Text = value,
        CustomMinimumSize = new Vector2(0, height),
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        WrapMode = TextEdit.LineWrappingMode.None,
        ScrollFitContentHeight = false,
    };

    private static VBoxContainer ReviewColumnUx6Y(HBoxContainer parent, string title)
    {
        var panel = StudioStyle.Panel($"Ux6Y{title}", StudioStyle.SurfaceRaised, 7, 1);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.CustomMinimumSize = new Vector2(245, 0);
        var margin = StudioStyle.Margin(8, 6, 8, 6);
        panel.AddChild(margin);
        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(StudioStyle.Label(title, 9, StudioStyle.Accent));
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(body);
        root.AddChild(scroll);
        margin.AddChild(root);
        parent.AddChild(panel);
        return body;
    }

    private static void AddReviewUx6Y(VBoxContainer target, string text, Color? color = null) =>
        target.AddChild(StudioStyle.Label(text, 10, color ?? StudioStyle.Text, wrap: true));

    private static void ClearUx6Y(Node node)
    {
        foreach (var child in node.GetChildren()) child.QueueFree();
    }

    private static bool TryParseIntUx6Y(string value, out int parsed) =>
        int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ||
        int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out parsed);

    private static IReadOnlyList<string> SplitLinesUx6Y(string value) => (value ?? string.Empty)
        .Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n')
        .Select(item => item.Trim()).Where(item => item.Length > 0)
        .Distinct(StringComparer.Ordinal).ToArray();

    private static IEnumerable<string> DescendantTextUx6Y(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            switch (child)
            {
                case Label label:
                    yield return label.Text;
                    break;
                case Button button:
                    yield return button.Text;
                    break;
            }
            foreach (var nested in DescendantTextUx6Y(child)) yield return nested;
        }
    }

    private static string HumanWeatherKindUx6Y(WeatherKind kind) => kind switch
    {
        WeatherKind.Clear => "clair",
        WeatherKind.Overcast => "couvert",
        WeatherKind.Rain => "pluie",
        WeatherKind.Storm => "orage",
        WeatherKind.Snow => "neige",
        WeatherKind.Fog => "brume",
        _ => HumanUx6Y(kind.ToString()),
    };

    private static string HumanUx6Y(string value) => Regex.Replace(value ?? string.Empty,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b",
        "identifiant protégé").Replace('_', ' ').Trim();

    private static bool ContainsUuidUx6Y(string value) => Regex.IsMatch(value,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b");

    private sealed record SourceSnapshotUx6Y(ProjectDefinition? Project, ProjectCommandHistory? History,
        ProjectRevision? Revision, IReadOnlyList<CommandHistoryJournalEntry> Journal, bool Dirty, bool? CanUndo,
        bool? CanRedo, AreaId? ActiveArea, IReadOnlyList<EntityId> Selection,
        EntityId? ActiveSelection, StudioUx6YApprovalSnapshot Approval);
}
