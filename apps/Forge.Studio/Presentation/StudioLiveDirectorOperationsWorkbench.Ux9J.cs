using System.Text.RegularExpressions;
using Forge.LiveDirector.Authoring;
using Forge.ModuleFormat.LiveDirector;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Operations;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioLiveDirectorOperationsWorkbenchUx9J : ColorRect
{
    private StudioSessionController _session;
    private StudioSessionController? _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private SourceSnapshotUx9J? _sourceSnapshot;
    private StudioUx9JOperationsDraft? _draft;
    private StudioUx9JOperationsPreview? _preview;
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
    private CheckBox _requiredAtRuntime = null!;
    private LineEdit _searchText = null!;
    private OptionButton _scriptKind = null!;
    private LineEdit _scriptSource = null!;
    private LineEdit _scriptSignature = null!;
    private SpinBox _scriptBudget = null!;
    private LineEdit _variableName = null!;
    private OptionButton _variableKind = null!;
    private LineEdit _variableValue = null!;
    private OptionButton _visibilityMode = null!;
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
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                          ReferenceEquals(_session, _evidenceSession);
    internal bool HasExplicitSelection => _explicitSelection;
    internal bool PreviewReady => _preview is
    {
        Succeeded: true, CanApply: true, CanonicalDryRun: true,
        SearchRuntime: true, ScriptRuntime: true, VariableRuntime: true,
        SceneRuntime: true, VisibilityRuntime: true
    };
    internal bool EvidenceResourcesReleased => _evidenceResourcesReleased;
    internal bool SourceSessionRestored => _sourceSessionRestored;
    internal bool EvidenceSourceUntouched => SourceUnchangedUx9J();
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx9J(this));
            return Visible && text.Contains("ORF-DM-027", StringComparison.Ordinal) &&
                   text.Contains("ORF-DM-028", StringComparison.Ordinal) &&
                   text.Contains("ORF-DM-029", StringComparison.Ordinal) &&
                   text.Contains("ORF-DM-032", StringComparison.Ordinal) &&
                   text.Contains("ORF-DM-033", StringComparison.Ordinal) &&
                   text.Contains("focus visuel", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("ForgeScript serveur", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("état persistant", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("rollback obligatoire", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("overlay viewport", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Tester localement", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun monde, script ou joueur réel",
                       StringComparison.OrdinalIgnoreCase) && !ContainsUuidUx9J(text);
        }
    }

    internal StudioLiveDirectorOperationsWorkbenchUx9J(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        BuildUiUx9J();
    }

    internal void Open()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9J();
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
        _session.CancelLiveOperationsApprovalUx9J();
        ResetUx9J();
    }

    internal bool PrepareEvidence(string mode)
    {
        CloseEvidence();
        var sourceProject = _session.Project;
        if (sourceProject is null) return false;
        _sourceSession = _session;
        _sourceSnapshot = CaptureSourceUx9J(_sourceSession);
        _evidenceRoot = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux9-j-ui-{Guid.NewGuid():N}");
        _evidenceResourcesReleased = false;
        _sourceSessionRestored = false;
        var controller = new StudioSessionController();
        var baseline = LiveDirectorOperationsProfile.CreateDefault("evidence:ux9-j-ui");
        if (!ProjectSession.Create(_evidenceRoot, sourceProject).Succeeded ||
            !WorkspaceLiveDirectorOperationsStore.Save(_evidenceRoot, baseline).Succeeded ||
            !controller.Open(_evidenceRoot).Succeeded)
        {
            controller.Close();
            DeleteEvidenceUx9J();
            RestoreSourceSessionUx9J();
            return false;
        }
        _evidenceSession = controller;
        _session = controller;
        OpenCurrentUx9J();
        _moduleChoice.Select(1);
        SelectModuleUx9J(1);
        ConfigureEvidenceUx9J(mode);
        PreviewCurrentUx9J();
        var ready = UsesIsolatedEvidence && HasExplicitSelection && PreviewReady &&
                    ShowsHumanSurface && EvidenceSourceUntouched;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_J_EVIDENCE_{(ready ? "READY" : "FAILED")} " +
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
        var draft = ReadDraftUx9J();
        var options = ReadOptionsUx9J();
        if (draft is null || options is null) return false;
        var preview = _session.PreviewLiveOperationsUx9J(draft, options);
        var applied = preview.CanApply && _approval.ButtonPressed &&
                      _session.ApplyApprovedLiveOperationsUx9J(draft).Succeeded;
        var undo = applied && _session.UndoLiveOperationsUx9J().Succeeded;
        var redo = undo && _session.RedoLiveOperationsUx9J().Succeeded;
        var save = redo && _session.SaveLiveOperationsUx9J().Succeeded;
        var probed = save && _probeConfirmation.ButtonPressed &&
                     _session.RunLocalLiveOperationsUx9J(true, NetworkRole.Administrator,
                         options).Succeeded;
        var expected = _session.CurrentLiveOperationsUx9J;
        var reopened = new StudioSessionController();
        var reopenedOk = probed && expected is not null && reopened.Open(_evidenceRoot).Succeeded &&
                         reopened.CurrentLiveOperationsUx9J is { } actual &&
                         new LiveDirectorOperationsStore().Serialize(expected) ==
                         new LiveDirectorOperationsStore().Serialize(actual);
        reopened.Close();
        RefreshAuditUx9J();
        return preview.CanonicalDryRun && applied && undo && redo && save && probed &&
               reopenedOk && _session.LiveOperationsProbeUx9J.ResourcesReleased &&
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
        ResetUx9J();
        DeleteEvidenceUx9J();
        RestoreSourceSessionUx9J();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9J();
        _moduleChoice.Select(1);
        SelectModuleUx9J(1);
    }

    internal void HideSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        Visible = false;
        ResetUx9J();
    }

    public override void _Ready()
    {
        GetViewport().SizeChanged += UpdateBoundsUx9J;
        UpdateBoundsUx9J();
    }

    public override void _ExitTree() => CloseEvidence();

    private void BuildUiUx9J()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color("10121af2");
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        _dialog = StudioStyle.Panel("Ux9JLiveOperations", StudioStyle.Surface, 10, 1);
        AddChild(_dialog);
        var root = new VBoxContainer
        {
            Name = "Ux9JRoot",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 6);
        _dialog.AddChild(root);

        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(heading);
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(headingText);
        headingText.AddChild(StudioStyle.Label("Live Director · opérations gouvernées", 20,
            StudioStyle.Text));
        headingText.AddChild(StudioStyle.Label(
            "Rechercher, prévisualiser, exécuter puis restaurer une sandbox locale.", 11,
            StudioStyle.Muted));
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);

        var routes = new GridContainer { Columns = 5, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        routes.AddThemeConstantOverride("h_separation", 7);
        root.AddChild(routes);
        routes.AddChild(RouteCardUx9J("ORF-DM-027", "Rechercher et avancer",
            "Index, requête sauvée, lien stable et focus visuel."));
        routes.AddChild(RouteCardUx9J("ORF-DM-028", "Exécuter ForgeScript",
            "Commande enregistrée ou snippet signé sur ForgeScript serveur."));
        routes.AddChild(RouteCardUx9J("ORF-DM-029", "Modifier une variable",
            "Quatre types, historique et état persistant."));
        routes.AddChild(RouteCardUx9J("ORF-DM-032", "Lancer une scène",
            "Cinq facettes, adaptateurs et rollback obligatoire."));
        routes.AddChild(RouteCardUx9J("ORF-DM-033", "Projeter la visibilité",
            "Vue MJ ou joueur et overlay viewport."));

        _moduleChoice = ChoiceUx9J(null, "Sélection", "Sélectionner un module");
        _moduleChoice.ItemSelected += index => SelectModuleUx9J((int)index);
        root.AddChild(LabeledRowUx9J("Module explicitement ciblé", _moduleChoice));
        _focusChoice = ChoiceUx9J(null, "Workflow", "Recherche et ForgeScript",
            "Variables et scène", "Visibilité MJ");
        _focusChoice.ItemSelected += index => SelectFocusUx9J((int)index);
        root.AddChild(LabeledRowUx9J("Workflow direct", _focusChoice));

        var editor = StudioStyle.Panel("Ux9JEditor", StudioStyle.SurfaceRaised, 9, 1);
        editor.CustomMinimumSize = new Vector2(0, 270);
        editor.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.AddChild(editor);
        var stack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        stack.AddThemeConstantOverride("separation", 7);
        editor.AddChild(stack);
        BuildSearchScriptPageUx9J(stack);
        BuildVariableScenePageUx9J(stack);
        BuildVisibilityPageUx9J(stack);

        var decisions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        decisions.AddThemeConstantOverride("separation", 18);
        root.AddChild(decisions);
        _approval = CheckUx9J(decisions,
            "J’approuve ce brouillon et cette révision après l’aperçu sec");
        _probeConfirmation = CheckUx9J(decisions,
            "Je confirme la sandbox locale restaurée en fin d’exécution");

        var review = StudioStyle.Panel("Ux9JReview", StudioStyle.SurfaceRaised, 7, 1);
        review.CustomMinimumSize = new Vector2(0, 102);
        root.AddChild(review);
        var reviewStack = new VBoxContainer();
        review.AddChild(reviewStack);
        _previewSummary = StudioStyle.Label("Aucun aperçu sec.", 10, StudioStyle.Text,
            wrap: true);
        reviewStack.AddChild(_previewSummary);
        _auditSummary = StudioStyle.Label("Journal local · aucune entrée", 10,
            StudioStyle.Muted, wrap: true);
        reviewStack.AddChild(_auditSummary);
        _status = StudioStyle.Label(
            "Sélectionnez explicitement le module avant d’exécuter une opération.", 10,
            StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        root.AddChild(StudioStyle.Label(
            "Sandbox isolée : aucun monde, script ou joueur réel, aucun serveur et aucun fichier externe ne sont modifiés.",
            10, StudioStyle.Warning, wrap: true));

        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 7);
        root.AddChild(actions);
        _previewButton = ActionButtonUx9J(actions, "Prévisualiser", PreviewCurrentUx9J, 122,
            primary: true);
        _applyButton = ActionButtonUx9J(actions, "Appliquer", ApplyCurrentUx9J, 104);
        _undoButton = ActionButtonUx9J(actions, "Annuler", UndoUx9J, 92);
        _redoButton = ActionButtonUx9J(actions, "Rétablir", RedoUx9J, 92);
        _saveButton = ActionButtonUx9J(actions, "Enregistrer", SaveUx9J, 112);
        actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _probeButton = ActionButtonUx9J(actions, "Tester localement", ProbeUx9J, 148);

        ConnectInvalidationUx9J();
        SelectFocusUx9J(0);
        UpdateActionsUx9J();
    }

    private void BuildSearchScriptPageUx9J(VBoxContainer parent)
    {
        var page = FocusPageUx9J(parent,
            "Recherche et ForgeScript · focus viewport puis exécution autoritaire bornée");
        var grid = GridUx9J(page);
        _searchText = LineUx9J(grid, "Nom ou tag", "Garde");
        _scriptKind = ChoiceUx9J(grid, "Type de script", "Commande enregistrée",
            "Snippet signé");
        _scriptSource = LineUx9J(grid, "Commande ou source", "announce");
        _scriptSignature = LineUx9J(grid, "Signature du snippet", "valid");
        _scriptBudget = SpinUx9J(grid, "Budget d’instructions", 1, 10_000, 1);
        _requiredAtRuntime = CheckUx9J(page,
            "Exiger ce profil au démarrage du runtime Live Director");
        page.AddChild(StudioStyle.Label(
            "Suivant parcourt les résultats déterministes ; shell, fichiers, réseau, processus et threads restent interdits.",
            10, StudioStyle.Success, wrap: true));
    }

    private void BuildVariableScenePageUx9J(VBoxContainer parent)
    {
        var page = FocusPageUx9J(parent,
            "Variables et scène · état typé persistable puis compensation multi-facettes");
        var grid = GridUx9J(page);
        _variableName = LineUx9J(grid, "Variable", "tension");
        _variableKind = ChoiceUx9J(grid, "Type", "Booléen", "Nombre", "Énumération",
            "Texte");
        _variableValue = LineUx9J(grid, "Valeur", "crisis");
        page.AddChild(StudioStyle.Label(
            "Scène Tempête sur la Crypte · spawns, dialogue, musique, météo et triggers · rollback obligatoire après le test.",
            10, StudioStyle.Success, wrap: true));
    }

    private void BuildVisibilityPageUx9J(VBoxContainer parent)
    {
        var page = FocusPageUx9J(parent,
            "Visibilité MJ · comparer l’omniscience à la projection d’un joueur sélectionné");
        var grid = GridUx9J(page);
        _visibilityMode = ChoiceUx9J(grid, "Projection", "Vue omnisciente MJ",
            "Vue du joueur sélectionné");
        page.AddChild(StudioStyle.Label(
            "L’overlay viewport superpose objets cachés, secrets, navigation et rencontres sans modifier le brouillard autoritaire.",
            10, StudioStyle.Success, wrap: true));
    }

    private VBoxContainer FocusPageUx9J(VBoxContainer parent, string title)
    {
        var page = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", 7);
        page.AddChild(StudioStyle.Label(title, 12, StudioStyle.Text));
        parent.AddChild(page);
        _focusPages.Add(page);
        return page;
    }

    private void OpenCurrentUx9J()
    {
        Visible = true;
        ResetUx9J();
        _moduleChoice.Clear();
        _moduleChoice.AddItem("Sélectionner un module");
        if (_session.Project is not null)
            _moduleChoice.AddItem(StudioSessionController.HumanUx8N(
                _session.Project.Module.DisplayName));
        _moduleChoice.Select(0);
        RefreshAuditUx9J();
        UpdateBoundsUx9J();
        UpdateActionsUx9J();
    }

    private void SelectModuleUx9J(int index)
    {
        _explicitSelection = index == 1 && _session.Project is not null;
        _session.CancelLiveOperationsApprovalUx9J();
        _preview = null;
        _approval.ButtonPressed = false;
        if (!_explicitSelection)
        {
            _draft = null;
            SetStatusUx9J("Sélectionnez explicitement le module.", false);
            UpdateActionsUx9J();
            return;
        }
        _draft = _session.CreateLiveOperationsDraftUx9J(explicitlySelected: true);
        if (_draft is null)
        {
            SetStatusUx9J(_session.LiveOperationsLoadMessageUx9J ??
                          "Le profil d’opérations est indisponible.", true);
            UpdateActionsUx9J();
            return;
        }
        PopulateDraftUx9J(_draft);
        PopulateOptionsUx9J(StudioUx9JOperationsProbeOptions.CreateDefault());
        SetStatusUx9J(_session.LiveOperationsFileMissingUx9J
            ? "Profil initial chargé en mémoire · aucun fichier créé avant Enregistrer."
            : "Profil d’opérations chargé · prévisualisez avant toute application.", false);
        UpdateActionsUx9J();
    }

    private void PopulateDraftUx9J(StudioUx9JOperationsDraft draft)
    {
        _updating = true;
        _requiredAtRuntime.ButtonPressed = draft.RequiredAtRuntime;
        _updating = false;
    }

    private void PopulateOptionsUx9J(StudioUx9JOperationsProbeOptions value)
    {
        _updating = true;
        _searchText.Text = value.SearchText;
        _scriptKind.Select(Array.IndexOf(ScriptKindValues, value.ScriptKind));
        _scriptSource.Text = value.ScriptSource;
        _scriptSignature.Text = value.ScriptSignature ?? string.Empty;
        _scriptBudget.Value = value.ScriptBudget;
        _variableName.Text = value.VariableName;
        _variableKind.Select(Array.IndexOf(VariableKindValues, value.VariableKind));
        _variableValue.Text = value.VariableValue;
        _visibilityMode.Select(Array.IndexOf(VisibilityModeValues, value.VisibilityMode));
        _updating = false;
    }

    private StudioUx9JOperationsDraft? ReadDraftUx9J() => _draft is null ? null : _draft with
    {
        RequiredAtRuntime = _requiredAtRuntime.ButtonPressed
    };

    private StudioUx9JOperationsProbeOptions? ReadOptionsUx9J()
    {
        if (!_explicitSelection) return null;
        var signature = _scriptSignature.Text.Trim();
        return new(_searchText.Text.Trim(), ScriptKindValues[_scriptKind.Selected],
            _scriptSource.Text.Trim(), string.IsNullOrWhiteSpace(signature) ? null : signature,
            (int)_scriptBudget.Value, _variableName.Text.Trim(),
            VariableKindValues[_variableKind.Selected], _variableValue.Text.Trim(),
            VisibilityModeValues[_visibilityMode.Selected]);
    }

    private void PreviewCurrentUx9J()
    {
        var draft = ReadDraftUx9J();
        var options = ReadOptionsUx9J();
        if (draft is null || options is null)
        {
            SetStatusUx9J("Sélectionnez d’abord le module.", true);
            return;
        }
        _preview = _session.PreviewLiveOperationsUx9J(draft, options);
        PopulatePreviewUx9J();
        SetStatusUx9J(_preview.Message, !_preview.Succeeded);
        UpdateActionsUx9J();
    }

    private void ApplyCurrentUx9J()
    {
        var draft = ReadDraftUx9J();
        if (draft is null) return;
        var focus = _focusChoice.Selected;
        var result = _session.ApplyApprovedLiveOperationsUx9J(draft);
        SetStatusUx9J(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            SelectModuleUx9J(1);
            SelectFocusUx9J(focus);
            WorkspaceChanged?.Invoke();
        }
        UpdateActionsUx9J();
    }

    private void UndoUx9J()
    {
        var result = _session.UndoLiveOperationsUx9J();
        SetStatusUx9J(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = false;
            _canRedo = true;
            SelectModuleUx9J(1);
            WorkspaceChanged?.Invoke();
        }
        UpdateActionsUx9J();
    }

    private void RedoUx9J()
    {
        var result = _session.RedoLiveOperationsUx9J();
        SetStatusUx9J(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            SelectModuleUx9J(1);
            WorkspaceChanged?.Invoke();
        }
        UpdateActionsUx9J();
    }

    private void SaveUx9J()
    {
        var result = _session.SaveLiveOperationsUx9J();
        SetStatusUx9J(result.Message, !result.Succeeded);
        UpdateActionsUx9J();
    }

    private void ProbeUx9J()
    {
        var options = ReadOptionsUx9J();
        if (options is null) return;
        var result = _session.RunLocalLiveOperationsUx9J(_probeConfirmation.ButtonPressed,
            NetworkRole.Administrator, options);
        SetStatusUx9J(result.Message, !result.Succeeded);
        RefreshAuditUx9J();
        PopulatePreviewUx9J();
        UpdateActionsUx9J();
    }

    private void PopulatePreviewUx9J()
    {
        var probe = _session.LiveOperationsProbeUx9J;
        _previewSummary.Text = _preview is null
            ? $"Sandbox · {probe.Status}."
            : $"{_preview.Message} · {string.Join(" · ", _preview.SemanticDiff)}\n" +
              $"Recherche {probe.SearchMatches} résultat(s), script {HumanCodeUx9J(probe.ScriptCode)}, " +
              $"variable {HumanCodeUx9J(probe.VariableCode)} ({probe.VariableKind}), " +
              $"scène {HumanCodeUx9J(probe.SceneCode)} ({probe.SceneFacets} facettes), " +
              $"visibilité {HumanVisibilityUx9J(probe.VisibilityMode)} ({probe.VisibilityLayers} couches).";
    }

    private void RefreshAuditUx9J()
    {
        var audit = _session.LiveOperationsAuditUx9J;
        var probe = _session.LiveOperationsProbeUx9J;
        _auditSummary.Text = audit.Count == 0
            ? "Journal local · aucune entrée"
            : $"Journal local · {audit.Count} entrée(s) · état {(probe.Compensated ? "restauré" : "actif")} · " +
              $"ressources {(probe.ResourcesReleased ? "libérées" : "à vérifier")}.";
    }

    private void ConfigureEvidenceUx9J(string mode)
    {
        _updating = true;
        _requiredAtRuntime.ButtonPressed = true;
        PopulateOptionsUx9J(StudioUx9JOperationsProbeOptions.CreateDefault());
        _approval.ButtonPressed = true;
        _probeConfirmation.ButtonPressed = true;
        _updating = false;
        var focus = mode.Contains("variable", StringComparison.OrdinalIgnoreCase) ||
                    mode.Contains("scene", StringComparison.OrdinalIgnoreCase) ? 1 :
            mode.Contains("visibility", StringComparison.OrdinalIgnoreCase) ||
            mode.Contains("fog", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
        _focusChoice.Select(focus);
        SelectFocusUx9J(focus);
    }

    private void SelectFocusUx9J(int index)
    {
        for (var i = 0; i < _focusPages.Count; i++) _focusPages[i].Visible = i == index;
    }

    private void ConnectInvalidationUx9J()
    {
        foreach (var line in new[]
                 {
                     _searchText, _scriptSource, _scriptSignature, _variableName, _variableValue
                 })
            line.TextChanged += _ => InvalidateUx9J();
        _scriptBudget.ValueChanged += _ => InvalidateUx9J();
        foreach (var option in new[] { _scriptKind, _variableKind, _visibilityMode })
            option.ItemSelected += _ => InvalidateUx9J();
        _requiredAtRuntime.Toggled += _ => InvalidateUx9J();
        _approval.Toggled += _ => UpdateActionsUx9J();
        _probeConfirmation.Toggled += _ => UpdateActionsUx9J();
    }

    private void InvalidateUx9J()
    {
        if (_updating) return;
        _preview = null;
        _session.CancelLiveOperationsApprovalUx9J();
        _approval.ButtonPressed = false;
        PopulatePreviewUx9J();
        UpdateActionsUx9J();
    }

    private void UpdateActionsUx9J()
    {
        var selected = _explicitSelection && _draft is not null;
        _previewButton.Disabled = !selected;
        _applyButton.Disabled = !selected || _preview is not { CanApply: true } ||
                                !_approval.ButtonPressed;
        _undoButton.Disabled = !_canUndo;
        _redoButton.Disabled = !_canRedo;
        _saveButton.Disabled = !_session.LiveOperationsHasUnsavedChangesUx9J;
        _probeButton.Disabled = !selected || !_probeConfirmation.ButtonPressed ||
                                _session.LiveOperationsHasUnsavedChangesUx9J;
    }

    private void ResetUx9J()
    {
        _draft = null;
        _preview = null;
        _explicitSelection = false;
        _canUndo = false;
        _canRedo = false;
        if (_previewSummary is not null) PopulatePreviewUx9J();
    }

    private SourceSnapshotUx9J CaptureSourceUx9J(StudioSessionController session) => new(
        session.Project, session.History, session.History?.State.Revision,
        session.History?.Journal.ToArray() ?? [], session.IsDirty, session.ActiveAreaId,
        session.SelectedEntityIds.ToArray(), session.SelectedEntityId,
        session.CaptureLiveOperationsApprovalUx9J());

    private bool SourceUnchangedUx9J()
    {
        var source = _sourceSession;
        var snapshot = _sourceSnapshot;
        if (source is null || snapshot is null) return _evidenceSession is null;
        return ReferenceEquals(source.Project, snapshot.Project) &&
               ReferenceEquals(source.History, snapshot.History) &&
               source.History?.State.Revision == snapshot.Revision &&
               (source.History?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               source.IsDirty == snapshot.Dirty && source.ActiveAreaId == snapshot.Area &&
               source.SelectedEntityIds.SequenceEqual(snapshot.Selection) &&
               source.SelectedEntityId == snapshot.Selected &&
               source.CaptureLiveOperationsApprovalUx9J() == snapshot.Approval;
    }

    private void RestoreSourceSessionUx9J()
    {
        if (_sourceSession is not null) _session = _sourceSession;
        _sourceSession = null;
        _sourceSnapshot = null;
        _sourceSessionRestored = true;
    }

    private void DeleteEvidenceUx9J()
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
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(Path.GetDirectoryName(full)?.TrimEnd(
                        Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), temp,
                    StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux9-j-ui-",
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
            SetStatusUx9J($"La copie temporaire n’a pas pu être libérée : {exception.Message}",
                true);
        }
    }

    private void UpdateBoundsUx9J()
    {
        if (_dialog is null) return;
        var size = GetViewportRect().Size;
        _dialog.Position = new Vector2(24, 18);
        _dialog.Size = new Vector2(Math.Max(960, size.X - 48), Math.Max(700, size.Y - 36));
    }

    private void SetStatusUx9J(string message, bool error)
    {
        var text = StudioSessionController.HumanUx8N(message);
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger :
            StudioStyle.Muted);
        StatusChanged?.Invoke(text, error);
    }

    private static Control RouteCardUx9J(string route, string title, string detail)
    {
        var panel = StudioStyle.Panel("Ux9J" + route.Replace("-", string.Empty),
            StudioStyle.SurfaceRaised, 6, 1);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var stack = new VBoxContainer();
        panel.AddChild(stack);
        stack.AddChild(StudioStyle.Label($"{route} · {title}", 10, StudioStyle.Text,
            wrap: true));
        stack.AddChild(StudioStyle.Label(detail, 9, StudioStyle.Muted, wrap: true));
        return panel;
    }

    private static Control LabeledRowUx9J(string label, Control control)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var title = StudioStyle.Label(label, 10, StudioStyle.Muted);
        title.CustomMinimumSize = new Vector2(230, 0);
        row.AddChild(title);
        row.AddChild(control);
        return row;
    }

    private static GridContainer GridUx9J(Control parent)
    {
        var grid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 7);
        parent.AddChild(grid);
        return grid;
    }

    private static SpinBox SpinUx9J(GridContainer parent, string label, double min, double max,
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
            CustomMinimumSize = new Vector2(260, 34)
        };
        parent.AddChild(value);
        return value;
    }

    private static LineEdit LineUx9J(GridContainer parent, string label, string placeholder)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var value = new LineEdit
        {
            PlaceholderText = placeholder,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(260, 34)
        };
        parent.AddChild(value);
        return value;
    }

    private static OptionButton ChoiceUx9J(GridContainer? parent, string label,
        params string[] choices)
    {
        parent?.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var value = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(260, 34)
        };
        foreach (var choice in choices) value.AddItem(choice);
        parent?.AddChild(value);
        return value;
    }

    private static CheckBox CheckUx9J(Control parent, string text)
    {
        var check = new CheckBox { Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        parent.AddChild(check);
        return check;
    }

    private static Button ActionButtonUx9J(HBoxContainer parent, string text, Action action,
        float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private static IEnumerable<string> DescendantTextUx9J(Node node)
    {
        if (node is Label label) yield return label.Text;
        if (node is Button button) yield return button.Text;
        if (node is CheckBox check) yield return check.Text;
        if (node is LineEdit line) yield return line.Text;
        if (node is OptionButton option)
            for (var index = 0; index < option.ItemCount; index++)
                yield return option.GetItemText(index);
        foreach (var child in node.GetChildren())
            foreach (var value in DescendantTextUx9J(child)) yield return value;
    }

    private static bool ContainsUuidUx9J(string text) => Regex.IsMatch(text,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b",
        RegexOptions.CultureInvariant);

    private static string HumanCodeUx9J(string code) => code switch
    {
        "LIVE_SCRIPT_APPLIED" => "appliqué",
        "LIVE_SCRIPT_PREVIEW" => "prévisualisé",
        "LIVE_VARIABLE_APPLIED" => "appliquée puis restaurée",
        "LIVE_VARIABLE_PREVIEW" => "prévisualisée",
        "LIVE_SCENE_APPLIED" => "appliquée puis restaurée",
        "LIVE_SCENE_PREVIEW" => "prévisualisée",
        _ => code
    };

    private static string HumanVisibilityUx9J(string mode) => mode switch
    {
        "omniscient" => "omnisciente MJ",
        "selected-player" => "du joueur sélectionné",
        _ => mode
    };

    private static readonly string[] ScriptKindValues =
        ["registered-command", "signed-snippet"];
    private static readonly string[] VariableKindValues = ["bool", "number", "enum", "string"];
    private static readonly string[] VisibilityModeValues = ["omniscient", "selected-player"];

    private sealed record SourceSnapshotUx9J(
        Forge.Domain.Projects.ProjectDefinition? Project,
        Forge.Application.Commands.ProjectCommandHistory? History,
        Forge.Application.Projects.ProjectRevision? Revision,
        IReadOnlyList<Forge.Application.Commands.CommandHistoryJournalEntry> Journal,
        bool Dirty,
        Forge.Domain.Areas.AreaId? Area,
        IReadOnlyList<Forge.Domain.Entities.EntityId> Selection,
        Forge.Domain.Entities.EntityId? Selected,
        StudioUx9JApprovalSnapshot Approval);
}
