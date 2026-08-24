using Forge.Domain.Rules;

namespace Forge.Network.World;

public sealed class AreaCombatAuthority(PartyService parties, AreaCombatPolicy policy)
{
    public WorldDecision AuthorizeDamage(Guid attackerCharacterId, Guid targetCharacterId, bool targetIsPlayer,
        long worldRevision, bool serverAuthorityGranted, bool duelAuthorized = false,
        bool hostileFactions = false, bool criminalAction = false, bool aggroInitiation = false)
    {
        if (!serverAuthorityGranted) return Reject("NET_AREA_COMBAT_SERVER_REQUIRED", worldRevision);
        if (!policy.ServerAuthoritative) return Reject("NET_AREA_COMBAT_AUTHORITY_REQUIRED", worldRevision);
        if (attackerCharacterId == Guid.Empty || targetCharacterId == Guid.Empty ||
            attackerCharacterId == targetCharacterId) return Reject("NET_DAMAGE_TARGET_INVALID", worldRevision);
        if (aggroInitiation && !policy.AggroEnabled)
            return Reject("NET_AREA_AGGRO_DISABLED", worldRevision);
        if (criminalAction && !policy.CrimeEnabled) return Reject("NET_AREA_CRIME_DISABLED", worldRevision);
        if (!targetIsPlayer) return Accept("NET_DAMAGE_ALLOWED", worldRevision);
        if (policy.PvpMode == AreaPvpMode.Disabled) return Reject("NET_AREA_PVP_DISABLED", worldRevision);
        if (policy.PvpMode == AreaPvpMode.DuelsOnly && !duelAuthorized)
            return Reject("NET_AREA_DUEL_REQUIRED", worldRevision);
        if (policy.PvpMode == AreaPvpMode.HostileFactions && !hostileFactions)
            return Reject("NET_AREA_HOSTILITY_REQUIRED", worldRevision);
        var allies = parties.AreAllies(attackerCharacterId, targetCharacterId);
        if (allies && policy.FriendlyFire is FriendlyFireMode.Disabled or FriendlyFireMode.PartyOnly)
            return Reject("NET_AREA_FRIENDLY_FIRE_DISABLED", worldRevision);
        return Accept("NET_DAMAGE_ALLOWED", worldRevision);
    }

    public WorldDecision AuthorizeEnvironmentalDamage(long worldRevision, bool serverAuthorityGranted) =>
        serverAuthorityGranted && policy.ServerAuthoritative && policy.EnvironmentalDamageEnabled
            ? Accept("NET_ENVIRONMENT_DAMAGE_ALLOWED", worldRevision)
            : Reject(!serverAuthorityGranted ? "NET_AREA_COMBAT_SERVER_REQUIRED" :
                policy.ServerAuthoritative ? "NET_ENVIRONMENT_DAMAGE_DISABLED" :
                    "NET_AREA_COMBAT_AUTHORITY_REQUIRED", worldRevision);

    private static WorldDecision Accept(string code, long revision) =>
        new(true, code, revision, $"accepted:{code}:revision={revision}");

    private static WorldDecision Reject(string code, long revision) =>
        new(false, code, revision, $"rejected:{code}:revision={revision}");
}
