namespace Forge.Network.World;

public sealed class PartyService
{
    private readonly Dictionary<Guid, Party> _parties = [];
    private readonly Dictionary<Guid, PartyInvitation> _invitations = [];
    private readonly Dictionary<Guid, int> _lootCursors = [];

    public Party Create(Guid leaderCharacterId, PartyRuntimePolicy? policy = null)
    {
        if (leaderCharacterId == Guid.Empty) throw new ArgumentException("NET_PARTY_LEADER_REQUIRED", nameof(leaderCharacterId));
        policy ??= new(8, PartyLootMode.NeedBeforeGreed, true, true, PartySeparationMode.Warn);
        ValidatePolicy(policy);
        var party = new Party(Guid.NewGuid(), leaderCharacterId, new HashSet<Guid> { leaderCharacterId },
            new Dictionary<Guid, PartyMemberRole> { [leaderCharacterId] = PartyMemberRole.Leader }, policy, 1);
        _parties.Add(party.PartyId, party);
        return party;
    }

    public WorldDecision Join(Guid partyId, Guid actorCharacterId, Guid joiningCharacterId, long expectedRevision)
    {
        if (!_parties.TryGetValue(partyId, out var party)) return Reject("NET_PARTY_UNKNOWN", partyId, 0);
        if (!CanManage(party, actorCharacterId)) return Reject("NET_PARTY_PERMISSION_DENIED", partyId, party.Revision);
        if (party.Revision != expectedRevision) return Reject("NET_PARTY_REVISION_STALE", partyId, party.Revision);
        if (joiningCharacterId == Guid.Empty || party.MemberCharacterIds.Contains(joiningCharacterId)) return Reject("NET_PARTY_MEMBER_INVALID", partyId, party.Revision);
        if (party.MemberCharacterIds.Count >= party.Policy.MaximumMembers) return Reject("NET_PARTY_FULL", partyId, party.Revision);
        _parties[partyId] = party with
        {
            MemberCharacterIds = party.MemberCharacterIds.Append(joiningCharacterId).ToHashSet(),
            Roles = party.Roles.Append(new KeyValuePair<Guid, PartyMemberRole>(joiningCharacterId, PartyMemberRole.Member))
                .ToDictionary(pair => pair.Key, pair => pair.Value),
            Revision = party.Revision + 1
        };
        return Accept("NET_PARTY_MEMBER_JOINED", partyId, party.Revision + 1);
    }

    public PartyInvitationDecision Invite(Guid partyId, Guid actorCharacterId, Guid invitedCharacterId,
        long expectedRevision, DateTimeOffset expiresAt)
    {
        if (!_parties.TryGetValue(partyId, out var party)) return RejectInvitation("NET_PARTY_UNKNOWN", partyId, 0);
        if (!CanManage(party, actorCharacterId)) return RejectInvitation("NET_PARTY_PERMISSION_DENIED", partyId, party.Revision);
        if (party.Revision != expectedRevision) return RejectInvitation("NET_PARTY_REVISION_STALE", partyId, party.Revision);
        if (invitedCharacterId == Guid.Empty || party.MemberCharacterIds.Contains(invitedCharacterId))
            return RejectInvitation("NET_PARTY_MEMBER_INVALID", partyId, party.Revision);
        if (expiresAt <= DateTimeOffset.UnixEpoch) return RejectInvitation("NET_PARTY_INVITATION_EXPIRY_INVALID", partyId, party.Revision);
        var invitation = new PartyInvitation(Guid.NewGuid(), partyId, invitedCharacterId, actorCharacterId,
            party.Revision, expiresAt);
        _invitations.Add(invitation.InvitationId, invitation);
        return new(true, "NET_PARTY_INVITATION_ISSUED", invitation, party.Revision,
            $"accepted:NET_PARTY_INVITATION_ISSUED:party={partyId}:actor={actorCharacterId}:invited={invitedCharacterId}:revision={party.Revision}");
    }

    public WorldDecision AcceptInvitation(Guid invitationId, Guid joiningCharacterId, DateTimeOffset now)
    {
        if (!_invitations.Remove(invitationId, out var invitation)) return Reject("NET_PARTY_INVITATION_UNKNOWN", Guid.Empty, 0);
        if (invitation.InvitedCharacterId != joiningCharacterId) return Reject("NET_PARTY_INVITATION_IDENTITY_MISMATCH", invitation.PartyId, invitation.PartyRevision);
        if (invitation.ExpiresAt <= now) return Reject("NET_PARTY_INVITATION_EXPIRED", invitation.PartyId, invitation.PartyRevision);
        return Join(invitation.PartyId, invitation.IssuedByCharacterId, joiningCharacterId, invitation.PartyRevision);
    }

    public WorldDecision AssignRole(Guid partyId, Guid actorCharacterId, Guid memberCharacterId,
        PartyMemberRole role, long expectedRevision)
    {
        if (!_parties.TryGetValue(partyId, out var party)) return Reject("NET_PARTY_UNKNOWN", partyId, 0);
        if (party.LeaderCharacterId != actorCharacterId) return Reject("NET_PARTY_PERMISSION_DENIED", partyId, party.Revision);
        if (party.Revision != expectedRevision) return Reject("NET_PARTY_REVISION_STALE", partyId, party.Revision);
        if (!party.MemberCharacterIds.Contains(memberCharacterId) || role == PartyMemberRole.Leader)
            return Reject("NET_PARTY_ROLE_INVALID", partyId, party.Revision);
        var roles = party.Roles.ToDictionary(pair => pair.Key, pair => pair.Value);
        roles[memberCharacterId] = role;
        _parties[partyId] = party with { Roles = roles, Revision = party.Revision + 1 };
        return Accept("NET_PARTY_ROLE_ASSIGNED", partyId, party.Revision + 1);
    }

    public WorldDecision TransferLeadership(Guid partyId, Guid actorCharacterId, Guid nextLeaderCharacterId,
        long expectedRevision)
    {
        if (!_parties.TryGetValue(partyId, out var party)) return Reject("NET_PARTY_UNKNOWN", partyId, 0);
        if (party.LeaderCharacterId != actorCharacterId) return Reject("NET_PARTY_PERMISSION_DENIED", partyId, party.Revision);
        if (party.Revision != expectedRevision) return Reject("NET_PARTY_REVISION_STALE", partyId, party.Revision);
        if (!party.MemberCharacterIds.Contains(nextLeaderCharacterId) || nextLeaderCharacterId == actorCharacterId)
            return Reject("NET_PARTY_LEADER_INVALID", partyId, party.Revision);
        var roles = party.Roles.ToDictionary(pair => pair.Key, pair => pair.Value);
        roles[actorCharacterId] = PartyMemberRole.Member;
        roles[nextLeaderCharacterId] = PartyMemberRole.Leader;
        _parties[partyId] = party with { LeaderCharacterId = nextLeaderCharacterId, Roles = roles, Revision = party.Revision + 1 };
        return Accept("NET_PARTY_LEADER_TRANSFERRED", partyId, party.Revision + 1);
    }

    public Guid ResolveLootRecipient(Guid partyId, IReadOnlySet<Guid> eligibleCharacterIds)
    {
        var party = _parties.GetValueOrDefault(partyId) ?? throw new KeyNotFoundException("NET_PARTY_UNKNOWN");
        var eligible = party.MemberCharacterIds.Where(eligibleCharacterIds.Contains).Order().ToArray();
        if (eligible.Length == 0) throw new InvalidOperationException("NET_PARTY_LOOT_ELIGIBLE_REQUIRED");
        if (party.Policy.Loot == PartyLootMode.Leader && eligible.Contains(party.LeaderCharacterId)) return party.LeaderCharacterId;
        var cursor = _lootCursors.GetValueOrDefault(partyId);
        var recipient = eligible[cursor % eligible.Length];
        _lootCursors[partyId] = cursor + 1;
        return recipient;
    }

    public bool CanShareQuest(Guid partyId) => Find(partyId)?.Policy.ShareQuests == true;
    public bool CanShareMarker(Guid partyId) => Find(partyId)?.Policy.ShareMarkers == true;

    public WorldDecision EvaluateSeparation(Guid partyId, float distance)
    {
        var party = Find(partyId);
        if (party is null) return Reject("NET_PARTY_UNKNOWN", partyId, 0);
        if (distance < 0 || !float.IsFinite(distance)) return Reject("NET_PARTY_DISTANCE_INVALID", partyId, party.Revision);
        return party.Policy.Separation switch
        {
            PartySeparationMode.Block when distance > 100 => Reject("NET_PARTY_SEPARATION_BLOCKED", partyId, party.Revision),
            PartySeparationMode.Warn when distance > 100 => Accept("NET_PARTY_SEPARATION_WARNING", partyId, party.Revision),
            _ => Accept("NET_PARTY_SEPARATION_ALLOWED", partyId, party.Revision)
        };
    }

    public Party? Find(Guid partyId) => _parties.GetValueOrDefault(partyId);
    public bool AreAllies(Guid first, Guid second) => _parties.Values.Any(p => p.MemberCharacterIds.Contains(first) && p.MemberCharacterIds.Contains(second));

    private static bool CanManage(Party party, Guid actorCharacterId) =>
        party.LeaderCharacterId == actorCharacterId || party.Roles.GetValueOrDefault(actorCharacterId) == PartyMemberRole.Officer;

    private static void ValidatePolicy(PartyRuntimePolicy policy)
    {
        if (policy.MaximumMembers is < 2 or > 64) throw new ArgumentOutOfRangeException(nameof(policy), "NET_PARTY_CAPACITY_INVALID");
    }

    private static PartyInvitationDecision RejectInvitation(string code, Guid id, long revision) =>
        new(false, code, null, revision, $"rejected:{code}:party={id}:revision={revision}");

    private static WorldDecision Accept(string code, Guid id, long revision) => new(true, code, revision, $"accepted:{code}:party={id}:revision={revision}");
    private static WorldDecision Reject(string code, Guid id, long revision) => new(false, code, revision, $"rejected:{code}:party={id}:revision={revision}");
}
