using Forge.Network.Content;
using Forge.Network.Replication;
using Forge.Network.Sessions;
using Forge.Network.Testing;
using Forge.Network.World;

namespace Forge.Network.Scenarios;

public sealed record NetworkBetaScenarioResult(
    bool Passed,
    long ServerRevision,
    long ClientARevision,
    long ClientBRevision,
    string SharedDigest,
    string ForgedMutationCode,
    string ReconnectionCode,
    IReadOnlyList<string> Audit);

public static class NetworkBetaScenario
{
    private static readonly Guid ServerId = Guid.Parse("90000000-0000-4000-8000-000000000001");
    private static readonly Guid SessionId = Guid.Parse("90000000-0000-4000-8000-000000000002");
    private static readonly Guid AccountA = Guid.Parse("90000000-0000-4000-8000-00000000000a");
    private static readonly Guid AccountB = Guid.Parse("90000000-0000-4000-8000-00000000000b");
    private static readonly Guid ConnectionA = Guid.Parse("90000000-0000-4000-8000-00000000001a");
    private static readonly Guid ConnectionB = Guid.Parse("90000000-0000-4000-8000-00000000001b");
    private static readonly Guid CharacterA = Guid.Parse("90000000-0000-4000-8000-00000000002a");
    private static readonly Guid CharacterB = Guid.Parse("90000000-0000-4000-8000-00000000002b");

    public static NetworkBetaScenarioResult Run()
    {
        var audit = new List<string>();
        var resource = new ContentResource("project.json", 42, new string('a', 64));
        var manifest = ContentManifestService.Create("crypt-of-ashes", "1.0.0", "1", [resource]);
        var configuration = new DedicatedServerConfiguration(ServerId, 1, "Crypte des Cendres", "127.0.0.1", 5121, 4, DiscoveryMode.Direct, manifest.Fingerprint, "1");
        var session = new AuthoritativeSession(configuration, SessionId);

        var connectA = session.Connect(ConnectionA, new(AccountA, "Elyra", SessionRole.Player), manifest.Fingerprint, "1");
        var connectB = session.Connect(ConnectionB, new(AccountB, "Kael", SessionRole.Player), manifest.Fingerprint, "1");
        audit.Add(connectA.AuditEntry); audit.Add(connectB.AuditEntry);
        var draftA = new CharacterDraft(CharacterA, "Elyra", "human", "guardian", new Dictionary<string, int> { ["might"] = 12 });
        var draftB = new CharacterDraft(CharacterB, "Kael", "elf", "scout", new Dictionary<string, int> { ["agility"] = 13 });
        var selectedA = session.SelectCharacter(ConnectionA, draftA, 2);
        var selectedB = session.SelectCharacter(ConnectionB, draftB, 3);
        audit.Add(selectedA.AuditEntry); audit.Add(selectedB.AuditEntry);
        session.Apply(new(Guid.Parse("90000000-0000-4000-8000-00000000003a"), ConnectionA, 4, "lobby.ready", new Dictionary<string, string> { ["ready"] = "true" }));
        session.Apply(new(Guid.Parse("90000000-0000-4000-8000-00000000003b"), ConnectionB, 5, "lobby.ready", new Dictionary<string, string> { ["ready"] = "true" }));

        var parties = new PartyService();
        var party = parties.Create(CharacterA);
        var joined = parties.Join(party.PartyId, CharacterA, CharacterB, 1);
        audit.Add(joined.AuditEntry);
        var zones = new ZoneInstanceService();
        var template = new ZoneTemplate(Guid.Parse("90000000-0000-4000-8000-000000000040"), "crypt-main", 1, 4, true);
        zones.RegisterTemplate(template);
        var zone = zones.Create(template.TemplateId, ZoneLifecycle.Session, party.PartyId).Instance!;
        zones.Enter(zone.InstanceId, CharacterA, party.PartyId, 1);
        zones.Enter(zone.InstanceId, CharacterB, party.PartyId, 2);

        var replication = new ReplicationService();
        var questEntity = new ReplicatedEntity(Guid.Parse("90000000-0000-4000-8000-000000000050"), zone.InstanceId, new(0, 0, 0), 0, new Dictionary<string, string> { ["quest.ashes"] = "entered-crypt", ["gate.brazier"] = "lit" }, true);
        replication.Upsert(ServerId, questEntity, true);
        var observerA = new ObserverScope(ConnectionA, zone.InstanceId, new(0, 0, 0), 50, new HashSet<Guid>());
        var observerB = new ObserverScope(ConnectionB, zone.InstanceId, new(0, 0, 0), 50, new HashSet<Guid>());
        var deltaA = replication.CreateDelta(observerA, 0);
        var deltaB = replication.CreateDelta(observerB, 0);

        var transport = new DeterministicNetworkHarness(new(1, 0, 0, false));
        transport.Send("server", "client-a", Digest(deltaA));
        transport.Send("server", "client-b", Digest(deltaB));
        transport.Advance();
        var clientADigest = AssertSingle(transport.Received("client-a"));
        var clientBDigest = AssertSingle(transport.Received("client-b"));

        var forged = session.Apply(new(Guid.Parse("90000000-0000-4000-8000-000000000060"), ConnectionA, session.Snapshot.Revision, "world.set-state", new Dictionary<string, string> { ["quest.ashes"] = "completed", ["gold"] = "999999" }));
        audit.Add(forged.AuditEntry);
        var reconnect = new ReconnectionService(TimeProvider.System, TimeSpan.FromMinutes(2));
        var ticket = reconnect.Issue(AccountB, CharacterB, SessionId, deltaB.ToRevision);
        var reconnected = reconnect.Redeem(ticket.Token, AccountB, SessionId);
        audit.Add(reconnected.AuditEntry);
        var catchupB = replication.CreateDelta(observerB, reconnected.Ticket!.LastAcknowledgedRevision);

        var passed = connectA.Accepted && connectB.Accepted && selectedA.Accepted && selectedB.Accepted && joined.Accepted &&
            clientADigest == clientBDigest && !forged.Accepted && forged.Code == "NET_INTENT_FORBIDDEN" && reconnected.Accepted && catchupB.ToRevision == deltaA.ToRevision;
        return new(passed, replication.Revision, deltaA.ToRevision, catchupB.ToRevision, clientADigest, forged.Code, reconnected.Code, audit);
    }

    private static string Digest(ReplicationDelta delta) => string.Join(';', delta.Upserts.Select(entity => $"{entity.EntityId}:{entity.Revision}:{string.Join(',', entity.Components.Select(x => $"{x.Key}={x.Value}"))}"));
    private static string AssertSingle(IReadOnlyList<string> values) => values.Count == 1 ? values[0] : throw new InvalidOperationException("NET_SCENARIO_DELIVERY_INVALID");
}
