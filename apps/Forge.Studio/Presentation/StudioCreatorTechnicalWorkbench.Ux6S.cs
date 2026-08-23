using System.Globalization;
using System.Text.RegularExpressions;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioCreatorTechnicalWorkbenchUx6S : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private SourceSnapshotUx6S? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;
    private bool _updating;
    private bool _explicitSelection;

    private PanelContainer _dialog = null!;
    private OptionButton _profileChoice = null!;
    private TabContainer _tabs = null!;
    private CheckBox _deviceEnabled = null!;
    private TextEdit _deviceAllowlist = null!;
    private CheckBox _deviceConsent = null!;
    private CheckBox _deviceRedaction = null!;
    private TextEdit _deviceRequested = null!;
    private CheckBox _resourceEnabled = null!;
    private LineEdit _resourceMaximumResults = null!;
    private LineEdit _resourceMaximumBytes = null!;
    private TextEdit _resourceNamespaces = null!;
    private LineEdit _resourceRequested = null!;
    private CheckBox _partyEnabled = null!;
    private LineEdit _partyMaximum = null!;
    private CheckBox _partyDirectSwitch = null!;
    private TextEdit _partyCommands = null!;
    private LineEdit _partyRequested = null!;
    private LineEdit _progressionMaximum = null!;
    private CheckBox _progressionDataDriven = null!;
    private CheckBox _progressionLargeUi = null!;
    private LineEdit _progressionRequested = null!;
    private VBoxContainer _runtimeResults = null!;
    private Label _review = null!;
    private Label _status = null!;
    private Button _apply = null!;
    private Button _undo = null!;
    private Button _redo = null!;

    private StudioUx6SDraft? _draft;
    private StudioUx6SPreview? _preview;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? CreatorPolicyChanged;

    internal bool IsOpen => Visible;
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                            !ReferenceEquals(_session, _sourceSession);
    internal bool HasExplicitSelection => _explicitSelection && _draft is not null;
    internal bool PreviewReady => _preview is
    {
        Succeeded: true,
        CanApply: true,
        CanonicalDryRun: true,
        DevicePolicyResolved: true,
        ResourcePolicyResolved: true,
        PartyPolicyResolved: true,
        ProgressionPolicyResolved: true,
        RuntimeClaimsBounded: true,
    };
    internal bool ShowsFourPolicies => _tabs.GetTabCount() == 4 && _draft is not null;
    internal bool EvidenceSourceUntouched => SourceMatches();
    internal bool EvidenceResourcesReleased => _evidenceReleased && _evidenceSession is null &&
                                                _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantText(this));
            return Visible && text.Contains("Requêtes de périphérique", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Ressources VM", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Contrôle de groupe", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Progression data-driven", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu sec canonique", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun accès matériel", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuid(text);
        }
    }

    internal StudioCreatorTechnicalWorkbenchUx6S(StudioSessionController session)
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

        _dialog = StudioStyle.Panel("Ux6SCreatorDialog", StudioStyle.Surface, 12, 1);
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
        titles.AddChild(StudioStyle.Label("POLITIQUES CREATOR · UX6-S", 10, StudioStyle.Accent));
        titles.AddChild(StudioStyle.Label(
            "Requêtes de périphérique, ressources VM, contrôle de groupe et progression data-driven", 18,
            StudioStyle.Text, wrap: true));
        titles.AddChild(StudioStyle.Label(
            "Aperçu de décision uniquement · aucun accès matériel, ressource, acteur ou classe n’est créé", 9,
            StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);
        root.AddChild(header);

        var selection = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selection.AddChild(StudioStyle.Label("PROFIL CREATOR DU MODULE", 9, StudioStyle.Accent));
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
        BuildDeviceTab();
        BuildResourceTab();
        BuildPartyTab();
        BuildProgressionTab();
        WireDraftInvalidation();

        var runtimePanel = StudioStyle.Panel("Ux6SCreatorRuntime", StudioStyle.SurfaceRaised, 8, 1);
        runtimePanel.CustomMinimumSize = new Vector2(0, 96);
        var runtimeMargin = StudioStyle.Margin(9, 7, 9, 7);
        runtimePanel.AddChild(runtimeMargin);
        _runtimeResults = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _runtimeResults.AddChild(StudioStyle.Label("APERÇU DE DÉCISION · aucun aperçu sec canonique", 9,
            StudioStyle.Accent));
        runtimeMargin.AddChild(_runtimeResults);
        root.AddChild(runtimePanel);
        _review = StudioStyle.Label("Aucune modification approuvée.", 9, StudioStyle.Muted, wrap: true);
        root.AddChild(_review);
        _status = StudioStyle.Label("Sélectionnez explicitement le profil Creator.", 10,
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

    private void BuildDeviceTab()
    {
        var page = TabPage("Périphériques");
        page.AddChild(StudioStyle.Label("ORF-EE-014 · Requêtes de périphérique gouvernées", 10,
            StudioStyle.Accent, wrap: true));
        page.AddChild(StudioStyle.Label(
            "Les requêtes sont filtrées par nom. L’activation exige toujours consentement et anonymisation des identifiants stables.",
            9, StudioStyle.Muted, wrap: true));
        _deviceEnabled = new CheckBox { Text = "Autoriser les requêtes déclarées" };
        _deviceConsent = new CheckBox { Text = "Consentement explicite obligatoire" };
        _deviceRedaction = new CheckBox { Text = "Anonymiser les identifiants stables" };
        page.AddChild(_deviceEnabled);
        page.AddChild(_deviceConsent);
        page.AddChild(_deviceRedaction);
        var columns = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _deviceAllowlist = Multi(160);
        _deviceRequested = Multi(160);
        columns.AddChild(LabeledColumn("Requêtes autorisées · une par ligne", _deviceAllowlist));
        columns.AddChild(LabeledColumn("Requêtes simulées · une par ligne", _deviceRequested));
        page.AddChild(columns);
        page.AddChild(StudioStyle.Label(
            "Aucune API système ni aucun périphérique n’est interrogé par cet aperçu.", 10,
            StudioStyle.Warning, wrap: true));
    }

    private void BuildResourceTab()
    {
        var page = TabPage("Ressources VM");
        page.AddChild(StudioStyle.Label("ORF-EE-017 · Commandes du gestionnaire de ressources", 10,
            StudioStyle.Accent, wrap: true));
        page.AddChild(StudioStyle.Label(
            "Les commandes sont limitées par nombre de résultats, volume total et espaces de noms autorisés.",
            9, StudioStyle.Muted, wrap: true));
        _resourceEnabled = new CheckBox { Text = "Autoriser les commandes de ressources" };
        page.AddChild(_resourceEnabled);
        var quotas = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _resourceMaximumResults = Field("256");
        _resourceMaximumBytes = Field("67108864");
        _resourceRequested = Field("32");
        quotas.AddChild(LabeledColumn("Résultats maximum", _resourceMaximumResults));
        quotas.AddChild(LabeledColumn("Octets maximum", _resourceMaximumBytes));
        quotas.AddChild(LabeledColumn("Résultats simulés", _resourceRequested));
        page.AddChild(quotas);
        _resourceNamespaces = Multi(172);
        AddLabeled(page, "Espaces de noms autorisés · un par ligne", _resourceNamespaces);
        page.AddChild(StudioStyle.Label(
            "L’aperçu vérifie le quota; il ne charge, ne liste et ne libère aucune ressource réelle.", 10,
            StudioStyle.Warning, wrap: true));
    }

    private void BuildPartyTab()
    {
        var page = TabPage("Contrôle de groupe");
        page.AddChild(StudioStyle.Label("ORF-EE-020 · Contrôle direct ou groupé borné", 10,
            StudioStyle.Accent, wrap: true));
        _partyEnabled = new CheckBox { Text = "Autoriser le contrôle de groupe" };
        _partyDirectSwitch = new CheckBox { Text = "Autoriser la bascule directe entre membres" };
        page.AddChild(_partyEnabled);
        page.AddChild(_partyDirectSwitch);
        var limits = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _partyMaximum = Field("8");
        _partyRequested = Field("8");
        limits.AddChild(LabeledColumn("Membres contrôlables maximum", _partyMaximum));
        limits.AddChild(LabeledColumn("Membres simulés", _partyRequested));
        page.AddChild(limits);
        _partyCommands = Multi(190);
        AddLabeled(page, "Commandes de groupe · une par ligne", _partyCommands);
        page.AddChild(StudioStyle.Label(
            "Le résolveur renvoie une décision; aucun acteur, ordre IA ou transport réseau n’est exécuté.", 10,
            StudioStyle.Warning, wrap: true));
    }

    private void BuildProgressionTab()
    {
        var page = TabPage("Progression");
        page.AddChild(StudioStyle.Label("ORF-EE-021 · Jusqu’à huit classes et parcours extensibles", 10,
            StudioStyle.Accent, wrap: true));
        page.AddChild(StudioStyle.Label(
            "La politique garantit au moins huit parcours data-driven et peut en décrire jusqu’à 256 sans plafond implicite.",
            9, StudioStyle.Muted, wrap: true));
        var limits = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _progressionMaximum = Field("64");
        _progressionRequested = Field("8");
        limits.AddChild(LabeledColumn("Parcours maximum", _progressionMaximum));
        limits.AddChild(LabeledColumn("Classes/parcours simulés", _progressionRequested));
        page.AddChild(limits);
        _progressionDataDriven = new CheckBox { Text = "Progression définie par les données" };
        _progressionLargeUi = new CheckBox { Text = "Interface adaptée aux grands ensembles" };
        page.AddChild(_progressionDataDriven);
        page.AddChild(_progressionLargeUi);
        page.AddChild(StudioStyle.Label(
            "L’aperçu contrôle une capacité; il ne crée aucune classe, aucun niveau et aucun écran runtime.", 10,
            StudioStyle.Warning, wrap: true));
    }

    internal void Open() => OpenBound(_sourceSession, evidence: false, cancelApproval: false);

    internal void RestoreSourceSurfaceAfterSmoke() =>
        OpenBound(_sourceSession, evidence: false, cancelApproval: false);

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null) return;
        if (Visible) _session.CancelCreatorPolicyApprovalUx6S();
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
        _evidenceRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux6-s-ui-{Guid.NewGuid():N}");
        _evidenceReleased = false;
        try
        {
            var project = StudioSessionController.CreateEvidenceProjectUx6S(_sourceSession.Project);
            if (!ProjectSession.Create(_evidenceRoot, project).Succeeded)
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
            _draft = StudioSessionController.ChangedCreatorPolicyDraftUx6S(_draft);
            PopulateDraft();
            Preview();
            _tabs.CurrentTab = mode.Contains("resource", StringComparison.OrdinalIgnoreCase) ? 1 :
                mode.Contains("party", StringComparison.OrdinalIgnoreCase) ||
                mode.Contains("group", StringComparison.OrdinalIgnoreCase) ? 2 :
                mode.Contains("progress", StringComparison.OrdinalIgnoreCase) ||
                mode.Contains("class", StringComparison.OrdinalIgnoreCase) ? 3 : 0;
            var ready = PreviewReady && ShowsFourPolicies && ShowsHumanSurface && SourceMatches();
            if (!ready)
                GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_S_UI_FAILED " +
                            $"preview={PreviewReady.ToString().ToLowerInvariant()} " +
                            $"policies={ShowsFourPolicies.ToString().ToLowerInvariant()} " +
                            $"human={ShowsHumanSurface.ToString().ToLowerInvariant()} " +
                            $"source={SourceMatches().ToString().ToLowerInvariant()}");
            return ready;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_S_EVIDENCE_FAILED type={exception.GetType().Name}");
            ReleaseEvidence();
            return false;
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        var ready = PrepareEvidence("ux6-s-creator-technical");
        if (!ready || !UsesIsolatedEvidence || !PreviewReady || _evidenceSession is null)
        {
            CloseEvidence();
            return false;
        }
        try
        {
            var isolated = _evidenceSession;
            var before = isolated.Project;
            var applied = isolated.ApplyApprovedCreatorPolicyUx6S();
            var after = isolated.Project;
            var undo = isolated.UndoCreatorPolicyUx6S();
            var undone = ReferenceEquals(before, isolated.Project);
            var redo = isolated.RedoCreatorPolicyUx6S();
            var redone = ReferenceEquals(after, isolated.Project);
            var saved = isolated.SaveCreatorPolicyUx6S();
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
        if (cancelApproval) _session.CancelCreatorPolicyApprovalUx6S();
        ResetControls(keepReleaseState: evidence);
        RebuildProfileChoices();
        Visible = true;
        UpdateBounds();
    }

    private void RebuildProfileChoices()
    {
        _updating = true;
        _profileChoice.Clear();
        _profileChoice.AddItem("Choisir explicitement le profil Creator…");
        foreach (var choice in _session.CreatorPolicyChoicesUx6S()) _profileChoice.AddItem(choice.Label);
        _profileChoice.Select(0);
        _updating = false;
    }

    private void SelectProfile(int index)
    {
        if (_updating) return;
        _explicitSelection = index > 0;
        _draft = _session.CreateCreatorPolicyDraftUx6S(_explicitSelection);
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
        _deviceEnabled.ButtonPressed = _draft.DeviceQueriesEnabled;
        _deviceAllowlist.Text = string.Join('\n', _draft.AllowedDeviceQueries);
        _deviceConsent.ButtonPressed = _draft.RequireConsent;
        _deviceRedaction.ButtonPressed = _draft.RedactStableIdentifiers;
        _deviceRequested.Text = string.Join('\n', _draft.RequestedDeviceQueries);
        _resourceEnabled.ButtonPressed = _draft.ResourceCommandsEnabled;
        _resourceMaximumResults.Text = _draft.MaximumResourceResults.ToString(CultureInfo.InvariantCulture);
        _resourceMaximumBytes.Text = _draft.MaximumResourceBytes.ToString(CultureInfo.InvariantCulture);
        _resourceNamespaces.Text = string.Join('\n', _draft.AllowedResourceNamespaces);
        _resourceRequested.Text = _draft.RequestedResourceResults.ToString(CultureInfo.InvariantCulture);
        _partyEnabled.ButtonPressed = _draft.PartyControlEnabled;
        _partyMaximum.Text = _draft.MaximumControlledMembers.ToString(CultureInfo.InvariantCulture);
        _partyDirectSwitch.ButtonPressed = _draft.AllowDirectSwitch;
        _partyCommands.Text = string.Join('\n', _draft.PartyCommands);
        _partyRequested.Text = _draft.RequestedPartyMembers.ToString(CultureInfo.InvariantCulture);
        _progressionMaximum.Text = _draft.MaximumProgressionTracks.ToString(CultureInfo.InvariantCulture);
        _progressionDataDriven.ButtonPressed = _draft.DataDrivenProgression;
        _progressionLargeUi.ButtonPressed = _draft.SupportLargeSetUi;
        _progressionRequested.Text = _draft.RequestedProgressionTracks.ToString(CultureInfo.InvariantCulture);
        _updating = false;
        UpdateButtons();
    }

    private bool TryReadDraft(out StudioUx6SDraft? draft, out string error)
    {
        draft = null;
        error = string.Empty;
        if (_draft is null)
        {
            error = "Sélectionnez explicitement le profil Creator.";
            return false;
        }
        if (!int.TryParse(_resourceMaximumResults.Text, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var maximumResults) ||
            !long.TryParse(_resourceMaximumBytes.Text, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var maximumBytes) ||
            !int.TryParse(_resourceRequested.Text, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var requestedResources) ||
            !int.TryParse(_partyMaximum.Text, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var maximumParty) ||
            !int.TryParse(_partyRequested.Text, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var requestedParty) ||
            !int.TryParse(_progressionMaximum.Text, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var maximumProgression) ||
            !int.TryParse(_progressionRequested.Text, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var requestedProgression))
        {
            error = "Les quotas et capacités doivent être des nombres entiers exacts.";
            return false;
        }
        draft = _draft with
        {
            DeviceQueriesEnabled = _deviceEnabled.ButtonPressed,
            AllowedDeviceQueries = Lines(_deviceAllowlist.Text),
            RequireConsent = _deviceConsent.ButtonPressed,
            RedactStableIdentifiers = _deviceRedaction.ButtonPressed,
            RequestedDeviceQueries = Lines(_deviceRequested.Text),
            ResourceCommandsEnabled = _resourceEnabled.ButtonPressed,
            MaximumResourceResults = maximumResults,
            MaximumResourceBytes = maximumBytes,
            AllowedResourceNamespaces = Lines(_resourceNamespaces.Text),
            RequestedResourceResults = requestedResources,
            PartyControlEnabled = _partyEnabled.ButtonPressed,
            MaximumControlledMembers = maximumParty,
            AllowDirectSwitch = _partyDirectSwitch.ButtonPressed,
            PartyCommands = Lines(_partyCommands.Text),
            RequestedPartyMembers = requestedParty,
            MaximumProgressionTracks = maximumProgression,
            DataDrivenProgression = _progressionDataDriven.ButtonPressed,
            SupportLargeSetUi = _progressionLargeUi.ButtonPressed,
            RequestedProgressionTracks = requestedProgression,
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
        _preview = _session.PreviewCreatorPolicyUx6S(draft);
        RenderPreview();
        SetStatus(_preview.Message, error: !_preview.Succeeded);
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedCreatorPolicyUx6S();
        SetStatus(result.Message, error: !result.Succeeded);
        if (!result.Succeeded) return;
        CreatorPolicyChanged?.Invoke();
        ReloadAfterMutation();
    }

    private void Undo()
    {
        var result = _session.UndoCreatorPolicyUx6S();
        SetStatus(result.Message, error: !result.Succeeded);
        if (result.Succeeded)
        {
            CreatorPolicyChanged?.Invoke();
            ReloadAfterMutation();
        }
    }

    private void Redo()
    {
        var result = _session.RedoCreatorPolicyUx6S();
        SetStatus(result.Message, error: !result.Succeeded);
        if (result.Succeeded)
        {
            CreatorPolicyChanged?.Invoke();
            ReloadAfterMutation();
        }
    }

    private void Save()
    {
        var result = _session.SaveCreatorPolicyUx6S();
        SetStatus(result.Message, error: !result.Succeeded);
    }

    private void ReloadAfterMutation()
    {
        _draft = _session.CreateCreatorPolicyDraftUx6S(true);
        _preview = null;
        if (_draft is not null) PopulateDraft();
        ClearPreview();
        UpdateButtons();
    }

    private void RenderPreview()
    {
        ClearChildren(_runtimeResults);
        _runtimeResults.AddChild(StudioStyle.Label("APERÇU DE DÉCISION · aperçu sec canonique", 9,
            StudioStyle.Accent));
        if (_preview is null) return;
        foreach (var line in _preview.RuntimePreview.Take(5))
            _runtimeResults.AddChild(StudioStyle.Label(line, 9,
                line.StartsWith("Aucun ", StringComparison.Ordinal) ? StudioStyle.Warning : StudioStyle.Text,
                wrap: true));
        if (_preview.Diagnostics.Count > 0)
            foreach (var diagnostic in _preview.Diagnostics)
                _runtimeResults.AddChild(StudioStyle.Label($"{diagnostic.Code} · {diagnostic.Message}", 9,
                    StudioStyle.Danger, wrap: true));
        _review.Text = _preview.Diff.Count == 0
            ? "Aucune différence persistante."
            : "DIFF HUMAIN · " + string.Join(" · ", _preview.Diff);
        UpdateButtons();
    }

    private void ClearPreview()
    {
        ClearChildren(_runtimeResults);
        _runtimeResults.AddChild(StudioStyle.Label("APERÇU DE DÉCISION · aucun aperçu sec canonique", 9,
            StudioStyle.Accent));
        _review.Text = "Aucune modification approuvée.";
    }

    private void WireDraftInvalidation()
    {
        foreach (var check in new[]
                 {
                     _deviceEnabled, _deviceConsent, _deviceRedaction, _resourceEnabled, _partyEnabled,
                     _partyDirectSwitch, _progressionDataDriven, _progressionLargeUi,
                 })
            check.Toggled += _ => DraftChanged();
        foreach (var field in new[]
                 {
                     _resourceMaximumResults, _resourceMaximumBytes, _resourceRequested, _partyMaximum,
                     _partyRequested, _progressionMaximum, _progressionRequested,
                 })
            field.TextChanged += _ => DraftChanged();
        foreach (var text in new[]
                 {
                     _deviceAllowlist, _deviceRequested, _resourceNamespaces, _partyCommands,
                 })
            text.TextChanged += DraftChanged;
    }

    private void DraftChanged()
    {
        if (_updating) return;
        _session.CancelCreatorPolicyApprovalUx6S();
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
        if (_profileChoice is not null)
        {
            _profileChoice.Clear();
            _profileChoice.AddItem("Choisir explicitement le profil Creator…");
            _profileChoice.Select(0);
        }
        ClearEditors();
        ClearPreview();
        _status.Text = "Sélectionnez explicitement le profil Creator.";
        if (!keepReleaseState) _evidenceReleased = true;
        _updating = false;
        UpdateButtons();
    }

    private void ClearEditors()
    {
        foreach (var check in new[]
                 {
                     _deviceEnabled, _deviceConsent, _deviceRedaction, _resourceEnabled, _partyEnabled,
                     _partyDirectSwitch, _progressionDataDriven, _progressionLargeUi,
                 })
            if (check is not null) check.ButtonPressed = false;
        foreach (var field in new[]
                 {
                     _resourceMaximumResults, _resourceMaximumBytes, _resourceRequested, _partyMaximum,
                     _partyRequested, _progressionMaximum, _progressionRequested,
                 })
            if (field is not null) field.Text = string.Empty;
        foreach (var text in new[]
                 {
                     _deviceAllowlist, _deviceRequested, _resourceNamespaces, _partyCommands,
                 })
            if (text is not null) text.Text = string.Empty;
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

    private SourceSnapshotUx6S CaptureSource()
    {
        var history = _sourceSession.History!;
        return new(_sourceSession.Project!, history, history.State.Revision, history.Journal.ToArray(),
            _sourceSession.IsDirty, history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureCreatorPolicyApprovalUx6S());
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
               _sourceSession.CaptureCreatorPolicyApprovalUx6S() == snapshot.Approval;
    }

    private void ReleaseEvidence()
    {
        _evidenceSession?.Close();
        _evidenceSession = null;
        var released = string.IsNullOrWhiteSpace(_evidenceRoot) ||
                       StudioSessionController.DeleteTemporaryUx6S(_evidenceRoot);
        _evidenceRoot = null;
        _evidenceReleased = released;
        _session = _sourceSession;
        if (!released) GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_S_UI_CLEANUP_FAILED");
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
        CustomMinimumSize = new Vector2(120, 34),
    };

    private static TextEdit Multi(float height) => new()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        CustomMinimumSize = new Vector2(0, height),
        WrapMode = TextEdit.LineWrappingMode.Boundary,
        ScrollFitContentHeight = false,
    };

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

    private static IReadOnlyList<string> Lines(string text) => text.Replace("\r", string.Empty,
            StringComparison.Ordinal).Split('\n').Select(item => item.Trim())
        .Where(item => item.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)
        .Order(StringComparer.OrdinalIgnoreCase).ToArray();

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
                case TextEdit textEdit when textEdit.Visible: yield return textEdit.Text; break;
                case OptionButton option when option.Visible:
                    for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
                    break;
            }
            foreach (var text in DescendantText(child)) yield return text;
        }
    }

    private static bool ContainsUuid(string value) => Regex.IsMatch(value,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b");

    private sealed record SourceSnapshotUx6S(
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
        StudioUx6SApprovalSnapshot Approval);
}
