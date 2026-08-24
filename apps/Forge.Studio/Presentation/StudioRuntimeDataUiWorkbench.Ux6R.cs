using System.Globalization;
using System.Text.RegularExpressions;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioRuntimeDataUiWorkbenchUx6R : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private SourceSnapshotUx6R? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;
    private bool _updating;
    private bool _explicitSelection;
    private int _currentRuleIndex = -1;
    private int _currentUiIndex = -1;
    private int _currentJsonIndex = -1;
    private bool _multiRecordDraftPreserved;

    private PanelContainer _dialog = null!;
    private OptionButton _profileChoice = null!;
    private TabContainer _tabs = null!;
    private OptionButton _ruleChoice = null!;
    private LineEdit _ruleKey = null!;
    private OptionButton _ruleKind = null!;
    private CheckBox _ruleRequired = null!;
    private TextEdit _ruleSchema = null!;
    private TextEdit _ruleDefaults = null!;
    private OptionButton _uiChoice = null!;
    private LineEdit _uiKey = null!;
    private LineEdit _uiTitle = null!;
    private LineEdit _uiTheme = null!;
    private CheckBox _uiRequired = null!;
    private TextEdit _widgets = null!;
    private TextEdit _events = null!;
    private OptionButton _jsonChoice = null!;
    private LineEdit _jsonKey = null!;
    private TextEdit _jsonSchema = null!;
    private TextEdit _jsonValue = null!;
    private LineEdit _jsonBytes = null!;
    private LineEdit _jsonDepth = null!;
    private CheckBox _jsonMutable = null!;
    private LineEdit _snapshotComponents = null!;
    private LineEdit _snapshotReferences = null!;
    private LineEdit _snapshotInventory = null!;
    private LineEdit _snapshotBlackboard = null!;
    private CheckBox _snapshotRestore = null!;
    private VBoxContainer _runtimeResults = null!;
    private Label _review = null!;
    private Label _status = null!;
    private Button _apply = null!;
    private Button _undo = null!;
    private Button _redo = null!;

    private StudioUx6RDraft? _draft;
    private StudioUx6RPreview? _preview;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? RuntimeDataChanged;

    internal bool IsOpen => Visible;
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                            !ReferenceEquals(_session, _sourceSession);
    internal bool HasExplicitSelection => _explicitSelection && _draft is not null;
    internal bool PreviewReady => _preview is
    {
        Succeeded: true,
        CanApply: true,
        CanonicalDryRun: true,
        RuleResolved: true,
        UiEventPublished: true,
        JsonMutationSucceeded: true,
        SnapshotPolicyReady: true,
        RuntimeClaimsBounded: true,
    };
    internal bool ShowsFiveWorkflows => _tabs.GetTabCount() == 4 && _draft is
    {
        Rules.Count: > 0,
        UiDocuments.Count: > 0,
        JsonDocuments.Count: > 0,
    };
    internal bool EvidenceSourceUntouched => SourceMatches();
    internal bool EvidenceResourcesReleased => _evidenceReleased && _evidenceSession is null &&
                                                _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool MultiRecordDraftPreserved => _multiRecordDraftPreserved;
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantText(this));
            return Visible && text.Contains("Règles data-driven", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Interface scriptable", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Événements GUI typés", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("JSON runtime", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("État sérialisable", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu sec canonique", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuid(text);
        }
    }

    internal StudioRuntimeDataUiWorkbenchUx6R(StudioSessionController session)
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

        _dialog = StudioStyle.Panel("Ux6RDialog", StudioStyle.Surface, 12, 1);
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
        titles.AddChild(StudioStyle.Label("DONNÉES & UI RUNTIME · UX6-R", 10, StudioStyle.Accent));
        titles.AddChild(StudioStyle.Label("Règles data-driven, interface scriptable, événements GUI typés, JSON runtime et état sérialisable", 18,
            StudioStyle.Text, wrap: true));
        titles.AddChild(StudioStyle.Label("Une commande canonique · aperçu en mémoire · aucun nœud moteur muté", 9,
            StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);
        root.AddChild(header);

        var selection = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selection.AddChild(StudioStyle.Label("PROFIL DU MODULE", 9, StudioStyle.Accent));
        _profileChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _profileChoice.ItemSelected += index => SelectProfile((int)index);
        selection.AddChild(_profileChoice);
        root.AddChild(selection);

        var tabHost = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 330),
            CustomMaximumSize = new Vector2(-1, 620),
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
        BuildRulesTab();
        BuildUiTab();
        BuildJsonTab();
        BuildSnapshotTab();
        WireDraftInvalidation();

        var runtimePanel = StudioStyle.Panel("Ux6RRuntime", StudioStyle.SurfaceRaised, 8, 1);
        runtimePanel.CustomMinimumSize = new Vector2(0, 72);
        var runtimeMargin = StudioStyle.Margin(9, 7, 9, 7);
        runtimePanel.AddChild(runtimeMargin);
        _runtimeResults = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _runtimeResults.AddChild(StudioStyle.Label("APERÇU DES SERVICES · aucun aperçu sec canonique", 9,
            StudioStyle.Accent));
        runtimeMargin.AddChild(_runtimeResults);
        root.AddChild(runtimePanel);
        _review = StudioStyle.Label("Aucune modification approuvée.", 9, StudioStyle.Muted, wrap: true);
        root.AddChild(_review);
        _status = StudioStyle.Label("Sélectionnez explicitement le profil du module.", 10,
            StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 8);
        actions.AddChild(ActionButton("Aperçu sec canonique", Preview, 172));
        _apply = ActionButton("Appliquer", Apply, 120, primary: true);
        _undo = ActionButton("Annuler", Undo, 110);
        _redo = ActionButton("Rétablir", Redo, 116);
        actions.AddChild(_apply);
        actions.AddChild(_undo);
        actions.AddChild(_redo);
        actions.AddChild(ActionButton("Enregistrer", Save, 126));
        root.AddChild(actions);
        UpdateButtons();
    }

    private void BuildRulesTab()
    {
        var page = TabPage("Règles data-driven");
        page.AddChild(StudioStyle.Label("ORF-EE-011 · schémas de classe, progression, arme, VFX et incantation", 10,
            StudioStyle.Accent, wrap: true));
        page.AddChild(StudioStyle.Label("Choisissez une règle, puis modifiez sa clé, son type et ses valeurs. Le JSON reste visible car il constitue le contrat runtime.",
            9, StudioStyle.Muted, wrap: true));
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _ruleChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _ruleChoice.ItemSelected += index => SelectRule((int)index);
        row.AddChild(_ruleChoice);
        row.AddChild(ActionButton("Nouvelle règle", AddRule, 126));
        row.AddChild(ActionButton("Retirer", RemoveRule, 92));
        page.AddChild(row);
        _ruleKey = Field("class.hero");
        AddLabeled(page, "Clé humaine stable", _ruleKey);
        _ruleKind = EnumChoice(Enum.GetNames<Forge.Domain.RuntimeExtensions.RuleSchemaKind>());
        AddLabeled(page, "Famille de règle", _ruleKind);
        _ruleRequired = new CheckBox { Text = "Requise par le runtime" };
        page.AddChild(_ruleRequired);
        var columns = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var schema = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        schema.AddChild(StudioStyle.Label("SCHÉMA JSON", 9, StudioStyle.Accent));
        _ruleSchema = Multi(132);
        schema.AddChild(_ruleSchema);
        columns.AddChild(schema);
        var defaults = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        defaults.AddChild(StudioStyle.Label("VALEURS PAR DÉFAUT", 9, StudioStyle.Accent));
        _ruleDefaults = Multi(132);
        defaults.AddChild(_ruleDefaults);
        columns.AddChild(defaults);
        page.AddChild(columns);
    }

    private void BuildUiTab()
    {
        var page = TabPage("Interface & événements");
        page.AddChild(StudioStyle.Label("ORF-EE-012/013 · Interface scriptable et événements GUI typés", 10,
            StudioStyle.Accent, wrap: true));
        page.AddChild(StudioStyle.Label("Les identités techniques restent protégées. Un widget par ligne : clé | type | parent | texte | binding | interactif | ordre | permission.",
            9, StudioStyle.Muted, wrap: true));
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _uiChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _uiChoice.ItemSelected += index => SelectUi((int)index);
        row.AddChild(_uiChoice);
        row.AddChild(ActionButton("Nouvelle interface", AddUi, 146));
        row.AddChild(ActionButton("Retirer", RemoveUi, 92));
        page.AddChild(row);
        var identity = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _uiKey = Field("runtime.hud");
        _uiTitle = Field("HUD d’aventure");
        _uiTheme = Field("forge-dark");
        identity.AddChild(LabeledColumn("Clé", _uiKey));
        identity.AddChild(LabeledColumn("Titre", _uiTitle));
        identity.AddChild(LabeledColumn("Thème", _uiTheme));
        page.AddChild(identity);
        _uiRequired = new CheckBox { Text = "Interface requise au runtime" };
        page.AddChild(_uiRequired);
        var columns = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var widgets = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        widgets.AddChild(StudioStyle.Label("WIDGETS · édition directe", 9, StudioStyle.Accent));
        _widgets = Multi(194);
        widgets.AddChild(_widgets);
        columns.AddChild(widgets);
        var events = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        events.AddChild(StudioStyle.Label("ÉVÉNEMENTS · widget | événement | commande | contexte | permission", 9,
            StudioStyle.Accent, wrap: true));
        _events = Multi(194);
        events.AddChild(_events);
        columns.AddChild(events);
        page.AddChild(columns);
    }

    private void BuildJsonTab()
    {
        var page = TabPage("JSON runtime");
        page.AddChild(StudioStyle.Label("ORF-EE-015 · document JSON versionné, permissions, quotas et révisions", 10,
            StudioStyle.Accent, wrap: true));
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _jsonChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _jsonChoice.ItemSelected += index => SelectJson((int)index);
        row.AddChild(_jsonChoice);
        row.AddChild(ActionButton("Nouveau document", AddJson, 148));
        row.AddChild(ActionButton("Retirer", RemoveJson, 92));
        page.AddChild(row);
        _jsonKey = Field("runtime.session");
        AddLabeled(page, "Clé humaine stable", _jsonKey);
        var quotas = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _jsonBytes = Field("16384");
        _jsonDepth = Field("16");
        quotas.AddChild(LabeledColumn("Quota UTF-8 (octets)", _jsonBytes));
        quotas.AddChild(LabeledColumn("Profondeur maximale", _jsonDepth));
        _jsonMutable = new CheckBox { Text = "Modifiable par ForgeScript autorisé" };
        quotas.AddChild(_jsonMutable);
        page.AddChild(quotas);
        var columns = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var schema = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        schema.AddChild(StudioStyle.Label("SCHÉMA JSON SÛR", 9, StudioStyle.Accent));
        _jsonSchema = Multi(210);
        schema.AddChild(_jsonSchema);
        columns.AddChild(schema);
        var value = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        value.AddChild(StudioStyle.Label("VALEUR INITIALE", 9, StudioStyle.Accent));
        _jsonValue = Multi(210);
        value.AddChild(_jsonValue);
        columns.AddChild(value);
        page.AddChild(columns);
    }

    private void BuildSnapshotTab()
    {
        var page = TabPage("État sérialisable");
        page.AddChild(StudioStyle.Label("ORF-EE-016 · composants, références, inventaire et blackboard bornés", 10,
            StudioStyle.Accent, wrap: true));
        page.AddChild(StudioStyle.Label("Composants séparés par des virgules. Les identités d’entités restent sous l’autorité du runtime et ne sont pas saisies ici.",
            9, StudioStyle.Muted, wrap: true));
        _snapshotComponents = Field("transform, combat, inventory, blackboard");
        AddLabeled(page, "Composants sauvegardables", _snapshotComponents);
        var quotas = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _snapshotReferences = Field("256");
        _snapshotInventory = Field("512");
        _snapshotBlackboard = Field("65536");
        quotas.AddChild(LabeledColumn("Références max.", _snapshotReferences));
        quotas.AddChild(LabeledColumn("Objets inventaire max.", _snapshotInventory));
        quotas.AddChild(LabeledColumn("Blackboard max. (octets)", _snapshotBlackboard));
        page.AddChild(quotas);
        _snapshotRestore = new CheckBox { Text = "Restaurer les références résolues" };
        page.AddChild(_snapshotRestore);
        page.AddChild(StudioStyle.Label("L’aperçu contrôle uniquement la politique. Il ne crée pas de sauvegarde produit et ne restaure aucun acteur.",
            10, StudioStyle.Warning, wrap: true));
    }

    internal void Open() => OpenBound(_sourceSession, evidence: false, cancelApproval: false);

    internal void RestoreSourceSurfaceAfterSmoke() =>
        OpenBound(_sourceSession, evidence: false, cancelApproval: false);

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null) return;
        if (Visible) _session.CancelRuntimeDataApprovalUx6R();
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
        if (_sourceSession.Project is null || _sourceSession.History is null) return false;
        if (_evidenceSession is not null) ReleaseEvidence();
        _sourceSnapshot = CaptureSource();
        _evidenceRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux6-r-ui-{Guid.NewGuid():N}");
        _evidenceReleased = false;
        try
        {
            var project = StudioSessionController.CreateEvidenceProjectUx6R(_sourceSession.Project);
            if (!Forge.ModuleFormat.Workspaces.ProjectSession.Create(_evidenceRoot, project).Succeeded)
            {
                ReleaseEvidence();
                return false;
            }
            _evidenceSession = new StudioSessionController();
            if (!_evidenceSession.Open(_evidenceRoot).Succeeded)
            {
                ReleaseEvidence();
                return false;
            }
            OpenBound(_evidenceSession, evidence: true, cancelApproval: false);
            _profileChoice.Select(1);
            SelectProfile(1);
            if (_draft is null)
            {
                ReleaseEvidence();
                return false;
            }
            _draft = StudioSessionController.ChangedDraftUx6R(_draft);
            PopulateDraft();
            Preview();
            _tabs.CurrentTab = mode.Contains("ui", StringComparison.OrdinalIgnoreCase) ? 1 :
                mode.Contains("json", StringComparison.OrdinalIgnoreCase) ? 2 :
                mode.Contains("snapshot", StringComparison.OrdinalIgnoreCase) ? 3 : 0;
            var previewReady = PreviewReady;
            var workflowsReady = ShowsFiveWorkflows;
            var humanReady = ShowsHumanSurface;
            var sourceReady = SourceMatches();
            var ready = previewReady && workflowsReady && humanReady && sourceReady;
            if (!ready)
            {
                var humanText = string.Join(' ', DescendantText(this));
                GD.Print($"OPENRPG_FORGE_STUDIO_UX6_R_UI_FAILED preview={previewReady.ToString().ToLowerInvariant()} " +
                         $"workflows={workflowsReady.ToString().ToLowerInvariant()} " +
                         $"human={humanReady.ToString().ToLowerInvariant()} " +
                         $"source={sourceReady.ToString().ToLowerInvariant()} " +
                         $"rule_text={humanText.Contains("Règles data-driven", StringComparison.OrdinalIgnoreCase).ToString().ToLowerInvariant()} " +
                         $"ui_text={humanText.Contains("Interface scriptable", StringComparison.OrdinalIgnoreCase).ToString().ToLowerInvariant()} " +
                         $"event_text={humanText.Contains("Événements GUI typés", StringComparison.OrdinalIgnoreCase).ToString().ToLowerInvariant()} " +
                         $"json_text={humanText.Contains("JSON runtime", StringComparison.OrdinalIgnoreCase).ToString().ToLowerInvariant()} " +
                         $"snapshot_text={humanText.Contains("État sérialisable", StringComparison.OrdinalIgnoreCase).ToString().ToLowerInvariant()} " +
                         $"preview_text={humanText.Contains("Aperçu sec canonique", StringComparison.OrdinalIgnoreCase).ToString().ToLowerInvariant()} " +
                         $"uuid={ContainsUuid(humanText).ToString().ToLowerInvariant()} " +
                         $"rules={_draft?.Rules.Count ?? -1} ui={_draft?.UiDocuments.Count ?? -1} " +
                         $"json={_draft?.JsonDocuments.Count ?? -1} tabs={_tabs.GetTabCount()}");
            }
            return ready;
        }
        catch
        {
            ReleaseEvidence();
            return false;
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        _multiRecordDraftPreserved = false;
        var ready = PrepareEvidence("ux6-r-runtime-data-ui");
        if (!ready || !UsesIsolatedEvidence || !PreviewReady || _evidenceSession is null)
        {
            CloseEvidence();
            return false;
        }
        try
        {
            var isolated = _evidenceSession;
            _multiRecordDraftPreserved = ExerciseMultiRecordDraftPreservation();
            if (!_multiRecordDraftPreserved)
                return false;
            Preview();
            if (!PreviewReady)
                return false;
            var before = isolated.Project;
            var applied = isolated.ApplyApprovedRuntimeDataUx6R();
            var after = isolated.Project;
            var undo = isolated.UndoRuntimeDataUx6R();
            var undone = ReferenceEquals(before, isolated.Project);
            var redo = isolated.RedoRuntimeDataUx6R();
            var redone = ReferenceEquals(after, isolated.Project);
            var saved = isolated.SaveRuntimeDataUx6R();
            return applied.Succeeded && undo.Succeeded && undone && redo.Succeeded && redone &&
                   saved.Succeeded && SourceMatches();
        }
        finally
        {
            CloseEvidence();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && IsInsideTree()) UpdateBounds();
    }

    private void OpenBound(StudioSessionController controller, bool evidence, bool cancelApproval)
    {
        _session = controller;
        if (cancelApproval) _session.CancelRuntimeDataApprovalUx6R();
        ResetControls(keepReleaseState: evidence);
        RebuildProfileChoices();
        Visible = true;
        UpdateBounds();
    }

    private void RebuildProfileChoices()
    {
        _updating = true;
        _profileChoice.Clear();
        _profileChoice.AddItem("Choisir explicitement le profil runtime…");
        foreach (var choice in _session.RuntimeDataChoicesUx6R()) _profileChoice.AddItem(choice.Label);
        _profileChoice.Select(0);
        _updating = false;
    }

    private void SelectProfile(int index)
    {
        if (_updating) return;
        _explicitSelection = index > 0;
        _draft = _session.CreateRuntimeDataDraftUx6R(_explicitSelection);
        _preview = null;
        if (_draft is null)
        {
            ClearEditors();
            SetStatus("Sélection explicite requise.", error: true);
            return;
        }
        PopulateDraft();
        SetStatus("Profil chargé · préparez un aperçu sec canonique.");
    }

    private void PopulateDraft()
    {
        if (_draft is null) return;
        _updating = true;
        RebuildRules();
        RebuildUiDocuments();
        RebuildJsonDocuments();
        _snapshotComponents.Text = string.Join(", ", _draft.Snapshot.Components);
        _snapshotReferences.Text = _draft.Snapshot.MaxReferences.ToString(CultureInfo.InvariantCulture);
        _snapshotInventory.Text = _draft.Snapshot.MaxInventoryItems.ToString(CultureInfo.InvariantCulture);
        _snapshotBlackboard.Text = _draft.Snapshot.MaxBlackboardBytes.ToString(CultureInfo.InvariantCulture);
        _snapshotRestore.ButtonPressed = _draft.Snapshot.RestoreReferences;
        _updating = false;
        UpdateButtons();
    }

    private void RebuildRules(int selected = 0)
    {
        _ruleChoice.Clear();
        if (_draft is null || _draft.Rules.Count == 0)
        {
            _currentRuleIndex = -1;
            _ruleChoice.AddItem("Aucune règle · créez-en une explicitement");
            ClearRuleEditor();
            return;
        }
        foreach (var item in _draft.Rules) _ruleChoice.AddItem(Human(item.Key));
        _ruleChoice.Select(Math.Clamp(selected, 0, _draft.Rules.Count - 1));
        LoadRule(_ruleChoice.Selected);
    }

    private void SelectRule(int index)
    {
        if (_updating || _draft is null || index < 0 || index >= _draft.Rules.Count) return;
        if (index == _currentRuleIndex) return;
        if (_currentRuleIndex >= 0 && !SaveRule(out var error))
        {
            _updating = true;
            _ruleChoice.Select(_currentRuleIndex);
            _updating = false;
            SetStatus(error, true);
            return;
        }
        LoadRule(index);
    }

    private void LoadRule(int index)
    {
        if (_draft is null || index < 0 || index >= _draft.Rules.Count) return;
        var item = _draft.Rules[index];
        var wasUpdating = _updating;
        _updating = true;
        _ruleKey.Text = item.Key;
        SelectEnum(_ruleKind, item.Kind);
        _ruleRequired.ButtonPressed = item.Required;
        _ruleSchema.Text = item.JsonSchema;
        _ruleDefaults.Text = item.DefaultsJson;
        _currentRuleIndex = index;
        _updating = wasUpdating;
    }

    private bool SaveRule(out string error)
    {
        error = string.Empty;
        if (_draft is null || _draft.Rules.Count == 0) return true;
        var index = _currentRuleIndex;
        if (index < 0 || index >= _draft.Rules.Count) return true;
        if (string.IsNullOrWhiteSpace(_ruleKey.Text))
        {
            error = "La clé de règle est requise.";
            return false;
        }
        var values = _draft.Rules.ToArray();
        values[index] = values[index] with
        {
            Key = _ruleKey.Text.Trim(),
            Kind = _ruleKind.GetItemText(_ruleKind.Selected),
            JsonSchema = _ruleSchema.Text,
            DefaultsJson = _ruleDefaults.Text,
            Required = _ruleRequired.ButtonPressed,
        };
        _draft = _draft with { Rules = values };
        if (index < _ruleChoice.ItemCount) _ruleChoice.SetItemText(index, Human(values[index].Key));
        return true;
    }

    private void AddRule()
    {
        if (_draft is null) return;
        if (!SaveRule(out var error)) { SetStatus(error, true); return; }
        var index = _draft.Rules.Count + 1;
        var key = UniqueKey(_draft.Rules.Select(item => item.Key), $"rule.{index}");
        var item = new StudioUx6RRuleDraft(Guid.NewGuid(), key, "Class", "{\"type\":\"object\"}",
            "{}", false, Ux6RProvenance());
        _draft = _draft with { Rules = _draft.Rules.Append(item).ToArray() };
        _updating = true;
        RebuildRules(_draft.Rules.Count - 1);
        _updating = false;
        InvalidatePreview();
    }

    private void RemoveRule()
    {
        if (_draft is null || _draft.Rules.Count == 0) return;
        var index = _currentRuleIndex;
        if (index < 0 || index >= _draft.Rules.Count) return;
        _draft = _draft with { Rules = _draft.Rules.Where((_, i) => i != index).ToArray() };
        _updating = true;
        RebuildRules(Math.Max(0, index - 1));
        _updating = false;
        InvalidatePreview();
    }

    private void RebuildUiDocuments(int selected = 0)
    {
        _uiChoice.Clear();
        if (_draft is null || _draft.UiDocuments.Count == 0)
        {
            _currentUiIndex = -1;
            _uiChoice.AddItem("Aucune interface · créez-en une explicitement");
            ClearUiEditor();
            return;
        }
        foreach (var item in _draft.UiDocuments) _uiChoice.AddItem(Human(item.Title));
        _uiChoice.Select(Math.Clamp(selected, 0, _draft.UiDocuments.Count - 1));
        LoadUi(_uiChoice.Selected);
    }

    private void SelectUi(int index)
    {
        if (_updating || _draft is null || index < 0 || index >= _draft.UiDocuments.Count) return;
        if (index == _currentUiIndex) return;
        if (_currentUiIndex >= 0 && !SaveUi(out var error))
        {
            _updating = true;
            _uiChoice.Select(_currentUiIndex);
            _updating = false;
            SetStatus(error, true);
            return;
        }
        LoadUi(index);
    }

    private void LoadUi(int index)
    {
        if (_draft is null || index < 0 || index >= _draft.UiDocuments.Count) return;
        var item = _draft.UiDocuments[index];
        var wasUpdating = _updating;
        _updating = true;
        _uiKey.Text = item.Key;
        _uiTitle.Text = item.Title;
        _uiTheme.Text = item.ThemeKey;
        _uiRequired.ButtonPressed = item.Required;
        _widgets.Text = string.Join('\n', item.Widgets.Select(widget => string.Join(" | ",
            widget.Key, widget.Kind, widget.ParentKey, widget.Text.Replace('\n', ' '), widget.BindingKey,
            widget.Interactive.ToString().ToLowerInvariant(), widget.FocusOrder.ToString(CultureInfo.InvariantCulture),
            widget.Permission)));
        _events.Text = string.Join('\n', item.Events.Select(binding => string.Join(" | ",
            binding.WidgetKey, binding.Event, binding.Command, binding.InputContext, binding.Permission)));
        _currentUiIndex = index;
        _updating = wasUpdating;
    }

    private bool SaveUi(out string error)
    {
        error = string.Empty;
        if (_draft is null || _draft.UiDocuments.Count == 0) return true;
        var index = _currentUiIndex;
        if (index < 0 || index >= _draft.UiDocuments.Count) return true;
        var source = _draft.UiDocuments[index];
        if (!TryParseWidgets(source, _widgets.Text, out var widgets, out error) ||
            !TryParseEvents(source, _events.Text, out var events, out error)) return false;
        var values = _draft.UiDocuments.ToArray();
        values[index] = source with
        {
            Key = _uiKey.Text.Trim(),
            Title = _uiTitle.Text.Trim(),
            ThemeKey = _uiTheme.Text.Trim(),
            Required = _uiRequired.ButtonPressed,
            Widgets = widgets,
            Events = events,
        };
        _draft = _draft with { UiDocuments = values };
        if (index < _uiChoice.ItemCount) _uiChoice.SetItemText(index, Human(values[index].Title));
        return true;
    }

    private void AddUi()
    {
        if (_draft is null) return;
        if (!SaveUi(out var error)) { SetStatus(error, true); return; }
        var index = _draft.UiDocuments.Count + 1;
        var key = UniqueKey(_draft.UiDocuments.Select(item => item.Key), $"runtime.ui.{index}");
        var id = Guid.NewGuid();
        var panel = new StudioUx6RWidgetDraft(StudioSessionController.StableIdUx6R(id, "widget:root"),
            "root", "Panel", string.Empty, string.Empty, string.Empty, false, 0, "player");
        var item = new StudioUx6RUiDocumentDraft(id, key, $"Interface runtime {index}", "forge-dark", false,
            [panel], [], Ux6RProvenance());
        _draft = _draft with { UiDocuments = _draft.UiDocuments.Append(item).ToArray() };
        _updating = true;
        RebuildUiDocuments(_draft.UiDocuments.Count - 1);
        _updating = false;
        InvalidatePreview();
    }

    private void RemoveUi()
    {
        if (_draft is null || _draft.UiDocuments.Count == 0) return;
        var index = _currentUiIndex;
        if (index < 0 || index >= _draft.UiDocuments.Count) return;
        _draft = _draft with { UiDocuments = _draft.UiDocuments.Where((_, i) => i != index).ToArray() };
        _updating = true;
        RebuildUiDocuments(Math.Max(0, index - 1));
        _updating = false;
        InvalidatePreview();
    }

    private void RebuildJsonDocuments(int selected = 0)
    {
        _jsonChoice.Clear();
        if (_draft is null || _draft.JsonDocuments.Count == 0)
        {
            _currentJsonIndex = -1;
            _jsonChoice.AddItem("Aucun document JSON · créez-en un explicitement");
            ClearJsonEditor();
            return;
        }
        foreach (var item in _draft.JsonDocuments) _jsonChoice.AddItem(Human(item.Key));
        _jsonChoice.Select(Math.Clamp(selected, 0, _draft.JsonDocuments.Count - 1));
        LoadJson(_jsonChoice.Selected);
    }

    private void SelectJson(int index)
    {
        if (_updating || _draft is null || index < 0 || index >= _draft.JsonDocuments.Count) return;
        if (index == _currentJsonIndex) return;
        if (_currentJsonIndex >= 0 && !SaveJson(out var error))
        {
            _updating = true;
            _jsonChoice.Select(_currentJsonIndex);
            _updating = false;
            SetStatus(error, true);
            return;
        }
        LoadJson(index);
    }

    private void LoadJson(int index)
    {
        if (_draft is null || index < 0 || index >= _draft.JsonDocuments.Count) return;
        var item = _draft.JsonDocuments[index];
        var wasUpdating = _updating;
        _updating = true;
        _jsonKey.Text = item.Key;
        _jsonSchema.Text = item.JsonSchema;
        _jsonValue.Text = item.InitialValue;
        _jsonBytes.Text = item.MaxBytes.ToString(CultureInfo.InvariantCulture);
        _jsonDepth.Text = item.MaxDepth.ToString(CultureInfo.InvariantCulture);
        _jsonMutable.ButtonPressed = item.Mutable;
        _currentJsonIndex = index;
        _updating = wasUpdating;
    }

    private bool SaveJson(out string error)
    {
        error = string.Empty;
        if (_draft is null || _draft.JsonDocuments.Count == 0) return true;
        var index = _currentJsonIndex;
        if (index < 0 || index >= _draft.JsonDocuments.Count) return true;
        if (!int.TryParse(_jsonBytes.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bytes) ||
            !int.TryParse(_jsonDepth.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var depth))
        {
            error = "Les quotas JSON doivent être des nombres entiers exacts.";
            return false;
        }
        var values = _draft.JsonDocuments.ToArray();
        values[index] = values[index] with
        {
            Key = _jsonKey.Text.Trim(),
            JsonSchema = _jsonSchema.Text,
            InitialValue = _jsonValue.Text,
            MaxBytes = bytes,
            MaxDepth = depth,
            Mutable = _jsonMutable.ButtonPressed,
        };
        _draft = _draft with { JsonDocuments = values };
        if (index < _jsonChoice.ItemCount) _jsonChoice.SetItemText(index, Human(values[index].Key));
        return true;
    }

    private void AddJson()
    {
        if (_draft is null) return;
        if (!SaveJson(out var error)) { SetStatus(error, true); return; }
        var index = _draft.JsonDocuments.Count + 1;
        var key = UniqueKey(_draft.JsonDocuments.Select(item => item.Key), $"runtime.document.{index}");
        var item = new StudioUx6RJsonDocumentDraft(Guid.NewGuid(), key, "{\"type\":\"object\"}",
            "{}", 16_384, 16, true, Ux6RProvenance());
        _draft = _draft with { JsonDocuments = _draft.JsonDocuments.Append(item).ToArray() };
        _updating = true;
        RebuildJsonDocuments(_draft.JsonDocuments.Count - 1);
        _updating = false;
        InvalidatePreview();
    }

    private void RemoveJson()
    {
        if (_draft is null || _draft.JsonDocuments.Count == 0) return;
        var index = _currentJsonIndex;
        if (index < 0 || index >= _draft.JsonDocuments.Count) return;
        _draft = _draft with { JsonDocuments = _draft.JsonDocuments.Where((_, i) => i != index).ToArray() };
        _updating = true;
        RebuildJsonDocuments(Math.Max(0, index - 1));
        _updating = false;
        InvalidatePreview();
    }

    private bool SaveSnapshot(out string error)
    {
        error = string.Empty;
        if (_draft is null) return false;
        if (!int.TryParse(_snapshotReferences.Text, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var references) ||
            !int.TryParse(_snapshotInventory.Text, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var inventory) ||
            !int.TryParse(_snapshotBlackboard.Text, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var blackboard))
        {
            error = "Les quotas de sauvegarde doivent être des nombres entiers exacts.";
            return false;
        }
        var components = _snapshotComponents.Text.Split(',', StringSplitOptions.TrimEntries |
            StringSplitOptions.RemoveEmptyEntries);
        _draft = _draft with
        {
            Snapshot = _draft.Snapshot with
            {
                Components = components,
                MaxReferences = references,
                MaxInventoryItems = inventory,
                MaxBlackboardBytes = blackboard,
                RestoreReferences = _snapshotRestore.ButtonPressed,
            },
        };
        return true;
    }

    private bool ReadDraft(out StudioUx6RDraft? draft)
    {
        draft = null;
        if (_draft is null) { SetStatus("Sélectionnez explicitement le profil.", true); return false; }
        if (!SaveRule(out var error) || !SaveUi(out error) || !SaveJson(out error) || !SaveSnapshot(out error))
        {
            SetStatus(error, true);
            return false;
        }
        draft = _draft;
        return true;
    }

    private void Preview()
    {
        if (!ReadDraft(out var draft) || draft is null) return;
        _preview = _session.PreviewRuntimeDataUx6R(draft);
        RenderPreview(_preview);
        UpdateButtons();
        SetStatus(_preview.Message, !_preview.Succeeded);
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedRuntimeDataUx6R();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) { UpdateButtons(); return; }
        RuntimeDataChanged?.Invoke();
        ReloadAfterMutation();
    }

    private void Undo()
    {
        var result = _session.UndoRuntimeDataUx6R();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) { RuntimeDataChanged?.Invoke(); ReloadAfterMutation(); }
    }

    private void Redo()
    {
        var result = _session.RedoRuntimeDataUx6R();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) { RuntimeDataChanged?.Invoke(); ReloadAfterMutation(); }
    }

    private void Save()
    {
        var result = _session.SaveRuntimeDataUx6R();
        SetStatus(result.Message, !result.Succeeded);
    }

    private void ReloadAfterMutation()
    {
        _draft = _session.CreateRuntimeDataDraftUx6R(true);
        _preview = null;
        if (_draft is not null) PopulateDraft();
        UpdateButtons();
    }

    private void RenderPreview(StudioUx6RPreview preview)
    {
        ClearChildren(_runtimeResults);
        _runtimeResults.AddChild(StudioStyle.Label("APERÇU DES SERVICES · copie en mémoire", 9,
            StudioStyle.Accent));
        if (preview.RuntimePreview.Count > 0)
            _runtimeResults.AddChild(StudioStyle.Label(string.Join(" · ", preview.RuntimePreview), 9,
                StudioStyle.Text, wrap: true));
        foreach (var diagnostic in preview.Diagnostics)
            _runtimeResults.AddChild(StudioStyle.Label($"{Human(diagnostic.Code)} · {diagnostic.Message}",
                9, StudioStyle.Danger, wrap: true));
        _review.Text = preview.Succeeded
            ? $"{preview.PatchSummary} · {string.Join(' ', preview.Diff)}"
            : "Aucune approbation · corrigez les diagnostics avant application.";
    }

    private void InvalidatePreview()
    {
        _preview = null;
        _session.CancelRuntimeDataApprovalUx6R();
        _review.Text = "Brouillon modifié · relancez l’aperçu sec canonique.";
        UpdateButtons();
    }

    private void OnDraftEditorChanged()
    {
        if (_updating || _draft is null) return;
        InvalidatePreview();
    }

    private void WireDraftInvalidation()
    {
        foreach (var field in new[]
                 {
                     _ruleKey, _uiKey, _uiTitle, _uiTheme, _jsonKey, _jsonBytes, _jsonDepth,
                     _snapshotComponents, _snapshotReferences, _snapshotInventory, _snapshotBlackboard,
                 })
            field.TextChanged += _ => OnDraftEditorChanged();
        foreach (var field in new[] { _ruleSchema, _ruleDefaults, _widgets, _events, _jsonSchema, _jsonValue })
            field.TextChanged += OnDraftEditorChanged;
        foreach (var field in new[] { _ruleRequired, _uiRequired, _jsonMutable, _snapshotRestore })
            field.Toggled += _ => OnDraftEditorChanged();
        _ruleKind.ItemSelected += _ => OnDraftEditorChanged();
    }

    private bool ExerciseMultiRecordDraftPreservation()
    {
        if (_draft is not { Rules.Count: > 0, UiDocuments.Count: > 0, JsonDocuments.Count: > 0 })
            return false;

        var ruleId = _draft.Rules[0].Id;
        const string ruleDefaults = "{\"stance\":\"guarded\",\"bonus\":7}";
        _ruleDefaults.Text = ruleDefaults;
        AddRule();
        var rulePreserved = _draft.Rules.Count > 1 &&
                            _draft.Rules.Single(item => item.Id == ruleId).DefaultsJson == ruleDefaults;
        _ruleChoice.Select(0);
        SelectRule(0);
        rulePreserved &= _ruleDefaults.Text == ruleDefaults;

        var uiId = _draft.UiDocuments[0].Id;
        const string uiTitle = "HUD multi-sélection préservé";
        _uiTitle.Text = uiTitle;
        AddUi();
        var uiPreserved = _draft.UiDocuments.Count > 1 &&
                          _draft.UiDocuments.Single(item => item.Id == uiId).Title == uiTitle;
        _uiChoice.Select(0);
        SelectUi(0);
        uiPreserved &= _uiTitle.Text == uiTitle;

        var jsonId = _draft.JsonDocuments[0].Id;
        const string jsonValue = "{\"status\":\"multi-selection\",\"count\":4}";
        _jsonValue.Text = jsonValue;
        AddJson();
        var jsonPreserved = _draft.JsonDocuments.Count > 1 &&
                            _draft.JsonDocuments.Single(item => item.Id == jsonId).InitialValue == jsonValue;
        _jsonChoice.Select(0);
        SelectJson(0);
        jsonPreserved &= _jsonValue.Text == jsonValue;

        return rulePreserved && uiPreserved && jsonPreserved;
    }

    private void UpdateButtons()
    {
        _apply.Disabled = _preview is not { Succeeded: true, CanApply: true };
        _undo.Disabled = !(_session.History?.CanUndo ?? false);
        _redo.Disabled = !(_session.History?.CanRedo ?? false);
    }

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = message;
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Muted;
        StatusChanged?.Invoke(message, error);
    }

    private void ResetControls(bool keepReleaseState = false)
    {
        _draft = null;
        _preview = null;
        _explicitSelection = false;
        _currentRuleIndex = -1;
        _currentUiIndex = -1;
        _currentJsonIndex = -1;
        _updating = true;
        _profileChoice.Clear();
        _ruleChoice.Clear();
        _uiChoice.Clear();
        _jsonChoice.Clear();
        ClearEditors();
        ClearChildren(_runtimeResults);
        _runtimeResults.AddChild(StudioStyle.Label("APERÇU DES SERVICES · aucun aperçu sec canonique", 9,
            StudioStyle.Accent));
        _review.Text = "Aucune modification approuvée.";
        _updating = false;
        if (!keepReleaseState) _evidenceReleased = _evidenceSession is null;
        UpdateButtons();
    }

    private void ClearEditors()
    {
        ClearRuleEditor();
        ClearUiEditor();
        ClearJsonEditor();
        _snapshotComponents.Text = string.Empty;
        _snapshotReferences.Text = string.Empty;
        _snapshotInventory.Text = string.Empty;
        _snapshotBlackboard.Text = string.Empty;
        _snapshotRestore.ButtonPressed = false;
    }

    private void ClearRuleEditor()
    {
        _ruleKey.Text = string.Empty;
        _ruleSchema.Text = string.Empty;
        _ruleDefaults.Text = string.Empty;
        _ruleRequired.ButtonPressed = false;
    }

    private void ClearUiEditor()
    {
        _uiKey.Text = string.Empty;
        _uiTitle.Text = string.Empty;
        _uiTheme.Text = string.Empty;
        _uiRequired.ButtonPressed = false;
        _widgets.Text = string.Empty;
        _events.Text = string.Empty;
    }

    private void ClearJsonEditor()
    {
        _jsonKey.Text = string.Empty;
        _jsonSchema.Text = string.Empty;
        _jsonValue.Text = string.Empty;
        _jsonBytes.Text = string.Empty;
        _jsonDepth.Text = string.Empty;
        _jsonMutable.ButtonPressed = false;
    }

    private SourceSnapshotUx6R CaptureSource() => new(_sourceSession.Project!, _sourceSession.History!,
        _sourceSession.History!.State.Revision, _sourceSession.History.Journal.ToArray(),
        _sourceSession.IsDirty, _sourceSession.History.CanUndo, _sourceSession.History.CanRedo,
        _sourceSession.ActiveAreaId, _sourceSession.CaptureRuntimeDataApprovalUx6R());

    private bool SourceMatches()
    {
        var snapshot = _sourceSnapshot;
        var history = _sourceSession.History;
        return snapshot is null ||
               ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history is not null &&
               history.State.Revision == snapshot.Revision && history.Journal.SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.IsDirty && history.CanUndo == snapshot.CanUndo &&
               history.CanRedo == snapshot.CanRedo && _sourceSession.ActiveAreaId == snapshot.AreaId &&
               _sourceSession.CaptureRuntimeDataApprovalUx6R() == snapshot.Approval;
    }

    private void ReleaseEvidence()
    {
        _evidenceSession?.Close();
        _evidenceSession = null;
        var released = true;
        if (!string.IsNullOrWhiteSpace(_evidenceRoot))
        {
            try
            {
                if (Directory.Exists(_evidenceRoot)) Directory.Delete(_evidenceRoot, recursive: true);
                released = !Directory.Exists(_evidenceRoot);
            }
            catch { released = false; }
        }
        _evidenceRoot = null;
        _session = _sourceSession;
        _evidenceReleased = released;
    }

    private bool TryParseWidgets(StudioUx6RUiDocumentDraft document, string text,
        out IReadOnlyList<StudioUx6RWidgetDraft> widgets, out string error)
    {
        var values = new List<StudioUx6RWidgetDraft>();
        error = string.Empty;
        foreach (var line in Lines(text))
        {
            var cells = line.Split('|').Select(item => item.Trim()).ToArray();
            if (cells.Length != 8 || !bool.TryParse(cells[5], out var interactive) ||
                !int.TryParse(cells[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out var focus))
            {
                widgets = [];
                error = "Widget attendu : clé | type | parent | texte | binding | interactif | ordre | permission.";
                return false;
            }
            var existing = document.Widgets.FirstOrDefault(item =>
                string.Equals(item.Key, cells[0], StringComparison.OrdinalIgnoreCase));
            values.Add(new StudioUx6RWidgetDraft(existing?.Id ??
                StudioSessionController.StableIdUx6R(document.Id, "widget:" + cells[0]), cells[0], cells[1],
                cells[2], cells[3], cells[4], interactive, focus, cells[7]));
        }
        widgets = values;
        return true;
    }

    private bool TryParseEvents(StudioUx6RUiDocumentDraft document, string text,
        out IReadOnlyList<StudioUx6REventDraft> events, out string error)
    {
        var values = new List<StudioUx6REventDraft>();
        error = string.Empty;
        foreach (var line in Lines(text))
        {
            var cells = line.Split('|').Select(item => item.Trim()).ToArray();
            if (cells.Length != 5)
            {
                events = [];
                error = "Événement attendu : widget | événement | commande | contexte | permission.";
                return false;
            }
            var existing = document.Events.FirstOrDefault(item =>
                string.Equals(item.WidgetKey, cells[0], StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Event, cells[1], StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Command, cells[2], StringComparison.Ordinal));
            values.Add(new StudioUx6REventDraft(existing?.Id ?? StudioSessionController.StableIdUx6R(document.Id,
                $"event:{cells[0]}:{cells[1]}:{cells[2]}"), cells[0], cells[1], cells[2], cells[3], cells[4]));
        }
        events = values;
        return true;
    }

    private static IEnumerable<string> Lines(string text) => text.Replace("\r", string.Empty,
        StringComparison.Ordinal).Split('\n').Select(item => item.Trim()).Where(item => item.Length > 0);

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
        CustomMinimumSize = new Vector2(120, 34),
    };

    private static TextEdit Multi(float height) => new()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        CustomMinimumSize = new Vector2(0, height),
        WrapMode = TextEdit.LineWrappingMode.Boundary,
        ScrollFitContentHeight = false,
    };

    private static OptionButton EnumChoice(IEnumerable<string> values)
    {
        var choice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var value in values) choice.AddItem(value);
        return choice;
    }

    private static void SelectEnum(OptionButton choice, string value)
    {
        for (var index = 0; index < choice.ItemCount; index++)
            if (string.Equals(choice.GetItemText(index), value, StringComparison.OrdinalIgnoreCase))
            {
                choice.Select(index);
                return;
            }
        choice.Select(0);
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
        var width = Math.Clamp(viewport.X - 64, 1080, 1740);
        var height = Math.Clamp(viewport.Y - 44, 760, 1010);
        _dialog.Position = new Vector2((viewport.X - width) / 2f, (viewport.Y - height) / 2f);
        _dialog.Size = new Vector2(width, height);
    }

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
                case OptionButton option when option.Visible:
                    for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
                    break;
            }
            foreach (var text in DescendantText(child)) yield return text;
        }
    }

    private static string UniqueKey(IEnumerable<string> values, string candidate)
    {
        var used = values.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!used.Contains(candidate)) return candidate;
        for (var index = 2; ; index++)
        {
            var next = candidate + "." + index.ToString(CultureInfo.InvariantCulture);
            if (!used.Contains(next)) return next;
        }
    }

    private static string Human(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "aucun";
        if (Guid.TryParse(value, out _)) return "identité protégée";
        return string.Join(' ', value.Trim().Replace('_', ' ').Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool ContainsUuid(string value) => Regex.IsMatch(value,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b");

    private static string Ux6RProvenance() => "studio:ux6-r:runtime-data-ui";

    private sealed record SourceSnapshotUx6R(
        Forge.Domain.Projects.ProjectDefinition Project,
        Forge.Application.Commands.ProjectCommandHistory History,
        Forge.Application.Projects.ProjectRevision Revision,
        IReadOnlyList<Forge.Application.Commands.CommandHistoryJournalEntry> Journal,
        bool IsDirty,
        bool CanUndo,
        bool CanRedo,
        Forge.Domain.Areas.AreaId? AreaId,
        StudioUx6RApprovalSnapshot Approval);
}
