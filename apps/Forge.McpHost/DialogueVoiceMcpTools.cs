using System.ComponentModel;
using Forge.Mcp.Dialogues;
using Forge.Mcp.Projects;
using ModelContextProtocol.Server;

namespace Forge.McpHost;

[McpServerToolType]
public sealed class DialogueVoiceMcpTools(ForgeProjectAgentService project)
{
    [McpServerTool]
    [Description("Plans a governed local dialogue voice import and cue link without writing files or project data.")]
    public DialogueVoiceImportPlanResult PlanDialogueVoiceImport(DialogueVoiceImportPlanRequest request) =>
        project.PlanDialogueVoiceImport(request);

    [McpServerTool]
    [Description("Applies a previously inspected dialogue voice import plan with revision, hash and confinement guards.")]
    public DialogueVoiceImportPlanResult ApplyDialogueVoiceImportPlan(string planId) =>
        project.ApplyDialogueVoiceImportPlan(planId);

    [McpServerTool]
    [Description("Reverts the most recently applied dialogue voice cue while retaining its immutable content-addressed blob.")]
    public DialogueVoiceImportPlanResult RevertDialogueVoiceImportPlan(string planId) =>
        project.RevertDialogueVoiceImportPlan(planId);
}
