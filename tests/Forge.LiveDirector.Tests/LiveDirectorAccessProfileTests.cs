using Forge.LiveDirector.Audit;
using Forge.LiveDirector.Authoring;
using Forge.LiveDirector.Players;
using Forge.LiveDirector.Scenarios;
using Forge.LiveDirector.World;
using Forge.Domain.Quality;
using Forge.Network.Authoring;
using Forge.Network.Operations;

namespace Forge.LiveDirector.Tests;

public sealed class LiveDirectorAccessProfileTests
{
    [Fact]
    public void Default_profile_covers_the_five_r105_capabilities()
    {
        var profile = LiveDirectorAccessProfile.CreateDefault();

        Assert.Empty(LiveDirectorAccessValidator.Validate(profile));
        Assert.True(profile.Authentication.RequireSecondFactor);
        Assert.Equal(6, profile.Inspection.VisibleSections.Count);
        Assert.Equal(8, profile.Players.VisibleColumns.Count);
        Assert.True(profile.Audit.ImmutableHashChain);
        Assert.Equal(7, profile.Chat.Channels.Count);
    }

    [Fact]
    public void Validator_reports_localized_paths_and_object_ids()
    {
        var initial = LiveDirectorAccessProfile.CreateDefault();
        var invalid = initial with
        {
            Authentication = initial.Authentication with { RequireSecondFactor = false },
            Inspection = initial.Inspection with { ReadOnlyByDefault = false },
            Players = initial.Players with { RefreshSeconds = 0 },
            Audit = initial.Audit with { ImmutableHashChain = false },
            Chat = initial.Chat with { Channels = ["gm"] }
        };

        var diagnostics = LiveDirectorAccessValidator.Validate(invalid);

        Assert.Contains(diagnostics, item => item is
        { Code: "LIVE_ACCESS_AUTH_POLICY_INVALID", Path: "/liveDirector/access/authentication" } &&
            item.ObjectId == invalid.Authentication.Id);
        Assert.Contains(diagnostics, item => item.Code == "LIVE_ACCESS_INSPECTION_POLICY_INVALID");
        Assert.Contains(diagnostics, item => item.Code == "LIVE_ACCESS_PLAYER_POLICY_INVALID");
        Assert.Contains(diagnostics, item => item.Code == "LIVE_ACCESS_AUDIT_POLICY_INVALID");
        Assert.Contains(diagnostics, item => item.Code == "LIVE_ACCESS_CHAT_POLICY_INVALID");
    }

    [Fact]
    public void History_previews_applies_rejects_players_and_compensates()
    {
        var initial = LiveDirectorAccessProfile.CreateDefault();
        var history = new LiveDirectorAccessHistory(initial);
        var candidate = initial with
        {
            ServerRevision = 1,
            Players = initial.Players with { RefreshSeconds = 3 }
        };
        var command = new LiveDirectorAccessCommand(Guid.NewGuid(), 1, "test:r105", 0, "gm",
            NetworkRole.Administrator, NetworkAuthorityScope.Server, false, candidate);

        var preview = history.Execute(command);
        var applied = history.Execute(command with { Apply = true, CommandId = Guid.NewGuid() });
        var denied = history.Execute(command with
        {
            CommandId = Guid.NewGuid(),
            ExpectedRevision = 1,
            Role = NetworkRole.Player,
            Candidate = candidate with { ServerRevision = 2 }
        });
        var undone = history.Undo("gm", NetworkRole.Administrator);
        var redone = history.Redo("gm", NetworkRole.Administrator);

        Assert.True(preview.Succeeded);
        Assert.False(preview.Applied);
        Assert.True(applied.Applied);
        Assert.Contains("players", applied.SemanticDiff);
        Assert.Contains(denied.Diagnostics, item => item.Code == "LIVE_ACCESS_PERMISSION_DENIED");
        Assert.True(undone.Succeeded);
        Assert.True(redone.Succeeded);
    }

    [Fact]
    public void Store_runtime_resolution_and_forgescript_handles_are_exact()
    {
        var profile = LiveDirectorAccessProfile.CreateDefault();
        var store = new LiveDirectorAccessStore();
        var loaded = store.Deserialize(store.Serialize(profile));
        var optional = LiveDirectorAccessRuntimeAdapter.Resolve(null, false);
        var required = LiveDirectorAccessRuntimeAdapter.Resolve(null, true);
        var runtime = LiveDirectorAccessRuntimeAdapter.Build(loaded);
        var snapshot = runtime.Scripts.Read(runtime.Scripts.Handle);

        Assert.Equal(store.Serialize(profile), store.Serialize(loaded));
        Assert.False(optional.Available);
        Assert.Empty(optional.Diagnostics);
        Assert.Equal("LIVE_ACCESS_REQUIRED_MISSING", Assert.Single(required.Diagnostics).Code);
        Assert.Equal(7, snapshot.ChatChannels);
        Assert.Equal(profile.Audit.Id, runtime.Scripts.Audit(runtime.Scripts.Handle).PolicyId);
        Assert.Throws<InvalidOperationException>(() => runtime.Scripts.Read(new(profile.Id, 2)));
    }

    [Fact]
    public void Player_latency_refresh_is_authoritative_bounded_and_permissioned()
    {
        var auth = new AuthorizationService();
        var chooser = new LiveWorldChooser(auth);
        var service = new LivePlayerService(chooser, auth);
        var zone = Guid.NewGuid();
        var account = Guid.NewGuid();
        service.Upsert(new(account, Guid.NewGuid(), "elyra", "Elyra", zone, 42, null,
            "player", "connected", ["none"]));
        var gm = new AuthorizationSubject(Guid.NewGuid(), NetworkRole.Administrator, new HashSet<Guid> { zone });
        var player = new AuthorizationSubject(Guid.NewGuid(), NetworkRole.Player, new HashSet<Guid> { zone });

        Assert.Equal(1, service.RefreshLatencies(gm, _ => 37));
        Assert.Equal(37, Assert.Single(service.List(gm)).LatencyMs);
        Assert.Throws<ArgumentOutOfRangeException>(() => service.RefreshLatencies(gm, _ => 60_001));
        Assert.Throws<UnauthorizedAccessException>(() => service.RefreshLatencies(player, _ => 40));
    }

    [Fact]
    public void Audit_query_is_bounded_and_verification_recomputes_hashes()
    {
        var actor = Guid.NewGuid();
        var correlation = Guid.NewGuid();
        var journal = new LiveAuditJournal(() => DateTimeOffset.UnixEpoch);
        journal.Append(actor, "director.inspect", [Guid.NewGuid()], "before", "after", "reason", correlation);
        journal.Append(Guid.NewGuid(), "chat.send", [], "", "message", "reason", Guid.NewGuid());

        Assert.True(journal.Verify());
        Assert.Single(journal.Query(actorId: actor, command: "inspect", correlationId: correlation, limit: 1));
        var entries = (List<LiveAuditJournal.Entry>)typeof(LiveAuditJournal)
            .GetField("_entries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(journal)!;
        entries[0] = entries[0] with { After = "tampered" };
        Assert.False(journal.Verify());
    }

    [Fact]
    public void Integrated_server_scenario_exercises_all_five_capabilities()
    {
        var result = LiveDirectorAccessAcceptanceScenario.Run();

        Assert.True(result.Passed, System.Text.Json.JsonSerializer.Serialize(result));
        Assert.Equal("LIVE_GM_AUTHENTICATED", result.AuthenticationCode);
        Assert.Equal("LIVE_INSPECTION_READY", result.InspectionCode);
        Assert.Equal(6, result.InspectionSections);
        Assert.Equal(37, result.LatencyMs);
        Assert.True(result.AuditVerified);
        Assert.Equal("impersonation", result.LastChannel);
        Assert.Equal("LIVE_ACCESS_PERMISSION_DENIED", result.PermissionCode);
    }
}
