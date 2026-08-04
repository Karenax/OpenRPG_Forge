namespace Forge.Network.World;

public sealed record CombatPolicy(bool PvpEnabled, FriendlyFirePolicy FriendlyFire);

public sealed class CombatAuthority(PartyService parties, CombatPolicy policy)
{
    public WorldDecision AuthorizeDamage(Guid attackerCharacterId, Guid targetCharacterId, bool targetIsPlayer, long worldRevision)
    {
        if (attackerCharacterId == Guid.Empty || targetCharacterId == Guid.Empty || attackerCharacterId == targetCharacterId)
            return Reject("NET_DAMAGE_TARGET_INVALID", worldRevision);
        if (!targetIsPlayer) return Accept("NET_DAMAGE_ALLOWED", worldRevision);
        if (!policy.PvpEnabled) return Reject("NET_PVP_DISABLED", worldRevision);
        var allies = parties.AreAllies(attackerCharacterId, targetCharacterId);
        if (allies && policy.FriendlyFire is FriendlyFirePolicy.Disabled or FriendlyFirePolicy.PartyOnly)
            return Reject("NET_FRIENDLY_FIRE_DISABLED", worldRevision);
        return Accept("NET_DAMAGE_ALLOWED", worldRevision);
    }

    private static WorldDecision Accept(string code, long revision) => new(true, code, revision, $"accepted:{code}:revision={revision}");
    private static WorldDecision Reject(string code, long revision) => new(false, code, revision, $"rejected:{code}:revision={revision}");
}
