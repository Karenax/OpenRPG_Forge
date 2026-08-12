using System.Text.Json;
using Forge.Scripting;

namespace Forge.Scripting.Tests;

public sealed class ForgeScriptQualityServiceTests
{
    [Fact]
    public void TestWorld_is_seeded_fixture_driven_assertive_mocked_and_ci_reportable()
    {
        var fixture = new ScriptFixture("fixture", 42,
            new Dictionary<string, object?> { ["hp"] = 10 },
            new Dictionary<string, object?> { ["clock"] = "mock" });
        var test = new ScriptTestCase("damage", fixture,
            world => world.Events.Add($"arranged:{world.Services["clock"]}"),
            world => world.State["hp"] = (int)world.State["hp"]! - world.Random.Next(3, 4),
            [new("hp", world => (int)world.State["hp"]! == 7, "expected 7")]);

        var first = ForgeScriptQualityService.RunSuite([test], 1);
        var second = ForgeScriptQualityService.RunSuite([test], 1);
        var report = ForgeScriptQualityService.ToCiReport(first);

        Assert.True(first[0].Passed);
        Assert.Equal(first[0].Id, second[0].Id);
        Assert.Equal(first[0].Seed, second[0].Seed);
        Assert.Equal(first[0].Passed, second[0].Passed);
        Assert.Equal(first[0].Events, second[0].Events);
        Assert.Contains("\"passed\":1", report);
        Assert.Contains("\"deterministicSeeds\":[42]", report);
        Assert.Throws<InvalidOperationException>(() => ForgeScriptQualityService.RunSuite([test, test], 1));
    }

    [Fact]
    public void Profiler_aggregates_per_handler_call_graph_hotspots_and_threshold_alerts()
    {
        var samples = new[]
        {
            new ProfileSample("combat.as", "Tick", 70, 200, TimeSpan.FromMilliseconds(2), ["OnTick", "Tick"]),
            new ProfileSample("combat.as", "Tick", 60, 150, TimeSpan.FromMilliseconds(3), ["Tick", "Damage"]),
            new ProfileSample("combat.as", "Use", 10, 20, TimeSpan.FromMilliseconds(1), ["OnUse", "Use"]),
        };

        var report = ForgeScriptQualityService.Profile(samples, 100, 250, TimeSpan.FromMilliseconds(4));

        Assert.Equal(2, report.Hotspots.Count);
        Assert.Equal("Tick", report.Hotspots[0].Function);
        Assert.Equal(130, report.Hotspots[0].Instructions);
        Assert.Equal(140, report.TotalInstructions);
        Assert.Equal(3, report.Alerts.Count);
        Assert.Equal(["OnTick", "Tick", "Damage"], report.CallGraph["combat.as::Tick"]);
    }

    [Fact]
    public void Documentation_is_generated_from_versioned_registration_metadata_with_mcp_resources()
    {
        var registration = new ApiRegistration("Damage", "void Damage(Entity target)",
            "Inflicts damage.", "1.0", new HashSet<string> { "server", "rpg-kit" },
            "Damage(hero)", "forge://api/Damage");

        var document = ForgeScriptQualityService.GenerateDocumentation([registration], "1.0");

        Assert.Contains("Do not edit manually", document.Markdown);
        Assert.Contains("## Damage", document.Markdown);
        Assert.Contains("Availability: rpg-kit, server", document.Markdown);
        Assert.Contains("forge://api/Damage", document.Markdown);
        Assert.Equal("forge://api/Damage", Assert.Single(document.McpResources));
    }

    [Fact]
    public void Static_analysis_finds_all_five_versioned_rule_families_with_paths_and_severity_policy()
    {
        var source = new ScriptSource("scripts/test.as",
            "OnHeartbeat FindAll();\nUnsafeQuery();\nDeprecatedApi();\nServerOnly();\ntarget.Use();",
            "client", new HashSet<string> { "target" });
        var settings = ForgeScriptQualityProfile.Default.StaticAnalysis with { TreatWarningsAsErrors = true };

        var diagnostics = ForgeScriptQualityService.Analyze(source, settings);

        Assert.Equal(5, diagnostics.Count);
        Assert.All(diagnostics, item => Assert.Equal("error", item.Severity));
        Assert.Contains(diagnostics, item => item.Code == "SCR-LINT-EXPENSIVE-HEARTBEAT");
        Assert.Contains(diagnostics, item => item.Code == "SCR-LINT-UNSAFE-QUERY");
        Assert.Contains(diagnostics, item => item.Code == "SCR-LINT-NULL" && item.Path == "scripts/test.as:5");
        Assert.Contains(diagnostics, item => item.Code == "SCR-LINT-DEPRECATED");
        Assert.Contains(diagnostics, item => item.Code == "SCR-LINT-AUTHORITY");
    }

    [Fact]
    public void Script_state_persists_only_registered_components_migrates_and_rejects_unknown_or_oversized_state()
    {
        var service = new ForgeScriptQualityService(1024);
        service.RegisterPersistent(new("score", 2, typeof(int), (version, json) =>
            version == 1 ? json.GetInt32() + 1 : json.GetInt32())
        { Validate = value => value is int number && number >= 0 });
        var json = service.SerializeState(new Dictionary<string, object?>
        {
            ["score"] = 7,
            ["vm-pointer"] = "0xBAD",
        });
        var roundTrip = service.DeserializeState(json);
        var migratedEnvelope = JsonSerializer.Serialize(new ScriptStateEnvelope(1,
            new Dictionary<string, PersistedScriptComponent>
            {
                ["score"] = new(1, JsonSerializer.SerializeToElement(7)),
            }));
        var invalid = JsonSerializer.Serialize(new ScriptStateEnvelope(1,
            new Dictionary<string, PersistedScriptComponent>
            {
                ["unknown"] = new(1, JsonSerializer.SerializeToElement(1)),
            }));

        Assert.Contains("score", json);
        Assert.DoesNotContain("vm-pointer", json);
        Assert.Equal(7, roundTrip["score"]);
        Assert.Equal(8, service.DeserializeState(migratedEnvelope)["score"]);
        Assert.Throws<InvalidOperationException>(() => service.DeserializeState(invalid));
        Assert.Throws<InvalidOperationException>(() => service.DeserializeState(new string('x', 1025)));
    }

    [Fact]
    public void Quality_profile_validates_stable_identity_references_schema_api_and_all_feature_settings()
    {
        var valid = ForgeScriptQualityProfile.Default;
        var invalid = valid with
        {
            Id = Guid.Empty,
            ApiVersion = "99.0",
            Documentation = valid.Documentation with { OutputRelativePath = "../generated.md" },
            State = valid.State with { MaximumBytes = 1 },
        };

        Assert.Empty(ForgeScriptQualityProfileValidator.Validate(valid));
        var diagnostics = ForgeScriptQualityProfileValidator.Validate(invalid);
        Assert.Contains(diagnostics, item => item.Code == "script.quality.uuid_required" && item.Path == "script/quality/id");
        Assert.Contains(diagnostics, item => item.Code == "script.quality.api_unsupported");
        Assert.Contains(diagnostics, item => item.Code == "script.quality.documentation_path_invalid");
        Assert.Contains(diagnostics, item => item.Code == "script.quality.state_budget_invalid");
    }

    [Fact]
    public void Quality_configuration_preview_apply_undo_redo_and_atomic_store_round_trip()
    {
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var initial = ForgeScriptQualityProfile.Default;
            var changed = initial with
            {
                Provenance = "test:r100",
                UnitTests = initial.UnitTests with { MaximumCases = 256 },
            };
            var history = new ForgeScriptQualityHistory(initial);
            var preview = history.Preview(new(Guid.NewGuid(), 0, changed, "test preview"));
            var applied = history.Apply(new(Guid.NewGuid(), 0, changed, "test apply"));
            var undone = history.Undo();
            var redone = history.Redo();
            var store = new ForgeScriptQualityStore(root);
            store.Save(redone.Profile);
            var restored = store.Load();

            Assert.True(preview.Succeeded);
            Assert.Equal(0, preview.Revision);
            Assert.True(applied.Succeeded);
            Assert.Equal(initial, undone.Profile);
            Assert.Equal(changed, redone.Profile);
            Assert.Equal(changed.Id, restored.Id);
            Assert.Equal(changed.Provenance, restored.Provenance);
            Assert.Equal(changed.UnitTests, restored.UnitTests);
            Assert.Equal(changed.Documentation.Availability, restored.Documentation.Availability);
            Assert.Equal(changed.StaticAnalysis.EnabledRules, restored.StaticAnalysis.EnabledRules);
            Assert.Equal(changed.State.RegisteredComponents, restored.State.RegisteredComponents);
            Assert.False(history.Apply(new(Guid.NewGuid(), 0, initial, "stale")).Succeeded);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
