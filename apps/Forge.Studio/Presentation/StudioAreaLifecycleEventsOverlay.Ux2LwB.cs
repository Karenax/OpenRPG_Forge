using Forge.Domain.Areas;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioAreaLifecycleBindingCardUx2LwB : VBoxContainer
{
    private readonly string _humanEventName;
    private readonly CheckBox _enabled;
    private readonly OptionButton _script;
    private readonly CheckBox _required;
    private readonly Label _contract;
    private readonly List<string?> _scriptValues = [];
    private StudioUx2LwBBindingDraft? _source;
    private bool _updating;

    internal event Action? Changed;

    internal StudioUx2LwBBindingDraft? Value => _source is null
        ? null
        : _source with
        {
            Enabled = _enabled.ButtonPressed,
            ScriptPath = _script.Selected >= 0 && _script.Selected < _scriptValues.Count
                ? _scriptValues[_script.Selected]
                : null,
            Required = _required.ButtonPressed,
        };

    internal StudioAreaLifecycleBindingCardUx2LwB(string humanEventName, string eventName)
    {
        _humanEventName = humanEventName;
        AddThemeConstantOverride("separation", 7);
        AddChild(StudioStyle.Label(humanEventName, 15));
        AddChild(StudioStyle.Label(
            eventName == "area.entered"
                ? "Émis après l’arrivée effective dans cette zone."
                : "Émis avant le départ effectif de cette zone.",
            10,
            StudioStyle.Muted,
            wrap: true));

        var controls = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        controls.AddThemeConstantOverride("separation", 8);
        AddChild(controls);
        _enabled = new CheckBox { Text = "Configurer ce lien" };
        controls.AddChild(_enabled);
        _required = new CheckBox { Text = "Script obligatoire au runtime" };
        controls.AddChild(_required);
        _contract = StudioStyle.Label("Contrat ForgeScript : non chargé", 10, StudioStyle.Muted);
        controls.AddChild(_contract);

        _script = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "Uniquement les scripts .forge réels du projet et leur point d’entrée main.",
        };
        StudioStyle.ApplyOptionButton(_script);
        AddChild(_script);
        AddChild(StudioStyle.Label(
            "Provenance : fichier .forge du workspace ouvert · point d’entrée main détecté localement.",
            9,
            StudioStyle.Muted,
            wrap: true));

        _enabled.Toggled += _ => RaiseChanged();
        _required.Toggled += _ => RaiseChanged();
        _script.ItemSelected += _ => RaiseChanged();
    }

    internal void Load(
        StudioUx2LwBBindingDraft value,
        IReadOnlyList<StudioEventScriptChoice> scripts)
    {
        _source = value;
        _updating = true;
        _enabled.ButtonPressed = value.Enabled;
        _required.ButtonPressed = value.Required;
        _contract.Text = $"Contrat ForgeScript : {value.ForgeScriptApiVersion}";
        _script.Clear();
        _scriptValues.Clear();
        _script.AddItem("Aucun script · observation typée uniquement");
        _scriptValues.Add(null);
        foreach (var script in scripts)
        {
            _script.AddItem(StudioSessionController.ContainsTechnicalIdentifierUx2LwB(script.DisplayName)
                ? StudioSessionController.HumanAreaLifecycleScriptNameUx2LwB(script.RelativePath)
                : script.DisplayName);
            _scriptValues.Add(script.RelativePath);
        }
        var selected = _scriptValues.FindIndex(path => string.Equals(
            path,
            value.ScriptPath,
            StringComparison.Ordinal));
        if (selected < 0 && !string.IsNullOrWhiteSpace(value.ScriptPath))
        {
            _script.AddItem($"Source indisponible · {StudioSessionController.HumanAreaLifecycleScriptNameUx2LwB(value.ScriptPath)}");
            _scriptValues.Add(value.ScriptPath);
            selected = _scriptValues.Count - 1;
        }
        _script.Select(Math.Max(0, selected));
        _updating = false;
        UpdateEnabledState();
    }

    internal void Reset()
    {
        _source = null;
        _updating = true;
        _enabled.ButtonPressed = false;
        _required.ButtonPressed = false;
        _script.Clear();
        _scriptValues.Clear();
        _script.AddItem("Aucun script · observation typée uniquement");
        _scriptValues.Add(null);
        _script.Select(0);
        _contract.Text = "Contrat ForgeScript : non chargé";
        _updating = false;
        SetEditingEnabled(false);
    }

    internal void SetEditingEnabled(bool enabled)
    {
        _enabled.Disabled = !enabled;
        _script.Disabled = !enabled || !_enabled.ButtonPressed;
        _required.Disabled = !enabled || !_enabled.ButtonPressed;
    }

    private void RaiseChanged()
    {
        if (_updating)
        {
            return;
        }
        UpdateEnabledState();
        Changed?.Invoke();
    }

    private void UpdateEnabledState()
    {
        var cardEditable = !_enabled.Disabled;
        _script.Disabled = !cardEditable || !_enabled.ButtonPressed;
        _required.Disabled = !cardEditable || !_enabled.ButtonPressed;
        TooltipText = _enabled.ButtonPressed
            ? $"{_humanEventName} configurée ; aperçu sec requis avant application."
            : $"{_humanEventName} non reliée ; le runtime conserve son événement typé.";
    }
}

internal sealed partial class StudioAreaLifecycleEventsOverlayUx2LwB : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceTempRoot;
    private StudioUx2LwBEvidenceSourceSnapshot? _evidenceSourceSnapshot;
    private readonly PanelContainer _dialog;
    private readonly OptionButton _area;
    private readonly Label _selection;
    private readonly StudioAreaLifecycleBindingCardUx2LwB _entered;
    private readonly StudioAreaLifecycleBindingCardUx2LwB _exited;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _observations;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;
    private IReadOnlyList<StudioUx2LwBAreaChoice> _choices = [];
    private StudioUx2LwBDraft? _draft;
    private StudioUx2LwBPreview? _lastPreview;
    private bool _applied;
    private bool _canonicalPreviewReady;
    private bool _lastEvidenceSourceUntouched;
    private bool _lastEvidenceResourcesReleased;
    private bool _lastSourceRestored = true;
    private bool _lastEvidencePreviewDryRun;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? AreaLifecycleChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _choices.Count;
    internal bool HasExplicitSelection => _draft is not null && _area.Selected > 0;
    internal bool CanonicalPreviewReady => _canonicalPreviewReady && _lastPreview?.Succeeded == true;
    internal bool ShowsBothEvents => _lastPreview?.Observations.Select(item => item.EventName)
        .OrderBy(item => item, StringComparer.Ordinal)
        .SequenceEqual(new[] { "area.entered", "area.exited" }) == true;
    internal bool ShowsNoExecutionBoundary => _lastPreview?.Observations.Count == 2 &&
                                              _lastPreview.Observations.All(item => item.Summary.Contains(
                                                  "aucun script n’a été exécuté",
                                                  StringComparison.OrdinalIgnoreCase));
    internal bool ShowsStructuredDiagnostics => _lastPreview is not null &&
                                                 _lastPreview.Diagnostics.All(item =>
                                                     !string.IsNullOrWhiteSpace(item.Code) &&
                                                     !string.IsNullOrWhiteSpace(item.Path));
    internal bool UsesRealScriptCatalog
    {
        get
        {
            var scripts = _session.FindForgeScripts();
            return scripts.Count > 0 && scripts.All(script =>
                _session.Session is { } workspace && File.Exists(Path.Combine(
                    workspace.RootDirectory,
                    script.RelativePath.Replace('/', Path.DirectorySeparatorChar))));
        }
    }
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                          ReferenceEquals(_session, _evidenceSession);
    internal bool EvidenceSourceUntouched => _evidenceSourceSnapshot is { } snapshot
        ? SourceMatchesSnapshot(snapshot)
        : _lastEvidenceSourceUntouched;
    internal bool EvidenceResourcesReleased => _evidenceSession is null &&
                                               _evidenceTempRoot is null &&
                                               _lastEvidenceResourcesReleased;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession) && _lastSourceRestored;

    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantLabels(_dialog).Select(label => label.Text));
            return text.Contains("Entrée", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Sortie", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun script", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !StudioSessionController.ContainsTechnicalIdentifierUx2LwB(text);
        }
    }

    internal StudioAreaLifecycleEventsOverlayUx2LwB(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = _sourceSession;
        Name = "StudioAreaLifecycleEventsOverlayUx2LwB";
        Visible = false;
        ZIndex = 465;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect
        {
            Color = new Color(0, 0, 0, .82f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        _dialog = StudioStyle.Panel("AreaLifecycleEventsDialog", StudioStyle.Chrome, 12, 2);
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
        heading.AddChild(StudioStyle.Label("MONDE VIVANT · CYCLE DE ZONE", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Entrée et sortie", 20));
        heading.AddChild(StudioStyle.Label(
            "AREA-022 · aperçu sec canonique · émission typée observée · aucun script exécuté, " +
            "aucun dispatch réseau et aucune simulation moteur dans cet outil",
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
            CustomMinimumSize = new Vector2(0, 470),
            CustomMaximumSize = new Vector2(-1, 760),
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

        _entered = new StudioAreaLifecycleBindingCardUx2LwB("Entrée dans la zone", "area.entered");
        content.AddChild(Card(
            "AreaEnteredAuthoring",
            "Entrée dans la zone",
            "Le runtime émet l’événement typé après l’arrivée. Un script réel peut être relié ; cet éditeur ne l’exécute pas.",
            _entered));
        _exited = new StudioAreaLifecycleBindingCardUx2LwB("Sortie de la zone", "area.exited");
        content.AddChild(Card(
            "AreaExitedAuthoring",
            "Sortie de la zone",
            "Le runtime émet l’événement typé avant le départ. Une autre zone est nécessaire pour observer une transition.",
            _exited));

        _diff = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _diff.AddThemeConstantOverride("separation", 4);
        content.AddChild(Card(
            "AreaLifecycleHumanDiff",
            "Diff humain avant application",
            "Seuls les liens d’entrée et de sortie de la zone sélectionnée peuvent changer.",
            _diff));
        _observations = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _observations.AddThemeConstantOverride("separation", 4);
        content.AddChild(Card(
            "AreaLifecycleRuntimeObservation",
            "Observation du contrat runtime",
            "L’aperçu inspecte les événements typés et les bindings ; il ne lance jamais ForgeScript.",
            _observations));
        _diagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _diagnostics.AddThemeConstantOverride("separation", 4);
        content.AddChild(Card(
            "AreaLifecycleDiagnostics",
            "Diagnostics localisés",
            "Codes et chemins canoniques restent structurés en interne ; la vue fournit une action humaine.",
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
        _preview.TooltipText = "Valider et observer les contrats sans modifier le projet ni exécuter de script.";
        _preview.Pressed += Preview;
        footer.AddChild(_preview);
        _apply = StudioStyle.Button("Appliquer", primary: true);
        _apply.TooltipText = "Disponible seulement après un aperçu sec réussi et encore frais.";
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
            "Commande unique Fondation de zone · voisins, module, géométrie, navigation et politiques préservés.",
            10,
            StudioStyle.Muted,
            wrap: true));

        _entered.Changed += InvalidatePreview;
        _exited.Changed += InvalidatePreview;
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
            _session.CancelAreaLifecyclePreviewUx2LwB();
        }
        _choices = _session.AreaLifecycleChoicesUx2LwB;
        _area.Clear();
        _area.AddItem("Choisissez explicitement une zone…");
        foreach (var choice in _choices)
        {
            _area.AddItem(choice.Label);
        }
        _area.Select(0);
        _draft = null;
        _lastPreview = null;
        _applied = false;
        _canonicalPreviewReady = false;
        ResetControls();
        _selection.Text = _choices.Count == 0
            ? "Aucune zone n’est disponible dans ce projet."
            : "Choisissez explicitement une zone ; aucune fiche n’est sélectionnée automatiquement.";
        _selection.AddThemeColorOverride(
            "font_color",
            _choices.Count == 0 ? StudioStyle.Danger : StudioStyle.Warning);
        _summary.Text = "Sélectionnez une zone puis lancez l’aperçu sec · aucun changement appliqué.";
        RenderDiff(null);
        RenderObservations([]);
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
        {
            return;
        }
        var sourceUntouched = _evidenceSourceSnapshot is not { } snapshot || SourceMatchesSnapshot(snapshot);
        _session.CancelAreaLifecyclePreviewUx2LwB();
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
        var choiceIndex = _choices.ToList().FindIndex(item => item.IsEvidenceTarget);
        if (choiceIndex < 0)
        {
            Close();
            return false;
        }
        _area.Select(choiceIndex + 1);
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
               CanonicalPreviewReady && ShowsBothEvents && ShowsNoExecutionBoundary &&
               ShowsStructuredDiagnostics && ShowsHumanSurface && UsesRealScriptCatalog &&
               UsesIsolatedEvidence && EvidenceSourceUntouched;
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
            var ready = PrepareEvidence("ux2-lw-b-area-lifecycle");
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
                         originalTarget is not null && UsesIsolatedEvidence &&
                         SourceMatchesSnapshot(sourceSnapshot);
            Apply();
            var appliedProject = transactionSession.Project;
            var appliedTarget = targetId is { } appliedAreaId
                ? appliedProject?.Areas.SingleOrDefault(area => area.Id == appliedAreaId)
                : null;
            var applied = dryRun && _applied && appliedProject is not null && appliedTarget is not null &&
                          !ReferenceEquals(originalProject, appliedProject) &&
                          ReferenceEquals(originalTarget!.Foundation.Creation,
                              appliedTarget.Foundation.Creation) &&
                          ReferenceEquals(originalTarget.Foundation.TileKit,
                              appliedTarget.Foundation.TileKit) &&
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
                                string.Equals(
                                    expectedAreaJson,
                                    AreaJsonSerializer.Serialize(restored),
                                    StringComparison.Ordinal) &&
                                StudioSessionController.ProjectRoundTripMatchesUx2A(
                                    expectedProject,
                                    restoredProject,
                                    persistedAreaId);
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
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_LW_B_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}");
            return false;
        }
        finally
        {
            reopened?.Close();
            if (_evidenceSession is not null || _evidenceTempRoot is not null)
            {
                Close();
            }
            _sourceSession.RestoreAreaLifecycleApprovalUx2LwB(sourceSnapshot.Approval);
        }
    }

    private bool TryOpenIsolatedEvidenceSession()
    {
        var snapshot = CaptureSourceSnapshot();
        var evidenceProject = _sourceSession.CreateUx2LwBEvidenceProject();
        var sourceWorkspace = _sourceSession.Session;
        if (snapshot is null || evidenceProject is null || sourceWorkspace is null)
        {
            return false;
        }
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "openrpg-forge-studio-ux2-lw-b-evidence-" + Guid.NewGuid().ToString("N"));
        var evidenceSession = new StudioSessionController();
        try
        {
            var created = ProjectSession.Create(tempRoot, evidenceProject);
            var opened = created.Succeeded &&
                         StudioSessionController.CopyUx2LwBEvidenceScripts(
                             sourceWorkspace.RootDirectory,
                             tempRoot) &&
                         evidenceSession.Open(tempRoot, snapshot.ActiveAreaId?.ToString()).Succeeded;
            if (!opened)
            {
                evidenceSession.Close();
                _lastEvidenceResourcesReleased = StudioSessionController.DeleteUx2LwBTemporaryWorkspace(tempRoot);
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
            var ready = _choices.Any(item => item.IsEvidenceTarget) &&
                        _session.FindForgeScripts().Count > 0 &&
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
                $"OPENRPG_FORGE_STUDIO_UX2_LW_B_EVIDENCE_FAILED type={exception.GetType().Name} message={exception.Message}");
            evidenceSession.Close();
            var released = StudioSessionController.DeleteUx2LwBTemporaryWorkspace(tempRoot);
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
        var released = StudioSessionController.DeleteUx2LwBTemporaryWorkspace(tempRoot);
        _evidenceTempRoot = released ? null : tempRoot;
        _lastSourceRestored = true;
        return released;
    }

    private StudioUx2LwBEvidenceSourceSnapshot? CaptureSourceSnapshot()
    {
        var project = _sourceSession.Project;
        var history = _sourceSession.History;
        if (project is null || history is null)
        {
            return null;
        }
        return new StudioUx2LwBEvidenceSourceSnapshot(
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
            _sourceSession.CaptureAreaLifecycleApprovalUx2LwB());
    }

    private bool SourceMatchesSnapshot(StudioUx2LwBEvidenceSourceSnapshot snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) &&
               history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.IsDirty &&
               history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureAreaLifecycleApprovalUx2LwB() == snapshot.Approval;
    }

    private void SelectArea(int selected)
    {
        if (selected <= 0)
        {
            _draft = null;
            _lastPreview = null;
            _canonicalPreviewReady = false;
            _session.CancelAreaLifecyclePreviewUx2LwB();
            ResetControls();
            SetEditingEnabled(false);
            _selection.Text = "Choisissez explicitement une zone ; aucune fiche n’est sélectionnée automatiquement.";
            _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
            RenderDiff(null);
            RenderObservations([]);
            RenderDiagnostics([]);
            UpdateTransactionButtons();
            return;
        }
        LoadChoice(selected - 1);
    }

    private void LoadChoice(int choiceIndex)
    {
        if (choiceIndex < 0 || choiceIndex >= _choices.Count)
        {
            return;
        }
        _session.CancelAreaLifecyclePreviewUx2LwB();
        _draft = _session.CreateAreaLifecycleDraftUx2LwB(_choices[choiceIndex].AreaId);
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

        var scripts = _session.FindForgeScripts();
        _entered.Load(_draft.Entered, scripts);
        _exited.Load(_draft.Exited, scripts);
        SetEditingEnabled(true);
        _selection.Text = $"Zone sélectionnée : {_draft.AreaDisplayName} · aucune sélection implicite";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Text);
        _summary.Text =
            "Modifiez les liens puis lancez l’aperçu sec. L’émission typée peut être observée ; aucun script ne sera exécuté.";
        RenderDiff(null);
        RenderObservations([]);
        RenderDiagnostics([]);
        UpdateTransactionButtons();
        SetStatus("Fiche chargée · aperçu sec requis", error: false);
    }

    private StudioUx2LwBDraft? ReadDraft()
    {
        var entered = _entered.Value;
        var exited = _exited.Value;
        return _draft is null || entered is null || exited is null
            ? null
            : _draft with { Entered = entered, Exited = exited };
    }

    private bool ConfigureEvidenceDraft()
    {
        var draft = _draft;
        var scripts = _session.FindForgeScripts();
        if (draft is null || scripts.Count == 0)
        {
            return false;
        }
        var enteredScript = scripts[0].RelativePath;
        var exitedScript = scripts.Count > 1 ? scripts[1].RelativePath : enteredScript;
        _draft = draft with
        {
            Entered = draft.Entered with
            {
                Enabled = true,
                ScriptPath = enteredScript,
                Required = draft.Entered.Enabled && string.Equals(
                    draft.Entered.ScriptPath,
                    enteredScript,
                    StringComparison.Ordinal)
                        ? !draft.Entered.Required
                        : true,
            },
            Exited = draft.Exited with
            {
                Enabled = true,
                ScriptPath = exitedScript,
                Required = false,
            },
        };
        _entered.Load(_draft.Entered, scripts);
        _exited.Load(_draft.Exited, scripts);
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
        _lastPreview = _session.PreviewAreaLifecycleUx2LwB(draft);
        _canonicalPreviewReady = _lastPreview.Succeeded;
        RenderDiff(_lastPreview.Diff);
        RenderObservations(_lastPreview.Observations);
        RenderDiagnostics(_lastPreview.Diagnostics);
        _summary.Text = _lastPreview.Succeeded
            ? _lastPreview.Message + "\n" + ObservationSummary(_lastPreview.Observations)
            : _lastPreview.Message;
        _summary.AddThemeColorOverride(
            "font_color",
            _lastPreview.Succeeded ? StudioStyle.Text : StudioStyle.Danger);
        SetStatus(_lastPreview.Message, error: !_lastPreview.Succeeded);
        UpdateTransactionButtons();
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedAreaLifecycleUx2LwB();
        _applied = result.Succeeded;
        _canonicalPreviewReady = false;
        _lastPreview = null;
        if (result.Succeeded && _draft is not null)
        {
            _draft = _session.CreateAreaLifecycleDraftUx2LwB(_draft.AreaId);
            AreaLifecycleChanged?.Invoke();
        }
        SetStatus(result.Message, error: !result.Succeeded);
        _summary.Text = result.Message;
        RenderDiff(null);
        RenderObservations([]);
        RenderDiagnostics([]);
        UpdateTransactionButtons();
    }

    private void Undo()
    {
        var result = _session.Undo();
        _session.CancelAreaLifecyclePreviewUx2LwB();
        _canonicalPreviewReady = false;
        _lastPreview = null;
        _applied = false;
        ReloadSelectedArea();
        if (result.Succeeded)
        {
            AreaLifecycleChanged?.Invoke();
        }
        SetStatus(result.Message, error: !result.Succeeded);
    }

    private void Redo()
    {
        var result = _session.Redo();
        _session.CancelAreaLifecyclePreviewUx2LwB();
        _canonicalPreviewReady = false;
        _lastPreview = null;
        _applied = result.Succeeded;
        ReloadSelectedArea();
        if (result.Succeeded)
        {
            AreaLifecycleChanged?.Invoke();
        }
        SetStatus(result.Message, error: !result.Succeeded);
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
        if (areaId is null)
        {
            UpdateTransactionButtons();
            return;
        }
        _choices = _session.AreaLifecycleChoicesUx2LwB;
        var index = _choices.ToList().FindIndex(item => item.AreaId == areaId);
        if (index >= 0)
        {
            _area.Select(index + 1);
            LoadChoice(index);
        }
        else
        {
            _area.Select(0);
            SelectArea(0);
        }
    }

    private void InvalidatePreview()
    {
        if (_draft is null)
        {
            return;
        }
        _session.CancelAreaLifecyclePreviewUx2LwB();
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _applied = false;
        _summary.Text = "Brouillon modifié · relancez l’aperçu sec avant application.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Warning);
        _status.Text = "Aperçu précédent effacé · aucun changement appliqué";
        _status.AddThemeColorOverride("font_color", StudioStyle.Warning);
        RenderDiff(null);
        RenderObservations([]);
        RenderDiagnostics([]);
        UpdateTransactionButtons();
    }

    private void ResetControls()
    {
        _entered.Reset();
        _exited.Reset();
    }

    private void SetEditingEnabled(bool enabled)
    {
        _entered.SetEditingEnabled(enabled);
        _exited.SetEditingEnabled(enabled);
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

    private void RenderDiff(StudioUx2LwBSemanticDiff? diff)
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

    private void RenderObservations(IReadOnlyList<StudioUx2LwBEventObservation> observations)
    {
        ClearChildren(_observations);
        if (observations.Count == 0)
        {
            _observations.AddChild(StudioStyle.Label(
                "Lancez l’aperçu sec pour observer le contrat typé.",
                10,
                StudioStyle.Muted,
                wrap: true));
            return;
        }
        foreach (var observation in observations)
        {
            var state = observation.RuntimeEventObserved ? "observé" : "contrat prêt";
            var script = observation.HumanScriptName is null
                ? "observation sans script"
                : $"source « {observation.HumanScriptName} »" +
                  (observation.ScriptRequired ? " · obligatoire" : " · optionnelle");
            _observations.AddChild(StudioStyle.Label(
                $"{observation.HumanEventName} · {state} · {script}\n{observation.Summary}",
                10,
                observation.RuntimeEventObserved ? StudioStyle.Text : StudioStyle.Warning,
                wrap: true));
        }
    }

    private void RenderDiagnostics(IReadOnlyList<StudioUx2LwBDiagnostic> diagnostics)
    {
        ClearChildren(_diagnostics);
        if (diagnostics.Count == 0)
        {
            _diagnostics.AddChild(StudioStyle.Label(
                "Aucun blocage localisé.",
                10,
                StudioStyle.Muted,
                wrap: true));
            return;
        }
        foreach (var diagnostic in diagnostics)
        {
            var location = diagnostic.Path.EndsWith("entered", StringComparison.Ordinal)
                ? "Carte Entrée"
                : diagnostic.Path.EndsWith("exited", StringComparison.Ordinal)
                    ? "Carte Sortie"
                    : "Fondation de la zone";
            _diagnostics.AddChild(StudioStyle.Label(
                $"{location} · {diagnostic.Message}",
                10,
                diagnostic.Blocking ? StudioStyle.Danger : StudioStyle.Warning,
                wrap: true));
        }
    }

    private static string ObservationSummary(IReadOnlyList<StudioUx2LwBEventObservation> observations)
    {
        var observed = observations.Count(item => item.RuntimeEventObserved);
        return $"{observed}/{observations.Count} événement(s) typé(s) observé(s) dans ce contexte · " +
               "binding inspecté uniquement, sans exécution ForgeScript.";
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
