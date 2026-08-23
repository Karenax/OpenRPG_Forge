using System.Text.RegularExpressions;
using Forge.LiveDirector.Authoring;
using Forge.ModuleFormat.LiveDirector;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Operations;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioLiveDirectorWorkspaceWorkbenchUx9F : ColorRect
{
    private StudioSessionController _session;
    private StudioSessionController? _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private SourceSnapshotUx9F? _sourceSnapshot;
    private StudioUx9FLiveDirectorDraft? _draft;
    private StudioUx9FLiveDirectorPreview? _preview;
    private bool _explicitSelection;
    private bool _updating;
    private bool _canUndo;
    private bool _canRedo;
    private bool _evidenceResourcesReleased = true;
    private bool _sourceSessionRestored = true;

    private PanelContainer _dialog = null!;
    private OptionButton _moduleChoice = null!;
    private OptionButton _focusChoice = null!;
    private readonly List<Control> _focusPages = [];
    private SpinBox _sessionMinutes = null!;
    private CheckBox _requiredAtRuntime = null!;
    private LineEdit _catalogText = null!;
    private LineEdit _catalogPackage = null!;
    private LineEdit _catalogType = null!;
    private SpinBox _maximumCost = null!;
    private SpinBox _maximumInstances = null!;
    private SpinBox _spawnCount = null!;
    private OptionButton _placement = null!;
    private LineEdit _instanceId = null!;
    private SpinBox _x = null!;
    private SpinBox _y = null!;
    private SpinBox _z = null!;
    private SpinBox _spacing = null!;
    private LineEdit _chooserText = null!;
    private CheckBox _privacyOverride = null!;
    private CheckBox _approval = null!;
    private CheckBox _probeConfirmation = null!;
    private Label _previewSummary = null!;
    private Label _auditSummary = null!;
    private Label _status = null!;
    private Button _previewButton = null!;
    private Button _applyButton = null!;
    private Button _undoButton = null!;
    private Button _redoButton = null!;
    private Button _saveButton = null!;
    private Button _probeButton = null!;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? WorkspaceChanged;

    internal bool IsOpen => Visible;
    internal bool UsesIsolatedEvidence =>
        _evidenceSession is not null && ReferenceEquals(_session, _evidenceSession);
    internal bool HasExplicitSelection => _explicitSelection;
    internal bool PreviewReady => _preview is
    {
        Succeeded: true, CanApply: true, CanonicalDryRun: true,
        ClientRuntime: true, CreatorRuntime: true, SpawnRuntime: true,
        ChooserRuntime: true, FocusRuntime: true
    };
    internal bool EvidenceResourcesReleased => _evidenceResourcesReleased;
    internal bool SourceSessionRestored => _sourceSessionRestored;
    internal bool EvidenceSourceUntouched => SourceUnchangedUx9F();
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx9F(this));
            return Visible && text.Contains("ORF-DM-001", StringComparison.Ordinal) &&
                   text.Contains("ORF-DM-003", StringComparison.Ordinal) &&
                   text.Contains("ORF-DM-004", StringComparison.Ordinal) &&
                   text.Contains("ORF-DM-005", StringComparison.Ordinal) &&
                   text.Contains("ORF-DM-006", StringComparison.Ordinal) &&
                   text.Contains("MFA", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("allowlist", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("formation", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("confidentialité", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("lecture seule", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Tester localement", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucune connexion au serveur réel",
                       StringComparison.OrdinalIgnoreCase) && !ContainsUuidUx9F(text);
        }
    }

    internal StudioLiveDirectorWorkspaceWorkbenchUx9F(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        BuildUiUx9F();
    }

    internal void Open()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9F();
    }

    internal void Close()
    {
        if (_evidenceSession is not null)
        {
            CloseEvidence();
            return;
        }
        if (!Visible) return;
        Visible = false;
        _session.CancelLiveDirectorApprovalUx9F();
        ResetUx9F();
    }

    internal bool PrepareEvidence(string mode)
    {
        CloseEvidence();
        var sourceProject = _session.Project;
        if (sourceProject is null) return false;
        _sourceSession = _session;
        _sourceSnapshot = CaptureSourceUx9F(_sourceSession);
        _evidenceRoot = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux9-f-ui-{Guid.NewGuid():N}");
        _evidenceResourcesReleased = false;
        _sourceSessionRestored = false;
        var controller = new StudioSessionController();
        var baseline = LiveDirectorWorkspaceProfile.CreateDefault("evidence:ux9-f-ui");
        if (!ProjectSession.Create(_evidenceRoot, sourceProject).Succeeded ||
            !WorkspaceLiveDirectorStore.Save(_evidenceRoot, baseline).Succeeded ||
            !controller.Open(_evidenceRoot).Succeeded)
        {
            controller.Close();
            DeleteEvidenceUx9F();
            RestoreSourceSessionUx9F();
            return false;
        }
        _evidenceSession = controller;
        _session = controller;
        OpenCurrentUx9F();
        _moduleChoice.Select(1);
        SelectModuleUx9F(1);
        ConfigureEvidenceUx9F(mode);
        PreviewCurrentUx9F();
        var ready = UsesIsolatedEvidence && HasExplicitSelection && PreviewReady &&
                    ShowsHumanSurface && EvidenceSourceUntouched;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_F_EVIDENCE_{(ready ? "READY" : "FAILED")} " +
                 $"isolated={UsesIsolatedEvidence.ToString().ToLowerInvariant()} " +
                 $"selection={HasExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"preview={PreviewReady.ToString().ToLowerInvariant()} " +
                 $"human={ShowsHumanSurface.ToString().ToLowerInvariant()} " +
                 $"source_untouched={EvidenceSourceUntouched.ToString().ToLowerInvariant()}");
        if (!ready) CloseEvidence();
        return ready;
    }

    internal bool RunUiEvidence()
    {
        if (!UsesIsolatedEvidence || !PreviewReady || _evidenceRoot is null) return false;
        var draft = ReadDraftUx9F();
        var options = ReadProbeOptionsUx9F();
        if (draft is null || options is null) return false;
        var preview = _session.PreviewLiveDirectorUx9F(draft, options);
        var applied = preview.CanApply && _approval.ButtonPressed &&
                      _session.ApplyApprovedLiveDirectorUx9F(draft).Succeeded;
        var undo = applied && _session.UndoLiveDirectorUx9F().Succeeded;
        var redo = undo && _session.RedoLiveDirectorUx9F().Succeeded;
        var save = redo && _session.SaveLiveDirectorUx9F().Succeeded;
        var probed = save && _probeConfirmation.ButtonPressed &&
                     _session.RunLocalLiveDirectorUx9F(true, NetworkRole.Administrator,
                         options).Succeeded;
        var expected = _session.CurrentLiveDirectorUx9F;
        var reopened = new StudioSessionController();
        var reopenedOk = probed && expected is not null && reopened.Open(_evidenceRoot).Succeeded &&
                         reopened.CurrentLiveDirectorUx9F is { } actual &&
                         string.Equals(new LiveDirectorWorkspaceStore().Serialize(expected),
                             new LiveDirectorWorkspaceStore().Serialize(actual),
                             StringComparison.Ordinal);
        reopened.Close();
        RefreshAuditUx9F();
        return preview.CanonicalDryRun && applied && undo && redo && save && probed &&
               reopenedOk && _session.LiveDirectorProbeUx9F.ResourcesReleased &&
               EvidenceSourceUntouched;
    }

    internal void CloseEvidence()
    {
        if (_evidenceSession is not null)
        {
            _evidenceSession.Close();
            _evidenceSession = null;
        }
        Visible = false;
        ResetUx9F();
        DeleteEvidenceUx9F();
        RestoreSourceSessionUx9F();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9F();
        _moduleChoice.Select(1);
        SelectModuleUx9F(1);
    }

    internal void HideSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        Visible = false;
        ResetUx9F();
    }

    public override void _Ready()
    {
        GetViewport().SizeChanged += UpdateBoundsUx9F;
        UpdateBoundsUx9F();
    }

    private void BuildUiUx9F()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color("10121af2");
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        _dialog = StudioStyle.Panel("Ux9FLiveDirector", StudioStyle.Surface, 10, 1);
        AddChild(_dialog);
        var root = new VBoxContainer
        {
            Name = "Ux9FRoot",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 6);
        _dialog.AddChild(root);

        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(heading);
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(headingText);
        headingText.AddChild(StudioStyle.Label("Live Director · espace de direction", 20,
            StudioStyle.Text));
        headingText.AddChild(StudioStyle.Label(
            "Configurer un client séparé et éprouver Creator, spawn, Chooser et focus sous autorité serveur.",
            11, StudioStyle.Muted));
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);

        var routes = new GridContainer
        {
            Columns = 5,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        routes.AddThemeConstantOverride("h_separation", 7);
        root.AddChild(routes);
        routes.AddChild(RouteCardUx9F("ORF-DM-001", "Client séparé",
            "Rôle, MFA, expiration, révocation et audit."));
        routes.AddChild(RouteCardUx9F("ORF-DM-003", "Creator",
            "Allowlist, filtres et miniatures serveur."));
        routes.AddChild(RouteCardUx9F("ORF-DM-004", "Spawn spatial",
            "Single, formation, scatter, aperçu et compensation."));
        routes.AddChild(RouteCardUx9F("ORF-DM-005", "Chooser",
            "Zones, instances, diagnostics et confidentialité."));
        routes.AddChild(RouteCardUx9F("ORF-DM-006", "Focus",
            "Spectateur cross-zone en lecture seule."));
        root.AddChild(StudioStyle.Label(
            "Limite : sandbox mémoire isolée uniquement · aucune connexion au serveur réel, aucun spawn dans le module et aucune caméra de partie ouverte.",
            10, StudioStyle.Warning));

        _moduleChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _moduleChoice.ItemSelected += index => SelectModuleUx9F((int)index);
        root.AddChild(LabeledRowUx9F("Module explicite", _moduleChoice));
        _focusChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _focusChoice.AddItem("Client et palette Creator");
        _focusChoice.AddItem("Spawn spatial et compensation");
        _focusChoice.AddItem("Chooser et focus spectateur");
        _focusChoice.ItemSelected += index => SelectFocusUx9F((int)index);
        root.AddChild(LabeledRowUx9F("Workflow affiché", _focusChoice));

        var editor = StudioStyle.Panel("Ux9FEditor", StudioStyle.SurfaceRaised, 9, 1);
        editor.CustomMinimumSize = new Vector2(0, 325);
        editor.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.AddChild(editor);
        var editorStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        editorStack.AddThemeConstantOverride("separation", 7);
        editor.AddChild(editorStack);
        editorStack.AddChild(StudioStyle.Label("Édition directe et sandbox locale", 14,
            StudioStyle.Text));
        BuildClientCreatorPageUx9F(editorStack);
        BuildSpawnPageUx9F(editorStack);
        BuildChooserFocusPageUx9F(editorStack);

        var confirmations = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        confirmations.AddThemeConstantOverride("separation", 18);
        root.AddChild(confirmations);
        _approval = CheckUx9F(confirmations,
            "J’approuve ce seul aperçu sec pour la révision affichée");
        _probeConfirmation = CheckUx9F(confirmations,
            "Je confirme la sandbox locale en rôle Administrateur");

        var review = StudioStyle.Panel("Ux9FReview", StudioStyle.SurfaceRaised, 7, 1);
        review.CustomMinimumSize = new Vector2(0, 126);
        root.AddChild(review);
        var reviewStack = new VBoxContainer();
        review.AddChild(reviewStack);
        _previewSummary = StudioStyle.Label(
            "Prévisualisez le profil · aucun client ni objet instancié.", 10,
            StudioStyle.Muted);
        _previewSummary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        reviewStack.AddChild(_previewSummary);
        _auditSummary = StudioStyle.Label("Journal local · aucune entrée", 10,
            StudioStyle.Muted);
        _auditSummary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        reviewStack.AddChild(_auditSummary);
        _status = StudioStyle.Label("Sélectionnez explicitement le module.", 10,
            StudioStyle.Muted);
        root.AddChild(_status);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        root.AddChild(actions);
        _previewButton = ActionButtonUx9F(actions, "Prévisualiser", PreviewCurrentUx9F, 132,
            true);
        _applyButton = ActionButtonUx9F(actions, "Appliquer", ApplyCurrentUx9F, 104);
        _undoButton = ActionButtonUx9F(actions, "Annuler", UndoUx9F, 92);
        _redoButton = ActionButtonUx9F(actions, "Rétablir", RedoUx9F, 92);
        _saveButton = ActionButtonUx9F(actions, "Enregistrer", SaveUx9F, 112);
        _probeButton = ActionButtonUx9F(actions, "Tester localement", ProbeUx9F, 144);
        ConnectInvalidationUx9F();
        SelectFocusUx9F(0);
        UpdateActionsUx9F();
    }

    private void BuildClientCreatorPageUx9F(VBoxContainer parent)
    {
        var page = FocusPageUx9F(parent,
            "Client indépendant et palette Creator filtrée");
        var profileGrid = GridUx9F(page);
        _sessionMinutes = SpinUx9F(profileGrid, "Expiration de session (minutes)", 1, 1440, 1);
        _requiredAtRuntime = CheckUx9F(page,
            "Exiger ce profil au démarrage du runtime réseau");
        var filterGrid = GridUx9F(page);
        _catalogText = LineUx9F(filterGrid, "Recherche Creator", "gardien");
        _catalogPackage = LineUx9F(filterGrid, "Package autorisé", "crypt");
        _catalogType = LineUx9F(filterGrid, "Type", "creature");
        _maximumCost = SpinUx9F(filterGrid, "Coût maximal", 0, 1_000_000, 1);
        page.AddChild(StudioStyle.Label(
            "La sandbox applique allowlist serveur, zone, permission, package, texte, type, coût et miniature.",
            10, StudioStyle.Success));
    }

    private void BuildSpawnPageUx9F(VBoxContainer parent)
    {
        var page = FocusPageUx9F(parent,
            "Spawn borné avec identifiants stables et compensation sûre");
        var grid = GridUx9F(page);
        _maximumInstances = SpinUx9F(grid, "Limite du profil", 1, 1024, 1);
        _spawnCount = SpinUx9F(grid, "Quantité sandbox", 1, 1024, 1);
        grid.AddChild(StudioStyle.Label("Placement", 10, StudioStyle.Muted));
        _placement = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _placement.AddItem("single");
        _placement.AddItem("formation");
        _placement.AddItem("scatter");
        grid.AddChild(_placement);
        _instanceId = LineUx9F(grid, "Instance", "crypt-01");
        _x = SpinUx9F(grid, "Coordonnée X", -100_000, 100_000, 0.5);
        _y = SpinUx9F(grid, "Coordonnée Y", -100_000, 100_000, 0.5);
        _z = SpinUx9F(grid, "Coordonnée Z", -100_000, 100_000, 0.5);
        _spacing = SpinUx9F(grid, "Espacement", 0.1, 1000, 0.1);
        page.AddChild(StudioStyle.Label(
            "Prévisualiser conserve les mêmes identifiants ; Tester localement applique puis compense atomiquement dans la copie mémoire.",
            10, StudioStyle.Success));
    }

    private void BuildChooserFocusPageUx9F(VBoxContainer parent)
    {
        var page = FocusPageUx9F(parent,
            "Navigation hiérarchique et focus spectateur sans mutation");
        var grid = GridUx9F(page);
        _chooserText = LineUx9F(grid, "Filtre Chooser", "Place");
        _privacyOverride = CheckUx9F(page,
            "Autoriser explicitement l’override de confidentialité Administrateur");
        page.AddChild(StudioStyle.Label(
            "Le Chooser groupe zone puis instance, filtre le privé par défaut et expose ses diagnostics.",
            10, StudioStyle.Success));
        page.AddChild(StudioStyle.Label(
            "Le focus cible une zone distante en lecture seule ; aucune position, variable ou état du monde n’est modifié.",
            10, StudioStyle.Muted));
    }

    private VBoxContainer FocusPageUx9F(VBoxContainer parent, string title)
    {
        var page = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", 7);
        page.AddChild(StudioStyle.Label(title, 12, StudioStyle.Text));
        parent.AddChild(page);
        _focusPages.Add(page);
        return page;
    }

    private void OpenCurrentUx9F()
    {
        Visible = true;
        ResetUx9F();
        _moduleChoice.Clear();
        _moduleChoice.AddItem("Sélectionner un module");
        if (_session.Project is not null)
            _moduleChoice.AddItem(StudioSessionController.HumanUx8N(
                _session.Project.Module.DisplayName));
        _moduleChoice.Select(0);
        RefreshAuditUx9F();
        UpdateBoundsUx9F();
        UpdateActionsUx9F();
    }

    private void SelectModuleUx9F(int index)
    {
        _explicitSelection = index == 1 && _session.Project is not null;
        _session.CancelLiveDirectorApprovalUx9F();
        _preview = null;
        _approval.ButtonPressed = false;
        if (!_explicitSelection)
        {
            _draft = null;
            SetStatusUx9F("Sélectionnez explicitement le module.", false);
            UpdateActionsUx9F();
            return;
        }
        _draft = _session.CreateLiveDirectorDraftUx9F(explicitlySelected: true);
        if (_draft is null)
        {
            SetStatusUx9F(_session.LiveDirectorLoadMessageUx9F ??
                          "Le profil Live Director est indisponible.", true);
            UpdateActionsUx9F();
            return;
        }
        PopulateDraftUx9F(_draft);
        PopulateProbeOptionsUx9F(StudioUx9FProbeOptions.CreateDefault());
        SetStatusUx9F(_session.LiveDirectorFileMissingUx9F
            ? "Profil initial chargé en mémoire · aucun fichier créé avant Enregistrer."
            : "Live Director chargé · prévisualisez avant toute application.", false);
        UpdateActionsUx9F();
    }

    private void PopulateDraftUx9F(StudioUx9FLiveDirectorDraft draft)
    {
        _updating = true;
        _sessionMinutes.Value = draft.SessionMinutes;
        _maximumInstances.Value = draft.MaximumInstances;
        _requiredAtRuntime.ButtonPressed = draft.RequiredAtRuntime;
        _updating = false;
    }

    private void PopulateProbeOptionsUx9F(StudioUx9FProbeOptions options)
    {
        _updating = true;
        _catalogText.Text = options.CatalogText;
        _catalogPackage.Text = options.CatalogPackage;
        _catalogType.Text = options.CatalogType;
        _maximumCost.Value = options.MaximumCost;
        _spawnCount.Value = options.SpawnCount;
        var placementIndex = options.Placement switch
        {
            "single" => 0,
            "scatter" => 2,
            _ => 1
        };
        _placement.Select(placementIndex);
        _instanceId.Text = options.InstanceId;
        _x.Value = options.X;
        _y.Value = options.Y;
        _z.Value = options.Z;
        _spacing.Value = options.Spacing;
        _chooserText.Text = options.ChooserText;
        _privacyOverride.ButtonPressed = options.AllowPrivacyOverride;
        _updating = false;
    }

    private StudioUx9FLiveDirectorDraft? ReadDraftUx9F() => _draft is null ? null : _draft with
    {
        SessionMinutes = (int)_sessionMinutes.Value,
        MaximumInstances = (int)_maximumInstances.Value,
        RequiredAtRuntime = _requiredAtRuntime.ButtonPressed
    };

    private StudioUx9FProbeOptions? ReadProbeOptionsUx9F()
    {
        if (_draft is null || _placement.Selected < 0) return null;
        return new(_catalogText.Text, _catalogPackage.Text, _catalogType.Text,
            (int)_maximumCost.Value, (int)_spawnCount.Value,
            _placement.GetItemText(_placement.Selected), _instanceId.Text, _x.Value, _y.Value,
            _z.Value, _spacing.Value, _chooserText.Text, _privacyOverride.ButtonPressed);
    }

    private void PreviewCurrentUx9F()
    {
        var draft = ReadDraftUx9F();
        var options = ReadProbeOptionsUx9F();
        if (draft is null || options is null)
        {
            SetStatusUx9F("Sélectionnez d’abord le module.", true);
            return;
        }
        _preview = _session.PreviewLiveDirectorUx9F(draft, options);
        PopulatePreviewUx9F();
        SetStatusUx9F(_preview.Message, !_preview.Succeeded);
        UpdateActionsUx9F();
    }

    private void ApplyCurrentUx9F()
    {
        var draft = ReadDraftUx9F();
        if (draft is null || !_approval.ButtonPressed) return;
        var focus = _focusChoice.Selected;
        var options = ReadProbeOptionsUx9F();
        var result = _session.ApplyApprovedLiveDirectorUx9F(draft);
        SetStatusUx9F(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            WorkspaceChanged?.Invoke();
            SelectModuleUx9F(1);
            if (options is not null) PopulateProbeOptionsUx9F(options);
            _focusChoice.Select(focus);
            SelectFocusUx9F(focus);
        }
        UpdateActionsUx9F();
    }

    private void UndoUx9F()
    {
        var result = _session.UndoLiveDirectorUx9F();
        SetStatusUx9F(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = false;
            _canRedo = true;
            WorkspaceChanged?.Invoke();
            SelectModuleUx9F(1);
        }
        UpdateActionsUx9F();
    }

    private void RedoUx9F()
    {
        var result = _session.RedoLiveDirectorUx9F();
        SetStatusUx9F(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            WorkspaceChanged?.Invoke();
            SelectModuleUx9F(1);
        }
        UpdateActionsUx9F();
    }

    private void SaveUx9F()
    {
        var result = _session.SaveLiveDirectorUx9F();
        SetStatusUx9F(result.Message, !result.Succeeded);
        UpdateActionsUx9F();
    }

    private void ProbeUx9F()
    {
        var options = ReadProbeOptionsUx9F();
        if (options is null) return;
        var result = _session.RunLocalLiveDirectorUx9F(_probeConfirmation.ButtonPressed,
            NetworkRole.Administrator, options);
        SetStatusUx9F(result.Message, !result.Succeeded);
        RefreshAuditUx9F();
        PopulatePreviewUx9F();
        UpdateActionsUx9F();
    }

    private void PopulatePreviewUx9F()
    {
        if (_preview is null)
        {
            var probe = _session.LiveDirectorProbeUx9F;
            _previewSummary.Text = probe.AuditEntries == 0
                ? "Prévisualisez le profil · aucun client ni objet instancié."
                : $"Dernière sandbox · {probe.Status} · Creator {probe.CatalogMatches} résultat, " +
                  $"spawn {HumanProbeCodeUx9F(probe.SpawnApplyCode)}, " +
                  $"compensation {HumanProbeCodeUx9F(probe.CompensationCode)}, " +
                  $"Chooser public {probe.PublicChooserMatches}, " +
                  $"focus {HumanProbeCodeUx9F(probe.FocusCode)}.";
            return;
        }
        var changes = _preview.SemanticDiff.Count == 0 ? "Aucun changement" :
            string.Join(" · ", _preview.SemanticDiff);
        var probePreview = _preview.Probe;
        _previewSummary.Text =
            $"{_preview.Message}\nChangements · {changes}\n" +
            $"Client · MFA {(probePreview.SecondFactorEnforced ? "imposé" : "absent")}, session active, connexion isolée. " +
            $"Creator · {probePreview.CatalogMatches} résultat avec miniature. " +
            $"Spawn · aperçu local non appliqué, IDs {(probePreview.StableObjectIds ? "stables" : "instables")}, {probePreview.DistinctCoordinates} coordonnées distinctes. " +
            $"Chooser · privé {(probePreview.PrivateHidden ? "masqué" : "visible")}, override {(probePreview.PrivateOverrideAllowed ? "autorisé" : "refusé")}. " +
            $"Focus · aperçu spectateur, lecture seule {(probePreview.FocusReadOnly ? "confirmée" : "non confirmée")}.";
    }

    private void RefreshAuditUx9F()
    {
        var audit = _session.LiveDirectorAuditUx9F;
        var probe = _session.LiveDirectorProbeUx9F;
        _auditSummary.Text = audit.Count == 0
            ? "Journal local · aucune entrée"
            : $"Journal local · {audit.Count} entrées · sandbox : {probe.Status}, ressources " +
              $"{(probe.ResourcesReleased ? "libérées" : "encore actives")}.";
    }

    private void ConfigureEvidenceUx9F(string mode)
    {
        if (_draft is null) return;
        _updating = true;
        _sessionMinutes.Value = 45;
        _maximumInstances.Value = 8;
        _requiredAtRuntime.ButtonPressed = true;
        PopulateProbeOptionsUx9F(StudioUx9FProbeOptions.CreateDefault());
        _approval.ButtonPressed = true;
        _probeConfirmation.ButtonPressed = true;
        var focus = mode.Contains("spawn", StringComparison.OrdinalIgnoreCase) ? 1 :
            mode.Contains("chooser", StringComparison.OrdinalIgnoreCase) ||
            mode.Contains("focus", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
        _focusChoice.Select(focus);
        SelectFocusUx9F(focus);
        _updating = false;
        InvalidateUx9F(cancelApproval: false, preserveApproval: true);
    }

    private void SelectFocusUx9F(int index)
    {
        for (var page = 0; page < _focusPages.Count; page++)
            _focusPages[page].Visible = page == index;
    }

    private void ConnectInvalidationUx9F()
    {
        foreach (var spin in new[]
                 {
                     _sessionMinutes, _maximumCost, _maximumInstances, _spawnCount,
                     _x, _y, _z, _spacing
                 })
            spin.ValueChanged += _ => InvalidateUx9F();
        foreach (var line in new[]
                 {
                     _catalogText, _catalogPackage, _catalogType, _instanceId, _chooserText
                 })
            line.TextChanged += _ => InvalidateUx9F();
        _placement.ItemSelected += _ => InvalidateUx9F();
        _requiredAtRuntime.Toggled += _ => InvalidateUx9F();
        _privacyOverride.Toggled += _ => InvalidateUx9F();
        _approval.Toggled += _ => UpdateActionsUx9F();
        _probeConfirmation.Toggled += _ => UpdateActionsUx9F();
    }

    private void InvalidateUx9F(bool cancelApproval = true, bool preserveApproval = false)
    {
        if (_updating) return;
        _preview = null;
        if (cancelApproval) _session.CancelLiveDirectorApprovalUx9F();
        if (!preserveApproval) _approval.ButtonPressed = false;
        PopulatePreviewUx9F();
        UpdateActionsUx9F();
    }

    private void UpdateActionsUx9F()
    {
        var selected = _explicitSelection && _draft is not null;
        _previewButton.Disabled = !selected;
        _applyButton.Disabled = !selected || !PreviewReady || !_approval.ButtonPressed;
        _undoButton.Disabled = !_canUndo;
        _redoButton.Disabled = !_canRedo;
        _saveButton.Disabled = !_session.LiveDirectorHasUnsavedChangesUx9F;
        _probeButton.Disabled = !selected || PreviewReady ||
                                _session.LiveDirectorHasUnsavedChangesUx9F ||
                                !_probeConfirmation.ButtonPressed;
    }

    private void ResetUx9F()
    {
        _draft = null;
        _preview = null;
        _explicitSelection = false;
        _canUndo = false;
        _canRedo = false;
        _updating = true;
        if (_approval is not null) _approval.ButtonPressed = false;
        if (_probeConfirmation is not null) _probeConfirmation.ButtonPressed = false;
        _updating = false;
        if (_previewSummary is not null) PopulatePreviewUx9F();
    }

    private SourceSnapshotUx9F CaptureSourceUx9F(StudioSessionController session) => new(
        session.Project, session.History, session.History?.State.Revision,
        session.History?.Journal.ToArray() ?? [], session.IsDirty, session.ActiveAreaId,
        session.SelectedEntityIds.ToArray(), session.SelectedEntityId,
        session.CaptureLiveDirectorApprovalUx9F());

    private bool SourceUnchangedUx9F()
    {
        var source = _sourceSession;
        var snapshot = _sourceSnapshot;
        if (source is null || snapshot is null) return true;
        return ReferenceEquals(source.Project, snapshot.Project) &&
               ReferenceEquals(source.History, snapshot.History) &&
               source.History?.State.Revision == snapshot.Revision &&
               (source.History?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               source.IsDirty == snapshot.Dirty && source.ActiveAreaId == snapshot.Area &&
               source.SelectedEntityIds.SequenceEqual(snapshot.Selection) &&
               source.SelectedEntityId == snapshot.Selected &&
               source.CaptureLiveDirectorApprovalUx9F() == snapshot.Approval;
    }

    private void RestoreSourceSessionUx9F()
    {
        if (_sourceSession is not null) _session = _sourceSession;
        _sourceSession = null;
        _sourceSnapshot = null;
        _sourceSessionRestored = true;
    }

    private void DeleteEvidenceUx9F()
    {
        var root = _evidenceRoot;
        _evidenceRoot = null;
        if (string.IsNullOrWhiteSpace(root))
        {
            _evidenceResourcesReleased = true;
            return;
        }
        try
        {
            var full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            if (!string.Equals(Path.GetDirectoryName(full)?.TrimEnd(Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar), temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux9-f-ui-",
                    StringComparison.Ordinal))
            {
                _evidenceResourcesReleased = false;
                return;
            }
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            _evidenceResourcesReleased = !Directory.Exists(full);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _evidenceResourcesReleased = false;
            SetStatusUx9F($"La copie temporaire n’a pas pu être libérée : {exception.Message}",
                true);
        }
    }

    private void UpdateBoundsUx9F()
    {
        if (_dialog is null) return;
        var size = GetViewportRect().Size;
        _dialog.Position = new Vector2(24, 18);
        _dialog.Size = new Vector2(Math.Max(960, size.X - 48), Math.Max(700, size.Y - 36));
    }

    private void SetStatusUx9F(string message, bool error)
    {
        var text = StudioSessionController.HumanUx8N(message);
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger :
            StudioStyle.Muted);
        StatusChanged?.Invoke(text, error);
    }

    private static Control RouteCardUx9F(string route, string title, string detail)
    {
        var panel = StudioStyle.Panel("Ux9F" + route.Replace("-", string.Empty),
            StudioStyle.SurfaceRaised, 6, 1);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 2);
        panel.AddChild(stack);
        stack.AddChild(StudioStyle.Label($"{route} · {title}", 10, StudioStyle.Text));
        var label = StudioStyle.Label(detail, 9, StudioStyle.Muted);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        stack.AddChild(label);
        return panel;
    }

    private static Control LabeledRowUx9F(string label, Control control)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var title = StudioStyle.Label(label, 10, StudioStyle.Muted);
        title.CustomMinimumSize = new Vector2(230, 0);
        row.AddChild(title);
        row.AddChild(control);
        return row;
    }

    private static GridContainer GridUx9F(Control parent)
    {
        var grid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 7);
        parent.AddChild(grid);
        return grid;
    }

    private static SpinBox SpinUx9F(GridContainer parent, string label, double min, double max,
        double step)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var value = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            AllowGreater = false,
            AllowLesser = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(300, 34)
        };
        parent.AddChild(value);
        return value;
    }

    private static LineEdit LineUx9F(GridContainer parent, string label, string placeholder)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var value = new LineEdit
        {
            PlaceholderText = placeholder,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(300, 34)
        };
        parent.AddChild(value);
        return value;
    }

    private static CheckBox CheckUx9F(Control parent, string text)
    {
        var check = new CheckBox { Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        parent.AddChild(check);
        return check;
    }

    private static Button ActionButtonUx9F(HBoxContainer parent, string text, Action action,
        float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private static IEnumerable<string> DescendantTextUx9F(Node node)
    {
        if (node is Label label) yield return label.Text;
        if (node is Button button) yield return button.Text;
        if (node is CheckBox check) yield return check.Text;
        if (node is LineEdit line) yield return line.Text;
        if (node is OptionButton option)
            for (var index = 0; index < option.ItemCount; index++)
                yield return option.GetItemText(index);
        foreach (var child in node.GetChildren())
            foreach (var value in DescendantTextUx9F(child))
                yield return value;
    }

    private static bool ContainsUuidUx9F(string text) => Regex.IsMatch(text,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b",
        RegexOptions.CultureInvariant);

    private static string HumanProbeCodeUx9F(string code) => code switch
    {
        "LIVE_SPAWN_APPLIED" => "appliqué",
        "LIVE_SPAWN_NOT_APPLIED" => "non appliqué",
        "LIVE_SPAWN_COMPENSATED" => "compensée",
        "LIVE_SPAWN_NOT_COMPENSATED" => "non requise",
        "LIVE_ACTION_APPLIED" => "appliqué en lecture seule",
        "LIVE_ACTION_PREVIEW" => "prévisualisé en lecture seule",
        _ => code
    };

    private sealed record SourceSnapshotUx9F(
        Forge.Domain.Projects.ProjectDefinition? Project,
        Forge.Application.Commands.ProjectCommandHistory? History,
        Forge.Application.Projects.ProjectRevision? Revision,
        IReadOnlyList<Forge.Application.Commands.CommandHistoryJournalEntry> Journal,
        bool Dirty,
        Forge.Domain.Areas.AreaId? Area,
        IReadOnlyList<Forge.Domain.Entities.EntityId> Selection,
        Forge.Domain.Entities.EntityId? Selected,
        StudioUx9FApprovalSnapshot Approval);
}
