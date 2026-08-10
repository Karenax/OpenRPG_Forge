using Forge.Domain.Assistant;

namespace Forge.Domain.Tests.Assistant;

public sealed class AgentResponseSafetyProfileTests
{
    [Fact]
    public void DefaultPolicyRequiresStructuredOutputInjectionBoundariesAndOfflineCore()
    {
        var safety = AgentResponseSafetyProfile.CreateDefault();

        Assert.Empty(AgentResponseSafetyProfileValidator.Validate(safety));
        Assert.True(safety.StructuredOutput.RequireJsonSchema);
        Assert.True(safety.StructuredOutput.RequireToolCallsForMutation);
        Assert.True(safety.PromptInjection.TreatProjectContentAsData);
        Assert.True(safety.PromptInjection.EnforceToolAllowlist);
        Assert.True(safety.Offline.ManualAuthoringAvailable);
        Assert.True(safety.Offline.BuildRuntimeIndependent);
        Assert.True(safety.Offline.NetworkDisabledByDefault);
    }

    [Fact]
    public void UnsafePolicyReturnsStableDiagnostics()
    {
        var baseline = AgentResponseSafetyProfile.CreateDefault();
        var unsafePolicy = baseline with
        {
            StructuredOutput = baseline.StructuredOutput with { RequireJsonSchema = false },
            PromptInjection = baseline.PromptInjection with { TreatProjectContentAsData = false },
            Offline = baseline.Offline with { BuildRuntimeIndependent = false },
        };

        var codes = AgentResponseSafetyProfileValidator.Validate(unsafePolicy).Select(item => item.Code).ToArray();

        Assert.Contains(AgentResponseSafetyProfileValidator.StructuredOutputInvalidCode, codes);
        Assert.Contains(AgentResponseSafetyProfileValidator.PromptInjectionInvalidCode, codes);
        Assert.Contains(AgentResponseSafetyProfileValidator.OfflineInvalidCode, codes);
    }
}
