using Forge.Domain.Areas;
using Forge.Domain.Rules;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioAreaCombatPoliciesOverlayUx2LwD : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceTempRoot;
    private StudioUx2LwDEvidenceSourceSnapshot? _evidenceSourceSnapshot;
    private readonly PanelContainer _dialog;
    private readonly OptionButton _area;
    private readonly Label _selection;
    private readonly OptionButton _pvp;
    private readonly OptionButton _friendlyFire;
    private readonly CheckBox _crime;
    private readonly CheckBox _aggro;
    private readonly CheckBox _environmentalDamage;
    private readonly CheckBox _serverAuthority;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _runtime;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;
    private IReadOnlyList<StudioUx2LwDAreaChoice> _choices = [];
    private StudioUx2LwDDraft? _draft;
    private StudioUx2LwDPreview? _lastPreview;
    private bool _updating;
    private bool _applied;
    private bool _canonicalPreviewReady;
    private bool _lastEvidenceSourceUntouched;
    private bool _lastEvidenceResourcesReleased;
    private bool _lastSourceRestored = true;
    private bool _lastEvidencePreviewDryRun;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? AreaCombatChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _choices.Count;
    internal bool HasExplicitSelection => _draft is not null && _area.Selected > 0;
    internal bool CanonicalPreviewReady => _canonicalPreviewReady && _lastPreview?.Succeeded == true;
    internal bool ShowsCompleteCombatPolicy
    {
        get
        {
            var text = string.Join(' ', DescendantLabels(_dialog).Select(label => label.Text));
            return text.Contains("PvP", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Tir allié", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Crime", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aggro", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("environnementaux", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Autorité serveur", StringComparison.OrdinalIgnoreCase);
        }
    }
    internal bool ShowsNoSimulationBoundary => _lastPreview is { Runtime.EvaluatedLive: false } preview &&
                                                preview.Runtime.Boundary.Contains("aucun joueur",
                                                    StringComparison.OrdinalIgnoreCase) &&
                                                preview.Runtime.Boundary.Contains("réseau",
                                                    StringComparison.OrdinalIgnoreCase);
    internal bool ShowsServerAuthority => _lastPreview?.Runtime.Authority.Contains(
        "avant tout coût ou dégât", StringComparison.OrdinalIgnoreCase) == true;
    internal bool ShowsStructuredDiagnostics => _lastPreview is not null &&
                                                 _lastPreview.Diagnostics.All(item =>
                                                     !string.IsNullOrWhiteSpace(item.Code) &&
                                                     !string.IsNullOrWhiteSpace(item.Path));
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                          ReferenceEquals(_session, _evidenceSession);
    internal bool EvidenceSourceUntouched => _evidenceSourceSnapshot is { } snapshot
        ? SourceMatchesSnapshot(snapshot)
        : _lastEvidenceSourceUntouched;
    internal bool EvidenceResourcesReleased => _evidenceSession is null && _evidenceTempRoot is null &&
                                               _lastEvidenceResourcesReleased;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession) && _lastSourceRestored;

    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantLabels(_dialog).Select(label => label.Text));
            return text.Contains("Combat de zone", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("autorité serveur", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun dégât", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !StudioSessionController.ContainsTechnicalIdentifierUx2LwD(text);
        }
    }

    internal StudioAreaCombatPoliciesOverlayUx2LwD(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = _sourceSession;
        Name = "StudioAreaCombatPoliciesOverlayUx2LwD";
        Visible = false;
        ZIndex = 468;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect
        {
            Color = new Color(0, 0, 0, .82f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        _dialog = StudioStyle.Panel("AreaCombatPoliciesDialog", StudioStyle.Chrome, 12, 2);
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
        header.AddThemeConstantOverride("separation", 8);
        root.AddChild(header);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("MONDE VIVANT · AUTORITÉ DE COMBAT", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Combat de zone", 20));
        heading.AddChild(StudioStyle.Label(
            "AREA-027 · politique canonique complète · aperçu sec uniquement · aucune décision live, " +
            "aucun dégât, aucun moteur et aucun dispatch réseau simulés dans cet outil",
            10,
            StudioStyle.Muted,
            wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.TooltipText = "Fermer sans appliquer le brouillon · Échap";
        close.Pressed += Close;
        header.AddChild(close);

        var selector = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selector.AddThemeConstantOverride("separation", 8);
        root.AddChild(selector);
        selector.AddChild(StudioStyle.Label("Zone à configurer", 10, StudioStyle.Muted));
        _area = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_area);
        _area.ItemSelected += index => SelectArea((int)index);
        selector.AddChild(_area);
        _selection = StudioStyle.Label(
            "Choisissez explicitement une zone ; aucune fiche n’est sélectionnée automatiquement.",
            11,
            StudioStyle.Warning,
            wrap: true);
        root.AddChild(_selection);

        var bodyHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 480),
            CustomMaximumSize = new Vector2(-1, 780),
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

        var modes = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        modes.AddThemeConstantOverride("h_separation", 10);
        modes.AddThemeConstantOverride("v_separation", 6);
        modes.AddChild(StudioStyle.Label("Mode PvP", 10, StudioStyle.Muted));
        _pvp = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_pvp);
        foreach (var mode in Enum.GetValues<AreaPvpMode>())
            _pvp.AddItem(StudioSessionController.HumanPvpModeUx2LwD(mode));
        modes.AddChild(_pvp);
        modes.AddChild(StudioStyle.Label("Tir allié", 10, StudioStyle.Muted));
        _friendlyFire = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_friendlyFire);
        foreach (var mode in Enum.GetValues<FriendlyFireMode>())
            _friendlyFire.AddItem(StudioSessionController.HumanFriendlyFireUx2LwD(mode));
        modes.AddChild(_friendlyFire);
        content.AddChild(Card(
            "AreaCombatRelations",
            "Relations hostiles",
            "Ces choix décrivent ce que l’autorité peut accepter ; aucun joueur, duel, groupe ou faction n’est synthétisé.",
            modes));

        var gates = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        gates.AddThemeConstantOverride("separation", 5);
        _crime = new CheckBox { Text = "Évaluer les actions criminelles" };
        _aggro = new CheckBox { Text = "Autoriser l’aggro et l’initiation hostile" };
        _environmentalDamage = new CheckBox { Text = "Autoriser les dégâts environnementaux" };
        _serverAuthority = new CheckBox { Text = "Exiger et activer l’autorité serveur" };
        gates.AddChild(_crime);
        gates.AddChild(_aggro);
        gates.AddChild(_environmentalDamage);
        gates.AddChild(_serverAuthority);
        gates.AddChild(StudioStyle.Label(
            "L’aggro désactivée suspend toute initiation hostile avant le dégât. Sans autorité serveur, " +
            "le runtime canonique refuse la décision avant tout coût ou dégât.",
            10,
            StudioStyle.Muted,
            wrap: true));
        content.AddChild(Card(
            "AreaCombatRuntimeGates",
            "Garde-fous runtime",
            "Les options sont persistées ; leur aperçu décrit le contrat mais ne lance pas le runtime.",
            gates));

        _diff = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _diff.AddThemeConstantOverride("separation", 4);
        content.AddChild(Card(
            "AreaCombatHumanDiff",
            "Diff humain avant application",
            "Seule la politique Combat de la zone sélectionnée peut changer.",
            _diff));
        _runtime = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _runtime.AddThemeConstantOverride("separation", 4);
        content.AddChild(Card(
            "AreaCombatRuntimeProjection",
            "Aperçu runtime honnête",
            "Projection de la décision d’autorité ; aucune simulation de joueur, groupe, duel, faction, moteur ou réseau.",
            _runtime));
        _diagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _diagnostics.AddThemeConstantOverride("separation", 4);
        content.AddChild(Card(
            "AreaCombatDiagnostics",
            "Diagnostics localisés",
            "Codes et chemins restent structurés en interne ; cette vue propose une correction humaine.",
            _diagnostics));

        _summary = StudioStyle.Label(
            "Sélectionnez une zone puis lancez l’aperçu sec · aucun changement appliqué.",
            11,
            StudioStyle.Muted,
            wrap: true);
        root.AddChild(_summary);
        _status = StudioStyle.Label("Prêt · sélection explicite requise", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        var footer = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footer.AddThemeConstantOverride("separation", 7);
        root.AddChild(footer);
        _preview = StudioStyle.Button("Aperçu sec", primary: true);
        _preview.TooltipText = "Valider la commande sans modifier le projet ni simuler un combat.";
        _preview.Pressed += Preview;
        footer.AddChild(_preview);
        _apply = StudioStyle.Button("Appliquer", primary: true);
        _apply.TooltipText = "Disponible après un aperçu sec réussi et encore frais.";
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
        footer.AddChild(StudioStyle.Label(
            "Commande area-only · Heartbeats, repos, cinématiques, perception, expérience et zones voisines préservés.",
            10,
            StudioStyle.Muted,
            wrap: true));

        _pvp.ItemSelected += _ => InvalidatePreview();
        _friendlyFire.ItemSelected += _ => InvalidatePreview();
        _crime.Toggled += _ => InvalidatePreview();
        _aggro.Toggled += _ => InvalidatePreview();
        _environmentalDamage.Toggled += _ => InvalidatePreview();
        _serverAuthority.Toggled += _ => InvalidatePreview();
        ResetControls();
        SetEditingEnabled(false);
        Resized += UpdateDialogBounds;
    }

    internal void Open()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null)
            Close();
        _session = _sourceSession;
        OpenBoundSession();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null)
            Close();
        _session = _sourceSession;
        OpenBoundSession(cancelApproval: false);
    }

    private void OpenBoundSession(bool cancelApproval = true)
    {
        if (cancelApproval)
            _session.CancelAreaCombatPreviewUx2LwD();
        _choices = _session.AreaCombatChoicesUx2LwD;
        _updating = true;
        _area.Clear();
        _area.AddItem("Choisissez explicitement une zone…");
        foreach (var choice in _choices)
            _area.AddItem(choice.Label);
        _area.Select(0);
        _updating = false;
        _draft = null;
        _lastPreview = null;
        _applied = false;
        _canonicalPreviewReady = false;
        ResetControls();
        _selection.Text = _choices.Count == 0
            ? "Aucune zone n’est disponible dans ce projet."
            : "Choisissez explicitement une zone ; aucune fiche n’est sélectionnée automatiquement.";
        _selection.AddThemeColorOverride("font_color",
            _choices.Count == 0 ? StudioStyle.Danger : StudioStyle.Warning);
        _summary.Text = "Sélectionnez une zone puis lancez l’aperçu sec · aucun changement appliqué.";
        RenderDiff(null);
        RenderRuntime(null);
        RenderDiagnostics([]);
        SetEditingEnabled(false);
        UpdateTransactionButtons();
        SetStatus("Prêt · sélection explicite requise", error: false);
        Visible = true;
        MoveToFront();
        UpdateDialogBounds();
        _area.GrabFocus();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceTempRoot is null)
            return;
        var sourceUntouched = _evidenceSourceSnapshot is not { } snapshot || SourceMatchesSnapshot(snapshot);
        _session.CancelAreaCombatPreviewUx2LwD();
        Visible = false;
        _draft = null;
        _lastPreview = null;
        _applied = false;
        _canonicalPreviewReady = false;
        ResetControls();
        var released = ReleaseIsolatedEvidenceSession();
        _lastEvidenceSourceUntouched = sourceUntouched;
        _lastEvidenceResourcesReleased = released;
        _lastSourceRestored = ReferenceEquals(_session, _sourceSession);
    }

    internal bool PrepareEvidence(string mode)
    {
        _ = mode;
        if ((_evidenceSession is not null || _evidenceTempRoot is not null) &&
            !ReleaseIsolatedEvidenceSession())
            return false;
        if (!TryOpenIsolatedEvidenceSession())
            return false;
        var choiceIndex = _choices.ToList().FindIndex(item => item.IsEvidenceTarget);
        if (choiceIndex < 0)
        {
            Close();
            return false;
        }
        _updating = true;
        _area.Select(choiceIndex + 1);
        _updating = false;
        LoadChoice(choiceIndex);
        if (_draft is null || !ConfigureEvidenceDraft())
        {
            Close();
            return false;
        }

        var history = _session.History;
        var project = _session.Project;
        var revision = history?.State.Revision;
        var journal = history?.Journal.ToArray() ?? [];
        var canUndo = history?.CanUndo;
        var canRedo = history?.CanRedo;
        var dirty = _session.IsDirty;
        Preview();
        _lastEvidencePreviewDryRun = project is not null &&
                                     ReferenceEquals(project, _session.Project) &&
                                     ReferenceEquals(history, _session.History) &&
                                     history?.State.Revision == revision &&
                                     (history?.Journal ?? []).SequenceEqual(journal) &&
                                     history?.CanUndo == canUndo && history?.CanRedo == canRedo &&
                                     _session.IsDirty == dirty;
        _selection.Text += " · session de preuve isolée";
        _summary.Text = "PREUVE ISOLÉE · le projet source reste strictement inchangé.\n" + _summary.Text;
        SetStatus("Preuve isolée prête · fermeture = suppression de la session temporaire", error: false);
        return _lastEvidencePreviewDryRun && !_apply.Disabled && HasExplicitSelection &&
               CanonicalPreviewReady && ShowsCompleteCombatPolicy && ShowsNoSimulationBoundary &&
               ShowsServerAuthority && ShowsStructuredDiagnostics && ShowsHumanSurface &&
               UsesIsolatedEvidence && EvidenceSourceUntouched;
    }

    internal bool RunUiTransactionEvidence()
    {
        var sourceSnapshot = CaptureSourceSnapshot();
        if (sourceSnapshot is null)
            return false;
        StudioSessionController? reopened = null;
        try
        {
            var ready = PrepareEvidence("ux2-lw-d-area-combat-authority");
            if (!ready || !UsesIsolatedEvidence)
                return false;
            var transaction = _session;
            var originalProject = transaction.Project;
            var targetId = _draft?.AreaId;
            var originalTarget = targetId is { } sourceAreaId
                ? originalProject?.Areas.SingleOrDefault(area => area.Id == sourceAreaId)
                : null;
            var originalProfile = originalTarget?.RuntimePolicies;
            var dryRun = _lastEvidencePreviewDryRun && originalProject is not null &&
                         originalTarget is not null && originalProfile is not null &&
                         SourceMatchesSnapshot(sourceSnapshot);
            Apply();
            var appliedProject = transaction.Project;
            var appliedTarget = targetId is { } appliedAreaId
                ? appliedProject?.Areas.SingleOrDefault(area => area.Id == appliedAreaId)
                : null;
            var appliedProfile = appliedTarget?.RuntimePolicies;
            var applied = dryRun && _applied && appliedProject is not null && appliedTarget is not null &&
                          appliedProfile is not null && !ReferenceEquals(originalProject, appliedProject) &&
                          originalProfile!.Heartbeats.SequenceEqual(appliedProfile.Heartbeats) &&
                          originalProfile!.Heartbeats.Zip(appliedProfile.Heartbeats, ReferenceEquals)
                              .All(value => value) &&
                          ReferenceEquals(originalProfile!.Rest, appliedProfile.Rest) &&
                          ReferenceEquals(originalProfile!.Cinematics, appliedProfile.Cinematics) &&
                          ReferenceEquals(originalProfile!.Rules.Perception, appliedProfile.Rules.Perception) &&
                          ReferenceEquals(originalProfile!.Experience, appliedProfile.Experience) &&
                          ReferenceEquals(originalTarget!.Foundation, appliedTarget.Foundation) &&
                          ReferenceEquals(originalTarget!.Navigation, appliedTarget.Navigation) &&
                          StudioSessionController.AreaCombatNeighborsMatchUx2LwD(
                              originalTarget!, appliedTarget);
            var expectedAreaJson = appliedTarget is null ? null : AreaJsonSerializer.Serialize(appliedTarget);
            var expectedProject = applied ? appliedProject : null;
            if (applied)
                Undo();
            var undone = applied && ReferenceEquals(originalProject, transaction.Project);
            if (undone)
                Redo();
            var redone = undone && ReferenceEquals(appliedProject, transaction.Project);
            var saved = redone && transaction.Save().Succeeded;
            var persisted = false;
            if (saved && transaction.Session is { } workspace && targetId is { } persistedAreaId &&
                expectedAreaJson is not null)
            {
                reopened = new StudioSessionController();
                if (reopened.Open(workspace.RootDirectory, persistedAreaId.ToString()).Succeeded)
                {
                    var restoredProject = reopened.Project;
                    var restoredTarget = restoredProject?.Areas.SingleOrDefault(area => area.Id == persistedAreaId);
                    persisted = expectedProject is not null && restoredProject is not null &&
                                restoredTarget is not null && string.Equals(expectedAreaJson,
                                    AreaJsonSerializer.Serialize(restoredTarget), StringComparison.Ordinal) &&
                                StudioSessionController.ProjectRoundTripMatchesUx2A(
                                    expectedProject, restoredProject, persistedAreaId);
                }
            }
            var sourceUntouched = SourceMatchesSnapshot(sourceSnapshot);
            reopened?.Close();
            reopened = null;
            Close();
            return dryRun && applied && undone && redone && saved && persisted && sourceUntouched &&
                   EvidenceResourcesReleased && SourceSessionRestored;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_LW_D_UI_TRANSACTION_FAILED " +
                        $"type={exception.GetType().Name} message={exception.Message}");
            return false;
        }
        finally
        {
            reopened?.Close();
            if (_evidenceSession is not null || _evidenceTempRoot is not null)
                Close();
            _sourceSession.RestoreAreaCombatApprovalUx2LwD(sourceSnapshot.Approval);
        }
    }

    private bool TryOpenIsolatedEvidenceSession()
    {
        var snapshot = CaptureSourceSnapshot();
        var evidenceProject = _sourceSession.CreateUx2LwDEvidenceProject();
        if (snapshot is null || evidenceProject is null || _sourceSession.Session is null)
            return false;
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-lw-d-evidence-" + Guid.NewGuid().ToString("N"));
        var evidence = new StudioSessionController();
        try
        {
            var created = ProjectSession.Create(tempRoot, evidenceProject);
            var opened = created.Succeeded &&
                         evidence.Open(tempRoot, snapshot.ActiveAreaId?.ToString()).Succeeded;
            if (!opened)
            {
                evidence.Close();
                _lastEvidenceResourcesReleased =
                    StudioSessionController.DeleteUx2LwDTemporaryWorkspace(tempRoot);
                _evidenceTempRoot = _lastEvidenceResourcesReleased ? null : tempRoot;
                _session = _sourceSession;
                _lastSourceRestored = true;
                return false;
            }
            _evidenceSourceSnapshot = snapshot;
            _evidenceSession = evidence;
            _evidenceTempRoot = tempRoot;
            _session = evidence;
            _lastEvidenceResourcesReleased = false;
            _lastSourceRestored = false;
            _lastEvidencePreviewDryRun = false;
            OpenBoundSession();
            var ready = _choices.Any(item => item.IsEvidenceTarget) && SourceMatchesSnapshot(snapshot);
            if (!ready)
                Close();
            return ready;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_LW_D_EVIDENCE_FAILED " +
                        $"type={exception.GetType().Name} message={exception.Message}");
            evidence.Close();
            var released = StudioSessionController.DeleteUx2LwDTemporaryWorkspace(tempRoot);
            _evidenceSourceSnapshot = null;
            _evidenceSession = null;
            _evidenceTempRoot = released ? null : tempRoot;
            _session = _sourceSession;
            _lastEvidenceResourcesReleased = released;
            _lastSourceRestored = true;
            return false;
        }
    }

    private bool ReleaseIsolatedEvidenceSession()
    {
        var evidence = _evidenceSession;
        var tempRoot = _evidenceTempRoot;
        evidence?.Close();
        _evidenceSession = null;
        _evidenceSourceSnapshot = null;
        _session = _sourceSession;
        var released = StudioSessionController.DeleteUx2LwDTemporaryWorkspace(tempRoot);
        _evidenceTempRoot = released ? null : tempRoot;
        _lastSourceRestored = true;
        return released;
    }

    private StudioUx2LwDEvidenceSourceSnapshot? CaptureSourceSnapshot()
    {
        var project = _sourceSession.Project;
        var history = _sourceSession.History;
        if (project is null || history is null)
            return null;
        return new StudioUx2LwDEvidenceSourceSnapshot(
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
            _sourceSession.CaptureAreaCombatApprovalUx2LwD());
    }

    private bool SourceMatchesSnapshot(StudioUx2LwDEvidenceSourceSnapshot snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) &&
               history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.IsDirty &&
               history?.CanUndo == snapshot.CanUndo && history?.CanRedo == snapshot.CanRedo &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureAreaCombatApprovalUx2LwD() == snapshot.Approval;
    }

    private void SelectArea(int selected)
    {
        if (_updating)
            return;
        _session.CancelAreaCombatPreviewUx2LwD();
        _draft = null;
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _applied = false;
        ResetControls();
        RenderDiff(null);
        RenderRuntime(null);
        RenderDiagnostics([]);
        if (selected <= 0 || selected > _choices.Count)
        {
            SetEditingEnabled(false);
            _selection.Text =
                "Choisissez explicitement une zone ; aucune fiche n’est sélectionnée automatiquement.";
            _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
            UpdateTransactionButtons();
            return;
        }
        LoadChoice(selected - 1);
    }

    private void LoadChoice(int choiceIndex)
    {
        if (choiceIndex < 0 || choiceIndex >= _choices.Count)
            return;
        _session.CancelAreaCombatPreviewUx2LwD();
        _draft = _session.CreateAreaCombatDraftUx2LwD(_choices[choiceIndex].AreaId);
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _applied = false;
        if (_draft is null)
        {
            ResetControls();
            SetEditingEnabled(false);
            SetStatus("Cette zone ne peut pas être chargée.", error: true);
            return;
        }
        LoadDraft(_draft);
        SetEditingEnabled(true);
        _selection.Text = $"Zone sélectionnée : {_draft.AreaDisplayName} · aucune sélection implicite";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Text);
        _summary.Text = "Modifiez Combat puis lancez l’aperçu sec. Aucun combat ni dégât n’est exécuté ici.";
        RenderDiff(null);
        RenderRuntime(null);
        RenderDiagnostics([]);
        SetStatus("Politique chargée · aperçu sec requis", error: false);
        UpdateTransactionButtons();
    }

    private void LoadDraft(StudioUx2LwDDraft draft)
    {
        _updating = true;
        _pvp.Select((int)draft.PvpMode);
        _friendlyFire.Select((int)draft.FriendlyFire);
        _crime.ButtonPressed = draft.CrimeEnabled;
        _aggro.ButtonPressed = draft.AggroEnabled;
        _environmentalDamage.ButtonPressed = draft.EnvironmentalDamageEnabled;
        _serverAuthority.ButtonPressed = draft.ServerAuthoritative;
        _updating = false;
    }

    private StudioUx2LwDDraft? ReadDraft() => _draft is null
        ? null
        : _draft with
        {
            PvpMode = (AreaPvpMode)_pvp.Selected,
            FriendlyFire = (FriendlyFireMode)_friendlyFire.Selected,
            CrimeEnabled = _crime.ButtonPressed,
            AggroEnabled = _aggro.ButtonPressed,
            EnvironmentalDamageEnabled = _environmentalDamage.ButtonPressed,
            ServerAuthoritative = _serverAuthority.ButtonPressed,
        };

    private bool ConfigureEvidenceDraft()
    {
        if (_draft is null)
            return false;
        _draft = _draft with
        {
            PvpMode = _draft.PvpMode == AreaPvpMode.Open
                ? AreaPvpMode.HostileFactions
                : AreaPvpMode.Open,
            FriendlyFire = _draft.FriendlyFire == FriendlyFireMode.Enabled
                ? FriendlyFireMode.Disabled
                : FriendlyFireMode.Enabled,
            CrimeEnabled = !_draft.CrimeEnabled,
            AggroEnabled = !_draft.AggroEnabled,
            EnvironmentalDamageEnabled = !_draft.EnvironmentalDamageEnabled,
            ServerAuthoritative = true,
        };
        LoadDraft(_draft);
        return true;
    }

    private void Preview()
    {
        var draft = ReadDraft();
        if (draft is null)
        {
            SetStatus("Choisissez explicitement une zone avant l’aperçu.", error: true);
            return;
        }
        _draft = draft;
        _lastPreview = _session.PreviewAreaCombatUx2LwD(draft);
        _canonicalPreviewReady = _lastPreview.Succeeded;
        RenderDiff(_lastPreview.Diff);
        RenderRuntime(_lastPreview.Runtime);
        RenderDiagnostics(_lastPreview.Diagnostics);
        _summary.Text = _lastPreview.Message;
        _summary.AddThemeColorOverride("font_color",
            _lastPreview.Succeeded ? StudioStyle.Text : StudioStyle.Danger);
        SetStatus(_lastPreview.Message, error: !_lastPreview.Succeeded);
        UpdateTransactionButtons();
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedAreaCombatUx2LwD();
        _canonicalPreviewReady = false;
        _lastPreview = null;
        _applied = result.Succeeded;
        if (result.Succeeded && _draft is not null)
        {
            var areaId = _draft.AreaId;
            AreaCombatChanged?.Invoke();
            _draft = _session.CreateAreaCombatDraftUx2LwD(areaId);
            if (_draft is not null)
                LoadDraft(_draft);
        }
        SetStatus(result.Message, error: !result.Succeeded);
        _summary.Text = result.Message;
        RenderDiff(null);
        RenderRuntime(null);
        RenderDiagnostics([]);
        UpdateTransactionButtons();
    }

    private void Undo()
    {
        var result = _session.Undo();
        _session.CancelAreaCombatPreviewUx2LwD();
        _canonicalPreviewReady = false;
        _lastPreview = null;
        _applied = false;
        if (result.Succeeded)
        {
            ReloadSelectedArea();
            AreaCombatChanged?.Invoke();
        }
        SetStatus(result.Message, error: !result.Succeeded);
        UpdateTransactionButtons();
    }

    private void Redo()
    {
        var result = _session.Redo();
        _session.CancelAreaCombatPreviewUx2LwD();
        _canonicalPreviewReady = false;
        _lastPreview = null;
        _applied = result.Succeeded;
        if (result.Succeeded)
        {
            ReloadSelectedArea();
            AreaCombatChanged?.Invoke();
        }
        SetStatus(result.Message, error: !result.Succeeded);
        UpdateTransactionButtons();
    }

    private void Save()
    {
        var result = _session.Save();
        SetStatus(result.Message, error: !result.Succeeded);
        UpdateTransactionButtons();
    }

    private void ReloadSelectedArea()
    {
        var areaId = _draft?.AreaId;
        _choices = _session.AreaCombatChoicesUx2LwD;
        var index = areaId is null ? -1 : _choices.ToList().FindIndex(item => item.AreaId == areaId);
        _updating = true;
        _area.Clear();
        _area.AddItem("Choisissez explicitement une zone…");
        foreach (var choice in _choices)
            _area.AddItem(choice.Label);
        _area.Select(index + 1);
        _updating = false;
        if (index >= 0)
            LoadChoice(index);
        else
            SelectArea(0);
    }

    private void InvalidatePreview()
    {
        if (_updating || _draft is null)
            return;
        _session.CancelAreaCombatPreviewUx2LwD();
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _applied = false;
        _summary.Text = "Brouillon modifié · relancez l’aperçu sec avant application.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Warning);
        _status.Text = "Aperçu précédent effacé · aucun changement appliqué";
        _status.AddThemeColorOverride("font_color", StudioStyle.Warning);
        RenderDiff(null);
        RenderRuntime(null);
        RenderDiagnostics([]);
        UpdateTransactionButtons();
    }

    private void ResetControls()
    {
        _updating = true;
        _pvp.Select(0);
        _friendlyFire.Select(0);
        _crime.ButtonPressed = false;
        _aggro.ButtonPressed = false;
        _environmentalDamage.ButtonPressed = false;
        _serverAuthority.ButtonPressed = false;
        _updating = false;
    }

    private void SetEditingEnabled(bool enabled)
    {
        _pvp.Disabled = !enabled;
        _friendlyFire.Disabled = !enabled;
        _crime.Disabled = !enabled;
        _aggro.Disabled = !enabled;
        _environmentalDamage.Disabled = !enabled;
        _serverAuthority.Disabled = !enabled;
        _preview.Disabled = !enabled;
    }

    private void UpdateTransactionButtons()
    {
        _apply.Disabled = _lastPreview?.CanApply != true || !_canonicalPreviewReady;
        _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo;
        _save.Disabled = !_session.IsDirty;
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(message, error);
    }

    private void RenderDiff(StudioUx2LwDSemanticDiff? diff)
    {
        ClearChildren(_diff);
        if (diff is null)
        {
            _diff.AddChild(StudioStyle.Label("Aucun aperçu frais.", 10, StudioStyle.Muted));
            return;
        }
        if (!diff.HasChanges)
        {
            _diff.AddChild(StudioStyle.Label("Aucune différence sémantique.", 10, StudioStyle.Muted));
            return;
        }
        foreach (var change in diff.Changes)
            _diff.AddChild(StudioStyle.Label("• " + change, 10, StudioStyle.Text, wrap: true));
    }

    private void RenderRuntime(StudioUx2LwDRuntimeProjection? projection)
    {
        ClearChildren(_runtime);
        if (projection is null)
        {
            _runtime.AddChild(StudioStyle.Label(
                "Lancez l’aperçu sec pour projeter les décisions d’autorité.",
                10,
                StudioStyle.Muted,
                wrap: true));
            return;
        }
        var lines = new[]
        {
            $"Autorité serveur · {projection.Authority}",
            $"PvP · {projection.Pvp}",
            $"Tir allié · {projection.FriendlyFire}",
            $"Crime · {projection.Crime}",
            $"Aggro · {projection.Aggro}",
            $"Dégâts environnementaux · {projection.EnvironmentalDamage}",
            projection.Boundary,
        };
        foreach (var line in lines)
            _runtime.AddChild(StudioStyle.Label("• " + line, 10, StudioStyle.Text, wrap: true));
    }

    private void RenderDiagnostics(IReadOnlyList<StudioUx2LwDDiagnostic> diagnostics)
    {
        ClearChildren(_diagnostics);
        if (diagnostics.Count == 0)
        {
            _diagnostics.AddChild(StudioStyle.Label("Aucun blocage localisé.", 10, StudioStyle.Muted));
            return;
        }
        foreach (var diagnostic in diagnostics)
        {
            var location = diagnostic.Path.EndsWith("serverAuthoritative", StringComparison.Ordinal)
                ? "Carte Autorité serveur"
                : "Carte Combat";
            _diagnostics.AddChild(StudioStyle.Label(
                $"{location} · {diagnostic.Message}",
                10,
                diagnostic.Blocking ? StudioStyle.Danger : StudioStyle.Warning,
                wrap: true));
        }
    }

    private static PanelContainer Card(string name, string title, string description, Control content)
    {
        var panel = StudioStyle.Panel(name, StudioStyle.SurfaceRaised, 8, 1);
        var margin = StudioStyle.Margin(12, 9, 12, 9);
        panel.AddChild(margin);
        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 5);
        margin.AddChild(root);
        root.AddChild(StudioStyle.Label(title, 13));
        root.AddChild(StudioStyle.Label(description, 10, StudioStyle.Muted, wrap: true));
        root.AddChild(content);
        return panel;
    }

    private static void ClearChildren(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static IEnumerable<Label> DescendantLabels(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Label label)
                yield return label;
            foreach (var descendant in DescendantLabels(child))
                yield return descendant;
        }
    }

    private void UpdateDialogBounds()
    {
        var viewport = GetViewportRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0)
            return;
        var scale = viewport.X >= 3_200 ? 1.72f : viewport.X >= 2_300 ? 1.24f : 1f;
        var width = Math.Clamp((viewport.X - 52f) / scale, 900f, 1_420f);
        var height = Math.Clamp((viewport.Y - 42f) / scale, 690f, 980f);
        var renderedWidth = width * scale;
        var renderedHeight = height * scale;
        _dialog.Scale = Vector2.One * scale;
        _dialog.CustomMaximumSize = new Vector2(width, height);
        _dialog.Size = new Vector2(width, height);
        _dialog.Position = new Vector2(
            (viewport.X - renderedWidth) / 2f,
            Math.Max(20f, (viewport.Y - renderedHeight) * .45f));
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }
}
