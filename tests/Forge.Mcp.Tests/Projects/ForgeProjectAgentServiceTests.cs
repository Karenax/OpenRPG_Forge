using System.Text.Json;
using Forge.Domain.Dialogues;
using Forge.Mcp.Dialogues;
using Forge.Mcp.Projects;
using Forge.Mcp.Scripts;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Mcp.Tests.Projects;

public sealed class ForgeProjectAgentServiceTests
{
    [Fact]
    public void SummaryRevisionAndDiagnosticsAreReadOnlyAndStructured()
    {
        using var workspace = new TestWorkspace();

        var summary = workspace.Project.GetSummary();

        Assert.Equal("crypt-of-ashes", summary.ModuleSlug);
        Assert.Equal(0, summary.Revision);
        Assert.True(summary.EntityCount >= 16);
        Assert.Empty(workspace.Project.GetDiagnostics());
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    [Fact]
    public void DialoguePlanSchemaIsValidJsonSchema()
    {
        using var document = JsonDocument.Parse(DialoguePlanSchema.Json);

        Assert.Equal(
            "OpenRPG Forge dialogue mutation plan",
            document.RootElement.GetProperty("title").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("properties").GetProperty("schemaVersion").GetProperty("const").GetInt32());
    }

    [Fact]
    public void DialoguePlanSupportsDryRunDiffApplyInverseAndAudit()
    {
        using var workspace = new TestWorkspace();
        var request = CreatePlan(workspace.RootDirectory);

        var planned = workspace.Project.PlanDialogue(request);

        Assert.True(planned.Succeeded);
        Assert.False(planned.Applied);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        Assert.Equal("add", planned.Diff?.Operation);
        Assert.Equal("remove", planned.Inverse?.Operation);
        Assert.Equal("planned", Assert.Single(planned.Audit).Action);

        var applied = workspace.Project.ApplyDialoguePlan(planned.PlanId!);

        Assert.True(applied.Succeeded);
        Assert.True(applied.Applied);
        Assert.Equal(1, workspace.Project.GetCurrentRevision());
        Assert.Equal(["planned", "apply"], applied.Audit.Select(item => item.Action));
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!;
        Assert.Contains(persisted.Project.Dialogues, item => item.Id.ToString() == request.DialogueId);

        var reverted = workspace.Project.RevertDialoguePlan(planned.PlanId!);

        Assert.True(reverted.Succeeded);
        Assert.True(reverted.Reverted);
        Assert.Equal(2, workspace.Project.GetCurrentRevision());
        Assert.Equal(["planned", "apply", "revert"], reverted.Audit.Select(item => item.Action));
        persisted = ProjectSession.Open(workspace.RootDirectory).Session!;
        Assert.DoesNotContain(persisted.Project.Dialogues, item => item.Id.ToString() == request.DialogueId);
    }

    [Fact]
    public void DialoguePlanRejectsInvalidGraphBeforeMutation()
    {
        using var workspace = new TestWorkspace();
        var request = CreatePlan(workspace.RootDirectory);
        var unreachableId = Guid.Parse("d0e3a712-f41e-4695-ac1a-3db62c01d543").ToString("D");
        request = request with
        {
            Nodes =
            [
                .. request.Nodes,
                new DialogueNodeInput(unreachableId, null, "dialogue.unreachable", "Unreachable"),
            ],
        };

        var result = workspace.Project.PlanDialogue(request);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, item => item.Code == DialogueValidator.UnreachableNodeCode);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    [Fact]
    public void ScriptBindingPlanValidatesRevisionEntityBindingAndSafePath()
    {
        using var workspace = new TestWorkspace();
        var session = ProjectSession.Open(workspace.RootDirectory).Session!;
        var area = Assert.Single(session.Project.Areas);
        var entity = area.Entities.First();
        var request = new ScriptBindingPlanRequest(
            1,
            0,
            area.Id.ToString(),
            entity.Id.ToString(),
            "scriptPath",
            "scripts/interaction.forge");

        var planned = workspace.Project.PlanScriptBinding(request);
        var unsafePlan = workspace.Project.PlanScriptBinding(request with { RelativeScriptPath = "../outside.forge" });

        Assert.True(planned.Succeeded);
        Assert.Equal("replace", planned.Diff?.Operation);
        Assert.False(unsafePlan.Succeeded);
        Assert.Equal("script_binding.path_unsafe", Assert.Single(unsafePlan.Diagnostics).Code);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    private static DialoguePlanRequest CreatePlan(string workspaceDirectory)
    {
        var project = ProjectSession.Open(workspaceDirectory).Session!.Project;
        var speaker = project.Blueprints.First().Id.ToString();
        var dialogueId = Guid.Parse("7b02e466-b43a-442b-a833-8613a1c71951").ToString("D");
        var nodeId = Guid.Parse("659eb07b-5d3f-4f87-80de-26ec4b20c837").ToString("D");
        return new DialoguePlanRequest(
            1,
            0,
            dialogueId,
            "mcp-dialogue",
            "MCP dialogue",
            nodeId,
            [new DialogueNodeInput(nodeId, speaker, "dialogue.mcp", "A dry-run dialogue")]);
    }
}
