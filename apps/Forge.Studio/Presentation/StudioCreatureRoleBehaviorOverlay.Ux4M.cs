using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioCreatureRoleBehaviorOverlayUx4M : Control
{
    private StudioSessionController _session;
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private StudioCreatureRoleBehaviorEvidenceSourceSnapshotUx4M? _sourceSnapshot;
    private BlueprintId? _evidenceBlueprintId;
    private bool _usesRealProfile;
    private bool _usesFallback;
    private bool _previewDryRun;
    private bool _resourcesReleased = true;
    private bool _sourceRestored = true;
    private bool _updating;
    private bool _applied;
    private string _lastFailure = "none";
    private IReadOnlyList<StudioCreatureRoleBehaviorChoiceUx4M> _choices = [];
    private StudioCreatureRoleBehaviorDraftUx4M? _draft;
    private StudioCreatureRoleBehaviorPreviewUx4M? _previewResult;

    private readonly OptionButton _creature;
    private readonly CheckBox _enabled;
    private readonly CheckBox _required;
    private readonly TabContainer _tabs;
    private readonly LineEdit _ruleset;
    private readonly TextEdit _lore;
    private readonly LineEdit _roleTags;
    private readonly TextEdit _overrides;
    private readonly LineEdit _ambientGraph;
    private readonly TextEdit _idleSets;
    private readonly TextEdit _schedules;
    private readonly TextEdit _interactions;
    private readonly CheckBox _interruptCombat;
    private readonly CheckBox _interruptConversation;
    private readonly CheckBox _interruptMovement;
    private readonly OptionButton _resumeMode;
    private readonly LineEdit _resumeDelay;
    private readonly LineEdit _autoTemplate;
    private readonly LineEdit _autoSeed;
    private readonly LineEdit _targetLevel;
    private readonly CheckBox _applyOnSave;
    private readonly CheckBox _manualLock;
    private readonly TextEdit _tracks;
    private readonly TextEdit _skills;
    private readonly TextEdit _talents;
    private readonly Label _context;
    private readonly VBoxContainer _diagnostics;
    private readonly RichTextLabel _summary;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    internal event Action? CreatureChanged;
    internal event Action<string, bool>? StatusChanged;
    internal bool IsOpen => Visible;
    internal int ChoiceCount => _choices.Count;
    internal bool HasExplicitSelection => _creature.Selected > 0 && _draft is not null;
    internal bool CanonicalPreviewReady => _previewResult?.CanApply == true;
    internal bool ShowsRoleAndLore => !string.IsNullOrWhiteSpace(_ruleset.Text) &&
                                      _lore.Text.Contains("deity", StringComparison.OrdinalIgnoreCase) &&
                                      _summary.Text.Contains("Rôle", StringComparison.OrdinalIgnoreCase) &&
                                      _summary.Text.Contains("lore", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsAmbientGraph => !string.IsNullOrWhiteSpace(_idleSets.Text) &&
                                       _summary.Text.Contains("Graphe", StringComparison.OrdinalIgnoreCase) &&
                                       _summary.Text.Contains("horaire", StringComparison.OrdinalIgnoreCase) &&
                                       _summary.Text.Contains("interaction", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsAutoLevel => !string.IsNullOrWhiteSpace(_autoTemplate.Text) &&
                                    _summary.Text.Contains("Auto-level", StringComparison.OrdinalIgnoreCase) &&
                                    _summary.Text.Contains("cible", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', VisibleText(this));
            return text.Contains("Rôle, activité et auto-level", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Divinité et informations de rôle", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Animations ambiantes", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Template de progression", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aucun acteur, animation, script", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsGuid(text) && !text.Contains(".json", StringComparison.OrdinalIgnoreCase);
        }
    }
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                           !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealProfileEvidence => _usesRealProfile;
    internal bool UsesCanonicalFallbackEvidence => _usesFallback;
    internal bool EvidencePreviewDryRun => _previewDryRun;
    internal bool EvidenceSourceUntouched => _sourceSnapshot is not null && SourceMatches(_sourceSnapshot);
    internal bool EvidenceResourcesReleased => _resourcesReleased;
    internal bool SourceSessionRestored => _sourceRestored;
    internal string LastEvidenceFailure => _lastFailure;

    internal StudioCreatureRoleBehaviorOverlayUx4M(StudioSessionController session)
    {
        _session = session;
        _sourceSession = session;
        Name = "StudioCreatureRoleBehaviorOverlayUx4M";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        var shade = new ColorRect { Color = new Color(0.012f, 0.018f, 0.03f, 0.94f) };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        var dialog = StudioStyle.Panel("CreatureRoleBehaviorDialogUx4M", StudioStyle.Chrome, 14, 2);
        dialog.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        dialog.OffsetLeft = 52;
        dialog.OffsetTop = 28;
        dialog.OffsetRight = -52;
        dialog.OffsetBottom = -28;
        AddChild(dialog);
        var root = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 7);
        dialog.AddChild(root);

        var header = new HBoxContainer();
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("RÔLE, ACTIVITÉ ET AUTO-LEVEL · UX4-M", 17,
            StudioStyle.Accent, true));
        titles.AddChild(StudioStyle.Label(
            "Divinité et informations de rôle · animations ambiantes · template de progression déterministe.",
            10, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);
        root.AddChild(header);

        var selection = new HBoxContainer();
        selection.AddChild(StudioStyle.Label("Créature", 9, StudioStyle.Muted));
        _creature = new OptionButton { CustomMinimumSize = new Vector2(390, 30) };
        selection.AddChild(_creature);
        _enabled = new CheckBox { Text = "Profil actif" };
        selection.AddChild(_enabled);
        _required = new CheckBox { Text = "Requis au runtime" };
        selection.AddChild(_required);
        _context = StudioStyle.Label("Sélection explicite requise.", 9, StudioStyle.Muted, wrap: true);
        _context.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        selection.AddChild(_context);
        root.AddChild(selection);

        var tabHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 405),
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            ClipContents = true,
        };
        root.AddChild(tabHost);
        _tabs = new TabContainer();
        _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        tabHost.AddChild(_tabs);

        var role = Tab(_tabs, "RÔLE & LORE");
        role.AddChild(StudioStyle.Label(
            "Le lore est descriptif : aucune règle mécanique implicite n’est déduite d’une divinité ou d’un métier.",
            9, StudioStyle.Muted, wrap: true));
        _ruleset = Field(role, "Template de ruleset", "forge.generic");
        _lore = Multi(role, "Champs de lore · une ligne clé=valeur", 108,
            "deity=deity.embers\noccupation=crypt-warden");
        _roleTags = Field(role, "Tags de rôle séparés par virgule", "guardian, mystic");
        _overrides = Multi(role, "Overrides explicites · une ligne clé=valeur", 76,
            "ambient.radius=4");

        var ambient = Tab(_tabs, "ANIMATIONS AMBIANTES");
        ambient.AddChild(StudioStyle.Label(
            "IDs internes stables préservés. Chaque ligne reste éditable directement et les doublons bloquent l’aperçu.",
            9, StudioStyle.Muted, wrap: true));
        _ambientGraph = Field(ambient, "Profil IA / graphe ambiant", "ambient.warden");
        _idleSets = Multi(ambient, "Idle sets · nom|animations|poids|min s|max s", 94,
            "idle.day|idle, inspect|2|2|5");
        _schedules = Multi(ambient, "Horaires · nom|début|fin|idle|waypoint|conversation", 74,
            "night-watch|1200|360|idle.night||");
        _interactions = Multi(ambient, "Interactions · nom|cible|animation|cooldown s", 74,
            "inspect-brazier|brazier|inspect|12.5");
        var interruption = new HBoxContainer();
        _interruptCombat = new CheckBox { Text = "Interrompre au combat" };
        _interruptConversation = new CheckBox { Text = "Interrompre en conversation" };
        _interruptMovement = new CheckBox { Text = "Interrompre en déplacement" };
        interruption.AddChild(_interruptCombat);
        interruption.AddChild(_interruptConversation);
        interruption.AddChild(_interruptMovement);
        ambient.AddChild(interruption);
        var resume = new HBoxContainer();
        resume.AddChild(StudioStyle.Label("Reprise", 9, StudioStyle.Muted));
        _resumeMode = new OptionButton { CustomMinimumSize = new Vector2(180, 30) };
        foreach (var mode in Enum.GetValues<AmbientResumeMode>()) _resumeMode.AddItem(HumanResume(mode));
        resume.AddChild(_resumeMode);
        _resumeDelay = Field(resume, "Délai de reprise (s)", "0.75");
        ambient.AddChild(resume);

        var autoLevel = Tab(_tabs, "AUTO-LEVEL");
        autoLevel.AddChild(StudioStyle.Label(
            "Le preview est déterministe. Appliquer sur sauvegarde peut remplacer uniquement la progression ciblée.",
            9, StudioStyle.Muted, wrap: true));
        var autoHead = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _autoTemplate = Field(autoHead, "Template de progression", "warden-balanced");
        _autoSeed = Field(autoHead, "Seed déterministe", "42");
        _targetLevel = Field(autoHead, "Niveau cible", "6");
        autoLevel.AddChild(autoHead);
        var autoFlags = new HBoxContainer();
        _applyOnSave = new CheckBox { Text = "Appliquer le résultat dans la transaction" };
        _manualLock = new CheckBox { Text = "Verrouillage manuel" };
        autoFlags.AddChild(_applyOnSave);
        autoFlags.AddChild(_manualLock);
        autoLevel.AddChild(autoFlags);
        _tracks = Multi(autoLevel, "Pistes · piste|poids", 76, "warden|2");
        _skills = Multi(autoLevel, "Compétences · compétence|un rang tous les N niveaux|max", 76,
            "awareness|2|10");
        _talents = Multi(autoLevel, "Talents · talent|niveau de déverrouillage", 64,
            "ember-stance|2");

        _diagnostics = new VBoxContainer();
        root.AddChild(_diagnostics);
        _summary = new RichTextLabel
        {
            BbcodeEnabled = true,
            CustomMinimumSize = new Vector2(0, 168),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            FitContent = false,
            SelectionEnabled = true,
        };
        root.AddChild(_summary);
        root.AddChild(StudioStyle.Label(
            "Aperçu Domain/Application uniquement. Aucun acteur, animation, script, sauvegarde automatique ou réseau n’est exécuté.",
            9, StudioStyle.Muted, wrap: true));
        var footer = new HBoxContainer();
        _preview = StudioStyle.Button("Prévisualiser", primary: true, compact: true);
        _apply = StudioStyle.Button("Appliquer", compact: true);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        {
            button.CustomMinimumSize = new Vector2(button == _preview ? 132 : 104, 34);
            footer.AddChild(button);
        }
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        root.AddChild(footer);

        ConnectEvents();
        RenderEmpty("Choisissez explicitement une créature.");
        SetEditingEnabled(false);
    }

    internal void Open()
    {
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence();
        _session = _sourceSession;
        OpenBoundSession();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        _session = _sourceSession;
        OpenBoundSession();
    }

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) &&
            _evidenceSession is null && _evidenceRoot is null) return;
        _session.CancelCreatureRoleBehaviorUx4M();
        _previewResult = null;
        _draft = null;
        Visible = false;
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence();
    }

    private void OpenBoundSession()
    {
        _session.CancelCreatureRoleBehaviorUx4M();
        _previewResult = null;
        _draft = null;
        _applied = false;
        _choices = _session.CreatureRoleBehaviorChoicesUx4M;
        _updating = true;
        _creature.Clear();
        _creature.AddItem("Choisir explicitement une créature…");
        foreach (var choice in _choices) _creature.AddItem(choice.Label);
        _creature.Select(0);
        _updating = false;
        ClearControls();
        Visible = true;
        SetEditingEnabled(false);
        _context.Text = _choices.Count == 0
            ? "État vide réel : aucune créature éditable."
            : "Sélection explicite requise · profil optionnel, progression préservée.";
        RenderEmpty(_choices.Count == 0
            ? "Aucune créature ne peut recevoir ce profil dans le module ouvert."
            : "Choisissez une créature pour éditer son rôle, ses activités et son auto-level.");
    }

    private void SelectCreature(long selected)
    {
        if (_updating) return;
        _session.CancelCreatureRoleBehaviorUx4M();
        _previewResult = null;
        _apply.Disabled = true;
        if (selected <= 0 || selected > _choices.Count)
        {
            _draft = null;
            ClearControls();
            SetEditingEnabled(false);
            RenderEmpty("Sélection explicite requise.");
            return;
        }
        var choice = _choices[(int)selected - 1];
        _draft = _session.CreateCreatureRoleBehaviorDraftUx4M(choice.BlueprintId);
        if (_draft is null)
        {
            ClearControls();
            SetEditingEnabled(false);
            RenderEmpty(choice.IsReadOnly
                ? "Créature installée en lecture seule : créez une variante projet."
                : "Le profil porte des valeurs non représentables ; corrigez-le via l’autorité canonique.");
            return;
        }
        Populate(_draft);
        SetEditingEnabled(true);
        _context.Text = $"{choice.Label} · {(choice.HasProfile ? "profil existant" : "création explicite")}.";
        RenderEmpty("Modifiez le brouillon puis prévisualisez la commande canonique.");
    }

    private void Populate(StudioCreatureRoleBehaviorDraftUx4M draft)
    {
        _updating = true;
        _enabled.ButtonPressed = draft.Enabled;
        _required.ButtonPressed = draft.RequiredAtRuntime;
        _ruleset.Text = draft.RulesetTemplateId;
        _lore.Text = draft.LoreFields;
        _roleTags.Text = draft.RoleTags;
        _overrides.Text = draft.Overrides;
        _ambientGraph.Text = draft.AmbientGraphId;
        _idleSets.Text = draft.IdleSets;
        _schedules.Text = draft.Schedules;
        _interactions.Text = draft.Interactions;
        _interruptCombat.ButtonPressed = draft.InterruptOnCombat;
        _interruptConversation.ButtonPressed = draft.InterruptOnConversation;
        _interruptMovement.ButtonPressed = draft.InterruptOnMovement;
        _resumeMode.Select((int)draft.ResumeMode);
        _resumeDelay.Text = draft.ResumeDelaySeconds;
        _autoTemplate.Text = draft.AutoLevelTemplateId;
        _autoSeed.Text = draft.AutoLevelSeed;
        _targetLevel.Text = draft.TargetLevel;
        _applyOnSave.ButtonPressed = draft.ApplyOnSave;
        _manualLock.ButtonPressed = draft.ManualLock;
        _tracks.Text = draft.TrackAllocations;
        _skills.Text = draft.SkillAllocations;
        _talents.Text = draft.TalentUnlocks;
        _updating = false;
        UpdateProfileFields();
    }

    private StudioCreatureRoleBehaviorDraftUx4M? ReadDraft() => _draft is null ? null : _draft with
    {
        Enabled = _enabled.ButtonPressed,
        RequiredAtRuntime = _required.ButtonPressed,
        RulesetTemplateId = _ruleset.Text,
        LoreFields = _lore.Text,
        RoleTags = _roleTags.Text,
        Overrides = _overrides.Text,
        AmbientGraphId = _ambientGraph.Text,
        IdleSets = _idleSets.Text,
        Schedules = _schedules.Text,
        Interactions = _interactions.Text,
        InterruptOnCombat = _interruptCombat.ButtonPressed,
        InterruptOnConversation = _interruptConversation.ButtonPressed,
        InterruptOnMovement = _interruptMovement.ButtonPressed,
        ResumeMode = (AmbientResumeMode)_resumeMode.Selected,
        ResumeDelaySeconds = _resumeDelay.Text,
        AutoLevelTemplateId = _autoTemplate.Text,
        AutoLevelSeed = _autoSeed.Text,
        TargetLevel = _targetLevel.Text,
        ApplyOnSave = _applyOnSave.ButtonPressed,
        ManualLock = _manualLock.ButtonPressed,
        TrackAllocations = _tracks.Text,
        SkillAllocations = _skills.Text,
        TalentUnlocks = _talents.Text,
    };

    private void Preview()
    {
        var draft = ReadDraft();
        if (draft is null)
        {
            SetStatus("Sélectionnez une créature avant de prévisualiser.", true);
            return;
        }
        _draft = draft;
        _previewResult = _session.PreviewCreatureRoleBehaviorUx4M(draft);
        _previewDryRun = _previewResult.Succeeded;
        RenderPreview(_previewResult, _previewDryRun);
        _apply.Disabled = !_previewResult.CanApply;
        SetStatus(_previewResult.Message, !_previewResult.Succeeded);
    }

    private void Apply()
    {
        var draft = ReadDraft();
        if (draft is null)
        {
            SetStatus("Brouillon incomplet.", true);
            return;
        }
        var result = _session.ApplyApprovedCreatureRoleBehaviorUx4M(draft);
        _applied = result.Succeeded;
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        CreatureChanged?.Invoke();
        _draft = _session.CreateCreatureRoleBehaviorDraftUx4M(draft.BlueprintId);
        if (_draft is not null) Populate(_draft);
        _previewResult = null;
        _apply.Disabled = true;
    }

    private void Undo()
    {
        var result = _session.UndoCreatureRoleBehaviorUx4M();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) Reload();
    }

    private void Redo()
    {
        var result = _session.RedoCreatureRoleBehaviorUx4M();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) Reload();
    }

    private void Save()
    {
        var result = _session.Save();
        SetStatus(result.Message, !result.Succeeded);
    }

    private void Reload()
    {
        if (_draft is null) return;
        _draft = _session.CreateCreatureRoleBehaviorDraftUx4M(_draft.BlueprintId);
        if (_draft is not null) Populate(_draft);
        _previewResult = null;
        _apply.Disabled = true;
        CreatureChanged?.Invoke();
    }

    private void RenderPreview(StudioCreatureRoleBehaviorPreviewUx4M result, bool dryRun)
    {
        foreach (var child in _diagnostics.GetChildren()) child.QueueFree();
        foreach (var diagnostic in result.Diagnostics)
            _diagnostics.AddChild(StudioStyle.Label($"{diagnostic.HumanMessage} · {diagnostic.Path}",
                9, StudioStyle.Danger, wrap: true));
        _summary.Text = $"[b]{(result.Succeeded ? "APERÇU CANONIQUE" : "APERÇU REFUSÉ")}[/b]\n" +
                        $"{result.Message}\n{result.PatchSummary}\nInverse : {result.InverseSummary}\n\n" +
                        $"{result.RoleSummary}\n{result.AmbientSummary}\n" +
                        $"Auto-level : {result.AutoLevelSummary}\n{result.RuntimeSummary}\n" +
                        $"[color={(dryRun ? "#8ce0bd" : "#ff7777")}]Aperçu sans mutation : " +
                        $"{dryRun.ToString().ToLowerInvariant()}[/color]";
    }

    private void RenderEmpty(string message)
    {
        foreach (var child in _diagnostics.GetChildren()) child.QueueFree();
        _summary.Text = message;
    }

    private void InvalidatePreview()
    {
        if (_updating) return;
        _session.CancelCreatureRoleBehaviorUx4M();
        _previewResult = null;
        _apply.Disabled = true;
    }

    private void UpdateProfileFields()
    {
        var enabled = _draft is not null && _enabled.ButtonPressed;
        _required.Disabled = !enabled;
        foreach (var field in LineFields()) field.Editable = enabled;
        foreach (var field in TextFields()) field.Editable = enabled;
        foreach (var check in ProfileChecks()) check.Disabled = !enabled;
        _resumeMode.Disabled = !enabled;
    }

    private void SetEditingEnabled(bool enabled)
    {
        _enabled.Disabled = !enabled;
        _required.Disabled = !enabled || !_enabled.ButtonPressed;
        foreach (var field in LineFields()) field.Editable = enabled && _enabled.ButtonPressed;
        foreach (var field in TextFields()) field.Editable = enabled && _enabled.ButtonPressed;
        foreach (var check in ProfileChecks()) check.Disabled = !enabled || !_enabled.ButtonPressed;
        _resumeMode.Disabled = !enabled || !_enabled.ButtonPressed;
        _preview.Disabled = !enabled;
        _apply.Disabled = !enabled || !CanonicalPreviewReady;
        _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo;
        _save.Disabled = _session.Project is null;
    }

    private void ClearControls()
    {
        _updating = true;
        _enabled.ButtonPressed = false;
        _required.ButtonPressed = false;
        foreach (var field in LineFields()) field.Text = string.Empty;
        foreach (var field in TextFields()) field.Text = string.Empty;
        foreach (var check in ProfileChecks()) check.ButtonPressed = false;
        _resumeMode.Select(0);
        _updating = false;
        UpdateProfileFields();
    }

    private void ConnectEvents()
    {
        _creature.ItemSelected += SelectCreature;
        _enabled.Toggled += _ => { UpdateProfileFields(); InvalidatePreview(); };
        _required.Toggled += _ => InvalidatePreview();
        foreach (var field in LineFields()) field.TextChanged += _ => InvalidatePreview();
        foreach (var field in TextFields()) field.TextChanged += InvalidatePreview;
        foreach (var check in ProfileChecks()) check.Toggled += _ => InvalidatePreview();
        _resumeMode.ItemSelected += _ => InvalidatePreview();
        _preview.Pressed += Preview;
        _apply.Pressed += Apply;
        _undo.Pressed += Undo;
        _redo.Pressed += Redo;
        _save.Pressed += Save;
    }

    internal bool PrepareEvidence(string mode)
    {
        _lastFailure = "none";
        _previewResult = null;
        if ((_evidenceSession is not null || _evidenceRoot is not null) && !ReleaseEvidence())
        {
            _lastFailure = "release_previous";
            return false;
        }
        if (!TryOpenEvidence())
        {
            _lastFailure = _lastFailure == "none" ? "open" : _lastFailure;
            return false;
        }
        var index = _choices.ToList().FindIndex(item => item.BlueprintId == _evidenceBlueprintId);
        if (index < 0)
        {
            _lastFailure = "fixture_creature_missing";
            Close();
            return false;
        }
        _creature.Select(index + 1);
        SelectCreature(index + 1);
        if (_draft is null)
        {
            _lastFailure = "draft";
            Close();
            return false;
        }
        _draft = StudioSessionController.ChangedCreatureRoleBehaviorDraftUx4M(_draft);
        Populate(_draft);
        _tabs.CurrentTab = mode.Contains("ambient", StringComparison.OrdinalIgnoreCase) ? 1 :
            mode.Contains("auto", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
        var project = _session.Project!;
        var history = _session.History!;
        var revision = history.State.Revision;
        var journal = history.Journal.ToArray();
        var dirty = _session.IsDirty;
        Preview();
        _previewDryRun = ReferenceEquals(project, _session.Project) && ReferenceEquals(history, _session.History) &&
                         history.State.Revision == revision && history.Journal.SequenceEqual(journal) &&
                         _session.IsDirty == dirty && EvidenceSourceUntouched;
        _summary.Text = $"[b]{(_usesRealProfile ? "PROFIL RÉEL" : "FALLBACK CANONIQUE TEMPORAIRE")}[/b] " +
                        $"· copie isolée, source strictement inchangée.\n{_summary.Text}";
        var ready = _previewDryRun && CanonicalPreviewReady && HasExplicitSelection && ShowsRoleAndLore &&
                    ShowsAmbientGraph && ShowsAutoLevel && ShowsHumanSurface && UsesIsolatedEvidence &&
                    EvidenceSourceUntouched && (_usesRealProfile ^ _usesFallback);
        if (!ready)
        {
            _lastFailure = $"ready:preview={CanonicalPreviewReady}:selection={HasExplicitSelection}:" +
                           $"role={ShowsRoleAndLore}:ambient={ShowsAmbientGraph}:auto={ShowsAutoLevel}:" +
                           $"human={ShowsHumanSurface}:isolated={UsesIsolatedEvidence}:" +
                           $"source={EvidenceSourceUntouched}:origin={_usesRealProfile ^ _usesFallback}";
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4M_EVIDENCE_FAILED failure={_lastFailure}");
            Close();
            return false;
        }
        SetStatus("Preuve UX4-M isolée prête · aucune donnée source modifiée.", false);
        return true;
    }

    internal bool RunUiTransactionEvidence()
    {
        var snapshot = CaptureSource();
        if (snapshot is null) return false;
        StudioSessionController? reopened = null;
        try
        {
            if (!PrepareEvidence("ux4-m-creature-role-behavior") || _evidenceSession is null ||
                !ReferenceEquals(_session, _evidenceSession) || !CanonicalPreviewReady) return false;
            var transaction = _session;
            var original = transaction.Project;
            if (original is null || transaction.History is null) return false;
            var originalJson = ProjectJsonSerializer.Serialize(original);
            Apply();
            var appliedProject = transaction.Project;
            var applied = _applied && appliedProject is not null &&
                          ProjectJsonSerializer.Serialize(appliedProject) != originalJson;
            var appliedJson = appliedProject is null ? string.Empty : ProjectJsonSerializer.Serialize(appliedProject);
            if (applied) Undo();
            var undone = applied && transaction.Project is not null &&
                         ProjectJsonSerializer.Serialize(transaction.Project) == originalJson;
            if (undone) Redo();
            var redone = undone && transaction.Project is not null &&
                         ProjectJsonSerializer.Serialize(transaction.Project) == appliedJson;
            var saved = redone && transaction.Save().Succeeded;
            var persisted = false;
            if (saved && transaction.Session is { } workspace)
            {
                reopened = new();
                persisted = reopened.Open(workspace.RootDirectory).Succeeded &&
                            ProjectJsonSerializer.Serialize(reopened.Project!) == appliedJson;
            }
            var sourceUntouched = SourceMatches(snapshot);
            reopened?.Close();
            reopened = null;
            Close();
            return applied && undone && redone && saved && persisted && sourceUntouched &&
                   EvidenceResourcesReleased && SourceSessionRestored;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4M_UI_TRANSACTION_FAILED type={exception.GetType().Name} " +
                        $"message={exception.Message}");
            return false;
        }
        finally
        {
            reopened?.Close();
            if (_evidenceSession is not null || _evidenceRoot is not null) Close();
            _sourceSession.RestoreCreatureRoleBehaviorApprovalUx4M(snapshot.Approval);
        }
    }

    internal void CloseEvidence() => Close();

    private bool TryOpenEvidence()
    {
        var snapshot = CaptureSource();
        var evidence = _sourceSession.CreateUx4MEvidenceProject();
        if (snapshot is null || evidence is null) return false;
        var root = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux4m-evidence-" + Guid.NewGuid().ToString("N"));
        var session = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(root, evidence.Project).Succeeded || !session.Open(root).Succeeded)
            {
                session.Close();
                _resourcesReleased = DeleteEvidenceRoot(root);
                return false;
            }
            _sourceSnapshot = snapshot;
            _evidenceSession = session;
            _evidenceRoot = root;
            _evidenceBlueprintId = evidence.BlueprintId;
            _session = session;
            _usesRealProfile = evidence.UsesRealProfile;
            _usesFallback = evidence.UsesCanonicalFallback;
            _previewDryRun = false;
            _resourcesReleased = false;
            _sourceRestored = false;
            OpenBoundSession();
            return SourceMatches(snapshot) && (_usesRealProfile ^ _usesFallback);
        }
        catch (Exception exception)
        {
            _lastFailure = "open_exception:" + exception.GetType().Name;
            session.Close();
            _resourcesReleased = DeleteEvidenceRoot(root);
            _session = _sourceSession;
            _sourceRestored = true;
            return false;
        }
    }

    private StudioCreatureRoleBehaviorEvidenceSourceSnapshotUx4M? CaptureSource()
    {
        var project = _sourceSession.Project;
        var history = _sourceSession.History;
        return project is null || history is null ? null : new(project, history, history.State.Revision,
            history.Journal.ToArray(), _sourceSession.IsDirty, history.CanUndo, history.CanRedo,
            _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(),
            _sourceSession.SelectedEntityId, _sourceSession.CaptureCreatureRoleBehaviorApprovalUx4M());
    }

    private bool SourceMatches(StudioCreatureRoleBehaviorEvidenceSourceSnapshotUx4M snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history is not null &&
               history.State.Revision == snapshot.Revision && history.Journal.SequenceEqual(snapshot.Journal) &&
               history.CanUndo == snapshot.CanUndo && history.CanRedo == snapshot.CanRedo &&
               _sourceSession.IsDirty == snapshot.IsDirty && _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureCreatureRoleBehaviorApprovalUx4M() == snapshot.Approval;
    }

    private bool ReleaseEvidence()
    {
        var session = _evidenceSession;
        var root = _evidenceRoot;
        _evidenceSession = null;
        _evidenceRoot = null;
        _sourceSnapshot = null;
        _evidenceBlueprintId = null;
        _session = _sourceSession;
        _usesRealProfile = false;
        _usesFallback = false;
        _previewDryRun = false;
        var released = true;
        try { session?.Close(); }
        catch { released = false; }
        var rootReleased = DeleteEvidenceRoot(root);
        if (!rootReleased) _evidenceRoot = root;
        _resourcesReleased = released && rootReleased;
        _sourceRestored = true;
        return _resourcesReleased;
    }

    private static bool DeleteEvidenceRoot(string? root)
    {
        if (string.IsNullOrWhiteSpace(root)) return true;
        try
        {
            var full = Path.GetFullPath(root);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) +
                       Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4m-evidence-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full) && !File.Exists(full);
        }
        catch
        {
            return false;
        }
    }

    private IEnumerable<LineEdit> LineFields()
    {
        yield return _ruleset; yield return _roleTags; yield return _ambientGraph; yield return _resumeDelay;
        yield return _autoTemplate; yield return _autoSeed; yield return _targetLevel;
    }

    private IEnumerable<TextEdit> TextFields()
    {
        yield return _lore; yield return _overrides; yield return _idleSets; yield return _schedules;
        yield return _interactions; yield return _tracks; yield return _skills; yield return _talents;
    }

    private IEnumerable<CheckBox> ProfileChecks()
    {
        yield return _interruptCombat; yield return _interruptConversation; yield return _interruptMovement;
        yield return _applyOnSave; yield return _manualLock;
    }

    private void SetStatus(string message, bool error) => StatusChanged?.Invoke(message, error);

    private static LineEdit Field(Control parent, string label, string placeholder)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        var field = new LineEdit
        {
            PlaceholderText = placeholder,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        box.AddChild(field);
        parent.AddChild(box);
        return field;
    }

    private static TextEdit Multi(Control parent, string label, float height, string placeholder)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted, wrap: true));
        var field = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, height),
            PlaceholderText = placeholder,
            WrapMode = TextEdit.LineWrappingMode.None,
            ScrollFitContentHeight = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        box.AddChild(field);
        parent.AddChild(box);
        return field;
    }

    private static VBoxContainer Tab(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            FollowFocus = true,
        };
        tabs.AddChild(scroll);
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 7);
        scroll.AddChild(content);
        return content;
    }

    private static string HumanResume(AmbientResumeMode mode) => mode switch
    {
        AmbientResumeMode.Resume => "Reprendre",
        AmbientResumeMode.Restart => "Redémarrer",
        AmbientResumeMode.Stop => "Arrêter",
        _ => mode.ToString(),
    };

    private static IEnumerable<string> VisibleText(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Label { Visible: true } label) yield return label.Text;
            if (child is Button { Visible: true } button) yield return button.Text;
            foreach (var value in VisibleText(child)) yield return value;
        }
    }

    private static bool ContainsGuid(string text) => System.Text.RegularExpressions.Regex.IsMatch(text,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b");
}
