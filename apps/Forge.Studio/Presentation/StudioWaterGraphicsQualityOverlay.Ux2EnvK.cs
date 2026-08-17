using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWaterGraphicsQualityOverlayUx2EnvK : ColorRect
{
    private static readonly string[] WaterQualityValues = ["low", "medium", "high"];
    private static readonly string[] AntiAliasingValues = ["disabled", "fxaa", "msaa2", "msaa4", "msaa8"];
    private static readonly int[] AnisotropyValues = [1, 2, 4, 8, 16];
    private static readonly string[] UpscalingValues = ["disabled", "fsr", "native"];

    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx2EnvKEvidenceSourceSnapshot? _evidenceSourceSnapshot;
    private string? _evidenceTempRoot;
    private bool _evidenceUsesRealModuleData;
    private bool _evidenceReleaseSucceeded = true;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _profile;
    private readonly Label _selection;
    private readonly TabContainer _tabs;
    private readonly CheckBox _refraction;
    private readonly CheckBox _reflections;
    private readonly CheckBox _depthColor;
    private readonly OptionButton _fallbackQuality;
    private readonly OptionButton _antiAliasing;
    private readonly OptionButton _anisotropy;
    private readonly OptionButton _upscaling;
    private readonly CheckBox _capabilityFallback;
    private readonly VBoxContainer _waterPlans;
    private readonly VBoxContainer _graphicsPlans;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    private IReadOnlyList<StudioUx2EnvKProfileChoice> _choices = [];
    private StudioUx2EnvKDraft? _draft;
    private StudioUx2EnvKPreview? _lastPreview;
    private bool _updating;
    private bool _applied;
    private bool _canonicalPreviewReady;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? WaterGraphicsChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _choices.Count;
    internal bool HasExplicitSelection => _draft is not null && _profile.Selected > 0;
    internal bool CanonicalPreviewReady => _canonicalPreviewReady && _lastPreview is
    {
        Succeeded: true,
        CanonicalDryRun: true,
        AreaWaterReadOnly: true,
        RendererMutationSkipped: true,
        HardwareDetectionSkipped: true,
        RuntimeClaimsBounded: true,
    };
    internal bool ShowsWaterPlan => _lastPreview is { WaterPlans.Count: > 0 } &&
                                    _waterPlans.GetChildCount() == _lastPreview.WaterPlans.Count;
    internal bool ShowsGraphicsPlan => _lastPreview is { GraphicsPlans.Count: 4 } &&
                                       _graphicsPlans.GetChildCount() == 4;
    internal bool ShowsStructuredDiagnostics => _lastPreview?.Groups.Count == 4 &&
                                                _diagnostics.GetChildCount() == 4;
    internal bool ShowsBoundedRuntimeClaim => _lastPreview is
    {
        RendererMutationSkipped: true,
        HardwareDetectionSkipped: true,
        RuntimeClaimsBounded: true,
    } && _lastPreview.Summary.Contains("aucun matériau", StringComparison.Ordinal) &&
         _lastPreview.Summary.Contains("aucun GPU", StringComparison.Ordinal);
    internal bool ShowsHumanSurface
    {
        get
        {
            var titles = Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle);
            var text = string.Join(' ', DescendantVisibleTextUx2EnvK(this).Concat(titles));
            return text.Contains("Qualité de l’eau", StringComparison.Ordinal) &&
                   text.Contains("Qualité graphique", StringComparison.Ordinal) &&
                   text.Contains("Plans bornés", StringComparison.Ordinal) &&
                   text.Contains("Aperçu & validation", StringComparison.Ordinal) &&
                   text.Contains("Prévisualiser", StringComparison.Ordinal) &&
                   text.Contains("Appliquer", StringComparison.Ordinal) &&
                   !ContainsUuidUx2EnvK(text);
        }
    }
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                             !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealModuleSource => UsesIsolatedEvidence && _evidenceUsesRealModuleData;
    internal bool EvidenceSourceUntouched => SourceSnapshotMatchesUx2EnvK();
    internal bool EvidenceResourcesReleased => _evidenceReleaseSucceeded &&
                                                _evidenceSession is null && _evidenceTempRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);

    internal StudioWaterGraphicsQualityOverlayUx2EnvK(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.014f, .019f, .026f, .97f);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _dialog = StudioStyle.Panel("Ux2EnvKWaterGraphicsQualityDialog", StudioStyle.Surface, 12, 1);
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
        headingText.AddChild(StudioStyle.Label("Qualité eau & image", 18));
        headingText.AddChild(StudioStyle.Label(
            "Politique Creator persistée et plans lisibles · aucun changement live du renderer dans Studio.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        root.AddChild(heading);

        _profile = StudioUx2EnvBControls.Options();
        _profile.ItemSelected += SelectProfileUx2EnvK;
        root.AddChild(StudioUx2EnvBControls.Field("Profil module · sélection explicite", _profile, 560));
        _selection = StudioStyle.Label("Aucun profil sélectionné.", 10, StudioStyle.Warning, wrap: true);
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

        var waterTab = ScrollTabUx2EnvK("Qualité de l’eau");
        _refraction = StudioUx2EnvBControls.Check("Réfraction de surface");
        _reflections = StudioUx2EnvBControls.Check("Réflexions d’espace écran demandées");
        _depthColor = StudioUx2EnvBControls.Check("Couleur selon la profondeur");
        _fallbackQuality = OptionsUx2EnvK(
            StudioSessionController.HumanWaterQualityUx2EnvK,
            WaterQualityValues);
        var waterFields = GridUx2EnvK(2);
        waterFields.AddChild(_refraction);
        waterFields.AddChild(_reflections);
        waterFields.AddChild(_depthColor);
        waterFields.AddChild(StudioUx2EnvBControls.Field("Qualité de repli", _fallbackQuality));
        waterTab.AddChild(StudioUx2EnvBControls.Card(
            "Ux2EnvKWaterPolicy",
            "Politique globale d’eau",
            "Cette politique complète les masses d’eau de zone sans modifier leurs volumes, matériaux, flux ou flottabilité.",
            waterFields));
        waterTab.AddChild(StudioStyle.Label(
            "Les surfaces réelles restent définies dans Climat & eau. Une liste vide est honnêtement signalée.",
            9, StudioStyle.Muted, wrap: true));

        var graphicsTab = ScrollTabUx2EnvK("Qualité graphique");
        _antiAliasing = OptionsUx2EnvK(
            StudioSessionController.HumanAntiAliasingUx2EnvK,
            AntiAliasingValues);
        _anisotropy = StudioUx2EnvBControls.Options();
        foreach (var value in AnisotropyValues) _anisotropy.AddItem($"{value}×");
        _upscaling = OptionsUx2EnvK(
            StudioSessionController.HumanUpscalingUx2EnvK,
            UpscalingValues);
        _capabilityFallback = StudioUx2EnvBControls.Check("Autoriser un repli selon les capacités");
        var graphicsFields = GridUx2EnvK(2);
        graphicsFields.AddChild(StudioUx2EnvBControls.Field("Anticrénelage", _antiAliasing));
        graphicsFields.AddChild(StudioUx2EnvBControls.Field("Filtrage anisotrope", _anisotropy));
        graphicsFields.AddChild(StudioUx2EnvBControls.Field("Mise à l’échelle", _upscaling));
        graphicsFields.AddChild(_capabilityFallback);
        graphicsTab.AddChild(StudioUx2EnvBControls.Card(
            "Ux2EnvKGraphicsPolicy",
            "Profil graphique demandé",
            "Le profil est canonique. La plateforme et l’adaptateur Godot restent autorités pour l’application réelle.",
            graphicsFields));
        graphicsTab.AddChild(StudioStyle.Label(
            "Aucune sonde GPU n’est lancée et aucun viewport n’est reconfiguré par cet aperçu.",
            9, StudioStyle.Muted, wrap: true));

        var plansTab = ScrollTabUx2EnvK("Plans bornés");
        _waterPlans = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _graphicsPlans = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        plansTab.AddChild(StudioUx2EnvBControls.Card(
            "Ux2EnvKWaterPlans",
            "Inventaire eau de zone · lecture seule",
            "Le plan canonique expose qualité, matériau, effets de surface, flottabilité et navigation sans instancier Godot.",
            _waterPlans));
        plansTab.AddChild(StudioUx2EnvBControls.Card(
            "Ux2EnvKGraphicsPlans",
            "Décision de qualité",
            "Demandes et repli sont distingués de l’état runtime réellement appliqué.",
            _graphicsPlans));

        var reviewTab = ScrollTabUx2EnvK("Aperçu & validation");
        _diff = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _diagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        reviewTab.AddChild(StudioUx2EnvBControls.Card(
            "Ux2EnvKDiff",
            "Modifications proposées",
            "Le dry-run canonique ne change ni projet, ni historique.",
            _diff));
        reviewTab.AddChild(StudioUx2EnvBControls.Card(
            "Ux2EnvKDiagnostics",
            "Validation structurée",
            "Sélection, eau, graphismes et commande restent séparés.",
            _diagnostics));

        _summary = StudioStyle.Label(
            "Sélectionnez le profil puis prévisualisez · aucun changement appliqué.",
            10, StudioStyle.Muted, wrap: true);
        _summary.CustomMinimumSize = new Vector2(0, 64);
        root.AddChild(_summary);
        root.AddChild(StudioStyle.Label(
            "Aperçu sec obligatoire · l’application produit une seule commande canonique annulable.",
            9, StudioStyle.Muted, wrap: true));
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 6);
        _preview = StudioStyle.Button("Prévisualiser", compact: true);
        _apply = StudioStyle.Button("Appliquer", compact: true);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        {
            button.CustomMinimumSize = new Vector2(132, 34);
            actions.AddChild(button);
        }
        root.AddChild(actions);
        _preview.Pressed += PreviewUx2EnvK;
        _apply.Pressed += ApplyUx2EnvK;
        _undo.Pressed += UndoUx2EnvK;
        _redo.Pressed += RedoUx2EnvK;
        _save.Pressed += SaveUx2EnvK;
        _status = StudioStyle.Label("Prêt · sélection explicite requise", 9, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        WireDraftChangesUx2EnvK();
        SetEditingEnabledUx2EnvK(false);
        ResetReviewUx2EnvK();
        UpdateActionStateUx2EnvK();
    }

    internal void Open()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null)
            ReleaseIsolatedEvidenceSessionUx2EnvK();
        _session = _sourceSession;
        Visible = true;
        UpdateDialogBoundsUx2EnvK();
        RefreshChoicesUx2EnvK();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceTempRoot is null) return;
        if (Visible || !ReferenceEquals(_session, _sourceSession))
            _session.CancelWaterGraphicsApprovalUx2EnvK();
        Visible = false;
        _draft = null;
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _applied = false;
        ReleaseIsolatedEvidenceSessionUx2EnvK();
        _session = _sourceSession;
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        var approval = _sourceSession.CaptureWaterGraphicsApprovalUx2EnvK();
        _session = _sourceSession;
        Visible = true;
        UpdateDialogBoundsUx2EnvK();
        RefreshChoicesUx2EnvK();
        _sourceSession.RestoreWaterGraphicsApprovalUx2EnvK(approval);
    }

    internal bool PrepareEvidence(string mode)
    {
        var sourceApproval = _sourceSession.CaptureWaterGraphicsApprovalUx2EnvK();
        Close();
        _sourceSession.RestoreWaterGraphicsApprovalUx2EnvK(sourceApproval);
        _evidenceReleaseSucceeded = true;
        var sourceProject = _sourceSession.Project;
        if (sourceProject is null || _sourceSession.History is null) return false;
        _evidenceSourceSnapshot = CaptureSourceSnapshotUx2EnvK();
        var tempRoot = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux2-env-k-{Guid.NewGuid():N}");
        var evidenceSession = new StudioSessionController();
        try
        {
            var created = ProjectSession.Create(tempRoot, sourceProject);
            if (!created.Succeeded || !evidenceSession.Open(tempRoot).Succeeded)
            {
                evidenceSession.Close();
                var released = StudioSessionController.DeleteUx2EnvKTemporaryWorkspace(tempRoot);
                ResetEvidenceStateUx2EnvK();
                _evidenceReleaseSucceeded = released;
                if (!released) _evidenceTempRoot = tempRoot;
                return false;
            }
            _evidenceSession = evidenceSession;
            _evidenceTempRoot = tempRoot;
            _evidenceUsesRealModuleData = true;
            _session = evidenceSession;
            Visible = true;
            UpdateDialogBoundsUx2EnvK();
            RefreshChoicesUx2EnvK();
            if (_choices.Count != 1)
            {
                Close();
                return false;
            }
            _profile.Select(1);
            LoadProfileUx2EnvK();
            ConfigureEvidenceDraftUx2EnvK();
            PreviewUx2EnvK();
            _tabs.CurrentTab = 2;
            var ready = CanonicalPreviewReady && HasExplicitSelection && ShowsWaterPlan && ShowsGraphicsPlan &&
                        ShowsStructuredDiagnostics && ShowsBoundedRuntimeClaim && ShowsHumanSurface &&
                        UsesIsolatedEvidence && UsesRealModuleSource && EvidenceSourceUntouched;
            if (!ready)
            {
                Close();
                return false;
            }
            return true;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_K_EVIDENCE_FAILED mode={mode} " +
                        $"type={exception.GetType().Name} message={exception.Message}");
            var sessionReleased = true;
            try
            {
                evidenceSession.Close();
            }
            catch (Exception releaseException)
            {
                sessionReleased = false;
                GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_K_EVIDENCE_RELEASE_FAILED " +
                            $"type={releaseException.GetType().Name} message={releaseException.Message}");
            }
            var released = StudioSessionController.DeleteUx2EnvKTemporaryWorkspace(tempRoot);
            ResetEvidenceStateUx2EnvK();
            _evidenceReleaseSucceeded = sessionReleased && released;
            if (!released) _evidenceTempRoot = tempRoot;
            _session = _sourceSession;
            Visible = false;
            return false;
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        var sourceSnapshot = CaptureSourceSnapshotUx2EnvK();
        if (!PrepareEvidence("ux2-env-k-water-graphics") || !CanonicalPreviewReady || !UsesIsolatedEvidence)
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
        ApplyUx2EnvK();
        var applied = _applied && isolated.Project is not null;
        var appliedJson = applied ? ProjectJsonSerializer.Serialize(isolated.Project!) : string.Empty;
        UndoUx2EnvK();
        var undone = !_applied && ReferenceEquals(isolatedProjectBefore, isolated.Project) &&
                     isolated.History?.CanRedo == true;
        RedoUx2EnvK();
        var redone = _applied && isolated.Project is not null &&
                     string.Equals(appliedJson, ProjectJsonSerializer.Serialize(isolated.Project!),
                         StringComparison.Ordinal);
        SaveUx2EnvK();
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
        return applied && undone && redone && roundTrip && SourceSnapshotMatchesUx2EnvK(sourceSnapshot) &&
               EvidenceResourcesReleased && SourceSessionRestored;
    }

    private void RefreshChoicesUx2EnvK()
    {
        _updating = true;
        _choices = _session.WaterGraphicsChoicesUx2EnvK;
        _profile.Clear();
        _profile.AddItem("Choisir le profil qualité…");
        foreach (var choice in _choices) _profile.AddItem(choice.Label);
        _profile.Select(0);
        _updating = false;
        ClearSelectionUx2EnvK();
    }

    private void SelectProfileUx2EnvK(long index)
    {
        if (_updating) return;
        if (index <= 0 || index > _choices.Count)
        {
            ClearSelectionUx2EnvK();
            return;
        }
        LoadProfileUx2EnvK();
    }

    private void LoadProfileUx2EnvK()
    {
        _draft = _session.CreateWaterGraphicsDraftUx2EnvK(explicitlySelected: true);
        if (_draft is null)
        {
            ClearSelectionUx2EnvK();
            return;
        }
        PopulateDraftUx2EnvK(_draft);
        _selection.Text = _choices[0].WaterBodyCount == 0
            ? "Profil sélectionné · aucune masse d’eau dans les zones; l’état vide est conservé."
            : $"Profil sélectionné · {_choices[0].WaterBodyCount} masse(s) d’eau seront inventoriées en lecture seule.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Success);
        SetEditingEnabledUx2EnvK(true);
        InvalidatePreviewUx2EnvK();
    }

    private void ClearSelectionUx2EnvK()
    {
        _session.CancelWaterGraphicsApprovalUx2EnvK();
        _draft = null;
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _applied = false;
        _selection.Text = "Aucun profil sélectionné.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
        SetEditingEnabledUx2EnvK(false);
        ResetReviewUx2EnvK();
        UpdateActionStateUx2EnvK();
    }

    private void PopulateDraftUx2EnvK(StudioUx2EnvKDraft draft)
    {
        _updating = true;
        _refraction.ButtonPressed = draft.Refraction;
        _reflections.ButtonPressed = draft.ScreenSpaceReflections;
        _depthColor.ButtonPressed = draft.DepthColor;
        SelectValueUx2EnvK(_fallbackQuality, WaterQualityValues, draft.FallbackQuality);
        SelectValueUx2EnvK(_antiAliasing, AntiAliasingValues, draft.AntiAliasing);
        _anisotropy.Select(Math.Max(0, Array.IndexOf(AnisotropyValues, draft.AnisotropicLevel)));
        SelectValueUx2EnvK(_upscaling, UpscalingValues, draft.Upscaling);
        _capabilityFallback.ButtonPressed = draft.CapabilityFallback;
        _updating = false;
    }

    private StudioUx2EnvKDraft ReadDraftUx2EnvK()
    {
        var source = _draft ?? throw new InvalidOperationException("No quality profile is selected.");
        return source with
        {
            Refraction = _refraction.ButtonPressed,
            ScreenSpaceReflections = _reflections.ButtonPressed,
            DepthColor = _depthColor.ButtonPressed,
            FallbackQuality = WaterQualityValues[Math.Clamp(_fallbackQuality.Selected, 0,
                WaterQualityValues.Length - 1)],
            AntiAliasing = AntiAliasingValues[Math.Clamp(_antiAliasing.Selected, 0,
                AntiAliasingValues.Length - 1)],
            AnisotropicLevel = AnisotropyValues[Math.Clamp(_anisotropy.Selected, 0,
                AnisotropyValues.Length - 1)],
            Upscaling = UpscalingValues[Math.Clamp(_upscaling.Selected, 0, UpscalingValues.Length - 1)],
            CapabilityFallback = _capabilityFallback.ButtonPressed,
        };
    }

    private void PreviewUx2EnvK()
    {
        if (_draft is null)
        {
            SetStatusUx2EnvK("Sélectionnez explicitement le profil avant l’aperçu.", error: true);
            return;
        }
        _draft = ReadDraftUx2EnvK();
        _lastPreview = _session.PreviewWaterGraphicsUx2EnvK(_draft);
        _canonicalPreviewReady = _lastPreview.Succeeded && _lastPreview.CanonicalDryRun;
        RenderPreviewUx2EnvK(_lastPreview);
        SetStatusUx2EnvK(_lastPreview.Message, !_lastPreview.Succeeded);
        UpdateActionStateUx2EnvK();
    }

    private void ApplyUx2EnvK()
    {
        if (!_canonicalPreviewReady || _lastPreview is not { Succeeded: true, CanApply: true })
        {
            SetStatusUx2EnvK("Prévisualisez une modification valide avant de l’appliquer.", error: true);
            return;
        }
        var result = _session.ApplyApprovedWaterGraphicsUx2EnvK();
        _applied = result.Succeeded;
        _canonicalPreviewReady = false;
        SetStatusUx2EnvK(result.Message, !result.Succeeded);
        if (result.Succeeded) WaterGraphicsChanged?.Invoke();
        UpdateActionStateUx2EnvK();
    }

    private void UndoUx2EnvK()
    {
        var result = _session.Undo();
        _applied = false;
        if (result.Succeeded) ReloadSelectedProfileUx2EnvK();
        SetStatusUx2EnvK(result.Succeeded ? "Modification annulée." : "Aucune modification à annuler.",
            !result.Succeeded);
        if (result.Succeeded) WaterGraphicsChanged?.Invoke();
        UpdateActionStateUx2EnvK();
    }

    private void RedoUx2EnvK()
    {
        var result = _session.Redo();
        _applied = result.Succeeded;
        if (result.Succeeded) ReloadSelectedProfileUx2EnvK();
        SetStatusUx2EnvK(result.Succeeded ? "Modification rétablie." : "Aucune modification à rétablir.",
            !result.Succeeded);
        if (result.Succeeded) WaterGraphicsChanged?.Invoke();
        UpdateActionStateUx2EnvK();
    }

    private void SaveUx2EnvK()
    {
        var result = _session.Save();
        SetStatusUx2EnvK(result.Succeeded ? "Projet enregistré." : "L’enregistrement a échoué.", !result.Succeeded);
        UpdateActionStateUx2EnvK();
    }

    private void ReloadSelectedProfileUx2EnvK()
    {
        _draft = _session.CreateWaterGraphicsDraftUx2EnvK(explicitlySelected: true);
        if (_draft is not null) PopulateDraftUx2EnvK(_draft);
        _lastPreview = null;
        _canonicalPreviewReady = false;
        ResetReviewUx2EnvK();
    }

    private void RenderPreviewUx2EnvK(StudioUx2EnvKPreview preview)
    {
        ClearChildrenUx2EnvK(_waterPlans);
        foreach (var plan in preview.WaterPlans)
        {
            _waterPlans.AddChild(StudioStyle.Label(
                $"{plan.Area} · {plan.WaterBody} · qualité {plan.Quality}\n" +
                $"{plan.Material} · {plan.SurfaceEffects}\n{plan.Physics}",
                10, StudioStyle.Text, wrap: true));
        }
        ClearChildrenUx2EnvK(_graphicsPlans);
        foreach (var plan in preview.GraphicsPlans)
        {
            _graphicsPlans.AddChild(StudioStyle.Label(
                $"{plan.Setting} · {plan.Requested}\n{plan.Fallback} · {plan.RuntimeState}",
                10, StudioStyle.Text, wrap: true));
        }
        ClearChildrenUx2EnvK(_diff);
        if (preview.Diff.Entries.Count == 0)
            _diff.AddChild(StudioStyle.Label("Aucune différence canonique.", 10, StudioStyle.Muted));
        else
            foreach (var entry in preview.Diff.Entries)
                _diff.AddChild(StudioStyle.Label($"• {entry}", 10, StudioStyle.Text, wrap: true));
        ClearChildrenUx2EnvK(_diagnostics);
        foreach (var group in preview.Groups)
        {
            var details = group.Diagnostics.Count == 0
                ? "aucun blocage"
                : string.Join(" · ", group.Diagnostics.Select(item => item.Message));
            _diagnostics.AddChild(StudioStyle.Label(
                $"{group.Title} · {group.State} · {details}",
                10,
                group.Diagnostics.Any(item => item.Blocking) ? StudioStyle.Warning : StudioStyle.Muted,
                wrap: true));
        }
        _summary.Text = preview.Summary;
        _summary.AddThemeColorOverride("font_color", preview.Succeeded ? StudioStyle.Text : StudioStyle.Warning);
    }

    private void ResetReviewUx2EnvK()
    {
        ClearChildrenUx2EnvK(_waterPlans);
        ClearChildrenUx2EnvK(_graphicsPlans);
        ClearChildrenUx2EnvK(_diff);
        ClearChildrenUx2EnvK(_diagnostics);
        _waterPlans.AddChild(StudioStyle.Label("Prévisualisez pour obtenir l’inventaire d’eau.",
            10, StudioStyle.Muted));
        _graphicsPlans.AddChild(StudioStyle.Label("Prévisualisez pour obtenir le plan graphique.",
            10, StudioStyle.Muted));
        _diff.AddChild(StudioStyle.Label("Aucun aperçu sec.", 10, StudioStyle.Muted));
        _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic.", 10, StudioStyle.Muted));
        _summary.Text = "Sélectionnez le profil puis prévisualisez · aucun changement appliqué.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
    }

    private void InvalidatePreviewUx2EnvK()
    {
        if (_updating) return;
        _session.CancelWaterGraphicsApprovalUx2EnvK();
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _applied = false;
        ResetReviewUx2EnvK();
        SetStatusUx2EnvK("Brouillon modifié · relancez l’aperçu sec.");
        UpdateActionStateUx2EnvK();
    }

    private void WireDraftChangesUx2EnvK()
    {
        _refraction.Toggled += _ => InvalidatePreviewUx2EnvK();
        _reflections.Toggled += _ => InvalidatePreviewUx2EnvK();
        _depthColor.Toggled += _ => InvalidatePreviewUx2EnvK();
        _fallbackQuality.ItemSelected += _ => InvalidatePreviewUx2EnvK();
        _antiAliasing.ItemSelected += _ => InvalidatePreviewUx2EnvK();
        _anisotropy.ItemSelected += _ => InvalidatePreviewUx2EnvK();
        _upscaling.ItemSelected += _ => InvalidatePreviewUx2EnvK();
        _capabilityFallback.Toggled += _ => InvalidatePreviewUx2EnvK();
    }

    private void SetEditingEnabledUx2EnvK(bool enabled)
    {
        foreach (var check in new[] { _refraction, _reflections, _depthColor, _capabilityFallback })
            check.Disabled = !enabled;
        foreach (var option in new[] { _fallbackQuality, _antiAliasing, _anisotropy, _upscaling })
            option.Disabled = !enabled;
    }

    private void UpdateActionStateUx2EnvK()
    {
        _preview.Disabled = _draft is null;
        _apply.Disabled = !_canonicalPreviewReady || _lastPreview is not { Succeeded: true, CanApply: true };
        _undo.Disabled = _session.History?.CanUndo != true;
        _redo.Disabled = _session.History?.CanRedo != true;
        _save.Disabled = _session.Project is null;
    }

    private void ConfigureEvidenceDraftUx2EnvK()
    {
        _refraction.ButtonPressed = !_refraction.ButtonPressed;
        _reflections.ButtonPressed = !_reflections.ButtonPressed;
        _depthColor.ButtonPressed = !_depthColor.ButtonPressed;
        _fallbackQuality.Select(_fallbackQuality.Selected == 2 ? 1 : 2);
        _antiAliasing.Select(_antiAliasing.Selected == 4 ? 3 : 4);
        _anisotropy.Select(_anisotropy.Selected == 4 ? 3 : 4);
        _upscaling.Select(_upscaling.Selected == 1 ? 2 : 1);
        _capabilityFallback.ButtonPressed = !_capabilityFallback.ButtonPressed;
        InvalidatePreviewUx2EnvK();
    }

    private StudioUx2EnvKEvidenceSourceSnapshot CaptureSourceSnapshotUx2EnvK()
    {
        var project = _sourceSession.Project!;
        var history = _sourceSession.History!;
        return new(
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
            _sourceSession.CaptureWaterGraphicsApprovalUx2EnvK());
    }

    private bool SourceSnapshotMatchesUx2EnvK(StudioUx2EnvKEvidenceSourceSnapshot? expected = null)
    {
        var snapshot = expected ?? _evidenceSourceSnapshot;
        if (snapshot is null) return true;
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) &&
               history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               history?.CanUndo == snapshot.CanUndo && history?.CanRedo == snapshot.CanRedo &&
               _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureWaterGraphicsApprovalUx2EnvK() == snapshot.Approval;
    }

    private bool ReleaseIsolatedEvidenceSessionUx2EnvK()
    {
        var evidenceSession = _evidenceSession;
        var tempRoot = _evidenceTempRoot;
        var sessionReleased = true;
        try
        {
            evidenceSession?.Close();
        }
        catch (Exception exception)
        {
            sessionReleased = false;
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_K_EVIDENCE_RELEASE_FAILED " +
                        $"type={exception.GetType().Name} message={exception.Message}");
        }
        var directoryReleased = StudioSessionController.DeleteUx2EnvKTemporaryWorkspace(tempRoot);
        ResetEvidenceStateUx2EnvK();
        _session = _sourceSession;
        _evidenceReleaseSucceeded = sessionReleased && directoryReleased;
        if (!directoryReleased) _evidenceTempRoot = tempRoot;
        return _evidenceReleaseSucceeded;
    }

    private void ResetEvidenceStateUx2EnvK()
    {
        _evidenceSession = null;
        _evidenceTempRoot = null;
        _evidenceUsesRealModuleData = false;
        _evidenceSourceSnapshot = null;
    }

    private VBoxContainer ScrollTabUx2EnvK(string title)
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

    private static GridContainer GridUx2EnvK(int columns)
    {
        var grid = new GridContainer { Columns = columns, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 8);
        return grid;
    }

    private static OptionButton OptionsUx2EnvK(Func<string, string> label, IEnumerable<string> values)
    {
        var options = StudioUx2EnvBControls.Options();
        foreach (var value in values) options.AddItem(label(value));
        return options;
    }

    private static void SelectValueUx2EnvK(OptionButton option, IReadOnlyList<string> values, string value)
    {
        var index = values.ToList().FindIndex(item => string.Equals(item, value, StringComparison.Ordinal));
        option.Select(Math.Max(0, index));
    }

    private static void ClearChildrenUx2EnvK(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void SetStatusUx2EnvK(string text, bool error = false)
    {
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Warning : StudioStyle.Muted);
        StatusChanged?.Invoke(text, error);
    }

    private void UpdateDialogBoundsUx2EnvK()
    {
        var viewport = GetViewportRect().Size;
        var width = Mathf.Clamp(viewport.X - 48, 1180, 1740);
        var height = Mathf.Clamp(viewport.Y - 48, 720, 1030);
        _dialog.Size = new Vector2(width, height);
        _dialog.Position = new Vector2((viewport.X - width) / 2, (viewport.Y - height) / 2);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && Visible) UpdateDialogBoundsUx2EnvK();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _ExitTree()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null) Close();
    }

    private static IEnumerable<string> DescendantVisibleTextUx2EnvK(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Label { Visible: true } label) yield return label.Text;
            if (child is Button { Visible: true } button) yield return button.Text;
            if (child is OptionButton { Visible: true } option)
                for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
            foreach (var nested in DescendantVisibleTextUx2EnvK(child)) yield return nested;
        }
    }

    private static bool ContainsUuidUx2EnvK(string text) => System.Text.RegularExpressions.Regex.IsMatch(
        text, "(?i)(?:[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}|[0-9a-f]{32})");
}
