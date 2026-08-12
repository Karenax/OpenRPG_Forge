using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Replication;
using Forge.Network.Scenarios;
using Forge.Network.World;

namespace Forge.Network.Tests;

public sealed class NetworkFoundationProfileTests
{
    [Fact]
    public void Configuration_preview_apply_undo_redo_and_roundtrip_are_exact()
    {
        var initial = NetworkFoundationProfile.CreateDefault();
        var candidate = initial with
        {
            ServerRevision = 1,
            Server = initial.Server with { Capacity = 16 },
            Interest = initial.Interest with { MaximumDistance = 512 }
        };
        var history = new NetworkFoundationHistory(initial);
        var command = new NetworkFoundationCommand(Guid.NewGuid(), 1, "test:r102", 0, "author:karen",
            NetworkRole.Administrator, false, candidate);
        var preview = history.Execute(command);
        Assert.True(preview.Succeeded);
        Assert.False(preview.Applied);
        Assert.Equal(initial, history.Current);
        var applied = history.Execute(command with { Apply = true });
        Assert.True(applied.Applied);
        Assert.Contains("interest", applied.SemanticDiff);
        Assert.Equal(initial, history.Undo("author:karen", NetworkRole.Administrator).Profile);
        Assert.Equal(candidate, history.Redo("author:karen", NetworkRole.Administrator).Profile);

        var store = new NetworkFoundationStore();
        var json = store.Serialize(candidate);
        var loaded = store.Deserialize(json);
        Assert.Equal(candidate.Id, loaded.Id);
        Assert.Equal(candidate.Replication.Components, loaded.Replication.Components);
        Assert.Equal(json, store.Serialize(loaded));
    }

    [Fact]
    public void Validation_localizes_identity_schema_authority_permission_and_stale_revision()
    {
        var initial = NetworkFoundationProfile.CreateDefault();
        var invalid = initial with
        {
            ServerRevision = 1,
            Party = initial.Party with { MaximumMembers = 1 },
            Interest = initial.Interest with { MaximumDistance = 0 }
        };
        var validation = NetworkFoundationValidator.Validate(invalid);
        Assert.Contains(validation, item => item is { Code: "NET_PARTY_CAPACITY_INVALID", Path: "/network/party/maximumMembers" } && item.ObjectId == invalid.Party.Id);
        Assert.Contains(validation, item => item is { Code: "NET_INTEREST_DISTANCE_INVALID", Path: "/network/interest/maximumDistance" });
        var history = new NetworkFoundationHistory(initial);
        var rejected = history.Execute(new(Guid.NewGuid(), 1, "test:r102", 9, "player",
            NetworkRole.Player, true, invalid));
        Assert.False(rejected.Succeeded);
        Assert.Contains(rejected.Diagnostics, item => item.Code == "NET_PERMISSION_DENIED");
        Assert.Contains(rejected.Diagnostics, item => item.Code == "NET_REVISION_STALE");
    }

    [Fact]
    public void Runtime_is_optional_or_required_and_script_handles_are_generational()
    {
        Assert.Empty(NetworkFoundationRuntimeAdapter.Resolve(null, false).Diagnostics);
        Assert.Equal("NET_FOUNDATION_REQUIRED_MISSING",
            Assert.Single(NetworkFoundationRuntimeAdapter.Resolve(null, true).Diagnostics).Code);
        var profile = NetworkFoundationProfile.CreateDefault();
        var runtime = NetworkFoundationRuntimeAdapter.Build(profile);
        Assert.Equal(profile.Id, runtime.Scripts.Read(runtime.Scripts.Handle).Id);
        Assert.Throws<InvalidOperationException>(() => runtime.Scripts.Read(runtime.Scripts.Handle with { Generation = 2 }));
    }

    [Fact]
    public void Party_supports_invitations_roles_leader_loot_quests_markers_and_separation()
    {
        var leader = Guid.NewGuid();
        var member = Guid.NewGuid();
        var service = new PartyService();
        var party = service.Create(leader, new(4, PartyLootMode.RoundRobin, true, true, PartySeparationMode.Block));
        var invite = service.Invite(party.PartyId, leader, member, 1, DateTimeOffset.UtcNow.AddMinutes(5));
        Assert.True(service.AcceptInvitation(invite.Invitation!.InvitationId, member, DateTimeOffset.UtcNow).Accepted);
        Assert.True(service.AssignRole(party.PartyId, leader, member, PartyMemberRole.Officer, 2).Accepted);
        Assert.True(service.TransferLeadership(party.PartyId, leader, member, 3).Accepted);
        Assert.True(service.CanShareQuest(party.PartyId));
        Assert.True(service.CanShareMarker(party.PartyId));
        Assert.Equal("NET_PARTY_SEPARATION_BLOCKED", service.EvaluateSeparation(party.PartyId, 101).Code);
        var eligible = new HashSet<Guid> { leader, member };
        Assert.NotEqual(service.ResolveLootRecipient(party.PartyId, eligible), service.ResolveLootRecipient(party.PartyId, eligible));
    }

    [Fact]
    public void Replication_applies_component_policy_six_interest_rules_and_reconciliation()
    {
        var policy = new ReplicationRuntimePolicy([new("transform", true, true, true)], 4096,
            true, true, true, true, true, true);
        var service = new ReplicationService(policy);
        var connection = Guid.NewGuid();
        var zone = Guid.NewGuid();
        var entity = new ReplicatedEntity(Guid.NewGuid(), zone, new(100, 0, 0), 0,
            new Dictionary<string, string> { ["transform"] = "100,0,0" }, OwnerConnectionId: connection);
        service.Upsert(Guid.NewGuid(), entity, true);
        var observer = new ObserverScope(connection, Guid.NewGuid(), new(0, 0, 0), 1,
            new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid>(),
            new HashSet<Guid> { entity.EntityId });
        var delta = service.CreateDelta(observer, 0);
        Assert.Equal(entity.EntityId, Assert.Single(delta.Upserts).EntityId);
        Assert.True(delta.Compressed);
        Assert.InRange(delta.EstimatedBytes, 1, 4096);
        var predicted = service.Predict(connection, entity.EntityId, "transform", "101,0,0", 1);
        Assert.True(predicted.Accepted);
        Assert.Equal("NET_PREDICTION_CORRECTED", service.Reconcile(predicted.Ticket!.PredictionId).Code);
        Assert.Throws<InvalidOperationException>(() => service.Upsert(Guid.NewGuid(),
            entity with { EntityId = Guid.NewGuid(), Components = new Dictionary<string, string> { ["secret"] = "x" } }, true));
    }

    [Fact]
    public void Dedicated_server_integration_checks_permissions_audit_and_recovery()
    {
        var result = NetworkFoundationAcceptanceScenario.Run();
        Assert.True(result.Passed);
        Assert.Equal("NET_PERMISSION_DENIED", result.PermissionCode);
        Assert.Equal("NET_PREDICTION_CORRECTED", result.PredictionCode);
        Assert.Equal(7, result.AuditEntries);
    }
}
