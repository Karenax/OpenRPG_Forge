namespace Forge.Scripting.Tests;

public sealed class ForgeScriptR014ContractTests
{
    private readonly NativeForgeScriptRuntime _runtime = new();

    [Fact]
    public void Opaque_handles_validate_kind_generation_and_stale_reuse()
    {
        var registry = new ForgeScriptHandleRegistry();
        var entityId = Guid.Parse("14000000-0000-4000-8000-000000000101");
        var entity = registry.Register(ForgeScriptHandleKind.Entity, entityId);

        Assert.True(registry.TryResolve(entity, ForgeScriptHandleKind.Entity, out var resolved, out var validDiagnostic));
        Assert.Equal(entityId, resolved);
        Assert.Null(validDiagnostic);
        Assert.False(registry.TryResolve(entity, ForgeScriptHandleKind.Area, out _, out var typeDiagnostic));
        Assert.Equal("script.handle.type_invalid", typeDiagnostic?.Code);

        var released = registry.Release(entity, ForgeScriptHandleKind.Entity);
        Assert.Equal("script.handle.released", released?.Code);
        Assert.False(registry.TryResolve(entity, ForgeScriptHandleKind.Entity, out _, out var staleDiagnostic));
        Assert.Equal("script.handle.stale", staleDiagnostic?.Code);

        var replacement = registry.Register(ForgeScriptHandleKind.Entity, Guid.NewGuid());
        Assert.Equal(entity.Slot, replacement.Slot);
        Assert.True(replacement.Generation > entity.Generation);
    }

    [Fact]
    public void Execution_policy_previews_applies_undoes_redoes_and_roundtrips_exactly()
    {
        var initial = ForgeScriptExecutionPolicy.Default;
        var history = new ForgeScriptExecutionPolicyHistory(initial);
        var strict = initial with
        {
            HandlerInstructionLimit = 5_000,
            TickInstructionLimit = 25_000,
            ContextMemoryLimitBytes = 4 * 1024 * 1024,
            DeterministicSeed = 14_014
        };
        var command = new ForgeScriptExecutionPolicyCommand(Guid.NewGuid(), 0, strict, "R014 strict preview");

        Assert.True(history.Preview(command).Succeeded);
        Assert.Equal(initial, history.Policy);
        Assert.True(history.Apply(command).Succeeded);
        Assert.Equal(strict, history.Policy);
        Assert.Equal(initial, history.Undo().Policy);
        Assert.Equal(strict, history.Redo().Policy);

        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-r014", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ForgeScriptExecutionPolicyStore(root);
            store.Save(strict);
            Assert.Equal(strict, store.Load());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Api_negotiation_is_semantic_capability_aware_and_documents_migration()
    {
        Assert.Equal(new ForgeScriptSemanticVersion(1, 0, 0), ForgeScriptSemanticVersion.Parse("1.0"));
        var accepted = ForgeScriptApiCompatibility.Negotiate("1.0", new HashSet<string> { "determinism", "opaque-handles" });
        var rejected = ForgeScriptApiCompatibility.Negotiate("2.0.0", new HashSet<string> { "missing" });

        Assert.True(accepted.Succeeded);
        Assert.Equal("1.0.0", accepted.Selected);
        Assert.Contains(accepted.Deprecations, item => item.Symbol == "variable" && item.Replacement == "local_set_string");
        Assert.False(rejected.Succeeded);
        Assert.Equal("script.api.version_incompatible", rejected.DiagnosticCode);
        Assert.Contains("missing", rejected.MissingCapabilities);
        Assert.Contains(ForgeScriptApiCompatibility.Migrations, item => item.From == "1.0" && item.To == "1.0.0");
    }

    [Fact]
    public void Native_handles_rng_clock_and_telemetry_are_typed_and_deterministic()
    {
        var artifact = Compile("""
            void main() {
                AssetId asset = resolve_asset("ui/quest-marker");
                EffectHandle effect = create_effect("ash-burn");
                say(format_int(random_int(1, 1000000)));
                say(format_int(simulation_tick()));
                say(format_int(simulation_time_ms()));
                release_effect(effect);
            }
            """);
        var quotas = new ForgeScriptQuotas(deterministicSeed: 14014, simulationTick: 42,
            simulationTimeMilliseconds: 7000);

        var first = _runtime.Execute(artifact, quotas);
        var second = _runtime.Execute(artifact, quotas);
        var changed = _runtime.Execute(artifact, new ForgeScriptQuotas(deterministicSeed: 14015,
            simulationTick: 42, simulationTimeMilliseconds: 7000));

        Assert.Equal(ForgeScriptExecutionStatus.Completed, first.Status);
        Assert.Equal(first.Events, second.Events);
        Assert.NotEqual(first.Events.Where(item => item.Kind == "say").First().Value,
            changed.Events.Where(item => item.Kind == "say").First().Value);
        Assert.Contains(first.Events, item => item.Kind == "say" && item.Value == "42");
        Assert.Contains(first.Events, item => item.Kind == "say" && item.Value == "7000");
        Assert.Contains(first.Events, item => item.Kind == "asset.resolved");
        Assert.Contains(first.Events, item => item.Kind == "effect.released");
        Assert.True(first.Telemetry?.Instructions > 0);
        Assert.True(first.Telemetry?.PeakMemoryBytes > 0);
        Assert.Equal((ulong)14014, first.Telemetry?.DeterministicSeed);
    }

    [Fact]
    public void Native_memory_budget_fails_safe_and_the_next_handler_recovers()
    {
        var rejected = _runtime.Execute(Compile("void main() { say(\"memory\"); }"),
            new ForgeScriptQuotas(memoryLimitBytes: 65_536));
        var healthy = _runtime.Execute(Compile("void main() { say(\"healthy-after-memory\"); }"));

        Assert.Equal(ForgeScriptExecutionStatus.QuotaExceeded, rejected.Status);
        Assert.Contains(rejected.Diagnostics, item => item.Message.Contains("memory_limit", StringComparison.Ordinal));
        Assert.Equal(ForgeScriptExecutionStatus.Completed, healthy.Status);
        Assert.Equal("healthy-after-memory", Assert.Single(healthy.Events).Value);
    }

    [Fact]
    public async Task Native_cancellation_and_aggregate_budgets_fail_safe()
    {
        var endless = Compile("void main() { while (true) { int value = 1; } }");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        var cancelled = await _runtime.ExecuteAsync(endless,
            new ForgeScriptQuotas(instructionLimit: int.MaxValue, timeoutMilliseconds: 5000), cancellation.Token);

        Assert.Equal(ForgeScriptExecutionStatus.Cancelled, cancelled.Status);
        Assert.Contains(cancelled.Diagnostics, item => item.Message == "script.cancelled");

        var policy = ForgeScriptExecutionPolicy.Default with
        {
            HandlerInstructionLimit = 1,
            TickInstructionLimit = 1,
            ModuleInstructionLimit = 10,
        };
        var session = new ForgeScriptExecutionSession(_runtime, policy);
        _ = await session.ExecuteHandlerAsync(Compile("void main() { say(\"bounded\"); }"), "OnUse");
        var aggregate = await session.ExecuteHandlerAsync(Compile("void main() {}"), "OnUseAgain");

        Assert.Equal(ForgeScriptExecutionStatus.QuotaExceeded, aggregate.Status);
        Assert.Contains(aggregate.Diagnostics, item => item.Message == "quota.aggregate_instruction_limit_exceeded");
        Assert.Equal(1, session.Snapshot.HandlersRejected);
    }

    private ForgeScriptArtifact Compile(string source)
    {
        var result = _runtime.Compile(source);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Message)));
        return Assert.IsType<ForgeScriptArtifact>(result.Artifact);
    }
}
