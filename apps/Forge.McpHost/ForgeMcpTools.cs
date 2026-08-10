using System.ComponentModel;
using Forge.Application.Validation;
using Forge.Mcp;
using Forge.Mcp.Abilities;
using Forge.Mcp.Assistant;
using Forge.Mcp.Assets;
using Forge.Mcp.Build;
using Forge.Mcp.Creatures;
using Forge.Mcp.Factions;
using Forge.Mcp.Items;
using Forge.Mcp.Navigation;
using Forge.Mcp.Dialogues;
using Forge.Mcp.Bosses;
using Forge.Mcp.Quests;
using Forge.Mcp.Projects;
using Forge.Mcp.Quality;
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
    private readonly ForgeScriptInteropService _scriptInterop;

    public ForgeMcpTools(ForgeProjectAgentService project, ForgeScriptAgentService scripts,
        ForgeScriptInteropService scriptInterop)
    {
        _project = project;
        _scripts = scripts;
        _scriptInterop = scriptInterop;
    }

    [McpServerTool]
    [Description("Returns a read-only summary of the currently opened OpenRPG Forge project.")]
    public ForgeProjectSummary GetProjectSummary() => _project.GetSummary();

    [McpServerTool]
    [Description("Returns the current transactional project revision without mutating the project.")]
    public long GetCurrentRevision() => _project.GetCurrentRevision();

    [McpServerTool]
    [Description("Returns bounded structured project, world, area, entity, dialogue, quest, script, asset, diagnostic and selection context without reading raw folder contents.")]
    public AgentStructuredContextResource GetStructuredAgentContext(
        int maximumItems = 200,
        IReadOnlyList<string>? selectionObjectIds = null) =>
        _project.GetStructuredAgentContext(new AgentContextQuery(maximumItems, selectionObjectIds));

    [McpServerTool]
    [Description("Returns the deterministic typed reference graph and an optional bounded reverse-impact report without mutation.")]
    public AgentReferenceGraphResource QueryAgentReferenceGraph(
        string? impactTargetId = null,
        int? maximumDepth = null,
        int? maximumNodes = null) =>
        _project.QueryAgentReferenceGraph(impactTargetId, maximumDepth, maximumNodes);

    [McpServerTool]
    [Description("Returns the versioned semantic-tool catalog with domain command, target, provenance and required permission.")]
    public AgentSemanticToolCatalogResource GetSemanticToolCatalog() => _project.GetSemanticToolCatalog();

    [McpServerTool]
    [Description("Validates a structured semantic command batch against the current revision and returns a deterministic diff without writing.")]
    public AgentChangePlanPreviewResult PreviewAgentChangePlan(AgentChangePlanPreviewRequest request) =>
        _project.PreviewAgentChangePlan(request);

    [McpServerTool]
    [Description("Begins a governed AI transaction after validating plan, permission, scope, budget and privacy without mutating the project.")]
    public AgentChangeTransactionResult BeginAgentChangeTransaction(AgentChangeTransactionBeginRequest request) =>
        _project.BeginAgentChangeTransaction(request);

    [McpServerTool]
    [Description("Previews a begun AI transaction and returns a structured semantic diff without writing project state.")]
    public AgentChangeTransactionResult PreviewAgentChangeTransaction(string transactionId) =>
        _project.PreviewAgentChangeTransaction(transactionId);

    [McpServerTool]
    [Description("Atomically commits a previously previewed and authorized AI transaction at its optimistic revision.")]
    public AgentChangeTransactionResult CommitAgentChangeTransaction(string transactionId) =>
        _project.CommitAgentChangeTransaction(transactionId);

    [McpServerTool]
    [Description("Rolls back the latest committed AI transaction through the canonical project inverse.")]
    public AgentChangeTransactionResult RollbackAgentChangeTransaction(string transactionId) =>
        _project.RollbackAgentChangeTransaction(transactionId);

    [McpServerTool]
    [Description("Returns the persistent redacted AI audit with actors, plans, commands, hashes, tests, provenance and rollback IDs.")]
    public AgentAiAuditResource GetAgentAiAudit() => _project.GetAgentAiAudit();

    [McpServerTool]
    [Description("Validates an assistant result against its JSON schema, identities and semantic-tool allowlist, with only bounded fence repair and no mutation.")]
    public AgentStructuredResultValidationResource ValidateAgentStructuredResult(
        AgentStructuredResultValidationRequest request) => _project.ValidateAgentStructuredResult(request);

    [McpServerTool]
    [Description("Reports whether manual authoring, build and runtime remain available without network or AI metadata.")]
    public AgentOfflineReadinessResource GetAgentOfflineReadiness() => _project.GetAgentOfflineReadiness();

    [McpServerTool]
    [Description("Reads the canonical workspace build profile and structured load diagnostics without mutation.")]
    public BuildProfileResource GetBuildProfile() => _project.GetBuildProfile();

    [McpServerTool]
    [Description("Reads the canonical project compatibility profile through an optional UUID/version/provenance filter.")]
    public ProjectCompatibilityResource QueryProjectCompatibility(string? idOrSearch = null) =>
        _project.QueryProjectCompatibility(idOrSearch);

    [McpServerTool]
    [Description("Previews a validated project compatibility change and semantic diff without writing files.")]
    public ProjectCompatibilityPlanResult PreviewProjectCompatibility(ProjectCompatibilityPlanRequest request) =>
        _project.PreviewProjectCompatibility(request);

    [McpServerTool]
    [Description("Applies a previously previewed project compatibility plan through the canonical command and atomic store.")]
    public ProjectCompatibilityPlanResult ApplyProjectCompatibilityPlan(string planId) =>
        _project.ApplyProjectCompatibilityPlan(planId);

    [McpServerTool]
    [Description("Reverts the latest applied project compatibility plan through canonical history.")]
    public ProjectCompatibilityPlanResult RevertProjectCompatibilityPlan(string planId) =>
        _project.RevertProjectCompatibilityPlan(planId);

    [McpServerTool]
    [Description("Inspects the disposable content-addressed build cache without writing or deleting files.")]
    public BuildCacheResource InspectBuildCache() => _project.InspectBuildCache();

    [McpServerTool]
    [Description("Previews canonical JSON normalization with stable structured changes and no writes.")]
    public BuildNormalizationResource PreviewBuildNormalization() => _project.PreviewBuildNormalization();

    [McpServerTool]
    [Description("Previews a versioned workspace migration, backup requirement and downgrade availability without mutation.")]
    public BuildMigrationResource PreviewWorkspaceMigration(string? targetVersion = null) =>
        _project.PreviewWorkspaceMigration(targetVersion);

    [McpServerTool]
    [Description("Returns the bounded rebuildable build graph with forward dependencies and reverse dependents.")]
    public BuildReferenceIndexResource GetBuildReferences() => _project.GetBuildReferences();

    [McpServerTool]
    [Description("Returns the stable area, graph, entity, package and script semantic diff in text and structured form.")]
    public BuildSemanticDiffResource GetBuildSemanticDiff() => _project.GetBuildSemanticDiff();

    [McpServerTool]
    [Description("Validates the canonical project and build profile with stable localized diagnostics without mutation.")]
    public BuildValidationResource ValidateBuild() => _project.ValidateBuild();

    [McpServerTool]
    [Description("Returns the deterministic incremental build plan and dependency references without mutation.")]
    public BuildPlanResource PlanBuild() => _project.PlanBuild();

    [McpServerTool]
    [Description("Builds a governed .orpmod at the explicit destination without invoking an arbitrary shell.")]
    public BuildExecutionResource BuildModule(string destinationPath) => _project.BuildModule(destinationPath);

    [McpServerTool]
    [Description("Runs an isolated byte-reproducibility build test and removes its temporary outputs.")]
    public BuildExecutionResource TestBuild() => _project.TestBuild();

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
    [Description("Returns filtered canonical quest metadata, ordered journal states, terminal kinds and localization summaries without mutation.")]
    public QuestJournalResource QueryQuestJournals(string? questIdOrSearch = null, int limit = 50) =>
        _project.QueryQuestJournals(questIdOrSearch, limit);

    [McpServerTool]
    [Description("Validates and dry-runs a complete canonical quest journal mutation, returning its semantic diff without applying it.")]
    public QuestJournalPlanResult PreviewQuestJournal(QuestJournalPlanRequest request) =>
        _project.PreviewQuestJournal(request);

    [McpServerTool]
    [Description("Executes the stored quest scenario in a bounded sandbox and returns transition traces, effects and narrative diagnostics without mutating the project.")]
    public QuestSimulationResource SimulateQuestJournal(QuestSimulationRequest request) =>
        _project.SimulateQuestJournal(request);

    [McpServerTool]
    [Description("Returns filtered canonical waypoint, spawn and transition anchors without mutation.")]
    public AnchorResource QueryAnchors(string? areaIdOrSearch = null, int limit = 100) =>
        _project.QueryAnchors(areaIdOrSearch, limit);

    [McpServerTool]
    [Description("Validates and dry-runs a complete area anchor catalog mutation without applying it.")]
    public AnchorPlanResult PreviewAnchors(AnchorPlanRequest request) =>
        _project.PreviewAnchors(request);

    [McpServerTool]
    [Description("Returns advanced item economy, equipment, ownership, loot, crafting and generation summaries without mutation.")]
    public ItemAdvancedResource QueryAdvancedItems(string? blueprintIdOrSearch = null, int limit = 50) =>
        _project.QueryAdvancedItems(blueprintIdOrSearch, limit);

    [McpServerTool]
    [Description("Validates and dry-runs an advanced item mutation, returning its semantic diff without applying it.")]
    public ItemAdvancedPlanResult PreviewAdvancedItem(ItemAdvancedPlanRequest request) =>
        _project.PreviewAdvancedItem(request);

    [McpServerTool]
    [Description("Returns canonical item foundations filtered by blueprint ID, slug, name or immutable type without mutation.")]
    public ItemFoundationResource QueryItemFoundations(string? blueprintIdOrSearch = null, int limit = 50) =>
        _project.QueryItemFoundations(blueprintIdOrSearch, limit);

    [McpServerTool]
    [Description("Validates and dry-runs an item-foundation mutation and optional explicit type migration without applying it.")]
    public ItemFoundationPlanResult PreviewItemFoundation(ItemFoundationPlanRequest request) =>
        _project.PreviewItemFoundation(request);

    [McpServerTool]
    [Description("Returns canonical creature foundations filtered by blueprint ID, slug or display name without mutation.")]
    public CreatureFoundationResource QueryCreatureFoundations(string? blueprintIdOrSearch = null, int limit = 50) =>
        _project.QueryCreatureFoundations(blueprintIdOrSearch, limit);

    [McpServerTool]
    [Description("Validates and dry-runs a canonical creature-foundation mutation, returning its semantic diff without applying it.")]
    public CreatureFoundationPlanResult PreviewCreatureFoundation(CreatureFoundationPlanRequest request) =>
        _project.PreviewCreatureFoundation(request);

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
    [Description("Returns filtered canonical dialogue graphs, roles, references and stable ordering without mutation.")]
    public DialogueGraphResource QueryDialogues(string? dialogueIdOrSearch = null, int limit = 50) =>
        _project.QueryDialogues(dialogueIdOrSearch, limit);

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
    [Description("Searches canonical P0 gameplay movement, interaction, cameras, input and save resources by text or kind without mutation.")]
    public GameplayFoundationResource QueryGameplayFoundation(string? query = null, string? kind = null) =>
        _project.QueryGameplayFoundation(query, kind);

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
    [Description("Searches canonical typed project variables by text and scope without mutation.")]
    public ProjectVariableResource QueryProjectVariables(string? query = null, string? scope = null) =>
        _project.QueryProjectVariables(query, scope);

    [McpServerTool]
    [Description("Validates and dry-runs a complete typed project-variable plan with semantic diff.")]
    public ProjectVariablePlanResult PlanProjectVariables(ProjectVariablePlanRequest request) =>
        _project.PlanProjectVariables(request);

    [McpServerTool]
    [Description("Applies a previously validated project-variable plan if its revision still matches.")]
    public ProjectVariablePlanResult ApplyProjectVariablePlan(string planId) =>
        _project.ApplyProjectVariablePlan(planId);

    [McpServerTool]
    [Description("Reverts the most recently applied project-variable plan through project history.")]
    public ProjectVariablePlanResult RevertProjectVariablePlan(string planId) =>
        _project.RevertProjectVariablePlan(planId);

    [McpServerTool]
    [Description("Returns localized project metadata, stable technical identity and player-start foundation without mutation.")]
    public ProjectFoundationResource GetProjectFoundation() => _project.GetProjectFoundation();

    [McpServerTool]
    [Description("Validates and dry-runs a versioned project-foundation plan with semantic diff and no filesystem access.")]
    public ProjectFoundationPlanResult PlanProjectFoundation(ProjectFoundationPlanRequest request) =>
        _project.PlanProjectFoundation(request);

    [McpServerTool]
    [Description("Applies a previously validated project-foundation plan if its base revision still matches.")]
    public ProjectFoundationPlanResult ApplyProjectFoundationPlan(string planId) =>
        _project.ApplyProjectFoundationPlan(planId);

    [McpServerTool]
    [Description("Reverts the most recently applied project-foundation plan through project history.")]
    public ProjectFoundationPlanResult RevertProjectFoundationPlan(string planId) =>
        _project.RevertProjectFoundationPlan(planId);

    [McpServerTool]
    [Description("Returns one independent area's canonical wizard, modular-tile and lifecycle foundation by UUID.")]
    public AreaFoundationResource GetAreaFoundation(string areaId) => _project.GetAreaFoundation(areaId);

    [McpServerTool]
    [Description("Validates and dry-runs an area-foundation change with semantic diff and no direct file access.")]
    public AreaFoundationPlanResult PlanAreaFoundation(AreaFoundationPlanRequest request) =>
        _project.PlanAreaFoundation(request);

    [McpServerTool]
    [Description("Applies a previously validated area-foundation plan if its base revision still matches.")]
    public AreaFoundationPlanResult ApplyAreaFoundationPlan(string planId) =>
        _project.ApplyAreaFoundationPlan(planId);

    [McpServerTool]
    [Description("Reverts the most recently applied area-foundation plan through canonical project history.")]
    public AreaFoundationPlanResult RevertAreaFoundationPlan(string planId) =>
        _project.RevertAreaFoundationPlan(planId);

    [McpServerTool]
    [Description("Returns one area's surface navigation, portals, Play From Here profile and nine-category readiness report without mutation.")]
    public AreaRuntimeResource GetAreaRuntime(string areaId) => _project.GetAreaRuntime(areaId);

    [McpServerTool]
    [Description("Validates and dry-runs a versioned Play From Here profile with semantic diff and no direct file access.")]
    public AreaPlaytestPlanResult PlanAreaPlaytest(AreaPlaytestPlanRequest request) =>
        _project.PlanAreaPlaytest(request);

    [McpServerTool]
    [Description("Applies a previously validated Play From Here plan if its base revision still matches.")]
    public AreaPlaytestPlanResult ApplyAreaPlaytestPlan(string planId) =>
        _project.ApplyAreaPlaytestPlan(planId);

    [McpServerTool]
    [Description("Reverts the most recently applied Play From Here plan through canonical project history.")]
    public AreaPlaytestPlanResult RevertAreaPlaytestPlan(string planId) =>
        _project.RevertAreaPlaytestPlan(planId);

    [McpServerTool]
    [Description("Returns canonical module rules and selected-area rules without mutating the project.")]
    public RulesProfileResource GetRules(string? areaId = null) => _project.GetRules(areaId);

    [McpServerTool]
    [Description("Searches canonical ruleset plugin packages and their registered capability counts without mutation.")]
    public RulesetPluginResource QueryRulesetPlugins(string? query = null) =>
        _project.QueryRulesetPlugins(query);

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

    [McpServerTool(Name = "forge_get_audio_emitters", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description("Returns canonical audio emitters filtered by area, emitter UUID or search text.")]
    public AudioEmitterResource GetAudioEmitters(string? areaId = null, string? emitterId = null,
        string? search = null) => _project.GetAudioEmitters(areaId, emitterId, search);

    [McpServerTool(Name = "forge_plan_audio_emitters", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description("Validates and dry-runs an atomic audio-emitter catalog change with semantic diff.")]
    public AudioEmitterPlanResult PlanAudioEmitters(AudioEmitterPlanRequest request) =>
        _project.PlanAudioEmitters(request);

    [McpServerTool(Name = "forge_apply_audio_emitter_plan", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description("Applies a previously validated audio-emitter plan when its base revision still matches.")]
    public AudioEmitterPlanResult ApplyAudioEmitterPlan(string planId) =>
        _project.ApplyAudioEmitterPlan(planId);

    [McpServerTool(Name = "forge_revert_audio_emitter_plan", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description("Reverts the most recently applied audio-emitter plan through canonical command history.")]
    public AudioEmitterPlanResult RevertAudioEmitterPlan(string planId) =>
        _project.RevertAudioEmitterPlan(planId);

    [McpServerTool(Name = "forge_get_content_packages", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description("Returns canonical content packages filtered by package UUID or resource search text.")]
    public ContentPackageResource GetContentPackages(string? packageId = null, string? search = null) =>
        _project.GetContentPackages(packageId, search);

    [McpServerTool(Name = "forge_plan_content_packages", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description("Validates and dry-runs an atomic content-package catalog change with semantic diff.")]
    public ContentPackagePlanResult PlanContentPackages(ContentPackagePlanRequest request) =>
        _project.PlanContentPackages(request);

    [McpServerTool(Name = "forge_get_localization", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description("Returns canonical localized messages filtered by key or search text, including package governance.")]
    public LocalizationResource GetLocalization(string? key = null, string? search = null) =>
        _project.GetLocalization(key, search);

    [McpServerTool(Name = "forge_plan_localization", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description("Validates and dry-runs an atomic localization catalog change with semantic diff.")]
    public LocalizationPlanResult PlanLocalization(LocalizationPlanRequest request) =>
        _project.PlanLocalization(request);

    [McpServerTool(Name = "forge_get_content_resources", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description("Searches unified project, package, localization and Asset Vault resources with facets, references and actions.")]
    public UnifiedContentResourceResult GetContentResources(
        string? query = null,
        string? packageNamespace = null,
        string? kind = null,
        string? licenseSpdx = null,
        string? status = null,
        int maximumResults = 300) =>
        _project.GetContentResources(new UnifiedContentResourceQuery(query, packageNamespace, kind, licenseSpdx,
            status, maximumResults));

    [McpServerTool(Name = "forge_plan_content_resource_aliases", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description("Validates and dry-runs alias/reference changes for a UUID-backed namespaced content resource.")]
    public ContentResourceAliasPlanResult PlanContentResourceAliases(ContentResourceAliasPlanRequest request) =>
        _project.PlanContentResourceAliases(request);

    [McpServerTool(Name = "forge_get_asset_governance", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description("Reads persisted asset provenance, quarantine decisions and IP boundaries with bounded filters.")]
    public AssetGovernanceResource GetAssetGovernance(
        string? assetId = null,
        string? origin = null,
        string? quarantineStatus = null,
        int maximumResults = 100) =>
        _project.GetAssetGovernance(new AssetGovernanceQuery(assetId, origin, quarantineStatus, maximumResults));

    [McpServerTool(Name = "forge_plan_asset_governance", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description("Validates and dry-runs one asset-governance change with semantic diff; it never imports or writes files.")]
    public AssetGovernancePlanResult PlanAssetGovernance(AssetGovernancePlanRequest request) =>
        _project.PlanAssetGovernance(request);

    [McpServerTool(Name = "forge_get_quality_foundation", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description("Reads the canonical P0 license, domain-test, headless, save and structured-log policy.")]
    public QualityFoundationResource GetQualityFoundation() => _project.GetQualityFoundation();

    [McpServerTool(Name = "forge_validate_canonical_project", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description("Runs schema, reference, graph, geometry and navigation validation and returns stable structured diagnostics without mutation.")]
    public CanonicalValidationResource ValidateCanonicalProject() => _project.ValidateCanonicalProject();

    [McpServerTool(Name = "forge_plan_quality_foundation", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description("Validates and dry-runs a QA-foundation policy change with semantic diff; it never writes files.")]
    public QualityFoundationPlanResult PlanQualityFoundation(QualityFoundationPlanRequest request) =>
        _project.PlanQualityFoundation(request);

    [McpServerTool]
    [Description("Returns canonical data-driven rules, declarative UI, runtime JSON and entity snapshot policies without mutation.")]
    public RuntimeExtensionResource GetRuntimeExtensions() => _project.GetRuntimeExtensions();

    [McpServerTool(Name = "forge_get_post_launch_capabilities", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description("Reads the canonical G13 post-launch capability profile, optionally filtered by section.")]
    public PostLaunchCapabilityResource GetPostLaunchCapabilities(string? section = null) =>
        _project.GetPostLaunchCapabilities(section);

    [McpServerTool(Name = "forge_preview_post_launch_capability", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description("Previews a deterministic mobility, protocol, media or soak decision without mutating the project.")]
    public PostLaunchPreviewResult PreviewPostLaunchCapability(PostLaunchPreviewRequest request) =>
        _project.PreviewPostLaunchCapability(request);

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
    [Description("Returns the canonical ForgeScript .as sources known by this editor session without mutation.")]
    public IReadOnlyList<ScriptSourceDocument> GetForgeScriptSources() => _scriptInterop.GetSources();

    [McpServerTool]
    [Description("Validates and dry-runs a canonical ForgeScript .as source transaction.")]
    public ScriptInteropResult PlanForgeScriptSource(ScriptSourceTransaction transaction) =>
        _scriptInterop.PlanCreateOrUpdateSource(transaction);

    [McpServerTool]
    [Description("Applies a previously validated ForgeScript source transaction at its base revision.")]
    public ScriptInteropResult ApplyForgeScriptSourcePlan(string planId) => _scriptInterop.ApplySourcePlan(planId);

    [McpServerTool]
    [Description("Reverts the latest applied ForgeScript source transaction when no later mutation exists.")]
    public ScriptInteropResult RevertForgeScriptSourcePlan(string planId) => _scriptInterop.RevertSourcePlan(planId);

    [McpServerTool]
    [Description("Returns structured compiler diagnostics for a canonical ForgeScript source path.")]
    public IReadOnlyList<AgentDiagnostic> GetForgeScriptDiagnostics(string path) =>
        _scriptInterop.GetDiagnostics(path);

    [McpServerTool]
    [Description("Runs a canonical ForgeScript source in the quota-limited sandbox without project mutation.")]
    public ScriptInteropTestResult RunForgeScriptTests(string path) => _scriptInterop.RunTests(path);

    [McpServerTool]
    [Description("Validates and dry-runs an event assignment with typed entry, priority, condition and authority.")]
    public ScriptEventBindingPlanResult PlanForgeScriptEventBinding(ScriptEventBindingPlanRequest request) =>
        _scriptInterop.PlanEventBinding(request);

    [McpServerTool]
    [Description("Applies a previously validated ForgeScript event assignment.")]
    public ScriptEventBindingPlanResult ApplyForgeScriptEventBindingPlan(string planId) =>
        _scriptInterop.ApplyEventBindingPlan(planId);

    [McpServerTool]
    [Description("Reverts the latest applied ForgeScript event assignment.")]
    public ScriptEventBindingPlanResult RevertForgeScriptEventBindingPlan(string planId) =>
        _scriptInterop.RevertEventBindingPlan(planId);

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

    [McpServerTool]
    [Description("Returns canonical entity scales filtered by area and stable entity IDs without exposing engine nodes.")]
    public EntityScaleResource GetEntityScales(string? areaId = null, IReadOnlyList<string>? entityIds = null) =>
        _project.GetEntityScales(areaId, entityIds);

    [McpServerTool]
    [Description("Validates and dry-runs a uniform or non-uniform entity scale transaction with a semantic diff.")]
    public EntityScalePlanResult PlanEntityScale(EntityScalePlanRequest request) =>
        _project.PlanEntityScale(request);

    [McpServerTool]
    [Description("Applies a previously validated entity scale transaction at its base revision.")]
    public EntityScalePlanResult ApplyEntityScalePlan(string planId) =>
        _project.ApplyEntityScalePlan(planId);

    [McpServerTool]
    [Description("Reverts the latest applied entity scale transaction when no later mutation exists.")]
    public EntityScalePlanResult RevertEntityScalePlan(string planId) =>
        _project.RevertEntityScalePlan(planId);
}
