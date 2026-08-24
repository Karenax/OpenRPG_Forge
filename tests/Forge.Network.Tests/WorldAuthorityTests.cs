using Forge.Network.World;
using Forge.Domain.Areas;
using Forge.Domain.Rules;
using Forge.Domain.RuntimePolicies;

namespace Forge.Network.Tests;

public sealed class WorldAuthorityTests
{
    [Theory]
    [InlineData(CharacterStorageScope.Local)]
    [InlineData(CharacterStorageScope.Server)]
    public void Character_scope_is_preserved_and_owner_enforced(CharacterStorageScope scope)
    {
        var owner = Guid.NewGuid();
        var character = new NetworkCharacter(Guid.NewGuid(), owner, "Elyra", scope, "crypt-v1", 0);
        var registry = new CharacterRegistry();
        Assert.Equal("NET_CHARACTER_OWNER_MISMATCH", registry.Register(character, Guid.NewGuid()).Code);
        Assert.True(registry.Register(character, owner).Accepted);
        Assert.Equal(scope, registry.Find(character.CharacterId)!.StorageScope);
    }

    [Fact]
    public void Only_party_leader_can_add_member_at_expected_revision()
    {
        var leader = Guid.NewGuid();
        var joining = Guid.NewGuid();
        var service = new PartyService();
        var party = service.Create(leader);
        Assert.Equal("NET_PARTY_PERMISSION_DENIED", service.Join(party.PartyId, joining, joining, 1).Code);
        Assert.True(service.Join(party.PartyId, leader, joining, 1).Accepted);
        Assert.Contains(joining, service.Find(party.PartyId)!.MemberCharacterIds);
        Assert.Equal("NET_PARTY_REVISION_STALE", service.Join(party.PartyId, leader, Guid.NewGuid(), 1).Code);
    }

    [Fact]
    public void Pvp_and_friendly_fire_are_authoritative()
    {
        var leader = Guid.NewGuid();
        var ally = Guid.NewGuid();
        var parties = new PartyService();
        var party = parties.Create(leader);
        parties.Join(party.PartyId, leader, ally, 1);
        var authority = new CombatAuthority(parties, new(true, FriendlyFirePolicy.Disabled));
        Assert.Equal("NET_FRIENDLY_FIRE_DISABLED", authority.AuthorizeDamage(leader, ally, true, 5).Code);
        Assert.True(authority.AuthorizeDamage(leader, Guid.NewGuid(), true, 5).Accepted);
        Assert.True(authority.AuthorizeDamage(leader, Guid.NewGuid(), false, 5).Accepted);
    }

    [Fact]
    public void Area_combat_requires_server_authority_and_honors_duels_crime_and_environment()
    {
        var leader = Guid.NewGuid();
        var ally = Guid.NewGuid();
        var parties = new PartyService();
        var party = parties.Create(leader);
        parties.Join(party.PartyId, leader, ally, 1);
        var policy = new AreaCombatPolicy(Guid.NewGuid(), 1, AreaPvpMode.DuelsOnly,
            FriendlyFireMode.Enabled, true, true, false, true, "test");
        var authority = new AreaCombatAuthority(parties, policy);
        Assert.Equal("NET_AREA_DUEL_REQUIRED",
            authority.AuthorizeDamage(leader, ally, true, 8, serverAuthorityGranted: true).Code);
        Assert.True(authority.AuthorizeDamage(leader, ally, true, 8, serverAuthorityGranted: true,
            duelAuthorized: true).Accepted);
        Assert.Equal("NET_ENVIRONMENT_DAMAGE_DISABLED",
            authority.AuthorizeEnvironmentalDamage(8, serverAuthorityGranted: true).Code);
        Assert.Equal("NET_AREA_COMBAT_SERVER_REQUIRED",
            authority.AuthorizeDamage(leader, ally, true, 8, serverAuthorityGranted: false,
                duelAuthorized: true).Code);
        Assert.Equal("NET_AREA_COMBAT_SERVER_REQUIRED",
            authority.AuthorizeEnvironmentalDamage(8, serverAuthorityGranted: false).Code);
        var localPolicy = new AreaCombatPolicy(policy.Id, 1, policy.PvpMode, policy.FriendlyFire,
            policy.CrimeEnabled, policy.AggroEnabled, policy.EnvironmentalDamageEnabled, false, "test");
        var local = new AreaCombatAuthority(parties, localPolicy);
        Assert.Equal("NET_AREA_COMBAT_AUTHORITY_REQUIRED",
            local.AuthorizeDamage(leader, Guid.NewGuid(), true, 8, serverAuthorityGranted: true,
                duelAuthorized: true).Code);
    }

    [Fact]
    public void Area_combat_applies_aggro_before_live_non_player_damage()
    {
        var attacker = Guid.NewGuid();
        var target = Guid.NewGuid();
        var parties = new PartyService();
        var disabled = new AreaCombatPolicy(Guid.NewGuid(), 1, AreaPvpMode.Disabled,
            FriendlyFireMode.Disabled, true, false, true, true, "test");
        var authority = new AreaCombatAuthority(parties, disabled);

        var rejected = authority.AuthorizeDamage(attacker, target, targetIsPlayer: false, worldRevision: 12,
            serverAuthorityGranted: true, aggroInitiation: true);
        var passiveDamage = authority.AuthorizeDamage(attacker, target, targetIsPlayer: false, worldRevision: 13,
            serverAuthorityGranted: true, aggroInitiation: false);

        Assert.False(rejected.Accepted);
        Assert.Equal("NET_AREA_AGGRO_DISABLED", rejected.Code);
        Assert.Equal(12, rejected.Revision);
        Assert.True(passiveDamage.Accepted);
        Assert.Equal("NET_DAMAGE_ALLOWED", passiveDamage.Code);
    }

    [Fact]
    public void Cross_area_patrol_transfer_requires_server_owner_lease_and_revision()
    {
        var segment = new CrossAreaPatrolSegment(Guid.NewGuid(), AreaId.From(Guid.NewGuid()),
            AreaId.From(Guid.NewGuid()), PortalId.From(Guid.NewGuid()), Guid.NewGuid(), true,
            PatrolOwnershipAuthority.Server);
        var now = new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);
        var lease = new PatrolOwnershipLease("guard.elyra", "server.eu-1", 4, now.AddMinutes(1));
        Assert.Equal("NET_PATROL_SERVER_REQUIRED", CrossAreaPatrolAuthority.Authorize(segment, lease,
            "guard.elyra", "server.eu-1", 4, now, false).Code);
        Assert.Equal("NET_PATROL_OWNERSHIP_DENIED", CrossAreaPatrolAuthority.Authorize(segment, lease,
            "guard.elyra", "server.eu-2", 4, now, true).Code);
        var accepted = CrossAreaPatrolAuthority.Authorize(segment, lease, "guard.elyra", "server.eu-1", 4, now,
            true);
        Assert.True(accepted.Allowed);
        Assert.Equal(5, accepted.Revision);
    }

    [Fact]
    public void Dynamic_zone_is_bounded_and_party_isolated()
    {
        var template = new ZoneTemplate(Guid.NewGuid(), "crypt-depths", 1, 2, true);
        var party = Guid.NewGuid();
        var service = new ZoneInstanceService();
        Assert.Empty(service.RegisterTemplate(template));
        var created = service.Create(template.TemplateId, ZoneLifecycle.Session, party);
        Assert.True(created.Decision.Accepted);
        Assert.Equal("NET_ZONE_INSTANCE_LIMIT", service.Create(template.TemplateId, ZoneLifecycle.Ephemeral, party).Decision.Code);
        Assert.Equal("NET_ZONE_PARTY_MISMATCH", service.Enter(created.Instance!.InstanceId, Guid.NewGuid(), Guid.NewGuid(), 1).Code);
        Assert.True(service.Enter(created.Instance.InstanceId, Guid.NewGuid(), party, 1).Accepted);
    }

    [Fact]
    public void Invalid_zone_template_reports_localized_diagnostics()
    {
        var diagnostics = new ZoneInstanceService().RegisterTemplate(new(Guid.Empty, "", 0, 65, true));
        Assert.Equal(4, diagnostics.Count);
        Assert.Contains(diagnostics, x => x.Code == "NET_ZONE_INSTANCE_CAP_INVALID" && x.Path == "/network/zones/maxInstances");
    }
}
