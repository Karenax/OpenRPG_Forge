using System.Text.RegularExpressions;
using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Creator;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioBlueprintDiscoveryCardUx3C : PanelContainer
{
    internal const string PayloadKind = "openrpg-forge/blueprint-card-v1";
    internal StudioUx3CBlueprintChoice Choice { get; }

    internal event Action<Guid>? Selected;
    internal event Action<Guid>? FavoriteRequested;

    internal StudioBlueprintDiscoveryCardUx3C(StudioUx3CBlueprintChoice choice, Texture2D? thumbnail)
    {
        Choice = choice;
        CustomMinimumSize = new Vector2(0, 92);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseDefaultCursorShape = CursorShape.Drag;
        AddThemeStyleboxOverride("panel", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 7, 1));
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 8);
        AddChild(row);
        var previewHost = new Control { CustomMinimumSize = new Vector2(108, 76), ClipContents = true };
        row.AddChild(previewHost);
        if (thumbnail is not null)
        {
            var texture = new TextureRect
            {
                Texture = thumbnail,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            };
            texture.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            previewHost.AddChild(texture);
        }
        else
        {
            var fallback = StudioStyle.Label("APERÇU\nSÉMANTIQUE", 9, StudioStyle.Muted, wrap: true);
            fallback.HorizontalAlignment = HorizontalAlignment.Center;
            fallback.VerticalAlignment = VerticalAlignment.Center;
            fallback.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            previewHost.AddChild(fallback);
        }
        var details = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        details.AddThemeConstantOverride("separation", 2);
        var select = StudioStyle.Button(choice.Label, compact: true);
        select.Alignment = HorizontalAlignment.Left;
        select.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        select.Pressed += () => Selected?.Invoke(choice.Id);
        details.AddChild(select);
        details.AddChild(StudioStyle.Label(
            $"Kit {choice.Kit} · licence {choice.License} · {choice.UsageCount} instance(s)",
            9, StudioStyle.Muted, wrap: true));
        details.AddChild(StudioStyle.Label(
            choice.PreviewFileExists ? "Miniature locale disponible" :
            string.IsNullOrWhiteSpace(choice.PreviewRelativePath)
                ? "Aucune miniature authorée · repli sémantique"
                : "Miniature authorée absente du disque · repli sémantique",
            9, choice.PreviewFileExists ? StudioStyle.Success : StudioStyle.Warning, wrap: true));
        row.AddChild(details);
        var favorite = StudioStyle.Button(choice.IsFavorite ? "★" : "☆", compact: true);
        favorite.TooltipText = choice.IsFavorite ? "Retirer des favoris" : "Ajouter aux favoris";
        favorite.Pressed += () => FavoriteRequested?.Invoke(choice.Id);
        row.AddChild(favorite);
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        var preview = StudioStyle.Label($"  Glisser « {Choice.Label.Split('·')[0].Trim()} »  ",
            10, StudioStyle.Text);
        preview.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.Selection, StudioStyle.Success, 6, 1));
        SetDragPreview(preview);
        return CreatePayload();
    }

    internal Variant CreateEvidencePayload() => CreatePayload();

    private Godot.Collections.Dictionary CreatePayload() => new()
    {
        ["kind"] = PayloadKind,
        ["schemaVersion"] = BlueprintDropPlanner.CurrentSchemaVersion,
        ["blueprintId"] = Choice.Id.ToString("D"),
        ["provenance"] = "studio:ux3-c-card",
    };
}

internal sealed partial class StudioBlueprintDropTargetUx3C : PanelContainer
{
    internal event Action<Guid>? BlueprintDropped;

    internal StudioBlueprintDropTargetUx3C()
    {
        CustomMinimumSize = new Vector2(0, 82);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseDefaultCursorShape = CursorShape.CanDrop;
        AddThemeStyleboxOverride("panel", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Accent, 8, 1));
        var label = StudioStyle.Label(
            "GLISSEZ UNE CARTE ICI\nLe dépôt prépare un placement sur la grille de la zone active ; rien n’est écrit avant Appliquer.",
            10, StudioStyle.Text, wrap: true);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        AddChild(label);
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) => TryRead(data, out _);

    public override void _DropData(Vector2 atPosition, Variant data) => Dispatch(data);

    internal bool DispatchEvidencePayload(Variant data) => Dispatch(data);

    private bool Dispatch(Variant data)
    {
        if (!TryRead(data, out var blueprintId)) return false;
        BlueprintDropped?.Invoke(blueprintId);
        return true;
    }

    private static bool TryRead(Variant data, out Guid blueprintId)
    {
        blueprintId = Guid.Empty;
        if (data.VariantType != Variant.Type.Dictionary) return false;
        var dictionary = data.AsGodotDictionary();
        return dictionary.ContainsKey("kind") && dictionary.ContainsKey("schemaVersion") &&
               dictionary.ContainsKey("blueprintId") && dictionary.ContainsKey("provenance") &&
               string.Equals(dictionary["kind"].AsString(), StudioBlueprintDiscoveryCardUx3C.PayloadKind,
                   StringComparison.Ordinal) &&
               dictionary["schemaVersion"].AsInt32() == BlueprintDropPlanner.CurrentSchemaVersion &&
               !string.IsNullOrWhiteSpace(dictionary["provenance"].AsString()) &&
               Guid.TryParse(dictionary["blueprintId"].AsString(), out blueprintId) && blueprintId != Guid.Empty;
    }
}

internal sealed partial class StudioBlueprintDiscoveryWorkbenchUx3C : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private SourceSnapshotUx3C? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;
    private bool _usesRealModuleEvidence;
    private bool _usesCanonicalFallbackEvidence;
    private bool _updating;
    private bool _explicitSelection;
    private bool _dragDropObserved;

    private readonly LineEdit _search;
    private readonly OptionButton _view;
    private readonly OptionButton _kind;
    private readonly LineEdit _kit;
    private readonly LineEdit _style;
    private readonly LineEdit _license;
    private readonly LineEdit _compatibility;
    private readonly LineEdit _tags;
    private readonly VBoxContainer _results;
    private readonly Label _resultSummary;
    private readonly TextureRect _thumbnail;
    private readonly Label _thumbnailFallback;
    private readonly Label _selection;
    private readonly Label _metadata;
    private readonly CheckBox _favorite;
    private readonly CheckBox _discoveryApproval;
    private readonly Button _discoveryPreviewButton;
    private readonly Button _discoveryApplyButton;
    private readonly Label _discoverySummary;
    private readonly VBoxContainer _discoveryDiff;
    private readonly VBoxContainer _discoveryDiagnostics;
    private readonly StudioBlueprintDropTargetUx3C _dropTarget;
    private readonly CheckBox _dropApproval;
    private readonly Button _dropPreviewButton;
    private readonly Button _dropApplyButton;
    private readonly Label _dropSummary;
    private readonly VBoxContainer _dropDiff;
    private readonly VBoxContainer _dropDiagnostics;
    private readonly Label _status;
    private readonly Button _workspaceUndo;
    private readonly Button _workspaceRedo;
    private readonly Button _workspaceSave;
    private readonly Button _projectUndo;
    private readonly Button _projectRedo;
    private readonly Button _projectSave;

    private readonly Dictionary<Guid, StudioBlueprintDiscoveryCardUx3C> _cards = [];
    private IReadOnlyList<StudioUx3CBlueprintChoice> _choices = [];
    private StudioUx3CBlueprintChoice? _selected;
    private StudioUx3CDiscoveryDraft? _discoveryDraft;
    private StudioUx3CDropDraft? _dropDraft;
    private StudioUx3CDiscoveryPreview? _discoveryPreview;
    private StudioUx3CDropPreview? _dropPreview;
    private bool _discoveryPreviewReady;
    private bool _dropPreviewReady;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? ProjectChanged;

    internal bool IsOpen => Visible;
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealModuleEvidence => _usesRealModuleEvidence;
    internal bool UsesCanonicalFallbackEvidence => _usesCanonicalFallbackEvidence;
    internal bool HasExplicitSelection => _explicitSelection && _selected is not null;
    internal bool DiscoveryPreviewReady => _discoveryPreviewReady && _discoveryPreview is
    { Succeeded: true, CanApply: true, DryRunSucceeded: true, RuntimeClaimsBounded: true };
    internal bool DropPreviewReady => _dropPreviewReady && _dropPreview is
    { Succeeded: true, CanApply: true, DryRunSucceeded: true, RuntimeClaimsBounded: true };
    internal bool DragDropObserved => _dragDropObserved;
    internal bool ThumbnailRendered => _selected?.PreviewFileExists == true && _thumbnail.Texture is not null;
    internal bool EvidenceSourceUntouched => SourceMatchesUx3C();
    internal bool EvidenceResourcesReleased => _evidenceReleased && _evidenceSession is null && _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx3C(this));
            return text.Contains("Découverte et dépôt de blueprints", StringComparison.Ordinal) &&
                   text.Contains("Favoris et récents", StringComparison.Ordinal) &&
                   text.Contains("Recherche avancée", StringComparison.Ordinal) &&
                   text.Contains("Miniature et prévisualisation", StringComparison.Ordinal) &&
                   text.Contains("Glissez une carte", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("deux transactions séparées", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun asset", StringComparison.OrdinalIgnoreCase) &&
                   !UuidRegexUx3C().IsMatch(text);
        }
    }

    internal StudioBlueprintDiscoveryWorkbenchUx3C(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.012f, .018f, .027f, .98f);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        var dialog = StudioStyle.Panel("Ux3CBlueprintDiscoveryDialog", StudioStyle.Surface, 12, 1);
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
        headingText.AddChild(StudioStyle.Label("Découverte et dépôt de blueprints", 18));
        headingText.AddChild(StudioStyle.Label(
            "Favoris, récents, recherche avancée, miniatures et drag-and-drop · deux transactions séparées avec aperçu sec canonique.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        root.AddChild(heading);

        var body = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            PropagateMaximumSize = true,
        };
        body.AddThemeConstantOverride("separation", 10);
        root.AddChild(body);

        var leftPanel = StudioStyle.Panel("Ux3CDiscovery", StudioStyle.SurfaceRaised, 8, 1);
        leftPanel.CustomMinimumSize = new Vector2(520, 0);
        leftPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        var leftMargin = StudioStyle.Margin(10, 9, 10, 9);
        leftPanel.AddChild(leftMargin);
        var left = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        left.AddThemeConstantOverride("separation", 6);
        leftMargin.AddChild(left);
        body.AddChild(leftPanel);
        left.AddChild(StudioStyle.Label("Recherche avancée", 12, StudioStyle.Accent));
        _search = StudioUx2EnvBControls.Text("Nom, type, kit, style, licence ou tag…");
        _search.TextChanged += _ => FiltersChangedUx3C();
        _view = StudioUx2EnvBControls.Options("Tout", "Favoris", "Récents", "Standard", "Projet");
        _view.ItemSelected += _ => FiltersChangedUx3C();
        _kind = StudioUx2EnvBControls.Options("Tous les types", "Créature", "Plaçable", "Porte", "Objet",
            "Déclencheur", "Départ joueur");
        _kind.ItemSelected += _ => FiltersChangedUx3C();
        left.AddChild(_search);
        left.AddChild(RowUx3C(_view, _kind));
        _kit = FilterUx3C("Kit");
        _style = FilterUx3C("Style");
        _license = FilterUx3C("Licence");
        _compatibility = FilterUx3C("Compatibilité");
        _tags = FilterUx3C("Tags");
        left.AddChild(RowUx3C(_kit, _style));
        left.AddChild(RowUx3C(_license, _compatibility));
        left.AddChild(_tags);
        _resultSummary = StudioStyle.Label("Ouvrez un module pour indexer ses blueprints.", 9, StudioStyle.Muted,
            wrap: true);
        left.AddChild(_resultSummary);
        var resultScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        left.AddChild(resultScroll);
        _results = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _results.AddThemeConstantOverride("separation", 5);
        resultScroll.AddChild(_results);

        var rightScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        body.AddChild(rightScroll);
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 8);
        rightScroll.AddChild(right);
        right.AddChild(StudioStyle.Label("Miniature et prévisualisation", 12, StudioStyle.Accent));
        var detail = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        detail.AddThemeConstantOverride("separation", 10);
        var thumbnailHost = new Control { CustomMinimumSize = new Vector2(240, 170), ClipContents = true };
        _thumbnail = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Visible = false,
        };
        _thumbnail.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        thumbnailHost.AddChild(_thumbnail);
        _thumbnailFallback = StudioStyle.Label("Sélectionnez explicitement une carte.", 10, StudioStyle.Muted,
            wrap: true);
        _thumbnailFallback.HorizontalAlignment = HorizontalAlignment.Center;
        _thumbnailFallback.VerticalAlignment = VerticalAlignment.Center;
        _thumbnailFallback.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        thumbnailHost.AddChild(_thumbnailFallback);
        detail.AddChild(thumbnailHost);
        var detailText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _selection = StudioStyle.Label("Aucun blueprint sélectionné.", 11, StudioStyle.Warning, wrap: true);
        _metadata = StudioStyle.Label(
            "L’aperçu charge uniquement une miniature locale gouvernée ; aucun asset, prefab ou moteur n’est instancié.",
            9, StudioStyle.Muted, wrap: true);
        detailText.AddChild(_selection);
        detailText.AddChild(_metadata);
        detail.AddChild(detailText);
        right.AddChild(detail);

        _favorite = StudioUx2EnvBControls.Check("Conserver dans mes favoris");
        _favorite.Toggled += _ => InvalidateDiscoveryUx3C();
        _discoveryApproval = StudioUx2EnvBControls.Check(
            "J’approuve la mémorisation du favori et du récent dans mon profil Studio");
        _discoveryApproval.Toggled += _ => InvalidateDiscoveryUx3C();
        right.AddChild(StudioUx2EnvBControls.Card("Ux3CPreferences", "Favoris et récents",
            "Transaction personnelle séparée du placement. Le projet n’est pas modifié.",
            RowUx3C(_favorite, _discoveryApproval)));
        var discoveryButtons = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        discoveryButtons.AddThemeConstantOverride("separation", 8);
        _discoveryPreviewButton = StudioStyle.Button("Prévisualiser favoris/récents", compact: true);
        _discoveryApplyButton = StudioStyle.Button("Appliquer favoris/récents", compact: true);
        _discoveryPreviewButton.Pressed += PreviewDiscoveryUx3C;
        _discoveryApplyButton.Pressed += () => { ApplyDiscoveryUx3C(); };
        discoveryButtons.AddChild(_discoveryPreviewButton);
        discoveryButtons.AddChild(_discoveryApplyButton);
        right.AddChild(discoveryButtons);
        var discoveryReview = ReviewUx3C(out _discoveryDiff, out _discoveryDiagnostics);
        right.AddChild(discoveryReview);
        _discoverySummary = StudioStyle.Label("Aucun aperçu personnel frais.", 9, StudioStyle.Muted, wrap: true);
        right.AddChild(_discoverySummary);

        right.AddChild(StudioStyle.Label("Drag-and-drop vers la zone", 12, StudioStyle.Accent));
        _dropTarget = new StudioBlueprintDropTargetUx3C();
        _dropTarget.BlueprintDropped += HandleDropUx3C;
        right.AddChild(_dropTarget);
        _dropApproval = StudioUx2EnvBControls.Check(
            "J’approuve le placement de cette carte dans la zone active");
        _dropApproval.Toggled += _ => InvalidateDropUx3C();
        right.AddChild(_dropApproval);
        var dropButtons = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        dropButtons.AddThemeConstantOverride("separation", 8);
        _dropPreviewButton = StudioStyle.Button("Prévisualiser le dépôt", compact: true);
        _dropApplyButton = StudioStyle.Button("Appliquer le placement", compact: true);
        _dropPreviewButton.Pressed += PreviewDropUx3C;
        _dropApplyButton.Pressed += () => { ApplyDropUx3C(); };
        dropButtons.AddChild(_dropPreviewButton);
        dropButtons.AddChild(_dropApplyButton);
        right.AddChild(dropButtons);
        right.AddChild(ReviewUx3C(out _dropDiff, out _dropDiagnostics));
        _dropSummary = StudioStyle.Label("Glissez une carte pour préparer un dépôt.", 9, StudioStyle.Muted, wrap: true);
        right.AddChild(_dropSummary);
        right.AddChild(StudioStyle.Label(
            "Frontière runtime · miniature et plan de placement seulement : aucun asset, prefab, script, moteur, acteur, serveur ou transport réseau n’est chargé ou exécuté.",
            9, StudioStyle.Warning, wrap: true));

        var footer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footer.AddThemeConstantOverride("separation", 4);
        var workspaceRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        workspaceRow.AddThemeConstantOverride("separation", 6);
        workspaceRow.AddChild(StudioStyle.Label("Profil Studio", 9, StudioStyle.Muted));
        _workspaceUndo = StudioStyle.Button("Annuler préférences", compact: true);
        _workspaceRedo = StudioStyle.Button("Rétablir préférences", compact: true);
        _workspaceSave = StudioStyle.Button("Enregistrer préférences", compact: true);
        _workspaceUndo.Pressed += () => RunActionUx3C(_session.UndoDiscoveryUx3C(), refresh: true);
        _workspaceRedo.Pressed += () => RunActionUx3C(_session.RedoDiscoveryUx3C(), refresh: true);
        _workspaceSave.Pressed += () => RunActionUx3C(_session.SaveDiscoveryUx3C(), refresh: false);
        workspaceRow.AddChild(_workspaceUndo);
        workspaceRow.AddChild(_workspaceRedo);
        workspaceRow.AddChild(_workspaceSave);
        footer.AddChild(workspaceRow);
        var projectRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        projectRow.AddThemeConstantOverride("separation", 6);
        projectRow.AddChild(StudioStyle.Label("Projet", 9, StudioStyle.Muted));
        _projectUndo = StudioStyle.Button("Annuler placement", compact: true);
        _projectRedo = StudioStyle.Button("Rétablir placement", compact: true);
        _projectSave = StudioStyle.Button("Enregistrer projet", compact: true);
        _projectUndo.Pressed += () => RunActionUx3C(_session.UndoDropUx3C(), refresh: true);
        _projectRedo.Pressed += () => RunActionUx3C(_session.RedoDropUx3C(), refresh: true);
        _projectSave.Pressed += () => RunActionUx3C(_session.SaveDropUx3C(), refresh: false);
        projectRow.AddChild(_projectUndo);
        projectRow.AddChild(_projectRedo);
        projectRow.AddChild(_projectSave);
        footer.AddChild(projectRow);
        _status = StudioStyle.Label("Surface fermée.", 9, StudioStyle.Muted, wrap: true);
        footer.AddChild(_status);
        root.AddChild(footer);
        ResetUx3C();
    }

    internal void Open()
    {
        if (_evidenceSession is not null) ReleaseEvidenceUx3C();
        _session = _sourceSession;
        _usesRealModuleEvidence = false;
        _usesCanonicalFallbackEvidence = false;
        _sourceSnapshot = null;
        _evidenceReleased = true;
        Visible = true;
        ResetUx3C();
        RefreshChoicesUx3C();
        SetStatusUx3C("Bibliothèque ouverte · sélectionnez explicitement une carte.", false);
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && ReferenceEquals(_session, _sourceSession)) return;
        if (_evidenceSession is not null) ReleaseEvidenceUx3C();
        _session.CancelDiscoveryApprovalUx3C();
        _session.CancelDropApprovalUx3C();
        Visible = false;
        ResetUx3C(keepReleaseState: true);
    }

    internal void CloseEvidence()
    {
        if (_evidenceSession is not null) ReleaseEvidenceUx3C();
        Visible = false;
        ResetUx3C(keepReleaseState: true);
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        _session = _sourceSession;
        Visible = true;
        ResetUx3C(keepReleaseState: true);
        RefreshChoicesUx3C();
    }

    internal void HideSourceSurfaceAfterSmoke()
    {
        _session = _sourceSession;
        Visible = false;
        ResetUx3C(keepReleaseState: true);
    }

    internal bool PrepareEvidence(string mode)
    {
        ReleaseEvidenceUx3C();
        _sourceSnapshot = CaptureSourceUx3C();
        var evidence = _sourceSession.CreateBlueprintDiscoveryEvidenceProjectUx3C();
        if (evidence is null) return false;
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux3-c-ui-{Guid.NewGuid():N}");
        var isolated = new StudioSessionController();
        if (!ProjectSession.Create(root, evidence.Project).Succeeded ||
            !StudioSessionController.WriteEvidenceThumbnailUx3C(root, evidence.Project, evidence.BlueprintId) ||
            !isolated.Open(root).Succeeded ||
            !isolated.SelectArea(AreaId.From(evidence.AreaId)))
        {
            isolated.Close();
            StudioSessionController.DeleteTemporaryBlueprintUx3A(root);
            return false;
        }
        _evidenceRoot = root;
        _evidenceSession = isolated;
        _session = isolated;
        _usesRealModuleEvidence = evidence.UsesRealModule;
        _usesCanonicalFallbackEvidence = evidence.UsesCanonicalFallback;
        _evidenceReleased = false;
        Visible = true;
        ResetUx3C(keepReleaseState: true);
        RefreshChoicesUx3C();
        var target = _choices.FirstOrDefault(item => item.Id == evidence.BlueprintId);
        if (target is null) return FailEvidenceUx3C();
        _search.Text = target.Label.Split('·')[0].Trim();
        SelectBlueprintUx3C(target.Id);
        _favorite.SetPressedNoSignal(!target.IsFavorite);
        _discoveryApproval.SetPressedNoSignal(true);
        PreviewDiscoveryUx3C();
        if (!_cards.TryGetValue(target.Id, out var card) ||
            !_dropTarget.DispatchEvidencePayload(card.CreateEvidencePayload())) return FailEvidenceUx3C();
        _dropApproval.SetPressedNoSignal(true);
        PreviewDropUx3C();
        var ready = UsesIsolatedEvidence && (_usesRealModuleEvidence ^ _usesCanonicalFallbackEvidence) &&
                    HasExplicitSelection && DiscoveryPreviewReady && DropPreviewReady && _dragDropObserved &&
                    ThumbnailRendered && ShowsHumanSurface && SourceMatchesUx3C();
        return ready || FailEvidenceUx3C();
    }

    internal bool RunUiTransactionEvidence()
    {
        if (!PrepareEvidence("ux3-c-blueprint-discovery") || _evidenceSession is null ||
            !ReferenceEquals(_session, _evidenceSession)) return false;
        var evidence = _evidenceSession;
        try
        {
            var preferenceApplied = ApplyDiscoveryUx3C();
            var preferenceUndo = preferenceApplied && evidence.UndoDiscoveryUx3C().Succeeded;
            var preferenceRedo = preferenceUndo && evidence.RedoDiscoveryUx3C().Succeeded;
            var preferenceSaved = preferenceRedo && evidence.SaveDiscoveryUx3C().Succeeded;
            _discoveryPreviewReady = false;
            var dropPrepared = _selected is not null && _cards.TryGetValue(_selected.Id, out var card) &&
                               _dropTarget.DispatchEvidencePayload(card.CreateEvidencePayload());
            _dropApproval.SetPressedNoSignal(true);
            if (dropPrepared) PreviewDropUx3C();
            var dropApplied = ApplyDropUx3C();
            var dropUndo = dropApplied && evidence.UndoDropUx3C().Succeeded;
            var dropRedo = dropUndo && evidence.RedoDropUx3C().Succeeded;
            var projectSaved = dropRedo && evidence.SaveDropUx3C().Succeeded;
            var projectJson = ProjectJsonSerializer.Serialize(evidence.Project!);
            var workspaceJson = EditorWorkspaceProfileJsonSerializer.Serialize(evidence.WorkspaceProfile);
            var reopened = new StudioSessionController();
            var persisted = reopened.Open(_evidenceRoot!).Succeeded &&
                            ProjectJsonSerializer.Serialize(reopened.Project!) == projectJson &&
                            EditorWorkspaceProfileJsonSerializer.Serialize(reopened.WorkspaceProfile) == workspaceJson;
            reopened.Close();
            return preferenceApplied && preferenceUndo && preferenceRedo && preferenceSaved && dropPrepared && dropApplied &&
                   dropUndo && dropRedo && projectSaved && persisted && SourceMatchesUx3C();
        }
        finally
        {
            CloseEvidence();
        }
    }

    private void RefreshChoicesUx3C()
    {
        if (_session.Project is null)
        {
            _choices = [];
            RebuildCardsUx3C();
            return;
        }
        EditorPaletteFilter filter;
        try { filter = BuildFilterUx3C(); }
        catch (ArgumentException exception)
        {
            _resultSummary.Text = "Filtres refusés · " + exception.Message;
            return;
        }
        _choices = _session.BuildBlueprintDiscoveryUx3C(CurrentViewUx3C(), _search.Text, filter);
        RebuildCardsUx3C();
    }

    private void RebuildCardsUx3C()
    {
        ClearUx3C(_results);
        _cards.Clear();
        foreach (var choice in _choices.Take(100))
        {
            var card = new StudioBlueprintDiscoveryCardUx3C(choice, LoadTextureUx3C(choice.PreviewAbsolutePath));
            card.Selected += SelectBlueprintUx3C;
            card.FavoriteRequested += QuickFavoriteUx3C;
            _cards[choice.Id] = card;
            _results.AddChild(card);
        }
        _resultSummary.Text = _choices.Count == 0
            ? "Aucun blueprint ne correspond à cette recherche."
            : $"{_choices.Count} résultat(s) · 6 dimensions filtrables · {Math.Min(100, _choices.Count)} carte(s) affichée(s).";
        if (_selected is not null && _choices.All(item => item.Id != _selected.Id)) ClearSelectionUx3C();
    }

    private void SelectBlueprintUx3C(Guid blueprintId)
    {
        var choice = _session.BuildBlueprintDiscoveryUx3C(BlueprintPaletteView.All, null,
            EditorPaletteFilter.ShowAll).FirstOrDefault(item => item.Id == blueprintId);
        if (choice is null) return;
        _selected = choice;
        _explicitSelection = true;
        _discoveryDraft = _session.CreateDiscoveryDraftUx3C(choice.Id);
        _dropDraft = null;
        _favorite.SetPressedNoSignal(choice.IsFavorite);
        _discoveryApproval.SetPressedNoSignal(false);
        _dropApproval.SetPressedNoSignal(false);
        _selection.Text = $"Sélection · {choice.Label}";
        _metadata.Text = $"Kit {choice.Kit} · licence {choice.License} · " +
                         $"styles {(choice.Styles.Count == 0 ? "non renseignés" : string.Join(", ", choice.Styles))} · " +
                         $"tags {(choice.Tags.Count == 0 ? "non renseignés" : string.Join(", ", choice.Tags))}.";
        var texture = LoadTextureUx3C(choice.PreviewAbsolutePath);
        _thumbnail.Texture = texture;
        _thumbnail.Visible = texture is not null;
        _thumbnailFallback.Visible = texture is null;
        _thumbnailFallback.Text = string.IsNullOrWhiteSpace(choice.PreviewRelativePath)
            ? "Aucune miniature authorée.\nAperçu sémantique uniquement."
            : "Miniature indisponible sur disque.\nChemin conservé, repli sémantique explicite.";
        ResetPreviewsUx3C();
        UpdateButtonsUx3C();
    }

    private void QuickFavoriteUx3C(Guid blueprintId)
    {
        SelectBlueprintUx3C(blueprintId);
        _favorite.SetPressedNoSignal(!_favorite.ButtonPressed);
        _discoveryApproval.SetPressedNoSignal(true);
        PreviewDiscoveryUx3C();
        SetStatusUx3C("Favori préparé · vérifiez l’aperçu puis appliquez.", false);
    }

    private StudioUx3CDiscoveryDraft? ReadDiscoveryDraftUx3C()
    {
        if (_discoveryDraft is null || _selected is null) return null;
        var filter = BuildFilterUx3C();
        return _discoveryDraft with
        {
            View = CurrentViewUx3C(),
            Query = _search.Text.Trim(),
            Kinds = filter.Kinds,
            Kits = filter.Kits,
            Styles = filter.Styles,
            Licenses = filter.Licenses,
            Compatibility = filter.Compatibility,
            Tags = filter.Tags,
            Favorite = _favorite.ButtonPressed,
            Approved = _discoveryApproval.ButtonPressed,
        };
    }

    private void PreviewDiscoveryUx3C()
    {
        var draft = ReadDiscoveryDraftUx3C();
        if (draft is null) return;
        _discoveryDraft = draft;
        _discoveryPreview = _session.PreviewDiscoveryUx3C(draft);
        _discoveryPreviewReady = _discoveryPreview.Succeeded && _discoveryPreview.CanApply;
        RenderReviewUx3C(_discoveryDiff, _discoveryDiagnostics, _discoveryPreview.SemanticDiff,
            _discoveryPreview.Diagnostics.Select(item => $"{item.Code} · {item.Message}"));
        _discoverySummary.Text = _discoveryPreview.Message;
        SetStatusUx3C(_discoveryPreview.Message, !_discoveryPreview.Succeeded);
        UpdateButtonsUx3C();
    }

    private bool ApplyDiscoveryUx3C()
    {
        var draft = ReadDiscoveryDraftUx3C();
        if (draft is null || !_discoveryPreviewReady) return false;
        var result = _session.ApplyApprovedDiscoveryUx3C(draft);
        SetStatusUx3C(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            var selectedId = _selected?.Id;
            RefreshChoicesUx3C();
            if (selectedId is not null) SelectBlueprintUx3C(selectedId.Value);
        }
        return result.Succeeded;
    }

    private void HandleDropUx3C(Guid blueprintId)
    {
        if (_selected?.Id != blueprintId) SelectBlueprintUx3C(blueprintId);
        _dropDraft = _session.CreateDropDraftUx3C(blueprintId);
        _dragDropObserved = _dropDraft is not null;
        _dropApproval.SetPressedNoSignal(false);
        _dropPreviewReady = false;
        _dropSummary.Text = _dropDraft is null
            ? "Aucun emplacement libre et valide n’a été trouvé dans la zone active."
            : $"Carte reçue · position proposée X {_dropDraft.Position.X:0.##}, Y {_dropDraft.Position.Y:0.##}, Z {_dropDraft.Position.Z:0.##}.";
        UpdateButtonsUx3C();
    }

    private void PreviewDropUx3C()
    {
        if (_dropDraft is null) return;
        _dropDraft = _dropDraft with { Approved = _dropApproval.ButtonPressed };
        _dropPreview = _session.PreviewDropUx3C(_dropDraft);
        if (_dropPreview.CorrectedPosition is { } corrected) _dropDraft = _dropDraft with { Position = corrected };
        _dropPreviewReady = _dropPreview.Succeeded && _dropPreview.CanApply;
        RenderReviewUx3C(_dropDiff, _dropDiagnostics, _dropPreview.SemanticDiff,
            _dropPreview.Diagnostics.Select(item => $"{item.Code} · {item.Message}"));
        _dropSummary.Text = _dropPreview.Message;
        SetStatusUx3C(_dropPreview.Message, !_dropPreview.Succeeded);
        UpdateButtonsUx3C();
    }

    private bool ApplyDropUx3C()
    {
        if (_dropDraft is null || !_dropPreviewReady) return false;
        _dropDraft = _dropDraft with { Approved = _dropApproval.ButtonPressed };
        var result = _session.ApplyApprovedDropUx3C(_dropDraft);
        SetStatusUx3C(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _dropPreviewReady = false;
            _dropApplyButton.Disabled = true;
            ProjectChanged?.Invoke();
        }
        return result.Succeeded;
    }

    private void FiltersChangedUx3C()
    {
        if (_updating) return;
        InvalidateDiscoveryUx3C();
        RefreshChoicesUx3C();
    }

    private void InvalidateDiscoveryUx3C()
    {
        if (_updating) return;
        _session.CancelDiscoveryApprovalUx3C();
        _discoveryPreviewReady = false;
        _discoveryApplyButton.Disabled = true;
        _discoverySummary.Text = "Brouillon personnel modifié · relancez l’aperçu sec.";
    }

    private void InvalidateDropUx3C()
    {
        if (_updating) return;
        _session.CancelDropApprovalUx3C();
        _dropPreviewReady = false;
        _dropApplyButton.Disabled = true;
        _dropSummary.Text = "Dépôt modifié · relancez l’aperçu sec.";
    }

    private void ResetUx3C(bool keepReleaseState = false)
    {
        _updating = true;
        _search.Text = string.Empty;
        _view.Select(0);
        _kind.Select(0);
        foreach (var edit in new[] { _kit, _style, _license, _compatibility, _tags }) edit.Text = string.Empty;
        _favorite.SetPressedNoSignal(false);
        _discoveryApproval.SetPressedNoSignal(false);
        _dropApproval.SetPressedNoSignal(false);
        _thumbnail.Texture = null;
        _thumbnail.Visible = false;
        _thumbnailFallback.Visible = true;
        _thumbnailFallback.Text = "Sélectionnez explicitement une carte.";
        _selection.Text = "Aucun blueprint sélectionné.";
        _metadata.Text = "L’aperçu charge uniquement une miniature locale gouvernée ; aucun asset, prefab ou moteur n’est instancié.";
        _updating = false;
        _choices = [];
        _cards.Clear();
        _selected = null;
        _discoveryDraft = null;
        _dropDraft = null;
        _explicitSelection = false;
        _dragDropObserved = false;
        if (!keepReleaseState) _evidenceReleased = _evidenceSession is null;
        ClearUx3C(_results);
        ResetPreviewsUx3C();
        UpdateButtonsUx3C();
    }

    private void ResetPreviewsUx3C()
    {
        _discoveryPreview = null;
        _dropPreview = null;
        _discoveryPreviewReady = false;
        _dropPreviewReady = false;
        RenderReviewUx3C(_discoveryDiff, _discoveryDiagnostics, [], []);
        RenderReviewUx3C(_dropDiff, _dropDiagnostics, [], []);
        _discoverySummary.Text = "Aucun aperçu personnel frais.";
        _dropSummary.Text = "Glissez une carte pour préparer un dépôt.";
    }

    private void ClearSelectionUx3C()
    {
        _selected = null;
        _discoveryDraft = null;
        _dropDraft = null;
        _explicitSelection = false;
        _selection.Text = "Aucun blueprint sélectionné.";
        _thumbnail.Texture = null;
        _thumbnail.Visible = false;
        _thumbnailFallback.Visible = true;
        ResetPreviewsUx3C();
    }

    private void UpdateButtonsUx3C()
    {
        var selected = _selected is not null;
        _favorite.Disabled = !selected;
        _discoveryApproval.Disabled = !selected;
        _discoveryPreviewButton.Disabled = !selected;
        _discoveryApplyButton.Disabled = !DiscoveryPreviewReady;
        _dropApproval.Disabled = _dropDraft is null;
        _dropPreviewButton.Disabled = _dropDraft is null;
        _dropApplyButton.Disabled = !DropPreviewReady;
        _workspaceUndo.Disabled = !_session.CanUndoWorkspaceProfile;
        _workspaceRedo.Disabled = false;
        _workspaceSave.Disabled = !_session.WorkspaceProfileHasUnsavedChanges;
        _projectUndo.Disabled = !_session.CanUndo;
        _projectRedo.Disabled = !_session.CanRedo;
        _projectSave.Disabled = !_session.IsDirty;
    }

    private void RunActionUx3C(StudioUx3CActionResult result, bool refresh)
    {
        SetStatusUx3C(result.Message, !result.Succeeded);
        if (result.Succeeded && refresh)
        {
            var id = _selected?.Id;
            RefreshChoicesUx3C();
            if (id is not null) SelectBlueprintUx3C(id.Value);
            ProjectChanged?.Invoke();
        }
        UpdateButtonsUx3C();
    }

    private LineEdit FilterUx3C(string placeholder)
    {
        var edit = StudioUx2EnvBControls.Text(placeholder);
        edit.TextChanged += _ => FiltersChangedUx3C();
        return edit;
    }

    private EditorPaletteFilter BuildFilterUx3C()
    {
        var kinds = _kind.Selected switch
        {
            1 => [BlueprintKind.Creature],
            2 => [BlueprintKind.Placeable],
            3 => [BlueprintKind.Door],
            4 => [BlueprintKind.Item],
            5 => [BlueprintKind.Trigger],
            6 => [BlueprintKind.PlayerSpawn],
            _ => Enum.GetValues<BlueprintKind>(),
        };
        return new EditorPaletteFilter(kinds, TokensUx3C(_kit.Text), TokensUx3C(_style.Text),
            TokensUx3C(_license.Text), TokensUx3C(_compatibility.Text), TokensUx3C(_tags.Text));
    }

    private BlueprintPaletteView CurrentViewUx3C() => _view.Selected switch
    {
        1 => BlueprintPaletteView.Favorites,
        2 => BlueprintPaletteView.Recent,
        3 => BlueprintPaletteView.InstalledPacks,
        4 => BlueprintPaletteView.ProjectContent,
        _ => BlueprintPaletteView.All,
    };

    private Texture2D? LoadTextureUx3C(string? absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath)) return null;
        try
        {
            var image = Image.LoadFromFile(absolutePath);
            if (image is null || image.IsEmpty()) return null;
            if (image.GetWidth() > 512 || image.GetHeight() > 320)
            {
                var ratio = Math.Min(512f / image.GetWidth(), 320f / image.GetHeight());
                image.Resize(Math.Max(1, (int)Math.Round(image.GetWidth() * ratio)),
                    Math.Max(1, (int)Math.Round(image.GetHeight() * ratio)), Image.Interpolation.Lanczos);
            }
            return ImageTexture.CreateFromImage(image);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private void SetStatusUx3C(string message, bool error)
    {
        _status.Text = HumanTextUx3C(message);
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Success;
        StatusChanged?.Invoke(_status.Text, error);
    }

    private SourceSnapshotUx3C CaptureSourceUx3C()
    {
        var history = _sourceSession.History;
        return new(_sourceSession.Project, history, history?.State.Revision, history?.Journal.ToArray() ?? [],
            _sourceSession.IsDirty, history?.CanUndo, history?.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            EditorWorkspaceProfileJsonSerializer.Serialize(_sourceSession.WorkspaceProfile),
            _sourceSession.CaptureBlueprintDiscoveryApprovalUx3C());
    }

    private bool SourceMatchesUx3C()
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
               EditorWorkspaceProfileJsonSerializer.Serialize(_sourceSession.WorkspaceProfile) == snapshot.WorkspaceJson &&
               _sourceSession.CaptureBlueprintDiscoveryApprovalUx3C() == snapshot.Approval;
    }

    private void ReleaseEvidenceUx3C()
    {
        _evidenceSession?.Close();
        _evidenceSession = null;
        _evidenceReleased = string.IsNullOrWhiteSpace(_evidenceRoot) ||
                            StudioSessionController.DeleteTemporaryBlueprintUx3A(_evidenceRoot);
        _evidenceRoot = null;
        _session = _sourceSession;
    }

    private bool FailEvidenceUx3C()
    {
        ReleaseEvidenceUx3C();
        Visible = false;
        return false;
    }

    private static HBoxContainer RowUx3C(params Control[] controls)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 6);
        foreach (var control in controls)
        {
            control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(control);
        }
        return row;
    }

    private static Control ReviewUx3C(out VBoxContainer diff, out VBoxContainer diagnostics)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 8);
        diff = ReviewColumnUx3C(row, "Diff humain");
        diagnostics = ReviewColumnUx3C(row, "Diagnostics");
        return row;
    }

    private static VBoxContainer ReviewColumnUx3C(HBoxContainer row, string title)
    {
        var panel = StudioStyle.Panel("Ux3C" + title.Replace(" ", string.Empty), StudioStyle.Surface, 7, 1);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var margin = StudioStyle.Margin(8, 6, 8, 6);
        panel.AddChild(margin);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 3);
        column.AddChild(StudioStyle.Label(title, 9, StudioStyle.Accent));
        margin.AddChild(column);
        row.AddChild(panel);
        return column;
    }

    private static void RenderReviewUx3C(VBoxContainer diff, VBoxContainer diagnostics,
        IEnumerable<string> changes, IEnumerable<string> errors)
    {
        ClearReviewUx3C(diff);
        ClearReviewUx3C(diagnostics);
        var changeItems = changes.ToArray();
        var errorItems = errors.ToArray();
        foreach (var item in changeItems) diff.AddChild(StudioStyle.Label(HumanTextUx3C(item), 9, StudioStyle.Text, wrap: true));
        foreach (var item in errorItems) diagnostics.AddChild(StudioStyle.Label(HumanTextUx3C(item), 9, StudioStyle.Danger, wrap: true));
        if (changeItems.Length == 0) diff.AddChild(StudioStyle.Label("Aucun changement.", 9, StudioStyle.Muted));
        if (errorItems.Length == 0) diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic.", 9, StudioStyle.Muted));
    }

    private static void ClearReviewUx3C(Node root)
    {
        foreach (var child in root.GetChildren().Skip(1).ToArray())
        {
            root.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static void ClearUx3C(Node root)
    {
        foreach (var child in root.GetChildren().ToArray())
        {
            root.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static IReadOnlyList<string> TokensUx3C(string text) => text
        .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(item => item.Length > 0).ToArray();

    private static IEnumerable<string> DescendantTextUx3C(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Label label) yield return label.Text;
            if (child is Button button) yield return button.Text;
            if (child is LineEdit edit) yield return edit.Text;
            foreach (var nested in DescendantTextUx3C(child)) yield return nested;
        }
    }

    private static string HumanTextUx3C(string text) => UuidRegexUx3C().Replace(text ?? string.Empty,
        "référence protégée");

    [GeneratedRegex(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex UuidRegexUx3C();

    private sealed record SourceSnapshotUx3C(
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
        string WorkspaceJson,
        StudioUx3CApprovalSnapshot Approval);
}
