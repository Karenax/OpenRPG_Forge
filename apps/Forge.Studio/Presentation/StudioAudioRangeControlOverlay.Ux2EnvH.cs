using Forge.Domain.Areas;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioAudioRangeControlOverlayUx2EnvH : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx2EnvHEvidenceSourceSnapshot? _evidenceSourceSnapshot;
    private string? _evidenceTempRoot;
    private bool _evidenceUsesRealModuleData;
    private bool _evidenceUsesCanonicalFallback;
    private bool _evidenceReleaseSucceeded = true;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _area;
    private readonly Label _selection;
    private readonly TabContainer _tabs;
    private readonly OptionButton _mode;
    private readonly LineEdit _innerRadius;
    private readonly LineEdit _outerRadius;
    private readonly LineEdit _airAbsorption;
    private readonly OptionButton _curve;
    private readonly CheckBox _debugSpheres;
    private readonly Label _rangePlan;
    private readonly CheckBox _initiallyActive;
    private readonly OptionButton _instancePolicy;
    private readonly OptionButton _authority;
    private readonly LineEdit _defaultFade;
    private readonly LineEdit _startedEvent;
    private readonly LineEdit _stoppedEvent;
    private readonly LineEdit _pausedEvent;
    private readonly LineEdit _fadedEvent;
    private readonly LineEdit _apiVersion;
    private readonly VBoxContainer _scriptCatalog;
    private readonly VBoxContainer _controlPlan;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    private IReadOnlyList<StudioUx2EnvHAreaChoice> _choices = [];
    private StudioUx2EnvHDraft? _draft;
    private StudioUx2EnvHPreview? _lastPreview;
    private bool _updating;
    private bool _applied;
    private bool _canonicalPreviewReady;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? AudioRangeChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _choices.Count;
    internal bool HasExplicitSelection => _draft is not null && _area.Selected > 0;
    internal bool CanonicalPreviewReady => _canonicalPreviewReady && _lastPreview is
    {
        Succeeded: true,
        CanonicalDryRun: true,
        ScriptContractAligned: true,
        AuthorityDiagnosticExercised: true,
        PlayerInstantiationSkipped: true,
        ScriptExecutionSkipped: true,
        RuntimeClaimsBounded: true,
    };
    internal bool ShowsRangePlan => _lastPreview is { Attenuation.Count: 13, DebugSpheres.Count: 2 } &&
                                    _rangePlan.Text.Contains("Absorption", StringComparison.Ordinal) &&
                                    _rangePlan.Text.Contains("Sphère intérieure", StringComparison.Ordinal) &&
                                    _rangePlan.Text.Contains("Sphère extérieure", StringComparison.Ordinal);
    internal bool ShowsScriptControlPlan => _lastPreview is { ScriptActions.Count: 5, ControlSteps.Count: >= 6 } &&
                                            _scriptCatalog.GetChildCount() == 5 &&
                                            _controlPlan.GetChildCount() >= 6;
    internal bool ShowsStructuredDiagnostics => _lastPreview?.Groups.Count == 3;
    internal bool ShowsBoundedRuntimeClaim => _lastPreview is
    {
        PlayerInstantiationSkipped: true,
        ScriptExecutionSkipped: true,
        RuntimeClaimsBounded: true,
    } && _lastPreview.Summary.Contains("aucun AudioStreamPlayer", StringComparison.Ordinal) &&
         _lastPreview.Summary.Contains("aucun script n’est exécuté", StringComparison.Ordinal);
    internal bool ShowsHumanSurface
    {
        get
        {
            var titles = Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle);
            var text = string.Join(' ', DescendantVisibleTextUx2EnvH(this).Concat(titles));
            return text.Contains("Portée audible", StringComparison.Ordinal) &&
                   text.Contains("Contrôle & scripts", StringComparison.Ordinal) &&
                   text.Contains("Aperçu & validation", StringComparison.Ordinal) &&
                   text.Contains("Rayon intérieur", StringComparison.Ordinal) &&
                   text.Contains("Démarrage par script", StringComparison.Ordinal) &&
                   text.Contains("Prévisualiser", StringComparison.Ordinal) &&
                   !ContainsUuidUx2EnvH(text);
        }
    }
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                             !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealModuleSource => UsesIsolatedEvidence && _evidenceUsesRealModuleData;
    internal bool UsesCanonicalFallbackEvidence => UsesIsolatedEvidence && _evidenceUsesCanonicalFallback;
    internal bool EvidenceSourceUntouched => SourceSnapshotMatchesUx2EnvH();
    internal bool EvidenceResourcesReleased => _evidenceReleaseSucceeded &&
                                                _evidenceSession is null && _evidenceTempRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);

    internal StudioAudioRangeControlOverlayUx2EnvH(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.014f, .019f, .026f, .97f);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _dialog = StudioStyle.Panel("Ux2EnvHAudioRangeControlDialog", StudioStyle.Surface, 12, 1);
        _dialog.ClipContents = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(14, 12, 14, 12);
        _dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        headingText.AddChild(StudioStyle.Label("Portée & contrôle audio", 18));
        headingText.AddChild(StudioStyle.Label(
            "Portée audible, atténuation, absorption de l’air et commandes ForgeScript · aperçu sans playback Studio.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        root.AddChild(heading);

        _area = StudioUx2EnvBControls.Options();
        _area.ItemSelected += SelectAreaUx2EnvH;
        root.AddChild(StudioUx2EnvBControls.Field("Zone à modifier · sélection explicite", _area, 500));
        _selection = StudioStyle.Label("Aucune zone sélectionnée.", 10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        var tabsHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 330),
            CustomMaximumSize = new Vector2(-1, 620),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            PropagateMaximumSize = true,
        };
        root.AddChild(tabsHost);
        _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        tabsHost.AddChild(_tabs);

        var rangeTab = ScrollTabUx2EnvH("Portée audible");
        _mode = StudioUx2EnvBControls.Options("Ambiance globale 2D", "Source spatiale 3D");
        _innerRadius = TextUx2EnvH("rayon intérieur en mètres");
        _outerRadius = TextUx2EnvH("rayon extérieur en mètres");
        _airAbsorption = TextUx2EnvH("0 à 1");
        _curve = StudioUx2EnvBControls.Options("Linéaire", "Inverse", "Inverse carrée");
        _debugSpheres = StudioUx2EnvBControls.Check("Afficher les deux sphères de diagnostic au runtime");
        var rangeGrid = GridUx2EnvH(3);
        rangeGrid.AddChild(StudioUx2EnvBControls.Field("Mode d’écoute", _mode));
        rangeGrid.AddChild(StudioUx2EnvBControls.Field("Rayon intérieur (m)", _innerRadius));
        rangeGrid.AddChild(StudioUx2EnvBControls.Field("Rayon extérieur (m)", _outerRadius));
        rangeGrid.AddChild(StudioUx2EnvBControls.Field("Absorption de l’air", _airAbsorption));
        rangeGrid.AddChild(StudioUx2EnvBControls.Field("Courbe d’atténuation", _curve));
        rangeGrid.AddChild(_debugSpheres);
        rangeTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvHRange", "Portée canonique",
            "Les nombres restent bruts jusqu’au dry-run : aucune valeur valide n’est arrondie ni plafonnée par l’interface.",
            rangeGrid));
        _rangePlan = StudioStyle.Label(
            "Aucun plan de portée · sélectionnez une zone puis prévisualisez.",
            10, StudioStyle.Muted, wrap: true);
        _rangePlan.CustomMinimumSize = new Vector2(0, 180);
        rangeTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvHRangePlan", "Aperçu des gains et sphères",
            "Treize distances sont évaluées par le service canonique; l’absorption de l’air est affichée séparément.",
            _rangePlan));
        rangeTab.AddChild(StudioStyle.Label(
            "Les sphères sont un plan de diagnostic. Seul le runtime Godot crée les MeshInstance3D correspondantes.",
            9, StudioStyle.Muted, wrap: true));

        var controlTab = ScrollTabUx2EnvH("Contrôle & scripts");
        _initiallyActive = StudioUx2EnvBControls.Check("Actif au chargement");
        _instancePolicy = StudioUx2EnvBControls.Options("Instance unique", "Redémarrer", "Chevauchement");
        _authority = StudioUx2EnvBControls.Options("Autorité locale", "Autorité serveur");
        _defaultFade = TextUx2EnvH("durée en secondes");
        var controlGrid = GridUx2EnvH(2);
        controlGrid.AddChild(_initiallyActive);
        controlGrid.AddChild(StudioUx2EnvBControls.Field("Politique d’instances", _instancePolicy));
        controlGrid.AddChild(StudioUx2EnvBControls.Field("Autorité", _authority));
        controlGrid.AddChild(StudioUx2EnvBControls.Field("Fondu par défaut (s)", _defaultFade));
        controlTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvHControl", "Démarrage par script et instances",
            "Désactivez « Actif au chargement » pour laisser Start/Stop/Pause/Resume/Fade piloter la lecture au runtime.",
            controlGrid));

        _startedEvent = TextUx2EnvH("audio.started");
        _stoppedEvent = TextUx2EnvH("audio.stopped");
        _pausedEvent = TextUx2EnvH("audio.paused");
        _fadedEvent = TextUx2EnvH("audio.faded");
        _apiVersion = TextUx2EnvH("1.0");
        var eventGrid = GridUx2EnvH(2);
        eventGrid.AddChild(StudioUx2EnvBControls.Field("Callback de démarrage", _startedEvent));
        eventGrid.AddChild(StudioUx2EnvBControls.Field("Callback d’arrêt", _stoppedEvent));
        eventGrid.AddChild(StudioUx2EnvBControls.Field("Callback de pause", _pausedEvent));
        eventGrid.AddChild(StudioUx2EnvBControls.Field("Callback de fondu", _fadedEvent));
        eventGrid.AddChild(StudioUx2EnvBControls.Field("Version API ForgeScript", _apiVersion));
        controlTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvHEvents", "Callbacks namespacés",
            "Les noms sont persistés tels quels et validés avant la commande; aucun handler n’est exécuté dans Studio.",
            eventGrid));

        _scriptCatalog = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _controlPlan = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        controlTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvHScriptCatalog", "API ForgeScript exposée",
            "Les cinq signatures proviennent du catalogue canonique ForgeScript.", _scriptCatalog));
        controlTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvHControlPlan", "Simulation du handle typé",
            "Le service d’état simule les transitions et l’autorité; aucun AudioStreamPlayer n’est créé.", _controlPlan));

        var reviewTab = ScrollTabUx2EnvH("Aperçu & validation");
        _diff = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _diagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        reviewTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvHDiff", "Modifications proposées",
            "Le dry-run canonique ne change ni projet, ni historique.", _diff));
        reviewTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvHDiagnostics", "Validation structurée",
            "Portée, contrôle et commande restent regroupés séparément.", _diagnostics));

        _summary = StudioStyle.Label(
            "Sélectionnez une zone puis prévisualisez · aucun changement appliqué.",
            10, StudioStyle.Muted, wrap: true);
        _summary.CustomMinimumSize = new Vector2(0, 62);
        root.AddChild(_summary);
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 6);
        _preview = StudioStyle.Button("Prévisualiser", compact: true);
        _apply = StudioStyle.Button("Appliquer", compact: true);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        {
            button.CustomMinimumSize = new Vector2(126, 34);
            actions.AddChild(button);
        }
        var actionNote = StudioStyle.Label(
            "Aperçu sec obligatoire · une seule commande set_area_environment est enregistrée.",
            9, StudioStyle.Muted, wrap: true);
        actionNote.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        actionNote.CustomMinimumSize = new Vector2(300, 34);
        actions.AddChild(actionNote);
        root.AddChild(actions);
        _preview.Pressed += PreviewUx2EnvH;
        _apply.Pressed += ApplyUx2EnvH;
        _undo.Pressed += UndoUx2EnvH;
        _redo.Pressed += RedoUx2EnvH;
        _save.Pressed += SaveUx2EnvH;
        _status = StudioStyle.Label("Prêt · sélection explicite requise", 9, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        WireDraftChangesUx2EnvH();
        SetEditingEnabledUx2EnvH(false);
        ResetReviewUx2EnvH();
        UpdateActionStateUx2EnvH();
    }

    internal void Open()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null)
            ReleaseIsolatedEvidenceSessionUx2EnvH();
        _session = _sourceSession;
        Visible = true;
        UpdateDialogBoundsUx2EnvH();
        RefreshChoicesUx2EnvH();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceTempRoot is null) return;
        if (Visible || !ReferenceEquals(_session, _sourceSession))
            _session.CancelAudioRangeApprovalUx2EnvH();
        Visible = false;
        _draft = null;
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _applied = false;
        ReleaseIsolatedEvidenceSessionUx2EnvH();
        _session = _sourceSession;
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        var approval = _sourceSession.CaptureAudioRangeApprovalUx2EnvH();
        _session = _sourceSession;
        Visible = true;
        UpdateDialogBoundsUx2EnvH();
        RefreshChoicesUx2EnvH();
        _sourceSession.RestoreAudioRangeApprovalUx2EnvH(approval);
    }

    internal bool PrepareEvidence(string mode)
    {
        var sourceApproval = _sourceSession.CaptureAudioRangeApprovalUx2EnvH();
        Close();
        _sourceSession.RestoreAudioRangeApprovalUx2EnvH(sourceApproval);
        _evidenceReleaseSucceeded = true;
        var sourceProject = _sourceSession.Project;
        if (sourceProject is null || _sourceSession.History is null || sourceProject.Areas.Count == 0) return false;
        _evidenceSourceSnapshot = CaptureSourceSnapshotUx2EnvH();
        var tempRoot = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux2-env-h-{Guid.NewGuid():N}");
        var evidenceSession = new StudioSessionController();
        try
        {
            var realArea = sourceProject.Areas.FirstOrDefault(area =>
                area.Environment.Audio.Spatial.Mode == AreaSoundMode.Spatial3D);
            _evidenceUsesRealModuleData = realArea is not null;
            _evidenceUsesCanonicalFallback = realArea is null;
            var evidenceProject = realArea is null
                ? StudioSessionController.CreateUx2EnvHContractProject(sourceProject)
                : sourceProject;
            var evidenceAreaId = realArea?.Id ?? evidenceProject.Areas[0].Id;
            var created = ProjectSession.Create(tempRoot, evidenceProject);
            if (!created.Succeeded || !evidenceSession.Open(tempRoot, evidenceAreaId.ToString()).Succeeded)
            {
                evidenceSession.Close();
                var released = StudioSessionController.DeleteUx2EnvHTemporaryWorkspace(tempRoot);
                ResetEvidenceStateUx2EnvH();
                _evidenceReleaseSucceeded = released;
                if (!released) _evidenceTempRoot = tempRoot;
                return false;
            }
            _evidenceSession = evidenceSession;
            _evidenceTempRoot = tempRoot;
            _session = evidenceSession;
            Visible = true;
            UpdateDialogBoundsUx2EnvH();
            RefreshChoicesUx2EnvH();
            var choiceIndex = _choices.ToList().FindIndex(choice => choice.AreaId == evidenceAreaId);
            if (choiceIndex < 0)
            {
                Close();
                return false;
            }
            _area.Select(choiceIndex + 1);
            LoadChoiceUx2EnvH(choiceIndex);
            if (!ConfigureEvidenceDraftUx2EnvH())
            {
                Close();
                return false;
            }
            PreviewUx2EnvH();
            _tabs.CurrentTab = 2;
            var ready = CanonicalPreviewReady && HasExplicitSelection && ShowsRangePlan &&
                        ShowsScriptControlPlan && ShowsStructuredDiagnostics && ShowsBoundedRuntimeClaim &&
                        ShowsHumanSurface && UsesIsolatedEvidence &&
                        (UsesRealModuleSource ^ UsesCanonicalFallbackEvidence) && EvidenceSourceUntouched;
            if (!ready)
            {
                Close();
                return false;
            }
            return true;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_H_EVIDENCE_FAILED mode={mode} " +
                        $"type={exception.GetType().Name} message={exception.Message}");
            var sessionReleased = true;
            try
            {
                evidenceSession.Close();
            }
            catch (Exception releaseException)
            {
                sessionReleased = false;
                GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_H_EVIDENCE_RELEASE_FAILED " +
                            $"type={releaseException.GetType().Name} message={releaseException.Message}");
            }
            var released = StudioSessionController.DeleteUx2EnvHTemporaryWorkspace(tempRoot);
            ResetEvidenceStateUx2EnvH();
            _evidenceReleaseSucceeded = sessionReleased && released;
            if (!released) _evidenceTempRoot = tempRoot;
            _session = _sourceSession;
            Visible = false;
            return false;
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        var sourceSnapshot = CaptureSourceSnapshotUx2EnvH();
        if (!PrepareEvidence("ux2-env-h-audio-range-control") ||
            !CanonicalPreviewReady || !UsesIsolatedEvidence)
        {
            Close();
            return false;
        }
        var isolated = _session;
        if (_lastPreview is not { Succeeded: true, CanApply: true })
        {
            Close();
            return false;
        }
        var isolatedProjectBefore = isolated.Project;
        ApplyUx2EnvH();
        var applied = _applied && isolated.Project is not null;
        var appliedJson = applied ? ProjectJsonSerializer.Serialize(isolated.Project!) : string.Empty;
        UndoUx2EnvH();
        var undone = !_applied && ReferenceEquals(isolatedProjectBefore, isolated.Project) &&
                     isolated.History?.CanRedo == true;
        RedoUx2EnvH();
        var redone = _applied && isolated.Project is not null &&
                     string.Equals(appliedJson, ProjectJsonSerializer.Serialize(isolated.Project!),
                         StringComparison.Ordinal);
        SaveUx2EnvH();
        var reopen = new StudioSessionController();
        var roundTrip = false;
        try
        {
            var opened = _evidenceTempRoot is not null && reopen.Open(_evidenceTempRoot).Succeeded;
            roundTrip = opened && reopen.Project is not null &&
                        string.Equals(appliedJson, ProjectJsonSerializer.Serialize(reopen.Project!),
                            StringComparison.Ordinal);
        }
        finally
        {
            reopen.Close();
        }
        Close();
        return applied && undone && redone && roundTrip && SourceSnapshotMatchesUx2EnvH(sourceSnapshot) &&
               EvidenceResourcesReleased && SourceSessionRestored;
    }

    private VBoxContainer ScrollTabUx2EnvH(string title)
    {
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(1080, 0),
        };
        content.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(content);
        _tabs.AddChild(scroll);
        _tabs.SetTabTitle(_tabs.GetTabCount() - 1, title);
        return content;
    }

    private void RefreshChoicesUx2EnvH()
    {
        _updating = true;
        _choices = _session.AudioRangeChoicesUx2EnvH;
        _area.Clear();
        _area.AddItem("Choisir une zone…");
        foreach (var choice in _choices) _area.AddItem(choice.Label);
        _area.Select(0);
        _updating = false;
        ClearSelectionUx2EnvH();
    }

    private void SelectAreaUx2EnvH(long index)
    {
        if (_updating) return;
        if (index <= 0 || index > _choices.Count)
        {
            ClearSelectionUx2EnvH();
            return;
        }
        LoadChoiceUx2EnvH((int)index - 1);
    }

    private void ClearSelectionUx2EnvH()
    {
        _draft = null;
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _applied = false;
        _selection.Text = "Aucune zone sélectionnée · aucune donnée chargée.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
        SetEditingEnabledUx2EnvH(false);
        ResetReviewUx2EnvH();
        _session.CancelAudioRangeApprovalUx2EnvH();
        UpdateActionStateUx2EnvH();
    }

    private void LoadChoiceUx2EnvH(int choiceIndex)
    {
        if (choiceIndex < 0 || choiceIndex >= _choices.Count)
        {
            ClearSelectionUx2EnvH();
            return;
        }
        _draft = _session.CreateAudioRangeDraftUx2EnvH(_choices[choiceIndex].AreaId);
        if (_draft is null)
        {
            ClearSelectionUx2EnvH();
            SetStatusUx2EnvH("La zone choisie n’est plus disponible.", error: true);
            return;
        }
        PopulateUx2EnvH(_draft);
        _selection.Text = $"Zone sélectionnée · {HumanLabelUx2EnvH(_draft.AreaDisplayName)} · " +
                          $"{_choices[choiceIndex].RangeSummary} · {_choices[choiceIndex].ControlSummary}";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Accent);
        SetEditingEnabledUx2EnvH(true);
        ResetReviewUx2EnvH();
        SetStatusUx2EnvH("Fiche chargée · prévisualisation sèche obligatoire");
        UpdateActionStateUx2EnvH();
    }

    private void PopulateUx2EnvH(StudioUx2EnvHDraft draft)
    {
        _updating = true;
        _mode.Select((int)draft.Mode);
        _innerRadius.Text = draft.InnerRadius;
        _outerRadius.Text = draft.OuterRadius;
        _airAbsorption.Text = draft.AirAbsorption;
        while (_curve.ItemCount > 3) _curve.RemoveItem(3);
        var curveIndex = CurveIndexUx2EnvH(draft.AttenuationCurve);
        if (curveIndex < 0)
        {
            _curve.AddItem($"Valeur existante à corriger · {HumanLabelUx2EnvH(draft.AttenuationCurve)}");
            curveIndex = 3;
        }
        _curve.Select(curveIndex);
        _debugSpheres.ButtonPressed = draft.DebugSpheresVisible;
        _initiallyActive.ButtonPressed = draft.InitiallyActive;
        _instancePolicy.Select((int)draft.InstancePolicy);
        _authority.Select((int)draft.Authority);
        _defaultFade.Text = draft.DefaultFadeSeconds;
        _startedEvent.Text = draft.StartedEvent;
        _stoppedEvent.Text = draft.StoppedEvent;
        _pausedEvent.Text = draft.PausedEvent;
        _fadedEvent.Text = draft.FadedEvent;
        _apiVersion.Text = draft.ForgeScriptApiVersion;
        _updating = false;
    }

    private StudioUx2EnvHDraft? CaptureDraftUx2EnvH()
    {
        if (_draft is null) return null;
        return _draft with
        {
            Mode = (AreaSoundMode)_mode.Selected,
            InnerRadius = _innerRadius.Text,
            OuterRadius = _outerRadius.Text,
            AirAbsorption = _airAbsorption.Text,
            AttenuationCurve = CurveValueUx2EnvH(_curve.Selected, _draft.AttenuationCurve),
            DebugSpheresVisible = _debugSpheres.ButtonPressed,
            InitiallyActive = _initiallyActive.ButtonPressed,
            InstancePolicy = (AudioInstancePolicy)_instancePolicy.Selected,
            Authority = (AudioAuthority)_authority.Selected,
            DefaultFadeSeconds = _defaultFade.Text,
            StartedEvent = _startedEvent.Text,
            StoppedEvent = _stoppedEvent.Text,
            PausedEvent = _pausedEvent.Text,
            FadedEvent = _fadedEvent.Text,
            ForgeScriptApiVersion = _apiVersion.Text,
        };
    }

    private void InvalidatePreviewUx2EnvH()
    {
        if (_updating) return;
        _canonicalPreviewReady = false;
        _lastPreview = null;
        _applied = false;
        _session.CancelAudioRangeApprovalUx2EnvH();
        ResetReviewUx2EnvH();
        SetStatusUx2EnvH("Réglages modifiés · relancez la prévisualisation");
        UpdateActionStateUx2EnvH();
    }

    private void PreviewUx2EnvH()
    {
        var draft = CaptureDraftUx2EnvH();
        if (draft is null)
        {
            SetStatusUx2EnvH("Sélectionnez explicitement une zone.", error: true);
            return;
        }
        _draft = draft;
        _lastPreview = _session.PreviewAudioRangeUx2EnvH(draft);
        _canonicalPreviewReady = _lastPreview.Succeeded && _lastPreview.CanonicalDryRun;
        _applied = false;
        RenderPreviewUx2EnvH(_lastPreview);
        SetStatusUx2EnvH(_lastPreview.Message, !_lastPreview.Succeeded);
        UpdateActionStateUx2EnvH();
    }

    private void ApplyUx2EnvH()
    {
        if (!_canonicalPreviewReady)
        {
            SetStatusUx2EnvH("Relancez l’aperçu sec avant d’appliquer.", error: true);
            return;
        }
        var result = _session.ApplyApprovedAudioRangeUx2EnvH();
        _canonicalPreviewReady = false;
        _applied = result.Succeeded;
        if (result.Succeeded) AudioRangeChanged?.Invoke();
        SetStatusUx2EnvH(result.Message, !result.Succeeded);
        UpdateActionStateUx2EnvH();
    }

    private void UndoUx2EnvH()
    {
        var result = _session.Undo();
        if (result.Succeeded) _applied = false;
        ReloadAfterHistoryUx2EnvH();
        SetStatusUx2EnvH(result.Message, !result.Succeeded);
    }

    private void RedoUx2EnvH()
    {
        var result = _session.Redo();
        if (result.Succeeded) _applied = true;
        ReloadAfterHistoryUx2EnvH();
        SetStatusUx2EnvH(result.Message, !result.Succeeded);
    }

    private void SaveUx2EnvH()
    {
        var result = _session.Save();
        UpdateActionStateUx2EnvH();
        SetStatusUx2EnvH(result.Message, !result.Succeeded);
    }

    private void ReloadAfterHistoryUx2EnvH()
    {
        if (_draft is not null)
        {
            _draft = _session.CreateAudioRangeDraftUx2EnvH(_draft.AreaId);
            if (_draft is not null) PopulateUx2EnvH(_draft);
        }
        _canonicalPreviewReady = false;
        _lastPreview = null;
        ResetReviewUx2EnvH();
        UpdateActionStateUx2EnvH();
    }

    private void RenderPreviewUx2EnvH(StudioUx2EnvHPreview preview)
    {
        ClearChildrenUx2EnvH(_diff);
        ClearChildrenUx2EnvH(_diagnostics);
        ClearChildrenUx2EnvH(_scriptCatalog);
        ClearChildrenUx2EnvH(_controlPlan);
        foreach (var entry in preview.Diff.Entries)
            _diff.AddChild(StudioStyle.Label($"• {entry}", 9, StudioStyle.Text, wrap: true));
        if (preview.Diff.Entries.Count == 0)
            _diff.AddChild(StudioStyle.Label("Aucune différence.", 9, StudioStyle.Muted, wrap: true));
        foreach (var group in preview.Groups)
        {
            var color = group.Diagnostics.Count == 0 ? StudioStyle.Accent : StudioStyle.Warning;
            _diagnostics.AddChild(StudioStyle.Label($"{group.Title} · {group.State}", 10, color, wrap: true));
            foreach (var diagnostic in group.Diagnostics)
                _diagnostics.AddChild(StudioStyle.Label($"  • {diagnostic.Message}", 9, StudioStyle.Warning, wrap: true));
        }
        foreach (var action in preview.ScriptActions)
            _scriptCatalog.AddChild(StudioStyle.Label(
                $"{action.Name} · {action.Signature}\n{action.Summary} · disponibilité {action.Availability}",
                9, StudioStyle.Text, wrap: true));
        foreach (var step in preview.ControlSteps)
            _controlPlan.AddChild(StudioStyle.Label(
                $"{step.Operation} · {step.Status} · gain {step.Gain} · instance(s) {step.ActiveInstances} · " +
                $"handle génération {step.HandleGeneration}\n{step.Event} · {step.Diagnostic}",
                9, StudioStyle.Text, wrap: true));
        _rangePlan.Text = preview.Attenuation.Count == 0
            ? "Aucun plan de portée disponible."
            : "Distance · gain de courbe · gain après Absorption de l’air\n" +
              string.Join("   ", preview.Attenuation.Select(item =>
                  $"{item.Distance}: {item.CurveGain} → {item.GainAfterAirAbsorption}")) + "\n" +
              (preview.DebugSpheres.Count == 0
                  ? "Sphères de diagnostic masquées."
                  : string.Join(" · ", preview.DebugSpheres.Select(item => $"{item.Kind} {item.Radius}")));
        _summary.Text = string.IsNullOrWhiteSpace(preview.Summary) ? preview.Message : preview.Summary;
    }

    private void ResetReviewUx2EnvH()
    {
        ClearChildrenUx2EnvH(_diff);
        ClearChildrenUx2EnvH(_diagnostics);
        ClearChildrenUx2EnvH(_scriptCatalog);
        ClearChildrenUx2EnvH(_controlPlan);
        _diff.AddChild(StudioStyle.Label("Aucun aperçu calculé.", 9, StudioStyle.Muted, wrap: true));
        _diagnostics.AddChild(StudioStyle.Label("Validation en attente.", 9, StudioStyle.Muted, wrap: true));
        _scriptCatalog.AddChild(StudioStyle.Label("Catalogue en attente du dry-run.", 9, StudioStyle.Muted, wrap: true));
        _controlPlan.AddChild(StudioStyle.Label("Simulation en attente du dry-run.", 9, StudioStyle.Muted, wrap: true));
        _rangePlan.Text = "Aucun plan de portée · sélectionnez une zone puis prévisualisez.";
        _summary.Text = "Sélectionnez une zone puis prévisualisez · aucun changement appliqué.";
    }

    private void UpdateActionStateUx2EnvH()
    {
        var selected = _draft is not null;
        _preview.Disabled = !selected;
        _apply.Disabled = !_canonicalPreviewReady || _lastPreview?.CanApply != true;
        _undo.Disabled = _session.History?.CanUndo != true;
        _redo.Disabled = _session.History?.CanRedo != true;
        _save.Disabled = !_session.IsDirty;
    }

    private void SetEditingEnabledUx2EnvH(bool enabled)
    {
        foreach (var edit in DraftLineEditsUx2EnvH()) edit.Editable = enabled;
        foreach (var option in new[] { _mode, _curve, _instancePolicy, _authority }) option.Disabled = !enabled;
        _debugSpheres.Disabled = !enabled;
        _initiallyActive.Disabled = !enabled;
    }

    private bool ConfigureEvidenceDraftUx2EnvH()
    {
        var current = CaptureDraftUx2EnvH();
        if (current is null) return false;
        var outer = current.OuterRadius.Trim() == "47.5" ? "49.25" : "47.5";
        var draft = current with
        {
            Mode = AreaSoundMode.Spatial3D,
            InnerRadius = "3.25",
            OuterRadius = outer,
            AirAbsorption = "0.18",
            AttenuationCurve = "inverse-square",
            DebugSpheresVisible = true,
            InitiallyActive = false,
            InstancePolicy = AudioInstancePolicy.Restart,
            Authority = AudioAuthority.Server,
            DefaultFadeSeconds = "1.75",
            StartedEvent = "audio.studio_started",
            StoppedEvent = "audio.studio_stopped",
            PausedEvent = "audio.studio_paused",
            FadedEvent = "audio.studio_faded",
            ForgeScriptApiVersion = "1.0",
        };
        _draft = draft;
        PopulateUx2EnvH(draft);
        return true;
    }

    private StudioUx2EnvHEvidenceSourceSnapshot CaptureSourceSnapshotUx2EnvH()
    {
        var history = _sourceSession.History ??
                      throw new InvalidOperationException("The source history is unavailable.");
        var project = _sourceSession.Project ??
                      throw new InvalidOperationException("The source project is unavailable.");
        return new StudioUx2EnvHEvidenceSourceSnapshot(
            project,
            history,
            history.State.Revision,
            history.Journal.ToArray(),
            _sourceSession.IsDirty,
            history.CanUndo,
            history.CanRedo,
            _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(),
            _sourceSession.SelectedEntityId,
            _sourceSession.CaptureAudioRangeApprovalUx2EnvH());
    }

    private bool SourceSnapshotMatchesUx2EnvH(StudioUx2EnvHEvidenceSourceSnapshot? expected = null)
    {
        var snapshot = expected ?? _evidenceSourceSnapshot;
        var history = _sourceSession.History;
        if (snapshot is null || history is null) return false;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) &&
               history.State.Revision == snapshot.Revision &&
               history.Journal.SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.IsDirty &&
               history.CanUndo == snapshot.CanUndo && history.CanRedo == snapshot.CanRedo &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureAudioRangeApprovalUx2EnvH() == snapshot.Approval;
    }

    private bool ReleaseIsolatedEvidenceSessionUx2EnvH()
    {
        var evidence = _evidenceSession;
        var tempRoot = _evidenceTempRoot;
        var sessionReleased = true;
        if (evidence is not null)
        {
            try
            {
                evidence.Close();
            }
            catch (Exception exception)
            {
                sessionReleased = false;
                GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_H_RELEASE_FAILED " +
                            $"type={exception.GetType().Name} message={exception.Message}");
            }
        }
        var rootReleased = StudioSessionController.DeleteUx2EnvHTemporaryWorkspace(tempRoot);
        ResetEvidenceStateUx2EnvH();
        _session = _sourceSession;
        _evidenceReleaseSucceeded = _evidenceReleaseSucceeded && sessionReleased && rootReleased;
        if (!rootReleased) _evidenceTempRoot = tempRoot;
        return sessionReleased && rootReleased;
    }

    private void ResetEvidenceStateUx2EnvH()
    {
        _evidenceSession = null;
        _evidenceTempRoot = null;
        _evidenceUsesRealModuleData = false;
        _evidenceUsesCanonicalFallback = false;
        _evidenceSourceSnapshot = null;
    }

    private void WireDraftChangesUx2EnvH()
    {
        foreach (var edit in DraftLineEditsUx2EnvH()) edit.TextChanged += _ => InvalidatePreviewUx2EnvH();
        foreach (var option in new[] { _mode, _curve, _instancePolicy, _authority })
            option.ItemSelected += _ => InvalidatePreviewUx2EnvH();
        _debugSpheres.Toggled += _ => InvalidatePreviewUx2EnvH();
        _initiallyActive.Toggled += _ => InvalidatePreviewUx2EnvH();
    }

    private IReadOnlyList<LineEdit> DraftLineEditsUx2EnvH() =>
        [_innerRadius, _outerRadius, _airAbsorption, _defaultFade, _startedEvent,
            _stoppedEvent, _pausedEvent, _fadedEvent, _apiVersion];

    private void SetStatusUx2EnvH(string text, bool error = false)
    {
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(text, error);
    }

    private void UpdateDialogBoundsUx2EnvH()
    {
        var viewport = GetViewportRect().Size;
        var margin = viewport.X >= 2400 ? 70f : 28f;
        _dialog.Position = new Vector2(margin, margin);
        _dialog.Size = new Vector2(Math.Max(1180, viewport.X - margin * 2),
            Math.Max(720, viewport.Y - margin * 2));
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && Visible) UpdateDialogBoundsUx2EnvH();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey { Pressed: true } key) return;
        if (key.Keycode == Key.Escape) Close();
        if (key.CtrlPressed && key.Keycode == Key.Enter) PreviewUx2EnvH();
    }

    public override void _ExitTree()
    {
        ReleaseIsolatedEvidenceSessionUx2EnvH();
    }

    private static LineEdit TextUx2EnvH(string placeholder) => StudioUx2EnvBControls.Text(placeholder);

    private static GridContainer GridUx2EnvH(int columns)
    {
        var grid = new GridContainer { Columns = columns, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 6);
        return grid;
    }

    private static int CurveIndexUx2EnvH(string? curve) => curve?.Trim().ToLowerInvariant() switch
    {
        "linear" => 0,
        "inverse" => 1,
        "inverse-square" => 2,
        _ => -1,
    };

    private static string CurveValueUx2EnvH(int index, string source) => index switch
    {
        1 => "inverse",
        2 => "inverse-square",
        3 => source,
        _ => "linear",
    };

    private static string HumanLabelUx2EnvH(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(text) || ContainsUuidUx2EnvH(text) ? "Zone sélectionnée" : text;
    }

    private static void ClearChildrenUx2EnvH(Node node)
    {
        foreach (var child in node.GetChildren()) child.Free();
    }

    private static IEnumerable<string> DescendantVisibleTextUx2EnvH(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            switch (child)
            {
                case Label label when !string.IsNullOrWhiteSpace(label.Text):
                    yield return label.Text;
                    break;
                case Button button when !string.IsNullOrWhiteSpace(button.Text):
                    yield return button.Text;
                    break;
                case LineEdit edit when !string.IsNullOrWhiteSpace(edit.Text):
                    yield return edit.Text;
                    break;
                case OptionButton option:
                    for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
                    break;
                case ItemList list:
                    for (var index = 0; index < list.ItemCount; index++) yield return list.GetItemText(index);
                    break;
            }
            foreach (var descendant in DescendantVisibleTextUx2EnvH(child)) yield return descendant;
        }
    }

    private static bool ContainsUuidUx2EnvH(string text) => System.Text.RegularExpressions.Regex.IsMatch(
        text, "(?i)(?:[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}|[0-9a-f]{32})");
}
