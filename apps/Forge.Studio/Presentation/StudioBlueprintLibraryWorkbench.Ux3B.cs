using System.Globalization;
using System.Text.RegularExpressions;
using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioBlueprintLibraryWorkbenchUx3B : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private SourceSnapshotUx3B? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;
    private bool _usesRealModuleEvidence;
    private bool _usesCanonicalFallbackEvidence;
    private bool _updating;
    private bool _explicitSelection;

    private readonly OptionButton _blueprint;
    private readonly Label _selection;
    private readonly TabContainer _tabs;

    private readonly OptionButton _sourceInstance;
    private readonly ItemList _promotedOverrides;
    private readonly OptionButton _versionBump;
    private readonly ItemList _excludedInstances;
    private readonly TextEdit _categories;
    private readonly CheckBox _updateApproval;
    private readonly VBoxContainer _updateDiff;
    private readonly VBoxContainer _updateDiagnostics;
    private readonly Label _updateSummary;
    private readonly Button _updatePreviewButton;
    private readonly Button _updateApplyButton;

    private readonly CheckBox _variantEnabled;
    private readonly OptionButton _baseBlueprint;
    private readonly TextEdit _variantOverrides;
    private readonly CheckBox _variantRequired;
    private readonly CheckBox _compositionEnabled;
    private readonly ItemList _compositionNodes;
    private readonly OptionButton _nodeBlueprint;
    private readonly OptionButton _nodeParent;
    private readonly LineEdit _nodeSocket;
    private readonly LineEdit[] _nodeTransform;
    private readonly Button _nodeAddOrUpdate;
    private readonly Button _nodeRemove;
    private readonly TextEdit _assemblyRules;
    private readonly TextEdit _parameters;
    private readonly TextEdit _assemblyScripts;
    private readonly TextEdit _authorCollections;
    private readonly CheckBox _compositionRequired;
    private readonly CheckBox _prefabApproval;
    private readonly VBoxContainer _prefabDiff;
    private readonly VBoxContainer _prefabDiagnostics;
    private readonly Label _prefabSummary;
    private readonly Button _prefabPreviewButton;
    private readonly Button _prefabApplyButton;

    private readonly Label _status;
    private readonly Button _undoButton;
    private readonly Button _redoButton;
    private readonly Button _saveButton;

    private IReadOnlyList<StudioUx3BBlueprintChoice> _blueprints = [];
    private IReadOnlyList<StudioUx3BInstanceChoice> _instances = [];
    private StudioUx3BUpdateDraft? _updateDraft;
    private StudioUx3BPrefabDraft? _prefabDraft;
    private StudioUx3BUpdatePreview? _updatePreview;
    private StudioUx3BPrefabPreview? _prefabPreview;
    private string? _updatePreviewFingerprint;
    private string? _prefabPreviewFingerprint;
    private bool _updatePreviewReady;
    private bool _prefabPreviewReady;
    private List<StudioUx3BCompositionNodeDraft> _nodes = [];

    internal event Action<string, bool>? StatusChanged;
    internal event Action? BlueprintChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _explicitSelection && _updateDraft is not null && _prefabDraft is not null;
    internal bool UpdatePreviewReady => _updatePreviewReady && _updatePreview is
    {
        Succeeded: true, CanApply: true, DryRunSucceeded: true, RuntimeClaimsBounded: true,
    };
    internal bool PrefabPreviewReady => _prefabPreviewReady && _prefabPreview is
    {
        Succeeded: true, CanApply: true, DryRunSucceeded: true, RuntimeClaimsBounded: true,
    };
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealModuleEvidence => _usesRealModuleEvidence;
    internal bool UsesCanonicalFallbackEvidence => _usesCanonicalFallbackEvidence;
    internal bool EvidenceSourceUntouched => SourceMatchesUx3B();
    internal bool EvidenceResourcesReleased => _evidenceReleased && _evidenceSession is null && _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool ShowsTwoTransactions => _tabs.GetTabCount() == 2 &&
                                           _tabs.GetTabTitle(0) == "Propagation et migration" &&
                                           _tabs.GetTabTitle(1) == "Variantes et compositions" &&
                                           _updatePreviewButton.Text.Contains("propagation", StringComparison.OrdinalIgnoreCase) &&
                                           _prefabPreviewButton.Text.Contains("prefab", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx3B(this)
                .Concat(Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle)));
            return text.Contains("Bibliothèque avancée de blueprints", StringComparison.Ordinal) &&
                   text.Contains("Propagation et migration", StringComparison.Ordinal) &&
                   text.Contains("Variantes et compositions", StringComparison.Ordinal) &&
                   text.Contains("deux transactions séparées", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aperçu sec canonique", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucune instance", StringComparison.OrdinalIgnoreCase) &&
                   !UuidRegexUx3B().IsMatch(text);
        }
    }

    internal StudioBlueprintLibraryWorkbenchUx3B(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.012f, .018f, .027f, .98f);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        var dialog = StudioStyle.Panel("Ux3BBlueprintLibraryDialog", StudioStyle.Surface, 12, 1);
        dialog.ClipContents = true;
        dialog.PropagateMaximumSize = true;
        dialog.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        dialog.OffsetLeft = 18;
        dialog.OffsetTop = 14;
        dialog.OffsetRight = -18;
        dialog.OffsetBottom = -14;
        AddChild(dialog);
        var margin = StudioStyle.Margin(14, 12, 14, 12);
        margin.PropagateMaximumSize = true;
        dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            PropagateMaximumSize = true,
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        headingText.AddChild(StudioStyle.Label("Bibliothèque avancée de blueprints", 18));
        headingText.AddChild(StudioStyle.Label(
            "Propagation sélective, migration d’instances, catégories hiérarchiques, variantes et compositions · deux transactions séparées avec aperçu sec canonique.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        root.AddChild(heading);

        _blueprint = StudioUx2EnvBControls.Options();
        _blueprint.ItemSelected += SelectBlueprintUx3B;
        root.AddChild(StudioUx2EnvBControls.Field("Blueprint Projet · sélection explicite", _blueprint, 620));
        _selection = StudioStyle.Label(
            "Choisissez explicitement un blueprint Projet ; aucune définition ni instance n’est sélectionnée automatiquement.",
            10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        var tabsHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 390),
            CustomMaximumSize = new Vector2(-1, 650),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            PropagateMaximumSize = true,
        };
        root.AddChild(tabsHost);
        _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        tabsHost.AddChild(_tabs);

        var updateTab = ScrollTabUx3B("Propagation et migration", out var updateBody);
        _tabs.AddChild(updateTab);
        updateBody.AddChild(StudioStyle.Label(
            "Mettre à jour la définition depuis une instance source, promouvoir seulement les surcharges choisies et migrer les autres instances avec exclusions explicites.",
            10, StudioStyle.Text, wrap: true));
        _sourceInstance = StudioUx2EnvBControls.Options();
        _sourceInstance.ItemSelected += SelectSourceInstanceUx3B;
        updateBody.AddChild(StudioUx2EnvBControls.Field("Instance source · sélection explicite", _sourceInstance, 620));
        _promotedOverrides = MultiListUx3B(108);
        _promotedOverrides.MultiSelected += (_, _) => InvalidateUpdateUx3B();
        _versionBump = StudioUx2EnvBControls.Options("Correctif", "Mineure", "Majeure");
        _versionBump.ItemSelected += _ => InvalidateUpdateUx3B();
        updateBody.AddChild(RowUx3B(
            StudioUx2EnvBControls.Field("Surcharges à promouvoir", _promotedOverrides),
            StudioUx2EnvBControls.Field("Nouvelle version sémantique", _versionBump, 240)));
        _excludedInstances = MultiListUx3B(112);
        _excludedInstances.MultiSelected += (_, _) => InvalidateUpdateUx3B();
        _categories = StudioUx2EnvBControls.Lines("Un chemin humain par ligne · Monde/Créatures/Gardiens", 112);
        _categories.TextChanged += InvalidateUpdateUx3B;
        updateBody.AddChild(RowUx3B(
            StudioUx2EnvBControls.Field("Instances exclues de la migration", _excludedInstances),
            StudioUx2EnvBControls.Field("Catégories hiérarchiques", _categories)));
        _updateApproval = StudioUx2EnvBControls.Check(
            "J’approuve cette propagation et cette migration d’instances");
        _updateApproval.Toggled += _ => InvalidateUpdateUx3B();
        updateBody.AddChild(StudioUx2EnvBControls.Card("Ux3BUpdateApproval", "Approbation propagation",
            "Cette approbation est indépendante de la configuration prefab. Toute modification du projet ou du brouillon invalide l’aperçu.",
            _updateApproval));
        var updateButtons = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        updateButtons.AddThemeConstantOverride("separation", 8);
        _updatePreviewButton = StudioStyle.Button("Prévisualiser la propagation", compact: true);
        _updateApplyButton = StudioStyle.Button("Appliquer la propagation", compact: true);
        _updatePreviewButton.Pressed += PreviewUpdateUx3B;
        _updateApplyButton.Pressed += ApplyUpdateUx3B;
        updateButtons.AddChild(_updatePreviewButton);
        updateButtons.AddChild(_updateApplyButton);
        updateBody.AddChild(updateButtons);
        var updateReview = ReviewHostUx3B(out _updateDiff, out _updateDiagnostics);
        updateBody.AddChild(updateReview);
        _updateSummary = StudioStyle.Label("Aucun aperçu de propagation frais.", 10, StudioStyle.Muted, wrap: true);
        updateBody.AddChild(_updateSummary);

        var prefabTab = ScrollTabUx3B("Variantes et compositions", out var prefabBody);
        _tabs.AddChild(prefabTab);
        prefabBody.AddChild(StudioStyle.Label(
            "Configurer un héritage prefab et une composition structurée. L’aperçu résout le résultat sur une copie immuable ; aucune instance n’est placée ou exécutée.",
            10, StudioStyle.Text, wrap: true));
        _variantEnabled = StudioUx2EnvBControls.Check("Activer une variante de blueprint");
        _variantEnabled.Toggled += _ => { UpdatePrefabEnabledUx3B(); InvalidatePrefabUx3B(); };
        _baseBlueprint = StudioUx2EnvBControls.Options();
        _baseBlueprint.ItemSelected += _ => InvalidatePrefabUx3B();
        _variantRequired = StudioUx2EnvBControls.Check("Variante requise au runtime");
        _variantRequired.Toggled += _ => InvalidatePrefabUx3B();
        prefabBody.AddChild(RowUx3B(_variantEnabled,
            StudioUx2EnvBControls.Field("Blueprint de base · même famille", _baseBlueprint, 500),
            _variantRequired));
        _variantOverrides = StudioUx2EnvBControls.Lines(
            "Une surcharge par ligne : Composant ; clé ; valeur", 104);
        _variantOverrides.TextChanged += InvalidatePrefabUx3B;
        prefabBody.AddChild(StudioUx2EnvBControls.Field(
            "Surcharges de variante · Composant, Matériau, Statistique ou Script", _variantOverrides));

        _compositionEnabled = StudioUx2EnvBControls.Check("Activer une composition de prefabs");
        _compositionEnabled.Toggled += _ => { UpdatePrefabEnabledUx3B(); InvalidatePrefabUx3B(); };
        _compositionRequired = StudioUx2EnvBControls.Check("Composition requise au runtime");
        _compositionRequired.Toggled += _ => InvalidatePrefabUx3B();
        prefabBody.AddChild(RowUx3B(_compositionEnabled, _compositionRequired));
        _compositionNodes = new ItemList
        {
            CustomMinimumSize = new Vector2(260, 156),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        _compositionNodes.ItemSelected += SelectNodeUx3B;
        _nodeBlueprint = StudioUx2EnvBControls.Options();
        _nodeBlueprint.ItemSelected += _ => InvalidatePrefabUx3B();
        _nodeParent = StudioUx2EnvBControls.Options();
        _nodeParent.ItemSelected += _ => InvalidatePrefabUx3B();
        _nodeSocket = StudioUx2EnvBControls.Text("socle");
        _nodeSocket.TextChanged += _ => InvalidatePrefabUx3B();
        _nodeTransform = Enumerable.Range(0, 10).Select(_ => StudioUx2EnvBControls.Text("0")).ToArray();
        _nodeTransform[3].Text = "0";
        _nodeTransform[4].Text = "0";
        _nodeTransform[5].Text = "0";
        _nodeTransform[6].Text = "1";
        _nodeTransform[7].Text = "1";
        _nodeTransform[8].Text = "1";
        _nodeTransform[9].Text = "1";
        foreach (var edit in _nodeTransform) edit.TextChanged += _ => InvalidatePrefabUx3B();
        _nodeAddOrUpdate = StudioStyle.Button("Ajouter le nœud", compact: true);
        _nodeRemove = StudioStyle.Button("Retirer le nœud", compact: true);
        _nodeAddOrUpdate.Pressed += AddOrUpdateNodeUx3B;
        _nodeRemove.Pressed += RemoveNodeUx3B;
        var nodeEditor = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        nodeEditor.AddThemeConstantOverride("separation", 6);
        nodeEditor.AddChild(RowUx3B(
            StudioUx2EnvBControls.Field("Blueprint du nœud", _nodeBlueprint),
            StudioUx2EnvBControls.Field("Parent humain", _nodeParent),
            StudioUx2EnvBControls.Field("Point d’assemblage", _nodeSocket)));
        nodeEditor.AddChild(RowUx3B(
            TransformFieldUx3B("Position X", _nodeTransform[0]), TransformFieldUx3B("Y", _nodeTransform[1]),
            TransformFieldUx3B("Z", _nodeTransform[2]), TransformFieldUx3B("Rotation X", _nodeTransform[3]),
            TransformFieldUx3B("Y", _nodeTransform[4]), TransformFieldUx3B("Z", _nodeTransform[5]),
            TransformFieldUx3B("W", _nodeTransform[6])));
        nodeEditor.AddChild(RowUx3B(
            TransformFieldUx3B("Échelle X", _nodeTransform[7]),
            TransformFieldUx3B("Y", _nodeTransform[8]),
            TransformFieldUx3B("Z", _nodeTransform[9]), _nodeAddOrUpdate, _nodeRemove));
        prefabBody.AddChild(RowUx3B(
            StudioUx2EnvBControls.Field("Nœuds ordonnés de composition", _compositionNodes), nodeEditor));
        _assemblyRules = StudioUx2EnvBControls.Lines("Une règle d’assemblage par ligne", 86);
        _parameters = StudioUx2EnvBControls.Lines("Un paramètre exposé par ligne", 86);
        _assemblyScripts = StudioUx2EnvBControls.Lines("Un script .forge gouverné par ligne", 86);
        _authorCollections = StudioUx2EnvBControls.Lines("Une collection humaine par ligne", 86);
        foreach (var lines in new[] { _assemblyRules, _parameters, _assemblyScripts, _authorCollections })
            lines.TextChanged += InvalidatePrefabUx3B;
        prefabBody.AddChild(RowUx3B(
            StudioUx2EnvBControls.Field("Règles d’assemblage", _assemblyRules),
            StudioUx2EnvBControls.Field("Paramètres", _parameters),
            StudioUx2EnvBControls.Field("Scripts gouvernés", _assemblyScripts),
            StudioUx2EnvBControls.Field("Collections auteur", _authorCollections)));
        _prefabApproval = StudioUx2EnvBControls.Check(
            "J’approuve séparément les variantes et compositions");
        _prefabApproval.Toggled += _ => InvalidatePrefabUx3B();
        prefabBody.AddChild(StudioUx2EnvBControls.Card("Ux3BPrefabApproval", "Approbation prefab",
            "Cette approbation ne vaut pas approbation de la propagation. Toute écriture impose un nouveau dry-run canonique.",
            _prefabApproval));
        var prefabButtons = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        prefabButtons.AddThemeConstantOverride("separation", 8);
        _prefabPreviewButton = StudioStyle.Button("Prévisualiser le prefab", compact: true);
        _prefabApplyButton = StudioStyle.Button("Appliquer le prefab", compact: true);
        _prefabPreviewButton.Pressed += PreviewPrefabUx3B;
        _prefabApplyButton.Pressed += ApplyPrefabUx3B;
        prefabButtons.AddChild(_prefabPreviewButton);
        prefabButtons.AddChild(_prefabApplyButton);
        prefabBody.AddChild(prefabButtons);
        var prefabReview = ReviewHostUx3B(out _prefabDiff, out _prefabDiagnostics);
        prefabBody.AddChild(prefabReview);
        _prefabSummary = StudioStyle.Label("Aucun aperçu prefab frais.", 10, StudioStyle.Muted, wrap: true);
        prefabBody.AddChild(_prefabSummary);

        root.AddChild(StudioStyle.Label(
            "Aperçu sec : aucune instance, ressource, asset, script, scène, moteur ou connexion réseau n’est exécuté dans Studio.",
            9, StudioStyle.Warning, wrap: true));
        var footer = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footer.AddThemeConstantOverride("separation", 8);
        _undoButton = StudioStyle.Button("Annuler", compact: true);
        _redoButton = StudioStyle.Button("Rétablir", compact: true);
        _saveButton = StudioStyle.Button("Enregistrer", compact: true);
        _undoButton.Pressed += UndoUx3B;
        _redoButton.Pressed += RedoUx3B;
        _saveButton.Pressed += SaveUx3B;
        footer.AddChild(_undoButton);
        footer.AddChild(_redoButton);
        footer.AddChild(_saveButton);
        _status = StudioStyle.Label("Aucune mutation préparée.", 10, StudioStyle.Muted, wrap: true);
        _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        footer.AddChild(_status);
        root.AddChild(footer);

        ResetControlsUx3B();
    }

    internal void Open() => OpenBoundUx3B(_sourceSession, evidence: false, cancelApprovals: false);
    internal void RestoreSourceSurfaceAfterSmoke() =>
        OpenBoundUx3B(_sourceSession, evidence: false, cancelApprovals: false);
    internal void HideSourceSurfaceAfterSmoke()
    {
        Visible = false;
        _session = _sourceSession;
    }

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null) return;
        if (Visible) _session.CancelBlueprintLibraryApprovalsUx3B();
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidenceUx3B();
        _session = _sourceSession;
        ResetControlsUx3B();
    }

    internal void CloseEvidence()
    {
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidenceUx3B();
        _session = _sourceSession;
        ResetControlsUx3B(keepReleaseState: true);
    }

    internal bool PrepareEvidence(string mode)
    {
        if (!mode.StartsWith("ux3-b", StringComparison.OrdinalIgnoreCase)) return false;
        ReleaseEvidenceUx3B();
        _sourceSnapshot = CaptureSourceUx3B();
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux3-b-ui-{Guid.NewGuid():N}");
        try
        {
            var evidence = _sourceSession.CreateBlueprintLibraryEvidenceProjectUx3B();
            if (evidence is null)
            {
                GD.PrintErr("OPENRPG_FORGE_STUDIO_UX3_B_UI_EVIDENCE_PROJECT_MISSING");
                return false;
            }
            var created = ProjectSession.Create(root, evidence.Project);
            if (!created.Succeeded)
            {
                GD.PrintErr("OPENRPG_FORGE_STUDIO_UX3_B_UI_CREATE_FAILED " +
                            string.Join('|', created.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                return false;
            }
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
            OpenBoundUx3B(isolated, evidence: true, cancelApprovals: true);
            if (!SelectEvidenceBlueprintUx3B(evidence))
            {
                GD.PrintErr("OPENRPG_FORGE_STUDIO_UX3_B_UI_STAGE_FAILED stage=blueprint_selection");
                return false;
            }
            if (!ConfigureEvidenceUpdateUx3B(evidence))
            {
                GD.PrintErr("OPENRPG_FORGE_STUDIO_UX3_B_UI_STAGE_FAILED stage=update_preview");
                return false;
            }
            if (!ConfigureEvidencePrefabUx3B(evidence))
            {
                GD.PrintErr("OPENRPG_FORGE_STUDIO_UX3_B_UI_STAGE_FAILED stage=prefab_preview");
                return false;
            }
            _tabs.CurrentTab = mode.EndsWith("-prefabs", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            var ready = HasExplicitSelection && UpdatePreviewReady && PrefabPreviewReady && ShowsTwoTransactions &&
                        ShowsHumanSurface && SourceMatchesUx3B() &&
                        (_usesRealModuleEvidence ^ _usesCanonicalFallbackEvidence);
            if (!ready)
                GD.PrintErr("OPENRPG_FORGE_STUDIO_UX3_B_UI_PREPARE_FAILED " +
                            $"selection={HasExplicitSelection.ToString().ToLowerInvariant()} " +
                            $"update={UpdatePreviewReady.ToString().ToLowerInvariant()} " +
                            $"prefab={PrefabPreviewReady.ToString().ToLowerInvariant()} " +
                            $"transactions={ShowsTwoTransactions.ToString().ToLowerInvariant()} " +
                            $"human={ShowsHumanSurface.ToString().ToLowerInvariant()} " +
                            $"source={SourceMatchesUx3B().ToString().ToLowerInvariant()}");
            return ready;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX3_B_EVIDENCE_FAILED type={exception.GetType().Name}");
            return false;
        }
        finally
        {
            if (!UpdatePreviewReady || !PrefabPreviewReady || !SourceMatchesUx3B())
            {
                if (_evidenceSession is not null) ReleaseEvidenceUx3B();
                else StudioSessionController.DeleteTemporaryBlueprintUx3A(root);
            }
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        var sourceApproval = _sourceSession.CaptureBlueprintLibraryApprovalUx3B();
        try
        {
            var ready = PrepareEvidence("ux3-b-blueprint-library");
            if (!ready || _evidenceSession is null || !ReferenceEquals(_session, _evidenceSession) ||
                !UpdatePreviewReady || _updateDraft is null || string.IsNullOrWhiteSpace(_evidenceRoot))
            {
                GD.PrintErr("OPENRPG_FORGE_STUDIO_UX3_B_UI_TRANSACTION_FAILED stage=prepare");
                return false;
            }
            var update = _session.ApplyApprovedUpdateUx3B(_updateDraft);
            if (!update.Succeeded)
            {
                GD.PrintErr("OPENRPG_FORGE_STUDIO_UX3_B_UI_TRANSACTION_FAILED stage=update_apply " +
                            HumanTextUx3B(update.Message));
                return false;
            }
            var evidence = _session.CreateBlueprintLibraryEvidenceProjectUx3B();
            var targetId = _updateDraft.BlueprintId;
            ReloadAfterMutationUx3B(targetId);
            var baseId = evidence?.BaseBlueprintId ?? _blueprints.First(item => item.Id != targetId).Id;
            if (!ConfigurePrefabForTransactionUx3B(baseId))
            {
                GD.PrintErr("OPENRPG_FORGE_STUDIO_UX3_B_UI_TRANSACTION_FAILED stage=prefab_rebase");
                return false;
            }
            var prefab = _session.ApplyApprovedPrefabUx3B(_prefabDraft!);
            var undoPrefab = prefab.Succeeded ? _session.UndoBlueprintLibraryUx3B() : new(false, "non exécuté");
            var undoUpdate = undoPrefab.Succeeded ? _session.UndoBlueprintLibraryUx3B() : new(false, "non exécuté");
            var redoUpdate = undoUpdate.Succeeded ? _session.RedoBlueprintLibraryUx3B() : new(false, "non exécuté");
            var redoPrefab = redoUpdate.Succeeded ? _session.RedoBlueprintLibraryUx3B() : new(false, "non exécuté");
            var saved = redoPrefab.Succeeded ? _session.SaveBlueprintLibraryUx3B() : new(false, "non exécuté");
            var expected = _session.Project is null ? string.Empty : ProjectJsonSerializer.Serialize(_session.Project);
            var reopened = new StudioSessionController();
            var roundTrip = saved.Succeeded && reopened.Open(_evidenceRoot).Succeeded &&
                            reopened.Project is not null && ProjectJsonSerializer.Serialize(reopened.Project) == expected;
            reopened.Close();
            var valid = update.Succeeded && prefab.Succeeded && undoPrefab.Succeeded && undoUpdate.Succeeded &&
                        redoUpdate.Succeeded && redoPrefab.Succeeded && saved.Succeeded && roundTrip && SourceMatchesUx3B();
            if (!valid)
                GD.PrintErr("OPENRPG_FORGE_STUDIO_UX3_B_UI_TRANSACTION_FAILED stage=history " +
                            $"prefab={prefab.Succeeded} undo_prefab={undoPrefab.Succeeded} " +
                            $"undo_update={undoUpdate.Succeeded} redo_update={redoUpdate.Succeeded} " +
                            $"redo_prefab={redoPrefab.Succeeded} save={saved.Succeeded} round_trip={roundTrip} " +
                            $"source={SourceMatchesUx3B()}");
            return valid;
        }
        finally
        {
            CloseEvidence();
            _sourceSession.RestoreBlueprintLibraryApprovalUx3B(sourceApproval);
        }
    }

    private void OpenBoundUx3B(StudioSessionController session, bool evidence, bool cancelApprovals)
    {
        _session = session;
        if (cancelApprovals) _session.CancelBlueprintLibraryApprovalsUx3B();
        Visible = true;
        _blueprints = _session.BlueprintChoicesUx3B;
        _updating = true;
        _blueprint.Clear();
        _blueprint.AddItem("Choisir explicitement un blueprint Projet…");
        foreach (var choice in _blueprints) _blueprint.AddItem(choice.Label);
        _blueprint.Select(0);
        _updating = false;
        _explicitSelection = false;
        _updateDraft = null;
        _prefabDraft = null;
        _instances = [];
        ResetSelectionControlsUx3B();
        ResetPreviewsUx3B();
        if (!evidence)
        {
            _usesRealModuleEvidence = false;
            _usesCanonicalFallbackEvidence = false;
        }
    }

    private void SelectBlueprintUx3B(long selected)
    {
        if (_updating) return;
        _session.CancelBlueprintLibraryApprovalsUx3B();
        _explicitSelection = selected > 0 && selected <= _blueprints.Count;
        if (!_explicitSelection)
        {
            _updateDraft = null;
            _prefabDraft = null;
            _instances = [];
            ResetSelectionControlsUx3B();
            ResetPreviewsUx3B();
            return;
        }
        var choice = _blueprints[(int)selected - 1];
        _instances = _session.InstanceChoicesUx3B(choice.Id);
        _prefabDraft = _session.CreatePrefabDraftUx3B(choice.Id);
        _updateDraft = null;
        _selection.Text = choice.IsReadOnly
            ? $"{choice.Label} · définition Standard protégée ; aucune écriture autorisée."
            : $"{choice.Label} · version {choice.Version} · sélection explicite prête.";
        _selection.Modulate = choice.IsReadOnly ? StudioStyle.Warning : StudioStyle.Success;
        PopulateBlueprintControlsUx3B(choice);
        ResetPreviewsUx3B();
    }

    private void SelectSourceInstanceUx3B(long selected)
    {
        if (_updating || !_explicitSelection || selected <= 0 || selected > _instances.Count)
        {
            _updateDraft = null;
            PopulateUpdateDraftUx3B();
            InvalidateUpdateUx3B();
            return;
        }
        var choice = _instances[(int)selected - 1];
        _updateDraft = _session.CreateUpdateDraftUx3B(choice.BlueprintId, choice.EntityId);
        PopulateUpdateDraftUx3B();
        ResetUpdatePreviewUx3B();
    }

    private void PopulateBlueprintControlsUx3B(StudioUx3BBlueprintChoice choice)
    {
        _updating = true;
        _sourceInstance.Clear();
        _sourceInstance.AddItem(_instances.Count == 0
            ? "État vide · aucune instance existante"
            : "Choisir explicitement une instance source…");
        foreach (var instance in _instances) _sourceInstance.AddItem(instance.Label);
        _sourceInstance.Select(0);
        _baseBlueprint.Clear();
        _baseBlueprint.AddItem("Choisir un blueprint de base…");
        foreach (var blueprint in _blueprints.Where(item => item.Id != choice.Id && item.Kind == choice.Kind))
            _baseBlueprint.AddItem(blueprint.Label);
        PopulatePrefabDraftUx3B();
        _updating = false;
        UpdateEditingStateUx3B();
    }

    private void PopulateUpdateDraftUx3B()
    {
        _updating = true;
        _promotedOverrides.Clear();
        _excludedInstances.Clear();
        if (_updateDraft is not null)
        {
            var source = _instances.First(item => item.EntityId == _updateDraft.SourceEntityId);
            foreach (var item in source.Overrides)
            {
                _promotedOverrides.AddItem($"{HumanPropertyUx3B(item.Key)} · {HumanValueKindUx3B(item.Kind)} · {HumanTextUx3B(item.Value)}");
                if (_updateDraft.PromotedOverrideKeys.Contains(item.Key))
                    _promotedOverrides.Select(_promotedOverrides.ItemCount - 1, false);
            }
            foreach (var item in _instances.Where(item => item.EntityId != _updateDraft.SourceEntityId))
            {
                _excludedInstances.AddItem(item.Label);
                if (_updateDraft.ExcludedEntityIds.Contains(item.EntityId))
                    _excludedInstances.Select(_excludedInstances.ItemCount - 1, false);
            }
            _versionBump.Select((int)_updateDraft.VersionBump);
            _categories.Text = string.Join('\n', _updateDraft.CategoryPaths);
            _updateApproval.ButtonPressed = _updateDraft.Approved;
        }
        else
        {
            _versionBump.Select(1);
            _categories.Text = string.Empty;
            _updateApproval.ButtonPressed = false;
        }
        _updating = false;
        UpdateEditingStateUx3B();
    }

    private void PopulatePrefabDraftUx3B()
    {
        if (_prefabDraft is null) return;
        _variantEnabled.ButtonPressed = _prefabDraft.VariantEnabled;
        var compatible = CompatibleBaseChoicesUx3B().ToArray();
        var baseIndex = _prefabDraft.BaseBlueprintId is { } baseId
            ? Array.FindIndex(compatible, item => item.Id == baseId)
            : -1;
        _baseBlueprint.Select(baseIndex + 1);
        _variantOverrides.Text = string.Join('\n', _prefabDraft.VariantOverrides.Select(item =>
            $"{HumanOverrideKindUx3B(item.Kind)} ; {item.Key} ; {HumanTextUx3B(item.Value)}"));
        _variantRequired.ButtonPressed = _prefabDraft.VariantRequiredAtRuntime;
        _compositionEnabled.ButtonPressed = _prefabDraft.CompositionEnabled;
        _compositionRequired.ButtonPressed = _prefabDraft.CompositionRequiredAtRuntime;
        _nodes = _prefabDraft.CompositionNodes.ToList();
        _assemblyRules.Text = string.Join('\n', _prefabDraft.AssemblyRules);
        _parameters.Text = string.Join('\n', _prefabDraft.Parameters);
        _assemblyScripts.Text = string.Join('\n', _prefabDraft.AssemblyScripts);
        _authorCollections.Text = string.Join('\n', _prefabDraft.AuthorCollections);
        _prefabApproval.ButtonPressed = _prefabDraft.Approved;
        PopulateNodeChoicesUx3B();
        RebuildNodesUx3B();
        UpdatePrefabEnabledUx3B();
    }

    private bool TryReadUpdateDraftUx3B(out StudioUx3BUpdateDraft? draft, out string error)
    {
        draft = null;
        error = string.Empty;
        if (_updateDraft is null)
        {
            error = "Choisissez explicitement une instance source.";
            return false;
        }
        var source = _instances.First(item => item.EntityId == _updateDraft.SourceEntityId);
        var promoted = new List<string>();
        for (var index = 0; index < source.Overrides.Count; index++)
            if (_promotedOverrides.IsSelected(index)) promoted.Add(source.Overrides[index].Key);
        var other = _instances.Where(item => item.EntityId != _updateDraft.SourceEntityId).ToArray();
        var excluded = new List<Guid>();
        for (var index = 0; index < other.Length; index++)
            if (_excludedInstances.IsSelected(index)) excluded.Add(other[index].EntityId);
        draft = _updateDraft with
        {
            PromotedOverrideKeys = promoted,
            VersionBump = (BlueprintVersionBump)_versionBump.Selected,
            ExcludedEntityIds = excluded,
            CategoryPaths = SplitLinesUx3B(_categories.Text),
            Approved = _updateApproval.ButtonPressed,
        };
        return true;
    }

    private bool TryReadPrefabDraftUx3B(out StudioUx3BPrefabDraft? draft, out string error)
    {
        draft = null;
        error = string.Empty;
        if (_prefabDraft is null)
        {
            error = "Choisissez explicitement un blueprint Projet.";
            return false;
        }
        var compatible = CompatibleBaseChoicesUx3B().ToArray();
        Guid? baseId = _baseBlueprint.Selected > 0 && _baseBlueprint.Selected <= compatible.Length
            ? compatible[_baseBlueprint.Selected - 1].Id
            : null;
        if (!TryParseVariantOverridesUx3B(_variantOverrides.Text, out var overrides, out error)) return false;
        draft = _prefabDraft with
        {
            VariantEnabled = _variantEnabled.ButtonPressed,
            BaseBlueprintId = baseId,
            VariantOverrides = overrides,
            VariantRequiredAtRuntime = _variantRequired.ButtonPressed,
            CompositionEnabled = _compositionEnabled.ButtonPressed,
            CompositionNodes = _nodes.ToArray(),
            AssemblyRules = SplitLinesUx3B(_assemblyRules.Text),
            Parameters = SplitLinesUx3B(_parameters.Text),
            AssemblyScripts = SplitLinesUx3B(_assemblyScripts.Text),
            CompositionRequiredAtRuntime = _compositionRequired.ButtonPressed,
            AuthorCollections = SplitLinesUx3B(_authorCollections.Text),
            Approved = _prefabApproval.ButtonPressed,
        };
        return true;
    }

    private void PreviewUpdateUx3B()
    {
        if (!TryReadUpdateDraftUx3B(out var draft, out var error) || draft is null)
        {
            SetLocalStatusUx3B(error, true);
            return;
        }
        _updateDraft = draft;
        _updatePreview = _session.PreviewUpdateUx3B(draft);
        RenderUpdatePreviewUx3B();
    }

    private void PreviewPrefabUx3B()
    {
        if (!TryReadPrefabDraftUx3B(out var draft, out var error) || draft is null)
        {
            SetLocalStatusUx3B(error, true);
            return;
        }
        _prefabDraft = draft;
        _prefabPreview = _session.PreviewPrefabUx3B(draft);
        RenderPrefabPreviewUx3B();
    }

    private void ApplyUpdateUx3B()
    {
        if (!TryReadUpdateDraftUx3B(out var draft, out var error) || draft is null || !UpdatePreviewReady ||
            _updatePreviewFingerprint != _session.UpdateDraftFingerprintUx3B(draft))
        {
            SetLocalStatusUx3B(error.Length > 0 ? error : "Le plan de propagation a changé ; relancez l’aperçu.", true);
            InvalidateUpdateUx3B();
            return;
        }
        var id = draft.BlueprintId;
        var result = _session.ApplyApprovedUpdateUx3B(draft);
        SetLocalStatusUx3B(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            BlueprintChanged?.Invoke();
            ReloadAfterMutationUx3B(id);
        }
    }

    private void ApplyPrefabUx3B()
    {
        if (!TryReadPrefabDraftUx3B(out var draft, out var error) || draft is null || !PrefabPreviewReady ||
            _prefabPreviewFingerprint != _session.PrefabDraftFingerprintUx3B(draft))
        {
            SetLocalStatusUx3B(error.Length > 0 ? error : "Le prefab a changé ; relancez l’aperçu.", true);
            InvalidatePrefabUx3B();
            return;
        }
        var id = draft.BlueprintId;
        var result = _session.ApplyApprovedPrefabUx3B(draft);
        SetLocalStatusUx3B(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            BlueprintChanged?.Invoke();
            ReloadAfterMutationUx3B(id);
        }
    }

    private void UndoUx3B()
    {
        var result = _session.UndoBlueprintLibraryUx3B();
        SetLocalStatusUx3B(result.Message, !result.Succeeded);
        if (result.Succeeded) { BlueprintChanged?.Invoke(); ReloadAfterMutationUx3B(_prefabDraft?.BlueprintId); }
    }

    private void RedoUx3B()
    {
        var result = _session.RedoBlueprintLibraryUx3B();
        SetLocalStatusUx3B(result.Message, !result.Succeeded);
        if (result.Succeeded) { BlueprintChanged?.Invoke(); ReloadAfterMutationUx3B(_prefabDraft?.BlueprintId); }
    }

    private void SaveUx3B()
    {
        var result = _session.SaveBlueprintLibraryUx3B();
        SetLocalStatusUx3B(result.Message, !result.Succeeded);
        UpdateButtonsUx3B();
    }

    private void ReloadAfterMutationUx3B(Guid? id)
    {
        _blueprints = _session.BlueprintChoicesUx3B;
        OpenBoundUx3B(_session, evidence: !ReferenceEquals(_session, _sourceSession), cancelApprovals: false);
        if (id is not { } target) return;
        var index = _blueprints.ToList().FindIndex(item => item.Id == target);
        if (index >= 0)
        {
            _blueprint.Select(index + 1);
            SelectBlueprintUx3B(index + 1);
        }
    }

    private void RenderUpdatePreviewUx3B()
    {
        ClearReviewUx3B(_updateDiff);
        ClearReviewUx3B(_updateDiagnostics);
        if (_updatePreview is null) { ResetUpdatePreviewUx3B(); return; }
        foreach (var item in _updatePreview.SemanticDiff)
            _updateDiff.AddChild(StudioStyle.Label("• " + HumanTextUx3B(item), 10, StudioStyle.Text, wrap: true));
        foreach (var item in _updatePreview.Instances.Take(8))
            _updateDiff.AddChild(StudioStyle.Label($"• {HumanTextUx3B(item.Label)} · {HumanMigrationUx3B(item.Action)}",
                9, item.Excluded ? StudioStyle.Warning : StudioStyle.Muted, wrap: true));
        if (_updatePreview.SemanticDiff.Count == 0)
            _updateDiff.AddChild(StudioStyle.Label("Aucun changement sémantique.", 10, StudioStyle.Muted));
        RenderDiagnosticsUx3B(_updateDiagnostics, _updatePreview.Diagnostics);
        _updateSummary.Text = HumanTextUx3B(_updatePreview.Message) +
                              " · aucune instance n’est réécrite pendant l’aperçu.";
        _updatePreviewReady = _updatePreview.Succeeded && _updatePreview.CanApply &&
                              _updatePreview.DryRunSucceeded && _updatePreview.RuntimeClaimsBounded &&
                              _updateDraft is not null;
        _updatePreviewFingerprint = _updatePreviewReady
            ? _session.UpdateDraftFingerprintUx3B(_updateDraft!) : null;
        SetLocalStatusUx3B(_updatePreview.Message, !_updatePreview.Succeeded);
        UpdateButtonsUx3B();
    }

    private void RenderPrefabPreviewUx3B()
    {
        ClearReviewUx3B(_prefabDiff);
        ClearReviewUx3B(_prefabDiagnostics);
        if (_prefabPreview is null) { ResetPrefabPreviewUx3B(); return; }
        foreach (var item in _prefabPreview.SemanticDiff)
            _prefabDiff.AddChild(StudioStyle.Label("• " + HumanTextUx3B(item), 10, StudioStyle.Text, wrap: true));
        if (_prefabPreview.SemanticDiff.Count == 0)
            _prefabDiff.AddChild(StudioStyle.Label("Aucun changement sémantique.", 10, StudioStyle.Muted));
        RenderDiagnosticsUx3B(_prefabDiagnostics, _prefabPreview.Diagnostics);
        _prefabSummary.Text = HumanTextUx3B(_prefabPreview.Message) +
                              " · résolution sur une copie immuable, sans placement ni asset chargé.";
        _prefabPreviewReady = _prefabPreview.Succeeded && _prefabPreview.CanApply &&
                              _prefabPreview.DryRunSucceeded && _prefabPreview.RuntimeClaimsBounded &&
                              _prefabDraft is not null;
        _prefabPreviewFingerprint = _prefabPreviewReady
            ? _session.PrefabDraftFingerprintUx3B(_prefabDraft!) : null;
        SetLocalStatusUx3B(_prefabPreview.Message, !_prefabPreview.Succeeded);
        UpdateButtonsUx3B();
    }

    private void AddOrUpdateNodeUx3B()
    {
        if (!_compositionEnabled.ButtonPressed || _nodeBlueprint.Selected <= 0)
        {
            SetLocalStatusUx3B("Choisissez le blueprint humain du nœud de composition.", true);
            return;
        }
        if (!TryReadTransformUx3B(out var transform, out var error))
        {
            SetLocalStatusUx3B(error, true);
            return;
        }
        var blueprintChoices = _blueprints.ToArray();
        var blueprintId = blueprintChoices[_nodeBlueprint.Selected - 1].Id;
        var selected = _compositionNodes.GetSelectedItems().FirstOrDefault(-1);
        Guid id = selected >= 0 && selected < _nodes.Count ? _nodes[selected].Id : Guid.NewGuid();
        var parents = ParentNodeChoicesUx3B(id).ToArray();
        Guid? parent = _nodeParent.Selected > 0 && _nodeParent.Selected <= parents.Length
            ? parents[_nodeParent.Selected - 1].Id
            : null;
        var node = new StudioUx3BCompositionNodeDraft(id, blueprintId, parent,
            string.IsNullOrWhiteSpace(_nodeSocket.Text) ? null : _nodeSocket.Text.Trim(), transform);
        if (selected >= 0 && selected < _nodes.Count) _nodes[selected] = node;
        else _nodes.Add(node);
        RebuildNodesUx3B(id);
        InvalidatePrefabUx3B();
    }

    private void RemoveNodeUx3B()
    {
        var selected = _compositionNodes.GetSelectedItems().FirstOrDefault(-1);
        if (selected < 0 || selected >= _nodes.Count) return;
        var id = _nodes[selected].Id;
        if (_nodes.Any(item => item.ParentNodeId == id))
        {
            SetLocalStatusUx3B("Retirez d’abord les nœuds enfants de ce parent.", true);
            return;
        }
        _nodes.RemoveAt(selected);
        RebuildNodesUx3B();
        InvalidatePrefabUx3B();
    }

    private void SelectNodeUx3B(long selected)
    {
        if (_updating || selected < 0 || selected >= _nodes.Count) return;
        var node = _nodes[(int)selected];
        _updating = true;
        var blueprintIndex = _blueprints.ToList().FindIndex(item => item.Id == node.BlueprintId);
        _nodeBlueprint.Select(blueprintIndex + 1);
        PopulateParentChoicesUx3B(node.Id, node.ParentNodeId);
        _nodeSocket.Text = node.Socket ?? string.Empty;
        SetTransformUx3B(node.Transform);
        _nodeAddOrUpdate.Text = "Mettre à jour le nœud";
        _updating = false;
    }

    private void RebuildNodesUx3B(Guid? select = null)
    {
        _compositionNodes.Clear();
        for (var index = 0; index < _nodes.Count; index++)
        {
            var node = _nodes[index];
            var blueprint = _blueprints.FirstOrDefault(item => item.Id == node.BlueprintId);
            var parentIndex = node.ParentNodeId is { } parent
                ? _nodes.FindIndex(item => item.Id == parent) + 1
                : 0;
            _compositionNodes.AddItem($"Nœud {index + 1} · {blueprint?.Label ?? "blueprint hérité protégé"} · " +
                                      (parentIndex == 0 ? "racine" : $"enfant du nœud {parentIndex}"));
        }
        PopulateNodeChoicesUx3B();
        if (select is { } id)
        {
            var index = _nodes.FindIndex(item => item.Id == id);
            if (index >= 0) { _compositionNodes.Select(index); SelectNodeUx3B(index); }
        }
        else ResetNodeEditorUx3B();
    }

    private void PopulateNodeChoicesUx3B()
    {
        _nodeBlueprint.Clear();
        _nodeBlueprint.AddItem("Choisir un blueprint…");
        foreach (var choice in _blueprints) _nodeBlueprint.AddItem(choice.Label);
        PopulateParentChoicesUx3B(null, null);
    }

    private void PopulateParentChoicesUx3B(Guid? current, Guid? selected)
    {
        _nodeParent.Clear();
        _nodeParent.AddItem("Racine de composition");
        var parents = ParentNodeChoicesUx3B(current).ToArray();
        foreach (var item in parents)
        {
            var index = _nodes.FindIndex(node => node.Id == item.Id);
            _nodeParent.AddItem($"Nœud {index + 1}");
        }
        var selectedIndex = selected is { } parent ? Array.FindIndex(parents, item => item.Id == parent) : -1;
        _nodeParent.Select(selectedIndex + 1);
    }

    private IEnumerable<StudioUx3BCompositionNodeDraft> ParentNodeChoicesUx3B(Guid? current) =>
        _nodes.Where(item => item.Id != current);

    private void ResetNodeEditorUx3B()
    {
        _updating = true;
        _nodeBlueprint.Select(0);
        PopulateParentChoicesUx3B(null, null);
        _nodeSocket.Text = string.Empty;
        SetTransformUx3B(TransformValue.Identity);
        _nodeAddOrUpdate.Text = "Ajouter le nœud";
        _updating = false;
    }

    private void UpdatePrefabEnabledUx3B()
    {
        var variant = _variantEnabled.ButtonPressed;
        _baseBlueprint.Disabled = !variant;
        _variantOverrides.Editable = variant;
        _variantRequired.Disabled = !variant;
        var composition = _compositionEnabled.ButtonPressed;
        _compositionNodes.MouseFilter = composition ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        _nodeBlueprint.Disabled = !composition;
        _nodeParent.Disabled = !composition;
        _nodeSocket.Editable = composition;
        foreach (var edit in _nodeTransform) edit.Editable = composition;
        _nodeAddOrUpdate.Disabled = !composition;
        _nodeRemove.Disabled = !composition;
        _assemblyRules.Editable = composition;
        _parameters.Editable = composition;
        _assemblyScripts.Editable = composition;
        _compositionRequired.Disabled = !composition;
    }

    private void InvalidateUpdateUx3B()
    {
        if (_updating) return;
        _session.CancelUpdateBlueprintApprovalUx3B();
        _updatePreviewReady = false;
        _updatePreviewFingerprint = null;
        _updateApplyButton.Disabled = true;
        _updateSummary.Text = "Brouillon de propagation modifié · relancez l’aperçu sec canonique.";
    }

    private void InvalidatePrefabUx3B()
    {
        if (_updating) return;
        _session.CancelPrefabBlueprintApprovalUx3B();
        _prefabPreviewReady = false;
        _prefabPreviewFingerprint = null;
        _prefabApplyButton.Disabled = true;
        _prefabSummary.Text = "Brouillon prefab modifié · relancez l’aperçu sec canonique.";
    }

    private void ResetPreviewsUx3B()
    {
        ResetUpdatePreviewUx3B();
        ResetPrefabPreviewUx3B();
    }

    private void ResetUpdatePreviewUx3B()
    {
        _updatePreview = null;
        _updatePreviewReady = false;
        _updatePreviewFingerprint = null;
        ClearReviewUx3B(_updateDiff);
        ClearReviewUx3B(_updateDiagnostics);
        _updateDiff.AddChild(StudioStyle.Label("Aucun aperçu frais.", 10, StudioStyle.Muted));
        _updateDiagnostics.AddChild(StudioStyle.Label("Aucun diagnostic.", 10, StudioStyle.Muted));
        _updateSummary.Text = _updateDraft is null
            ? "Choisissez une instance source."
            : "Propagation chargée · configurez, approuvez puis prévisualisez.";
        UpdateButtonsUx3B();
    }

    private void ResetPrefabPreviewUx3B()
    {
        _prefabPreview = null;
        _prefabPreviewReady = false;
        _prefabPreviewFingerprint = null;
        ClearReviewUx3B(_prefabDiff);
        ClearReviewUx3B(_prefabDiagnostics);
        _prefabDiff.AddChild(StudioStyle.Label("Aucun aperçu frais.", 10, StudioStyle.Muted));
        _prefabDiagnostics.AddChild(StudioStyle.Label("Aucun diagnostic.", 10, StudioStyle.Muted));
        _prefabSummary.Text = _prefabDraft is null
            ? "Choisissez un blueprint Projet."
            : "Prefab chargé · configurez, approuvez puis prévisualisez.";
        UpdateButtonsUx3B();
    }

    private void ResetSelectionControlsUx3B()
    {
        _updating = true;
        _sourceInstance.Clear();
        _sourceInstance.AddItem("Choisir explicitement une instance source…");
        _promotedOverrides.Clear();
        _excludedInstances.Clear();
        _categories.Text = string.Empty;
        _updateApproval.ButtonPressed = false;
        _baseBlueprint.Clear();
        _baseBlueprint.AddItem("Choisir un blueprint de base…");
        _variantEnabled.ButtonPressed = false;
        _variantOverrides.Text = string.Empty;
        _variantRequired.ButtonPressed = false;
        _compositionEnabled.ButtonPressed = false;
        _compositionRequired.ButtonPressed = false;
        _nodes = [];
        _compositionNodes.Clear();
        _assemblyRules.Text = string.Empty;
        _parameters.Text = string.Empty;
        _assemblyScripts.Text = string.Empty;
        _authorCollections.Text = string.Empty;
        _prefabApproval.ButtonPressed = false;
        PopulateNodeChoicesUx3B();
        ResetNodeEditorUx3B();
        _updating = false;
        UpdatePrefabEnabledUx3B();
        UpdateEditingStateUx3B();
    }

    private void ResetControlsUx3B(bool keepReleaseState = false)
    {
        _blueprints = [];
        _instances = [];
        _updateDraft = null;
        _prefabDraft = null;
        _explicitSelection = false;
        _updating = true;
        _blueprint.Clear();
        _blueprint.AddItem("Choisir explicitement un blueprint Projet…");
        _blueprint.Select(0);
        _selection.Text = "Choisissez explicitement un blueprint Projet ; aucune définition ni instance n’est sélectionnée automatiquement.";
        _selection.Modulate = StudioStyle.Warning;
        _updating = false;
        ResetSelectionControlsUx3B();
        ResetPreviewsUx3B();
        if (!keepReleaseState) _evidenceReleased = _evidenceSession is null;
    }

    private void UpdateEditingStateUx3B()
    {
        var choice = _prefabDraft is null ? null : _blueprints.FirstOrDefault(item => item.Id == _prefabDraft.BlueprintId);
        var editable = choice is { IsReadOnly: false };
        _sourceInstance.Disabled = !editable || _instances.Count == 0;
        _promotedOverrides.MouseFilter = editable ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        _versionBump.Disabled = !editable;
        _excludedInstances.MouseFilter = editable ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        _categories.Editable = editable;
        _updateApproval.Disabled = !editable;
        _prefabApproval.Disabled = !editable;
        _variantEnabled.Disabled = !editable;
        _compositionEnabled.Disabled = !editable;
        _authorCollections.Editable = editable;
        UpdatePrefabEnabledUx3B();
        UpdateButtonsUx3B();
    }

    private void UpdateButtonsUx3B()
    {
        var editable = _prefabDraft is not null &&
                       _blueprints.FirstOrDefault(item => item.Id == _prefabDraft.BlueprintId)?.IsReadOnly == false;
        _updatePreviewButton.Disabled = !editable || _updateDraft is null;
        _updateApplyButton.Disabled = !UpdatePreviewReady;
        _prefabPreviewButton.Disabled = !editable;
        _prefabApplyButton.Disabled = !PrefabPreviewReady;
        _undoButton.Disabled = _session.History?.CanUndo != true;
        _redoButton.Disabled = _session.History?.CanRedo != true;
        _saveButton.Disabled = !_session.IsDirty;
    }

    private bool SelectEvidenceBlueprintUx3B(StudioUx3BEvidenceProject evidence)
    {
        var index = _blueprints.ToList().FindIndex(item => item.Id == evidence.BlueprintId);
        if (index < 0) return false;
        _blueprint.Select(index + 1);
        SelectBlueprintUx3B(index + 1);
        return _explicitSelection && _prefabDraft is not null;
    }

    private bool ConfigureEvidenceUpdateUx3B(StudioUx3BEvidenceProject evidence)
    {
        var index = _instances.ToList().FindIndex(item => item.EntityId == evidence.SourceEntityId);
        if (index < 0) return false;
        _sourceInstance.Select(index + 1);
        SelectSourceInstanceUx3B(index + 1);
        if (_updateDraft is null || _promotedOverrides.ItemCount == 0) return false;
        _promotedOverrides.Select(0, false);
        var excluded = _instances.Where(item => item.EntityId != evidence.SourceEntityId).ToArray();
        var excludedIndex = Array.FindIndex(excluded, item => item.EntityId == evidence.ExcludedEntityId);
        if (excludedIndex >= 0) _excludedInstances.Select(excludedIndex, false);
        _versionBump.Select((int)BlueprintVersionBump.Minor);
        _categories.Text = "Monde/Créatures/Gardiens\nBibliothèque/UX3";
        _updateApproval.ButtonPressed = true;
        PreviewUpdateUx3B();
        return UpdatePreviewReady;
    }

    private bool ConfigureEvidencePrefabUx3B(StudioUx3BEvidenceProject evidence)
    {
        if (_prefabDraft is null) return false;
        var compatible = CompatibleBaseChoicesUx3B().ToArray();
        var index = Array.FindIndex(compatible, item => item.Id == evidence.BaseBlueprintId);
        if (index < 0) return false;
        _variantEnabled.ButtonPressed = true;
        _baseBlueprint.Select(index + 1);
        _variantOverrides.Text = "Matériau ; body ; asset:ember";
        _variantRequired.ButtonPressed = true;
        _compositionEnabled.ButtonPressed = true;
        _compositionRequired.ButtonPressed = true;
        _nodes = [new StudioUx3BCompositionNodeDraft(
            StableEvidenceGuidUx3B(evidence.BlueprintId, "root"), evidence.BaseBlueprintId, null, "socle",
            new TransformValue(new Vector3Value(2, 0, 3), QuaternionValue.Identity, Vector3Value.One))];
        RebuildNodesUx3B();
        _assemblyRules.Text = "socket-compatible";
        _parameters.Text = "intensity";
        _assemblyScripts.Text = string.Empty;
        _authorCollections.Text = "Bosses\nCrypt/Encounter";
        _prefabApproval.ButtonPressed = true;
        PreviewPrefabUx3B();
        return PrefabPreviewReady;
    }

    private bool ConfigurePrefabForTransactionUx3B(Guid baseId)
    {
        if (_prefabDraft is null) return false;
        var evidence = new StudioUx3BEvidenceProject(_session.Project!, _prefabDraft.BlueprintId, baseId,
            Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, false, true);
        return ConfigureEvidencePrefabUx3B(evidence);
    }

    private IEnumerable<StudioUx3BBlueprintChoice> CompatibleBaseChoicesUx3B()
    {
        var current = _prefabDraft is null ? null : _blueprints.FirstOrDefault(item => item.Id == _prefabDraft.BlueprintId);
        return current is null ? [] : _blueprints.Where(item => item.Id != current.Id && item.Kind == current.Kind);
    }

    private SourceSnapshotUx3B CaptureSourceUx3B()
    {
        var history = _sourceSession.History;
        return new(_sourceSession.Project, history, history?.State.Revision, history?.Journal.ToArray() ?? [],
            _sourceSession.IsDirty, history?.CanUndo, history?.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureBlueprintLibraryApprovalUx3B());
    }

    private bool SourceMatchesUx3B()
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
               _sourceSession.CaptureBlueprintLibraryApprovalUx3B() == snapshot.Approval;
    }

    private void ReleaseEvidenceUx3B()
    {
        _evidenceSession?.Close();
        _evidenceSession = null;
        _evidenceReleased = string.IsNullOrWhiteSpace(_evidenceRoot) ||
                            StudioSessionController.DeleteTemporaryBlueprintUx3A(_evidenceRoot);
        _evidenceRoot = null;
        _session = _sourceSession;
    }

    private static bool TryParseVariantOverridesUx3B(string text,
        out IReadOnlyList<StudioUx3BVariantOverrideDraft> result, out string error)
    {
        var values = new List<StudioUx3BVariantOverrideDraft>();
        foreach (var line in SplitLinesUx3B(text))
        {
            var parts = line.Split(';', 3, StringSplitOptions.TrimEntries);
            if (parts.Length != 3 || !TryParseOverrideKindUx3B(parts[0], out var kind) ||
                parts[1].Length == 0 || parts[2].Length == 0)
            {
                result = [];
                error = "Chaque surcharge exige Type ; Clé ; Valeur.";
                return false;
            }
            values.Add(new StudioUx3BVariantOverrideDraft(kind, parts[1], parts[2]));
        }
        result = values;
        error = string.Empty;
        return true;
    }

    private static bool TryParseOverrideKindUx3B(string value, out PrefabVariantOverrideKind kind)
    {
        var normalized = value.Trim().ToLowerInvariant();
        kind = normalized switch
        {
            "composant" or "component" => PrefabVariantOverrideKind.Component,
            "matériau" or "materiau" or "material" => PrefabVariantOverrideKind.Material,
            "statistique" or "stat" => PrefabVariantOverrideKind.Stat,
            "script" => PrefabVariantOverrideKind.Script,
            _ => (PrefabVariantOverrideKind)(-1),
        };
        return Enum.IsDefined(kind);
    }

    private bool TryReadTransformUx3B(out TransformValue transform, out string error)
    {
        var values = new double[10];
        for (var index = 0; index < values.Length; index++)
        {
            if (!double.TryParse(_nodeTransform[index].Text.Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out values[index]) || !double.IsFinite(values[index]))
            {
                transform = TransformValue.Identity;
                error = "La transform du nœud exige dix nombres finis exacts.";
                return false;
            }
        }
        try
        {
            transform = new TransformValue(new Vector3Value((float)values[0], (float)values[1], (float)values[2]),
                new QuaternionValue((float)values[3], (float)values[4], (float)values[5], (float)values[6]),
                new Vector3Value((float)values[7], (float)values[8], (float)values[9]));
            error = string.Empty;
            return true;
        }
        catch (ArgumentException exception)
        {
            transform = TransformValue.Identity;
            error = exception.Message;
            return false;
        }
    }

    private void SetTransformUx3B(TransformValue transform)
    {
        var values = new[]
        {
            transform.Position.X, transform.Position.Y, transform.Position.Z,
            transform.Rotation.X, transform.Rotation.Y, transform.Rotation.Z, transform.Rotation.W,
            transform.Scale.X, transform.Scale.Y, transform.Scale.Z,
        };
        for (var index = 0; index < values.Length; index++)
            _nodeTransform[index].Text = values[index].ToString("R", CultureInfo.InvariantCulture);
    }

    private void SetLocalStatusUx3B(string message, bool error)
    {
        _status.Text = HumanTextUx3B(message);
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Success;
        StatusChanged?.Invoke(_status.Text, error);
    }

    private static void RenderDiagnosticsUx3B(VBoxContainer host,
        IReadOnlyList<StudioUx3BDiagnostic> diagnostics)
    {
        foreach (var item in diagnostics)
            host.AddChild(StudioStyle.Label($"• {HumanDiagnosticUx3B(item.Code)} · {HumanTextUx3B(item.Message)}",
                10, StudioStyle.Danger, wrap: true));
        if (diagnostics.Count == 0)
            host.AddChild(StudioStyle.Label("Aucun diagnostic bloquant.", 10, StudioStyle.Success));
    }

    private static ScrollContainer ScrollTabUx3B(string title, out VBoxContainer body)
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

    private static ItemList MultiListUx3B(float height) => new()
    {
        CustomMinimumSize = new Vector2(0, height),
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        SelectMode = ItemList.SelectModeEnum.Multi,
    };

    private static HBoxContainer RowUx3B(params Control[] controls)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 8);
        foreach (var control in controls) row.AddChild(control);
        return row;
    }

    private static VBoxContainer TransformFieldUx3B(string title, Control control) =>
        StudioUx2EnvBControls.Field(title, control, 90);

    private static HBoxContainer ReviewHostUx3B(out VBoxContainer diff, out VBoxContainer diagnostics)
    {
        var host = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 118),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        host.AddThemeConstantOverride("separation", 8);
        diff = ReviewColumnUx3B(host, "Diff sémantique");
        diagnostics = ReviewColumnUx3B(host, "Diagnostics humains");
        return host;
    }

    private static VBoxContainer ReviewColumnUx3B(HBoxContainer host, string title)
    {
        var panel = StudioStyle.Panel("Ux3B" + title.Replace(" ", string.Empty), StudioStyle.Surface, 8, 1);
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

    private static void ClearReviewUx3B(Node root)
    {
        foreach (var child in root.GetChildren().Skip(1).ToArray())
        {
            root.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static IReadOnlyList<string> SplitLinesUx3B(string text) =>
        text.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim()).Where(item => item.Length > 0).ToArray();

    private static string HumanOverrideKindUx3B(PrefabVariantOverrideKind kind) => kind switch
    {
        PrefabVariantOverrideKind.Component => "Composant",
        PrefabVariantOverrideKind.Material => "Matériau",
        PrefabVariantOverrideKind.Stat => "Statistique",
        PrefabVariantOverrideKind.Script => "Script",
        _ => "Type protégé",
    };

    private static string HumanValueKindUx3B(EntityPropertyValueKind kind) => kind switch
    {
        EntityPropertyValueKind.String => "texte",
        EntityPropertyValueKind.Number => "nombre",
        EntityPropertyValueKind.Boolean => "oui/non",
        _ => "valeur protégée",
    };

    private static string HumanPropertyUx3B(string value) => value switch
    {
        "visual.scale" => "Échelle visuelle",
        "appearance.variant" => "Variante d’apparence",
        "instance.note" => "Note de l’instance",
        _ => HumanTextUx3B(value.Replace('.', ' ')),
    };

    private static string HumanMigrationUx3B(string action) => action switch
    {
        "source" => "instance source conservée",
        "migrate" => "contrainte migrée",
        "excluded" => "exclusion protégée",
        _ => HumanTextUx3B(action),
    };

    private static string HumanDiagnosticUx3B(string code) => code switch
    {
        "blueprint.update.approval_required" => "Approbation propagation requise",
        "blueprint.prefab.approval_required" => "Approbation prefab requise",
        "blueprint.read_only" => "Définition Standard protégée",
        "command.revision_conflict" => "Projet modifié",
        "blueprint.variant.reference_missing" => "Base de variante invalide",
        "blueprint.composition.hierarchy_invalid" => "Hiérarchie de composition invalide",
        _ => "Diagnostic canonique",
    };

    private static string HumanTextUx3B(string text) => UuidRegexUx3B().Replace(text ?? string.Empty,
        "référence protégée");

    private static IEnumerable<string> DescendantTextUx3B(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Label label) yield return label.Text;
            if (child is Button button) yield return button.Text;
            if (child is LineEdit line) yield return line.Text;
            if (child is TextEdit text) yield return text.Text;
            foreach (var nested in DescendantTextUx3B(child)) yield return nested;
        }
    }

    private static Guid StableEvidenceGuidUx3B(Guid source, string discriminator)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{source:D}|ux3-b-ui|{discriminator}"));
        return new Guid(bytes.AsSpan(0, 16));
    }

    [GeneratedRegex(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex UuidRegexUx3B();

    private sealed record SourceSnapshotUx3B(
        ProjectDefinition? Project,
        ProjectCommandHistory? History,
        ProjectRevision? Revision,
        IReadOnlyList<CommandHistoryJournalEntry> Journal,
        bool Dirty,
        bool? CanUndo,
        bool? CanRedo,
        AreaId? ActiveArea,
        IReadOnlyList<EntityId> Selection,
        EntityId? ActiveSelection,
        StudioUx3BApprovalSnapshot Approval);
}
