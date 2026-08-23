using System.Text.RegularExpressions;
using Forge.Domain.Content;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioBuildCollaborationWorkbenchUx8C : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private SourceSnapshotUx8C? _sourceSnapshot;
    private StudioUx8CGovernanceDraft? _draft;
    private StudioUx8CGovernancePreview? _governancePreview;
    private StudioUx8CMergePreview? _mergePreview;
    private bool _explicitSelection;
    private bool _transactionReady;
    private bool _updating;
    private bool _evidenceResourcesReleased = true;

    private PanelContainer _dialog = null!;
    private OptionButton _moduleChoice = null!;
    private Label _scope = null!;
    private TabContainer _tabs = null!;
    private LineEdit _ours = null!;
    private LineEdit _theirs = null!;
    private OptionButton _mergePolicy = null!;
    private Label _mergeSummary = null!;
    private Label _mergeConflicts = null!;
    private LineEdit _branchPrefix = null!;
    private LineEdit _ownershipPath = null!;
    private LineEdit _workflowPath = null!;
    private LineEdit _fixtureCatalogPath = null!;
    private readonly List<CheckBox> _ciChecks = [];
    private Label _fixtures = null!;
    private Label _governanceSummary = null!;
    private Label _evidenceSummary = null!;
    private Label _status = null!;
    private Button _previewGovernance = null!;
    private Button _applyGovernance = null!;
    private Button _undo = null!;
    private Button _redo = null!;
    private Button _save = null!;

    internal event Action<string, bool>? StatusChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _explicitSelection && _draft is not null;
    internal bool MergeReady => _mergePreview is
    { Succeeded: true, TableConflicts: 1, GraphConflicts: 1, PreservedBoth: true };
    internal bool GovernanceReady => _governancePreview is
    { Succeeded: true, WorktreeReady: true, CiReady: true, FixturesReady: true };
    internal bool TransactionReady => _transactionReady;
    internal bool EvidenceSourceUntouched => SourceMatchesUx8C();
    internal bool EvidenceResourcesReleased => _evidenceResourcesReleased;
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx8C(this));
            return Visible &&
                   text.Contains("Merge assisté", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Worktrees et agents", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Intégration continue", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Fixtures de référence", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Préserver les deux", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucune CI distante", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("source ouverte inchangée", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuidUx8C(text);
        }
    }

    internal StudioBuildCollaborationWorkbenchUx8C(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = session;
        BuildUiUx8C();
    }

    private void BuildUiUx8C()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        Color = new Color(0.012f, 0.018f, 0.03f, 0.95f);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _dialog = StudioStyle.Panel("Ux8CBuildCollaborationWorkbench", StudioStyle.Surface, 12, 1);
        _dialog.ClipContents = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(15, 12, 15, 12);
        _dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("BUILD & IMPACT · UX8-C", 10, StudioStyle.Accent));
        titles.AddChild(StudioStyle.Label("Merge assisté et gouvernance du dépôt", 18, StudioStyle.Text,
            wrap: true));
        titles.AddChild(StudioStyle.Label(
            "Résolution en mémoire + profil transactionnel · source ouverte inchangée",
            9, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        header.AddChild(ActionButtonUx8C("Fermer", Close, 82));
        root.AddChild(header);

        var selection = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selection.AddThemeConstantOverride("separation", 8);
        var moduleLabel = StudioStyle.Label("Module et profil", 11, StudioStyle.Muted);
        moduleLabel.CustomMinimumSize = new Vector2(150, 0);
        selection.AddChild(moduleLabel);
        _moduleChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_moduleChoice);
        _moduleChoice.ItemSelected += index => SelectModuleUx8C((int)index);
        selection.AddChild(_moduleChoice);
        root.AddChild(selection);

        var bodyHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 470),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        root.AddChild(bodyHost);
        var body = new HSplitContainer { SplitOffsets = [475] };
        bodyHost.AddChild(body);
        body.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildRouteColumnUx8C(body);
        BuildWorkbenchTabsUx8C(body);

        _status = StudioStyle.Label("Sélectionnez explicitement le module.", 10, StudioStyle.Muted, wrap: true);
        _status.CustomMinimumSize = new Vector2(0, 30);
        root.AddChild(_status);
        root.AddChild(StudioStyle.Label(
            "Frontière honnête · aucune CI distante, aucun agent, aucun shell et aucune écriture Git ne sont exécutés.",
            9, StudioStyle.Warning, wrap: true));
        UpdateButtonsUx8C();
    }

    private void BuildRouteColumnUx8C(HSplitContainer body)
    {
        var panel = StudioStyle.Panel("Ux8CRoutes", StudioStyle.SurfaceRaised, 8, 1);
        panel.CustomMinimumSize = new Vector2(430, 0);
        body.AddChild(panel);
        var margin = StudioStyle.Margin(10, 8, 10, 8);
        panel.AddChild(margin);
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        margin.AddChild(scroll);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(column);
        column.AddChild(StudioStyle.Label("QUATRE AUTORITÉS CANONIQUES", 10, StudioStyle.Accent));
        _scope = StudioStyle.Label("Sélectionnez explicitement le module.", 10, StudioStyle.Muted, wrap: true);
        _scope.CustomMinimumSize = new Vector2(0, 52);
        column.AddChild(_scope);
        column.AddChild(RouteCardUx8C("ORF-BUILD-012", "Merge assisté",
            "Trois voies sur listes et graphes UUID, conflits inspectables et variante PreserveBoth déterministe."));
        column.AddChild(RouteCardUx8C("ORF-BUILD-013", "Worktrees et agents",
            "Branche dédiée, propriété exclusive, périmètres déclarés et quatre contrôles d’intégration."));
        column.AddChild(RouteCardUx8C("ORF-BUILD-014", "Intégration continue",
            "Schémas, C#, natif, ForgeScript, tests, module de référence et licences configurés sans exécution distante."));
        column.AddChild(RouteCardUx8C("ORF-BUILD-015", "Fixtures de référence",
            "Catalogue versionné Tiny, Vertical, Stress, Malicious et Migration avec chemins sûrs."));
        column.AddChild(StudioStyle.Label(
            "Le merge produit un plan en mémoire. Le profil est la seule écriture humaine possible, après aperçu sec et garde de révision.",
            10, StudioStyle.Warning, wrap: true));
    }

    private void BuildWorkbenchTabsUx8C(HSplitContainer body)
    {
        var panel = StudioStyle.Panel("Ux8CWorkbench", StudioStyle.SurfaceRaised, 8, 1);
        panel.CustomMinimumSize = new Vector2(650, 0);
        body.AddChild(panel);
        var margin = StudioStyle.Margin(10, 8, 10, 8);
        panel.AddChild(margin);
        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        margin.AddChild(_tabs);
        BuildMergeTabUx8C();
        BuildGovernanceTabUx8C();
    }

    private void BuildMergeTabUx8C()
    {
        var scroll = NewTabScrollUx8C("Merge assisté");
        var column = (VBoxContainer)scroll.GetChild(0);
        column.AddChild(StudioStyle.Label("VARIANTES HUMAINES", 10, StudioStyle.Accent));
        _ours = AddLineUx8C(column, "Notre variante", "Libellé local proposé");
        _theirs = AddLineUx8C(column, "Variante reçue", "Libellé reçu");
        _mergePolicy = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_mergePolicy);
        _mergePolicy.AddItem("Conserver notre variante et signaler le conflit");
        _mergePolicy.AddItem("Préférer notre variante");
        _mergePolicy.AddItem("Préférer la variante reçue");
        _mergePolicy.AddItem("Préserver les deux variantes");
        _mergePolicy.Select(3);
        var policyRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var label = StudioStyle.Label("Résolution", 10, StudioStyle.Muted);
        label.CustomMinimumSize = new Vector2(150, 0);
        policyRow.AddChild(label);
        policyRow.AddChild(_mergePolicy);
        column.AddChild(policyRow);
        column.AddChild(ActionButtonUx8C("Prévisualiser le merge", PreviewMergeUx8C, 190, primary: true));
        _mergeSummary = StudioStyle.Label("Aucun merge calculé.", 11, StudioStyle.Muted, wrap: true);
        _mergeConflicts = StudioStyle.Label(
            "Les conflits et la résolution déterministe apparaîtront ici.", 10, StudioStyle.Text, wrap: true);
        column.AddChild(_mergeSummary);
        column.AddChild(new HSeparator());
        column.AddChild(_mergeConflicts);
        column.AddChild(StudioStyle.Label(
            "Aucune donnée source n’est remplacée : le résultat sert de plan de résolution inspectable.",
            9, StudioStyle.Warning, wrap: true));
    }

    private void BuildGovernanceTabUx8C()
    {
        var scroll = NewTabScrollUx8C("Gouvernance");
        var column = (VBoxContainer)scroll.GetChild(0);
        column.AddChild(StudioStyle.Label("WORKTREES ET PROPRIÉTÉ", 10, StudioStyle.Accent));
        _branchPrefix = AddLineUx8C(column, "Préfixe de branche", "codex/");
        _ownershipPath = AddLineUx8C(column, "Manifeste de propriété", ".forge/task-ownership.json");
        column.AddChild(StudioStyle.Label(
            "Contrôles imposés · tracker · backlog · graph · verify", 9, StudioStyle.Muted, wrap: true));
        column.AddChild(new HSeparator());
        column.AddChild(StudioStyle.Label("INTÉGRATION CONTINUE", 10, StudioStyle.Accent));
        _workflowPath = AddLineUx8C(column, "Workflow versionné", ".github/workflows/ci.yml");
        var checkGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var label in new[]
                 {
                     "Valider les schémas", "Compiler le code managé", "Compiler le code natif",
                     "Compiler ForgeScript", "Exécuter les tests", "Packager le module témoin",
                     "Vérifier les licences",
                 })
        {
            var check = new CheckBox { Text = label, ButtonPressed = true };
            checkGrid.AddChild(check);
            _ciChecks.Add(check);
        }
        column.AddChild(checkGrid);
        column.AddChild(new HSeparator());
        column.AddChild(StudioStyle.Label("FIXTURES DE RÉFÉRENCE", 10, StudioStyle.Accent));
        _fixtureCatalogPath = AddLineUx8C(column, "Catalogue versionné",
            "tests/fixtures/build/reference-fixtures.json");
        _fixtures = StudioStyle.Label("Les cinq fixtures apparaîtront après sélection.", 9, StudioStyle.Text,
            wrap: true);
        column.AddChild(_fixtures);
        _governanceSummary = StudioStyle.Label("Aucun aperçu sec.", 10, StudioStyle.Muted, wrap: true);
        _evidenceSummary = StudioStyle.Label("Preuve dépôt non exécutée.", 9, StudioStyle.Muted, wrap: true);
        column.AddChild(new HSeparator());
        column.AddChild(_governanceSummary);
        column.AddChild(_evidenceSummary);
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 6);
        _previewGovernance = ActionButtonUx8C("Aperçu sec", PreviewGovernanceUx8C, 108, primary: true);
        _applyGovernance = ActionButtonUx8C("Appliquer", ApplyGovernanceUx8C, 100);
        _undo = ActionButtonUx8C("Annuler", UndoGovernanceUx8C, 90);
        _redo = ActionButtonUx8C("Rétablir", RedoGovernanceUx8C, 90);
        _save = ActionButtonUx8C("Enregistrer", SaveGovernanceUx8C, 108);
        actions.AddChild(_previewGovernance);
        actions.AddChild(_applyGovernance);
        actions.AddChild(_undo);
        actions.AddChild(_redo);
        actions.AddChild(_save);
        column.AddChild(actions);
    }

    private ScrollContainer NewTabScrollUx8C(string name)
    {
        var scroll = new ScrollContainer
        {
            Name = name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 7);
        scroll.AddChild(column);
        _tabs.AddChild(scroll);
        return scroll;
    }

    internal void Open()
    {
        CloseEvidenceUx8C();
        _session = _sourceSession;
        ResetUx8C();
        PopulateModuleChoiceUx8C();
        Visible = true;
        UpdateBoundsUx8C();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null) return;
        Visible = false;
        CloseEvidenceUx8C();
        _sourceSession.CloseUx8C();
        ResetUx8C();
    }

    internal bool PrepareEvidence(string mode)
    {
        _ = mode;
        if (_evidenceSession is not null && MergeReady && GovernanceReady && SourceMatchesUx8C())
        {
            Visible = true;
            UpdateBoundsUx8C();
            return ShowsHumanSurface;
        }
        CloseEvidenceUx8C();
        if (_sourceSession.Project is null || _sourceSession.History is null || _sourceSession.Session is null)
            return false;
        _sourceSnapshot = CaptureSourceUx8C();
        _evidenceRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux8-c",
            Guid.NewGuid().ToString("N"));
        try
        {
            var created = ProjectSession.Create(_evidenceRoot, _sourceSession.Project);
            if (!created.Succeeded || created.Session is null ||
                !CopyWorkspaceUx8C(_sourceSession.Session.RootDirectory, _evidenceRoot))
                return FailEvidenceUx8C("La copie de preuve n’a pas pu être créée.");
            var profile = WorkspaceBuildProfileStore.Load(_sourceSession.Session.RootDirectory);
            if (!profile.Succeeded || !WorkspaceBuildProfileStore.Save(_evidenceRoot, profile.Profile).Succeeded)
                return FailEvidenceUx8C("Le profil de build n’a pas pu être copié.");
            _evidenceSession = new StudioSessionController();
            if (!_evidenceSession.Open(_evidenceRoot).Succeeded)
                return FailEvidenceUx8C("La copie de preuve n’a pas pu être ouverte.");
            _session = _evidenceSession;
            ResetUx8C();
            PopulateModuleChoiceUx8C();
            Visible = true;
            _moduleChoice.Select(1);
            SelectModuleUx8C(1);
            _ours.Text = "Clairière · équipe locale";
            _theirs.Text = "Clairière · contribution reçue";
            _mergePolicy.Select(3);
            PreviewMergeUx8C();
            PreviewGovernanceUx8C();
            _tabs.CurrentTab = 1;
            UpdateBoundsUx8C();
            _evidenceResourcesReleased = false;
            return HasExplicitSelection && MergeReady && GovernanceReady && ShowsHumanSurface && SourceMatchesUx8C();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or InvalidOperationException)
        {
            return FailEvidenceUx8C("Preuve UX8-C interrompue · " + exception.Message);
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        if (!PrepareEvidence("ux8-c-build-governance") || _evidenceSession is null ||
            !ReferenceEquals(_session, _evidenceSession) || _draft is null || _governancePreview?.CanApply != true)
            return false;
        var current = ReadDraftUx8C();
        if (current is null) return false;
        var applied = _session.ApplyApprovedGovernanceUx8C(current).Succeeded;
        var undo = applied && _session.UndoGovernanceUx8C().Succeeded;
        var redo = undo && _session.RedoGovernanceUx8C().Succeeded;
        var saved = redo && _session.SaveGovernanceUx8C().Succeeded;
        _transactionReady = applied && undo && redo && saved &&
                            File.Exists(Path.Combine(_evidenceRoot!, WorkspaceBuildProfileStore.RelativePath));
        RefreshDraftUx8C();
        PreviewGovernanceUx8C();
        _governanceSummary.Text = _transactionReady
            ? "Transaction prouvée · appliquer, annuler, rétablir et enregistrer dans la copie."
            : "La transaction de preuve a échoué.";
        _governanceSummary.Modulate = _transactionReady ? StudioStyle.Success : StudioStyle.Danger;
        SetStatusUx8C(_governanceSummary.Text, !_transactionReady);
        UpdateButtonsUx8C();
        return _transactionReady && MergeReady && SourceMatchesUx8C();
    }

    internal void CloseEvidence()
    {
        CloseEvidenceUx8C();
        _session = _sourceSession;
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        _session = _sourceSession;
        ResetUx8C();
        PopulateModuleChoiceUx8C();
        Visible = true;
        UpdateBoundsUx8C();
    }

    internal void HideSourceSurfaceAfterSmoke()
    {
        CloseEvidenceUx8C();
        _session = _sourceSession;
        Visible = false;
        ResetUx8C();
    }

    public override void _ExitTree() => CloseEvidenceUx8C();

    public override void _Notification(int what)
    {
        if (what == NotificationResized && Visible) UpdateBoundsUx8C();
    }

    private void PopulateModuleChoiceUx8C()
    {
        _updating = true;
        _moduleChoice.Clear();
        _moduleChoice.AddItem("Choisir explicitement le module ouvert…");
        _moduleChoice.AddItem("Gouverner · " + HumanTextUx8C(_session.Project?.Module.DisplayName ??
                                                              "Module indisponible"));
        _moduleChoice.Select(0);
        _updating = false;
    }

    private void SelectModuleUx8C(int index)
    {
        if (_updating) return;
        _explicitSelection = index == 1;
        _draft = _explicitSelection ? _session.CurrentGovernanceDraftUx8C() : null;
        _governancePreview = null;
        _mergePreview = null;
        _transactionReady = false;
        if (_draft is null)
        {
            _scope.Text = "Sélectionnez explicitement le module pour ouvrir ses autorités de build.";
            ClearControlsUx8C();
            SetStatusUx8C("Aucun module sélectionné.");
        }
        else
        {
            PopulateDraftUx8C(_draft);
            _scope.Text = $"{HumanTextUx8C(_draft.ModuleName)} · profil versionné · " +
                          $"{_draft.Fixtures.Count} fixtures · merge en mémoire · commande transactionnelle.";
            SetStatusUx8C("Module sélectionné · prévisualisez chaque autorité avant toute écriture.");
        }
        UpdateButtonsUx8C();
    }

    private void PopulateDraftUx8C(StudioUx8CGovernanceDraft draft)
    {
        _updating = true;
        _branchPrefix.Text = draft.BranchPrefix;
        _ownershipPath.Text = draft.OwnershipManifestPath;
        _workflowPath.Text = draft.WorkflowPath;
        _fixtureCatalogPath.Text = draft.FixtureCatalogPath;
        var values = new[]
        {
            draft.ValidateSchemas, draft.BuildManagedCode, draft.BuildNativeCode,
            draft.CompileForgeScript, draft.RunTests, draft.PackReferenceModule, draft.VerifyLicenses,
        };
        for (var index = 0; index < _ciChecks.Count; index++)
            _ciChecks[index].ButtonPressed = values[index];
        _fixtures.Text = string.Join(" · ", draft.Fixtures.OrderBy(item => item.Kind)
            .Select(item => $"{HumanFixtureKindUx8C(item.Kind)} : {HumanTextUx8C(item.Name)} ({item.ExpectedOutcome})"));
        var baseName = _session.Project?.Areas.FirstOrDefault()?.DisplayName ?? "Zone principale";
        _ours.Text = HumanTextUx8C(baseName) + " · équipe locale";
        _theirs.Text = HumanTextUx8C(baseName) + " · contribution reçue";
        _mergePolicy.Select(3);
        _updating = false;
    }

    private StudioUx8CGovernanceDraft? ReadDraftUx8C()
    {
        var source = _draft;
        if (source is null) return null;
        return source with
        {
            BranchPrefix = _branchPrefix.Text,
            OwnershipManifestPath = _ownershipPath.Text,
            WorkflowPath = _workflowPath.Text,
            ValidateSchemas = _ciChecks[0].ButtonPressed,
            BuildManagedCode = _ciChecks[1].ButtonPressed,
            BuildNativeCode = _ciChecks[2].ButtonPressed,
            CompileForgeScript = _ciChecks[3].ButtonPressed,
            RunTests = _ciChecks[4].ButtonPressed,
            PackReferenceModule = _ciChecks[5].ButtonPressed,
            VerifyLicenses = _ciChecks[6].ButtonPressed,
            FixtureCatalogPath = _fixtureCatalogPath.Text,
        };
    }

    private void PreviewMergeUx8C()
    {
        if (!_explicitSelection)
        {
            SetStatusUx8C("Sélectionnez explicitement le module avant le merge.", true);
            return;
        }
        var policy = _mergePolicy.Selected switch
        {
            0 => ContentMergePolicy.Manual,
            1 => ContentMergePolicy.PreferOurs,
            2 => ContentMergePolicy.PreferTheirs,
            _ => ContentMergePolicy.PreserveBoth,
        };
        _mergePreview = _session.PreviewAssistedMergeUx8C(_ours.Text, _theirs.Text, policy);
        _mergeSummary.Text = HumanTextUx8C(_mergePreview.Message);
        _mergeSummary.Modulate = _mergePreview.Succeeded ? StudioStyle.Success : StudioStyle.Danger;
        _mergeConflicts.Text = _mergePreview.Succeeded
            ? string.Join('\n', _mergePreview.Conflicts.Concat(_mergePreview.ResultLines).Select(line => "• " +
                HumanTextUx8C(line)))
            : "• " + HumanTextUx8C(_mergePreview.Message);
        SetStatusUx8C(_mergePreview.Message, !_mergePreview.Succeeded);
        UpdateButtonsUx8C();
    }

    private void PreviewGovernanceUx8C()
    {
        var draft = ReadDraftUx8C();
        if (draft is null)
        {
            SetStatusUx8C("Sélectionnez explicitement le module avant l’aperçu.", true);
            return;
        }
        _governancePreview = _session.PreviewGovernanceUx8C(draft);
        _governanceSummary.Text = HumanTextUx8C(_governancePreview.Message + " · " +
                                                  _governancePreview.Patch);
        _governanceSummary.Modulate = _governancePreview.Succeeded ? StudioStyle.Success : StudioStyle.Danger;
        _evidenceSummary.Text = HumanTextUx8C(_governancePreview.EvidenceSummary +
                                               (_governancePreview.Diagnostics.Count == 0
                                                   ? string.Empty
                                                   : " · " + string.Join(" · ", _governancePreview.Diagnostics)));
        SetStatusUx8C(_governancePreview.Message, !_governancePreview.Succeeded);
        UpdateButtonsUx8C();
    }

    private void ApplyGovernanceUx8C()
    {
        var draft = ReadDraftUx8C();
        var result = draft is null
            ? new StudioUx8CActionResult(false, "Aucun brouillon approuvé.")
            : _session.ApplyApprovedGovernanceUx8C(draft);
        SetStatusUx8C(result.Message, !result.Succeeded);
        if (result.Succeeded) RefreshDraftUx8C();
        UpdateButtonsUx8C();
    }

    private void UndoGovernanceUx8C() => RunActionUx8C(_session.UndoGovernanceUx8C());
    private void RedoGovernanceUx8C() => RunActionUx8C(_session.RedoGovernanceUx8C());
    private void SaveGovernanceUx8C() => RunActionUx8C(_session.SaveGovernanceUx8C());

    private void RunActionUx8C(StudioUx8CActionResult result)
    {
        SetStatusUx8C(result.Message, !result.Succeeded);
        if (result.Succeeded) RefreshDraftUx8C();
        UpdateButtonsUx8C();
    }

    private void RefreshDraftUx8C()
    {
        _draft = _session.CurrentGovernanceDraftUx8C();
        _governancePreview = null;
        if (_draft is not null) PopulateDraftUx8C(_draft);
    }

    private void ResetUx8C()
    {
        _explicitSelection = false;
        _draft = null;
        _governancePreview = null;
        _mergePreview = null;
        _transactionReady = false;
        _scope.Text = "Sélectionnez explicitement le module.";
        ClearControlsUx8C();
        _status.Text = "Sélectionnez explicitement le module.";
        _status.Modulate = StudioStyle.Muted;
        UpdateButtonsUx8C();
    }

    private void ClearControlsUx8C()
    {
        _ours.Text = string.Empty;
        _theirs.Text = string.Empty;
        _mergeSummary.Text = "Aucun merge calculé.";
        _mergeSummary.Modulate = StudioStyle.Muted;
        _mergeConflicts.Text = "Les conflits et la résolution déterministe apparaîtront ici.";
        _branchPrefix.Text = string.Empty;
        _ownershipPath.Text = string.Empty;
        _workflowPath.Text = string.Empty;
        _fixtureCatalogPath.Text = string.Empty;
        _fixtures.Text = "Les cinq fixtures apparaîtront après sélection.";
        _governanceSummary.Text = "Aucun aperçu sec.";
        _governanceSummary.Modulate = StudioStyle.Muted;
        _evidenceSummary.Text = "Preuve dépôt non exécutée.";
    }

    private void UpdateButtonsUx8C()
    {
        var selected = HasExplicitSelection;
        _previewGovernance.Disabled = !selected;
        _applyGovernance.Disabled = !selected || _governancePreview?.CanApply != true;
        _undo.Disabled = !selected || !_session.CanUndoScriptCacheProfile;
        _redo.Disabled = !selected || !_session.CanRedoScriptCacheProfile;
        _save.Disabled = !selected;
    }

    private void SetStatusUx8C(string message, bool error = false)
    {
        _status.Text = HumanTextUx8C(message);
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Muted;
        StatusChanged?.Invoke(_status.Text, error);
    }

    private bool FailEvidenceUx8C(string message)
    {
        SetStatusUx8C(message, true);
        CloseEvidenceUx8C();
        return false;
    }

    private void CloseEvidenceUx8C()
    {
        var evidence = _evidenceSession;
        var root = _evidenceRoot;
        _evidenceSession = null;
        _evidenceRoot = null;
        if (evidence is not null) evidence.Close();
        _session = _sourceSession;
        _evidenceResourcesReleased = true;
        if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                _evidenceResourcesReleased = false;
            }
            catch (UnauthorizedAccessException)
            {
                _evidenceResourcesReleased = false;
            }
        }
    }

    private SourceSnapshotUx8C CaptureSourceUx8C() => new(
        _sourceSession.Project!, _sourceSession.History!, _sourceSession.History!.State.Revision,
        _sourceSession.History.Journal.ToArray(), _sourceSession.IsDirty,
        _sourceSession.History.CanUndo, _sourceSession.History.CanRedo,
        _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(),
        _sourceSession.SelectedEntityId, _sourceSession.CaptureGovernanceApprovalUx8C());

    private bool SourceMatchesUx8C()
    {
        var snapshot = _sourceSnapshot;
        var history = _sourceSession.History;
        return snapshot is not null && ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.IsDirty && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo && _sourceSession.ActiveAreaId == snapshot.AreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureGovernanceApprovalUx8C() == snapshot.Approval;
    }

    private void UpdateBoundsUx8C()
    {
        var viewport = GetViewportRect().Size;
        var width = Math.Clamp(viewport.X - 54, 1180, 1840);
        var height = Math.Clamp(viewport.Y - 40, 780, 1040);
        _dialog.Position = new Vector2((viewport.X - width) / 2f, (viewport.Y - height) / 2f);
        _dialog.Size = new Vector2(width, height);
    }

    private static bool CopyWorkspaceUx8C(string sourceRoot, string targetRoot)
    {
        try
        {
            var source = Path.GetFullPath(sourceRoot);
            var target = Path.GetFullPath(targetRoot);
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(source, file);
                var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (segments.Any(segment => segment is ".git" or "bin" or "obj")) continue;
                var destination = Path.Combine(target, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination, overwrite: true);
            }
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException)
        {
            _ = exception;
            return false;
        }
    }

    private static ScrollContainer TabScrollUx8C(string name) => new() { Name = name };

    private static LineEdit AddLineUx8C(VBoxContainer parent, string labelText, string placeholder)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var label = StudioStyle.Label(labelText, 10, StudioStyle.Muted);
        label.CustomMinimumSize = new Vector2(150, 0);
        row.AddChild(label);
        var edit = new LineEdit
        {
            PlaceholderText = placeholder,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        StudioStyle.ApplyLineEdit(edit);
        row.AddChild(edit);
        parent.AddChild(row);
        return edit;
    }

    private static PanelContainer RouteCardUx8C(string route, string title, string description)
    {
        var panel = StudioStyle.Panel("Ux8CRouteCard", StudioStyle.Surface, 6, 1);
        var margin = StudioStyle.Margin(8, 6, 8, 6);
        panel.AddChild(margin);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 3);
        margin.AddChild(column);
        column.AddChild(StudioStyle.Label($"{route} · {title}", 11, StudioStyle.Text, wrap: true));
        column.AddChild(StudioStyle.Label(description, 9, StudioStyle.Muted, wrap: true));
        return panel;
    }

    private static Button ActionButtonUx8C(string text, Action action, float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        return button;
    }

    private static string HumanFixtureKindUx8C(Forge.Domain.Build.ReferenceFixtureKind kind) => kind switch
    {
        Forge.Domain.Build.ReferenceFixtureKind.Tiny => "Minimal",
        Forge.Domain.Build.ReferenceFixtureKind.VerticalSlice => "Tranche verticale",
        Forge.Domain.Build.ReferenceFixtureKind.Stress => "Charge",
        Forge.Domain.Build.ReferenceFixtureKind.Malicious => "Hostile",
        Forge.Domain.Build.ReferenceFixtureKind.Migration => "Migration",
        _ => "Inconnue",
    };

    private static string HumanTextUx8C(string value) => Regex.Replace(value.Replace('\\', '/'),
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
        "élément protégé", RegexOptions.CultureInvariant);

    private static bool ContainsUuidUx8C(string value) => Regex.IsMatch(value,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b");

    private static IEnumerable<string> DescendantTextUx8C(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            switch (child)
            {
                case Label label when label.Visible: yield return label.Text; break;
                case Button button when button.Visible: yield return button.Text; break;
                case CheckBox check when check.Visible: yield return check.Text; break;
                case OptionButton option when option.Visible:
                    for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
                    break;
            }
            foreach (var text in DescendantTextUx8C(child)) yield return text;
        }
    }

    private sealed record SourceSnapshotUx8C(
        Forge.Domain.Projects.ProjectDefinition Project,
        Forge.Application.Commands.ProjectCommandHistory History,
        Forge.Application.Projects.ProjectRevision Revision,
        IReadOnlyList<Forge.Application.Commands.CommandHistoryJournalEntry> Journal,
        bool IsDirty,
        bool CanUndo,
        bool CanRedo,
        Forge.Domain.Areas.AreaId? AreaId,
        IReadOnlyList<Forge.Domain.Entities.EntityId> SelectedEntityIds,
        Forge.Domain.Entities.EntityId? SelectedEntityId,
        StudioUx8CApprovalSnapshot Approval);
}
