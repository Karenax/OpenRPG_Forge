using Forge.Domain.Blueprints;
using Forge.Domain.Factions;
using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Application.Placeables;
using Forge.RpgKit.Domain.Placeables;

namespace Forge.RpgKit.Domain.Tests.Placeables;

public sealed class PlaceableFactionRuntimeAdapterTests
{
    private static readonly Guid Sentinels = Guid.Parse("c2100000-0000-4000-8000-000000000001");
    private static readonly Guid Allies = Guid.Parse("c2100000-0000-4000-8000-000000000002");
    private static readonly Guid Raiders = Guid.Parse("c2100000-0000-4000-8000-000000000003");

    [Fact]
    public void Runtime_builds_typed_primary_memberships_and_effect_policies()
    {
        var placeable = Placeable();
        var adapter = new PlaceableFactionRuntimeAdapter();
        var resolution = adapter.Resolve(placeable, Factions());
        var runtime = adapter.Build(resolution.Handle!, placeable, Factions());

        Assert.True(resolution.Available);
        Assert.Equal(Sentinels, runtime.PrimaryFactionId);
        Assert.Equal([Sentinels, Allies], runtime.MembershipFactionIds.Order());
        Assert.True(runtime.InheritHostility);
        Assert.True(runtime.UnauthorizedActionsAreCrime);
        Assert.True(runtime.MembersOnlyAccess);
        Assert.True(runtime.BlockFriendlyTargeting);
    }

    [Fact]
    public void Member_can_use_without_hostility_or_crime_and_friendly_damage_is_blocked()
    {
        var placeable = Placeable();
        var adapter = new PlaceableFactionRuntimeAdapter();
        var handle = adapter.Resolve(placeable, Factions()).Handle!;
        var actor = new PlaceableFactionActor(Allies, new HashSet<Guid> { Allies });
        var use = adapter.Evaluate(handle, placeable, Factions(), actor, "use");
        var damage = adapter.Evaluate(handle, placeable, Factions(), actor, "damage");

        Assert.True(use.IsMember && use.AccessAllowed && use.TargetAllowed);
        Assert.False(use.Hostile || use.Crime);
        Assert.False(damage.TargetAllowed);
        Assert.Equal("RPG_PLACEABLE_FACTION_FRIENDLY_TARGET_BLOCKED", damage.Code);
    }

    [Fact]
    public void Enemy_inherits_hostility_crime_access_and_targeting_outcomes()
    {
        var placeable = Placeable();
        var adapter = new PlaceableFactionRuntimeAdapter();
        var handle = adapter.Resolve(placeable, Factions()).Handle!;
        var actor = new PlaceableFactionActor(Raiders, new HashSet<Guid> { Raiders });
        var decision = adapter.Evaluate(handle, placeable, Factions(), actor, "destroy");

        Assert.False(decision.IsMember || decision.AccessAllowed);
        Assert.True(decision.Hostile && decision.Crime && decision.TargetAllowed);
        Assert.Equal(-80, decision.RelationshipScore);
        Assert.Equal("hostile", decision.Disposition);
        Assert.Equal("RPG_PLACEABLE_FACTION_ACCESS_DENIED", decision.Code);
    }

    [Fact]
    public void Optional_required_unknown_and_stale_profiles_fail_deterministically()
    {
        var placeable = Placeable();
        var adapter = new PlaceableFactionRuntimeAdapter();
        var optional = adapter.Resolve(placeable with
        {
            FactionOwnership = null,
            FactionOwnershipRequiredAtRuntime = false,
        }, Factions());
        var required = adapter.Resolve(placeable with
        {
            FactionOwnership = null,
            FactionOwnershipRequiredAtRuntime = true,
        }, Factions());
        var unknown = adapter.Resolve(placeable with
        {
            FactionOwnership = placeable.FactionOwnership! with
            {
                PrimaryFactionId = Guid.Parse("c2100000-0000-4000-8000-000000000099"),
                MembershipFactionIds = [Guid.Parse("c2100000-0000-4000-8000-000000000099")],
            },
        }, Factions());
        var handle = adapter.Resolve(placeable, Factions()).Handle!;

        Assert.True(optional.OptionalFallback);
        Assert.Equal("WORLD_PLACEABLE_FACTION_OWNERSHIP_REQUIRED_MISSING", required.DiagnosticCode);
        Assert.Equal("WORLD_PLACEABLE_FACTION_REFERENCE_MISSING", unknown.DiagnosticCode);
        Assert.Throws<InvalidOperationException>(() => adapter.Build(
            handle with { OwnershipId = Guid.NewGuid() }, placeable, Factions()));
    }

    private static PlaceableWorldProfile Placeable() => new(
        Guid.Parse("c2100000-0000-4000-8000-000000000010"),
        1,
        "test:ux1-n",
        BlueprintId.From(Guid.Parse("c2100000-0000-4000-8000-000000000011")),
        null,
        null,
        null,
        new([], new(false, 1, 1, true, "placeable")),
        null,
        FactionOwnership: new(
            Guid.Parse("c2100000-0000-4000-8000-000000000012"),
            1,
            "test:ux1-n",
            Sentinels,
            [Sentinels, Allies],
            true,
            true,
            true,
            true),
        FactionOwnershipRequiredAtRuntime: true);

    private static FactionCatalog Factions() => new(
        [
            new(Sentinels, 1, "test:ux1-n", "sentinels", "Sentinelles", false, [], ["guard"]),
            new(Allies, 1, "test:ux1-n", "allies", "Alliés", false, [], ["ally"]),
            new(Raiders, 1, "test:ux1-n", "raiders", "Pillards", false, [], ["enemy"]),
        ],
        [
            new(Raiders, Sentinels, -80, "Guerre ouverte", "hostile"),
            new(Allies, Sentinels, 80, "Alliance", "friendly"),
        ],
        [
            new("hostile", -100, -1, 1),
            new("neutral", 0, 0, 1),
            new("friendly", 1, 100, 1),
        ],
        []);
}
