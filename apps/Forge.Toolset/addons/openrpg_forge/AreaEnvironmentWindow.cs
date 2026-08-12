#if TOOLS
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Areas;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Audio;
using Forge.Domain.Spatial;
using Godot;

namespace Forge.Toolset.Editor;

public partial class AreaEnvironmentWindow : Window
{
    private Func<ProjectState?>? _state;
    private Func<AreaId?>? _active;
    private Func<AreaId, AreaEnvironmentProfile, bool>? _apply;
    private Action<Window>? _brand;
    private AreaEnvironmentProfile? _current;

    private OptionButton? _classification;
    private LineEdit? _tags;
    private OptionButton? _dayNight;
    private SpinBox? _dayLength;
    private SpinBox? _ambient;
    private SpinBox? _sun;
    private SpinBox? _temperature;
    private OptionButton? _toneMapping;
    private OptionButton? _quality;
    private SpinBox? _exposure;
    private SpinBox? _whitePoint;
    private CheckBox? _hdrOutput;
    private CheckBox? _forwardPlus;
    private CheckBox? _perPixel;
    private CheckBox? _materialInstances;
    private SpinBox? _dynamicLights;
    private SpinBox? _shadowLights;
    private SpinBox? _shadowAtlas;
    private OptionButton? _skyMode;
    private LineEdit? _panorama;
    private LineEdit? _zenith;
    private LineEdit? _horizon;
    private LineEdit? _ground;
    private SpinBox? _stars;
    private SpinBox? _clouds;
    private SpinBox? _moon;
    private SpinBox? _skyEnergy;
    private SpinBox? _skyTransition;
    private OptionButton? _fogMode;
    private SpinBox? _fogDensity;
    private SpinBox? _fogStart;
    private SpinBox? _fogEnd;
    private SpinBox? _windDirectionX;
    private SpinBox? _windDirectionZ;
    private SpinBox? _windSpeed;
    private SpinBox? _windGust;
    private SpinBox? _windInterval;
    private CheckBox? _windVegetation;
    private CheckBox? _windParticles;
    private LineEdit? _windAudio;
    private OptionButton? _weatherKind;
    private SpinBox? _weatherIntensity;
    private SpinBox? _weatherHumidity;
    private SpinBox? _weatherWetness;
    private CheckBox? _weatherLightning;
    private SpinBox? _weatherMinimumDuration;
    private SpinBox? _weatherMaximumDuration;
    private SpinBox? _weatherTransition;
    private LineEdit? _weatherAudio;
    private LineEdit? _weatherGameplay;
    private CheckBox? _weatherAuthority;
    private CheckBox? _weatherPersist;
    private CheckBox? _weatherReplicate;
    private CheckBox? _waterEnabled;
    private OptionButton? _waterKind;
    private SpinBox? _waterCenterX;
    private SpinBox? _waterCenterY;
    private SpinBox? _waterCenterZ;
    private SpinBox? _waterSizeX;
    private SpinBox? _waterSizeY;
    private SpinBox? _waterSizeZ;
    private SpinBox? _waterDepth;
    private SpinBox? _waterFlow;
    private SpinBox? _waterBuoyancy;
    private SpinBox? _waterNavigationCost;
    private CheckBox? _waterSwimming;
    private OptionButton? _waterQuality;
    private LineEdit? _waterAudio;
    private CheckBox? _reverbZoneEnabled;
    private SpinBox? _reverbWet;
    private SpinBox? _reverbDry;
    private SpinBox? _reverbBlend;
    private SpinBox? _reverbSizeX;
    private SpinBox? _reverbSizeY;
    private SpinBox? _reverbSizeZ;
    private LineEdit? _dayMusic;
    private LineEdit? _nightMusic;
    private LineEdit? _combatMusic;
    private LineEdit? _ambience;
    private LineEdit? _reverb;
    private LineEdit? _playlist;
    private OptionButton? _audioOrder;
    private SpinBox? _audioInterval;
    private SpinBox? _audioVariation;
    private SpinBox? _audioVolume;
    private LineEdit? _audioSearch;
    private Label? _audioSearchResults;
    private Label? _audioWaveform;
    private OptionButton? _r061Order;
    private SpinBox? _audioSeed;
    private SpinBox? _audioNoRepeat;
    private LineEdit? _audioWeights;
    private OptionButton? _audioDistribution;
    private LineEdit? _audioCurve;
    private SpinBox? _audioMinimumInterval;
    private SpinBox? _audioMaximumInterval;
    private SpinBox? _audioMinimumPitch;
    private SpinBox? _audioMaximumPitch;
    private CheckBox? _audioTempoSync;
    private SpinBox? _audioTempo;
    private SpinBox? _audioBeatDivision;
    private SpinBox? _audioTargetLoudness;
    private LineEdit? _audioBus;
    private SpinBox? _audioDucking;
    private SpinBox? _audioAccessibilityCap;
    private OptionButton? _audioMeter;
    private LineEdit? _audioAreaScope;
    private OptionButton? _audioListenerPolicy;
    private LineEdit? _audioListenerGains;
    private CheckBox? _audioRequired;
    private Label? _audioStatisticalPreview;
    private AudioStreamPlayer? _audioAuditionPlayer;
    private OptionButton? _soundMode;
    private SpinBox? _innerRadius;
    private SpinBox? _outerRadius;
    private CheckBox? _audioDebugRanges;
    private Label? _audioAttenuationPreview;
    private CheckBox? _initiallyActive;
    private OptionButton? _instancePolicy;
    private OptionButton? _authority;
    private Label? _audioRuntimePreview;
    private LineEdit? _adaptiveResource;
    private LineEdit? _adaptiveWeather;
    private LineEdit? _adaptiveTag;
    private SpinBox? _adaptiveCrossfade;
    private LineEdit? _adaptiveVolumeId;
    private CheckBox? _musicBeatSync;
    private SpinBox? _musicTempo;
    private SpinBox? _musicBeatsPerBar;
    private CheckBox? _mixMonitor;
    private Label? _mixMonitorPreview;
    private LineEdit? _captionResource;
    private LineEdit? _captionFr;
    private CheckBox? _captionDirection;
    private OptionButton? _captionKind;
    private OptionButton? _captionImportance;
    private CheckBox? _captionRequired;
    private Label? _captionState;
    private LineEdit? _emitterName;
    private LineEdit? _emitterResource;
    private LineEdit? _emitterPreview;
    private LineEdit? _emitterTags;
    private LineEdit? _emitterLocale;
    private LineEdit? _emitterLicense;
    private LineEdit? _emitterProvenance;
    private LineEdit? _emitterManifest;
    private LineEdit? _emitterHash;
    private LineEdit? _emitterTranscript;
    private LineEdit? _emitterWaveform;
    private SpinBox? _emitterDuration;
    private OptionButton? _emitterSelection;
    private SpinBox? _emitterWeight;
    private SpinBox? _emitterLoudness;
    private SpinBox? _emitterMemory;
    private OptionButton? _emitterLoopMode;
    private SpinBox? _emitterRepeatCount;
    private SpinBox? _emitterLoopStart;
    private SpinBox? _emitterLoopEnd;
    private SpinBox? _emitterCrossfade;
    private LineEdit? _emitterStopEvent;
    private LineEdit? _emitterActivatedScript;
    private LineEdit? _emitterCompletedScript;
    private SpinBox? _emitterPositionX;
    private SpinBox? _emitterPositionY;
    private SpinBox? _emitterPositionZ;
    private SpinBox? _emitterInnerRadius;
    private SpinBox? _emitterOuterRadius;
    private SpinBox? _emitterCone;
    private SpinBox? _emitterOcclusion;
    private SpinBox? _emitterHeight;
    private CheckBox? _emitterVirtualize;
    private CheckBox? _emitterRequired;
    private Label? _emitterState;
    private LineEdit? _classificationKey;
    private LineEdit? _classificationPack;
    private CheckBox? _classificationRendering;
    private CheckBox? _classificationAudio;
    private CheckBox? _classificationNavigation;
    private CheckBox? _classificationWeather;
    private CheckBox? _classificationCamera;
    private CheckBox? _classificationGameplay;
    private LineEdit? _lightingPreset;
    private LineEdit? _ambientColor;
    private LineEdit? _directionalColor;
    private CheckBox? _simpleLighting;
    private SpinBox? _noonSun;
    private SpinBox? _midnightMoon;
    private SpinBox? _timedEventHour;
    private LineEdit? _timedEventName;
    private LineEdit? _timedEventScript;
    private CheckBox? _probeEnabled;
    private LineEdit? _probeAsset;
    private SpinBox? _probeRadius;
    private CheckBox? _lightingVolumeEnabled;
    private LineEdit? _volumeLightingPreset;
    private SpinBox? _lightingVolumeBlend;
    private LineEdit? _fogDayColor;
    private LineEdit? _fogNightColor;
    private LineEdit? _fogNoiseAsset;
    private SpinBox? _fogNoiseScale;
    private CheckBox? _localFogEnabled;
    private SpinBox? _localFogDensity;
    private LineEdit? _localFogColor;
    private OptionButton? _fogQuality;
    private CheckBox? _environmentRequired;
    private Label? _r054Preview;
    private TabContainer? _tabs;
    private string? _capturePath;
    private double _captureDelay;
    private bool _captureOpened;
    private bool _r054Capture;
    private bool _r061Capture;
    private bool _r062Capture;
    private bool _r063Capture;
    private bool _syncingAudioControls;
    private AudioPlaybackControlState? _previewControlState;
    private Label? _status;

    internal string RenderSummary => _current is null
        ? "unavailable"
        : $"{_current.Rendering.MaterialWorkflow}:{_current.Rendering.ToneMapping}:{_current.Sky.Mode}";

    public void Initialize(
        Func<ProjectState?> state,
        Func<AreaId?> active,
        Func<AreaId, AreaEnvironmentProfile, bool> apply,
        Action<Window> brand)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _active = active ?? throw new ArgumentNullException(nameof(active));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _brand = brand ?? throw new ArgumentNullException(nameof(brand));
        Title = "Environnement de zone · OpenRPG Forge";
        MinSize = new Vector2I(1240, 760);
        Size = new Vector2I(1320, 820);
        Transient = true;
        Visible = false;
        Build();
        var captureArgument = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--audio-emitter-ui-capture=", StringComparison.Ordinal));
        var r054CaptureArgument = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--r054-area-environment-ui-capture=", StringComparison.Ordinal));
        var r061CaptureArgument = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--r061-audio-workbench-ui-capture=", StringComparison.Ordinal));
        var r062CaptureArgument = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--r062-audio-orchestration-ui-capture=", StringComparison.Ordinal));
        var r063CaptureArgument = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--r063-audio-caption-ui-capture=", StringComparison.Ordinal));
        if (r063CaptureArgument is not null)
        {
            _capturePath = r063CaptureArgument["--r063-audio-caption-ui-capture=".Length..];
            _r063Capture = true;
            SetProcess(true);
            return;
        }
        if (r062CaptureArgument is not null)
        {
            _capturePath = r062CaptureArgument["--r062-audio-orchestration-ui-capture=".Length..];
            _r062Capture = true;
            SetProcess(true);
            return;
        }
        if (r061CaptureArgument is not null)
        {
            _capturePath = r061CaptureArgument["--r061-audio-workbench-ui-capture=".Length..];
            _r061Capture = true;
            SetProcess(true);
            return;
        }
        if (r054CaptureArgument is not null)
        {
            _capturePath = r054CaptureArgument["--r054-area-environment-ui-capture=".Length..];
            _r054Capture = true;
            SetProcess(true);
            return;
        }
        if (captureArgument is not null)
        {
            _capturePath = captureArgument["--audio-emitter-ui-capture=".Length..];
            SetProcess(true);
        }
    }

    public override void _Process(double delta)
    {
        if (_capturePath is null) return;
        _captureDelay += delta;
        if (!_captureOpened && _captureDelay >= 1.0)
        {
            Open();
            _captureOpened = true;
            if (_r054Capture)
            {
                _tabs!.CurrentTab = 5;
                PrepareR054FieldsForCapture();
                return;
            }
            if (_r061Capture)
            {
                _tabs!.CurrentTab = 6;
                PrepareR061FieldsForCapture();
                return;
            }
            if (_r062Capture)
            {
                _tabs!.CurrentTab = 7;
                PrepareR062FieldsForCapture();
                return;
            }
            if (_r063Capture)
            {
                _tabs!.CurrentTab = 3;
                PrepareR063FieldsForCapture();
                return;
            }
            _tabs!.CurrentTab = 4;
            _emitterName!.Text = "Cascade ancienne";
            _emitterResource!.Text = "audio/waterfall.ogg";
            _emitterTags!.Text = "eau, ambiance";
            _emitterLocale!.Text = "fr";
            _emitterLicense!.Text = "CC0-1.0";
            _emitterProvenance!.Text = "asset-vault:waterfall";
            _emitterManifest!.Text = "assets/asset-manifests.json";
            _emitterHash!.Text = new string('a', 64);
            _emitterLoopMode!.Selected = (int)AudioLoopMode.SeamlessRegion;
            _emitterLoopStart!.Value = 1;
            _emitterLoopEnd!.Value = 8;
            _emitterCrossfade!.Value = .25;
            _emitterPositionX!.Value = 6;
            _emitterPositionY!.Value = 2;
            _emitterPositionZ!.Value = -5;
            _emitterState!.Text = "✓ Aperçu canonique · prêt à valider et appliquer";
            return;
        }
        if (!_captureOpened || _captureDelay < 2.5) return;
        var destination = Path.GetFullPath(_capturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(destination);
        var marker = _r063Capture
            ? "OPENRPG_FORGE_R063_AUDIO_CAPTION_UI_CAPTURE"
            : _r062Capture
            ? "OPENRPG_FORGE_R062_AUDIO_ORCHESTRATION_UI_CAPTURE"
            : _r061Capture
            ? "OPENRPG_FORGE_R061_AUDIO_WORKBENCH_UI_CAPTURE"
            : _r054Capture
                ? "OPENRPG_FORGE_R054_AREA_ENVIRONMENT_UI_CAPTURE"
                : "OPENRPG_FORGE_AUDIO_EMITTER_UI_CAPTURE";
        GD.Print($"{marker}_{(error == Error.Ok ? "PASS" : "FAILED")} " +
            $"path={destination} size={image.GetWidth()}x{image.GetHeight()}");
        _capturePath = null;
        SetProcess(false);
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    public void Open()
    {
        Refresh();
        PopupCentered(Size);
        _brand?.Invoke(this);
    }

    internal bool ConfigureRenderForSmokeTest(AreaEnvironmentProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _current = profile;
        Populate(profile);
        return true;
    }

    internal bool ApplyForSmokeTest() => Save();

    internal bool PrepareR054ForSmokeTest()
    {
        if (_current is null) return false;
        PrepareR054FieldsForCapture();
        return true;
    }

    private void Build()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 20);
        }

        AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);
        var header = new Label { Text = "ENVIRONNEMENT DE ZONE", ThemeTypeVariation = "HeaderSmall" };
        header.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(header);
        root.AddChild(new Label
        {
            Text = "Rendu, ciel, vent, météo, eau et audio restent dans un profil canonique annulable.",
        });

        _tabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        root.AddChild(_tabs);
        BuildRenderingTab(_tabs);
        BuildFogTab(_tabs);
        BuildClimateTab(_tabs);
        BuildAudioTab(_tabs);
        BuildAudioEmitterTab(_tabs);
        BuildR054EnvironmentTab(_tabs);
        BuildR061AudioWorkbenchTab(_tabs);
        BuildR062AudioOrchestrationTab(_tabs);

        _status = new Label { Text = "Prêt · prévisualisez les budgets avant application." };
        _status.AddThemeColorOverride("font_color", new Color("9bd9bd"));
        root.AddChild(_status);
        var actions = new HBoxContainer();
        actions.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var apply = new Button { Text = "Appliquer le profil", CustomMinimumSize = new Vector2(190, 40) };
        apply.Pressed += () => Save();
        actions.AddChild(apply);
        var close = new Button { Text = "Fermer", CustomMinimumSize = new Vector2(110, 40) };
        close.Pressed += Hide;
        actions.AddChild(close);
        root.AddChild(actions);
    }

    private void BuildRenderingTab(TabContainer tabs)
    {
        var scroll = Tab(tabs, "Rendu & ciel");
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 14);
        scroll.AddChild(columns);

        var area = Card(columns, "ZONE & LUMIÈRE");
        _classification = EnumOption<AreaEnvironmentClass>();
        area.AddChild(FieldRow("Classification", _classification));
        _tags = Field("Tags : cave, naturel…");
        area.AddChild(_tags);
        _dayNight = EnumOption<DayNightMode>();
        area.AddChild(FieldRow("Cycle", _dayNight));
        _dayLength = Spin(1, 1440, 24, 1);
        area.AddChild(FieldRow("Durée du jour (min)", _dayLength));
        _ambient = Spin(0, 20, .35, .05);
        area.AddChild(FieldRow("Ambiante", _ambient));
        _sun = Spin(0, 50, 1, .05);
        area.AddChild(FieldRow("Directionnelle", _sun));
        _temperature = Spin(1000, 20000, 6500, 100);
        area.AddChild(FieldRow("Température K", _temperature));

        var render = Card(columns, "PBR · HDR · BUDGETS");
        render.AddChild(new Label { Text = "Workflow · Metallic / Roughness" });
        _toneMapping = EnumOption<ToneMappingProfile>();
        render.AddChild(FieldRow("Tone mapper", _toneMapping));
        _quality = EnumOption<RenderQualityTier>();
        render.AddChild(FieldRow("Qualité", _quality));
        _exposure = Spin(.05, 8, 1, .05);
        render.AddChild(FieldRow("Exposition", _exposure));
        _whitePoint = Spin(1, 32, 6, .25);
        render.AddChild(FieldRow("Point blanc", _whitePoint));
        _hdrOutput = Check("Sortie HDR", true);
        _forwardPlus = Check("Forward+", true);
        _perPixel = Check("Éclairage par pixel", true);
        _materialInstances = Check("Instances de matériaux", true);
        render.AddChild(_hdrOutput);
        render.AddChild(_forwardPlus);
        render.AddChild(_perPixel);
        render.AddChild(_materialInstances);
        _dynamicLights = Spin(0, 4096, 32, 1);
        render.AddChild(FieldRow("Lumières dynamiques", _dynamicLights));
        _shadowLights = Spin(0, 4096, 8, 1);
        render.AddChild(FieldRow("Avec ombres", _shadowLights));
        _shadowAtlas = Spin(512, 16384, 4096, 512);
        render.AddChild(FieldRow("Atlas d’ombres", _shadowAtlas));

        var sky = Card(columns, "CIEL DE RÉFÉRENCE");
        _skyMode = EnumOption<SkyProfileMode>();
        sky.AddChild(FieldRow("Mode", _skyMode));
        _panorama = Field("HDRI relatif · skies/dawn.exr");
        sky.AddChild(_panorama);
        _zenith = Field("Zénith · #4c78a8");
        sky.AddChild(_zenith);
        _horizon = Field("Horizon · #e4b88a");
        sky.AddChild(_horizon);
        _ground = Field("Sol · #38402f");
        sky.AddChild(_ground);
        _stars = Spin(0, 4, .15, .05);
        sky.AddChild(FieldRow("Étoiles", _stars));
        _clouds = Spin(0, 1, .2, .05);
        sky.AddChild(FieldRow("Nuages", _clouds));
        _moon = Spin(0, 4, .1, .05);
        sky.AddChild(FieldRow("Lune", _moon));
        _skyEnergy = Spin(.05, 8, 1, .05);
        sky.AddChild(FieldRow("Énergie", _skyEnergy));
        _skyTransition = Spin(0, 3600, 2, .25);
        sky.AddChild(FieldRow("Transition (s)", _skyTransition));
    }

    private void BuildFogTab(TabContainer tabs)
    {
        var scroll = Tab(tabs, "Brouillard");
        var card = Card(scroll, "PROFIL ATMOSPHÉRIQUE");
        _fogMode = EnumOption<FogMode>();
        card.AddChild(FieldRow("Mode", _fogMode));
        _fogDensity = Spin(0, 1, 0, .01);
        card.AddChild(FieldRow("Densité", _fogDensity));
        _fogStart = Spin(0, 10000, 20, 1);
        card.AddChild(FieldRow("Début", _fogStart));
        _fogEnd = Spin(0, 10000, 100, 1);
        card.AddChild(FieldRow("Fin", _fogEnd));
    }

    private void BuildClimateTab(TabContainer tabs)
    {
        var scroll = Tab(tabs, "Climat & eau");
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 14);
        scroll.AddChild(columns);

        var wind = Card(columns, "VENT GLOBAL");
        _windDirectionX = Spin(-1, 1, .8, .05);
        _windDirectionZ = Spin(-1, 1, .3, .05);
        _windSpeed = Spin(0, 100, 3, .25);
        _windGust = Spin(0, 8, .35, .05);
        _windInterval = Spin(.1, 3600, 8, .5);
        wind.AddChild(FieldRow("Direction X", _windDirectionX));
        wind.AddChild(FieldRow("Direction Z", _windDirectionZ));
        wind.AddChild(FieldRow("Vitesse", _windSpeed));
        wind.AddChild(FieldRow("Rafales", _windGust));
        wind.AddChild(FieldRow("Intervalle", _windInterval));
        _windVegetation = Check("Influence la végétation", true);
        _windParticles = Check("Influence les particules", true);
        _windAudio = Field("Cue audio · wind.light");
        wind.AddChild(_windVegetation);
        wind.AddChild(_windParticles);
        wind.AddChild(_windAudio);

        var weather = Card(columns, "MÉTÉO RUNTIME");
        _weatherKind = EnumOption<WeatherKind>();
        weather.AddChild(FieldRow("État initial", _weatherKind));
        _weatherIntensity = Spin(0, 1, 0, .05);
        _weatherHumidity = Spin(0, 1, .35, .05);
        _weatherWetness = Spin(0, 1, 0, .05);
        weather.AddChild(FieldRow("Intensité", _weatherIntensity));
        weather.AddChild(FieldRow("Humidité", _weatherHumidity));
        weather.AddChild(FieldRow("Sol humide", _weatherWetness));
        _weatherLightning = Check("Éclairs", false);
        weather.AddChild(_weatherLightning);
        _weatherMinimumDuration = Spin(0, 86400, 180, 5);
        _weatherMaximumDuration = Spin(0, 86400, 420, 5);
        _weatherTransition = Spin(0, 3600, 12, .5);
        weather.AddChild(FieldRow("Durée min", _weatherMinimumDuration));
        weather.AddChild(FieldRow("Durée max", _weatherMaximumDuration));
        weather.AddChild(FieldRow("Transition", _weatherTransition));
        _weatherAudio = Field("Cue audio · weather.rain");
        _weatherGameplay = Field("Effet gameplay · surface.wet");
        weather.AddChild(_weatherAudio);
        weather.AddChild(_weatherGameplay);
        _weatherAuthority = Check("Autorité serveur", true);
        _weatherPersist = Check("Persister dans la sauvegarde", true);
        _weatherReplicate = Check("Répliquer en réseau", true);
        weather.AddChild(_weatherAuthority);
        weather.AddChild(_weatherPersist);
        weather.AddChild(_weatherReplicate);

        var water = Card(columns, "EAU & RÉVERBÉRATION");
        _waterEnabled = Check("Plan d’eau actif", false);
        water.AddChild(_waterEnabled);
        _waterKind = EnumOption<WaterBodyKind>();
        water.AddChild(FieldRow("Type", _waterKind));
        _waterCenterX = Spin(-4096, 4096, 0, .5);
        _waterCenterY = Spin(-4096, 4096, 0, .25);
        _waterCenterZ = Spin(-4096, 4096, 0, .5);
        water.AddChild(FieldRow("Centre X", _waterCenterX));
        water.AddChild(FieldRow("Hauteur Y", _waterCenterY));
        water.AddChild(FieldRow("Centre Z", _waterCenterZ));
        _waterSizeX = Spin(.1, 4096, 20, .5);
        _waterSizeY = Spin(.1, 4096, 2, .25);
        _waterSizeZ = Spin(.1, 4096, 15, .5);
        water.AddChild(FieldRow("Largeur", _waterSizeX));
        water.AddChild(FieldRow("Volume Y", _waterSizeY));
        water.AddChild(FieldRow("Profondeur Z", _waterSizeZ));
        _waterDepth = Spin(.1, 4096, 2, .25);
        _waterFlow = Spin(0, 100, 0, .1);
        _waterBuoyancy = Spin(0, 10, 1, .1);
        _waterNavigationCost = Spin(.1, 100, 2, .1);
        water.AddChild(FieldRow("Profondeur", _waterDepth));
        water.AddChild(FieldRow("Courant", _waterFlow));
        water.AddChild(FieldRow("Flottabilité", _waterBuoyancy));
        water.AddChild(FieldRow("Coût navigation", _waterNavigationCost));
        _waterSwimming = Check("Nage autorisée", true);
        water.AddChild(_waterSwimming);
        _waterQuality = EnumOption<WaterQualityTier>();
        water.AddChild(FieldRow("Qualité", _waterQuality));
        _waterAudio = Field("Cue audio · water.lake");
        water.AddChild(_waterAudio);
        _reverbZoneEnabled = Check("Zone de réverbération", false);
        water.AddChild(_reverbZoneEnabled);
        _reverbWet = Spin(0, 1, .55, .05);
        _reverbDry = Spin(0, 1, 1, .05);
        _reverbBlend = Spin(0, 60, 1, .1);
        _reverbSizeX = Spin(.1, 4096, 12, .5);
        _reverbSizeY = Spin(.1, 4096, 6, .5);
        _reverbSizeZ = Spin(.1, 4096, 12, .5);
        water.AddChild(FieldRow("Réverb humide", _reverbWet));
        water.AddChild(FieldRow("Réverb sec", _reverbDry));
        water.AddChild(FieldRow("Fondu réverb", _reverbBlend));
        water.AddChild(FieldRow("Zone X", _reverbSizeX));
        water.AddChild(FieldRow("Zone Y", _reverbSizeY));
        water.AddChild(FieldRow("Zone Z", _reverbSizeZ));
    }

    private void BuildR054EnvironmentTab(TabContainer tabs)
    {
        var scroll = Tab(tabs, "Classification & atmosphère R054");
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 14);
        scroll.AddChild(columns);

        var classification = Card(columns, "CLASSIFICATION EXTENSIBLE");
        _classificationKey = Field("custom:moonlit-forest");
        _classificationPack = Field("Pack · forest-environments");
        classification.AddChild(_classificationKey);
        classification.AddChild(_classificationPack);
        _classificationRendering = Check("Pilote rendu", true);
        _classificationAudio = Check("Pilote audio", true);
        _classificationNavigation = Check("Pilote navigation", true);
        _classificationWeather = Check("Pilote météo", true);
        _classificationCamera = Check("Pilote caméra", true);
        _classificationGameplay = Check("Pilote gameplay", true);
        foreach (var item in new[] { _classificationRendering, _classificationAudio, _classificationNavigation,
                     _classificationWeather, _classificationCamera, _classificationGameplay })
            classification.AddChild(item);
        _environmentRequired = Check("Métadonnées requises au runtime", false);
        classification.AddChild(_environmentRequired);

        var lighting = Card(columns, "JOUR/NUIT · LUMIÈRE PHYSIQUE");
        _lightingPreset = Field("Preset · physical-moonlit");
        _ambientColor = Field("Ambiante · #30405f");
        _directionalColor = Field("Directionnelle · #ffe2b5");
        lighting.AddChild(_lightingPreset);
        lighting.AddChild(_ambientColor);
        lighting.AddChild(_directionalColor);
        _simpleLighting = Check("Contrôles simplifiés", false);
        lighting.AddChild(_simpleLighting);
        _noonSun = Spin(0, 20, 1.4, .05);
        _midnightMoon = Spin(0, 20, .8, .05);
        lighting.AddChild(FieldRow("Soleil à midi", _noonSun));
        lighting.AddChild(FieldRow("Lune à minuit", _midnightMoon));
        _timedEventHour = Spin(0, 23.99, 18, .25);
        _timedEventName = Field("Événement · world.sunset");
        _timedEventScript = Field("ForgeScript · scripts/sunset.forge");
        lighting.AddChild(FieldRow("Heure événement", _timedEventHour));
        lighting.AddChild(_timedEventName);
        lighting.AddChild(_timedEventScript);
        _probeEnabled = Check("Probe de réflexion", true);
        _probeAsset = Field("AssetId · asset:forest-probe");
        _probeRadius = Spin(.1, 4096, 24, .5);
        lighting.AddChild(_probeEnabled);
        lighting.AddChild(_probeAsset);
        lighting.AddChild(FieldRow("Rayon probe", _probeRadius));
        _lightingVolumeEnabled = Check("Override local de lumière", true);
        _volumeLightingPreset = Field("Preset local · physical-shrine");
        _lightingVolumeBlend = Spin(0, 128, 3, .25);
        lighting.AddChild(_lightingVolumeEnabled);
        lighting.AddChild(_volumeLightingPreset);
        lighting.AddChild(FieldRow("Fondu volume", _lightingVolumeBlend));

        var fog = Card(columns, "BROUILLARD GOUVERNÉ");
        _fogDayColor = Field("Jour · #b7c9dd");
        _fogNightColor = Field("Nuit · #263248");
        _fogNoiseAsset = Field("Bruit · asset:fog-noise");
        fog.AddChild(_fogDayColor);
        fog.AddChild(_fogNightColor);
        fog.AddChild(_fogNoiseAsset);
        _fogNoiseScale = Spin(.1, 4096, 12, .5);
        fog.AddChild(FieldRow("Échelle bruit", _fogNoiseScale));
        _localFogEnabled = Check("Volume local", true);
        _localFogDensity = Spin(0, 1, .3, .05);
        _localFogColor = Field("Couleur locale · #9fb4c8");
        fog.AddChild(_localFogEnabled);
        fog.AddChild(FieldRow("Densité locale", _localFogDensity));
        fog.AddChild(_localFogColor);
        _fogQuality = EnumOption<AreaFogQualityTier>();
        fog.AddChild(FieldRow("Qualité desktop", _fogQuality));
        _r054Preview = new Label
        {
            Text = "Aperçu vide · configurez classification, lumière et brouillard.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _r054Preview.AddThemeColorOverride("font_color", new Color("9bd9bd"));
        fog.AddChild(_r054Preview);
    }

    private void BuildAudioTab(TabContainer tabs)
    {
        var scroll = Tab(tabs, "Audio");
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 14);
        scroll.AddChild(columns);
        var music = Card(columns, "MUSIQUE & AMBIANCE");
        _dayMusic = Field("Musique jour");
        _nightMusic = Field("Musique nuit");
        _combatMusic = Field("Musique combat");
        _ambience = Field("Ambiance");
        _reverb = Field("Réverbération");
        _playlist = Field("Playlist : birds.ogg, wind.ogg");
        foreach (var field in new Control[] { _dayMusic, _nightMusic, _combatMusic, _ambience, _reverb, _playlist }) music.AddChild(field);
        _audioOrder = EnumOption<AudioOrderMode>();
        music.AddChild(FieldRow("Ordre", _audioOrder));
        _audioInterval = Spin(0, 3600, 10, .5);
        music.AddChild(FieldRow("Intervalle", _audioInterval));
        _audioVariation = Spin(0, 3600, 2, .5);
        music.AddChild(FieldRow("Variation", _audioVariation));
        _audioVolume = Spin(0, 4, 1, .05);
        music.AddChild(FieldRow("Volume relatif", _audioVolume));

        var spatial = Card(columns, "SPATIAL & RUNTIME");
        _soundMode = EnumOption<AreaSoundMode>();
        spatial.AddChild(FieldRow("Mode", _soundMode));
        _innerRadius = Spin(0, 10000, 2, .5);
        spatial.AddChild(FieldRow("Portée interne", _innerRadius));
        _outerRadius = Spin(0, 10000, 20, .5);
        spatial.AddChild(FieldRow("Portée externe", _outerRadius));
        _initiallyActive = Check("Actif au chargement", true);
        spatial.AddChild(_initiallyActive);
        _instancePolicy = EnumOption<AudioInstancePolicy>();
        spatial.AddChild(FieldRow("Instances", _instancePolicy));
        _authority = EnumOption<AudioAuthority>();
        spatial.AddChild(FieldRow("Autorité", _authority));
        _adaptiveResource = Field("Couche adaptative");
        _adaptiveWeather = Field("Météo requise");
        _adaptiveTag = Field("Tag de lieu");
        spatial.AddChild(_adaptiveResource);
        spatial.AddChild(_adaptiveWeather);
        spatial.AddChild(_adaptiveTag);

        var captions = Card(columns, "ACCESSIBILITÉ AUDIO");
        _captionResource = Field("Son important à sous-titrer");
        _captionFr = Field("Sous-titre français");
        _captionDirection = Check("Indiquer la direction", true);
        captions.AddChild(_captionResource);
        captions.AddChild(_captionFr);
        captions.AddChild(_captionDirection);
        _captionKind = new OptionButton();
        foreach (var item in new[] { "Dialogue", "Effet", "Ambiance" }) _captionKind.AddItem(item);
        captions.AddChild(FieldRow("Type de son", _captionKind));
        _captionImportance = new OptionButton();
        foreach (var item in new[] { "Information", "Important", "Critique" })
            _captionImportance.AddItem(item);
        _captionImportance.Selected = 1;
        captions.AddChild(FieldRow("Importance", _captionImportance));
        _captionRequired = Check("Caption requise au runtime", false);
        captions.AddChild(_captionRequired);
        var captionPreview = new Button { Text = "Prévisualiser le sous-titre" };
        captionPreview.Pressed += RefreshCaptionPreview;
        captions.AddChild(captionPreview);
        _captionState = new Label
        {
            Text = "État vide · aucun son important configuré.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _captionState.AddThemeColorOverride("font_color", new Color("9bd9bd"));
        captions.AddChild(_captionState);
        captions.AddChild(new Label
        {
            Text = "Distribution · ressource gouvernée, licence SPDX, provenance, manifeste et SHA-256 requis.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
    }

    private void BuildAudioEmitterTab(TabContainer tabs)
    {
        var scroll = Tab(tabs, "Objets sonores");
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 14);
        scroll.AddChild(columns);

        var identity = Card(columns, "OBJET & CLIP PRINCIPAL");
        identity.AddChild(new Label { Text = "Un objet vide reste désactivé. Ressource, licence et provenance sont obligatoires." });
        _emitterName = Field("Nom · Cascade ancienne");
        _emitterResource = Field("Ressource relative · audio/waterfall.ogg");
        _emitterPreview = Field("Aperçu optionnel · audio/waterfall.preview.ogg");
        _emitterTags = Field("Tags · eau, ambiance");
        _emitterLocale = Field("Langue · fr ou und");
        _emitterLicense = Field("Licence SPDX · CC0-1.0");
        _emitterProvenance = Field("Provenance · asset-vault:waterfall");
        _emitterManifest = Field("Manifeste · assets/asset-manifests.json");
        _emitterHash = Field("SHA-256 · 64 caractères hexadécimaux");
        _emitterTranscript = Field("Transcript · chute d'eau lointaine");
        _emitterWaveform = Field("Waveform · 0.1, 0.4, 0.8, 0.3");
        foreach (var field in new Control[] { _emitterName, _emitterResource, _emitterPreview, _emitterTags,
                     _emitterLocale, _emitterLicense, _emitterProvenance, _emitterManifest, _emitterHash,
                     _emitterTranscript, _emitterWaveform })
            identity.AddChild(field);
        _emitterDuration = Spin(0, 86400, 0, .1);
        identity.AddChild(FieldRow("Durée (secondes)", _emitterDuration));
        _emitterSelection = EnumOption<AudioClipSelectionMode>();
        identity.AddChild(FieldRow("Sélection", _emitterSelection));
        _emitterWeight = Spin(.05, 1000, 1, .05);
        identity.AddChild(FieldRow("Poids", _emitterWeight));
        _emitterLoudness = Spin(-120, 24, -18, .5);
        identity.AddChild(FieldRow("Loudness LUFS", _emitterLoudness));
        _emitterMemory = Spin(1024, 1073741824, 1048576, 1024);
        identity.AddChild(FieldRow("Mémoire (octets)", _emitterMemory));

        var playback = Card(columns, "LECTURE & SCRIPTS");
        _emitterLoopMode = EnumOption<AudioLoopMode>();
        playback.AddChild(FieldRow("Boucle", _emitterLoopMode));
        _emitterRepeatCount = Spin(0, 10000, 0, 1);
        playback.AddChild(FieldRow("Répétitions", _emitterRepeatCount));
        _emitterLoopStart = Spin(0, 86400, 0, .05);
        playback.AddChild(FieldRow("Début région", _emitterLoopStart));
        _emitterLoopEnd = Spin(0, 86400, 0, .05);
        playback.AddChild(FieldRow("Fin région", _emitterLoopEnd));
        _emitterCrossfade = Spin(0, 60, 0, .05);
        playback.AddChild(FieldRow("Fondu croisé", _emitterCrossfade));
        _emitterStopEvent = Field("Événement d'arrêt · audio.waterfall.stop");
        _emitterActivatedScript = Field("Script d'activation optionnel");
        _emitterCompletedScript = Field("Script de fin optionnel");
        playback.AddChild(_emitterStopEvent);
        playback.AddChild(_emitterActivatedScript);
        playback.AddChild(_emitterCompletedScript);
        _emitterRequired = Check("Extension requise au runtime", false);
        playback.AddChild(_emitterRequired);

        var spatial = Card(columns, "POSITION & PORTÉE 3D");
        _emitterPositionX = Spin(-4096, 4096, 0, .25);
        _emitterPositionY = Spin(-4096, 4096, 0, .25);
        _emitterPositionZ = Spin(-4096, 4096, 0, .25);
        spatial.AddChild(FieldRow("Position X", _emitterPositionX));
        spatial.AddChild(FieldRow("Position Y", _emitterPositionY));
        spatial.AddChild(FieldRow("Position Z", _emitterPositionZ));
        _emitterInnerRadius = Spin(0, 10000, 2, .25);
        _emitterOuterRadius = Spin(.01, 10000, 30, .25);
        spatial.AddChild(FieldRow("Portée interne", _emitterInnerRadius));
        spatial.AddChild(FieldRow("Portée externe", _emitterOuterRadius));
        _emitterCone = Spin(1, 360, 180, 1);
        spatial.AddChild(FieldRow("Cône (°)", _emitterCone));
        _emitterOcclusion = Spin(0, 1, 0, .05);
        spatial.AddChild(FieldRow("Occlusion", _emitterOcclusion));
        _emitterHeight = Spin(-100, 100, 0, .1);
        spatial.AddChild(FieldRow("Décalage hauteur", _emitterHeight));
        _emitterVirtualize = Check("Virtualiser hors portée", true);
        spatial.AddChild(_emitterVirtualize);
        _emitterState = new Label { Text = "État vide · aucun objet sonore placé." };
        _emitterState.AddThemeColorOverride("font_color", new Color("b7a7cf"));
        spatial.AddChild(_emitterState);
    }

    private void BuildR061AudioWorkbenchTab(TabContainer tabs)
    {
        var scroll = Tab(tabs, "Atelier audio");
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 14);
        scroll.AddChild(columns);

        var browser = Card(columns, "RESSOURCES & APERÇU");
        browser.AddChild(new Label
        {
            Text = "Recherchez par nom, chemin, tag ou transcript. Licence, provenance et hash restent visibles.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        _audioSearch = Field("Rechercher · pluie, combat, voix…");
        browser.AddChild(_audioSearch);
        var searchActions = new HBoxContainer();
        var search = new Button { Text = "Rechercher", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        search.Pressed += RefreshAudioSearch;
        searchActions.AddChild(search);
        var audition = new Button { Text = "▶ Écouter", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        audition.Pressed += AuditionAudioSearch;
        searchActions.AddChild(audition);
        browser.AddChild(searchActions);
        _audioSearchResults = new Label
        {
            Text = "État vide · ajoutez un objet sonore gouverné ou lancez une recherche.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _audioSearchResults.AddThemeColorOverride("font_color", new Color("c9badc"));
        browser.AddChild(_audioSearchResults);
        _audioWaveform = new Label
        {
            Text = "Waveform  ▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _audioWaveform.AddThemeColorOverride("font_color", new Color("ff9858"));
        browser.AddChild(_audioWaveform);

        var sequence = Card(columns, "SÉQUENCE & MODULATION");
        _r061Order = EnumOption<AudioOrderMode>();
        sequence.AddChild(FieldRow("Sélection", _r061Order));
        _audioSeed = Spin(int.MinValue, int.MaxValue, 2026, 1);
        sequence.AddChild(FieldRow("Seed", _audioSeed));
        _audioNoRepeat = Spin(0, 1024, 1, 1);
        sequence.AddChild(FieldRow("Fenêtre sans répétition", _audioNoRepeat));
        _audioWeights = Field("Poids · rain.ogg=3, wind.ogg=1");
        sequence.AddChild(_audioWeights);
        _audioDistribution = EnumOption<AudioModulationDistribution>();
        sequence.AddChild(FieldRow("Distribution", _audioDistribution));
        _audioCurve = Field("Courbe · 0:0, 0.5:0.35, 1:1");
        sequence.AddChild(_audioCurve);
        _audioMinimumInterval = Spin(0, 86400, 8, .25);
        _audioMaximumInterval = Spin(0, 86400, 12, .25);
        sequence.AddChild(FieldRow("Intervalle min", _audioMinimumInterval));
        sequence.AddChild(FieldRow("Intervalle max", _audioMaximumInterval));
        _audioMinimumPitch = Spin(-48, 48, -1, .1);
        _audioMaximumPitch = Spin(-48, 48, 1, .1);
        sequence.AddChild(FieldRow("Pitch min (demi-tons)", _audioMinimumPitch));
        sequence.AddChild(FieldRow("Pitch max (demi-tons)", _audioMaximumPitch));
        _audioTempoSync = Check("Synchroniser au tempo", false);
        sequence.AddChild(_audioTempoSync);
        _audioTempo = Spin(20, 400, 120, 1);
        _audioBeatDivision = Spin(.0625, 16, 1, .0625);
        sequence.AddChild(FieldRow("Tempo BPM", _audioTempo));
        sequence.AddChild(FieldRow("Division de temps", _audioBeatDivision));
        var statistics = new Button { Text = "Calculer l'aperçu statistique" };
        statistics.Pressed += RefreshAudioStatistics;
        sequence.AddChild(statistics);
        _audioStatisticalPreview = new Label
        {
            Text = "Aperçu · aucune séquence calculée.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _audioStatisticalPreview.AddThemeColorOverride("font_color", new Color("9bd9bd"));
        sequence.AddChild(_audioStatisticalPreview);

        var mix = Card(columns, "MIX & AUDITEURS");
        _audioTargetLoudness = Spin(-60, 0, -18, .5);
        mix.AddChild(FieldRow("Cible LUFS", _audioTargetLoudness));
        _audioBus = Field("Bus · Ambience");
        mix.AddChild(_audioBus);
        _audioDucking = Spin(-80, 0, -6, .5);
        mix.AddChild(FieldRow("Ducking (dB)", _audioDucking));
        _audioAccessibilityCap = Spin(0, 4, 1, .05);
        mix.AddChild(FieldRow("Plafond accessibilité", _audioAccessibilityCap));
        _audioMeter = EnumOption<AudioMeterMode>();
        mix.AddChild(FieldRow("Mesure", _audioMeter));
        _audioAreaScope = Field("Portée de zone · UUID ou vide");
        mix.AddChild(_audioAreaScope);
        _audioListenerPolicy = EnumOption<AudioListenerPolicy>();
        mix.AddChild(FieldRow("Auditeurs", _audioListenerPolicy));
        _audioListenerGains = Field("Personnalisation · joueur=0.8");
        mix.AddChild(_audioListenerGains);
        _audioRequired = Check("Contrat audio requis au runtime", false);
        mix.AddChild(_audioRequired);
        var governance = new Label
        {
            Text = "Distribution · UUID, schéma, licence SPDX, provenance, manifeste et SHA-256 vérifiés avant build.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        governance.AddThemeColorOverride("font_color", new Color("b7a7cf"));
        mix.AddChild(governance);

        _audioAuditionPlayer = new AudioStreamPlayer { Bus = "Master" };
        AddChild(_audioAuditionPlayer);
        _audioOrder!.ItemSelected += index => SyncAudioOrder((int)index, fromWorkbench: false);
        _r061Order.ItemSelected += index => SyncAudioOrder((int)index, fromWorkbench: true);
        _audioInterval!.ValueChanged += _ => SyncLegacyIntervalToWorkbench();
        _audioVariation!.ValueChanged += _ => SyncLegacyIntervalToWorkbench();
        _audioMinimumInterval.ValueChanged += _ => SyncWorkbenchIntervalToLegacy();
        _audioMaximumInterval.ValueChanged += _ => SyncWorkbenchIntervalToLegacy();
    }

    private void BuildR062AudioOrchestrationTab(TabContainer tabs)
    {
        var scroll = Tab(tabs, "Orchestration audio");
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 14);
        scroll.AddChild(columns);

        var spatial = Card(columns, "PORTÉE & CONTRÔLE");
        spatial.AddChild(new Label
        {
            Text = "Courbe d'atténuation, rayons de debug et handle ForgeScript restent alignés avec le runtime.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        _audioDebugRanges = Check("Afficher les sphères interne / externe", false);
        spatial.AddChild(_audioDebugRanges);
        var previewRange = new Button { Text = "Prévisualiser la portée" };
        previewRange.Pressed += RefreshR062Previews;
        spatial.AddChild(previewRange);
        _audioAttenuationPreview = new Label
        {
            Text = "Aperçu vide · choisissez une portée 3D.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _audioAttenuationPreview.AddThemeColorOverride("font_color", new Color("9bd9bd"));
        spatial.AddChild(_audioAttenuationPreview);
        var runtimeActions = new HBoxContainer();
        foreach (var operation in new[]
                 {
                     (Key: "Start", Label: "Démarrer"),
                     (Key: "Pause", Label: "Pause"),
                     (Key: "Resume", Label: "Reprendre"),
                     (Key: "Fade", Label: "Fondu"),
                     (Key: "Stop", Label: "Arrêter"),
                 })
        {
            var capturedOperation = operation.Key;
            var button = new Button { Text = operation.Label, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            button.Pressed += () => PreviewRuntimeControl(capturedOperation);
            runtimeActions.AddChild(button);
        }
        spatial.AddChild(runtimeActions);
        _audioRuntimePreview = new Label
        {
            Text = "Handle typé · arrêté · API 1.0",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _audioRuntimePreview.AddThemeColorOverride("font_color", new Color("c9badc"));
        spatial.AddChild(_audioRuntimePreview);

        var adaptive = Card(columns, "AMBIANCE & MUSIQUE");
        adaptive.AddChild(new Label
        {
            Text = "Les couches réagissent à l'heure, la météo, le lieu, le danger et les volumes intérieurs.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        _adaptiveCrossfade = Spin(0, 120, 2, .1);
        adaptive.AddChild(FieldRow("Fondu adaptatif", _adaptiveCrossfade));
        _adaptiveVolumeId = Field("Volume audio · forest-volume");
        adaptive.AddChild(_adaptiveVolumeId);
        _musicBeatSync = Check("Synchroniser les transitions à la mesure", true);
        adaptive.AddChild(_musicBeatSync);
        _musicTempo = Spin(20, 400, 120, 1);
        adaptive.AddChild(FieldRow("Tempo musical", _musicTempo));
        _musicBeatsPerBar = Spin(1, 32, 4, 1);
        adaptive.AddChild(FieldRow("Temps par mesure", _musicBeatsPerBar));
        var transitionPreview = new Button { Text = "Simuler jour → nuit → combat" };
        transitionPreview.Pressed += RefreshR062Previews;
        adaptive.AddChild(transitionPreview);

        var monitoring = Card(columns, "BUS & MONITORING");
        monitoring.AddChild(new Label
        {
            Text = "Bus hiérarchiques, snapshots, profils plateforme et plage dynamique sont mesurés en direct.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        _mixMonitor = Check("Activer le monitoring temps réel", true);
        monitoring.AddChild(_mixMonitor);
        var refreshMonitor = new Button { Text = "Actualiser les vumètres" };
        refreshMonitor.Pressed += RefreshR062Previews;
        monitoring.AddChild(refreshMonitor);
        _mixMonitorPreview = new Label
        {
            Text = "Master  -∞ dB\nMusic  -∞ dB\nAmbience  -∞ dB",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _mixMonitorPreview.AddThemeColorOverride("font_color", new Color("ff9858"));
        monitoring.AddChild(_mixMonitorPreview);
        monitoring.AddChild(new Label
        {
            Text = "Contrat de distribution · UUID, schéma, provenance, manifeste, SPDX et SHA-256 hérités du profil audio.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
    }

    private void Refresh()
    {
        var id = _active?.Invoke();
        var area = _state?.Invoke()?.Project.Areas.SingleOrDefault(item => item.Id == id);
        if (area is null) return;
        _current = area.Environment;
        Populate(_current);
    }

    private void Populate(AreaEnvironmentProfile profile)
    {
        _classification!.Selected = (int)profile.Classification;
        _tags!.Text = string.Join(", ", profile.Tags);
        _dayNight!.Selected = (int)profile.Lighting.Mode;
        _dayLength!.Value = profile.Lighting.DayLengthMinutes;
        _ambient!.Value = profile.Lighting.AmbientIntensity;
        _sun!.Value = profile.Lighting.DirectionalIntensity;
        _temperature!.Value = profile.Lighting.ColorTemperatureKelvin;
        _toneMapping!.Selected = (int)profile.Rendering.ToneMapping;
        _quality!.Selected = (int)profile.Rendering.Quality;
        _exposure!.Value = profile.Rendering.Exposure;
        _whitePoint!.Value = profile.Rendering.WhitePoint;
        _hdrOutput!.ButtonPressed = profile.Rendering.HdrOutput;
        _forwardPlus!.ButtonPressed = profile.Rendering.ForwardPlus;
        _perPixel!.ButtonPressed = profile.Rendering.PerPixelLighting;
        _materialInstances!.ButtonPressed = profile.Rendering.MaterialInstances;
        var desktop = profile.Rendering.LightBudgets.FirstOrDefault(item => item.Platform == "desktop") ?? profile.Rendering.LightBudgets[0];
        _dynamicLights!.Value = desktop.MaximumDynamicLights;
        _shadowLights!.Value = desktop.MaximumShadowCastingLights;
        _shadowAtlas!.Value = desktop.ShadowAtlasSize;
        _skyMode!.Selected = (int)profile.Sky.Mode;
        _panorama!.Text = profile.Sky.PanoramaResource;
        _zenith!.Text = profile.Sky.ZenithColor;
        _horizon!.Text = profile.Sky.HorizonColor;
        _ground!.Text = profile.Sky.GroundColor;
        _stars!.Value = profile.Sky.StarIntensity;
        _clouds!.Value = profile.Sky.CloudCoverage;
        _moon!.Value = profile.Sky.MoonIntensity;
        _skyEnergy!.Value = profile.Sky.EnergyMultiplier;
        _skyTransition!.Value = profile.Sky.TransitionSeconds;
        _fogMode!.Selected = (int)profile.Fog.Mode;
        _fogDensity!.Value = profile.Fog.Density;
        _fogStart!.Value = profile.Fog.StartDistance;
        _fogEnd!.Value = profile.Fog.EndDistance;
        var environmentAuthoring = profile.Authoring;
        _classificationKey!.Text = environmentAuthoring.Classification.Key;
        _classificationPack!.Text = environmentAuthoring.Classification.ExtensionPack;
        _classificationRendering!.ButtonPressed = environmentAuthoring.Classification.DrivesRendering;
        _classificationAudio!.ButtonPressed = environmentAuthoring.Classification.DrivesAudio;
        _classificationNavigation!.ButtonPressed = environmentAuthoring.Classification.DrivesNavigation;
        _classificationWeather!.ButtonPressed = environmentAuthoring.Classification.DrivesWeather;
        _classificationCamera!.ButtonPressed = environmentAuthoring.Classification.DrivesCamera;
        _classificationGameplay!.ButtonPressed = environmentAuthoring.Classification.DrivesGameplay;
        _lightingPreset!.Text = environmentAuthoring.Lighting.PresetKey;
        _ambientColor!.Text = environmentAuthoring.Lighting.AmbientColor;
        _directionalColor!.Text = environmentAuthoring.Lighting.DirectionalColor;
        _simpleLighting!.ButtonPressed = environmentAuthoring.Lighting.SimpleControls;
        _noonSun!.Value = environmentAuthoring.Lighting.Curves
            .OrderBy(item => Math.Abs(item.Hour - 12)).FirstOrDefault()?.SunIntensity ?? profile.Lighting.DirectionalIntensity;
        _midnightMoon!.Value = environmentAuthoring.Lighting.Curves
            .OrderBy(item => Math.Abs(item.Hour)).FirstOrDefault()?.MoonIntensity ?? profile.Sky.MoonIntensity;
        var timedEvent = environmentAuthoring.Lighting.Events.FirstOrDefault();
        _timedEventHour!.Value = timedEvent?.Hour ?? 18;
        _timedEventName!.Text = timedEvent?.EventName ?? string.Empty;
        _timedEventScript!.Text = timedEvent?.ForgeScriptPath ?? string.Empty;
        var probe = environmentAuthoring.Lighting.Probes.FirstOrDefault();
        _probeEnabled!.ButtonPressed = probe is not null;
        _probeAsset!.Text = probe?.AssetId ?? string.Empty;
        _probeRadius!.Value = probe?.RadiusMeters ?? 24;
        var lightingVolume = environmentAuthoring.Lighting.VolumeOverrides.FirstOrDefault();
        _lightingVolumeEnabled!.ButtonPressed = lightingVolume is not null;
        _volumeLightingPreset!.Text = lightingVolume?.LightingPresetKey ?? string.Empty;
        _lightingVolumeBlend!.Value = lightingVolume?.BlendDistanceMeters ?? 3;
        _fogDayColor!.Text = environmentAuthoring.Fog.DayColor;
        _fogNightColor!.Text = environmentAuthoring.Fog.NightColor;
        _fogNoiseAsset!.Text = environmentAuthoring.Fog.Noise?.AssetId ?? string.Empty;
        _fogNoiseScale!.Value = environmentAuthoring.Fog.Noise?.ScaleMeters ?? 12;
        var localFog = environmentAuthoring.Fog.LocalVolumes.FirstOrDefault();
        _localFogEnabled!.ButtonPressed = localFog is not null;
        _localFogDensity!.Value = localFog?.Density ?? .3;
        _localFogColor!.Text = localFog?.Color ?? "#9fb4c8";
        _fogQuality!.Selected = (int)(environmentAuthoring.Fog.QualityProfiles.TryGetValue("desktop", out var fogQuality)
            ? fogQuality : AreaFogQualityTier.High);
        _environmentRequired!.ButtonPressed = profile.RequireAuthoringRuntimeMetadata ||
            environmentAuthoring.RequiredAtRuntime;
        _r054Preview!.Text = $"✓ {environmentAuthoring.Classification.Key} · " +
            $"{environmentAuthoring.Lighting.Curves.Count} point(s) · " +
            $"{environmentAuthoring.Lighting.Probes.Count} probe(s) · " +
            $"{environmentAuthoring.Fog.LocalVolumes.Count} volume(s) de brouillard";
        _windDirectionX!.Value = profile.Wind.DirectionX;
        _windDirectionZ!.Value = profile.Wind.DirectionZ;
        _windSpeed!.Value = profile.Wind.Speed;
        _windGust!.Value = profile.Wind.GustStrength;
        _windInterval!.Value = profile.Wind.GustIntervalSeconds;
        _windVegetation!.ButtonPressed = profile.Wind.InfluencesVegetation;
        _windParticles!.ButtonPressed = profile.Wind.InfluencesParticles;
        _windAudio!.Text = profile.Wind.AudioCue;
        var weather = profile.Weather.States.Single(item => item.Id == profile.Weather.InitialStateId);
        _weatherKind!.Selected = (int)weather.Kind;
        _weatherIntensity!.Value = weather.Intensity;
        _weatherHumidity!.Value = weather.Humidity;
        _weatherWetness!.Value = weather.SurfaceWetness;
        _weatherLightning!.ButtonPressed = weather.Lightning;
        _weatherMinimumDuration!.Value = weather.MinimumDurationSeconds;
        _weatherMaximumDuration!.Value = weather.MaximumDurationSeconds;
        _weatherTransition!.Value = weather.TransitionSeconds;
        _weatherAudio!.Text = weather.AudioCue;
        _weatherGameplay!.Text = weather.GameplayEffect;
        _weatherAuthority!.ButtonPressed = profile.Weather.ServerAuthoritative;
        _weatherPersist!.ButtonPressed = profile.Weather.PersistState;
        _weatherReplicate!.ButtonPressed = profile.Weather.ReplicateState;
        var water = profile.Water.Bodies.FirstOrDefault();
        _waterEnabled!.ButtonPressed = water is not null;
        _waterKind!.Selected = (int)(water?.Kind ?? WaterBodyKind.Plane);
        _waterCenterX!.Value = water?.Center.X ?? 0;
        _waterCenterY!.Value = water?.Center.Y ?? 0;
        _waterCenterZ!.Value = water?.Center.Z ?? 0;
        _waterSizeX!.Value = water?.Size.X ?? 20;
        _waterSizeY!.Value = water?.Size.Y ?? 2;
        _waterSizeZ!.Value = water?.Size.Z ?? 15;
        _waterDepth!.Value = water?.MaximumDepth ?? 2;
        _waterFlow!.Value = water?.FlowSpeed ?? 0;
        _waterBuoyancy!.Value = water?.Buoyancy ?? 1;
        _waterNavigationCost!.Value = water?.NavigationCost ?? 2;
        _waterSwimming!.ButtonPressed = water?.SwimmingEnabled ?? true;
        _waterQuality!.Selected = (int)(water?.Quality ?? WaterQualityTier.High);
        _waterAudio!.Text = water?.AudioCue ?? string.Empty;
        var reverbZone = profile.Reverb.Zones.FirstOrDefault();
        _reverbZoneEnabled!.ButtonPressed = reverbZone is not null;
        _reverbWet!.Value = reverbZone?.Wet ?? .55;
        _reverbDry!.Value = reverbZone?.Dry ?? 1;
        _reverbBlend!.Value = reverbZone?.BlendSeconds ?? 1;
        _reverbSizeX!.Value = reverbZone?.Size.X ?? 12;
        _reverbSizeY!.Value = reverbZone?.Size.Y ?? 6;
        _reverbSizeZ!.Value = reverbZone?.Size.Z ?? 12;
        _dayMusic!.Text = profile.Audio.DayMusic;
        _nightMusic!.Text = profile.Audio.NightMusic;
        _combatMusic!.Text = profile.Audio.CombatMusic;
        _ambience!.Text = profile.Audio.Ambience;
        _reverb!.Text = profile.Reverb.DefaultPreset;
        _playlist!.Text = string.Join(", ", profile.Audio.Playlist);
        _audioOrder!.Selected = (int)profile.Audio.OrderMode;
        _audioInterval!.Value = profile.Audio.IntervalSeconds;
        _audioVariation!.Value = profile.Audio.IntervalVariationSeconds;
        _audioVolume!.Value = profile.Audio.RelativeVolume;
        var audioAuthoring = profile.Audio.Authoring;
        _r061Order!.Selected = (int)profile.Audio.OrderMode;
        _audioSeed!.Value = audioAuthoring.Selection.DeterministicSeed;
        _audioNoRepeat!.Value = audioAuthoring.Selection.NoRepeatWindow;
        _audioWeights!.Text = string.Join(", ", audioAuthoring.Selection.Weights
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => $"{item.Key}={item.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}"));
        _audioDistribution!.Selected = (int)audioAuthoring.Modulation.Distribution;
        _audioCurve!.Text = string.Join(", ", audioAuthoring.Modulation.Curve.Select(point =>
            $"{point.Position.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}:" +
            point.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)));
        _audioMinimumInterval!.Value = audioAuthoring.Modulation.MinimumIntervalSeconds;
        _audioMaximumInterval!.Value = audioAuthoring.Modulation.MaximumIntervalSeconds;
        _audioMinimumPitch!.Value = audioAuthoring.Modulation.MinimumPitchSemitones;
        _audioMaximumPitch!.Value = audioAuthoring.Modulation.MaximumPitchSemitones;
        _audioTempoSync!.ButtonPressed = audioAuthoring.Modulation.SyncToTempo;
        _audioTempo!.Value = audioAuthoring.Modulation.TempoBpm;
        _audioBeatDivision!.Value = audioAuthoring.Modulation.BeatDivision;
        _audioTargetLoudness!.Value = audioAuthoring.Mix.TargetLoudnessLufs;
        _audioBus!.Text = audioAuthoring.Mix.Bus;
        _audioDucking!.Value = audioAuthoring.Mix.DuckingDb;
        _audioAccessibilityCap!.Value = audioAuthoring.Mix.AccessibilityVolumeCap;
        _audioMeter!.Selected = (int)audioAuthoring.Mix.MeterMode;
        _audioAreaScope!.Text = audioAuthoring.Ambient.AreaScope;
        _audioListenerPolicy!.Selected = (int)audioAuthoring.Ambient.ListenerPolicy;
        _audioListenerGains!.Text = string.Join(", ", audioAuthoring.Ambient.ListenerGains
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => $"{item.Key}={item.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}"));
        _audioRequired!.ButtonPressed = audioAuthoring.RequiredAtRuntime;
        RefreshAudioSearch();
        RefreshAudioStatistics();
        _soundMode!.Selected = (int)profile.Audio.Spatial.Mode;
        _innerRadius!.Value = profile.Audio.Spatial.InnerRadius;
        _outerRadius!.Value = profile.Audio.Spatial.OuterRadius;
        _audioDebugRanges!.ButtonPressed = profile.Audio.Spatial.DebugSpheresVisible;
        _initiallyActive!.ButtonPressed = profile.Audio.RuntimeControl.InitiallyActive;
        _instancePolicy!.Selected = (int)profile.Audio.RuntimeControl.InstancePolicy;
        _authority!.Selected = (int)profile.Audio.RuntimeControl.Authority;
        var adaptiveLayer = profile.Audio.AdaptiveLayers.FirstOrDefault();
        _adaptiveCrossfade!.Value = adaptiveLayer?.CrossfadeSeconds ?? 2;
        _adaptiveVolumeId!.Text = adaptiveLayer?.VolumeId ?? string.Empty;
        var musicState = profile.Audio.Orchestration.MusicStates.FirstOrDefault();
        _musicBeatSync!.ButtonPressed = musicState?.BeatSynchronized ?? false;
        _musicTempo!.Value = musicState?.TempoBpm ?? 120;
        _musicBeatsPerBar!.Value = musicState?.BeatsPerBar ?? 4;
        _mixMonitor!.ButtonPressed = profile.Audio.Orchestration.MixBuses.Any(item => item.MonitorEnabled);
        RefreshR062Previews();
        var caption = profile.Audio.Orchestration.Captions.FirstOrDefault();
        _captionResource!.Text = caption?.Resource ?? string.Empty;
        _captionFr!.Text = caption?.Localizations.FirstOrDefault(item =>
            item.Locale.Equals("fr", StringComparison.OrdinalIgnoreCase))?.Text ??
            caption?.Localizations.FirstOrDefault()?.Text ?? string.Empty;
        _captionDirection!.ButtonPressed = caption?.ShowDirection ?? true;
        _captionKind!.Selected = caption?.CueKind switch
        {
            "dialogue" => 0,
            "ambient" => 2,
            _ => 1,
        };
        _captionImportance!.Selected = caption?.Importance switch
        {
            "informational" => 0,
            "critical" => 2,
            _ => 1,
        };
        _captionRequired!.ButtonPressed = caption?.RequiredAtRuntime ?? false;
        RefreshCaptionPreview();
        var emitter = profile.Audio.Emitters.Definitions.FirstOrDefault();
        var clip = emitter?.ClipSet.Clips.FirstOrDefault();
        var instance = emitter is null
            ? null
            : profile.Audio.Emitters.Instances.FirstOrDefault(item => item.DefinitionId == emitter.Id);
        _emitterName!.Text = emitter?.Name ?? string.Empty;
        _emitterResource!.Text = clip?.ResourcePath ?? string.Empty;
        _emitterPreview!.Text = clip?.PreviewResourcePath ?? string.Empty;
        _emitterTags!.Text = clip is null ? string.Empty : string.Join(", ", clip.Tags);
        _emitterLocale!.Text = clip?.Locale ?? "und";
        _emitterLicense!.Text = clip?.LicenseSpdx ?? string.Empty;
        _emitterProvenance!.Text = emitter?.Provenance ?? string.Empty;
        _emitterManifest!.Text = emitter?.PackageManifest ?? "assets/asset-manifests.json";
        _emitterHash!.Text = clip?.ContentSha256 ?? string.Empty;
        _emitterTranscript!.Text = clip?.Transcript ?? string.Empty;
        _emitterWaveform!.Text = clip?.WaveformPeaks is { Count: > 0 }
            ? string.Join(", ", clip.WaveformPeaks.Select(value =>
                value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)))
            : string.Empty;
        _emitterDuration!.Value = clip?.DurationSeconds ?? 0;
        _emitterSelection!.Selected = (int)(emitter?.ClipSet.Selection ?? AudioClipSelectionMode.Ordered);
        _emitterWeight!.Value = clip?.Weight ?? 1;
        _emitterLoudness!.Value = clip?.LoudnessLufs ?? -18;
        _emitterMemory!.Value = clip?.MemoryBytes ?? 1048576;
        _emitterLoopMode!.Selected = (int)(emitter?.Loop.Mode ?? AudioLoopMode.OneShot);
        _emitterRepeatCount!.Value = emitter?.Loop.RepeatCount ?? 0;
        _emitterLoopStart!.Value = emitter?.Loop.RegionStartSeconds ?? 0;
        _emitterLoopEnd!.Value = emitter?.Loop.RegionEndSeconds ?? 0;
        _emitterCrossfade!.Value = emitter?.Loop.CrossfadeSeconds ?? 0;
        _emitterStopEvent!.Text = emitter?.Loop.StopEvent ?? string.Empty;
        _emitterActivatedScript!.Text = emitter?.ActivatedScript ?? string.Empty;
        _emitterCompletedScript!.Text = emitter?.CompletedScript ?? string.Empty;
        _emitterRequired!.ButtonPressed = emitter?.Required ?? false;
        _emitterPositionX!.Value = instance?.Transform.Position.X ?? 0;
        _emitterPositionY!.Value = instance?.Transform.Position.Y ?? 0;
        _emitterPositionZ!.Value = instance?.Transform.Position.Z ?? 0;
        _emitterInnerRadius!.Value = emitter?.Spatial.InnerRadius ?? 2;
        _emitterOuterRadius!.Value = emitter?.Spatial.OuterRadius ?? 30;
        _emitterCone!.Value = emitter?.Spatial.ConeAngleDegrees ?? 180;
        _emitterOcclusion!.Value = emitter?.Spatial.Occlusion ?? 0;
        _emitterHeight!.Value = emitter?.Spatial.HeightOffset ?? 0;
        _emitterVirtualize!.ButtonPressed = emitter?.Spatial.VirtualizeOutsideRange ?? true;
        _emitterState!.Text = emitter is null
            ? "État vide · aucun objet sonore placé."
            : $"✓ {profile.Audio.Emitters.Definitions.Count} définition(s) · {profile.Audio.Emitters.Instances.Count} instance(s)";
    }

    private bool Save()
    {
        var id = _active?.Invoke();
        if (id is null || _current is null) return false;
        try
        {
            var desktop = new DynamicLightBudgetProfile(
                "desktop",
                (int)_dynamicLights!.Value,
                (int)_shadowLights!.Value,
                (int)_shadowAtlas!.Value,
                0.8);
            var budgets = _current.Rendering.LightBudgets.Where(item => item.Platform != "desktop").Append(desktop).ToArray();
            var rendering = new AreaRenderingProfile(
                AreaRenderingProfile.CurrentSchemaVersion,
                PbrMaterialWorkflow.MetallicRoughness,
                (ToneMappingProfile)_toneMapping!.Selected,
                (RenderQualityTier)_quality!.Selected,
                _forwardPlus!.ButtonPressed,
                _perPixel!.ButtonPressed,
                _materialInstances!.ButtonPressed,
                _hdrOutput!.ButtonPressed,
                _exposure!.Value,
                _whitePoint!.Value,
                _current.Rendering.LutResource,
                budgets);
            var sky = new SkyProfile(
                SkyProfile.CurrentSchemaVersion,
                (SkyProfileMode)_skyMode!.Selected,
                _panorama!.Text,
                _zenith!.Text,
                _horizon!.Text,
                _ground!.Text,
                _stars!.Value,
                _clouds!.Value,
                _moon!.Value,
                _skyEnergy!.Value,
                _skyTransition!.Value);
            var wind = new WindProfile(
                WindProfile.CurrentSchemaVersion,
                (float)_windDirectionX!.Value,
                (float)_windDirectionZ!.Value,
                (float)_windSpeed!.Value,
                (float)_windGust!.Value,
                (float)_windInterval!.Value,
                _windVegetation!.ButtonPressed,
                _windParticles!.ButtonPressed,
                _windAudio!.Text,
                _current.Wind.GameplayForce,
                _current.Wind.LocalZones);
            var previousWeather = _current.Weather.States.Single(item => item.Id == _current.Weather.InitialStateId);
            var initialWeather = new WeatherStateProfile(
                previousWeather.Id,
                ((WeatherKind)_weatherKind!.Selected).ToString(),
                (WeatherKind)_weatherKind.Selected,
                (float)_weatherIntensity!.Value,
                (float)_weatherHumidity!.Value,
                (float)_weatherWetness!.Value,
                _weatherLightning!.ButtonPressed,
                previousWeather.PrecipitationResource,
                _weatherAudio!.Text,
                _weatherGameplay!.Text,
                (float)_weatherMinimumDuration!.Value,
                (float)_weatherMaximumDuration!.Value,
                (float)_weatherTransition!.Value);
            var weather = new WeatherProfile(
                WeatherProfile.CurrentSchemaVersion,
                initialWeather.Id,
                _current.Weather.States.Where(item => item.Id != initialWeather.Id).Append(initialWeather).ToArray(),
                _weatherAuthority!.ButtonPressed,
                _current.Weather.DeterministicSeed,
                _weatherPersist!.ButtonPressed,
                _weatherReplicate!.ButtonPressed);
            var previousWater = _current.Water.Bodies.FirstOrDefault();
            var waterBodies = _current.Water.Bodies
                .Where(item => previousWater is null || item.Id != previousWater.Id)
                .ToList();
            if (_waterEnabled!.ButtonPressed)
            {
                waterBodies.Add(new WaterBodyProfile(
                    previousWater?.Id ?? Guid.NewGuid(),
                    previousWater?.Name ?? "Plan d'eau principal",
                    (WaterBodyKind)_waterKind!.Selected,
                    new Vector3Value((float)_waterCenterX!.Value, (float)_waterCenterY!.Value, (float)_waterCenterZ!.Value),
                    new Vector3Value((float)_waterSizeX!.Value, (float)_waterSizeY!.Value, (float)_waterSizeZ!.Value),
                    (float)_waterDepth!.Value,
                    previousWater?.FlowDirectionX ?? 1,
                    previousWater?.FlowDirectionZ ?? 0,
                    (float)_waterFlow!.Value,
                    (float)_waterBuoyancy!.Value,
                    (float)_waterNavigationCost!.Value,
                    _waterSwimming!.ButtonPressed,
                    (WaterQualityTier)_waterQuality!.Selected,
                    previousWater?.MaterialResource ?? string.Empty,
                    _waterAudio!.Text));
            }

            var water = new WaterProfile(WaterProfile.CurrentSchemaVersion, waterBodies);
            var previousReverb = _current.Reverb.Zones.FirstOrDefault();
            var reverbZones = _current.Reverb.Zones
                .Where(item => previousReverb is null || item.Id != previousReverb.Id)
                .ToList();
            var reverbPreset = string.IsNullOrWhiteSpace(_reverb!.Text) ? "neutral" : _reverb.Text;
            if (_reverbZoneEnabled!.ButtonPressed)
            {
                reverbZones.Add(new ReverbZoneProfile(
                    previousReverb?.Id ?? Guid.NewGuid(),
                    previousReverb?.Name ?? "Zone de réverbération principale",
                    previousReverb?.Center ?? new Vector3Value(0, 0, 0),
                    new Vector3Value((float)_reverbSizeX!.Value, (float)_reverbSizeY!.Value, (float)_reverbSizeZ!.Value),
                    reverbPreset,
                    (float)_reverbWet!.Value,
                    (float)_reverbDry!.Value,
                    (float)_reverbBlend!.Value,
                    previousReverb?.ImpulseResource ?? string.Empty,
                    previousReverb?.LicenseSpdx ?? string.Empty,
                    previousReverb?.ContentSha256 ?? string.Empty,
                    previousReverb?.Provenance ?? string.Empty));
            }

            var reverb = new AreaReverbProfile(
                AreaReverbProfile.CurrentSchemaVersion,
                reverbPreset,
                reverbZones);
            IReadOnlyList<AdaptiveAmbienceLayer> layers = string.IsNullOrWhiteSpace(_adaptiveResource!.Text)
                ? []
                : [new(_adaptiveResource.Text.Trim(), 18, 6, _adaptiveWeather!.Text.Trim(),
                    _adaptiveTag!.Text.Trim(), 0, false, 1, (float)_adaptiveCrossfade!.Value,
                    _adaptiveVolumeId!.Text.Trim())];
            var master = Guid.NewGuid();
            var music = Guid.NewGuid();
            IReadOnlyList<SoundCaption> captions;
            if (string.IsNullOrWhiteSpace(_captionResource!.Text) || string.IsNullOrWhiteSpace(_captionFr!.Text))
            {
                captions = [];
            }
            else
            {
                var captionFingerprint = $"{_captionResource.Text.Trim()}|fr|{_captionFr.Text.Trim()}|" +
                                         $"{_captionDirection!.ButtonPressed}|{_captionKind!.Selected}|" +
                                         $"{_captionImportance!.Selected}|{_captionRequired!.ButtonPressed}";
                var captionHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(captionFingerprint)))
                    .ToLowerInvariant();
                var previousCaption = _current.Audio.Orchestration.Captions.FirstOrDefault(item =>
                    item.Resource.Equals(_captionResource.Text.Trim(), StringComparison.Ordinal));
                var cueKind = _captionKind.Selected switch { 0 => "dialogue", 2 => "ambient", _ => "effect" };
                var importance = _captionImportance.Selected switch
                {
                    0 => "informational",
                    2 => "critical",
                    _ => "important",
                };
                captions = [new(_captionResource.Text.Trim(), [new("fr", _captionFr.Text.Trim())],
                    _captionDirection.ButtonPressed, importance,
                    previousCaption?.Id is { } previousId && previousId != Guid.Empty
                        ? previousId
                        : new Guid(Convert.FromHexString(captionHash[..32])),
                    SoundCaption.CurrentSchemaVersion, "toolset:r063", "assets/audio-captions.json",
                    "CC0-1.0", captionHash, cueKind, 1.5f, 6, _captionRequired.ButtonPressed)];
            }
            var orchestration = new AreaAudioOrchestration(
                [
                    new(MusicState.Exploration, _dayMusic!.Text, [], 2, 5,
                        _musicBeatSync!.ButtonPressed, (float)_musicTempo!.Value, (int)_musicBeatsPerBar!.Value),
                    new(MusicState.Tension, _nightMusic!.Text, [], 2, 5,
                        _musicBeatSync.ButtonPressed, (float)_musicTempo.Value, (int)_musicBeatsPerBar.Value),
                    new(MusicState.Combat, _combatMusic!.Text, ["combat-drums"], 1, 3,
                        _musicBeatSync.ButtonPressed, (float)_musicTempo.Value, (int)_musicBeatsPerBar.Value),
                ],
                [
                    new(master, "Master", null, 1, "default", "all", .8f, true,
                        _mixMonitor!.ButtonPressed),
                    new(music, "Music", master, 1, "music", "all", .8f, false,
                        _mixMonitor.ButtonPressed),
                ],
                captions);
            var previousEmitter = _current.Audio.Emitters.Definitions.FirstOrDefault();
            var previousInstance = previousEmitter is null
                ? null
                : _current.Audio.Emitters.Instances.FirstOrDefault(item => item.DefinitionId == previousEmitter.Id);
            AudioEmitterCatalog emitters;
            if (string.IsNullOrWhiteSpace(_emitterName!.Text) && string.IsNullOrWhiteSpace(_emitterResource!.Text))
            {
                emitters = AudioEmitterCatalog.Empty;
            }
            else
            {
                var definitionId = previousEmitter?.Id ?? Guid.NewGuid();
                var provenance = _emitterProvenance!.Text.Trim();
                var hash = _emitterHash!.Text.Trim().ToLowerInvariant();
                var clip = new AudioClipDefinition(
                    previousEmitter?.ClipSet.Clips.FirstOrDefault()?.Id ?? Guid.NewGuid(),
                    _emitterResource!.Text.Trim(), (float)_emitterWeight!.Value,
                    _emitterTags!.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                    previousEmitter?.ClipSet.Clips.FirstOrDefault()?.Variant ?? "default",
                    _emitterLocale!.Text.Trim(), (float)_emitterLoudness!.Value,
                    _emitterPreview!.Text.Trim(),
                    string.Equals(Path.GetExtension(_emitterResource!.Text), ".wav", StringComparison.OrdinalIgnoreCase)
                        ? "audio/wav" : "audio/ogg",
                    (long)_emitterMemory!.Value, _emitterLicense!.Text.Trim(), provenance, hash,
                    _emitterTranscript!.Text.Trim(), ParseFloatList(_emitterWaveform!.Text, 0, 1),
                    (float)_emitterDuration!.Value);
                var clipSet = new AudioClipSet(previousEmitter?.ClipSet.Id ?? Guid.NewGuid(),
                    AudioClipSet.CurrentSchemaVersion, $"{_emitterName.Text.Trim()} clips",
                    (AudioClipSelectionMode)_emitterSelection!.Selected, [clip], provenance);
                var definition = new AudioEmitterDefinition(definitionId,
                    AudioEmitterDefinition.CurrentSchemaVersion, _emitterName.Text.Trim(), clipSet,
                    new AudioLoopPolicy((AudioLoopMode)_emitterLoopMode!.Selected,
                        (int)_emitterRepeatCount!.Value, (float)_emitterLoopStart!.Value,
                        (float)_emitterLoopEnd!.Value, (float)_emitterCrossfade!.Value,
                        _emitterStopEvent!.Text.Trim()),
                    new AudioEmitterSpatialProfile((float)_emitterInnerRadius!.Value,
                        (float)_emitterOuterRadius!.Value, "inverse-square", (float)_emitterCone!.Value,
                        -12, (float)_emitterOcclusion!.Value, (float)_emitterHeight!.Value,
                        _emitterVirtualize!.ButtonPressed), null, 0,
                    _emitterActivatedScript!.Text.Trim(), _emitterCompletedScript!.Text.Trim(),
                    _emitterRequired!.ButtonPressed, provenance, _emitterManifest!.Text.Trim(),
                    _emitterLicense!.Text.Trim(), hash);
                var instance = new AudioEmitterInstance(previousInstance?.Id ?? Guid.NewGuid(), definitionId,
                    new TransformValue(new Vector3Value((float)_emitterPositionX!.Value,
                            (float)_emitterPositionY!.Value, (float)_emitterPositionZ!.Value),
                        previousInstance?.Transform.Rotation ?? QuaternionValue.Identity, Vector3Value.One),
                    true, provenance);
                emitters = new AudioEmitterCatalog(AudioEmitterCatalog.CurrentSchemaVersion,
                    [definition], [instance]);
                var audioDiagnostics = AudioEmitterCatalogValidator.Validate(emitters,
                    orchestration.MixBuses.Select(item => item.Id).ToHashSet(),
                    path: "environment/audio/emitters");
                if (audioDiagnostics.Count > 0)
                    throw new ArgumentException($"{audioDiagnostics[0].Code} · {audioDiagnostics[0].Path} · {audioDiagnostics[0].Message}");
            }
            var playlist = _playlist!.Text.Split(',',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var selectionWeights = ParseFloatMap(_audioWeights!.Text, .0001f, 100_000);
            var listenerGains = ParseFloatMap(_audioListenerGains!.Text, 0, 4);
            var modulationCurve = ParseCurve(_audioCurve!.Text);
            var audioFingerprint = string.Join('|', playlist) + $"|{_r061Order!.Selected}|{_audioSeed!.Value:R}|" +
                $"{_audioNoRepeat!.Value:R}|{_audioMinimumInterval!.Value:R}|{_audioMaximumInterval!.Value:R}|" +
                $"{_audioTargetLoudness!.Value:R}|{_audioBus!.Text}|{_audioListenerPolicy!.Selected}";
            var audioHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(audioFingerprint)))
                .ToLowerInvariant();
            var audioAuthoring = new AudioAuthoringProfile(
                _current.Audio.Authoring.Id,
                AudioAuthoringProfile.CurrentSchemaVersion,
                "toolset:r061",
                "assets/asset-manifests.json",
                "CC0-1.0",
                audioHash,
                _audioRequired!.ButtonPressed,
                new((int)_audioSeed.Value, (int)_audioNoRepeat.Value, selectionWeights),
                new((float)_audioMinimumInterval.Value, (float)_audioMaximumInterval.Value,
                    (float)_audioMinimumPitch!.Value, (float)_audioMaximumPitch!.Value,
                    .85f, 1.15f, (AudioModulationDistribution)_audioDistribution!.Selected,
                    modulationCurve, (int)_audioSeed.Value, _audioTempoSync!.ButtonPressed,
                    (float)_audioTempo!.Value, (float)_audioBeatDivision!.Value),
                new((float)_audioTargetLoudness.Value, _audioBus.Text.Trim(),
                    (float)_audioDucking!.Value, 1, (float)_audioAccessibilityCap!.Value,
                    (AudioMeterMode)_audioMeter!.Selected),
                new(_audioAreaScope!.Text.Trim(), _audioBus.Text.Trim(),
                    (AudioListenerPolicy)_audioListenerPolicy!.Selected, listenerGains));
            var audio = new AreaAudioProfile(
                _dayMusic.Text,
                _nightMusic.Text,
                _combatMusic.Text,
                _ambience!.Text,
                string.IsNullOrWhiteSpace(_reverb!.Text) ? "neutral" : _reverb.Text,
                2,
                playlist,
                (AudioOrderMode)_r061Order.Selected,
                (float)_audioInterval!.Value,
                (float)_audioVariation!.Value,
                (float)_audioVolume!.Value,
                new((AreaSoundMode)_soundMode!.Selected, (float)_innerRadius!.Value,
                    (float)_outerRadius!.Value, .1f, "linear", _audioDebugRanges!.ButtonPressed),
                new(_initiallyActive!.ButtonPressed, (AudioInstancePolicy)_instancePolicy!.Selected,
                    (AudioAuthority)_authority!.Selected, 1, "audio.started", "audio.stopped",
                    _current.Audio.RuntimeControl.PausedEvent, _current.Audio.RuntimeControl.FadedEvent,
                    _current.Audio.RuntimeControl.ForgeScriptApiVersion),
                layers,
                orchestration,
                emitters,
                audioAuthoring);
            var currentAuthoring = _current.Authoring;
            var environmentClass = (AreaEnvironmentClass)_classification!.Selected;
            var curves = new AreaDayNightCurvePoint[]
            {
                new(0, 0, (float)_midnightMoon!.Value, -45),
                new(12, (float)_noonSun!.Value, 0, 60),
                new(24, 0, (float)_midnightMoon.Value, -45),
            };
            IReadOnlyList<AreaTimedEnvironmentEvent> timedEvents =
                string.IsNullOrWhiteSpace(_timedEventName!.Text) ||
                string.IsNullOrWhiteSpace(_timedEventScript!.Text)
                    ? []
                    : [new(currentAuthoring.Lighting.Events.FirstOrDefault()?.Id ?? Guid.NewGuid(),
                        (float)_timedEventHour!.Value, _timedEventName.Text.Trim(),
                        _timedEventScript.Text.Trim(), true)];
            IReadOnlyList<AreaReflectionProbeProfile> probes = !_probeEnabled!.ButtonPressed
                ? []
                : [new(currentAuthoring.Lighting.Probes.FirstOrDefault()?.Id ?? Guid.NewGuid(),
                    "Probe principal", currentAuthoring.Lighting.Probes.FirstOrDefault()?.Transform ?? TransformValue.Identity,
                    _probeAsset!.Text.Trim(), currentAuthoring.Lighting.Probes.FirstOrDefault()?.BlueprintId,
                    (float)_probeRadius!.Value, false,
                    currentAuthoring.Lighting.Probes.FirstOrDefault()?.Overrides ??
                    new Dictionary<string, string>())];
            IReadOnlyList<AreaEnvironmentVolumeOverride> lightingVolumes =
                !_lightingVolumeEnabled!.ButtonPressed
                    ? []
                    : [new(currentAuthoring.Lighting.VolumeOverrides.FirstOrDefault()?.Id ?? Guid.NewGuid(),
                        "Volume lumière principal",
                        currentAuthoring.Lighting.VolumeOverrides.FirstOrDefault()?.Transform ?? TransformValue.Identity,
                        _volumeLightingPreset!.Text.Trim(), (FogMode)_fogMode!.Selected,
                        (float)_lightingVolumeBlend!.Value,
                        currentAuthoring.Lighting.VolumeOverrides.FirstOrDefault()?.Overrides ??
                        new Dictionary<string, string>())];
            var noise = string.IsNullOrWhiteSpace(_fogNoiseAsset!.Text)
                ? null
                : new AreaFogNoiseProfile(_fogNoiseAsset.Text.Trim(), (float)_fogNoiseScale!.Value,
                    .1f, .05f, 1);
            IReadOnlyList<AreaLocalFogVolume> fogVolumes = !_localFogEnabled!.ButtonPressed
                ? []
                : [new(currentAuthoring.Fog.LocalVolumes.FirstOrDefault()?.Id ?? Guid.NewGuid(),
                    "Brouillard local principal",
                    currentAuthoring.Fog.LocalVolumes.FirstOrDefault()?.Transform ?? TransformValue.Identity,
                    (FogMode)_fogMode!.Selected, (float)_localFogDensity!.Value, .2f,
                    _localFogColor!.Text.Trim(), 2,
                    currentAuthoring.Fog.LocalVolumes.FirstOrDefault()?.Overrides ??
                    new Dictionary<string, string>())];
            var authoring = new AreaEnvironmentAuthoringProfile(currentAuthoring.Id,
                AreaEnvironmentAuthoringProfile.CurrentSchemaVersion, "toolset:r054",
                new(environmentClass, _classificationKey!.Text.Trim(), _classificationPack!.Text.Trim(),
                    _classificationRendering!.ButtonPressed, _classificationAudio!.ButtonPressed,
                    _classificationNavigation!.ButtonPressed, _classificationWeather!.ButtonPressed,
                    _classificationCamera!.ButtonPressed, _classificationGameplay!.ButtonPressed),
                new(_lightingPreset!.Text.Trim(), _ambientColor!.Text.Trim(),
                    _directionalColor!.Text.Trim(), _simpleLighting!.ButtonPressed, curves, timedEvents, probes,
                    lightingVolumes),
                new(_fogDayColor!.Text.Trim(), _fogNightColor!.Text.Trim(), noise, fogVolumes,
                    new Dictionary<string, AreaFogQualityTier>(StringComparer.Ordinal)
                    {
                        ["desktop"] = (AreaFogQualityTier)_fogQuality!.Selected,
                        ["low"] = AreaFogQualityTier.Low,
                    }),
                _environmentRequired!.ButtonPressed);
            var profile = new AreaEnvironmentProfile(
                AreaEnvironmentProfile.CurrentSchemaVersion,
                (AreaEnvironmentClass)_classification!.Selected,
                _tags!.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                new((DayNightMode)_dayNight!.Selected, (float)_dayLength!.Value, (float)_ambient!.Value, (float)_sun!.Value, (float)_temperature!.Value, _hdrOutput.ButtonPressed, true),
                new((FogMode)_fogMode!.Selected, (float)_fogDensity!.Value, (float)_fogStart!.Value, (float)_fogEnd!.Value, 0),
                audio,
                rendering,
                sky,
                wind,
                weather,
                water,
                reverb,
                authoring,
                _environmentRequired.ButtonPressed);
            var applied = _apply?.Invoke(id, profile) == true;
            if (applied) _current = profile;
            _status!.Text = applied
                ? $"✓ Profil appliqué · {desktop.MaximumDynamicLights} lumières · {rendering.ToneMapping} · {sky.Mode}"
                : "✕ Profil refusé";
            return applied;
        }
        catch (ArgumentException exception)
        {
            _status!.Text = $"✕ {exception.Message}";
            return false;
        }
    }

    internal bool ConfigureAudioForSmokeTest(AreaEnvironmentProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _current = profile;
        Populate(profile);
        _tabs!.CurrentTab = 6;
        return _audioSearch is not null && _audioWaveform is not null && _audioStatisticalPreview is not null &&
               _r061Order?.Selected == (int)profile.Audio.OrderMode &&
               _audioBus?.Text == profile.Audio.Authoring.Mix.Bus;
    }

    internal bool ConfigureR062AudioForSmokeTest(AreaEnvironmentProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _current = profile;
        Populate(profile);
        _tabs!.CurrentTab = 7;
        return _audioDebugRanges?.ButtonPressed == profile.Audio.Spatial.DebugSpheresVisible &&
               _musicBeatSync?.ButtonPressed == profile.Audio.Orchestration.MusicStates.FirstOrDefault()?.BeatSynchronized &&
               _mixMonitorPreview?.Text.Contains("LUFS", StringComparison.Ordinal) == true &&
               _audioRuntimePreview is not null;
    }

    internal bool ConfigureR063AudioForSmokeTest(AreaEnvironmentProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _current = profile;
        Populate(profile);
        _tabs!.CurrentTab = 3;
        var caption = profile.Audio.Orchestration.Captions.SingleOrDefault();
        return caption is not null && _captionResource?.Text == caption.Resource &&
               _captionFr?.Text == caption.Localizations[0].Text &&
               _captionState?.Text.Contains("Aperçu runtime", StringComparison.Ordinal) == true;
    }

    private void RefreshCaptionPreview()
    {
        if (_captionState is null || _captionResource is null || _captionFr is null) return;
        if (string.IsNullOrWhiteSpace(_captionResource.Text) || string.IsNullOrWhiteSpace(_captionFr.Text))
        {
            _captionState.Text = "État vide · aucun son important configuré.";
            return;
        }
        var arrow = _captionDirection?.ButtonPressed == true ? "← " : string.Empty;
        var importance = _captionImportance?.Selected switch
        {
            0 => "information",
            2 => "critique",
            _ => "important",
        };
        _captionState.Text = $"✓ Aperçu runtime · {arrow}{_captionFr.Text.Trim()}\n" +
                             $"fr → fallback localisé · {importance} · 1,5-6 s · lecteur d'écran prêt";
    }

    private void PreviewRuntimeControl(string operation)
    {
        if (_current is null || _audioRuntimePreview is null) return;
        _previewControlState ??= AudioOrchestrationRuntimeService.Create(_current.Audio.Authoring.Id);
        var control = _current.Audio.RuntimeControl;
        var caller = control.Authority;
        _previewControlState = operation switch
        {
            "Start" => AudioOrchestrationRuntimeService.Start(_previewControlState, control, caller),
            "Pause" => AudioOrchestrationRuntimeService.Pause(_previewControlState, control, caller),
            "Resume" => AudioOrchestrationRuntimeService.Resume(_previewControlState, control, caller),
            "Fade" => AudioOrchestrationRuntimeService.Fade(_previewControlState, control, caller, .25f,
                control.DefaultFadeSeconds),
            "Stop" => AudioOrchestrationRuntimeService.Stop(_previewControlState, control, caller),
            _ => _previewControlState,
        };
        _audioRuntimePreview.Text = _previewControlState.Diagnostics.Count > 0
            ? $"✕ {_previewControlState.Diagnostics[0].Code}"
            : $"Handle {_previewControlState.Handle.Id.ToString("N")[..8]}:{_previewControlState.Handle.Generation} · " +
              $"{_previewControlState.Status.ToString().ToLowerInvariant()} · gain {_previewControlState.Gain:0.00} · " +
              $"{_previewControlState.Events.LastOrDefault() ?? "aucun événement"}";
    }

    private void RefreshR062Previews()
    {
        if (_current is null || _audioAttenuationPreview is null || _mixMonitorPreview is null) return;
        var spatial = new AudioSpatialProfile((AreaSoundMode)_soundMode!.Selected,
            (float)_innerRadius!.Value, (float)_outerRadius!.Value, _current.Audio.Spatial.AirAbsorption,
            _current.Audio.Spatial.AttenuationCurve, _audioDebugRanges!.ButtonPressed);
        var attenuation = AudioOrchestrationRuntimeService.PreviewAttenuation(spatial, 9);
        _audioAttenuationPreview.Text = $"Interne {spatial.InnerRadius:0.0} m · externe {spatial.OuterRadius:0.0} m · " +
                                        $"courbe {spatial.AttenuationCurve}\n" +
                                        string.Join("  ", attenuation.Samples.Where((_, index) => index % 2 == 0)
                                            .Select(item => $"{item.Distance:0}m:{item.Gain:0.00}")) +
                                        $"\nDebug · {attenuation.DebugSpheres.Count} sphère(s)";
        var readings = _current.Audio.Orchestration.MixBuses.ToDictionary(item => item.Name,
            item => (PeakDb: -6f, RmsDb: -12f, LoudnessLufs: -18f), StringComparer.Ordinal);
        var monitoring = AudioOrchestrationRuntimeService.MonitorMix(_current.Audio.Orchestration,
            readings, "all", "default");
        _mixMonitorPreview.Text = monitoring.Count == 0
            ? "Monitoring inactif · activez au moins un bus."
            : string.Join('\n', monitoring.Select(item =>
                $"{item.Bus,-12} pic {item.PeakDb,5:0.0} dB · RMS {item.RmsDb,5:0.0} · {item.LoudnessLufs,5:0.0} LUFS · gain {item.EffectiveGain:0.00}"));
    }

    private void SyncAudioOrder(int index, bool fromWorkbench)
    {
        if (_syncingAudioControls) return;
        _syncingAudioControls = true;
        if (fromWorkbench) _audioOrder!.Select(index);
        else _r061Order!.Select(index);
        _syncingAudioControls = false;
    }

    private void SyncLegacyIntervalToWorkbench()
    {
        if (_syncingAudioControls) return;
        _syncingAudioControls = true;
        _audioMinimumInterval!.Value = Math.Max(0, _audioInterval!.Value - _audioVariation!.Value);
        _audioMaximumInterval!.Value = Math.Max(0, _audioInterval.Value + _audioVariation.Value);
        _syncingAudioControls = false;
    }

    private void SyncWorkbenchIntervalToLegacy()
    {
        if (_syncingAudioControls) return;
        _syncingAudioControls = true;
        var minimum = Math.Min(_audioMinimumInterval!.Value, _audioMaximumInterval!.Value);
        var maximum = Math.Max(_audioMinimumInterval.Value, _audioMaximumInterval.Value);
        _audioInterval!.Value = (minimum + maximum) / 2;
        _audioVariation!.Value = (maximum - minimum) / 2;
        _syncingAudioControls = false;
    }

    private void RefreshAudioSearch()
    {
        if (_current is null || _audioSearchResults is null || _audioWaveform is null) return;
        try
        {
            var results = AudioResourceBrowser.Search(_current.Audio.Emitters,
                new AudioResourceSearch(_audioSearch?.Text ?? string.Empty, Limit: 20));
            if (results.Count == 0)
            {
                _audioSearchResults.Text = "État vide · aucune ressource ne correspond aux facettes.";
                _audioWaveform.Text = "Waveform  ▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁";
                return;
            }
            var first = results[0];
            _audioSearchResults.Text = $"{results.Count} résultat(s) · {first.Name}\n" +
                $"{first.ResourcePath} · {first.LoudnessLufs:0.0} LUFS · {first.DurationSeconds:0.0} s\n" +
                $"{first.LicenseSpdx} · {first.Provenance}\n" +
                $"Tags: {string.Join(", ", first.Tags)}\nTranscript: " +
                (string.IsNullOrWhiteSpace(first.Transcript) ? "non renseigné" : first.Transcript);
            _audioWaveform.Text = $"Waveform  {RenderWaveform(first.WaveformPeaks)}";
        }
        catch (ArgumentException exception)
        {
            _audioSearchResults.Text = $"✕ audio.preview.invalid · {exception.Message}";
        }
    }

    private void AuditionAudioSearch()
    {
        if (_current is null || _audioAuditionPlayer is null) return;
        var results = AudioResourceBrowser.Search(_current.Audio.Emitters,
            new AudioResourceSearch(_audioSearch?.Text ?? string.Empty, Limit: 20));
        var plan = AudioResourceBrowser.PlanAudition(results, _current.Audio.Authoring.Mix, batch: false);
        if (plan.Resources.Count == 0)
        {
            _status!.Text = "✕ audio.preview.empty · aucune ressource à écouter";
            return;
        }
        var resource = plan.Resources[0];
        var path = string.IsNullOrWhiteSpace(resource.PreviewResourcePath)
            ? resource.ResourcePath
            : resource.PreviewResourcePath;
        var uri = path.StartsWith("res://", StringComparison.Ordinal) ? path : $"res://{path}";
        if (!ResourceLoader.Exists(uri) || ResourceLoader.Load<AudioStream>(uri) is not { } stream)
        {
            _status!.Text = $"✕ audio.preview.resource_unavailable · importez {path} dans le projet";
            return;
        }
        _audioAuditionPlayer.Stream = stream;
        _audioAuditionPlayer.Bus = AudioServer.GetBusIndex(plan.Bus) >= 0 ? plan.Bus : "Master";
        _audioAuditionPlayer.VolumeDb = Mathf.LinearToDb(Math.Max(.0001f, plan.NormalizedGain));
        _audioAuditionPlayer.Play();
        _status!.Text = $"▶ Aperçu · {resource.Name} · {plan.Bus} · gain {plan.NormalizedGain:0.00}";
    }

    private void RefreshAudioStatistics()
    {
        if (_current is null || _audioStatisticalPreview is null) return;
        try
        {
            var profile = BuildWorkbenchAudioProfile();
            var statistics = AreaAudioSequencePlanner.PreviewStatistics(profile, 512, (int)_audioSeed!.Value);
            _audioStatisticalPreview.Text = statistics.SampleCount == 0
                ? "Aperçu · playlist vide."
                : $"✓ 512 tirages · délai {statistics.MinimumDelaySeconds:0.00}-{statistics.MaximumDelaySeconds:0.00} s " +
                  $"(moy. {statistics.MeanDelaySeconds:0.00})\npitch {statistics.MinimumPitchSemitones:0.0}-" +
                  $"{statistics.MaximumPitchSemitones:0.0} · volume moy. {statistics.MeanRelativeVolume:0.00}";
        }
        catch (ArgumentException exception)
        {
            _audioStatisticalPreview.Text = $"✕ audio.modulation.invalid · {exception.Message}";
        }
    }

    private AreaAudioProfile BuildWorkbenchAudioProfile()
    {
        var source = _current!.Audio;
        var playlist = _playlist!.Text.Split(',',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var authoring = source.Authoring with
        {
            Selection = new((int)_audioSeed!.Value, (int)_audioNoRepeat!.Value,
                ParseFloatMap(_audioWeights!.Text, .0001f, 100_000)),
            Modulation = new((float)_audioMinimumInterval!.Value, (float)_audioMaximumInterval!.Value,
                (float)_audioMinimumPitch!.Value, (float)_audioMaximumPitch!.Value, .85f, 1.15f,
                (AudioModulationDistribution)_audioDistribution!.Selected, ParseCurve(_audioCurve!.Text),
                (int)_audioSeed.Value, _audioTempoSync!.ButtonPressed, (float)_audioTempo!.Value,
                (float)_audioBeatDivision!.Value),
            Mix = new((float)_audioTargetLoudness!.Value, _audioBus!.Text.Trim(),
                (float)_audioDucking!.Value, 1, (float)_audioAccessibilityCap!.Value,
                (AudioMeterMode)_audioMeter!.Selected),
            Ambient = new(_audioAreaScope!.Text.Trim(), _audioBus.Text.Trim(),
                (AudioListenerPolicy)_audioListenerPolicy!.Selected,
                ParseFloatMap(_audioListenerGains!.Text, 0, 4)),
            RequiredAtRuntime = _audioRequired!.ButtonPressed,
        };
        return new(source.DayMusic, source.NightMusic, source.CombatMusic, source.Ambience,
            source.ReverbPreset, source.TransitionSeconds, playlist,
            (AudioOrderMode)_r061Order!.Selected, (float)_audioInterval!.Value,
            (float)_audioVariation!.Value, (float)_audioVolume!.Value, source.Spatial,
            source.RuntimeControl, source.AdaptiveLayers, source.Orchestration, source.Emitters, authoring);
    }

    private void PrepareR061FieldsForCapture()
    {
        _audioSearch!.Text = "pluie forêt";
        _audioSearchResults!.Text = "3 résultats gouvernés · Pluie douce sous les arbres\n" +
            "audio/forest/rain-soft.ogg · -21.4 LUFS · 48.2 s\n" +
            "CC0-1.0 · asset-vault:forgotten-forest\nTags: pluie, forêt, ambiance · Transcript: pluie légère et feuillage";
        _audioWaveform!.Text = "Waveform  ▁▂▅▇▆▃▂▄█▆▃▂▅▇▄▁▃▆█▅▂▁▄▇";
        _r061Order!.Selected = (int)AudioOrderMode.Weighted;
        _audioSeed!.Value = 2026;
        _audioNoRepeat!.Value = 3;
        _audioWeights!.Text = "rain-soft.ogg=3, wind-leaves.ogg=1, owl.ogg=0.5";
        _audioDistribution!.Selected = (int)AudioModulationDistribution.Triangular;
        _audioCurve!.Text = "0:0, 0.5:0.35, 1:1";
        _audioMinimumInterval!.Value = 8;
        _audioMaximumInterval!.Value = 18;
        _audioMinimumPitch!.Value = -.8;
        _audioMaximumPitch!.Value = .8;
        _audioTempoSync!.ButtonPressed = true;
        _audioTempo!.Value = 96;
        _audioBeatDivision!.Value = 2;
        _audioStatisticalPreview!.Text = "✓ 512 tirages · délai 8,75-17,50 s (moy. 12,92)\n" +
            "pitch -0,8 à +0,8 · aucune répétition sur 3 cues";
        _audioTargetLoudness!.Value = -18;
        _audioBus!.Text = "Ambience";
        _audioDucking!.Value = -6;
        _audioAccessibilityCap!.Value = .85;
        _audioMeter!.Selected = (int)AudioMeterMode.Loudness;
        _audioAreaScope!.Text = "Crypte des Cendres";
        _audioListenerPolicy!.Selected = (int)AudioListenerPolicy.Personalized;
        _audioListenerGains!.Text = "joueur-local=0.85, compagnon=0.65";
        _audioRequired!.ButtonPressed = true;
        _status!.Text = "✓ R061 prêt · preview, seed, LUFS et personnalisation validés";
    }

    private void PrepareR062FieldsForCapture()
    {
        _soundMode!.Selected = (int)AreaSoundMode.Spatial3D;
        _innerRadius!.Value = 3;
        _outerRadius!.Value = 42;
        _audioDebugRanges!.ButtonPressed = true;
        _audioAttenuationPreview!.Text = "Interne 3,0 m · externe 42,0 m · courbe inverse-square\n" +
                                         "0m:1,00  10m:0,76  21m:0,33  31m:0,16  42m:0,00\n" +
                                         "Debug · 2 sphères visibles dans la vue 3D";
        _audioRuntimePreview!.Text = "Handle a0620000:1 · paused · gain 0,75 · audio.paused · API 1.0";
        _adaptiveResource!.Text = "audio/forest/night-rain.ogg";
        _adaptiveWeather!.Text = "rain";
        _adaptiveTag!.Text = "forest";
        _adaptiveCrossfade!.Value = 3;
        _adaptiveVolumeId!.Text = "forest-volume";
        _musicBeatSync!.ButtonPressed = true;
        _musicTempo!.Value = 120;
        _musicBeatsPerBar!.Value = 4;
        _mixMonitor!.ButtonPressed = true;
        _mixMonitorPreview!.Text = "Master       pic  -3,0 dB · RMS  -9,0 · -14,0 LUFS · gain 0,80\n" +
                                   "Music        pic  -6,0 dB · RMS -12,0 · -18,0 LUFS · gain 0,64\n" +
                                   "Ambience     pic  -8,0 dB · RMS -15,0 · -21,0 LUFS · gain 0,56";
        _status!.Text = "✓ R062 prêt · portée, scripts, crossfade, musique et bus monitorés";
    }

    private void PrepareR063FieldsForCapture()
    {
        _dayMusic!.Text = "builtin://audio/forest-day";
        _nightMusic!.Text = "builtin://audio/forest-night";
        _combatMusic!.Text = "builtin://audio/combat";
        _ambience!.Text = "builtin://audio/forest";
        _captionResource!.Text = "builtin://audio/dragon-roar";
        _captionFr!.Text = "[Rugissement de dragon à gauche]";
        _captionDirection!.ButtonPressed = true;
        _captionKind!.Selected = 1;
        _captionImportance!.Selected = 2;
        _captionRequired!.ButtonPressed = true;
        RefreshCaptionPreview();
        _status!.Text = "✓ R063 prêt · caption localisée, directionnelle, gouvernée et accessible";
    }

    private static IReadOnlyDictionary<string, float> ParseFloatMap(string text, float minimum, float maximum)
    {
        var result = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var entry in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = entry.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) ||
                !float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var value) ||
                !float.IsFinite(value) || value < minimum || value > maximum)
                throw new ArgumentException($"Valeur clé=nombre invalide : '{entry}'.");
            result.Add(parts[0], value);
        }
        return result;
    }

    private static IReadOnlyList<float> ParseFloatList(string text, float minimum, float maximum)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        return text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(entry =>
            {
                if (!float.TryParse(entry, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var value) ||
                    !float.IsFinite(value) || value < minimum || value > maximum)
                    throw new ArgumentException($"Valeur numérique invalide : '{entry}'.");
                return value;
            }).ToArray();
    }

    private static IReadOnlyList<AudioModulationCurvePoint> ParseCurve(string text)
    {
        var result = new List<AudioModulationCurvePoint>();
        foreach (var entry in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = entry.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 ||
                !float.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var position) ||
                !float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var value))
                throw new ArgumentException($"Point de courbe invalide : '{entry}'.");
            result.Add(new(position, value));
        }
        return result;
    }

    private static string RenderWaveform(IReadOnlyList<float> peaks)
    {
        const string levels = "▁▂▃▄▅▆▇█";
        return string.Concat(peaks.Take(48).Select(peak => levels[Math.Clamp(
            (int)MathF.Round(peak * (levels.Length - 1)), 0, levels.Length - 1)]));
    }

    private void PrepareR054FieldsForCapture()
    {
        _classification!.Selected = (int)AreaEnvironmentClass.Exterior;
        _classificationKey!.Text = "custom:moonlit-forest";
        _classificationPack!.Text = "forest-environments-2026";
        _classificationRendering!.ButtonPressed = true;
        _classificationAudio!.ButtonPressed = true;
        _classificationNavigation!.ButtonPressed = true;
        _classificationWeather!.ButtonPressed = true;
        _classificationCamera!.ButtonPressed = true;
        _classificationGameplay!.ButtonPressed = true;
        _environmentRequired!.ButtonPressed = true;
        _lightingPreset!.Text = "physical-moonlit";
        _ambientColor!.Text = "#30405f";
        _directionalColor!.Text = "#ffe2b5";
        _simpleLighting!.ButtonPressed = false;
        _noonSun!.Value = 1.4;
        _midnightMoon!.Value = .8;
        _timedEventHour!.Value = 18;
        _timedEventName!.Text = "world.sunset";
        _timedEventScript!.Text = "scripts/sunset.forge";
        _probeEnabled!.ButtonPressed = true;
        _probeAsset!.Text = "asset:forest-reflection-probe";
        _probeRadius!.Value = 24;
        _lightingVolumeEnabled!.ButtonPressed = true;
        _volumeLightingPreset!.Text = "physical-moon-shrine";
        _lightingVolumeBlend!.Value = 3;
        _fogMode!.Selected = (int)FogMode.Volumetric;
        _fogDayColor!.Text = "#b7c9dd";
        _fogNightColor!.Text = "#263248";
        _fogNoiseAsset!.Text = "asset:fog-noise-soft";
        _fogNoiseScale!.Value = 12;
        _localFogEnabled!.ButtonPressed = true;
        _localFogDensity!.Value = .3;
        _localFogColor!.Text = "#9fb4c8";
        _fogQuality!.Selected = (int)AreaFogQualityTier.High;
        _r054Preview!.Text = "✓ Aperçu 18:00 · soleil 0,35 · lune 0,20 · événement world.sunset\n" +
            "1 probe · 1 override lumière · 1 volume brouillard · qualité High";
        _status!.Text = "✓ R054 prêt · mutation canonique annulable · runtime requis";
    }

    private static ScrollContainer Tab(TabContainer tabs, string name)
    {
        var scroll = new ScrollContainer
        {
            Name = name,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        tabs.AddChild(scroll);
        return scroll;
    }

    private static VBoxContainer Card(Container parent, string title)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(360, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("15111f"),
            BorderColor = new Color("4f3b67"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 9,
            CornerRadiusTopRight = 9,
            CornerRadiusBottomLeft = 9,
            CornerRadiusBottomRight = 9,
            ContentMarginLeft = 14,
            ContentMarginTop = 12,
            ContentMarginRight = 14,
            ContentMarginBottom = 12,
        });
        parent.AddChild(panel);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 7);
        var heading = new Label { Text = title };
        heading.AddThemeColorOverride("font_color", new Color("ffb06d"));
        content.AddChild(heading);
        panel.AddChild(content);
        return content;
    }

    private static OptionButton EnumOption<T>() where T : struct, Enum
    {
        var button = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var value in Enum.GetValues<T>()) button.AddItem(value.ToString());
        return button;
    }

    private static LineEdit Field(string hint) => new() { PlaceholderText = hint };

    private static SpinBox Spin(double min, double max, double value, double step) => new()
    {
        MinValue = min,
        MaxValue = max,
        Value = value,
        Step = step,
        CustomMinimumSize = new Vector2(110, 0),
    };

    private static CheckBox Check(string label, bool value) => new() { Text = label, ButtonPressed = value };

    private static HBoxContainer FieldRow(string text, Control control)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = text, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        row.AddChild(control);
        return row;
    }
}
#endif
