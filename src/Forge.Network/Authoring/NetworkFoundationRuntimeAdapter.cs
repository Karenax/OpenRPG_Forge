using Forge.Network.Replication;
using Forge.Network.Sessions;
using Forge.Network.World;

namespace Forge.Network.Authoring;

public sealed record NetworkFoundationResolution(
    bool Available,
    NetworkFoundationProfile? Profile,
    IReadOnlyList<NetworkDiagnostic> Diagnostics);

public sealed record NetworkFoundationRuntime(
    AuthoritativeSession Session,
    PartyService Parties,
    ReplicationService Replication,
    NetworkFoundationScriptService Scripts);

public static class NetworkFoundationRuntimeAdapter
{
    public static NetworkFoundationResolution Resolve(NetworkFoundationProfile? profile, bool required)
    {
        if (profile is null)
        {
            return required
                ? new(false, null,
                    [new("NET_FOUNDATION_REQUIRED_MISSING", "/network", "Required network foundation profile is missing.")])
                : new(false, null, []);
        }

        var diagnostics = NetworkFoundationValidator.Validate(profile);
        return diagnostics.Count == 0
            ? new(true, profile, [])
            : new(false, null, diagnostics);
    }

    public static NetworkFoundationRuntime Build(NetworkFoundationProfile profile, Guid? sessionId = null)
    {
        var resolution = Resolve(profile, profile.RequiredAtRuntime);
        if (!resolution.Available) throw new InvalidOperationException(resolution.Diagnostics[0].Code);
        var replicationPolicy = new ReplicationRuntimePolicy(
            profile.Replication.Components.Select(item => new ComponentReplicationRule(item.ComponentId,
                item.DeltaCompression, item.OwnerPrediction, item.Reconciliation)).ToArray(),
            profile.Replication.MaximumDeltaBytes,
            profile.Interest.ByArea,
            profile.Interest.ByDistance,
            profile.Interest.ByVisibility,
            profile.Interest.ByParty,
            profile.Interest.ByOwnership,
            profile.Interest.ExplicitSubscriptions);
        var partyPolicy = new PartyRuntimePolicy(profile.Party.MaximumMembers,
            profile.Party.Loot switch
            {
                PartyLootPolicy.Leader => PartyLootMode.Leader,
                PartyLootPolicy.RoundRobin => PartyLootMode.RoundRobin,
                _ => PartyLootMode.NeedBeforeGreed
            },
            profile.Party.ShareQuests,
            profile.Party.ShareMarkers,
            profile.Party.Separation switch
            {
                PartySeparationPolicy.Allow => PartySeparationMode.Allow,
                PartySeparationPolicy.Block => PartySeparationMode.Block,
                _ => PartySeparationMode.Warn
            });
        var parties = new PartyService();
        _ = partyPolicy;
        return new(new(profile.Server, sessionId), parties, new(replicationPolicy), new(profile));
    }

    public static Party CreateParty(NetworkFoundationProfile profile, PartyService service, Guid leaderCharacterId)
    {
        var policy = new PartyRuntimePolicy(profile.Party.MaximumMembers,
            (PartyLootMode)profile.Party.Loot, profile.Party.ShareQuests, profile.Party.ShareMarkers,
            (PartySeparationMode)profile.Party.Separation);
        return service.Create(leaderCharacterId, policy);
    }
}
