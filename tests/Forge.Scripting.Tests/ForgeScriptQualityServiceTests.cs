using System.Text.Json;
using Forge.Scripting;

namespace Forge.Scripting.Tests;

public sealed class ForgeScriptQualityServiceTests
{
    [Fact]
    public void TestWorld_is_seeded_fixture_driven_assertive_and_ci_reportable()
    {
        var test = new ScriptTestCase("damage", new("fixture", 42, new Dictionary<string, object?> { ["hp"] = 10 }, new Dictionary<string, object?> { ["clock"] = "mock" }), world => world.Events.Add("arranged"), world => world.State["hp"] = (int)world.State["hp"]! - 3, [new("hp", world => (int)world.State["hp"]! == 7, "expected 7")]); var result = ForgeScriptQualityService.Run(test); Assert.True(result.Passed); Assert.Contains("\"passed\":1", ForgeScriptQualityService.ToCiReport([result])); Assert.Equal(42, result.Seed);
    }

    [Fact]
    public void Profiler_reports_hotspots_call_paths_totals_and_threshold_alerts()
    {
        var slow = new ProfileSample("a", "Tick", 120, 300, TimeSpan.FromMilliseconds(4), ["OnTick", "Tick"]); var fast = new ProfileSample("a", "Use", 10, 20, TimeSpan.FromMilliseconds(1), ["OnUse", "Use"]); var report = ForgeScriptQualityService.Profile([fast, slow], 100, 250, TimeSpan.FromMilliseconds(3)); Assert.Equal("Tick", report.Hotspots[0].Function); Assert.Equal(130, report.TotalInstructions); Assert.Equal(3, report.Alerts.Count);
    }

    [Fact]
    public void Documentation_is_generated_from_versioned_registration_metadata()
    {
        var registration = new ApiRegistration("Damage", "void Damage(Entity target)", "Inflige des dégâts.", "1.0", new HashSet<string> { "server", "rpg-kit" }, "Damage(hero)", "forge://api/Damage"); var document = ForgeScriptQualityService.GenerateDocumentation([registration], "1.0"); Assert.Contains("## Damage", document.Markdown); Assert.Contains("server", document.Markdown); Assert.Equal("forge://api/Damage", Assert.Single(document.McpResources));
    }

    [Fact]
    public void Static_analysis_finds_heartbeat_query_null_deprecated_and_authority_risks()
    {
        var source = new ScriptSource("scripts/test.as", "OnHeartbeat FindAll();\nUnsafeQuery();\nDeprecatedApi();\nServerOnly();\ntarget.Use();", "client", new HashSet<string> { "target" }); var diagnostics = ForgeScriptQualityService.Analyze(source); Assert.Equal(5, diagnostics.Count); Assert.Contains(diagnostics, item => item.Code == "SCR-LINT-AUTHORITY");
    }

    [Fact]
    public void Script_state_persists_only_registered_components_and_validates_on_restore()
    {
        var service = new ForgeScriptQualityService(); service.RegisterPersistent(new("score", 1, typeof(int), (_, json) => json.GetInt32())); var json = service.SerializeState(new Dictionary<string, object?> { ["score"] = 7, ["vm-pointer"] = "0xBAD" }); Assert.Contains("score", json); Assert.DoesNotContain("vm-pointer", json); Assert.Equal(7, service.DeserializeState(json)["score"]); var invalid = JsonSerializer.Serialize(new ScriptStateEnvelope(1, new Dictionary<string, JsonElement> { ["unknown"] = JsonSerializer.SerializeToElement(1) })); Assert.Throws<InvalidOperationException>(() => service.DeserializeState(invalid));
    }
}
