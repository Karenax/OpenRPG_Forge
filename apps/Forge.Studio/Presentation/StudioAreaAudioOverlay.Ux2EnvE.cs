using Forge.Domain.Areas;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioUx2EnvEAdaptiveLayerRow :
    StudioUx2EnvBRowBase<StudioUx2EnvEAdaptiveLayerDraft>
{
    private readonly LineEdit _resource;
    private readonly SpinBox _start;
    private readonly SpinBox _end;
    private readonly LineEdit _weather;
    private readonly LineEdit _location;
    private readonly SpinBox _danger;
    private readonly CheckBox _indoors;
    private readonly SpinBox _volume;
    private readonly SpinBox _crossfade;
    private readonly LineEdit _volumeName;

    public override StudioUx2EnvEAdaptiveLayerDraft Value => new(
        _resource.Text, _start.Value, _end.Value, _weather.Text, _location.Text,
        _danger.Value, _indoors.ButtonPressed, _volume.Value, _crossfade.Value, _volumeName.Text);

    internal StudioUx2EnvEAdaptiveLayerRow(StudioUx2EnvEAdaptiveLayerDraft value) : base("Couche d’ambiance")
    {
        _resource = StudioUx2EnvBControls.Text("Ressource intégrée ou gouvernée");
        _start = Number(0, 24, .25, value.StartHour);
        _end = Number(0, 24, .25, value.EndHour);
        _weather = StudioUx2EnvBControls.Text("Météo optionnelle");
        _location = StudioUx2EnvBControls.Text("Tag de lieu optionnel");
        _danger = Number(0, 1, .01, value.MinimumDanger);
        _indoors = StudioUx2EnvBControls.Check("Intérieur uniquement");
        _volume = Number(0, 4, .01, value.Volume);
        _crossfade = Number(0, 120, .05, value.CrossfadeSeconds);
        _volumeName = StudioUx2EnvBControls.Text("Volume nommé optionnel");
        _resource.Text = value.Resource; _weather.Text = value.Weather; _location.Text = value.LocationTag;
        _indoors.ButtonPressed = value.IndoorOnly; _volumeName.Text = value.VolumeId;
        var grid = new GridContainer { Columns = 5, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddChild(StudioUx2EnvBControls.Field("Ressource", _resource, 250));
        grid.AddChild(StudioUx2EnvBControls.Field("Début (h)", _start));
        grid.AddChild(StudioUx2EnvBControls.Field("Fin (h)", _end));
        grid.AddChild(StudioUx2EnvBControls.Field("Météo", _weather, 150));
        grid.AddChild(StudioUx2EnvBControls.Field("Lieu", _location, 150));
        grid.AddChild(StudioUx2EnvBControls.Field("Danger minimal", _danger));
        grid.AddChild(StudioUx2EnvBControls.Field("Volume", _volume));
        grid.AddChild(StudioUx2EnvBControls.Field("Fondu (s)", _crossfade));
        grid.AddChild(StudioUx2EnvBControls.Field("Volume de zone", _volumeName, 180));
        grid.AddChild(_indoors); Root.AddChild(grid);
        Watch(_resource); Watch(_start); Watch(_end); Watch(_weather); Watch(_location); Watch(_danger);
        Watch(_indoors); Watch(_volume); Watch(_crossfade); Watch(_volumeName);
    }

    public override void SetEditingEnabled(bool enabled)
    {
        base.SetEditingEnabled(enabled);
        foreach (var line in new[] { _resource, _weather, _location, _volumeName }) line.Editable = enabled;
        foreach (var number in new[] { _start, _end, _danger, _volume, _crossfade }) number.Editable = enabled;
        _indoors.Disabled = !enabled;
    }

    private static SpinBox Number(double min, double max, double step, double value)
    {
        var result = StudioUx2EnvBControls.Number(min, max, step, allowGreater: true);
        result.Value = value; return result;
    }
}

internal sealed partial class StudioUx2EnvEMusicStateRow :
    StudioUx2EnvBRowBase<StudioUx2EnvEMusicStateDraft>
{
    private readonly OptionButton _state;
    private readonly LineEdit _track;
    private readonly TextEdit _stems;
    private readonly SpinBox _transition;
    private readonly SpinBox _cooldown;
    private readonly CheckBox _beatSync;
    private readonly SpinBox _tempo;
    private readonly SpinBox _beats;

    public override StudioUx2EnvEMusicStateDraft Value => new(
        (MusicState)_state.Selected, _track.Text, Lines(_stems.Text), _transition.Value, _cooldown.Value,
        _beatSync.ButtonPressed, _tempo.Value, _beats.Value);

    internal StudioUx2EnvEMusicStateRow(StudioUx2EnvEMusicStateDraft value) : base("État musical")
    {
        _state = StudioUx2EnvBControls.Options("Exploration", "Tension", "Combat", "Affrontement majeur", "Victoire");
        _track = StudioUx2EnvBControls.Text("Piste intégrée ou gouvernée");
        _stems = StudioUx2EnvBControls.Lines("Une couche musicale par ligne", 64);
        _transition = Number(0, 600, .05, value.TransitionSeconds);
        _cooldown = Number(0, 86_400, .25, value.CooldownSeconds);
        _beatSync = StudioUx2EnvBControls.Check("Synchroniser à la mesure");
        _tempo = Number(20, 400, .1, value.TempoBpm);
        _beats = Number(1, 32, 1, value.BeatsPerBar);
        _state.Select((int)value.State); _track.Text = value.Track;
        _stems.Text = string.Join('\n', value.Stems); _beatSync.ButtonPressed = value.BeatSynchronized;
        var grid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddChild(StudioUx2EnvBControls.Field("État", _state, 190));
        grid.AddChild(StudioUx2EnvBControls.Field("Piste principale", _track, 300));
        grid.AddChild(StudioUx2EnvBControls.Field("Fondu (s)", _transition));
        grid.AddChild(StudioUx2EnvBControls.Field("Délai minimal (s)", _cooldown));
        grid.AddChild(StudioUx2EnvBControls.Field("Couches musicales", _stems, 340));
        grid.AddChild(StudioUx2EnvBControls.Field("Tempo (BPM)", _tempo));
        grid.AddChild(StudioUx2EnvBControls.Field("Temps par mesure", _beats));
        grid.AddChild(_beatSync); Root.AddChild(grid);
        Watch(_state); Watch(_track); Watch(_stems); Watch(_transition); Watch(_cooldown);
        Watch(_beatSync); Watch(_tempo); Watch(_beats);
    }

    public override void SetEditingEnabled(bool enabled)
    {
        base.SetEditingEnabled(enabled); _state.Disabled = !enabled; _track.Editable = enabled;
        _stems.Editable = enabled; _transition.Editable = enabled; _cooldown.Editable = enabled;
        _beatSync.Disabled = !enabled; _tempo.Editable = enabled; _beats.Editable = enabled;
    }

    private static IReadOnlyList<string> Lines(string text) => text.Replace("\r", string.Empty).Split('\n')
        .Select(item => item.Trim()).Where(item => item.Length > 0).ToArray();

    private static SpinBox Number(double min, double max, double step, double value)
    {
        var result = StudioUx2EnvBControls.Number(min, max, step, allowGreater: true);
        result.Value = value; return result;
    }
}

internal sealed partial class StudioUx2EnvEMixBusRow : StudioUx2EnvBRowBase<StudioUx2EnvEMixBusDraft>
{
    private readonly Guid _id;
    private readonly IReadOnlyList<Guid?> _parentIds;
    private readonly LineEdit _name;
    private readonly OptionButton _parent;
    private readonly SpinBox _volume;
    private readonly LineEdit _snapshot;
    private readonly LineEdit _platform;
    private readonly SpinBox _range;
    private readonly CheckBox _enhance;
    private readonly CheckBox _monitor;

    public override StudioUx2EnvEMixBusDraft Value => new(
        _id, _name.Text, _parent.Selected >= 0 && _parent.Selected < _parentIds.Count
            ? _parentIds[(int)_parent.Selected]
            : null, _volume.Value, _snapshot.Text, _platform.Text, _range.Value,
        _enhance.ButtonPressed, _monitor.ButtonPressed);

    internal StudioUx2EnvEMixBusRow(
        StudioUx2EnvEMixBusDraft value,
        IReadOnlyList<(Guid Id, string Name)> buses) : base("Bus de mixage")
    {
        _id = value.Id; _name = StudioUx2EnvBControls.Text("Nom humain du bus");
        _parent = StudioUx2EnvBControls.Options();
        var available = new List<(Guid? Id, string Label)> { (null, "Aucun parent") };
        available.AddRange(buses.Where(item => item.Id != value.Id).Select(item => ((Guid?)item.Id, item.Name)));
        _parentIds = available.Select(item => item.Id).ToArray();
        foreach (var item in available) _parent.AddItem(item.Label);
        var parentIndex = available.FindIndex(item => item.Id == value.ParentId);
        _parent.Select(Math.Max(0, parentIndex));
        _volume = Number(0, 4, .01, value.Volume); _snapshot = StudioUx2EnvBControls.Text("default");
        _platform = StudioUx2EnvBControls.Text("all"); _range = Number(0, 1, .01, value.DynamicRange);
        _enhance = StudioUx2EnvBControls.Check("Renforcer les sons importants");
        _monitor = StudioUx2EnvBControls.Check("Inclure dans le plan de mesure");
        _name.Text = value.Name; _snapshot.Text = value.Snapshot; _platform.Text = value.Platform;
        _enhance.ButtonPressed = value.EnhanceImportantSounds; _monitor.ButtonPressed = value.MonitorEnabled;
        var grid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddChild(StudioUx2EnvBControls.Field("Nom", _name, 200));
        grid.AddChild(StudioUx2EnvBControls.Field("Parent", _parent, 200));
        grid.AddChild(StudioUx2EnvBControls.Field("Volume", _volume));
        grid.AddChild(StudioUx2EnvBControls.Field("Plage dynamique", _range));
        grid.AddChild(StudioUx2EnvBControls.Field("Snapshot runtime", _snapshot, 180));
        grid.AddChild(StudioUx2EnvBControls.Field("Plateforme", _platform, 150));
        grid.AddChild(_enhance); grid.AddChild(_monitor); Root.AddChild(grid);
        Watch(_name); Watch(_parent); Watch(_volume); Watch(_snapshot); Watch(_platform); Watch(_range);
        Watch(_enhance); Watch(_monitor);
    }

    public override void SetEditingEnabled(bool enabled)
    {
        base.SetEditingEnabled(enabled); _name.Editable = enabled; _parent.Disabled = !enabled;
        _volume.Editable = enabled; _snapshot.Editable = enabled; _platform.Editable = enabled;
        _range.Editable = enabled; _enhance.Disabled = !enabled; _monitor.Disabled = !enabled;
    }

    private static SpinBox Number(double min, double max, double step, double value)
    {
        var result = StudioUx2EnvBControls.Number(min, max, step); result.Value = value; return result;
    }
}

internal sealed partial class StudioUx2EnvESnapshotRow : StudioUx2EnvBRowBase<StudioUx2EnvESnapshotDraft>
{
    private readonly Guid _id;
    private readonly LineEdit _name;
    private readonly SpinBox _transition;
    private readonly LineEdit _trigger;
    private readonly LineEdit _provenance;
    private readonly IReadOnlyList<(string Name, CheckBox Included, SpinBox Volume)> _levels;

    public override StudioUx2EnvESnapshotDraft Value => new(
        _id, _name.Text,
        _levels.Where(item => item.Included.ButtonPressed)
            .Select(item => new StudioUx2EnvESnapshotBusDraft(item.Name, item.Volume.Value)).ToArray(),
        _transition.Value, _trigger.Text, _provenance.Text);

    internal StudioUx2EnvESnapshotRow(
        StudioUx2EnvESnapshotDraft value,
        IReadOnlyList<string> busNames) : base("Snapshot audio")
    {
        _id = value.Id; _name = StudioUx2EnvBControls.Text("Nom humain du snapshot");
        _transition = StudioUx2EnvBControls.Number(0, 60, .05); _transition.Value = value.TransitionSeconds;
        _trigger = StudioUx2EnvBControls.Text("Événement optionnel");
        _provenance = StudioUx2EnvBControls.Text("Origine du réglage");
        _name.Text = value.Name; _trigger.Text = value.TriggerEvent; _provenance.Text = value.Provenance;
        var heading = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioUx2EnvBControls.Field("Nom", _name, 220));
        heading.AddChild(StudioUx2EnvBControls.Field("Transition (s)", _transition));
        heading.AddChild(StudioUx2EnvBControls.Field("Événement", _trigger, 210));
        heading.AddChild(StudioUx2EnvBControls.Field("Provenance", _provenance, 210)); Root.AddChild(heading);
        var byName = value.BusVolumes.ToDictionary(item => item.BusName, StringComparer.Ordinal);
        var levels = new List<(string Name, CheckBox Included, SpinBox Volume)>();
        var busGrid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var busName in busNames)
        {
            var included = StudioUx2EnvBControls.Check(busName);
            var volume = StudioUx2EnvBControls.Number(0, 4, .01);
            included.ButtonPressed = byName.TryGetValue(busName, out var level); volume.Value = level?.Volume ?? 1;
            busGrid.AddChild(included); busGrid.AddChild(StudioUx2EnvBControls.Field("Niveau", volume));
            busGrid.AddChild(new Control()); Watch(included); Watch(volume);
            levels.Add((busName, included, volume));
        }
        if (levels.Count == 0)
            busGrid.AddChild(StudioStyle.Label("Créez d’abord un bus dans l’étape 1/2.", 10, StudioStyle.Warning));
        _levels = levels; Root.AddChild(busGrid);
        Watch(_name); Watch(_transition); Watch(_trigger); Watch(_provenance);
    }

    public override void SetEditingEnabled(bool enabled)
    {
        base.SetEditingEnabled(enabled); _name.Editable = enabled; _transition.Editable = enabled;
        _trigger.Editable = enabled; _provenance.Editable = enabled;
        foreach (var level in _levels) { level.Included.Disabled = !enabled; level.Volume.Editable = enabled; }
    }
}

internal sealed partial class StudioAreaAudioOverlayUx2EnvE : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx2EnvEEvidenceSourceSnapshot? _evidenceSourceSnapshot;
    private string? _evidenceTempRoot;
    private bool _evidenceUsesRealModuleData;
    private bool _evidenceUsesCanonicalFallback;
    private bool _evidenceReleaseSucceeded = true;
    private readonly PanelContainer _dialog;
    private readonly OptionButton _area;
    private readonly Label _selection;
    private readonly TabContainer _tabs;
    private readonly LineEdit _day;
    private readonly LineEdit _night;
    private readonly LineEdit _combat;
    private readonly LineEdit _ambience;
    private readonly SpinBox _transition;
    private readonly TextEdit _playlist;
    private readonly OptionButton _order;
    private readonly SpinBox _interval;
    private readonly SpinBox _variation;
    private readonly SpinBox _relativeVolume;
    private readonly StudioUx2EnvBCollectionEditor<StudioUx2EnvEAdaptiveLayerDraft> _layers;
    private readonly StudioUx2EnvBCollectionEditor<StudioUx2EnvEMusicStateDraft> _music;
    private readonly StudioUx2EnvBCollectionEditor<StudioUx2EnvEMixBusDraft> _buses;
    private readonly StudioUx2EnvBCollectionEditor<StudioUx2EnvESnapshotDraft> _snapshots;
    private IReadOnlyList<string> _snapshotBusNames = [];
    private readonly VBoxContainer _audioDiff;
    private readonly VBoxContainer _snapshotDiff;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _audioSummary;
    private readonly Label _snapshotSummary;
    private readonly Label _status;
    private readonly Button _previewAudio;
    private readonly Button _applyAudio;
    private readonly Button _previewSnapshots;
    private readonly Button _applySnapshots;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;
    private IReadOnlyList<StudioUx2EnvEAreaChoice> _choices = [];
    private StudioUx2EnvEAudioDraft? _audioDraft;
    private StudioUx2EnvESnapshotsDraft? _snapshotDraft;
    private StudioUx2EnvEAudioPreview? _lastAudioPreview;
    private StudioUx2EnvESnapshotPreview? _lastSnapshotPreview;
    private bool _updating;
    private bool _audioStepReviewed;
    private int _appliedSteps;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? AreaAudioChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _choices.Count;
    internal bool HasExplicitSelection => _audioDraft is not null && _snapshotDraft is not null && _area.Selected > 0;
    internal bool AudioPreviewReady => _lastAudioPreview is
    {
        Succeeded: true, CanonicalDryRun: true, AdaptiveCrossfadePlanBuilt: true,
        MusicTransitionPlanBuilt: true, MixPlanBuilt: true, ListenerMixPlanBuilt: true,
        PlaylistPlanBuilt: true,
        PlayerInstantiationSkipped: true, RuntimeClaimsBounded: true,
    };
    internal bool SnapshotPreviewReady => _lastSnapshotPreview is
    {
        Succeeded: true, CanonicalDryRun: true, BusReferencesResolved: true,
        SeparateTransactionDeclared: true,
    };
    internal bool ShowsCompleteCollections => _audioDraft is not null && _snapshotDraft is not null &&
                                               _layers.Values.Count == _audioDraft.AdaptiveLayers.Count &&
                                               _music.Values.Count == _audioDraft.MusicStates.Count &&
                                               _buses.Values.Count == _audioDraft.MixBuses.Count &&
                                               _snapshots.Values.Count == _snapshotDraft.Snapshots.Count;
    internal bool ShowsStructuredDiagnostics => _lastAudioPreview?.Groups.Count == 5 &&
                                                _lastSnapshotPreview?.Groups.Count == 5;
    internal bool ShowsTwoTransactions
    {
        get
        {
            var text = string.Join(' ', DescendantVisibleTextUx2EnvE(this));
            return text.Contains("Étape audio 1/2", StringComparison.Ordinal) &&
                   text.Contains("Étape snapshots 2/2", StringComparison.Ordinal) &&
                   text.Contains("deux commandes", StringComparison.OrdinalIgnoreCase);
        }
    }
    internal bool ShowsHumanSurface
    {
        get
        {
            var tabTitles = Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle);
            var text = string.Join(' ', DescendantVisibleTextUx2EnvE(this).Concat(tabTitles));
            return text.Contains("Audio de zone", StringComparison.Ordinal) &&
                   text.Contains("Ambiances adaptatives", StringComparison.Ordinal) &&
                   text.Contains("États musicaux", StringComparison.Ordinal) &&
                   text.Contains("Bus de mixage", StringComparison.Ordinal) &&
                   text.Contains("Snapshots", StringComparison.Ordinal) &&
                   text.Contains("Aucun AudioStreamPlayer", StringComparison.Ordinal) && !ContainsUuidUx2EnvE(text);
        }
    }
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealModuleSource => UsesIsolatedEvidence && _evidenceUsesRealModuleData;
    internal bool UsesCanonicalFallbackEvidence => UsesIsolatedEvidence && _evidenceUsesCanonicalFallback;
    internal bool EvidenceSourceUntouched => SourceSnapshotMatchesUx2EnvE();
    internal bool EvidenceResourcesReleased => _evidenceReleaseSucceeded &&
                                               _evidenceSession is null && _evidenceTempRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);

    internal StudioAreaAudioOverlayUx2EnvE(StudioSessionController session)
    {
        _sourceSession = session; _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.012f, .018f, .026f, .97f); MouseFilter = MouseFilterEnum.Stop; Visible = false;
        _dialog = StudioStyle.Panel("Ux2EnvEAreaAudioDialog", StudioStyle.Surface, 12, 1); AddChild(_dialog);
        var margin = StudioStyle.Margin(14, 12, 14, 12); _dialog.AddChild(margin);
        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 7); margin.AddChild(root);
        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        headingText.AddChild(StudioStyle.Label("Audio de zone, musique et mixage", 18));
        headingText.AddChild(StudioStyle.Label(
            "Couches adaptatives, états musicaux, bus et snapshots · deux commandes canoniques séparées.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true); close.Pressed += Close; heading.AddChild(close);
        root.AddChild(heading);
        _area = StudioUx2EnvBControls.Options(); _area.ItemSelected += SelectAreaUx2EnvE;
        root.AddChild(StudioUx2EnvBControls.Field("Zone à modifier · sélection explicite", _area, 440));
        _selection = StudioStyle.Label("Aucune zone sélectionnée.", 10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);
        _tabs = new TabContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(_tabs);

        _day = StudioUx2EnvBControls.Text("Musique de jour");
        _night = StudioUx2EnvBControls.Text("Musique de nuit");
        _combat = StudioUx2EnvBControls.Text("Musique de combat");
        _ambience = StudioUx2EnvBControls.Text("Ambiance globale");
        _transition = NumberUx2EnvE(0, 600, .05, greater: true);
        _playlist = StudioUx2EnvBControls.Lines("Une piste par ligne", 92);
        _order = StudioUx2EnvBControls.Options("Séquentiel", "Aléatoire déterministe", "Sac mélangé", "Pondéré");
        _interval = NumberUx2EnvE(0, 86_400, .25, greater: true);
        _variation = NumberUx2EnvE(0, 86_400, .25, greater: true);
        _relativeVolume = NumberUx2EnvE(0, 4, .01);
        BuildZoneTabUx2EnvE();

        _layers = new StudioUx2EnvBCollectionEditor<StudioUx2EnvEAdaptiveLayerDraft>(
            "Ajouter une couche d’ambiance", value => new StudioUx2EnvEAdaptiveLayerRow(value),
            () => new StudioUx2EnvEAdaptiveLayerDraft("builtin://openrpg/ambience/new", 6, 18,
                string.Empty, string.Empty, 0, false, 1, 1, string.Empty));
        BuildLayersTabUx2EnvE();

        _music = new StudioUx2EnvBCollectionEditor<StudioUx2EnvEMusicStateDraft>(
            "Ajouter un état musical", value => new StudioUx2EnvEMusicStateRow(value),
            () => new StudioUx2EnvEMusicStateDraft(MusicState.Exploration,
                "builtin://openrpg/music/new", [], 1, 0, false, 120, 4));
        BuildMusicTabUx2EnvE();

        _buses = new StudioUx2EnvBCollectionEditor<StudioUx2EnvEMixBusDraft>(
            "Ajouter un bus de mixage", value => new StudioUx2EnvEMixBusRow(value,
                (_audioDraft?.MixBuses ?? []).Select(item => (item.Id, item.Name)).ToArray()),
            () => new StudioUx2EnvEMixBusDraft(Guid.NewGuid(), "Nouveau bus", null, 1,
                "default", "all", .8, false, true));
        BuildBusesTabUx2EnvE();

        _snapshots = new StudioUx2EnvBCollectionEditor<StudioUx2EnvESnapshotDraft>(
            "Ajouter un snapshot", value => new StudioUx2EnvESnapshotRow(value, _snapshotBusNames),
            () => new StudioUx2EnvESnapshotDraft(Guid.NewGuid(), "Nouveau snapshot",
                _snapshotBusNames.Select(name => new StudioUx2EnvESnapshotBusDraft(name, 1)).ToArray(),
                1, string.Empty, "studio:audio"));
        BuildSnapshotsTabUx2EnvE();

        _audioDiff = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _snapshotDiff = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _diagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        BuildReviewTabUx2EnvE();

        _audioSummary = StudioStyle.Label("Étape audio 1/2 · aperçu sec requis.", 9, StudioStyle.Muted, wrap: true);
        _snapshotSummary = StudioStyle.Label("Étape snapshots 2/2 · aperçu sec séparé requis.",
            9, StudioStyle.Muted, wrap: true);
        root.AddChild(_audioSummary); root.AddChild(_snapshotSummary);
        var audioActions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        audioActions.AddThemeConstantOverride("separation", 6); root.AddChild(audioActions);
        audioActions.AddChild(StudioStyle.Label("Étape audio 1/2", 10, StudioStyle.Accent));
        _previewAudio = ActionButtonUx2EnvE("Prévisualiser audio");
        _applyAudio = ActionButtonUx2EnvE("Appliquer audio");
        audioActions.AddChild(_previewAudio); audioActions.AddChild(_applyAudio);
        var audioNote = StudioStyle.Label("set_area_environment · première commande, annulable seule",
            9, StudioStyle.Muted, wrap: true);
        audioNote.SizeFlagsHorizontal = SizeFlags.ExpandFill; audioActions.AddChild(audioNote);
        var snapshotActions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        snapshotActions.AddThemeConstantOverride("separation", 6); root.AddChild(snapshotActions);
        snapshotActions.AddChild(StudioStyle.Label("Étape snapshots 2/2", 10, StudioStyle.Accent));
        _previewSnapshots = ActionButtonUx2EnvE("Prévisualiser snapshots");
        _applySnapshots = ActionButtonUx2EnvE("Appliquer snapshots");
        snapshotActions.AddChild(_previewSnapshots); snapshotActions.AddChild(_applySnapshots);
        var snapshotNote = StudioStyle.Label("set_area_authoring · seconde commande, aperçu frais après l’audio",
            9, StudioStyle.Muted, wrap: true);
        snapshotNote.SizeFlagsHorizontal = SizeFlags.ExpandFill; snapshotActions.AddChild(snapshotNote);
        var historyActions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        historyActions.AddThemeConstantOverride("separation", 6); root.AddChild(historyActions);
        historyActions.AddChild(StudioStyle.Label("Historique des deux commandes", 10, StudioStyle.Accent));
        _undo = ActionButtonUx2EnvE("Annuler une étape"); _redo = ActionButtonUx2EnvE("Rétablir une étape");
        _save = ActionButtonUx2EnvE("Enregistrer");
        historyActions.AddChild(_undo); historyActions.AddChild(_redo); historyActions.AddChild(_save);
        var bounded = StudioStyle.Label(
            "Aucun AudioStreamPlayer, bus Godot, périphérique ou playback live n’est instancié : plans et niveaux simulés uniquement.",
            9, StudioStyle.Warning, wrap: true);
        bounded.CustomMinimumSize = new Vector2(0, 30); root.AddChild(bounded);
        _status = StudioStyle.Label("Prêt · sélection explicite requise", 9, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        _previewAudio.Pressed += PreviewAudioUx2EnvE; _applyAudio.Pressed += ApplyAudioUx2EnvE;
        _previewSnapshots.Pressed += PreviewSnapshotsUx2EnvE; _applySnapshots.Pressed += ApplySnapshotsUx2EnvE;
        _undo.Pressed += UndoUx2EnvE; _redo.Pressed += RedoUx2EnvE; _save.Pressed += SaveUx2EnvE;
        ConnectInvalidationUx2EnvE(); SetEditingEnabledUx2EnvE(false); ResetControlsUx2EnvE();
    }

    private void BuildZoneTabUx2EnvE()
    {
        var tab = ScrollTabUx2EnvE("Audio de zone");
        var resources = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        resources.AddChild(StudioUx2EnvBControls.Field("Jour", _day, 240));
        resources.AddChild(StudioUx2EnvBControls.Field("Nuit", _night, 240));
        resources.AddChild(StudioUx2EnvBControls.Field("Combat", _combat, 240));
        resources.AddChild(StudioUx2EnvBControls.Field("Ambiance", _ambience, 240));
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvEZoneResources", "Ressources de la zone",
            "Les ressources intégrées sont acceptées ; les ressources de module doivent déjà avoir une provenance gouvernée.",
            resources));
        var schedule = new GridContainer { Columns = 5, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        schedule.AddChild(StudioUx2EnvBControls.Field("Transition (s)", _transition));
        schedule.AddChild(StudioUx2EnvBControls.Field("Ordre", _order, 190));
        schedule.AddChild(StudioUx2EnvBControls.Field("Intervalle (s)", _interval));
        schedule.AddChild(StudioUx2EnvBControls.Field("Variation (s)", _variation));
        schedule.AddChild(StudioUx2EnvBControls.Field("Volume relatif", _relativeVolume));
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddChild(schedule); content.AddChild(StudioUx2EnvBControls.Field("Playlist · une piste par ligne", _playlist));
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvEZoneSchedule", "Playlist et cadence",
            "La liste complète est conservée et son ordre reste auteur.", content));
    }

    private void BuildLayersTabUx2EnvE()
    {
        var tab = ScrollTabUx2EnvE("Ambiances adaptatives");
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvELayers", "Couches selon heure, météo et lieu",
            "Le plan compare deux contextes déterministes ; il ne démarre aucun son.", _layers));
    }

    private void BuildMusicTabUx2EnvE()
    {
        var tab = ScrollTabUx2EnvE("États musicaux");
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvEMusic", "Musique adaptative",
            "Transitions, délais, couches et synchronisation à la mesure sont persistés.", _music));
    }

    private void BuildBusesTabUx2EnvE()
    {
        var tab = ScrollTabUx2EnvE("Bus de mixage");
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvEBuses", "Hiérarchie et plans de mesure",
            "Les parents sont choisis par nom humain. Les niveaux sont simulés, jamais lus depuis Godot.", _buses));
    }

    private void BuildSnapshotsTabUx2EnvE()
    {
        var tab = ScrollTabUx2EnvE("Snapshots");
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvESnapshots", "Snapshots de mixage",
            "Cette collection relève de l’étape 2/2 et de set_area_authoring, distincte du profil audio.",
            _snapshots));
    }

    private void BuildReviewTabUx2EnvE()
    {
        var tab = ScrollTabUx2EnvE("Revue");
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvEAudioDiff", "Diff audio · étape 1/2",
            "Zone, ambiances, musique et bus sont regroupés sans identifiant interne.", _audioDiff));
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvESnapshotDiff", "Diff snapshots · étape 2/2",
            "La seconde commande reste visible et annulable indépendamment.", _snapshotDiff));
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvEDiagnostics", "Validation canonique",
            "Les cinq groupes restent lisibles même quand ils sont conformes.", _diagnostics));
    }

    private VBoxContainer ScrollTabUx2EnvE(string title)
    {
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(1080, 0),
        };
        content.AddThemeConstantOverride("separation", 8); scroll.AddChild(content); _tabs.AddChild(scroll);
        _tabs.SetTabTitle(_tabs.GetTabCount() - 1, title); return content;
    }

    internal void Open()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null) ReleaseIsolatedEvidenceSessionUx2EnvE();
        _session = _sourceSession; Visible = true; UpdateDialogBoundsUx2EnvE(); RefreshChoicesUx2EnvE();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceTempRoot is null) return;
        if (Visible || !ReferenceEquals(_session, _sourceSession)) _session.CancelAudioApprovalsUx2EnvE();
        Visible = false; _audioDraft = null; _snapshotDraft = null;
        _lastAudioPreview = null; _lastSnapshotPreview = null; _audioStepReviewed = false; _appliedSteps = 0;
        ReleaseIsolatedEvidenceSessionUx2EnvE(); _session = _sourceSession;
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        var approval = _sourceSession.CaptureAudioApprovalUx2EnvE();
        _session = _sourceSession; Visible = true; UpdateDialogBoundsUx2EnvE(); RefreshChoicesUx2EnvE();
        _sourceSession.RestoreAudioApprovalUx2EnvE(approval);
    }

    internal bool PrepareEvidence(string mode)
    {
        var sourceApproval = _sourceSession.CaptureAudioApprovalUx2EnvE();
        Close(); _sourceSession.RestoreAudioApprovalUx2EnvE(sourceApproval);
        _evidenceReleaseSucceeded = true;
        var sourceProject = _sourceSession.Project;
        if (sourceProject is null || _sourceSession.History is null || sourceProject.Areas.Count == 0) return false;
        _evidenceSourceSnapshot = CaptureSourceSnapshotUx2EnvE();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux2-env-e-{Guid.NewGuid():N}");
        var evidenceSession = new StudioSessionController();
        try
        {
            var realArea = sourceProject.Areas.FirstOrDefault(AreaHasRichAudioUx2EnvE);
            _evidenceUsesRealModuleData = realArea is not null; _evidenceUsesCanonicalFallback = realArea is null;
            var evidenceProject = realArea is null
                ? StudioSessionController.CreateUx2EnvEContractProject(sourceProject)
                : sourceProject;
            var evidenceAreaId = realArea?.Id ?? evidenceProject.Areas[0].Id;
            var created = ProjectSession.Create(tempRoot, evidenceProject);
            if (!created.Succeeded || !evidenceSession.Open(tempRoot).Succeeded)
            {
                evidenceSession.Close();
                var released = StudioSessionController.DeleteUx2EnvETemporaryWorkspace(tempRoot);
                ResetEvidenceStateUx2EnvE(); _evidenceReleaseSucceeded = released;
                if (!released) _evidenceTempRoot = tempRoot;
                return false;
            }
            _evidenceSession = evidenceSession; _evidenceTempRoot = tempRoot; _session = evidenceSession;
            Visible = true; UpdateDialogBoundsUx2EnvE(); RefreshChoicesUx2EnvE();
            var choiceIndex = _choices.ToList().FindIndex(choice => choice.AreaId == evidenceAreaId);
            if (choiceIndex < 0) { Close(); return false; }
            _area.Select(choiceIndex + 1); LoadChoiceUx2EnvE(choiceIndex);
            if (!ConfigureEvidenceDraftsUx2EnvE()) { Close(); return false; }
            PreviewAudioUx2EnvE(); PreviewSnapshotsUx2EnvE();
            var ready = AudioPreviewReady && SnapshotPreviewReady && HasExplicitSelection &&
                        ShowsCompleteCollections && ShowsStructuredDiagnostics && ShowsTwoTransactions &&
                        ShowsHumanSurface && UsesIsolatedEvidence &&
                        (UsesRealModuleSource ^ UsesCanonicalFallbackEvidence) && EvidenceSourceUntouched;
            if (!ready) { Close(); return false; }
            return true;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_E_EVIDENCE_FAILED mode={mode} " +
                        $"type={exception.GetType().Name} message={exception.Message}");
            var sessionReleased = true;
            try { evidenceSession.Close(); }
            catch (Exception releaseException)
            {
                sessionReleased = false;
                GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_E_EVIDENCE_RELEASE_FAILED " +
                            $"type={releaseException.GetType().Name} message={releaseException.Message}");
            }
            var released = StudioSessionController.DeleteUx2EnvETemporaryWorkspace(tempRoot);
            ResetEvidenceStateUx2EnvE(); _evidenceReleaseSucceeded = sessionReleased && released;
            if (!released) _evidenceTempRoot = tempRoot;
            _session = _sourceSession; Visible = false; return false;
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        var sourceSnapshot = CaptureSourceSnapshotUx2EnvE();
        if (!PrepareEvidence("ux2-env-e-area-audio") || !AudioPreviewReady || !SnapshotPreviewReady ||
            !UsesIsolatedEvidence)
        {
            Close(); return false;
        }
        var isolated = _session;
        if (_lastAudioPreview is not { Succeeded: true, CanApply: true } ||
            _lastSnapshotPreview is not { Succeeded: true, CanApply: true })
        {
            Close(); return false;
        }
        var isolatedProjectBefore = isolated.Project;
        ApplyAudioUx2EnvE();
        var firstApplied = _appliedSteps == 1 && isolated.Project is not null &&
                           isolated.History?.Journal.LastOrDefault()?.CommandName == "set_area_environment";
        var projectAfterAudio = isolated.Project;
        PreviewSnapshotsUx2EnvE();
        ApplySnapshotsUx2EnvE();
        var secondApplied = _appliedSteps == 2 && isolated.Project is not null &&
                            isolated.History?.Journal.LastOrDefault()?.CommandName == "set_area_authoring";
        var projectAfterBoth = isolated.Project;
        var finalJson = secondApplied ? ProjectJsonSerializer.Serialize(projectAfterBoth!) : string.Empty;
        UndoUx2EnvE();
        var snapshotUndone = _appliedSteps == 1 && ReferenceEquals(projectAfterAudio, isolated.Project);
        UndoUx2EnvE();
        var audioUndone = _appliedSteps == 0 && ReferenceEquals(isolatedProjectBefore, isolated.Project);
        RedoUx2EnvE();
        var audioRedone = _appliedSteps == 1 && ReferenceEquals(projectAfterAudio, isolated.Project);
        RedoUx2EnvE();
        var snapshotRedone = _appliedSteps == 2 && ReferenceEquals(projectAfterBoth, isolated.Project);
        SaveUx2EnvE();
        var reopen = new StudioSessionController();
        var roundTrip = false;
        try
        {
            var opened = _evidenceTempRoot is not null && reopen.Open(_evidenceTempRoot).Succeeded;
            roundTrip = opened && reopen.Project is not null &&
                        string.Equals(finalJson, ProjectJsonSerializer.Serialize(reopen.Project!),
                            StringComparison.Ordinal);
        }
        finally { reopen.Close(); }
        Close();
        return firstApplied && secondApplied && snapshotUndone && audioUndone && audioRedone && snapshotRedone &&
               roundTrip && SourceSnapshotMatchesUx2EnvE(sourceSnapshot) && EvidenceResourcesReleased &&
               SourceSessionRestored;
    }

    private void RefreshChoicesUx2EnvE()
    {
        _updating = true; _choices = _session.AudioChoicesUx2EnvE;
        _area.Clear(); _area.AddItem("Choisir explicitement une zone…");
        foreach (var choice in _choices) _area.AddItem(choice.Label);
        _area.Select(0); _updating = false; ClearSelectionUx2EnvE();
    }

    private void SelectAreaUx2EnvE(long index)
    {
        if (_updating) return;
        if (index <= 0 || index > _choices.Count) { ClearSelectionUx2EnvE(); return; }
        LoadChoiceUx2EnvE((int)index - 1);
    }

    private void LoadChoiceUx2EnvE(int choiceIndex)
    {
        var choice = _choices[choiceIndex]; _session.CancelAudioApprovalsUx2EnvE();
        _audioDraft = _session.CreateAudioDraftUx2EnvE(choice.AreaId);
        _snapshotDraft = _session.CreateSnapshotsDraftUx2EnvE(choice.AreaId);
        _lastAudioPreview = null; _lastSnapshotPreview = null; _audioStepReviewed = false; _appliedSteps = 0;
        if (_audioDraft is null || _snapshotDraft is null)
        {
            ResetControlsUx2EnvE();
            _selection.Text = "La zone choisie a changé. Rechargez la page Environnement.";
            _selection.AddThemeColorOverride("font_color", StudioStyle.Danger);
            SetEditingEnabledUx2EnvE(false); UpdateButtonsUx2EnvE();
            SetStatusUx2EnvE("Zone indisponible · aucune mutation", true); return;
        }
        PopulateAudioUx2EnvE(_audioDraft); PopulateSnapshotsUx2EnvE(_snapshotDraft);
        _selection.Text = $"{choice.Label} · collections complètes chargées";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Accent);
        _audioSummary.Text = "Étape audio 1/2 prête · environnement B/C/D et tails voisins seront préservés.";
        _snapshotSummary.Text = "Étape snapshots 2/2 prête · auteur, visibilité, LOD et voisins seront préservés.";
        SetEditingEnabledUx2EnvE(true); UpdateButtonsUx2EnvE();
        SetStatusUx2EnvE("Brouillons prêts · deux aperçus secs requis", false);
    }

    private void ClearSelectionUx2EnvE()
    {
        _session.CancelAudioApprovalsUx2EnvE(); _audioDraft = null; _snapshotDraft = null;
        _lastAudioPreview = null; _lastSnapshotPreview = null; _audioStepReviewed = false; _appliedSteps = 0;
        ResetControlsUx2EnvE(); SetEditingEnabledUx2EnvE(false); UpdateButtonsUx2EnvE();
        _selection.Text = "Aucune zone sélectionnée · aucune mutation possible.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
    }

    private void PopulateAudioUx2EnvE(StudioUx2EnvEAudioDraft draft)
    {
        _updating = true; _day.Text = draft.DayMusic; _night.Text = draft.NightMusic;
        _combat.Text = draft.CombatMusic; _ambience.Text = draft.Ambience;
        _transition.Value = draft.TransitionSeconds; _playlist.Text = string.Join('\n', draft.Playlist);
        _order.Select((int)draft.OrderMode); _interval.Value = draft.IntervalSeconds;
        _variation.Value = draft.IntervalVariationSeconds; _relativeVolume.Value = draft.RelativeVolume;
        _layers.SetValues(draft.AdaptiveLayers); _music.SetValues(draft.MusicStates); _buses.SetValues(draft.MixBuses);
        _updating = false; RenderAudioDiffUx2EnvE(null); RenderDiagnosticsUx2EnvE([], []);
    }

    private void PopulateSnapshotsUx2EnvE(StudioUx2EnvESnapshotsDraft draft)
    {
        _updating = true;
        _snapshotBusNames = draft.AvailableBusNames
            .Union(draft.Snapshots.SelectMany(item => item.BusVolumes.Select(level => level.BusName)),
                StringComparer.Ordinal)
            .Order(StringComparer.CurrentCultureIgnoreCase).ToArray();
        _snapshots.SetValues(draft.Snapshots); _updating = false; RenderSnapshotDiffUx2EnvE(null);
    }

    private StudioUx2EnvEAudioDraft? ReadAudioDraftUx2EnvE()
    {
        if (_audioDraft is null) return null;
        return _audioDraft with
        {
            DayMusic = _day.Text,
            NightMusic = _night.Text,
            CombatMusic = _combat.Text,
            Ambience = _ambience.Text,
            TransitionSeconds = _transition.Value,
            Playlist = ReadLinesUx2EnvE(_playlist.Text),
            OrderMode = (AudioOrderMode)_order.Selected,
            IntervalSeconds = _interval.Value,
            IntervalVariationSeconds = _variation.Value,
            RelativeVolume = _relativeVolume.Value,
            AdaptiveLayers = _layers.Values,
            MusicStates = _music.Values,
            MixBuses = _buses.Values,
        };
    }

    private StudioUx2EnvESnapshotsDraft? ReadSnapshotsDraftUx2EnvE() => _snapshotDraft is null
        ? null
        : _snapshotDraft with { Snapshots = _snapshots.Values };

    private bool ConfigureEvidenceDraftsUx2EnvE()
    {
        var audio = ReadAudioDraftUx2EnvE(); var snapshots = ReadSnapshotsDraftUx2EnvE();
        if (audio is null || snapshots is null || audio.AdaptiveLayers.Count == 0 ||
            audio.MusicStates.Count == 0 || audio.MixBuses.Count == 0 || snapshots.Snapshots.Count == 0)
            return false;
        var configuredAudio = audio with
        {
            RelativeVolume = audio.RelativeVolume <= 3.9375
                ? audio.RelativeVolume + .0625
                : audio.RelativeVolume - .0625,
            AdaptiveLayers = audio.AdaptiveLayers.Select((item, index) => index == 0
                ? item with { Volume = item.Volume <= 3.95 ? item.Volume + .05 : item.Volume - .05 }
                : item).ToArray(),
            MusicStates = audio.MusicStates.Select((item, index) => index == 0
                ? item with { TransitionSeconds = item.TransitionSeconds + .125 }
                : item).ToArray(),
            MixBuses = audio.MixBuses.Select((item, index) => index == 0
                ? item with { Volume = item.Volume <= 3.95 ? item.Volume + .05 : item.Volume - .05 }
                : item).ToArray(),
        };
        var configuredSnapshots = snapshots with
        {
            Snapshots = snapshots.Snapshots.Select((item, index) => index == 0
                ? item with
                {
                    TransitionSeconds = item.TransitionSeconds <= 59.875
                        ? item.TransitionSeconds + .125
                        : item.TransitionSeconds - .125,
                }
                : item).ToArray(),
        };
        _audioDraft = configuredAudio; _snapshotDraft = configuredSnapshots;
        PopulateAudioUx2EnvE(configuredAudio); PopulateSnapshotsUx2EnvE(configuredSnapshots);
        InvalidateAudioUx2EnvE(); InvalidateSnapshotsUx2EnvE(); return true;
    }

    private void PreviewAudioUx2EnvE()
    {
        var draft = ReadAudioDraftUx2EnvE(); if (draft is null) return;
        var result = _session.PreviewAudioUx2EnvE(draft); _lastAudioPreview = result;
        _audioStepReviewed = result.Succeeded && !result.Diff.HasChanges;
        RenderAudioDiffUx2EnvE(result); RenderDiagnosticsUx2EnvE(result.Groups, _lastSnapshotPreview?.Groups ?? []);
        _audioSummary.Text = string.IsNullOrWhiteSpace(result.Summary)
            ? result.Message
            : result.Message + "\n" + result.Summary;
        _audioSummary.AddThemeColorOverride("font_color", result.Succeeded ? StudioStyle.Success : StudioStyle.Danger);
        SetStatusUx2EnvE(result.Message, !result.Succeeded); UpdateButtonsUx2EnvE();
    }

    private void PreviewSnapshotsUx2EnvE()
    {
        var draft = ReadSnapshotsDraftUx2EnvE(); if (draft is null) return;
        var result = _session.PreviewSnapshotsUx2EnvE(draft); _lastSnapshotPreview = result;
        RenderSnapshotDiffUx2EnvE(result); RenderDiagnosticsUx2EnvE(_lastAudioPreview?.Groups ?? [], result.Groups);
        _snapshotSummary.Text = string.IsNullOrWhiteSpace(result.Summary)
            ? result.Message
            : result.Message + "\n" + result.Summary;
        _snapshotSummary.AddThemeColorOverride("font_color", result.Succeeded ? StudioStyle.Success : StudioStyle.Danger);
        SetStatusUx2EnvE(result.Message, !result.Succeeded); UpdateButtonsUx2EnvE();
    }

    private void ApplyAudioUx2EnvE()
    {
        var pendingSnapshots = ReadSnapshotsDraftUx2EnvE()?.Snapshots.ToArray() ?? [];
        var result = _session.ApplyApprovedAudioUx2EnvE(); SetStatusUx2EnvE(result.Message, !result.Succeeded);
        if (!result.Succeeded) { InvalidateAudioUx2EnvE(); UpdateButtonsUx2EnvE(); return; }
        _appliedSteps = 1; _audioStepReviewed = true; _lastAudioPreview = null; _lastSnapshotPreview = null;
        _audioDraft = _session.CreateAudioDraftUx2EnvE(_audioDraft?.AreaId);
        if (_audioDraft is not null) PopulateAudioUx2EnvE(_audioDraft);
        RebaseSnapshotsAfterAudioUx2EnvE(pendingSnapshots);
        _audioSummary.Text = "Étape 1/2 appliquée · profil audio enregistré. Les snapshots n’ont pas changé.";
        _audioSummary.AddThemeColorOverride("font_color", StudioStyle.Success);
        _snapshotSummary.Text = "Étape 2/2 invalidée par la nouvelle révision · relancez son aperçu sec.";
        _snapshotSummary.AddThemeColorOverride("font_color", StudioStyle.Warning);
        AreaAudioChanged?.Invoke(); UpdateButtonsUx2EnvE();
    }

    private void ApplySnapshotsUx2EnvE()
    {
        if (_appliedSteps == 0 && !_audioStepReviewed)
        {
            SetStatusUx2EnvE("Validez d’abord l’étape audio 1/2, même lorsqu’elle ne contient aucun changement.", true);
            return;
        }
        var result = _session.ApplyApprovedSnapshotsUx2EnvE(); SetStatusUx2EnvE(result.Message, !result.Succeeded);
        if (!result.Succeeded) { InvalidateSnapshotsUx2EnvE(); UpdateButtonsUx2EnvE(); return; }
        _appliedSteps = Math.Min(2, _appliedSteps + 1); _lastSnapshotPreview = null;
        _snapshotDraft = _session.CreateSnapshotsDraftUx2EnvE(_snapshotDraft?.AreaId);
        if (_snapshotDraft is not null) PopulateSnapshotsUx2EnvE(_snapshotDraft);
        _snapshotSummary.Text = "Étape 2/2 appliquée · seconde commande enregistrée et annulable séparément.";
        _snapshotSummary.AddThemeColorOverride("font_color", StudioStyle.Success);
        AreaAudioChanged?.Invoke(); UpdateButtonsUx2EnvE();
    }

    private void RebaseSnapshotsAfterAudioUx2EnvE(IReadOnlyList<StudioUx2EnvESnapshotDraft> pending)
    {
        var fresh = _session.CreateSnapshotsDraftUx2EnvE(_audioDraft?.AreaId);
        if (fresh is null) { _snapshotDraft = null; _snapshots.SetValues([]); return; }
        var pendingById = pending.ToDictionary(item => item.Id);
        var rebased = fresh.Snapshots.Select(item => pendingById.GetValueOrDefault(item.Id, item)).ToList();
        rebased.AddRange(pending.Where(item => fresh.Snapshots.All(existing => existing.Id != item.Id)));
        _snapshotDraft = fresh with { Snapshots = rebased };
        PopulateSnapshotsUx2EnvE(_snapshotDraft); _session.CancelSnapshotsApprovalUx2EnvE();
    }

    private void UndoUx2EnvE()
    {
        var result = _session.Undo(); SetStatusUx2EnvE(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        _appliedSteps = Math.Max(0, _appliedSteps - 1); _audioStepReviewed = false;
        _session.CancelAudioApprovalsUx2EnvE();
        _lastAudioPreview = null; _lastSnapshotPreview = null;
        _audioSummary.Text = _appliedSteps == 0
            ? "Étape audio 1/2 annulée · le profil précédent est restauré."
            : "Étape audio 1/2 reste appliquée.";
        _snapshotSummary.Text = "Une commande a été annulée · les aperçus doivent être relancés.";
        AreaAudioChanged?.Invoke(); UpdateButtonsUx2EnvE();
    }

    private void RedoUx2EnvE()
    {
        var result = _session.Redo(); SetStatusUx2EnvE(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        _appliedSteps = Math.Min(2, _appliedSteps + 1); _audioStepReviewed = _appliedSteps > 0;
        _session.CancelAudioApprovalsUx2EnvE();
        _lastAudioPreview = null; _lastSnapshotPreview = null;
        _audioSummary.Text = "Une commande a été rétablie · historique à deux étapes conservé.";
        _snapshotSummary.Text = _appliedSteps == 2
            ? "Étape snapshots 2/2 rétablie."
            : "Étape snapshots 2/2 reste annulée.";
        AreaAudioChanged?.Invoke(); UpdateButtonsUx2EnvE();
    }

    private void SaveUx2EnvE()
    {
        var result = _session.Save(); SetStatusUx2EnvE(result.Message, !result.Succeeded); UpdateButtonsUx2EnvE();
    }

    private void ConnectInvalidationUx2EnvE()
    {
        _day.TextChanged += _ => InvalidateAudioUx2EnvE(); _night.TextChanged += _ => InvalidateAudioUx2EnvE();
        _combat.TextChanged += _ => InvalidateAudioUx2EnvE(); _ambience.TextChanged += _ => InvalidateAudioUx2EnvE();
        _transition.ValueChanged += _ => InvalidateAudioUx2EnvE();
        _playlist.TextChanged += () => InvalidateAudioUx2EnvE(); _order.ItemSelected += _ => InvalidateAudioUx2EnvE();
        _interval.ValueChanged += _ => InvalidateAudioUx2EnvE();
        _variation.ValueChanged += _ => InvalidateAudioUx2EnvE();
        _relativeVolume.ValueChanged += _ => InvalidateAudioUx2EnvE();
        _layers.Changed += InvalidateAudioUx2EnvE; _music.Changed += InvalidateAudioUx2EnvE;
        _buses.Changed += InvalidateAudioUx2EnvE; _snapshots.Changed += InvalidateSnapshotsUx2EnvE;
    }

    private void InvalidateAudioUx2EnvE()
    {
        if (_updating) return;
        _session.CancelAudioApprovalsUx2EnvE(); _audioStepReviewed = false;
        _lastAudioPreview = null; _lastSnapshotPreview = null;
        _audioSummary.Text = "Étape audio 1/2 modifiée · nouvel aperçu sec requis.";
        _snapshotSummary.Text = "Le brouillon audio a changé · l’aperçu snapshots sera à refaire après application.";
        RenderAudioDiffUx2EnvE(null); RenderSnapshotDiffUx2EnvE(null); RenderDiagnosticsUx2EnvE([], []);
        UpdateButtonsUx2EnvE();
    }

    private void InvalidateSnapshotsUx2EnvE()
    {
        if (_updating) return;
        _session.CancelSnapshotsApprovalUx2EnvE(); _lastSnapshotPreview = null;
        _snapshotSummary.Text = "Étape snapshots 2/2 modifiée · nouvel aperçu sec requis.";
        RenderSnapshotDiffUx2EnvE(null); RenderDiagnosticsUx2EnvE(_lastAudioPreview?.Groups ?? [], []);
        UpdateButtonsUx2EnvE();
    }

    private void SetEditingEnabledUx2EnvE(bool enabled)
    {
        foreach (var line in new[] { _day, _night, _combat, _ambience }) line.Editable = enabled;
        foreach (var number in new[] { _transition, _interval, _variation, _relativeVolume }) number.Editable = enabled;
        _playlist.Editable = enabled; _order.Disabled = !enabled;
        _layers.SetEditingEnabled(enabled); _music.SetEditingEnabled(enabled);
        _buses.SetEditingEnabled(enabled); _snapshots.SetEditingEnabled(enabled);
        _previewAudio.Disabled = !enabled; _previewSnapshots.Disabled = !enabled;
    }

    private void UpdateButtonsUx2EnvE()
    {
        var selected = _audioDraft is not null && _snapshotDraft is not null;
        _previewAudio.Disabled = !selected || _appliedSteps > 0;
        _previewSnapshots.Disabled = !selected || _appliedSteps == 0 && _lastAudioPreview is not { Succeeded: true };
        _applyAudio.Disabled = _appliedSteps > 0 ||
                               _lastAudioPreview is not { Succeeded: true, CanApply: true };
        _applySnapshots.Disabled = _appliedSteps == 0 && !_audioStepReviewed ||
                                   _lastSnapshotPreview is not { Succeeded: true, CanApply: true };
        var history = _session.History;
        _undo.Disabled = history?.CanUndo != true; _redo.Disabled = history?.CanRedo != true;
        _save.Disabled = !_session.IsDirty;
    }

    private void ResetControlsUx2EnvE()
    {
        _updating = true; _day.Text = string.Empty; _night.Text = string.Empty; _combat.Text = string.Empty;
        _ambience.Text = string.Empty; _transition.Value = 0; _playlist.Text = string.Empty; _order.Select(0);
        _interval.Value = 0; _variation.Value = 0; _relativeVolume.Value = 1;
        _layers.SetValues([]); _music.SetValues([]); _buses.SetValues([]);
        _snapshotBusNames = []; _snapshots.SetValues([]); _updating = false;
        _audioStepReviewed = false;
        RenderAudioDiffUx2EnvE(null); RenderSnapshotDiffUx2EnvE(null); RenderDiagnosticsUx2EnvE([], []);
        _audioSummary.Text = "Étape audio 1/2 · aperçu sec requis.";
        _snapshotSummary.Text = "Étape snapshots 2/2 · aperçu sec séparé requis.";
    }

    private void RenderAudioDiffUx2EnvE(StudioUx2EnvEAudioPreview? preview)
    {
        ClearChildrenUx2EnvE(_audioDiff);
        if (preview is null)
        {
            _audioDiff.AddChild(StudioStyle.Label("Aucun aperçu audio frais.", 10, StudioStyle.Muted)); return;
        }
        if (!preview.Diff.HasChanges)
        {
            _audioDiff.AddChild(StudioStyle.Label("Aucun changement audio sémantique.", 10, StudioStyle.Muted));
            return;
        }
        AddDiffSectionUx2EnvE(_audioDiff, "Zone", preview.Diff.Zone);
        AddDiffSectionUx2EnvE(_audioDiff, "Ambiances adaptatives", preview.Diff.Ambience);
        AddDiffSectionUx2EnvE(_audioDiff, "États musicaux", preview.Diff.Music);
        AddDiffSectionUx2EnvE(_audioDiff, "Bus de mixage", preview.Diff.Buses);
    }

    private void RenderSnapshotDiffUx2EnvE(StudioUx2EnvESnapshotPreview? preview)
    {
        ClearChildrenUx2EnvE(_snapshotDiff);
        if (preview is null)
        {
            _snapshotDiff.AddChild(StudioStyle.Label("Aucun aperçu snapshots frais.", 10, StudioStyle.Muted)); return;
        }
        if (!preview.Diff.HasChanges)
        {
            _snapshotDiff.AddChild(StudioStyle.Label("Aucun changement de snapshot sémantique.",
                10, StudioStyle.Muted)); return;
        }
        AddDiffSectionUx2EnvE(_snapshotDiff, "Snapshots", preview.Diff.Entries);
    }

    private static void AddDiffSectionUx2EnvE(VBoxContainer root, string title, IReadOnlyList<string> entries)
    {
        if (entries.Count == 0) return;
        root.AddChild(StudioStyle.Label(title, 10, StudioStyle.Accent));
        foreach (var entry in entries)
            root.AddChild(StudioStyle.Label("• " + entry, 10, StudioStyle.Text, wrap: true));
    }

    private void RenderDiagnosticsUx2EnvE(
        IReadOnlyList<StudioUx2EnvEDiagnosticGroup> audio,
        IReadOnlyList<StudioUx2EnvEDiagnosticGroup> snapshots)
    {
        ClearChildrenUx2EnvE(_diagnostics);
        var groups = MergeGroupsUx2EnvE(audio, snapshots);
        if (groups.Count == 0)
        {
            _diagnostics.AddChild(StudioStyle.Label("Les cinq groupes apparaîtront après les aperçus secs.",
                10, StudioStyle.Muted, wrap: true)); return;
        }
        foreach (var group in groups)
        {
            var color = group.Diagnostics.Any(item => item.Blocking)
                ? StudioStyle.Danger
                : group.Diagnostics.Count > 0 ? StudioStyle.Warning : StudioStyle.Success;
            _diagnostics.AddChild(StudioStyle.Label($"{group.Title} · {group.State}", 10, color));
            foreach (var diagnostic in group.Diagnostics)
                _diagnostics.AddChild(StudioStyle.Label("• " + diagnostic.Message, 10,
                    diagnostic.Blocking ? StudioStyle.Danger : StudioStyle.Warning, wrap: true));
        }
    }

    private static IReadOnlyList<StudioUx2EnvEDiagnosticGroup> MergeGroupsUx2EnvE(
        IReadOnlyList<StudioUx2EnvEDiagnosticGroup> audio,
        IReadOnlyList<StudioUx2EnvEDiagnosticGroup> snapshots)
    {
        if (audio.Count == 0) return snapshots;
        if (snapshots.Count == 0) return audio;
        return audio.Select(group =>
        {
            var counterpart = snapshots.FirstOrDefault(item => item.Key == group.Key);
            if (counterpart is null) return group;
            var diagnostics = group.Diagnostics.Concat(counterpart.Diagnostics)
                .DistinctBy(item => (item.Code, item.Path, item.Message)).ToArray();
            return group with
            {
                State = diagnostics.Any(item => item.Blocking) ? "à corriger" :
                    diagnostics.Length > 0 ? "avertissement" : "conforme",
                Diagnostics = diagnostics,
            };
        }).ToArray();
    }

    private void SetStatusUx2EnvE(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(message, error);
    }

    private StudioUx2EnvEEvidenceSourceSnapshot CaptureSourceSnapshotUx2EnvE()
    {
        var history = _sourceSession.History ?? throw new InvalidOperationException("No source history.");
        var project = _sourceSession.Project ?? throw new InvalidOperationException("No source project.");
        return new StudioUx2EnvEEvidenceSourceSnapshot(
            project, history, history.State.Revision, history.Journal.ToArray(), _sourceSession.IsDirty,
            history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(),
            _sourceSession.SelectedEntityId, _sourceSession.CaptureAudioApprovalUx2EnvE());
    }

    private bool SourceSnapshotMatchesUx2EnvE(StudioUx2EnvEEvidenceSourceSnapshot? snapshot = null)
    {
        snapshot ??= _evidenceSourceSnapshot; if (snapshot is null) return false;
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history is not null &&
               history.State.Revision == snapshot.Revision && history.Journal.SequenceEqual(snapshot.Journal) &&
               history.CanUndo == snapshot.CanUndo && history.CanRedo == snapshot.CanRedo &&
               _sourceSession.IsDirty == snapshot.IsDirty && _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureAudioApprovalUx2EnvE() == snapshot.Approval;
    }

    private bool ReleaseIsolatedEvidenceSessionUx2EnvE()
    {
        var evidenceSession = _evidenceSession; var tempRoot = _evidenceTempRoot;
        _evidenceSession = null; _evidenceTempRoot = null; _evidenceSourceSnapshot = null;
        _session = _sourceSession; _evidenceUsesRealModuleData = false; _evidenceUsesCanonicalFallback = false;
        var released = true;
        try { evidenceSession?.Close(); }
        catch (Exception exception)
        {
            released = false;
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_E_RELEASE_FAILED " +
                        $"type={exception.GetType().Name} message={exception.Message}");
        }
        var rootReleased = StudioSessionController.DeleteUx2EnvETemporaryWorkspace(tempRoot);
        if (!rootReleased) _evidenceTempRoot = tempRoot;
        _evidenceReleaseSucceeded = released && rootReleased; return _evidenceReleaseSucceeded;
    }

    private void ResetEvidenceStateUx2EnvE()
    {
        _evidenceSession = null; _evidenceTempRoot = null; _evidenceSourceSnapshot = null;
        _evidenceUsesRealModuleData = false; _evidenceUsesCanonicalFallback = false;
    }

    private static bool AreaHasRichAudioUx2EnvE(AreaDefinition area) =>
        area.Environment.Audio.AdaptiveLayers.Count > 0 &&
        area.Environment.Audio.Orchestration.MusicStates.Count > 0 &&
        area.Environment.Audio.Orchestration.MixBuses.Count > 0 &&
        area.Authoring.Experience.AudioSnapshots.Count > 0;

    private void UpdateDialogBoundsUx2EnvE()
    {
        var viewport = GetViewportRect().Size; if (viewport.X <= 0 || viewport.Y <= 0) return;
        var scale = viewport.X >= 3_200 ? 1.7f : viewport.X >= 2_300 ? 1.23f : 1f;
        var width = Math.Clamp((viewport.X - 48f) / scale, 1_180f, 1_680f);
        var height = Math.Clamp((viewport.Y - 36f) / scale, 820f, 1_100f);
        _dialog.Scale = Vector2.One * scale; _dialog.CustomMaximumSize = new Vector2(width, height);
        _dialog.Size = new Vector2(width, height);
        _dialog.Position = new Vector2((viewport.X - width * scale) / 2f,
            Math.Max(14f, (viewport.Y - height * scale) * .45f));
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && Visible) UpdateDialogBoundsUx2EnvE();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Close(); GetViewport().SetInputAsHandled();
        }
    }

    public override void _ExitTree()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null) Close();
    }

    private static Button ActionButtonUx2EnvE(string text)
    {
        var button = StudioStyle.Button(text, compact: true); button.CustomMinimumSize = new Vector2(176, 34);
        return button;
    }

    private static SpinBox NumberUx2EnvE(double min, double max, double step, bool greater = false) =>
        StudioUx2EnvBControls.Number(min, max, step, greater);

    private static IReadOnlyList<string> ReadLinesUx2EnvE(string text) => text.Replace("\r", string.Empty).Split('\n')
        .Select(item => item.Trim()).Where(item => item.Length > 0).ToArray();

    private static void ClearChildrenUx2EnvE(Node root)
    {
        foreach (var child in root.GetChildren()) child.QueueFree();
    }

    private static IEnumerable<string> DescendantVisibleTextUx2EnvE(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            switch (child)
            {
                case Label label: yield return label.Text; break;
                case LineEdit line: yield return line.Text; break;
                case TextEdit text: yield return text.Text; break;
                case OptionButton option:
                    for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
                    break;
                case Button button: yield return button.Text; break;
            }
            foreach (var descendant in DescendantVisibleTextUx2EnvE(child)) yield return descendant;
        }
    }

    private static bool ContainsUuidUx2EnvE(string text) => System.Text.RegularExpressions.Regex.IsMatch(
        text, @"\b(?:[0-9a-f]{32}|[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\b",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase |
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);
}
