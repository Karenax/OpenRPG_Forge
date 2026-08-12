using System.Security.Cryptography;
using System.Text;
using Forge.Network.Authoring;
using Forge.Network.Chat;
using Forge.Network.Content;
using Forge.Network.Events;
using Forge.Network.Operations;

namespace Forge.Network.Scenarios;

public sealed record NetworkOperationsAcceptanceResult(bool Passed, long ConfigurationRevision,
    string ContentCode, int ContentChunks, string VersionCode, int VersionDetails,
    string PermissionCode, string ChatCode, int ChatMessages, int BridgeReceipts,
    int EventHandlers, int EventTraces, string BackpressureCode, int AuditEntries);

public static class NetworkOperationsAcceptanceScenario
{
    private static readonly Guid Zone = Guid.Parse("a1030000-0000-4000-8000-000000000101");
    private static readonly Guid Player = Guid.Parse("a1030000-0000-4000-8000-000000000102");
    private static readonly Guid Target = Guid.Parse("a1030000-0000-4000-8000-000000000103");

    public static NetworkOperationsAcceptanceResult Run()
    {
        var initial = NetworkOperationsProfile.CreateDefault("scenario:r103");
        var candidate = initial with
        {
            ServerRevision = 1,
            Content = initial.Content with { ChunkBytes = 16 * 1024 },
            Chat = initial.Chat with { LocalRadius = 35 },
            Events = initial.Events with { MaximumPendingEvents = 8 },
            Provenance = "scenario:r103"
        };
        var history = new NetworkOperationsHistory(initial);
        var applied = history.Execute(new(Guid.Parse("a1030000-0000-4000-8000-000000000104"), 1,
            "scenario:r103", 0, "server.integration", NetworkRole.Administrator, true, candidate));
        var denied = history.Execute(new(Guid.Parse("a1030000-0000-4000-8000-000000000105"), 1,
            "scenario:r103", 1, "player.integration", NetworkRole.Player, true,
            candidate with { ServerRevision = 2 }));
        var store = new NetworkOperationsStore();
        var loaded = store.Deserialize(store.Serialize(history.Current));
        var bridge = new RecordingBridge();
        var runtime = NetworkOperationsRuntimeAdapter.Build(loaded, TimeProvider.System, [bridge]);

        var builder = new AuthorizationSubject(Guid.Parse("a1030000-0000-4000-8000-000000000106"),
            NetworkRole.Builder, new HashSet<Guid> { Zone }, new HashSet<string> { "content.publish" });
        var permission = runtime.Authorization.Authorize(builder, NetworkCapability.BuildContent, Zone,
            "content.publish");

        var chatPlayer = new AuthorizationSubject(Player, NetworkRole.Player, new HashSet<Guid> { Zone });
        var local = runtime.Chat.Send(chatPlayer, Guid.NewGuid(), ChatChannelKind.Local, "spoiler près du feu",
            new(Zone, null, "common", 5, 5));
        var tell = runtime.Chat.Send(chatPlayer, Guid.NewGuid(), ChatChannelKind.Tell, "Retrouve-moi.",
            new(null, Target, "common", 5, 5));
        var observerNear = new ChatObserverContext(Target, null, Zone, 8, 8, NetworkRole.Player);
        var observerFar = observerNear with { X = 100, Y = 100 };

        var eventOrder = new List<string>();
        runtime.Events.Subscribe(new OrderedHandler("quest.updated", "audit", 10, eventOrder));
        runtime.Events.Subscribe(new OrderedHandler("quest.updated", "gameplay", 100, eventOrder));
        runtime.Events.Publish(new(Guid.Parse("a1030000-0000-4000-8000-000000000107"), "quest.updated",
            Guid.Parse("a1030000-0000-4000-8000-000000000108"), builder.AccountId, 1,
            DateTimeOffset.UnixEpoch, new Dictionary<string, string> { ["quest"] = "ashes" }));
        var backpressureCode = ExerciseBackpressure(builder.AccountId);

        var (contentCode, chunks, deltaRequired) = ExerciseContent(loaded, runtime.ContentPolicy);
        var versionMismatch = VersionLockNegotiator.Check(loaded.VersionLock, loaded.VersionLock.ModuleId,
            "2.0.0", loaded.VersionLock.ProtocolVersion, new string('c', 64));
        var script = runtime.Scripts.Read(runtime.Scripts.Handle);
        var audits = new[] { applied.AuditEntry, denied.AuditEntry, permission.AuditEntry,
            local.AuditEntry, tell.AuditEntry, versionMismatch.AuditEntry };
        var passed = applied.Applied && !denied.Succeeded &&
            denied.Diagnostics.Any(item => item.Code == "NET_OPERATIONS_PERMISSION_DENIED") &&
            store.Serialize(loaded) == store.Serialize(candidate) && permission.Allowed &&
            local.Accepted && local.Message!.Text.Contains("•••••••", StringComparison.Ordinal) &&
            local.Message.AccessibleText.Length > local.Message.Text.Length &&
            runtime.Chat.VisibleTo(local.Message, observerNear) && !runtime.Chat.VisibleTo(local.Message, observerFar) &&
            tell.Accepted && runtime.Chat.VisibleTo(tell.Message!, observerNear) &&
            bridge.Messages.Count == 2 && eventOrder.SequenceEqual(["gameplay", "audit"]) &&
            runtime.Events.Traces.Count == 2 && backpressureCode == "NET_EVENT_BACKPRESSURE_REJECTED" &&
            contentCode == "NET_CONTENT_SYNC_APPLIED" && chunks >= 2 && deltaRequired &&
            !versionMismatch.Compatible && versionMismatch.Details.Count == 2 &&
            script.RoleCount == 6 && audits.All(item => item.Contains(':', StringComparison.Ordinal));
        return new(passed, loaded.ServerRevision, contentCode, chunks, versionMismatch.Code,
            versionMismatch.Details.Count, permission.Code, local.Code, runtime.Chat.Since(0, _ => true).Count,
            bridge.Messages.Count, eventOrder.Count, runtime.Events.Traces.Count, backpressureCode, audits.Length);
    }

    private static (string Code, int Chunks, bool DeltaRequired) ExerciseContent(
        NetworkOperationsProfile profile, Forge.Domain.RuntimeExtensions.ContentSynchronizationPolicy policy)
    {
        var root = Path.Combine(Path.GetTempPath(), "OpenRPGForgeR103", Guid.NewGuid().ToString("N"));
        try
        {
            var bytes = Encoding.UTF8.GetBytes(new string('x', profile.Content.ChunkBytes * 2 + 17));
            var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            var resource = new ContentRepositoryResource("modules/crypt/content.bin", bytes.Length, hash,
                "CC0-1.0", "scenario:r103");
            var manifest = ContentRepositoryManifestService.Create(Guid.NewGuid(), profile.VersionLock.ModuleId,
                profile.VersionLock.ModuleVersion, profile.Content.TrustedSignerFingerprints[0], [resource]);
            var partial = Path.Combine(root, profile.Content.CacheDirectory, "partial", $"{hash}.partial");
            Directory.CreateDirectory(Path.GetDirectoryName(partial)!);
            File.WriteAllBytes(partial, bytes[..11]);
            var service = new ContentRepositorySyncService(policy, root, profile.Content.ChunkBytes,
                profile.Content.DeltaEnabled);
            var plan = service.Plan(manifest);
            var applied = service.Synchronize(manifest, new MemoryTransport(resource.Path, bytes));
            return (applied.Valid && applied.Applied ? "NET_CONTENT_SYNC_APPLIED" : "NET_CONTENT_SYNC_FAILED",
                plan.Items.Single().ChunkCount, plan.Items.Single().DeltaRequired);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static string ExerciseBackpressure(Guid actor)
    {
        var bus = new ServerEventBus(1);
        bus.Subscribe(new BurstHandler(bus, actor));
        try
        {
            bus.Publish(new(Guid.NewGuid(), "burst", Guid.NewGuid(), actor, 1, DateTimeOffset.UnixEpoch,
                new Dictionary<string, string>()));
            return "NET_EVENT_BACKPRESSURE_NOT_EXERCISED";
        }
        catch (InvalidOperationException exception)
        {
            return exception.Message;
        }
    }

    private sealed class RecordingBridge : IChatBridgeAdapter
    {
        public string Id => "audit-log";
        public List<ChatMessage> Messages { get; } = [];
        public string Forward(ChatMessage message) { Messages.Add(message); return "forwarded"; }
    }

    private sealed class OrderedHandler(string kind, string id, int priority, ICollection<string> order)
        : IServerEventHandler
    {
        public string EventKind => kind;
        public int Priority => priority;
        public void Handle(ServerEvent serverEvent) => order.Add(id);
    }

    private sealed class BurstHandler(ServerEventBus bus, Guid actor) : IServerEventHandler
    {
        public string EventKind => "burst";
        public void Handle(ServerEvent serverEvent)
        {
            bus.Publish(new(Guid.NewGuid(), "queued", serverEvent.CorrelationId, actor, 1,
                DateTimeOffset.UnixEpoch, new Dictionary<string, string>()));
            bus.Publish(new(Guid.NewGuid(), "overflow", serverEvent.CorrelationId, actor, 1,
                DateTimeOffset.UnixEpoch, new Dictionary<string, string>()));
        }
    }

    private sealed class MemoryTransport(string path, byte[] bytes) : IContentRepositoryTransport
    {
        public Stream OpenRead(string resourcePath, long offset)
        {
            if (!string.Equals(path, resourcePath, StringComparison.Ordinal))
                throw new InvalidOperationException("NET_CONTENT_PATH_MISMATCH");
            return new MemoryStream(bytes[(int)offset..], writable: false);
        }
    }
}
