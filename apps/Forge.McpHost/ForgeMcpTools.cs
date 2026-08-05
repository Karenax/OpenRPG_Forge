using System.ComponentModel;
using Forge.Mcp;
using Forge.Mcp.Assets;
using Forge.Mcp.Dialogues;
using Forge.Mcp.Projects;
using Forge.Mcp.Scripts;
using ModelContextProtocol.Server;

namespace Forge.McpHost;

[McpServerToolType]
public sealed class ForgeMcpTools
{
    private readonly ForgeProjectAgentService _project;
    private readonly ForgeScriptAgentService _scripts;

    public ForgeMcpTools(ForgeProjectAgentService project, ForgeScriptAgentService scripts)
    {
        _project = project;
        _scripts = scripts;
    }

    [McpServerTool]
    [Description("Returns a read-only summary of the currently opened OpenRPG Forge project.")]
    public ForgeProjectSummary GetProjectSummary() => _project.GetSummary();

    [McpServerTool]
    [Description("Returns the current transactional project revision without mutating the project.")]
    public long GetCurrentRevision() => _project.GetCurrentRevision();

    [McpServerTool]
    [Description("Validates the project and returns structured diagnostics without mutating it.")]
    public IReadOnlyList<AgentDiagnostic> GetProjectDiagnostics() => _project.GetDiagnostics();

    [McpServerTool]
    [Description("Returns the canonical module and selected-area runtime policies without mutating the project.")]
    public RuntimePolicyResource GetRuntimePolicies(string? areaId = null) =>
        _project.GetRuntimePolicies(areaId);

    [McpServerTool]
    [Description("Validates and dry-runs a versioned runtime-policy plan. It returns a diff and inverse without applying changes.")]
    public RuntimePolicyPlanResult PlanRuntimePolicies(RuntimePolicyPlanRequest request) =>
        _project.PlanRuntimePolicies(request);

    [McpServerTool]
    [Description("Applies a previously validated runtime-policy plan if its base revision still matches.")]
    public RuntimePolicyPlanResult ApplyRuntimePolicyPlan(string planId) =>
        _project.ApplyRuntimePolicyPlan(planId);

    [McpServerTool]
    [Description("Reverts the most recently applied runtime-policy plan through its inverse operation.")]
    public RuntimePolicyPlanResult RevertRuntimePolicyPlan(string planId) =>
        _project.RevertRuntimePolicyPlan(planId);

    [McpServerTool]
    [Description("Searches the versioned local Asset Vault catalog by text, source, category, license and availability.")]
    public AgentAssetCatalogResult GetAssetCatalog(
        string? query = null,
        string? source = null,
        string? category = null,
        string? licenseSpdx = null,
        string? availability = null,
        int maximumResults = 100) =>
        _project.GetAssets(new AgentAssetCatalogQuery(
            query,
            source,
            category,
            licenseSpdx,
            availability,
            maximumResults));

    [McpServerTool]
    [Description("Returns the versioned JSON schema used by dialogue mutation plans.")]
    public string GetDialoguePlanSchema() => DialoguePlanSchema.Json;

    [McpServerTool]
    [Description("Validates and dry-runs a versioned dialogue plan. It returns a diff and inverse without applying changes.")]
    public DialoguePlanResult PlanDialogue(DialoguePlanRequest request) => _project.PlanDialogue(request);

    [McpServerTool]
    [Description("Applies a previously validated dialogue plan if its base revision still matches.")]
    public DialoguePlanResult ApplyDialoguePlan(string planId) => _project.ApplyDialoguePlan(planId);

    [McpServerTool]
    [Description("Reverts the most recently applied dialogue plan through its inverse operation.")]
    public DialoguePlanResult RevertDialoguePlan(string planId) => _project.RevertDialoguePlan(planId);

    [McpServerTool]
    [Description("Returns the versioned, allowlisted ForgeScript API available to module scripts.")]
    public ForgeScriptApiDescription GetForgeScriptApi() => _scripts.GetApi();

    [McpServerTool]
    [Description("Compiles ForgeScript source and returns bytecode metadata plus structured diagnostics.")]
    public ForgeScriptCompileResponse CompileForgeScript(string source) => _scripts.Compile(source);

    [McpServerTool]
    [Description("Compiles and executes ForgeScript source inside the quota-limited sandbox.")]
    public ForgeScriptSandboxResponse TestForgeScript(string source) => _scripts.TestInSandbox(source);

    [McpServerTool]
    [Description("Validates and dry-runs a safe ForgeScript binding to an entity override.")]
    public ScriptBindingPlanResult PlanScriptBinding(ScriptBindingPlanRequest request) =>
        _project.PlanScriptBinding(request);
}
