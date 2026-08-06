using Forge.Domain.Assets;
using Forge.Domain.Assistant;

namespace Forge.Domain.Tests.Assets;

public sealed class ExternalWorkerSecurityPolicyTests
{
    [Fact]
    public void DefaultPolicyDeniesArbitraryExecutionAndAllowsOnlyTypedOperations()
    {
        var policy = ExternalWorkerSecurityPolicy.CreateDefault();

        Assert.Empty(ExternalWorkerSecurityPolicyValidator.Validate(policy));
        Assert.True(policy.DenyFreeFormPython);
        Assert.True(policy.DenyShell);
        Assert.True(policy.DenyEval);
        Assert.True(policy.RequireContainedPaths);
        Assert.False(policy.NetworkAllowed);
        Assert.Contains(AssetExternalOperation.ValidateModel, policy.AllowedBlenderOperations);
        Assert.Contains(AssetExternalOperation.ExportGlb, policy.AllowedBlenderOperations);
        Assert.Equal(AssistantPermissionLevel.Configure, policy.RequiredPermission);
    }

    [Fact]
    public void UnsafeExecutionNetworkAndUnboundedResourcesProduceLocalizedDiagnostics()
    {
        var policy = ExternalWorkerSecurityPolicy.CreateDefault() with
        {
            DenyFreeFormPython = false,
            DenyShell = false,
            DenyEval = false,
            RequireContainedPaths = false,
            NetworkAllowed = true,
            MaximumJobSeconds = 901,
            MaximumOutputBytes = 42,
        };

        var diagnostics = ExternalWorkerSecurityPolicyValidator.Validate(policy);

        Assert.Contains(diagnostics, item => item.Code == ExternalWorkerSecurityPolicyValidator.UnsafeExecutionCode && item.Path.EndsWith("/execution", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == ExternalWorkerSecurityPolicyValidator.NetworkForbiddenCode && item.Path.EndsWith("/networkAllowed", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == ExternalWorkerSecurityPolicyValidator.TimeoutInvalidCode && item.Path.EndsWith("/maximumJobSeconds", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == ExternalWorkerSecurityPolicyValidator.OutputLimitInvalidCode && item.Path.EndsWith("/maximumOutputBytes", StringComparison.Ordinal));
    }

    [Fact]
    public void DuplicateUnsupportedOrMissingCoreOperationsAreRejected()
    {
        var policy = ExternalWorkerSecurityPolicy.CreateDefault() with
        {
            AllowedBlenderOperations =
            [
                AssetExternalOperation.ValidateModel,
                AssetExternalOperation.ValidateModel,
                AssetExternalOperation.EditAudio,
            ],
        };

        var diagnostics = ExternalWorkerSecurityPolicyValidator.Validate(policy);

        Assert.Contains(diagnostics, item => item.Code == ExternalWorkerSecurityPolicyValidator.OperationInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == ExternalWorkerSecurityPolicyValidator.RequiredOperationMissingCode);
    }
}
