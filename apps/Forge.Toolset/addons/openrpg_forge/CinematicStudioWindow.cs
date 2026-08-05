#if TOOLS
using Forge.Application.Projects;
using Forge.Application.Sequences;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Godot;

namespace Forge.Toolset.Editor;

public partial class CinematicStudioWindow : Window
{
    private readonly List<EntityId> _entityIds = [];
    private Func<ProjectState?>? _state;
    private Func<AreaId?>? _active;
    private Func<ModuleCinematicProfile, AreaId, AreaCinematicProfile, bool>? _apply;
    private Action<Window>? _brand;
    private ModuleCinematicProfile? _module;
    private AreaCinematicProfile? _area;
    private CheckBox? _startupEnabled;
    private LineEdit? _sequenceName;
    private SpinBox? _duration;
    private CheckBox? _skippable;
    private CheckBox? _synchronized;
    private LineEdit? _anchorName;
    private SpinBox? _cameraX;
    private SpinBox? _cameraY;
    private SpinBox? _cameraZ;
    private SpinBox? _lookX;
    private SpinBox? _lookY;
    private SpinBox? _lookZ;
    private SpinBox? _fov;
    private SpinBox? _safeArea;
    private OptionButton? _entity;
    private SpinBox? _targetX;
    private SpinBox? _targetY;
    private SpinBox? _targetZ;
    private LineEdit? _audio;
    private LineEdit? _signal;
    private HSlider? _timeline;
    private Label? _preview;
    private Label? _status;
    private SafeAreaPreview? _safeAreaPreview;

    internal string Summary => _module is null || _area is null
        ? "unavailable"
        : $"{_module.Startup.Kind}:{_module.Sequences.Count}:{_area.CameraAnchors.Count}";

    public void Initialize(Func<ProjectState?> state, Func<AreaId?> active,
        Func<ModuleCinematicProfile, AreaId, AreaCinematicProfile, bool> apply, Action<Window> brand)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _active = active ?? throw new ArgumentNullException(nameof(active));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _brand = brand ?? throw new ArgumentNullException(nameof(brand));
        Title = "Séquences & caméras · OpenRPG Forge";
        MinSize = new Vector2I(1160, 760);
        Size = new Vector2I(1220, 800);
        Transient = true;
        Visible = false;
        Build();
    }

    public void Open()
    {
        Refresh();
        PopupCentered(Size);
        _brand?.Invoke(this);
    }

    internal bool ConfigureForSmokeTest(ModuleCinematicProfile module, AreaCinematicProfile area)
    {
        Refresh();
        _module = module;
        _area = area;
        Populate();
        return true;
    }

    internal bool ApplyForSmokeTest() => Save();

    private void Build()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 20);
        AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);
        var heading = new Label { Text = "SÉQUENCES, CAMÉRAS & MOUVEMENTS", ThemeTypeVariation = "HeaderSmall" };
        heading.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(heading);
        root.AddChild(new Label
        {
            Text = "Construisez une introduction jouable, cadrez une caméra et animez une entité sur une timeline. " +
                   "La prévisualisation reste sans mutation jusqu’à l’application.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        var columns = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 14);
        root.AddChild(columns);
        var story = Card(columns, "DÉROULÉ");
        _startupEnabled = new CheckBox { Text = "Jouer cette séquence au lancement", ButtonPressed = true };
        story.AddChild(_startupEnabled);
        _sequenceName = Field("Introduction");
        _duration = Spin(250, 120_000, 6_000, 250);
        _skippable = new CheckBox { Text = "Autoriser Passer (Échap)", ButtonPressed = true };
        _synchronized = new CheckBox { Text = "Synchroniser en multijoueur", ButtonPressed = false };
        story.AddChild(Row("Nom", _sequenceName));
        story.AddChild(Row("Durée (ms)", _duration));
        story.AddChild(_skippable);
        story.AddChild(_synchronized);
        _audio = Field("assets/audio/intro.ogg (facultatif)");
        _signal = Field("intro.started (facultatif)");
        story.AddChild(Row("Audio", _audio));
        story.AddChild(Row("Signal", _signal));
        story.AddChild(new HSeparator());
        story.AddChild(new Label { Text = "MOUVEMENT D’ENTITÉ" });
        _entity = new OptionButton();
        story.AddChild(Row("Cible", _entity));
        _targetX = Spin(-4096, 4096, 0, .25);
        _targetY = Spin(-4096, 4096, 0, .25);
        _targetZ = Spin(-4096, 4096, 0, .25);
        story.AddChild(VectorRow("Arrivée", _targetX, _targetY, _targetZ));

        var camera = Card(columns, "CAMERA ANCHOR");
        _anchorName = Field("Plan d’ouverture");
        camera.AddChild(Row("Nom", _anchorName));
        _cameraX = Spin(-4096, 4096, 6, .25);
        _cameraY = Spin(-4096, 4096, 8, .25);
        _cameraZ = Spin(-4096, 4096, 10, .25);
        camera.AddChild(VectorRow("Position", _cameraX, _cameraY, _cameraZ));
        _lookX = Spin(-4096, 4096, 0, .25);
        _lookY = Spin(-4096, 4096, 1, .25);
        _lookZ = Spin(-4096, 4096, 0, .25);
        camera.AddChild(VectorRow("Regard", _lookX, _lookY, _lookZ));
        _fov = Spin(10, 120, 55, 1);
        _safeArea = Spin(0, .45, .08, .01);
        camera.AddChild(Row("Champ de vision", _fov));
        camera.AddChild(Row("Marge sûre", _safeArea));
        _safeAreaPreview = new SafeAreaPreview { CustomMinimumSize = new Vector2(360, 230) };
        camera.AddChild(_safeAreaPreview);
        _safeArea.ValueChanged += value => { _safeAreaPreview.Margin = (float)value; _safeAreaPreview.QueueRedraw(); };
        camera.AddChild(new Label
        {
            Text = "Le cadre orange représente la zone sûre utilisée pour les dialogues et sous-titres.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color("bcaacb"),
        });

        var timelineCard = new PanelContainer();
        timelineCard.AddThemeStyleboxOverride("panel", Style("100d1b", "513a6d"));
        root.AddChild(timelineCard);
        var timelineBox = new VBoxContainer();
        timelineCard.AddChild(timelineBox);
        var timelineTitle = new HBoxContainer();
        timelineTitle.AddChild(new Label { Text = "TIMELINE · PRÉVISUALISATION" });
        _preview = new Label
        {
            Text = "0 ms",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        timelineTitle.AddChild(_preview);
        timelineBox.AddChild(timelineTitle);
        _timeline = new HSlider { MinValue = 0, MaxValue = 6_000, Step = 50 };
        _timeline.ValueChanged += _ => Preview();
        timelineBox.AddChild(_timeline);

        var footer = new HBoxContainer();
        _status = new Label { Text = "Prêt · aucune mutation.", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _status.AddThemeColorOverride("font_color", new Color("9bd9bd"));
        footer.AddChild(_status);
        var apply = new Button { Text = "Valider et appliquer", CustomMinimumSize = new Vector2(190, 40) };
        apply.Pressed += () => Save();
        footer.AddChild(apply);
        var close = new Button { Text = "Fermer", CustomMinimumSize = new Vector2(110, 40) };
        close.Pressed += Hide;
        footer.AddChild(close);
        root.AddChild(footer);
    }

    private void Refresh()
    {
        var state = _state?.Invoke();
        var activeId = _active?.Invoke();
        var activeArea = activeId is null ? null : state?.Project.Areas.SingleOrDefault(item => item.Id == activeId);
        if (state is null || activeArea is null)
        {
            _status!.Text = "Aucun projet ou aucune zone active.";
            return;
        }

        _module = state.Project.Module.RuntimePolicies.Cinematics;
        _area = activeArea.RuntimePolicies.Cinematics;
        _entityIds.Clear();
        _entity!.Clear();
        _entity.AddItem("Aucun mouvement");
        foreach (var entity in activeArea.Entities.OrderBy(item => item.Id.Value))
        {
            _entityIds.Add(entity.Id);
            _entity.AddItem(entity.Id.ToString()[..8]);
        }

        Populate();
    }

    private void Populate()
    {
        if (_module is null || _area is null) return;
        var sequence = _module.Sequences.FirstOrDefault();
        var anchor = _area.CameraAnchors.FirstOrDefault();
        _startupEnabled!.ButtonPressed = _module.Startup.Kind == StartupFlowKind.Sequence;
        _sequenceName!.Text = sequence?.DisplayName ?? "Introduction";
        _duration!.Value = sequence?.DurationMilliseconds ?? 6_000;
        _timeline!.MaxValue = _duration.Value;
        _skippable!.ButtonPressed = sequence?.Skippable ?? true;
        _synchronized!.ButtonPressed = sequence?.MultiplayerSynchronized ?? false;
        _anchorName!.Text = anchor?.Name ?? "Plan d’ouverture";
        Set(_cameraX!, _cameraY!, _cameraZ!, anchor?.Transform.Position ?? new Vector3Value(6, 8, 10));
        Set(_lookX!, _lookY!, _lookZ!, anchor?.LookAt ?? new Vector3Value(0, 1, 0));
        _fov!.Value = anchor?.FieldOfViewDegrees ?? 55;
        _safeArea!.Value = anchor?.SafeAreaMargin ?? .08;
        _safeAreaPreview!.Margin = (float)_safeArea.Value;
        _audio!.Text = sequence?.Tracks.FirstOrDefault(item => item.Kind == SequenceTrackKind.Audio)?.ResourcePath ?? string.Empty;
        _signal!.Text = sequence?.Tracks.FirstOrDefault(item => item.Kind == SequenceTrackKind.Signal)?
            .Keyframes.FirstOrDefault()?.Value ?? string.Empty;
        var transform = sequence?.Tracks.FirstOrDefault(item => item.Kind == SequenceTrackKind.Transform);
        var entityIndex = transform?.TargetId is null
            ? -1
            : _entityIds.FindIndex(item => item.Value == transform.TargetId.Value);
        _entity!.Selected = entityIndex + 1;
        var target = transform?.Keyframes.LastOrDefault()?.Transform?.Position ?? Vector3Value.Zero;
        Set(_targetX!, _targetY!, _targetZ!, target);
        _status!.Text = $"Prêt · {_module.Sequences.Count} séquence(s) · {_area.CameraAnchors.Count} caméra(s).";
        Preview();
    }

    private bool Save()
    {
        var activeId = _active?.Invoke();
        if (_module is null || _area is null || activeId is null) return false;
        try
        {
            var duration = (int)_duration!.Value;
            var anchorId = _area.CameraAnchors.FirstOrDefault()?.Id ?? Guid.NewGuid();
            var anchor = new CameraAnchorDefinition(anchorId, NonEmpty(_anchorName!.Text, "Plan d’ouverture"),
                new TransformValue(Vector(_cameraX!, _cameraY!, _cameraZ!), QuaternionValue.Identity,
                    Vector3Value.One), Vector(_lookX!, _lookY!, _lookZ!), CameraFraming.Wide, (float)_fov!.Value,
                duration, (float)_safeArea!.Value, ["cinematic", "opening"], "forge-ui");
            var sequenceId = _module.Sequences.FirstOrDefault()?.Id ?? Guid.NewGuid();
            var tracks = new List<SequenceTrackDefinition>
            {
                new(_module.Sequences.FirstOrDefault()?.Tracks.FirstOrDefault(item => item.Kind == SequenceTrackKind.Camera)?.Id ?? Guid.NewGuid(),
                    "Caméra", SequenceTrackKind.Camera, SequenceTargetKind.CameraAnchor, anchorId, string.Empty,
                    [new SequenceKeyframe(Guid.NewGuid(), 0, null, string.Empty, SequenceEasing.Linear)], "forge-ui"),
            };
            var overrides = new List<TransformOverrideComponent>();
            var tweens = new List<AnimationTweenDefinition>();
            if (_entity!.Selected > 0 && _entity.Selected - 1 < _entityIds.Count)
            {
                var state = _state!.Invoke()!;
                var entityId = _entityIds[_entity.Selected - 1];
                var source = state.Project.Areas.Single(item => item.Id == activeId).Entities
                    .Single(item => item.Id == entityId).Transform;
                var destination = new TransformValue(Vector(_targetX!, _targetY!, _targetZ!), source.Rotation,
                    source.Scale);
                tracks.Add(new SequenceTrackDefinition(Guid.NewGuid(), "Mouvement", SequenceTrackKind.Transform,
                    SequenceTargetKind.Entity, entityId.Value, string.Empty,
                    [new SequenceKeyframe(Guid.NewGuid(), 0, source, string.Empty, SequenceEasing.Linear),
                     new SequenceKeyframe(Guid.NewGuid(), duration, destination, string.Empty,
                         SequenceEasing.SmoothStep)], "forge-ui"));
                overrides.Add(new TransformOverrideComponent(Guid.NewGuid(), 1, entityId, null, source,
                    destination, duration, SequenceEasing.SmoothStep, TransformAuthority.Server,
                    TransformSaveBehavior.CommitFinal, true, "forge-ui"));
                tweens.Add(new AnimationTweenDefinition(Guid.NewGuid(), 1, entityId,
                    AnimationChannelKind.Transform, string.Empty, source, destination, AnimationTweenValue.Number(0),
                    AnimationTweenValue.Number(1), duration, SequenceEasing.SmoothStep, TransformAuthority.Server,
                    TransformSaveBehavior.CommitFinal, true, "forge-ui"));
            }

            if (!string.IsNullOrWhiteSpace(_audio!.Text))
                tracks.Add(new SequenceTrackDefinition(Guid.NewGuid(), "Audio", SequenceTrackKind.Audio,
                    SequenceTargetKind.AudioResource, null, _audio.Text.Trim(),
                    [new SequenceKeyframe(Guid.NewGuid(), 1, null, string.Empty, SequenceEasing.Linear)], "forge-ui"));
            if (!string.IsNullOrWhiteSpace(_signal!.Text))
                tracks.Add(new SequenceTrackDefinition(Guid.NewGuid(), "Signal", SequenceTrackKind.Signal,
                    SequenceTargetKind.Blackboard, null, string.Empty,
                    [new SequenceKeyframe(Guid.NewGuid(), duration / 2, null, _signal.Text.Trim(),
                        SequenceEasing.Linear)], "forge-ui"));
            var sequence = new SequenceDefinition(sequenceId, 1, Slug(_sequenceName!.Text),
                NonEmpty(_sequenceName.Text, "Introduction"), duration, _skippable!.ButtonPressed,
                _synchronized!.ButtonPressed, tracks, [], "forge-ui");
            var startup = new StartupFlowDefinition(_module.Startup.Id, 1,
                _startupEnabled!.ButtonPressed ? StartupFlowKind.Sequence : StartupFlowKind.None,
                _startupEnabled.ButtonPressed ? sequenceId : null, string.Empty, StartupFlowKind.None, null,
                string.Empty, _skippable.ButtonPressed, "Passer", "forge-ui");
            var module = new ModuleCinematicProfile(_module.Id, 1, startup, [sequence], "forge-ui", overrides,
                tweens);
            var area = new AreaCinematicProfile(_area.Id, 1, [anchor], "forge-ui");
            var succeeded = _apply?.Invoke(module, activeId, area) == true;
            _status!.Text = succeeded
                ? "✓ Séquence validée, sauvegardable et annulable."
                : "✕ Application refusée · consultez les diagnostics.";
            if (succeeded) { _module = module; _area = area; Preview(); }
            return succeeded;
        }
        catch (ArgumentException exception)
        {
            _status!.Text = $"✕ {exception.Message}";
            return false;
        }
    }

    private void Preview()
    {
        _timeline!.MaxValue = _duration!.Value;
        if (_module?.Sequences.FirstOrDefault() is not { } sequence)
        {
            _preview!.Text = $"{(int)_timeline.Value} ms · aperçu caméra";
            return;
        }
        var evaluation = SequencePlayer.Advance(sequence, SequencePlaybackState.Start(sequence.Id),
            (int)Math.Min(_timeline.Value, sequence.DurationMilliseconds), true, _ => false);
        _preview!.Text = $"{evaluation.State.TimeMilliseconds} ms · caméra {(evaluation.CameraAnchorId is null ? "libre" : "ancrée")} · " +
                         $"{evaluation.Transforms.Count} mouvement(s) · {evaluation.Signals.Count} signal(aux)";
    }

    private static VBoxContainer Card(Container parent, string title)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", Style("171221", "513a6d"));
        parent.AddChild(panel);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 9);
        panel.AddChild(box);
        var heading = new Label { Text = title };
        heading.AddThemeColorOverride("font_color", new Color("ffb06f"));
        box.AddChild(heading);
        return box;
    }

    private static StyleBoxFlat Style(string background, string border)
    {
        var style = new StyleBoxFlat { BgColor = new Color(background), BorderColor = new Color(border) };
        style.SetBorderWidthAll(1); style.SetCornerRadiusAll(10);
        style.ContentMarginLeft = style.ContentMarginRight = 16;
        style.ContentMarginTop = style.ContentMarginBottom = 14;
        return style;
    }

    private static HBoxContainer Row(string label, Control control)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(125, 0) });
        control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(control);
        return row;
    }

    private static HBoxContainer VectorRow(string label, SpinBox x, SpinBox y, SpinBox z)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(125, 0) });
        foreach (var item in new[] { x, y, z }) { item.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(item); }
        return row;
    }

    private static LineEdit Field(string placeholder) => new() { PlaceholderText = placeholder };
    private static SpinBox Spin(double min, double max, double value, double step) => new()
    { MinValue = min, MaxValue = max, Value = value, Step = step, AllowGreater = false, AllowLesser = false };
    private static Vector3Value Vector(SpinBox x, SpinBox y, SpinBox z) =>
        new((float)x.Value, (float)y.Value, (float)z.Value);
    private static void Set(SpinBox x, SpinBox y, SpinBox z, Vector3Value value)
    { x.Value = value.X; y.Value = value.Y; z.Value = value.Z; }
    private static string NonEmpty(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    private static string Slug(string value)
    {
        var normalized = new string(NonEmpty(value, "introduction").ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray());
        return string.Join('-', normalized.Split('-', StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed partial class SafeAreaPreview : Control
    {
        internal float Margin { get; set; } = .08f;
        public override void _Draw()
        {
            DrawRect(new Rect2(Vector2.Zero, Size), new Color("090711"), true);
            var inset = new Vector2(Size.X * Margin, Size.Y * Margin);
            DrawRect(new Rect2(inset, Size - (inset * 2)), new Color("ff9858"), false, 2);
            DrawLine(new Vector2(Size.X / 2, 0), new Vector2(Size.X / 2, Size.Y), new Color("5d496e"), 1);
            DrawLine(new Vector2(0, Size.Y / 2), new Vector2(Size.X, Size.Y / 2), new Color("5d496e"), 1);
        }
    }
}
#endif
