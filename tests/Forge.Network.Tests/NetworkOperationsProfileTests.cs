using Forge.Network.Authoring;
using Forge.Network.Chat;
using Forge.Network.Operations;
using Forge.Network.Scenarios;

namespace Forge.Network.Tests;

public sealed class NetworkOperationsProfileTests
{
    [Fact]
    public void Default_profile_covers_content_lock_roles_chat_and_event_bus()
    {
        var profile = NetworkOperationsProfile.CreateDefault();

        Assert.Empty(NetworkOperationsValidator.Validate(profile));
        Assert.True(profile.Content.DeltaEnabled);
        Assert.True(profile.VersionLock.StrictBeforeJoin);
        Assert.Equal(6, profile.Roles.Grants.Count);
        Assert.Contains(ChatChannelKind.DungeonMaster, profile.Chat.Channels);
        Assert.True(profile.Events.TraceEnabled);
    }

    [Fact]
    public void Validator_reports_localized_paths_and_object_ids()
    {
        var profile = NetworkOperationsProfile.CreateDefault() with
        {
            Content = NetworkOperationsProfile.CreateDefault().Content with { ChunkBytes = 1 },
            VersionLock = NetworkOperationsProfile.CreateDefault().VersionLock with { ContentFingerprint = "bad" },
            Chat = NetworkOperationsProfile.CreateDefault().Chat with { LocalRadius = 0 },
            Events = NetworkOperationsProfile.CreateDefault().Events with { MaximumPendingEvents = 0 }
        };

        var diagnostics = NetworkOperationsValidator.Validate(profile);

        Assert.Contains(diagnostics, item => item is { Code: "NET_CONTENT_POLICY_INVALID", Path: "/network/operations/content" } && item.ObjectId == profile.Content.Id);
        Assert.Contains(diagnostics, item => item.Code == "NET_VERSION_LOCK_INVALID");
        Assert.Contains(diagnostics, item => item.Code == "NET_CHAT_POLICY_INVALID");
        Assert.Contains(diagnostics, item => item.Code == "NET_EVENT_POLICY_INVALID");
    }

    [Fact]
    public void History_previews_applies_rejects_stale_permission_and_compensates()
    {
        var initial = NetworkOperationsProfile.CreateDefault();
        var history = new NetworkOperationsHistory(initial);
        var candidate = initial with { ServerRevision = 1, Chat = initial.Chat with { LocalRadius = 45 } };
        var command = new NetworkOperationsCommand(Guid.NewGuid(), 1, "test:r103", 0, "author",
            NetworkRole.Administrator, false, candidate);

        var preview = history.Execute(command);
        var applied = history.Execute(command with { Apply = true });
        var denied = history.Execute(command with
        {
            CommandId = Guid.NewGuid(),
            ExpectedRevision = 1,
            Role = NetworkRole.Player,
            Candidate = candidate with { ServerRevision = 2 }
        });
        var undone = history.Undo("author", NetworkRole.Administrator);
        var redone = history.Redo("author", NetworkRole.Administrator);

        Assert.True(preview.Succeeded);
        Assert.False(preview.Applied);
        Assert.True(applied.Applied);
        Assert.Contains("chat", applied.SemanticDiff);
        Assert.Contains(denied.Diagnostics, item => item.Code == "NET_OPERATIONS_PERMISSION_DENIED");
        Assert.True(undone.Succeeded);
        Assert.True(redone.Succeeded);
    }

    [Fact]
    public void Store_round_trip_is_exact_and_version_mismatch_is_human_readable()
    {
        var profile = NetworkOperationsProfile.CreateDefault();
        var store = new NetworkOperationsStore();
        var loaded = store.Deserialize(store.Serialize(profile));
        var mismatch = VersionLockNegotiator.Check(profile.VersionLock, profile.VersionLock.ModuleId,
            "2.0.0", "2", new string('c', 64));

        Assert.Equal(store.Serialize(profile), store.Serialize(loaded));
        Assert.False(mismatch.Compatible);
        Assert.Equal("NET_VERSION_LOCK_MISMATCH", mismatch.Code);
        Assert.Equal(3, mismatch.Details.Count);
        Assert.Contains(mismatch.Details, item => item.StartsWith("Module attendu", StringComparison.Ordinal));
    }

    [Fact]
    public void Runtime_resolves_optional_and_required_extensions_and_exposes_typed_handles()
    {
        var profile = NetworkOperationsProfile.CreateDefault();
        var optional = NetworkOperationsRuntimeAdapter.Resolve(null, false);
        var required = NetworkOperationsRuntimeAdapter.Resolve(null, true);
        var runtime = NetworkOperationsRuntimeAdapter.Build(profile);
        var snapshot = runtime.Scripts.Read(runtime.Scripts.Handle);

        Assert.False(optional.Available);
        Assert.Empty(optional.Diagnostics);
        Assert.Equal("NET_OPERATIONS_REQUIRED_MISSING", Assert.Single(required.Diagnostics).Code);
        Assert.Equal(6, snapshot.RoleCount);
        Assert.Equal(profile.Content.Id, runtime.Scripts.Content(runtime.Scripts.Handle).PolicyId);
        Assert.Throws<InvalidOperationException>(() => runtime.Scripts.Read(new(profile.Id, 2)));
    }

    [Fact]
    public void Runtime_applies_profile_grants_channels_and_operational_parameters()
    {
        var profile = NetworkOperationsProfile.CreateDefault();
        var player = profile.Roles.Grants.Single(item => item.Role == NetworkRole.Player);
        profile = profile with
        {
            Content = profile.Content with { ChunkBytes = 32 * 1024, DeltaEnabled = false },
            Roles = profile.Roles with
            {
                Grants = profile.Roles.Grants.Select(item => item.Role == NetworkRole.Player
                    ? item with { Capabilities = [NetworkCapability.Play], CustomCapabilities = ["journal.read"] }
                    : item).ToArray()
            },
            Chat = profile.Chat with
            {
                Channels = profile.Chat.Channels.Where(item => item != ChatChannelKind.Global).ToArray()
            }
        };
        var runtime = NetworkOperationsRuntimeAdapter.Build(profile);
        var subject = new AuthorizationSubject(Guid.NewGuid(), NetworkRole.Player, new HashSet<Guid>(),
            new HashSet<string> { "journal.read" });

        Assert.Equal(32 * 1024, runtime.ContentChunkBytes);
        Assert.False(runtime.ContentDeltaEnabled);
        Assert.Equal(profile.VersionLock, runtime.VersionLock);
        Assert.Equal(profile.Events.Subscriptions, runtime.EventSubscriptions);
        Assert.Equal("NET_AUTH_CAPABILITY_DENIED",
            runtime.Authorization.Authorize(subject, NetworkCapability.Chat).Code);
        Assert.Equal("NET_CHAT_CHANNEL_DENIED",
            runtime.Chat.Send(subject, null, ChatChannelKind.Global, null, "hello").Code);
        Assert.Equal(player.Id, runtime.Roles.Grants.Single(item => item.Role == NetworkRole.Player).Id);
    }

    [Fact]
    public void Incomplete_operation_document_is_rejected_without_null_reference_failure()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            new NetworkOperationsStore().Deserialize("{\"schemaVersion\":1}"));

        Assert.Equal("NET_OPERATIONS_INCOMPLETE", exception.Message);
    }

    [Fact]
    public void Server_acceptance_scenario_exercises_all_five_capabilities()
    {
        var result = NetworkOperationsAcceptanceScenario.Run();

        Assert.True(result.Passed);
        Assert.Equal("NET_CONTENT_SYNC_APPLIED", result.ContentCode);
        Assert.True(result.ContentChunks >= 2);
        Assert.Equal("NET_VERSION_LOCK_MISMATCH", result.VersionCode);
        Assert.Equal("NET_AUTH_ALLOWED", result.PermissionCode);
        Assert.Equal("NET_CHAT_ACCEPTED", result.ChatCode);
        Assert.Equal("NET_EVENT_BACKPRESSURE_REJECTED", result.BackpressureCode);
    }
}
