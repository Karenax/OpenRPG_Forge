using System.Text.Json.Nodes;
using Forge.Domain.Assistant;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Workspaces;

public sealed class AssistantWorkspaceProfileStoreTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(), "openrpg-forge-assistant-tests", Guid.NewGuid().ToString("N"));

    public AssistantWorkspaceProfileStoreTests()
    {
        Directory.CreateDirectory(_temporaryRoot);
    }

    [Fact]
    public void MissingProfileReturnsSafeOfflineDefault()
    {
        var loaded = AssistantWorkspaceProfileStore.Load(_temporaryRoot);

        Assert.True(loaded.Succeeded);
        Assert.True(loaded.FileMissing);
        Assert.Equal(AssistantProviderKind.DeterministicOffline, loaded.Profile.ActiveProvider.Kind);
    }

    [Fact]
    public void SaveReloadAndSaveAgainAreDeterministic()
    {
        var profile = AssistantWorkspaceProfile.CreateDefault();

        var saved = AssistantWorkspaceProfileStore.Save(_temporaryRoot, profile);
        var path = Path.Combine(_temporaryRoot, "assistant", "profile.json");
        var first = File.ReadAllText(path);
        var loaded = AssistantWorkspaceProfileStore.Load(_temporaryRoot);
        var savedAgain = AssistantWorkspaceProfileStore.Save(_temporaryRoot, loaded.Profile);
        var second = File.ReadAllText(path);

        Assert.True(saved.Succeeded);
        Assert.True(savedAgain.Succeeded);
        Assert.True(loaded.Succeeded);
        Assert.False(loaded.FileMissing);
        Assert.Equal(first, second);
        Assert.Equal(profile.Id, loaded.Profile.Id);
        Assert.Equal(profile.ActiveProvider, loaded.Profile.ActiveProvider);
        Assert.Equal(profile.Prompts[0].Key, loaded.Profile.Prompts[0].Key);
        Assert.Equal(profile.Prompts[0].Version, loaded.Profile.Prompts[0].Version);
        Assert.Equal(profile.Prompts[0].Tests[0].Variables, loaded.Profile.Prompts[0].Tests[0].Variables);
        Assert.Equal(profile.AgentWorkflow.Id, loaded.Profile.AgentWorkflow.Id);
        Assert.Equal(profile.AgentWorkflow.ContextResources, loaded.Profile.AgentWorkflow.ContextResources);
        Assert.Equal(profile.AgentWorkflow.Tools, loaded.Profile.AgentWorkflow.Tools);
        Assert.Equal(profile.AgentWorkflow.References, loaded.Profile.AgentWorkflow.References);
        Assert.Equal(profile.AgentWorkflow.EffectiveGovernance.Id, loaded.Profile.AgentWorkflow.EffectiveGovernance.Id);
        Assert.Equal(profile.AgentWorkflow.EffectiveGovernance.Transactions,
            loaded.Profile.AgentWorkflow.EffectiveGovernance.Transactions);
        Assert.Equal(profile.AgentWorkflow.EffectiveGovernance.SemanticDiff.Categories,
            loaded.Profile.AgentWorkflow.EffectiveGovernance.SemanticDiff.Categories);
        Assert.Equal(profile.AgentWorkflow.EffectiveGovernance.Authorization.AllowedTools,
            loaded.Profile.AgentWorkflow.EffectiveGovernance.Authorization.AllowedTools);
        Assert.Equal(profile.AgentWorkflow.EffectiveGovernance.BudgetPrivacy.Providers,
            loaded.Profile.AgentWorkflow.EffectiveGovernance.BudgetPrivacy.Providers);
        Assert.Equal(profile.AgentWorkflow.EffectiveSafety.StructuredOutput,
            loaded.Profile.AgentWorkflow.EffectiveSafety.StructuredOutput);
        Assert.Equal(profile.AgentWorkflow.EffectiveSafety.PromptInjection.SuspiciousInstructionPatterns,
            loaded.Profile.AgentWorkflow.EffectiveSafety.PromptInjection.SuspiciousInstructionPatterns);
        Assert.Equal(profile.AgentWorkflow.EffectiveSafety.Offline,
            loaded.Profile.AgentWorkflow.EffectiveSafety.Offline);
        Assert.Equal(10, loaded.Profile.AgentWorkflow.ContextResources.Count);
        Assert.Equal(5, loaded.Profile.AgentWorkflow.Tools.Count);
    }

    [Fact]
    public void InvalidProfileReturnsStableReadDiagnosticWithoutPartialState()
    {
        var directory = Path.Combine(_temporaryRoot, "assistant");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "profile.json"), "{ invalid }");

        var loaded = AssistantWorkspaceProfileStore.Load(_temporaryRoot);

        Assert.False(loaded.Succeeded);
        Assert.Equal(AssistantWorkspaceProfileStore.ReadFailedCode, Assert.Single(loaded.Diagnostics).Code);
        Assert.Equal(AssistantProviderKind.DeterministicOffline, loaded.Profile.ActiveProvider.Kind);
    }

    [Fact]
    public void LegacyProfileWithoutAgentWorkflowMigratesToSafeDefault()
    {
        Assert.True(AssistantWorkspaceProfileStore.Save(_temporaryRoot, AssistantWorkspaceProfile.CreateDefault()).Succeeded);
        var path = Path.Combine(_temporaryRoot, "assistant", "profile.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        document["profile"]!.AsObject().Remove("agentWorkflow");
        File.WriteAllText(path, document.ToJsonString());

        var loaded = AssistantWorkspaceProfileStore.Load(_temporaryRoot);

        Assert.True(loaded.Succeeded);
        Assert.Equal(AgentSemanticWorkflow.DefaultId, loaded.Profile.AgentWorkflow.Id);
        Assert.Empty(AgentSemanticWorkflowValidator.Validate(loaded.Profile.AgentWorkflow));
    }

    [Fact]
    public void LegacyWorkflowWithoutGovernanceMigratesToSafeDefault()
    {
        Assert.True(AssistantWorkspaceProfileStore.Save(_temporaryRoot, AssistantWorkspaceProfile.CreateDefault()).Succeeded);
        var path = Path.Combine(_temporaryRoot, "assistant", "profile.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        document["profile"]!["agentWorkflow"]!.AsObject().Remove("governance");
        File.WriteAllText(path, document.ToJsonString());

        var loaded = AssistantWorkspaceProfileStore.Load(_temporaryRoot);

        Assert.True(loaded.Succeeded);
        Assert.Equal(AgentExecutionGovernance.DefaultId, loaded.Profile.AgentWorkflow.EffectiveGovernance.Id);
        Assert.False(loaded.Profile.AgentWorkflow.EffectiveGovernance.Authorization.AllowBoundedAutonomy);
        Assert.Empty(AgentExecutionGovernanceValidator.Validate(
            loaded.Profile.AgentWorkflow.EffectiveGovernance, loaded.Profile.AgentWorkflow));
    }

    [Fact]
    public void LegacyWorkflowWithoutSafetyMigratesToSafeDefault()
    {
        Assert.True(AssistantWorkspaceProfileStore.Save(_temporaryRoot, AssistantWorkspaceProfile.CreateDefault()).Succeeded);
        var path = Path.Combine(_temporaryRoot, "assistant", "profile.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        document["profile"]!["agentWorkflow"]!.AsObject().Remove("safety");
        File.WriteAllText(path, document.ToJsonString());

        var loaded = AssistantWorkspaceProfileStore.Load(_temporaryRoot);

        Assert.True(loaded.Succeeded);
        Assert.Equal(AgentResponseSafetyProfile.DefaultId, loaded.Profile.AgentWorkflow.EffectiveSafety.Id);
        Assert.True(loaded.Profile.AgentWorkflow.EffectiveSafety.Offline.BuildRuntimeIndependent);
        Assert.Empty(AgentResponseSafetyProfileValidator.Validate(loaded.Profile.AgentWorkflow.EffectiveSafety));
    }

    [Fact]
    public void AiAuditRoundTripsDeterministicallyAndRejectsInvalidInput()
    {
        var entry = new AgentAiAuditEntry(
            Guid.Parse("a2120000-0000-4000-8000-000000000001"), 1,
            Guid.Parse("a2120000-0000-4000-8000-000000000002"),
            Guid.Parse("a2120000-0000-4000-8000-000000000003"),
            Guid.Parse("a2120000-0000-4000-8000-000000000004"),
            AgentTransactionStage.Previewed, "client", "author", null, null,
            ["project.set_variable"], "input-hash", "output-hash", ["validate project"],
            "test:r021", 0, new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero), true);

        Assert.True(AgentAiAuditStore.Save(_temporaryRoot, [entry]).Succeeded);
        var path = Path.Combine(_temporaryRoot, "assistant", "ai-audit.json");
        var first = File.ReadAllText(path);
        var loaded = AgentAiAuditStore.Load(_temporaryRoot);
        Assert.True(loaded.Succeeded);
        var restored = Assert.Single(loaded.Entries);
        Assert.Equal(entry.Id, restored.Id);
        Assert.Equal(entry.TransactionId, restored.TransactionId);
        Assert.Equal(entry.RollbackId, restored.RollbackId);
        Assert.Equal(entry.Commands, restored.Commands);
        Assert.Equal(entry.Tests, restored.Tests);
        Assert.Equal(entry.OutputHash, restored.OutputHash);
        Assert.True(AgentAiAuditStore.Save(_temporaryRoot, loaded.Entries).Succeeded);
        Assert.Equal(first, File.ReadAllText(path));

        File.WriteAllText(path, "{ invalid }");
        var invalid = AgentAiAuditStore.Load(_temporaryRoot);
        Assert.False(invalid.Succeeded);
        Assert.Equal(AgentAiAuditStore.ReadFailedCode, Assert.Single(invalid.Diagnostics).Code);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot)) Directory.Delete(_temporaryRoot, recursive: true);
    }
}
