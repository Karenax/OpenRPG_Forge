namespace Forge.Domain.RuntimeExtensions;

public static class PostLaunchCapabilityResolver
{
    public sealed record MobilityDecision(bool ZeroGravityActive, float AppliedAcceleration, float MaximumSpeed,
        bool VehicleAccepted, bool MountAccepted, string Diagnostic);

    public sealed record AchievementDecision(string Key, int Progress, int Threshold, bool Unlocked,
        bool PlatformDispatch, bool TelemetryAllowed);

    public sealed record DirectorDecision(bool Accepted, string Kind, int TargetCount, bool SnapshotRequired,
        string SemanticDiff, string Diagnostic);

    public sealed record RecordingDecision(bool Accepted, IReadOnlyList<Guid> Members, bool RedactPrivateData,
        string ExportFormat, string Diagnostic);

    public sealed record NetworkContentDecision(bool VoiceEnabled, int BitrateKbps, bool TrustedContentRequired,
        bool OfflineFallback, string Diagnostic);

    public sealed record ShardReloadDecision(bool Accepted, string ShardKey, bool AtomicSwap, bool Rollback,
        int DrainTimeoutSeconds, string Diagnostic);

    public sealed record ProtocolDecision(bool Compatible, int NegotiatedVersion, bool ExtensionAllowed,
        IReadOnlyList<string> GrantedCapabilities, string Diagnostic);

    public sealed record CreatorTechnicalDecision(bool Accepted, string WaterQuality, IReadOnlyList<string> DeviceQueries,
        int ResourceLimit, int DynamicAreaLimit, string GraphicsMode, string Diagnostic);

    public sealed record RpgDistributionDecision(bool PartyControl, int MaximumParty, int ProgressionTracks,
        string RepositoryChannel, bool SignedMetadata, bool Observability, string Diagnostic);

    public sealed record ProductionDecision(bool Accepted, string Kind, bool RequiresReview, int WorkUnits,
        string Diagnostic);

    public static MobilityDecision ResolveMobility(GameplayProductionProfile profile, bool insideZeroGravity,
        float inputMagnitude, int requestedSeats, bool requestMount)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!float.IsFinite(inputMagnitude) || inputMagnitude is < 0 or > 1)
            return new(false, 0, 0, false, false, "POST_MOBILITY_INPUT_INVALID");
        var gravity = profile.ZeroGravity.Enabled && insideZeroGravity;
        var vehicleAccepted = profile.Vehicles.Enabled && requestedSeats is >= 1 &&
                              requestedSeats <= profile.Vehicles.MaximumSeats;
        var mountAccepted = profile.Mounts.Enabled && requestMount;
        return new(gravity, gravity ? profile.ZeroGravity.ThrusterAcceleration * inputMagnitude : 0,
            gravity ? profile.ZeroGravity.MaximumSpeed : 0, vehicleAccepted, mountAccepted,
            vehicleAccepted || mountAccepted || gravity ? "POST_MOBILITY_READY" : "POST_MOBILITY_OPTIONAL_DISABLED");
    }

    public static AchievementDecision EvaluateAchievement(GameplayProductionProfile profile, string key, int progress)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (progress < 0) throw new ArgumentOutOfRangeException(nameof(progress));
        var normalized = DataDrivenRuleSchema.NormalizeKey(key, nameof(key));
        var definition = profile.Achievements.SingleOrDefault(item => item.Key == normalized)
            ?? throw new KeyNotFoundException($"Achievement '{normalized}' is not defined.");
        return new(definition.Key, progress, definition.Threshold, progress >= definition.Threshold,
            progress >= definition.Threshold && definition.PlatformKey is not null,
            definition.PrivacySafeTelemetry);
    }

    public static DirectorDecision PlanDirectorIntervention(LiveDirectorProductionProfile profile, string kind,
        int targetCount, int levelDelta = 0, float scale = 1)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (targetCount < 1) return new(false, kind, targetCount, false, "no changes", "POST_DIRECTOR_TARGETS_EMPTY");
        return kind switch
        {
            "force-rest" when !profile.ForcedRest.Enabled || targetCount > profile.ForcedRest.MaximumTargets =>
                new(false, kind, targetCount, false, "no changes", "POST_DIRECTOR_REST_DENIED"),
            "force-rest" => new(true, kind, targetCount, false,
                $"force-rest targets={targetCount};override={profile.ForcedRest.AllowRulesOverride}", "POST_DIRECTOR_PREVIEW"),
            "adjust-level" when !profile.LevelAdjustment.Enabled || levelDelta == 0 ||
                                     Math.Abs(levelDelta) > profile.LevelAdjustment.MaximumDelta =>
                new(false, kind, targetCount, true, "no changes", "POST_DIRECTOR_LEVEL_DENIED"),
            "adjust-level" => new(true, kind, targetCount, profile.LevelAdjustment.RequireRollbackSnapshot,
                $"adjust-level targets={targetCount};delta={levelDelta};rebuild={profile.LevelAdjustment.RequireRebuild}",
                "POST_DIRECTOR_PREVIEW"),
            "appearance" when !profile.AppearanceOverrides.Enabled || scale < profile.AppearanceOverrides.MinimumScale ||
                                  scale > profile.AppearanceOverrides.MaximumScale =>
                new(false, kind, targetCount, false, "no changes", "POST_DIRECTOR_APPEARANCE_DENIED"),
            "appearance" => new(true, kind, targetCount, false,
                $"appearance targets={targetCount};scale={scale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)};asset-validation={profile.AppearanceOverrides.ValidateAssets}",
                "POST_DIRECTOR_PREVIEW"),
            _ => new(false, kind, targetCount, false, "no changes", "POST_DIRECTOR_KIND_UNKNOWN"),
        };
    }

    public static RecordingDecision ResolveRecording(LiveDirectorProductionProfile profile, int hotkey,
        IReadOnlyDictionary<string, IReadOnlyList<Guid>> queryResults)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(queryResults);
        var group = profile.SavedGroups.SingleOrDefault(item => item.Hotkey == hotkey);
        if (group is null) return new(false, [], true, profile.SessionRecording.ExportFormat, "POST_GROUP_UNKNOWN");
        var members = queryResults.GetValueOrDefault(group.Query, []).Distinct().Take(group.MaximumMembers).ToArray();
        if (!profile.SessionRecording.Enabled)
            return new(false, members, true, profile.SessionRecording.ExportFormat, "POST_RECORDING_DISABLED");
        return new(true, members, profile.SessionRecording.RedactPrivateData,
            profile.SessionRecording.ExportFormat, "POST_RECORDING_READY");
    }

    public static NetworkContentDecision ResolveNetworkContent(GameplayProductionProfile gameplay,
        NetworkProductionProfile network, bool signerTrusted, string license)
    {
        ArgumentNullException.ThrowIfNull(gameplay);
        ArgumentNullException.ThrowIfNull(network);
        var licensed = gameplay.CuratedPacks.AllowedLicenses.Contains(license.Trim().ToUpperInvariant(), StringComparer.Ordinal);
        var trusted = !gameplay.CuratedPacks.RequireTrustedSignature || signerTrusted;
        var accepted = licensed && trusted;
        return new(network.Voice.Enabled, network.Voice.Enabled ? network.Voice.MaximumBitrateKbps : 0,
            gameplay.CuratedPacks.RequireTrustedSignature, gameplay.CuratedPacks.AllowOfflineFallback,
            accepted ? "POST_NETWORK_CONTENT_READY" : licensed ? "POST_NETWORK_SIGNER_UNTRUSTED" : "POST_NETWORK_LICENSE_BLOCKED");
    }

    public static ShardReloadDecision PlanShardReload(NetworkProductionProfile profile, string shardKey,
        int packageMegabytes, int activePlayers)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var key = DataDrivenRuleSchema.NormalizeKey(shardKey, nameof(shardKey));
        var shard = profile.Shards.SingleOrDefault(item => item.Key == key);
        if (shard is null) return new(false, key, false, false, 0, "POST_SHARD_UNKNOWN");
        if (activePlayers < 0 || activePlayers > shard.MaximumPlayers)
            return new(false, key, false, false, 0, "POST_SHARD_CAPACITY_INVALID");
        if (!profile.HotReload.Enabled || packageMegabytes is < 1 || packageMegabytes > profile.HotReload.MaximumPackageMegabytes)
            return new(false, key, profile.HotReload.AtomicSwap, profile.HotReload.RequireRollback,
                profile.HotReload.DrainTimeoutSeconds, "POST_HOT_RELOAD_DENIED");
        return new(true, key, profile.HotReload.AtomicSwap, profile.HotReload.RequireRollback,
            profile.HotReload.DrainTimeoutSeconds, "POST_HOT_RELOAD_PREVIEW");
    }

    public static ProtocolDecision NegotiateProtocol(NetworkProductionProfile profile, string platform,
        int minimumVersion, int maximumVersion, IReadOnlyList<string> requestedCapabilities, bool signedExtension)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var normalized = DataDrivenRuleSchema.NormalizeKey(platform, nameof(platform));
        var compatible = profile.Protocol.Platforms.Contains(normalized, StringComparer.Ordinal) &&
                         maximumVersion >= profile.Protocol.MinimumVersion && minimumVersion <= profile.Protocol.MaximumVersion;
        var version = compatible ? Math.Min(maximumVersion, profile.Protocol.MaximumVersion) : 0;
        var extensionAllowed = profile.ServerExtensions.Enabled && signedExtension;
        var granted = extensionAllowed
            ? requestedCapabilities.Select(item => DataDrivenRuleSchema.NormalizeKey(item, nameof(requestedCapabilities)))
                .Intersect(profile.ServerExtensions.AllowedCapabilities, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
            : [];
        return new(compatible, version, extensionAllowed, granted,
            compatible ? "POST_PROTOCOL_COMPATIBLE" : "POST_PROTOCOL_INCOMPATIBLE");
    }

    public static CreatorTechnicalDecision ResolveCreatorTechnical(CreatorExperienceProductionProfile profile,
        IReadOnlyList<string> requestedDeviceQueries, int requestedResourceResults, int requestedAreaInstances)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var queries = profile.DeviceQueries.Enabled
            ? requestedDeviceQueries.Select(item => DataDrivenRuleSchema.NormalizeKey(item, nameof(requestedDeviceQueries)))
                .Intersect(profile.DeviceQueries.AllowedQueries, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
            : [];
        var accepted = profile.ResourceCommands.Enabled && requestedResourceResults is >= 0 &&
                       requestedResourceResults <= profile.ResourceCommands.MaximumResults &&
                       requestedAreaInstances is >= 0 && requestedAreaInstances <= profile.DynamicAreas.MaximumInstances &&
                       (requestedAreaInstances == 0 || profile.DynamicAreas.Enabled);
        return new(accepted, profile.Water.FallbackQuality, queries, profile.ResourceCommands.MaximumResults,
            profile.DynamicAreas.MaximumInstances, $"{profile.Graphics.AntiAliasing}+aniso{profile.Graphics.AnisotropicLevel}",
            accepted ? "POST_CREATOR_TECHNICAL_READY" : "POST_CREATOR_QUOTA_EXCEEDED");
    }

    public static RpgDistributionDecision ResolveRpgDistribution(CreatorExperienceProductionProfile profile,
        int requestedPartyMembers, int requestedTracks)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var accepted = requestedPartyMembers <= profile.PartyControl.MaximumControlledMembers &&
                       requestedTracks <= profile.ProgressionTracks.MaximumTracks;
        return new(accepted && profile.PartyControl.Enabled, profile.PartyControl.MaximumControlledMembers,
            profile.ProgressionTracks.MaximumTracks, profile.RepositoryMetadata.Channel,
            profile.RepositoryMetadata.RequireSignature, profile.Observability.Enabled,
            accepted ? "POST_RPG_DISTRIBUTION_READY" : "POST_RPG_DISTRIBUTION_LIMIT");
    }

    public static ProductionDecision PlanProduction(ProductionAutomationProfile profile, string kind,
        int workUnits, long memoryGrowthMegabytes = 0, int handleGrowth = 0, double p95LatencyMilliseconds = 0)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (kind is "image" or "audio" or "vfx")
        {
            var accepted = profile.MediaGeneration.Enabled && profile.MediaGeneration.MediaKinds.Contains(kind, StringComparer.Ordinal) &&
                           workUnits is >= 1 && workUnits <= profile.MediaGeneration.MaximumJobs;
            return new(accepted, kind, true, workUnits, accepted ? "POST_MEDIA_PREVIEW" : "POST_MEDIA_DENIED");
        }
        if (kind == "soak")
        {
            var accepted = profile.SoakTests.Enabled && workUnits is >= 1 && workUnits <= profile.SoakTests.VirtualPlayers &&
                           memoryGrowthMegabytes <= profile.SoakTests.MaximumMemoryGrowthMegabytes &&
                           handleGrowth <= profile.SoakTests.MaximumHandleGrowth &&
                           p95LatencyMilliseconds <= profile.SoakTests.MaximumP95LatencyMilliseconds;
            return new(accepted, kind, false, workUnits, accepted ? "POST_SOAK_PASS" : "POST_SOAK_BUDGET_EXCEEDED");
        }
        return new(false, kind, false, workUnits, "POST_PRODUCTION_KIND_UNKNOWN");
    }
}
