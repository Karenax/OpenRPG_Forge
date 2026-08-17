using Forge.Domain.Areas;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioAreaUserEventsOverlayUx2LwC : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceTempRoot;
    private StudioUx2LwCEvidenceSourceSnapshot? _evidenceSourceSnapshot;
    private readonly PanelContainer _dialog;
    private readonly OptionButton _area;
    private readonly OptionButton _event;
    private readonly Label _selection;
    private readonly LineEdit _name;
    private readonly LineEdit _schema;
    private readonly SpinBox _payloadVersion;
    private readonly Label _apiVersion;
    private readonly OptionButton _script;
    private readonly OptionButton _handler;
    private readonly Label _scriptProvenance;
    private readonly CheckBox _required;
    private readonly TextEdit _payload;
    private readonly CheckBox _delete;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _payloadSummary;
    private readonly Label _scriptSummary;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;
    private IReadOnlyList<StudioUx2LwCAreaChoice> _areaChoices = [];
    private IReadOnlyList<StudioUx2LwCEventChoice> _eventChoices = [];
    private IReadOnlyList<StudioUx2LwCScriptChoice> _scriptChoices = [];
    private readonly List<Guid?> _eventIds = [];
    private readonly List<string?> _scriptPaths = [];
    private readonly List<string?> _handlerValues = [];
    private StudioUx2LwCDraft? _draft;
    private AreaId? _lastAreaId;
    private Guid? _lastEventId;
    private StudioUx2LwCPreview? _lastPreview;
    private bool _updating;
    private bool _applied;
    private bool _canonicalPreviewReady;
    private bool _lastEvidenceSourceUntouched;
    private bool _lastEvidenceResourcesReleased;
    private bool _lastSourceRestored = true;
    private bool _lastEvidencePreviewDryRun;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? AreaUserEventsChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _areaChoices.Count;
    internal bool HasExplicitAreaSelection => _draft is not null && _area.Selected > 0;
    internal bool HasExplicitEventSelection => _draft is not null && _event.Selected > 0;
    internal bool CanonicalPreviewReady => _canonicalPreviewReady && _lastPreview?.Succeeded == true;
    internal bool ShowsTypedPayload => _lastPreview is { Succeeded: true } preview &&
                                       preview.PayloadSummary.Contains("(", StringComparison.Ordinal) &&
                                       _payload.Text.Contains(':');
    internal bool ShowsNoExecutionBoundary => _lastPreview is { Executed: false } preview &&
                                              preview.Message.Contains("aucun script n’a été exécuté",
                                                  StringComparison.OrdinalIgnoreCase);
    internal bool ShowsStructuredDiagnostics => _lastPreview is not null &&
                                                 _lastPreview.Diagnostics.All(item =>
                                                     !string.IsNullOrWhiteSpace(item.Code) &&
                                                     !string.IsNullOrWhiteSpace(item.Path));
    internal bool UsesRealScriptCatalog => _scriptChoices.Count > 0 && _scriptChoices.All(script =>
        _session.Session is { } workspace && File.Exists(Path.Combine(workspace.RootDirectory,
            script.RelativePath.Replace('/', Path.DirectorySeparatorChar))));
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
            return text.Contains("Événement de zone", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Payload typé", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun script", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("source locale", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !StudioSessionController.ContainsTechnicalIdentifierUx2LwC(text);
        }
    }

    internal StudioAreaUserEventsOverlayUx2LwC(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = _sourceSession;
        Name = "StudioAreaUserEventsOverlayUx2LwC";
        Visible = false;
        ZIndex = 466;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect
        {
            Color = new Color(0, 0, 0, .82f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        _dialog = StudioStyle.Panel("AreaUserEventsDialog", StudioStyle.Chrome, 12, 2);
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
        heading.AddChild(StudioStyle.Label("MONDE VIVANT · ÉVÉNEMENTS UTILISATEUR", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Événement de zone", 20));
        heading.AddChild(StudioStyle.Label(
            "AREA-024 · payload typé · compilation ForgeScript en aperçu sec · aucun script exécuté, " +
            "aucun dispatch réseau et aucune simulation moteur dans cet outil",
            10,
            StudioStyle.Muted,
            wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.TooltipText = "Fermer sans appliquer le brouillon · Échap";
        close.Pressed += Close;
        header.AddChild(close);

        var selectors = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selectors.AddThemeConstantOverride("h_separation", 10);
        selectors.AddThemeConstantOverride("v_separation", 6);
        root.AddChild(selectors);
        selectors.AddChild(StudioStyle.Label("Zone", 10, StudioStyle.Muted));
        selectors.AddChild(StudioStyle.Label("Événement", 10, StudioStyle.Muted));
        _area = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_area);
        _area.ItemSelected += index => SelectArea((int)index);
        selectors.AddChild(_area);
        _event = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_event);
        _event.ItemSelected += index => SelectEvent((int)index);
        selectors.AddChild(_event);
        _selection = StudioStyle.Label(
            "Choisissez explicitement une zone puis un événement ; aucune fiche n’est sélectionnée automatiquement.",
            11,
            StudioStyle.Warning,
            wrap: true);
        root.AddChild(_selection);

        var bodyHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 500),
            CustomMaximumSize = new Vector2(-1, 790),
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

        var identity = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        identity.AddThemeConstantOverride("h_separation", 10);
        identity.AddThemeConstantOverride("v_separation", 6);
        identity.AddChild(StudioStyle.Label("Nom dans l’espace de la zone", 10, StudioStyle.Muted));
        _name = NewLine("area.zone.signal");
        identity.AddChild(_name);
        identity.AddChild(StudioStyle.Label("Schéma de payload", 10, StudioStyle.Muted));
        _schema = NewLine("schema://area/signal/v1");
        identity.AddChild(_schema);
        identity.AddChild(StudioStyle.Label("Version du payload", 10, StudioStyle.Muted));
        _payloadVersion = new SpinBox { MinValue = 1, MaxValue = 999, Step = 1, Value = 1 };
        StudioStyle.ApplySpinBox(_payloadVersion);
        identity.AddChild(_payloadVersion);
        identity.AddChild(StudioStyle.Label("Contrat ForgeScript", 10, StudioStyle.Muted));
        _apiVersion = StudioStyle.Label("Non chargé", 10, StudioStyle.Text);
        identity.AddChild(_apiVersion);
        content.AddChild(Card(
            "AreaUserEventIdentity",
            "Identité et schéma",
            "Le nom reste dans l’espace de la zone ; le schéma décrit le payload persistant.",
            identity));

        var payloadRoot = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        payloadRoot.AddThemeConstantOverride("separation", 5);
        _payload = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 90),
            PlaceholderText = "message:string=Signal de zone\nintensite:int=2",
            WrapMode = TextEdit.LineWrappingMode.Boundary,
        };
        payloadRoot.AddChild(_payload);
        payloadRoot.AddChild(StudioStyle.Label(
            "Un champ par ligne : nom:type=valeur · types autorisés : string, bool, int, long, float, guid. " +
            "La valeur est un échantillon de validation et n’est pas persistée dans la fiche.",
            9,
            StudioStyle.Muted,
            wrap: true));
        content.AddChild(Card(
            "AreaUserEventPayload",
            "Payload typé",
            "Les champs sont validés par le contrat runtime avant toute application.",
            payloadRoot));

        var dispatch = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        dispatch.AddThemeConstantOverride("h_separation", 10);
        dispatch.AddThemeConstantOverride("v_separation", 6);
        dispatch.AddChild(StudioStyle.Label("Source locale", 10, StudioStyle.Muted));
        _script = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_script);
        dispatch.AddChild(_script);
        dispatch.AddChild(StudioStyle.Label("Point d’entrée", 10, StudioStyle.Muted));
        _handler = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_handler);
        dispatch.AddChild(_handler);
        dispatch.AddChild(StudioStyle.Label("Provenance", 10, StudioStyle.Muted));
        _scriptProvenance = StudioStyle.Label("Choisissez une source locale réelle.", 9, StudioStyle.Muted,
            wrap: true);
        dispatch.AddChild(_scriptProvenance);
        _required = new CheckBox { Text = "Source et handler obligatoires au runtime" };
        dispatch.AddChild(_required);
        dispatch.AddChild(StudioStyle.Label(
            "main reçoit le signal sans variables de champ ; un handler CustomEventRef reçoit la référence typée.",
            9,
            StudioStyle.Muted,
            wrap: true));
        content.AddChild(Card(
            "AreaUserEventDispatch",
            "Dispatch ForgeScript",
            "Seules les sources .forge réelles du workspace et leurs handlers détectés sont proposés. " +
            "L’aperçu compile la source mais ne l’exécute pas.",
            dispatch));

        _delete = new CheckBox { Text = "Retirer cet événement de la zone" };
        content.AddChild(_delete);

        _diff = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _diff.AddThemeConstantOverride("separation", 4);
        content.AddChild(Card(
            "AreaUserEventHumanDiff",
            "Diff humain avant application",
            "Seule la collection d’événements de la zone sélectionnée peut changer.",
            _diff));
        _diagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _diagnostics.AddThemeConstantOverride("separation", 4);
        content.AddChild(Card(
            "AreaUserEventDiagnostics",
            "Diagnostics localisés",
            "Les codes et chemins canoniques restent structurés ; cette vue explique l’action à mener.",
            _diagnostics));
        _payloadSummary = StudioStyle.Label("Payload non validé", 10, StudioStyle.Muted, wrap: true);
        _scriptSummary = StudioStyle.Label("Script non validé", 10, StudioStyle.Muted, wrap: true);
        var contract = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        contract.AddChild(_payloadSummary);
        contract.AddChild(_scriptSummary);
        content.AddChild(Card(
            "AreaUserEventRuntimeContract",
            "Contrat runtime observé",
            "Compilation locale uniquement ; l’exécution réelle appartient au runtime GameRoot.",
            contract));

        _summary = StudioStyle.Label(
            "Sélectionnez une zone puis un événement et lancez l’aperçu sec · aucun changement appliqué.",
            11,
            StudioStyle.Muted,
            wrap: true);
        root.AddChild(_summary);
        _status = StudioStyle.Label("Prêt · deux sélections explicites requises", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        var footer = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footer.AddThemeConstantOverride("separation", 7);
        root.AddChild(footer);
        _preview = StudioStyle.Button("Aperçu sec", primary: true);
        _preview.TooltipText = "Valider la commande et compiler sans modifier le projet ni exécuter le script.";
        _preview.Pressed += Preview;
        footer.AddChild(_preview);
        _apply = StudioStyle.Button("Appliquer", primary: true);
        _apply.TooltipText = "Disponible seulement après un aperçu frais et réussi.";
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
            "Commande Fondation de zone · autres événements, carte, couches, module et zones voisines préservés.",
            10,
            StudioStyle.Muted,
            wrap: true));

        _name.TextChanged += _ => InvalidatePreview();
        _schema.TextChanged += _ => InvalidatePreview();
        _payloadVersion.ValueChanged += _ => InvalidatePreview();
        _payload.TextChanged += InvalidatePreview;
        _script.ItemSelected += index => SelectScript((int)index);
        _handler.ItemSelected += _ => InvalidatePreview();
        _required.Toggled += _ => InvalidatePreview();
        _delete.Toggled += _ => InvalidatePreview();
        ResetControls();
        SetEditingEnabled(false);
        Resized += UpdateDialogBounds;
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

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null)
        {
            Close();
        }
        _session = _sourceSession;
        OpenBoundSession(cancelApproval: false);
    }

    private void OpenBoundSession(bool cancelApproval = true)
    {
        if (cancelApproval)
        {
            _session.CancelAreaUserEventPreviewUx2LwC();
        }
        _updating = true;
        _areaChoices = _session.AreaUserEventAreasUx2LwC;
        _scriptChoices = _session.AreaUserEventScriptsUx2LwC();
        _area.Clear();
        _area.AddItem("Choisissez explicitement une zone…");
        foreach (var choice in _areaChoices)
        {
            _area.AddItem(choice.Label);
        }
        _area.Select(0);
        ResetEventChoices();
        _updating = false;
        _draft = null;
        _lastPreview = null;
        _applied = false;
        _canonicalPreviewReady = false;
        ResetControls();
        SetEditingEnabled(false);
        _selection.Text = _areaChoices.Count == 0
            ? "Aucune zone n’est disponible dans ce projet."
            : "Choisissez explicitement une zone puis un événement ; aucune fiche n’est sélectionnée automatiquement.";
        _selection.AddThemeColorOverride("font_color",
            _areaChoices.Count == 0 ? StudioStyle.Danger : StudioStyle.Warning);
        _summary.Text = "Sélectionnez une zone puis un événement et lancez l’aperçu sec · aucun changement appliqué.";
        RenderDiff(null);
        RenderDiagnostics([]);
        UpdateTransactionButtons();
        SetStatus("Prêt · deux sélections explicites requises", error: false);
        Visible = true;
        MoveToFront();
        UpdateDialogBounds();
        _area.GrabFocus();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceTempRoot is null)
        {
            return;
        }
        var sourceUntouched = _evidenceSourceSnapshot is not { } snapshot || SourceMatchesSnapshot(snapshot);
        _session.CancelAreaUserEventPreviewUx2LwC();
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
        {
            return false;
        }
        if (!TryOpenIsolatedEvidenceSession())
        {
            return false;
        }
        var areaIndex = _areaChoices.ToList().FindIndex(item => item.IsEvidenceTarget);
        if (areaIndex < 0)
        {
            Close();
            return false;
        }
        _area.Select(areaIndex + 1);
        SelectArea(areaIndex + 1);
        _event.Select(1);
        SelectEvent(1);
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
        return _lastEvidencePreviewDryRun && !_apply.Disabled && HasExplicitAreaSelection &&
               HasExplicitEventSelection && CanonicalPreviewReady && ShowsTypedPayload &&
               ShowsNoExecutionBoundary && ShowsStructuredDiagnostics && ShowsHumanSurface &&
               UsesRealScriptCatalog && UsesIsolatedEvidence && EvidenceSourceUntouched;
    }

    internal bool RunUiTransactionEvidence()
    {
        var sourceSnapshot = CaptureSourceSnapshot();
        if (sourceSnapshot is null)
        {
            return false;
        }
        StudioSessionController? reopened = null;
        try
        {
            var ready = PrepareEvidence("ux2-lw-c-area-user-events");
            if (!ready || !UsesIsolatedEvidence)
            {
                return false;
            }
            var transactionSession = _session;
            var originalProject = transactionSession.Project;
            var targetId = _draft?.AreaId;
            var originalTarget = targetId is { } sourceAreaId
                ? originalProject?.Areas.SingleOrDefault(area => area.Id == sourceAreaId)
                : null;
            var dryRun = ready && _lastEvidencePreviewDryRun && originalProject is not null &&
                         originalTarget is not null && SourceMatchesSnapshot(sourceSnapshot);
            Apply();
            var appliedProject = transactionSession.Project;
            var appliedTarget = targetId is { } appliedAreaId
                ? appliedProject?.Areas.SingleOrDefault(area => area.Id == appliedAreaId)
                : null;
            var applied = dryRun && _applied && appliedProject is not null && appliedTarget is not null &&
                          !ReferenceEquals(originalProject, appliedProject) &&
                          ReferenceEquals(originalTarget!.Authoring.Map, appliedTarget.Authoring.Map) &&
                          ReferenceEquals(originalTarget.Authoring.Layers, appliedTarget.Authoring.Layers) &&
                          ReferenceEquals(originalTarget.Authoring.Experience, appliedTarget.Authoring.Experience) &&
                          ReferenceEquals(originalTarget.Foundation, appliedTarget.Foundation) &&
                          ReferenceEquals(originalTarget.Navigation, appliedTarget.Navigation) &&
                          ReferenceEquals(originalTarget.RuntimePolicies, appliedTarget.RuntimePolicies);
            var expectedAreaJson = appliedTarget is null ? null : AreaJsonSerializer.Serialize(appliedTarget);
            var expectedProject = applied ? appliedProject : null;
            if (applied)
            {
                Undo();
            }
            var undone = applied && ReferenceEquals(originalProject, transactionSession.Project);
            if (undone)
            {
                Redo();
            }
            var redone = undone && ReferenceEquals(appliedProject, transactionSession.Project);
            var saved = redone && transactionSession.Save().Succeeded;
            var persisted = false;
            if (saved && transactionSession.Session is { } workspace && targetId is { } persistedAreaId &&
                expectedAreaJson is not null)
            {
                reopened = new StudioSessionController();
                if (reopened.Open(workspace.RootDirectory, persistedAreaId.ToString()).Succeeded)
                {
                    var restoredProject = reopened.Project;
                    var restored = restoredProject?.Areas.SingleOrDefault(area => area.Id == persistedAreaId);
                    persisted = expectedProject is not null && restoredProject is not null && restored is not null &&
                                string.Equals(expectedAreaJson, AreaJsonSerializer.Serialize(restored),
                                    StringComparison.Ordinal) &&
                                StudioSessionController.ProjectRoundTripMatchesUx2A(expectedProject,
                                    restoredProject, persistedAreaId);
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
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_LW_C_UI_TRANSACTION_FAILED " +
                        $"type={exception.GetType().Name} message={exception.Message}");
            return false;
        }
        finally
        {
            reopened?.Close();
            if (_evidenceSession is not null || _evidenceTempRoot is not null)
            {
                Close();
            }
            _sourceSession.RestoreAreaUserEventApprovalUx2LwC(sourceSnapshot.Approval);
        }
    }

    private bool TryOpenIsolatedEvidenceSession()
    {
        var snapshot = CaptureSourceSnapshot();
        var evidenceProject = _sourceSession.CreateUx2LwCEvidenceProject();
        var sourceWorkspace = _sourceSession.Session;
        if (snapshot is null || evidenceProject is null || sourceWorkspace is null)
        {
            return false;
        }
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-lw-c-evidence-" + Guid.NewGuid().ToString("N"));
        var evidenceSession = new StudioSessionController();
        try
        {
            var created = ProjectSession.Create(tempRoot, evidenceProject);
            var opened = created.Succeeded &&
                         StudioSessionController.CopyUx2LwCEvidenceScripts(sourceWorkspace.RootDirectory, tempRoot) &&
                         evidenceSession.Open(tempRoot, snapshot.ActiveAreaId?.ToString()).Succeeded;
            if (!opened)
            {
                evidenceSession.Close();
                _lastEvidenceResourcesReleased =
                    StudioSessionController.DeleteUx2LwCTemporaryWorkspace(tempRoot);
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
            _lastEvidencePreviewDryRun = false;
            OpenBoundSession();
            var ready = _areaChoices.Any(item => item.IsEvidenceTarget) &&
                        _scriptChoices.Any(item => item.Handlers.Count > 0) &&
                        SourceMatchesSnapshot(snapshot);
            if (!ready)
            {
                Close();
            }
            return ready;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_LW_C_EVIDENCE_FAILED " +
                        $"type={exception.GetType().Name} message={exception.Message}");
            evidenceSession.Close();
            var released = StudioSessionController.DeleteUx2LwCTemporaryWorkspace(tempRoot);
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
        var released = StudioSessionController.DeleteUx2LwCTemporaryWorkspace(tempRoot);
        _evidenceTempRoot = released ? null : tempRoot;
        _lastSourceRestored = true;
        return released;
    }

    private StudioUx2LwCEvidenceSourceSnapshot? CaptureSourceSnapshot()
    {
        var project = _sourceSession.Project;
        var history = _sourceSession.History;
        if (project is null || history is null)
        {
            return null;
        }
        return new StudioUx2LwCEvidenceSourceSnapshot(
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
            _sourceSession.CaptureAreaUserEventApprovalUx2LwC());
    }

    private bool SourceMatchesSnapshot(StudioUx2LwCEvidenceSourceSnapshot snapshot)
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
               _sourceSession.CaptureAreaUserEventApprovalUx2LwC() == snapshot.Approval;
    }

    private void SelectArea(int selected)
    {
        if (_updating)
        {
            return;
        }
        _session.CancelAreaUserEventPreviewUx2LwC();
        _draft = null;
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _applied = false;
        ResetControls();
        SetEditingEnabled(false);
        RenderDiff(null);
        RenderDiagnostics([]);
        if (selected <= 0 || selected > _areaChoices.Count)
        {
            ResetEventChoices();
            _selection.Text =
                "Choisissez explicitement une zone puis un événement ; aucune fiche n’est sélectionnée automatiquement.";
            _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
            UpdateTransactionButtons();
            return;
        }
        PopulateEventChoices(_areaChoices[selected - 1].AreaId);
        _selection.Text = "Zone sélectionnée · choisissez maintenant un événement existant ou une nouvelle fiche.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
        SetStatus("Zone sélectionnée · événement explicite requis", error: false);
        UpdateTransactionButtons();
    }

    private void PopulateEventChoices(AreaId areaId)
    {
        _eventChoices = _session.AreaUserEventChoicesUx2LwC(areaId);
        _updating = true;
        _event.Clear();
        _eventIds.Clear();
        _event.AddItem("Choisissez explicitement un événement…");
        _eventIds.Add(null);
        _event.AddItem("Nouvel événement de zone");
        _eventIds.Add(null);
        foreach (var choice in _eventChoices)
        {
            _event.AddItem(choice.Label);
            _eventIds.Add(choice.EventId);
        }
        _event.Select(0);
        _event.Disabled = false;
        _updating = false;
    }

    private void ResetEventChoices()
    {
        _eventChoices = [];
        _event.Clear();
        _eventIds.Clear();
        _event.AddItem("Choisissez d’abord une zone…");
        _eventIds.Add(null);
        _event.Select(0);
        _event.Disabled = true;
    }

    private void SelectEvent(int selected)
    {
        if (_updating || _area.Selected <= 0 || _area.Selected > _areaChoices.Count)
        {
            return;
        }
        _session.CancelAreaUserEventPreviewUx2LwC();
        var areaChoice = _areaChoices[_area.Selected - 1];
        _draft = selected switch
        {
            1 => _session.CreateNewAreaUserEventDraftUx2LwC(areaChoice.AreaId),
            > 1 when selected < _eventIds.Count =>
                _session.CreateAreaUserEventDraftUx2LwC(areaChoice.AreaId, _eventIds[selected]),
            _ => null,
        };
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _applied = false;
        if (_draft is null)
        {
            ResetControls();
            SetEditingEnabled(false);
            _selection.Text = "Choisissez explicitement un événement ; aucune fiche n’est chargée.";
            _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
            UpdateTransactionButtons();
            return;
        }
        _lastAreaId = _draft.AreaId;
        _lastEventId = _draft.EventId;
        LoadDraft(_draft);
        SetEditingEnabled(true);
        _selection.Text = $"Zone et événement sélectionnés · {_draft.AreaDisplayName} · aucune sélection implicite";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Text);
        _summary.Text = "Modifiez le payload et le dispatch puis lancez l’aperçu sec. " +
                        "La source sera compilée localement sans être exécutée.";
        RenderDiff(null);
        RenderDiagnostics([]);
        SetStatus("Fiche chargée · aperçu sec requis", error: false);
        UpdateTransactionButtons();
    }

    private void SelectScript(int selected)
    {
        if (_updating)
        {
            return;
        }
        PopulateHandlers(selected, selected >= 0 && selected < _scriptPaths.Count
            ? _scriptPaths[selected]
            : null, null);
        _scriptProvenance.Text = selected > 0 && selected <= _scriptChoices.Count
            ? _scriptChoices[selected - 1].Provenance
            : "Choisissez une source locale réelle.";
        InvalidatePreview();
    }

    private void LoadDraft(StudioUx2LwCDraft draft)
    {
        _updating = true;
        _name.Text = draft.NamespacedName;
        _schema.Text = draft.PayloadSchema;
        _payloadVersion.Value = draft.PayloadVersion;
        _apiVersion.Text = draft.ForgeScriptApiVersion;
        _required.ButtonPressed = draft.Required;
        _delete.ButtonPressed = draft.Delete;
        _delete.Disabled = draft.IsNew;
        _payload.Text = string.Join('\n', draft.PayloadFields.Select(field =>
            $"{field.Name}:{field.Type}={field.Value}"));
        PopulateScripts(draft.ScriptPath, draft.Subscriptions.FirstOrDefault());
        _updating = false;
    }

    private void PopulateScripts(string? selectedPath, string? selectedHandler)
    {
        _script.Clear();
        _scriptPaths.Clear();
        _script.AddItem("Choisissez une source locale…");
        _scriptPaths.Add(null);
        foreach (var choice in _scriptChoices)
        {
            _script.AddItem(choice.DisplayName);
            _scriptPaths.Add(choice.RelativePath);
        }
        var selected = _scriptPaths.FindIndex(path => string.Equals(path, selectedPath, StringComparison.Ordinal));
        if (selected < 0 && !string.IsNullOrWhiteSpace(selectedPath))
        {
            _script.AddItem($"Source indisponible · {StudioSessionController.HumanAreaUserEventScriptNameUx2LwC(selectedPath)}");
            _scriptPaths.Add(selectedPath);
            selected = _scriptPaths.Count - 1;
        }
        _script.Select(Math.Max(0, selected));
        _scriptProvenance.Text = selected > 0 && selected <= _scriptChoices.Count
            ? _scriptChoices[selected - 1].Provenance
            : selected > _scriptChoices.Count
                ? "Source enregistrée indisponible dans ce workspace."
                : "Choisissez une source locale réelle.";
        PopulateHandlers(_script.Selected, selectedPath, selectedHandler);
    }

    private void PopulateHandlers(int scriptIndex, string? scriptPath, string? selectedHandler)
    {
        _handler.Clear();
        _handlerValues.Clear();
        _handler.AddItem("Choisissez un point d’entrée…");
        _handlerValues.Add(null);
        _scriptProvenance.Text = "Choisissez une source locale réelle.";
        var resolvedPath = scriptIndex >= 0 && scriptIndex < _scriptPaths.Count
            ? _scriptPaths[scriptIndex]
            : scriptPath;
        var script = _scriptChoices.SingleOrDefault(item => string.Equals(
            item.RelativePath, resolvedPath, StringComparison.Ordinal));
        if (script is not null)
        {
            foreach (var handler in script.Handlers)
            {
                _handler.AddItem(StudioSessionController.HumanAreaUserEventTokenUx2LwC(handler));
                _handlerValues.Add(handler);
            }
        }
        var selected = _handlerValues.FindIndex(handler => string.Equals(
            handler, selectedHandler, StringComparison.Ordinal));
        _handler.Select(Math.Max(0, selected));
        _handler.Disabled = _draft is null || script is null;
    }

    private StudioUx2LwCDraft? ReadDraft()
    {
        if (_draft is null)
        {
            return null;
        }
        var scriptPath = _script.Selected >= 0 && _script.Selected < _scriptPaths.Count
            ? _scriptPaths[_script.Selected] ?? string.Empty
            : string.Empty;
        var handler = _handler.Selected >= 0 && _handler.Selected < _handlerValues.Count
            ? _handlerValues[_handler.Selected]
            : null;
        return _draft with
        {
            Delete = _delete.ButtonPressed,
            NamespacedName = _name.Text.Trim(),
            PayloadSchema = _schema.Text.Trim(),
            PayloadVersion = (int)_payloadVersion.Value,
            ScriptPath = scriptPath,
            Subscriptions = handler is null ? [] : [handler],
            Required = _required.ButtonPressed,
            PayloadFields = ParsePayloadFields(_payload.Text),
        };
    }

    private static IReadOnlyList<StudioUx2LwCPayloadFieldDraft> ParsePayloadFields(string text)
    {
        var fields = new List<StudioUx2LwCPayloadFieldDraft>();
        foreach (var raw in text.Replace("\r", string.Empty, StringComparison.Ordinal)
                     .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var equals = raw.IndexOf('=');
            var colon = raw.IndexOf(':');
            if (colon <= 0 || equals <= colon + 1)
            {
                fields.Add(new(string.Empty, string.Empty, raw));
                continue;
            }
            fields.Add(new(raw[..colon].Trim(), raw[(colon + 1)..equals].Trim(), raw[(equals + 1)..].Trim()));
        }
        return fields;
    }

    private bool ConfigureEvidenceDraft()
    {
        var draft = _draft;
        var script = _scriptChoices.FirstOrDefault(item => item.Handlers.Contains("main", StringComparer.Ordinal)) ??
                     _scriptChoices.FirstOrDefault(item => item.Handlers.Count > 0);
        if (draft is null || script is null)
        {
            return false;
        }
        _draft = draft with
        {
            NamespacedName = draft.NamespacedName,
            PayloadSchema = "schema://area/signal/v1",
            PayloadVersion = 1,
            ForgeScriptApiVersion = AreaUserEvent.CurrentForgeScriptApiVersion,
            ScriptPath = script.RelativePath,
            Subscriptions = [script.Handlers[0]],
            Required = true,
            PayloadFields = [new("message", "string", "Signal de zone")],
        };
        LoadDraft(_draft);
        return true;
    }

    private void Preview()
    {
        var draft = ReadDraft();
        if (draft is null)
        {
            SetStatus("Choisissez explicitement une zone et un événement avant l’aperçu.", error: true);
            return;
        }
        _draft = draft;
        _lastPreview = _session.PreviewAreaUserEventUx2LwC(draft);
        _canonicalPreviewReady = _lastPreview.Succeeded;
        RenderDiff(_lastPreview.Diff);
        RenderDiagnostics(_lastPreview.Diagnostics);
        _payloadSummary.Text = _lastPreview.PayloadSummary;
        _scriptSummary.Text = _lastPreview.ScriptSummary;
        _summary.Text = _lastPreview.Message;
        _summary.AddThemeColorOverride("font_color",
            _lastPreview.Succeeded ? StudioStyle.Text : StudioStyle.Danger);
        SetStatus(_lastPreview.Message, error: !_lastPreview.Succeeded);
        UpdateTransactionButtons();
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedAreaUserEventUx2LwC();
        _canonicalPreviewReady = false;
        _lastPreview = null;
        if (result.Succeeded)
        {
            AreaUserEventsChanged?.Invoke();
            ReloadSelectedEvent();
        }
        _applied = result.Succeeded;
        SetStatus(result.Message, error: !result.Succeeded);
        _summary.Text = result.Message;
        RenderDiff(null);
        RenderDiagnostics([]);
        UpdateTransactionButtons();
    }

    private void Undo()
    {
        var result = _session.Undo();
        _session.CancelAreaUserEventPreviewUx2LwC();
        _canonicalPreviewReady = false;
        _lastPreview = null;
        _applied = false;
        if (result.Succeeded)
        {
            ReloadSelectedEvent();
            AreaUserEventsChanged?.Invoke();
        }
        SetStatus(result.Message, error: !result.Succeeded);
        UpdateTransactionButtons();
    }

    private void Redo()
    {
        var result = _session.Redo();
        _session.CancelAreaUserEventPreviewUx2LwC();
        _canonicalPreviewReady = false;
        _lastPreview = null;
        _applied = result.Succeeded;
        if (result.Succeeded)
        {
            ReloadSelectedEvent();
            AreaUserEventsChanged?.Invoke();
        }
        _applied = result.Succeeded;
        SetStatus(result.Message, error: !result.Succeeded);
        UpdateTransactionButtons();
    }

    private void Save()
    {
        var result = _session.Save();
        SetStatus(result.Message, error: !result.Succeeded);
        UpdateTransactionButtons();
    }

    private void ReloadSelectedEvent()
    {
        var areaId = _lastAreaId;
        var eventId = _lastEventId;
        _areaChoices = _session.AreaUserEventAreasUx2LwC;
        _scriptChoices = _session.AreaUserEventScriptsUx2LwC();
        var areaIndex = areaId is null ? -1 : _areaChoices.ToList().FindIndex(item => item.AreaId == areaId);
        if (areaIndex < 0)
        {
            _updating = true;
            _area.Select(0);
            _updating = false;
            SelectArea(0);
            return;
        }
        _updating = true;
        _area.Select(areaIndex + 1);
        _updating = false;
        PopulateEventChoices(_areaChoices[areaIndex].AreaId);
        var eventIndex = eventId is null
            ? -1
            : _eventChoices.ToList().FindIndex(item => item.EventId == eventId);
        if (eventIndex < 0)
        {
            _updating = true;
            _event.Select(0);
            _updating = false;
            _draft = null;
            ResetControls();
            SetEditingEnabled(false);
            return;
        }
        _event.Select(eventIndex + 2);
        SelectEvent(eventIndex + 2);
    }

    private void InvalidatePreview()
    {
        if (_updating || _draft is null)
        {
            return;
        }
        _session.CancelAreaUserEventPreviewUx2LwC();
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _applied = false;
        _summary.Text = "Brouillon modifié · relancez l’aperçu sec avant application.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Warning);
        _payloadSummary.Text = "Payload à revalider";
        _scriptSummary.Text = "Compilation à revalider · aucun script exécuté";
        RenderDiff(null);
        RenderDiagnostics([]);
        SetStatus("Aperçu précédent effacé · aucun changement appliqué", error: false);
        UpdateTransactionButtons();
    }

    private void ResetControls()
    {
        _updating = true;
        _name.Text = string.Empty;
        _schema.Text = string.Empty;
        _payloadVersion.Value = 1;
        _apiVersion.Text = "Non chargé";
        _payload.Text = string.Empty;
        _script.Clear();
        _scriptPaths.Clear();
        _script.AddItem("Choisissez une source locale…");
        _scriptPaths.Add(null);
        _handler.Clear();
        _handlerValues.Clear();
        _handler.AddItem("Choisissez un point d’entrée…");
        _handlerValues.Add(null);
        _required.ButtonPressed = false;
        _delete.ButtonPressed = false;
        _delete.Disabled = true;
        _payloadSummary.Text = "Payload non validé";
        _scriptSummary.Text = "Script non validé";
        _updating = false;
    }

    private void SetEditingEnabled(bool enabled)
    {
        _event.Disabled = _area.Selected <= 0;
        _name.Editable = enabled;
        _schema.Editable = enabled;
        _payloadVersion.Editable = enabled;
        _payload.Editable = enabled;
        _script.Disabled = !enabled;
        var selectedScriptPath = _script.Selected >= 0 && _script.Selected < _scriptPaths.Count
            ? _scriptPaths[_script.Selected]
            : null;
        _handler.Disabled = !enabled || _scriptChoices.All(item => !string.Equals(
            item.RelativePath, selectedScriptPath, StringComparison.Ordinal));
        _required.Disabled = !enabled;
        if (!enabled)
        {
            _delete.Disabled = true;
        }
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

    private void RenderDiff(StudioUx2LwCSemanticDiff? diff)
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
        {
            _diff.AddChild(StudioStyle.Label("• " + change, 10, StudioStyle.Text, wrap: true));
        }
    }

    private void RenderDiagnostics(IReadOnlyList<StudioUx2LwCDiagnostic> diagnostics)
    {
        ClearChildren(_diagnostics);
        if (diagnostics.Count == 0)
        {
            _diagnostics.AddChild(StudioStyle.Label("Aucun blocage localisé.", 10, StudioStyle.Muted));
            return;
        }
        foreach (var diagnostic in diagnostics)
        {
            var location = diagnostic.Path.Contains("payload", StringComparison.OrdinalIgnoreCase)
                ? "Payload typé"
                : diagnostic.Path.Contains("script", StringComparison.OrdinalIgnoreCase) ||
                  diagnostic.Path.Contains("subscription", StringComparison.OrdinalIgnoreCase)
                    ? "Source et point d’entrée"
                    : "Fiche événement";
            _diagnostics.AddChild(StudioStyle.Label(
                $"{location} · {diagnostic.Message}",
                10,
                diagnostic.Blocking ? StudioStyle.Danger : StudioStyle.Warning,
                wrap: true));
        }
    }

    private static LineEdit NewLine(string placeholder)
    {
        var line = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyLineEdit(line);
        return line;
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
            {
                yield return label;
            }
            foreach (var descendant in DescendantLabels(child))
            {
                yield return descendant;
            }
        }
    }

    private void UpdateDialogBounds()
    {
        var viewport = GetViewportRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0)
        {
            return;
        }
        var scale = viewport.X >= 3_200 ? 1.72f : viewport.X >= 2_300 ? 1.24f : 1f;
        var width = Math.Clamp((viewport.X - 52f) / scale, 940f, 1_460f);
        var height = Math.Clamp((viewport.Y - 42f) / scale, 720f, 1_010f);
        var renderedWidth = width * scale;
        var renderedHeight = height * scale;
        _dialog.Scale = Vector2.One * scale;
        _dialog.CustomMaximumSize = new Vector2(width, height);
        _dialog.Size = new Vector2(width, height);
        _dialog.Position = new Vector2((viewport.X - renderedWidth) / 2f,
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
