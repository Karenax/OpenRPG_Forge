namespace Forge.Network.World;

public sealed class PartyService
{
    private readonly Dictionary<Guid, Party> _parties = [];

    public Party Create(Guid leaderCharacterId)
    {
        if (leaderCharacterId == Guid.Empty) throw new ArgumentException("NET_PARTY_LEADER_REQUIRED", nameof(leaderCharacterId));
        var party = new Party(Guid.NewGuid(), leaderCharacterId, new HashSet<Guid> { leaderCharacterId }, 1);
        _parties.Add(party.PartyId, party);
        return party;
    }

    public WorldDecision Join(Guid partyId, Guid actorCharacterId, Guid joiningCharacterId, long expectedRevision)
    {
        if (!_parties.TryGetValue(partyId, out var party)) return Reject("NET_PARTY_UNKNOWN", partyId, 0);
        if (party.LeaderCharacterId != actorCharacterId) return Reject("NET_PARTY_PERMISSION_DENIED", partyId, party.Revision);
        if (party.Revision != expectedRevision) return Reject("NET_PARTY_REVISION_STALE", partyId, party.Revision);
        if (joiningCharacterId == Guid.Empty || party.MemberCharacterIds.Contains(joiningCharacterId)) return Reject("NET_PARTY_MEMBER_INVALID", partyId, party.Revision);
        _parties[partyId] = party with { MemberCharacterIds = party.MemberCharacterIds.Append(joiningCharacterId).ToHashSet(), Revision = party.Revision + 1 };
        return Accept("NET_PARTY_MEMBER_JOINED", partyId, party.Revision + 1);
    }

    public Party? Find(Guid partyId) => _parties.GetValueOrDefault(partyId);
    public bool AreAllies(Guid first, Guid second) => _parties.Values.Any(p => p.MemberCharacterIds.Contains(first) && p.MemberCharacterIds.Contains(second));

    private static WorldDecision Accept(string code, Guid id, long revision) => new(true, code, revision, $"accepted:{code}:party={id}:revision={revision}");
    private static WorldDecision Reject(string code, Guid id, long revision) => new(false, code, revision, $"rejected:{code}:party={id}:revision={revision}");
}
