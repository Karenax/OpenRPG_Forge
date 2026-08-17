using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;

namespace Forge.Domain.RuntimePolicies;

public enum PatrolOwnershipAuthority { Local, Host, Server }
public enum LoadingMediaKind { AccessibleFallback, Image, Video }

public sealed record SpatialScatterEmitter(
    Guid Id,
    string CueId,
    Vector3Value Center,
    Vector3Value Size,
    bool SurfaceOnly,
    int DeterministicSeed,
    float MinimumIntervalSeconds,
    float MaximumIntervalSeconds,
    float Volume,
    bool RequireNavigable,
    IReadOnlyList<string> BiomeTags,
    IReadOnlyList<string> ExclusionTags,
    string Provenance);

public sealed record AudioCueDefinition(
    Guid Id,
    string CueId,
    string Resource,
    string Bus,
    float Volume,
    bool Loop,
    string LicenseSpdx,
    string ContentSha256,
    string ForgeScriptApiVersion,
    string Provenance);

public sealed record LocalizedVoiceTake(
    string Locale,
    string Resource,
    string Subtitle,
    string PhonemeResource,
    string LicenseSpdx,
    string ContentSha256);

public sealed record VoiceConsentMetadata(
    bool SyntheticVoice,
    bool TtsUseAllowed,
    string ConsentReference,
    string SpeakerAttribution);

public sealed record CreatureVoiceProfile(
    Guid Id,
    BlueprintId CreatureBlueprintId,
    string DefaultLocale,
    IReadOnlyList<LocalizedVoiceTake> Takes,
    string TtsPluginId,
    VoiceConsentMetadata Consent,
    string Provenance);

public sealed record CrossAreaPatrolSegment(
    Guid Id,
    AreaId FromAreaId,
    AreaId ToAreaId,
    PortalId PortalId,
    Guid ResumeWaypointId,
    bool PrefetchDestination,
    PatrolOwnershipAuthority Authority);

public sealed record CrossAreaPatrolRoute(
    Guid Id,
    string OwnerId,
    IReadOnlyList<CrossAreaPatrolSegment> Segments,
    bool Loop,
    string Provenance);

public sealed record AreaLoadingPresentation(
    Guid Id,
    string Title,
    string ImageResource,
    string VideoResource,
    IReadOnlyDictionary<string, IReadOnlyList<string>> LocalizedTips,
    bool ShowProgress,
    string AccessibleFallback,
    bool PrefetchRequiredResources,
    int MinimumVisibleMilliseconds,
    string Provenance);

public sealed record AreaExperienceProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public IReadOnlyList<AudioCueDefinition> AudioCues { get; }
    public IReadOnlyList<SpatialScatterEmitter> ScatterEmitters { get; }
    public IReadOnlyList<CreatureVoiceProfile> Voices { get; }
    public IReadOnlyList<CrossAreaPatrolRoute> PatrolRoutes { get; }
    public AreaLoadingPresentation Loading { get; }
    public string Provenance { get; }

    public AreaExperienceProfile(Guid id, int schemaVersion, IReadOnlyList<AudioCueDefinition> audioCues,
        IReadOnlyList<SpatialScatterEmitter> scatterEmitters, IReadOnlyList<CreatureVoiceProfile> voices,
        IReadOnlyList<CrossAreaPatrolRoute> patrolRoutes, AreaLoadingPresentation loading, string provenance)
    {
        ArgumentNullException.ThrowIfNull(audioCues);
        ArgumentNullException.ThrowIfNull(scatterEmitters);
        ArgumentNullException.ThrowIfNull(voices);
        ArgumentNullException.ThrowIfNull(patrolRoutes);
        ArgumentNullException.ThrowIfNull(loading);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var cues = audioCues.OrderBy(item => item.CueId, StringComparer.Ordinal).ToArray();
        var scatter = scatterEmitters.OrderBy(item => item.Id).ToArray();
        var voiceValues = voices.OrderBy(item => item.Id).ToArray();
        var patrols = patrolRoutes.OrderBy(item => item.Id).ToArray();
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || loading.Id == Guid.Empty ||
            HasDuplicateIds(cues.Select(item => item.Id).Concat(scatter.Select(item => item.Id))
                .Concat(voiceValues.Select(item => item.Id)).Concat(patrols.Select(item => item.Id))) ||
            cues.Select(item => item.CueId).Distinct(StringComparer.Ordinal).Count() != cues.Length ||
            voiceValues.Select(item => item.CreatureBlueprintId).Distinct().Count() != voiceValues.Length ||
            patrols.Select(item => item.OwnerId).Distinct(StringComparer.Ordinal).Count() != patrols.Length ||
            cues.Any(InvalidCue) || scatter.Any(item => InvalidScatter(item, cues)) ||
            voiceValues.Any(InvalidVoice) || patrols.Any(InvalidPatrol) || InvalidLoading(loading))
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Area experience profile is invalid.");
        }

        Id = id;
        SchemaVersion = schemaVersion;
        AudioCues = cues;
        ScatterEmitters = scatter;
        Voices = voiceValues;
        PatrolRoutes = patrols;
        Loading = loading;
        Provenance = provenance.Trim();
    }

    public static AreaExperienceProfile CreateDefault(AreaId areaId)
    {
        ArgumentNullException.ThrowIfNull(areaId);
        return new AreaExperienceProfile(areaId.Value, CurrentSchemaVersion, [], [], [], [],
            new AreaLoadingPresentation(RuntimePolicyIdFactory.Create(areaId.Value, "experience:loading"),
                "Chargement", string.Empty, string.Empty,
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["fr"] = ["Préparation de la zone…"],
                    ["en"] = ["Preparing the area…"],
                }, true, "Chargement de la zone en cours.", true, 250, "migration-default"),
            "migration-default");
    }

    private static bool HasDuplicateIds(IEnumerable<Guid> ids)
    {
        var values = ids.ToArray();
        return values.Any(id => id == Guid.Empty) || values.Distinct().Count() != values.Length;
    }

    private static bool InvalidCue(AudioCueDefinition cue) =>
        string.IsNullOrWhiteSpace(cue.CueId) || string.IsNullOrWhiteSpace(cue.Bus) ||
        !float.IsFinite(cue.Volume) || cue.Volume is < 0 or > 4 ||
        string.IsNullOrWhiteSpace(cue.ForgeScriptApiVersion) || string.IsNullOrWhiteSpace(cue.Provenance) ||
        (!string.IsNullOrWhiteSpace(cue.Resource) &&
         (string.IsNullOrWhiteSpace(cue.LicenseSpdx) || !IsSha256(cue.ContentSha256)));

    private static bool InvalidScatter(SpatialScatterEmitter item, IReadOnlyList<AudioCueDefinition> cues) =>
        string.IsNullOrWhiteSpace(item.CueId) || !cues.Any(cue => cue.CueId == item.CueId) ||
        item.Size.X <= 0 || item.Size.Y <= 0 || item.Size.Z <= 0 || item.MinimumIntervalSeconds < 0 ||
        !float.IsFinite(item.MinimumIntervalSeconds) || !float.IsFinite(item.MaximumIntervalSeconds) ||
        item.MaximumIntervalSeconds < item.MinimumIntervalSeconds || !float.IsFinite(item.Volume) ||
        item.Volume is < 0 or > 4 ||
        string.IsNullOrWhiteSpace(item.Provenance);

    private static bool InvalidVoice(CreatureVoiceProfile voice) =>
        voice.CreatureBlueprintId is null || string.IsNullOrWhiteSpace(voice.DefaultLocale) ||
        voice.Takes.Count == 0 || voice.Takes.Select(item => item.Locale).Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() != voice.Takes.Count || voice.Takes.Any(take =>
                string.IsNullOrWhiteSpace(take.Locale) || string.IsNullOrWhiteSpace(take.Subtitle) ||
                (!string.IsNullOrWhiteSpace(take.Resource) &&
                 (string.IsNullOrWhiteSpace(take.LicenseSpdx) || !IsSha256(take.ContentSha256)))) ||
        (voice.Takes.Any(take => string.IsNullOrWhiteSpace(take.Resource)) &&
         (string.IsNullOrWhiteSpace(voice.TtsPluginId) || !voice.Consent.TtsUseAllowed ||
          string.IsNullOrWhiteSpace(voice.Consent.ConsentReference))) ||
        string.IsNullOrWhiteSpace(voice.Provenance);

    private static bool InvalidPatrol(CrossAreaPatrolRoute route) =>
        string.IsNullOrWhiteSpace(route.OwnerId) || string.IsNullOrWhiteSpace(route.Provenance) ||
        route.Segments.Count == 0 || route.Segments.Select(item => item.Id).Distinct().Count() != route.Segments.Count ||
        route.Segments.Any(item => item.Id == Guid.Empty || item.FromAreaId is null || item.ToAreaId is null ||
            item.PortalId is null || item.ResumeWaypointId == Guid.Empty || item.FromAreaId == item.ToAreaId) ||
        route.Segments.Zip(route.Segments.Skip(1), (left, right) => left.ToAreaId != right.FromAreaId).Any(value => value) ||
        route.Loop && route.Segments[^1].ToAreaId != route.Segments[0].FromAreaId;

    private static bool InvalidLoading(AreaLoadingPresentation loading) =>
        string.IsNullOrWhiteSpace(loading.Title) || string.IsNullOrWhiteSpace(loading.AccessibleFallback) ||
        loading.MinimumVisibleMilliseconds is < 0 or > 30000 || string.IsNullOrWhiteSpace(loading.Provenance) ||
        loading.LocalizedTips.Count == 0 || loading.LocalizedTips.Any(pair =>
            string.IsNullOrWhiteSpace(pair.Key) || pair.Value.Count == 0 || pair.Value.Any(string.IsNullOrWhiteSpace));

    private static bool IsSha256(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);
}
