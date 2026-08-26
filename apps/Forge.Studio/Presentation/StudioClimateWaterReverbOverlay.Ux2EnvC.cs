using System.Globalization;
using Forge.Domain.Areas;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal static class StudioUx2EnvCNumberText
{
    internal static void Set(SpinBox control, double value)
    {
        control.Value = value;
        control.GetLineEdit().Text = value.ToString("0.#######", CultureInfo.InvariantCulture);
    }
}

internal sealed partial class StudioUx2EnvCVectorEditor : HBoxContainer
{
    private readonly SpinBox _x;
    private readonly SpinBox _y;
    private readonly SpinBox _z;

    internal event Action? Changed;

    internal Vector3Value Value => new((float)_x.Value, (float)_y.Value, (float)_z.Value);
    internal IReadOnlyList<SpinBox> Axes => [_x, _y, _z];

    internal StudioUx2EnvCVectorEditor(Vector3Value value)
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 5);
        _x = Axis(value.X); _y = Axis(value.Y); _z = Axis(value.Z);
        AddChild(StudioUx2EnvBControls.Field("X", _x, 82));
        AddChild(StudioUx2EnvBControls.Field("Y", _y, 82));
        AddChild(StudioUx2EnvBControls.Field("Z", _z, 82));
        _x.ValueChanged += _ => Changed?.Invoke();
        _y.ValueChanged += _ => Changed?.Invoke();
        _z.ValueChanged += _ => Changed?.Invoke();
    }

    internal void SetEditingEnabled(bool enabled)
    {
        _x.Editable = enabled; _y.Editable = enabled; _z.Editable = enabled;
    }

    private static SpinBox Axis(float value)
    {
        var control = StudioUx2EnvBControls.Number(-1_000_000, 1_000_000, .1,
            allowGreater: true, allowLesser: true);
        StudioUx2EnvCNumberText.Set(control, value);
        return control;
    }
}

internal sealed partial class StudioUx2EnvCWindZoneRow : StudioUx2EnvBRowBase<StudioUx2EnvCWindZoneDraft>
{
    private readonly Guid _id;
    private readonly StudioUx2EnvCVectorEditor _center;
    private readonly SpinBox _radius;
    private readonly SpinBox _directionX;
    private readonly SpinBox _directionZ;
    private readonly SpinBox _speed;
    private readonly SpinBox _gust;

    public override StudioUx2EnvCWindZoneDraft Value => new(
        _id, _center.Value, (float)_radius.Value, (float)_directionX.Value,
        (float)_directionZ.Value, (float)_speed.Value, (float)_gust.Value);

    internal StudioUx2EnvCWindZoneRow(StudioUx2EnvCWindZoneDraft value) : base("Zone de vent locale")
    {
        _id = value.Id;
        _center = new StudioUx2EnvCVectorEditor(value.Center);
        _radius = Number(0, 1_000_000, .1, value.Radius, greater: true);
        _directionX = Number(-1, 1, .01, value.DirectionX);
        _directionZ = Number(-1, 1, .01, value.DirectionZ);
        _speed = Number(0, 1_000_000, .1, value.Speed, greater: true);
        _gust = Number(0, 8, .05, value.GustMultiplier);
        Root.AddChild(StudioUx2EnvBControls.Field("Centre", _center, 320));
        var grid = new GridContainer { Columns = 5, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddChild(StudioUx2EnvBControls.Field("Rayon (m)", _radius));
        grid.AddChild(StudioUx2EnvBControls.Field("Direction X", _directionX));
        grid.AddChild(StudioUx2EnvBControls.Field("Direction Z", _directionZ));
        grid.AddChild(StudioUx2EnvBControls.Field("Vitesse", _speed));
        grid.AddChild(StudioUx2EnvBControls.Field("Rafale", _gust));
        Root.AddChild(grid);
        foreach (var axis in _center.Axes) Watch(axis);
        Watch(_radius); Watch(_directionX); Watch(_directionZ); Watch(_speed); Watch(_gust);
    }

    public override void SetEditingEnabled(bool enabled)
    {
        base.SetEditingEnabled(enabled); _center.SetEditingEnabled(enabled);
        foreach (var control in new[] { _radius, _directionX, _directionZ, _speed, _gust })
            control.Editable = enabled;
    }

    private static SpinBox Number(double min, double max, double step, double value, bool greater = false)
    {
        var control = StudioUx2EnvBControls.Number(min, max, step, greater);
        StudioUx2EnvCNumberText.Set(control, value);
        return control;
    }
}

internal sealed partial class StudioUx2EnvCWeatherStateRow : StudioUx2EnvBRowBase<StudioUx2EnvCWeatherStateDraft>
{
    private readonly Guid _id;
    private readonly LineEdit _name;
    private readonly OptionButton _kind;
    private readonly SpinBox _intensity;
    private readonly SpinBox _humidity;
    private readonly SpinBox _wetness;
    private readonly SpinBox _wetnessAlbedoDarkening;
    private readonly SpinBox _wetnessRoughnessReduction;
    private readonly SpinBox _wetnessSpecularIncrease;
    private readonly CheckBox _lightning;
    private readonly LineEdit _precipitation;
    private readonly LineEdit _audio;
    private readonly LineEdit _gameplay;
    private readonly SpinBox _minimum;
    private readonly SpinBox _maximum;
    private readonly SpinBox _transition;
    private readonly SpinBox _precipitationDirectionX;
    private readonly SpinBox _precipitationDirectionZ;
    private readonly SpinBox _precipitationWindInfluence;
    private readonly SpinBox _precipitationSpread;
    private readonly SpinBox _precipitationMinimumVelocity;
    private readonly SpinBox _precipitationMaximumVelocity;
    private readonly SpinBox _precipitationGravity;
    private readonly SpinBox _precipitationDensity;
    private readonly SpinBox _precipitationMinimumParticles;
    private readonly SpinBox _precipitationWidth;
    private readonly SpinBox _precipitationLength;
    private readonly SpinBox _precipitationLifetime;
    private readonly SpinBox _precipitationRandomness;
    private readonly LineEdit _precipitationColor;

    public override StudioUx2EnvCWeatherStateDraft Value => new(
        _id, _name.Text, (WeatherKind)_kind.Selected, (float)_intensity.Value,
        (float)_humidity.Value, (float)_wetness.Value,
        new SurfaceWetnessMaterialProfile(
            SurfaceWetnessMaterialProfile.CurrentSchemaVersion,
            (float)_wetnessAlbedoDarkening.Value,
            (float)_wetnessRoughnessReduction.Value,
            (float)_wetnessSpecularIncrease.Value),
        _lightning.ButtonPressed,
        _precipitation.Text, _audio.Text, _gameplay.Text, (float)_minimum.Value,
        (float)_maximum.Value, (float)_transition.Value,
        new PrecipitationVisualProfile(
            PrecipitationVisualProfile.CurrentSchemaVersion,
            (float)_precipitationDirectionX.Value,
            (float)_precipitationDirectionZ.Value,
            (float)_precipitationWindInfluence.Value,
            (float)_precipitationSpread.Value,
            (float)_precipitationMinimumVelocity.Value,
            (float)_precipitationMaximumVelocity.Value,
            (float)_precipitationGravity.Value,
            (float)_precipitationDensity.Value,
            checked((int)_precipitationMinimumParticles.Value),
            (float)_precipitationWidth.Value,
            (float)_precipitationLength.Value,
            (float)_precipitationLifetime.Value,
            (float)_precipitationRandomness.Value,
            _precipitationColor.Text));

    internal StudioUx2EnvCWeatherStateRow(StudioUx2EnvCWeatherStateDraft value) : base("État météo")
    {
        _id = value.Id;
        _name = Text(value.Name); _kind = StudioUx2EnvBControls.Options("Clair", "Couvert", "Pluie", "Orage", "Neige", "Brouillard");
        _kind.Select((int)value.Kind);
        _intensity = Number(0, 1, .01, value.Intensity);
        _humidity = Number(0, 1, .01, value.Humidity);
        _wetness = Number(0, 1, .01, value.SurfaceWetness);
        _wetnessAlbedoDarkening = Number(0, .35, .01, value.WetnessMaterial.AlbedoDarkening);
        _wetnessRoughnessReduction = Number(0, .8, .01, value.WetnessMaterial.RoughnessReduction);
        _wetnessSpecularIncrease = Number(0, .6, .01, value.WetnessMaterial.SpecularIncrease);
        _lightning = StudioUx2EnvBControls.Check("Éclairs déclarés"); _lightning.ButtonPressed = value.Lightning;
        _precipitation = Text(value.PrecipitationResource);
        _audio = Text(value.AudioCue); _gameplay = Text(value.GameplayEffect);
        _minimum = Number(0, 1_000_000, 1, value.MinimumDurationSeconds, greater: true);
        _maximum = Number(0, 1_000_000, 1, value.MaximumDurationSeconds, greater: true);
        _transition = Number(0, 3_600, .25, value.TransitionSeconds);
        var particles = value.Precipitation;
        _precipitationDirectionX = Number(-4, 4, .01, particles.DirectionX);
        _precipitationDirectionZ = Number(-4, 4, .01, particles.DirectionZ);
        _precipitationWindInfluence = Number(0, 4, .01, particles.WindInfluence);
        _precipitationSpread = Number(0, 180, .25, particles.SpreadDegrees);
        _precipitationMinimumVelocity = Number(0, 100, .1, particles.MinimumVelocity);
        _precipitationMaximumVelocity = Number(0, 100, .1, particles.MaximumVelocity);
        _precipitationGravity = Number(-100, 100, .1, particles.GravityY);
        _precipitationDensity = Number(0, 100_000, 1, particles.ParticlesPerIntensity);
        _precipitationMinimumParticles = Number(0, 100_000, 1, particles.MinimumParticles);
        _precipitationWidth = Number(.001, 10, .001, particles.ParticleWidth);
        _precipitationLength = Number(.001, 20, .001, particles.ParticleLength);
        _precipitationLifetime = Number(.1, 120, .1, particles.LifetimeSeconds);
        _precipitationRandomness = Number(0, 1, .01, particles.Randomness);
        _precipitationColor = Text(particles.Color);
        var identity = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        identity.AddChild(StudioUx2EnvBControls.Field("Nom", _name, 200));
        identity.AddChild(StudioUx2EnvBControls.Field("Phénomène", _kind, 160));
        identity.AddChild(_lightning);
        Root.AddChild(identity);
        var state = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        state.AddChild(StudioUx2EnvBControls.Field("Intensité", _intensity));
        state.AddChild(StudioUx2EnvBControls.Field("Humidité", _humidity));
        state.AddChild(StudioUx2EnvBControls.Field("Sol humide", _wetness));
        state.AddChild(StudioUx2EnvBControls.Field("Assombrissement mouillé", _wetnessAlbedoDarkening));
        state.AddChild(StudioUx2EnvBControls.Field("Réduction de rugosité", _wetnessRoughnessReduction));
        state.AddChild(StudioUx2EnvBControls.Field("Gain spéculaire", _wetnessSpecularIncrease));
        state.AddChild(StudioUx2EnvBControls.Field("Durée minimale (s)", _minimum));
        state.AddChild(StudioUx2EnvBControls.Field("Durée maximale (s)", _maximum));
        state.AddChild(StudioUx2EnvBControls.Field("Transition (s)", _transition));
        Root.AddChild(state);
        var bindings = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        bindings.AddChild(StudioUx2EnvBControls.Field("Ressource de précipitation", _precipitation, 220));
        bindings.AddChild(StudioUx2EnvBControls.Field("Cue audio", _audio, 180));
        bindings.AddChild(StudioUx2EnvBControls.Field("Effet gameplay déclaratif", _gameplay, 220));
        Root.AddChild(bindings);
        var motion = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        motion.AddChild(StudioUx2EnvBControls.Field("Direction X", _precipitationDirectionX));
        motion.AddChild(StudioUx2EnvBControls.Field("Direction Z", _precipitationDirectionZ));
        motion.AddChild(StudioUx2EnvBControls.Field("Influence du vent", _precipitationWindInfluence));
        motion.AddChild(StudioUx2EnvBControls.Field("Dispersion (°)", _precipitationSpread));
        motion.AddChild(StudioUx2EnvBControls.Field("Vitesse minimale", _precipitationMinimumVelocity));
        motion.AddChild(StudioUx2EnvBControls.Field("Vitesse maximale", _precipitationMaximumVelocity));
        motion.AddChild(StudioUx2EnvBControls.Field("Gravité Y", _precipitationGravity));
        motion.AddChild(StudioUx2EnvBControls.Field("Densité", _precipitationDensity));
        Root.AddChild(motion);
        var appearance = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        appearance.AddChild(StudioUx2EnvBControls.Field("Particules minimales", _precipitationMinimumParticles));
        appearance.AddChild(StudioUx2EnvBControls.Field("Largeur", _precipitationWidth));
        appearance.AddChild(StudioUx2EnvBControls.Field("Longueur", _precipitationLength));
        appearance.AddChild(StudioUx2EnvBControls.Field("Durée de vie", _precipitationLifetime));
        appearance.AddChild(StudioUx2EnvBControls.Field("Aléatoire", _precipitationRandomness));
        appearance.AddChild(StudioUx2EnvBControls.Field("Couleur RGBA", _precipitationColor));
        Root.AddChild(appearance);
        Watch(_name); Watch(_kind); Watch(_intensity); Watch(_humidity); Watch(_wetness);
        Watch(_wetnessAlbedoDarkening); Watch(_wetnessRoughnessReduction); Watch(_wetnessSpecularIncrease);
        Watch(_lightning);
        Watch(_precipitation); Watch(_audio); Watch(_gameplay); Watch(_minimum); Watch(_maximum); Watch(_transition);
        foreach (var control in new[] { _precipitationDirectionX, _precipitationDirectionZ,
                     _precipitationWindInfluence, _precipitationSpread, _precipitationMinimumVelocity,
                     _precipitationMaximumVelocity, _precipitationGravity, _precipitationDensity,
                     _precipitationMinimumParticles, _precipitationWidth, _precipitationLength,
                     _precipitationLifetime, _precipitationRandomness }) Watch(control);
        Watch(_precipitationColor);
    }

    public override void SetEditingEnabled(bool enabled)
    {
        base.SetEditingEnabled(enabled);
        foreach (var control in new[] { _name, _precipitation, _audio, _gameplay }) control.Editable = enabled;
        _precipitationColor.Editable = enabled;
        _kind.Disabled = !enabled; _lightning.Disabled = !enabled;
        foreach (var control in new[] { _intensity, _humidity, _wetness, _wetnessAlbedoDarkening,
                     _wetnessRoughnessReduction, _wetnessSpecularIncrease, _minimum, _maximum, _transition })
            control.Editable = enabled;
        foreach (var control in new[] { _precipitationDirectionX, _precipitationDirectionZ,
                     _precipitationWindInfluence, _precipitationSpread, _precipitationMinimumVelocity,
                     _precipitationMaximumVelocity, _precipitationGravity, _precipitationDensity,
                     _precipitationMinimumParticles, _precipitationWidth, _precipitationLength,
                     _precipitationLifetime, _precipitationRandomness }) control.Editable = enabled;
    }

    private static LineEdit Text(string value)
    {
        var control = StudioUx2EnvBControls.Text(); control.Text = value; return control;
    }

    private static SpinBox Number(double min, double max, double step, double value, bool greater = false)
    {
        var control = StudioUx2EnvBControls.Number(min, max, step, greater);
        StudioUx2EnvCNumberText.Set(control, value);
        return control;
    }
}

internal sealed partial class StudioUx2EnvCWaterBodyRow : StudioUx2EnvBRowBase<StudioUx2EnvCWaterBodyDraft>
{
    private readonly Guid _id;
    private readonly LineEdit _name;
    private readonly OptionButton _kind;
    private readonly StudioUx2EnvCVectorEditor _center;
    private readonly StudioUx2EnvCVectorEditor _size;
    private readonly SpinBox _depth;
    private readonly SpinBox _flowX;
    private readonly SpinBox _flowZ;
    private readonly SpinBox _flowSpeed;
    private readonly SpinBox _buoyancy;
    private readonly SpinBox _navigationCost;
    private readonly CheckBox _swimming;
    private readonly OptionButton _quality;
    private readonly LineEdit _material;
    private readonly LineEdit _audio;

    public override StudioUx2EnvCWaterBodyDraft Value => new(
        _id, _name.Text, (WaterBodyKind)_kind.Selected, _center.Value, _size.Value,
        (float)_depth.Value, (float)_flowX.Value, (float)_flowZ.Value, (float)_flowSpeed.Value,
        (float)_buoyancy.Value, (float)_navigationCost.Value, _swimming.ButtonPressed,
        (WaterQualityTier)_quality.Selected, _material.Text, _audio.Text);

    internal StudioUx2EnvCWaterBodyRow(StudioUx2EnvCWaterBodyDraft value) : base("Surface d’eau")
    {
        _id = value.Id;
        _name = Text(value.Name); _kind = StudioUx2EnvBControls.Options("Plan", "Rivière", "Volume");
        _kind.Select((int)value.Kind);
        _center = new StudioUx2EnvCVectorEditor(value.Center);
        _size = new StudioUx2EnvCVectorEditor(value.Size);
        _depth = Number(0, 1_000_000, .1, value.MaximumDepth, greater: true);
        _flowX = Number(-1, 1, .01, value.FlowDirectionX);
        _flowZ = Number(-1, 1, .01, value.FlowDirectionZ);
        _flowSpeed = Number(0, 100, .05, value.FlowSpeed);
        _buoyancy = Number(0, 10, .05, value.Buoyancy);
        _navigationCost = Number(.1, 100, .05, value.NavigationCost);
        _swimming = StudioUx2EnvBControls.Check("Nage autorisée"); _swimming.ButtonPressed = value.SwimmingEnabled;
        _quality = StudioUx2EnvBControls.Options("Basse", "Moyenne", "Haute", "Ultra");
        _quality.Select((int)value.Quality);
        _material = Text(value.MaterialResource); _audio = Text(value.AudioCue);
        var identity = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        identity.AddChild(StudioUx2EnvBControls.Field("Nom", _name, 220));
        identity.AddChild(StudioUx2EnvBControls.Field("Forme", _kind, 160));
        identity.AddChild(_swimming);
        Root.AddChild(identity);
        var geometry = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        geometry.AddChild(StudioUx2EnvBControls.Field("Centre", _center, 320));
        geometry.AddChild(StudioUx2EnvBControls.Field("Taille", _size, 320));
        Root.AddChild(geometry);
        var physics = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        physics.AddChild(StudioUx2EnvBControls.Field("Profondeur max.", _depth));
        physics.AddChild(StudioUx2EnvBControls.Field("Flux X", _flowX));
        physics.AddChild(StudioUx2EnvBControls.Field("Flux Z", _flowZ));
        physics.AddChild(StudioUx2EnvBControls.Field("Vitesse du flux", _flowSpeed));
        physics.AddChild(StudioUx2EnvBControls.Field("Flottabilité déclarée", _buoyancy));
        physics.AddChild(StudioUx2EnvBControls.Field("Coût navigation", _navigationCost));
        physics.AddChild(StudioUx2EnvBControls.Field("Qualité", _quality));
        physics.AddChild(new Control());
        Root.AddChild(physics);
        var resources = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        resources.AddChild(StudioUx2EnvBControls.Field("Matériau relatif", _material, 260));
        resources.AddChild(StudioUx2EnvBControls.Field("Cue audio", _audio, 220));
        Root.AddChild(resources);
        foreach (var axis in _center.Axes.Concat(_size.Axes)) Watch(axis);
        Watch(_name); Watch(_kind); Watch(_depth); Watch(_flowX); Watch(_flowZ); Watch(_flowSpeed);
        Watch(_buoyancy); Watch(_navigationCost); Watch(_swimming); Watch(_quality); Watch(_material); Watch(_audio);
    }

    public override void SetEditingEnabled(bool enabled)
    {
        base.SetEditingEnabled(enabled); _center.SetEditingEnabled(enabled); _size.SetEditingEnabled(enabled);
        _name.Editable = enabled; _kind.Disabled = !enabled; _swimming.Disabled = !enabled;
        _quality.Disabled = !enabled; _material.Editable = enabled; _audio.Editable = enabled;
        foreach (var control in new[] { _depth, _flowX, _flowZ, _flowSpeed, _buoyancy, _navigationCost })
            control.Editable = enabled;
    }

    private static LineEdit Text(string value)
    {
        var control = StudioUx2EnvBControls.Text(); control.Text = value; return control;
    }

    private static SpinBox Number(double min, double max, double step, double value, bool greater = false)
    {
        var control = StudioUx2EnvBControls.Number(min, max, step, greater);
        StudioUx2EnvCNumberText.Set(control, value);
        return control;
    }
}

internal sealed partial class StudioUx2EnvCReverbZoneRow : StudioUx2EnvBRowBase<StudioUx2EnvCReverbZoneDraft>
{
    private readonly Guid _id;
    private readonly LineEdit _name;
    private readonly StudioUx2EnvCVectorEditor _center;
    private readonly StudioUx2EnvCVectorEditor _size;
    private readonly LineEdit _preset;
    private readonly SpinBox _wet;
    private readonly SpinBox _dry;
    private readonly SpinBox _blend;
    private readonly LineEdit _impulse;
    private readonly LineEdit _spdx;
    private readonly LineEdit _sha;
    private readonly LineEdit _provenance;

    public override StudioUx2EnvCReverbZoneDraft Value => new(
        _id, _name.Text, _center.Value, _size.Value, _preset.Text, (float)_wet.Value,
        (float)_dry.Value, (float)_blend.Value, _impulse.Text, _spdx.Text, _sha.Text, _provenance.Text);

    internal StudioUx2EnvCReverbZoneRow(StudioUx2EnvCReverbZoneDraft value) : base("Zone de réverbération")
    {
        _id = value.Id;
        _name = Text(value.Name); _center = new StudioUx2EnvCVectorEditor(value.Center);
        _size = new StudioUx2EnvCVectorEditor(value.Size); _preset = Text(value.Preset);
        _wet = Number(0, 1, .01, value.Wet); _dry = Number(0, 1, .01, value.Dry);
        _blend = Number(0, 60, .05, value.BlendSeconds);
        _impulse = Text(value.ImpulseResource); _spdx = Text(value.LicenseSpdx);
        _sha = Text(value.ContentSha256); _provenance = Text(value.Provenance);
        var identity = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        identity.AddChild(StudioUx2EnvBControls.Field("Nom", _name, 220));
        identity.AddChild(StudioUx2EnvBControls.Field("Preset algorithmique", _preset, 220));
        identity.AddChild(StudioUx2EnvBControls.Field("Centre", _center, 320));
        identity.AddChild(StudioUx2EnvBControls.Field("Taille", _size, 320));
        Root.AddChild(identity);
        var mix = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        mix.AddChild(StudioUx2EnvBControls.Field("Signal traité", _wet));
        mix.AddChild(StudioUx2EnvBControls.Field("Signal direct", _dry));
        mix.AddChild(StudioUx2EnvBControls.Field("Transition (s)", _blend));
        Root.AddChild(mix);
        var impulse = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        impulse.AddChild(StudioUx2EnvBControls.Field("Réponse impulsionnelle relative", _impulse, 280));
        impulse.AddChild(StudioUx2EnvBControls.Field("Licence SPDX", _spdx, 180));
        impulse.AddChild(StudioUx2EnvBControls.Field("Empreinte SHA-256", _sha, 280));
        impulse.AddChild(StudioUx2EnvBControls.Field("Provenance", _provenance, 240));
        Root.AddChild(impulse);
        foreach (var axis in _center.Axes.Concat(_size.Axes)) Watch(axis);
        Watch(_name); Watch(_preset); Watch(_wet); Watch(_dry); Watch(_blend);
        Watch(_impulse); Watch(_spdx); Watch(_sha); Watch(_provenance);
    }

    public override void SetEditingEnabled(bool enabled)
    {
        base.SetEditingEnabled(enabled); _center.SetEditingEnabled(enabled); _size.SetEditingEnabled(enabled);
        foreach (var control in new[] { _name, _preset, _impulse, _spdx, _sha, _provenance })
            control.Editable = enabled;
        foreach (var control in new[] { _wet, _dry, _blend }) control.Editable = enabled;
    }

    private static LineEdit Text(string value)
    {
        var control = StudioUx2EnvBControls.Text(); control.Text = value; return control;
    }

    private static SpinBox Number(double min, double max, double step, double value)
    {
        var control = StudioUx2EnvBControls.Number(min, max, step);
        StudioUx2EnvCNumberText.Set(control, value);
        return control;
    }
}

internal sealed partial class StudioClimateWaterReverbOverlayUx2EnvC : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx2EnvCEvidenceSourceSnapshot? _evidenceSourceSnapshot;
    private string? _evidenceTempRoot;
    private bool _evidenceUsesRealModuleData;
    private bool _evidenceUsesCanonicalFallback;
    private bool _evidenceReleaseSucceeded = true;
    private readonly PanelContainer _dialog;
    private readonly OptionButton _area;
    private readonly Label _selection;
    private readonly TabContainer _tabs;
    private readonly SpinBox _windDirectionX;
    private readonly SpinBox _windDirectionZ;
    private readonly SpinBox _windSpeed;
    private readonly SpinBox _windGustStrength;
    private readonly SpinBox _windGustInterval;
    private readonly CheckBox _windVegetation;
    private readonly CheckBox _windParticles;
    private readonly LineEdit _windAudio;
    private readonly SpinBox _windGameplayForce;
    private readonly StudioUx2EnvBCollectionEditor<StudioUx2EnvCWindZoneDraft> _windZones;
    private readonly OptionButton _initialWeather;
    private readonly StudioUx2EnvBCollectionEditor<StudioUx2EnvCWeatherStateDraft> _weatherStates;
    private readonly CheckBox _weatherAuthority;
    private readonly SpinBox _weatherSeed;
    private readonly CheckBox _weatherPersist;
    private readonly CheckBox _weatherReplicate;
    private readonly StudioUx2EnvBCollectionEditor<StudioUx2EnvCWaterBodyDraft> _waterBodies;
    private readonly LineEdit _reverbDefault;
    private readonly StudioUx2EnvBCollectionEditor<StudioUx2EnvCReverbZoneDraft> _reverbZones;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;
    private IReadOnlyList<StudioUx2EnvCAreaChoice> _choices = [];
    private IReadOnlyList<Guid> _initialWeatherIds = [];
    private StudioUx2EnvCDraft? _draft;
    private StudioUx2EnvCPreview? _lastPreview;
    private bool _updating;
    private bool _applied;
    private bool _canonicalPreviewReady;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? AreaEnvironmentChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _choices.Count;
    internal bool HasExplicitSelection => _draft is not null && _area.Selected > 0;
    internal bool CanonicalPreviewReady => _canonicalPreviewReady && _lastPreview is
    {
        Succeeded: true,
        CanonicalDryRun: true,
        ClimatePlanBuilt: true,
        WeatherSimulationBuilt: true,
        WaterPlansBuilt: true,
        ReverbPlanBuilt: true,
        RuntimeClaimsBounded: true,
    };
    internal bool ShowsCompleteCollections => _draft is not null &&
        _windZones.Values.Count == _draft.WindZones.Count &&
        _weatherStates.Values.Count == _draft.WeatherStates.Count &&
        _waterBodies.Values.Count == _draft.WaterBodies.Count &&
        _reverbZones.Values.Count == _draft.ReverbZones.Count;
    internal bool ShowsStructuredDiagnostics => _lastPreview?.Groups.Count == 4;
    internal bool ShowsHumanSurface
    {
        get
        {
            var tabTitles = Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle);
            var text = string.Join(' ', DescendantVisibleText(this).Concat(tabTitles));
            return text.Contains("Vent", StringComparison.Ordinal) &&
                   text.Contains("Météo", StringComparison.Ordinal) &&
                   text.Contains("Eau", StringComparison.Ordinal) &&
                   text.Contains("Réverbération", StringComparison.Ordinal) &&
                   text.Contains("simulation déterministe", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuid(text);
        }
    }
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealModuleSource => UsesIsolatedEvidence && _evidenceUsesRealModuleData;
    internal bool UsesCanonicalFallbackEvidence => UsesIsolatedEvidence && _evidenceUsesCanonicalFallback;
    internal bool EvidenceSourceUntouched => SourceSnapshotMatches();
    internal bool EvidenceResourcesReleased => _evidenceReleaseSucceeded &&
                                               _evidenceSession is null && _evidenceTempRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);

    internal StudioClimateWaterReverbOverlayUx2EnvC(StudioSessionController session)
    {
        _sourceSession = session; _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.015f, .02f, .027f, .96f);
        MouseFilter = MouseFilterEnum.Stop; Visible = false;
        _dialog = StudioStyle.Panel("Ux2EnvCClimateDialog", StudioStyle.Surface, 12, 1);
        AddChild(_dialog);
        var margin = StudioStyle.Margin(14, 12, 14, 12); _dialog.AddChild(margin);
        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 8); margin.AddChild(root);
        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; root.AddChild(heading);
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        headingText.AddChild(StudioStyle.Label("Climat, eau et réverbération", 18));
        headingText.AddChild(StudioStyle.Label(
            "Authoring complet et simulation déterministe locale · aucun adaptateur Godot ni provider live n’est lancé.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true); close.Pressed += Close; heading.AddChild(close);
        _area = StudioUx2EnvBControls.Options(); _area.ItemSelected += SelectArea;
        root.AddChild(StudioUx2EnvBControls.Field("Zone à modifier · sélection explicite", _area, 420));
        _selection = StudioStyle.Label("Aucune zone sélectionnée.", 10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);
        _tabs = new TabContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(_tabs);

        _windDirectionX = Number(-1, 1, .01); _windDirectionZ = Number(-1, 1, .01);
        _windSpeed = Number(0, 1_000_000, .1, greater: true);
        _windGustStrength = Number(0, 8, .05); _windGustInterval = Number(.1, 3_600, .1);
        _windVegetation = StudioUx2EnvBControls.Check("Influence végétation déclarée");
        _windParticles = StudioUx2EnvBControls.Check("Influence particules déclarée");
        _windAudio = StudioUx2EnvBControls.Text("Cue audio facultatif");
        _windGameplayForce = Number(0, 100, .1);
        _windZones = new StudioUx2EnvBCollectionEditor<StudioUx2EnvCWindZoneDraft>(
            "Ajouter une zone de vent", value => new StudioUx2EnvCWindZoneRow(value),
            () => new StudioUx2EnvCWindZoneDraft(Guid.NewGuid(), Vector3Value.Zero, 8, 1, 0, 3, 1));
        BuildWindTab();

        _initialWeather = StudioUx2EnvBControls.Options();
        _weatherAuthority = StudioUx2EnvBControls.Check("Autorité serveur déclarée (aucune connexion)");
        _weatherSeed = Number(int.MinValue, int.MaxValue, 1, lesser: true, greater: true);
        _weatherPersist = StudioUx2EnvBControls.Check("Persistance d’état déclarée");
        _weatherReplicate = StudioUx2EnvBControls.Check("Réplication déclarée (aucun transport)");
        _weatherStates = new StudioUx2EnvBCollectionEditor<StudioUx2EnvCWeatherStateDraft>(
            "Ajouter un état météo", value => new StudioUx2EnvCWeatherStateRow(value),
            () => new StudioUx2EnvCWeatherStateDraft(Guid.NewGuid(), "Nouvel état", WeatherKind.Clear,
                0, .35f, 0, SurfaceWetnessMaterialProfile.CreateDefault(), false,
                string.Empty, string.Empty, string.Empty, 300, 600, 8,
                PrecipitationVisualProfile.CreateDefault(WeatherKind.Clear)));
        BuildWeatherTab();

        _waterBodies = new StudioUx2EnvBCollectionEditor<StudioUx2EnvCWaterBodyDraft>(
            "Ajouter une surface d’eau", value => new StudioUx2EnvCWaterBodyRow(value),
            () => new StudioUx2EnvCWaterBodyDraft(Guid.NewGuid(), "Nouvelle surface", WaterBodyKind.Plane,
                Vector3Value.Zero, new Vector3Value(10, 1, 10), 2, 1, 0, 0, 1, 1,
                true, WaterQualityTier.Medium, string.Empty, string.Empty));
        BuildWaterTab();

        _reverbDefault = StudioUx2EnvBControls.Text("preset par défaut");
        _reverbZones = new StudioUx2EnvBCollectionEditor<StudioUx2EnvCReverbZoneDraft>(
            "Ajouter une zone de réverbération", value => new StudioUx2EnvCReverbZoneRow(value),
            () => new StudioUx2EnvCReverbZoneDraft(Guid.NewGuid(), "Nouvelle zone", Vector3Value.Zero,
                new Vector3Value(10, 4, 10), "neutral", .3f, 1, .5f,
                string.Empty, string.Empty, string.Empty, "authoring manuel"));
        BuildReverbTab();

        _diff = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _diagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        BuildReviewTab();
        _summary = StudioStyle.Label(
            "Sélectionnez une zone puis lancez l’aperçu sec · aucun changement appliqué.",
            10, StudioStyle.Muted, wrap: true);
        _summary.CustomMinimumSize = new Vector2(0, 50); root.AddChild(_summary);
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 6); root.AddChild(actions);
        _preview = StudioStyle.Button("Prévisualiser", compact: true);
        _apply = StudioStyle.Button("Appliquer", compact: true);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        {
            button.CustomMinimumSize = new Vector2(116, 34); actions.AddChild(button);
        }
        _preview.Pressed += Preview; _apply.Pressed += Apply; _undo.Pressed += Undo;
        _redo.Pressed += Redo; _save.Pressed += Save;
        var actionNote = StudioStyle.Label(
            "Simulation bornée seulement : aucun réseau, ForgeScript, effet de flottabilité, convolution audio ou rendu live.",
            9, StudioStyle.Muted, wrap: true);
        actionNote.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        actionNote.CustomMinimumSize = new Vector2(0, 32); root.AddChild(actionNote);
        _status = StudioStyle.Label("Prêt · sélection explicite requise", 9, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        ConnectInvalidation(); SetEditingEnabled(false); ResetControls();
    }

    internal void Open()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null) ReleaseIsolatedEvidenceSession();
        _session = _sourceSession; Visible = true; UpdateDialogBounds(); RefreshChoices();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceTempRoot is null) return;
        if (Visible || !ReferenceEquals(_session, _sourceSession)) _session.CancelClimateUx2EnvC();
        Visible = false; _draft = null; _lastPreview = null; _canonicalPreviewReady = false; _applied = false;
        ReleaseIsolatedEvidenceSession(); _session = _sourceSession;
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        var approval = _sourceSession.CaptureClimateApprovalUx2EnvC();
        _session = _sourceSession; Visible = true; UpdateDialogBounds(); RefreshChoices();
        _sourceSession.RestoreClimateApprovalUx2EnvC(approval);
    }

    internal bool PrepareEvidence(string mode)
    {
        var sourceApproval = _sourceSession.CaptureClimateApprovalUx2EnvC();
        Close(); _sourceSession.RestoreClimateApprovalUx2EnvC(sourceApproval);
        _evidenceReleaseSucceeded = true;
        var sourceProject = _sourceSession.Project;
        if (sourceProject is null || _sourceSession.History is null || sourceProject.Areas.Count == 0) return false;
        _evidenceSourceSnapshot = CaptureSourceSnapshot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux2-env-c-{Guid.NewGuid():N}");
        var evidenceSession = new StudioSessionController();
        try
        {
            var realArea = sourceProject.Areas.Where(area => area.Environment.Weather.States.Count > 0)
                .OrderByDescending(area => area.Environment.Water.Bodies.Count > 0)
                .ThenByDescending(area => area.Environment.Reverb.Zones.Count > 0)
                .FirstOrDefault();
            _evidenceUsesRealModuleData = realArea is not null;
            _evidenceUsesCanonicalFallback = realArea is null;
            var evidenceProject = realArea is null
                ? StudioSessionController.CreateUx2EnvCContractProject(sourceProject)
                : sourceProject;
            var evidenceAreaId = realArea?.Id ?? evidenceProject.Areas[0].Id;
            var created = ProjectSession.Create(tempRoot, evidenceProject);
            if (!created.Succeeded || !evidenceSession.Open(tempRoot).Succeeded)
            {
                evidenceSession.Close();
                var released = StudioSessionController.DeleteUx2EnvCTemporaryWorkspace(tempRoot);
                ResetEvidenceState();
                _evidenceReleaseSucceeded = released;
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
                        ShowsStructuredDiagnostics && ShowsHumanSurface && UsesIsolatedEvidence &&
                        (UsesRealModuleSource ^ UsesCanonicalFallbackEvidence) && EvidenceSourceUntouched;
            if (!ready) { Close(); return false; }
            return true;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_C_EVIDENCE_FAILED mode={mode} " +
                        $"type={exception.GetType().Name} message={exception.Message}");
            var sessionReleased = true;
            try { evidenceSession.Close(); }
            catch (Exception releaseException)
            {
                sessionReleased = false;
                GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_C_EVIDENCE_RELEASE_FAILED " +
                            $"type={releaseException.GetType().Name} message={releaseException.Message}");
            }
            var released = StudioSessionController.DeleteUx2EnvCTemporaryWorkspace(tempRoot);
            ResetEvidenceState();
            _evidenceReleaseSucceeded = sessionReleased && released;
            if (!released) _evidenceTempRoot = tempRoot;
            _session = _sourceSession; Visible = false; return false;
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        var sourceSnapshot = CaptureSourceSnapshot();
        if (!PrepareEvidence("ux2-env-c-climate-water-reverb") || !CanonicalPreviewReady || !UsesIsolatedEvidence)
        {
            Close(); return false;
        }
        var isolated = _session;
        if (_lastPreview is not { Succeeded: true, CanApply: true }) { Close(); return false; }
        Apply();
        var applied = _applied && isolated.Project is not null;
        var appliedJson = applied ? ProjectJsonSerializer.Serialize(isolated.Project!) : string.Empty;
        Undo(); var undone = !_applied;
        Redo();
        var redone = _applied && isolated.Project is not null &&
                     string.Equals(appliedJson, ProjectJsonSerializer.Serialize(isolated.Project!), StringComparison.Ordinal);
        Save();
        var reopen = new StudioSessionController();
        var roundTrip = false;
        try
        {
            var opened = _evidenceTempRoot is not null && reopen.Open(_evidenceTempRoot).Succeeded;
            roundTrip = opened && reopen.Project is not null &&
                        string.Equals(appliedJson, ProjectJsonSerializer.Serialize(reopen.Project!), StringComparison.Ordinal);
        }
        finally { reopen.Close(); }
        Close();
        return applied && undone && redone && roundTrip && SourceSnapshotMatches(sourceSnapshot) &&
               EvidenceResourcesReleased && SourceSessionRestored;
    }

    private void BuildWindTab()
    {
        var tab = ScrollTab("Vent");
        var global = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        global.AddChild(StudioUx2EnvBControls.Field("Direction X", _windDirectionX));
        global.AddChild(StudioUx2EnvBControls.Field("Direction Z", _windDirectionZ));
        global.AddChild(StudioUx2EnvBControls.Field("Vitesse (m/s)", _windSpeed));
        global.AddChild(StudioUx2EnvBControls.Field("Force de rafale", _windGustStrength));
        global.AddChild(StudioUx2EnvBControls.Field("Intervalle rafale (s)", _windGustInterval));
        global.AddChild(StudioUx2EnvBControls.Field("Force gameplay déclarée", _windGameplayForce));
        global.AddChild(StudioUx2EnvBControls.Field("Cue audio", _windAudio, 220));
        var flags = new VBoxContainer(); flags.AddChild(_windVegetation); flags.AddChild(_windParticles);
        global.AddChild(flags);
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvCWindGlobal", "Vent global",
            "Direction, vitesse, rafales et liaisons restent des données d’authoring ; aucun shader live n’est piloté.", global));
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvCWindZones", "Zones de vent locales",
            "Toutes les zones sont chargées avec centre, rayon, direction, vitesse et multiplicateur.", _windZones));
    }

    private void BuildWeatherTab()
    {
        var tab = ScrollTab("Météo");
        var policy = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        policy.AddChild(StudioUx2EnvBControls.Field("État initial", _initialWeather, 220));
        policy.AddChild(StudioUx2EnvBControls.Field("Graine déterministe", _weatherSeed, 180));
        var flags = new VBoxContainer(); flags.AddChild(_weatherAuthority); flags.AddChild(_weatherPersist);
        flags.AddChild(_weatherReplicate); policy.AddChild(flags);
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvCWeatherPolicy", "Politique météo",
            "L’aperçu simule localement une transition déterministe sans scheduler, réseau ni réplication active.", policy));
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvCWeatherStates", "États météo",
            "Chaque état conserve phénomène, humidité, précipitation, audio, effet déclaré et temporalité.", _weatherStates));
    }

    private void BuildWaterTab()
    {
        var tab = ScrollTab("Eau");
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvCWaterBodies", "Surfaces et volumes d’eau",
            "Géométrie, flux, profondeur, qualité, nage et coût de navigation sont authorés. La flottabilité n’est pas exécutée.",
            _waterBodies));
    }

    private void BuildReverbTab()
    {
        var tab = ScrollTab("Réverbération");
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvCReverbDefault", "Preset par défaut",
            "Fallback algorithmique utilisé hors zone ; aucun bus audio live n’est modifié.",
            StudioUx2EnvBControls.Field("Preset humain", _reverbDefault, 280)));
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvCReverbZones", "Zones acoustiques",
            "Les réponses impulsionnelles exigent ressource relative, licence, empreinte et provenance. L’aperçu ne convolutionne aucun signal.",
            _reverbZones));
    }

    private void BuildReviewTab()
    {
        var tab = ScrollTab("Revue");
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvCDiff", "Diff sémantique",
            "Vent, météo, eau et réverbération sont résumés sans identifiant interne.", _diff));
        tab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvCDiagnostics", "Validation canonique",
            "Les erreurs sont groupées avant toute commande d’application.", _diagnostics));
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
            CustomMinimumSize = new Vector2(980, 0),
        };
        content.AddThemeConstantOverride("separation", 8); scroll.AddChild(content); _tabs.AddChild(scroll);
        _tabs.SetTabTitle(_tabs.GetTabCount() - 1, title); return content;
    }

    private void RefreshChoices()
    {
        _updating = true; _choices = _session.ClimateChoicesUx2EnvC;
        _area.Clear(); _area.AddItem("Choisir explicitement une zone…");
        foreach (var choice in _choices) _area.AddItem(choice.Label);
        _area.Select(0); _updating = false; ClearSelection();
    }

    private void SelectArea(long index)
    {
        if (_updating) return;
        var choiceIndex = (int)index - 1;
        if (choiceIndex < 0 || choiceIndex >= _choices.Count) ClearSelection(); else LoadChoice(choiceIndex);
    }

    private void ClearSelection()
    {
        _draft = null; _lastPreview = null; _applied = false; _canonicalPreviewReady = false;
        _session.CancelClimateUx2EnvC(); ResetControls();
        _selection.Text = "Choisissez explicitement une zone ; aucune fiche n’est sélectionnée automatiquement.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
        _summary.Text = "Sélectionnez une zone puis lancez l’aperçu sec · aucun changement appliqué.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        SetEditingEnabled(false); UpdateTransactionButtons(); SetStatus("Prêt · sélection explicite requise", false);
    }

    private void LoadChoice(int choiceIndex)
    {
        var choice = _choices[choiceIndex]; _session.CancelClimateUx2EnvC();
        _draft = _session.CreateClimateDraftUx2EnvC(choice.AreaId);
        _lastPreview = null; _applied = false; _canonicalPreviewReady = false;
        if (_draft is null)
        {
            ResetControls(); _selection.Text = "La zone choisie a changé. Rechargez la page Environnement.";
            _selection.AddThemeColorOverride("font_color", StudioStyle.Danger);
            SetEditingEnabled(false); UpdateTransactionButtons(); SetStatus("Zone indisponible · aucune mutation", true);
            return;
        }
        Populate(_draft);
        _selection.Text = choice.Label + (choice.HasWater ? " · eau authorée" : " · aucune eau authorée") +
                          (choice.HasSpatialReverb ? " · zones acoustiques authorées" : " · preset acoustique global");
        _selection.AddThemeColorOverride("font_color", StudioStyle.Accent);
        _summary.Text = "Profils complets chargés · fondations du lot B et tous les voisins restent préservés.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        SetEditingEnabled(true); UpdateTransactionButtons(); SetStatus("Brouillon prêt · aperçu sec requis", false);
    }

    private void Populate(StudioUx2EnvCDraft draft)
    {
        _updating = true;
        StudioUx2EnvCNumberText.Set(_windDirectionX, draft.WindDirectionX);
        StudioUx2EnvCNumberText.Set(_windDirectionZ, draft.WindDirectionZ);
        StudioUx2EnvCNumberText.Set(_windSpeed, draft.WindSpeed);
        StudioUx2EnvCNumberText.Set(_windGustStrength, draft.WindGustStrength);
        StudioUx2EnvCNumberText.Set(_windGustInterval, draft.WindGustIntervalSeconds);
        _windVegetation.ButtonPressed = draft.WindInfluencesVegetation;
        _windParticles.ButtonPressed = draft.WindInfluencesParticles; _windAudio.Text = draft.WindAudioCue;
        StudioUx2EnvCNumberText.Set(_windGameplayForce, draft.WindGameplayForce);
        _windZones.SetValues(draft.WindZones);
        _weatherStates.SetValues(draft.WeatherStates); RefreshInitialWeatherChoices(draft.InitialWeatherStateId);
        _weatherAuthority.ButtonPressed = draft.WeatherServerAuthoritative;
        StudioUx2EnvCNumberText.Set(_weatherSeed, draft.WeatherDeterministicSeed);
        _weatherPersist.ButtonPressed = draft.WeatherPersistState; _weatherReplicate.ButtonPressed = draft.WeatherReplicateState;
        _waterBodies.SetValues(draft.WaterBodies); _reverbDefault.Text = draft.ReverbDefaultPreset;
        _reverbZones.SetValues(draft.ReverbZones); _updating = false;
        RenderDiff(null); RenderDiagnostics([]);
    }

    private StudioUx2EnvCDraft? ReadDraft()
    {
        if (_draft is null) return null;
        var seedValue = _weatherSeed.Value;
        if (seedValue != Math.Truncate(seedValue) || seedValue < int.MinValue || seedValue > int.MaxValue)
            throw new InvalidOperationException("Weather seed must be an exact 32-bit integer.");
        var initialIndex = (int)_initialWeather.Selected - 1;
        var initialId = initialIndex >= 0 && initialIndex < _initialWeatherIds.Count
            ? _initialWeatherIds[initialIndex]
            : Guid.Empty;
        return _draft with
        {
            WindDirectionX = (float)_windDirectionX.Value,
            WindDirectionZ = (float)_windDirectionZ.Value,
            WindSpeed = (float)_windSpeed.Value,
            WindGustStrength = (float)_windGustStrength.Value,
            WindGustIntervalSeconds = (float)_windGustInterval.Value,
            WindInfluencesVegetation = _windVegetation.ButtonPressed,
            WindInfluencesParticles = _windParticles.ButtonPressed,
            WindAudioCue = _windAudio.Text,
            WindGameplayForce = (float)_windGameplayForce.Value,
            WindZones = _windZones.Values,
            InitialWeatherStateId = initialId,
            WeatherStates = _weatherStates.Values,
            WeatherServerAuthoritative = _weatherAuthority.ButtonPressed,
            WeatherDeterministicSeed = (int)seedValue,
            WeatherPersistState = _weatherPersist.ButtonPressed,
            WeatherReplicateState = _weatherReplicate.ButtonPressed,
            WaterBodies = _waterBodies.Values,
            ReverbDefaultPreset = _reverbDefault.Text,
            ReverbZones = _reverbZones.Values,
        };
    }

    private bool ConfigureEvidenceDraft()
    {
        var current = ReadDraft();
        if (current is null || current.WeatherStates.Count == 0) return false;
        var waters = current.WaterBodies.Select((body, index) => index == 0
            ? body with { FlowSpeed = body.FlowSpeed <= 99.875f ? body.FlowSpeed + .125f : body.FlowSpeed - .125f }
            : body).ToArray();
        var configured = current with
        {
            WindSpeed = current.WindSpeed + .125f,
            WeatherDeterministicSeed = current.WeatherDeterministicSeed ^ 1,
            WaterBodies = waters,
            ReverbDefaultPreset = current.ReverbDefaultPreset + "-preview",
        };
        Populate(configured); InvalidatePreview(); return true;
    }

    private void RefreshInitialWeatherChoices(Guid preferred)
    {
        var states = _weatherStates.Values;
        _initialWeatherIds = states.Select(state => state.Id).ToArray();
        _initialWeather.Clear(); _initialWeather.AddItem("Choisir un état initial…");
        foreach (var state in states)
            _initialWeather.AddItem(string.IsNullOrWhiteSpace(state.Name) ? "État sans nom" : state.Name.Trim());
        var index = _initialWeatherIds.ToList().FindIndex(id => id == preferred);
        _initialWeather.Select(index < 0 ? 0 : index + 1);
    }

    private void Preview()
    {
        StudioUx2EnvCDraft? draft;
        try { draft = ReadDraft(); }
        catch (Exception exception) when (exception is OverflowException or InvalidOperationException)
        {
            RenderLocalFailure("Une valeur numérique ne peut pas être convertie sans perte."); return;
        }
        if (draft is null) return;
        var result = _session.PreviewClimateUx2EnvC(draft); _lastPreview = result;
        _canonicalPreviewReady = result.Succeeded && result.CanonicalDryRun && result.RuntimeClaimsBounded;
        _apply.Disabled = !result.CanApply || !result.Diff.HasChanges || !_canonicalPreviewReady;
        RenderDiff(result); RenderDiagnostics(result.Groups);
        _summary.Text = string.IsNullOrWhiteSpace(result.Summary) ? result.Message : result.Message + "\n" + result.Summary;
        _summary.AddThemeColorOverride("font_color", result.Succeeded ? StudioStyle.Success : StudioStyle.Danger);
        SetStatus(result.Message, !result.Succeeded); UpdateTransactionButtons(keepApplyState: true);
    }

    private void RenderLocalFailure(string message)
    {
        var diagnostic = new StudioUx2EnvCDiagnostic("studio.environment.value_invalid", "environment", message);
        var groups = new[]
        {
            new StudioUx2EnvCDiagnosticGroup("wind", "Vent", "à corriger", [diagnostic]),
            new StudioUx2EnvCDiagnosticGroup("weather", "Météo", "à corriger", [diagnostic]),
            new StudioUx2EnvCDiagnosticGroup("water", "Eau", "à corriger", [diagnostic]),
            new StudioUx2EnvCDiagnosticGroup("reverb", "Réverbération", "à corriger", [diagnostic]),
        };
        _lastPreview = new StudioUx2EnvCPreview(false, message, string.Empty, false,
            new StudioUx2EnvCSemanticDiff([], [], [], []), groups, false, false, false, false, false, true);
        _canonicalPreviewReady = false; _apply.Disabled = true;
        RenderDiff(_lastPreview); RenderDiagnostics(groups); _summary.Text = message;
        _summary.AddThemeColorOverride("font_color", StudioStyle.Danger); SetStatus(message, true);
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedClimateUx2EnvC(); SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) { _apply.Disabled = true; _canonicalPreviewReady = false; return; }
        _applied = true; _apply.Disabled = true; _preview.Disabled = true; SetEditingEnabled(false);
        UpdateTransactionButtons();
        _summary.Text = "Appliqué · climat, eau et réverbération de la seule zone choisie ont changé ; fondations et voisins sont préservés.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Success); AreaEnvironmentChanged?.Invoke();
    }

    private void Undo()
    {
        var result = _session.Undo(); SetStatus(result.Message, !result.Succeeded); if (!result.Succeeded) return;
        _applied = false; UpdateTransactionButtons();
        _summary.Text = "Annulé · la révision précédente de l’environnement est restaurée.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Accent); AreaEnvironmentChanged?.Invoke();
    }

    private void Redo()
    {
        var result = _session.Redo(); SetStatus(result.Message, !result.Succeeded); if (!result.Succeeded) return;
        _applied = true; UpdateTransactionButtons();
        _summary.Text = "Rétabli · les profils approuvés sont de nouveau présents.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Success); AreaEnvironmentChanged?.Invoke();
    }

    private void Save()
    {
        var result = _session.Save(); SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) { _save.Disabled = true; _summary.Text += "\nProjet enregistré · la réouverture relira le même agrégat canonique."; }
    }

    private void ConnectInvalidation()
    {
        foreach (var control in new[] { _windDirectionX, _windDirectionZ, _windSpeed, _windGustStrength,
                     _windGustInterval, _windGameplayForce, _weatherSeed })
            control.ValueChanged += _ => InvalidatePreview();
        _windVegetation.Toggled += _ => InvalidatePreview(); _windParticles.Toggled += _ => InvalidatePreview();
        _windAudio.TextChanged += _ => InvalidatePreview(); _windZones.Changed += InvalidatePreview;
        _initialWeather.ItemSelected += _ => InvalidatePreview();
        _weatherStates.Changed += () =>
        {
            var preferredIndex = (int)_initialWeather.Selected - 1;
            var preferred = preferredIndex >= 0 && preferredIndex < _initialWeatherIds.Count
                ? _initialWeatherIds[preferredIndex]
                : Guid.Empty;
            var wasUpdating = _updating; _updating = true; RefreshInitialWeatherChoices(preferred); _updating = wasUpdating;
            InvalidatePreview();
        };
        _weatherAuthority.Toggled += _ => InvalidatePreview(); _weatherPersist.Toggled += _ => InvalidatePreview();
        _weatherReplicate.Toggled += _ => InvalidatePreview(); _waterBodies.Changed += InvalidatePreview;
        _reverbDefault.TextChanged += _ => InvalidatePreview(); _reverbZones.Changed += InvalidatePreview;
    }

    private void InvalidatePreview()
    {
        if (_updating || _draft is null) return;
        _session.CancelClimateUx2EnvC(); _lastPreview = null; _canonicalPreviewReady = false; _applied = false;
        _apply.Disabled = true; _save.Disabled = true;
        _summary.Text = "Brouillon modifié · relancez l’aperçu sec avant application.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Warning);
        RenderDiff(null); RenderDiagnostics([]); UpdateTransactionButtons(keepApplyState: true);
        SetStatus("Aperçu précédent effacé · validation à relancer", false);
    }

    private void ResetControls()
    {
        _updating = true;
        _windDirectionX.Value = 0; _windDirectionZ.Value = 0; _windSpeed.Value = 0;
        _windGustStrength.Value = 0; _windGustInterval.Value = 8;
        _windVegetation.ButtonPressed = false; _windParticles.ButtonPressed = false;
        _windAudio.Text = string.Empty; _windGameplayForce.Value = 0; _windZones.SetValues([]);
        _weatherStates.SetValues([]); RefreshInitialWeatherChoices(Guid.Empty);
        _weatherAuthority.ButtonPressed = false; _weatherSeed.Value = 0;
        _weatherPersist.ButtonPressed = false; _weatherReplicate.ButtonPressed = false;
        _waterBodies.SetValues([]); _reverbDefault.Text = "neutral"; _reverbZones.SetValues([]);
        _updating = false; RenderDiff(null); RenderDiagnostics([]);
    }

    private void SetEditingEnabled(bool enabled)
    {
        foreach (var control in new[] { _windDirectionX, _windDirectionZ, _windSpeed, _windGustStrength,
                     _windGustInterval, _windGameplayForce, _weatherSeed }) control.Editable = enabled;
        _windVegetation.Disabled = !enabled; _windParticles.Disabled = !enabled; _windAudio.Editable = enabled;
        _windZones.SetEditingEnabled(enabled); _initialWeather.Disabled = !enabled;
        _weatherStates.SetEditingEnabled(enabled); _weatherAuthority.Disabled = !enabled;
        _weatherPersist.Disabled = !enabled; _weatherReplicate.Disabled = !enabled;
        _waterBodies.SetEditingEnabled(enabled); _reverbDefault.Editable = enabled;
        _reverbZones.SetEditingEnabled(enabled); _preview.Disabled = !enabled;
    }

    private void UpdateTransactionButtons(bool keepApplyState = false)
    {
        var history = _session.History; if (!keepApplyState) _apply.Disabled = true;
        _undo.Disabled = !_applied || history?.CanUndo != true;
        _redo.Disabled = _applied || history?.CanRedo != true;
        _save.Disabled = !_applied || !_session.IsDirty;
    }

    private void RenderDiff(StudioUx2EnvCPreview? preview)
    {
        ClearChildren(_diff);
        if (preview is null) { _diff.AddChild(StudioStyle.Label("Aucun aperçu frais.", 10, StudioStyle.Muted)); return; }
        if (!preview.Diff.HasChanges)
        {
            _diff.AddChild(StudioStyle.Label("Aucun changement sémantique.", 10, StudioStyle.Muted)); return;
        }
        AddDiffSection("Vent", preview.Diff.Wind); AddDiffSection("Météo", preview.Diff.Weather);
        AddDiffSection("Eau", preview.Diff.Water); AddDiffSection("Réverbération", preview.Diff.Reverb);
    }

    private void AddDiffSection(string title, IReadOnlyList<string> entries)
    {
        if (entries.Count == 0) return;
        _diff.AddChild(StudioStyle.Label(title, 10, StudioStyle.Accent));
        foreach (var entry in entries) _diff.AddChild(StudioStyle.Label("• " + entry, 10, StudioStyle.Text, wrap: true));
    }

    private void RenderDiagnostics(IReadOnlyList<StudioUx2EnvCDiagnosticGroup> groups)
    {
        ClearChildren(_diagnostics);
        if (groups.Count == 0)
        {
            _diagnostics.AddChild(StudioStyle.Label("Les diagnostics apparaîtront après l’aperçu sec.",
                10, StudioStyle.Muted, wrap: true)); return;
        }
        foreach (var group in groups)
        {
            var color = group.Diagnostics.Count == 0 ? StudioStyle.Success : StudioStyle.Danger;
            _diagnostics.AddChild(StudioStyle.Label($"{group.Title} · {group.State}", 10, color));
            foreach (var diagnostic in group.Diagnostics)
                _diagnostics.AddChild(StudioStyle.Label("• " + diagnostic.Message, 10,
                    diagnostic.Blocking ? StudioStyle.Danger : StudioStyle.Warning, wrap: true));
        }
    }

    private static void ClearChildren(Node root)
    {
        foreach (var child in root.GetChildren()) child.QueueFree();
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message; _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(message, error);
    }

    private StudioUx2EnvCEvidenceSourceSnapshot CaptureSourceSnapshot()
    {
        var history = _sourceSession.History ?? throw new InvalidOperationException("No source history.");
        var project = _sourceSession.Project ?? throw new InvalidOperationException("No source project.");
        return new StudioUx2EnvCEvidenceSourceSnapshot(
            project, history, history.State.Revision, history.Journal.ToArray(), _sourceSession.IsDirty,
            history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(),
            _sourceSession.SelectedEntityId, _sourceSession.CaptureClimateApprovalUx2EnvC());
    }

    private bool SourceSnapshotMatches(StudioUx2EnvCEvidenceSourceSnapshot? snapshot = null)
    {
        snapshot ??= _evidenceSourceSnapshot; if (snapshot is null) return false;
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) && ReferenceEquals(snapshot.History, history) &&
               history is not null && history.State.Revision == snapshot.Revision &&
               history.Journal.SequenceEqual(snapshot.Journal) && history.CanUndo == snapshot.CanUndo &&
               history.CanRedo == snapshot.CanRedo && _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureClimateApprovalUx2EnvC() == snapshot.Approval;
    }

    private bool ReleaseIsolatedEvidenceSession()
    {
        var evidenceSession = _evidenceSession; var tempRoot = _evidenceTempRoot;
        _evidenceSession = null; _evidenceTempRoot = null; _evidenceSourceSnapshot = null; _session = _sourceSession;
        _evidenceUsesRealModuleData = false; _evidenceUsesCanonicalFallback = false;
        var released = true;
        try { evidenceSession?.Close(); }
        catch (Exception exception)
        {
            released = false;
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_C_RELEASE_FAILED type={exception.GetType().Name} message={exception.Message}");
        }
        var rootReleased = StudioSessionController.DeleteUx2EnvCTemporaryWorkspace(tempRoot);
        if (!rootReleased) _evidenceTempRoot = tempRoot;
        _evidenceReleaseSucceeded = released && rootReleased;
        return _evidenceReleaseSucceeded;
    }

    private void ResetEvidenceState()
    {
        _evidenceSession = null; _evidenceTempRoot = null; _evidenceSourceSnapshot = null;
        _evidenceUsesRealModuleData = false; _evidenceUsesCanonicalFallback = false;
    }

    private void UpdateDialogBounds()
    {
        var viewport = GetViewportRect().Size; if (viewport.X <= 0 || viewport.Y <= 0) return;
        var scale = viewport.X >= 3_200 ? 1.7f : viewport.X >= 2_300 ? 1.23f : 1f;
        var width = Math.Clamp((viewport.X - 52f) / scale, 1_080f, 1_620f);
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

    private static SpinBox Number(double min, double max, double step, bool lesser = false, bool greater = false) =>
        StudioUx2EnvBControls.Number(min, max, step, greater, lesser);

    private static IEnumerable<string> DescendantVisibleText(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            switch (child)
            {
                case Label label:
                    yield return label.Text;
                    break;
                case LineEdit line:
                    yield return line.Text;
                    break;
                case TextEdit text:
                    yield return text.Text;
                    break;
                case OptionButton option:
                    for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
                    break;
                case Button button:
                    yield return button.Text;
                    break;
            }
            foreach (var descendant in DescendantVisibleText(child)) yield return descendant;
        }
    }

    private static bool ContainsUuid(string text) => System.Text.RegularExpressions.Regex.IsMatch(
        text, @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase |
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);
}
