using Forge.Domain.Assistant;

namespace Forge.Domain.Tests.Assistant;

public sealed class AssistantWorkspaceProfileTests
{
    [Fact]
    public void DefaultProfileIsOfflineGovernedAndValid()
    {
        var profile = AssistantWorkspaceProfile.CreateDefault();

        Assert.Empty(AssistantWorkspaceProfileValidator.Validate(profile));
        Assert.Equal(AssistantProviderKind.DeterministicOffline, profile.ActiveProvider.Kind);
        Assert.False(profile.NetworkAllowed);
        Assert.False(profile.ShareProjectContent);
        Assert.Equal(AssistantPermissionLevel.Configure, profile.MaximumPermission);
        Assert.Single(profile.Prompts);
        Assert.Single(profile.Prompts[0].Tests);
    }

    [Fact]
    public void OpenAiCompatibleAndOllamaProvidersShareTheCanonicalContract()
    {
        var baseline = AssistantWorkspaceProfile.CreateDefault();
        var openAiCompatible = Provider(
            Guid.Parse("a1210000-0000-4000-8000-000000000001"),
            "lm-studio",
            AssistantProviderKind.OpenAiCompatible,
            "http://127.0.0.1:1234/v1");
        var ollama = Provider(
            Guid.Parse("a1210000-0000-4000-8000-000000000002"),
            "ollama",
            AssistantProviderKind.Ollama,
            "http://localhost:11434");
        var profile = new AssistantWorkspaceProfile(
            baseline.Id,
            baseline.SchemaVersion,
            openAiCompatible.Id,
            [openAiCompatible, ollama],
            baseline.Prompts,
            baseline.MaximumPermission,
            true,
            false,
            "test:g12-a");

        Assert.Empty(AssistantWorkspaceProfileValidator.Validate(profile));
        Assert.Equal("lm-studio", profile.ActiveProvider.Key);
    }

    [Fact]
    public void InvalidNetworkPolicyAndDuplicateProvidersReturnStableDiagnostics()
    {
        var baseline = AssistantWorkspaceProfile.CreateDefault();
        var network = Provider(
            Guid.Parse("a1210000-0000-4000-8000-000000000003"),
            "vllm",
            AssistantProviderKind.OpenAiCompatible,
            "http://127.0.0.1:8000/v1");
        var profile = new AssistantWorkspaceProfile(
            baseline.Id,
            baseline.SchemaVersion,
            network.Id,
            [network, network],
            baseline.Prompts,
            baseline.MaximumPermission,
            false,
            false,
            "test:g12-a");

        var codes = AssistantWorkspaceProfileValidator.Validate(profile).Select(item => item.Code).ToArray();

        Assert.Contains(AssistantWorkspaceProfileValidator.ProviderInvalidCode, codes);
        Assert.Contains(AssistantWorkspaceProfileValidator.NetworkPolicyInvalidCode, codes);
    }

    [Fact]
    public void AgentWorkflowDefinesAllStructuredSurfacesAndSemanticTools()
    {
        var workflow = AgentSemanticWorkflow.CreateDefault();

        Assert.Empty(AgentSemanticWorkflowValidator.Validate(workflow));
        Assert.Equal(Enum.GetValues<AgentContextResourceKind>(), workflow.ContextResources);
        Assert.Equal(5, workflow.Tools.Count);
        Assert.True(workflow.References.IncludeReverseReferences);
        Assert.True(workflow.DryRun.ProhibitWrites);
        Assert.Equal(AssistantPermissionLevel.ApplyApprovedPlan, workflow.MaximumPermission);
    }

    [Fact]
    public void StructuredChangePlanRequiresAllSectionsKnownToolsAndSufficientPermission()
    {
        var workflow = AgentSemanticWorkflow.CreateDefault();
        var target = Guid.Parse("a2000000-0000-4000-8000-000000000901");
        var valid = new StructuredChangePlan(
            Guid.Parse("a2000000-0000-4000-8000-000000000902"),
            Guid.Parse("a2000000-0000-4000-8000-000000000903"), 1, "test:r020", 0,
            AssistantPermissionLevel.ApplyApprovedPlan, "Place an encounter",
            [new AgentSemanticCommand(Guid.Parse("a2000000-0000-4000-8000-000000000904"),
                "area.add_entity", target, new Dictionary<string, string> { ["blueprint"] = "warden" })],
            ["area is loaded"], [target], ["collision"], ["validate area"]);

        Assert.Empty(AgentSemanticWorkflowValidator.ValidatePlan(valid, workflow));

        var invalid = valid with
        {
            Commands = [valid.Commands[0] with { ToolName = "shell.execute" }],
            Assumptions = [],
            Permission = AssistantPermissionLevel.ReadOnly,
        };
        var codes = AgentSemanticWorkflowValidator.ValidatePlan(invalid, workflow).Select(item => item.Code).ToArray();
        Assert.Contains(AgentSemanticWorkflowValidator.ChangePlanInvalidCode, codes);
        Assert.Contains(AgentSemanticWorkflowValidator.ChangePlanToolUnknownCode, codes);
    }

    [Fact]
    public void AgentGovernanceBoundsAuthorizationBudgetPrivacyAndAutonomy()
    {
        var workflow = AgentSemanticWorkflow.CreateDefault();
        var target = Guid.Parse("a2100000-0000-4000-8000-000000000901");
        var plan = new StructuredChangePlan(
            Guid.Parse("a2100000-0000-4000-8000-000000000902"),
            Guid.Parse("a2100000-0000-4000-8000-000000000903"), 1, "test:r021", 0,
            AssistantPermissionLevel.ApplyApprovedPlan, "Set state",
            [new AgentSemanticCommand(Guid.Parse("a2100000-0000-4000-8000-000000000904"),
                "project.set_variable", target, new Dictionary<string, string> { ["key"] = "campaign.chapter" })],
            ["project loaded"], [target], ["migration"], ["validate project"]);
        AgentExecutionAuthorizationRequest Request(bool confirmed, string provider = "offline-ci", int input = 10) =>
            new(provider, false, confirmed, input, 10, 0, 1, ["module/variables/campaign.chapter"]);

        Assert.Empty(AgentExecutionGovernanceValidator.Validate(workflow.EffectiveGovernance, workflow));
        Assert.True(AgentExecutionGovernanceValidator.Authorize(workflow, plan, Request(true)).Succeeded);
        Assert.Contains(AgentExecutionGovernanceValidator.Authorize(workflow, plan, Request(false)).Diagnostics,
            item => item.Code == AgentExecutionGovernanceValidator.PermissionDeniedCode);
        Assert.Contains(AgentExecutionGovernanceValidator.Authorize(workflow, plan, Request(true, input: 5000)).Diagnostics,
            item => item.Code == AgentExecutionGovernanceValidator.BudgetExceededCode);
        Assert.Contains(AgentExecutionGovernanceValidator.Authorize(workflow, plan, Request(true, "openai-compatible")).Diagnostics,
            item => item.Code == AgentExecutionGovernanceValidator.PrivacyDeniedCode);

        var autonomous = workflow with { MaximumPermission = AssistantPermissionLevel.BoundedAutonomous };
        var autonomousPlan = plan with { Permission = AssistantPermissionLevel.BoundedAutonomous };
        Assert.Contains(AgentExecutionGovernanceValidator.Authorize(autonomous, autonomousPlan, Request(false)).Diagnostics,
            item => item.Code == AgentExecutionGovernanceValidator.PermissionDeniedCode);
    }

    private static AssistantProviderDefinition Provider(
        Guid id,
        string key,
        AssistantProviderKind kind,
        string endpoint) =>
        new(id, 1, key, key, kind, endpoint, "local-model", string.Empty, true, true,
            8192, 2048, 0, 0, "test:g12-a");
}
