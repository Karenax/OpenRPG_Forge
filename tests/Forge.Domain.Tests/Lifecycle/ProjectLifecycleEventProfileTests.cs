using Forge.Domain.Lifecycle;
using Forge.Domain.Modules;

namespace Forge.Domain.Tests.Lifecycle;

public sealed class ProjectLifecycleEventProfileTests
{
    private static readonly ModuleId ModuleId =
        Forge.Domain.Modules.ModuleId.From(Guid.Parse("a1520000-0000-4000-8000-000000000001"));

    [Fact]
    public void Ux0FDefaultProfileCoversBothEventsInEveryRuntimeContextDeterministically()
    {
        var first = ProjectLifecycleEventProfile.CreateDefault(ModuleId);
        var second = ProjectLifecycleEventProfile.CreateDefault(ModuleId);

        Assert.Empty(ProjectLifecycleEventProfileValidator.Validate(first, ModuleId.Value));
        Assert.Equal(8, first.Bindings.Count);
        Assert.Equal(first.Bindings.Select(binding => binding.Id), second.Bindings.Select(binding => binding.Id));
        Assert.Equal(
            new[] { RuntimeLifecycleEventKind.ProjectLoaded, RuntimeLifecycleEventKind.WorldStarted },
            first.Bindings.Select(binding => binding.Event).Distinct());
        Assert.Equal(Enum.GetValues<RuntimeAuthority>(),
            first.Bindings.Where(binding => binding.Event == RuntimeLifecycleEventKind.ProjectLoaded)
                .Select(binding => binding.Authority));
        Assert.All(first.Bindings, binding =>
        {
            Assert.Null(binding.ScriptPath);
            Assert.False(binding.RequiredAtRuntime);
        });
    }

    [Fact]
    public void Ux0FResolverSelectsTheScriptForTheExactEventAndRuntimeContext()
    {
        var source = ProjectLifecycleEventProfile.CreateDefault(ModuleId);
        var bindings = source.Bindings.Select(binding =>
            binding.Event == RuntimeLifecycleEventKind.ProjectLoaded && binding.Authority == RuntimeAuthority.Server
                ? binding with
                {
                    Handler = "OnServerProjectReady",
                    ScriptPath = "scripts/server-project-ready.forge",
                    RequiredAtRuntime = true,
                    Provenance = "test:ux0-f",
                }
                : binding).ToArray();
        var profile = new ProjectLifecycleEventProfile(source.Id, source.SchemaVersion, "test:ux0-f",
            true, false, bindings);

        var resolution = ProjectLifecycleEventProfileRuntime.Resolve(profile,
            RuntimeLifecycleEventKind.ProjectLoaded, RuntimeAuthority.Server, required: true);

        Assert.True(resolution.Available);
        Assert.Equal("scripts/server-project-ready.forge", resolution.Binding!.ScriptPath);
        Assert.Equal("OnServerProjectReady", resolution.Binding.Handler);
        Assert.True(resolution.Binding.RequiredAtRuntime);
        Assert.Null(ProjectLifecycleEventProfileRuntime.Resolve(profile,
            RuntimeLifecycleEventKind.WorldStarted, RuntimeAuthority.Client, required: false).Binding!.ScriptPath);
    }

    [Fact]
    public void Ux0FValidatorReportsStableDiagnosticsForUnsafeOrIncompleteProfiles()
    {
        var source = ProjectLifecycleEventProfile.CreateDefault(ModuleId);
        var invalidBinding = source.Bindings[0] with
        {
            Id = Guid.Empty,
            SchemaVersion = 2,
            Provenance = string.Empty,
            ForgeScriptApiVersion = "latest",
            Handler = "bad handler!",
            ScriptPath = "../outside.csx",
            RequiredAtRuntime = true,
        };
        var invalid = new ProjectLifecycleEventProfile(Guid.Empty, 2, string.Empty,
            false, true, [invalidBinding]);

        var diagnostics = ProjectLifecycleEventProfileValidator.Validate(invalid, ModuleId.Value);

        Assert.Contains(diagnostics, item => item.Code == ProjectLifecycleEventProfileValidator.IdentityInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == ProjectLifecycleEventProfileValidator.SchemaUnsupportedCode);
        Assert.Contains(diagnostics, item => item.Code == ProjectLifecycleEventProfileValidator.BindingSetInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == ProjectLifecycleEventProfileValidator.BindingIdentityInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == ProjectLifecycleEventProfileValidator.BindingContractInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == ProjectLifecycleEventProfileValidator.RequiredDisabledCode);
        Assert.All(diagnostics, item => Assert.StartsWith("module/runtimePolicies/lifecycleEvents", item.Path));
    }

    [Theory]
    [InlineData("scripts/../outside.forge")]
    [InlineData("scripts//empty-segment.forge")]
    [InlineData("scripts/C:/drive-path.forge")]
    [InlineData("other/not-a-script.forge")]
    [InlineData("scripts/not-forgescript.cs")]
    public void Ux0FValidatorRejectsNonPortableOrEscapingScriptPaths(string scriptPath)
    {
        var source = ProjectLifecycleEventProfile.CreateDefault(ModuleId);
        var bindings = source.Bindings.Select((binding, index) => index == 0
            ? binding with { ScriptPath = scriptPath }
            : binding).ToArray();
        var profile = new ProjectLifecycleEventProfile(source.Id, source.SchemaVersion, "test:ux0-f",
            true, false, bindings);

        Assert.Contains(ProjectLifecycleEventProfileValidator.Validate(profile),
            item => item.Code == ProjectLifecycleEventProfileValidator.BindingContractInvalidCode);
    }
}
