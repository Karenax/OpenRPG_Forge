using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioTriggerDiscoveryAccessOverlay : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx1AEEvidenceSourceSnapshot? _evidenceSourceSnapshot;
    private string? _evidenceTempRoot;
    private bool _lastEvidenceSourceUntouched = true;
    private bool _lastEvidenceResourcesReleased = true;
    private bool _lastSourceRestored = true;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _trigger;
    private readonly Label _selection;
    private readonly Button _discoveryTab;
    private readonly Button _interactionTab;
    private readonly Button _accessTab;
    private readonly VBoxContainer _discoveryPanel;
    private readonly VBoxContainer _interactionPanel;
    private readonly VBoxContainer _accessPanel;

    private readonly CheckBox _discoveryEnabled;
    private readonly StudioTriggerStringListEditor _senses;
    private readonly SpinBox _difficulty;
    private readonly OptionButton _revealTarget;
    private readonly LineEdit _feedback;
    private readonly CheckBox _permanent;
    private readonly OptionButton _scope;

    private readonly CheckBox _interactionEnabled;
    private readonly LineEdit _verb;
    private readonly LineEdit _icon;
    private readonly LineEdit _outline;
    private readonly LineEdit _prompt;
    private readonly OptionButton _script;
    private readonly Label _scriptHint;
    private readonly CheckBox _visibleInteraction;
    private readonly LineEdit _permission;

    private readonly CheckBox _accessEnabled;
    private readonly OptionButton _credential;
    private readonly LineEdit _customCredential;
    private readonly CheckBox _consumeCredential;
    private readonly LineEdit _deniedFeedback;

    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;

    private IReadOnlyList<StudioTriggerDiscoveryAccessChoice> _choices = [];
    private IReadOnlyList<StudioTriggerRevealTargetChoice> _targetChoices = [];
    private IReadOnlyList<StudioTriggerScriptChoice> _scriptChoices = [];
    private IReadOnlyList<StudioTriggerCredentialChoice> _credentialChoices = [];
    private StudioTriggerDiscoveryAccessDraft? _draft;
    private bool _updating;
    private bool _applied;
    private bool _runtimePreviewReady;
    private int _section;

    internal event Action<string, bool>? StatusChanged;

    internal event Action? TriggerChanged;

    internal bool IsOpen => Visible;

    internal int SectionCount => 3;

    internal int ChoiceCount => _choices.Count;

    internal bool HasExplicitSelection => _draft is not null && _trigger.Selected > 0;

    internal bool RuntimePreviewReady => _runtimePreviewReady;

    internal bool UsesIsolatedEvidence =>
        _evidenceSession is not null && ReferenceEquals(_session, _evidenceSession);

    internal bool EvidenceSourceUntouched => _evidenceSourceSnapshot is { } snapshot
        ? SourceMatchesSnapshot(snapshot)
        : _lastEvidenceSourceUntouched;

    internal bool EvidenceResourcesReleased =>
        _evidenceSession is null && _evidenceTempRoot is null && _lastEvidenceResourcesReleased;

    internal bool SourceSessionRestored =>
        ReferenceEquals(_session, _sourceSession) && _lastSourceRestored;

    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ',
                _selection.Text,
                _summary.Text,
                _status.Text,
                _discoveryTab.Text,
                _interactionTab.Text,
                _accessTab.Text,
                _scriptHint.Text);
            return HasExplicitSelection && SectionCount == 3 &&
                   text.Contains("Secret", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Accès", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains(".json", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal StudioTriggerDiscoveryAccessOverlay(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = _sourceSession;
        Name = "StudioTriggerDiscoveryAccessOverlay";
        Visible = false;
        ZIndex = 450;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect
        {
            Color = new Color(0, 0, 0, .8f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        _dialog = StudioStyle.Panel("TriggerDiscoveryAccessDialog", StudioStyle.Chrome, 12, 2);
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
        root.AddThemeConstantOverride("separation", 9);
        margin.AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        root.AddChild(header);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("DÉCOUVERTE & ACCÈS", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Secret, clic et justificatif", 20));
        heading.AddChild(StudioStyle.Label(
            "ORF-TRG-005 · ORF-TRG-008 · ORF-TRG-012 · ORF-TRG-013 · aperçu obligatoire avant application",
            10,
            StudioStyle.Muted,
            wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.TooltipText = "Fermer sans appliquer le brouillon · Échap";
        close.Pressed += Close;
        header.AddChild(close);

        var selector = new HBoxContainer();
        selector.AddThemeConstantOverride("separation", 8);
        root.AddChild(selector);
        selector.AddChild(StudioStyle.Label("Déclencheur à configurer", 10, StudioStyle.Muted));
        _trigger = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_trigger);
        _trigger.ItemSelected += SelectTrigger;
        selector.AddChild(_trigger);
        _selection = StudioStyle.Label(
            "Choisissez explicitement un déclencheur doté d’un volume de base.",
            11,
            StudioStyle.Warning,
            wrap: true);
        root.AddChild(_selection);

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 6);
        root.AddChild(tabs);
        _discoveryTab = Tab("Secret & révélation");
        _discoveryTab.Pressed += () => ShowSection(0);
        tabs.AddChild(_discoveryTab);
        _interactionTab = Tab("Clic & présentation");
        _interactionTab.Pressed += () => ShowSection(1);
        tabs.AddChild(_interactionTab);
        _accessTab = Tab("Accès & justificatif");
        _accessTab.Pressed += () => ShowSection(2);
        tabs.AddChild(_accessTab);

        var bodyHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 330),
            CustomMaximumSize = new Vector2(-1, 560),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            PropagateMaximumSize = true,
        };
        root.AddChild(bodyHost);
        var body = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true,
        };
        body.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bodyHost.AddChild(body);
        var panels = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddChild(panels);

        _discoveryPanel = PanelColumn();
        panels.AddChild(_discoveryPanel);
        _discoveryPanel.AddChild(StudioStyle.Label("Secret et révélation", 17));
        _discoveryPanel.AddChild(StudioStyle.Label(
            "Décrivez comment ce déclencheur devient visible et quel objet de la zone est révélé. La simulation vérifie aussi le cas encore caché.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _discoveryEnabled = Check("Activer la découverte de ce secret");
        _discoveryPanel.AddChild(_discoveryEnabled);
        var discoveryColumns = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        discoveryColumns.AddThemeConstantOverride("separation", 10);
        _discoveryPanel.AddChild(discoveryColumns);
        var sensesColumn = PanelColumn();
        sensesColumn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        discoveryColumns.AddChild(sensesColumn);
        sensesColumn.AddChild(StudioStyle.SectionLabel("SENS DE DÉCOUVERTE"));
        _senses = new StudioTriggerStringListEditor(
            "Ajoutez au moins un sens lorsque la découverte est active.",
            "vue, fouille, magie…");
        sensesColumn.AddChild(_senses);
        var discoveryGrid = Grid();
        discoveryGrid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        discoveryColumns.AddChild(discoveryGrid);
        _difficulty = AddField(discoveryGrid, "Difficulté", Number(0, 1_000_000, 1));
        _revealTarget = AddField(discoveryGrid, "Cible révélée", Option());
        _feedback = AddField(discoveryGrid, "Feedback", Text("trigger.secret.revealed"));
        _permanent = AddField(discoveryGrid, "Mémoire", Check("Révélation permanente"));
        _scope = AddField(discoveryGrid, "Portée", Option());
        _scope.AddItem("Joueur seulement");
        _scope.AddItem("Groupe");
        _scope.AddItem("Monde partagé");

        _interactionPanel = PanelColumn();
        panels.AddChild(_interactionPanel);
        _interactionPanel.AddChild(StudioStyle.Label("Clic et présentation", 17));
        _interactionPanel.AddChild(StudioStyle.Label(
            "Le joueur voit un verbe, un curseur, une surbrillance et un texte. Le script OnClick reste limité à la liste Validation du volume de base.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _interactionEnabled = Check("Permettre le clic sur ce déclencheur");
        _interactionPanel.AddChild(_interactionEnabled);
        var interactionGrid = Grid();
        _interactionPanel.AddChild(interactionGrid);
        _verb = AddField(interactionGrid, "Verbe", Text("inspecter"));
        _icon = AddField(interactionGrid, "Curseur", Text("cursor.inspect"));
        _outline = AddField(interactionGrid, "Surbrillance", Text("outline.interactive"));
        _prompt = AddField(interactionGrid, "Texte affiché", Text("trigger.inspect"));
        _script = AddField(interactionGrid, "Action OnClick", Option());
        _visibleInteraction = AddField(interactionGrid, "Présentation", Check("Afficher l’interaction"));
        _permission = AddField(interactionGrid, "Permission", Text("trigger.use"));
        _scriptHint = StudioStyle.Label(
            "Les scripts non déclarés par Validation sont visibles mais ne peuvent pas être appliqués.",
            10,
            StudioStyle.Warning,
            wrap: true);
        _interactionPanel.AddChild(_scriptHint);

        _accessPanel = PanelColumn();
        panels.AddChild(_accessPanel);
        _accessPanel.AddChild(StudioStyle.Label("Accès et justificatif", 17));
        _accessPanel.AddChild(StudioStyle.Label(
            "Choisissez un objet clé du projet ou saisissez un justificatif métier nommé. La consommation n’est activée que sur demande explicite.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _accessEnabled = Check("Exiger un justificatif");
        _accessPanel.AddChild(_accessEnabled);
        var accessGrid = Grid();
        _accessPanel.AddChild(accessGrid);
        _credential = AddField(accessGrid, "Justificatif", Option());
        _customCredential = AddField(accessGrid, "Justificatif nommé", Text("quest:secret-authorized"));
        _consumeCredential = AddField(accessGrid, "Après usage", Check("Consommer l’objet clé"));
        _deniedFeedback = AddField(accessGrid, "Refus affiché", Text("trigger.access.denied"));
        _accessPanel.AddChild(StudioStyle.Label(
            "L’aperçu montre séparément : secret caché, justificatif manquant, permission manquante et accès autorisé.",
            10,
            StudioStyle.Warning,
            wrap: true));

        _summary = StudioStyle.Label(
            "Sélectionnez un déclencheur puis prévisualisez · aucun changement appliqué.",
            10,
            StudioStyle.Muted,
            wrap: true);
        root.AddChild(_summary);
        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 7);
        root.AddChild(footer);
        _preview = StudioStyle.Button("1 · Prévisualiser", compact: true);
        _preview.Pressed += Preview;
        footer.AddChild(_preview);
        _apply = StudioStyle.Button("2 · Appliquer", primary: true, compact: true);
        _apply.Disabled = true;
        _apply.Pressed += Apply;
        footer.AddChild(_apply);
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _undo = StudioStyle.Button("Annuler découverte & accès", compact: true);
        _undo.Disabled = true;
        _undo.Pressed += Undo;
        footer.AddChild(_undo);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.Disabled = true;
        _redo.Pressed += Redo;
        footer.AddChild(_redo);
        _status = StudioStyle.Label("Prêt · sélection explicite requise", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        ConnectInvalidation();
        Resized += UpdateDialogBounds;
        UpdateDialogBounds();
        ShowSection(0);
        SetEditingEnabled(false);
    }

    internal void Open()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null)
        {
            Close();
        }
        _session = _sourceSession;
        OpenBoundSession();
    }

    private void OpenBoundSession()
    {
        _session.CancelTriggerDiscoveryAccessUx1AE();
        _choices = _session.TriggerDiscoveryAccessChoicesUx1AE;
        _trigger.Clear();
        _trigger.AddItem("Choisissez explicitement un déclencheur…");
        foreach (var choice in _choices)
        {
            _trigger.AddItem(choice.Label);
        }
        _trigger.Select(0);
        _draft = null;
        _applied = false;
        _runtimePreviewReady = false;
        _selection.Text = _choices.Count == 0
            ? "Aucun profil de déclencheur n’est disponible dans la zone active."
            : "Choisissez explicitement un déclencheur doté d’un volume de base.";
        _selection.AddThemeColorOverride(
            "font_color",
            _choices.Count == 0 ? StudioStyle.Danger : StudioStyle.Warning);
        _summary.Text = "Sélectionnez un déclencheur puis prévisualisez · aucun changement appliqué.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        _status.Text = "Prêt · sélection explicite requise";
        SetEditingEnabled(false);
        _undo.Disabled = true;
        _redo.Disabled = true;
        Visible = true;
        MoveToFront();
        ShowSection(0);
        UpdateDialogBounds();
        _trigger.GrabFocus();
    }

    internal void Close()
    {
        var sourceUntouched = _evidenceSourceSnapshot is not { } snapshot || SourceMatchesSnapshot(snapshot);
        _session.CancelTriggerDiscoveryAccessUx1AE();
        Visible = false;
        _draft = null;
        _applied = false;
        _runtimePreviewReady = false;
        var released = ReleaseIsolatedEvidenceSession();
        _lastEvidenceSourceUntouched = sourceUntouched;
        _lastEvidenceResourcesReleased = released;
        _lastSourceRestored = ReferenceEquals(_session, _sourceSession);
    }

    internal bool PrepareEvidence(string mode)
    {
        Open();
        if (!TryOpenIsolatedEvidenceSession())
        {
            return false;
        }
        var index = _choices.ToList().FindIndex(item =>
            item.IsEvidence && item.HasFoundation && item.HasDiscovery && item.HasInteraction && item.HasAccess);
        if (index < 0)
        {
            Close();
            return false;
        }
        _trigger.Select(index + 1);
        LoadChoice(index);
        if (_draft is null)
        {
            Close();
            return false;
        }
        ShowSection(mode.Contains("click", StringComparison.OrdinalIgnoreCase) ||
                    mode.Contains("interaction", StringComparison.OrdinalIgnoreCase) ? 1 :
            mode.Contains("access", StringComparison.OrdinalIgnoreCase) ||
            mode.Contains("credential", StringComparison.OrdinalIgnoreCase) ? 2 : 0);
        Preview();
        _selection.Text += " · session de preuve isolée";
        _summary.Text =
            "PREUVE ISOLÉE · le projet source reste strictement inchangé.\n" + _summary.Text;
        SetStatus("Preuve isolée prête · fermeture = suppression de la session temporaire", error: false);
        return !_apply.Disabled && HasExplicitSelection && RuntimePreviewReady && ShowsHumanSurface &&
               UsesIsolatedEvidence && EvidenceSourceUntouched;
    }

    internal bool RunUiTransactionEvidence()
    {
        var sourceSnapshot = CaptureSourceSnapshot();
        if (sourceSnapshot is null)
        {
            return false;
        }
        try
        {
            var ready = PrepareEvidence("ux1-ae-discovery-access");
            var transactionSession = _session;
            var originalProject = transactionSession.Project;
            var originalDirty = transactionSession.IsDirty;
            var transactionHistory = transactionSession.History;
            var originalRevision = transactionHistory?.State.Revision;
            var originalJournal = transactionHistory?.Journal.ToArray() ?? [];
            var originalCanUndo = transactionHistory?.CanUndo;
            var originalCanRedo = transactionHistory?.CanRedo;
            var dryRun = ready && originalProject is not null &&
                         ReferenceEquals(originalProject, transactionSession.Project) &&
                         transactionSession.IsDirty == originalDirty &&
                         ReferenceEquals(transactionHistory, transactionSession.History) &&
                         transactionHistory?.State.Revision == originalRevision &&
                         (transactionHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                         transactionHistory?.CanUndo == originalCanUndo &&
                         transactionHistory?.CanRedo == originalCanRedo &&
                         UsesIsolatedEvidence && SourceMatchesSnapshot(sourceSnapshot);
            Apply();
            var appliedProject = transactionSession.Project;
            var applied = dryRun && _applied && !ReferenceEquals(originalProject, appliedProject);
            if (applied)
            {
                Undo();
            }
            var undone = applied && ReferenceEquals(originalProject, transactionSession.Project) &&
                         transactionSession.IsDirty == originalDirty;
            if (undone)
            {
                Redo();
            }
            var redone = undone && ReferenceEquals(appliedProject, transactionSession.Project);
            if (redone)
            {
                Undo();
            }
            var restored = redone && ReferenceEquals(originalProject, transactionSession.Project) &&
                           transactionSession.IsDirty == originalDirty;
            var sourceUntouched = SourceMatchesSnapshot(sourceSnapshot);
            Close();
            return dryRun && applied && undone && redone && restored && sourceUntouched &&
                   EvidenceResourcesReleased && SourceSessionRestored;
        }
        catch (Exception exception)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AE_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}");
            Close();
            return false;
        }
    }

    private bool TryOpenIsolatedEvidenceSession()
    {
        var snapshot = CaptureSourceSnapshot();
        var evidenceProject = _sourceSession.CreateUx1AEEvidenceProject();
        if (snapshot is null || evidenceProject is null)
        {
            return false;
        }

        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "openrpg-forge-studio-ux1ae-evidence-" + Guid.NewGuid().ToString("N"));
        var evidenceSession = new StudioSessionController();
        try
        {
            var created = ProjectSession.Create(tempRoot, evidenceProject);
            if (!created.Succeeded ||
                !evidenceSession.Open(tempRoot, snapshot.ActiveAreaId?.ToString()).Succeeded)
            {
                evidenceSession.Close();
                _lastEvidenceResourcesReleased = DeleteEvidenceRoot(tempRoot);
                _evidenceTempRoot = _lastEvidenceResourcesReleased ? null : tempRoot;
                _session = _sourceSession;
                _lastSourceRestored = true;
                return false;
            }

            _evidenceSourceSnapshot = snapshot;
            _evidenceSession = evidenceSession;
            _evidenceTempRoot = tempRoot;
            _session = evidenceSession;
            _lastEvidenceResourcesReleased = false;
            _lastSourceRestored = false;
            OpenBoundSession();
            var ready = _choices.Any(item => item.IsEvidence && item.HasFoundation) &&
                        SourceMatchesSnapshot(snapshot);
            if (!ready)
            {
                Close();
            }
            return ready;
        }
        catch (Exception exception)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AE_EVIDENCE_FAILED type={exception.GetType().Name} message={exception.Message}");
            evidenceSession.Close();
            var released = DeleteEvidenceRoot(tempRoot);
            _evidenceSourceSnapshot = null;
            _evidenceSession = null;
            _evidenceTempRoot = released ? null : tempRoot;
            _session = _sourceSession;
            _lastEvidenceResourcesReleased = released;
            _lastSourceRestored = true;
            return false;
        }
    }

    private StudioUx1AEEvidenceSourceSnapshot? CaptureSourceSnapshot()
    {
        var project = _sourceSession.Project;
        var history = _sourceSession.History;
        if (project is null || history is null)
        {
            return null;
        }
        return new StudioUx1AEEvidenceSourceSnapshot(
            project,
            history,
            history.State.Revision,
            history.Journal.ToArray(),
            _sourceSession.IsDirty,
            history.CanUndo,
            history.CanRedo,
            _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(),
            _sourceSession.SelectedEntityId);
    }

    private bool SourceMatchesSnapshot(StudioUx1AEEvidenceSourceSnapshot snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) &&
               history is not null &&
               history.State.Revision == snapshot.Revision &&
               history.Journal.SequenceEqual(snapshot.Journal) &&
               history.CanUndo == snapshot.CanUndo &&
               history.CanRedo == snapshot.CanRedo &&
               _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId;
    }

    private bool ReleaseIsolatedEvidenceSession()
    {
        var evidenceSession = _evidenceSession;
        var tempRoot = _evidenceTempRoot;
        _evidenceSession = null;
        _evidenceTempRoot = null;
        _evidenceSourceSnapshot = null;
        _session = _sourceSession;
        var released = true;
        try
        {
            evidenceSession?.Close();
        }
        catch (Exception exception)
        {
            released = false;
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AE_EVIDENCE_RELEASE_FAILED type={exception.GetType().Name} message={exception.Message}");
        }
        var rootReleased = DeleteEvidenceRoot(tempRoot);
        if (!rootReleased)
        {
            _evidenceTempRoot = tempRoot;
        }
        return released && rootReleased;
    }

    private static bool DeleteEvidenceRoot(string? tempRoot)
    {
        if (tempRoot is null)
        {
            return true;
        }
        try
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            return !Directory.Exists(tempRoot);
        }
        catch (Exception exception)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AE_EVIDENCE_DELETE_FAILED type={exception.GetType().Name} message={exception.Message}");
            return false;
        }
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
        if (_evidenceSession is not null || _evidenceTempRoot is not null)
        {
            Close();
        }
    }

    private void SelectTrigger(long index)
    {
        if (_updating)
        {
            return;
        }
        if (index <= 0)
        {
            _draft = null;
            _runtimePreviewReady = false;
            SetEditingEnabled(false);
            _selection.Text = "Choisissez explicitement un déclencheur doté d’un volume de base.";
            _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
            return;
        }
        LoadChoice((int)index - 1);
    }

    private void LoadChoice(int choiceIndex)
    {
        _session.CancelTriggerDiscoveryAccessUx1AE();
        _draft = null;
        _runtimePreviewReady = false;
        _applied = false;
        _apply.Disabled = true;
        _undo.Disabled = true;
        _redo.Disabled = true;
        if (choiceIndex < 0 || choiceIndex >= _choices.Count)
        {
            SetEditingEnabled(false);
            return;
        }
        var choice = _choices[choiceIndex];
        var draft = _session.CreateTriggerDiscoveryAccessDraftUx1AE(choice.ProfileId);
        if (draft is null)
        {
            _selection.Text = choice.HasFoundation
                ? "Ce déclencheur a changé. Fermez puis rouvrez la fiche."
                : "Volume de base requis · configurez d’abord la forme, le filtre et les événements avec l’outil Déclencheur.";
            _selection.AddThemeColorOverride("font_color", StudioStyle.Danger);
            _summary.Text = "Aucune modification possible sans Foundation canonique.";
            _summary.AddThemeColorOverride("font_color", StudioStyle.Danger);
            SetEditingEnabled(false);
            return;
        }

        _draft = draft;
        _targetChoices = _session.TriggerRevealTargetChoicesUx1AE(choice.ProfileId);
        _scriptChoices = _session.TriggerScriptChoicesUx1AE(choice.ProfileId);
        _credentialChoices = _session.TriggerCredentialChoicesUx1AE(choice.ProfileId);
        Populate(draft);
        _selection.Text = choice.Label;
        _selection.AddThemeColorOverride("font_color", StudioStyle.Success);
        _summary.Text = "Brouillon chargé · prévisualisez les quatre états avant application.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        SetEditingEnabled(true);
        SetStatus("Brouillon local · aucun document modifié", error: false);
    }

    private void Populate(StudioTriggerDiscoveryAccessDraft draft)
    {
        _updating = true;
        _discoveryEnabled.ButtonPressed = draft.DiscoveryEnabled;
        _senses.SetValues(draft.Senses);
        _difficulty.Value = draft.Difficulty;
        PopulateTargets(draft.RevealTargetId);
        _feedback.Text = draft.FeedbackId;
        _permanent.ButtonPressed = draft.Permanent;
        _scope.Select((int)draft.Scope);

        _interactionEnabled.ButtonPressed = draft.InteractionEnabled;
        _verb.Text = draft.Verb;
        _icon.Text = draft.Icon;
        _outline.Text = draft.Outline;
        _prompt.Text = draft.PromptKey;
        PopulateScripts(draft.ScriptId);
        _visibleInteraction.ButtonPressed = draft.Visible;
        _permission.Text = draft.Permission;

        _accessEnabled.ButtonPressed = draft.AccessEnabled;
        PopulateCredentials(draft.CredentialId);
        _consumeCredential.ButtonPressed = draft.ConsumeCredential;
        _deniedFeedback.Text = draft.DeniedFeedbackKey;
        _updating = false;
        UpdateCredentialMode();
    }

    private void PopulateTargets(Guid selected)
    {
        _revealTarget.Clear();
        var selectedIndex = -1;
        for (var index = 0; index < _targetChoices.Count; index++)
        {
            var choice = _targetChoices[index];
            _revealTarget.AddItem(choice.Label);
            _revealTarget.SetItemDisabled(index, !choice.Resolved);
            if (choice.TargetId == selected)
            {
                selectedIndex = index;
            }
        }
        if (_targetChoices.Count == 0)
        {
            _revealTarget.AddItem("Aucune cible disponible dans la zone");
            _revealTarget.SetItemDisabled(0, true);
            _revealTarget.Select(0);
        }
        else
        {
            _revealTarget.Select(Math.Max(0, selectedIndex));
        }
    }

    private void PopulateScripts(string selected)
    {
        _script.Clear();
        var selectedIndex = -1;
        for (var index = 0; index < _scriptChoices.Count; index++)
        {
            var choice = _scriptChoices[index];
            _script.AddItem(choice.AllowedByValidation ? choice.Label : $"{choice.Label} · non autorisé");
            _script.SetItemDisabled(index, !choice.AllowedByValidation);
            if (string.Equals(choice.ScriptId, selected, StringComparison.Ordinal))
            {
                selectedIndex = index;
            }
        }
        if (_scriptChoices.Count == 0)
        {
            _script.AddItem("Aucun script déclaré dans Validation");
            _script.SetItemDisabled(0, true);
            _script.Select(0);
        }
        else
        {
            _script.Select(Math.Max(0, selectedIndex));
        }
    }

    private void PopulateCredentials(string selected)
    {
        _credential.Clear();
        _credential.AddItem("Justificatif nommé…");
        var selectedIndex = 0;
        for (var index = 0; index < _credentialChoices.Count; index++)
        {
            var choice = _credentialChoices[index];
            _credential.AddItem(choice.Label);
            if (string.Equals(choice.CredentialId, selected, StringComparison.Ordinal))
            {
                selectedIndex = index + 1;
            }
        }
        _credential.Select(selectedIndex);
        _customCredential.Text = selectedIndex == 0 ? selected : string.Empty;
    }

    private StudioTriggerDiscoveryAccessDraft? ReadDraft()
    {
        if (_draft is null)
        {
            return null;
        }
        var targetIndex = _targetChoices.Count == 0 ? -1 : (int)_revealTarget.Selected;
        var scriptIndex = _scriptChoices.Count == 0 ? -1 : (int)_script.Selected;
        var credentialIndex = (int)_credential.Selected - 1;
        return _draft with
        {
            DiscoveryEnabled = _discoveryEnabled.ButtonPressed,
            Senses = _senses.Values,
            Difficulty = (int)Math.Round(_difficulty.Value),
            RevealTargetId = targetIndex >= 0 && targetIndex < _targetChoices.Count
                ? _targetChoices[targetIndex].TargetId
                : Guid.Empty,
            FeedbackId = _feedback.Text,
            Permanent = _permanent.ButtonPressed,
            Scope = (TriggerWorldProfile.DiscoveryScope)Math.Clamp((int)_scope.Selected, 0, 2),
            InteractionEnabled = _interactionEnabled.ButtonPressed,
            Verb = _verb.Text,
            Icon = _icon.Text,
            Outline = _outline.Text,
            PromptKey = _prompt.Text,
            ScriptId = scriptIndex >= 0 && scriptIndex < _scriptChoices.Count
                ? _scriptChoices[scriptIndex].ScriptId
                : string.Empty,
            Visible = _visibleInteraction.ButtonPressed,
            Permission = _permission.Text,
            AccessEnabled = _accessEnabled.ButtonPressed,
            CredentialId = credentialIndex >= 0 && credentialIndex < _credentialChoices.Count
                ? _credentialChoices[credentialIndex].CredentialId
                : _customCredential.Text,
            ConsumeCredential = _consumeCredential.ButtonPressed,
            DeniedFeedbackKey = _deniedFeedback.Text,
        };
    }

    private void Preview()
    {
        var draft = ReadDraft();
        if (draft is null)
        {
            return;
        }
        var result = _session.PreviewTriggerDiscoveryAccessUx1AE(draft);
        _runtimePreviewReady = result.Succeeded && result.HiddenBlocked && result.CredentialBlocked &&
                               result.PermissionBlocked && result.Allowed;
        _apply.Disabled = !result.CanApply || !_runtimePreviewReady;
        _summary.Text = result.Succeeded ? result.Summary : result.Message;
        _summary.AddThemeColorOverride(
            "font_color",
            result.Succeeded ? StudioStyle.Success : StudioStyle.Danger);
        SetStatus(result.Message, error: !result.Succeeded);
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedTriggerDiscoveryAccessUx1AE();
        SetStatus(result.Message, error: !result.Succeeded);
        if (!result.Succeeded)
        {
            _apply.Disabled = true;
            _runtimePreviewReady = false;
            return;
        }
        _applied = true;
        _apply.Disabled = true;
        _preview.Disabled = true;
        _undo.Disabled = false;
        _redo.Disabled = true;
        SetEditingEnabled(false);
        _summary.Text =
            "Appliqué · seuls Secret, Clic & présentation et Accès ont changé. Foundation, Portal, Validation et profils voisins sont préservés.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Success);
        TriggerChanged?.Invoke();
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(result.Message, error: !result.Succeeded);
        if (result.Succeeded)
        {
            _applied = false;
            _undo.Disabled = true;
            _redo.Disabled = false;
            TriggerChanged?.Invoke();
        }
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(result.Message, error: !result.Succeeded);
        if (result.Succeeded)
        {
            _applied = true;
            _undo.Disabled = false;
            _redo.Disabled = true;
            TriggerChanged?.Invoke();
        }
    }

    private void ShowSection(int section)
    {
        _section = Math.Clamp(section, 0, 2);
        _discoveryPanel.Visible = _section == 0;
        _interactionPanel.Visible = _section == 1;
        _accessPanel.Visible = _section == 2;
        ApplyTabStyle(_discoveryTab, _section == 0);
        ApplyTabStyle(_interactionTab, _section == 1);
        ApplyTabStyle(_accessTab, _section == 2);
    }

    private void ConnectInvalidation()
    {
        foreach (var edit in new[]
                 {
                     _feedback, _verb, _icon, _outline, _prompt, _permission,
                     _customCredential, _deniedFeedback,
                 })
        {
            edit.TextChanged += _ => InvalidatePreview();
        }
        _senses.Changed += InvalidatePreview;
        _difficulty.ValueChanged += _ => InvalidatePreview();
        _revealTarget.ItemSelected += _ => InvalidatePreview();
        _scope.ItemSelected += _ => InvalidatePreview();
        _script.ItemSelected += _ => InvalidatePreview();
        _credential.ItemSelected += _ =>
        {
            UpdateCredentialMode();
            InvalidatePreview();
        };
        foreach (var check in new[]
                 {
                     _discoveryEnabled, _permanent, _interactionEnabled, _visibleInteraction,
                     _accessEnabled, _consumeCredential,
                 })
        {
            check.Toggled += _ => InvalidatePreview();
        }
    }

    private void InvalidatePreview()
    {
        if (_updating || _draft is null || _applied)
        {
            return;
        }
        _session.CancelTriggerDiscoveryAccessUx1AE();
        _runtimePreviewReady = false;
        _apply.Disabled = true;
        _preview.Disabled = false;
        _summary.Text = "Aperçu à recalculer · aucun changement appliqué.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
    }

    private void UpdateCredentialMode()
    {
        _customCredential.Editable = !_credential.Disabled && _credential.Selected == 0;
        _customCredential.TooltipText = _credential.Selected == 0
            ? "Nom métier stable du justificatif."
            : "Un objet clé nommé est sélectionné.";
    }

    private void SetEditingEnabled(bool enabled)
    {
        foreach (var edit in new[]
                 {
                     _feedback, _verb, _icon, _outline, _prompt, _permission,
                     _customCredential, _deniedFeedback,
                 })
        {
            edit.Editable = enabled;
        }
        _difficulty.Editable = enabled;
        foreach (var option in new[] { _revealTarget, _scope, _script, _credential })
        {
            option.Disabled = !enabled;
        }
        foreach (var check in new[]
                 {
                     _discoveryEnabled, _permanent, _interactionEnabled, _visibleInteraction,
                     _accessEnabled, _consumeCredential,
                 })
        {
            check.Disabled = !enabled;
        }
        _senses.SetEditingEnabled(enabled);
        _discoveryTab.Disabled = !enabled;
        _interactionTab.Disabled = !enabled;
        _accessTab.Disabled = !enabled;
        _preview.Disabled = !enabled;
        if (!enabled)
        {
            _apply.Disabled = true;
        }
        UpdateCredentialMode();
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Success);
        StatusChanged?.Invoke(message, error);
    }

    private void UpdateDialogBounds()
    {
        var scale = Size.X >= 3200 ? 1.72f : Size.X >= 2300 ? 1.24f : 1f;
        var width = Math.Clamp((Size.X - 56) / scale, 900, 1160);
        var height = Math.Clamp((Size.Y - 72) / scale, 680, 850);
        var renderedWidth = width * scale;
        var renderedHeight = height * scale;
        _dialog.Scale = Vector2.One * scale;
        _dialog.CustomMaximumSize = new Vector2(width, height);
        _dialog.Position = new Vector2(
            (Size.X - renderedWidth) * .5f,
            Math.Max(24, (Size.Y - renderedHeight) * .45f));
        _dialog.Size = new Vector2(width, height);
    }

    private static VBoxContainer PanelColumn()
    {
        var panel = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.AddThemeConstantOverride("separation", 8);
        return panel;
    }

    private static Button Tab(string text)
    {
        var button = StudioStyle.Button(text, compact: true);
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return button;
    }

    private static void ApplyTabStyle(Button button, bool selected)
    {
        button.AddThemeStyleboxOverride(
            "normal",
            StudioStyle.Box(
                selected ? StudioStyle.Selection : StudioStyle.SurfaceRaised,
                selected ? StudioStyle.Success : StudioStyle.Border,
                6,
                selected ? 2 : 1));
    }

    private static GridContainer Grid() => new()
    {
        Columns = 2,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
    };

    private static T AddField<T>(GridContainer grid, string label, T control) where T : Control
    {
        var title = StudioStyle.Label(label, 10, StudioStyle.Muted);
        title.CustomMinimumSize = new Vector2(180, 0);
        title.VerticalAlignment = VerticalAlignment.Center;
        grid.AddChild(title);
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        grid.AddChild(control);
        return control;
    }

    private static LineEdit Text(string placeholder)
    {
        var value = new LineEdit { PlaceholderText = placeholder };
        StudioStyle.ApplyLineEdit(value);
        return value;
    }

    private static SpinBox Number(double min, double max, double step)
    {
        var value = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            AllowGreater = false,
            AllowLesser = false,
        };
        StudioStyle.ApplySpinBox(value);
        return value;
    }

    private static OptionButton Option()
    {
        var value = new OptionButton();
        StudioStyle.ApplyOptionButton(value);
        return value;
    }

    private static CheckBox Check(string text)
    {
        var value = new CheckBox { Text = text, FocusMode = FocusModeEnum.All };
        value.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        value.AddThemeColorOverride("font_color", StudioStyle.Text);
        return value;
    }
}
