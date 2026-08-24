using Forge.Application.Commands;
using Forge.Domain.Blueprints;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;
using System.Text.RegularExpressions;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioBlueprintGovernanceWorkbenchUx3D : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private SourceSnapshotUx3D? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;
    private bool _usesRealModuleEvidence;
    private bool _usesCanonicalFallbackEvidence;
    private bool _updating;
    private bool _explicitSelection;

    private readonly Label _context;
    private readonly OptionButton _blueprint;
    private readonly Label _selectionSummary;
    private readonly Label _usageSummary;
    private readonly TabContainer _tabs;
    private readonly TextEdit _dependencies;
    private readonly LineEdit _platformBudget;
    private readonly CheckBox _dependencyApproval;
    private readonly Button _dependencyPreviewButton;
    private readonly Button _dependencyApplyButton;
    private readonly Label _dependencySummary;
    private readonly VBoxContainer _dependencyDiff;
    private readonly VBoxContainer _dependencyDiagnostics;
    private readonly LineEdit _source;
    private readonly LineEdit _author;
    private readonly LineEdit _license;
    private readonly LineEdit _hash;
    private readonly LineEdit _permission;
    private readonly CheckBox _aiGenerated;
    private readonly TextEdit _modifications;
    private readonly CheckBox _provenanceApproval;
    private readonly Button _provenancePreviewButton;
    private readonly Button _provenanceApplyButton;
    private readonly Label _provenanceSummary;
    private readonly VBoxContainer _provenanceDiff;
    private readonly VBoxContainer _provenanceDiagnostics;
    private readonly Label _status;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    private IReadOnlyList<StudioUx3DBlueprintChoice> _choices = [];
    private StudioUx3DBlueprintChoice? _selected;
    private StudioUx3DDependencyDraft? _dependencyDraft;
    private StudioUx3DProvenanceDraft? _provenanceDraft;
    private StudioUx3DDependencyPreview? _dependencyPreview;
    private StudioUx3DProvenancePreview? _provenancePreview;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? ProjectChanged;

    internal bool IsOpen => Visible;
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealModuleEvidence => _usesRealModuleEvidence;
    internal bool UsesCanonicalFallbackEvidence => _usesCanonicalFallbackEvidence;
    internal bool HasExplicitSelection => _explicitSelection && _selected is not null;
    internal bool DependencyPreviewReady => _dependencyPreview is
    { Succeeded: true, CanApply: true, DryRunSucceeded: true, BuildGraphResolved: true, RuntimeClaimsBounded: true };
    internal bool ProvenancePreviewReady => _provenancePreview is
    { Succeeded: true, CanApply: true, DryRunSucceeded: true, DistributionReady: true, RuntimeClaimsBounded: true };
    internal bool EvidenceSourceUntouched => SourceMatchesUx3D();
    internal bool EvidenceResourcesReleased => _evidenceReleased;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool ShowsHumanSurface
    {
        get
        {
            var labels = FindChildren("*", "Label", true, false).Cast<Label>()
                .Select(item => item.Text).Where(item => !string.IsNullOrWhiteSpace(item)).ToArray();
            var buttons = FindChildren("*", "Button", true, false).Cast<Button>()
                .Select(item => item.Text).Where(item => !string.IsNullOrWhiteSpace(item)).ToArray();
            var tabs = Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle).ToArray();
            var all = labels.Concat(buttons).Concat(tabs).ToArray();
            return all.Any(item => item.Contains("Dépendances automatiques", StringComparison.Ordinal)) &&
                   all.Any(item => item.Contains("Provenance et licence", StringComparison.Ordinal)) &&
                   all.Any(item => item.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase)) &&
                   all.All(item => !Regex.IsMatch(item,
                       "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
                       RegexOptions.CultureInvariant));
        }
    }

    internal StudioBlueprintGovernanceWorkbenchUx3D(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        Visible = false;
        Color = StudioStyle.Background;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 190;

        var margin = StudioStyle.Margin(22, 18, 22, 18);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(margin);
        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddThemeConstantOverride("separation", 10);
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("UX3-D · Gouvernance des blueprints", 20, StudioStyle.Text));
        titles.AddChild(StudioStyle.Label(
            "Dépendances automatiques et provenance distribuable · deux aperçus, deux transactions canoniques.",
            10, StudioStyle.Muted, wrap: true));
        _context = StudioStyle.Label("Projet courant · aucune donnée n’est synthétisée.", 10, StudioStyle.Accent, wrap: true);
        titles.AddChild(_context);
        header.AddChild(titles);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);
        root.AddChild(header);

        var main = new HSplitContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        main.SplitOffsets = [300];
        root.AddChild(main);
        var selectorPanel = StudioStyle.Panel("Ux3DSelector", StudioStyle.Surface, 9, 1);
        selectorPanel.CustomMinimumSize = new Vector2(280, 0);
        var selectorMargin = StudioStyle.Margin(12, 12, 12, 12);
        selectorPanel.AddChild(selectorMargin);
        var selector = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        selector.AddThemeConstantOverride("separation", 8);
        selectorMargin.AddChild(selector);
        selector.AddChild(StudioStyle.Label("BLUEPRINT CIBLE", 10, StudioStyle.Muted));
        selector.AddChild(StudioStyle.Label(
            "Choisissez explicitement une définition Projet. Les blueprints Standard restent visibles mais protégés.",
            10, StudioStyle.Text, wrap: true));
        _blueprint = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _blueprint.ItemSelected += SelectBlueprintUx3D;
        selector.AddChild(_blueprint);
        _selectionSummary = StudioStyle.Label("Aucune sélection.", 10, StudioStyle.Warning, wrap: true);
        selector.AddChild(_selectionSummary);
        _usageSummary = StudioStyle.Label(string.Empty, 9, StudioStyle.Muted, wrap: true);
        selector.AddChild(_usageSummary);
        selector.AddChild(new HSeparator());
        selector.AddChild(StudioStyle.Label(
            "LIMITES HONNÊTES\nL’aperçu construit le graphe déterministe et contrôle l’éligibilité distribution. " +
            "Il ne produit aucun package, ne charge aucun asset et n’exécute aucun script.",
            9, StudioStyle.Muted, wrap: true));
        main.AddChild(selectorPanel);

        var contentPanel = StudioStyle.Panel("Ux3DContent", StudioStyle.Surface, 9, 1);
        contentPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        contentPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        var contentMargin = StudioStyle.Margin(12, 10, 12, 10);
        contentPanel.AddChild(contentMargin);
        _tabs = new TabContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        contentMargin.AddChild(_tabs);
        main.AddChild(contentPanel);

        var dependencyTab = ScrollTabUx3D("Dépendances automatiques", out var dependencyBody);
        dependencyBody.AddChild(StudioStyle.Label(
            "MANIFESTE MINIMAL · un chemin relatif ou pack:// par ligne", 10, StudioStyle.Accent));
        dependencyBody.AddChild(StudioStyle.Label(
            "Les manifestes de blueprints ferment transitivement leurs propres ressources. Toute référence absente est bloquée avant le build.",
            9, StudioStyle.Muted, wrap: true));
        _dependencies = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 126),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            PlaceholderText = "content/shared/material.asset.json\npack://forge.effects/2.0.0/effect.asset",
            WrapMode = TextEdit.LineWrappingMode.None,
        };
        dependencyBody.AddChild(_dependencies);
        dependencyBody.AddChild(StudioStyle.Label("Budget plateforme (octets, 1 à 16 777 216)", 9, StudioStyle.Muted));
        _platformBudget = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill, PlaceholderText = "1048576" };
        dependencyBody.AddChild(_platformBudget);
        _dependencyApproval = new CheckBox { Text = "J’approuve ce manifeste de dépendances." };
        dependencyBody.AddChild(_dependencyApproval);
        var dependencyActions = new HBoxContainer();
        dependencyActions.AddThemeConstantOverride("separation", 8);
        _dependencyPreviewButton = StudioStyle.Button("Aperçu sec des dépendances", compact: true);
        _dependencyPreviewButton.Pressed += PreviewDependenciesUx3D;
        dependencyActions.AddChild(_dependencyPreviewButton);
        _dependencyApplyButton = StudioStyle.Button("Appliquer les dépendances", compact: true);
        _dependencyApplyButton.Pressed += () => ApplyDependenciesUx3D();
        dependencyActions.AddChild(_dependencyApplyButton);
        dependencyBody.AddChild(dependencyActions);
        _dependencySummary = StudioStyle.Label("Sélectionnez un blueprint.", 10, StudioStyle.Muted, wrap: true);
        dependencyBody.AddChild(_dependencySummary);
        AddReviewUx3D(dependencyBody, out _dependencyDiff, out _dependencyDiagnostics);
        _tabs.AddChild(dependencyTab);

        var provenanceTab = ScrollTabUx3D("Provenance et licence", out var provenanceBody);
        provenanceBody.AddChild(StudioStyle.Label(
            "GOUVERNANCE DISTRIBUTION · source, auteur, SPDX, permission et SHA-256", 10, StudioStyle.Accent));
        provenanceBody.AddChild(StudioStyle.Label(
            "La validation reproduit la décision canonique d’inclusion dans un package. Les valeurs techniques restent internes au document.",
            9, StudioStyle.Muted, wrap: true));
        _source = FieldUx3D(provenanceBody, "Source canonique", "project://blueprints/mon-asset");
        _author = FieldUx3D(provenanceBody, "Auteur ou organisation", "Équipe du module");
        _license = FieldUx3D(provenanceBody, "Licence SPDX", "MIT");
        _hash = FieldUx3D(provenanceBody, "SHA-256 du contenu", "64 caractères hexadécimaux");
        _permission = FieldUx3D(provenanceBody, "Preuve de permission", "spdx:MIT ou référence d’autorisation");
        _aiGenerated = new CheckBox { Text = "Contenu généré avec assistance IA" };
        provenanceBody.AddChild(_aiGenerated);
        provenanceBody.AddChild(StudioStyle.Label("Modifications déclarées · une par ligne", 9, StudioStyle.Muted));
        _modifications = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 82),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            WrapMode = TextEdit.LineWrappingMode.Boundary,
        };
        provenanceBody.AddChild(_modifications);
        _provenanceApproval = new CheckBox { Text = "J’approuve la provenance et la licence." };
        provenanceBody.AddChild(_provenanceApproval);
        var provenanceActions = new HBoxContainer();
        provenanceActions.AddThemeConstantOverride("separation", 8);
        _provenancePreviewButton = StudioStyle.Button("Aperçu sec de distribution", compact: true);
        _provenancePreviewButton.Pressed += PreviewProvenanceUx3D;
        provenanceActions.AddChild(_provenancePreviewButton);
        _provenanceApplyButton = StudioStyle.Button("Appliquer la provenance", compact: true);
        _provenanceApplyButton.Pressed += () => ApplyProvenanceUx3D();
        provenanceActions.AddChild(_provenanceApplyButton);
        provenanceBody.AddChild(provenanceActions);
        _provenanceSummary = StudioStyle.Label("Sélectionnez un blueprint.", 10, StudioStyle.Muted, wrap: true);
        provenanceBody.AddChild(_provenanceSummary);
        AddReviewUx3D(provenanceBody, out _provenanceDiff, out _provenanceDiagnostics);
        _tabs.AddChild(provenanceTab);

        _status = StudioStyle.Label("Prêt.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        var footer = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footer.AddThemeConstantOverride("separation", 8);
        var footerNote = StudioStyle.Label(
            "Transactions séparées · après une application, rechargez et prévisualisez l’autre bloc.",
            9, StudioStyle.Muted, wrap: true);
        footerNote.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        footer.AddChild(footerNote);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _undo.Pressed += UndoUx3D;
        footer.AddChild(_undo);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.Pressed += RedoUx3D;
        footer.AddChild(_redo);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        _save.Pressed += SaveUx3D;
        footer.AddChild(_save);
        root.AddChild(footer);

        WireDraftChangesUx3D();
        ResetUx3D();
    }

    internal void Open() => OpenBoundUx3D(_sourceSession, evidence: false, cancelApproval: false);
    internal void RestoreSourceSurfaceAfterSmoke() => OpenBoundUx3D(_sourceSession, evidence: false, cancelApproval: false);
    internal void HideSourceSurfaceAfterSmoke()
    {
        Visible = false;
        _session = _sourceSession;
    }

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null) return;
        if (Visible) _session.CloseUx3D();
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidenceUx3D();
        _session = _sourceSession;
        ResetUx3D();
    }

    internal void CloseEvidence()
    {
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidenceUx3D();
        _session = _sourceSession;
        ResetUx3D(keepReleaseState: true);
    }

    internal bool PrepareEvidence(string mode)
    {
        if (!mode.StartsWith("ux3-d", StringComparison.OrdinalIgnoreCase)) return false;
        ReleaseEvidenceUx3D();
        _sourceSnapshot = CaptureSourceUx3D();
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux3-d-ui-{Guid.NewGuid():N}");
        try
        {
            var evidence = _sourceSession.CreateBlueprintGovernanceEvidenceProjectUx3D();
            if (evidence is null || !ProjectSession.Create(root, evidence.Project).Succeeded ||
                !StudioSessionController.MaterializeGovernanceEvidenceFilesUx3D(root, evidence)) return false;
            var isolated = new StudioSessionController();
            if (!isolated.Open(root).Succeeded)
            {
                isolated.Close();
                StudioSessionController.DeleteTemporaryBlueprintUx3A(root);
                return false;
            }
            _evidenceSession = isolated;
            _evidenceRoot = root;
            _evidenceReleased = false;
            _usesRealModuleEvidence = evidence.UsesRealModule;
            _usesCanonicalFallbackEvidence = evidence.UsesCanonicalFallback;
            OpenBoundUx3D(isolated, evidence: true, cancelApproval: true);
            var index = _choices.ToList().FindIndex(item => item.Id == evidence.EditableBlueprintId);
            if (index < 0) return FailAndReleaseUx3D();
            _blueprint.Select(index + 1);
            SelectBlueprintUx3D(index + 1);
            if (_dependencyDraft is null || _provenanceDraft is null) return FailAndReleaseUx3D();
            _dependencyDraft = StudioSessionController.ChangedDependencyDraftUx3D(_dependencyDraft, evidence);
            PopulateDependencyUx3D();
            _dependencyApproval.ButtonPressed = true;
            PreviewDependenciesUx3D();
            _provenanceDraft = StudioSessionController.ChangedProvenanceDraftUx3D(_provenanceDraft);
            PopulateProvenanceUx3D();
            _provenanceApproval.ButtonPressed = true;
            PreviewProvenanceUx3D();
            _tabs.CurrentTab = mode.EndsWith("-dependencies", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            var ready = HasExplicitSelection && DependencyPreviewReady && ProvenancePreviewReady &&
                        ShowsHumanSurface && SourceMatchesUx3D() && UsesIsolatedEvidence &&
                        (_usesRealModuleEvidence ^ _usesCanonicalFallbackEvidence);
            if (!ready)
            {
                GD.PrintErr("OPENRPG_FORGE_STUDIO_UX3_D_UI_PREPARE_FAILED " +
                            $"selection={HasExplicitSelection.ToString().ToLowerInvariant()} " +
                            $"dependencies={DependencyPreviewReady.ToString().ToLowerInvariant()} " +
                            $"provenance={ProvenancePreviewReady.ToString().ToLowerInvariant()} " +
                            $"human={ShowsHumanSurface.ToString().ToLowerInvariant()} " +
                            $"source={SourceMatchesUx3D().ToString().ToLowerInvariant()}");
                return FailAndReleaseUx3D();
            }
            return true;
        }
        catch
        {
            return FailAndReleaseUx3D();
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        if (!PrepareEvidence("ux3-d-blueprint-governance")) return false;
        var isolated = _evidenceSession;
        var root = _evidenceRoot;
        var reopened = new StudioSessionController();
        try
        {
            if (isolated is null || root is null || !ReferenceEquals(_session, isolated) ||
                !DependencyPreviewReady || !ProvenancePreviewReady || _dependencyDraft is null) return false;
            var sourceJson = ProjectJsonSerializer.Serialize(isolated.Project!);
            if (!ApplyDependenciesUx3D()) return false;
            ReloadSelectionUx3D();
            if (_provenanceDraft is null) return false;
            _provenanceDraft = StudioSessionController.ChangedProvenanceDraftUx3D(_provenanceDraft);
            PopulateProvenanceUx3D();
            _provenanceApproval.ButtonPressed = true;
            PreviewProvenanceUx3D();
            if (!ProvenancePreviewReady || !ApplyProvenanceUx3D()) return false;
            var finalJson = ProjectJsonSerializer.Serialize(isolated.Project!);
            var undoOne = _session.UndoGovernanceUx3D().Succeeded;
            var undoTwo = _session.UndoGovernanceUx3D().Succeeded &&
                          ProjectJsonSerializer.Serialize(isolated.Project!) == sourceJson;
            var redoOne = _session.RedoGovernanceUx3D().Succeeded;
            var redoTwo = _session.RedoGovernanceUx3D().Succeeded &&
                          ProjectJsonSerializer.Serialize(isolated.Project!) == finalJson;
            var saved = _session.SaveGovernanceUx3D().Succeeded;
            var persisted = saved && reopened.Open(root).Succeeded &&
                            ProjectJsonSerializer.Serialize(reopened.Project!) == finalJson;
            reopened.Close();
            return undoOne && undoTwo && redoOne && redoTwo && persisted && SourceMatchesUx3D();
        }
        finally
        {
            reopened.Close();
            CloseEvidence();
        }
    }

    private void OpenBoundUx3D(StudioSessionController session, bool evidence, bool cancelApproval)
    {
        _session = session;
        if (cancelApproval) _session.CloseUx3D();
        Visible = true;
        _context.Text = evidence
            ? "PREUVE ISOLÉE · copie canonique enrichie du module ; la source reste strictement inchangée."
            : "PROJET COURANT · état réel, aucune dépendance ni provenance synthétisée.";
        RebuildBlueprintsUx3D();
        ResetSelectionUx3D();
        UpdateButtonsUx3D();
    }

    private void RebuildBlueprintsUx3D()
    {
        _choices = _session.BlueprintGovernanceChoicesUx3D;
        _blueprint.Clear();
        _blueprint.AddItem("Choisir explicitement un blueprint…");
        foreach (var choice in _choices) _blueprint.AddItem(choice.Label);
    }

    private void SelectBlueprintUx3D(long index)
    {
        if (_updating) return;
        if (index <= 0 || index > _choices.Count)
        {
            ResetSelectionUx3D();
            return;
        }
        _selected = _choices[(int)index - 1];
        _explicitSelection = true;
        _dependencyDraft = _session.CreateDependencyDraftUx3D(_selected.Id);
        _provenanceDraft = _session.CreateProvenanceDraftUx3D(_selected.Id);
        _selectionSummary.Text = _selected.IsReadOnly
            ? $"{_selected.Label}\nStandard installé · données protégées en lecture seule."
            : $"{_selected.Label}\nProjet · dépendances et provenance éditables.";
        _selectionSummary.Modulate = _selected.IsReadOnly ? StudioStyle.Warning : StudioStyle.Success;
        _usageSummary.Text = _selected.IsUsed
            ? "Utilisé dans le module · son graphe de dépendances entre dans le manifeste minimal."
            : "Non utilisé actuellement · sa configuration est conservée mais exclue du manifeste minimal.";
        PopulateDependencyUx3D();
        PopulateProvenanceUx3D();
        ResetPreviewsUx3D();
        UpdateButtonsUx3D();
    }

    private void ReloadSelectionUx3D()
    {
        if (_selected is null) return;
        var id = _selected.Id;
        RebuildBlueprintsUx3D();
        var index = _choices.ToList().FindIndex(item => item.Id == id);
        if (index < 0)
        {
            ResetSelectionUx3D();
            return;
        }
        _blueprint.Select(index + 1);
        SelectBlueprintUx3D(index + 1);
    }

    private void PreviewDependenciesUx3D()
    {
        var draft = ReadDependencyDraftUx3D();
        if (draft is null) return;
        _dependencyDraft = draft;
        _dependencyPreview = _session.PreviewDependenciesUx3D(draft);
        RenderReviewUx3D(_dependencyDiff, _dependencyDiagnostics, _dependencyPreview.SemanticDiff,
            _dependencyPreview.Diagnostics);
        _dependencySummary.Text = _dependencyPreview.Message;
        SetLocalStatusUx3D(_dependencyPreview.Message, !_dependencyPreview.Succeeded);
        UpdateButtonsUx3D();
    }

    private void PreviewProvenanceUx3D()
    {
        var draft = ReadProvenanceDraftUx3D();
        if (draft is null) return;
        _provenanceDraft = draft;
        _provenancePreview = _session.PreviewProvenanceUx3D(draft);
        RenderReviewUx3D(_provenanceDiff, _provenanceDiagnostics, _provenancePreview.SemanticDiff,
            _provenancePreview.Diagnostics);
        _provenanceSummary.Text = _provenancePreview.Message;
        SetLocalStatusUx3D(_provenancePreview.Message, !_provenancePreview.Succeeded);
        UpdateButtonsUx3D();
    }

    private bool ApplyDependenciesUx3D()
    {
        var draft = ReadDependencyDraftUx3D();
        if (draft is null || !DependencyPreviewReady) return false;
        var result = _session.ApplyApprovedDependenciesUx3D(draft);
        SetLocalStatusUx3D(result.Message, !result.Succeeded);
        if (!result.Succeeded) return false;
        ProjectChanged?.Invoke();
        ReloadSelectionUx3D();
        return true;
    }

    private bool ApplyProvenanceUx3D()
    {
        var draft = ReadProvenanceDraftUx3D();
        if (draft is null || !ProvenancePreviewReady) return false;
        var result = _session.ApplyApprovedProvenanceUx3D(draft);
        SetLocalStatusUx3D(result.Message, !result.Succeeded);
        if (!result.Succeeded) return false;
        ProjectChanged?.Invoke();
        ReloadSelectionUx3D();
        return true;
    }

    private void UndoUx3D()
    {
        var result = _session.UndoGovernanceUx3D();
        SetLocalStatusUx3D(result.Message, !result.Succeeded);
        if (result.Succeeded) { ProjectChanged?.Invoke(); ReloadSelectionUx3D(); }
    }

    private void RedoUx3D()
    {
        var result = _session.RedoGovernanceUx3D();
        SetLocalStatusUx3D(result.Message, !result.Succeeded);
        if (result.Succeeded) { ProjectChanged?.Invoke(); ReloadSelectionUx3D(); }
    }

    private void SaveUx3D()
    {
        var result = _session.SaveGovernanceUx3D();
        SetLocalStatusUx3D(result.Message, !result.Succeeded);
        UpdateButtonsUx3D();
    }

    private StudioUx3DDependencyDraft? ReadDependencyDraftUx3D()
    {
        if (_dependencyDraft is null) return null;
        if (!long.TryParse(_platformBudget.Text.Trim(), out var budget))
        {
            SetLocalStatusUx3D("Le budget doit être un nombre entier d’octets.", true);
            return null;
        }
        return _dependencyDraft with
        {
            Dependencies = LinesUx3D(_dependencies.Text),
            PlatformByteBudget = budget,
            Approved = _dependencyApproval.ButtonPressed,
        };
    }

    private StudioUx3DProvenanceDraft? ReadProvenanceDraftUx3D() => _provenanceDraft is null ? null :
        _provenanceDraft with
        {
            Source = _source.Text,
            Author = _author.Text,
            LicenseSpdx = _license.Text,
            ContentHash = _hash.Text,
            PermissionEvidence = _permission.Text,
            IsAiGenerated = _aiGenerated.ButtonPressed,
            Modifications = LinesUx3D(_modifications.Text),
            Approved = _provenanceApproval.ButtonPressed,
        };

    private void PopulateDependencyUx3D()
    {
        _updating = true;
        _dependencies.Text = _dependencyDraft is null ? string.Empty : string.Join('\n', _dependencyDraft.Dependencies);
        _platformBudget.Text = _dependencyDraft?.PlatformByteBudget.ToString() ?? string.Empty;
        _dependencyApproval.ButtonPressed = _dependencyDraft?.Approved == true;
        _updating = false;
    }

    private void PopulateProvenanceUx3D()
    {
        _updating = true;
        _source.Text = _provenanceDraft?.Source ?? string.Empty;
        _author.Text = _provenanceDraft?.Author ?? string.Empty;
        _license.Text = _provenanceDraft?.LicenseSpdx ?? string.Empty;
        _hash.Text = _provenanceDraft?.ContentHash ?? string.Empty;
        _permission.Text = _provenanceDraft?.PermissionEvidence ?? string.Empty;
        _aiGenerated.ButtonPressed = _provenanceDraft?.IsAiGenerated == true;
        _modifications.Text = _provenanceDraft is null ? string.Empty : string.Join('\n', _provenanceDraft.Modifications);
        _provenanceApproval.ButtonPressed = _provenanceDraft?.Approved == true;
        _updating = false;
    }

    private void WireDraftChangesUx3D()
    {
        _dependencies.TextChanged += DraftChangedUx3D;
        _platformBudget.TextChanged += _ => DraftChangedUx3D();
        _dependencyApproval.Toggled += _ => DraftChangedUx3D();
        _source.TextChanged += _ => DraftChangedUx3D();
        _author.TextChanged += _ => DraftChangedUx3D();
        _license.TextChanged += _ => DraftChangedUx3D();
        _hash.TextChanged += _ => DraftChangedUx3D();
        _permission.TextChanged += _ => DraftChangedUx3D();
        _aiGenerated.Toggled += _ => DraftChangedUx3D();
        _modifications.TextChanged += DraftChangedUx3D;
        _provenanceApproval.Toggled += _ => DraftChangedUx3D();
    }

    private void DraftChangedUx3D()
    {
        if (_updating) return;
        ResetPreviewsUx3D();
        UpdateButtonsUx3D();
    }

    private void ResetSelectionUx3D()
    {
        _updating = true;
        if (_blueprint.ItemCount > 0) _blueprint.Select(0);
        _updating = false;
        _selected = null;
        _explicitSelection = false;
        _dependencyDraft = null;
        _provenanceDraft = null;
        _selectionSummary.Text = "Aucune sélection explicite.";
        _selectionSummary.Modulate = StudioStyle.Warning;
        _usageSummary.Text = _choices.Count == 0
            ? "État vide · ce module ne contient aucun blueprint."
            : $"{_choices.Count} blueprint(s) disponible(s).";
        PopulateDependencyUx3D();
        PopulateProvenanceUx3D();
        ResetPreviewsUx3D();
    }

    private void ResetPreviewsUx3D()
    {
        _dependencyPreview = null;
        _provenancePreview = null;
        _dependencySummary.Text = _selected is null ? "Sélectionnez un blueprint." : "Modifiez puis lancez l’aperçu sec.";
        _provenanceSummary.Text = _selected is null ? "Sélectionnez un blueprint." : "Modifiez puis lancez l’aperçu sec.";
        ClearChildrenUx3D(_dependencyDiff);
        ClearChildrenUx3D(_dependencyDiagnostics);
        ClearChildrenUx3D(_provenanceDiff);
        ClearChildrenUx3D(_provenanceDiagnostics);
    }

    private void ResetUx3D(bool keepReleaseState = false)
    {
        _choices = [];
        _selected = null;
        _explicitSelection = false;
        _dependencyDraft = null;
        _provenanceDraft = null;
        _usesRealModuleEvidence = false;
        _usesCanonicalFallbackEvidence = false;
        if (!keepReleaseState) _evidenceReleased = _evidenceSession is null;
        ResetSelectionUx3D();
        UpdateButtonsUx3D();
    }

    private void UpdateButtonsUx3D()
    {
        var editable = _selected is { IsReadOnly: false };
        _dependencyPreviewButton.Disabled = !editable;
        _dependencyApplyButton.Disabled = !DependencyPreviewReady;
        _provenancePreviewButton.Disabled = !editable;
        _provenanceApplyButton.Disabled = !ProvenancePreviewReady;
        _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo;
        _save.Disabled = !_session.IsDirty;
    }

    private SourceSnapshotUx3D CaptureSourceUx3D()
    {
        var history = _sourceSession.History;
        return new(_sourceSession.Project, history, history?.State.Revision, history?.Journal.ToArray() ?? [],
            _sourceSession.IsDirty, history?.CanUndo, history?.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureBlueprintGovernanceApprovalUx3D());
    }

    private bool SourceMatchesUx3D()
    {
        if (_sourceSnapshot is null) return true;
        var history = _sourceSession.History;
        return ReferenceEquals(_sourceSnapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(_sourceSnapshot.History, history) && history?.State.Revision == _sourceSnapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(_sourceSnapshot.Journal) &&
               _sourceSession.IsDirty == _sourceSnapshot.Dirty && history?.CanUndo == _sourceSnapshot.CanUndo &&
               history?.CanRedo == _sourceSnapshot.CanRedo && _sourceSession.ActiveAreaId == _sourceSnapshot.ActiveArea &&
               _sourceSession.SelectedEntityIds.SequenceEqual(_sourceSnapshot.Selection) &&
               _sourceSession.SelectedEntityId == _sourceSnapshot.ActiveSelection &&
               _sourceSession.CaptureBlueprintGovernanceApprovalUx3D() == _sourceSnapshot.Approval;
    }

    private void ReleaseEvidenceUx3D()
    {
        _evidenceSession?.Close();
        _evidenceSession = null;
        _evidenceReleased = string.IsNullOrWhiteSpace(_evidenceRoot) ||
                            StudioSessionController.DeleteTemporaryBlueprintUx3A(_evidenceRoot);
        _evidenceRoot = null;
        _session = _sourceSession;
    }

    private bool FailAndReleaseUx3D()
    {
        CloseEvidence();
        return false;
    }

    private void SetLocalStatusUx3D(string message, bool error)
    {
        _status.Text = message;
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Success;
        StatusChanged?.Invoke(message, error);
    }

    private static ScrollContainer ScrollTabUx3D(string title, out VBoxContainer body)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 7);
        scroll.AddChild(body);
        return scroll;
    }

    private static LineEdit FieldUx3D(VBoxContainer parent, string label, string placeholder)
    {
        parent.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        var field = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill, PlaceholderText = placeholder };
        parent.AddChild(field);
        return field;
    }

    private static void AddReviewUx3D(VBoxContainer body, out VBoxContainer diff, out VBoxContainer diagnostics)
    {
        var review = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        review.AddThemeConstantOverride("separation", 8);
        diff = ReviewColumnUx3D(review, "DIFF SÉMANTIQUE");
        diagnostics = ReviewColumnUx3D(review, "DIAGNOSTICS LOCALISÉS");
        body.AddChild(review);
    }

    private static VBoxContainer ReviewColumnUx3D(HBoxContainer host, string title)
    {
        var panel = StudioStyle.Panel("Ux3D" + title.Replace(" ", string.Empty), StudioStyle.SurfaceRaised, 7, 1);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.CustomMinimumSize = new Vector2(0, 96);
        var margin = StudioStyle.Margin(8, 7, 8, 7);
        panel.AddChild(margin);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddChild(StudioStyle.Label(title, 9, StudioStyle.Muted));
        margin.AddChild(column);
        host.AddChild(panel);
        return column;
    }

    private static void RenderReviewUx3D(VBoxContainer diff, VBoxContainer diagnostics,
        IReadOnlyList<string> semantic, IReadOnlyList<StudioUx3DDiagnostic> items)
    {
        ClearChildrenUx3D(diff, keepFirst: true);
        ClearChildrenUx3D(diagnostics, keepFirst: true);
        foreach (var line in semantic.Take(8)) diff.AddChild(StudioStyle.Label("• " + line, 9, StudioStyle.Text, wrap: true));
        if (semantic.Count == 0) diff.AddChild(StudioStyle.Label("Aucun changement calculé.", 9, StudioStyle.Muted, wrap: true));
        foreach (var item in items.Take(8)) diagnostics.AddChild(StudioStyle.Label(
            $"{item.Code} · {item.Path}\n{item.Message}", 9, StudioStyle.Warning, wrap: true));
        if (items.Count == 0) diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic bloquant.", 9, StudioStyle.Success, wrap: true));
    }

    private static void ClearChildrenUx3D(Node node, bool keepFirst = false)
    {
        var children = node.GetChildren().ToArray();
        for (var index = keepFirst ? 1 : 0; index < children.Length; index++)
        {
            node.RemoveChild(children[index]);
            children[index].QueueFree();
        }
    }

    private static IReadOnlyList<string> LinesUx3D(string text) => text
        .Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n')
        .Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).ToArray();

    private sealed record SourceSnapshotUx3D(
        Forge.Domain.Projects.ProjectDefinition? Project,
        ProjectCommandHistory? History,
        Forge.Application.Projects.ProjectRevision? Revision,
        IReadOnlyList<CommandHistoryJournalEntry> Journal,
        bool Dirty,
        bool? CanUndo,
        bool? CanRedo,
        Forge.Domain.Areas.AreaId? ActiveArea,
        IReadOnlyList<Forge.Domain.Entities.EntityId> Selection,
        Forge.Domain.Entities.EntityId? ActiveSelection,
        StudioUx3DApprovalSnapshot Approval);
}
