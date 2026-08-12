using Forge.RpgKit.Application.Scripting;
using Forge.Scripting;

namespace Forge.RpgKit.Domain.Tests.Scripting;

public sealed class ForgeScriptQualityRuntimeAdapterTests
{
    private static readonly ForgeScriptSourceDocument Document = new(
        Guid.Parse("c7100000-0000-4000-8000-000000000200"), 1, "test:r100", "scripts/quality.as",
        "void main() { say(\"quality\"); }", "1.0.0",
        ForgeScriptSourceDistributionPolicy.SourceAndBytecode, true, [], []);

    [Fact]
    public void Resolution_supports_optional_fallback_required_diagnostic_and_stale_handle_protection()
    {
        var adapter = new ForgeScriptQualityRuntimeAdapter();
        var available = adapter.Resolve(ForgeScriptQualityProfile.Default, Document, true);

        Assert.True(available.Available);
        Assert.True(adapter.Resolve(null, null, false).OptionalFallback);
        Assert.Equal(ForgeScriptQualityRuntimeAdapter.RequiredMissingCode,
            adapter.Resolve(null, Document, true).DiagnosticCode);
        Assert.Throws<InvalidOperationException>(() => adapter.Build(
            new(Guid.NewGuid(), Document.Id), ForgeScriptQualityProfile.Default, Document));
    }

    [Fact]
    public void Runtime_adapter_reuses_quality_services_and_executes_only_in_native_sandbox()
    {
        var adapter = new ForgeScriptQualityRuntimeAdapter();
        var handle = adapter.Resolve(ForgeScriptQualityProfile.Default, Document, true).Handle!;
        var state = adapter.Build(handle, ForgeScriptQualityProfile.Default, Document);
        var tests = adapter.RunTests(handle, state,
        [
            new("nominal", new("fixture", 100, new Dictionary<string, object?> { ["ready"] = true },
                    new Dictionary<string, object?> { ["clock"] = "mock" }),
                _ => { }, world => world.Events.Add("tested"),
                [new("ready", world => (bool)world.State["ready"]!, "not ready")]),
        ]);
        var report = adapter.Profile(handle, state,
            [new("quality.as", "main", 10, 20, TimeSpan.FromMilliseconds(1), ["main"])]);
        var docs = adapter.GenerateDocumentation(handle, state, ForgeScriptApiCatalog.Registrations);
        var diagnostics = adapter.Analyze(handle, state,
            new("scripts/quality.as", "UnsafeQuery();", "server", new HashSet<string>()));
        var execution = adapter.Execute(handle, state, new NativeForgeScriptRuntime(),
            new ForgeScriptQuotas(10_000, 1000, 32, 8));

        Assert.True(Assert.Single(tests).Passed);
        Assert.Equal(10, report.TotalInstructions);
        Assert.Contains("# ForgeScript API 1.0", docs.Markdown);
        Assert.Equal("SCR-LINT-UNSAFE-QUERY", Assert.Single(diagnostics).Code);
        Assert.Equal(ForgeScriptExecutionStatus.Completed, execution.Status);
        Assert.Contains(execution.Events, item => item.Kind == "say");
    }
}
