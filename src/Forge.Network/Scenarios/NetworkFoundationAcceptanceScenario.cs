using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Replication;
using Forge.Network.Sessions;
using Forge.Network.World;

namespace Forge.Network.Scenarios;

public sealed record NetworkFoundationAcceptanceResult(
    bool Passed,
    long ConfigurationRevision,
    int PartyMembers,
    int ReplicatedEntities,
    string PredictionCode,
    string PermissionCode,
    int AuditEntries);

public static class NetworkFoundationAcceptanceScenario
{
    private static readonly Guid Leader = Guid.Parse("a1020000-0000-4000-8000-000000000101");
    private static readonly Guid Member = Guid.Parse("a1020000-0000-4000-8000-000000000102");
    private static readonly Guid Connection = Guid.Parse("a1020000-0000-4000-8000-000000000103");
    private static readonly Guid Entity = Guid.Parse("a1020000-0000-4000-8000-000000000104");
    private static readonly Guid Zone = Guid.Parse("a1020000-0000-4000-8000-000000000105");

    public static NetworkFoundationAcceptanceResult Run()
    {
        var initial = NetworkFoundationProfile.CreateDefault("scenario:r102");
        var candidate = initial with
        {
            ServerRevision = 1,
            Dedicated = initial.Dedicated with { ServiceMode = DedicatedServiceMode.WindowsService },
            Party = initial.Party with { Separation = PartySeparationPolicy.Block }
        };
        var history = new NetworkFoundationHistory(initial);
        var command = new NetworkFoundationCommand(Guid.Parse("a1020000-0000-4000-8000-000000000106"),
            1, "scenario:r102", 0, "server.integration", NetworkRole.Administrator, true, candidate);
        var applied = history.Execute(command);
        var permission = history.Execute(command with
        {
            CommandId = Guid.Parse("a1020000-0000-4000-8000-000000000107"),
            ExpectedRevision = 1,
            Role = NetworkRole.Player,
            Candidate = candidate with { ServerRevision = 2 }
        });

        var runtime = NetworkFoundationRuntimeAdapter.Build(history.Current,
            Guid.Parse("a1020000-0000-4000-8000-000000000108"));
        var party = NetworkFoundationRuntimeAdapter.CreateParty(history.Current, runtime.Parties, Leader);
        var invitation = runtime.Parties.Invite(party.PartyId, Leader, Member, 1,
            new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var joined = runtime.Parties.AcceptInvitation(invitation.Invitation!.InvitationId, Member,
            new DateTimeOffset(2026, 8, 11, 0, 0, 0, TimeSpan.Zero));
        var role = runtime.Parties.AssignRole(party.PartyId, Leader, Member, PartyMemberRole.Officer, 2);

        runtime.Replication.Upsert(initial.Server.ServerId,
            new(Entity, Zone, new(2, 0, 0), 0,
                new Dictionary<string, string> { ["transform"] = "2,0,0", ["gameplay"] = "hp=12" },
                OwnerConnectionId: Connection), true);
        var observer = new ObserverScope(Connection, Zone, new(0, 0, 0), 10,
            new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { Entity },
            new HashSet<Guid> { Entity });
        var delta = runtime.Replication.CreateDelta(observer, 0);
        var prediction = runtime.Replication.Predict(Connection, Entity, "transform", "3,0,0", 1);
        runtime.Replication.Upsert(initial.Server.ServerId,
            new(Entity, Zone, new(3.5f, 0, 0), 0,
                new Dictionary<string, string> { ["transform"] = "3.5,0,0", ["gameplay"] = "hp=12" },
                OwnerConnectionId: Connection), true);
        var reconciled = runtime.Replication.Reconcile(prediction.Ticket!.PredictionId);

        var script = runtime.Scripts.Read(runtime.Scripts.Handle);
        var auditEntries = new[] { applied.AuditEntry, permission.AuditEntry, invitation.AuditEntry,
            joined.AuditEntry, role.AuditEntry, prediction.AuditEntry, reconciled.AuditEntry };
        var passed = applied.Applied && !permission.Succeeded &&
            permission.Diagnostics.Any(item => item.Code == "NET_PERMISSION_DENIED") &&
            joined.Accepted && role.Accepted && runtime.Parties.Find(party.PartyId)!.MemberCharacterIds.Count == 2 &&
            delta.Upserts.Count == 1 && delta.Compressed && prediction.Accepted &&
            reconciled.Code == "NET_PREDICTION_CORRECTED" && script.Revision == 1 &&
            auditEntries.All(entry => entry.Contains(':', StringComparison.Ordinal));
        return new(passed, history.Current.ServerRevision,
            runtime.Parties.Find(party.PartyId)!.MemberCharacterIds.Count, delta.Upserts.Count,
            reconciled.Code, permission.Diagnostics.First().Code, auditEntries.Length);
    }
}
