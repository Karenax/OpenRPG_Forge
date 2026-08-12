using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Scenarios;
using Forge.Network.Security;
using Forge.Network.Testing;

namespace Forge.Network.Tests;

public sealed class NetworkAssuranceProfileTests
{
    [Fact]
    public void Default_profile_covers_logs_anti_cheat_and_deterministic_tests()
    {
        var profile = NetworkAssuranceProfile.CreateDefault();

        Assert.Empty(NetworkAssuranceValidator.Validate(profile));
        Assert.NotEmpty(profile.Logs.RemoteSinks);
        Assert.True(profile.AntiCheat.RequireSignedContent);
        Assert.True(profile.AntiCheat.AuthoritativeRandom);
        Assert.False(profile.AntiCheat.TrustClientSaves);
        Assert.True(profile.DeterministicTests.ReplayPasses >= 2);
    }

    [Fact]
    public void Validator_reports_localized_paths_and_object_ids()
    {
        var initial = NetworkAssuranceProfile.CreateDefault();
        var profile = initial with
        {
            Logs = initial.Logs with { RotationBytes = 1 },
            AntiCheat = initial.AntiCheat with { TrustClientSaves = true },
            DeterministicTests = initial.DeterministicTests with { ClientCount = 1 }
        };

        var diagnostics = NetworkAssuranceValidator.Validate(profile);

        Assert.Contains(diagnostics, item => item is
        { Code: "NET_ASSURANCE_LOG_POLICY_INVALID", Path: "/network/assurance/logs" } &&
            item.ObjectId == profile.Logs.Id);
        Assert.Contains(diagnostics, item => item.Code == "NET_ASSURANCE_ANTICHEAT_POLICY_INVALID");
        Assert.Contains(diagnostics, item => item.Code == "NET_ASSURANCE_TEST_POLICY_INVALID");
    }

    [Fact]
    public void History_previews_applies_rejects_permission_and_compensates()
    {
        var initial = NetworkAssuranceProfile.CreateDefault();
        var history = new NetworkAssuranceHistory(initial);
        var candidate = initial with
        {
            ServerRevision = 1,
            Logs = initial.Logs with { RotationFiles = 5 }
        };
        var command = new NetworkAssuranceCommand(Guid.NewGuid(), 1, "test:r104", 0, "author",
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
        Assert.Contains("structured-logs", applied.SemanticDiff);
        Assert.Contains(denied.Diagnostics, item => item.Code == "NET_ASSURANCE_PERMISSION_DENIED");
        Assert.True(undone.Succeeded);
        Assert.True(redone.Succeeded);
    }

    [Fact]
    public void Store_round_trip_runtime_resolution_and_handles_are_exact()
    {
        var profile = NetworkAssuranceProfile.CreateDefault();
        var store = new NetworkAssuranceStore();
        var loaded = store.Deserialize(store.Serialize(profile));
        var optional = NetworkAssuranceRuntimeAdapter.Resolve(null, false);
        var required = NetworkAssuranceRuntimeAdapter.Resolve(null, true);
        var runtime = NetworkAssuranceRuntimeAdapter.Build(loaded, sinks:
            [new Forge.Network.Observability.MemoryRemoteLogSink("audit")]);
        var snapshot = runtime.Scripts.Read(runtime.Scripts.Handle);

        Assert.Equal(store.Serialize(profile), store.Serialize(loaded));
        Assert.False(optional.Available);
        Assert.Empty(optional.Diagnostics);
        Assert.Equal("NET_ASSURANCE_REQUIRED_MISSING", Assert.Single(required.Diagnostics).Code);
        Assert.Equal(3, snapshot.SimulatedClients);
        Assert.Equal(profile.Logs.Id, runtime.Scripts.Logs(runtime.Scripts.Handle).PolicyId);
        Assert.Throws<InvalidOperationException>(() => runtime.Scripts.Read(new(profile.Id, 2)));
    }

    [Fact]
    public void Anti_cheat_enforces_signed_content_authoritative_rng_and_untrusted_saves()
    {
        var profile = NetworkAssuranceProfile.CreateDefault();
        var service = new AntiCheatService(profile.AntiCheat);
        var connection = Guid.NewGuid();

        var allowed = service.ValidateJoin(connection, new(new string('b', 64), new string('a', 64), true));
        var rejected = service.ValidateJoin(connection, new(new string('b', 64), new string('c', 64), true));
        var save = service.ValidateClientSave(connection, "untrusted"u8);
        var random = service.NextAuthoritativeRandom(1, 21);
        var repeated = new AntiCheatService(profile.AntiCheat).NextAuthoritativeRandom(1, 21);

        Assert.True(allowed.Allowed);
        Assert.Equal("NET_ANTICHEAT_CONTENT_SIGNATURE_REJECTED", rejected.Code);
        Assert.True(rejected.Quarantined);
        Assert.Equal("NET_ANTICHEAT_CLIENT_SAVE_REJECTED", save.Code);
        Assert.Equal(random, repeated);
    }

    [Fact]
    public void Deterministic_runner_replays_faults_soak_and_authoritative_state()
    {
        var profile = NetworkAssuranceProfile.CreateDefault();
        var first = DeterministicNetworkTestRunner.Run(profile.DeterministicTests);
        var second = DeterministicNetworkTestRunner.Run(profile.DeterministicTests);

        Assert.True(first.Passed);
        Assert.Equal("NET_DETERMINISM_PASS", first.Code);
        Assert.Equal(profile.DeterministicTests.ClientCount * profile.DeterministicTests.ReplayPasses,
            first.StateAssertions);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
    }

    [Fact]
    public void Server_acceptance_scenario_exercises_all_three_capabilities()
    {
        var result = NetworkAssuranceAcceptanceScenario.Run();

        Assert.True(result.Passed, System.Text.Json.JsonSerializer.Serialize(result));
        Assert.Equal("NET_LOG_ROUTED", result.LogCode);
        Assert.True(result.LogSegments >= 2);
        Assert.Equal("NET_ANTICHEAT_JOIN_ALLOWED", result.JoinCode);
        Assert.True(result.Quarantined);
        Assert.Equal("NET_ANTICHEAT_CLIENT_SAVE_REJECTED", result.ClientSaveCode);
        Assert.Equal("NET_DETERMINISM_PASS", result.TestCode);
    }
}
