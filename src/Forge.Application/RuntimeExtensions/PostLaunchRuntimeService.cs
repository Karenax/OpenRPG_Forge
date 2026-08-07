using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;

namespace Forge.Application.RuntimeExtensions;

public sealed record PostLaunchCapabilitySummary(Guid Id, int SchemaVersion, bool ZeroGravity,
    bool Vehicles, bool Mounts, int Achievements, int SavedDirectorGroups, bool SessionRecording,
    bool Voice, int Shards, bool HotReload, bool ServerExtensions, bool DynamicAreas,
    bool MediaGeneration, bool SoakTests, string Provenance);

public sealed record MediaGenerationPlan(bool Succeeded, Guid JobId, string Code, string SemanticDiff,
    string StagingPath, bool RequiresReview, string LicenseSpdx, string RightsProvenance,
    bool UsesModuleIndependentFallback);

public static class PostLaunchRuntimeService
{
    public static PostLaunchCapabilityProfile GetProfile(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.Module.RuntimePolicies.Extensions.PostLaunch;
    }

    public static PostLaunchCapabilitySummary Summarize(ProjectDefinition project)
    {
        var profile = GetProfile(project);
        return new(profile.Id, profile.SchemaVersion, profile.Gameplay.ZeroGravity.Enabled,
            profile.Gameplay.Vehicles.Enabled, profile.Gameplay.Mounts.Enabled,
            profile.Gameplay.Achievements.Count, profile.LiveDirector.SavedGroups.Count,
            profile.LiveDirector.SessionRecording.Enabled, profile.Network.Voice.Enabled,
            profile.Network.Shards.Count, profile.Network.HotReload.Enabled,
            profile.Network.ServerExtensions.Enabled, profile.CreatorExperience.DynamicAreas.Enabled,
            profile.Production.MediaGeneration.Enabled, profile.Production.SoakTests.Enabled,
            profile.Provenance);
    }

    public static MediaGenerationPlan PlanMediaGeneration(ProjectDefinition project, Guid jobId, string kind,
        string provider, string licenseSpdx, string rightsProvenance, long expectedBytes, bool providerAvailable)
    {
        ArgumentNullException.ThrowIfNull(project);
        return PlanMediaGeneration(GetProfile(project), jobId, kind, provider, licenseSpdx, rightsProvenance,
            expectedBytes, providerAvailable);
    }

    public static MediaGenerationPlan PlanMediaGeneration(PostLaunchCapabilityProfile profile, Guid jobId,
        string kind, string provider, string licenseSpdx, string rightsProvenance, long expectedBytes,
        bool providerAvailable)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (jobId == Guid.Empty) throw new ArgumentOutOfRangeException(nameof(jobId));
        var production = profile.Production;
        var media = production.MediaGeneration;
        var normalizedKind = Normalize(kind, nameof(kind));
        var normalizedProvider = Normalize(provider, nameof(provider));
        var license = licenseSpdx?.Trim().ToUpperInvariant() ?? string.Empty;
        var provenance = rightsProvenance?.Trim() ?? string.Empty;
        if (!media.Enabled || !media.MediaKinds.Contains(normalizedKind, StringComparer.Ordinal))
            return Failure(jobId, "POST_MEDIA_KIND_DISABLED", normalizedKind, license, provenance, media);
        if (!media.Providers.Contains(normalizedProvider, StringComparer.Ordinal) && providerAvailable)
            return Failure(jobId, "POST_MEDIA_PROVIDER_NOT_ALLOWED", normalizedKind, license, provenance, media);
        if (media.RequireRightsMetadata && (string.IsNullOrWhiteSpace(license) || string.IsNullOrWhiteSpace(provenance)))
            return Failure(jobId, "POST_MEDIA_RIGHTS_REQUIRED", normalizedKind, license, provenance, media);
        if (expectedBytes is < 1 or > 4_294_967_296 || expectedBytes > media.MaximumBytesPerJob)
            return Failure(jobId, "POST_MEDIA_SIZE_EXCEEDED", normalizedKind, license, provenance, media);
        var fallback = !providerAvailable;
        if (fallback && !media.ModuleIndependentFallback)
            return Failure(jobId, "POST_MEDIA_PROVIDER_UNAVAILABLE", normalizedKind, license, provenance, media);
        var path = $".forge/media-staging/{jobId:N}/{normalizedKind}.candidate";
        var diff = $"stage {normalizedKind};provider={(fallback ? "fallback" : normalizedProvider)};bytes={expectedBytes};license={license};review={media.RequireHumanReview}";
        return new(true, jobId, fallback ? "POST_MEDIA_FALLBACK_PLANNED" : "POST_MEDIA_PLANNED", diff,
            path, media.RequireHumanReview, license, provenance, fallback);
    }

    private static MediaGenerationPlan Failure(Guid jobId, string code, string kind, string license,
        string provenance, ProductionAutomationProfile.MediaGenerationSettings media) =>
        new(false, jobId, code, $"no changes;kind={kind}", string.Empty, media.RequireHumanReview,
            license, provenance, false);

    private static string Normalize(string value, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameter);
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')))
            throw new ArgumentException("Keys may contain only ASCII letters, digits, dots, dashes and underscores.", parameter);
        return normalized;
    }
}
