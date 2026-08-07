using Forge.Domain.Modules;
using Forge.Domain.RuntimePolicies;

namespace Forge.Domain.RuntimeExtensions;

public sealed record PostLaunchCapabilityProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public GameplayProductionProfile Gameplay { get; }
    public LiveDirectorProductionProfile LiveDirector { get; }
    public NetworkProductionProfile Network { get; }
    public CreatorExperienceProductionProfile CreatorExperience { get; }
    public ProductionAutomationProfile Production { get; }
    public string Provenance { get; }

    public PostLaunchCapabilityProfile(Guid id, int schemaVersion, GameplayProductionProfile gameplay,
        LiveDirectorProductionProfile liveDirector, NetworkProductionProfile network,
        CreatorExperienceProductionProfile creatorExperience, ProductionAutomationProfile production,
        string provenance)
    {
        ArgumentNullException.ThrowIfNull(gameplay);
        ArgumentNullException.ThrowIfNull(liveDirector);
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(creatorExperience);
        ArgumentNullException.ThrowIfNull(production);
        var ids = gameplay.AllIds().Concat(liveDirector.AllIds()).Concat(network.AllIds())
            .Concat(creatorExperience.AllIds()).Concat(production.AllIds()).Append(id).ToArray();
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || ids.Distinct().Count() != ids.Length)
            throw new ArgumentException("Post-launch capabilities require schema v1 and globally unique UUIDs.", nameof(id));
        Id = id;
        SchemaVersion = schemaVersion;
        Gameplay = gameplay;
        LiveDirector = liveDirector;
        Network = network;
        CreatorExperience = creatorExperience;
        Production = production;
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }

    public static PostLaunchCapabilityProfile CreateDefault(ModuleId moduleId)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        var root = RuntimePolicyIdFactory.Create(moduleId.Value, "post-launch:root");
        return new PostLaunchCapabilityProfile(root, 1,
            GameplayProductionProfile.CreateDefault(moduleId.Value),
            LiveDirectorProductionProfile.CreateDefault(moduleId.Value),
            NetworkProductionProfile.CreateDefault(moduleId.Value),
            CreatorExperienceProductionProfile.CreateDefault(moduleId.Value),
            ProductionAutomationProfile.CreateDefault(moduleId.Value), "migration-default");
    }

    public IReadOnlyList<Guid> AllIds() => Gameplay.AllIds().Concat(LiveDirector.AllIds())
        .Concat(Network.AllIds()).Concat(CreatorExperience.AllIds()).Concat(Production.AllIds()).Append(Id).ToArray();
}
