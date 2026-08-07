using System.ComponentModel;
using Forge.Application.Validation;
using Forge.Mcp;
using Forge.Mcp.Abilities;
using Forge.Mcp.Assets;
using Forge.Mcp.Creatures;
using Forge.Mcp.Factions;
using Forge.Mcp.Items;
using Forge.Mcp.Dialogues;
using Forge.Mcp.Bosses;
using Forge.Mcp.Quests;
using Forge.Mcp.Projects;
using Forge.Mcp.Scripts;
using Forge.Mcp.WorldFeatures;
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
    [Description("Returns filtered canonical world-feature profiles for placeables, doors, triggers, encounters, routes, merchants, companions and scripts without mutation.")]
    public WorldFeatureResource QueryWorldFeatures(string? kind = null, string? idOrTarget = null, int limit = 100) =>
        _project.QueryWorldFeatures(kind, idOrTarget, limit);

    [McpServerTool]
    [Description("Validates and dry-runs a complete canonical world-feature catalog mutation, returning its semantic diff without applying it.")]
    public WorldFeaturePlanResult PreviewWorldFeatures(WorldFeaturePlanRequest request) =>
        _project.PreviewWorldFeatures(request);

    [McpServerTool]
    [Description("Returns advanced quest journal, repetition, sharing, exclusivity and notification policies without mutation.")]
    public QuestOperationsResource QueryQuestOperations(string? questIdOrSearch = null, int limit = 50) =>
        _project.QueryQuestOperations(questIdOrSearch, limit);

    [McpServerTool]
    [Description("Validates and dry-runs an advanced quest-policy mutation, returning its semantic diff without applying it.")]
    public QuestOperationsPlanResult PreviewQuestOperations(QuestOperationsPlanRequest request) =>
        _project.PreviewQuestOperations(request);

    [McpServerTool]
    [Description("Returns advanced item economy, equipment, ownership, loot, crafting and generation summaries without mutation.")]
    public ItemAdvancedResource QueryAdvancedItems(string? blueprintIdOrSearch = null, int limit = 50) =>
        _project.QueryAdvancedItems(blueprintIdOrSearch, limit);

    [McpServerTool]
    [Description("Validates and dry-runs an advanced item mutation, returning its semantic diff without applying it.")]
    public ItemAdvancedPlanResult PreviewAdvancedItem(ItemAdvancedPlanRequest request) =>
        _project.PreviewAdvancedItem(request);

    [McpServerTool]
    [Description("Returns canonical creature-event profiles filtered by blueprint ID, slug or display name without mutation.")]
    public CreatureEventResource QueryCreatureEvents(string? blueprintIdOrSearch = null, int limit = 50) =>
        _project.QueryCreatureEvents(blueprintIdOrSearch, limit);

    [McpServerTool]
    [Description("Validates and dry-runs a canonical creature-event mutation, returning its semantic diff without applying it.")]
    public CreatureEventPlanResult PreviewCreatureEvents(CreatureEventPlanRequest request) =>
        _project.PreviewCreatureEvents(request);

    [McpServerTool]
    [Description("Returns canonical factions, inherited tags and policies, memberships, witness policies and dynamic state without mutation.")]
    public FactionResource QueryFactions(string? factionIdOrSearch = null, int limit = 50) =>
        _project.QueryFactions(factionIdOrSearch, limit);

    [McpServerTool]
    [Description("Validates and dry-runs a canonical faction-catalog mutation, returning its semantic diff without applying it.")]
    public FactionPlanResult PreviewFactions(FactionPlanRequest request) =>
        _project.PreviewFactions(request);

    [McpServerTool]
    [Description("Returns filtered canonical dialogue presentation profiles and cue counts without mutation.")]
    public DialoguePresentationResource QueryDialoguePresentations(
        string? dialogueIdOrSearch = null, int limit = 50) =>
        _project.QueryDialoguePresentations(dialogueIdOrSearch, limit);

    [McpServerTool]
    [Description("Validates and dry-runs a dialogue presentation mutation, returning its semantic diff without applying it.")]
    public DialoguePresentationPlanResult PreviewDialoguePresentation(DialoguePresentationPlanRequest request) =>
        _project.PreviewDialoguePresentation(request);

    [McpServerTool]
    [Description("Validates the project and returns structured diagnostics without mutating it.")]
    public IReadOnlyList<AgentDiagnostic> GetProjectDiagnostics() => _project.GetDiagnostics();

    [McpServerTool]
    [Description("Returns the canonical governed lore index, its independent revision and load diagnostics without mutation.")]
    public LoreIndexResource GetLoreIndex() => _project.GetLoreIndex();

    [McpServerTool]
    [Description("Retrieves a bounded, source-attributed and hashed lore context packet without mutation.")]
    public LoreContextResource QueryLoreIndex(string query, int limit = 8) =>
        _project.QueryLoreIndex(query, limit);

    [McpServerTool]
    [Description("Validates and dry-runs a lore-index transaction. It returns a semantic diff and inverse without applying changes.")]
    public LoreIndexPlanResult PlanLoreIndex(LoreIndexPlanRequest request) =>
        _project.PlanLoreIndex(request);

    [McpServerTool]
    [Description("Applies a previously validated lore-index plan if its transaction and base revision still match.")]
    public LoreIndexPlanResult ApplyLoreIndexPlan(string planId) =>
        _project.ApplyLoreIndexPlan(planId);

    [McpServerTool]
    [Description("Reverts the most recently applied lore-index plan through the canonical history and persisted inverse.")]
    public LoreIndexPlanResult RevertLoreIndexPlan(string planId) =>
        _project.RevertLoreIndexPlan(planId);

    [McpServerTool]
    [Description("Returns the canonical assisted area-plan catalog and its independent revision without mutation.")]
    public AssistedAreaPlanResource GetAssistedAreaPlans() => _project.GetAssistedAreaPlans();

    [McpServerTool]
    [Description("Uses the deterministic offline assistant to propose a bounded crypt or village topology. No geometry is applied.")]
    public AssistedAreaPlanProposalResource ProposeAssistedAreaPlan(AssistedAreaPlanProposalRequest request) =>
        _project.ProposeAssistedAreaPlan(request);

    [McpServerTool]
    [Description("Validates and dry-runs an assisted area-plan catalog transaction without creating geometry.")]
    public AssistedAreaPlanPlanResult PlanAssistedAreaPlans(AssistedAreaPlanPlanRequest request) =>
        _project.PlanAssistedAreaPlans(request);

    [McpServerTool]
    [Description("Persists a previously validated area-plan transaction. It still does not create geometry.")]
    public AssistedAreaPlanPlanResult ApplyAssistedAreaPlan(string planId) =>
        _project.ApplyAssistedAreaPlan(planId);

    [McpServerTool]
    [Description("Reverts the latest persisted assisted area-plan transaction through canonical history.")]
    public AssistedAreaPlanPlanResult RevertAssistedAreaPlan(string planId) =>
        _project.RevertAssistedAreaPlan(planId);

    [McpServerTool]
    [Description("Validates and dry-runs the atomic construction of an explicitly approved assisted area plan.")]
    public AssistedAreaBuildPlanResult PlanAssistedAreaBuild(AssistedAreaBuildPlanRequest request) =>
        _project.PlanAssistedAreaBuild(request);

    [McpServerTool]
    [Description("Applies a previously validated assisted area build through canonical placement commands and persists the project.")]
    public AssistedAreaBuildPlanResult ApplyAssistedAreaBuild(string planId) =>
        _project.ApplyAssistedAreaBuild(planId);

    [McpServerTool]
    [Description("Atomically reverts the latest assisted area build through canonical project history.")]
    public AssistedAreaBuildPlanResult RevertAssistedAreaBuild(string planId) =>
        _project.RevertAssistedAreaBuild(planId);

    [McpServerTool]
    [Description("Uses the deterministic offline assistant to propose a sourced, localized and linted dialogue graph without mutation.")]
    public AssistedDialogueProposalResource ProposeAssistedDialogue(AssistedDialogueProposalRequest request) =>
        _project.ProposeAssistedDialogue(request);

    [McpServerTool]
    [Description("Validates and dry-runs an explicitly approved assisted dialogue proposal through the canonical dialogue command.")]
    public AssistedDialogueApplyPlanResult PlanAssistedDialogue(AssistedDialogueApplyPlanRequest request) =>
        _project.PlanAssistedDialogue(request);

    [McpServerTool]
    [Description("Applies a previously validated assisted dialogue plan and persists the canonical project.")]
    public AssistedDialogueApplyPlanResult ApplyAssistedDialoguePlan(string planId) =>
        _project.ApplyAssistedDialoguePlan(planId);

    [McpServerTool]
    [Description("Atomically reverts the latest assisted dialogue plan through canonical project history.")]
    public AssistedDialogueApplyPlanResult RevertAssistedDialoguePlan(string planId) =>
        _project.RevertAssistedDialoguePlan(planId);

    [McpServerTool]
    [Description("Uses the deterministic offline assistant to propose a sourced, linted quest with objectives, rewards, failure paths and test scenarios without mutation.")]
    public AssistedQuestProposalResource ProposeAssistedQuest(AssistedQuestProposalRequest request) =>
        _project.ProposeAssistedQuest(request);

    [McpServerTool]
    [Description("Validates and dry-runs an explicitly approved assisted quest proposal through the canonical quest command.")]
    public AssistedQuestApplyPlanResult PlanAssistedQuest(AssistedQuestApplyPlanRequest request) =>
        _project.PlanAssistedQuest(request);

    [McpServerTool]
    [Description("Applies a previously validated assisted quest plan and persists the canonical project.")]
    public AssistedQuestApplyPlanResult ApplyAssistedQuestPlan(string planId) =>
        _project.ApplyAssistedQuestPlan(planId);

    [McpServerTool]
    [Description("Atomically reverts the latest assisted quest plan through canonical project history.")]
    public AssistedQuestApplyPlanResult RevertAssistedQuestPlan(string planId) =>
        _project.RevertAssistedQuestPlan(planId);

    [McpServerTool]
    [Description("Uses the deterministic offline assistant to compose a canonical boss creature, encounter, abilities, phases, dialogue and rewards, then runs bounded balance simulations without mutation.")]
    public AssistedBossProposalResource ProposeAssistedBoss(AssistedBossProposalRequest request) =>
        _project.ProposeAssistedBoss(request);

    [McpServerTool]
    [Description("Validates and dry-runs an explicitly approved assisted boss as one atomic multi-domain transaction.")]
    public AssistedBossApplyPlanResult PlanAssistedBoss(AssistedBossApplyPlanRequest request) =>
        _project.PlanAssistedBoss(request);

    [McpServerTool]
    [Description("Applies a previously previewed boss package and persists its canonical blueprint, dialogue and runtime orchestration document.")]
    public AssistedBossApplyPlanResult ApplyAssistedBossPlan(string planId) =>
        _project.ApplyAssistedBossPlan(planId);

    [McpServerTool]
    [Description("Atomically reverts the latest assisted boss package through canonical project history.")]
    public AssistedBossApplyPlanResult RevertAssistedBossPlan(string planId) =>
        _project.RevertAssistedBossPlan(planId);

    [McpServerTool]
    [Description("Uses the deterministic offline assistant to create a canonical combat ability with targeting, costs, effects, cues, AI tags, dependencies, five combat tests and balance notes without mutation.")]
    public AssistedAbilityProposalResource ProposeAssistedAbility(AssistedAbilityProposalRequest request) =>
        _project.ProposeAssistedAbility(request);

    [McpServerTool]
    [Description("Validates and dry-runs an explicitly approved assisted ability through the canonical project command.")]
    public AssistedAbilityApplyPlanResult PlanAssistedAbility(AssistedAbilityApplyPlanRequest request) =>
        _project.PlanAssistedAbility(request);

    [McpServerTool]
    [Description("Applies a previously previewed ability and persists its owner combat profile and runtime orchestration document atomically.")]
    public AssistedAbilityApplyPlanResult ApplyAssistedAbilityPlan(string planId) =>
        _project.ApplyAssistedAbilityPlan(planId);

    [McpServerTool]
    [Description("Atomically reverts the latest assisted ability through canonical project history.")]
    public AssistedAbilityApplyPlanResult RevertAssistedAbilityPlan(string planId) =>
        _project.RevertAssistedAbilityPlan(planId);

    [McpServerTool]
    [Description("Uses the deterministic offline assistant to produce a canonical typed Blender primitive recipe, material, UV, LOD, collision and governed GLB export request without mutation or free-form Python.")]
    public AssistedBlenderCreationProposalResource ProposeAssistedBlenderCreation(
        AssistedBlenderCreationProposalRequest request) =>
        _project.ProposeAssistedBlenderCreation(request);

    [McpServerTool]
    [Description("Validates and dry-runs an explicitly approved typed Blender creation as one confined three-file transaction.")]
    public AssistedBlenderCreationApplyPlanResult PlanAssistedBlenderCreation(
        AssistedBlenderCreationApplyPlanRequest request) =>
        _project.PlanAssistedBlenderCreation(request);

    [McpServerTool]
    [Description("Applies a previously previewed Blender creation plan and writes the canonical recipe, external-edit configuration and worker request atomically.")]
    public AssistedBlenderCreationApplyPlanResult ApplyAssistedBlenderCreationPlan(string planId) =>
        _project.ApplyAssistedBlenderCreationPlan(planId);

    [McpServerTool]
    [Description("Atomically reverts the latest assisted Blender creation and restores all governed files.")]
    public AssistedBlenderCreationApplyPlanResult RevertAssistedBlenderCreationPlan(string planId) =>
        _project.RevertAssistedBlenderCreationPlan(planId);

    [McpServerTool]
    [Description("Generates a governed text-to-3D or image-to-3D OBJ proposal through an interchangeable offline or loopback provider without writing files; returns license, provenance, topology tests, staging plan and cost.")]
    public AssistedModel3DGenerationProposalResource ProposeAssistedModel3DGeneration(
        AssistedModel3DGenerationProposalRequest request) =>
        _project.ProposeAssistedModel3DGeneration(request);

    [McpServerTool]
    [Description("Validates and dry-runs an explicitly human-approved provider-generated 3D asset as one confined four-file transaction.")]
    public AssistedModel3DGenerationApplyPlanResult PlanAssistedModel3DGeneration(
        AssistedModel3DGenerationApplyPlanRequest request) =>
        _project.PlanAssistedModel3DGeneration(request);

    [McpServerTool]
    [Description("Applies a previously previewed 3D provider plan and writes its OBJ staging source, governance receipt, external-edit configuration and typed Blender request atomically.")]
    public AssistedModel3DGenerationApplyPlanResult ApplyAssistedModel3DGenerationPlan(string planId) =>
        _project.ApplyAssistedModel3DGenerationPlan(planId);

    [McpServerTool]
    [Description("Atomically reverts the latest provider-generated 3D asset and restores every governed file.")]
    public AssistedModel3DGenerationApplyPlanResult RevertAssistedModel3DGenerationPlan(string planId) =>
        _project.RevertAssistedModel3DGenerationPlan(planId);

    [McpServerTool]
    [Description("Returns the canonical external-worker security policy, including the typed Blender allowlist, confined-path rule, timeout, output cap, permission and provenance. No exec, eval, shell or free-form Python tool is exposed.")]
    public ExternalWorkerSecurityResource GetExternalWorkerSecurity() =>
        _project.GetExternalWorkerSecurity();

    [McpServerTool]
    [Description("Validates and dry-runs a canonical external-worker security policy. Requires a bounded revision and Configure permission; it never writes during planning.")]
    public ExternalWorkerSecurityPlanResult PlanExternalWorkerSecurity(ExternalWorkerSecurityPlanRequest request) =>
        _project.PlanExternalWorkerSecurity(request);

    [McpServerTool]
    [Description("Applies only a previously validated worker-security plan bound to its transaction and revision; arbitrary execution remains impossible.")]
    public ExternalWorkerSecurityPlanResult ApplyExternalWorkerSecurityPlan(string planId) =>
        _project.ApplyExternalWorkerSecurityPlan(planId);

    [McpServerTool]
    [Description("Reverts the latest applied worker-security plan through its canonical inverse operation.")]
    public ExternalWorkerSecurityPlanResult RevertExternalWorkerSecurityPlan(string planId) =>
        _project.RevertExternalWorkerSecurityPlan(planId);

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
        int maximumResults = 100,
        bool? installed = null) =>
        _project.GetAssets(new AgentAssetCatalogQuery(
            query,
            source,
            category,
            licenseSpdx,
            availability,
            maximumResults,
            installed));

    [McpServerTool]
    [Description("Validates and dry-runs installation of a governed catalog asset as a project blueprint. License, manifest, identity and canonical GLB checks run before a plan is returned.")]
    public AgentAssetInstallPlanResult PlanAssetInstall(AgentAssetInstallPlanRequest request) =>
        _project.PlanAssetInstall(request);

    [McpServerTool]
    [Description("Applies a previously validated asset-install plan through the transactional project command bus if its base revision still matches.")]
    public AgentAssetInstallPlanResult ApplyAssetInstallPlan(string planId) =>
        _project.ApplyAssetInstallPlan(planId);

    [McpServerTool]
    [Description("Reverts the most recently applied asset-install plan through its recorded inverse operation.")]
    public AgentAssetInstallPlanResult RevertAssetInstallPlan(string planId) =>
        _project.RevertAssetInstallPlan(planId);

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
    [Description("Uses the configured assistant to propose or repair a confined ForgeScript source, compile it and run generated sandbox tests without writing it.")]
    public AssistedForgeScriptProposalResource ProposeAssistedForgeScript(AssistedForgeScriptProposalRequest request) =>
        _project.ProposeAssistedForgeScript(request);

    [McpServerTool]
    [Description("Validates and dry-runs an approved, compiled and tested ForgeScript proposal at the current project revision.")]
    public AssistedForgeScriptApplyPlanResult PlanAssistedForgeScript(AssistedForgeScriptApplyPlanRequest request) =>
        _project.PlanAssistedForgeScript(request);

    [McpServerTool]
    [Description("Writes a previously previewed ForgeScript plan atomically inside scripts/*.forge.")]
    public AssistedForgeScriptApplyPlanResult ApplyAssistedForgeScriptPlan(string planId) =>
        _project.ApplyAssistedForgeScriptPlan(planId);

    [McpServerTool]
    [Description("Restores the previous source of the latest applied ForgeScript plan if no later file change occurred.")]
    public AssistedForgeScriptApplyPlanResult RevertAssistedForgeScriptPlan(string planId) =>
        _project.RevertAssistedForgeScriptPlan(planId);

    [McpServerTool]
    [Description("Validates and dry-runs a safe ForgeScript binding to an entity override.")]
    public ScriptBindingPlanResult PlanScriptBinding(ScriptBindingPlanRequest request) =>
        _project.PlanScriptBinding(request);
}
