#if TOOLS
using Forge.LiveDirector.Authoring;
using Forge.LiveDirector.Scenarios;
using Forge.Network.Operations;
using Godot;

namespace Forge.Toolset.Editor;

public partial class LiveDirectorWindow : Window
{
    private Label? _session;
    private Label? _authority;
    private Label? _audit;
    private RichTextLabel? _timeline;
    private string? _capturePath;
    private double _captureDelay;
    private bool _prepared;
    private Action<Window>? _applyIcon;
    private Action? _openAccess;
    private LiveDirectorWorkspaceProfile _workspace = LiveDirectorWorkspaceProfile.CreateDefault("toolset:r125");
    private readonly LiveDirectorWorkspaceHistory _workspaceHistory;
    private SpinBox? _sessionMinutes;
    private SpinBox? _maximumInstances;
    private Label? _workspaceStatus;
    private Label? _workspaceAudit;
    private LiveDirectorActionProfile _actionProfile = LiveDirectorActionProfile.CreateDefault("toolset:r126");
    private readonly LiveDirectorActionHistory _actionHistory;
    private SpinBox? _possessionMinutes;
    private Label? _actionStatus;
    private Label? _actionAudit;
    private LiveDirectorAuthorityProfile _authorityProfile = LiveDirectorAuthorityProfile.CreateDefault("toolset:r127");
    private readonly LiveDirectorAuthorityHistory _authorityHistory;
    private SpinBox? _currencyCap;
    private Label? _authorityProfileStatus;
    private Label? _authorityProfileAudit;
    private LiveDirectorGroupProfile _groupProfile = LiveDirectorGroupProfile.CreateDefault("toolset:r128");
    private readonly LiveDirectorGroupHistory _groupHistory;
    private SpinBox? _selectionCap;
    private Label? _groupStatus;
    private Label? _groupAudit;
    private LiveDirectorOperationsProfile _operationsProfile = LiveDirectorOperationsProfile.CreateDefault("toolset:r129");
    private readonly LiveDirectorOperationsHistory _operationsHistory;
    private Label? _operationsProfileStatus;
    private LiveDirectorNarrativeProfile _narrativeProfile = LiveDirectorNarrativeProfile.CreateDefault("toolset:r130");
    private readonly LiveDirectorNarrativeHistory _narrativeHistory;
    private Label? _narrativeStatus;

    public LiveDirectorWindow()
    {
        _workspaceHistory = new(_workspace);
        _actionHistory = new(_actionProfile);
        _authorityHistory = new(_authorityProfile);
        _groupHistory = new(_groupProfile);
        _operationsHistory = new(_operationsProfile);
        _narrativeHistory = new(_narrativeProfile);
    }

    public void Initialize(Action<Window> applyIcon, Action? openAccess = null)
    {
        _applyIcon = applyIcon;
        _openAccess = openAccess;
    }

    public override void _Ready()
    {
        Title = "Live Director · OpenRPG Forge";
        Size = new(1320, 820);
        MinSize = new(1020, 680);
        CloseRequested += Hide;
        BuildUi();
        _applyIcon?.Invoke(this);
        var argument = OS.GetCmdlineUserArgs().FirstOrDefault(value => value.StartsWith("--live-director-ui-capture=", StringComparison.Ordinal));
        if (argument is not null) { _capturePath = argument["--live-director-ui-capture=".Length..]; SetProcess(true); }
    }

    public override void _Process(double delta)
    {
        if (_capturePath is null) return;
        _captureDelay += delta;
        if (!_prepared && _captureDelay >= 1) { RunScenario(); _prepared = true; return; }
        if (!_prepared || _captureDelay < 2) return;
        var error = GetViewport().GetTexture().GetImage().SavePng(_capturePath);
        GD.Print($"OPENRPG_FORGE_LIVE_DIRECTOR_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} path={_capturePath}");
        _capturePath = null; SetProcess(false);
    }

    public void Open() => PopupCentered();

    public bool RunR125SmokeTest()
    {
        var store = new LiveDirectorWorkspaceStore();
        var loaded = store.Deserialize(store.Serialize(_workspace));
        var invalid = LiveDirectorWorkspaceValidator.Validate(_workspace with
        {
            Spawn = _workspace.Spawn with { MaximumInstances = 0 }
        });
        var script = new LiveDirectorWorkspaceScriptService(loaded);
        return store.Serialize(loaded) == store.Serialize(_workspace) &&
            invalid.Any(item => item.Code == "LIVE_SPAWN_POLICY_INVALID") &&
            LiveDirectorWorkspaceRuntimeAdapter.Resolve(null, false).Diagnostics.Count == 0 &&
            LiveDirectorWorkspaceRuntimeAdapter.Resolve(null, true).Diagnostics.Single().Code == "LIVE_WORKSPACE_REQUIRED_MISSING" &&
            script.Read(script.Handle) is { SessionMinutes: 30, MaximumInstances: 64, FilterCount: 5, SpectatorCamera: true };
    }

    public bool RunR126SmokeTest()
    {
        var store = new LiveDirectorActionStore(); var loaded = store.Deserialize(store.Serialize(_actionProfile));
        var invalid = LiveDirectorActionValidator.Validate(_actionProfile with { Possession = _actionProfile.Possession with { MaximumMinutes = 0 } });
        var script = new LiveDirectorActionScriptService(loaded);
        return store.Serialize(loaded) == store.Serialize(_actionProfile) && invalid.Any(x => x.Code == "LIVE_POSSESSION_POLICY_INVALID") &&
            LiveDirectorActionRuntimeAdapter.Resolve(null, false).Diagnostics.Count == 0 && LiveDirectorActionRuntimeAdapter.Resolve(null, true).Diagnostics.Single().Code == "LIVE_ACTION_PROFILE_REQUIRED_MISSING" &&
            script.Read(script.Handle) is { HealScopeCount: 4, PossessionMinutes: 120, Confirmation: true, Consent: true };
    }

    public bool RunR127SmokeTest()
    {
        var store = new LiveDirectorAuthorityStore(); var loaded = store.Deserialize(store.Serialize(_authorityProfile));
        var invalid = LiveDirectorAuthorityValidator.Validate(_authorityProfile with { MaximumProtectionMinutes = 0 });
        var script = new LiveDirectorAuthorityScriptService(loaded);
        return store.Serialize(loaded) == store.Serialize(_authorityProfile) && invalid.Any(x => x.Code == "LIVE_PROTECTION_POLICY_INVALID") &&
            LiveDirectorAuthorityRuntimeAdapter.Resolve(null, false).Diagnostics.Count == 0 && LiveDirectorAuthorityRuntimeAdapter.Resolve(null, true).Diagnostics.Single().Code == "LIVE_AUTHORITY_REQUIRED_MISSING" &&
            script.Read(script.Handle) is { ImpersonationModes: 4, ProtectionMinutes: 1440, CurrencyCap: 100000, ExperienceCap: 1000000, DoorStates: 5 };
    }

    public bool RunR128SmokeTest()
    {
        var store = new LiveDirectorGroupStore(); var loaded = store.Deserialize(store.Serialize(_groupProfile)); var invalid = LiveDirectorGroupValidator.Validate(_groupProfile with { SelectionCap = 0 }); var script = new LiveDirectorGroupScriptService(loaded);
        return store.Serialize(loaded) == store.Serialize(_groupProfile) && invalid.Any(x => x.Code == "LIVE_SELECTION_POLICY_INVALID") && LiveDirectorGroupRuntimeAdapter.Resolve(null, false).Diagnostics.Count == 0 && LiveDirectorGroupRuntimeAdapter.Resolve(null, true).Diagnostics.Single().Code == "LIVE_GROUP_REQUIRED_MISSING" && script.Read(script.Handle) is { SelectionCap: 64, Formations: 4, TravelModes: 4, Lasso: true };
    }
    public bool RunR129SmokeTest() { var s = new LiveDirectorOperationsStore(); var l = s.Deserialize(s.Serialize(_operationsProfile)); var bad = LiveDirectorOperationsValidator.Validate(_operationsProfile with { ViewportOverlayAdapter = false }); var x = new LiveDirectorOperationsScriptService(l); return s.Serialize(l) == s.Serialize(_operationsProfile) && bad.Any(d => d.Code == "LIVE_VISIBILITY_POLICY_INVALID") && LiveDirectorOperationsRuntimeAdapter.Resolve(null, false).Diagnostics.Count == 0 && LiveDirectorOperationsRuntimeAdapter.Resolve(null, true).Diagnostics.Single().Code == "LIVE_OPS_REQUIRED_MISSING" && x.Read(x.Handle) is { VariableKinds: 4, SceneFacets: 5, VisibilityLayers: 4, VisualFocus: true }; }
    public bool RunR130SmokeTest() { var s = new LiveDirectorNarrativeStore(); var l = s.Deserialize(s.Serialize(_narrativeProfile)); var bad = LiveDirectorNarrativeValidator.Validate(_narrativeProfile with { AiRuntimeAdapter = false }); var x = new LiveDirectorNarrativeScriptService(l); return s.Serialize(l) == s.Serialize(_narrativeProfile) && bad.Any(d => d.Code == "LIVE_ENCOUNTER_POLICY_INVALID") && LiveDirectorNarrativeRuntimeAdapter.Resolve(null, true).Diagnostics.Single().Code == "LIVE_NARRATIVE_REQUIRED_MISSING" && x.Read(x.Handle) is { QuestScopes: 2, EncounterActions: 5, SpawnAdapter: true, AiAdapter: true }; }

    private void BuildUi()
    {
        var background = new ColorRect { Color = new("0b0915") }; background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); AddChild(background);
        var margin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" }) margin.AddThemeConstantOverride(side, 24); AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 14); margin.AddChild(root);
        root.AddChild(Heading("LIVE DIRECTOR", 28));
        var subtitle = new Label { Text = "Dirigez la session en direct avec autorité, prévisualisation et audit immuable." }; subtitle.AddThemeColorOverride("font_color", new("c9bddc")); root.AddChild(subtitle);
        var summaries = new HBoxContainer(); summaries.AddThemeConstantOverride("separation", 12); root.AddChild(summaries);
        AddSummary(summaries, "SESSION", "2 joueurs · Crypte"); AddSummary(summaries, "AUTORITÉ", "Rôle GM · moindre privilège"); AddSummary(summaries, "MONDE LIVE", "32 capacités disponibles"); AddSummary(summaries, "AUDIT", "Chaîne SHA-256 vérifiable");
        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; body.AddThemeConstantOverride("separation", 12); root.AddChild(body);

        var players = Card(270); body.AddChild(players); var playerBox = Box(players, "JOUEURS & CIBLES");
        AddItem(playerBox, "● Elyra", "Crypte · 42 ms · Groupe A", "70d6a3"); AddItem(playerBox, "● Cael", "Crypte · 48 ms · Groupe A", "70d6a3"); AddItem(playerBox, "◆ Gardien des braises", "Créature · active", "ff9858"); AddItem(playerBox, "◇ Porte du sanctuaire", "Verrouillée · portail", "c9bddc");

        var actions = Card(430); body.AddChild(actions);
        var actionScroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; actions.AddChild(actionScroll);
        var actionBox = Box(actionScroll, "CREATOR & CHOOSER");
        actionBox.AddChild(new LineEdit { PlaceholderText = "Rechercher joueur, objet, quête...", CustomMinimumSize = new(0, 40) });
        actionBox.AddChild(new Label { Text = "Monde > Crypte > Instance principale\nCatalogue : allowlist serveur · miniatures · coût · zone · permissions", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var grid = new GridContainer { Columns = 2 }; grid.AddThemeConstantOverride("h_separation", 8); grid.AddThemeConstantOverride("v_separation", 8); actionBox.AddChild(grid);
        foreach (var text in new[] { "Téléporter", "Examiner", "Soigner", "Posséder", "Lancer une scène", "Modifier une quête", "Contrôler rencontre", "Envoyer message" }) AddAction(grid, text);
        actionBox.AddChild(Heading("SCÈNE PRÉPARÉE", 14));
        var scene = new Label { Text = "Tempête sur la Crypte\n2 spawns · dialogue · musique · pluie\nRollback : 2 étapes", AutowrapMode = TextServer.AutowrapMode.WordSmart }; scene.AddThemeColorOverride("font_color", new("c9bddc")); actionBox.AddChild(scene);
        var policyGrid = new GridContainer { Columns = 2 }; actionBox.AddChild(policyGrid);
        policyGrid.AddChild(new Label { Text = "Session GM" });
        _sessionMinutes = new SpinBox { MinValue = 1, MaxValue = 1440, Suffix = " min", CustomMinimumSize = new(170, 32) }; policyGrid.AddChild(_sessionMinutes);
        policyGrid.AddChild(new Label { Text = "Instances max." });
        _maximumInstances = new SpinBox { MinValue = 1, MaxValue = 1024, CustomMinimumSize = new(170, 32) }; policyGrid.AddChild(_maximumInstances);
        var policyButtons = new HBoxContainer(); actionBox.AddChild(policyButtons);
        AddCommand(policyButtons, "Prévisualiser", () => SaveWorkspace(false));
        AddCommand(policyButtons, "Appliquer", () => SaveWorkspace(true));
        AddCommand(policyButtons, "Annuler", UndoWorkspace);
        AddCommand(policyButtons, "Rétablir", RedoWorkspace);
        _workspaceStatus = Status("Profil R125 prêt"); actionBox.AddChild(_workspaceStatus);
        _workspaceAudit = Status("Audit non modifié"); actionBox.AddChild(_workspaceAudit);
        actionBox.AddChild(Heading("ACTIONS SÛRES R126", 14));
        actionBox.AddChild(new Label { Text = "Téléporter · Détruire · Soigner · Limbo · Posséder\nConsentement, confirmation, preview et adaptateurs runtime", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var actionPolicyGrid = new GridContainer { Columns = 2 }; actionBox.AddChild(actionPolicyGrid);
        actionPolicyGrid.AddChild(new Label { Text = "Possession max." });
        _possessionMinutes = new SpinBox { MinValue = 1, MaxValue = 120, Suffix = " min", CustomMinimumSize = new(170, 32) }; actionPolicyGrid.AddChild(_possessionMinutes);
        var actionPolicyButtons = new HBoxContainer(); actionBox.AddChild(actionPolicyButtons);
        AddCommand(actionPolicyButtons, "Prévisualiser", () => SaveActionProfile(false));
        AddCommand(actionPolicyButtons, "Appliquer", () => SaveActionProfile(true));
        AddCommand(actionPolicyButtons, "Annuler", UndoActionProfile);
        AddCommand(actionPolicyButtons, "Rétablir", RedoActionProfile);
        _actionStatus = Status("Profil R126 prêt"); actionBox.AddChild(_actionStatus);
        _actionAudit = Status("Audit non modifié"); actionBox.AddChild(_actionAudit);
        actionBox.AddChild(Heading("AUTORITÉ & ÉCONOMIE R127", 14));
        actionBox.AddChild(new Label { Text = "Impersonation · protection · or · XP · portes", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var authorityGrid = new GridContainer { Columns = 2 }; actionBox.AddChild(authorityGrid);
        authorityGrid.AddChild(new Label { Text = "Ajustement or max." });
        _currencyCap = new SpinBox { MinValue = 1, MaxValue = 10000000, CustomMinimumSize = new(170, 32) }; authorityGrid.AddChild(_currencyCap);
        var authorityButtons = new HBoxContainer(); actionBox.AddChild(authorityButtons);
        AddCommand(authorityButtons, "Prévisualiser", () => SaveAuthorityProfile(false)); AddCommand(authorityButtons, "Appliquer", () => SaveAuthorityProfile(true)); AddCommand(authorityButtons, "Annuler", UndoAuthorityProfile); AddCommand(authorityButtons, "Rétablir", RedoAuthorityProfile);
        _authorityProfileStatus = Status("Profil R127 prêt"); actionBox.AddChild(_authorityProfileStatus); _authorityProfileAudit = Status("Audit non modifié"); actionBox.AddChild(_authorityProfileAudit);
        actionBox.AddChild(Heading("GROUPES & DÉPLACEMENTS R128", 14)); actionBox.AddChild(new Label { Text = "Triggers · sélection · mouvement · attaque · voyage joueur" });
        var groupGrid = new GridContainer { Columns = 2 }; actionBox.AddChild(groupGrid); groupGrid.AddChild(new Label { Text = "Sélection max." }); _selectionCap = new SpinBox { MinValue = 1, MaxValue = 1024, CustomMinimumSize = new(170, 32) }; groupGrid.AddChild(_selectionCap);
        var groupButtons = new HBoxContainer(); actionBox.AddChild(groupButtons); AddCommand(groupButtons, "Prévisualiser", () => SaveGroupProfile(false)); AddCommand(groupButtons, "Appliquer", () => SaveGroupProfile(true)); AddCommand(groupButtons, "Annuler", UndoGroupProfile); AddCommand(groupButtons, "Rétablir", RedoGroupProfile); _groupStatus = Status("Profil R128 prêt"); actionBox.AddChild(_groupStatus); _groupAudit = Status("Audit non modifié"); actionBox.AddChild(_groupAudit);
        actionBox.AddChild(Heading("OPÉRATIONS R129", 14)); actionBox.AddChild(new Label { Text = "Recherche · ForgeScript · variables · scènes · brouillard GM" }); var opsButtons = new HBoxContainer(); actionBox.AddChild(opsButtons); AddCommand(opsButtons, "Prévisualiser", () => SaveOperationsProfile(false)); AddCommand(opsButtons, "Appliquer", () => SaveOperationsProfile(true)); AddCommand(opsButtons, "Annuler", UndoOperationsProfile); AddCommand(opsButtons, "Rétablir", RedoOperationsProfile); _operationsProfileStatus = Status("Profil R129 prêt"); actionBox.AddChild(_operationsProfileStatus);
        actionBox.AddChild(Heading("NARRATION LIVE R130", 14)); actionBox.AddChild(new Label { Text = "Quêtes · rencontres · conséquences · spawn/IA" }); var narrativeButtons = new HBoxContainer(); actionBox.AddChild(narrativeButtons); AddCommand(narrativeButtons, "Prévisualiser", () => SaveNarrative(false)); AddCommand(narrativeButtons, "Appliquer", () => SaveNarrative(true)); AddCommand(narrativeButtons, "Annuler", UndoNarrative); AddCommand(narrativeButtons, "Rétablir", RedoNarrative); _narrativeStatus = Status("Profil R130 prêt"); actionBox.AddChild(_narrativeStatus);
        var run = new Button { Text = "▶ Exécuter le scénario GM", CustomMinimumSize = new(0, 48) }; run.AddThemeStyleboxOverride("normal", Style("d84f32", "ff8b63")); run.Pressed += RunScenario; actionBox.AddChild(run);

        var auditCard = Card(360); body.AddChild(auditCard); var auditBox = Box(auditCard, "SESSION & AUDIT");
        _session = Status("En attente du test réseau"); auditBox.AddChild(_session);
        _authority = Status("Autorité non vérifiée"); auditBox.AddChild(_authority);
        _audit = Status("Journal non vérifié"); auditBox.AddChild(_audit);
        _timeline = new RichTextLabel { BbcodeEnabled = true, SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new(0, 300), Text = "[color=#9f92b8]La chronologie des actions apparaîtra ici.[/color]" }; auditBox.AddChild(_timeline);
        var footer = new HBoxContainer(); root.AddChild(footer);
        var access = new Button { Text = "Accès & supervision R105", CustomMinimumSize = new(230, 38) };
        access.Pressed += () => _openAccess?.Invoke(); footer.AddChild(access);
        footer.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var close = new Button { Text = "Fermer", CustomMinimumSize = new(120, 38) }; close.Pressed += Hide; footer.AddChild(close);
        RefreshWorkspace();
        RefreshActionProfile();
        RefreshAuthorityProfile();
        RefreshGroupProfile();
        RefreshOperationsProfile();
        RefreshNarrative();
    }

    private void SaveWorkspace(bool apply)
    {
        var candidate = _workspace with
        {
            ServerRevision = _workspace.ServerRevision + 1,
            Provenance = "toolset:r125",
            Client = _workspace.Client with { SessionMinutes = (int)_sessionMinutes!.Value },
            Spawn = _workspace.Spawn with { MaximumInstances = (int)_maximumInstances!.Value }
        };
        var result = _workspaceHistory.Execute(new(Guid.NewGuid(), 1, "toolset:r125", _workspace.ServerRevision,
            "toolset:live-director", NetworkRole.GameMaster, apply, candidate));
        if (result.Applied) _workspace = result.Profile;
        _workspaceStatus!.Text = result.Diagnostics.Count == 0 ? $"✓ {(apply ? "Profil appliqué" : "Prévisualisation valide")} · {result.SemanticDiff}" : $"⚠ {result.Diagnostics[0].Code}";
        _workspaceAudit!.Text = result.AuditEntry;
    }

    private void UndoWorkspace()
    {
        var result = _workspaceHistory.Undo("toolset:live-director", NetworkRole.GameMaster);
        if (result.Succeeded) _workspace = result.Profile; _workspaceAudit!.Text = result.AuditEntry; RefreshWorkspace();
    }

    private void RedoWorkspace()
    {
        var result = _workspaceHistory.Redo("toolset:live-director", NetworkRole.GameMaster);
        if (result.Succeeded) _workspace = result.Profile; _workspaceAudit!.Text = result.AuditEntry; RefreshWorkspace();
    }

    private void RefreshWorkspace()
    {
        if (_sessionMinutes is null) return;
        _sessionMinutes.Value = _workspace.Client.SessionMinutes; _maximumInstances!.Value = _workspace.Spawn.MaximumInstances;
        _workspaceStatus!.Text = "✓ Client séparé, Creator, spawn, Chooser et focus caméra prêts";
        _workspaceAudit!.Text = $"actor={_workspace.AuditIdentity} · scope={_workspace.AuthorityScope} · revision={_workspace.ServerRevision}";
    }

    private void SaveActionProfile(bool apply)
    {
        var candidate = _actionProfile with { ServerRevision = _actionProfile.ServerRevision + 1, Provenance = "toolset:r126", Possession = _actionProfile.Possession with { MaximumMinutes = (int)_possessionMinutes!.Value } };
        var result = _actionHistory.Execute(new(Guid.NewGuid(), 1, "toolset:r126", _actionProfile.ServerRevision, "toolset:live-director", NetworkRole.GameMaster, apply, candidate));
        if (result.Applied) _actionProfile = result.Profile;
        _actionStatus!.Text = result.Diagnostics.Count == 0 ? $"✓ {(apply ? "Actions appliquées" : "Prévisualisation valide")} · {result.SemanticDiff}" : $"⚠ {result.Diagnostics[0].Code}";
        _actionAudit!.Text = result.AuditEntry;
    }

    private void UndoActionProfile()
    {
        var result = _actionHistory.Undo("toolset:live-director", NetworkRole.GameMaster); if (result.Succeeded) _actionProfile = result.Profile; _actionAudit!.Text = result.AuditEntry; RefreshActionProfile();
    }

    private void RedoActionProfile()
    {
        var result = _actionHistory.Redo("toolset:live-director", NetworkRole.GameMaster); if (result.Succeeded) _actionProfile = result.Profile; _actionAudit!.Text = result.AuditEntry; RefreshActionProfile();
    }

    private void RefreshActionProfile()
    {
        if (_possessionMinutes is null) return; _possessionMinutes.Value = _actionProfile.Possession.MaximumMinutes;
        _actionStatus!.Text = "✓ Téléportation, destruction, soin, limbo et possession gouvernés";
        _actionAudit!.Text = $"actor={_actionProfile.AuditIdentity} · scope={_actionProfile.AuthorityScope} · revision={_actionProfile.ServerRevision}";
    }

    private void SaveAuthorityProfile(bool apply)
    {
        var candidate = _authorityProfile with { ServerRevision = _authorityProfile.ServerRevision + 1, Provenance = "toolset:r127", MaximumCurrencyAdjustment = (long)_currencyCap!.Value };
        var r = _authorityHistory.Execute(new(Guid.NewGuid(), 1, "toolset:r127", _authorityProfile.ServerRevision, "toolset:live-director", NetworkRole.GameMaster, apply, candidate));
        if (r.Applied) _authorityProfile = r.Profile; _authorityProfileStatus!.Text = r.Diagnostics.Count == 0 ? $"✓ {(apply ? "Autorité appliquée" : "Prévisualisation valide")} · {r.SemanticDiff}" : $"⚠ {r.Diagnostics[0].Code}"; _authorityProfileAudit!.Text = r.AuditEntry;
    }
    private void UndoAuthorityProfile() { var r = _authorityHistory.Undo("toolset:live-director", NetworkRole.GameMaster); if (r.Succeeded) _authorityProfile = r.Profile; _authorityProfileAudit!.Text = r.AuditEntry; RefreshAuthorityProfile(); }
    private void RedoAuthorityProfile() { var r = _authorityHistory.Redo("toolset:live-director", NetworkRole.GameMaster); if (r.Succeeded) _authorityProfile = r.Profile; _authorityProfileAudit!.Text = r.AuditEntry; RefreshAuthorityProfile(); }
    private void RefreshAuthorityProfile() { if (_currencyCap is null) return; _currencyCap.Value = _authorityProfile.MaximumCurrencyAdjustment; _authorityProfileStatus!.Text = "✓ Impersonation, protection, économie, progression et portes gouvernées"; _authorityProfileAudit!.Text = $"actor={_authorityProfile.AuditIdentity} · scope={_authorityProfile.AuthorityScope} · revision={_authorityProfile.ServerRevision}"; }
    private void SaveGroupProfile(bool apply) { var c = _groupProfile with { ServerRevision = _groupProfile.ServerRevision + 1, Provenance = "toolset:r128", SelectionCap = (int)_selectionCap!.Value }; var r = _groupHistory.Execute(new(Guid.NewGuid(), 1, "toolset:r128", _groupProfile.ServerRevision, "toolset:live-director", NetworkRole.GameMaster, apply, c)); if (r.Applied) _groupProfile = r.Profile; _groupStatus!.Text = r.Diagnostics.Count == 0 ? $"✓ {(apply ? "Groupe appliqué" : "Prévisualisation valide")} · {r.SemanticDiff}" : $"⚠ {r.Diagnostics[0].Code}"; _groupAudit!.Text = r.AuditEntry; }
    private void UndoGroupProfile() { var r = _groupHistory.Undo("toolset:live-director", NetworkRole.GameMaster); if (r.Succeeded) _groupProfile = r.Profile; _groupAudit!.Text = r.AuditEntry; RefreshGroupProfile(); }
    private void RedoGroupProfile() { var r = _groupHistory.Redo("toolset:live-director", NetworkRole.GameMaster); if (r.Succeeded) _groupProfile = r.Profile; _groupAudit!.Text = r.AuditEntry; RefreshGroupProfile(); }
    private void RefreshGroupProfile() { if (_selectionCap is null) return; _selectionCap.Value = _groupProfile.SelectionCap; _groupStatus!.Text = "✓ Triggers, sélection, groupes et voyage joueur gouvernés"; _groupAudit!.Text = $"actor={_groupProfile.AuditIdentity} · scope={_groupProfile.AuthorityScope} · revision={_groupProfile.ServerRevision}"; }
    private void SaveOperationsProfile(bool apply) { var c = _operationsProfile with { ServerRevision = _operationsProfile.ServerRevision + 1, Provenance = "toolset:r129" }; var r = _operationsHistory.Execute(new(Guid.NewGuid(), 1, "toolset:r129", _operationsProfile.ServerRevision, "toolset:live-director", NetworkRole.GameMaster, apply, c)); if (r.Applied) _operationsProfile = r.Profile; _operationsProfileStatus!.Text = r.Diagnostics.Count == 0 ? $"✓ {(apply ? "Opérations appliquées" : "Prévisualisation valide")} · {r.SemanticDiff}" : $"⚠ {r.Diagnostics[0].Code}"; }
    private void UndoOperationsProfile() { var r = _operationsHistory.Undo("toolset:live-director", NetworkRole.GameMaster); if (r.Succeeded) _operationsProfile = r.Profile; RefreshOperationsProfile(); }
    private void RedoOperationsProfile() { var r = _operationsHistory.Redo("toolset:live-director", NetworkRole.GameMaster); if (r.Succeeded) _operationsProfile = r.Profile; RefreshOperationsProfile(); }
    private void RefreshOperationsProfile() { if (_operationsProfileStatus is null) return; _operationsProfileStatus.Text = $"✓ Recherche, scripts, variables, scènes et visibilité · revision={_operationsProfile.ServerRevision}"; }
    private void SaveNarrative(bool apply) { var c = _narrativeProfile with { ServerRevision = _narrativeProfile.ServerRevision + 1, Provenance = "toolset:r130" }; var r = _narrativeHistory.Execute(new(Guid.NewGuid(), 1, "toolset:r130", _narrativeProfile.ServerRevision, "toolset:live-director", NetworkRole.GameMaster, apply, c)); if (r.Applied) _narrativeProfile = r.Profile; _narrativeStatus!.Text = r.Diagnostics.Count == 0 ? $"✓ {(apply ? "Narration appliquée" : "Prévisualisation valide")} · {r.SemanticDiff}" : $"⚠ {r.Diagnostics[0].Code}"; }
    private void UndoNarrative() { var r = _narrativeHistory.Undo("toolset:live-director", NetworkRole.GameMaster); if (r.Succeeded) _narrativeProfile = r.Profile; RefreshNarrative(); }
    private void RedoNarrative() { var r = _narrativeHistory.Redo("toolset:live-director", NetworkRole.GameMaster); if (r.Succeeded) _narrativeProfile = r.Profile; RefreshNarrative(); }
    private void RefreshNarrative() { if (_narrativeStatus is null) return; _narrativeStatus.Text = $"✓ Quêtes et rencontres live gouvernées · revision={_narrativeProfile.ServerRevision}"; }

    private void RunScenario()
    {
        var result = LiveDirectorScenario.Run();
        SetStatus(_session!, result.Passed ? "✓ Session réseau synchronisée" : "✕ Session indisponible", result.Passed);
        SetStatus(_authority!, result.Passed ? "✓ Action GM acceptée · joueur refusé" : "✕ Autorité invalide", result.Passed);
        SetStatus(_audit!, result.Passed ? $"✓ Audit immuable · {result.AuditEntries} entrée" : "✕ Audit invalide", result.Passed);
        _timeline!.Text = $"[color=#ff9858]15:00[/color]  GM connecté à la session\n[color=#70d6a3]15:00[/color]  Gardien live créé\n[color=#70d6a3]15:00[/color]  Quête Cendres → terminée\n[color=#ff806d]15:00[/color]  Création joueur refusée\n\n[color=#9f92b8]Digest réseau[/color]\n{result.NetworkDigest}\n\n[color=#9f92b8]Hash audit[/color]\n{result.AuditHash}";
    }

    private static PanelContainer Card(float width) { var card = new PanelContainer { CustomMinimumSize = new(width, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; card.AddThemeStyleboxOverride("panel", Style("151022", "47365f")); return card; }
    private static VBoxContainer Box(Container card, string title) { var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 10); card.AddChild(box); box.AddChild(Heading(title, 14)); return box; }
    private static Label Heading(string text, int size) { var label = new Label { Text = text }; label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", new("ff9858")); return label; }
    private static Label Status(string text) { var label = new Label { Text = text }; label.AddThemeColorOverride("font_color", new("aa9bc7")); return label; }
    private static void SetStatus(Label label, string text, bool ok) { label.Text = text; label.AddThemeColorOverride("font_color", new(ok ? "70d6a3" : "ff806d")); }
    private static void AddSummary(Container owner, string title, string value) { var card = Card(0); card.CustomMinimumSize = new(0, 82); owner.AddChild(card); var box = new VBoxContainer(); card.AddChild(box); box.AddChild(Heading(title, 12)); box.AddChild(new Label { Text = value }); }
    private static void AddItem(Container owner, string title, string detail, string color) { var label = new Label { Text = $"{title}\n   {detail}" }; label.AddThemeColorOverride("font_color", new(color)); owner.AddChild(label); }
    private static void AddAction(Container owner, string text) { var button = new Button { Text = text, CustomMinimumSize = new(190, 40) }; button.AddThemeStyleboxOverride("normal", Style("251a35", "5d4777")); owner.AddChild(button); }
    private static void AddCommand(Container owner, string text, Action pressed) { var button = new Button { Text = text }; button.Pressed += pressed; owner.AddChild(button); }
    private static StyleBoxFlat Style(string bg, string border) { var style = new StyleBoxFlat { BgColor = new(bg), BorderColor = new(border), CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10, ContentMarginLeft = 16, ContentMarginTop = 14, ContentMarginRight = 16, ContentMarginBottom = 14 }; style.SetBorderWidthAll(1); return style; }
}
#endif
