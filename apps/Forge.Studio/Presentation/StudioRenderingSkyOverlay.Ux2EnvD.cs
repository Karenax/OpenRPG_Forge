using Forge.Domain.Areas;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioUx2EnvDLightBudgetRow : StudioUx2EnvBRowBase<StudioUx2EnvDLightBudgetDraft>
{
    private readonly LineEdit _platform;
    private readonly SpinBox _dynamic;
    private readonly SpinBox _shadows;
    private readonly SpinBox _atlas;
    private readonly SpinBox _warning;

    public override StudioUx2EnvDLightBudgetDraft Value => new(
        _platform.Text,
        _dynamic.Value,
        _shadows.Value,
        _atlas.Value,
        _warning.Value);

    internal StudioUx2EnvDLightBudgetRow(StudioUx2EnvDLightBudgetDraft value) : base("Budget plateforme")
    {
        _platform = StudioUx2EnvBControls.Text("desktop"); _platform.Text = value.Platform;
        _dynamic = Number(0, 4096, 1, value.MaximumDynamicLights);
        _shadows = Number(0, 4096, 1, value.MaximumShadowCastingLights);
        _atlas = Number(512, 16384, 512, value.ShadowAtlasSize);
        _warning = Number(.5, 1, .01, value.WarningThreshold);
        var grid = new GridContainer { Columns = 5, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddChild(StudioUx2EnvBControls.Field("Plateforme", _platform, 180));
        grid.AddChild(StudioUx2EnvBControls.Field("Lumières max.", _dynamic));
        grid.AddChild(StudioUx2EnvBControls.Field("Avec ombres max.", _shadows));
        grid.AddChild(StudioUx2EnvBControls.Field("Atlas d’ombres", _atlas));
        grid.AddChild(StudioUx2EnvBControls.Field("Seuil d’alerte", _warning));
        Root.AddChild(grid);
        Watch(_platform); Watch(_dynamic); Watch(_shadows); Watch(_atlas); Watch(_warning);
    }

    public override void SetEditingEnabled(bool enabled)
    {
        base.SetEditingEnabled(enabled); _platform.Editable = enabled;
        foreach (var control in new[] { _dynamic, _shadows, _atlas, _warning }) control.Editable = enabled;
    }

    private static SpinBox Number(double min, double max, double step, double value)
    {
        var control = StudioUx2EnvBControls.Number(min, max, step); control.Value = value; return control;
    }
}

internal sealed partial class StudioRenderingSkyOverlayUx2EnvD : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx2EnvDEvidenceSourceSnapshot? _evidenceSourceSnapshot;
    private string? _evidenceTempRoot;
    private bool _evidenceUsesRealModuleData;
    private bool _evidenceUsesCanonicalFallback;
    private bool _evidenceReleaseSucceeded = true;
    private readonly PanelContainer _dialog;
    private readonly OptionButton _area;
    private readonly Label _selection;
    private readonly TabContainer _tabs;
    private readonly OptionButton _workflow;
    private readonly OptionButton _quality;
    private readonly CheckBox _forwardPlus;
    private readonly CheckBox _perPixel;
    private readonly CheckBox _materialInstances;
    private readonly OptionButton _toneMapping;
    private readonly CheckBox _hdrOutput;
    private readonly SpinBox _exposure;
    private readonly SpinBox _whitePoint;
    private readonly LineEdit _lut;
    private readonly SpinBox _brightness;
    private readonly SpinBox _contrastMultiplier;
    private readonly SpinBox _saturationMultiplier;
    private readonly SpinBox _ambientOcclusionIntensityMultiplier;
    private readonly SpinBox _ambientOcclusionPower;
    private readonly SpinBox _sunPitch;
    private readonly SpinBox _sunYaw;
    private readonly SpinBox _sunAngularDistance;
    private readonly SpinBox _shadowBlur;
    private readonly StudioUx2EnvBCollectionEditor<StudioUx2EnvDLightBudgetDraft> _budgets;
    private readonly OptionButton _skyMode;
    private readonly LineEdit _panorama;
    private readonly LineEdit _zenith;
    private readonly LineEdit _horizon;
    private readonly LineEdit _ground;
    private readonly SpinBox _stars;
    private readonly SpinBox _clouds;
    private readonly SpinBox _moon;
    private readonly SpinBox _energy;
    private readonly SpinBox _transition;
    private readonly OptionButton _simulationPlatform;
    private readonly SpinBox _simulatedDynamic;
    private readonly SpinBox _simulatedShadows;
    private readonly SpinBox _simulatedElapsed;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;
    private IReadOnlyList<StudioUx2EnvDAreaChoice> _choices = [];
    private IReadOnlyList<string> _simulationPlatforms = [];
    private StudioUx2EnvDDraft? _draft;
    private StudioUx2EnvDPreview? _lastPreview;
    private bool _updating;
    private bool _applied;
    private bool _canonicalPreviewReady;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? AreaEnvironmentChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _choices.Count;
    internal bool HasExplicitSelection => _draft is not null && _area.Selected > 0;
    internal bool CanonicalPreviewReady => _canonicalPreviewReady && _lastPreview is
    {
        Succeeded: true,
        CanonicalDryRun: true,
        RenderPlanBuilt: true,
        VisualQualityPlanBuilt: true,
        SkyTransitionPlanBuilt: true,
        AdapterInstantiationSkipped: true,
        RuntimeClaimsBounded: true,
    };
    internal bool ShowsCompleteBudgets => _draft is not null && _budgets.Values.Count == _draft.LightBudgets.Count;
    internal bool ShowsPlanWarnings => _lastPreview is { BudgetDiagnosticsBuilt: true } &&
                                       _lastPreview.Groups.SelectMany(group => group.Diagnostics)
                                           .Any(item => !item.Blocking);
    internal bool ShowsStructuredDiagnostics => _lastPreview?.Groups.Count == 4;
    internal bool ShowsHumanSurface
    {
        get
        {
            var tabTitles = Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle);
            var text = string.Join(' ', DescendantVisibleText(this).Concat(tabTitles));
            return text.Contains("PBR", StringComparison.Ordinal) &&
                   text.Contains("HDR", StringComparison.Ordinal) &&
                   text.Contains("Budgets", StringComparison.Ordinal) &&
                   text.Contains("Ciel", StringComparison.Ordinal) &&
                   text.Contains("simulation auteur", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuid(text);
        }
    }
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealModuleSource => UsesIsolatedEvidence && _evidenceUsesRealModuleData;
    internal bool UsesCanonicalFallbackEvidence => UsesIsolatedEvidence && _evidenceUsesCanonicalFallback;
    internal bool EvidenceSourceUntouched => SourceSnapshotMatches();
    internal bool EvidenceResourcesReleased => _evidenceReleaseSucceeded &&
                                               _evidenceSession is null && _evidenceTempRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);

    internal StudioRenderingSkyOverlayUx2EnvD(StudioSessionController session)
    {
        _sourceSession = session; _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.015f, .02f, .027f, .96f); MouseFilter = MouseFilterEnum.Stop; Visible = false;
        _dialog = StudioStyle.Panel("Ux2EnvDRenderingSkyDialog", StudioStyle.Surface, 12, 1); AddChild(_dialog);
        var margin = StudioStyle.Margin(14, 12, 14, 12); _dialog.AddChild(margin);
        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 8); margin.AddChild(root);
        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; root.AddChild(heading);
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        headingText.AddChild(StudioStyle.Label("Rendu de référence et ciel", 18));
        headingText.AddChild(StudioStyle.Label(
            "PBR, HDR, qualité, budgets et ciel · plans déterministes sans prétendre mesurer le matériel actif.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true); close.Pressed += Close; heading.AddChild(close);
        _area = StudioUx2EnvBControls.Options(); _area.ItemSelected += SelectArea;
        root.AddChild(StudioUx2EnvBControls.Field("Zone à modifier · sélection explicite", _area, 420));
        _selection = StudioStyle.Label("Aucune zone sélectionnée.", 10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);
        _tabs = new TabContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(_tabs);

        _workflow = StudioUx2EnvBControls.Options("Metallic / Roughness");
        _quality = StudioUx2EnvBControls.Options("Basse", "Moyenne", "Élevée", "Ultra");
        _forwardPlus = StudioUx2EnvBControls.Check("Forward+ demandé");
        _perPixel = StudioUx2EnvBControls.Check("Éclairage par pixel demandé");
        _materialInstances = StudioUx2EnvBControls.Check("Instances de matériaux");
        BuildPbrTab();

        _toneMapping = StudioUx2EnvBControls.Options("AgX", "ACES", "Filmic", "Linéaire");
        _hdrOutput = StudioUx2EnvBControls.Check("Sortie HDR demandée");
        _exposure = Number(.05, 8, .01); _whitePoint = Number(1, 32, .05);
        _lut = StudioUx2EnvBControls.Text("grading/neutral-lut.tres");
        _brightness = Number(.1, 4, .01); _contrastMultiplier = Number(.25, 4, .01);
        _saturationMultiplier = Number(0, 4, .01);
        _ambientOcclusionIntensityMultiplier = Number(0, 4, .01);
        _ambientOcclusionPower = Number(.1, 8, .01);
        _sunPitch = Number(-89, 89, .25); _sunYaw = Number(-360, 360, .25);
        _sunAngularDistance = Number(0, 90, .01); _shadowBlur = Number(0, 10, .01);
        BuildColorTab();

        _budgets = new StudioUx2EnvBCollectionEditor<StudioUx2EnvDLightBudgetDraft>(
            "Ajouter un budget plateforme", value => new StudioUx2EnvDLightBudgetRow(value),
            () => new StudioUx2EnvDLightBudgetDraft("nouvelle-plateforme", 16, 4, 2048, .75));
        _simulationPlatform = StudioUx2EnvBControls.Options();
        _simulatedDynamic = Number(0, 1_000_000, 1, greater: true);
        _simulatedShadows = Number(0, 1_000_000, 1, greater: true);
        _simulatedElapsed = Number(0, 3600, .25);
        BuildBudgetsTab();

        _skyMode = StudioUx2EnvBControls.Options("Physique", "Procédural", "Panorama HDRI");
        _panorama = StudioUx2EnvBControls.Text("skies/dawn.exr");
        _zenith = StudioUx2EnvBControls.Text("#4c78a8");
        _horizon = StudioUx2EnvBControls.Text("#e4b88a");
        _ground = StudioUx2EnvBControls.Text("#38402f");
        _stars = Number(0, 4, .01); _clouds = Number(0, 1, .01); _moon = Number(0, 4, .01);
        _energy = Number(.05, 8, .01); _transition = Number(0, 3600, .25);
        BuildSkyTab();

        _diff = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _diagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        BuildReviewTab();
        _summary = StudioStyle.Label(
            "Sélectionnez une zone puis lancez l’aperçu sec · aucun changement appliqué.",
            10, StudioStyle.Muted, wrap: true);
        _summary.CustomMinimumSize = new Vector2(0, 50); root.AddChild(_summary);
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 6); root.AddChild(actions);
        _preview = StudioStyle.Button("Prévisualiser", compact: true);
        _apply = StudioStyle.Button("Appliquer", compact: true);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        {
            button.CustomMinimumSize = new Vector2(116, 34); actions.AddChild(button);
        }
        _preview.Pressed += Preview; _apply.Pressed += Apply; _undo.Pressed += Undo;
        _redo.Pressed += Redo; _save.Pressed += Save;
        var note = StudioStyle.Label(
            "Les comptes sont une simulation auteur. Le dry-run ne détecte ni GPU, ni driver, ni écran HDR, ni backend actif.",
            9, StudioStyle.Muted, wrap: true);
        note.SizeFlagsHorizontal = SizeFlags.ExpandFill; note.CustomMinimumSize = new Vector2(0, 32); root.AddChild(note);
        _status = StudioStyle.Label("Prêt · sélection explicite requise", 9, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        ConnectInvalidation(); SetEditingEnabled(false); ResetControls();
    }

    internal void Open()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null) ReleaseIsolatedEvidenceSession();
        _session = _sourceSession; Visible = true; UpdateDialogBounds(); RefreshChoices();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceTempRoot is null) return;
        if (Visible || !ReferenceEquals(_session, _sourceSession)) _session.CancelRenderingUx2EnvD();
        Visible = false; _draft = null; _lastPreview = null; _canonicalPreviewReady = false; _applied = false;
        ReleaseIsolatedEvidenceSession(); _session = _sourceSession;
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        var approval = _sourceSession.CaptureRenderingApprovalUx2EnvD();
        _session = _sourceSession; Visible = true; UpdateDialogBounds(); RefreshChoices();
        _sourceSession.RestoreRenderingApprovalUx2EnvD(approval);
    }

    internal bool PrepareEvidence(string mode)
    {
        var sourceApproval = _sourceSession.CaptureRenderingApprovalUx2EnvD();
        Close(); _sourceSession.RestoreRenderingApprovalUx2EnvD(sourceApproval);
        _evidenceReleaseSucceeded = true;
        var sourceProject = _sourceSession.Project;
        if (sourceProject is null || _sourceSession.History is null || sourceProject.Areas.Count == 0) return false;
        _evidenceSourceSnapshot = CaptureSourceSnapshot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux2-env-d-{Guid.NewGuid():N}");
        var evidenceSession = new StudioSessionController();
        try
        {
            var realArea = sourceProject.Areas.FirstOrDefault(area => area.Environment.Rendering.LightBudgets.Any(
                budget => budget.MaximumDynamicLights > 0 && budget.MaximumShadowCastingLights > 0));
            _evidenceUsesRealModuleData = realArea is not null; _evidenceUsesCanonicalFallback = realArea is null;
            var evidenceProject = realArea is null
                ? StudioSessionController.CreateUx2EnvDContractProject(sourceProject)
                : sourceProject;
            var evidenceAreaId = realArea?.Id ?? evidenceProject.Areas[0].Id;
            var created = ProjectSession.Create(tempRoot, evidenceProject);
            if (!created.Succeeded || !evidenceSession.Open(tempRoot).Succeeded)
            {
                evidenceSession.Close();
                var released = StudioSessionController.DeleteUx2EnvDTemporaryWorkspace(tempRoot);
                ResetEvidenceState(); _evidenceReleaseSucceeded = released;
                if (!released) _evidenceTempRoot = tempRoot;
                return false;
            }
            _evidenceSession = evidenceSession; _evidenceTempRoot = tempRoot; _session = evidenceSession;
            Visible = true; UpdateDialogBounds(); RefreshChoices();
            var choiceIndex = _choices.ToList().FindIndex(choice => choice.AreaId == evidenceAreaId);
            if (choiceIndex < 0) { Close(); return false; }
            _area.Select(choiceIndex + 1); LoadChoice(choiceIndex);
            if (!ConfigureEvidenceDraft()) { Close(); return false; }
            Preview();
            var ready = CanonicalPreviewReady && HasExplicitSelection && ShowsCompleteBudgets && ShowsPlanWarnings &&
                        ShowsStructuredDiagnostics && ShowsHumanSurface && UsesIsolatedEvidence &&
                        (UsesRealModuleSource ^ UsesCanonicalFallbackEvidence) && EvidenceSourceUntouched;
            if (!ready) { Close(); return false; }
            return true;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_D_EVIDENCE_FAILED mode={mode} " +
                        $"type={exception.GetType().Name} message={exception.Message}");
            var sessionReleased = true;
            try { evidenceSession.Close(); }
            catch (Exception releaseException)
            {
                sessionReleased = false;
                GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_D_EVIDENCE_RELEASE_FAILED " +
                            $"type={releaseException.GetType().Name} message={releaseException.Message}");
            }
            var released = StudioSessionController.DeleteUx2EnvDTemporaryWorkspace(tempRoot);
            ResetEvidenceState(); _evidenceReleaseSucceeded = sessionReleased && released;
            if (!released) _evidenceTempRoot = tempRoot;
            _session = _sourceSession; Visible = false; return false;
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        var sourceSnapshot = CaptureSourceSnapshot();
        if (!PrepareEvidence("ux2-env-d-rendering-sky") || !CanonicalPreviewReady || !UsesIsolatedEvidence)
        {
            Close(); return false;
        }
        var isolated = _session;
        if (_lastPreview is not { Succeeded: true, CanApply: true }) { Close(); return false; }
        var isolatedProjectBefore = isolated.Project;
        Apply();
        var applied = _applied && isolated.Project is not null;
        var appliedJson = applied ? ProjectJsonSerializer.Serialize(isolated.Project!) : string.Empty;
        Undo();
        var undone = !_applied && ReferenceEquals(isolatedProjectBefore, isolated.Project) &&
                     isolated.History?.CanRedo == true;
        Redo();
        var redone = _applied && isolated.Project is not null &&
                     string.Equals(appliedJson, ProjectJsonSerializer.Serialize(isolated.Project!), StringComparison.Ordinal);
        Save();
        var reopen = new StudioSessionController();
        var roundTrip = false;
        try
        {
            var opened = _evidenceTempRoot is not null && reopen.Open(_evidenceTempRoot).Succeeded;
            roundTrip = opened && reopen.Project is not null &&
                        string.Equals(appliedJson, ProjectJsonSerializer.Serialize(reopen.Project!), StringComparison.Ordinal);
        }
        finally { reopen.Close(); }
        Close();
        return applied && undone && redone && roundTrip && SourceSnapshotMatches(sourceSnapshot) &&
               EvidenceResourcesReleased && SourceSessionRestored;
    }

    private void BuildPbrTab()
    {
        var tab = ScrollTab("PBR & qualité");
        var profile = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        profile.AddChild(StudioUx2EnvBControls.Field("Workflow canonique", _workflow, 240));
        profile.AddChild(StudioUx2EnvBControls.Field("Qualité demandée", _quality, 200));
        var flags = new VBoxContainer(); flags.AddChild(_forwardPlus); flags.AddChild(_perPixel);
        flags.AddChild(_materialInstances); profile.AddChild(flags); profile.AddChild(new Control());
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvDPbr", "Profil PBR de zone",
            "Le workflow Metallic / Roughness et les instances sont persistés. Les canaux de textures restent gouvernés par les profils d’assets.",
            profile));
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvDChannels", "Canaux PBR gouvernés",
            "Base Color, Normal, Roughness, Metallic, AO, Emissive et Height restent liés aux profils d’import d’assets ; cette fiche ne les réécrit pas.",
            StudioStyle.Label("Aucune texture ni ressource source n’est modifiée par cet authoring de zone.",
                10, StudioStyle.Muted, wrap: true)));
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvDBackend", "Demande de backend",
            "Forward+ et l’éclairage par pixel sont des choix de profil. Cette surface ne certifie pas le GPU ou le driver actif.",
            StudioStyle.Label("Le viewport Studio applique le profil enregistré via l’adaptateur existant.",
                10, StudioStyle.Muted, wrap: true)));
    }

    private void BuildColorTab()
    {
        var tab = ScrollTab("HDR & couleur");
        var grid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddChild(StudioUx2EnvBControls.Field("Tone mapper", _toneMapping, 180));
        grid.AddChild(StudioUx2EnvBControls.Field("Exposition", _exposure));
        grid.AddChild(StudioUx2EnvBControls.Field("Point blanc", _whitePoint));
        grid.AddChild(StudioUx2EnvBControls.Field("LUT relative", _lut, 280));
        grid.AddChild(_hdrOutput); grid.AddChild(new Control());
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvDColor", "Gestion de couleur",
            "AgX, ACES, Filmic ou linéaire, exposition, point blanc, LUT et demande HDR sont authorés sans certifier un écran HDR.",
            grid));
        var grading = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grading.AddChild(StudioUx2EnvBControls.Field("Luminosité", _brightness));
        grading.AddChild(StudioUx2EnvBControls.Field("Contraste", _contrastMultiplier));
        grading.AddChild(StudioUx2EnvBControls.Field("Saturation", _saturationMultiplier));
        grading.AddChild(StudioUx2EnvBControls.Field("Intensité occlusion", _ambientOcclusionIntensityMultiplier));
        grading.AddChild(StudioUx2EnvBControls.Field("Puissance occlusion", _ambientOcclusionPower));
        grading.AddChild(new Control());
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvDVisualTuning", "Étalonnage visuel de la zone",
            "Ces valeurs appartiennent au module et sont appliquées sans branche dédiée à son nom.", grading));
        var sun = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        sun.AddChild(StudioUx2EnvBControls.Field("Soleil vertical (°)", _sunPitch));
        sun.AddChild(StudioUx2EnvBControls.Field("Soleil horizontal (°)", _sunYaw));
        sun.AddChild(StudioUx2EnvBControls.Field("Diamètre apparent (°)", _sunAngularDistance));
        sun.AddChild(StudioUx2EnvBControls.Field("Flou des ombres", _shadowBlur));
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvDSunTuning", "Soleil et ombres",
            "Orientation, taille apparente et douceur des ombres sont persistées dans la zone.", sun));
    }

    private void BuildBudgetsTab()
    {
        var tab = ScrollTab("Budgets");
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvDBudgets", "Budgets data-driven par plateforme",
            "Aucune limite Aurora n’est codée en dur : chaque plateforme garde ses lumières, ombres, atlas et seuil.",
            _budgets));
        var simulation = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        simulation.AddChild(StudioUx2EnvBControls.Field("Plateforme simulée", _simulationPlatform, 200));
        simulation.AddChild(StudioUx2EnvBControls.Field("Lumières simulées", _simulatedDynamic));
        simulation.AddChild(StudioUx2EnvBControls.Field("Avec ombres simulées", _simulatedShadows));
        simulation.AddChild(StudioUx2EnvBControls.Field("Temps de ciel simulé (s)", _simulatedElapsed));
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvDSimulation", "Simulation auteur de seuils",
            "Ces entrées ne sont pas persistées et ne comptent aucune lumière du runtime ; elles exercent seulement le plan de budget.",
            simulation));
    }

    private void BuildSkyTab()
    {
        var tab = ScrollTab("Ciel");
        var identity = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        identity.AddChild(StudioUx2EnvBControls.Field("Mode", _skyMode, 200));
        identity.AddChild(StudioUx2EnvBControls.Field("Panorama HDRI relatif", _panorama, 300));
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvDSkyMode", "Mode de ciel",
            "Le panorama exige une ressource relative ; physique et procédural utilisent les paramètres canoniques.", identity));
        var colors = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        colors.AddChild(StudioUx2EnvBControls.Field("Zénith", _zenith));
        colors.AddChild(StudioUx2EnvBControls.Field("Horizon", _horizon));
        colors.AddChild(StudioUx2EnvBControls.Field("Sol", _ground));
        colors.AddChild(StudioUx2EnvBControls.Field("Étoiles", _stars));
        colors.AddChild(StudioUx2EnvBControls.Field("Nuages", _clouds));
        colors.AddChild(StudioUx2EnvBControls.Field("Lune", _moon));
        colors.AddChild(StudioUx2EnvBControls.Field("Énergie", _energy));
        colors.AddChild(StudioUx2EnvBControls.Field("Transition (s)", _transition));
        colors.AddChild(new Control());
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvDSkyProfile", "Atmosphère et transition",
            "Couleurs, étoiles, nuages, lune, énergie et durée alimentent un plan temporel borné, sans animation live pendant le dry-run.",
            colors));
    }

    private void BuildReviewTab()
    {
        var tab = ScrollTab("Revue");
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvDDiff", "Diff sémantique",
            "PBR, couleur, budgets et ciel sont résumés sans identifiant interne.", _diff));
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvDDiagnostics", "Validation et avertissements de plan",
            "Les erreurs bloquantes restent distinctes des avertissements de simulation.", _diagnostics));
    }

    private VBoxContainer ScrollTab(string title)
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
            CustomMinimumSize = new Vector2(980, 0),
        };
        content.AddThemeConstantOverride("separation", 8); scroll.AddChild(content); _tabs.AddChild(scroll);
        _tabs.SetTabTitle(_tabs.GetTabCount() - 1, title); return content;
    }

    private void RefreshChoices()
    {
        _updating = true; _choices = _session.RenderingChoicesUx2EnvD;
        _area.Clear(); _area.AddItem("Choisir explicitement une zone…");
        foreach (var choice in _choices) _area.AddItem(choice.Label);
        _area.Select(0); _updating = false; ClearSelection();
    }

    private void SelectArea(long index)
    {
        if (_updating) return;
        var choiceIndex = (int)index - 1;
        if (choiceIndex < 0 || choiceIndex >= _choices.Count) ClearSelection(); else LoadChoice(choiceIndex);
    }

    private void ClearSelection()
    {
        _draft = null; _lastPreview = null; _applied = false; _canonicalPreviewReady = false;
        _session.CancelRenderingUx2EnvD(); ResetControls();
        _selection.Text = "Choisissez explicitement une zone ; aucune fiche n’est sélectionnée automatiquement.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
        _summary.Text = "Sélectionnez une zone puis lancez l’aperçu sec · aucun changement appliqué.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        SetEditingEnabled(false); UpdateTransactionButtons(); SetStatus("Prêt · sélection explicite requise", false);
    }

    private void LoadChoice(int choiceIndex)
    {
        var choice = _choices[choiceIndex]; _session.CancelRenderingUx2EnvD();
        _draft = _session.CreateRenderingDraftUx2EnvD(choice.AreaId);
        _lastPreview = null; _applied = false; _canonicalPreviewReady = false;
        if (_draft is null)
        {
            ResetControls(); _selection.Text = "La zone choisie a changé. Rechargez la page Environnement.";
            _selection.AddThemeColorOverride("font_color", StudioStyle.Danger);
            SetEditingEnabled(false); UpdateTransactionButtons(); SetStatus("Zone indisponible · aucune mutation", true);
            return;
        }
        Populate(_draft);
        _selection.Text = $"{choice.Label} · profil complet chargé";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Accent);
        _summary.Text = "Rendu et ciel chargés · fondations, climat, eau, audio et tous les voisins restent préservés.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        SetEditingEnabled(true); UpdateTransactionButtons(); SetStatus("Brouillon prêt · aperçu sec requis", false);
    }

    private void Populate(StudioUx2EnvDDraft draft)
    {
        _updating = true;
        _workflow.Select((int)draft.MaterialWorkflow); _toneMapping.Select((int)draft.ToneMapping);
        _quality.Select((int)draft.Quality); _forwardPlus.ButtonPressed = draft.ForwardPlus;
        _perPixel.ButtonPressed = draft.PerPixelLighting; _materialInstances.ButtonPressed = draft.MaterialInstances;
        _hdrOutput.ButtonPressed = draft.HdrOutput; _exposure.Value = draft.Exposure;
        _whitePoint.Value = draft.WhitePoint; _lut.Text = draft.LutResource;
        _brightness.Value = draft.VisualTuning.Brightness;
        _contrastMultiplier.Value = draft.VisualTuning.ContrastMultiplier;
        _saturationMultiplier.Value = draft.VisualTuning.SaturationMultiplier;
        _ambientOcclusionIntensityMultiplier.Value = draft.VisualTuning.AmbientOcclusionIntensityMultiplier;
        _ambientOcclusionPower.Value = draft.VisualTuning.AmbientOcclusionPower;
        _sunPitch.Value = draft.VisualTuning.DirectionalLightPitchDegrees;
        _sunYaw.Value = draft.VisualTuning.DirectionalLightYawDegrees;
        _sunAngularDistance.Value = draft.VisualTuning.DirectionalLightAngularDistance;
        _shadowBlur.Value = draft.VisualTuning.ShadowBlur;
        _budgets.SetValues(draft.LightBudgets); RefreshSimulationPlatforms(draft.SimulationPlatform);
        _simulatedDynamic.Value = draft.SimulatedDynamicLights;
        _simulatedShadows.Value = draft.SimulatedShadowCastingLights;
        _simulatedElapsed.Value = draft.SimulatedTransitionElapsedSeconds;
        _skyMode.Select((int)draft.SkyMode); _panorama.Text = draft.PanoramaResource;
        _zenith.Text = draft.ZenithColor; _horizon.Text = draft.HorizonColor; _ground.Text = draft.GroundColor;
        _stars.Value = draft.StarIntensity; _clouds.Value = draft.CloudCoverage; _moon.Value = draft.MoonIntensity;
        _energy.Value = draft.EnergyMultiplier; _transition.Value = draft.TransitionSeconds;
        _updating = false; RenderDiff(null); RenderDiagnostics([]);
    }

    private StudioUx2EnvDDraft? ReadDraft()
    {
        if (_draft is null) return null;
        var simulationIndex = (int)_simulationPlatform.Selected - 1;
        var platform = simulationIndex >= 0 && simulationIndex < _simulationPlatforms.Count
            ? _simulationPlatforms[simulationIndex]
            : string.Empty;
        return _draft with
        {
            MaterialWorkflow = (PbrMaterialWorkflow)_workflow.Selected,
            ToneMapping = (ToneMappingProfile)_toneMapping.Selected,
            Quality = (RenderQualityTier)_quality.Selected,
            ForwardPlus = _forwardPlus.ButtonPressed,
            PerPixelLighting = _perPixel.ButtonPressed,
            MaterialInstances = _materialInstances.ButtonPressed,
            HdrOutput = _hdrOutput.ButtonPressed,
            Exposure = _exposure.Value,
            WhitePoint = _whitePoint.Value,
            LutResource = _lut.Text,
            VisualTuning = new AreaVisualTuningProfile(
                AreaVisualTuningProfile.CurrentSchemaVersion,
                _brightness.Value,
                _contrastMultiplier.Value,
                _saturationMultiplier.Value,
                _ambientOcclusionIntensityMultiplier.Value,
                _ambientOcclusionPower.Value,
                _sunPitch.Value,
                _sunYaw.Value,
                _sunAngularDistance.Value,
                _shadowBlur.Value),
            LightBudgets = _budgets.Values,
            SkyMode = (SkyProfileMode)_skyMode.Selected,
            PanoramaResource = _panorama.Text,
            ZenithColor = _zenith.Text,
            HorizonColor = _horizon.Text,
            GroundColor = _ground.Text,
            StarIntensity = _stars.Value,
            CloudCoverage = _clouds.Value,
            MoonIntensity = _moon.Value,
            EnergyMultiplier = _energy.Value,
            TransitionSeconds = _transition.Value,
            SimulationPlatform = platform,
            SimulatedDynamicLights = _simulatedDynamic.Value,
            SimulatedShadowCastingLights = _simulatedShadows.Value,
            SimulatedTransitionElapsedSeconds = _simulatedElapsed.Value,
        };
    }

    private bool ConfigureEvidenceDraft()
    {
        var current = ReadDraft();
        if (current is null) return false;
        var budgetIndex = current.LightBudgets.ToList().FindIndex(item =>
            item.MaximumDynamicLights > 0 && item.MaximumShadowCastingLights > 0);
        if (budgetIndex < 0) return false;
        var selected = current.LightBudgets[budgetIndex];
        var nextMaximum = selected.MaximumDynamicLights < 4096
            ? selected.MaximumDynamicLights + 1
            : Math.Max(selected.MaximumShadowCastingLights, selected.MaximumDynamicLights - 1);
        var threshold = selected.WarningThreshold < .99 ? selected.WarningThreshold + .01 : selected.WarningThreshold - .01;
        var budgets = current.LightBudgets.Select((item, index) => index == budgetIndex
            ? item with { MaximumDynamicLights = nextMaximum, WarningThreshold = threshold }
            : item).ToArray();
        var configuredBudget = budgets[budgetIndex];
        var simulatedDynamic = Math.Ceiling(configuredBudget.MaximumDynamicLights * configuredBudget.WarningThreshold);
        var simulatedShadows = Math.Min(simulatedDynamic,
            Math.Ceiling(configuredBudget.MaximumShadowCastingLights * configuredBudget.WarningThreshold));
        var configured = current with
        {
            Exposure = current.Exposure <= 7.9375 ? current.Exposure + .0625 : current.Exposure - .0625,
            LightBudgets = budgets,
            EnergyMultiplier = current.EnergyMultiplier <= 7.96875
                ? current.EnergyMultiplier + .03125
                : current.EnergyMultiplier - .03125,
            SimulationPlatform = configuredBudget.Platform,
            SimulatedDynamicLights = simulatedDynamic,
            SimulatedShadowCastingLights = simulatedShadows,
            SimulatedTransitionElapsedSeconds = Math.Min(3600, current.TransitionSeconds / 2),
        };
        Populate(configured); InvalidatePreview(); return true;
    }

    private void RefreshSimulationPlatforms(string preferred)
    {
        _simulationPlatforms = _budgets.Values.Select(item => item.Platform.Trim().ToLowerInvariant()).ToArray();
        _simulationPlatform.Clear(); _simulationPlatform.AddItem("Choisir une plateforme du profil…");
        foreach (var platform in _simulationPlatforms)
            _simulationPlatform.AddItem(string.IsNullOrWhiteSpace(platform) ? "Plateforme sans nom" : platform);
        var index = _simulationPlatforms.ToList().FindIndex(item =>
            string.Equals(item, preferred.Trim().ToLowerInvariant(), StringComparison.Ordinal));
        _simulationPlatform.Select(index < 0 ? 0 : index + 1);
    }

    private void Preview()
    {
        var draft = ReadDraft(); if (draft is null) return;
        var result = _session.PreviewRenderingUx2EnvD(draft); _lastPreview = result;
        _canonicalPreviewReady = result.Succeeded && result.CanonicalDryRun && result.AdapterInstantiationSkipped &&
                                 result.RuntimeClaimsBounded;
        _apply.Disabled = !result.CanApply || !result.Diff.HasChanges || !_canonicalPreviewReady;
        RenderDiff(result); RenderDiagnostics(result.Groups);
        _summary.Text = string.IsNullOrWhiteSpace(result.Summary) ? result.Message : result.Message + "\n" + result.Summary;
        _summary.AddThemeColorOverride("font_color", result.Succeeded ? StudioStyle.Success : StudioStyle.Danger);
        SetStatus(result.Message, !result.Succeeded); UpdateTransactionButtons(keepApplyState: true);
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedRenderingUx2EnvD(); SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) { _apply.Disabled = true; _canonicalPreviewReady = false; return; }
        _applied = true; _apply.Disabled = true; _preview.Disabled = true; SetEditingEnabled(false);
        UpdateTransactionButtons();
        _summary.Text = "Appliqué · seuls le rendu et le ciel de la zone choisie ont changé ; le viewport consommera le profil enregistré.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Success); AreaEnvironmentChanged?.Invoke();
    }

    private void Undo()
    {
        var result = _session.Undo(); SetStatus(result.Message, !result.Succeeded); if (!result.Succeeded) return;
        _applied = false; UpdateTransactionButtons();
        _summary.Text = "Annulé · la révision précédente du rendu et du ciel est restaurée.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Accent); AreaEnvironmentChanged?.Invoke();
    }

    private void Redo()
    {
        var result = _session.Redo(); SetStatus(result.Message, !result.Succeeded); if (!result.Succeeded) return;
        _applied = true; UpdateTransactionButtons();
        _summary.Text = "Rétabli · le profil approuvé est de nouveau présent.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Success); AreaEnvironmentChanged?.Invoke();
    }

    private void Save()
    {
        var result = _session.Save(); SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) { _save.Disabled = true; _summary.Text += "\nProjet enregistré · la réouverture relira le même agrégat canonique."; }
    }

    private void ConnectInvalidation()
    {
        _workflow.ItemSelected += _ => InvalidatePreview(); _quality.ItemSelected += _ => InvalidatePreview();
        _forwardPlus.Toggled += _ => InvalidatePreview(); _perPixel.Toggled += _ => InvalidatePreview();
        _materialInstances.Toggled += _ => InvalidatePreview(); _toneMapping.ItemSelected += _ => InvalidatePreview();
        _hdrOutput.Toggled += _ => InvalidatePreview(); _exposure.ValueChanged += _ => InvalidatePreview();
        _whitePoint.ValueChanged += _ => InvalidatePreview(); _lut.TextChanged += _ => InvalidatePreview();
        foreach (var control in new[] { _brightness, _contrastMultiplier, _saturationMultiplier,
                     _ambientOcclusionIntensityMultiplier, _ambientOcclusionPower, _sunPitch, _sunYaw,
                     _sunAngularDistance, _shadowBlur })
            control.ValueChanged += _ => InvalidatePreview();
        _budgets.Changed += () =>
        {
            var selectedIndex = (int)_simulationPlatform.Selected - 1;
            var selected = selectedIndex >= 0 && selectedIndex < _simulationPlatforms.Count
                ? _simulationPlatforms[selectedIndex]
                : string.Empty;
            var wasUpdating = _updating; _updating = true; RefreshSimulationPlatforms(selected); _updating = wasUpdating;
            InvalidatePreview();
        };
        _simulationPlatform.ItemSelected += _ => InvalidatePreview();
        _simulatedDynamic.ValueChanged += _ => InvalidatePreview();
        _simulatedShadows.ValueChanged += _ => InvalidatePreview();
        _simulatedElapsed.ValueChanged += _ => InvalidatePreview();
        _skyMode.ItemSelected += _ => InvalidatePreview(); _panorama.TextChanged += _ => InvalidatePreview();
        _zenith.TextChanged += _ => InvalidatePreview(); _horizon.TextChanged += _ => InvalidatePreview();
        _ground.TextChanged += _ => InvalidatePreview(); _stars.ValueChanged += _ => InvalidatePreview();
        _clouds.ValueChanged += _ => InvalidatePreview(); _moon.ValueChanged += _ => InvalidatePreview();
        _energy.ValueChanged += _ => InvalidatePreview(); _transition.ValueChanged += _ => InvalidatePreview();
    }

    private void InvalidatePreview()
    {
        if (_updating || _draft is null) return;
        _session.CancelRenderingUx2EnvD(); _lastPreview = null; _canonicalPreviewReady = false; _applied = false;
        _apply.Disabled = true; _save.Disabled = true;
        _summary.Text = "Brouillon modifié · relancez l’aperçu sec avant application.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Warning);
        RenderDiff(null); RenderDiagnostics([]); UpdateTransactionButtons(keepApplyState: true);
        SetStatus("Aperçu précédent effacé · validation à relancer", false);
    }

    private void ResetControls()
    {
        _updating = true;
        _workflow.Select(0); _quality.Select(0); _forwardPlus.ButtonPressed = false;
        _perPixel.ButtonPressed = false; _materialInstances.ButtonPressed = false;
        _toneMapping.Select(0); _hdrOutput.ButtonPressed = false; _exposure.Value = 1;
        _whitePoint.Value = 6; _lut.Text = string.Empty; _budgets.SetValues([]);
        var visual = AreaVisualTuningProfile.CreateDefault();
        _brightness.Value = visual.Brightness; _contrastMultiplier.Value = visual.ContrastMultiplier;
        _saturationMultiplier.Value = visual.SaturationMultiplier;
        _ambientOcclusionIntensityMultiplier.Value = visual.AmbientOcclusionIntensityMultiplier;
        _ambientOcclusionPower.Value = visual.AmbientOcclusionPower;
        _sunPitch.Value = visual.DirectionalLightPitchDegrees; _sunYaw.Value = visual.DirectionalLightYawDegrees;
        _sunAngularDistance.Value = visual.DirectionalLightAngularDistance; _shadowBlur.Value = visual.ShadowBlur;
        RefreshSimulationPlatforms(string.Empty); _simulatedDynamic.Value = 0;
        _simulatedShadows.Value = 0; _simulatedElapsed.Value = 0;
        _skyMode.Select(0); _panorama.Text = string.Empty; _zenith.Text = "#4c78a8";
        _horizon.Text = "#e4b88a"; _ground.Text = "#38402f";
        _stars.Value = 0; _clouds.Value = 0; _moon.Value = 0; _energy.Value = 1; _transition.Value = 0;
        _updating = false; RenderDiff(null); RenderDiagnostics([]);
    }

    private void SetEditingEnabled(bool enabled)
    {
        _workflow.Disabled = !enabled; _quality.Disabled = !enabled; _forwardPlus.Disabled = !enabled;
        _perPixel.Disabled = !enabled; _materialInstances.Disabled = !enabled; _toneMapping.Disabled = !enabled;
        _hdrOutput.Disabled = !enabled; _exposure.Editable = enabled; _whitePoint.Editable = enabled;
        foreach (var control in new[] { _brightness, _contrastMultiplier, _saturationMultiplier,
                     _ambientOcclusionIntensityMultiplier, _ambientOcclusionPower, _sunPitch, _sunYaw,
                     _sunAngularDistance, _shadowBlur }) control.Editable = enabled;
        _lut.Editable = enabled; _budgets.SetEditingEnabled(enabled); _simulationPlatform.Disabled = !enabled;
        _simulatedDynamic.Editable = enabled; _simulatedShadows.Editable = enabled;
        _simulatedElapsed.Editable = enabled; _skyMode.Disabled = !enabled; _panorama.Editable = enabled;
        _zenith.Editable = enabled; _horizon.Editable = enabled; _ground.Editable = enabled;
        foreach (var control in new[] { _stars, _clouds, _moon, _energy, _transition }) control.Editable = enabled;
        _preview.Disabled = !enabled;
    }

    private void UpdateTransactionButtons(bool keepApplyState = false)
    {
        var history = _session.History; if (!keepApplyState) _apply.Disabled = true;
        _undo.Disabled = !_applied || history?.CanUndo != true;
        _redo.Disabled = _applied || history?.CanRedo != true;
        _save.Disabled = !_applied || !_session.IsDirty;
    }

    private void RenderDiff(StudioUx2EnvDPreview? preview)
    {
        ClearChildren(_diff);
        if (preview is null) { _diff.AddChild(StudioStyle.Label("Aucun aperçu frais.", 10, StudioStyle.Muted)); return; }
        if (!preview.Diff.HasChanges)
        {
            _diff.AddChild(StudioStyle.Label("Aucun changement sémantique.", 10, StudioStyle.Muted)); return;
        }
        AddDiffSection("PBR et qualité", preview.Diff.Pbr); AddDiffSection("HDR et couleur", preview.Diff.Color);
        AddDiffSection("Budgets", preview.Diff.Budgets); AddDiffSection("Ciel", preview.Diff.Sky);
    }

    private void AddDiffSection(string title, IReadOnlyList<string> entries)
    {
        if (entries.Count == 0) return;
        _diff.AddChild(StudioStyle.Label(title, 10, StudioStyle.Accent));
        foreach (var entry in entries) _diff.AddChild(StudioStyle.Label("• " + entry, 10, StudioStyle.Text, wrap: true));
    }

    private void RenderDiagnostics(IReadOnlyList<StudioUx2EnvDDiagnosticGroup> groups)
    {
        ClearChildren(_diagnostics);
        if (groups.Count == 0)
        {
            _diagnostics.AddChild(StudioStyle.Label("Les diagnostics apparaîtront après l’aperçu sec.",
                10, StudioStyle.Muted, wrap: true)); return;
        }
        foreach (var group in groups)
        {
            var color = group.Diagnostics.Any(item => item.Blocking)
                ? StudioStyle.Danger
                : group.Diagnostics.Count > 0 ? StudioStyle.Warning : StudioStyle.Success;
            _diagnostics.AddChild(StudioStyle.Label($"{group.Title} · {group.State}", 10, color));
            foreach (var diagnostic in group.Diagnostics)
                _diagnostics.AddChild(StudioStyle.Label("• " + diagnostic.Message, 10,
                    diagnostic.Blocking ? StudioStyle.Danger : StudioStyle.Warning, wrap: true));
        }
    }

    private static void ClearChildren(Node root)
    {
        foreach (var child in root.GetChildren()) child.QueueFree();
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message; _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(message, error);
    }

    private StudioUx2EnvDEvidenceSourceSnapshot CaptureSourceSnapshot()
    {
        var history = _sourceSession.History ?? throw new InvalidOperationException("No source history.");
        var project = _sourceSession.Project ?? throw new InvalidOperationException("No source project.");
        return new StudioUx2EnvDEvidenceSourceSnapshot(
            project, history, history.State.Revision, history.Journal.ToArray(), _sourceSession.IsDirty,
            history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(),
            _sourceSession.SelectedEntityId, _sourceSession.CaptureRenderingApprovalUx2EnvD());
    }

    private bool SourceSnapshotMatches(StudioUx2EnvDEvidenceSourceSnapshot? snapshot = null)
    {
        snapshot ??= _evidenceSourceSnapshot; if (snapshot is null) return false;
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) && ReferenceEquals(snapshot.History, history) &&
               history is not null && history.State.Revision == snapshot.Revision &&
               history.Journal.SequenceEqual(snapshot.Journal) && history.CanUndo == snapshot.CanUndo &&
               history.CanRedo == snapshot.CanRedo && _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureRenderingApprovalUx2EnvD() == snapshot.Approval;
    }

    private bool ReleaseIsolatedEvidenceSession()
    {
        var evidenceSession = _evidenceSession; var tempRoot = _evidenceTempRoot;
        _evidenceSession = null; _evidenceTempRoot = null; _evidenceSourceSnapshot = null; _session = _sourceSession;
        _evidenceUsesRealModuleData = false; _evidenceUsesCanonicalFallback = false;
        var released = true;
        try { evidenceSession?.Close(); }
        catch (Exception exception)
        {
            released = false;
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_D_RELEASE_FAILED type={exception.GetType().Name} message={exception.Message}");
        }
        var rootReleased = StudioSessionController.DeleteUx2EnvDTemporaryWorkspace(tempRoot);
        if (!rootReleased) _evidenceTempRoot = tempRoot;
        _evidenceReleaseSucceeded = released && rootReleased;
        return _evidenceReleaseSucceeded;
    }

    private void ResetEvidenceState()
    {
        _evidenceSession = null; _evidenceTempRoot = null; _evidenceSourceSnapshot = null;
        _evidenceUsesRealModuleData = false; _evidenceUsesCanonicalFallback = false;
    }

    private void UpdateDialogBounds()
    {
        var viewport = GetViewportRect().Size; if (viewport.X <= 0 || viewport.Y <= 0) return;
        var scale = viewport.X >= 3_200 ? 1.7f : viewport.X >= 2_300 ? 1.23f : 1f;
        var width = Math.Clamp((viewport.X - 52f) / scale, 1_080f, 1_620f);
        var height = Math.Clamp((viewport.Y - 42f) / scale, 760f, 1_060f);
        _dialog.Scale = Vector2.One * scale; _dialog.CustomMaximumSize = new Vector2(width, height);
        _dialog.Size = new Vector2(width, height);
        _dialog.Position = new Vector2((viewport.X - width * scale) / 2f,
            Math.Max(18f, (viewport.Y - height * scale) * .45f));
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && Visible) UpdateDialogBounds();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Close(); GetViewport().SetInputAsHandled();
        }
    }

    public override void _ExitTree()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null) Close();
    }

    private static SpinBox Number(double min, double max, double step, bool greater = false) =>
        StudioUx2EnvBControls.Number(min, max, step, greater);

    private static IEnumerable<string> DescendantVisibleText(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            switch (child)
            {
                case Label label: yield return label.Text; break;
                case LineEdit line: yield return line.Text; break;
                case TextEdit text: yield return text.Text; break;
                case OptionButton option:
                    for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
                    break;
                case Button button: yield return button.Text; break;
            }
            foreach (var descendant in DescendantVisibleText(child)) yield return descendant;
        }
    }

    private static bool ContainsUuid(string text) => System.Text.RegularExpressions.Regex.IsMatch(
        text, @"\b(?:[0-9a-f]{32}|[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\b",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase |
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);
}
