using Forge.Domain.Modules;
using Forge.Domain.RuntimeExtensions;

namespace Forge.Domain.Tests.RuntimeExtensions;

public sealed class PostLaunchCapabilityResolverTests
{
    private static readonly ModuleId Module = ModuleId.From(Guid.Parse("41000000-0000-4000-8000-000000000001"));
    private static readonly Guid Area = Guid.Parse("41000000-0000-4000-8000-000000000002");

    [Fact]
    public void G13AC_resolves_zero_gravity_vehicle_and_mount_rules()
    {
        var profile = CreateProfile().Gameplay;
        var result = PostLaunchCapabilityResolver.ResolveMobility(profile, true, .5f, 3, true);

        Assert.True(result.ZeroGravityActive);
        Assert.Equal(7.5f, result.AppliedAcceleration);
        Assert.True(result.VehicleAccepted);
        Assert.True(result.MountAccepted);
    }

    [Fact]
    public void G13AD_unlocks_achievement_and_keeps_curated_content_governed()
    {
        var profile = CreateProfile();
        var achievement = PostLaunchCapabilityResolver.EvaluateAchievement(profile.Gameplay, "crypt-clear", 1);
        var content = PostLaunchCapabilityResolver.ResolveNetworkContent(profile.Gameplay, profile.Network,
            signerTrusted: true, "CC0-1.0");

        Assert.True(achievement.Unlocked);
        Assert.True(achievement.PlatformDispatch);
        Assert.Equal("POST_NETWORK_CONTENT_READY", content.Diagnostic);
        Assert.True(content.TrustedContentRequired);
    }

    [Fact]
    public void G13AE_previews_force_rest_level_migration_and_appearance_override()
    {
        var profile = CreateProfile().LiveDirector;
        var rest = PostLaunchCapabilityResolver.PlanDirectorIntervention(profile, "force-rest", 4);
        var level = PostLaunchCapabilityResolver.PlanDirectorIntervention(profile, "adjust-level", 1, levelDelta: -2);
        var appearance = PostLaunchCapabilityResolver.PlanDirectorIntervention(profile, "appearance", 1, scale: 1.25f);

        Assert.True(rest.Accepted);
        Assert.True(level.SnapshotRequired);
        Assert.Contains("scale=1.25", appearance.SemanticDiff, StringComparison.Ordinal);
    }

    [Fact]
    public void G13AF_resolves_saved_group_and_privacy_safe_recording()
    {
        var profile = CreateProfile().LiveDirector;
        var member = Guid.NewGuid();
        var result = PostLaunchCapabilityResolver.ResolveRecording(profile, 1,
            new Dictionary<string, IReadOnlyList<Guid>> { ["tag:guards"] = [member, member] });

        Assert.True(result.Accepted);
        Assert.Equal([member], result.Members);
        Assert.True(result.RedactPrivateData);
        Assert.Equal("orp-replay", result.ExportFormat);
    }

    [Fact]
    public void G13AG_configures_voice_and_rejects_untrusted_curated_content()
    {
        var profile = CreateProfile();
        var result = PostLaunchCapabilityResolver.ResolveNetworkContent(profile.Gameplay, profile.Network,
            signerTrusted: false, "CC0-1.0");

        Assert.True(result.VoiceEnabled);
        Assert.Equal(96, result.BitrateKbps);
        Assert.Equal("POST_NETWORK_SIGNER_UNTRUSTED", result.Diagnostic);
    }

    [Fact]
    public void G13AH_previews_atomic_shard_hot_reload_with_rollback()
    {
        var result = PostLaunchCapabilityResolver.PlanShardReload(CreateProfile().Network,
            "eu-crypt", packageMegabytes: 64, activePlayers: 12);

        Assert.True(result.Accepted);
        Assert.True(result.AtomicSwap);
        Assert.True(result.Rollback);
        Assert.Equal(45, result.DrainTimeoutSeconds);
    }

    [Fact]
    public void G13AI_negotiates_cross_platform_protocol_and_signed_extensions()
    {
        var result = PostLaunchCapabilityResolver.NegotiateProtocol(CreateProfile().Network, "linux", 1, 3,
            ["metrics", "unsafe-shell"], signedExtension: true);

        Assert.True(result.Compatible);
        Assert.Equal(2, result.NegotiatedVersion);
        Assert.True(result.ExtensionAllowed);
        Assert.Equal(["metrics"], result.GrantedCapabilities);
    }

    [Fact]
    public void G13AJ_resolves_water_device_resource_dynamic_area_and_graphics_capabilities()
    {
        var result = PostLaunchCapabilityResolver.ResolveCreatorTechnical(CreateProfile().CreatorExperience,
            ["graphics-tier", "hardware-serial"], requestedResourceResults: 50, requestedAreaInstances: 2);

        Assert.True(result.Accepted);
        Assert.Equal(["graphics-tier"], result.DeviceQueries);
        Assert.Equal("msaa8+aniso16", result.GraphicsMode);
        Assert.Equal(8, result.DynamicAreaLimit);
    }

    [Fact]
    public void G13AK_resolves_party_progression_repository_and_observability_policy()
    {
        var result = PostLaunchCapabilityResolver.ResolveRpgDistribution(CreateProfile().CreatorExperience,
            requestedPartyMembers: 8, requestedTracks: 32);

        Assert.True(result.PartyControl);
        Assert.Equal(64, result.ProgressionTracks);
        Assert.True(result.SignedMetadata);
        Assert.True(result.Observability);
    }

    [Fact]
    public void G13AL_governs_media_generation_and_evaluates_soak_budgets()
    {
        var profile = CreateProfile().Production;
        var media = PostLaunchCapabilityResolver.PlanProduction(profile, "image", 2);
        var soak = PostLaunchCapabilityResolver.PlanProduction(profile, "soak", 48,
            memoryGrowthMegabytes: 100, handleGrowth: 200, p95LatencyMilliseconds: 180);
        var failed = PostLaunchCapabilityResolver.PlanProduction(profile, "soak", 48,
            memoryGrowthMegabytes: 300, handleGrowth: 200, p95LatencyMilliseconds: 180);

        Assert.True(media.Accepted);
        Assert.True(media.RequiresReview);
        Assert.True(soak.Accepted);
        Assert.False(failed.Accepted);
        Assert.Equal("POST_SOAK_BUDGET_EXCEEDED", failed.Diagnostic);
    }

    private static PostLaunchCapabilityProfile CreateProfile()
    {
        var source = PostLaunchCapabilityProfile.CreateDefault(Module);
        var gameplay = new GameplayProductionProfile(source.Gameplay.Id, 1,
            source.Gameplay.ZeroGravity with { Enabled = true, ThrusterAcceleration = 15, MaximumSpeed = 30 },
            source.Gameplay.Vehicles with { Enabled = true, MaximumSeats = 6, MotionMode = "physics" },
            source.Gameplay.Mounts with { Enabled = true, AllowCombat = true },
            [new GameplayProductionProfile.AchievementDefinition(Guid.Parse("41000000-0000-4000-8000-000000000010"),
                "crypt-clear", "quest.completed", 1, "crypt-clear", false)],
            source.Gameplay.CuratedPacks with { AllowedLicenses = ["CC0-1.0", "MIT"] }, "test");
        var director = new LiveDirectorProductionProfile(source.LiveDirector.Id, 1,
            source.LiveDirector.ForcedRest with { Enabled = true, AllowRulesOverride = true },
            source.LiveDirector.LevelAdjustment with { Enabled = true },
            [new LiveDirectorProductionProfile.SavedGroup(Guid.Parse("41000000-0000-4000-8000-000000000011"),
                "guards", "tag:guards", 1, 32)],
            source.LiveDirector.AppearanceOverrides with { Enabled = true },
            source.LiveDirector.SessionRecording with { Enabled = true }, "test");
        var network = new NetworkProductionProfile(source.Network.Id, 1,
            source.Network.Voice with { Enabled = true, MaximumBitrateKbps = 96 },
            [new NetworkProductionProfile.ShardDefinition(Guid.Parse("41000000-0000-4000-8000-000000000012"),
                "eu-crypt", [Area], 64, "eu-west")],
            source.Network.HotReload with { Enabled = true, DrainTimeoutSeconds = 45 },
            source.Network.ServerExtensions with { Enabled = true, AllowedCapabilities = ["metrics", "world-read"] },
            source.Network.Protocol with { MaximumVersion = 2 }, "test");
        var creator = new CreatorExperienceProductionProfile(source.CreatorExperience.Id, 1,
            source.CreatorExperience.Water with { FallbackQuality = "high" },
            source.CreatorExperience.DeviceQueries with { Enabled = true },
            source.CreatorExperience.ResourceCommands,
            source.CreatorExperience.DynamicAreas with { Enabled = true, MaximumInstances = 8 },
            source.CreatorExperience.PartyControl,
            source.CreatorExperience.ProgressionTracks,
            source.CreatorExperience.RepositoryMetadata,
            source.CreatorExperience.Observability with { Enabled = true },
            source.CreatorExperience.Graphics with { AntiAliasing = "msaa8", AnisotropicLevel = 16 }, "test");
        var production = new ProductionAutomationProfile(source.Production.Id, 1,
            source.Production.MediaGeneration with { Enabled = true, Providers = ["local"] },
            source.Production.SoakTests with
            {
                Enabled = true,
                VirtualPlayers = 64,
                MaximumMemoryGrowthMegabytes = 256,
                MaximumHandleGrowth = 500,
                MaximumP95LatencyMilliseconds = 200,
            }, "test");
        return new PostLaunchCapabilityProfile(source.Id, 1, gameplay, director, network, creator, production, "test");
    }
}
