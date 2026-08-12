using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Validation;

namespace Forge.Domain.Audio;

public enum AudioModulationDistribution
{
    Uniform,
    Triangular,
    Gaussian,
}

public enum AudioListenerPolicy
{
    EveryListener,
    LocalPlayer,
    Party,
    Personalized,
}

public enum AudioMeterMode
{
    Peak,
    Rms,
    Loudness,
}

public sealed record AudioModulationCurvePoint(float Position, float Value);

public sealed record AudioPlaybackSelectionProfile(
    int DeterministicSeed,
    int NoRepeatWindow,
    IReadOnlyDictionary<string, float> Weights);

public sealed record AudioModulationProfile(
    float MinimumIntervalSeconds,
    float MaximumIntervalSeconds,
    float MinimumPitchSemitones,
    float MaximumPitchSemitones,
    float MinimumVolume,
    float MaximumVolume,
    AudioModulationDistribution Distribution,
    IReadOnlyList<AudioModulationCurvePoint> Curve,
    int DeterministicSeed,
    bool SyncToTempo,
    float TempoBpm,
    float BeatDivision);

public sealed record AudioLoudnessMixProfile(
    float TargetLoudnessLufs,
    string Bus,
    float DuckingDb,
    float DistanceScale,
    float AccessibilityVolumeCap,
    AudioMeterMode MeterMode);

public sealed record AreaAmbientScopeProfile(
    string AreaScope,
    string Bus,
    AudioListenerPolicy ListenerPolicy,
    IReadOnlyDictionary<string, float> ListenerGains);

public sealed record AudioAuthoringProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string PackageManifest,
    string LicenseSpdx,
    string ContentSha256,
    bool RequiredAtRuntime,
    AudioPlaybackSelectionProfile Selection,
    AudioModulationProfile Modulation,
    AudioLoudnessMixProfile Mix,
    AreaAmbientScopeProfile Ambient)
{
    public const int CurrentSchemaVersion = 1;

    public static AudioAuthoringProfile CreateDefault(
        IReadOnlyList<string> playlist,
        float intervalSeconds,
        float intervalVariationSeconds)
    {
        var canonical = $"{string.Join('|', playlist)}:{intervalSeconds:R}:{intervalVariationSeconds:R}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        return new(
            new Guid(Convert.FromHexString(hash[..32])),
            CurrentSchemaVersion,
            "migration-default",
            "builtin://openrpg/audio-profile",
            "CC0-1.0",
            hash,
            false,
            new(2026, 1, new Dictionary<string, float>(StringComparer.Ordinal)),
            new(
                Math.Max(0, intervalSeconds - intervalVariationSeconds),
                Math.Max(0, intervalSeconds + intervalVariationSeconds),
                0,
                0,
                1,
                1,
                AudioModulationDistribution.Uniform,
                [new(0, 0), new(1, 1)],
                2026,
                false,
                120,
                1),
            new(-18, "Ambience", 0, 1, 1, AudioMeterMode.Loudness),
            new(string.Empty, "Ambience", AudioListenerPolicy.EveryListener,
                new Dictionary<string, float>(StringComparer.Ordinal)));
    }
}

public static class AudioAuthoringProfileValidator
{
    public const string SchemaUnsupportedCode = "audio.authoring.schema_unsupported";
    public const string IdentityInvalidCode = "audio.authoring.identity_invalid";
    public const string SelectionInvalidCode = "audio.selection.invalid";
    public const string SelectionReferenceMissingCode = "audio.selection.resource_missing";
    public const string ModulationInvalidCode = "audio.modulation.invalid";
    public const string MixInvalidCode = "audio.mix.invalid";
    public const string AmbientInvalidCode = "audio.ambient.invalid";

    public static IReadOnlyList<DomainDiagnostic> Validate(
        AudioAuthoringProfile profile,
        IReadOnlyList<string> playlist,
        AudioEmitterCatalog emitters,
        string path = "audio/authoring")
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(playlist);
        ArgumentNullException.ThrowIfNull(emitters);
        var diagnostics = new List<DomainDiagnostic>();
        if (profile.SchemaVersion != AudioAuthoringProfile.CurrentSchemaVersion)
            Add(SchemaUnsupportedCode, "Audio authoring schema must be 1.", $"{path}/schemaVersion");
        if (profile.Id == Guid.Empty || string.IsNullOrWhiteSpace(profile.Provenance) ||
            string.IsNullOrWhiteSpace(profile.PackageManifest) || !ValidSpdx(profile.LicenseSpdx) ||
            !ValidSha256(profile.ContentSha256))
            Add(IdentityInvalidCode,
                "Audio authoring requires UUID, provenance, package manifest, SPDX license and SHA-256.", path);

        var selection = profile.Selection;
        if (selection.NoRepeatWindow < 0 || selection.NoRepeatWindow > 1024 ||
            selection.Weights.Any(item => string.IsNullOrWhiteSpace(item.Key) ||
                !float.IsFinite(item.Value) || item.Value <= 0))
            Add(SelectionInvalidCode, "Selection weights must be positive and the no-repeat window must be between 0 and 1024.",
                $"{path}/selection");
        foreach (var key in selection.Weights.Keys.Where(key => !playlist.Contains(key, StringComparer.Ordinal)))
            Add(SelectionReferenceMissingCode, $"Weighted resource '{key}' is not in the playlist.",
                $"{path}/selection/weights/{key}");

        var modulation = profile.Modulation;
        if (!FiniteRange(modulation.MinimumIntervalSeconds, modulation.MaximumIntervalSeconds, 0, 86_400) ||
            !FiniteRange(modulation.MinimumPitchSemitones, modulation.MaximumPitchSemitones, -48, 48) ||
            !FiniteRange(modulation.MinimumVolume, modulation.MaximumVolume, 0, 4) ||
            modulation.Curve.Count < 2 || modulation.Curve.Any(point =>
                !float.IsFinite(point.Position) || point.Position is < 0 or > 1 ||
                !float.IsFinite(point.Value) || point.Value is < 0 or > 1) ||
            modulation.Curve.Zip(modulation.Curve.Skip(1), (first, second) => second.Position <= first.Position).Any(value => value) ||
            !float.IsFinite(modulation.TempoBpm) || modulation.TempoBpm is < 20 or > 400 ||
            !float.IsFinite(modulation.BeatDivision) || modulation.BeatDivision is <= 0 or > 16)
            Add(ModulationInvalidCode,
                "Modulation requires ordered normalized curve points, finite ranges, tempo 20-400 BPM and beat division 0-16.",
                $"{path}/modulation");

        var mix = profile.Mix;
        if (!float.IsFinite(mix.TargetLoudnessLufs) || mix.TargetLoudnessLufs is < -60 or > 0 ||
            string.IsNullOrWhiteSpace(mix.Bus) || !float.IsFinite(mix.DuckingDb) || mix.DuckingDb is < -80 or > 0 ||
            !float.IsFinite(mix.DistanceScale) || mix.DistanceScale <= 0 ||
            !float.IsFinite(mix.AccessibilityVolumeCap) || mix.AccessibilityVolumeCap is < 0 or > 4)
            Add(MixInvalidCode,
                "Audio mix requires target loudness, bus, ducking, distance scale and accessibility cap in supported ranges.",
                $"{path}/mix");

        var ambient = profile.Ambient;
        if (string.IsNullOrWhiteSpace(ambient.Bus) || ambient.ListenerGains.Any(item =>
                string.IsNullOrWhiteSpace(item.Key) || !float.IsFinite(item.Value) || item.Value is < 0 or > 4))
            Add(AmbientInvalidCode, "Ambient listener personalization requires a bus and finite gains between 0 and 4.",
                $"{path}/ambient");

        var governedResources = emitters.Definitions.SelectMany(item => item.ClipSet.Clips)
            .Select(item => item.ResourcePath).ToHashSet(StringComparer.Ordinal);
        if (!string.Equals(profile.Provenance, "migration-default", StringComparison.Ordinal))
        {
            for (var index = 0; index < playlist.Count; index++)
            {
                var resource = playlist[index];
                if (!resource.StartsWith("builtin://", StringComparison.Ordinal) && !governedResources.Contains(resource))
                    Add(SelectionReferenceMissingCode,
                        $"Audio resource '{resource}' has no governed clip metadata with license and provenance.",
                        $"{path}/playlist/{index}");
            }
        }
        return diagnostics;

        void Add(string code, string message, string diagnosticPath) =>
            diagnostics.Add(new DomainDiagnostic(code, message, diagnosticPath));
    }

    private static bool FiniteRange(float minimum, float maximum, float lower, float upper) =>
        float.IsFinite(minimum) && float.IsFinite(maximum) && minimum >= lower && maximum <= upper && minimum <= maximum;

    private static bool ValidSpdx(string value) => !string.IsNullOrWhiteSpace(value) &&
        !string.Equals(value.Trim(), "UNLICENSED", StringComparison.OrdinalIgnoreCase) &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or '+' or '(' or ')' or ' ');

    private static bool ValidSha256(string value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
}

public sealed record AudioAuthoringRuntimeResolution(
    AudioAuthoringProfile? Profile,
    IReadOnlyList<DomainDiagnostic> Diagnostics)
{
    public bool Available => Profile is not null;
}

public static class AudioAuthoringRuntimeResolver
{
    public const string RequiredMissingCode = "runtime.audio_authoring.required_missing";

    public static AudioAuthoringRuntimeResolution Resolve(AudioAuthoringProfile? profile, bool required)
    {
        if (profile is not null) return new(profile, []);
        return required
            ? new(null, [new(RequiredMissingCode, "Required audio authoring metadata is missing.", "environment/audio/authoring")])
            : new(null, []);
    }
}
