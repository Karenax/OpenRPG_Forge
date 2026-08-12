using Forge.Domain.Lifecycle;

namespace Forge.Domain.Tests.Lifecycle;

public sealed class ProjectGameplayEventProfileTests
{
    [Fact]
    public void R051DefaultProfileHasCanonicalBindingsAndSafeRuntimeHandle()
    {
        var moduleId = Guid.Parse("a1510000-0000-4000-8000-000000000001");
        var profile = ProjectGameplayEventProfile.CreateDefault(moduleId);

        Assert.Empty(ProjectGameplayEventProfileValidator.Validate(profile, moduleId));
        Assert.Equal(ProjectGameplayEventProfile.RequiredEventNames, profile.Bindings.Select(item => item.EventName));
        Assert.All(profile.Bindings, binding => Assert.Equal(EventAuthorityScope.Server, binding.Authority));
        var resolution = ProjectGameplayEventProfileRuntime.Resolve(profile, true);
        Assert.True(resolution.Available);
        Assert.Equal(14, resolution.Handle!.Bindings.Count);
        Assert.Null(ProjectGameplayEventProfileRuntime.Resolve(null, false).Handle);
        Assert.Equal(ProjectGameplayEventProfileValidator.MissingRequiredCode,
            Assert.Single(ProjectGameplayEventProfileRuntime.Resolve(null, true).Diagnostics).Code);
    }

    [Fact]
    public void R051ValidatorReportsStableLocalizedDiagnostics()
    {
        var baseline = ProjectGameplayEventProfile.CreateDefault(Guid.NewGuid());
        var invalidBinding = baseline.Bindings[0] with
        {
            Id = Guid.Empty,
            SchemaVersion = 2,
            Provenance = string.Empty,
            ForgeScriptApiVersion = "latest",
            Handler = "bad handler!",
            Authority = EventAuthorityScope.Local,
        };
        var invalid = new ProjectGameplayEventProfile(
            Guid.Empty,
            2,
            string.Empty,
            false,
            true,
            true,
            true,
            true,
            [invalidBinding]);

        var diagnostics = ProjectGameplayEventProfileValidator.Validate(invalid, Guid.NewGuid());

        Assert.Contains(diagnostics, item => item.Code == ProjectGameplayEventProfileValidator.IdentityInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == ProjectGameplayEventProfileValidator.SchemaUnsupportedCode);
        Assert.Contains(diagnostics, item => item.Code == ProjectGameplayEventProfileValidator.BindingSetInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == ProjectGameplayEventProfileValidator.BindingIdentityInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == ProjectGameplayEventProfileValidator.BindingContractInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == ProjectGameplayEventProfileValidator.RequiredDisabledCode);
        Assert.All(diagnostics, item => Assert.StartsWith("module/runtimePolicies/gameplayEvents", item.Path));
    }
}
