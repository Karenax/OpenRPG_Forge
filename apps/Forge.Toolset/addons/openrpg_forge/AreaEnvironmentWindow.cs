#if TOOLS
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
    private OptionButton? _soundMode;
    private SpinBox? _innerRadius;
    private SpinBox? _outerRadius;
    private CheckBox? _initiallyActive;
    private OptionButton? _instancePolicy;
    private OptionButton? _authority;
    private LineEdit? _adaptiveResource;
    private LineEdit? _adaptiveWeather;
    private LineEdit? _adaptiveTag;
    private LineEdit? _captionResource;
    private LineEdit? _captionFr;
    private CheckBox? _captionDirection;
    private LineEdit? _emitterName;
    private LineEdit? _emitterResource;
    private LineEdit? _emitterPreview;
    private LineEdit? _emitterTags;
    private LineEdit? _emitterLocale;
    private LineEdit? _emitterLicense;
    private LineEdit? _emitterProvenance;
    private LineEdit? _emitterManifest;
    private LineEdit? _emitterHash;
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
    private TabContainer? _tabs;
    private string? _capturePath;
    private double _captureDelay;
    private bool _captureOpened;
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
        GD.Print($"OPENRPG_FORGE_AUDIO_EMITTER_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} " +
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
        foreach (var field in new Control[] { _emitterName, _emitterResource, _emitterPreview, _emitterTags,
                     _emitterLocale, _emitterLicense, _emitterProvenance, _emitterManifest, _emitterHash })
            identity.AddChild(field);
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
        _soundMode!.Selected = (int)profile.Audio.Spatial.Mode;
        _innerRadius!.Value = profile.Audio.Spatial.InnerRadius;
        _outerRadius!.Value = profile.Audio.Spatial.OuterRadius;
        _initiallyActive!.ButtonPressed = profile.Audio.RuntimeControl.InitiallyActive;
        _instancePolicy!.Selected = (int)profile.Audio.RuntimeControl.InstancePolicy;
        _authority!.Selected = (int)profile.Audio.RuntimeControl.Authority;
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
                : [new(_adaptiveResource.Text.Trim(), 18, 6, _adaptiveWeather!.Text.Trim(), _adaptiveTag!.Text.Trim(), 0, false, 1)];
            var master = Guid.NewGuid();
            var music = Guid.NewGuid();
            IReadOnlyList<SoundCaption> captions = string.IsNullOrWhiteSpace(_captionResource!.Text) || string.IsNullOrWhiteSpace(_captionFr!.Text)
                ? []
                : [new(_captionResource.Text.Trim(), [new("fr", _captionFr.Text.Trim())], _captionDirection!.ButtonPressed, "important")];
            var orchestration = new AreaAudioOrchestration(
                [
                    new(MusicState.Exploration, _dayMusic!.Text, [], 2, 5),
                    new(MusicState.Tension, _nightMusic!.Text, [], 2, 5),
                    new(MusicState.Combat, _combatMusic!.Text, ["combat-drums"], 1, 3),
                ],
                [
                    new(master, "Master", null, 1, "default", "all", .8f, true),
                    new(music, "Music", master, 1, "music", "all", .8f, false),
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
                    (long)_emitterMemory!.Value, _emitterLicense!.Text.Trim(), provenance, hash);
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
            var audio = new AreaAudioProfile(
                _dayMusic.Text,
                _nightMusic.Text,
                _combatMusic.Text,
                _ambience!.Text,
                string.IsNullOrWhiteSpace(_reverb!.Text) ? "neutral" : _reverb.Text,
                2,
                _playlist!.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                (AudioOrderMode)_audioOrder!.Selected,
                (float)_audioInterval!.Value,
                (float)_audioVariation!.Value,
                (float)_audioVolume!.Value,
                new((AreaSoundMode)_soundMode!.Selected, (float)_innerRadius!.Value, (float)_outerRadius!.Value, .1f, "linear"),
                new(_initiallyActive!.ButtonPressed, (AudioInstancePolicy)_instancePolicy!.Selected, (AudioAuthority)_authority!.Selected, 1, "audio.started", "audio.stopped"),
                layers,
                orchestration,
                emitters);
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
                reverb);
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
