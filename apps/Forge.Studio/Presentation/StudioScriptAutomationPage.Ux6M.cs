using System.Globalization;
using System.Text.RegularExpressions;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioScriptAutomationPageUx6M : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private SourceSnapshot? _sourceSnapshot;
    private bool _evidenceReleased = true;
    private bool _updating;
    private bool _explicitModule;
    private StudioUx6MAutomationDraft? _baseDraft;
    private StudioUx6MPreview? _previewResult;
    private IReadOnlyList<StudioUx6MModuleChoice> _moduleChoices = [];
    private readonly List<ScriptAutomationProfile.CameraShot> _shots = [];
    private readonly List<ScriptAutomationProfile.TagRoute> _routes = [];
    private readonly List<ScriptAutomationProfile.FeatureFlag> _flags = [];
    private readonly List<ScriptAutomationProfile.AiSetting> _aiSettings = [];
    private int _shotIndex = -1;
    private int _routeIndex = -1;
    private int _flagIndex = -1;
    private int _aiIndex = -1;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _moduleChoice;
    private readonly Label _selection;
    private readonly TabContainer _tabs;
    private readonly OptionButton _shotChoice;
    private readonly LineEdit _rig;
    private readonly LineEdit _shot;
    private readonly LineEdit _duration;
    private readonly LineEdit _blend;
    private readonly CheckBox _lockControl;
    private readonly CheckBox _skippable;
    private readonly LineEdit _abortRecovery;
    private readonly CheckBox _restoreControl;
    private readonly CheckBox _accessibilitySkip;
    private readonly OptionButton _routeChoice;
    private readonly CheckBox _tagRoutingEnabled;
    private readonly CheckBox _explicitOnly;
    private readonly LineEdit _blueprintTag;
    private readonly LineEdit _eventName;
    private readonly LineEdit _routeScript;
    private readonly CheckBox _routeEnabled;
    private readonly OptionButton _flagChoice;
    private readonly LineEdit _flagId;
    private readonly OptionButton _flagKind;
    private readonly LineEdit _flagDefault;
    private readonly OptionButton _flagScope;
    private readonly LineEdit _flagDocumentation;
    private readonly LineEdit _flagVersion;
    private readonly OptionButton _aiChoice;
    private readonly LineEdit _aiId;
    private readonly OptionButton _aiKind;
    private readonly LineEdit _aiDefault;
    private readonly LineEdit _aiAllowed;
    private readonly LineEdit _aiPreview;
    private readonly CheckBox _aiServer;
    private readonly Label _review;
    private readonly Label _runtime;
    private readonly Label _diagnostics;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? AutomationChanged;

    internal bool IsOpen => Visible;
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && ReferenceEquals(_session, _evidenceSession);
    internal bool EvidenceResourcesReleased => _evidenceReleased;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool HasExplicitSelection => _explicitModule && _baseDraft is not null;
    internal bool PreviewReady => _previewResult is { Succeeded: true, CanApply: true, DryRunPure: true };
    internal bool RuntimeReady => _previewResult?.Resolution is
    {
        CameraSequencePlanned: true,
        CameraControlRestored: true,
        TagRouteResolved: true,
        ModuleFlagResolved: true,
        AiSettingResolved: true,
        InvalidAiFallback: true,
    };
    internal bool EvidenceSourceUntouched => _sourceSnapshot is not null && SourceMatches(_sourceSnapshot);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantText(this));
            return text.Contains("Caméra & cutscenes", StringComparison.Ordinal) &&
                   text.Contains("Routage par tags", StringComparison.Ordinal) &&
                   text.Contains("Switches module", StringComparison.Ordinal) &&
                   text.Contains("Switches IA", StringComparison.Ordinal) &&
                   text.Contains("Aperçu sec", StringComparison.Ordinal) && !ContainsUuid(text);
        }
    }

    internal StudioScriptAutomationPageUx6M(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.008f, .014f, .023f, .988f);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _dialog = StudioStyle.Panel("Ux6MScriptAutomation", StudioStyle.Surface, 12, 1);
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
        headingText.AddChild(StudioStyle.Label("Automatisation ForgeScript UX6-M", 19));
        headingText.AddChild(StudioStyle.Label(
            "Caméra & cutscenes · Routage par tags · Switches module · Switches IA · profil unique, commande canonique.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        root.AddChild(heading);

        _moduleChoice = StudioUx2EnvBControls.Options();
        _moduleChoice.CustomMinimumSize = new Vector2(640, 34);
        _moduleChoice.ItemSelected += SelectModule;
        var moduleRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        moduleRow.AddChild(Field("Module · sélection explicite", _moduleChoice, 640));
        var reload = StudioStyle.Button("Recharger", compact: true);
        reload.Pressed += RefreshModules;
        reload.CustomMinimumSize = new Vector2(125, 34);
        moduleRow.AddChild(reload);
        root.AddChild(moduleRow);
        _selection = StudioStyle.Label(
            "Sélectionnez le module. Aucun Camera3D, acteur IA, script ou réseau n’est exécuté par l’aperçu.",
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

        var left = PanelColumn(body, 1040, true);
        var tabsHost = new Control
        {
            CustomMinimumSize = new Vector2(1000, 455),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        left.AddChild(tabsHost);
        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        tabsHost.AddChild(_tabs);

        var cameraPage = TabPage(_tabs, "Caméra & cutscenes");
        _shotChoice = StudioUx2EnvBControls.Options();
        cameraPage.AddChild(CollectionToolbar("Plans ordonnés", _shotChoice, NewShot, RemoveShot,
            () => MoveShot(-1), () => MoveShot(1), SelectShot));
        _rig = Text("rig.dialogue");
        _shot = Text("shot.entry");
        cameraPage.AddChild(Pair("Rig humain", _rig, "Plan", _shot));
        _duration = Text("3");
        _blend = Text("0.5");
        cameraPage.AddChild(Pair("Durée · secondes", _duration, "Fondu · secondes", _blend));
        _lockControl = StudioUx2EnvBControls.Check("Verrouiller le contrôle joueur pendant ce plan");
        _skippable = StudioUx2EnvBControls.Check("Plan passable");
        cameraPage.AddChild(Pair("Contrôle", _lockControl, "Accessibilité du plan", _skippable));
        _abortRecovery = Text("restore-camera-and-control");
        cameraPage.AddChild(Field("Restauration après interruption", _abortRecovery));
        _restoreControl = StudioUx2EnvBControls.Check("Restaurer explicitement le contrôle joueur");
        _accessibilitySkip = StudioUx2EnvBControls.Check("Autoriser le skip accessible");
        cameraPage.AddChild(Pair("Fin de séquence", _restoreControl, "Accessibilité", _accessibilitySkip));
        cameraPage.AddChild(StudioStyle.Label(
            "L’aperçu résout la séquence, le verrou et la récupération en mémoire. Il ne déplace aucune caméra live.",
            9, StudioStyle.Muted, wrap: true));

        var tagPage = TabPage(_tabs, "Routage par tags");
        _tagRoutingEnabled = StudioUx2EnvBControls.Check("Routage tag/événement actif");
        _explicitOnly = StudioUx2EnvBControls.Check("Routes explicites uniquement");
        tagPage.AddChild(Pair("Activation", _tagRoutingEnabled, "Politique", _explicitOnly));
        _routeChoice = StudioUx2EnvBControls.Options();
        tagPage.AddChild(CollectionToolbar("Routes ordonnées", _routeChoice, NewRoute, RemoveRoute,
            () => MoveRoute(-1), () => MoveRoute(1), SelectRoute));
        _blueprintTag = Text("door.ash");
        _eventName = Text("opened");
        tagPage.AddChild(Pair("Tag de blueprint", _blueprintTag, "Événement exact", _eventName));
        _routeScript = Text("scripts/door_ash_opened");
        _routeEnabled = StudioUx2EnvBControls.Check("Route active");
        tagPage.AddChild(Pair("Script canonique", _routeScript, "État", _routeEnabled));
        tagPage.AddChild(StudioStyle.Label(
            "Le resolver compare tag et événement avec une égalité ordonnée et choisit la première route active.",
            9, StudioStyle.Muted, wrap: true));

        var flagPage = TabPage(_tabs, "Switches module");
        _flagChoice = StudioUx2EnvBControls.Options();
        flagPage.AddChild(CollectionToolbar("Switches typés", _flagChoice, NewFlag, RemoveFlag,
            () => MoveFlag(-1), () => MoveFlag(1), SelectFlag));
        _flagId = Text("rest.enabled");
        _flagKind = OptionsForEnum<ScriptAutomationProfile.SettingKind>();
        flagPage.AddChild(Pair("Nom stable", _flagId, "Type", _flagKind));
        _flagDefault = Text("true");
        _flagScope = OptionsForEnum<ScriptAutomationProfile.FlagScope>();
        flagPage.AddChild(Pair("Valeur par défaut", _flagDefault, "Portée", _flagScope));
        _flagDocumentation = Text("Décrivez le comportement et sa migration.");
        _flagVersion = Text("1");
        flagPage.AddChild(Pair("Documentation", _flagDocumentation, "Introduit au schéma", _flagVersion));
        flagPage.AddChild(StudioStyle.Label(
            "Booléen, entier, nombre, texte ou choix restent persistés comme valeurs typées et nommées.",
            9, StudioStyle.Muted, wrap: true));

        var aiPage = TabPage(_tabs, "Switches IA");
        _aiChoice = StudioUx2EnvBControls.Options();
        aiPage.AddChild(CollectionToolbar("Réglages IA typés", _aiChoice, NewAi, RemoveAi,
            () => MoveAi(-1), () => MoveAi(1), SelectAi));
        _aiId = Text("combat.aggression");
        _aiKind = OptionsForEnum<ScriptAutomationProfile.SettingKind>();
        aiPage.AddChild(Pair("Nom stable", _aiId, "Type", _aiKind));
        _aiDefault = Text("balanced");
        _aiAllowed = Text("passive, balanced, aggressive");
        aiPage.AddChild(Pair("Valeur par défaut", _aiDefault, "Valeurs autorisées · séparées par virgule", _aiAllowed));
        _aiPreview = Text("Aperçu borné de la sélection de cible et du risque.");
        _aiServer = StudioUx2EnvBControls.Check("Autorité serveur requise");
        aiPage.AddChild(Pair("Aperçu humain", _aiPreview, "Autorité", _aiServer));
        aiPage.AddChild(StudioStyle.Label(
            "Une valeur hors allowlist retombe sur le défaut canonique. Aucun agent IA n’est instancié.",
            9, StudioStyle.Muted, wrap: true));

        var right = PanelColumn(body, 570, false);
        right.AddChild(StudioStyle.Label("APERÇU SEC · AUTORITÉ G13-AA/AB", 10, StudioStyle.Accent));
        _review = StudioStyle.Label("Diff · aucun aperçu.", 9, StudioStyle.Muted, wrap: true);
        _review.CustomMinimumSize = new Vector2(0, 160);
        right.AddChild(_review);
        _runtime = StudioStyle.Label("Resolver · aucune simulation.", 9, StudioStyle.Muted, wrap: true);
        _runtime.CustomMinimumSize = new Vector2(0, 190);
        right.AddChild(_runtime);
        _diagnostics = StudioStyle.Label("Diagnostics · aucun.", 9, StudioStyle.Muted, wrap: true);
        _diagnostics.CustomMinimumSize = new Vector2(0, 100);
        right.AddChild(_diagnostics);

        _status = StudioStyle.Label("Sélectionnez le module pour commencer.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        var footer = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footer.AddThemeConstantOverride("separation", 8);
        _preview = ActionButton("Aperçu sec", Preview, 130);
        _apply = ActionButton("Appliquer", Apply, 120, primary: true);
        _undo = ActionButton("Annuler", Undo, 105);
        _redo = ActionButton("Rétablir", Redo, 105);
        _save = ActionButton("Enregistrer", Save, 125);
        footer.AddChild(_preview);
        footer.AddChild(_apply);
        footer.AddChild(_undo);
        footer.AddChild(_redo);
        footer.AddChild(_save);
        root.AddChild(footer);
        HookChanges();
        ResetControls();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && Visible && IsInsideTree()) UpdateBounds();
    }

    internal void Open() => OpenBound(_sourceSession, evidence: false, cancelApproval: false);

    internal void RestoreSourceSurfaceAfterSmoke() =>
        OpenBound(_sourceSession, evidence: false, cancelApproval: false);

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceRoot is null) return;
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence();
        else
        {
            _session.CancelScriptAutomationPreviewUx6M();
            _session = _sourceSession;
        }
        Visible = false;
        ResetControls();
    }

    internal void CloseEvidence()
    {
        if (_evidenceSession is null && _evidenceRoot is null) return;
        ReleaseEvidence();
        Visible = false;
        ResetControls();
    }

    internal bool PrepareEvidence(string mode)
    {
        if (!mode.StartsWith("ux6-m", StringComparison.OrdinalIgnoreCase)) return false;
        CloseEvidence();
        _sourceSnapshot = CaptureSource();
        _evidenceReleased = false;
        _evidenceRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux6-m-ui",
            Guid.NewGuid().ToString("N"));
        var evidence = _sourceSession.CreateUx6MEvidenceProject();
        if (evidence is null || !ProjectSession.Create(_evidenceRoot, evidence).Succeeded)
            return FailEvidence("La copie temporaire UX6-M n’a pas pu être créée.");
        _evidenceSession = new StudioSessionController();
        if (!_evidenceSession.Open(_evidenceRoot).Succeeded)
            return FailEvidence("La session temporaire UX6-M n’a pas pu être ouverte.");
        OpenBound(_evidenceSession, evidence: true);
        if (_moduleChoices.Count != 1) return FailEvidence("Le module temporaire est introuvable.");
        _moduleChoice.Select(1);
        SelectModule(1);
        _duration.Text = "4.25";
        _abortRecovery.Text = "restore-camera-and-control-reviewed";
        _routeScript.Text = "scripts/door_ash_opened_reviewed";
        _flagDefault.Text = "false";
        _aiDefault.Text = "aggressive";
        Preview();
        _tabs.CurrentTab = mode.Contains("tag", StringComparison.OrdinalIgnoreCase) ? 1 :
            mode.Contains("module", StringComparison.OrdinalIgnoreCase) ? 2 :
            mode.Contains("ai", StringComparison.OrdinalIgnoreCase) ? 3 : 0;
        var ready = HasExplicitSelection && PreviewReady && RuntimeReady && ShowsHumanSurface &&
                    UsesIsolatedEvidence && SourceMatches(_sourceSnapshot);
        if (!ready)
        {
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_M_EVIDENCE_FAILED " +
                        $"selection={HasExplicitSelection.ToString().ToLowerInvariant()} " +
                        $"preview={PreviewReady.ToString().ToLowerInvariant()} " +
                        $"runtime={RuntimeReady.ToString().ToLowerInvariant()} " +
                        $"human={ShowsHumanSurface.ToString().ToLowerInvariant()} " +
                        $"isolated={UsesIsolatedEvidence.ToString().ToLowerInvariant()} " +
                        $"source={SourceMatches(_sourceSnapshot).ToString().ToLowerInvariant()}");
            return FailEvidence("La surface UX6-M n’a pas satisfait tous les invariants de preuve.");
        }
        return true;
    }

    internal bool RunUiTransactionEvidence()
    {
        var snapshot = CaptureSource();
        try
        {
            if (!PrepareEvidence("ux6-m-script-automation") || _evidenceSession is null ||
                !ReferenceEquals(_session, _evidenceSession) || !PreviewReady) return false;
            var candidate = ReadDraft();
            if (candidate is null) return false;
            var applied = _session.ApplyApprovedScriptAutomationUx6M(candidate);
            if (!applied.Succeeded) return false;
            var undone = _session.UndoScriptAutomationUx6M();
            var redone = _session.RedoScriptAutomationUx6M();
            var saved = _session.SaveScriptAutomationUx6M();
            var expected = ProjectJsonSerializer.Serialize(_session.Project!);
            var reopened = new StudioSessionController();
            var persisted = saved.Succeeded && reopened.Open(_evidenceRoot!).Succeeded &&
                            reopened.Project is not null && string.Equals(
                                ProjectJsonSerializer.Serialize(reopened.Project), expected, StringComparison.Ordinal);
            reopened.Close();
            return undone.Succeeded && redone.Succeeded && persisted && SourceMatches(snapshot);
        }
        finally
        {
            CloseEvidence();
            _sourceSession.RestoreScriptAutomationApprovalUx6M(snapshot.Approval);
        }
    }

    private void OpenBound(StudioSessionController session, bool evidence, bool cancelApproval = true)
    {
        _session = session;
        if (cancelApproval) _session.CancelScriptAutomationPreviewUx6M();
        ResetControls();
        RefreshModules();
        Visible = true;
        MoveToFront();
        UpdateBounds();
        SetStatus(evidence
            ? "Copie temporaire UX6-M · le module source reste strictement inchangé."
            : "Sélectionnez le module puis éditez caméra, tags et switches.", false);
    }

    private void RefreshModules()
    {
        _moduleChoices = _session.ModuleChoicesUx6M();
        _updating = true;
        _moduleChoice.Clear();
        _moduleChoice.AddItem("Choisir explicitement le module…");
        foreach (var choice in _moduleChoices) _moduleChoice.AddItem($"{choice.Label} · {choice.Detail}");
        _moduleChoice.Select(0);
        _updating = false;
        _explicitModule = false;
        _baseDraft = null;
        ClearCollections();
        InvalidatePreview();
    }

    private void SelectModule(long selected)
    {
        if (_updating || selected <= 0 || selected > _moduleChoices.Count) return;
        var choice = _moduleChoices[(int)selected - 1];
        var draft = _session.CreateScriptAutomationDraftUx6M(choice.ModuleId);
        if (draft is null)
        {
            SetStatus("Le profil d’automatisation n’est plus disponible.", true);
            return;
        }
        _explicitModule = true;
        Populate(draft);
        _selection.Text = $"{choice.Label} · identité technique protégée · quatre collections complètes.";
        SetStatus("Profil chargé · lancez l’aperçu sec avant toute application.", false);
    }

    private void Populate(StudioUx6MAutomationDraft draft)
    {
        _baseDraft = draft;
        _shots.Clear();
        _shots.AddRange(draft.Camera.Shots);
        _routes.Clear();
        _routes.AddRange(draft.TagRouting.Routes);
        _flags.Clear();
        _flags.AddRange(draft.ModuleFlags);
        _aiSettings.Clear();
        _aiSettings.AddRange(draft.AiSettings);
        _updating = true;
        _abortRecovery.Text = draft.Camera.AbortRecovery;
        _restoreControl.ButtonPressed = draft.Camera.RestorePlayerControl;
        _accessibilitySkip.ButtonPressed = draft.Camera.AccessibilitySkipEnabled;
        _tagRoutingEnabled.ButtonPressed = draft.TagRouting.Enabled;
        _explicitOnly.ButtonPressed = draft.TagRouting.ExplicitOnly;
        _updating = false;
        RebuildShotChoices(0);
        RebuildRouteChoices(0);
        RebuildFlagChoices(0);
        RebuildAiChoices(0);
        InvalidatePreview();
    }

    private StudioUx6MAutomationDraft? ReadDraft()
    {
        if (_baseDraft is null) return null;
        CommitAll();
        return _baseDraft with
        {
            Camera = new(_shots.ToArray(), _abortRecovery.Text.Trim(), _restoreControl.ButtonPressed,
                _accessibilitySkip.ButtonPressed),
            TagRouting = new(_tagRoutingEnabled.ButtonPressed, _explicitOnly.ButtonPressed, _routes.ToArray()),
            ModuleFlags = _flags.ToArray(),
            AiSettings = _aiSettings.ToArray(),
        };
    }

    private void Preview()
    {
        var draft = ReadDraft();
        if (draft is null)
        {
            SetStatus("Sélectionnez explicitement le module.", true);
            return;
        }
        _previewResult = _session.PreviewScriptAutomationUx6M(draft);
        _review.Text = _previewResult.SemanticDiff.Count == 0
            ? "Diff · aucun changement."
            : "Diff canonique\n• " + string.Join("\n• ", _previewResult.SemanticDiff);
        _runtime.Text = _previewResult.Resolution is null
            ? "Resolver · non exécuté."
            : "Resolver G13-AA/AB\n• " + string.Join("\n• ", _previewResult.Resolution.Trace);
        _diagnostics.Text = _previewResult.Diagnostics.Count == 0
            ? "Diagnostics · aucun blocage."
            : "Diagnostics\n• " + string.Join("\n• ", _previewResult.Diagnostics.Select(item =>
                $"{item.Code} · {item.Message}"));
        SetStatus(_previewResult.Message, !_previewResult.Succeeded);
        UpdateButtons();
    }

    private void Apply()
    {
        var draft = ReadDraft();
        if (draft is null)
        {
            SetStatus("Aucun brouillon sélectionné.", true);
            return;
        }
        var result = _session.ApplyApprovedScriptAutomationUx6M(draft);
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        var refreshed = _session.CreateScriptAutomationDraftUx6M(_session.Project?.Module.Id.Value);
        if (refreshed is not null) Populate(refreshed);
        AutomationChanged?.Invoke();
    }

    private void Undo()
    {
        var result = _session.UndoScriptAutomationUx6M();
        SetStatus(result.Message, !result.Succeeded);
        ReloadAfterHistory(result.Succeeded);
    }

    private void Redo()
    {
        var result = _session.RedoScriptAutomationUx6M();
        SetStatus(result.Message, !result.Succeeded);
        ReloadAfterHistory(result.Succeeded);
    }

    private void Save()
    {
        var result = _session.SaveScriptAutomationUx6M();
        SetStatus(result.Message, !result.Succeeded);
    }

    private void ReloadAfterHistory(bool changed)
    {
        if (!changed) return;
        var refreshed = _session.CreateScriptAutomationDraftUx6M(_session.Project?.Module.Id.Value);
        if (refreshed is not null) Populate(refreshed);
        AutomationChanged?.Invoke();
    }

    private void NewShot()
    {
        CommitShot();
        _shots.Add(new(Guid.NewGuid(), "rig.dialogue", $"shot.{_shots.Count + 1}", 3, .5, true, true));
        RebuildShotChoices(_shots.Count - 1);
        InvalidatePreview();
    }

    private void RemoveShot()
    {
        if (_shotIndex < 0 || _shotIndex >= _shots.Count) return;
        _shots.RemoveAt(_shotIndex);
        RebuildShotChoices(Math.Min(_shotIndex, _shots.Count - 1));
        InvalidatePreview();
    }

    private void MoveShot(int delta)
    {
        CommitShot();
        _shotIndex = MoveItem(_shots, _shotIndex, delta);
        RebuildShotChoices(_shotIndex);
        InvalidatePreview();
    }

    private void SelectShot(long selected)
    {
        if (_updating) return;
        CommitShot();
        _shotIndex = (int)selected;
        PopulateShot();
        InvalidatePreview();
    }

    private void CommitShot()
    {
        if (_updating || _shotIndex < 0 || _shotIndex >= _shots.Count) return;
        var source = _shots[_shotIndex];
        _shots[_shotIndex] = source with
        {
            RigId = _rig.Text.Trim(),
            ShotId = _shot.Text.Trim(),
            DurationSeconds = ParseDouble(_duration.Text),
            BlendSeconds = ParseDouble(_blend.Text),
            LockPlayerControl = _lockControl.ButtonPressed,
            Skippable = _skippable.ButtonPressed,
        };
    }

    private void PopulateShot()
    {
        _updating = true;
        var item = _shotIndex >= 0 && _shotIndex < _shots.Count ? _shots[_shotIndex] : null;
        _rig.Text = item?.RigId ?? string.Empty;
        _shot.Text = item?.ShotId ?? string.Empty;
        _duration.Text = item?.DurationSeconds.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty;
        _blend.Text = item?.BlendSeconds.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty;
        _lockControl.ButtonPressed = item?.LockPlayerControl ?? false;
        _skippable.ButtonPressed = item?.Skippable ?? false;
        _updating = false;
    }

    private void RebuildShotChoices(int selected)
    {
        _updating = true;
        _shotChoice.Clear();
        foreach (var item in _shots) _shotChoice.AddItem($"{item.ShotId} · {item.RigId}");
        _shotIndex = _shots.Count == 0 ? -1 : Math.Clamp(selected, 0, _shots.Count - 1);
        if (_shotIndex >= 0) _shotChoice.Select(_shotIndex);
        _updating = false;
        PopulateShot();
    }

    private void NewRoute()
    {
        CommitRoute();
        _routes.Add(new($"tag.{_routes.Count + 1}", "used", "scripts/handler", true));
        RebuildRouteChoices(_routes.Count - 1);
        InvalidatePreview();
    }

    private void RemoveRoute()
    {
        if (_routeIndex < 0 || _routeIndex >= _routes.Count) return;
        _routes.RemoveAt(_routeIndex);
        RebuildRouteChoices(Math.Min(_routeIndex, _routes.Count - 1));
        InvalidatePreview();
    }

    private void MoveRoute(int delta)
    {
        CommitRoute();
        _routeIndex = MoveItem(_routes, _routeIndex, delta);
        RebuildRouteChoices(_routeIndex);
        InvalidatePreview();
    }

    private void SelectRoute(long selected)
    {
        if (_updating) return;
        CommitRoute();
        _routeIndex = (int)selected;
        PopulateRoute();
        InvalidatePreview();
    }

    private void CommitRoute()
    {
        if (_updating || _routeIndex < 0 || _routeIndex >= _routes.Count) return;
        _routes[_routeIndex] = new(_blueprintTag.Text.Trim(), _eventName.Text.Trim(),
            _routeScript.Text.Trim(), _routeEnabled.ButtonPressed);
    }

    private void PopulateRoute()
    {
        _updating = true;
        var item = _routeIndex >= 0 && _routeIndex < _routes.Count ? _routes[_routeIndex] : null;
        _blueprintTag.Text = item?.BlueprintTag ?? string.Empty;
        _eventName.Text = item?.Event ?? string.Empty;
        _routeScript.Text = item?.ScriptId ?? string.Empty;
        _routeEnabled.ButtonPressed = item?.Enabled ?? false;
        _updating = false;
    }

    private void RebuildRouteChoices(int selected)
    {
        _updating = true;
        _routeChoice.Clear();
        foreach (var item in _routes) _routeChoice.AddItem($"{item.BlueprintTag} · {item.Event}");
        _routeIndex = _routes.Count == 0 ? -1 : Math.Clamp(selected, 0, _routes.Count - 1);
        if (_routeIndex >= 0) _routeChoice.Select(_routeIndex);
        _updating = false;
        PopulateRoute();
    }

    private void NewFlag()
    {
        CommitFlag();
        _flags.Add(new($"module.switch.{_flags.Count + 1}", ScriptAutomationProfile.SettingKind.Boolean,
            "true", ScriptAutomationProfile.FlagScope.Module, "Documentez ce switch.", 1));
        RebuildFlagChoices(_flags.Count - 1);
        InvalidatePreview();
    }

    private void RemoveFlag()
    {
        if (_flagIndex < 0 || _flagIndex >= _flags.Count) return;
        _flags.RemoveAt(_flagIndex);
        RebuildFlagChoices(Math.Min(_flagIndex, _flags.Count - 1));
        InvalidatePreview();
    }

    private void MoveFlag(int delta)
    {
        CommitFlag();
        _flagIndex = MoveItem(_flags, _flagIndex, delta);
        RebuildFlagChoices(_flagIndex);
        InvalidatePreview();
    }

    private void SelectFlag(long selected)
    {
        if (_updating) return;
        CommitFlag();
        _flagIndex = (int)selected;
        PopulateFlag();
        InvalidatePreview();
    }

    private void CommitFlag()
    {
        if (_updating || _flagIndex < 0 || _flagIndex >= _flags.Count) return;
        _flags[_flagIndex] = new(_flagId.Text.Trim(),
            (ScriptAutomationProfile.SettingKind)_flagKind.Selected, _flagDefault.Text.Trim(),
            (ScriptAutomationProfile.FlagScope)_flagScope.Selected, _flagDocumentation.Text.Trim(),
            ParseInt(_flagVersion.Text));
    }

    private void PopulateFlag()
    {
        _updating = true;
        var item = _flagIndex >= 0 && _flagIndex < _flags.Count ? _flags[_flagIndex] : null;
        _flagId.Text = item?.Id ?? string.Empty;
        _flagKind.Select((int)(item?.Kind ?? ScriptAutomationProfile.SettingKind.Boolean));
        _flagDefault.Text = item?.DefaultValue ?? string.Empty;
        _flagScope.Select((int)(item?.Scope ?? ScriptAutomationProfile.FlagScope.Module));
        _flagDocumentation.Text = item?.Documentation ?? string.Empty;
        _flagVersion.Text = item?.IntroducedSchemaVersion.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _updating = false;
    }

    private void RebuildFlagChoices(int selected)
    {
        _updating = true;
        _flagChoice.Clear();
        foreach (var item in _flags) _flagChoice.AddItem($"{item.Id} · {item.Kind}");
        _flagIndex = _flags.Count == 0 ? -1 : Math.Clamp(selected, 0, _flags.Count - 1);
        if (_flagIndex >= 0) _flagChoice.Select(_flagIndex);
        _updating = false;
        PopulateFlag();
    }

    private void NewAi()
    {
        CommitAi();
        _aiSettings.Add(new($"ai.setting.{_aiSettings.Count + 1}", ScriptAutomationProfile.SettingKind.Choice,
            "balanced", ["passive", "balanced", "aggressive"], "Aperçu humain borné.", true));
        RebuildAiChoices(_aiSettings.Count - 1);
        InvalidatePreview();
    }

    private void RemoveAi()
    {
        if (_aiIndex < 0 || _aiIndex >= _aiSettings.Count) return;
        _aiSettings.RemoveAt(_aiIndex);
        RebuildAiChoices(Math.Min(_aiIndex, _aiSettings.Count - 1));
        InvalidatePreview();
    }

    private void MoveAi(int delta)
    {
        CommitAi();
        _aiIndex = MoveItem(_aiSettings, _aiIndex, delta);
        RebuildAiChoices(_aiIndex);
        InvalidatePreview();
    }

    private void SelectAi(long selected)
    {
        if (_updating) return;
        CommitAi();
        _aiIndex = (int)selected;
        PopulateAi();
        InvalidatePreview();
    }

    private void CommitAi()
    {
        if (_updating || _aiIndex < 0 || _aiIndex >= _aiSettings.Count) return;
        _aiSettings[_aiIndex] = new(_aiId.Text.Trim(),
            (ScriptAutomationProfile.SettingKind)_aiKind.Selected, _aiDefault.Text.Trim(),
            SplitValues(_aiAllowed.Text), _aiPreview.Text.Trim(), _aiServer.ButtonPressed);
    }

    private void PopulateAi()
    {
        _updating = true;
        var item = _aiIndex >= 0 && _aiIndex < _aiSettings.Count ? _aiSettings[_aiIndex] : null;
        _aiId.Text = item?.Id ?? string.Empty;
        _aiKind.Select((int)(item?.Kind ?? ScriptAutomationProfile.SettingKind.Boolean));
        _aiDefault.Text = item?.DefaultValue ?? string.Empty;
        _aiAllowed.Text = item is null ? string.Empty : string.Join(", ", item.AllowedValues);
        _aiPreview.Text = item?.Preview ?? string.Empty;
        _aiServer.ButtonPressed = item?.ServerAuthoritative ?? false;
        _updating = false;
    }

    private void RebuildAiChoices(int selected)
    {
        _updating = true;
        _aiChoice.Clear();
        foreach (var item in _aiSettings) _aiChoice.AddItem($"{item.Id} · {item.Kind}");
        _aiIndex = _aiSettings.Count == 0 ? -1 : Math.Clamp(selected, 0, _aiSettings.Count - 1);
        if (_aiIndex >= 0) _aiChoice.Select(_aiIndex);
        _updating = false;
        PopulateAi();
    }

    private void CommitAll()
    {
        CommitShot();
        CommitRoute();
        CommitFlag();
        CommitAi();
    }

    private void ClearCollections()
    {
        _shots.Clear();
        _routes.Clear();
        _flags.Clear();
        _aiSettings.Clear();
        RebuildShotChoices(-1);
        RebuildRouteChoices(-1);
        RebuildFlagChoices(-1);
        RebuildAiChoices(-1);
    }

    private void HookChanges()
    {
        foreach (var edit in new[] { _rig, _shot, _duration, _blend, _abortRecovery, _blueprintTag,
                     _eventName, _routeScript, _flagId, _flagDefault, _flagDocumentation, _flagVersion,
                     _aiId, _aiDefault, _aiAllowed, _aiPreview })
            edit.TextChanged += _ => InvalidatePreview();
        foreach (var check in new[] { _lockControl, _skippable, _restoreControl, _accessibilitySkip,
                     _tagRoutingEnabled, _explicitOnly, _routeEnabled, _aiServer })
            check.Toggled += _ => InvalidatePreview();
        _flagKind.ItemSelected += _ => InvalidatePreview();
        _flagScope.ItemSelected += _ => InvalidatePreview();
        _aiKind.ItemSelected += _ => InvalidatePreview();
    }

    private void InvalidatePreview()
    {
        if (_updating) return;
        _session.CancelScriptAutomationPreviewUx6M();
        _previewResult = null;
        _apply.Disabled = true;
        _review.Text = "Diff · relancez l’aperçu sec.";
        _runtime.Text = "Resolver · relancez l’aperçu sec.";
        _diagnostics.Text = "Diagnostics · en attente.";
        UpdateButtons();
    }

    private void ResetControls()
    {
        _updating = true;
        _explicitModule = false;
        _baseDraft = null;
        _previewResult = null;
        _moduleChoices = [];
        _shots.Clear();
        _routes.Clear();
        _flags.Clear();
        _aiSettings.Clear();
        _shotIndex = _routeIndex = _flagIndex = _aiIndex = -1;
        _selection.Text = "Sélectionnez explicitement le module.";
        _review.Text = "Diff · aucun aperçu.";
        _runtime.Text = "Resolver · aucune simulation.";
        _diagnostics.Text = "Diagnostics · aucun.";
        _updating = false;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        _preview.Disabled = !HasExplicitSelection;
        _apply.Disabled = _previewResult is not { Succeeded: true, CanApply: true };
        _undo.Disabled = _session.History?.CanUndo != true;
        _redo.Disabled = _session.History?.CanRedo != true;
        _save.Disabled = _session.Project is null;
    }

    private bool FailEvidence(string message)
    {
        SetStatus(message, true);
        ReleaseEvidence();
        Visible = false;
        return false;
    }

    private bool ReleaseEvidence()
    {
        _evidenceSession?.Close();
        _evidenceSession = null;
        var released = StudioSessionController.DeleteUx6FTemporaryWorkspace(_evidenceRoot);
        _evidenceRoot = null;
        _session = _sourceSession;
        _evidenceReleased = released;
        return released;
    }

    private SourceSnapshot CaptureSource()
    {
        var history = _sourceSession.History;
        return new(_sourceSession.Project, history, history?.State.Revision,
            history?.Journal.ToArray() ?? [], _sourceSession.IsDirty, _sourceSession.ActiveAreaId,
            _sourceSession.CaptureScriptAutomationApprovalUx6M());
    }

    private bool SourceMatches(SourceSnapshot? snapshot) => snapshot is not null &&
        ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
        ReferenceEquals(snapshot.History, _sourceSession.History) &&
        _sourceSession.History?.State.Revision == snapshot.Revision &&
        (_sourceSession.History?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
        _sourceSession.IsDirty == snapshot.Dirty && _sourceSession.ActiveAreaId == snapshot.AreaId &&
        Equals(_sourceSession.CaptureScriptAutomationApprovalUx6M(), snapshot.Approval);

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Success);
        StatusChanged?.Invoke(message, error);
    }

    private void UpdateBounds()
    {
        var viewport = GetViewportRect().Size;
        var width = Math.Clamp(viewport.X - 32, 1500, 1880);
        var height = Math.Clamp(viewport.Y - 28, 820, 1050);
        _dialog.Position = new Vector2((viewport.X - width) / 2, (viewport.Y - height) / 2);
        _dialog.Size = new Vector2(width, height);
    }

    private static VBoxContainer PanelColumn(HBoxContainer host, int minimum, bool expand)
    {
        var panel = StudioStyle.Panel("Ux6MColumn", StudioStyle.SurfaceRaised, 8, 1);
        panel.CustomMinimumSize = new Vector2(minimum, 0);
        panel.SizeFlagsHorizontal = expand ? SizeFlags.ExpandFill : SizeFlags.Fill;
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
        var page = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        page.AddThemeConstantOverride("separation", 7);
        scroll.AddChild(page);
        return page;
    }

    private static HBoxContainer CollectionToolbar(string label, OptionButton choice, Action add, Action remove,
        Action up, Action down, Action<long> select)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        choice.CustomMinimumSize = new Vector2(360, 34);
        choice.ItemSelected += index => select(index);
        row.AddChild(Field(label, choice, 360));
        foreach (var item in new (string Label, Action Action)[]
                 { ("Ajouter", add), ("Retirer", remove), ("Monter", up), ("Descendre", down) })
        {
            var button = StudioStyle.Button(item.Label, compact: true);
            button.CustomMinimumSize = new Vector2(95, 34);
            button.Pressed += item.Action;
            row.AddChild(button);
        }
        return row;
    }

    private static HBoxContainer Pair(string leftLabel, Control left, string rightLabel, Control right)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(Field(leftLabel, left));
        row.AddChild(Field(rightLabel, right));
        return row;
    }

    private static VBoxContainer Field(string label, Control control, int minimum = 220)
    {
        var field = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(minimum, 0),
        };
        field.AddThemeConstantOverride("separation", 3);
        field.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        field.AddChild(control);
        return field;
    }

    private static LineEdit Text(string value)
    {
        var edit = StudioUx2EnvBControls.Text();
        edit.Text = value;
        return edit;
    }

    private static OptionButton OptionsForEnum<T>() where T : struct, Enum
    {
        var option = StudioUx2EnvBControls.Options();
        foreach (var value in Enum.GetValues<T>()) option.AddItem(HumanEnum(value.ToString()));
        return option;
    }

    private static Button ActionButton(string text, Action action, int width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 35);
        button.Pressed += action;
        return button;
    }

    private static int MoveItem<T>(List<T> items, int index, int delta)
    {
        if (index < 0 || index >= items.Count) return index;
        var target = Math.Clamp(index + delta, 0, items.Count - 1);
        if (target == index) return index;
        var item = items[index];
        items.RemoveAt(index);
        items.Insert(target, item);
        return target;
    }

    private static double ParseDouble(string value) => double.TryParse(value, NumberStyles.Float,
        CultureInfo.InvariantCulture, out var parsed) ? parsed : double.NaN;

    private static int ParseInt(string value) => int.TryParse(value, NumberStyles.Integer,
        CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

    private static IReadOnlyList<string> SplitValues(string value) => value.Split(',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToArray();

    private static string HumanEnum(string value) => Regex.Replace(value, "(?<!^)([A-Z])", " $1");

    private static IEnumerable<string> DescendantText(Node node)
    {
        if (node is Label label) yield return label.Text;
        if (node is Button button) yield return button.Text;
        if (node is TabContainer tabs)
            for (var index = 0; index < tabs.GetTabCount(); index++) yield return tabs.GetTabTitle(index);
        foreach (var child in node.GetChildren())
            foreach (var text in DescendantText(child)) yield return text;
    }

    private static bool ContainsUuid(string text) => Regex.IsMatch(text,
        "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}");

    private sealed record SourceSnapshot(ProjectDefinition? Project, ProjectCommandHistory? History,
        ProjectRevision? Revision, IReadOnlyList<CommandHistoryJournalEntry> Journal, bool Dirty,
        Forge.Domain.Areas.AreaId? AreaId, StudioUx6MApprovalSnapshot Approval);
}
