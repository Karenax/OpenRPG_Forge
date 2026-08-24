using System.Globalization;
using System.Text.RegularExpressions;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioRuntimeTransformsWorkbenchUx6W : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private SourceSnapshotUx6W? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;
    private bool _updating;
    private bool _explicitAreaSelection;
    private bool _explicitEntitySelection;
    private bool _overrideDirty;
    private bool _tweenDirty;
    private Guid? _editingOverrideId;
    private Guid? _editingTweenId;
    private IReadOnlyList<StudioUx6WAreaChoice> _areas = [];
    private IReadOnlyList<StudioUx6WEntityChoice> _entities = [];
    private IReadOnlyList<StudioUx6WEntityChoice> _parentEntities = [];
    private IReadOnlyList<StudioUx6WItemChoice> _overrides = [];
    private IReadOnlyList<StudioUx6WItemChoice> _tweens = [];

    private OptionButton _areaChoice = null!;
    private OptionButton _entityChoice = null!;
    private OptionButton _overrideChoice = null!;
    private OptionButton _parentChoice = null!;
    private OptionButton _tweenChoice = null!;
    private OptionButton _channel = null!;
    private OptionButton _overrideEasing = null!;
    private OptionButton _overrideAuthority = null!;
    private OptionButton _overrideSave = null!;
    private OptionButton _tweenEasing = null!;
    private OptionButton _tweenAuthority = null!;
    private OptionButton _tweenSave = null!;
    private LineEdit _overrideDuration = null!;
    private LineEdit _tweenDuration = null!;
    private LineEdit _property = null!;
    private LineEdit _fromX = null!;
    private LineEdit _fromY = null!;
    private LineEdit _fromZ = null!;
    private LineEdit _fromW = null!;
    private LineEdit _toX = null!;
    private LineEdit _toY = null!;
    private LineEdit _toZ = null!;
    private LineEdit _toW = null!;
    private LineEdit _fromState = null!;
    private LineEdit _toState = null!;
    private CheckBox _overrideReplicate = null!;
    private CheckBox _tweenReplicate = null!;
    private TransformEditorUx6W _overrideFrom = null!;
    private TransformEditorUx6W _overrideTo = null!;
    private TransformEditorUx6W _tweenFrom = null!;
    private TransformEditorUx6W _tweenTo = null!;
    private TabContainer _tabs = null!;
    private VBoxContainer _results = null!;
    private Label _review = null!;
    private Label _status = null!;
    private Button _apply = null!;
    private Button _undo = null!;
    private Button _redo = null!;

    private StudioUx6WDraft? _draft;
    private StudioUx6WPreview? _preview;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? CinematicsChanged;

    internal bool IsOpen => Visible;
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                            !ReferenceEquals(_session, _sourceSession);
    internal bool HasExplicitSelection => _explicitAreaSelection && _explicitEntitySelection && _draft is not null;
    internal bool PreviewReady => _preview is
    {
        Succeeded: true,
        CanApply: true,
        CanonicalDryRun: true,
        OverrideRuntimeVerified: true,
        TransformTweenVerified: true,
        MaterialTweenVerified: true,
        StateTweenVerified: true,
        AuthorityVerified: true,
        CancellationVerified: true,
        RuntimeClaimsBounded: true,
    };
    internal bool ShowsThreeWorkflows => _tabs.GetTabCount() == 3 && _draft is not null;
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantText(this));
            return text.Contains("Transformations runtime", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu sec canonique", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aucun nœud Godot", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuid(text);
        }
    }
    internal bool EvidenceSourceUntouched => SourceMatches();
    internal bool EvidenceResourcesReleased => _evidenceReleased && _evidenceSession is null &&
                                                _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);

    internal StudioRuntimeTransformsWorkbenchUx6W(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        BuildUi();
    }

    private void BuildUi()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        Color = new Color(.012f, .018f, .032f, .94f);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var dialog = StudioStyle.Panel("Ux6WRuntimeTransformsDialog", StudioStyle.Surface, 12, 1);
        dialog.ClipContents = true;
        AddChild(dialog);
        dialog.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        dialog.OffsetLeft = 18;
        dialog.OffsetTop = 14;
        dialog.OffsetRight = -18;
        dialog.OffsetBottom = -14;
        var margin = StudioStyle.Margin(15, 12, 15, 12);
        dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("TRANSFORMATIONS RUNTIME · UX6-W", 10, StudioStyle.Accent));
        titles.AddChild(StudioStyle.Label("Overrides visuels et interpolations déterministes", 18,
            StudioStyle.Text, wrap: true));
        titles.AddChild(StudioStyle.Label(
            "ORF-EE-001 + ORF-EE-027 · aperçu sec canonique · aucun nœud Godot, matériau ou état live modifié",
            9, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);
        root.AddChild(header);

        var selection = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _areaChoice = Choice(300);
        _entityChoice = Choice(430);
        selection.AddChild(Column("Zone de contexte", _areaChoice));
        selection.AddChild(Column("Objet cible", _entityChoice));
        root.AddChild(selection);

        var tabHost = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 380),
            CustomMaximumSize = new Vector2(-1, 500),
            ClipContents = true,
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
        BuildOverrideTab();
        BuildTweenTab();
        BuildRuntimeTab();

        var preview = StudioStyle.Panel("Ux6WRuntimeTransformsPreview", StudioStyle.SurfaceRaised, 8, 1);
        preview.CustomMinimumSize = new Vector2(0, 126);
        preview.CustomMaximumSize = new Vector2(-1, 174);
        var previewMargin = StudioStyle.Margin(10, 8, 10, 8);
        preview.AddChild(previewMargin);
        var previewScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _results = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        previewScroll.AddChild(_results);
        previewMargin.AddChild(previewScroll);
        root.AddChild(preview);

        _review = StudioStyle.Label("Aucune modification approuvée.", 9, StudioStyle.Muted, wrap: true);
        root.AddChild(_review);
        _status = StudioStyle.Label("Sélectionnez explicitement une zone puis un objet.", 10,
            StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 8);
        actions.AddChild(ActionButton("Aperçu sec canonique", Preview, 176));
        _apply = ActionButton("Appliquer", Apply, 120, primary: true);
        _undo = ActionButton("Annuler", Undo, 110);
        _redo = ActionButton("Rétablir", Redo, 116);
        actions.AddChild(_apply);
        actions.AddChild(_undo);
        actions.AddChild(_redo);
        actions.AddChild(ActionButton("Enregistrer", Save, 126));
        root.AddChild(actions);

        _areaChoice.ItemSelected += index => SelectArea((int)index);
        _entityChoice.ItemSelected += index => SelectEntity((int)index);
        _overrideChoice.ItemSelected += index => SelectOverride((int)index);
        _tweenChoice.ItemSelected += index => SelectTween((int)index);
        WireFormInvalidation();
        ClearPreview();
        UpdateButtons();
    }

    private void BuildOverrideTab()
    {
        var page = TabPage("Overrides visuels");
        page.AddChild(StudioStyle.Label("ORF-EE-001 · transform hiérarchique persistant", 10,
            StudioStyle.Accent, wrap: true));
        var choose = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _overrideChoice = Choice(420);
        choose.AddChild(Column("Override", _overrideChoice));
        choose.AddChild(ActionButton("Nouvel override", NewOverride, 138));
        choose.AddChild(ActionButton("Retirer", RemoveOverride, 92));
        page.AddChild(choose);
        _parentChoice = Choice(420);
        page.AddChild(Column("Parent optionnel", _parentChoice));
        var transforms = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _overrideFrom = BuildTransformEditor(transforms, "Transform initial");
        _overrideTo = BuildTransformEditor(transforms, "Transform final");
        page.AddChild(transforms);
        var policy = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _overrideDuration = Field("1 à 86 400 000");
        _overrideEasing = EnumChoice<SequenceEasing>();
        _overrideAuthority = EnumChoice<TransformAuthority>();
        _overrideSave = EnumChoice<TransformSaveBehavior>();
        _overrideReplicate = new CheckBox { Text = "Répliquer" };
        policy.AddChild(Column("Durée (ms)", _overrideDuration));
        policy.AddChild(Column("Interpolation", _overrideEasing));
        policy.AddChild(Column("Autorité", _overrideAuthority));
        policy.AddChild(Column("Sauvegarde", _overrideSave));
        policy.AddChild(Column("Réseau", _overrideReplicate));
        page.AddChild(policy);
    }

    private void BuildTweenTab()
    {
        var page = TabPage("Tweens transform / matériau / état");
        page.AddChild(StudioStyle.Label("ORF-EE-027 · interpolation, autorité et annulation", 10,
            StudioStyle.Accent, wrap: true));
        var choose = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _tweenChoice = Choice(420);
        choose.AddChild(Column("Tween", _tweenChoice));
        choose.AddChild(ActionButton("Nouveau tween", NewTween, 132));
        choose.AddChild(ActionButton("Retirer", RemoveTween, 92));
        page.AddChild(choose);
        var channel = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _channel = EnumChoice<AnimationChannelKind>();
        _property = Field("ex. emission ou stance");
        channel.AddChild(Column("Canal", _channel));
        channel.AddChild(Column("Propriété", _property));
        page.AddChild(channel);
        var transforms = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _tweenFrom = BuildTransformEditor(transforms, "Transform initial");
        _tweenTo = BuildTransformEditor(transforms, "Transform final");
        page.AddChild(transforms);
        var values = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _fromX = Field("X / R"); _fromY = Field("Y / V"); _fromZ = Field("Z / B"); _fromW = Field("W / A");
        _toX = Field("X / R"); _toY = Field("Y / V"); _toZ = Field("Z / B"); _toW = Field("W / A");
        _fromState = Field("état initial"); _toState = Field("état final");
        var fromValues = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        fromValues.AddChild(StudioStyle.Label("Valeur initiale", 9, StudioStyle.Muted));
        fromValues.AddChild(ValueRow(_fromX, _fromY, _fromZ, _fromW));
        fromValues.AddChild(_fromState);
        var toValues = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        toValues.AddChild(StudioStyle.Label("Valeur finale", 9, StudioStyle.Muted));
        toValues.AddChild(ValueRow(_toX, _toY, _toZ, _toW));
        toValues.AddChild(_toState);
        values.AddChild(fromValues); values.AddChild(toValues);
        page.AddChild(values);
        var policy = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _tweenDuration = Field("1 à 86 400 000");
        _tweenEasing = EnumChoice<SequenceEasing>();
        _tweenAuthority = EnumChoice<TransformAuthority>();
        _tweenSave = EnumChoice<TransformSaveBehavior>();
        _tweenReplicate = new CheckBox { Text = "Répliquer" };
        policy.AddChild(Column("Durée (ms)", _tweenDuration));
        policy.AddChild(Column("Interpolation", _tweenEasing));
        policy.AddChild(Column("Autorité", _tweenAuthority));
        policy.AddChild(Column("Sauvegarde", _tweenSave));
        policy.AddChild(Column("Réseau", _tweenReplicate));
        page.AddChild(policy);
    }

    private void BuildRuntimeTab()
    {
        var page = TabPage("Aperçu runtime copié");
        page.AddChild(StudioStyle.Label("RUNTIMES PURS · TransformOverrideRuntime + AnimationTweenRuntime", 10,
            StudioStyle.Accent, wrap: true));
        page.AddChild(StudioStyle.Label(
            "L’aperçu avance chaque override et tween sur une copie, vérifie l’autorité serveur, les cinq courbes d’interpolation, l’état final et l’annulation.",
            10, StudioStyle.Text, wrap: true));
        page.AddChild(StudioStyle.Label(
            "Les canaux Transform, Matériau et État partagent une identité stable, une durée, une autorité, une politique de sauvegarde et une réplication explicite.",
            10, StudioStyle.Text, wrap: true));
        var warning = StudioStyle.Panel("Ux6WRuntimeBoundary", StudioStyle.Surface, 8, 1);
        var warningMargin = StudioStyle.Margin(10, 9, 10, 9);
        warning.AddChild(warningMargin);
        warningMargin.AddChild(StudioStyle.Label(
            "Aucun nœud Godot, matériau, état d’acteur, scène, serveur ou transport réseau n’est modifié par Studio. Le runtime de jeu reste l’unique consommateur visuel.",
            10, StudioStyle.Warning, wrap: true));
        page.AddChild(warning);
    }

    internal void Open() => OpenBound(_sourceSession, evidence: false, cancelApproval: false);

    internal void RestoreSourceSurfaceAfterSmoke() =>
        OpenBound(_sourceSession, evidence: false, cancelApproval: false);

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null) return;
        if (Visible) _session.CancelRuntimeTransformApprovalUx6W();
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
        if (!mode.StartsWith("ux6-w", StringComparison.OrdinalIgnoreCase)) return false;
        ReleaseEvidence();
        _sourceSnapshot = CaptureSource();
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux6-w-{Guid.NewGuid():N}");
        try
        {
            var source = _sourceSession.Project;
            if (source is null) return false;
            if (!ProjectSession.Create(root,
                    StudioSessionController.CreateRuntimeTransformEvidenceProjectUx6W(source)).Succeeded)
                return false;
            var isolated = new StudioSessionController();
            if (!isolated.Open(root).Succeeded)
            {
                isolated.Close();
                StudioSessionController.DeleteTemporaryUx6W(root);
                return false;
            }
            _evidenceSession = isolated;
            _evidenceRoot = root;
            _evidenceReleased = false;
            OpenBound(isolated, evidence: true, cancelApproval: true);
            var area = isolated.Project!.Areas.First(item => item.Entities.Count >= 2);
            var areaIndex = _areas.ToList().FindIndex(item => item.Id == area.Id);
            if (areaIndex < 0) return false;
            _areaChoice.Select(areaIndex + 1);
            SelectArea(areaIndex + 1);
            var entityIndex = _entities.ToList().FindIndex(item => item.Id == area.Entities[0].Id);
            if (entityIndex < 0) return false;
            _entityChoice.Select(entityIndex + 1);
            SelectEntity(entityIndex + 1);
            if (_draft is null) return false;
            _draft = StudioSessionController.ChangedRuntimeTransformDraftUx6W(_draft);
            PopulateItems();
            SelectOverride(1);
            var materialIndex = _draft.CandidateProfile.Tweens.ToList()
                .FindIndex(item => item.Channel == AnimationChannelKind.MaterialParameter);
            if (materialIndex >= 0)
            {
                _tweenChoice.Select(materialIndex + 1);
                SelectTween(materialIndex + 1);
            }
            _preview = _session.PreviewRuntimeTransformsUx6W(_draft);
            RenderPreview();
            _tabs.CurrentTab = mode.EndsWith("-tweens", StringComparison.OrdinalIgnoreCase)
                ? 1
                : mode.EndsWith("-runtime-preview", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
            return PreviewReady && ShowsThreeWorkflows && ShowsHumanSurface && SourceMatches();
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_W_EVIDENCE_FAILED type={exception.GetType().Name}");
            return false;
        }
        finally
        {
            if (!PreviewReady || !ShowsThreeWorkflows || !SourceMatches())
            {
                if (_evidenceSession is not null) ReleaseEvidence();
                else StudioSessionController.DeleteTemporaryUx6W(root);
            }
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        var sourceApproval = _sourceSession.CaptureRuntimeTransformApprovalUx6W();
        try
        {
            var ready = PrepareEvidence("ux6-w-runtime-transforms");
            if (!ready || _evidenceSession is null || !ReferenceEquals(_session, _evidenceSession) ||
                !PreviewReady || string.IsNullOrWhiteSpace(_evidenceRoot)) return false;
            var applied = _session.ApplyApprovedRuntimeTransformsUx6W();
            var undo = applied.Succeeded ? _session.UndoRuntimeTransformsUx6W() : new(false, "non exécuté");
            var redo = undo.Succeeded ? _session.RedoRuntimeTransformsUx6W() : new(false, "non exécuté");
            var saved = redo.Succeeded ? _session.SaveRuntimeTransformsUx6W() : new(false, "non exécuté");
            var json = _session.Project is null ? string.Empty : ProjectJsonSerializer.Serialize(_session.Project);
            var reopened = new StudioSessionController();
            var roundTrip = saved.Succeeded && reopened.Open(_evidenceRoot).Succeeded &&
                            reopened.Project is not null && string.Equals(json,
                                ProjectJsonSerializer.Serialize(reopened.Project), StringComparison.Ordinal);
            reopened.Close();
            return applied.Succeeded && undo.Succeeded && redo.Succeeded && saved.Succeeded && roundTrip &&
                   SourceMatches();
        }
        finally
        {
            CloseEvidence();
            _sourceSession.RestoreRuntimeTransformApprovalUx6W(sourceApproval);
        }
    }

    private void OpenBound(StudioSessionController target, bool evidence, bool cancelApproval)
    {
        _session = target;
        if (cancelApproval) _session.CancelRuntimeTransformApprovalUx6W();
        Visible = true;
        _draft = null;
        _preview = null;
        _explicitAreaSelection = false;
        _explicitEntitySelection = false;
        PopulateAreas();
        PopulateEntities();
        PopulateItems();
        ClearPreview();
        _status.Text = evidence
            ? "Preuve isolée · choisissez explicitement la zone et l’objet de démonstration."
            : "Sélectionnez explicitement une zone puis un objet.";
        UpdateButtons();
    }

    private void PopulateAreas()
    {
        _areas = _session.RuntimeTransformAreaChoicesUx6W();
        _areaChoice.Clear();
        _areaChoice.AddItem(_areas.Count == 0 ? "Aucune zone avec objet" : "Choisir une zone…");
        foreach (var item in _areas) _areaChoice.AddItem(item.Label);
        _areaChoice.Select(0);
        _areaChoice.Disabled = _areas.Count == 0;
    }

    private void PopulateEntities()
    {
        _entities = _session.RuntimeTransformAllEntityChoicesUx6W();
        _entityChoice.Clear();
        _entityChoice.AddItem(_entities.Count == 0 ? "Aucun objet disponible" : "Choisir un objet…");
        foreach (var item in _entities) _entityChoice.AddItem(item.Label);
        _entityChoice.Select(0);
        _entityChoice.Disabled = _entities.Count == 0;
        PopulateParentChoices(null);
    }

    private void SelectArea(int index)
    {
        if (_updating) return;
        _explicitAreaSelection = index > 0 && index <= _areas.Count;
        _draft = _explicitAreaSelection
            ? _session.CreateRuntimeTransformDraftUx6W(true, _areas[index - 1].Id)
            : null;
        _explicitEntitySelection = false;
        _entityChoice.Select(0);
        PopulateItems();
        ClearPreview();
        SetStatus(_explicitAreaSelection
            ? "Zone chargée · sélectionnez explicitement un objet."
            : "Sélectionnez explicitement une zone.");
    }

    private void SelectEntity(int index)
    {
        if (_updating) return;
        _explicitEntitySelection = _explicitAreaSelection && index > 0 && index <= _entities.Count;
        PopulateParentChoices(_explicitEntitySelection ? _entities[index - 1].Id : null);
        if (_explicitEntitySelection)
        {
            SetDefaultOverrideForm(_entities[index - 1].Id);
            SetDefaultTweenForm(_entities[index - 1].Id);
            SetStatus("Objet chargé · choisissez une entrée existante ou créez-en une explicitement.");
        }
        else SetStatus("Sélectionnez explicitement un objet.");
        UpdateButtons();
    }

    private void PopulateItems()
    {
        _overrides = _session.RuntimeOverrideChoicesUx6W(_draft);
        _overrideChoice.Clear();
        _overrideChoice.AddItem(_overrides.Count == 0 ? "Aucun override · état vide" : "Choisir un override…");
        foreach (var item in _overrides) _overrideChoice.AddItem(item.Label);
        _overrideChoice.Select(0);
        _tweens = _session.RuntimeTweenChoicesUx6W(_draft);
        _tweenChoice.Clear();
        _tweenChoice.AddItem(_tweens.Count == 0 ? "Aucun tween · état vide" : "Choisir un tween…");
        foreach (var item in _tweens) _tweenChoice.AddItem(item.Label);
        _tweenChoice.Select(0);
        _editingOverrideId = null;
        _editingTweenId = null;
        _overrideDirty = false;
        _tweenDirty = false;
    }

    private void SelectOverride(int index)
    {
        if (_updating || _draft is null || index <= 0 || index > _overrides.Count) return;
        var item = _draft.CandidateProfile.TransformOverrides.Single(value => value.Id == _overrides[index - 1].Id);
        _editingOverrideId = item.Id;
        _updating = true;
        SelectEntityChoice(item.EntityId);
        PopulateParentChoices(item.EntityId);
        SelectParent(item.ParentEntityId);
        PopulateTransform(_overrideFrom, item.From);
        PopulateTransform(_overrideTo, item.To);
        _overrideDuration.Text = item.DurationMilliseconds.ToString(CultureInfo.InvariantCulture);
        _overrideEasing.Select((int)item.Easing);
        _overrideAuthority.Select((int)item.Authority);
        _overrideSave.Select((int)item.SaveBehavior);
        _overrideReplicate.ButtonPressed = item.Replicate;
        _updating = false;
        _overrideDirty = false;
    }

    private void SelectTween(int index)
    {
        if (_updating || _draft is null || index <= 0 || index > _tweens.Count) return;
        var item = _draft.CandidateProfile.Tweens.Single(value => value.Id == _tweens[index - 1].Id);
        _editingTweenId = item.Id;
        _updating = true;
        SelectEntityChoice(item.EntityId);
        _channel.Select((int)item.Channel);
        _property.Text = item.Property;
        PopulateTransform(_tweenFrom, item.FromTransform ?? TransformValue.Identity);
        PopulateTransform(_tweenTo, item.ToTransform ?? TransformValue.Identity);
        _fromX.Text = Number(item.FromValue.X); _fromY.Text = Number(item.FromValue.Y);
        _fromZ.Text = Number(item.FromValue.Z); _fromW.Text = Number(item.FromValue.W);
        _toX.Text = Number(item.ToValue.X); _toY.Text = Number(item.ToValue.Y);
        _toZ.Text = Number(item.ToValue.Z); _toW.Text = Number(item.ToValue.W);
        _fromState.Text = item.FromValue.Text; _toState.Text = item.ToValue.Text;
        _tweenDuration.Text = item.DurationMilliseconds.ToString(CultureInfo.InvariantCulture);
        _tweenEasing.Select((int)item.Easing);
        _tweenAuthority.Select((int)item.Authority);
        _tweenSave.Select((int)item.SaveBehavior);
        _tweenReplicate.ButtonPressed = item.Replicate;
        _updating = false;
        _tweenDirty = false;
    }

    private void NewOverride()
    {
        if (!TrySelectedEntity(out var entity))
        {
            SetStatus("Sélectionnez d’abord un objet cible.", error: true);
            return;
        }
        _editingOverrideId = null;
        _overrideChoice.Select(0);
        SetDefaultOverrideForm(entity.Id);
        _overrideDirty = true;
        DraftChanged();
    }

    private void NewTween()
    {
        if (!TrySelectedEntity(out var entity))
        {
            SetStatus("Sélectionnez d’abord un objet cible.", error: true);
            return;
        }
        _editingTweenId = null;
        _tweenChoice.Select(0);
        SetDefaultTweenForm(entity.Id);
        _tweenDirty = true;
        DraftChanged();
    }

    private void RemoveOverride()
    {
        if (_draft is null || _editingOverrideId is null) return;
        _draft = _session.RemoveRuntimeOverrideUx6W(_draft, _editingOverrideId.Value);
        PopulateItems();
        DraftChanged();
    }

    private void RemoveTween()
    {
        if (_draft is null || _editingTweenId is null) return;
        _draft = _session.RemoveRuntimeTweenUx6W(_draft, _editingTweenId.Value);
        PopulateItems();
        DraftChanged();
    }

    private void Preview()
    {
        if (_draft is null || !_explicitAreaSelection || !_explicitEntitySelection)
        {
            SetStatus("Sélectionnez explicitement une zone puis un objet.", error: true);
            return;
        }
        try
        {
            if (_overrideDirty) _draft = _session.UpsertRuntimeOverrideUx6W(_draft, ReadOverrideInput());
            if (_tweenDirty) _draft = _session.UpsertRuntimeTweenUx6W(_draft, ReadTweenInput());
            PopulateItems();
            _preview = _session.PreviewRuntimeTransformsUx6W(_draft);
            RenderPreview();
            SetStatus(_preview.Message, error: !_preview.Succeeded);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           FormatException or OverflowException)
        {
            SetStatus(HumanError(exception.Message), error: true);
        }
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedRuntimeTransformsUx6W();
        SetStatus(result.Message, error: !result.Succeeded);
        if (!result.Succeeded) return;
        CinematicsChanged?.Invoke();
        ReloadAfterMutation();
    }

    private void Undo()
    {
        var result = _session.UndoRuntimeTransformsUx6W();
        SetStatus(result.Message, error: !result.Succeeded);
        if (!result.Succeeded) return;
        CinematicsChanged?.Invoke();
        ReloadAfterMutation();
    }

    private void Redo()
    {
        var result = _session.RedoRuntimeTransformsUx6W();
        SetStatus(result.Message, error: !result.Succeeded);
        if (!result.Succeeded) return;
        CinematicsChanged?.Invoke();
        ReloadAfterMutation();
    }

    private void Save()
    {
        var result = _session.SaveRuntimeTransformsUx6W();
        SetStatus(result.Message, error: !result.Succeeded);
    }

    private void ReloadAfterMutation()
    {
        var areaId = _draft?.CommandAreaId;
        PopulateAreas();
        PopulateEntities();
        var areaIndex = areaId is null ? -1 : _areas.ToList().FindIndex(item => item.Id == areaId);
        if (areaIndex < 0) { ResetControls(); return; }
        _updating = true;
        _areaChoice.Select(areaIndex + 1);
        _updating = false;
        _explicitAreaSelection = true;
        _draft = _session.CreateRuntimeTransformDraftUx6W(true, areaId);
        _explicitEntitySelection = false;
        PopulateItems();
        ClearPreview();
        UpdateButtons();
    }

    private StudioUx6WOverrideInput ReadOverrideInput()
    {
        if (!TrySelectedEntity(out var entity)) throw new InvalidOperationException("Objet cible requis.");
        var parent = _parentChoice.Selected <= 0 ? null : _parentEntities[_parentChoice.Selected - 1].Id;
        return new(_editingOverrideId, entity.Id, parent, ReadTransform(_overrideFrom),
            ReadTransform(_overrideTo), ReadInt(_overrideDuration, "Durée override"),
            (SequenceEasing)_overrideEasing.Selected, (TransformAuthority)_overrideAuthority.Selected,
            (TransformSaveBehavior)_overrideSave.Selected, _overrideReplicate.ButtonPressed);
    }

    private StudioUx6WTweenInput ReadTweenInput()
    {
        if (!TrySelectedEntity(out var entity)) throw new InvalidOperationException("Objet cible requis.");
        var channel = (AnimationChannelKind)_channel.Selected;
        var fromTransform = channel == AnimationChannelKind.Transform ? ReadTransform(_tweenFrom) : null;
        var toTransform = channel == AnimationChannelKind.Transform ? ReadTransform(_tweenTo) : null;
        var from = channel == AnimationChannelKind.State
            ? AnimationTweenValue.State(_fromState.Text)
            : new AnimationTweenValue(ReadFloat(_fromX), ReadFloat(_fromY), ReadFloat(_fromZ), ReadFloat(_fromW),
                string.Empty);
        var to = channel == AnimationChannelKind.State
            ? AnimationTweenValue.State(_toState.Text)
            : new AnimationTweenValue(ReadFloat(_toX), ReadFloat(_toY), ReadFloat(_toZ), ReadFloat(_toW),
                string.Empty);
        return new(_editingTweenId, entity.Id, channel, _property.Text, fromTransform, toTransform, from, to,
            ReadInt(_tweenDuration, "Durée tween"), (SequenceEasing)_tweenEasing.Selected,
            (TransformAuthority)_tweenAuthority.Selected, (TransformSaveBehavior)_tweenSave.Selected,
            _tweenReplicate.ButtonPressed);
    }

    private void SetDefaultOverrideForm(EntityId entityId)
    {
        var transform = ResolveEntityTransform(entityId);
        _updating = true;
        PopulateTransform(_overrideFrom, transform);
        PopulateTransform(_overrideTo, transform);
        _overrideDuration.Text = "1000";
        _overrideEasing.Select((int)SequenceEasing.SmoothStep);
        _overrideAuthority.Select((int)TransformAuthority.Server);
        _overrideSave.Select((int)TransformSaveBehavior.Resume);
        _overrideReplicate.ButtonPressed = true;
        SelectParent(null);
        _updating = false;
    }

    private void SetDefaultTweenForm(EntityId entityId)
    {
        var transform = ResolveEntityTransform(entityId);
        _updating = true;
        _channel.Select((int)AnimationChannelKind.Transform);
        _property.Text = string.Empty;
        PopulateTransform(_tweenFrom, transform);
        PopulateTransform(_tweenTo, transform);
        foreach (var field in new[] { _fromX, _fromY, _fromZ, _fromW, _toX, _toY, _toZ, _toW }) field.Text = "0";
        _fromState.Text = "idle"; _toState.Text = "active";
        _tweenDuration.Text = "1000";
        _tweenEasing.Select((int)SequenceEasing.EaseInOut);
        _tweenAuthority.Select((int)TransformAuthority.Server);
        _tweenSave.Select((int)TransformSaveBehavior.Resume);
        _tweenReplicate.ButtonPressed = true;
        _updating = false;
    }

    private void RenderPreview()
    {
        ClearChildren(_results);
        _results.AddChild(StudioStyle.Label("RÉSULTATS · runtimes déterministes copiés", 9, StudioStyle.Accent));
        if (_preview is null) return;
        foreach (var line in _preview.Results)
            _results.AddChild(StudioStyle.Label(line, 9,
                line.StartsWith("Aucun ", StringComparison.Ordinal) ? StudioStyle.Warning : StudioStyle.Text,
                wrap: true));
        foreach (var diagnostic in _preview.Diagnostics)
            _results.AddChild(StudioStyle.Label($"{diagnostic.Code} · {diagnostic.Message}", 9,
                StudioStyle.Danger, wrap: true));
        _review.Text = _preview.Diff.Count == 0
            ? "Aucune différence persistante · état vide ou profil inchangé."
            : "DIFF HUMAIN · " + string.Join(" · ", _preview.Diff);
        UpdateButtons();
    }

    private void ClearPreview()
    {
        if (_results is null) return;
        ClearChildren(_results);
        _results.AddChild(StudioStyle.Label("RÉSULTATS · aucun aperçu sec canonique", 9, StudioStyle.Accent));
        if (_review is not null) _review.Text = "Aucune modification approuvée.";
    }

    private void DraftChanged()
    {
        if (_updating) return;
        _session.CancelRuntimeTransformApprovalUx6W();
        _preview = null;
        ClearPreview();
        SetStatus("Brouillon modifié · relancez l’aperçu sec canonique.");
        UpdateButtons();
    }

    private void WireFormInvalidation()
    {
        foreach (var field in OverrideFields()) field.TextChanged += _ => { _overrideDirty = true; DraftChanged(); };
        foreach (var choice in new[] { _parentChoice, _overrideEasing, _overrideAuthority, _overrideSave })
            choice.ItemSelected += _ => { _overrideDirty = true; DraftChanged(); };
        _overrideReplicate.Toggled += _ => { _overrideDirty = true; DraftChanged(); };
        foreach (var field in TweenFields()) field.TextChanged += _ => { _tweenDirty = true; DraftChanged(); };
        foreach (var choice in new[] { _channel, _tweenEasing, _tweenAuthority, _tweenSave })
            choice.ItemSelected += _ => { _tweenDirty = true; DraftChanged(); };
        _tweenReplicate.Toggled += _ => { _tweenDirty = true; DraftChanged(); };
    }

    private IEnumerable<LineEdit> OverrideFields() => _overrideFrom.Fields.Concat(_overrideTo.Fields)
        .Append(_overrideDuration);

    private IEnumerable<LineEdit> TweenFields() => _tweenFrom.Fields.Concat(_tweenTo.Fields)
        .Concat([_property, _fromX, _fromY, _fromZ, _fromW, _toX, _toY, _toZ, _toW, _fromState, _toState,
            _tweenDuration]);

    private void PopulateParentChoices(EntityId? target)
    {
        var wasUpdating = _updating;
        _updating = true;
        _parentEntities = _entities.Where(item => item.Id != target).ToArray();
        _parentChoice.Clear();
        _parentChoice.AddItem("Aucun parent");
        foreach (var item in _parentEntities) _parentChoice.AddItem(item.Label);
        _parentChoice.Select(0);
        _updating = wasUpdating;
    }

    private void SelectParent(EntityId? id)
    {
        if (id is null) { _parentChoice.Select(0); return; }
        var index = _parentEntities.ToList().FindIndex(item => item.Id == id);
        _parentChoice.Select(index < 0 ? 0 : index + 1);
    }

    private void SelectEntityChoice(EntityId id)
    {
        var index = _entities.ToList().FindIndex(item => item.Id == id);
        if (index >= 0)
        {
            _entityChoice.Select(index + 1);
            _explicitEntitySelection = true;
        }
    }

    private bool TrySelectedEntity(out StudioUx6WEntityChoice entity)
    {
        var index = _entityChoice.Selected - 1;
        if (index >= 0 && index < _entities.Count)
        {
            entity = _entities[index];
            return true;
        }
        entity = null!;
        return false;
    }

    private TransformValue ResolveEntityTransform(EntityId id) => _session.Project!.Areas
        .SelectMany(item => item.Entities).First(item => item.Id == id).Transform;

    private void ResetControls(bool keepReleaseState = false)
    {
        _updating = true;
        _draft = null; _preview = null;
        _explicitAreaSelection = false; _explicitEntitySelection = false;
        _areas = []; _entities = []; _parentEntities = []; _overrides = []; _tweens = [];
        if (_areaChoice is not null) { _areaChoice.Clear(); _areaChoice.AddItem("Choisir une zone…"); }
        if (_entityChoice is not null) { _entityChoice.Clear(); _entityChoice.AddItem("Choisir un objet…"); }
        if (_overrideChoice is not null) { _overrideChoice.Clear(); _overrideChoice.AddItem("Aucun override"); }
        if (_tweenChoice is not null) { _tweenChoice.Clear(); _tweenChoice.AddItem("Aucun tween"); }
        ClearPreview();
        if (_status is not null) _status.Text = "Sélectionnez explicitement une zone puis un objet.";
        if (!keepReleaseState) _evidenceReleased = true;
        _updating = false;
        UpdateButtons();
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

    private SourceSnapshotUx6W CaptureSource()
    {
        var history = _sourceSession.History!;
        return new(_sourceSession.Project!, history, history.State.Revision, history.Journal.ToArray(),
            _sourceSession.IsDirty, history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureRuntimeTransformApprovalUx6W());
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
               _sourceSession.CaptureRuntimeTransformApprovalUx6W() == snapshot.Approval;
    }

    private void ReleaseEvidence()
    {
        _evidenceSession?.Close();
        _evidenceSession = null;
        var released = string.IsNullOrWhiteSpace(_evidenceRoot) ||
                       StudioSessionController.DeleteTemporaryUx6W(_evidenceRoot);
        _evidenceRoot = null;
        _evidenceReleased = released;
        _session = _sourceSession;
        if (!released) GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_W_UI_CLEANUP_FAILED");
    }

    private static TransformEditorUx6W BuildTransformEditor(Control parent, string title)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(StudioStyle.Label(title, 9, StudioStyle.Muted));
        var position = FieldRow("Position", "X", "Y", "Z", out var px, out var py, out var pz, out _);
        var rotation = FieldRow("Rotation", "X", "Y", "Z", out var rx, out var ry, out var rz, out var rw, true);
        var scale = FieldRow("Échelle", "X", "Y", "Z", out var sx, out var sy, out var sz, out _);
        box.AddChild(position); box.AddChild(rotation); box.AddChild(scale);
        parent.AddChild(box);
        return new(px, py, pz, rx, ry, rz, rw, sx, sy, sz);
    }

    private static HBoxContainer FieldRow(string title, string x, string y, string z, out LineEdit fx,
        out LineEdit fy, out LineEdit fz, out LineEdit fw, bool includeW = false)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(StudioStyle.Label(title, 8, StudioStyle.Muted));
        fx = Field(x, 62); fy = Field(y, 62); fz = Field(z, 62); fw = Field("W", 62);
        row.AddChild(fx); row.AddChild(fy); row.AddChild(fz); if (includeW) row.AddChild(fw);
        return row;
    }

    private static HBoxContainer ValueRow(params LineEdit[] fields)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var field in fields) row.AddChild(field);
        return row;
    }

    private static void PopulateTransform(TransformEditorUx6W editor, TransformValue value)
    {
        editor.Px.Text = Number(value.Position.X); editor.Py.Text = Number(value.Position.Y);
        editor.Pz.Text = Number(value.Position.Z); editor.Rx.Text = Number(value.Rotation.X);
        editor.Ry.Text = Number(value.Rotation.Y); editor.Rz.Text = Number(value.Rotation.Z);
        editor.Rw.Text = Number(value.Rotation.W); editor.Sx.Text = Number(value.Scale.X);
        editor.Sy.Text = Number(value.Scale.Y); editor.Sz.Text = Number(value.Scale.Z);
    }

    private static TransformValue ReadTransform(TransformEditorUx6W editor) => new(
        new Vector3Value(ReadFloat(editor.Px), ReadFloat(editor.Py), ReadFloat(editor.Pz)),
        new QuaternionValue(ReadFloat(editor.Rx), ReadFloat(editor.Ry), ReadFloat(editor.Rz),
            ReadFloat(editor.Rw)),
        new Vector3Value(ReadFloat(editor.Sx), ReadFloat(editor.Sy), ReadFloat(editor.Sz)));

    private static float ReadFloat(LineEdit field)
    {
        var normalized = field.Text.Trim().Replace(',', '.');
        if (!float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
            !float.IsFinite(value)) throw new FormatException("Saisissez des nombres finis.");
        return value;
    }

    private static int ReadInt(LineEdit field, string name)
    {
        if (!int.TryParse(field.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            throw new FormatException($"{name} doit être un entier.");
        return value;
    }

    private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string HumanError(string message) => string.IsNullOrWhiteSpace(message)
        ? "Le brouillon contient une valeur invalide."
        : message.Replace("Animation tween definition is invalid.", "Le tween contient une valeur invalide.",
            StringComparison.Ordinal).Replace("Transform override component is invalid.",
            "L’override contient une valeur invalide.", StringComparison.Ordinal);

    private static OptionButton EnumChoice<T>() where T : struct, Enum
    {
        var choice = Choice(150);
        foreach (var value in Enum.GetValues<T>()) choice.AddItem(HumanEnum(value));
        return choice;
    }

    private static string HumanEnum<T>(T value) where T : struct, Enum => value.ToString() switch
    {
        "Transform" => "Transform",
        "MaterialParameter" => "Paramètre matériau",
        "State" => "État",
        "Linear" => "Linéaire",
        "EaseIn" => "Accélération",
        "EaseOut" => "Décélération",
        "EaseInOut" => "Accélération/décélération",
        "SmoothStep" => "Lissage",
        "Local" => "Locale",
        "Owner" => "Propriétaire",
        "Server" => "Serveur",
        "Transient" => "Transitoire",
        "Resume" => "Reprendre",
        "CommitFinal" => "Conserver l’état final",
        _ => value.ToString(),
    };

    private VBoxContainer TabPage(string title)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var page = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", 7);
        scroll.AddChild(page);
        _tabs.AddChild(scroll);
        return page;
    }

    private static OptionButton Choice(float width) => new()
    {
        CustomMinimumSize = new Vector2(width, 34),
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
    };

    private static LineEdit Field(string placeholder, float width = 120) => new()
    {
        PlaceholderText = placeholder,
        CustomMinimumSize = new Vector2(width, 32),
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
    };

    private static VBoxContainer Column(string label, Control control)
    {
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddChild(StudioStyle.Label(label.ToUpperInvariant(), 8, StudioStyle.Muted));
        column.AddChild(control);
        return column;
    }

    private static Button ActionButton(string text, Action action, float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        return button;
    }

    private static void ClearChildren(Node node)
    {
        foreach (var child in node.GetChildren()) child.QueueFree();
    }

    private static IEnumerable<string> DescendantText(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Label label) yield return label.Text;
            else if (child is Button button) yield return button.Text;
            else if (child is OptionButton choice)
                for (var index = 0; index < choice.ItemCount; index++) yield return choice.GetItemText(index);
            foreach (var nested in DescendantText(child)) yield return nested;
        }
    }

    private static bool ContainsUuid(string value) => Regex.IsMatch(value,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b");

    private sealed record TransformEditorUx6W(LineEdit Px, LineEdit Py, LineEdit Pz, LineEdit Rx, LineEdit Ry,
        LineEdit Rz, LineEdit Rw, LineEdit Sx, LineEdit Sy, LineEdit Sz)
    {
        internal IEnumerable<LineEdit> Fields => [Px, Py, Pz, Rx, Ry, Rz, Rw, Sx, Sy, Sz];
    }

    private sealed record SourceSnapshotUx6W(ProjectDefinition Project, ProjectCommandHistory History,
        ProjectRevision Revision, IReadOnlyList<CommandHistoryJournalEntry> Journal, bool IsDirty, bool CanUndo,
        bool CanRedo, AreaId? AreaId, IReadOnlyList<EntityId> SelectedEntityIds, EntityId? SelectedEntityId,
        StudioUx6WApprovalSnapshot Approval);
}
