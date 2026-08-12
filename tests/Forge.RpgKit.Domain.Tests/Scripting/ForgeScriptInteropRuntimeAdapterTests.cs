using Forge.RpgKit.Application.Scripting;
using Forge.Scripting;

namespace Forge.RpgKit.Domain.Tests.Scripting;

public sealed class ForgeScriptInteropRuntimeAdapterTests
{
    private static readonly ForgeScriptSourceDocument Document = new(
        Guid.Parse("c7110000-0000-4000-8000-000000000201"), 1, "test:r101",
        "scripts/interop.as", "void main() { say(\"interop\"); }", "1.0.0",
        ForgeScriptSourceDistributionPolicy.SourceAndBytecode, true, [], []);

    [Fact]
    public void Adapter_resolves_optional_required_and_rejects_stale_handles()
    {
        var adapter = new ForgeScriptInteropRuntimeAdapter();

        Assert.True(adapter.Resolve(ForgeScriptInteropProfile.Default, Document, true).Available);
        Assert.True(adapter.Resolve(null, null, false).OptionalFallback);
        Assert.Equal(ForgeScriptInteropRuntimeAdapter.RequiredMissingCode,
            adapter.Resolve(null, Document, true).DiagnosticCode);
        Assert.Throws<InvalidOperationException>(() => adapter.Build(new(Guid.NewGuid(), Document.Id),
            ForgeScriptInteropProfile.Default, Document));
    }

    [Fact]
    public void Adapter_executes_nominal_aborts_abuse_and_recovers_in_native_sandbox()
    {
        var adapter = new ForgeScriptInteropRuntimeAdapter();
        var profile = ForgeScriptInteropProfile.Default;
        var handle = adapter.Resolve(profile, Document, true).Handle!;
        var state = adapter.Build(handle, profile, Document);
        var nominal = adapter.Execute(handle, state, new NativeForgeScriptRuntime());
        var abusiveDocument = Document with
        {
            Id = Guid.NewGuid(),
            Source = "void main() { while (true) { int value = 1; } }",
        };
        var abusiveHandle = adapter.Resolve(profile, abusiveDocument, true).Handle!;
        var abusive = adapter.Execute(abusiveHandle, adapter.Build(abusiveHandle, profile, abusiveDocument),
            new NativeForgeScriptRuntime(), new ForgeScriptQuotas(10, 1000, 32, 8));
        var recovered = adapter.Execute(handle, state, new NativeForgeScriptRuntime());

        Assert.Equal(ForgeScriptExecutionStatus.Completed, nominal.Status);
        Assert.Contains(nominal.Events, item => item.Kind == "say" && item.Value == "interop");
        Assert.Equal(ForgeScriptExecutionStatus.QuotaExceeded, abusive.Status);
        Assert.Equal(ForgeScriptExecutionStatus.Completed, recovered.Status);
    }
}
