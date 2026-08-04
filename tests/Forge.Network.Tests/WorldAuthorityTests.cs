using Forge.Network.World;

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
