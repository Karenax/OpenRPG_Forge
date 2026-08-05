using System.ComponentModel;
using Forge.Application.Validation;
using Forge.Mcp;
using Forge.Mcp.Assets;
using Forge.Mcp.Dialogues;
using Forge.Mcp.Projects;
using Forge.Mcp.Scripts;
using Forge.Scripting;
using Forge.ModuleFormat.Quality;
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
    [Description("Returns startup flows, sequences and selected-area camera anchors without mutating the project.")]
    public CinematicResource GetCinematics(string? areaId = null) => _project.GetCinematics(areaId);

    [McpServerTool]
    [Description("Validates and dry-runs a versioned cinematic plan. It returns a diff and inverse without applying changes.")]
    public CinematicPlanResult PlanCinematics(CinematicPlanRequest request) => _project.PlanCinematics(request);

    [McpServerTool]
    [Description("Applies a previously validated cinematic plan if its base revision still matches.")]
    public CinematicPlanResult ApplyCinematicPlan(string planId) => _project.ApplyCinematicPlan(planId);

    [McpServerTool]
    [Description("Reverts the most recently applied cinematic plan through its inverse operation.")]
    public CinematicPlanResult RevertCinematicPlan(string planId) => _project.RevertCinematicPlan(planId);

    [McpServerTool]
    [Description("Returns the canonical module gameplay controls without mutating the project.")]
    public GameplayControlResource GetGameplayControls() => _project.GetGameplayControls();

    [McpServerTool]
    [Description("Validates and dry-runs a versioned gameplay-control plan.")]
    public GameplayControlPlanResult PlanGameplayControls(GameplayControlPlanRequest request) =>
        _project.PlanGameplayControls(request);

    [McpServerTool]
    [Description("Applies a previously validated gameplay-control plan if its base revision still matches.")]
    public GameplayControlPlanResult ApplyGameplayControlPlan(string planId) =>
        _project.ApplyGameplayControlPlan(planId);

    [McpServerTool]
    [Description("Reverts the most recently applied gameplay-control plan through its inverse operation.")]
    public GameplayControlPlanResult RevertGameplayControlPlan(string planId) =>
        _project.RevertGameplayControlPlan(planId);

    [McpServerTool]
    [Description("Returns canonical module rules and selected-area rules without mutating the project.")]
    public RulesProfileResource GetRules(string? areaId = null) => _project.GetRules(areaId);

    [McpServerTool]
    [Description("Validates and dry-runs a versioned rules plan.")]
    public RulesProfilePlanResult PlanRules(RulesProfilePlanRequest request) => _project.PlanRules(request);

    [McpServerTool]
    [Description("Applies a previously validated rules plan if its base revision still matches.")]
    public RulesProfilePlanResult ApplyRulesPlan(string planId) => _project.ApplyRulesPlan(planId);

    [McpServerTool]
    [Description("Reverts the most recently applied rules plan through its inverse operation.")]
    public RulesProfilePlanResult RevertRulesPlan(string planId) => _project.RevertRulesPlan(planId);

    [McpServerTool]
    [Description("Returns selected-area audio, voice, patrol and loading configuration without mutation.")]
    public AreaExperienceResource GetAreaExperience(string? areaId = null) =>
        _project.GetAreaExperience(areaId);

    [McpServerTool]
    [Description("Validates and dry-runs a versioned area-experience plan.")]
    public AreaExperiencePlanResult PlanAreaExperience(AreaExperiencePlanRequest request) =>
        _project.PlanAreaExperience(request);

    [McpServerTool]
    [Description("Applies a previously validated area-experience plan if its base revision still matches.")]
    public AreaExperiencePlanResult ApplyAreaExperiencePlan(string planId) =>
        _project.ApplyAreaExperiencePlan(planId);

    [McpServerTool]
    [Description("Reverts the most recently applied area-experience plan through its inverse operation.")]
    public AreaExperiencePlanResult RevertAreaExperiencePlan(string planId) =>
        _project.RevertAreaExperiencePlan(planId);

    [McpServerTool]
    [Description("Returns canonical data-driven rules, declarative UI, runtime JSON and entity snapshot policies without mutation.")]
    public RuntimeExtensionResource GetRuntimeExtensions() => _project.GetRuntimeExtensions();

    [McpServerTool]
    [Description("Validates and dry-runs a versioned runtime-extension plan with a semantic diff.")]
    public RuntimeExtensionPlanResult PlanRuntimeExtensions(RuntimeExtensionPlanRequest request) =>
        _project.PlanRuntimeExtensions(request);

    [McpServerTool]
    [Description("Applies a previously validated runtime-extension plan if its base revision still matches.")]
    public RuntimeExtensionPlanResult ApplyRuntimeExtensionPlan(string planId) =>
        _project.ApplyRuntimeExtensionPlan(planId);

    [McpServerTool]
    [Description("Reverts the most recently applied runtime-extension plan through its inverse operation.")]
    public RuntimeExtensionPlanResult RevertRuntimeExtensionPlan(string planId) =>
        _project.RevertRuntimeExtensionPlan(planId);

    [McpServerTool]
    [Description("Checks deterministic generated documentation freshness without mutating source files.")]
    public GeneratedDocumentationResource CheckGeneratedDocumentation() =>
        _project.CheckGeneratedDocumentation();

    [McpServerTool]
    [Description("Returns a read-only project health dashboard by domain, severity, tests, performance, licenses, migrations and coverage.")]
    public ProjectHealthReport GetProjectHealth(bool testsPassed = false) =>
        _project.GetProjectHealth(testsPassed);

    [McpServerTool]
    [Description("Compiles and executes the read-only project quality lab for localization, ForgeScript, dialogues, quests and encounters.")]
    public WorkspaceQualityLabReport RunProjectQualityLab() => _project.RunQualityLab();

    [McpServerTool]
    [Description("Runs the read-only adverse-network, visual-fixture, benchmark, platform-budget and integrated-profiler laboratory.")]
    public WorkspacePerformanceLabReport RunProjectPerformanceLab() => _project.RunPerformanceLab();

    [McpServerTool]
    [Description("Runs the read-only editor/runtime accessibility, security-fuzz and dependency-governance laboratory.")]
    public WorkspaceGovernanceLabReport RunProjectGovernanceLab() => _project.RunGovernanceLab();

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
    [Description("Incrementally compiles a dependency-ordered ForgeScript batch with stable diagnostics and telemetry.")]
    public ForgeScriptCompilationReport CompileForgeScriptBatch(IReadOnlyList<ForgeScriptCompilationUnit> units) =>
        _scripts.CompileBatch(units);

    [McpServerTool]
    [Description("Runs the built-in ForgeScript compiler conformance cases.")]
    public IReadOnlyList<ForgeScriptConformanceResult> RunForgeScriptCompilerConformance() =>
        _scripts.RunCompilerConformance();

    [McpServerTool]
    [Description("Compiles and executes ForgeScript source inside the quota-limited sandbox.")]
    public ForgeScriptSandboxResponse TestForgeScript(string source) => _scripts.TestInSandbox(source);

    [McpServerTool]
    [Description("Validates and dry-runs a safe ForgeScript binding to an entity override.")]
    public ScriptBindingPlanResult PlanScriptBinding(ScriptBindingPlanRequest request) =>
        _project.PlanScriptBinding(request);
}
