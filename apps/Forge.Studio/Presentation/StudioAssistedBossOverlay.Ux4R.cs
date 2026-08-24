using System.Globalization;
using Godot;
using Forge.Domain.Areas;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioAssistedBossOverlayUx4R : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private StudioBossEvidenceSourceSnapshotUx4R? _sourceSnapshot;
    private AreaId? _evidenceAreaId;
    private bool _usesRealModule;
    private bool _usesFallback;
    private bool _previewDryRun;
    private bool _resourcesReleased = true;
    private bool _sourceRestored = true;
    private bool _applied;
    private bool _updating;
    private string _lastFailure = "none";
    private IReadOnlyList<StudioBossAreaChoiceUx4R> _areas = [];
    private StudioBossDraftUx4R? _draft;
    private StudioBossPreviewUx4R? _previewResult;

    private readonly OptionButton _area;
    private readonly LineEdit _slug;
    private readonly LineEdit _displayName;
    private readonly TextEdit _synopsis;
    private readonly LineEdit _health;
    private readonly LineEdit _offense;
    private readonly LineEdit _defense;
    private readonly LineEdit _threat;
    private readonly TextEdit _abilities;
    private readonly TextEdit _phaseScripts;
    private readonly TextEdit _summons;
    private readonly TextEdit _rewards;
    private readonly LineEdit _deathScript;
    private readonly TextEdit _citations;
    private readonly TextEdit _style;
    private readonly LineEdit _author;
    private readonly LineEdit _seed;
    private readonly CheckBox _approved;
    private readonly VBoxContainer _diagnostics;
    private readonly RichTextLabel _summary;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    internal event Action? BossChanged;
    internal event Action<string, bool>? StatusChanged;
    internal bool IsOpen => Visible;
    internal int AreaChoiceCount => _areas.Count;
    internal bool HasExplicitSelection => _area.Selected > 0 && _draft is not null;
    internal bool CanonicalPreviewReady => _previewResult?.CanApply == true;
    internal bool ShowsBossPackage => _previewResult?.Proposal?.Boss.Blueprint.Combat?.Abilities.Count >= 3 &&
                                      _previewResult.Proposal.Boss.Phases.Count >= 2;
    internal bool ShowsBalance => _previewResult?.Proposal?.Balance is { Passed: true, Runs: 32 };
    internal bool ShowsApproval => _approved.ButtonPressed && _summary.Text.Contains("APPROUVÉ", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsRealScripts => Lines(_phaseScripts.Text).Count >= 2 &&
                                      Lines(_phaseScripts.Text).All(item => item.EndsWith(".forge", StringComparison.OrdinalIgnoreCase)) &&
                                      _deathScript.Text.EndsWith(".forge", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', VisibleText(this));
            return text.Contains("Création assistée de boss", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Composition et scripts", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Approbation auteur", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsGuid(text) && !text.Contains(".json", StringComparison.OrdinalIgnoreCase);
        }
    }
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealModuleEvidence => _usesRealModule;
    internal bool UsesCanonicalFallbackEvidence => _usesFallback;
    internal bool EvidencePreviewDryRun => _previewDryRun;
    internal bool EvidenceSourceUntouched => _sourceSnapshot is not null && SourceMatches(_sourceSnapshot);
    internal bool EvidenceResourcesReleased => _resourcesReleased;
    internal bool SourceSessionRestored => _sourceRestored;
    internal string LastEvidenceFailure => _lastFailure;

    internal StudioAssistedBossOverlayUx4R(StudioSessionController session)
    {
        _session = session; _sourceSession = session; Name = "StudioAssistedBossOverlayUx4R";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); Visible = false; MouseFilter = MouseFilterEnum.Stop;
        var veil = new ColorRect { Color = new Color(0, 0, 0, .72f) };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(veil);
        var dialog = StudioStyle.Panel("AssistedBossDialogUx4R", StudioStyle.Chrome, 14, 2);
        dialog.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, margin: 30); AddChild(dialog);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 8); dialog.AddChild(root);
        var header = new HBoxContainer(); var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("CRÉATION ASSISTÉE DE BOSS · UX4-R", 14, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label(
            "Plan hors ligne déterministe · aucune exécution live.",
            9, StudioStyle.Muted, wrap: true)); header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true); close.CustomMinimumSize = new Vector2(76, 34);
        close.Pressed += Close; header.AddChild(close); root.AddChild(header);
        _area = Choice(root, "Zone du boss");
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            FollowFocus = true
        };
        var form = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 7); scroll.AddChild(form); root.AddChild(scroll);
        form.AddChild(StudioStyle.Label("Identité et intention", 11, StudioStyle.Accent));
        var identity = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _slug = Field(identity, "Slug canonique", "gardienne-des-braises");
        _displayName = Field(identity, "Nom affiché", "Gardienne des Braises"); form.AddChild(identity);
        _synopsis = Multiline(form, "Synopsis", 70);
        form.AddChild(StudioStyle.Label("Statistiques", 11, StudioStyle.Accent));
        var stats = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _health = Field(stats, "Santé maximale", "480"); _offense = Field(stats, "Offense", "24");
        _defense = Field(stats, "Défense", "12"); _threat = Field(stats, "Menace", "18"); form.AddChild(stats);
        form.AddChild(StudioStyle.Label("Composition et scripts", 11, StudioStyle.Accent));
        var composition = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _abilities = Multiline(composition, "Capacités (une par ligne)", 104);
        _phaseScripts = Multiline(composition, "Scripts de phase réels (2 à 3)", 104);
        _summons = Multiline(composition, "Invocations (slugs existants)", 76);
        _rewards = Multiline(composition, "Récompenses", 76); form.AddChild(composition);
        _deathScript = Field(form, "Script de mort réel", "scripts/boss_death.forge");
        var governance = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _citations = Multiline(governance, "Citations de contexte", 70);
        _style = Multiline(governance, "Règles de style", 70); form.AddChild(governance);
        var authoring = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _author = Field(authoring, "Auteur", "studio-author"); _seed = Field(authoring, "Seed déterministe", "20260820");
        form.AddChild(authoring);
        _approved = new CheckBox { Text = "Approbation auteur explicite · autoriser le dry-run ApplyApprovedPlan" };
        form.AddChild(StudioStyle.Label(
            "La simulation équilibre 32 exécutions déterministes. Studio n’invoque aucun provider réseau, acteur, script, combat ou moteur.",
            9, StudioStyle.Muted, wrap: true));
        _diagnostics = new VBoxContainer(); form.AddChild(_diagnostics);
        root.AddChild(_approved);
        _summary = new RichTextLabel
        {
            BbcodeEnabled = true,
            CustomMinimumSize = new Vector2(0, 142),
            FitContent = false,
            SelectionEnabled = true
        }; root.AddChild(_summary);
        root.AddChild(StudioStyle.Label(
            "Prévisualiser ne modifie rien. Appliquer écrit le blueprint, le dialogue et le document runtime dans une seule commande annulable.",
            9, StudioStyle.Muted, wrap: true));
        var footer = new HBoxContainer(); _preview = StudioStyle.Button("Prévisualiser", primary: true, compact: true);
        _apply = StudioStyle.Button("Appliquer", compact: true); _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true); _save = StudioStyle.Button("Enregistrer", compact: true);
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        { button.CustomMinimumSize = new Vector2(button == _preview ? 132 : 104, 34); footer.AddChild(button); }
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill }); root.AddChild(footer);
        ConnectEvents(); ClearControls(); SetEditingEnabled(false);
    }

    internal void Open()
    { if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence(); _session = _sourceSession; OpenBound(); }
    internal void RestoreSourceSurfaceAfterSmoke() { _session = _sourceSession; OpenBound(); }
    internal void HideSourceSurfaceAfterSmoke() { Visible = false; }
    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceRoot is null && ReferenceEquals(_session, _sourceSession)) return;
        _session.CancelBossUx4R(); _draft = null; _previewResult = null; Visible = false;
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence();
    }
    private void OpenBound()
    {
        _session.CancelBossUx4R(); _areas = _session.BossAreaChoicesUx4R; _updating = true; _area.Clear();
        _area.AddItem("Choisir une zone…"); foreach (var choice in _areas) _area.AddItem(choice.Label); _area.Select(0);
        _updating = false; ClearControls(); Visible = true; SetEditingEnabled(false);
    }
    private void SelectionChanged(long _)
    {
        if (_updating) return; _session.CancelBossUx4R(); _previewResult = null; _apply.Disabled = true;
        if (_area.Selected <= 0) { _draft = null; ClearControls(); SetEditingEnabled(false); return; }
        _draft = _session.CreateBossDraftUx4R(_areas[_area.Selected - 1].AreaId);
        if (_draft is null)
        { ClearControls(); SetEditingEnabled(false); RenderEmpty("Au moins deux scripts .forge et une créature existante sont requis."); return; }
        Populate(_draft); SetEditingEnabled(true); RenderEmpty("Complétez le brief, approuvez puis prévisualisez le lot atomique.");
    }
    private void Populate(StudioBossDraftUx4R draft)
    {
        _updating = true; _slug.Text = draft.Slug; _displayName.Text = draft.DisplayName; _synopsis.Text = draft.Synopsis;
        _health.Text = draft.MaximumHealth.ToString(CultureInfo.InvariantCulture);
        _offense.Text = draft.Offense.ToString(CultureInfo.InvariantCulture);
        _defense.Text = draft.Defense.ToString(CultureInfo.InvariantCulture);
        _threat.Text = draft.ThreatRating.ToString(CultureInfo.InvariantCulture);
        _abilities.Text = string.Join('\n', draft.AbilityNames); _phaseScripts.Text = string.Join('\n', draft.PhaseScripts);
        _summons.Text = string.Join('\n', draft.SummonBlueprintSlugs); _rewards.Text = string.Join('\n', draft.RewardItemSlugs);
        _deathScript.Text = draft.DeathScript; _citations.Text = string.Join('\n', draft.ContextCitations);
        _style.Text = string.Join('\n', draft.StyleRules); _author.Text = draft.AuthorId;
        _seed.Text = draft.Seed.ToString(CultureInfo.InvariantCulture); _approved.ButtonPressed = draft.Approved; _updating = false;
    }
    private StudioBossDraftUx4R? ReadDraft()
    {
        if (_draft is null || !int.TryParse(_health.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var health) ||
            !int.TryParse(_offense.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var offense) ||
            !int.TryParse(_defense.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var defense) ||
            !double.TryParse(_threat.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var threat) ||
            !int.TryParse(_seed.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed)) return null;
        return _draft with
        {
            Slug = _slug.Text.Trim(),
            DisplayName = _displayName.Text.Trim(),
            Synopsis = _synopsis.Text.Trim(),
            MaximumHealth = health,
            Offense = offense,
            Defense = defense,
            ThreatRating = threat,
            AbilityNames = Lines(_abilities.Text),
            PhaseScripts = Lines(_phaseScripts.Text),
            SummonBlueprintSlugs = Lines(_summons.Text),
            RewardItemSlugs = Lines(_rewards.Text),
            DeathScript = _deathScript.Text.Trim(),
            ContextCitations = Lines(_citations.Text),
            StyleRules = Lines(_style.Text),
            AuthorId = _author.Text.Trim(),
            Seed = seed,
            Approved = _approved.ButtonPressed
        };
    }
    private void Preview()
    {
        var draft = ReadDraft(); if (draft is null) { SetStatus("Valeurs numériques ou brouillon invalides.", true); return; }
        _draft = draft; _previewResult = _session.PreviewBossUx4R(draft); _previewDryRun = _previewResult.Succeeded;
        RenderPreview(_previewResult); _apply.Disabled = !_previewResult.CanApply;
        SetStatus(_previewResult.Message, !_previewResult.Succeeded);
    }
    private void Apply()
    {
        var draft = ReadDraft(); if (draft is null) return; var result = _session.ApplyApprovedBossUx4R(draft);
        _applied = result.Succeeded; SetStatus(result.Message, !result.Succeeded); if (!result.Succeeded) return;
        BossChanged?.Invoke(); _previewResult = null; _apply.Disabled = true;
    }
    private void Undo() { var result = _session.UndoBossUx4R(); SetStatus(result.Message, !result.Succeeded); if (result.Succeeded) Reload(); }
    private void Redo() { var result = _session.RedoBossUx4R(); SetStatus(result.Message, !result.Succeeded); if (result.Succeeded) Reload(); }
    private void Save() { var result = _session.Save(); SetStatus(result.Message, !result.Succeeded); }
    private void Reload()
    {
        if (_draft is null) return; _draft = _session.CreateBossDraftUx4R(_draft.AreaId); if (_draft is not null) Populate(_draft);
        _previewResult = null; _apply.Disabled = true; BossChanged?.Invoke();
    }
    private void RenderPreview(StudioBossPreviewUx4R result)
    {
        foreach (var child in _diagnostics.GetChildren()) child.QueueFree();
        foreach (var diagnostic in result.Diagnostics)
            _diagnostics.AddChild(StudioStyle.Label($"{diagnostic.HumanMessage} · {diagnostic.Path}", 9, StudioStyle.Danger, wrap: true));
        var proposal = result.Proposal;
        _summary.Text = $"[b]{(result.Succeeded ? "APERÇU CANONIQUE · APPROUVÉ" : "APERÇU REFUSÉ")}[/b]\n" +
            $"{result.Message}\n{result.PatchSummary}\nInverse : {result.InverseSummary}\n\n{result.PackageSummary}\n" +
            $"Simulation : {result.BalanceSummary}\nProvider : {proposal?.ProviderKey ?? "non évalué"} · coût : {proposal?.EstimatedCostUsd ?? 0:0.0000} USD\n" +
            $"Diff : {proposal?.SemanticDiff ?? "aucun"}\n" +
            $"[color={(_previewDryRun ? "#8ce0bd" : "#ff7777")}]Aperçu sans mutation : {_previewDryRun.ToString().ToLowerInvariant()}[/color]";
    }
    private void RenderEmpty(string text)
    { foreach (var child in _diagnostics.GetChildren()) child.QueueFree(); _summary.Text = text; }
    private void ClearControls()
    {
        _updating = true; foreach (var field in new[] { _slug, _displayName, _health, _offense, _defense, _threat,
            _deathScript, _author, _seed }) field.Text = string.Empty;
        foreach (var field in new[] { _synopsis, _abilities, _phaseScripts, _summons, _rewards, _citations, _style })
            field.Text = string.Empty; _approved.ButtonPressed = false; _updating = false;
        RenderEmpty("Choisissez explicitement une zone.");
    }
    private void SetEditingEnabled(bool enabled)
    {
        _preview.Disabled = !enabled; _apply.Disabled = !enabled || !CanonicalPreviewReady; _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo; _save.Disabled = _session.Project is null;
    }
    private void InvalidatePreview()
    { if (_updating) return; _session.CancelBossUx4R(); _previewResult = null; _apply.Disabled = true; }
    private void ConnectEvents()
    {
        _area.ItemSelected += SelectionChanged; _preview.Pressed += Preview; _apply.Pressed += Apply;
        _undo.Pressed += Undo; _redo.Pressed += Redo; _save.Pressed += Save;
        foreach (var field in new[] { _slug, _displayName, _health, _offense, _defense, _threat,
            _deathScript, _author, _seed }) field.TextChanged += _ => InvalidatePreview();
        foreach (var field in new[] { _synopsis, _abilities, _phaseScripts, _summons, _rewards, _citations, _style })
            field.TextChanged += InvalidatePreview; _approved.Toggled += _ => InvalidatePreview();
    }

    internal bool PrepareEvidence(string mode)
    {
        _lastFailure = "none"; _previewResult = null;
        if ((_evidenceSession is not null || _evidenceRoot is not null) && !ReleaseEvidence()) return false;
        if (!TryOpenEvidence()) return false;
        var areaIndex = _areas.ToList().FindIndex(item => item.AreaId == _evidenceAreaId);
        if (areaIndex < 0) { _lastFailure = "area_missing"; Close(); return false; }
        _updating = true; _area.Select(areaIndex + 1); _updating = false; SelectionChanged(0);
        if (_draft is null) { _lastFailure = "draft"; Close(); return false; }
        var summon = _session.BossBlueprintChoicesUx4R.First(item => item.Creature).Slug;
        var reward = _session.BossBlueprintChoicesUx4R.FirstOrDefault(item => item.Item)?.Slug ?? "braise-royale";
        _slug.Text = "gardienne-des-braises"; _displayName.Text = "Gardienne des Braises";
        _synopsis.Text = "Une gardienne en trois temps protège le cœur de la zone.";
        _health.Text = "480"; _offense.Text = "24"; _defense.Text = "12"; _threat.Text = "18";
        _abilities.Text = "Trait de braise\nCercle de feu\nCouronne ardente";
        _phaseScripts.Text = "scripts/boss_phase_1.forge\nscripts/boss_phase_2.forge\nscripts/boss_phase_3.forge";
        _summons.Text = summon; _rewards.Text = reward; _deathScript.Text = "scripts/boss_death.forge";
        _citations.Text = "contexte-projet#gardienne"; _style.Text = "combat télégraphié\nlecture claire";
        _author.Text = "studio-author"; _seed.Text = "20260820"; _approved.ButtonPressed = true;
        var project = _session.Project!; var history = _session.History!; var revision = history.State.Revision;
        var journal = history.Journal.ToArray(); var dirty = _session.IsDirty; Preview();
        _previewDryRun = ReferenceEquals(project, _session.Project) && ReferenceEquals(history, _session.History) &&
                         history.State.Revision == revision && history.Journal.SequenceEqual(journal) &&
                         _session.IsDirty == dirty && EvidenceSourceUntouched && CanonicalPreviewReady;
        _summary.Text = $"[b]{(_usesRealModule ? "MODULE RÉEL COPIÉ" : "FALLBACK CANONIQUE TEMPORAIRE")}[/b] · " +
                        $"session isolée, source strictement inchangée.\n{_summary.Text}";
        var ready = _previewDryRun && HasExplicitSelection && ShowsBossPackage && ShowsBalance && ShowsApproval &&
                    ShowsRealScripts && ShowsHumanSurface && UsesIsolatedEvidence && EvidenceSourceUntouched &&
                    (_usesRealModule ^ _usesFallback);
        if (!ready)
        {
            _lastFailure = $"ready:preview={CanonicalPreviewReady}:selection={HasExplicitSelection}:package={ShowsBossPackage}:" +
                           $"balance={ShowsBalance}:approval={ShowsApproval}:scripts={ShowsRealScripts}:human={ShowsHumanSurface}:" +
                           $"isolated={UsesIsolatedEvidence}:source={EvidenceSourceUntouched}:origin={_usesRealModule ^ _usesFallback}";
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4R_EVIDENCE_FAILED failure={_lastFailure}"); Close(); return false;
        }
        SetStatus("Preuve UX4-R isolée prête · aucun provider ni script exécuté.", false); return true;
    }

    internal bool RunUiTransactionEvidence()
    {
        var snapshot = CaptureSource(); if (snapshot is null) return false; StudioSessionController? reopened = null;
        try
        {
            if (!PrepareEvidence("ux4-r-assisted-boss") || _evidenceSession is null ||
                !ReferenceEquals(_session, _evidenceSession) || !CanonicalPreviewReady) return false;
            var transaction = _session; var original = transaction.Project; if (original is null) return false;
            var originalJson = ProjectJsonSerializer.Serialize(original); Apply(); var appliedProject = transaction.Project;
            var applied = _applied && appliedProject is not null && ProjectJsonSerializer.Serialize(appliedProject) != originalJson;
            var appliedJson = appliedProject is null ? string.Empty : ProjectJsonSerializer.Serialize(appliedProject);
            if (applied) Undo(); var undone = applied && ProjectJsonSerializer.Serialize(transaction.Project!) == originalJson;
            if (undone) Redo(); var redone = undone && ProjectJsonSerializer.Serialize(transaction.Project!) == appliedJson;
            var saved = redone && transaction.Save().Succeeded; var persisted = false;
            if (saved && transaction.Session is { } workspace)
            {
                reopened = new(); persisted = reopened.Open(workspace.RootDirectory).Succeeded &&
                ProjectJsonSerializer.Serialize(reopened.Project!) == appliedJson;
            }
            var sourceUntouched = SourceMatches(snapshot); reopened?.Close(); reopened = null; Close();
            return applied && undone && redone && saved && persisted && sourceUntouched &&
                   EvidenceResourcesReleased && SourceSessionRestored;
        }
        catch (Exception exception)
        { GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4R_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}"); return false; }
        finally
        {
            reopened?.Close(); if (_evidenceSession is not null || _evidenceRoot is not null) Close();
            _sourceSession.RestoreBossApprovalUx4R(snapshot.Approval);
        }
    }
    internal void CloseEvidence() => Close();
    private bool TryOpenEvidence()
    {
        var snapshot = CaptureSource(); var evidence = _sourceSession.CreateUx4REvidenceProject();
        if (snapshot is null || evidence is null || _sourceSession.Session is not { } sourceWorkspace) return false;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4r-evidence-" + Guid.NewGuid().ToString("N"));
        var session = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(root, evidence.Project).Succeeded ||
                !CopyScripts(sourceWorkspace.RootDirectory, root) || !StudioSessionController.EnsureUx4REvidenceScripts(root) ||
                !session.Open(root).Succeeded)
            { session.Close(); _resourcesReleased = DeleteEvidenceRoot(root); return false; }
            _sourceSnapshot = snapshot; _evidenceSession = session; _evidenceRoot = root; _evidenceAreaId = evidence.AreaId;
            _session = session; _usesRealModule = evidence.UsesRealModule; _usesFallback = evidence.UsesCanonicalFallback;
            _previewDryRun = false; _resourcesReleased = false; _sourceRestored = false; OpenBound();
            return SourceMatches(snapshot) && (_usesRealModule ^ _usesFallback);
        }
        catch { session.Close(); _resourcesReleased = DeleteEvidenceRoot(root); _session = _sourceSession; _sourceRestored = true; return false; }
    }
    private StudioBossEvidenceSourceSnapshotUx4R? CaptureSource()
    {
        var project = _sourceSession.Project; var history = _sourceSession.History;
        return project is null || history is null ? null : new(project, history, history.State.Revision,
            history.Journal.ToArray(), _sourceSession.IsDirty, history.CanUndo, history.CanRedo,
            _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureBossApprovalUx4R());
    }
    private bool SourceMatches(StudioBossEvidenceSourceSnapshotUx4R snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) && ReferenceEquals(snapshot.History, history) &&
               history is not null && history.State.Revision == snapshot.Revision && history.Journal.SequenceEqual(snapshot.Journal) &&
               history.CanUndo == snapshot.CanUndo && history.CanRedo == snapshot.CanRedo && _sourceSession.IsDirty == snapshot.Dirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId && _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.Selection) &&
               _sourceSession.SelectedEntityId == snapshot.ActiveEntity && _sourceSession.CaptureBossApprovalUx4R() == snapshot.Approval;
    }
    private bool ReleaseEvidence()
    {
        var session = _evidenceSession; var root = _evidenceRoot; _evidenceSession = null; _evidenceRoot = null;
        _sourceSnapshot = null; _evidenceAreaId = null; _session = _sourceSession; _usesRealModule = false;
        _usesFallback = false; _previewDryRun = false; var released = true;
        try { session?.Close(); } catch { released = false; }
        released &= DeleteEvidenceRoot(root); _resourcesReleased = released; _sourceRestored = true; return released;
    }
    private static bool CopyScripts(string sourceRoot, string destinationRoot)
    {
        try
        {
            var sourceScripts = Path.Combine(sourceRoot, "scripts"); if (!Directory.Exists(sourceScripts)) return true;
            foreach (var sourcePath in Directory.EnumerateFiles(sourceScripts, "*.forge", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(sourceRoot, sourcePath); var destinationPath = Path.Combine(destinationRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!); File.Copy(sourcePath, destinationPath, true);
            }
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return false; }
    }
    private static bool DeleteEvidenceRoot(string? root)
    {
        if (string.IsNullOrWhiteSpace(root)) return true;
        try { if (Directory.Exists(root)) Directory.Delete(root, true); return !Directory.Exists(root); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return false; }
    }
    private static IReadOnlyList<string> Lines(string value) => value.Replace("\r", string.Empty, StringComparison.Ordinal)
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static OptionButton Choice(Container parent, string label)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; box.AddChild(StudioStyle.Label(label, 8, StudioStyle.Muted));
        var result = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill }; box.AddChild(result); parent.AddChild(box); return result;
    }
    private static LineEdit Field(Container parent, string label, string placeholder)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; box.AddChild(StudioStyle.Label(label, 8, StudioStyle.Muted));
        var result = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(result); parent.AddChild(box); return result;
    }
    private static TextEdit Multiline(Container parent, string label, float height)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; box.AddChild(StudioStyle.Label(label, 8, StudioStyle.Muted));
        var result = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, height),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            WrapMode = TextEdit.LineWrappingMode.Boundary
        }; box.AddChild(result); parent.AddChild(box); return result;
    }
    private static IEnumerable<string> VisibleText(Node node)
    {
        if (node is Label label && label.Visible) yield return label.Text;
        if (node is Button button && button.Visible) yield return button.Text;
        foreach (var child in node.GetChildren()) foreach (var text in VisibleText(child)) yield return text;
    }
    private static bool ContainsGuid(string value) => System.Text.RegularExpressions.Regex.IsMatch(value,
        "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}");
    private void SetStatus(string message, bool error) => StatusChanged?.Invoke(message, error);
}
