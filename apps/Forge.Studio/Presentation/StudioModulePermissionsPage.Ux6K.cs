using System.Text.RegularExpressions;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Scripting;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioModulePermissionsPageUx6K : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx6KSourceSnapshot? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _sourceChoice;
    private readonly Label _selection;
    private readonly CodeEdit _source;
    private readonly LineEdit _apiVersion;
    private readonly CheckBox _denyByDefault;
    private readonly CheckBox _serverEnforced;
    private readonly CheckBox _requireSignature;
    private readonly CheckBox _allowUnsignedDevelopment;
    private readonly CheckBox _requiredAtRuntime;
    private readonly TabContainer _tabs;
    private readonly Dictionary<string, CheckBox> _moduleCapabilities = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CheckBox> _kitCapabilities = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CheckBox> _bindingCapabilities = new(StringComparer.Ordinal);

    private readonly OptionButton _kitChoice;
    private readonly LineEdit _kitId;
    private readonly Button _kitAdd;
    private readonly Button _kitRemove;
    private readonly Button _kitUp;
    private readonly Button _kitDown;

    private readonly OptionButton _bindingChoice;
    private readonly LineEdit _bindingEvent;
    private readonly LineEdit _bindingHandler;
    private readonly OptionButton _bindingKit;
    private readonly CheckBox _bindingEnabled;
    private readonly Button _bindingAdd;
    private readonly Button _bindingRemove;
    private readonly Button _bindingUp;
    private readonly Button _bindingDown;

    private readonly VBoxContainer _diagnosticRows;
    private readonly Label _policyReview;
    private readonly Label _runtimeReview;
    private readonly Label _console;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _simulate;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    private IReadOnlyList<StudioUx6FSourceChoice> _sourceChoices = [];
    private IReadOnlyList<StudioUx6KCapabilityChoice> _capabilities = [];
    private StudioUx6FDraft? _sourceDraft;
    private StudioUx6KPolicyDraft? _policyDraft;
    private List<ForgeScriptKitCapabilityGrant> _kits = [];
    private List<ForgeScriptPermissionEventBinding> _bindings = [];
    private StudioUx6KPreview? _previewResult;
    private StudioUx6KSimulation? _simulationResult;
    private int _selectedKit = -1;
    private int _selectedBinding = -1;
    private bool _explicitSource;
    private bool _explicitKit;
    private bool _explicitBinding;
    private bool _updating;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? PolicyChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _explicitSource && _explicitKit && _explicitBinding &&
                                          _sourceDraft is not null && _policyDraft is not null;
    internal bool HumanCapabilitiesReady => _capabilities.Count == ForgeScriptCapabilityManifest.Capabilities.Count &&
                                             _capabilities.Count == 6 &&
                                             _capabilities.All(item => !string.IsNullOrWhiteSpace(item.Label) &&
                                                                       !ContainsUuid(item.Label));
    internal bool PreviewReady => _previewResult is { Succeeded: true, CanApply: true, CanonicalDryRun: true };
    internal bool SimulationReady => _simulationResult is
    {
        Succeeded: true,
        TrustedServerExecuted: true,
        CapabilityIntersection: true,
        UndeclaredCapabilityDenied: true,
        UntrustedSignatureDenied: true,
        DevelopmentPolicyHonored: true,
        ClientAuthorityDenied: true,
        ModuleMismatchDenied: true,
        MissingOptionalIgnored: true,
        MissingRequiredDiagnosed: true,
        QuotaAbuseBlocked: true,
        RuntimeRecovered: true,
        CanonicalSourceCompiled: true,
    };
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                           !ReferenceEquals(_session, _sourceSession);
    internal bool EvidenceSourceUntouched => SourceMatches();
    internal bool EvidenceResourcesReleased => _evidenceReleased && _evidenceSession is null &&
                                                _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantText(this));
            return text.Contains("Permissions par module", StringComparison.Ordinal) &&
                   text.Contains("Allowlist du module", StringComparison.Ordinal) &&
                   text.Contains("Kits de capacités", StringComparison.Ordinal) &&
                   text.Contains("Liaisons d’événements", StringComparison.Ordinal) &&
                   text.Contains("Aperçu sec canonique", StringComparison.Ordinal) &&
                   text.Contains("Serveur autoritaire", StringComparison.Ordinal) &&
                   !ContainsUuid(text);
        }
    }

    internal StudioModulePermissionsPageUx6K(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.008f, .014f, .023f, .985f);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _dialog = StudioStyle.Panel("Ux6KModulePermissions", StudioStyle.Surface, 12, 1);
        _dialog.ClipContents = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(14, 12, 14, 12);
        _dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 7);
        margin.AddChild(root);

        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        headingText.AddChild(StudioStyle.Label("ForgeScript · Permissions par module R015", 19));
        headingText.AddChild(StudioStyle.Label(
            "Allowlist du module · Kits de capacités · Liaisons d’événements · Serveur autoritaire · Aperçu sec canonique · aucun UUID ni pointeur exposé.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        root.AddChild(heading);

        var sourceRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _sourceChoice = StudioUx2EnvBControls.Options();
        _sourceChoice.CustomMinimumSize = new Vector2(560, 34);
        _sourceChoice.ItemSelected += SelectSource;
        sourceRow.AddChild(Field("Source ForgeScript · sélection explicite", _sourceChoice, 560));
        var reload = StudioStyle.Button("Recharger manifeste et source", compact: true);
        reload.CustomMinimumSize = new Vector2(220, 34);
        reload.Pressed += Reload;
        sourceRow.AddChild(reload);
        root.AddChild(sourceRow);
        _selection = StudioStyle.Label(
            "Choisissez une source canonique puis un kit et une liaison. Le manifeste reste séparé du projet.",
            10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        var body = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 500),
            CustomMaximumSize = new Vector2(-1, 720),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            PropagateMaximumSize = true,
        };
        body.AddThemeConstantOverride("separation", 9);
        root.AddChild(body);

        var left = PanelColumn(body, 690, 1.2f);
        left.AddChild(StudioStyle.Label("SOURCE REVUE · COMPILATION EN SANDBOX JETABLE", 10,
            StudioStyle.Accent));
        _source = new CodeEdit
        {
            Editable = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 165),
            DrawTabs = true,
            GuttersDrawLineNumbers = true,
        };
        left.AddChild(_source);
        left.AddChild(StudioStyle.Label("ALLOWLIST DU MODULE · SIX FAMILLES CANONIQUES", 10,
            StudioStyle.Accent));
        var moduleGrid = new GridContainer
        {
            Columns = 2,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        left.AddChild(moduleGrid);
        foreach (var capability in ForgeScriptCapabilityManifest.Capabilities
                     .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            var cell = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var check = new CheckBox { Text = capability.DisplayName };
            check.Toggled += _ => InvalidatePolicy();
            _moduleCapabilities[capability.Id] = check;
            cell.AddChild(check);
            cell.AddChild(StudioStyle.Label(
                $"{HumanAuthority(capability.Authority)} · {capability.Quota} · {string.Join(", ", capability.ApiNames)}",
                8, StudioStyle.Muted, wrap: true));
            moduleGrid.AddChild(cell);
        }
        left.AddChild(StudioStyle.Label("DIAGNOSTICS CLIQUABLES", 9, StudioStyle.Accent));
        var diagnosticScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 95),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _diagnosticRows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        diagnosticScroll.AddChild(_diagnosticRows);
        left.AddChild(diagnosticScroll);

        var right = PanelColumn(body, 650, 1f);
        right.AddChild(StudioStyle.Label("MANIFESTE R015 · AUTORITÉ CANONIQUE VERSIONNÉE", 10,
            StudioStyle.Accent));
        var tabsHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 375),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        right.AddChild(tabsHost);
        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        tabsHost.AddChild(_tabs);

        var modulePage = TabPage(_tabs, "Module");
        modulePage.AddChild(StudioStyle.Label("Politique globale du module", 12, StudioStyle.Accent));
        _denyByDefault = Check("Refuser toute capacité non déclarée", disabled: true);
        _serverEnforced = Check("Application imposée par le serveur", disabled: true);
        _requireSignature = Check("Signature fiable requise pour la distribution");
        _allowUnsignedDevelopment = Check("Autoriser une signature absente en développement explicite");
        _requiredAtRuntime = Check("Manifeste obligatoire au runtime");
        modulePage.AddChild(_denyByDefault);
        modulePage.AddChild(_serverEnforced);
        modulePage.AddChild(_requireSignature);
        modulePage.AddChild(_allowUnsignedDevelopment);
        modulePage.AddChild(_requiredAtRuntime);
        _apiVersion = StudioUx2EnvBControls.Text("1.0.0");
        modulePage.AddChild(Field("Version d’API ForgeScript", _apiVersion, 280));
        modulePage.AddChild(StudioStyle.Label(
            "Deny-by-default et serveur autoritaire sont protégés. Une distribution non signée est refusée hors mode développement explicitement authoré.",
            9, StudioStyle.Muted, wrap: true));

        var kitPage = TabPage(_tabs, "Kits");
        kitPage.AddChild(StudioStyle.Label("Kits de capacités · sous-ensembles de l’allowlist", 12,
            StudioStyle.Accent));
        _kitChoice = StudioUx2EnvBControls.Options();
        _kitChoice.ItemSelected += SelectKit;
        kitPage.AddChild(Field("Kit sélectionné", _kitChoice, 400));
        _kitId = StudioUx2EnvBControls.Text(string.Empty);
        kitPage.AddChild(Field("Nom canonique humain du kit", _kitId, 400));
        var kitGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var capability in ForgeScriptCapabilityManifest.Capabilities
                     .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            var check = new CheckBox { Text = capability.DisplayName };
            check.Toggled += _ => InvalidatePolicy();
            _kitCapabilities[capability.Id] = check;
            kitGrid.AddChild(check);
        }
        kitPage.AddChild(kitGrid);
        var kitActions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _kitAdd = ActionButton("Ajouter", AddKit, 104);
        _kitRemove = ActionButton("Retirer", RemoveKit, 104);
        _kitUp = ActionButton("Monter", () => MoveKit(-1), 104);
        _kitDown = ActionButton("Descendre", () => MoveKit(1), 104);
        kitActions.AddChild(_kitAdd);
        kitActions.AddChild(_kitRemove);
        kitActions.AddChild(_kitUp);
        kitActions.AddChild(_kitDown);
        kitPage.AddChild(kitActions);

        var bindingPage = TabPage(_tabs, "Liaisons");
        bindingPage.AddChild(StudioStyle.Label("Liaisons d’événements · intersection module / kit / événement",
            12, StudioStyle.Accent));
        _bindingChoice = StudioUx2EnvBControls.Options();
        _bindingChoice.ItemSelected += SelectBinding;
        bindingPage.AddChild(Field("Liaison sélectionnée", _bindingChoice, 440));
        _bindingEvent = StudioUx2EnvBControls.Text(string.Empty);
        _bindingHandler = StudioUx2EnvBControls.Text(string.Empty);
        bindingPage.AddChild(Pair("Événement", _bindingEvent, "Handler", _bindingHandler));
        _bindingKit = StudioUx2EnvBControls.Options();
        bindingPage.AddChild(Field("Kit accordé", _bindingKit, 360));
        _bindingEnabled = Check("Liaison active · Serveur autoritaire");
        bindingPage.AddChild(_bindingEnabled);
        var bindingGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var capability in ForgeScriptCapabilityManifest.Capabilities
                     .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            var check = new CheckBox { Text = capability.DisplayName };
            check.Toggled += _ => InvalidatePolicy();
            _bindingCapabilities[capability.Id] = check;
            bindingGrid.AddChild(check);
        }
        bindingPage.AddChild(bindingGrid);
        var bindingActions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _bindingAdd = ActionButton("Ajouter", AddBinding, 104);
        _bindingRemove = ActionButton("Retirer", RemoveBinding, 104);
        _bindingUp = ActionButton("Monter", () => MoveBinding(-1), 104);
        _bindingDown = ActionButton("Descendre", () => MoveBinding(1), 104);
        bindingActions.AddChild(_bindingAdd);
        bindingActions.AddChild(_bindingRemove);
        bindingActions.AddChild(_bindingUp);
        bindingActions.AddChild(_bindingDown);
        bindingPage.AddChild(bindingActions);

        var review = StudioStyle.Panel("Ux6KReview", StudioStyle.SurfaceRaised, 8, 1);
        review.CustomMinimumSize = new Vector2(0, 125);
        right.AddChild(review);
        var reviewMargin = StudioStyle.Margin(9, 8, 9, 8);
        review.AddChild(reviewMargin);
        var reviewRows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        reviewMargin.AddChild(reviewRows);
        _policyReview = StudioStyle.Label("Aucun aperçu sec canonique.", 9, StudioStyle.Muted, wrap: true);
        _runtimeReview = StudioStyle.Label("Aucune simulation d’autorité et sandbox.", 9,
            StudioStyle.Muted, wrap: true);
        _console = StudioStyle.Label("Console R015 prête.", 8, StudioStyle.Muted, wrap: true);
        reviewRows.AddChild(_policyReview);
        reviewRows.AddChild(_runtimeReview);
        reviewRows.AddChild(_console);

        _status = StudioStyle.Label("Permissions par module prêtes.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        var footer = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footer.AddThemeConstantOverride("separation", 8);
        _preview = ActionButton("Prévisualiser", Preview, 128);
        _simulate = ActionButton("Tester autorité + sandbox", Simulate, 206);
        _apply = ActionButton("Appliquer", Apply, 108);
        _undo = ActionButton("Annuler", Undo, 104);
        _redo = ActionButton("Rétablir", Redo, 104);
        _save = ActionButton("Enregistrer", Save, 120);
        footer.AddChild(_preview);
        footer.AddChild(_simulate);
        footer.AddChild(_apply);
        footer.AddChild(_undo);
        footer.AddChild(_redo);
        footer.AddChild(_save);
        root.AddChild(footer);

        HookEditors();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && IsInsideTree()) UpdateBounds();
    }

    internal void Open() => OpenBound(_sourceSession, evidence: false, cancelApproval: false);

    internal void RestoreSourceSurfaceAfterSmoke() =>
        OpenBound(_sourceSession, evidence: false, cancelApproval: false);

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null) return;
        if (Visible) _session.CancelUx6KApproval();
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidence();
        _session = _sourceSession;
        ResetControls();
    }

    internal void CloseEvidence()
    {
        Visible = false;
        ReleaseEvidence();
        _session = _sourceSession;
        ResetControls();
    }

    internal bool PrepareEvidence(string mode)
    {
        if (!mode.StartsWith("ux6-k", StringComparison.OrdinalIgnoreCase))
            return FailEvidence("Mode de preuve UX6-K inconnu.");
        ReleaseEvidence();
        _sourceSnapshot = CaptureSource();
        if (_sourceSnapshot is null || _sourceSession.Project is null)
            return FailEvidence("Le module source n’est pas ouvert.");
        _evidenceRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux6-k-ui",
            Guid.NewGuid().ToString("N"));
        if (!ProjectSession.Create(_evidenceRoot, _sourceSession.Project).Succeeded)
            return FailEvidence("Impossible de créer la copie temporaire R015.");
        new ForgeScriptSourceStore(_evidenceRoot).Save(StudioSessionController.EvidenceDocumentUx6F());
        _evidenceSession = new StudioSessionController();
        if (!_evidenceSession.Open(_evidenceRoot).Succeeded)
            return FailEvidence("Impossible d’ouvrir la copie temporaire R015.");
        _session = _evidenceSession;
        OpenBound(_session, evidence: true);
        if (_sourceChoice.ItemCount < 2 || _kits.Count == 0 || _bindings.Count == 0)
            return FailEvidence("La copie temporaire ne fournit pas les sélections R015.");
        _sourceChoice.Select(1);
        SelectSource(1);
        _kitChoice.Select(1);
        SelectKit(1);
        _bindingChoice.Select(1);
        SelectBinding(1);
        _allowUnsignedDevelopment.ButtonPressed = false;
        InvalidatePolicy();
        Preview();
        Simulate();
        _tabs.CurrentTab = mode.Contains("bindings", StringComparison.OrdinalIgnoreCase)
            ? 2
            : mode.Contains("kits", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        var isolated = UsesIsolatedEvidence;
        var explicitSelection = HasExplicitSelection;
        var humanCapabilities = HumanCapabilitiesReady;
        var preview = PreviewReady;
        var simulation = SimulationReady;
        var humanSurface = ShowsHumanSurface;
        var sourceUntouched = SourceMatches();
        var ready = isolated && explicitSelection && humanCapabilities && preview && simulation && humanSurface &&
                    sourceUntouched;
        if (!ready)
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_K_UI_FAILED " +
                        $"isolated={isolated.ToString().ToLowerInvariant()} " +
                        $"explicit={explicitSelection.ToString().ToLowerInvariant()} " +
                        $"capabilities={humanCapabilities.ToString().ToLowerInvariant()} " +
                        $"preview={preview.ToString().ToLowerInvariant()} " +
                        $"simulation={simulation.ToString().ToLowerInvariant()} " +
                        $"human={humanSurface.ToString().ToLowerInvariant()} " +
                        $"source={sourceUntouched.ToString().ToLowerInvariant()}");
        return ready || FailEvidence("La surface R015 n’a pas satisfait tous les invariants humains.");
    }

    internal bool RunUiTransactionEvidence()
    {
        var ready = PrepareEvidence("ux6-k-script-module-permissions");
        try
        {
            if (!ready || _evidenceSession is null || !ReferenceEquals(_session, _evidenceSession) ||
                !PreviewReady || !SimulationReady)
                return false;
            var draft = ReadPolicy();
            if (draft is null) return false;
            var store = new ForgeScriptModulePermissionStore(_evidenceRoot!);
            var applied = _session.ApplyApprovedModulePermissionUx6K(draft);
            var appliedPolicy = store.Load();
            var undone = _session.UndoModulePermissionUx6K();
            var undoPolicy = store.Load();
            var redone = _session.RedoModulePermissionUx6K();
            var redoPolicy = store.Load();
            var saved = _session.SaveModulePermissionUx6K();
            var reopened = new StudioSessionController();
            var persisted = reopened.Open(_evidenceRoot!).Succeeded &&
                            reopened.CurrentModulePermissionUx6K() is { } reopenedDraft &&
                            PolicyContentsEqual(reopenedDraft, draft) &&
                            reopened.Project is not null && _sourceSnapshot is not null &&
                            string.Equals(ProjectJsonSerializer.Serialize(reopened.Project),
                                ProjectJsonSerializer.Serialize(_sourceSnapshot.Project), StringComparison.Ordinal);
            reopened.Close();
            return applied.Succeeded && undone.Succeeded && redone.Succeeded && saved.Succeeded && persisted &&
                   PolicyContentsEqual(appliedPolicy, draft) && !PolicyContentsEqual(undoPolicy, draft) &&
                   PolicyContentsEqual(redoPolicy, draft) && SourceMatches();
        }
        finally
        {
            CloseEvidence();
        }
    }

    private void OpenBound(StudioSessionController session, bool evidence, bool cancelApproval = true)
    {
        _session = session;
        if (cancelApproval) _session.CancelUx6KApproval();
        Visible = true;
        ResetControls();
        _capabilities = _session.ModulePermissionCapabilitiesUx6K();
        RefreshSources();
        ReloadPolicy();
        _selection.Text = evidence
            ? "COPIE TEMPORAIRE DU MODULE RÉEL · projet, historique, manifeste et approbations source strictement inchangés."
            : "Choisissez une source canonique, un kit et une liaison. Les UUID restent protégés.";
        UpdateBounds();
    }

    private void RefreshSources()
    {
        _sourceChoices = _session.ScriptSourceChoicesUx6F()
            .Where(choice => choice.Kind == StudioUx6FSourceKind.Canonical)
            .ToArray();
        _updating = true;
        _sourceChoice.Clear();
        _sourceChoice.AddItem("Choisir une source canonique…", 0);
        foreach (var choice in _sourceChoices)
            _sourceChoice.AddItem(ProtectTechnicalIdentifiers($"{choice.Label} · {choice.Detail}"));
        _sourceChoice.Select(0);
        _updating = false;
        _sourceDraft = null;
        _source.Text = _sourceChoices.Count == 0
            ? "// Aucune source canonique. Créez-la d’abord dans Fondation ForgeScript."
            : "// Sélectionnez explicitement une source canonique.";
        _explicitSource = false;
        Invalidate();
    }

    private void SelectSource(long selected)
    {
        if (_updating || selected <= 0 || selected > _sourceChoices.Count) return;
        var choice = _sourceChoices[(int)selected - 1];
        if (!_session.TryOpenCanonicalSourceUx6F(choice.RelativePath, out var draft, out var message) ||
            draft is null)
        {
            SetStatus(message, error: true);
            return;
        }
        _sourceDraft = draft;
        _source.Text = draft.Source;
        _explicitSource = true;
        InvalidateSimulation();
        SetStatus($"Source revue · {ProtectTechnicalIdentifiers(choice.Label)}.");
    }

    private void Reload()
    {
        RefreshSources();
        ReloadPolicy();
        SetStatus("Source et manifeste R015 rechargés.");
    }

    private void ReloadPolicy()
    {
        _policyDraft = _session.CurrentModulePermissionUx6K();
        if (_policyDraft is null)
        {
            SetStatus("Le manifeste R015 est absent, invalide ou appartient à un autre module.", error: true);
            UpdateButtons();
            return;
        }
        _kits = _policyDraft.KitGrants.ToList();
        _bindings = _policyDraft.EventBindings.ToList();
        PopulatePolicy(_policyDraft);
        PopulateKits(select: _kits.Count > 0 ? 0 : -1);
        PopulateBindings(select: _bindings.Count > 0 ? 0 : -1);
        _previewResult = null;
        _simulationResult = null;
        RenderDiagnostics([]);
        UpdateButtons();
    }

    private void PopulatePolicy(StudioUx6KPolicyDraft draft)
    {
        _updating = true;
        _apiVersion.Text = draft.ApiVersion;
        _denyByDefault.ButtonPressed = draft.DenyByDefault;
        _serverEnforced.ButtonPressed = draft.ServerEnforced;
        _requireSignature.ButtonPressed = draft.RequireSignedDistribution;
        _allowUnsignedDevelopment.ButtonPressed = draft.AllowUnsignedDevelopment;
        _requiredAtRuntime.ButtonPressed = draft.RequiredAtRuntime;
        foreach (var (id, check) in _moduleCapabilities)
            check.ButtonPressed = draft.AllowedCapabilities.Contains(id);
        _updating = false;
    }

    private void PopulateKits(int select)
    {
        _updating = true;
        _kitChoice.Clear();
        _kitChoice.AddItem("Choisir un kit…", 0);
        for (var index = 0; index < _kits.Count; index++)
            _kitChoice.AddItem(ProtectTechnicalIdentifiers($"Kit {index + 1} · {_kits[index].KitId}"));
        _selectedKit = select >= 0 && select < _kits.Count ? select : -1;
        _kitChoice.Select(_selectedKit + 1);
        PopulateKitEditor();
        _updating = false;
        PopulateBindingKitChoices();
        _explicitKit = _selectedKit >= 0;
    }

    private void PopulateKitEditor()
    {
        var selected = _selectedKit >= 0 && _selectedKit < _kits.Count ? _kits[_selectedKit] : null;
        _kitId.Text = selected?.KitId ?? string.Empty;
        foreach (var (id, check) in _kitCapabilities)
            check.ButtonPressed = selected?.Capabilities.Contains(id) == true;
    }

    private void SelectKit(long selected)
    {
        if (_updating) return;
        CommitKitEditor();
        _selectedKit = (int)selected - 1;
        _explicitKit = _selectedKit >= 0 && _selectedKit < _kits.Count;
        _updating = true;
        PopulateKitEditor();
        _updating = false;
        InvalidatePolicy();
    }

    private void CommitKitEditor()
    {
        if (_updating || _selectedKit < 0 || _selectedKit >= _kits.Count) return;
        var source = _kits[_selectedKit];
        _kits[_selectedKit] = source with
        {
            Provenance = "studio:ux6-k:r015",
            KitId = _kitId.Text.Trim(),
            Capabilities = SelectedCapabilities(_kitCapabilities),
        };
    }

    private void AddKit()
    {
        CommitKitEditor();
        var index = 1;
        string kitId;
        do kitId = $"kit-{index++}";
        while (_kits.Any(item => string.Equals(item.KitId, kitId, StringComparison.Ordinal)));
        var capabilities = SelectedCapabilities(_moduleCapabilities);
        if (capabilities.Count == 0 && _capabilities.Count > 0)
            capabilities = new HashSet<string>([_capabilities[0].Id], StringComparer.Ordinal);
        _kits.Add(new(Guid.NewGuid(), 1, "studio:ux6-k:r015", kitId, capabilities));
        PopulateKits(_kits.Count - 1);
        InvalidatePolicy();
        SetStatus("Kit ajouté au brouillon · prévisualisez avant application.");
    }

    private void RemoveKit()
    {
        CommitKitEditor();
        if (_selectedKit < 0 || _selectedKit >= _kits.Count) return;
        var kit = _kits[_selectedKit];
        if (_bindings.Any(item => string.Equals(item.KitId, kit.KitId, StringComparison.Ordinal)))
        {
            SetStatus("Retrait refusé · une liaison utilise encore ce kit.", error: true);
            return;
        }
        _kits.RemoveAt(_selectedKit);
        PopulateKits(Math.Min(_selectedKit, _kits.Count - 1));
        InvalidatePolicy();
    }

    private void MoveKit(int direction)
    {
        CommitKitEditor();
        var target = _selectedKit + direction;
        if (_selectedKit < 0 || target < 0 || target >= _kits.Count) return;
        (_kits[_selectedKit], _kits[target]) = (_kits[target], _kits[_selectedKit]);
        PopulateKits(target);
        InvalidatePolicy();
    }

    private void PopulateBindings(int select)
    {
        _updating = true;
        _bindingChoice.Clear();
        _bindingChoice.AddItem("Choisir une liaison…", 0);
        for (var index = 0; index < _bindings.Count; index++)
        {
            var binding = _bindings[index];
            _bindingChoice.AddItem(ProtectTechnicalIdentifiers(
                $"Liaison {index + 1} · {binding.EventName} → {binding.Handler}"));
        }
        _selectedBinding = select >= 0 && select < _bindings.Count ? select : -1;
        _bindingChoice.Select(_selectedBinding + 1);
        PopulateBindingEditor();
        _updating = false;
        _explicitBinding = _selectedBinding >= 0;
    }

    private void PopulateBindingEditor()
    {
        var selected = _selectedBinding >= 0 && _selectedBinding < _bindings.Count
            ? _bindings[_selectedBinding]
            : null;
        _bindingEvent.Text = selected?.EventName ?? string.Empty;
        _bindingHandler.Text = selected?.Handler ?? string.Empty;
        PopulateBindingKitChoices(selected?.KitId);
        _bindingEnabled.ButtonPressed = selected?.Enabled == true;
        foreach (var (id, check) in _bindingCapabilities)
            check.ButtonPressed = selected?.Capabilities.Contains(id) == true;
    }

    private void PopulateBindingKitChoices(string? selectedKit = null)
    {
        var current = selectedKit;
        if (current is null && _selectedBinding >= 0 && _selectedBinding < _bindings.Count)
            current = _bindings[_selectedBinding].KitId;
        _bindingKit.Clear();
        for (var index = 0; index < _kits.Count; index++)
        {
            _bindingKit.AddItem(ProtectTechnicalIdentifiers(_kits[index].KitId), index);
            if (string.Equals(_kits[index].KitId, current, StringComparison.Ordinal)) _bindingKit.Select(index);
        }
    }

    private void SelectBinding(long selected)
    {
        if (_updating) return;
        CommitBindingEditor();
        _selectedBinding = (int)selected - 1;
        _explicitBinding = _selectedBinding >= 0 && _selectedBinding < _bindings.Count;
        _updating = true;
        PopulateBindingEditor();
        _updating = false;
        InvalidatePolicy();
    }

    private void CommitBindingEditor()
    {
        if (_updating || _selectedBinding < 0 || _selectedBinding >= _bindings.Count) return;
        var source = _bindings[_selectedBinding];
        var kitIndex = (int)_bindingKit.GetSelectedId();
        var kitId = kitIndex >= 0 && kitIndex < _kits.Count ? _kits[kitIndex].KitId : source.KitId;
        _bindings[_selectedBinding] = source with
        {
            Provenance = "studio:ux6-k:r015",
            EventName = _bindingEvent.Text.Trim(),
            Handler = _bindingHandler.Text.Trim(),
            KitId = kitId,
            Capabilities = SelectedCapabilities(_bindingCapabilities),
            Authority = ForgeScriptEventAuthority.Server,
            Enabled = _bindingEnabled.ButtonPressed,
        };
    }

    private void AddBinding()
    {
        CommitBindingEditor();
        CommitKitEditor();
        if (_kits.Count == 0)
        {
            SetStatus("Ajoutez d’abord un kit de capacités.", error: true);
            return;
        }
        var index = _bindings.Count + 1;
        var kit = _kits[0];
        var capabilities = kit.Capabilities.Count > 0
            ? kit.Capabilities.ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        _bindings.Add(new(Guid.NewGuid(), 1, "studio:ux6-k:r015", $"module.event-{index}",
            $"handle_event_{index}", kit.KitId, capabilities, ForgeScriptEventAuthority.Server, true));
        PopulateBindings(_bindings.Count - 1);
        InvalidatePolicy();
        SetStatus("Liaison ajoutée au brouillon · prévisualisez avant application.");
    }

    private void RemoveBinding()
    {
        CommitBindingEditor();
        if (_selectedBinding < 0 || _selectedBinding >= _bindings.Count) return;
        _bindings.RemoveAt(_selectedBinding);
        PopulateBindings(Math.Min(_selectedBinding, _bindings.Count - 1));
        InvalidatePolicy();
    }

    private void MoveBinding(int direction)
    {
        CommitBindingEditor();
        var target = _selectedBinding + direction;
        if (_selectedBinding < 0 || target < 0 || target >= _bindings.Count) return;
        (_bindings[_selectedBinding], _bindings[target]) = (_bindings[target], _bindings[_selectedBinding]);
        PopulateBindings(target);
        InvalidatePolicy();
    }

    private StudioUx6KPolicyDraft? ReadPolicy()
    {
        if (_policyDraft is null) return null;
        CommitKitEditor();
        CommitBindingEditor();
        return _policyDraft with
        {
            Provenance = "studio:ux6-k:r015",
            ApiVersion = _apiVersion.Text.Trim(),
            DenyByDefault = _denyByDefault.ButtonPressed,
            ServerEnforced = _serverEnforced.ButtonPressed,
            RequireSignedDistribution = _requireSignature.ButtonPressed,
            AllowUnsignedDevelopment = _allowUnsignedDevelopment.ButtonPressed,
            RequiredAtRuntime = _requiredAtRuntime.ButtonPressed,
            AllowedCapabilities = SelectedCapabilities(_moduleCapabilities),
            KitGrants = _kits.ToArray(),
            EventBindings = _bindings.ToArray(),
        };
    }

    private void Preview()
    {
        var draft = ReadPolicy();
        if (draft is null) return;
        _previewResult = _session.PreviewModulePermissionUx6K(draft);
        _policyReview.Text = _previewResult.Message + System.Environment.NewLine +
                             (_previewResult.Succeeded
                                 ? $"Diff humain · {draft.AllowedCapabilities.Count} familles autorisées · " +
                                   $"{draft.KitGrants.Count} kit(s) · {draft.EventBindings.Count} liaison(s) · " +
                                   $"signature {(_requireSignature.ButtonPressed ? "requise" : "désactivée")}"
                                 : "Le manifeste canonique reste inchangé.");
        RenderDiagnostics(_previewResult.Diagnostics);
        SetStatus(_previewResult.Message, !_previewResult.Succeeded);
        UpdateButtons();
    }

    private void Simulate()
    {
        var draft = ReadPolicy();
        if (draft is null || _sourceDraft is null || !HasExplicitSelection)
        {
            SetStatus("Sélectionnez la source, un kit et une liaison avant la simulation.", error: true);
            return;
        }
        _simulationResult = _session.SimulateModulePermissionUx6K(draft, _sourceDraft);
        _runtimeReview.Text = _simulationResult.Message + System.Environment.NewLine +
                              $"serveur={YesNo(_simulationResult.TrustedServerExecuted)} · " +
                              $"intersection={YesNo(_simulationResult.CapabilityIntersection)} · " +
                              $"signature={YesNo(_simulationResult.UntrustedSignatureDenied)} · " +
                              $"quota+récupération={YesNo(_simulationResult.QuotaAbuseBlocked && _simulationResult.RuntimeRecovered)}";
        _console.Text = _simulationResult.Events.Count == 0
            ? "Aucun événement sandbox."
            : string.Join(System.Environment.NewLine,
                _simulationResult.Events.Take(4).Select(ProtectTechnicalIdentifiers));
        RenderDiagnostics(_simulationResult.Diagnostics);
        SetStatus(_simulationResult.Message, !_simulationResult.Succeeded);
        UpdateButtons();
    }

    private void Apply()
    {
        var draft = ReadPolicy();
        if (draft is null) return;
        var result = _session.ApplyApprovedModulePermissionUx6K(draft);
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            ReloadPolicy();
            PolicyChanged?.Invoke();
        }
        UpdateButtons();
    }

    private void Undo()
    {
        var result = _session.UndoModulePermissionUx6K();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) { ReloadPolicy(); PolicyChanged?.Invoke(); }
        UpdateButtons();
    }

    private void Redo()
    {
        var result = _session.RedoModulePermissionUx6K();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) { ReloadPolicy(); PolicyChanged?.Invoke(); }
        UpdateButtons();
    }

    private void Save()
    {
        var result = _session.SaveModulePermissionUx6K();
        SetStatus(result.Message, !result.Succeeded);
        UpdateButtons();
    }

    private void HookEditors()
    {
        foreach (var edit in new[] { _apiVersion, _kitId, _bindingEvent, _bindingHandler })
            edit.TextChanged += _ => InvalidatePolicy();
        foreach (var check in new[]
                 {
                     _requireSignature, _allowUnsignedDevelopment, _requiredAtRuntime, _bindingEnabled,
                 })
            check.Toggled += _ => InvalidatePolicy();
        _bindingKit.ItemSelected += _ => InvalidatePolicy();
    }

    private void InvalidatePolicy()
    {
        if (_updating) return;
        _session.CancelUx6KApproval();
        _previewResult = null;
        _simulationResult = null;
        _policyReview.Text = "Brouillon modifié · relancez l’aperçu sec canonique.";
        _runtimeReview.Text = "Simulation périmée · relancez autorité et sandbox.";
        UpdateButtons();
    }

    private void InvalidateSimulation()
    {
        _simulationResult = null;
        _runtimeReview.Text = "Source modifiée · relancez autorité et sandbox.";
        UpdateButtons();
    }

    private void Invalidate()
    {
        _previewResult = null;
        _simulationResult = null;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        _preview.Disabled = _policyDraft is null;
        _simulate.Disabled = !HasExplicitSelection;
        _apply.Disabled = !PreviewReady;
        _undo.Disabled = _policyDraft is null;
        _redo.Disabled = _policyDraft is null;
        _save.Disabled = _policyDraft is null || !_session.ModulePermissionHasUnsavedChangesUx6K;
        _kitRemove.Disabled = _selectedKit < 0;
        _kitUp.Disabled = _selectedKit <= 0;
        _kitDown.Disabled = _selectedKit < 0 || _selectedKit >= _kits.Count - 1;
        _bindingRemove.Disabled = _selectedBinding < 0;
        _bindingUp.Disabled = _selectedBinding <= 0;
        _bindingDown.Disabled = _selectedBinding < 0 || _selectedBinding >= _bindings.Count - 1;
    }

    private void ResetControls()
    {
        _explicitSource = _explicitKit = _explicitBinding = false;
        _sourceDraft = null;
        _policyDraft = null;
        _kits = [];
        _bindings = [];
        _selectedKit = _selectedBinding = -1;
        _previewResult = null;
        _simulationResult = null;
        _source.Text = string.Empty;
        _policyReview.Text = "Aucun aperçu sec canonique.";
        _runtimeReview.Text = "Aucune simulation d’autorité et sandbox.";
        _console.Text = "Console R015 prête.";
        RenderDiagnostics([]);
    }

    private void RenderDiagnostics(IReadOnlyList<StudioUx6FDiagnostic> diagnostics)
    {
        ClearChildren(_diagnosticRows);
        if (diagnostics.Count == 0)
        {
            _diagnosticRows.AddChild(StudioStyle.Label("Aucun diagnostic bloquant.", 9, StudioStyle.Success));
            return;
        }
        foreach (var diagnostic in diagnostics.Take(8))
        {
            var button = StudioStyle.Button($"{HumanDiagnostic(diagnostic.Code)} · {HumanPath(diagnostic.Path)}",
                compact: true);
            button.TooltipText = ProtectTechnicalIdentifiers(diagnostic.Message);
            button.Pressed += () => FocusDiagnostic(diagnostic);
            _diagnosticRows.AddChild(button);
        }
    }

    private void FocusDiagnostic(StudioUx6FDiagnostic diagnostic)
    {
        _status.Text = $"{HumanDiagnostic(diagnostic.Code)} · {ProtectTechnicalIdentifiers(diagnostic.Message)}";
        if (diagnostic.Path.Contains("apiVersion", StringComparison.Ordinal)) _apiVersion.GrabFocus();
        else if (diagnostic.Path.Contains("kit", StringComparison.Ordinal)) _kitId.GrabFocus();
        else if (diagnostic.Path.Contains("event", StringComparison.Ordinal)) _bindingEvent.GrabFocus();
        else _requireSignature.GrabFocus();
    }

    private StudioUx6KSourceSnapshot? CaptureSource()
    {
        var project = _sourceSession.Project;
        var history = _sourceSession.History;
        var session = _sourceSession.Session;
        if (project is null || history is null || session is null) return null;
        var policyPath = new ForgeScriptModulePermissionStore(session.RootDirectory).Path;
        return new(project, history, history.State.Revision, history.Journal.ToArray(), _sourceSession.IsDirty,
            history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.CaptureScriptApprovalUx6F(),
            _sourceSession.CaptureExecutionPolicyApprovalUx6J(),
            _sourceSession.CaptureModulePermissionApprovalUx6K(),
            File.Exists(policyPath), File.Exists(policyPath) ? File.ReadAllBytes(policyPath) : null);
    }

    private bool SourceMatches()
    {
        var snapshot = _sourceSnapshot;
        var history = _sourceSession.History;
        var session = _sourceSession.Session;
        if (snapshot is null || history is null || session is null) return false;
        var policyPath = new ForgeScriptModulePermissionStore(session.RootDirectory).Path;
        var existed = File.Exists(policyPath);
        var bytes = existed ? File.ReadAllBytes(policyPath) : null;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history.State.Revision == snapshot.Revision &&
               history.Journal.SequenceEqual(snapshot.Journal) && _sourceSession.IsDirty == snapshot.IsDirty &&
               history.CanUndo == snapshot.CanUndo && history.CanRedo == snapshot.CanRedo &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.CaptureScriptApprovalUx6F() == snapshot.ScriptApproval &&
               _sourceSession.CaptureExecutionPolicyApprovalUx6J() == snapshot.ExecutionPolicyApproval &&
               _sourceSession.CaptureModulePermissionApprovalUx6K() == snapshot.PermissionApproval &&
               existed == snapshot.PermissionFileExisted &&
               ((bytes is null && snapshot.PermissionFileBytes is null) ||
                (bytes is not null && snapshot.PermissionFileBytes is not null &&
                 bytes.SequenceEqual(snapshot.PermissionFileBytes)));
    }

    private bool FailEvidence(string message)
    {
        SetStatus(message, error: true);
        ReleaseEvidence();
        _session = _sourceSession;
        Visible = false;
        return false;
    }

    private bool ReleaseEvidence()
    {
        _evidenceSession?.Close();
        _evidenceSession = null;
        var released = StudioSessionController.DeleteUx6FTemporaryWorkspace(_evidenceRoot);
        _evidenceRoot = null;
        _evidenceReleased = released;
        return released;
    }

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = ProtectTechnicalIdentifiers(message);
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Success;
        StatusChanged?.Invoke(message, error);
    }

    private void UpdateBounds()
    {
        var viewport = GetViewportRect().Size;
        var width = Mathf.Clamp(viewport.X - 32, 1400, 1880);
        var height = Mathf.Clamp(viewport.Y - 28, 820, 1050);
        _dialog.Size = new Vector2(width, height);
        _dialog.Position = new Vector2(Mathf.Max(16, (viewport.X - width) * .5f),
            Mathf.Max(14, (viewport.Y - height) * .5f));
    }

    private static VBoxContainer PanelColumn(HBoxContainer host, int minimum, float stretchRatio)
    {
        var panel = StudioStyle.Panel("Ux6KColumn", StudioStyle.SurfaceRaised, 8, 1);
        panel.CustomMinimumSize = new Vector2(minimum, 0);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.SizeFlagsStretchRatio = stretchRatio;
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;
        host.AddChild(panel);
        var margin = StudioStyle.Margin(10, 9, 10, 9);
        panel.AddChild(margin);
        var column = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        column.AddThemeConstantOverride("separation", 6);
        margin.AddChild(column);
        return column;
    }

    private static VBoxContainer TabPage(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        tabs.AddChild(scroll);
        var page = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(page);
        return page;
    }

    private static VBoxContainer Field(string label, Control control, int minimum)
    {
        var field = new VBoxContainer { CustomMinimumSize = new Vector2(minimum, 0) };
        field.AddChild(StudioStyle.Label(label, 8, StudioStyle.Muted));
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        field.AddChild(control);
        return field;
    }

    private static HBoxContainer Pair(string leftLabel, Control left, string rightLabel, Control right)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(Field(leftLabel, left, 250));
        row.AddChild(Field(rightLabel, right, 250));
        return row;
    }

    private static CheckBox Check(string text, bool disabled = false) => new()
    {
        Text = text,
        Disabled = disabled,
    };

    private static Button ActionButton(string text, Action action, int width)
    {
        var button = StudioStyle.Button(text, compact: true);
        button.CustomMinimumSize = new Vector2(width, 36);
        button.Pressed += action;
        return button;
    }

    private static HashSet<string> SelectedCapabilities(IReadOnlyDictionary<string, CheckBox> controls) =>
        controls.Where(item => item.Value.ButtonPressed).Select(item => item.Key)
            .ToHashSet(StringComparer.Ordinal);

    private static bool PolicyContentsEqual(StudioUx6KPolicyDraft left, StudioUx6KPolicyDraft right) =>
        PolicyContentsEqual(ToPolicy(left), right);

    private static bool PolicyContentsEqual(ForgeScriptModulePermissionPolicy left,
        StudioUx6KPolicyDraft right) =>
        left.Id == right.Id && left.SchemaVersion == right.SchemaVersion &&
        left.Provenance == right.Provenance && left.ApiVersion == right.ApiVersion &&
        left.ModuleId == right.ModuleId && left.DenyByDefault == right.DenyByDefault &&
        left.ServerEnforced == right.ServerEnforced &&
        left.RequireSignedDistribution == right.RequireSignedDistribution &&
        left.AllowUnsignedDevelopment == right.AllowUnsignedDevelopment &&
        left.RequiredAtRuntime == right.RequiredAtRuntime &&
        left.AllowedCapabilities.SetEquals(right.AllowedCapabilities) &&
        left.KitGrants.Count == right.KitGrants.Count &&
        left.KitGrants.Zip(right.KitGrants).All(pair => GrantEqual(pair.First, pair.Second)) &&
        left.EventBindings.Count == right.EventBindings.Count &&
        left.EventBindings.Zip(right.EventBindings).All(pair => BindingEqual(pair.First, pair.Second));

    private static ForgeScriptModulePermissionPolicy ToPolicy(StudioUx6KPolicyDraft draft) => new(
        draft.Id, draft.SchemaVersion, draft.Provenance, draft.ApiVersion, draft.ModuleId,
        draft.DenyByDefault, draft.ServerEnforced, draft.RequireSignedDistribution,
        draft.AllowUnsignedDevelopment, draft.RequiredAtRuntime,
        draft.AllowedCapabilities.ToHashSet(StringComparer.Ordinal), draft.KitGrants.ToArray(),
        draft.EventBindings.ToArray());

    private static bool GrantEqual(ForgeScriptKitCapabilityGrant left, ForgeScriptKitCapabilityGrant right) =>
        left.Id == right.Id && left.SchemaVersion == right.SchemaVersion &&
        left.Provenance == right.Provenance && left.KitId == right.KitId &&
        left.Capabilities.SetEquals(right.Capabilities);

    private static bool BindingEqual(ForgeScriptPermissionEventBinding left,
        ForgeScriptPermissionEventBinding right) =>
        left.Id == right.Id && left.SchemaVersion == right.SchemaVersion &&
        left.Provenance == right.Provenance && left.EventName == right.EventName &&
        left.Handler == right.Handler && left.KitId == right.KitId &&
        left.Capabilities.SetEquals(right.Capabilities) && left.Authority == right.Authority &&
        left.Enabled == right.Enabled;

    private static string YesNo(bool value) => value ? "prouvé" : "non prouvé";

    private static string HumanAuthority(string authority) => authority switch
    {
        "server" => "Serveur autoritaire",
        "owner" => "Propriétaire autoritaire",
        "local" => "Local contrôlé",
        _ => authority,
    };

    private static string HumanDiagnostic(string code) => code switch
    {
        "script.permission.deny_default_required" => "Deny-by-default requis",
        "script.permission.server_enforcement_required" => "Autorité serveur requise",
        "script.permission.signature_required" => "Signature fiable requise",
        "script.permission.capability_invalid" => "Allowlist invalide",
        "script.permission.kit_capability_invalid" => "Capacités du kit invalides",
        "script.permission.binding_capability_invalid" => "Intersection de liaison invalide",
        "script.permission.binding_kit_missing" => "Kit de liaison absent",
        "script.permission.revision_conflict" => "Révision périmée",
        "script.permission.source_changed" => "Manifeste source modifié",
        _ => code.Replace('.', ' '),
    };

    private static string HumanPath(string path) => path switch
    {
        var value when value.Contains("kitGrants", StringComparison.Ordinal) => "Kits de capacités",
        var value when value.Contains("eventBindings", StringComparison.Ordinal) => "Liaisons d’événements",
        var value when value.Contains("apiVersion", StringComparison.Ordinal) => "Version d’API",
        var value when value.Contains("allowedCapabilities", StringComparison.Ordinal) => "Allowlist du module",
        _ => "Permissions par module",
    };

    private static void ClearChildren(Node node)
    {
        foreach (var child in node.GetChildren()) child.QueueFree();
    }

    private static IEnumerable<string> DescendantText(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Label label) yield return label.Text;
            else if (child is Button button) yield return button.Text;
            else if (child is LineEdit edit) yield return edit.Text;
            else if (child is CodeEdit code) yield return code.Text;
            foreach (var text in DescendantText(child)) yield return text;
        }
    }

    private static bool ContainsUuid(string text) => Regex.IsMatch(text,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
        RegexOptions.CultureInvariant);

    private static string ProtectTechnicalIdentifiers(string text) => Regex.Replace(text,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
        "référence interne protégée", RegexOptions.CultureInvariant);
}
