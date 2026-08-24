using Forge.Domain.Audio;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal interface IStudioUx2EnvFValueRow<T>
{
    Control Control { get; }
    T Value { get; }
    event Action? Changed;
    event Action? RemoveRequested;
    event Action<int>? MoveRequested;
    void SetEditingEnabled(bool enabled);
}

internal abstract partial class StudioUx2EnvFRowBase<T> : PanelContainer, IStudioUx2EnvFValueRow<T>
{
    private readonly Button _up;
    private readonly Button _down;
    private readonly Button _remove;
    protected readonly VBoxContainer Root;

    public Control Control => this;
    public abstract T Value { get; }
    public event Action? Changed;
    public event Action? RemoveRequested;
    public event Action<int>? MoveRequested;

    protected StudioUx2EnvFRowBase(string title)
    {
        AddThemeStyleboxOverride("panel", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        var margin = StudioStyle.Margin(8, 6, 8, 6); AddChild(margin);
        Root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        Root.AddThemeConstantOverride("separation", 6); margin.AddChild(Root);
        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label(title, 10, StudioStyle.Accent));
        heading.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _up = StudioStyle.Button("Monter", compact: true);
        _down = StudioStyle.Button("Descendre", compact: true);
        _remove = StudioStyle.Button("Retirer", compact: true);
        _up.Pressed += () => MoveRequested?.Invoke(-1);
        _down.Pressed += () => MoveRequested?.Invoke(1);
        _remove.Pressed += () => RemoveRequested?.Invoke();
        heading.AddChild(_up); heading.AddChild(_down); heading.AddChild(_remove); Root.AddChild(heading);
    }

    protected void Watch(LineEdit control) => control.TextChanged += _ => Changed?.Invoke();
    protected void Watch(TextEdit control) => control.TextChanged += () => Changed?.Invoke();
    protected void Watch(OptionButton control) => control.ItemSelected += _ => Changed?.Invoke();
    protected void Watch(CheckBox control) => control.Toggled += _ => Changed?.Invoke();
    protected void RaiseChanged() => Changed?.Invoke();

    public virtual void SetEditingEnabled(bool enabled)
    {
        _up.Disabled = !enabled; _down.Disabled = !enabled; _remove.Disabled = !enabled;
    }
}

internal sealed partial class StudioUx2EnvFCollectionEditor<T> : VBoxContainer
{
    private readonly VBoxContainer _rows;
    private readonly Button _add;
    private readonly Func<T, IStudioUx2EnvFValueRow<T>> _rowFactory;
    private readonly Func<T> _newFactory;
    private readonly List<IStudioUx2EnvFValueRow<T>> _items = [];
    private bool _editingEnabled;

    internal event Action? Changed;
    internal IReadOnlyList<T> Values => _items.Select(item => item.Value).ToArray();

    internal StudioUx2EnvFCollectionEditor(
        string addLabel,
        Func<T, IStudioUx2EnvFValueRow<T>> rowFactory,
        Func<T> newFactory)
    {
        _rowFactory = rowFactory; _newFactory = newFactory;
        AddThemeConstantOverride("separation", 6);
        _rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 6); AddChild(_rows);
        _add = StudioStyle.Button(addLabel, compact: true);
        _add.Pressed += () => { AddRow(_newFactory()); Changed?.Invoke(); };
        AddChild(_add);
    }

    internal void SetValues(IReadOnlyList<T> values)
    {
        foreach (var item in _items) item.Control.QueueFree();
        _items.Clear();
        foreach (var value in values) AddRow(value);
    }

    internal void SetEditingEnabled(bool enabled)
    {
        _editingEnabled = enabled; _add.Disabled = !enabled;
        foreach (var item in _items) item.SetEditingEnabled(enabled);
    }

    private void AddRow(T value)
    {
        var row = _rowFactory(value);
        row.Changed += () => Changed?.Invoke();
        row.RemoveRequested += () =>
        {
            _items.Remove(row); row.Control.QueueFree(); Changed?.Invoke();
        };
        row.MoveRequested += delta =>
        {
            var index = _items.IndexOf(row);
            var destination = Math.Clamp(index + delta, 0, _items.Count - 1);
            if (index == destination) return;
            _items.RemoveAt(index); _items.Insert(destination, row);
            _rows.MoveChild(row.Control, destination); Changed?.Invoke();
        };
        row.SetEditingEnabled(_editingEnabled); _items.Add(row); _rows.AddChild(row.Control);
    }
}

internal sealed partial class StudioUx2EnvFClipRow : StudioUx2EnvFRowBase<StudioUx2EnvFClipDraft>
{
    private readonly Guid _id;
    private readonly LineEdit _resource;
    private readonly LineEdit _weight;
    private readonly TextEdit _tags;
    private readonly LineEdit _variant;
    private readonly LineEdit _locale;
    private readonly LineEdit _loudness;
    private readonly LineEdit _preview;
    private readonly LineEdit _mime;
    private readonly LineEdit _memory;
    private readonly LineEdit _license;
    private readonly LineEdit _provenance;
    private readonly LineEdit _hash;
    private readonly TextEdit _transcript;
    private readonly TextEdit _waveform;
    private readonly LineEdit _duration;

    public override StudioUx2EnvFClipDraft Value => new(
        _id, _resource.Text, _weight.Text, Lines(_tags.Text), _variant.Text, _locale.Text,
        _loudness.Text, _preview.Text, _mime.Text, _memory.Text, _license.Text, _provenance.Text,
        _hash.Text, _transcript.Text, Lines(_waveform.Text), _duration.Text);

    internal StudioUx2EnvFClipRow(StudioUx2EnvFClipDraft value) : base("Clip gouverné · ordre modifiable")
    {
        _id = value.Id;
        _resource = Text("audio/ambience/cascade.ogg", value.ResourcePath);
        _weight = Text("1", value.Weight);
        _tags = StudioUx2EnvBControls.Lines("Un tag par ligne", 58); _tags.Text = string.Join('\n', value.Tags);
        _variant = Text("Proche", value.Variant);
        _locale = Text("fr", value.Locale);
        _loudness = Text("-18", value.LoudnessLufs);
        _preview = Text("audio/ambience/cascade-preview.ogg", value.PreviewResourcePath);
        _mime = Text("audio/ogg", value.MimeType);
        _memory = Text("1048576", value.MemoryBytes);
        _license = Text("CC0-1.0", value.LicenseSpdx);
        _provenance = Text("asset-vault:audio", value.Provenance);
        _hash = Text("Empreinte SHA-256", value.ContentSha256);
        _transcript = StudioUx2EnvBControls.Lines("Transcription ou description accessible", 58);
        _transcript.Text = value.Transcript;
        _waveform = StudioUx2EnvBControls.Lines("Pics 0..1, un par ligne · conservés sans recalcul", 58);
        _waveform.Text = string.Join('\n', value.WaveformPeaks);
        _duration = Text("12.5", value.DurationSeconds);

        var identity = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        identity.AddChild(Field("Ressource relative ou intégrée", _resource, 280));
        identity.AddChild(Field("Variante", _variant, 140));
        identity.AddChild(Field("Langue", _locale, 90));
        identity.AddChild(Field("Poids", _weight, 90));
        Root.AddChild(identity);
        var audio = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        audio.AddChild(Field("Niveau LUFS", _loudness)); audio.AddChild(Field("Durée (s)", _duration));
        audio.AddChild(Field("MIME", _mime)); audio.AddChild(Field("Mémoire déclarée (octets)", _memory, 180));
        Root.AddChild(audio);
        var governance = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        governance.AddChild(Field("Aperçu ressource (facultatif)", _preview, 260));
        governance.AddChild(Field("Licence SPDX", _license, 150));
        governance.AddChild(Field("Provenance", _provenance, 240));
        governance.AddChild(Field("SHA-256 du contenu", _hash, 360));
        Root.AddChild(governance);
        var tails = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        tails.AddChild(Field("Tags", _tags, 180)); tails.AddChild(Field("Transcription", _transcript, 260));
        tails.AddChild(Field("Forme d’onde préservée", _waveform, 220)); Root.AddChild(tails);
        foreach (var edit in new[] { _resource, _weight, _variant, _locale, _loudness, _preview, _mime,
                     _memory, _license, _provenance, _hash, _duration }) Watch(edit);
        Watch(_tags); Watch(_transcript); Watch(_waveform);
    }

    public override void SetEditingEnabled(bool enabled)
    {
        base.SetEditingEnabled(enabled);
        foreach (var edit in new[] { _resource, _weight, _variant, _locale, _loudness, _preview, _mime,
                     _memory, _license, _provenance, _hash, _duration }) edit.Editable = enabled;
        _tags.Editable = enabled; _transcript.Editable = enabled; _waveform.Editable = enabled;
    }

    private static LineEdit Text(string placeholder, string value)
    {
        var edit = StudioUx2EnvBControls.Text(placeholder); edit.Text = value; return edit;
    }

    private static VBoxContainer Field(string label, Control control, int width = 118) =>
        StudioUx2EnvBControls.Field(label, control, width);

    private static IReadOnlyList<string> Lines(string text) =>
        (text ?? string.Empty).Replace("\r", string.Empty).Split('\n')
            .Select(item => item.Trim()).Where(item => item.Length > 0).ToArray();
}

internal sealed partial class StudioUx2EnvFDefinitionRow : StudioUx2EnvFRowBase<StudioUx2EnvFDefinitionDraft>
{
    private readonly Guid _id;
    private readonly int _schemaVersion;
    private readonly Guid _clipSetId;
    private readonly int _clipSetSchemaVersion;
    private readonly LineEdit _name;
    private readonly LineEdit _clipSetName;
    private readonly OptionButton _selection;
    private readonly LineEdit _clipSetProvenance;
    private readonly StudioUx2EnvFCollectionEditor<StudioUx2EnvFClipDraft> _clips;
    private readonly OptionButton _loopMode;
    private readonly LineEdit _repeat;
    private readonly LineEdit _regionStart;
    private readonly LineEdit _regionEnd;
    private readonly LineEdit _crossfade;
    private readonly LineEdit _stopEvent;
    private readonly LineEdit _inner;
    private readonly LineEdit _outer;
    private readonly LineEdit _attenuation;
    private readonly LineEdit _cone;
    private readonly LineEdit _coneDb;
    private readonly LineEdit _occlusion;
    private readonly LineEdit _height;
    private readonly CheckBox _virtualize;
    private readonly OptionButton _mixBus;
    private readonly IReadOnlyList<Guid?> _mixBusIds;
    private readonly LineEdit _startDelay;
    private readonly LineEdit _activatedScript;
    private readonly LineEdit _completedScript;
    private readonly CheckBox _required;
    private readonly LineEdit _provenance;
    private readonly LineEdit _manifest;
    private readonly LineEdit _license;
    private readonly LineEdit _hash;

    public override StudioUx2EnvFDefinitionDraft Value => new(
        _id,
        _schemaVersion,
        _name.Text,
        _clipSetId,
        _clipSetSchemaVersion,
        _clipSetName.Text,
        (AudioClipSelectionMode)Math.Max(0, _selection.Selected),
        _clips.Values,
        _clipSetProvenance.Text,
        (AudioLoopMode)Math.Max(0, _loopMode.Selected),
        _repeat.Text,
        _regionStart.Text,
        _regionEnd.Text,
        _crossfade.Text,
        _stopEvent.Text,
        _inner.Text,
        _outer.Text,
        _attenuation.Text,
        _cone.Text,
        _coneDb.Text,
        _occlusion.Text,
        _height.Text,
        _virtualize.ButtonPressed,
        _mixBus.Selected >= 0 && _mixBus.Selected < _mixBusIds.Count ? _mixBusIds[_mixBus.Selected] : null,
        _startDelay.Text,
        _activatedScript.Text,
        _completedScript.Text,
        _required.ButtonPressed,
        _provenance.Text,
        _manifest.Text,
        _license.Text,
        _hash.Text);

    internal StudioUx2EnvFDefinitionRow(
        StudioUx2EnvFDefinitionDraft value,
        IReadOnlyList<StudioUx2EnvFMixBusChoice> mixBuses) : base("Émetteur sonore")
    {
        _id = value.Id; _schemaVersion = value.SchemaVersion;
        _clipSetId = value.ClipSetId; _clipSetSchemaVersion = value.ClipSetSchemaVersion;
        _name = Text("Nom humain", value.Name);
        _clipSetName = Text("Nom de la liste", value.ClipSetName);
        _selection = StudioUx2EnvBControls.Options("Ordonnée", "Pondérée");
        _selection.Select((int)value.Selection);
        _clipSetProvenance = Text("studio:audio", value.ClipSetProvenance);
        var identity = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        identity.AddChild(Field("Nom de l’émetteur", _name, 220));
        identity.AddChild(Field("Nom de la liste de clips", _clipSetName, 220));
        identity.AddChild(Field("Sélection des clips", _selection, 150));
        identity.AddChild(Field("Provenance de la liste", _clipSetProvenance, 200));
        Root.AddChild(identity);

        _clips = new StudioUx2EnvFCollectionEditor<StudioUx2EnvFClipDraft>(
            "Ajouter un clip gouverné",
            clip => new StudioUx2EnvFClipRow(clip),
            NewClip);
        _clips.SetValues(value.Clips); _clips.Changed += RaiseChanged;
        Root.AddChild(StudioUx2EnvBControls.Card(
            "Ux2EnvFClips",
            "Clips ordonnés et pondérés",
            "Monter/Descendre modifie l’ordre canonique. Les poids pilotent la sélection déterministe pondérée.",
            _clips));

        _loopMode = StudioUx2EnvBControls.Options(
            "Lecture unique", "Boucle continue", "Région sans rupture", "Répétition finie", "Pilotée par événement");
        _loopMode.Select((int)value.LoopMode);
        _repeat = Text("0", value.RepeatCount); _regionStart = Text("0", value.RegionStartSeconds);
        _regionEnd = Text("0", value.RegionEndSeconds); _crossfade = Text("0", value.CrossfadeSeconds);
        _stopEvent = Text("audio.stop", value.StopEvent);
        var loop = new GridContainer { Columns = 6, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        loop.AddChild(Field("Mode de lecture", _loopMode, 190)); loop.AddChild(Field("Répétitions", _repeat));
        loop.AddChild(Field("Début région (s)", _regionStart)); loop.AddChild(Field("Fin région (s)", _regionEnd));
        loop.AddChild(Field("Fondu (s)", _crossfade)); loop.AddChild(Field("Événement d’arrêt", _stopEvent, 190));
        Root.AddChild(StudioUx2EnvBControls.Card(
            "Ux2EnvFLoop",
            "Lecture unique, boucle et arrêt",
            "Les cinq modes canoniques sont persistés; l’aperçu ne démarre aucun lecteur.",
            loop));

        _inner = Text("1", value.InnerRadius); _outer = Text("25", value.OuterRadius);
        _attenuation = Text("inverse-square", value.AttenuationCurve);
        _cone = Text("360", value.ConeAngleDegrees); _coneDb = Text("-12", value.ConeAttenuationDb);
        _occlusion = Text("0", value.Occlusion); _height = Text("0", value.HeightOffset);
        _virtualize = StudioUx2EnvBControls.Check("Virtualiser hors portée");
        _virtualize.ButtonPressed = value.VirtualizeOutsideRange;
        var spatial = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        spatial.AddChild(Field("Rayon proche", _inner)); spatial.AddChild(Field("Rayon lointain", _outer));
        spatial.AddChild(Field("Courbe d’atténuation", _attenuation, 170)); spatial.AddChild(Field("Cône (°)", _cone));
        spatial.AddChild(Field("Atténuation du cône (dB)", _coneDb, 170));
        spatial.AddChild(Field("Occlusion 0..1", _occlusion)); spatial.AddChild(Field("Décalage hauteur", _height));
        spatial.AddChild(_virtualize);
        Root.AddChild(StudioUx2EnvBControls.Card(
            "Ux2EnvFSpatial",
            "Portée et spatialisation 3D",
            "Ces valeurs décrivent le rendu runtime; Studio ne mesure ni occlusion ni acoustique live.",
            spatial));

        var busChoices = mixBuses.ToList();
        if (value.MixBusId is { } selectedBus && busChoices.All(item => item.Id != selectedBus))
            busChoices.Add(new(selectedBus, "Bus absent · à corriger"));
        _mixBus = StudioUx2EnvBControls.Options();
        foreach (var choice in busChoices) _mixBus.AddItem(choice.Label);
        _mixBusIds = busChoices.Select(item => item.Id).ToArray();
        var busIndex = busChoices.FindIndex(item => item.Id == value.MixBusId);
        _mixBus.Select(Math.Max(0, busIndex));
        _startDelay = Text("0", value.StartDelaySeconds);
        _activatedScript = Text("scripts/audio/enter.forge", value.ActivatedScript);
        _completedScript = Text("scripts/audio/exit.forge", value.CompletedScript);
        _required = StudioUx2EnvBControls.Check("Ressource requise au runtime");
        _required.ButtonPressed = value.Required;
        _provenance = Text("studio:audio", value.Provenance);
        _manifest = Text("assets/audio-manifest.json", value.PackageManifest);
        _license = Text("CC0-1.0", value.LicenseSpdx);
        _hash = Text("Empreinte SHA-256", value.ContentSha256);
        var governance = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        governance.AddChild(Field("Bus de mixage", _mixBus, 220));
        governance.AddChild(Field("Délai initial (s)", _startDelay));
        governance.AddChild(Field("Script d’activation", _activatedScript, 230));
        governance.AddChild(Field("Script de fin", _completedScript, 230));
        governance.AddChild(Field("Provenance", _provenance, 200));
        governance.AddChild(Field("Manifeste du paquet", _manifest, 240));
        governance.AddChild(Field("Licence SPDX", _license));
        governance.AddChild(Field("SHA-256 du contenu", _hash, 330));
        governance.AddChild(_required);
        Root.AddChild(StudioUx2EnvBControls.Card(
            "Ux2EnvFGovernance",
            "Routage, scripts et gouvernance",
            "Le bus est choisi par nom humain. Les identités techniques restent internes.",
            governance));

        foreach (var edit in new[] { _name, _clipSetName, _clipSetProvenance, _repeat, _regionStart,
                     _regionEnd, _crossfade, _stopEvent, _inner, _outer, _attenuation, _cone,
                     _coneDb, _occlusion, _height, _startDelay, _activatedScript, _completedScript,
                     _provenance, _manifest, _license, _hash }) Watch(edit);
        Watch(_selection); Watch(_loopMode); Watch(_virtualize); Watch(_mixBus); Watch(_required);
    }

    public override void SetEditingEnabled(bool enabled)
    {
        base.SetEditingEnabled(enabled); _clips.SetEditingEnabled(enabled);
        foreach (var edit in new[] { _name, _clipSetName, _clipSetProvenance, _repeat, _regionStart,
                     _regionEnd, _crossfade, _stopEvent, _inner, _outer, _attenuation, _cone,
                     _coneDb, _occlusion, _height, _startDelay, _activatedScript, _completedScript,
                     _provenance, _manifest, _license, _hash }) edit.Editable = enabled;
        _selection.Disabled = !enabled; _loopMode.Disabled = !enabled; _virtualize.Disabled = !enabled;
        _mixBus.Disabled = !enabled; _required.Disabled = !enabled;
    }

    private static StudioUx2EnvFClipDraft NewClip() => new(
        Guid.NewGuid(), "builtin://openrpg/audio/new-clip", "1", ["ambiance"], "Nouvelle variante",
        "fr", "-18", string.Empty, "audio/ogg", "262144", "CC0-1.0", "studio:ux2-env-f",
        new string('d', 64), string.Empty, [], "0");

    private static LineEdit Text(string placeholder, string value)
    {
        var edit = StudioUx2EnvBControls.Text(placeholder); edit.Text = value; return edit;
    }

    private static VBoxContainer Field(string label, Control control, int width = 118) =>
        StudioUx2EnvBControls.Field(label, control, width);
}

internal sealed partial class StudioUx2EnvFInstanceRow : StudioUx2EnvFRowBase<StudioUx2EnvFInstanceDraft>
{
    private readonly Guid _id;
    private readonly OptionButton _definition;
    private readonly IReadOnlyList<Guid> _definitionIds;
    private readonly LineEdit[] _position;
    private readonly LineEdit[] _rotation;
    private readonly LineEdit[] _scale;
    private readonly CheckBox _enabled;
    private readonly LineEdit _provenance;

    public override StudioUx2EnvFInstanceDraft Value => new(
        _id,
        _definition.Selected >= 0 && _definition.Selected < _definitionIds.Count
            ? _definitionIds[_definition.Selected]
            : Guid.Empty,
        _position[0].Text, _position[1].Text, _position[2].Text,
        _rotation[0].Text, _rotation[1].Text, _rotation[2].Text, _rotation[3].Text,
        _scale[0].Text, _scale[1].Text, _scale[2].Text,
        _enabled.ButtonPressed,
        _provenance.Text);

    internal StudioUx2EnvFInstanceRow(
        StudioUx2EnvFInstanceDraft value,
        IReadOnlyList<KeyValuePair<Guid, string>> definitions) : base("Instance positionnée en 3D")
    {
        _id = value.Id;
        var choices = definitions.ToList();
        if (choices.All(item => item.Key != value.DefinitionId))
            choices.Add(new(value.DefinitionId, "Émetteur absent · à corriger"));
        _definition = StudioUx2EnvBControls.Options();
        foreach (var choice in choices) _definition.AddItem(choice.Value);
        _definitionIds = choices.Select(item => item.Key).ToArray();
        _definition.Select(Math.Max(0, choices.FindIndex(item => item.Key == value.DefinitionId)));
        _position = [Text("X", value.PositionX), Text("Y", value.PositionY), Text("Z", value.PositionZ)];
        _rotation = [Text("X", value.RotationX), Text("Y", value.RotationY),
            Text("Z", value.RotationZ), Text("W", value.RotationW)];
        _scale = [Text("X", value.ScaleX), Text("Y", value.ScaleY), Text("Z", value.ScaleZ)];
        _enabled = StudioUx2EnvBControls.Check("Instance active"); _enabled.ButtonPressed = value.Enabled;
        _provenance = Text("studio:audio", value.Provenance);
        var identity = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        identity.AddChild(StudioUx2EnvBControls.Field("Émetteur", _definition, 260));
        identity.AddChild(StudioUx2EnvBControls.Field("Provenance", _provenance, 220));
        identity.AddChild(_enabled); Root.AddChild(identity);
        var transform = new GridContainer { Columns = 5, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddVector(transform, "Position", _position, ["X", "Y", "Z"]);
        AddVector(transform, "Rotation quaternion", _rotation, ["X", "Y", "Z", "W"]);
        AddVector(transform, "Échelle", _scale, ["X", "Y", "Z"]);
        Root.AddChild(StudioUx2EnvBControls.Card(
            "Ux2EnvFTransform",
            "Transform complète",
            "Position, rotation et échelle sont conservées sans arrondi ni clamp silencieux.",
            transform));
        Watch(_definition); Watch(_enabled); Watch(_provenance);
        foreach (var edit in _position.Concat(_rotation).Concat(_scale)) Watch(edit);
    }

    public override void SetEditingEnabled(bool enabled)
    {
        base.SetEditingEnabled(enabled); _definition.Disabled = !enabled; _enabled.Disabled = !enabled;
        _provenance.Editable = enabled;
        foreach (var edit in _position.Concat(_rotation).Concat(_scale)) edit.Editable = enabled;
    }

    private static void AddVector(GridContainer grid, string label, IReadOnlyList<LineEdit> values,
        IReadOnlyList<string> axes)
    {
        grid.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        for (var index = 0; index < 4; index++)
            grid.AddChild(index < values.Count
                ? StudioUx2EnvBControls.Field(axes[index], values[index], 86)
                : new Control());
    }

    private static LineEdit Text(string placeholder, string value)
    {
        var edit = StudioUx2EnvBControls.Text(placeholder); edit.Text = value; return edit;
    }
}

internal sealed partial class StudioAudioEmittersOverlayUx2EnvF : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx2EnvFEvidenceSourceSnapshot? _evidenceSourceSnapshot;
    private string? _evidenceTempRoot;
    private bool _evidenceUsesRealModuleData;
    private bool _evidenceUsesCanonicalFallback;
    private bool _evidenceReleaseSucceeded = true;
    private readonly PanelContainer _dialog;
    private readonly OptionButton _area;
    private readonly Label _selection;
    private readonly TabContainer _tabs;
    private readonly StudioUx2EnvFCollectionEditor<StudioUx2EnvFDefinitionDraft> _definitions;
    private readonly StudioUx2EnvFCollectionEditor<StudioUx2EnvFInstanceDraft> _instances;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly VBoxContainer _plans;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;
    private IReadOnlyList<StudioUx2EnvFAreaChoice> _choices = [];
    private StudioUx2EnvFDraft? _draft;
    private StudioUx2EnvFPreview? _lastPreview;
    private bool _updating;
    private bool _applied;
    private bool _canonicalPreviewReady;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? AudioEmittersChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _choices.Count;
    internal bool HasExplicitSelection => _draft is not null && _area.Selected > 0;
    internal bool CanonicalPreviewReady => _canonicalPreviewReady && _lastPreview is
    {
        Succeeded: true,
        CanonicalDryRun: true,
        DeterministicPlansBuilt: true,
        PlayerInstantiationSkipped: true,
        ResourceLoadingSkipped: true,
        RuntimeClaimsBounded: true,
    };
    internal bool ShowsCompleteCollections => _draft is not null && _definitions.Values.Count > 0 &&
                                               _definitions.Values.All(item => item.Clips.Count > 0) &&
                                               _instances.Values.Count > 0;
    internal bool ShowsStructuredDiagnostics => _lastPreview?.Groups.Count == 4;
    internal bool ShowsBoundedRuntimeClaim => _lastPreview is
    {
        PlayerInstantiationSkipped: true,
        ResourceLoadingSkipped: true,
        RuntimeClaimsBounded: true,
    } && _lastPreview.Summary.Contains("runtime Godot", StringComparison.Ordinal);
    internal bool ShowsHumanSurface
    {
        get
        {
            var titles = Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle);
            var text = string.Join(' ', DescendantVisibleText(this).Concat(titles));
            return text.Contains("Émetteurs & clips", StringComparison.Ordinal) &&
                   text.Contains("Instances 3D", StringComparison.Ordinal) &&
                   text.Contains("Lecture unique", StringComparison.Ordinal) &&
                   text.Contains("Portée et spatialisation", StringComparison.Ordinal) &&
                   text.Contains("Aperçu & validation", StringComparison.Ordinal) &&
                   !ContainsUuidUx2EnvF(text);
        }
    }
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                           !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealModuleSource => UsesIsolatedEvidence && _evidenceUsesRealModuleData;
    internal bool UsesCanonicalFallbackEvidence => UsesIsolatedEvidence && _evidenceUsesCanonicalFallback;
    internal bool EvidenceSourceUntouched => SourceSnapshotMatches();
    internal bool EvidenceResourcesReleased => _evidenceReleaseSucceeded &&
                                               _evidenceSession is null && _evidenceTempRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);

    internal StudioAudioEmittersOverlayUx2EnvF(StudioSessionController session)
    {
        _sourceSession = session; _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.014f, .019f, .026f, .97f); MouseFilter = MouseFilterEnum.Stop; Visible = false;
        _dialog = StudioStyle.Panel("Ux2EnvFAudioEmittersDialog", StudioStyle.Surface, 12, 1); AddChild(_dialog);
        var margin = StudioStyle.Margin(14, 12, 14, 12); _dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 8); margin.AddChild(root);
        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; root.AddChild(heading);
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        headingText.AddChild(StudioStyle.Label("Émetteurs audio et instances 3D", 18));
        headingText.AddChild(StudioStyle.Label(
            "Clips gouvernés, ordre/poids, boucles, portée et position · aperçu déterministe sans playback Studio.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true); close.Pressed += Close; heading.AddChild(close);
        _area = StudioUx2EnvBControls.Options(); _area.ItemSelected += SelectArea;
        root.AddChild(StudioUx2EnvBControls.Field("Zone à modifier · sélection explicite", _area, 460));
        _selection = StudioStyle.Label("Aucune zone sélectionnée.", 10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);
        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddChild(_tabs);

        _definitions = new StudioUx2EnvFCollectionEditor<StudioUx2EnvFDefinitionDraft>(
            "Ajouter un émetteur sonore",
            value => new StudioUx2EnvFDefinitionRow(value, CurrentMixBuses()),
            NewDefinition);
        _definitions.Changed += DefinitionsChanged;
        var definitionsTab = ScrollTab("Émetteurs & clips");
        definitionsTab.AddChild(StudioStyle.Label(
            "Chaque émetteur possède sa liste complète de clips, sa politique de boucle, sa spatialisation et sa gouvernance.",
            10, StudioStyle.Muted, wrap: true));
        definitionsTab.AddChild(_definitions);

        _instances = new StudioUx2EnvFCollectionEditor<StudioUx2EnvFInstanceDraft>(
            "Ajouter une instance 3D",
            value => new StudioUx2EnvFInstanceRow(value, CurrentDefinitionChoices()),
            NewInstance);
        _instances.Changed += InvalidatePreview;
        var instancesTab = ScrollTab("Instances 3D");
        instancesTab.AddChild(StudioStyle.Label(
            "Une instance choisit un émetteur par son nom humain et conserve position, rotation, échelle et activation.",
            10, StudioStyle.Muted, wrap: true));
        instancesTab.AddChild(_instances);

        var reviewTab = ScrollTab("Aperçu & validation");
        _diff = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _diagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _plans = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        reviewTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvFDiff", "Modifications proposées",
            "Le dry-run canonique ne change ni projet, ni historique.", _diff));
        reviewTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvFDiagnostics", "Validation structurée",
            "Émetteurs, clips, boucles/spatialisation et instances restent séparés.", _diagnostics));
        reviewTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvFPlans", "Plans de départ déterministes",
            "Ces lignes ne sont pas une écoute : aucune ressource et aucun lecteur Godot ne sont créés.", _plans));

        _summary = StudioStyle.Label(
            "Sélectionnez une zone puis prévisualisez · aucun changement appliqué.",
            10, StudioStyle.Muted, wrap: true);
        _summary.CustomMinimumSize = new Vector2(0, 54); root.AddChild(_summary);
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 6); root.AddChild(actions);
        _preview = StudioStyle.Button("Prévisualiser", compact: true);
        _apply = StudioStyle.Button("Appliquer", compact: true);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        {
            button.CustomMinimumSize = new Vector2(126, 34); actions.AddChild(button);
        }
        _preview.Pressed += Preview; _apply.Pressed += Apply; _undo.Pressed += Undo;
        _redo.Pressed += Redo; _save.Pressed += Save;
        var note = StudioStyle.Label(
            "Studio produit un plan déterministe. Seul le runtime Godot peut charger le clip et instancier AudioStreamPlayer3D.",
            9, StudioStyle.Muted, wrap: true);
        note.SizeFlagsHorizontal = SizeFlags.ExpandFill; note.CustomMinimumSize = new Vector2(0, 30); root.AddChild(note);
        _status = StudioStyle.Label("Prêt · sélection explicite requise", 9, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        SetEditingEnabled(false); ResetReview(); UpdateActionState();
    }

    internal void Open()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null) ReleaseIsolatedEvidenceSession();
        _session = _sourceSession; Visible = true; UpdateDialogBounds(); RefreshChoices();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceTempRoot is null) return;
        if (Visible || !ReferenceEquals(_session, _sourceSession))
            _session.CancelAudioEmittersApprovalUx2EnvF();
        Visible = false; _draft = null; _lastPreview = null; _canonicalPreviewReady = false; _applied = false;
        ReleaseIsolatedEvidenceSession(); _session = _sourceSession;
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        var approval = _sourceSession.CaptureAudioEmittersApprovalUx2EnvF();
        _session = _sourceSession; Visible = true; UpdateDialogBounds(); RefreshChoices();
        _sourceSession.RestoreAudioEmittersApprovalUx2EnvF(approval);
    }

    internal bool PrepareEvidence(string mode)
    {
        var sourceApproval = _sourceSession.CaptureAudioEmittersApprovalUx2EnvF();
        Close(); _sourceSession.RestoreAudioEmittersApprovalUx2EnvF(sourceApproval);
        _evidenceReleaseSucceeded = true;
        var sourceProject = _sourceSession.Project;
        if (sourceProject is null || _sourceSession.History is null || sourceProject.Areas.Count == 0) return false;
        _evidenceSourceSnapshot = CaptureSourceSnapshot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux2-env-f-{Guid.NewGuid():N}");
        var evidenceSession = new StudioSessionController();
        try
        {
            var realArea = sourceProject.Areas.FirstOrDefault(area =>
                area.Environment.Audio.Emitters.Definitions.Any(definition => definition.ClipSet.Clips.Count > 0) &&
                area.Environment.Audio.Emitters.Instances.Any());
            _evidenceUsesRealModuleData = realArea is not null;
            _evidenceUsesCanonicalFallback = realArea is null;
            var evidenceProject = realArea is null
                ? StudioSessionController.CreateUx2EnvFContractProject(sourceProject)
                : sourceProject;
            var evidenceAreaId = realArea?.Id ?? evidenceProject.Areas[0].Id;
            var created = ProjectSession.Create(tempRoot, evidenceProject);
            if (!created.Succeeded || !evidenceSession.Open(tempRoot).Succeeded)
            {
                evidenceSession.Close();
                var released = StudioSessionController.DeleteUx2EnvFTemporaryWorkspace(tempRoot);
                ResetEvidenceState(); _evidenceReleaseSucceeded = released;
                if (!released) _evidenceTempRoot = tempRoot;
                return false;
            }
            _evidenceSession = evidenceSession; _evidenceTempRoot = tempRoot; _session = evidenceSession;
            Visible = true; UpdateDialogBounds(); RefreshChoices();
            var choiceIndex = _choices.ToList().FindIndex(choice => choice.AreaId == evidenceAreaId);
            if (choiceIndex < 0) { Close(); return false; }
            _area.Select(choiceIndex + 1); LoadChoice(choiceIndex);
            if (!ConfigureEvidenceDraft()) { Close(); return false; }
            Preview();
            var ready = CanonicalPreviewReady && HasExplicitSelection && ShowsCompleteCollections &&
                        ShowsStructuredDiagnostics && ShowsBoundedRuntimeClaim && ShowsHumanSurface &&
                        UsesIsolatedEvidence && (UsesRealModuleSource ^ UsesCanonicalFallbackEvidence) &&
                        EvidenceSourceUntouched;
            if (!ready) { Close(); return false; }
            return true;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_F_EVIDENCE_FAILED mode={mode} " +
                        $"type={exception.GetType().Name} message={exception.Message}");
            var sessionReleased = true;
            try { evidenceSession.Close(); }
            catch (Exception releaseException)
            {
                sessionReleased = false;
                GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_F_EVIDENCE_RELEASE_FAILED " +
                            $"type={releaseException.GetType().Name} message={releaseException.Message}");
            }
            var released = StudioSessionController.DeleteUx2EnvFTemporaryWorkspace(tempRoot);
            ResetEvidenceState(); _evidenceReleaseSucceeded = sessionReleased && released;
            if (!released) _evidenceTempRoot = tempRoot;
            _session = _sourceSession; Visible = false;
            return false;
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        var sourceSnapshot = CaptureSourceSnapshot();
        if (!PrepareEvidence("ux2-env-f-audio-emitters") || !CanonicalPreviewReady || !UsesIsolatedEvidence)
        {
            Close(); return false;
        }
        var isolated = _session;
        if (_lastPreview is not { Succeeded: true, CanApply: true }) { Close(); return false; }
        var isolatedProjectBefore = isolated.Project;
        Apply();
        var applied = _applied && isolated.Project is not null;
        var appliedJson = applied ? ProjectJsonSerializer.Serialize(isolated.Project!) : string.Empty;
        Undo();
        var undone = !_applied && ReferenceEquals(isolatedProjectBefore, isolated.Project) &&
                     isolated.History?.CanRedo == true;
        Redo();
        var redone = _applied && isolated.Project is not null &&
                     string.Equals(appliedJson, ProjectJsonSerializer.Serialize(isolated.Project!),
                         StringComparison.Ordinal);
        Save();
        var reopen = new StudioSessionController();
        var roundTrip = false;
        try
        {
            var opened = _evidenceTempRoot is not null && reopen.Open(_evidenceTempRoot).Succeeded;
            roundTrip = opened && reopen.Project is not null &&
                        string.Equals(appliedJson, ProjectJsonSerializer.Serialize(reopen.Project!),
                            StringComparison.Ordinal);
        }
        finally { reopen.Close(); }
        Close();
        return applied && undone && redone && roundTrip && SourceSnapshotMatches(sourceSnapshot) &&
               EvidenceResourcesReleased && SourceSessionRestored;
    }

    private VBoxContainer ScrollTab(string title)
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

    private void RefreshChoices()
    {
        _updating = true; _choices = _session.AudioEmitterChoicesUx2EnvF;
        _area.Clear(); _area.AddItem("Choisir explicitement une zone…");
        foreach (var choice in _choices) _area.AddItem(choice.Label);
        _area.Select(0); _updating = false; ClearSelection();
    }

    private void SelectArea(long index)
    {
        if (_updating) return;
        var choiceIndex = (int)index - 1;
        if (choiceIndex < 0 || choiceIndex >= _choices.Count) ClearSelection();
        else LoadChoice(choiceIndex);
    }

    private void ClearSelection()
    {
        _session.CancelAudioEmittersApprovalUx2EnvF(); _draft = null; _lastPreview = null;
        _canonicalPreviewReady = false; _applied = false; _definitions.SetValues([]); _instances.SetValues([]);
        _selection.Text = "Aucune zone sélectionnée · aucune identité technique n’est affichée.";
        _selection.Modulate = StudioStyle.Warning; SetEditingEnabled(false); ResetReview(); UpdateActionState();
    }

    private void LoadChoice(int choiceIndex)
    {
        var choice = _choices[choiceIndex]; _session.CancelAudioEmittersApprovalUx2EnvF();
        var draft = _session.CreateAudioEmittersDraftUx2EnvF(choice.AreaId);
        if (draft is null) { ClearSelection(); SetStatus("La zone choisie n’est plus disponible.", true); return; }
        _draft = draft; _lastPreview = null; _canonicalPreviewReady = false; _applied = false;
        Populate(draft);
        _selection.Text = $"Zone sélectionnée : {HumanLabelUx2EnvF(draft.AreaDisplayName)} · " +
                          $"{draft.Definitions.Count} émetteur(s), " +
                          $"{draft.Definitions.Sum(item => item.Clips.Count)} clip(s), " +
                          $"{draft.Instances.Count} instance(s).";
        _selection.Modulate = StudioStyle.Accent; SetEditingEnabled(true); ResetReview(); UpdateActionState();
        SetStatus(draft.Definitions.Count == 0
            ? "Catalogue vide · ajoutez explicitement un émetteur, un clip et une instance."
            : "Zone chargée · lancez l’aperçu sec avant toute application.");
    }

    private void Populate(StudioUx2EnvFDraft draft)
    {
        _updating = true;
        _definitions.SetValues(draft.Definitions);
        _instances.SetValues(draft.Instances);
        _updating = false;
    }

    private void DefinitionsChanged()
    {
        if (_updating) return;
        var instances = _instances.Values;
        _updating = true; _instances.SetValues(instances); _instances.SetEditingEnabled(_draft is not null);
        _updating = false; InvalidatePreview();
    }

    private void InvalidatePreview()
    {
        if (_updating || _draft is null) return;
        _session.CancelAudioEmittersApprovalUx2EnvF(); _lastPreview = null;
        _canonicalPreviewReady = false; _applied = false;
        _summary.Text = "La fiche a changé · relancez l’aperçu sec."; _summary.Modulate = StudioStyle.Warning;
        UpdateActionState();
    }

    private StudioUx2EnvFDraft? CaptureDraft()
    {
        if (_draft is null) return null;
        _draft = _draft with { Definitions = _definitions.Values, Instances = _instances.Values };
        return _draft;
    }

    private void Preview()
    {
        var draft = CaptureDraft();
        if (draft is null) { SetStatus("Sélectionnez explicitement une zone.", true); return; }
        var result = _session.PreviewAudioEmittersUx2EnvF(draft);
        _lastPreview = result; _canonicalPreviewReady = result.Succeeded && result.CanonicalDryRun;
        RenderPreview(result); UpdateActionState(); SetStatus(result.Message, !result.Succeeded);
    }

    private void Apply()
    {
        if (_lastPreview is not { Succeeded: true, CanApply: true })
        {
            SetStatus("Un aperçu sec valide et encore frais est requis.", true); return;
        }
        var result = _session.ApplyApprovedAudioEmittersUx2EnvF();
        _applied = result.Succeeded;
        if (result.Succeeded)
        {
            _draft = _session.CreateAudioEmittersDraftUx2EnvF(_draft?.AreaId);
            if (_draft is not null) Populate(_draft);
            _lastPreview = null; _canonicalPreviewReady = false;
            _summary.Text = "Émetteurs appliqués · enregistrer reste explicite.";
            _summary.Modulate = StudioStyle.Success; AudioEmittersChanged?.Invoke();
        }
        UpdateActionState(); SetStatus(result.Message, !result.Succeeded);
    }

    private void Undo()
    {
        var result = _session.Undo(); _applied = false;
        ReloadAfterHistory(); SetStatus(result.Message, !result.Succeeded);
    }

    private void Redo()
    {
        var result = _session.Redo(); _applied = result.Succeeded;
        ReloadAfterHistory(); SetStatus(result.Message, !result.Succeeded);
    }

    private void Save()
    {
        var result = _session.Save(); UpdateActionState(); SetStatus(result.Message, !result.Succeeded);
    }

    private void ReloadAfterHistory()
    {
        if (_draft is not null)
        {
            _draft = _session.CreateAudioEmittersDraftUx2EnvF(_draft.AreaId);
            if (_draft is not null) Populate(_draft);
        }
        _lastPreview = null; _canonicalPreviewReady = false; ResetReview(); UpdateActionState();
        AudioEmittersChanged?.Invoke();
    }

    private void RenderPreview(StudioUx2EnvFPreview preview)
    {
        ClearChildren(_diff); ClearChildren(_diagnostics); ClearChildren(_plans);
        if (preview.Diff.Entries.Count == 0)
            _diff.AddChild(StudioStyle.Label("Aucune différence.", 9, StudioStyle.Muted, wrap: true));
        else
            foreach (var entry in preview.Diff.Entries)
                _diff.AddChild(StudioStyle.Label($"• {entry}", 9, StudioStyle.Text, wrap: true));
        foreach (var group in preview.Groups)
        {
            var color = group.State == "conforme" ? StudioStyle.Success : StudioStyle.Warning;
            _diagnostics.AddChild(StudioStyle.Label($"{group.Title} · {group.State}", 10, color));
            foreach (var item in group.Diagnostics)
                _diagnostics.AddChild(StudioStyle.Label($"  • {item.Message}", 9, StudioStyle.Warning, wrap: true));
        }
        if (preview.PlaybackPlans.Count == 0)
            _plans.AddChild(StudioStyle.Label(
                "Aucun départ initial : ajoutez une instance active ou utilisez un mode non événementiel.",
                9, StudioStyle.Muted, wrap: true));
        else
            foreach (var plan in preview.PlaybackPlans)
                _plans.AddChild(StudioStyle.Label(
                    $"• {plan.Emitter} · clip {plan.Clip} · {plan.Loop} · {plan.Position} · " +
                    (plan.Autoplay ? "départ initial planifié" : "attente d’un événement"),
                    9, StudioStyle.Text, wrap: true));
        _summary.Text = preview.Summary.Length > 0 ? preview.Summary : preview.Message;
        _summary.Modulate = preview.Succeeded ? StudioStyle.Text : StudioStyle.Warning;
    }

    private void ResetReview()
    {
        ClearChildren(_diff); ClearChildren(_diagnostics); ClearChildren(_plans);
        _diff.AddChild(StudioStyle.Label("Aucun aperçu sec.", 9, StudioStyle.Muted));
        _diagnostics.AddChild(StudioStyle.Label("Validation en attente.", 9, StudioStyle.Muted));
        _plans.AddChild(StudioStyle.Label("Aucun plan calculé.", 9, StudioStyle.Muted));
        _summary.Text = _draft is null
            ? "Sélectionnez une zone puis prévisualisez · aucun changement appliqué."
            : "Fiche chargée · aucun changement appliqué.";
        _summary.Modulate = StudioStyle.Muted;
    }

    private void UpdateActionState()
    {
        _preview.Disabled = _draft is null;
        _apply.Disabled = _lastPreview is not { Succeeded: true, CanApply: true };
        _undo.Disabled = _session.History?.CanUndo != true;
        _redo.Disabled = _session.History?.CanRedo != true;
        _save.Disabled = !_session.IsDirty;
    }

    private void SetEditingEnabled(bool enabled)
    {
        _definitions.SetEditingEnabled(enabled); _instances.SetEditingEnabled(enabled);
    }

    private IReadOnlyList<StudioUx2EnvFMixBusChoice> CurrentMixBuses() =>
        _draft?.MixBuses ?? [new StudioUx2EnvFMixBusChoice(null, "Aucun bus dédié · Master au runtime")];

    private IReadOnlyList<KeyValuePair<Guid, string>> CurrentDefinitionChoices()
    {
        var definitions = _definitions.Values;
        var duplicateNames = definitions.GroupBy(item => HumanLabelUx2EnvF(item.Name),
                StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.CurrentCultureIgnoreCase);
        var occurrences = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);
        return definitions.Select(item =>
        {
            var name = HumanLabelUx2EnvF(item.Name);
            occurrences.TryGetValue(name, out var occurrence); occurrence++; occurrences[name] = occurrence;
            var label = duplicateNames.TryGetValue(name, out var count) && count > 1
                ? $"{name} · occurrence {occurrence}"
                : name;
            return new KeyValuePair<Guid, string>(item.Id, label);
        }).ToArray();
    }

    private StudioUx2EnvFDefinitionDraft NewDefinition()
    {
        var index = _definitions.Values.Count + 1;
        return new StudioUx2EnvFDefinitionDraft(
            Guid.NewGuid(), AudioEmitterDefinition.CurrentSchemaVersion, $"Nouvel émetteur {index}",
            Guid.NewGuid(), AudioClipSet.CurrentSchemaVersion, $"Clips de l’émetteur {index}",
            AudioClipSelectionMode.Ordered,
            [new StudioUx2EnvFClipDraft(Guid.NewGuid(), "builtin://openrpg/audio/new-clip", "1",
                ["ambiance"], "Variante principale", "fr", "-18", string.Empty, "audio/ogg", "262144",
                "CC0-1.0", "studio:ux2-env-f", new string('d', 64), string.Empty, [], "0")],
            "studio:ux2-env-f", AudioLoopMode.OneShot, "0", "0", "0", "0", string.Empty,
            "1", "25", "inverse-square", "360", "-12", "0", "0", true, null, "0",
            string.Empty, string.Empty, false, "studio:ux2-env-f", "assets/audio-manifest.json",
            "CC0-1.0", new string('e', 64));
    }

    private StudioUx2EnvFInstanceDraft NewInstance()
    {
        var definitionId = _definitions.Values.FirstOrDefault()?.Id ?? Guid.Empty;
        return new StudioUx2EnvFInstanceDraft(
            Guid.NewGuid(), definitionId, "0", "0", "0", "0", "0", "0", "1", "1", "1", "1",
            true, "studio:ux2-env-f");
    }

    private bool ConfigureEvidenceDraft()
    {
        var draft = CaptureDraft();
        if (draft is null || draft.Definitions.Count == 0 || draft.Definitions[0].Clips.Count == 0 ||
            draft.Instances.Count == 0) return false;
        draft = draft with
        {
            Definitions = draft.Definitions.Select((item, index) => index == 0
                ? item with
                {
                    Name = HumanLabelUx2EnvF(item.Name) + " · scène nord",
                    Clips = item.Clips.Select((clip, clipIndex) => clipIndex == 0
                        ? clip with { Weight = "2.5" }
                        : clip).ToArray(),
                    LoopMode = AudioLoopMode.FiniteCount,
                    RepeatCount = "3",
                    RegionStartSeconds = "0",
                    RegionEndSeconds = "0",
                    CrossfadeSeconds = "0",
                }
                : item).ToArray(),
            Instances = draft.Instances.Select((item, index) => index == 0
                ? item with { PositionX = "11", PositionY = "3.25", PositionZ = "-7" }
                : item).ToArray(),
        };
        _draft = draft; Populate(draft); return true;
    }

    private StudioUx2EnvFEvidenceSourceSnapshot CaptureSourceSnapshot()
    {
        var project = _sourceSession.Project ?? throw new InvalidOperationException("Source project is missing.");
        var history = _sourceSession.History ?? throw new InvalidOperationException("Source history is missing.");
        return new StudioUx2EnvFEvidenceSourceSnapshot(
            project,
            history,
            history.State.Revision,
            history.Journal.ToArray(),
            _sourceSession.IsDirty,
            history.CanUndo,
            history.CanRedo,
            _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(),
            _sourceSession.SelectedEntityId,
            _sourceSession.CaptureAudioEmittersApprovalUx2EnvF());
    }

    private bool SourceSnapshotMatches(StudioUx2EnvFEvidenceSourceSnapshot? expected = null)
    {
        var snapshot = expected ?? _evidenceSourceSnapshot;
        if (snapshot is null) return true;
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               history?.CanUndo == snapshot.CanUndo && history?.CanRedo == snapshot.CanRedo &&
               _sourceSession.IsDirty == snapshot.IsDirty && _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureAudioEmittersApprovalUx2EnvF() == snapshot.Approval;
    }

    private bool ReleaseIsolatedEvidenceSession()
    {
        var evidenceSession = _evidenceSession;
        var tempRoot = _evidenceTempRoot;
        if (evidenceSession is null && tempRoot is null)
        {
            _session = _sourceSession; return true;
        }
        var released = true;
        try { evidenceSession?.Close(); }
        catch (Exception exception)
        {
            released = false;
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_F_RELEASE_FAILED " +
                        $"type={exception.GetType().Name} message={exception.Message}");
        }
        var rootReleased = StudioSessionController.DeleteUx2EnvFTemporaryWorkspace(tempRoot);
        ResetEvidenceState();
        if (!rootReleased) _evidenceTempRoot = tempRoot;
        _session = _sourceSession; _evidenceReleaseSucceeded = released && rootReleased;
        return _evidenceReleaseSucceeded;
    }

    private void ResetEvidenceState()
    {
        _evidenceSession = null; _evidenceTempRoot = null; _evidenceSourceSnapshot = null;
        _evidenceUsesRealModuleData = false; _evidenceUsesCanonicalFallback = false;
    }

    private void SetStatus(string text, bool error = false)
    {
        _status.Text = text; _status.Modulate = error ? StudioStyle.Warning : StudioStyle.Muted;
        StatusChanged?.Invoke(text, error);
    }

    private void UpdateDialogBounds()
    {
        var viewport = GetViewportRect().Size; if (viewport.X <= 0 || viewport.Y <= 0) return;
        var scale = viewport.X >= 3_200 ? 1.7f : viewport.X >= 2_300 ? 1.23f : 1f;
        var width = Math.Clamp((viewport.X - 52f) / scale, 1_120f, 1_680f);
        var height = Math.Clamp((viewport.Y - 42f) / scale, 760f, 1_060f);
        _dialog.Scale = Vector2.One * scale; _dialog.CustomMaximumSize = new Vector2(width, height);
        _dialog.Size = new Vector2(width, height);
        _dialog.Position = new Vector2((viewport.X - width * scale) / 2f,
            Math.Max(18f, (viewport.Y - height * scale) * .45f));
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && Visible) UpdateDialogBounds();
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

    private static string HumanLabelUx2EnvF(string? value)
    {
        var text = (value ?? string.Empty).Replace('-', ' ').Replace('_', ' ').Trim();
        return string.IsNullOrWhiteSpace(text) || ContainsUuidUx2EnvF(text) ? "élément sans nom lisible" : text;
    }

    private static void ClearChildren(Node node)
    {
        foreach (var child in node.GetChildren()) child.QueueFree();
    }

    private static IEnumerable<string> DescendantVisibleText(Node root)
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
            foreach (var descendant in DescendantVisibleText(child)) yield return descendant;
        }
    }

    private static bool ContainsUuidUx2EnvF(string text) => System.Text.RegularExpressions.Regex.IsMatch(
        text,
        @"\b(?:[0-9a-f]{32}|[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\b",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase |
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);
}
