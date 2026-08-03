using Forge.Mcp.Scripts;
using Forge.Scripting;

namespace Forge.Mcp.Tests.Scripts;

public sealed class ForgeScriptInteropServiceTests
{
    [Fact]
    public void Interop_exposes_api_and_transactional_source_compile_diagnostics_tests_and_binding_preview()
    {
        var service = new ForgeScriptInteropService(new NativeForgeScriptRuntime()); Assert.Equal("1.0.0", service.GetApi().ApiVersion); var created = service.CreateOrUpdateSource(new(Guid.NewGuid(), 0, "scripts/greet.as", "void main() { say(\"hello\"); }", "mcp:test")); Assert.True(created.Succeeded); Assert.Equal(1, service.Revision); Assert.True(service.Compile("scripts/greet.as").Succeeded); Assert.Empty(service.GetDiagnostics("scripts/greet.as")); var tests = service.RunTests("scripts/greet.as"); Assert.True(tests.Succeeded); Assert.Contains(tests.Events, item => item.Kind == "say"); var preview = service.PreviewBinding("entity:hero", "OnUse", "scripts/greet.as"); Assert.True(preview.Resolved); Assert.Equal("bind:entity:hero.OnUse->scripts/greet.as", preview.SemanticDiff);
    }

    [Fact]
    public void Interop_rejects_stale_or_invalid_mutations_without_changing_revision()
    {
        var service = new ForgeScriptInteropService(new NativeForgeScriptRuntime()); Assert.True(service.CreateOrUpdateSource(new(Guid.NewGuid(), 0, "scripts/greet.as", "void main() {}", "editor:test")).Succeeded); var stale = service.CreateOrUpdateSource(new(Guid.NewGuid(), 0, "scripts/greet.as", "void main() { say(\"stale\"); }", "mcp:test")); Assert.False(stale.Succeeded); Assert.Equal(1, service.Revision); var invalid = service.CreateOrUpdateSource(new(Guid.NewGuid(), 1, "scripts/broken.as", "void main( {", "mcp:test")); Assert.False(invalid.Succeeded); Assert.NotEmpty(invalid.Diagnostics); Assert.Equal(1, service.Revision); Assert.False(service.PreviewBinding("entity", "OnUse", "scripts/missing.as").Resolved);
    }
}
