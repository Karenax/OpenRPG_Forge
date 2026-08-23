using System.Globalization;
using System.Text.RegularExpressions;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioBlueprintCatalogWorkbenchUx3A : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private SourceSnapshotUx3A? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;
    private bool _usesRealModuleEvidence;
    private bool _usesCanonicalFallbackEvidence;
    private bool _updating;
    private bool _explicitSelection;

    private readonly OptionButton _operation;
    private readonly OptionButton _blueprint;
    private readonly OptionButton _entity;
    private readonly Label _selection;
    private readonly TabContainer _tabs;
    private readonly LineEdit _slug;
    private readonly LineEdit _displayName;
    private readonly Label _kind;
    private readonly LineEdit _version;
    private readonly LineEdit _packageNamespace;
    private readonly Label _manifestSummary;
    private readonly LineEdit _byteBudget;
    private readonly TextEdit _parameters;
    private readonly TextEdit _dependencies;
    private readonly Label _protectedDependencies;
    private readonly Label _instanceInfo;
    private readonly OptionButton _paletteView;
    private readonly ItemList _paletteList;
    private readonly Label _paletteSummary;
    private readonly CheckBox _approval;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _previewButton;
    private readonly Button _applyButton;
    private readonly Button _undoButton;
    private readonly Button _redoButton;
    private readonly Button _saveButton;

    private IReadOnlyList<StudioUx3ABlueprintChoice> _blueprints = [];
    private IReadOnlyList<StudioUx3AEntityChoice> _entities = [];
    private IReadOnlyList<string> _protectedDependencyValues = [];
    private StudioUx3ADraft? _draft;
    private StudioUx3APreview? _preview;
    private string? _previewFingerprint;
    private bool _previewReady;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? BlueprintChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _explicitSelection && _draft is not null;
    internal bool PreviewReady => _previewReady && _preview is
    {
        Succeeded: true, CanApply: true, DryRunSucceeded: true, RuntimeClaimsBounded: true,
    };
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealModuleEvidence => _usesRealModuleEvidence;
    internal bool UsesCanonicalFallbackEvidence => _usesCanonicalFallbackEvidence;
    internal bool EvidenceSourceUntouched => SourceMatchesUx3A();
    internal bool EvidenceResourcesReleased => _evidenceReleased && _evidenceSession is null && _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool ShowsThreeWorkflows => _tabs.GetTabCount() == 3 &&
                                         _tabs.GetTabTitle(0) == "Blueprint réutilisable" &&
                                         _tabs.GetTabTitle(1) == "Instance et dérivation" &&
                                         _tabs.GetTabTitle(2) == "Palettes Standard et Projet";
    internal bool ShowsStandardAndProject =>
        _blueprints.Any(item => item.Origin == BlueprintPaletteOrigin.InstalledPack && item.IsReadOnly) &&
        _blueprints.Any(item => item.Origin == BlueprintPaletteOrigin.ProjectContent && !item.IsReadOnly);
    internal bool ShowsInstanceWorkflow => _entities.Count > 0 && _entity.ItemCount == _entities.Count + 1;
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx3A(this)
                .Concat(Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle)));
            return text.Contains("Blueprints et palettes", StringComparison.Ordinal) &&
                   text.Contains("Blueprint réutilisable", StringComparison.Ordinal) &&
                   text.Contains("Instance et dérivation", StringComparison.Ordinal) &&
                   text.Contains("Palettes Standard et Projet", StringComparison.Ordinal) &&
                   text.Contains("aperçu sec canonique", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucune instance", StringComparison.OrdinalIgnoreCase) &&
                   !UuidRegexUx3A().IsMatch(text);
        }
    }

    internal StudioBlueprintCatalogWorkbenchUx3A(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.012f, .018f, .027f, .975f);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        var dialog = StudioStyle.Panel("Ux3ABlueprintCatalogDialog", StudioStyle.Surface, 12, 1);
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
        headingText.AddChild(StudioStyle.Label("Blueprints et palettes", 18));
        headingText.AddChild(StudioStyle.Label(
            "Définition réutilisable, instance dérivée et palettes Standard/Projet · aperçu sec canonique avant toute écriture.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        root.AddChild(heading);

        _operation = StudioUx2EnvBControls.Options(
            "Modifier un blueprint existant", "Créer depuis une instance existante");
        _operation.ItemSelected += ChangeOperationUx3A;
        _blueprint = StudioUx2EnvBControls.Options();
        _blueprint.ItemSelected += SelectBlueprintUx3A;
        _entity = StudioUx2EnvBControls.Options();
        _entity.ItemSelected += SelectEntityUx3A;
        root.AddChild(RowUx3A(
            StudioUx2EnvBControls.Field("Parcours", _operation, 250),
            StudioUx2EnvBControls.Field("Blueprint · sélection explicite", _blueprint, 390),
            StudioUx2EnvBControls.Field("Instance · sélection explicite", _entity, 390)));
        _selection = StudioStyle.Label(
            "Choisissez explicitement une définition ou une instance ; rien n’est sélectionné automatiquement.",
            10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        var tabsHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 340),
            CustomMaximumSize = new Vector2(-1, 585),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            PropagateMaximumSize = true,
        };
        root.AddChild(tabsHost);
        _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        tabsHost.AddChild(_tabs);

        var definition = ScrollTabUx3A("Blueprint réutilisable", out var definitionBody);
        _tabs.AddChild(definition);
        _slug = StudioUx2EnvBControls.Text("objet-reutilisable");
        _displayName = StudioUx2EnvBControls.Text("Objet réutilisable");
        _kind = StudioStyle.Label("Type protégé · sélectionnez une définition", 10, StudioStyle.Muted, wrap: true);
        definitionBody.AddChild(RowUx3A(
            StudioUx2EnvBControls.Field("Clé auteur", _slug),
            StudioUx2EnvBControls.Field("Nom humain", _displayName),
            StudioUx2EnvBControls.Field("Famille canonique", _kind)));
        _version = StudioUx2EnvBControls.Text("1.0.0");
        _packageNamespace = StudioUx2EnvBControls.Text("project.objet.reutilisable");
        _byteBudget = StudioUx2EnvBControls.Text("1048576");
        definitionBody.AddChild(RowUx3A(
            StudioUx2EnvBControls.Field("Version sémantique", _version),
            StudioUx2EnvBControls.Field("Namespace du package", _packageNamespace),
            StudioUx2EnvBControls.Field("Budget plateforme · octets", _byteBudget)));
        _manifestSummary = StudioStyle.Label(
            "Manifeste et aperçu gouvernés · chemins techniques protégés et conservés sans réécriture.",
            10, StudioStyle.Muted, wrap: true);
        definitionBody.AddChild(StudioUx2EnvBControls.Card("Ux3AManifest", "Gouvernance du contenu",
            "Le Studio conserve les chemins canoniques, la provenance, le kit, la composition et tous les profils voisins.",
            _manifestSummary));
        _parameters = StudioUx2EnvBControls.Lines("Un paramètre exposé par ligne", 82);
        _dependencies = StudioUx2EnvBControls.Lines("Une dépendance éditable par ligne", 82);
        definitionBody.AddChild(RowUx3A(
            StudioUx2EnvBControls.Field("Paramètres exposés", _parameters),
            StudioUx2EnvBControls.Field("Dépendances gouvernées", _dependencies)));
        _protectedDependencies = StudioStyle.Label("Aucune dépendance héritée protégée.", 9,
            StudioStyle.Muted, wrap: true);
        definitionBody.AddChild(_protectedDependencies);
        _approval = StudioUx2EnvBControls.Check(
            "J’approuve cette définition et son application dans la palette Projet");
        definitionBody.AddChild(StudioUx2EnvBControls.Card("Ux3AApproval", "Approbation auteur",
            "L’aperçu lie la révision, le projet et l’empreinte exacte du brouillon. Toute modification l’invalide.",
            _approval));

        var instance = ScrollTabUx3A("Instance et dérivation", out var instanceBody);
        _tabs.AddChild(instance);
        _instanceInfo = StudioStyle.Label(
            "Choisissez une instance existante. Sa transform, ses tags et ses surcharges resteront bit à bit inchangés.",
            11, StudioStyle.Text, wrap: true);
        instanceBody.AddChild(StudioUx2EnvBControls.Card("Ux3AInstance", "Créer depuis une instance",
            "La commande crée un blueprint Projet puis retargete uniquement l’instance choisie. Aucune instance n’est placée, déplacée ou exécutée par Studio.",
            _instanceInfo));
        instanceBody.AddChild(StudioStyle.Label(
            "Le type canonique et tous les profils gameplay proviennent du blueprint source. La version de la nouvelle instance est épinglée explicitement.",
            10, StudioStyle.Muted, wrap: true));

        var palettes = ScrollTabUx3A("Palettes Standard et Projet", out var palettesBody);
        _tabs.AddChild(palettes);
        _paletteView = StudioUx2EnvBControls.Options("Toutes", "Standard installés", "Contenu du projet");
        _paletteView.ItemSelected += _ => RebuildPaletteUx3A();
        palettesBody.AddChild(StudioUx2EnvBControls.Field("Vue de palette", _paletteView, 280));
        _paletteList = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 190),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        palettesBody.AddChild(_paletteList);
        _paletteSummary = StudioStyle.Label(
            "Les définitions Standard sont installées et protégées ; le contenu Projet est éditable et persistant.",
            10, StudioStyle.Muted, wrap: true);
        palettesBody.AddChild(_paletteSummary);

        var review = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 120),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        review.AddThemeConstantOverride("separation", 8);
        _diff = ReviewColumnUx3A(review, "Diff sémantique");
        _diagnostics = ReviewColumnUx3A(review, "Diagnostics humains");
        root.AddChild(review);
        _summary = StudioStyle.Label("Sélectionnez une définition ou une instance.", 10,
            StudioStyle.Muted, wrap: true);
        root.AddChild(_summary);
        root.AddChild(StudioStyle.Label(
            "Aperçu sec : aucun objet, instance, asset, script, scène, moteur ou réseau n’est exécuté dans Studio.",
            9, StudioStyle.Warning, wrap: true));

        var footer = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footer.AddThemeConstantOverride("separation", 8);
        _previewButton = StudioStyle.Button("Prévisualiser", compact: true);
        _applyButton = StudioStyle.Button("Appliquer l’aperçu", compact: true);
        _undoButton = StudioStyle.Button("Annuler", compact: true);
        _redoButton = StudioStyle.Button("Rétablir", compact: true);
        _saveButton = StudioStyle.Button("Enregistrer", compact: true);
        _previewButton.Pressed += PreviewUx3A;
        _applyButton.Pressed += ApplyUx3A;
        _undoButton.Pressed += UndoUx3A;
        _redoButton.Pressed += RedoUx3A;
        _saveButton.Pressed += SaveUx3A;
        footer.AddChild(_previewButton);
        footer.AddChild(_applyButton);
        footer.AddChild(_undoButton);
        footer.AddChild(_redoButton);
        footer.AddChild(_saveButton);
        _status = StudioStyle.Label("Aucune mutation préparée.", 10, StudioStyle.Muted, wrap: true);
        _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        footer.AddChild(_status);
        root.AddChild(footer);

        WireChangesUx3A();
        ResetControlsUx3A();
    }

    internal void Open() => OpenBoundUx3A(_sourceSession, evidence: false, cancelApproval: false);
    internal void RestoreSourceSurfaceAfterSmoke() =>
        OpenBoundUx3A(_sourceSession, evidence: false, cancelApproval: false);
    internal void HideSourceSurfaceAfterSmoke()
    {
        Visible = false;
        _session = _sourceSession;
    }

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null) return;
        if (Visible) _session.CancelBlueprintApprovalUx3A();
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidenceUx3A();
        _session = _sourceSession;
        ResetControlsUx3A();
    }

    internal void CloseEvidence()
    {
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidenceUx3A();
        _session = _sourceSession;
        ResetControlsUx3A(keepReleaseState: true);
    }

    internal bool PrepareEvidence(string mode)
    {
        if (!mode.StartsWith("ux3-a", StringComparison.OrdinalIgnoreCase)) return false;
        ReleaseEvidenceUx3A();
        _sourceSnapshot = CaptureSourceUx3A();
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux3-a-ui-{Guid.NewGuid():N}");
        try
        {
            var evidence = _sourceSession.CreateBlueprintEvidenceProjectUx3A();
            if (evidence is null || !ProjectSession.Create(root, evidence.Project).Succeeded) return false;
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
            OpenBoundUx3A(isolated, evidence: true, cancelApproval: true);
            if (mode.EndsWith("-instance", StringComparison.OrdinalIgnoreCase))
            {
                _operation.Select(1);
                ChangeOperationUx3A(1);
                var entityIndex = _entities.ToList().FindIndex(item =>
                    item.AreaId == evidence.AreaId && item.EntityId == evidence.EntityId);
                if (entityIndex < 0) return false;
                _entity.Select(entityIndex + 1);
                SelectEntityUx3A(entityIndex + 1);
            }
            else
            {
                var blueprintIndex = _blueprints.ToList().FindIndex(item => item.Id == evidence.EditableBlueprintId);
                if (blueprintIndex < 0) return false;
                _operation.Select(0);
                _blueprint.Select(blueprintIndex + 1);
                SelectBlueprintUx3A(blueprintIndex + 1);
            }
            if (_draft is null) return false;
            _draft = StudioSessionController.ChangedBlueprintDraftUx3A(_draft);
            PopulateDraftUx3A();
            _preview = _session.PreviewBlueprintUx3A(_draft);
            RenderPreviewUx3A();
            _tabs.CurrentTab = mode.EndsWith("-palettes", StringComparison.OrdinalIgnoreCase) ? 2 :
                mode.EndsWith("-instance", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            var ready = PreviewReady && HasExplicitSelection && ShowsThreeWorkflows &&
                        ShowsStandardAndProject && ShowsInstanceWorkflow && ShowsHumanSurface &&
                        SourceMatchesUx3A() && (_usesRealModuleEvidence ^ _usesCanonicalFallbackEvidence);
            if (!ready)
                GD.PrintErr("OPENRPG_FORGE_STUDIO_UX3_A_UI_PREPARE_FAILED " +
                            $"preview={PreviewReady.ToString().ToLowerInvariant()} " +
                            $"palettes={ShowsStandardAndProject.ToString().ToLowerInvariant()} " +
                            $"instance={ShowsInstanceWorkflow.ToString().ToLowerInvariant()} " +
                            $"human={ShowsHumanSurface.ToString().ToLowerInvariant()} " +
                            $"source={SourceMatchesUx3A().ToString().ToLowerInvariant()}");
            return ready;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX3_A_EVIDENCE_FAILED type={exception.GetType().Name}");
            return false;
        }
        finally
        {
            if (!PreviewReady || !SourceMatchesUx3A())
            {
                if (_evidenceSession is not null) ReleaseEvidenceUx3A();
                else StudioSessionController.DeleteTemporaryBlueprintUx3A(root);
            }
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        var sourceApproval = _sourceSession.CaptureBlueprintApprovalUx3A();
        try
        {
            var ready = PrepareEvidence("ux3-a-blueprint-foundation");
            if (!ready || _evidenceSession is null || !ReferenceEquals(_session, _evidenceSession) ||
                !PreviewReady || _draft is null || string.IsNullOrWhiteSpace(_evidenceRoot)) return false;
            var applied = _session.ApplyApprovedBlueprintUx3A(_draft);
            var undo = applied.Succeeded ? _session.UndoBlueprintUx3A() : new(false, "non exécuté");
            var redo = undo.Succeeded ? _session.RedoBlueprintUx3A() : new(false, "non exécuté");
            var saved = redo.Succeeded ? _session.SaveBlueprintUx3A() : new(false, "non exécuté");
            var expected = _session.Project is null ? string.Empty : ProjectJsonSerializer.Serialize(_session.Project);
            var reopened = new StudioSessionController();
            var roundTrip = saved.Succeeded && reopened.Open(_evidenceRoot).Succeeded &&
                            reopened.Project is not null && ProjectJsonSerializer.Serialize(reopened.Project) == expected;
            reopened.Close();
            return applied.Succeeded && undo.Succeeded && redo.Succeeded && saved.Succeeded && roundTrip &&
                   SourceMatchesUx3A();
        }
        finally
        {
            CloseEvidence();
            _sourceSession.RestoreBlueprintApprovalUx3A(sourceApproval);
        }
    }

    private void OpenBoundUx3A(StudioSessionController session, bool evidence, bool cancelApproval)
    {
        _session = session;
        if (cancelApproval) _session.CancelBlueprintApprovalUx3A();
        Visible = true;
        _blueprints = _session.BlueprintChoicesUx3A;
        _entities = _session.EntityChoicesUx3A;
        _updating = true;
        _operation.Select(0);
        _blueprint.Clear();
        _blueprint.AddItem("Choisir explicitement un blueprint…");
        foreach (var choice in _blueprints) _blueprint.AddItem(choice.Label);
        _entity.Clear();
        _entity.AddItem("Choisir explicitement une instance…");
        foreach (var choice in _entities) _entity.AddItem(choice.Label);
        _paletteView.Select(0);
        _updating = false;
        _draft = null;
        _explicitSelection = false;
        UpdateSelectionModeUx3A();
        RebuildPaletteUx3A();
        ResetPreviewUx3A();
        if (!evidence)
        {
            _usesRealModuleEvidence = false;
            _usesCanonicalFallbackEvidence = false;
        }
    }

    private void ChangeOperationUx3A(long selected)
    {
        if (_updating) return;
        _session.CancelBlueprintApprovalUx3A();
        _draft = null;
        _explicitSelection = false;
        _updating = true;
        _blueprint.Select(0);
        _entity.Select(0);
        _updating = false;
        UpdateSelectionModeUx3A();
        ResetPreviewUx3A();
        _tabs.CurrentTab = selected == 1 ? 1 : 0;
    }

    private void UpdateSelectionModeUx3A()
    {
        var derive = _operation.Selected == 1;
        _blueprint.Disabled = derive;
        _entity.Disabled = !derive;
        _selection.Text = derive
            ? (_entities.Count == 0
                ? "État vide · aucune instance dérivable dans les zones du module."
                : "Choisissez explicitement une instance ; sa transform, ses tags et ses surcharges seront conservés.")
            : (_blueprints.Count == 0
                ? "État vide · aucun blueprint disponible."
                : "Choisissez explicitement un blueprint Standard ou Projet.");
        _selection.Modulate = (_entities.Count == 0 && derive) || (_blueprints.Count == 0 && !derive)
            ? StudioStyle.Danger : StudioStyle.Warning;
    }

    private void SelectBlueprintUx3A(long selected)
    {
        if (_updating || _operation.Selected != 0) return;
        _session.CancelBlueprintApprovalUx3A();
        _explicitSelection = selected > 0 && selected <= _blueprints.Count;
        _draft = _explicitSelection ? _session.CreateBlueprintDraftUx3A(_blueprints[(int)selected - 1].Id) : null;
        if (_draft is null)
        {
            UpdateSelectionModeUx3A();
            ResetPreviewUx3A();
            return;
        }
        var choice = _blueprints[(int)selected - 1];
        _selection.Text = choice.IsReadOnly
            ? $"{choice.Label} · {choice.UsageCount} référence(s) · définition Standard protégée."
            : $"{choice.Label} · {choice.UsageCount} référence(s) · contenu Projet éditable.";
        _selection.Modulate = choice.IsReadOnly ? StudioStyle.Warning : StudioStyle.Success;
        PopulateDraftUx3A();
        ResetPreviewUx3A();
    }

    private void SelectEntityUx3A(long selected)
    {
        if (_updating || _operation.Selected != 1) return;
        _session.CancelBlueprintApprovalUx3A();
        _explicitSelection = selected > 0 && selected <= _entities.Count;
        var choice = _explicitSelection ? _entities[(int)selected - 1] : null;
        _draft = choice is null ? null : _session.CreateDerivedBlueprintDraftUx3A(choice.AreaId, choice.EntityId);
        if (_draft is null || choice is null)
        {
            UpdateSelectionModeUx3A();
            ResetPreviewUx3A();
            return;
        }
        _selection.Text = $"{choice.Label} · création explicite dans la palette Projet.";
        _selection.Modulate = StudioStyle.Success;
        _instanceInfo.Text = $"{choice.Label}\nVersion actuelle {HumanTextUx3A(choice.VersionRequirement)} · " +
                             $"{choice.OverrideCount} surcharge(s) conservée(s) exactement.";
        PopulateDraftUx3A();
        ResetPreviewUx3A();
    }

    private void PopulateDraftUx3A()
    {
        if (_draft is null) return;
        _updating = true;
        _slug.Text = _draft.Slug;
        _displayName.Text = _draft.DisplayName;
        _kind.Text = $"Type protégé · {HumanKindUx3A(_draft.Kind)}";
        _version.Text = _draft.Version;
        _packageNamespace.Text = _draft.PackageNamespace;
        _byteBudget.Text = _draft.PlatformByteBudget.ToString(CultureInfo.InvariantCulture);
        _parameters.Text = string.Join('\n', _draft.ExposedParameters);
        _protectedDependencyValues = _draft.Dependencies.Where(ContainsUuidUx3A).ToArray();
        _dependencies.Text = string.Join('\n', _draft.Dependencies.Where(item => !ContainsUuidUx3A(item)));
        _protectedDependencies.Text = _protectedDependencyValues.Count == 0
            ? "Aucune dépendance héritée protégée."
            : $"{_protectedDependencyValues.Count} dépendance(s) héritée(s) protégée(s), conservée(s) sans affichage technique.";
        _manifestSummary.Text = $"Manifeste et aperçu gouvernés · {HumanPathUx3A(_draft.ManifestPath)} · " +
                                $"{(_draft.PreviewPath is null ? "aucun aperçu" : "aperçu conservé")}.";
        _approval.ButtonPressed = _draft.Approved;
        var readOnly = _draft.Operation == StudioUx3AOperation.EditBlueprint &&
                       _blueprints.FirstOrDefault(item => item.Id == _draft.BlueprintId)?.IsReadOnly == true;
        SetEditingEnabledUx3A(!readOnly);
        _updating = false;
    }

    private bool TryReadDraftUx3A(out StudioUx3ADraft? draft, out string error)
    {
        draft = null;
        error = string.Empty;
        if (_draft is null)
        {
            error = "Sélectionnez d’abord une définition ou une instance.";
            return false;
        }
        if (!long.TryParse(_byteBudget.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var budget))
        {
            error = "Le budget plateforme doit être un nombre entier exact.";
            return false;
        }
        var dependencies = SplitLinesUx3A(_dependencies.Text).Concat(_protectedDependencyValues)
            .Distinct(StringComparer.Ordinal).ToArray();
        draft = _draft with
        {
            Slug = _slug.Text.Trim(),
            DisplayName = _displayName.Text.Trim(),
            Version = _version.Text.Trim(),
            PackageNamespace = _packageNamespace.Text.Trim(),
            ExposedParameters = SplitLinesUx3A(_parameters.Text),
            Dependencies = dependencies,
            PlatformByteBudget = budget,
            Approved = _approval.ButtonPressed,
        };
        return true;
    }

    private void PreviewUx3A()
    {
        if (!TryReadDraftUx3A(out var current, out var error) || current is null)
        {
            SetLocalStatusUx3A(error, true);
            return;
        }
        _draft = current;
        _preview = _session.PreviewBlueprintUx3A(current);
        RenderPreviewUx3A();
    }

    private void ApplyUx3A()
    {
        if (!TryReadDraftUx3A(out var current, out var error) || current is null || !PreviewReady ||
            _previewFingerprint != _session.BlueprintDraftFingerprintUx3A(current))
        {
            SetLocalStatusUx3A(error.Length > 0 ? error : "Le brouillon a changé ; relancez l’aperçu.", true);
            InvalidatePreviewUx3A();
            return;
        }
        var result = _session.ApplyApprovedBlueprintUx3A(current);
        SetLocalStatusUx3A(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            BlueprintChanged?.Invoke();
            ReloadAfterMutationUx3A();
        }
    }

    private void UndoUx3A()
    {
        var result = _session.UndoBlueprintUx3A();
        SetLocalStatusUx3A(result.Message, !result.Succeeded);
        if (result.Succeeded) { BlueprintChanged?.Invoke(); ReloadAfterMutationUx3A(); }
    }

    private void RedoUx3A()
    {
        var result = _session.RedoBlueprintUx3A();
        SetLocalStatusUx3A(result.Message, !result.Succeeded);
        if (result.Succeeded) { BlueprintChanged?.Invoke(); ReloadAfterMutationUx3A(); }
    }

    private void SaveUx3A()
    {
        var result = _session.SaveBlueprintUx3A();
        SetLocalStatusUx3A(result.Message, !result.Succeeded);
        UpdateButtonsUx3A();
    }

    private void ReloadAfterMutationUx3A()
    {
        var operation = _operation.Selected;
        var blueprintId = _draft?.BlueprintId;
        var entityId = _draft?.SourceEntityId;
        _blueprints = _session.BlueprintChoicesUx3A;
        _entities = _session.EntityChoicesUx3A;
        OpenBoundUx3A(_session, evidence: !ReferenceEquals(_session, _sourceSession), cancelApproval: false);
        _operation.Select(operation);
        ChangeOperationUx3A(operation);
        if (operation == 0 && blueprintId is { } id)
        {
            var index = _blueprints.ToList().FindIndex(item => item.Id == id);
            if (index >= 0) { _blueprint.Select(index + 1); SelectBlueprintUx3A(index + 1); }
        }
        else if (operation == 1 && entityId is { } selectedEntity)
        {
            var index = _entities.ToList().FindIndex(item => item.EntityId == selectedEntity);
            if (index >= 0) { _entity.Select(index + 1); SelectEntityUx3A(index + 1); }
        }
        RebuildPaletteUx3A();
    }

    private void RenderPreviewUx3A()
    {
        ClearChildrenUx3A(_diff);
        ClearChildrenUx3A(_diagnostics);
        if (_preview is null)
        {
            ResetPreviewUx3A();
            return;
        }
        foreach (var item in _preview.SemanticDiff)
            _diff.AddChild(StudioStyle.Label("• " + HumanTextUx3A(item), 10, StudioStyle.Text, wrap: true));
        if (_preview.SemanticDiff.Count == 0)
            _diff.AddChild(StudioStyle.Label("Aucun changement sémantique.", 10, StudioStyle.Muted));
        foreach (var item in _preview.Diagnostics)
            _diagnostics.AddChild(StudioStyle.Label(
                $"• {HumanCodeUx3A(item.Code)} · {HumanTextUx3A(item.Message)}",
                10, StudioStyle.Danger, wrap: true));
        if (_preview.Diagnostics.Count == 0)
            _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic bloquant.", 10, StudioStyle.Success));
        _summary.Text = HumanTextUx3A(_preview.Message) +
                        " · aucune instance, ressource ou scène runtime n’est exécutée.";
        _previewReady = _preview.Succeeded && _preview.CanApply && _preview.DryRunSucceeded &&
                        _preview.RuntimeClaimsBounded && _draft is not null;
        _previewFingerprint = _previewReady ? _session.BlueprintDraftFingerprintUx3A(_draft!) : null;
        SetLocalStatusUx3A(_preview.Message, !_preview.Succeeded);
        UpdateButtonsUx3A();
    }

    private void WireChangesUx3A()
    {
        foreach (var edit in new[] { _slug, _displayName, _version, _packageNamespace, _byteBudget })
            edit.TextChanged += _ => InvalidatePreviewUx3A();
        _parameters.TextChanged += InvalidatePreviewUx3A;
        _dependencies.TextChanged += InvalidatePreviewUx3A;
        _approval.Toggled += _ => InvalidatePreviewUx3A();
    }

    private void InvalidatePreviewUx3A()
    {
        if (_updating) return;
        _session.CancelBlueprintApprovalUx3A();
        _previewReady = false;
        _previewFingerprint = null;
        _applyButton.Disabled = true;
        _summary.Text = "Brouillon modifié · relancez l’aperçu sec canonique.";
    }

    private void ResetPreviewUx3A()
    {
        _preview = null;
        _previewReady = false;
        _previewFingerprint = null;
        ClearChildrenUx3A(_diff);
        ClearChildrenUx3A(_diagnostics);
        _diff.AddChild(StudioStyle.Label("Aucun aperçu frais.", 10, StudioStyle.Muted));
        _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic.", 10, StudioStyle.Muted));
        _summary.Text = _draft is null
            ? "Sélectionnez une définition ou une instance."
            : "Sélection chargée · configurez, approuvez puis prévisualisez.";
        UpdateButtonsUx3A();
    }

    private void ResetControlsUx3A(bool keepReleaseState = false)
    {
        _updating = true;
        _operation.Select(0);
        _blueprint.Clear();
        _blueprint.AddItem("Choisir explicitement un blueprint…");
        _entity.Clear();
        _entity.AddItem("Choisir explicitement une instance…");
        _slug.Text = string.Empty;
        _displayName.Text = string.Empty;
        _kind.Text = "Type protégé · sélectionnez une définition";
        _version.Text = string.Empty;
        _packageNamespace.Text = string.Empty;
        _byteBudget.Text = string.Empty;
        _parameters.Text = string.Empty;
        _dependencies.Text = string.Empty;
        _protectedDependencyValues = [];
        _protectedDependencies.Text = "Aucune dépendance héritée protégée.";
        _manifestSummary.Text = "Manifeste et aperçu gouvernés · chemins techniques protégés.";
        _approval.ButtonPressed = false;
        _paletteList.Clear();
        _updating = false;
        _blueprints = [];
        _entities = [];
        _draft = null;
        _explicitSelection = false;
        if (!keepReleaseState) _evidenceReleased = _evidenceSession is null;
        SetEditingEnabledUx3A(false);
        ResetPreviewUx3A();
    }

    private void SetEditingEnabledUx3A(bool enabled)
    {
        _slug.Editable = enabled;
        _displayName.Editable = enabled;
        _version.Editable = enabled;
        _packageNamespace.Editable = enabled;
        _byteBudget.Editable = enabled;
        _parameters.Editable = enabled;
        _dependencies.Editable = enabled;
        _approval.Disabled = !enabled;
        _previewButton.Disabled = !enabled;
    }

    private void UpdateButtonsUx3A()
    {
        var readOnly = _draft?.Operation == StudioUx3AOperation.EditBlueprint &&
                       _blueprints.FirstOrDefault(item => item.Id == _draft.BlueprintId)?.IsReadOnly == true;
        _previewButton.Disabled = !HasExplicitSelection || readOnly;
        _applyButton.Disabled = !PreviewReady;
        _undoButton.Disabled = _session.History?.CanUndo != true;
        _redoButton.Disabled = _session.History?.CanRedo != true;
        _saveButton.Disabled = !_session.IsDirty;
    }

    private void RebuildPaletteUx3A()
    {
        _paletteList.Clear();
        var filtered = _blueprints.Where(item => _paletteView.Selected switch
        {
            1 => item.Origin == BlueprintPaletteOrigin.InstalledPack,
            2 => item.Origin == BlueprintPaletteOrigin.ProjectContent,
            _ => true,
        }).ToArray();
        foreach (var item in filtered) _paletteList.AddItem(item.Label +
            $" · v{item.Version} · {item.UsageCount} usage(s)");
        var standard = _blueprints.Count(item => item.Origin == BlueprintPaletteOrigin.InstalledPack);
        var project = _blueprints.Count(item => item.Origin == BlueprintPaletteOrigin.ProjectContent);
        _paletteSummary.Text = $"{standard} définition(s) Standard en lecture seule · " +
                               $"{project} définition(s) Projet éditable(s) · {filtered.Length} visible(s).";
    }

    private void SetLocalStatusUx3A(string message, bool error)
    {
        _status.Text = HumanTextUx3A(message);
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Success;
        StatusChanged?.Invoke(_status.Text, error);
    }

    private SourceSnapshotUx3A CaptureSourceUx3A()
    {
        var history = _sourceSession.History;
        return new(_sourceSession.Project, history, history?.State.Revision, history?.Journal.ToArray() ?? [],
            _sourceSession.IsDirty, history?.CanUndo, history?.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureBlueprintApprovalUx3A());
    }

    private bool SourceMatchesUx3A()
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
               _sourceSession.CaptureBlueprintApprovalUx3A() == snapshot.Approval;
    }

    private void ReleaseEvidenceUx3A()
    {
        _evidenceSession?.Close();
        _evidenceSession = null;
        _evidenceReleased = string.IsNullOrWhiteSpace(_evidenceRoot) ||
                            StudioSessionController.DeleteTemporaryBlueprintUx3A(_evidenceRoot);
        _evidenceRoot = null;
        _session = _sourceSession;
    }

    private static ScrollContainer ScrollTabUx3A(string title, out VBoxContainer body)
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

    private static HBoxContainer RowUx3A(params Control[] controls)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 8);
        foreach (var control in controls) row.AddChild(control);
        return row;
    }

    private static VBoxContainer ReviewColumnUx3A(HBoxContainer host, string title)
    {
        var panel = StudioStyle.Panel("Ux3A" + title.Replace(" ", string.Empty), StudioStyle.Surface, 8, 1);
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

    private static void ClearChildrenUx3A(Node root)
    {
        foreach (var child in root.GetChildren().Skip(1).ToArray())
        {
            root.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static IReadOnlyList<string> SplitLinesUx3A(string text) =>
        text.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim()).Where(item => item.Length > 0).ToArray();

    private static string HumanKindUx3A(BlueprintKind kind) => kind switch
    {
        BlueprintKind.Creature => "créature",
        BlueprintKind.Placeable => "objet plaçable",
        BlueprintKind.Door => "porte",
        BlueprintKind.Item => "objet d’inventaire",
        BlueprintKind.Trigger => "déclencheur",
        BlueprintKind.PlayerSpawn => "apparition joueur",
        _ => "famille protégée",
    };

    private static string HumanCodeUx3A(string code) => code switch
    {
        "blueprint.read_only" => "Définition Standard protégée",
        "blueprint.slug_invalid" => "Clé auteur invalide",
        "blueprint.slug_duplicate" => "Clé déjà utilisée",
        "command.revision_conflict" => "Projet modifié",
        "blueprint.approval_required" => "Approbation requise",
        _ => "Diagnostic canonique",
    };

    private static string HumanPathUx3A(string path) => ContainsUuidUx3A(path)
        ? "chemin stable protégé"
        : HumanTextUx3A(path);

    private static string HumanTextUx3A(string text) => UuidRegexUx3A().Replace(text ?? string.Empty,
        "référence protégée");

    private static bool ContainsUuidUx3A(string text) => UuidRegexUx3A().IsMatch(text ?? string.Empty);

    private static IEnumerable<string> DescendantTextUx3A(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Label label) yield return label.Text;
            if (child is Button button) yield return button.Text;
            if (child is LineEdit line) yield return line.Text;
            if (child is TextEdit text) yield return text.Text;
            foreach (var nested in DescendantTextUx3A(child)) yield return nested;
        }
    }

    [GeneratedRegex(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex UuidRegexUx3A();

    private sealed record SourceSnapshotUx3A(
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
        StudioUx3AApprovalSnapshot Approval);
}
