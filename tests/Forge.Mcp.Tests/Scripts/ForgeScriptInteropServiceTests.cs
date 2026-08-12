using Forge.Mcp.Scripts;
using Forge.Scripting;

namespace Forge.Mcp.Tests.Scripts;

public sealed class ForgeScriptInteropServiceTests
{
    private const string EventSource =
        "void OnDamaged(const int &in e) {} void main() { say(\"ready\"); }";

    [Fact]
    public void Interop_exposes_versioned_api_source_compile_diagnostics_tests_and_binding_preview()
    {
        var service = new ForgeScriptInteropService(new NativeForgeScriptRuntime());
        var created = service.CreateOrUpdateSource(new(Guid.NewGuid(), 0, "scripts/greet.as",
            "void main() { say(\"hello\"); }", "mcp:test"));
        var tests = service.RunTests("scripts/greet.as");
        var preview = service.PreviewBinding("entity:hero", "OnUse", "scripts/greet.as");

        Assert.Equal("1.0.0", service.GetApi().ApiVersion);
        Assert.True(created.Succeeded);
        Assert.Equal(1, service.Revision);
        Assert.True(service.Compile("scripts/greet.as").Succeeded);
        Assert.Empty(service.GetDiagnostics("scripts/greet.as"));
        Assert.True(tests.Succeeded);
        Assert.Contains(tests.Events, item => item.Kind == "say" && item.Value == "hello");
        Assert.True(preview.Resolved);
        Assert.Equal("bind:entity:hero.OnUse->scripts/greet.as", preview.SemanticDiff);
    }

    [Fact]
    public void Interop_rejects_stale_invalid_duplicate_and_oversized_mutations_with_localized_diagnostics()
    {
        var profile = ForgeScriptInteropProfile.Default with { MaximumSourceBytes = 1024 };
        var service = new ForgeScriptInteropService(new NativeForgeScriptRuntime(), profile);
        var sourceId = Guid.NewGuid();
        Assert.True(service.CreateOrUpdateSource(new(Guid.NewGuid(), 0, "scripts/greet.as",
            "void main() {}", "editor:test", sourceId)).Succeeded);

        var stale = service.PlanCreateOrUpdateSource(new(Guid.NewGuid(), 0, "scripts/stale.as",
            "void main() {}", "mcp:test"));
        var invalid = service.PlanCreateOrUpdateSource(new(Guid.NewGuid(), 1, "scripts/broken.as",
            "void main( {", "mcp:test"));
        var duplicate = service.PlanCreateOrUpdateSource(new(Guid.NewGuid(), 1, "scripts/duplicate.as",
            "void main() {}", "mcp:test", sourceId));
        var oversized = service.PlanCreateOrUpdateSource(new(Guid.NewGuid(), 1, "scripts/large.as",
            $"void main() {{ /*{new string('x', 1024)}*/ }}", "mcp:test"));

        Assert.False(stale.Succeeded);
        Assert.False(invalid.Succeeded);
        Assert.NotEmpty(invalid.Diagnostics);
        Assert.False(duplicate.Succeeded);
        Assert.Equal("script.source_uuid_duplicate", Assert.Single(duplicate.Diagnostics).Code);
        Assert.Equal("scripts/duplicate.as", duplicate.Diagnostics[0].Path);
        Assert.Equal("error", duplicate.Diagnostics[0].Severity);
        Assert.False(oversized.Succeeded);
        Assert.Equal("script.source_quota_exceeded", Assert.Single(oversized.Diagnostics).Code);
        Assert.Equal(1, service.Revision);
        Assert.False(service.PreviewBinding("entity", "OnUse", "scripts/missing.as").Resolved);
    }

    [Fact]
    public void Source_plan_previews_applies_undoes_and_redoes_without_parallel_mutation()
    {
        var service = new ForgeScriptInteropService(new NativeForgeScriptRuntime());
        var plan = service.PlanCreateOrUpdateSource(new(Guid.NewGuid(), 0,
            "scripts/events.as", EventSource, "mcp:test", Guid.NewGuid(), true));

        Assert.True(plan.Succeeded);
        Assert.False(plan.Applied);
        Assert.Empty(service.GetSources());
        var applied = service.ApplySourcePlan(plan.PlanId!);
        Assert.True(applied.Applied);
        Assert.Single(service.GetSources());
        var undone = service.RevertSourcePlan(plan.PlanId!);
        Assert.True(undone.Reverted);
        Assert.Empty(service.GetSources());
        var redone = service.RedoSourcePlan(plan.PlanId!);
        Assert.True(redone.Applied);
        Assert.False(redone.Reverted);
        Assert.Equal(EventSource, Assert.Single(service.GetSources()).Source);
    }

    [Fact]
    public void Typed_event_assignment_previews_applies_undoes_redoes_and_keeps_uuid_diagnostics()
    {
        var service = new ForgeScriptInteropService(new NativeForgeScriptRuntime());
        Assert.True(service.CreateOrUpdateSource(new(Guid.NewGuid(), 0,
            "scripts/events.as", EventSource, "mcp:test", Guid.NewGuid(), true)).Succeeded);
        var plan = service.PlanEventBinding(new(Guid.NewGuid(), service.Revision, "entity:warden",
            "creature.combat", "creature.damaged", "scripts/events.as", "OnDamaged", "int", 90,
            "is_enraged", ForgeScriptEventAuthority.Server));

        Assert.True(plan.Succeeded);
        Assert.Empty(service.GetBindings());
        Assert.True(service.ApplyEventBindingPlan(plan.PlanId!).Applied);
        Assert.Single(service.GetBindings());
        Assert.True(service.RevertEventBindingPlan(plan.PlanId!).Reverted);
        Assert.Empty(service.GetBindings());
        Assert.True(service.RedoEventBindingPlan(plan.PlanId!).Applied);
        Assert.Single(service.GetBindings());

        var invalid = service.PlanEventBinding(new(Guid.NewGuid(), service.Revision, "entity:warden",
            "creature.combat", "creature.damaged", "scripts/events.as", "MissingHandler", "int", 90,
            null, ForgeScriptEventAuthority.Server));
        var diagnostic = Assert.Single(invalid.Diagnostics, item => item.Code == "script.binding.entry_missing");
        Assert.StartsWith("script/eventBindings/0/handler", diagnostic.Path, StringComparison.Ordinal);
        Assert.NotNull(diagnostic.SubjectUuid);
        Assert.Equal("error", diagnostic.Severity);
    }

    [Fact]
    public void Snapshot_round_trip_restores_exact_sources_bindings_profile_and_revision()
    {
        var profile = ForgeScriptInteropProfile.Default with { Provenance = "test:r101" };
        var service = new ForgeScriptInteropService(new NativeForgeScriptRuntime(), profile);
        Assert.True(service.CreateOrUpdateSource(new(Guid.NewGuid(), 0,
            "scripts/events.as", EventSource, "mcp:test", Guid.NewGuid(), true)).Succeeded);
        var binding = service.PlanEventBinding(new(Guid.NewGuid(), service.Revision, "entity:warden",
            "creature.combat", "creature.damaged", "scripts/events.as", "OnDamaged", "int", 90,
            null, ForgeScriptEventAuthority.Server));
        Assert.True(service.ApplyEventBindingPlan(binding.PlanId!).Succeeded);

        var json = service.ExportSnapshot();
        var restored = new ForgeScriptInteropService(new NativeForgeScriptRuntime(), profile);
        restored.RestoreSnapshot(json);

        Assert.Equal(service.Revision, restored.Revision);
        Assert.Equal(service.GetSources(), restored.GetSources());
        Assert.Equal(service.GetBindings(), restored.GetBindings());
        Assert.Equal(json, restored.ExportSnapshot());
        Assert.Throws<InvalidOperationException>(() => restored.RestoreSnapshot(json));
    }

    [Fact]
    public void Abusive_test_is_quota_aborted_and_following_nominal_test_recovers()
    {
        var service = new ForgeScriptInteropService(new NativeForgeScriptRuntime());
        Assert.True(service.CreateOrUpdateSource(new(Guid.NewGuid(), 0, "scripts/abuse.as",
            "void main() { while (true) { int value = 1; } }", "mcp:test")).Succeeded);
        var abusive = service.RunTests("scripts/abuse.as", new ForgeScriptQuotas(10, 1000, 32, 8));
        Assert.Equal("quotaexceeded", abusive.Status);

        Assert.True(service.CreateOrUpdateSource(new(Guid.NewGuid(), service.Revision, "scripts/healthy.as",
            "void main() { say(\"recovered\"); }", "mcp:test")).Succeeded);
        var recovered = service.RunTests("scripts/healthy.as");

        Assert.True(recovered.Succeeded);
        Assert.Contains(recovered.Events, item => item.Kind == "say" && item.Value == "recovered");
    }
}
