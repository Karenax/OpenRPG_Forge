using System.Globalization;
using System.Text.RegularExpressions;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Combat;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioAssistedAbilityWorkbenchUx6Z : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private SourceSnapshotUx6Z? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;
    private bool _usesRealModuleEvidence;
    private bool _usesCanonicalFallbackEvidence;
    private bool _updating;
    private bool _explicitSelection;

    private readonly OptionButton _owner;
    private readonly Label _selection;
    private readonly TabContainer _tabs;
    private readonly LineEdit _slug;
    private readonly LineEdit _displayName;
    private readonly LineEdit _resourceCost;
    private readonly LineEdit _cooldown;
    private readonly OptionButton _targeting;
    private readonly ItemList _effects;
    private readonly OptionButton _effectKind;
    private readonly LineEdit _effectMagnitude;
    private readonly Button _effectSave;
    private readonly Button _effectRemove;
    private readonly Button _effectUp;
    private readonly Button _effectDown;
    private readonly LineEdit _vfx;
    private readonly LineEdit _audio;
    private readonly OptionButton _script;
    private readonly OptionButton _item;
    private readonly LineEdit _aiTags;
    private readonly TextEdit _balanceIntent;
    private readonly TextEdit _citations;
    private readonly TextEdit _styleRules;
    private readonly LineEdit _author;
    private readonly LineEdit _seed;
    private readonly CheckBox _approval;
    private readonly VBoxContainer _tests;
    private readonly VBoxContainer _balance;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _previewButton;
    private readonly Button _applyButton;
    private readonly Button _undoButton;
    private readonly Button _redoButton;
    private readonly Button _saveButton;

    private IReadOnlyList<StudioUx6ZOwnerChoice> _owners = [];
    private IReadOnlyList<StudioUx6ZScriptChoice> _scripts = [];
    private IReadOnlyList<StudioUx6ZItemChoice> _items = [];
    private readonly List<StudioUx6ZEffectDraft> _effectDrafts = [];
    private StudioUx6ZDraft? _draft;
    private StudioUx6ZPreview? _preview;
    private bool _previewReady;
    private string? _previewFingerprint;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? AbilityChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _explicitSelection && _draft is not null && _owner.Selected > 0;
    internal bool PreviewReady => _previewReady && _preview is
    {
        Succeeded: true, CanApply: true, DryRunSucceeded: true, PermissionSatisfied: true,
        RuntimeClaimsBounded: true,
    };
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealModuleEvidence => _usesRealModuleEvidence;
    internal bool UsesCanonicalFallbackEvidence => _usesCanonicalFallbackEvidence;
    internal bool EvidenceSourceUntouched => SourceMatchesUx6Z();
    internal bool EvidenceResourcesReleased => _evidenceReleased && _evidenceSession is null && _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool ShowsThreeWorkflows => _tabs.GetTabCount() == 3 &&
                                         _tabs.GetTabTitle(0) == "Design de combat" &&
                                         _tabs.GetTabTitle(1) == "Dépendances & gouvernance" &&
                                         _tabs.GetTabTitle(2) == "Tests & équilibrage";
    internal bool ShowsFiveTests => _preview?.Proposal?.Tests.Count == 5 &&
                                    _preview.Proposal.Tests.All(item => item.Passed) &&
                                    _tests.GetChildCount() >= 5;
    internal bool ShowsBalance => _preview?.Proposal?.Balance.Passed == true &&
                                  _balance.GetChildCount() >= 3;
    internal bool ShowsGovernance => _preview?.Proposal is
    {
        ApprovedForApplication: true,
        ProviderKey: "offline-studio",
        EstimatedCostUsd: 0,
    } && _approval.ButtonPressed;
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx6Z(this)
                .Concat(Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle)));
            return text.Contains("Assistant de création de pouvoirs", StringComparison.Ordinal) &&
                   text.Contains("Design de combat", StringComparison.Ordinal) &&
                   text.Contains("Dépendances & gouvernance", StringComparison.Ordinal) &&
                   text.Contains("Tests & équilibrage", StringComparison.Ordinal) &&
                   text.Contains("Aperçu sec canonique", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun ForgeScript", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuidUx6Z(text);
        }
    }

    internal StudioAssistedAbilityWorkbenchUx6Z(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.012f, .018f, .027f, .975f);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        var dialog = StudioStyle.Panel("Ux6ZAssistedAbilityDialog", StudioStyle.Surface, 12, 1);
        dialog.ClipContents = true;
        dialog.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        dialog.OffsetLeft = 18;
        dialog.OffsetTop = 14;
        dialog.OffsetRight = -18;
        dialog.OffsetBottom = -14;
        AddChild(dialog);
        var margin = StudioStyle.Margin(14, 12, 14, 12);
        dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        headingText.AddChild(StudioStyle.Label("Assistant de création de pouvoirs", 18));
        headingText.AddChild(StudioStyle.Label(
            "Capacité canonique, cinq scénarios du résolveur de combat, équilibrage et plan approuvé · aperçu sec canonique.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        root.AddChild(heading);

        _owner = StudioUx2EnvBControls.Options();
        _owner.ItemSelected += SelectOwnerUx6Z;
        root.AddChild(StudioUx2EnvBControls.Field("Porteur de combat · sélection explicite", _owner, 720));
        _selection = StudioStyle.Label(
            "Choisissez explicitement une créature ou un joueur disposant d’un profil de combat éditable.",
            10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        var tabsHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 350),
            CustomMaximumSize = new Vector2(-1, 610),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            PropagateMaximumSize = true,
        };
        root.AddChild(tabsHost);
        _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        tabsHost.AddChild(_tabs);

        var design = ScrollTabUx6Z("Design de combat", out var designBody);
        _tabs.AddChild(design);
        _slug = StudioUx2EnvBControls.Text("elan-de-braise");
        _displayName = StudioUx2EnvBControls.Text("Élan de braise");
        designBody.AddChild(RowUx6Z(
            StudioUx2EnvBControls.Field("Clé auteur", _slug),
            StudioUx2EnvBControls.Field("Nom humain", _displayName)));
        _resourceCost = StudioUx2EnvBControls.Text("12");
        _cooldown = StudioUx2EnvBControls.Text("6");
        _targeting = StudioUx2EnvBControls.Options("Soi-même", "Allié unique", "Ennemi unique", "Zone au sol");
        designBody.AddChild(RowUx6Z(
            StudioUx2EnvBControls.Field("Coût de ressource", _resourceCost),
            StudioUx2EnvBControls.Field("Recharge en secondes", _cooldown),
            StudioUx2EnvBControls.Field("Ciblage", _targeting)));
        _effects = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 105),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        _effects.ItemSelected += SelectEffectUx6Z;
        designBody.AddChild(StudioUx2EnvBControls.Card("Ux6ZEffects", "Effets canoniques ordonnés",
            "Ajoutez jusqu’à huit effets. Le vrai CombatResolver vérifie la cible, la ressource, la recharge et leur résultat.",
            _effects));
        _effectKind = StudioUx2EnvBControls.Options("Dégâts", "Soin", "Restauration de ressource");
        _effectMagnitude = StudioUx2EnvBControls.Text("18");
        _effectSave = StudioStyle.Button("Ajouter / remplacer", compact: true);
        _effectRemove = StudioStyle.Button("Retirer", compact: true);
        _effectUp = StudioStyle.Button("Monter", compact: true);
        _effectDown = StudioStyle.Button("Descendre", compact: true);
        var effectEdit = RowUx6Z(
            StudioUx2EnvBControls.Field("Type d’effet", _effectKind),
            StudioUx2EnvBControls.Field("Magnitude entière", _effectMagnitude),
            _effectSave, _effectRemove, _effectUp, _effectDown);
        designBody.AddChild(effectEdit);
        _vfx = StudioUx2EnvBControls.Text("vfx.elan-de-braise");
        _audio = StudioUx2EnvBControls.Text("audio.elan-de-braise");
        designBody.AddChild(RowUx6Z(
            StudioUx2EnvBControls.Field("Référence VFX", _vfx),
            StudioUx2EnvBControls.Field("Référence audio", _audio)));

        var governance = ScrollTabUx6Z("Dépendances & gouvernance", out var governanceBody);
        _tabs.AddChild(governance);
        _script = StudioUx2EnvBControls.Options();
        _item = StudioUx2EnvBControls.Options();
        governanceBody.AddChild(RowUx6Z(
            StudioUx2EnvBControls.Field("ForgeScript existant", _script),
            StudioUx2EnvBControls.Field("Objet requis optionnel", _item)));
        _aiTags = StudioUx2EnvBControls.Text("offensive, finisher, telegraphiee");
        governanceBody.AddChild(StudioUx2EnvBControls.Field("Tags IA séparés par des virgules", _aiTags));
        _balanceIntent = StudioUx2EnvBControls.Lines("Intention d’équilibrage visible par l’auteur", 72);
        governanceBody.AddChild(StudioUx2EnvBControls.Field("Intention d’équilibrage", _balanceIntent));
        _citations = StudioUx2EnvBControls.Lines("Une citation de contexte par ligne", 68);
        _styleRules = StudioUx2EnvBControls.Lines("Une règle de style par ligne", 68);
        governanceBody.AddChild(RowUx6Z(
            StudioUx2EnvBControls.Field("Citations de contexte", _citations),
            StudioUx2EnvBControls.Field("Règles de style", _styleRules)));
        _author = StudioUx2EnvBControls.Text("studio-author");
        _seed = StudioUx2EnvBControls.Text("20260821");
        governanceBody.AddChild(RowUx6Z(
            StudioUx2EnvBControls.Field("Auteur", _author),
            StudioUx2EnvBControls.Field("Seed déterministe", _seed)));
        _approval = StudioUx2EnvBControls.Check("J’approuve explicitement ce plan et la permission d’application");
        governanceBody.AddChild(StudioUx2EnvBControls.Card("Ux6ZApproval", "Approbation et permission",
            "Le dry-run lie révision, transaction, plan et empreinte exacte. Toute correction invalide l’approbation.",
            _approval));
        governanceBody.AddChild(StudioStyle.Label(
            "Mode hors ligne déterministe · coût réseau 0 USD · aucun fournisseur, aucun ForgeScript, aucun objet, VFX, audio ou combat live n’est exécuté dans Studio.",
            10, StudioStyle.Muted, wrap: true));

        var review = ScrollTabUx6Z("Tests & équilibrage", out var reviewBody);
        _tabs.AddChild(review);
        var reviewColumns = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        reviewColumns.AddThemeConstantOverride("separation", 10);
        _tests = ReviewColumnUx6Z(reviewColumns, "CINQ SCÉNARIOS");
        _balance = ReviewColumnUx6Z(reviewColumns, "ÉQUILIBRAGE & GOUVERNANCE");
        reviewBody.AddChild(reviewColumns);
        var diffColumns = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        diffColumns.AddThemeConstantOverride("separation", 10);
        _diff = ReviewColumnUx6Z(diffColumns, "DIFF SÉMANTIQUE");
        _diagnostics = ReviewColumnUx6Z(diffColumns, "DIAGNOSTICS LOCALISÉS");
        reviewBody.AddChild(diffColumns);
        _summary = StudioStyle.Label("Sélectionnez un porteur puis préparez un plan.", 10, StudioStyle.Muted,
            wrap: true);
        reviewBody.AddChild(_summary);

        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 8);
        _previewButton = StudioStyle.Button("Prévisualiser", compact: true, primary: true);
        _applyButton = StudioStyle.Button("Appliquer", compact: true);
        _undoButton = StudioStyle.Button("Annuler", compact: true);
        _redoButton = StudioStyle.Button("Rétablir", compact: true);
        _saveButton = StudioStyle.Button("Enregistrer", compact: true);
        foreach (var button in new[] { _previewButton, _applyButton, _undoButton, _redoButton, _saveButton })
        {
            button.CustomMinimumSize = new Vector2(104, 34);
            actions.AddChild(button);
        }
        actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _status = StudioStyle.Label("Aucune mutation appliquée.", 10, StudioStyle.Muted, wrap: true);
        _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        actions.AddChild(_status);
        root.AddChild(actions);

        _previewButton.Pressed += PreviewUx6Z;
        _applyButton.Pressed += ApplyUx6Z;
        _undoButton.Pressed += UndoUx6Z;
        _redoButton.Pressed += RedoUx6Z;
        _saveButton.Pressed += SaveUx6Z;
        _effectSave.Pressed += SaveEffectUx6Z;
        _effectRemove.Pressed += RemoveEffectUx6Z;
        _effectUp.Pressed += () => MoveEffectUx6Z(-1);
        _effectDown.Pressed += () => MoveEffectUx6Z(1);
        WireChangesUx6Z();
        ResetControlsUx6Z();
    }

    internal void Open() => OpenBoundUx6Z(_sourceSession, evidence: false, cancelApproval: false);
    internal void RestoreSourceSurfaceAfterSmoke() =>
        OpenBoundUx6Z(_sourceSession, evidence: false, cancelApproval: false);
    internal void HideSourceSurfaceAfterSmoke()
    {
        Visible = false;
        _session = _sourceSession;
    }

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null) return;
        if (Visible) _session.CancelAbilityApprovalUx6Z();
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidenceUx6Z();
        _session = _sourceSession;
        ResetControlsUx6Z();
    }

    internal void CloseEvidence()
    {
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidenceUx6Z();
        _session = _sourceSession;
        ResetControlsUx6Z(keepReleaseState: true);
    }

    internal bool PrepareEvidence(string mode)
    {
        if (!mode.StartsWith("ux6-z", StringComparison.OrdinalIgnoreCase)) return false;
        ReleaseEvidenceUx6Z();
        _sourceSnapshot = CaptureSourceUx6Z();
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux6-z-ui-{Guid.NewGuid():N}");
        try
        {
            var evidence = _sourceSession.CreateAbilityEvidenceProjectUx6Z();
            var sourceRoot = _sourceSession.Session?.RootDirectory;
            if (evidence is null || string.IsNullOrWhiteSpace(sourceRoot) ||
                !ProjectSession.Create(root, evidence.Project).Succeeded) return false;
            var scripts = StudioSessionController.CopyAbilityEvidenceScriptsUx6Z(sourceRoot, root);
            if (!scripts.Resolved) return false;
            var isolated = new StudioSessionController();
            if (!isolated.Open(root).Succeeded)
            {
                isolated.Close();
                StudioSessionController.DeleteTemporaryAbilityUx6Z(root);
                return false;
            }
            _evidenceSession = isolated;
            _evidenceRoot = root;
            _evidenceReleased = false;
            _usesRealModuleEvidence = evidence.UsesRealModule && scripts.Real;
            _usesCanonicalFallbackEvidence = evidence.UsesCanonicalFallback || !scripts.Real;
            OpenBoundUx6Z(isolated, evidence: true, cancelApproval: true);
            var ownerIndex = _owners.ToList().FindIndex(item => item.Id == evidence.OwnerId);
            if (ownerIndex < 0) ownerIndex = 0;
            if (_owners.Count == 0) return false;
            _owner.Select(ownerIndex + 1);
            SelectOwnerUx6Z(ownerIndex + 1);
            if (_draft is null) return false;
            _draft = StudioSessionController.ChangedAbilityDraftUx6Z(_draft) with
            {
                ScriptPath = _scripts[0].RelativePath,
                RequiredItemSlug = _items.FirstOrDefault()?.Slug,
            };
            PopulateDraftUx6Z();
            _preview = _session.PreviewAssistedAbilityUx6Z(_draft);
            RenderPreviewUx6Z();
            _tabs.CurrentTab = mode.EndsWith("-tests", StringComparison.OrdinalIgnoreCase) ? 2 :
                mode.EndsWith("-governance", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            var ready = PreviewReady && ShowsThreeWorkflows && ShowsFiveTests && ShowsBalance &&
                        ShowsGovernance && ShowsHumanSurface && SourceMatchesUx6Z() &&
                        (_usesRealModuleEvidence ^ _usesCanonicalFallbackEvidence);
            if (!ready)
                GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_Z_UI_PREPARE_FAILED " +
                            $"preview={PreviewReady.ToString().ToLowerInvariant()} " +
                            $"tests={ShowsFiveTests.ToString().ToLowerInvariant()} " +
                            $"balance={ShowsBalance.ToString().ToLowerInvariant()} " +
                            $"human={ShowsHumanSurface.ToString().ToLowerInvariant()} " +
                            $"source={SourceMatchesUx6Z().ToString().ToLowerInvariant()}");
            return ready;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_Z_EVIDENCE_FAILED type={exception.GetType().Name}");
            return false;
        }
        finally
        {
            if (!PreviewReady || !SourceMatchesUx6Z())
            {
                if (_evidenceSession is not null) ReleaseEvidenceUx6Z();
                else StudioSessionController.DeleteTemporaryAbilityUx6Z(root);
            }
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        var sourceApproval = _sourceSession.CaptureAbilityApprovalUx6Z();
        try
        {
            var ready = PrepareEvidence("ux6-z-assisted-ability");
            if (!ready || _evidenceSession is null || !ReferenceEquals(_session, _evidenceSession) ||
                !PreviewReady || _draft is null || string.IsNullOrWhiteSpace(_evidenceRoot)) return false;
            var applied = _session.ApplyApprovedAbilityUx6Z(_draft);
            var undo = applied.Succeeded ? _session.UndoAbilityUx6Z() : new(false, "non exécuté");
            var redo = undo.Succeeded ? _session.RedoAbilityUx6Z() : new(false, "non exécuté");
            var saved = redo.Succeeded ? _session.SaveAbilityUx6Z() : new(false, "non exécuté");
            var expected = _session.Project is null ? string.Empty : ProjectJsonSerializer.Serialize(_session.Project);
            var reopened = new StudioSessionController();
            var roundTrip = saved.Succeeded && reopened.Open(_evidenceRoot).Succeeded &&
                            reopened.Project is not null && ProjectJsonSerializer.Serialize(reopened.Project) == expected;
            reopened.Close();
            return applied.Succeeded && undo.Succeeded && redo.Succeeded && saved.Succeeded && roundTrip &&
                   SourceMatchesUx6Z();
        }
        finally
        {
            CloseEvidence();
            _sourceSession.RestoreAbilityApprovalUx6Z(sourceApproval);
        }
    }

    private void OpenBoundUx6Z(StudioSessionController session, bool evidence, bool cancelApproval)
    {
        _session = session;
        if (cancelApproval) _session.CancelAbilityApprovalUx6Z();
        Visible = true;
        _owners = _session.AbilityOwnerChoicesUx6Z;
        _scripts = _session.AbilityScriptChoicesUx6Z;
        _items = _session.AbilityItemChoicesUx6Z;
        _updating = true;
        _owner.Clear();
        _owner.AddItem("Choisir explicitement un porteur…");
        foreach (var choice in _owners) _owner.AddItem(choice.Label);
        PopulateDependencyOptionsUx6Z();
        _updating = false;
        _draft = null;
        _explicitSelection = false;
        _selection.Text = _owners.Count == 0
            ? "État vide · aucun porteur de combat éditable. Ajoutez ou équipez d’abord une créature ou un joueur."
            : "Choisissez explicitement un porteur ; aucune capacité n’est créée avant application.";
        _selection.Modulate = _owners.Count == 0 ? StudioStyle.Danger : StudioStyle.Warning;
        ResetPreviewUx6Z();
        if (!evidence)
        {
            _usesRealModuleEvidence = false;
            _usesCanonicalFallbackEvidence = false;
        }
    }

    private void SelectOwnerUx6Z(long selected)
    {
        if (_updating) return;
        _session.CancelAbilityApprovalUx6Z();
        _explicitSelection = selected > 0 && selected <= _owners.Count;
        _draft = _explicitSelection ? _session.CreateAbilityDraftUx6Z(_owners[(int)selected - 1].Id) : null;
        if (_draft is null)
        {
            _selection.Text = "Sélection invalide ou ForgeScript manquant · aucune mutation préparée.";
            _selection.Modulate = StudioStyle.Danger;
            ResetPreviewUx6Z();
            return;
        }
        var owner = _owners[(int)selected - 1];
        _selection.Text = $"{owner.Label} · {owner.ExistingAbilities} capacité(s) existante(s) strictement préservée(s).";
        _selection.Modulate = StudioStyle.Success;
        PopulateDraftUx6Z();
        ResetPreviewUx6Z();
    }

    private void PopulateDependencyOptionsUx6Z()
    {
        _script.Clear();
        _script.AddItem("Choisir un ForgeScript existant…");
        foreach (var choice in _scripts) _script.AddItem(choice.Label);
        _item.Clear();
        _item.AddItem("Aucun objet requis");
        foreach (var choice in _items) _item.AddItem(choice.Label);
    }

    private void PopulateDraftUx6Z()
    {
        if (_draft is null) return;
        _updating = true;
        _slug.Text = _draft.Slug;
        _displayName.Text = _draft.DisplayName;
        _resourceCost.Text = _draft.ResourceCost.ToString(CultureInfo.InvariantCulture);
        _cooldown.Text = _draft.CooldownSeconds.ToString("0.########", CultureInfo.InvariantCulture);
        _targeting.Select(TargetingIndexUx6Z(_draft.Targeting));
        _effectDrafts.Clear();
        _effectDrafts.AddRange(_draft.Effects);
        RebuildEffectsUx6Z();
        _vfx.Text = _draft.VfxCueKey;
        _audio.Text = _draft.AudioCueKey;
        var scriptIndex = _scripts.ToList().FindIndex(item => item.RelativePath == _draft.ScriptPath);
        _script.Select(scriptIndex >= 0 ? scriptIndex + 1 : 0);
        var itemIndex = _items.ToList().FindIndex(item => item.Slug == _draft.RequiredItemSlug);
        _item.Select(itemIndex >= 0 ? itemIndex + 1 : 0);
        _aiTags.Text = string.Join(", ", _draft.AiTags.Order(StringComparer.Ordinal));
        _balanceIntent.Text = _draft.BalanceIntent;
        _citations.Text = string.Join('\n', _draft.ContextCitations);
        _styleRules.Text = string.Join('\n', _draft.StyleRules);
        _author.Text = _draft.AuthorId;
        _seed.Text = _draft.Seed.ToString(CultureInfo.InvariantCulture);
        _approval.ButtonPressed = _draft.Approved;
        _updating = false;
    }

    private bool TryReadDraftUx6Z(out StudioUx6ZDraft? draft, out string error)
    {
        draft = null;
        error = string.Empty;
        if (_draft is null)
        {
            error = "Sélectionnez d’abord un porteur de combat.";
            return false;
        }
        if (!int.TryParse(_resourceCost.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var resource) ||
            !double.TryParse(_cooldown.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture,
                out var cooldown) || !double.IsFinite(cooldown) ||
            !int.TryParse(_seed.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed))
        {
            error = "Coût, recharge et seed doivent être des nombres exacts.";
            return false;
        }
        if (_script.Selected <= 0 || _script.Selected > _scripts.Count)
        {
            error = "Choisissez un ForgeScript existant.";
            return false;
        }
        if (_effectDrafts.Count is < 1 or > 8)
        {
            error = "Ajoutez entre un et huit effets canoniques.";
            return false;
        }
        var tags = SplitTokensUx6Z(_aiTags.Text);
        var citations = SplitLinesUx6Z(_citations.Text);
        var styles = SplitLinesUx6Z(_styleRules.Text);
        draft = _draft with
        {
            Slug = _slug.Text.Trim(),
            DisplayName = _displayName.Text.Trim(),
            ResourceCost = resource,
            CooldownSeconds = cooldown,
            Targeting = TargetingValueUx6Z(_targeting.Selected),
            Effects = _effectDrafts.ToArray(),
            VfxCueKey = _vfx.Text.Trim(),
            AudioCueKey = _audio.Text.Trim(),
            ScriptPath = _scripts[_script.Selected - 1].RelativePath,
            RequiredItemSlug = _item.Selected > 0 && _item.Selected <= _items.Count
                ? _items[_item.Selected - 1].Slug
                : null,
            AiTags = tags,
            BalanceIntent = _balanceIntent.Text.Trim(),
            ContextCitations = citations,
            StyleRules = styles,
            AuthorId = _author.Text.Trim(),
            Seed = seed,
            Approved = _approval.ButtonPressed,
        };
        return true;
    }

    private void PreviewUx6Z()
    {
        if (!TryReadDraftUx6Z(out var current, out var error) || current is null)
        {
            SetLocalStatusUx6Z(error, true);
            return;
        }
        _draft = current;
        _preview = _session.PreviewAssistedAbilityUx6Z(current);
        RenderPreviewUx6Z();
        _tabs.CurrentTab = 2;
    }

    private void ApplyUx6Z()
    {
        if (!TryReadDraftUx6Z(out var current, out var error) || current is null || !PreviewReady ||
            _previewFingerprint != _session.AbilityDraftFingerprintUx6Z(current))
        {
            SetLocalStatusUx6Z(error.Length > 0 ? error : "Le brouillon a changé ; relancez l’aperçu.", true);
            InvalidatePreviewUx6Z();
            return;
        }
        var result = _session.ApplyApprovedAbilityUx6Z(current);
        SetLocalStatusUx6Z(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            AbilityChanged?.Invoke();
            ReloadSelectedUx6Z();
        }
    }

    private void UndoUx6Z()
    {
        var result = _session.UndoAbilityUx6Z();
        SetLocalStatusUx6Z(result.Message, !result.Succeeded);
        if (result.Succeeded) { AbilityChanged?.Invoke(); ReloadSelectedUx6Z(); }
    }

    private void RedoUx6Z()
    {
        var result = _session.RedoAbilityUx6Z();
        SetLocalStatusUx6Z(result.Message, !result.Succeeded);
        if (result.Succeeded) { AbilityChanged?.Invoke(); ReloadSelectedUx6Z(); }
    }

    private void SaveUx6Z()
    {
        var result = _session.SaveAbilityUx6Z();
        SetLocalStatusUx6Z(result.Message, !result.Succeeded);
        UpdateButtonsUx6Z();
    }

    private void ReloadSelectedUx6Z()
    {
        var selected = _owner.Selected;
        _owners = _session.AbilityOwnerChoicesUx6Z;
        if (selected > 0 && selected <= _owners.Count)
            SelectOwnerUx6Z(selected);
        else
            ResetPreviewUx6Z();
    }

    private void SaveEffectUx6Z()
    {
        if (!int.TryParse(_effectMagnitude.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var magnitude) || magnitude < 0)
        {
            SetLocalStatusUx6Z("La magnitude doit être un entier positif ou nul.", true);
            return;
        }
        var effect = new StudioUx6ZEffectDraft(EffectKindValueUx6Z(_effectKind.Selected), magnitude);
        var selected = SelectedEffectUx6Z();
        if (selected >= 0) _effectDrafts[selected] = effect;
        else if (_effectDrafts.Count < 8) _effectDrafts.Add(effect);
        else
        {
            SetLocalStatusUx6Z("Huit effets maximum.", true);
            return;
        }
        RebuildEffectsUx6Z(selected >= 0 ? selected : _effectDrafts.Count - 1);
        InvalidatePreviewUx6Z();
    }

    private void RemoveEffectUx6Z()
    {
        var selected = SelectedEffectUx6Z();
        if (selected < 0) return;
        _effectDrafts.RemoveAt(selected);
        RebuildEffectsUx6Z(Math.Min(selected, _effectDrafts.Count - 1));
        InvalidatePreviewUx6Z();
    }

    private void MoveEffectUx6Z(int delta)
    {
        var selected = SelectedEffectUx6Z();
        var target = selected + delta;
        if (selected < 0 || target < 0 || target >= _effectDrafts.Count) return;
        (_effectDrafts[selected], _effectDrafts[target]) = (_effectDrafts[target], _effectDrafts[selected]);
        RebuildEffectsUx6Z(target);
        InvalidatePreviewUx6Z();
    }

    private void SelectEffectUx6Z(long selected)
    {
        if (_updating || selected < 0 || selected >= _effectDrafts.Count) return;
        var effect = _effectDrafts[(int)selected];
        _updating = true;
        _effectKind.Select(EffectKindIndexUx6Z(effect.Kind));
        _effectMagnitude.Text = effect.Magnitude.ToString(CultureInfo.InvariantCulture);
        _updating = false;
    }

    private void RebuildEffectsUx6Z(int selected = -1)
    {
        _updating = true;
        _effects.Clear();
        for (var index = 0; index < _effectDrafts.Count; index++)
        {
            var effect = _effectDrafts[index];
            _effects.AddItem($"{index + 1}. {HumanEffectKindUx6Z(effect.Kind)} · magnitude {effect.Magnitude}");
        }
        if (selected >= 0 && selected < _effectDrafts.Count) _effects.Select(selected);
        _updating = false;
    }

    private int SelectedEffectUx6Z()
    {
        var selected = _effects.GetSelectedItems();
        return selected.Length == 0 ? -1 : selected[0];
    }

    private void RenderPreviewUx6Z()
    {
        ClearUx6Z(_tests);
        ClearUx6Z(_balance);
        ClearUx6Z(_diff);
        ClearUx6Z(_diagnostics);
        if (_preview is null)
        {
            ResetPreviewUx6Z();
            return;
        }
        if (_preview.Proposal is { } proposal)
        {
            foreach (var test in proposal.Tests)
                AddReviewUx6Z(_tests,
                    $"{HumanTestUx6Z(test.Kind)} · {(test.Passed ? "PASS" : "ÉCHEC")} · {HumanTestEvidenceUx6Z(test.Kind)}",
                    test.Passed ? StudioStyle.Success : StudioStyle.Danger);
            AddReviewUx6Z(_balance, $"Score de puissance · {proposal.Balance.PowerScore:0.###}");
            AddReviewUx6Z(_balance,
                $"Magnitude {proposal.Balance.TotalMagnitude} · coût {proposal.Balance.ResourceCost} · recharge {proposal.Balance.CooldownSeconds:0.##} s.");
            AddReviewUx6Z(_balance,
                $"Intention auteur · {proposal.Orchestration.BalanceIntent}",
                proposal.Balance.Passed ? StudioStyle.Success : StudioStyle.Danger);
            AddReviewUx6Z(_balance,
                $"Provenance {proposal.Provenance} · fournisseur hors ligne · coût {proposal.EstimatedCostUsd:0.0000} USD.");
            AddReviewUx6Z(_balance,
                $"Citations · {string.Join(" · ", proposal.Citations.Select(HumanFreeTextUx6Z))}");
        }
        foreach (var item in _preview.SemanticDiff) AddReviewUx6Z(_diff, HumanFreeTextUx6Z(item));
        foreach (var item in _preview.Diagnostics)
            AddReviewUx6Z(_diagnostics, $"{HumanCodeUx6Z(item.Code)} · {HumanFreeTextUx6Z(item.Message)}",
                StudioStyle.Danger);
        if (_preview.Diagnostics.Count == 0)
            AddReviewUx6Z(_diagnostics, "Aucun diagnostic bloquant.", StudioStyle.Success);
        _summary.Text = _preview.Message;
        _previewReady = _preview.Succeeded && _preview.CanApply && _preview.DryRunSucceeded &&
                        _preview.PermissionSatisfied && _preview.RuntimeClaimsBounded && _draft is not null;
        _previewFingerprint = _previewReady ? _session.AbilityDraftFingerprintUx6Z(_draft!) : null;
        SetLocalStatusUx6Z(_preview.Message, !_preview.Succeeded);
        UpdateButtonsUx6Z();
    }

    private void WireChangesUx6Z()
    {
        foreach (var edit in new[] { _slug, _displayName, _resourceCost, _cooldown, _effectMagnitude, _vfx, _audio,
                     _aiTags, _author, _seed })
            edit.TextChanged += _ => InvalidatePreviewUx6Z();
        foreach (var edit in new[] { _balanceIntent, _citations, _styleRules })
            edit.TextChanged += InvalidatePreviewUx6Z;
        foreach (var option in new[] { _targeting, _effectKind, _script, _item })
            option.ItemSelected += _ => InvalidatePreviewUx6Z();
        _approval.Toggled += _ => InvalidatePreviewUx6Z();
    }

    private void InvalidatePreviewUx6Z()
    {
        if (_updating) return;
        _session.CancelAbilityApprovalUx6Z();
        _previewReady = false;
        _previewFingerprint = null;
        _applyButton.Disabled = true;
        _summary.Text = "Brouillon modifié · relancez le plan et son dry-run canonique.";
    }

    private void ResetPreviewUx6Z()
    {
        _preview = null;
        _previewReady = false;
        _previewFingerprint = null;
        ClearUx6Z(_tests);
        ClearUx6Z(_balance);
        ClearUx6Z(_diff);
        ClearUx6Z(_diagnostics);
        AddReviewUx6Z(_tests, "Cinq scénarios non exécutés.", StudioStyle.Muted);
        AddReviewUx6Z(_balance, "Équilibrage non calculé.", StudioStyle.Muted);
        AddReviewUx6Z(_diff, "Aucun plan préparé.", StudioStyle.Muted);
        AddReviewUx6Z(_diagnostics, "Aucun diagnostic.", StudioStyle.Muted);
        _summary.Text = _draft is null
            ? "Sélectionnez un porteur avant de créer une capacité."
            : "Porteur chargé · configurez puis approuvez le plan.";
        UpdateButtonsUx6Z();
    }

    private void ResetControlsUx6Z(bool keepReleaseState = false)
    {
        _updating = true;
        _owner.Clear();
        _owner.AddItem("Choisir explicitement un porteur…");
        _scripts = [];
        _items = [];
        PopulateDependencyOptionsUx6Z();
        _effectDrafts.Clear();
        RebuildEffectsUx6Z();
        _slug.Text = string.Empty;
        _displayName.Text = string.Empty;
        _resourceCost.Text = string.Empty;
        _cooldown.Text = string.Empty;
        _vfx.Text = string.Empty;
        _audio.Text = string.Empty;
        _aiTags.Text = string.Empty;
        _balanceIntent.Text = string.Empty;
        _citations.Text = string.Empty;
        _styleRules.Text = string.Empty;
        _author.Text = string.Empty;
        _seed.Text = string.Empty;
        _approval.ButtonPressed = false;
        _updating = false;
        _owners = [];
        _draft = null;
        _explicitSelection = false;
        if (!keepReleaseState) _evidenceReleased = _evidenceSession is null;
        ResetPreviewUx6Z();
    }

    private void UpdateButtonsUx6Z()
    {
        _previewButton.Disabled = !HasExplicitSelection;
        _applyButton.Disabled = !PreviewReady;
        _undoButton.Disabled = _session.History?.CanUndo != true;
        _redoButton.Disabled = _session.History?.CanRedo != true;
        _saveButton.Disabled = !_session.IsDirty;
    }

    private void SetLocalStatusUx6Z(string message, bool error)
    {
        _status.Text = message;
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Success;
        StatusChanged?.Invoke(message, error);
    }

    private SourceSnapshotUx6Z CaptureSourceUx6Z()
    {
        var history = _sourceSession.History;
        return new(_sourceSession.Project, history, history?.State.Revision, history?.Journal.ToArray() ?? [],
            _sourceSession.IsDirty, history?.CanUndo, history?.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureAbilityApprovalUx6Z());
    }

    private bool SourceMatchesUx6Z()
    {
        if (_sourceSnapshot is null) return true;
        var snapshot = _sourceSnapshot;
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.Dirty && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo && _sourceSession.ActiveAreaId == snapshot.ActiveArea &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.Selection) &&
               _sourceSession.SelectedEntityId == snapshot.ActiveSelection &&
               _sourceSession.CaptureAbilityApprovalUx6Z() == snapshot.Approval;
    }

    private void ReleaseEvidenceUx6Z()
    {
        _evidenceSession?.Close();
        _evidenceSession = null;
        _evidenceReleased = string.IsNullOrWhiteSpace(_evidenceRoot) ||
                            StudioSessionController.DeleteTemporaryAbilityUx6Z(_evidenceRoot);
        _evidenceRoot = null;
        _session = _sourceSession;
    }

    private static ScrollContainer ScrollTabUx6Z(string title, out VBoxContainer body)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(body);
        return scroll;
    }

    private static HBoxContainer RowUx6Z(params Control[] controls)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 8);
        foreach (var control in controls) row.AddChild(control);
        return row;
    }

    private static VBoxContainer ReviewColumnUx6Z(HBoxContainer host, string title)
    {
        var panel = StudioStyle.Panel("Ux6Z" + title.Replace(" ", string.Empty), StudioStyle.Surface, 8, 1);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var margin = StudioStyle.Margin(9, 8, 9, 8);
        panel.AddChild(margin);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 4);
        column.AddChild(StudioStyle.Label(title, 9, StudioStyle.Accent));
        margin.AddChild(column);
        host.AddChild(panel);
        return column;
    }

    private static void AddReviewUx6Z(VBoxContainer host, string text, Color? color = null) =>
        host.AddChild(StudioStyle.Label(text, 10, color ?? StudioStyle.Text, wrap: true));

    private static void ClearUx6Z(Node host)
    {
        foreach (var child in host.GetChildren().Skip(1).ToArray())
        {
            host.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static IReadOnlySet<string> SplitTokensUx6Z(string text) =>
        text.Replace('\r', '\n').Split([',', '\n', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim()).Where(item => item.Length > 0).ToHashSet(StringComparer.Ordinal);

    private static IReadOnlyList<string> SplitLinesUx6Z(string text) =>
        text.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim()).Where(item => item.Length > 0).ToArray();

    private static int TargetingIndexUx6Z(AbilityTargeting value) => value switch
    {
        AbilityTargeting.Self => 0,
        AbilityTargeting.SingleAlly => 1,
        AbilityTargeting.SingleEnemy => 2,
        AbilityTargeting.PointArea => 3,
        _ => 2,
    };

    private static AbilityTargeting TargetingValueUx6Z(long index) => index switch
    {
        0 => AbilityTargeting.Self,
        1 => AbilityTargeting.SingleAlly,
        2 => AbilityTargeting.SingleEnemy,
        3 => AbilityTargeting.PointArea,
        _ => AbilityTargeting.SingleEnemy,
    };

    private static int EffectKindIndexUx6Z(AbilityEffectKind value) => value switch
    {
        AbilityEffectKind.Damage => 0,
        AbilityEffectKind.Heal => 1,
        AbilityEffectKind.RestoreResource => 2,
        _ => 0,
    };

    private static AbilityEffectKind EffectKindValueUx6Z(long index) => index switch
    {
        0 => AbilityEffectKind.Damage,
        1 => AbilityEffectKind.Heal,
        2 => AbilityEffectKind.RestoreResource,
        _ => AbilityEffectKind.Damage,
    };

    private static string HumanEffectKindUx6Z(AbilityEffectKind kind) => kind switch
    {
        AbilityEffectKind.Damage => "Dégâts",
        AbilityEffectKind.Heal => "Soin",
        AbilityEffectKind.RestoreResource => "Restauration de ressource",
        _ => "Effet",
    };

    private static string HumanTestUx6Z(Forge.RpgKit.Domain.Abilities.AbilityTestKind kind) => kind switch
    {
        Forge.RpgKit.Domain.Abilities.AbilityTestKind.ValidTarget => "Cible valide",
        Forge.RpgKit.Domain.Abilities.AbilityTestKind.InvalidTarget => "Cible invalide refusée",
        Forge.RpgKit.Domain.Abilities.AbilityTestKind.ResourceCost => "Coût de ressource",
        Forge.RpgKit.Domain.Abilities.AbilityTestKind.Cooldown => "Recharge",
        Forge.RpgKit.Domain.Abilities.AbilityTestKind.Effect => "Résultat des effets",
        _ => "Scénario",
    };

    private static string HumanTestEvidenceUx6Z(Forge.RpgKit.Domain.Abilities.AbilityTestKind kind) => kind switch
    {
        Forge.RpgKit.Domain.Abilities.AbilityTestKind.ValidTarget => "le pouvoir est résolu sur sa cible prévue",
        Forge.RpgKit.Domain.Abilities.AbilityTestKind.InvalidTarget => "une cible interdite ne traverse pas le résolveur",
        Forge.RpgKit.Domain.Abilities.AbilityTestKind.ResourceCost => "une réserve insuffisante bloque le lancement",
        Forge.RpgKit.Domain.Abilities.AbilityTestKind.Cooldown => "un second lancement immédiat est refusé",
        Forge.RpgKit.Domain.Abilities.AbilityTestKind.Effect => "les effets produisent une magnitude observable",
        _ => "contrat vérifié",
    };

    private static string HumanCodeUx6Z(string code) => code switch
    {
        "assistant.ability.approval_required" => "Approbation requise",
        "assistant.ability.permission_denied" => "Permission insuffisante",
        "assistant.ability.revision_conflict" => "Projet modifié",
        "assistant.ability.plan_not_previewed" => "Plan non prévisualisé",
        "assistant.ability.proposal_invalid" => "Proposition invalide",
        "RPG_ABILITY_SCRIPT_MISSING" => "ForgeScript absent",
        "RPG_ABILITY_ITEM_MISSING" => "Objet absent",
        "RPG_ABILITY_TEST_FAILED" => "Test de combat échoué",
        "RPG_ABILITY_BALANCE_FAILED" => "Équilibrage refusé",
        _ => "Diagnostic canonique",
    };

    private static string HumanFreeTextUx6Z(string text) => UuidRegexUx6Z().Replace(text ?? string.Empty,
        "référence protégée");

    private static IEnumerable<string> DescendantTextUx6Z(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Label label) yield return label.Text;
            if (child is Button button) yield return button.Text;
            if (child is LineEdit line) yield return line.Text;
            if (child is TextEdit text) yield return text.Text;
            foreach (var nested in DescendantTextUx6Z(child)) yield return nested;
        }
    }

    private static bool ContainsUuidUx6Z(string value) => UuidRegexUx6Z().IsMatch(value ?? string.Empty);

    [GeneratedRegex(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex UuidRegexUx6Z();

    private sealed record SourceSnapshotUx6Z(
        ProjectDefinition? Project,
        ProjectCommandHistory? History,
        ProjectRevision? Revision,
        IReadOnlyList<CommandHistoryJournalEntry> Journal,
        bool Dirty,
        bool? CanUndo,
        bool? CanRedo,
        Forge.Domain.Areas.AreaId? ActiveArea,
        IReadOnlyList<EntityId> Selection,
        EntityId? ActiveSelection,
        StudioUx6ZApprovalSnapshot Approval);
}
