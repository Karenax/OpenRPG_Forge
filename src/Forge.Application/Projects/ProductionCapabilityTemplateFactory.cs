using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;
using System.Security.Cryptography;
using System.Text;

namespace Forge.Application.Projects;

public static class ProductionCapabilityTemplateFactory
{
    public static ModuleRuntimeExtensionProfile Create(
        ProjectDefinition project,
        string provenance = "sample:production-capabilities")
    {
        ArgumentNullException.ThrowIfNull(project);
        var moduleKey = project.Module.Slug;
        var extensions = project.Module.RuntimePolicies.Extensions;
        var source = extensions.PostLaunch;
        var areaId = project.Areas.FirstOrDefault()?.Id.Value ?? project.Module.EntryAreaId.Value;
        var gameplay = new GameplayProductionProfile(source.Gameplay.Id, 1,
            source.Gameplay.ZeroGravity with { Enabled = true, ThrusterAcceleration = 14, MaximumSpeed = 28 },
            source.Gameplay.Vehicles with { Enabled = true, MaximumSeats = 6, MotionMode = "physics" },
            source.Gameplay.Mounts with { Enabled = true, AllowCombat = true },
            [new GameplayProductionProfile.AchievementDefinition(
                Stable(project.Module.Id.Value, $"achievement:{moduleKey}-completed"), $"{moduleKey}-completed",
                "quest.completed", 1, $"{moduleKey}-completed", false)],
            source.Gameplay.CuratedPacks with { AllowedLicenses = ["CC0-1.0", "MIT"], Channel = "stable" },
            provenance);
        var director = new LiveDirectorProductionProfile(source.LiveDirector.Id, 1,
            source.LiveDirector.ForcedRest with { Enabled = true, AllowRulesOverride = true },
            source.LiveDirector.LevelAdjustment with { Enabled = true },
            [new LiveDirectorProductionProfile.SavedGroup(Stable(project.Module.Id.Value, $"group:{moduleKey}"),
                $"{moduleKey}-actors", "tag:guardian", 1, 32)],
            source.LiveDirector.AppearanceOverrides with { Enabled = true },
            source.LiveDirector.SessionRecording with { Enabled = true }, provenance);
        var network = new NetworkProductionProfile(source.Network.Id, 1,
            source.Network.Voice with { Enabled = true, MaximumBitrateKbps = 96 },
            [new NetworkProductionProfile.ShardDefinition(Stable(project.Module.Id.Value, $"shard:{moduleKey}"),
                $"{moduleKey}-default", [areaId], 64, "local")],
            source.Network.HotReload with { Enabled = true, DrainTimeoutSeconds = 45 },
            source.Network.ServerExtensions with { Enabled = true, AllowedCapabilities = ["metrics", "world-read"] },
            source.Network.Protocol with { MaximumVersion = 2 }, provenance);
        var creator = new CreatorExperienceProductionProfile(source.CreatorExperience.Id, 1,
            source.CreatorExperience.Water with { FallbackQuality = "high" },
            source.CreatorExperience.DeviceQueries with { Enabled = true },
            source.CreatorExperience.ResourceCommands,
            source.CreatorExperience.DynamicAreas with { Enabled = true, MaximumInstances = 8 },
            source.CreatorExperience.PartyControl,
            source.CreatorExperience.ProgressionTracks,
            source.CreatorExperience.RepositoryMetadata,
            source.CreatorExperience.Observability with { Enabled = true },
            source.CreatorExperience.Graphics with { AntiAliasing = "msaa8", AnisotropicLevel = 16 }, provenance);
        var production = new ProductionAutomationProfile(source.Production.Id, 1,
            source.Production.MediaGeneration with { Enabled = true, Providers = ["local"] },
            source.Production.SoakTests with
            {
                Enabled = true,
                DurationMinutes = 120,
                VirtualPlayers = 64,
                MaximumMemoryGrowthMegabytes = 256,
                MaximumHandleGrowth = 500,
                MaximumP95LatencyMilliseconds = 200,
            }, provenance);
        var post = new PostLaunchCapabilityProfile(source.Id, 1, gameplay, director, network, creator,
            production, provenance);
        return new ModuleRuntimeExtensionProfile(extensions.Id, 1, extensions.RuleSchemas,
            extensions.UiDocuments, extensions.JsonDocuments, extensions.SnapshotPolicy, provenance,
            extensions.ModernTooling, extensions.OperationalReadiness, post);
    }

    private static Guid Stable(Guid moduleId, string discriminator)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"openrpg-forge:production-capability-template:{moduleId:D}:{discriminator}"));
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes[..16]);
    }
}
