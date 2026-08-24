using System.Text.RegularExpressions;
using Forge.Application.Commands;
using Forge.Application.Modules;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioBlackboardWorkbenchUx6U : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private SourceSnapshotUx6U? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;
    private bool _updating;
    private bool _explicitSelection;
    private IReadOnlyList<StudioUx6UVariableChoice> _choices = [];

    private PanelContainer _dialog = null!;
    private OptionButton _variableChoice = null!;
    private TabContainer _tabs = null!;
    private LineEdit _humanName = null!;
    private LineEdit _canonicalKey = null!;
    private LineEdit _kind = null!;
    private LineEdit _scope = null!;
    private LineEdit _defaultValue = null!;
    private OptionButton _replication = null!;
    private CheckBox _debugVisible = null!;
    private CheckBox _requiredRuntime = null!;
    private LineEdit _runtimeValue = null!;
    private OptionButton _audience = null!;
    private OptionButton _snapshotScope = null!;
    private VBoxContainer _runtimeResults = null!;
    private Label _review = null!;
    private Label _status = null!;
    private Button _apply = null!;
    private Button _undo = null!;
    private Button _redo = null!;

    private StudioUx6UDraft? _draft;
    private StudioUx6UPreview? _preview;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? BlackboardDefinitionChanged;

    internal bool IsOpen => Visible;
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                            !ReferenceEquals(_session, _sourceSession);
    internal bool HasExplicitSelection => _explicitSelection && _draft is not null;
    internal bool PreviewReady => _preview is
    {
        Succeeded: true,
        CanApply: true,
        CanonicalDryRun: true,
        TypedMutation: true,
        DebugSnapshot: true,
        ReplicationDelta: true,
        SaveScopeRoundTrip: true,
        StaleHandleRejected: true,
        RuntimeClaimsBounded: true,
    };
    internal bool ShowsTwoWorkflows => _tabs.GetTabCount() == 2 && _draft is not null;
    internal bool EvidenceSourceUntouched => SourceMatches();
    internal bool EvidenceResourcesReleased => _evidenceReleased && _evidenceSession is null &&
                                                _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantText(this));
            return text.Contains("Variables et blackboard", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu sec canonique", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aucun serveur", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuid(text);
        }
    }

    internal StudioBlackboardWorkbenchUx6U(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        BuildUi();
    }

    private void BuildUi()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        Color = new Color(0.015f, 0.02f, 0.035f, 0.92f);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _dialog = StudioStyle.Panel("Ux6UBlackboardDialog", StudioStyle.Surface, 12, 1);
        _dialog.ClipContents = true;
        _dialog.PropagateMaximumSize = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(15, 12, 15, 12);
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
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("VARIABLES ET BLACKBOARD · UX6-U", 10, StudioStyle.Accent));
        titles.AddChild(StudioStyle.Label("Inspecter, valider et simuler une variable runtime", 18,
            StudioStyle.Text, wrap: true));
        titles.AddChild(StudioStyle.Label(
            "Définition persistante par commande canonique · blackboard copié en mémoire · aucun serveur, client ou GameRoot actif",
            9, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);
        root.AddChild(header);

        var selection = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selection.AddChild(StudioStyle.Label("VARIABLE DÉCLARÉE DU MODULE", 9, StudioStyle.Accent));
        _variableChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _variableChoice.ItemSelected += index => SelectVariable((int)index);
        selection.AddChild(_variableChoice);
        root.AddChild(selection);

        var tabHost = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 330),
            CustomMaximumSize = new Vector2(-1, 590),
            ClipContents = true,
            PropagateMaximumSize = true,
        };
        root.AddChild(tabHost);
        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        tabHost.AddChild(_tabs);
        _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildSchemaTab();
        BuildRuntimeTab();
        WireDraftInvalidation();

        var runtimePanel = StudioStyle.Panel("Ux6UBlackboardPreview", StudioStyle.SurfaceRaised, 8, 1);
        runtimePanel.CustomMinimumSize = new Vector2(0, 126);
        var runtimeMargin = StudioStyle.Margin(9, 7, 9, 7);
        runtimePanel.AddChild(runtimeMargin);
        _runtimeResults = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        runtimeMargin.AddChild(_runtimeResults);
        root.AddChild(runtimePanel);
        _review = StudioStyle.Label("Aucune modification approuvée.", 9, StudioStyle.Muted, wrap: true);
        root.AddChild(_review);
        _status = StudioStyle.Label("Sélectionnez explicitement une variable déclarée.", 10,
            StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 8);
        actions.AddChild(ActionButton("Aperçu sec canonique", Preview, 176));
        _apply = ActionButton("Appliquer", Apply, 120, primary: true);
        _undo = ActionButton("Annuler", Undo, 110);
        _redo = ActionButton("Rétablir", Redo, 116);
        actions.AddChild(_apply);
        actions.AddChild(_undo);
        actions.AddChild(_redo);
        actions.AddChild(ActionButton("Enregistrer", Save, 126));
        root.AddChild(actions);
        ClearPreview();
        UpdateButtons();
    }

    private void BuildSchemaTab()
    {
        var page = TabPage("Définition gouvernée");
        page.AddChild(StudioStyle.Label("ORF-GAME-022 · Schéma persistant de la variable", 10,
            StudioStyle.Accent, wrap: true));
        page.AddChild(StudioStyle.Label(
            "Le nom, la clé, le type et la portée sont protégés ici. Leur création ou modification complète reste disponible dans Projet > Variables.",
            9, StudioStyle.Muted, wrap: true));
        var identity = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _humanName = ReadOnlyField();
        _canonicalKey = ReadOnlyField();
        identity.AddChild(LabeledColumn("Nom humain", _humanName));
        identity.AddChild(LabeledColumn("Clé canonique protégée", _canonicalKey));
        page.AddChild(identity);
        var contract = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _kind = ReadOnlyField();
        _scope = ReadOnlyField();
        _replication = EnumChoice(Enum.GetValues<ProjectVariableReplicationMode>().Select(HumanReplication));
        contract.AddChild(LabeledColumn("Type protégé", _kind));
        contract.AddChild(LabeledColumn("Portée protégée", _scope));
        contract.AddChild(LabeledColumn("Réplication", _replication));
        page.AddChild(contract);
        _defaultValue = Field("Valeur par défaut typée");
        AddLabeled(page, "Valeur par défaut persistante", _defaultValue);
        var flags = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _debugVisible = new CheckBox { Text = "Visible dans la vue debug gouvernée" };
        _requiredRuntime = new CheckBox { Text = "Requise au démarrage runtime" };
        flags.AddChild(_debugVisible);
        flags.AddChild(_requiredRuntime);
        page.AddChild(flags);
        page.AddChild(StudioStyle.Label(
            "Appliquer modifie seulement ces quatre propriétés. Identité, type, portée, description, provenance et variables voisines restent intacts.",
            10, StudioStyle.Warning, wrap: true));
    }

    private void BuildRuntimeTab()
    {
        var page = TabPage("Blackboard en mémoire");
        page.AddChild(StudioStyle.Label("Handle typé, mutation, debug, réplication et sauvegarde", 10,
            StudioStyle.Accent, wrap: true));
        page.AddChild(StudioStyle.Label(
            "Cette simulation repart des valeurs par défaut du brouillon. Elle ne contacte aucun processus runtime et n’écrit aucune sauvegarde joueur.",
            9, StudioStyle.Muted, wrap: true));
        _runtimeValue = Field("Valeur runtime à valider");
        AddLabeled(page, "Valeur temporaire à écrire par handle", _runtimeValue);
        var policies = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _audience = EnumChoice(Enum.GetValues<BlackboardReplicationAudience>().Select(HumanAudience));
        _snapshotScope = EnumChoice(Enum.GetValues<ProjectVariableScope>().Select(HumanScope));
        policies.AddChild(LabeledColumn("Audience du delta", _audience));
        policies.AddChild(LabeledColumn("Portée du snapshot / reload", _snapshotScope));
        page.AddChild(policies);
        var explanation = StudioStyle.Panel("Ux6UBlackboardBoundary", StudioStyle.Surface, 8, 1);
        var explanationMargin = StudioStyle.Margin(10, 9, 10, 9);
        explanation.AddChild(explanationMargin);
        var lines = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        lines.AddChild(StudioStyle.Label("FRONTIÈRE RUNTIME HONNÊTE", 9, StudioStyle.Accent));
        lines.AddChild(StudioStyle.Label(
            "Le service canonique vérifie le handle et normalise la valeur. Le snapshot debug masque les variables protégées et le delta respecte la politique choisie.",
            10, StudioStyle.Text, wrap: true));
        lines.AddChild(StudioStyle.Label(
            "Aucun serveur, client, GameRoot, transport réseau, ForgeScript ou fichier de sauvegarde actif n’est exécuté.",
            10, StudioStyle.Warning, wrap: true));
        explanationMargin.AddChild(lines);
        page.AddChild(explanation);
    }

    internal void Open() => OpenBound(_sourceSession, evidence: false, cancelApproval: false);

    internal void RestoreSourceSurfaceAfterSmoke() =>
        OpenBound(_sourceSession, evidence: false, cancelApproval: false);

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null) return;
        if (Visible) _session.CancelBlackboardApprovalUx6U();
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidence();
        _session = _sourceSession;
        ResetControls();
    }

    internal void CloseEvidence()
    {
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidence();
        _session = _sourceSession;
        ResetControls(keepReleaseState: true);
    }

    internal bool PrepareEvidence(string mode)
    {
        if (!mode.StartsWith("ux6-u", StringComparison.OrdinalIgnoreCase)) return false;
        ReleaseEvidence();
        _sourceSnapshot = CaptureSource();
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux6-u-{Guid.NewGuid():N}");
        try
        {
            var source = _sourceSession.Project;
            if (source is null) return false;
            var created = ProjectSession.Create(root, StudioSessionController.CreateEvidenceProjectUx6U(source));
            if (!created.Succeeded) return false;
            var isolated = new StudioSessionController();
            if (!isolated.Open(root).Succeeded)
            {
                isolated.Close();
                StudioSessionController.DeleteTemporaryUx6U(root);
                return false;
            }
            _evidenceSession = isolated;
            _evidenceRoot = root;
            _evidenceReleased = false;
            OpenBound(isolated, evidence: true, cancelApproval: true);
            var index = _choices.ToList().FindIndex(choice =>
                isolated.FindBlackboardVariableUx6U(choice.Id)?.Key == "studio.blackboard.health");
            if (index < 0) return false;
            _variableChoice.Select(index + 1);
            SelectVariable(index + 1);
            if (_draft is null) return false;
            _draft = StudioSessionController.ChangedBlackboardDraftUx6U(_draft);
            PopulateDraft();
            _preview = _session.PreviewBlackboardUx6U(_draft);
            RenderPreview();
            return PreviewReady && ShowsTwoWorkflows && ShowsHumanSurface && SourceMatches();
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_U_EVIDENCE_FAILED type={exception.GetType().Name}");
            return false;
        }
        finally
        {
            if (!PreviewReady || !ShowsTwoWorkflows || !SourceMatches())
            {
                if (_evidenceSession is not null) ReleaseEvidence();
                else StudioSessionController.DeleteTemporaryUx6U(root);
            }
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        var approval = _sourceSession.CaptureBlackboardApprovalUx6U();
        try
        {
            var ready = PrepareEvidence("ux6-u-blackboard");
            if (!ready || _evidenceSession is null || !ReferenceEquals(_session, _evidenceSession) ||
                !PreviewReady) return false;
            var applied = _session.ApplyApprovedBlackboardUx6U();
            var undo = applied.Succeeded ? _session.UndoBlackboardUx6U() : new(false, "non exécuté");
            var redo = undo.Succeeded ? _session.RedoBlackboardUx6U() : new(false, "non exécuté");
            var saved = redo.Succeeded ? _session.SaveBlackboardUx6U() : new(false, "non exécuté");
            return applied.Succeeded && undo.Succeeded && redo.Succeeded && saved.Succeeded && SourceMatches();
        }
        finally
        {
            CloseEvidence();
            _sourceSession.RestoreBlackboardApprovalUx6U(approval);
        }
    }

    private void OpenBound(StudioSessionController target, bool evidence, bool cancelApproval)
    {
        _session = target;
        if (cancelApproval) _session.CancelBlackboardApprovalUx6U();
        Visible = true;
        _explicitSelection = false;
        _draft = null;
        _preview = null;
        PopulateChoices();
        ClearEditors();
        ClearPreview();
        _status.Text = evidence
            ? "Preuve isolée · sélectionnez la variable de démonstration."
            : "Sélectionnez explicitement une variable déclarée.";
        UpdateBounds();
        UpdateButtons();
    }

    private void PopulateChoices()
    {
        _choices = _session.BlackboardChoicesUx6U();
        _variableChoice.Clear();
        _variableChoice.AddItem(_choices.Count == 0
            ? "Aucune variable déclarée · utilisez Projet > Variables"
            : "Choisir explicitement une variable…");
        foreach (var choice in _choices) _variableChoice.AddItem(choice.Label);
        _variableChoice.Select(0);
        _variableChoice.Disabled = _choices.Count == 0;
    }

    private void SelectVariable(int index)
    {
        if (_updating) return;
        _session.CancelBlackboardApprovalUx6U();
        _preview = null;
        if (index <= 0 || index > _choices.Count)
        {
            _explicitSelection = false;
            _draft = null;
            ClearEditors();
            ClearPreview();
            SetStatus(_choices.Count == 0
                ? "Aucune variable déclarée · créez le schéma dans Projet > Variables."
                : "Sélectionnez explicitement une variable.");
            return;
        }
        _explicitSelection = true;
        _draft = _session.CreateBlackboardDraftUx6U(true, _choices[index - 1].Id);
        if (_draft is null)
        {
            SetStatus("La variable n’est plus disponible · rechargez la surface.", error: true);
            return;
        }
        PopulateDraft();
        ClearPreview();
        SetStatus("Variable chargée · la simulation repartira de sa valeur par défaut.");
    }

    private void PopulateDraft()
    {
        if (_draft is null) return;
        var variable = _session.FindBlackboardVariableUx6U(_draft.VariableId);
        if (variable is null) return;
        _updating = true;
        _humanName.Text = StudioUx0IVariableText.DisplayName(variable);
        _canonicalKey.Text = variable.Key;
        _kind.Text = HumanKind(variable.Kind);
        _scope.Text = HumanScope(variable.Scope);
        _defaultValue.Text = _draft.DefaultValue;
        _replication.Select((int)_draft.Replication);
        _debugVisible.ButtonPressed = _draft.DebugVisible;
        _requiredRuntime.ButtonPressed = _draft.RequiredAtRuntime;
        _runtimeValue.Text = _draft.RuntimeValue;
        _audience.Select((int)_draft.Audience);
        _snapshotScope.Select((int)_draft.SnapshotScope);
        _updating = false;
        UpdateButtons();
    }

    private bool TryReadDraft(out StudioUx6UDraft? draft, out string error)
    {
        draft = null;
        error = string.Empty;
        if (_draft is null)
        {
            error = "Sélectionnez explicitement une variable déclarée.";
            return false;
        }
        draft = _draft with
        {
            DefaultValue = _defaultValue.Text,
            Replication = (ProjectVariableReplicationMode)_replication.Selected,
            DebugVisible = _debugVisible.ButtonPressed,
            RequiredAtRuntime = _requiredRuntime.ButtonPressed,
            RuntimeValue = _runtimeValue.Text,
            Audience = (BlackboardReplicationAudience)_audience.Selected,
            SnapshotScope = (ProjectVariableScope)_snapshotScope.Selected,
        };
        return true;
    }

    private void Preview()
    {
        if (!TryReadDraft(out var draft, out var error) || draft is null)
        {
            SetStatus(error, error: true);
            return;
        }
        _draft = draft;
        _preview = _session.PreviewBlackboardUx6U(draft);
        RenderPreview();
        SetStatus(_preview.Message, error: !_preview.Succeeded);
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedBlackboardUx6U();
        SetStatus(result.Message, error: !result.Succeeded);
        if (!result.Succeeded) return;
        BlackboardDefinitionChanged?.Invoke();
        ReloadAfterMutation();
    }

    private void Undo()
    {
        var result = _session.UndoBlackboardUx6U();
        SetStatus(result.Message, error: !result.Succeeded);
        if (!result.Succeeded) return;
        BlackboardDefinitionChanged?.Invoke();
        ReloadAfterMutation();
    }

    private void Redo()
    {
        var result = _session.RedoBlackboardUx6U();
        SetStatus(result.Message, error: !result.Succeeded);
        if (!result.Succeeded) return;
        BlackboardDefinitionChanged?.Invoke();
        ReloadAfterMutation();
    }

    private void Save()
    {
        var result = _session.SaveBlackboardUx6U();
        SetStatus(result.Message, error: !result.Succeeded);
    }

    private void ReloadAfterMutation()
    {
        var selected = _draft?.VariableId;
        PopulateChoices();
        var index = selected is null ? -1 : _choices.ToList().FindIndex(item => item.Id == selected);
        if (index < 0)
        {
            _draft = null;
            _explicitSelection = false;
            ClearEditors();
        }
        else
        {
            _variableChoice.Select(index + 1);
            _explicitSelection = true;
            _draft = _session.CreateBlackboardDraftUx6U(true, selected);
            PopulateDraft();
        }
        _preview = null;
        ClearPreview();
        UpdateButtons();
    }

    private void RenderPreview()
    {
        ClearChildren(_runtimeResults);
        _runtimeResults.AddChild(StudioStyle.Label("APERÇU RUNTIME · aperçu sec canonique", 9,
            StudioStyle.Accent));
        if (_preview is null) return;
        foreach (var line in _preview.RuntimePreview.Take(7))
            _runtimeResults.AddChild(StudioStyle.Label(line, 9,
                line.StartsWith("Aucun ", StringComparison.Ordinal) ? StudioStyle.Warning : StudioStyle.Text,
                wrap: true));
        foreach (var diagnostic in _preview.Diagnostics)
            _runtimeResults.AddChild(StudioStyle.Label($"{diagnostic.Code} · {diagnostic.Message}", 9,
                StudioStyle.Danger, wrap: true));
        _review.Text = _preview.Diff.Count == 0
            ? "Aucune différence persistante · la simulation runtime reste disponible."
            : "DIFF HUMAIN · " + string.Join(" · ", _preview.Diff);
        UpdateButtons();
    }

    private void ClearPreview()
    {
        if (_runtimeResults is null) return;
        ClearChildren(_runtimeResults);
        _runtimeResults.AddChild(StudioStyle.Label("APERÇU RUNTIME · aucun aperçu sec canonique", 9,
            StudioStyle.Accent));
        if (_review is not null) _review.Text = "Aucune modification approuvée.";
    }

    private void WireDraftInvalidation()
    {
        _defaultValue.TextChanged += _ => DraftChanged();
        _runtimeValue.TextChanged += _ => DraftChanged();
        _replication.ItemSelected += _ => DraftChanged();
        _audience.ItemSelected += _ => DraftChanged();
        _snapshotScope.ItemSelected += _ => DraftChanged();
        _debugVisible.Toggled += _ => DraftChanged();
        _requiredRuntime.Toggled += _ => DraftChanged();
    }

    private void DraftChanged()
    {
        if (_updating) return;
        _session.CancelBlackboardApprovalUx6U();
        _preview = null;
        ClearPreview();
        SetStatus("Brouillon modifié · relancez l’aperçu sec canonique.");
        UpdateButtons();
    }

    private void ResetControls(bool keepReleaseState = false)
    {
        _updating = true;
        _explicitSelection = false;
        _draft = null;
        _preview = null;
        _choices = [];
        if (_variableChoice is not null)
        {
            _variableChoice.Clear();
            _variableChoice.AddItem("Choisir explicitement une variable…");
            _variableChoice.Select(0);
        }
        ClearEditors();
        ClearPreview();
        if (_status is not null) _status.Text = "Sélectionnez explicitement une variable déclarée.";
        if (!keepReleaseState) _evidenceReleased = true;
        _updating = false;
        UpdateButtons();
    }

    private void ClearEditors()
    {
        foreach (var field in new[] { _humanName, _canonicalKey, _kind, _scope, _defaultValue, _runtimeValue })
            if (field is not null) field.Text = string.Empty;
        if (_replication is not null) _replication.Select(0);
        if (_audience is not null) _audience.Select(0);
        if (_snapshotScope is not null) _snapshotScope.Select(0);
        if (_debugVisible is not null) _debugVisible.ButtonPressed = false;
        if (_requiredRuntime is not null) _requiredRuntime.ButtonPressed = false;
    }

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = message;
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Muted;
        StatusChanged?.Invoke(message, error);
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        if (_apply is null) return;
        _apply.Disabled = !PreviewReady;
        _undo.Disabled = _session.History?.CanUndo != true;
        _redo.Disabled = _session.History?.CanRedo != true;
    }

    private SourceSnapshotUx6U CaptureSource()
    {
        var history = _sourceSession.History!;
        return new(_sourceSession.Project!, history, history.State.Revision, history.Journal.ToArray(),
            _sourceSession.IsDirty, history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureBlackboardApprovalUx6U());
    }

    private bool SourceMatches()
    {
        var snapshot = _sourceSnapshot;
        if (snapshot is null) return true;
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.IsDirty && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo && _sourceSession.ActiveAreaId == snapshot.AreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureBlackboardApprovalUx6U() == snapshot.Approval;
    }

    private void ReleaseEvidence()
    {
        _evidenceSession?.Close();
        _evidenceSession = null;
        var released = string.IsNullOrWhiteSpace(_evidenceRoot) ||
                       StudioSessionController.DeleteTemporaryUx6U(_evidenceRoot);
        _evidenceRoot = null;
        _evidenceReleased = released;
        _session = _sourceSession;
        if (!released) GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_U_UI_CLEANUP_FAILED");
    }

    private VBoxContainer TabPage(string title)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var margin = StudioStyle.Margin(10, 9, 10, 9);
        margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(margin);
        var page = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", 8);
        margin.AddChild(page);
        _tabs.AddChild(scroll);
        return page;
    }

    private static LineEdit Field(string placeholder) => new()
    {
        PlaceholderText = placeholder,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        CustomMinimumSize = new Vector2(140, 34),
    };

    private static LineEdit ReadOnlyField()
    {
        var field = Field(string.Empty);
        field.Editable = false;
        return field;
    }

    private static OptionButton EnumChoice(IEnumerable<string> labels)
    {
        var choice = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(180, 34),
        };
        foreach (var label in labels) choice.AddItem(label);
        return choice;
    }

    private static VBoxContainer LabeledColumn(string label, Control control)
    {
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddChild(StudioStyle.Label(label.ToUpperInvariant(), 8, StudioStyle.Muted));
        column.AddChild(control);
        return column;
    }

    private static void AddLabeled(VBoxContainer parent, string label, Control control) =>
        parent.AddChild(LabeledColumn(label, control));

    private static Button ActionButton(string text, Action action, float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        return button;
    }

    private void UpdateBounds()
    {
        var viewport = GetViewportRect().Size;
        var width = Math.Clamp(viewport.X - 64, 1060, 1680);
        var height = Math.Clamp(viewport.Y - 44, 760, 1010);
        _dialog.Position = new Vector2((viewport.X - width) / 2f, (viewport.Y - height) / 2f);
        _dialog.Size = new Vector2(width, height);
    }

    private static string HumanKind(ProjectVariableValueKind value) => value switch
    {
        ProjectVariableValueKind.String => "Texte",
        ProjectVariableValueKind.Boolean => "Oui / non",
        ProjectVariableValueKind.Integer => "Entier",
        ProjectVariableValueKind.Number => "Nombre",
        _ => "Type inconnu",
    };

    private static string HumanScope(ProjectVariableScope value) => value switch
    {
        ProjectVariableScope.Session => "Session",
        ProjectVariableScope.Save => "Sauvegarde",
        ProjectVariableScope.Player => "Joueur",
        ProjectVariableScope.Party => "Groupe",
        ProjectVariableScope.Server => "Serveur",
        _ => "Portée inconnue",
    };

    private static string HumanReplication(ProjectVariableReplicationMode value) => value switch
    {
        ProjectVariableReplicationMode.None => "Aucune",
        ProjectVariableReplicationMode.Owner => "Propriétaire",
        ProjectVariableReplicationMode.Party => "Groupe",
        ProjectVariableReplicationMode.AllClients => "Tous les clients",
        ProjectVariableReplicationMode.ServerAuthority => "Autorité serveur",
        _ => "Politique inconnue",
    };

    private static string HumanAudience(BlackboardReplicationAudience value) => value switch
    {
        BlackboardReplicationAudience.Owner => "Propriétaire",
        BlackboardReplicationAudience.Party => "Groupe",
        BlackboardReplicationAudience.Client => "Client",
        BlackboardReplicationAudience.Server => "Serveur",
        _ => "Audience inconnue",
    };

    private static void ClearChildren(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static IEnumerable<string> DescendantText(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            switch (child)
            {
                case Label label when label.Visible: yield return label.Text; break;
                case Button button when button.Visible: yield return button.Text; break;
                case CheckBox checkBox when checkBox.Visible: yield return checkBox.Text; break;
                case LineEdit lineEdit when lineEdit.Visible: yield return lineEdit.Text; break;
                case OptionButton option when option.Visible:
                    for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
                    break;
            }
            foreach (var text in DescendantText(child)) yield return text;
        }
    }

    private static bool ContainsUuid(string value) => Regex.IsMatch(value,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b");

    private sealed record SourceSnapshotUx6U(
        ProjectDefinition Project,
        ProjectCommandHistory History,
        ProjectRevision Revision,
        IReadOnlyList<CommandHistoryJournalEntry> Journal,
        bool IsDirty,
        bool CanUndo,
        bool CanRedo,
        AreaId? AreaId,
        IReadOnlyList<EntityId> SelectedEntityIds,
        EntityId? SelectedEntityId,
        StudioUx6UApprovalSnapshot Approval);
}
