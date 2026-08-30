using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using Forge.Application.Commands;
using Forge.Application.Build;
using Forge.Application.Content;
using Forge.Application.Assistant;
using Forge.Application.Creator;
using Forge.Application.Assets;
using Forge.Application.Areas;
using Forge.Application.Blueprints;
using Forge.Application.Dialogues;
using Forge.Application.Entities;
using Forge.Application.Factions;
using Forge.Application.Projects;
using Forge.Application.Quality;
using Forge.Application.Quests;
using Forge.Application.Time;
using Forge.Application.Validation;
using Forge.Application.WorldFeatures;
using Forge.Application.Modules;
using Forge.Application.Localization;
using Forge.Application.Navigation;
using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.Domain.Audio;
using Forge.Domain.Assistant;
using Forge.Domain.Assets;
using Forge.Domain.Blueprints;
using Forge.Domain.Build;
using Forge.Domain.Creatures;
using Forge.Domain.Content;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Factions;
using Forge.Domain.Items;
using Forge.Domain.Lifecycle;
using Forge.Domain.Modules;
using Forge.Domain.Navigation;
using Forge.Domain.Localization;
using Forge.Domain.Quests;
using Forge.Domain.Quality;
using Forge.Domain.Projects;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Rules;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.Mcp.Abilities;
using Forge.Mcp.Assistant;
using Forge.Mcp.Build;
using Forge.Mcp.Dialogues;
using Forge.Mcp.Bosses;
using Forge.Mcp.Quests;
using Forge.Mcp.Assets;
using Forge.Mcp.Creatures;
using Forge.Mcp.Factions;
using Forge.Mcp.Items;
using Forge.Mcp.Navigation;
using Forge.Mcp.Quality;
using Forge.Mcp.Scripts;
using Forge.Mcp.WorldFeatures;
using Forge.RpgKit.Application.Abilities;
using Forge.RpgKit.Application.Dialogues;
using Forge.RpgKit.Application.Bosses;
using Forge.RpgKit.Application.Quests;
using Forge.RpgKit.Domain.Doors;
using Forge.ModuleFormat.Workspaces;
using Forge.ModuleFormat.Quality;
using Forge.Scripting;
using Forge.ModuleFormat.Assets;
using Forge.ModuleFormat.Documentation;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Content;
using Forge.ModuleFormat.Serialization;

namespace Forge.Mcp.Projects;

public sealed partial class ForgeProjectAgentService
{
    private static readonly HashSet<string> AllowedScriptBindings =
        new(StringComparer.Ordinal)
        {
            "scriptPath",
            "phaseTwoScript",
            "deathScript",
        };

    private readonly object _gate = new();
    private readonly ProjectSession _session;
    private readonly ProjectCommandHistory _history;
    private readonly SetDialogueHandler _dialogueHandler;
    private readonly PlaceEntityHandler _placeEntityHandler;
    private readonly ApplyAssistedDialogueHandler _assistedDialogueHandler;
    private readonly ApplyAssistedQuestHandler _assistedQuestHandler;
    private readonly IForgeScriptRuntime _forgeScriptRuntime;
    private readonly ApplyAssistedForgeScriptHandler _assistedForgeScriptHandler;
    private readonly ApplyAssistedBossHandler _assistedBossHandler;
    private readonly ApplyAssistedAbilityHandler _assistedAbilityHandler;
    private readonly ApplyAssistedBlenderCreationHandler _assistedBlenderCreationHandler;
    private readonly ApplyAssistedModel3DGenerationHandler _assistedModel3DGenerationHandler;
    private readonly SetExternalWorkerSecurityPolicyHandler _externalWorkerSecurityHandler;
    private readonly HttpClient _model3DHttpClient;
    private AssetExternalEditState _assistedBlenderState;
    private readonly ExternalWorkerSecurityPolicyCommandHistory _externalWorkerSecurityHistory;
    private readonly List<AgentDiagnostic> _externalWorkerSecurityLoadDiagnostics;
    private readonly SetEntityOverridesHandler _overridesHandler;
    private readonly ScaleEntitiesHandler _scaleEntitiesHandler;
    private readonly TranslateEntitiesHandler _translateEntitiesHandler;
    private readonly SetCreatureEventsHandler _creatureEventsHandler;
    private readonly SetCreatureFoundationHandler _creatureFoundationHandler;
    private readonly SetFactionCatalogHandler _factionHandler;
    private readonly SetDialoguePresentationHandler _dialoguePresentationHandler;
    private readonly SetQuestOperationsHandler _questOperationsHandler;
    private readonly SetQuestHandler _questJournalHandler;
    private readonly SetAreaNavigationHandler _areaNavigationHandler;
    private readonly SetNavigationTopologyHandler _navigationTopologyHandler;
    private readonly SetItemAdvancedProfileHandler _itemAdvancedHandler;
    private readonly SetItemFoundationHandler _itemFoundationHandler;
    private readonly SetWorldFeatureCatalogHandler _worldFeatureHandler;
    private readonly WorldFeatureScriptService _worldFeatureScripts = new();
    private readonly SetRuntimePoliciesHandler _runtimePoliciesHandler;
    private readonly SetCinematicProfilesHandler _cinematicHandler;
    private readonly SetGameplayControlProfileHandler _gameplayControlHandler;
    private readonly SetModuleConfigurationHandler _moduleConfigurationHandler;
    private readonly PatchModuleMetadataHandler _moduleMetadataPatchHandler;
    private readonly SetProjectFoundationHandler _projectFoundationHandler;
    private readonly SetAreaFoundationHandler _areaFoundationHandler;
    private readonly SetAreaPlaytestHandler _areaPlaytestHandler;
    private readonly SetRulesProfilesHandler _rulesProfilesHandler;
    private readonly SetAreaExperienceProfileHandler _areaExperienceHandler;
    private readonly PatchAreaBiomesHandler _areaBiomeHandler;
    private readonly PatchAreaEnvironmentHandler _areaEnvironmentPatchHandler;
    private readonly SetAreaEnvironmentHandler _areaEnvironmentHandler;
    private readonly SetAreaAuthoringHandler _areaAuthoringHandler;
    private readonly SetRuntimeExtensionsHandler _runtimeExtensionsHandler;
    private readonly SetContentWorkspaceHandler _contentWorkspaceHandler;
    private readonly SetLocalizationCatalogHandler _localizationHandler;
    private readonly InstallAssetBlueprintHandler _assetInstallHandler;
    private readonly UpdateBlueprintFromEntityHandler _blueprintUpdateHandler;
    private readonly ConfigureBlueprintPrefabHandler _blueprintPrefabHandler;
    private readonly SetLoreIndexHandler _loreIndexHandler;
    private readonly LoreIndexCommandHistory _loreIndexHistory;
    private readonly List<AgentDiagnostic> _loreLoadDiagnostics;
    private readonly SetAssistedAreaPlansHandler _areaPlanHandler;
    private readonly BuildAssistedAreaPlanHandler _areaBuildHandler;
    private readonly AssistedAreaPlanCommandHistory _areaPlanHistory;
    private readonly List<AgentDiagnostic> _areaPlanLoadDiagnostics;
    private readonly Dictionary<string, StoredDialoguePlan> _dialoguePlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredAssistedDialoguePlan> _assistedDialoguePlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredAssistedQuestPlan> _assistedQuestPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredAssistedForgeScriptPlan> _assistedForgeScriptPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredAssistedBossPlan> _assistedBossPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredAssistedAbilityPlan> _assistedAbilityPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredAssistedBlenderCreationPlan> _assistedBlenderCreationPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredAssistedModel3DGenerationPlan> _assistedModel3DGenerationPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredExternalWorkerSecurityPlan> _externalWorkerSecurityPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredRuntimePolicyPlan> _runtimePolicyPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredCinematicPlan> _cinematicPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredGameplayControlPlan> _gameplayControlPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredProjectVariablePlan> _projectVariablePlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredProjectDocumentationPlan> _projectDocumentationPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredProjectFoundationPlan> _projectFoundationPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredProjectMetadataPatchPlan> _projectMetadataPatchPlans =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredAreaFoundationPlan> _areaFoundationPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredAreaBiomePlan> _areaBiomePlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredAreaEnvironmentPatchPlan> _areaEnvironmentPatchPlans =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredAreaEnvironmentPlan> _areaEnvironmentPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredAreaExperienceAuthoringPlan> _areaExperienceAuthoringPlans =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredAreaPlaytestPlan> _areaPlaytestPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredEntityScalePlan> _entityScalePlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredEntityTranslationPlan> _entityTranslationPlans =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredWaypointPlan> _waypointPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredRulesProfilePlan> _rulesProfilePlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredAreaExperiencePlan> _areaExperiencePlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredAudioEmitterPlan> _audioEmitterPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredRuntimeExtensionPlan> _runtimeExtensionPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredAssetInstallPlan> _assetInstallPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredBlueprintUpdatePlan> _blueprintUpdatePlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredBlueprintPrefabPlan> _blueprintPrefabPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredEditorProductivityPlan> _editorProductivityPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredLoreIndexPlan> _loreIndexPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredAssistedAreaPlan> _areaPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredAssistedAreaBuildPlan> _areaBuildPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, StoredAgentChangeTransaction> _agentTransactions = [];
    private readonly List<AgentAiAuditEntry> _agentAudit;
    private readonly List<AgentDiagnostic> _agentAuditLoadDiagnostics;
    private readonly IClock _clock;
    private readonly SetWorkspaceBuildProfileHandler _buildProfileHandler;
    private readonly WorkspaceBuildProfileCommandHistory _buildProfileHistory;
    private readonly Dictionary<string, StoredProjectCompatibilityPlan> _projectCompatibilityPlans =
        new(StringComparer.Ordinal);

    private ForgeProjectAgentService(
        ProjectSession session,
        IClock clock,
        ProjectCommandHistory? sharedHistory = null)
    {
        _session = session;
        _clock = clock;
        var buildProfile = WorkspaceBuildProfileStore.Load(session.RootDirectory);
        _buildProfileHandler = new SetWorkspaceBuildProfileHandler(clock);
        _buildProfileHistory = new WorkspaceBuildProfileCommandHistory(buildProfile.Profile);
        _history = sharedHistory ?? new ProjectCommandHistory(new ProjectState(
            session.Project,
            new ProjectRevision(session.Revision)));
        _dialogueHandler = new SetDialogueHandler(clock);
        _placeEntityHandler = new PlaceEntityHandler(clock);
        _assistedDialogueHandler = new ApplyAssistedDialogueHandler(clock);
        _assistedQuestHandler = new ApplyAssistedQuestHandler(clock);
        _forgeScriptRuntime = new NativeForgeScriptRuntime();
        _assistedForgeScriptHandler = new ApplyAssistedForgeScriptHandler();
        _assistedBossHandler = new ApplyAssistedBossHandler(clock);
        _assistedAbilityHandler = new ApplyAssistedAbilityHandler(clock);
        _assistedBlenderCreationHandler = new ApplyAssistedBlenderCreationHandler(clock);
        _assistedModel3DGenerationHandler = new ApplyAssistedModel3DGenerationHandler(clock);
        _externalWorkerSecurityHandler = new SetExternalWorkerSecurityPolicyHandler(clock);
        _contentWorkspaceHandler = new SetContentWorkspaceHandler(clock);
        _localizationHandler = new SetLocalizationCatalogHandler(clock);
        _model3DHttpClient = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false })
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        var externalEdits = AssetExternalEditStore.Load(session.RootDirectory);
        _assistedBlenderState = new AssetExternalEditState(externalEdits.Configurations);
        var workerSecurity = ExternalWorkerSecurityPolicyStore.Load(session.RootDirectory);
        _externalWorkerSecurityHistory = new ExternalWorkerSecurityPolicyCommandHistory(workerSecurity.Policy);
        _externalWorkerSecurityLoadDiagnostics = workerSecurity.Diagnostics.Select(item =>
            new AgentDiagnostic(item.Code, item.Message, item.Path)).ToList();
        _overridesHandler = new SetEntityOverridesHandler(clock);
        _scaleEntitiesHandler = new ScaleEntitiesHandler(clock);
        _translateEntitiesHandler = new TranslateEntitiesHandler(clock);
        _creatureEventsHandler = new SetCreatureEventsHandler(clock);
        _creatureFoundationHandler = new SetCreatureFoundationHandler(clock);
        _factionHandler = new SetFactionCatalogHandler(clock);
        _dialoguePresentationHandler = new SetDialoguePresentationHandler(clock);
        _questOperationsHandler = new SetQuestOperationsHandler(clock);
        _questJournalHandler = new SetQuestHandler(clock);
        _areaNavigationHandler = new SetAreaNavigationHandler(clock);
        _navigationTopologyHandler = new SetNavigationTopologyHandler(clock);
        _itemAdvancedHandler = new SetItemAdvancedProfileHandler(clock);
        _itemFoundationHandler = new SetItemFoundationHandler(clock);
        _worldFeatureHandler = new SetWorldFeatureCatalogHandler(clock);
        _runtimePoliciesHandler = new SetRuntimePoliciesHandler(clock);
        _cinematicHandler = new SetCinematicProfilesHandler(clock);
        _gameplayControlHandler = new SetGameplayControlProfileHandler(clock);
        _moduleConfigurationHandler = new SetModuleConfigurationHandler(clock);
        _moduleMetadataPatchHandler = new PatchModuleMetadataHandler(clock);
        _projectFoundationHandler = new SetProjectFoundationHandler(clock);
        _areaFoundationHandler = new SetAreaFoundationHandler(clock);
        _areaPlaytestHandler = new SetAreaPlaytestHandler(clock);
        _rulesProfilesHandler = new SetRulesProfilesHandler(clock);
        _areaExperienceHandler = new SetAreaExperienceProfileHandler(clock);
        _areaBiomeHandler = new PatchAreaBiomesHandler(clock);
        _areaEnvironmentPatchHandler = new PatchAreaEnvironmentHandler(clock);
        _areaEnvironmentHandler = new SetAreaEnvironmentHandler(clock);
        _areaAuthoringHandler = new SetAreaAuthoringHandler(clock);
        _runtimeExtensionsHandler = new SetRuntimeExtensionsHandler(clock);
        _assetInstallHandler = new InstallAssetBlueprintHandler(clock);
        _blueprintUpdateHandler = new UpdateBlueprintFromEntityHandler(clock);
        _blueprintPrefabHandler = new ConfigureBlueprintPrefabHandler(clock);
        _loreIndexHandler = new SetLoreIndexHandler(clock);
        var loreIndex = LoreIndexStore.Load(session.RootDirectory);
        _loreIndexHistory = new LoreIndexCommandHistory(loreIndex.Index);
        _loreLoadDiagnostics = loreIndex.Diagnostics.Select(item =>
            new AgentDiagnostic(item.Code, item.Message, item.Path)).ToList();
        _areaPlanHandler = new SetAssistedAreaPlansHandler(clock);
        _areaBuildHandler = new BuildAssistedAreaPlanHandler(clock);
        var areaPlans = AssistedAreaPlanStore.Load(session.RootDirectory);
        _areaPlanHistory = new AssistedAreaPlanCommandHistory(areaPlans.Catalog);
        _areaPlanLoadDiagnostics = areaPlans.Diagnostics.Select(item =>
            new AgentDiagnostic(item.Code, item.Message, item.Path)).ToList();
        var agentAudit = AgentAiAuditStore.Load(session.RootDirectory);
        _agentAudit = agentAudit.Entries.ToList();
        _agentAuditLoadDiagnostics = agentAudit.Diagnostics.Select(item =>
            new AgentDiagnostic(item.Code, item.Message, item.Path)).ToList();
    }

    public string WorkspaceDirectory => _session.RootDirectory;

    public static ForgeProjectAgentOpenResult Open(string workspaceDirectory, IClock? clock = null)
    {
        var opened = ProjectSession.Open(workspaceDirectory);
        return opened.Session is null
            ? new ForgeProjectAgentOpenResult(
                null,
                opened.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray())
            : new ForgeProjectAgentOpenResult(
                new ForgeProjectAgentService(opened.Session, clock ?? new SystemClock()),
                []);
    }

    public static ForgeProjectAgentService Attach(
        ProjectSession session,
        ProjectCommandHistory sharedHistory,
        IClock? clock = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(sharedHistory);
        if (sharedHistory.State.Revision.Value < session.Revision)
            throw new InvalidOperationException(
                "Shared project history is older than the persisted workspace session.");
        if (sharedHistory.State.Project.Id != session.Project.Id)
            throw new InvalidOperationException(
                "Shared project history does not belong to the workspace session.");
        return new ForgeProjectAgentService(session, clock ?? new SystemClock(), sharedHistory);
    }

    public ForgeProjectSummary GetSummary()
    {
        lock (_gate)
        {
            var project = _history.State.Project;
            return new ForgeProjectSummary(
                project.Id.ToString(),
                project.Title,
                project.Module.Id.ToString(),
                project.Module.Slug,
                project.Module.DisplayName,
                project.Module.EntryAreaId.ToString(),
                _history.State.Revision.Value,
                project.Blueprints.Count,
                project.Areas.Count,
                project.Areas.Sum(area => area.Entities.Count),
                project.Dialogues.Count,
                project.Quests.Count);
        }
    }

    public long GetCurrentRevision()
    {
        lock (_gate)
        {
            return _history.State.Revision.Value;
        }
    }

    public BuildProfileResource GetBuildProfile()
    {
        lock (_gate)
        {
            var loaded = WorkspaceBuildProfileStore.Load(_session.RootDirectory);
            var profile = loaded.Profile;
            return new(profile.SchemaVersion, profile.Id.ToString("D"), profile.Provenance, loaded.FileMissing,
                profile.Layout, profile.CliOperations,
                profile.StableOrdering && profile.NormalizedTimestamps && profile.LockedDependencies &&
                profile.ContentHashes && profile.VerifyReproducibility,
                profile.IncrementalBuild && profile.RebuildAffectedOnly,
                new BuildGovernanceResource(
                    profile.Governance.Id.ToString("D"),
                    profile.Governance.SchemaVersion,
                    profile.Governance.Worktrees.BranchPrefix,
                    profile.Governance.ContinuousIntegration.WorkflowPath,
                    profile.Governance.ReferenceFixtures.Fixtures.Select(item => item.Kind.ToString()).ToArray(),
                    profile.Governance.Versioning.CoreVersion,
                    profile.Governance.Versioning.KitVersions,
                    profile.Governance.Versioning.PackageVersion,
                    profile.Governance.Versioning.ChangelogPath,
                    profile.Governance.ArchitectureDecisions.TemplatePath,
                    profile.Governance.ArchitectureDecisions.AllowedStatuses),
                new BuildCollaborationResource(
                    profile.Collaboration.Id.ToString("D"), profile.Collaboration.SchemaVersion,
                    profile.Collaboration.Provenance, profile.Collaboration.Cache.IndexFileName,
                    profile.Collaboration.Cache.MaximumEntries,
                    profile.Collaboration.Normalization.MaximumDocumentBytes,
                    profile.Collaboration.Migrations.BackupDirectory,
                    profile.Collaboration.Migrations.MaximumBackups,
                    profile.Collaboration.References.MaximumNodes,
                    profile.Collaboration.SemanticDiff.MaximumEntries,
                    ["cache-content-addressed-disposable", "json-utf8-lf-stable-atomic",
                     "migration-dry-run-backup-idempotent", "references-forward-reverse-rebuildable",
                     "semantic-diff-area-graph-entity-package-script-text-json"]),
                ToCompatibilityResource(profile.Compatibility),
                loaded.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
        }
    }

    public ProjectCompatibilityResource QueryProjectCompatibility(string? idOrSearch = null)
    {
        lock (_gate)
        {
            var compatibility = _buildProfileHistory.State.Profile.Compatibility;
            var filter = idOrSearch?.Trim();
            var found = string.IsNullOrWhiteSpace(filter) ||
                        string.Equals(compatibility.Id.ToString("D"), filter, StringComparison.OrdinalIgnoreCase) ||
                        compatibility.MinimumRuntimeVersion.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        compatibility.ProjectFormatVersion.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        compatibility.Provenance.Contains(filter, StringComparison.OrdinalIgnoreCase);
            return found
                ? ToCompatibilityResource(compatibility)
                : new(false, string.Empty, 0, string.Empty, string.Empty, string.Empty, false, false,
                    [new AgentDiagnostic("build.compatibility.not_found",
                        "No project compatibility profile matches the filter.", "build/compatibility")]);
        }
    }

    public ProjectCompatibilityPlanResult PreviewProjectCompatibility(ProjectCompatibilityPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return CompatibilityFailure(request.BaseRevision, "build.compatibility.mcp_schema_unsupported",
                    "Project compatibility MCP request schema must be v1.", "request/schemaVersion");
            var source = _buildProfileHistory.State.Profile;
            var compatibility = source.Compatibility with
            {
                Provenance = "openrpg-forge:mcp:r026",
                MinimumRuntimeVersion = request.MinimumRuntimeVersion,
                ProjectFormatVersion = request.ProjectFormatVersion,
                AutomaticMigrations = request.AutomaticMigrations,
                RequiredAtRuntime = request.RequiredAtRuntime,
            };
            var target = WithCompatibility(source, compatibility);
            var command = new SetWorkspaceBuildProfileCommand(CommandId.From(Guid.NewGuid()),
                new WorkspaceBuildProfileRevision(request.BaseRevision), target, true);
            var preview = _buildProfileHandler.Handle(_buildProfileHistory.State, command);
            if (!preview.Succeeded)
                return new(false, null, request.BaseRevision, null, false, false, null, [],
                    preview.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message,
                        "build/compatibility")).ToArray());
            var planId = Guid.NewGuid().ToString("N");
            var diff = CompatibilityDiff(source.Compatibility, compatibility);
            _projectCompatibilityPlans[planId] = new(planId, request.BaseRevision, target, diff);
            return new(true, planId, request.BaseRevision, null, false, false,
                ToCompatibilityResource(compatibility), diff, []);
        }
    }

    public ProjectCompatibilityPlanResult ApplyProjectCompatibilityPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_projectCompatibilityPlans.TryGetValue(planId, out var plan))
                return CompatibilityFailure(_buildProfileHistory.State.Revision.Value,
                    "build.compatibility.plan_missing", "Project compatibility plan does not exist.", "planId");
            if (plan.Applied)
                return CompatibilityFailure(_buildProfileHistory.State.Revision.Value,
                    "build.compatibility.plan_already_applied", "Project compatibility plan is already applied.",
                    "planId");
            var command = new SetWorkspaceBuildProfileCommand(CommandId.From(Guid.NewGuid()),
                new WorkspaceBuildProfileRevision(plan.BaseRevision), plan.Profile, false);
            var result = _buildProfileHandler.Handle(_buildProfileHistory.State, command);
            if (!result.Succeeded)
                return new(false, planId, plan.BaseRevision, null, false, false, null, plan.Diff,
                    result.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message,
                        "build/compatibility")).ToArray());
            var saved = WorkspaceBuildProfileStore.Save(_session.RootDirectory, result.State.Profile);
            if (!saved.Succeeded)
                return new(false, planId, plan.BaseRevision, null, false, false, null, plan.Diff,
                    saved.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
            _buildProfileHistory.Record(result);
            plan.Applied = true;
            plan.AppliedRevision = result.NewRevision!.Value.Value;
            return new(true, planId, plan.BaseRevision, plan.AppliedRevision, true, false,
                ToCompatibilityResource(result.State.Profile.Compatibility), plan.Diff, []);
        }
    }

    public ProjectCompatibilityPlanResult RevertProjectCompatibilityPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_projectCompatibilityPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted ||
                plan.AppliedRevision != _buildProfileHistory.State.Revision.Value)
                return CompatibilityFailure(_buildProfileHistory.State.Revision.Value,
                    "build.compatibility.revert_unavailable", "Project compatibility plan cannot be reverted now.",
                    "planId");
            if (!_buildProfileHistory.Undo())
                return CompatibilityFailure(_buildProfileHistory.State.Revision.Value,
                    "build.compatibility.revert_unavailable", "Project compatibility history is empty.", "planId");
            var saved = WorkspaceBuildProfileStore.Save(_session.RootDirectory, _buildProfileHistory.State.Profile);
            if (!saved.Succeeded)
            {
                _buildProfileHistory.Redo();
                return new(false, planId, plan.BaseRevision, null, true, false, null, plan.Diff,
                    saved.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
            }
            plan.Reverted = true;
            return new(true, planId, plan.BaseRevision, _buildProfileHistory.State.Revision.Value, true, true,
                ToCompatibilityResource(_buildProfileHistory.State.Profile.Compatibility), plan.Diff, []);
        }
    }

    private static WorkspaceBuildProfile WithCompatibility(
        WorkspaceBuildProfile source, ProjectCompatibilityProfile compatibility) =>
        new(source.Id, source.SchemaVersion, "openrpg-forge:mcp:r026", source.RequireGitRepository,
            source.TextDocuments, source.ExplicitGeneratedFiles, source.ReproducibleAssets, source.Layout,
            source.CliOperations, source.StableOrdering, source.NormalizedTimestamps, source.LockedDependencies,
            source.ContentHashes, source.VerifyReproducibility, source.IncrementalBuild, source.CachePath,
            source.RebuildAffectedOnly, source.Governance, source.Collaboration, compatibility);

    private static ProjectCompatibilityResource ToCompatibilityResource(ProjectCompatibilityProfile profile) =>
        new(true, profile.Id.ToString("D"), profile.SchemaVersion, profile.Provenance,
            profile.MinimumRuntimeVersion, profile.ProjectFormatVersion, profile.AutomaticMigrations,
            profile.RequiredAtRuntime, []);

    private static IReadOnlyList<string> CompatibilityDiff(
        ProjectCompatibilityProfile before, ProjectCompatibilityProfile after)
    {
        var changes = new List<string>();
        if (before.MinimumRuntimeVersion != after.MinimumRuntimeVersion)
            changes.Add($"minimumRuntimeVersion: {before.MinimumRuntimeVersion} -> {after.MinimumRuntimeVersion}");
        if (before.ProjectFormatVersion != after.ProjectFormatVersion)
            changes.Add($"projectFormatVersion: {before.ProjectFormatVersion} -> {after.ProjectFormatVersion}");
        if (before.AutomaticMigrations != after.AutomaticMigrations)
            changes.Add($"automaticMigrations: {before.AutomaticMigrations} -> {after.AutomaticMigrations}");
        if (before.RequiredAtRuntime != after.RequiredAtRuntime)
            changes.Add($"requiredAtRuntime: {before.RequiredAtRuntime} -> {after.RequiredAtRuntime}");
        return changes;
    }

    private static ProjectCompatibilityPlanResult CompatibilityFailure(
        long revision, string code, string message, string path) =>
        new(false, null, revision, null, false, false, null, [], [new AgentDiagnostic(code, message, path)]);

    public BuildValidationResource ValidateBuild()
    {
        lock (_gate)
        {
            var loaded = WorkspaceBuildProfileStore.Load(_session.RootDirectory);
            var quality = QualityFoundationProfileStore.Load(
                _session.RootDirectory, _history.State.Project.Id.Value);
            var canonical = quality.Succeeded
                ? WorkspaceCanonicalValidationService.Run(
                    _session.RootDirectory, _history.State.Project, quality.Profile)
                : null;
            var blocking = canonical is null
                ? []
                : WorkspaceCanonicalValidationService.BlockingDiagnostics(
                    _history.State.Project, quality.Profile, canonical);
            var diagnostics = loaded.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path))
                .Concat(quality.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)))
                .Concat(ProjectValidator.Validate(_history.State.Project)
                    .Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)))
                .Concat(blocking.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)))
                .ToArray();
            return new(diagnostics.Length == 0, _history.State.Project.Id.ToString(), loaded.Profile.Id.ToString("D"), diagnostics);
        }
    }

    public BuildPlanResource PlanBuild()
    {
        lock (_gate)
        {
            var build = WorkspaceBuildGraphBuilder.Build(_session.RootDirectory, _history.State.Project, _session.ProjectFilePath);
            var plan = WorkspaceIncrementalBuildPlanner.Plan(WorkspaceBuildStateStore.TryLoad(_session.RootDirectory), build.Graph);
            return new(build.Graph.SchemaVersion, build.Graph.Id.ToString(), plan.ChangedPaths, plan.RemovedPaths,
                plan.RebuiltResources.Select(item => item.Path).ToArray(),
                plan.ReusedResources.Select(item => item.Path).ToArray(),
                build.Graph.Nodes.Select(item => new BuildPlanNode(item.Id.ToString(), item.Path, item.Kind,
                    item.ContentSha256, item.Dependencies.Select(id => id.ToString()).ToArray())).ToArray());
        }
    }

    public BuildExecutionResource BuildModule(string destinationPath)
    {
        lock (_gate)
        {
            var validation = ValidateBuild();
            if (!validation.Valid)
                return new(false, Path.GetFullPath(destinationPath), string.Empty, false, [], [], validation.Diagnostics);
            var result = ModulePackager.Export(_session.RootDirectory, destinationPath);
            return new(true, result.PackagePath, result.Manifest.ContentSha256, true,
                result.BuildPlan.RebuiltResources.Select(item => item.Path).ToArray(),
                result.BuildPlan.ReusedResources.Select(item => item.Path).ToArray(), []);
        }
    }

    public BuildExecutionResource TestBuild()
    {
        lock (_gate)
        {
            var validation = ValidateBuild();
            if (!validation.Valid)
                return new(false, string.Empty, string.Empty, false, [], [], validation.Diagnostics);
            var root = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "mcp-build-test", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var firstPath = Path.Combine(root, "first.orpmod");
            var secondPath = Path.Combine(root, "second.orpmod");
            try
            {
                var first = ModulePackager.Export(_session.RootDirectory, firstPath);
                var second = ModulePackager.Export(_session.RootDirectory, secondPath);
                var reproducible = File.ReadAllBytes(firstPath).SequenceEqual(File.ReadAllBytes(secondPath));
                return new(reproducible, string.Empty, first.Manifest.ContentSha256, reproducible,
                    first.BuildPlan.RebuiltResources.Select(item => item.Path).ToArray(),
                    second.BuildPlan.ReusedResources.Select(item => item.Path).ToArray(),
                    reproducible ? [] : [new AgentDiagnostic("build.reproducibility_failed",
                        "Two consecutive builds produced different bytes.", "build/determinism")]);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }

    public BuildCacheResource InspectBuildCache()
    {
        lock (_gate)
        {
            var loaded = WorkspaceBuildProfileStore.Load(_session.RootDirectory);
            var report = WorkspaceBuildCollaborationService.RebuildCacheIndex(
                _session.RootDirectory, loaded.Profile, dryRun: true);
            return new(report.Succeeded, true, report.CacheDirectory, report.IndexPath, report.Entries,
                report.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
        }
    }

    public BuildNormalizationResource PreviewBuildNormalization()
    {
        lock (_gate)
        {
            var loaded = WorkspaceBuildProfileStore.Load(_session.RootDirectory);
            var report = WorkspaceBuildCollaborationService.NormalizeDocuments(
                _session.RootDirectory, loaded.Profile, dryRun: true);
            return new(report.Succeeded, true, report.Changes,
                report.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
        }
    }

    public BuildMigrationResource PreviewWorkspaceMigration(string? targetVersion = null)
    {
        lock (_gate)
        {
            var loaded = WorkspaceBuildProfileStore.Load(_session.RootDirectory);
            var report = WorkspaceBuildCollaborationService.MigrateWorkspace(
                _session.RootDirectory, loaded.Profile, dryRun: true, targetVersion);
            return new(report.Succeeded, true, report.FromVersion, report.ToVersion, false,
                report.Idempotent, report.DowngradeSupported, report.BackupDirectory, report.ChangedDocuments,
                report.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
        }
    }

    public BuildReferenceIndexResource GetBuildReferences()
    {
        lock (_gate)
        {
            var loaded = WorkspaceBuildProfileStore.Load(_session.RootDirectory);
            var index = WorkspaceBuildCollaborationService.BuildReferenceIndex(
                _session.RootDirectory, _history.State.Project, _session.ProjectFilePath,
                loaded.Profile.Collaboration);
            return new(index.GraphId, index.Nodes,
                index.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
        }
    }

    public BuildSemanticDiffResource GetBuildSemanticDiff()
    {
        lock (_gate)
        {
            var loaded = WorkspaceBuildProfileStore.Load(_session.RootDirectory);
            var graph = WorkspaceBuildGraphBuilder.Build(
                _session.RootDirectory, _history.State.Project, _session.ProjectFilePath).Graph;
            var current = WorkspaceBuildCollaborationService.CreateSemanticSnapshot(_history.State.Project, graph);
            var previous = WorkspaceBuildCollaborationService.LoadSemanticSnapshot(_session.RootDirectory);
            var report = WorkspaceBuildCollaborationService.Diff(previous, current, loaded.Profile.Collaboration);
            return new(report.Succeeded, report.Text, report.Changes,
                report.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
        }
    }

    public WorldFeatureResource QueryWorldFeatures(string? kind = null, string? idOrTarget = null, int limit = 100)
    {
        lock (_gate)
        {
            var values = _worldFeatureScripts.Query(_history.State, kind?.Trim(), idOrTarget?.Trim(), limit)
                .Select(item => new WorldFeatureResourceItem(item.Kind, item.Id.ToString("D"), item.TargetId,
                    item.Provenance, item.Properties)).ToArray();
            return new(1, _history.State.Revision.Value, kind?.Trim(), idOrTarget?.Trim(), values);
        }
    }

    public WorldFeaturePlanResult PreviewWorldFeatures(WorldFeaturePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return new(false, request.BaseRevision, null, null,
                    [new AgentDiagnostic("world.mcp_schema_unsupported",
                        "World-feature MCP request schema must be v1.", "request/schemaVersion")]);
            var current = _history.State.Project;
            var candidate = new ProjectDefinition(current.Id, current.Title,
                current.Module.WithWorldFeatures(request.Catalog), current.Blueprints, current.Areas,
                current.Dialogues, current.Quests);
            // The handler below remains the gate; this projection preserves canonical Code+Path in the MCP reply.
            var doorDiagnostics = request.Catalog.Doors
                .Where(item => item.Validation is not null || item.ValidationRequiredAtRuntime)
                .Select(item => DoorWorldValidationService.Validate(candidate, item.Id))
                .Where(item => !item.Available)
                .SelectMany(item => item.Diagnostics)
                .Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path))
                .DistinctBy(item => (item.Code, item.Path))
                .ToArray();
            var result = _worldFeatureHandler.Handle(_history.State, new SetWorldFeatureCatalogCommand(
                CommandId.From(Guid.NewGuid()), new ProjectRevision(request.BaseRevision), request.Catalog, true));
            if (!result.Succeeded)
            {
                var handlerDiagnostics = result.Diagnostics.Select(item =>
                {
                    var separator = item.Message.IndexOf(": ", StringComparison.Ordinal);
                    return separator > 0
                        ? new AgentDiagnostic(item.Code, item.Message[(separator + 2)..], item.Message[..separator])
                        : new AgentDiagnostic(item.Code, item.Message, "module/worldFeatures");
                });
                return new(false, request.BaseRevision, result.Patch, result.Inverse,
                    doorDiagnostics.Concat(handlerDiagnostics)
                        .DistinctBy(item => (item.Code, item.Path))
                        .ToArray());
            }

            if (doorDiagnostics.Length > 0)
                return new(false, request.BaseRevision, result.Patch, result.Inverse, doorDiagnostics);
            return new(result.Succeeded, request.BaseRevision, result.Patch, result.Inverse,
                result.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message,
                    "module/worldFeatures")).ToArray());
        }
    }

    public QuestOperationsResource QueryQuestOperations(string? questIdOrSearch = null, int limit = 50)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        lock (_gate)
        {
            var filter = questIdOrSearch?.Trim();
            var items = _history.State.Project.Quests.Where(item => item.Operations is not null)
                .Where(item => string.IsNullOrWhiteSpace(filter) ||
                    string.Equals(item.Id.ToString(), filter, StringComparison.OrdinalIgnoreCase) ||
                    item.Slug.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    item.DisplayName.Contains(filter, StringComparison.CurrentCultureIgnoreCase))
                .OrderBy(item => item.Slug, StringComparer.Ordinal).Take(limit)
                .Select(item => new QuestOperationsResourceItem(item.Id.ToString(), item.Slug, item.DisplayName,
                    item.Operations!.Id.ToString("D"), item.Operations.Journal.Priority,
                    item.Operations.Journal.NarrativeImportance, item.Operations.Repeat.Enabled,
                    item.Operations.Share.Mode.ToString(), item.Operations.ExclusiveChoices.Count,
                    item.Operations.Notification.Mode.ToString(), item.Operations.RequiredAtRuntime))
                .ToArray();
            return new(1, _history.State.Revision.Value, filter, items);
        }
    }

    public QuestJournalResource QueryQuestJournals(string? questIdOrSearch = null, int limit = 50)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        lock (_gate)
        {
            var filter = questIdOrSearch?.Trim();
            var items = _history.State.Project.Quests
                .Where(item => string.IsNullOrWhiteSpace(filter) ||
                    string.Equals(item.Id.ToString(), filter, StringComparison.OrdinalIgnoreCase) ||
                    item.Slug.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    item.Title.Fallback.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
                    item.Category.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
                    item.Owner.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
                    item.Authoring?.Notes.Design.Contains(filter, StringComparison.CurrentCultureIgnoreCase) == true ||
                    item.Authoring?.Notes.Canon.Contains(filter, StringComparison.CurrentCultureIgnoreCase) == true ||
                    item.Authoring?.Participants.Any(participant =>
                        participant.Role.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        participant.BindingValue.Contains(filter, StringComparison.OrdinalIgnoreCase)) == true ||
                    item.Experience?.Resources.Any(resource =>
                        resource.ReferenceId.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        resource.OwnerId.Contains(filter, StringComparison.OrdinalIgnoreCase)) == true ||
                    item.Experience?.Markers.Any(marker =>
                        marker.BindingValue.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        marker.Label.Fallback.Contains(filter, StringComparison.CurrentCultureIgnoreCase)) == true ||
                    item.Quality?.TestScenarios.Any(scenario =>
                        scenario.Label.Fallback.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
                        scenario.Fixtures.Any(fixture => fixture.ReferenceId.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
                        scenario.Steps.Any(step => step.Action.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase))) == true ||
                    item.Quality?.AiPlan.Synopsis.Fallback.Contains(filter, StringComparison.CurrentCultureIgnoreCase) == true ||
                    item.Quality?.AiPlan.Dependencies.Any(dependency =>
                        dependency.ReferenceId.Contains(filter, StringComparison.OrdinalIgnoreCase)) == true ||
                    item.Quality?.AiPlan.ContextCitations.Any(citation =>
                        citation.Contains(filter, StringComparison.OrdinalIgnoreCase)) == true ||
                    item.Quality?.AiPlan.StyleRules.Any(rule =>
                        rule.Contains(filter, StringComparison.OrdinalIgnoreCase)) == true)
                .OrderByDescending(item => item.Priority).ThenBy(item => item.Slug, StringComparer.Ordinal)
                .Take(limit)
                .Select(item => new QuestJournalResourceItem(
                    item.Id.ToString(), item.SchemaVersion, item.Provenance, item.Slug, item.Title.Key,
                    item.Title.Fallback, item.Description.Key, item.Category, item.Priority, item.Owner,
                    item.RequiredAtRuntime, item.InstancePolicy.DefaultScope.ToString(),
                    item.InstancePolicy.AllowedScopes.Select(scope => scope.ToString()).ToArray(),
                    item.States.OrderBy(state => state.DisplayOrder).ThenBy(state => state.Id.Value)
                        .Select(state => new QuestJournalStateResource(
                            state.Id.ToString(), state.SchemaVersion, state.Provenance, state.Slug,
                            state.DisplayName, state.DisplayOrder, state.LegacyJournalIndex,
                            state.TerminalKind.ToString(), state.Reactivation.ToString(),
                            state.Journal.Summary.Key, state.Journal.Summary.Fallback,
                            state.Journal.Details.Key, state.Journal.Objectives.Count,
                            state.Journal.ContextLinks.Count)).ToArray(),
                    item.Transitions.OrderBy(transition => transition.DisplayOrder)
                        .ThenBy(transition => transition.Id.Value)
                        .Select(transition => new QuestTransitionResource(
                            transition.Id.ToString(), transition.SchemaVersion, transition.Provenance,
                            transition.FromStateId.ToString(), transition.ToStateId.ToString(),
                            transition.DisplayOrder, transition.EventBinding.Kind.ToString(),
                            transition.EventBinding.EventName, transition.Priority,
                            transition.Idempotence.ToString(), transition.IdempotenceKey,
                            transition.ForgeScriptApiVersion, transition.Conditions.Count,
                            transition.Actions.Count)).ToArray(),
                    item.Simulation.Events.Count, item.Simulation.MaximumSteps,
                    QuestNarrativeValidator.Validate(item).Select(diagnostic =>
                        new AgentDiagnostic(diagnostic.Code, diagnostic.Message, diagnostic.Path)).ToArray(),
                    ForgeProjectQuestResourceMapper.MapAuthoring(item.Authoring),
                    ForgeProjectQuestResourceMapper.MapExperience(item.Experience),
                    ForgeProjectQuestResourceMapper.MapQuality(item.Quality)))
                .ToArray();
            return new(1, _history.State.Revision.Value, filter, items);
        }
    }

    public AnchorResource QueryAnchors(string? areaIdOrSearch = null, int limit = 100)
    {
        if (limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(limit));
        lock (_gate)
        {
            var filter = areaIdOrSearch?.Trim();
            var items = _history.State.Project.Areas
                .SelectMany(area => area.Navigation.Anchors.Instances.Select(instance =>
                    (Area: area, Catalog: area.Navigation.Anchors, Instance: instance,
                        Definition: area.Navigation.Anchors.Definitions.SingleOrDefault(definition =>
                            definition.Id == instance.DefinitionId))))
                .Where(item => item.Definition is not null)
                .Where(item => string.IsNullOrWhiteSpace(filter) ||
                    string.Equals(item.Area.Id.ToString(), filter, StringComparison.OrdinalIgnoreCase) ||
                    item.Area.Slug.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    item.Definition!.Slug.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    item.Definition.Label.Fallback.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
                    item.Definition.Tags.Any(tag => tag.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(item => item.Area.Slug, StringComparer.Ordinal)
                .ThenBy(item => item.Instance.DisplayOrder).ThenBy(item => item.Instance.Id).Take(limit)
                .Select(item => new AnchorResourceItem(item.Area.Id.ToString(), item.Catalog.Id.ToString("D"),
                    item.Instance.Id.ToString("D"), item.Definition!.Id.ToString("D"), item.Definition.Slug,
                    item.Definition.Label.Key, item.Definition.Label.Fallback, item.Definition.Kind.ToString(),
                    item.Definition.Scope.ToString(), item.Definition.Tags.Order(StringComparer.Ordinal).ToArray(),
                    item.Instance.Transform.Position.X, item.Instance.Transform.Position.Y,
                    item.Instance.Transform.Position.Z, item.Instance.Spawn is not null,
                    item.Instance.Portal is not null, item.Definition.RequiredAtRuntime)).ToArray();
            return new(1, _history.State.Revision.Value, filter, items);
        }
    }

    public WaypointAuthoringResource QueryWaypoints(string? areaIdOrSearch = null, int limit = 100)
    {
        if (limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(limit));
        lock (_gate)
        {
            var filter = areaIdOrSearch?.Trim();
            static bool MatchesWaypoint(WaypointProfile item, string value) =>
                item.Id.ToString("D").Equals(value, StringComparison.OrdinalIgnoreCase) ||
                item.Name.Contains(value, StringComparison.OrdinalIgnoreCase) ||
                item.Appearance.Layer.Contains(value, StringComparison.OrdinalIgnoreCase) ||
                item.Marker?.Text.Contains(value, StringComparison.CurrentCultureIgnoreCase) == true;

            var areas = _history.State.Project.Areas.OrderBy(area => area.Slug, StringComparer.Ordinal).ToArray();
            var waypoints = areas.SelectMany(area =>
                {
                    var areaMatches = !string.IsNullOrWhiteSpace(filter) &&
                        (string.Equals(area.Id.ToString(), filter, StringComparison.OrdinalIgnoreCase) ||
                         area.Slug.Contains(filter, StringComparison.OrdinalIgnoreCase));
                    return area.Navigation.Waypoints.Waypoints
                        .Where(item => string.IsNullOrWhiteSpace(filter) || areaMatches || MatchesWaypoint(item, filter))
                        .Select(item => (Area: area, Item: item));
                })
                .Select(match =>
                {
                    var area = match.Area;
                    var item = match.Item;
                    return
                    new WaypointResourceItem(area.Id.ToString(), area.Navigation.Waypoints.Id.ToString("D"),
                        item.Id.ToString("D"), item.Name, item.Position.X, item.Position.Y, item.Position.Z,
                        item.Appearance.Icon, item.Appearance.Color, item.Appearance.Layer,
                        item.Appearance.Visible, item.Appearance.ScreenSize, item.Marker?.Text,
                        item.Marker?.Icon, item.Marker?.VisibilityPolicy.ToString(),
                        item.Marker?.VisibilityScope.ToString(), item.Marker?.Tracked ?? false,
                        item.Marker?.Revealed ?? false, item.RequiredAtRuntime,
                        item.Arrival?.Facing.ToString(), item.Arrival?.WaitSeconds,
                        item.Arrival?.Animation, item.Arrival?.Interaction, item.Arrival?.Callback);
                })
                .Take(limit).ToArray();
            var routes = areas.SelectMany(area =>
                {
                    var areaMatches = !string.IsNullOrWhiteSpace(filter) &&
                        (string.Equals(area.Id.ToString(), filter, StringComparison.OrdinalIgnoreCase) ||
                         area.Slug.Contains(filter, StringComparison.OrdinalIgnoreCase));
                    var matchingWaypointIds = area.Navigation.Waypoints.Waypoints
                        .Where(item => !string.IsNullOrWhiteSpace(filter) && MatchesWaypoint(item, filter))
                        .Select(item => item.Id).ToHashSet();
                    return area.Navigation.Waypoints.PatrolRoutes
                        .Where(item => string.IsNullOrWhiteSpace(filter) || areaMatches ||
                            item.Id.ToString("D").Equals(filter, StringComparison.OrdinalIgnoreCase) ||
                            item.Points.Any(point => matchingWaypointIds.Contains(point.Id)))
                        .Select(item =>
                        {
                            var profile = area.Navigation.Waypoints;
                            var knownIds = profile.Waypoints.Select(waypoint => waypoint.Id)
                                .Concat(profile.Locators.Select(locator => locator.Id)).ToHashSet();
                            var segments = WaypointRouteService.AnalyzeRoute(item, knownPointIds: knownIds,
                                    costPerMeter: profile.RouteVisualization.CostPerMeter)
                                .Select(segment => new RouteSegmentResourceItem(segment.FromId.ToString("D"),
                                    segment.ToId.ToString("D"), segment.Direction, segment.Distance,
                                    segment.Cost, segment.TravelSeconds, segment.Action,
                                    segment.BrokenLink, segment.NavProjected)).ToArray();
                            return new PatrolRouteResourceItem(area.Id.ToString(), profile.Id.ToString("D"),
                                item.Id.ToString("D"), item.Traversal.ToString(), item.Points.Count,
                                item.Edges.Count, item.ScriptHandler, item.RequiredAtRuntime, segments);
                        });
                })
                .Take(limit).ToArray();
            var locators = areas.SelectMany(area =>
                {
                    var areaMatches = !string.IsNullOrWhiteSpace(filter) &&
                        (string.Equals(area.Id.ToString(), filter, StringComparison.OrdinalIgnoreCase) ||
                         area.Slug.Contains(filter, StringComparison.OrdinalIgnoreCase));
                    return area.Navigation.Waypoints.Locators.Where(item =>
                            string.IsNullOrWhiteSpace(filter) || areaMatches ||
                            item.Id.ToString("D").Equals(filter, StringComparison.OrdinalIgnoreCase) ||
                            item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                            item.Purpose.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                            item.Tags.Any(tag => tag.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                        .Select(item => new LocatorResourceItem(area.Id.ToString(),
                            area.Navigation.Waypoints.Id.ToString("D"), item.Id.ToString("D"),
                            item.Name, item.Purpose.ToString(), item.Position.X, item.Position.Y,
                            item.Position.Z, item.Tags.Order(StringComparer.Ordinal).ToArray(),
                            item.AssetId, item.BlueprintId?.ToString(), item.Arrival?.Facing.ToString(),
                            item.RequiredAtRuntime));
                }).Take(limit).ToArray();
            var locatorIds = locators.Select(item => Guid.Parse(item.LocatorId)).ToHashSet();
            var collections = areas.SelectMany(area =>
                {
                    var areaMatches = !string.IsNullOrWhiteSpace(filter) &&
                        (string.Equals(area.Id.ToString(), filter, StringComparison.OrdinalIgnoreCase) ||
                         area.Slug.Contains(filter, StringComparison.OrdinalIgnoreCase));
                    return area.Navigation.Waypoints.Collections.Where(item =>
                            string.IsNullOrWhiteSpace(filter) || areaMatches ||
                            item.Id.ToString("D").Equals(filter, StringComparison.OrdinalIgnoreCase) ||
                            item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                            item.Query.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                            item.LocatorIds.Any(locatorIds.Contains))
                        .Select(item => new AnchorCollectionResourceItem(area.Id.ToString(),
                            area.Navigation.Waypoints.Id.ToString("D"), item.Id.ToString("D"),
                            item.Name, item.Query, item.Purpose?.ToString(),
                            (item.RequiredTags ?? Array.Empty<string>()).Order(StringComparer.Ordinal).ToArray(),
                            item.Selection.ToString(), item.LocatorIds.Count, item.Seed,
                            item.RequiredAtRuntime));
                }).Take(limit).ToArray();
            var visualizations = areas.Where(area => string.IsNullOrWhiteSpace(filter) ||
                    string.Equals(area.Id.ToString(), filter, StringComparison.OrdinalIgnoreCase) ||
                    area.Slug.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    area.Navigation.Waypoints.PatrolRoutes.Any(route =>
                        route.Id.ToString("D").Equals(filter, StringComparison.OrdinalIgnoreCase)))
                .Take(limit).Select(area =>
                {
                    var profile = area.Navigation.Waypoints;
                    var view = profile.RouteVisualization;
                    return new RouteVisualizationResourceItem(area.Id.ToString(), profile.Id.ToString("D"),
                        view.Id.ToString("D"), view.ShowSegments, view.ShowDirection, view.ShowCosts,
                        view.ShowActions, view.ShowBrokenLinks, view.ShowNavigationProjection,
                        view.ShowTiming, view.CostPerMeter, view.RequiredAtRuntime);
                }).ToArray();
            return new(1, _history.State.Revision.Value, filter, waypoints, routes, locators,
                collections, visualizations);
        }
    }

    public WaypointAuthoringPlanResult PreviewWaypointAuthoring(WaypointAuthoringPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (!WaypointJsonBoundaryMapper.TryMap(
                    _history.State.Project, request, out var mapped, out var diagnostic))
                return WaypointFailure(request.BaseRevision, diagnostic!);
            return PreviewWaypointAuthoringCore(mapped!, storePlan: false);
        }
    }

    public WaypointAuthoringPlanResult PreviewWaypointAuthoringDomain(
        WaypointAuthoringDomainPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            return PreviewWaypointAuthoringCore(request, storePlan: false);
        }
    }

    public WaypointAuthoringPlanResult PlanWaypointAuthoring(WaypointAuthoringPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (!WaypointJsonBoundaryMapper.TryMap(
                    _history.State.Project, request, out var mapped, out var diagnostic))
                return WaypointFailure(request.BaseRevision, diagnostic!);
            return PreviewWaypointAuthoringCore(mapped!, storePlan: true);
        }
    }

    public WaypointAuthoringPlanResult PlanWaypointAuthoringDomain(
        WaypointAuthoringDomainPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            return PreviewWaypointAuthoringCore(request, storePlan: true);
        }
    }

    public WaypointAuthoringPlanResult ApplyWaypointAuthoringPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_waypointPlans.TryGetValue(planId, out var plan))
                return WaypointFailure(_history.State.Revision.Value, "waypoint.plan_missing",
                    $"Waypoint plan '{planId}' does not exist.");
            if (plan.Applied && !plan.Reverted)
                return WaypointFailure(plan.Command.BaseRevision.Value, "waypoint.plan_already_applied",
                    $"Waypoint plan '{planId}' is already applied.");
            if (plan.Reverted)
                return WaypointFailure(plan.Command.BaseRevision.Value, "waypoint.plan_requires_redo",
                    $"Waypoint plan '{planId}' was reverted and must be redone.");

            var command = plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
            var result = _navigationTopologyHandler.Handle(_history.State, command);
            if (!result.Succeeded)
            {
                plan.Audit.Add(new(planId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return ToWaypointResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = result.Diagnostics.Select(Map).ToArray(),
                };
            }
            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
            {
                plan.Audit.Add(new(planId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return ToWaypointResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = saved.Diagnostics.Select(item =>
                        new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
                };
            }
            _history.Record(result);
            plan.Applied = true;
            plan.Reverted = false;
            plan.AppliedRevision = result.NewRevision!.Value.Value;
            plan.Audit.Add(new(planId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToWaypointResult(plan);
        }
    }

    public WaypointAuthoringPlanResult RevertWaypointAuthoringPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_waypointPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return WaypointFailure(_history.State.Revision.Value, "waypoint.plan_not_applied",
                    $"Waypoint plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _history.State.Revision.Value)
                return WaypointFailure(plan.Command.BaseRevision.Value, "waypoint.revert_revision_conflict",
                    "Only the latest project mutation can be reverted safely.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return WaypointFailure(plan.Command.BaseRevision.Value, "waypoint.inverse_unavailable",
                    "The waypoint inverse is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return WaypointFailure(plan.Command.BaseRevision.Value, "waypoint.revert_save_failed",
                    "The reverted waypoint project could not be saved.");
            }
            plan.Reverted = true;
            plan.RevertedRevision = undone.State.Revision.Value;
            plan.Audit.Add(new(planId, "revert", plan.RevertedRevision.Value, _clock.UtcNow, true));
            return ToWaypointResult(plan);
        }
    }

    public WaypointAuthoringPlanResult RedoWaypointAuthoringPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_waypointPlans.TryGetValue(planId, out var plan) || !plan.Applied || !plan.Reverted)
                return WaypointFailure(_history.State.Revision.Value, "waypoint.plan_not_reverted",
                    $"Waypoint plan '{planId}' is not currently reverted.");
            if (plan.RevertedRevision != _history.State.Revision.Value)
                return WaypointFailure(plan.Command.BaseRevision.Value, "waypoint.redo_revision_conflict",
                    "Only the latest reverted project mutation can be redone safely.");
            var redone = _history.Redo();
            if (!redone.Succeeded)
                return WaypointFailure(plan.Command.BaseRevision.Value, "waypoint.redo_unavailable",
                    "The waypoint redo is unavailable.");
            var saved = _session.Save(redone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Undo();
                return WaypointFailure(plan.Command.BaseRevision.Value, "waypoint.redo_save_failed",
                    "The redone waypoint project could not be saved.");
            }
            plan.Reverted = false;
            plan.AppliedRevision = redone.State.Revision.Value;
            plan.Audit.Add(new(planId, "redo", plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToWaypointResult(plan);
        }
    }

    private WaypointAuthoringPlanResult PreviewWaypointAuthoringCore(
        WaypointAuthoringDomainPlanRequest request,
        bool storePlan)
    {
        if (request.SchemaVersion != 1)
            return WaypointFailure(request.BaseRevision, "waypoint.mcp_schema_unsupported",
                "Waypoint MCP request schema must be v1.", "request/schemaVersion");
        if (!Guid.TryParse(request.AreaId, out var parsed) || parsed == Guid.Empty)
            return WaypointFailure(request.BaseRevision, "waypoint.mcp_area_id_invalid",
                "Area ID must be a non-empty UUID.", "request/areaId");
        var areaId = AreaId.From(parsed);
        var area = _history.State.Project.Areas.SingleOrDefault(item => item.Id == areaId);
        if (area is null || request.Profile.AreaId != areaId)
            return WaypointFailure(request.BaseRevision, "waypoint.mcp_area_missing",
                "Area must exist and match the canonical waypoint profile.", "request/areaId");
        var command = new SetNavigationTopologyCommand(
            CommandId.From(Guid.NewGuid()),
            new ProjectRevision(request.BaseRevision),
            [new AreaNavigationTopologyUpdate(areaId, area.Navigation.WithWaypoints(request.Profile))],
            [],
            null,
            true);
        var result = _navigationTopologyHandler.Handle(_history.State, command);
        var diagnostics = result.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message,
            $"areas/{request.AreaId}/navigation/waypoints")).ToArray();
        var patch = result.Patch is null ? null : new ProjectPatch(
            result.Patch.Operation,
            $"areas/{request.AreaId}/navigation/waypoints",
            $"Configure {request.Profile.Waypoints.Count} waypoint(s), " +
            $"{request.Profile.PatrolRoutes.Count} patrol route(s), " +
            $"{request.Profile.Locators.Count} locator(s) and " +
            $"{request.Profile.Collections.Count} collection(s) through canonical navigation topology.");
        var inverse = result.Inverse is null ? null : new ProjectPatch(
            result.Inverse.Operation,
            $"areas/{request.AreaId}/navigation/waypoints",
            "Restore the previous canonical waypoint authoring profile.");
        if (!result.Succeeded || !storePlan)
            return new(result.Succeeded, null, request.BaseRevision, patch, inverse,
                diagnostics, false, false, []);

        var planId = Guid.NewGuid().ToString("D");
        var plan = new StoredWaypointPlan(planId, command, patch!, inverse!,
            [new WaypointPlanAuditEntry(planId, "planned", request.BaseRevision, _clock.UtcNow, true)]);
        _waypointPlans.Add(planId, plan);
        return ToWaypointResult(plan);
    }

    private static WaypointAuthoringPlanResult WaypointFailure(
        long revision,
        AgentDiagnostic diagnostic) =>
        new(false, null, revision, null, null, [diagnostic], false, false, []);

    private static WaypointAuthoringPlanResult WaypointFailure(
        long revision,
        string code,
        string message,
        string path = "navigation/waypoints") =>
        WaypointFailure(revision, new AgentDiagnostic(code, message, path));

    private static WaypointAuthoringPlanResult ToWaypointResult(StoredWaypointPlan plan) =>
        new(true, plan.PlanId, plan.Command.BaseRevision.Value, plan.Diff, plan.Inverse, [],
            plan.Applied, plan.Reverted, plan.Audit.ToArray());

    public AnchorPlanResult PreviewAnchors(AnchorPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return new(false, request.BaseRevision, null, null,
                    [new AgentDiagnostic("anchor.mcp_schema_unsupported",
                        "Anchor MCP request schema must be v1.", "request/schemaVersion")]);
            if (!Guid.TryParse(request.AreaId, out var parsed) || parsed == Guid.Empty)
                return new(false, request.BaseRevision, null, null,
                    [new AgentDiagnostic("anchor.mcp_area_id_invalid",
                        "Area ID must be a non-empty UUID.", "request/areaId")]);
            var areaId = AreaId.From(parsed);
            var area = _history.State.Project.Areas.SingleOrDefault(item => item.Id == areaId);
            if (area is null || request.Catalog.AreaId != areaId)
                return new(false, request.BaseRevision, null, null,
                    [new AgentDiagnostic("anchor.mcp_area_missing",
                        "Area must exist and match the canonical anchor catalog.", "request/areaId")]);
            var navigation = area.Navigation.WithAnchors(request.Catalog);
            var result = _areaNavigationHandler.Handle(_history.State, new SetAreaNavigationCommand(
                CommandId.From(Guid.NewGuid()), new ProjectRevision(request.BaseRevision), areaId,
                navigation, true));
            return new(result.Succeeded, request.BaseRevision, result.Patch, result.Inverse,
                result.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message,
                    $"areas/{request.AreaId}/navigation/anchors")).ToArray());
        }
    }

    public QuestJournalPlanResult PreviewQuestJournalDomain(QuestJournalDomainPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return new(false, request.BaseRevision, null, null,
                    [new AgentDiagnostic("quest.journal.mcp_schema_unsupported",
                        "Quest-journal MCP request schema must be v1.", "request/schemaVersion")]);
            if (!Guid.TryParse(request.QuestId, out var parsed) || parsed == Guid.Empty ||
                request.Quest.Id.Value != parsed)
                return new(false, request.BaseRevision, null, null,
                    [new AgentDiagnostic("quest.journal.mcp_id_invalid",
                        "Quest ID must be a non-empty UUID matching the canonical definition.",
                        "request/questId")]);
            var result = _questJournalHandler.Handle(_history.State, new SetQuestCommand(
                CommandId.From(Guid.NewGuid()), new ProjectRevision(request.BaseRevision), request.Quest, true));
            return new(result.Succeeded, request.BaseRevision, result.Patch, result.Inverse,
                result.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message,
                    $"quests/{request.QuestId}")).ToArray());
        }
    }

    public QuestJournalPlanResult PreviewQuestJournal(QuestJournalPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (!QuestJsonBoundaryMapper.TryMapJournal(
                    _history.State.Project, request, out var mapped, out var diagnostic))
                return new(false, request.BaseRevision, null, null, [diagnostic!]);
            return PreviewQuestJournalDomain(mapped!);
        }
    }

    public QuestSimulationResource SimulateQuestJournal(QuestSimulationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return SimulationFailure(request.QuestId, "quest.simulation.mcp_schema_unsupported",
                    "Quest-simulation MCP request schema must be v1.", "request/schemaVersion");
            if (!Guid.TryParse(request.QuestId, out var parsed) || parsed == Guid.Empty)
                return SimulationFailure(request.QuestId, "quest.simulation.mcp_id_invalid",
                    "Quest ID must be a non-empty UUID.", "request/questId");
            if (!Enum.TryParse<QuestProgressScope>(request.Scope, true, out var scope))
                return SimulationFailure(request.QuestId, "quest.simulation.mcp_scope_invalid",
                    "Quest scope must be Player, Party or World.", "request/scope");
            var quest = _history.State.Project.Quests.SingleOrDefault(item => item.Id.Value == parsed);
            if (quest is null)
                return SimulationFailure(request.QuestId, "quest.simulation.mcp_quest_missing",
                    "Quest does not exist in the current project.", $"quests/{request.QuestId}");
            try
            {
                var result = QuestSimulationEngine.Run(quest, scope, request.ScopeOwnerId, request.StartedAt,
                    request.AuthorityGranted);
                return new(true, request.QuestId, result.ScenarioId.ToString("D"), result.Status.ToString(),
                    result.Instance.CurrentStateId.ToString(), result.Steps.Count, result.Audit.Count,
                    result.Instance.Variables,
                    result.Steps.Select(step => new QuestSimulationStepResource(step.Id.ToString("D"),
                        step.Event.Id.ToString("D"), step.Event.EventName, step.BeforeStateId.ToString(),
                        step.AfterStateId.ToString(), step.TransitionId?.ToString(), step.Effects.Count,
                        step.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path))
                            .ToArray())).ToArray(),
                    result.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path))
                        .ToArray());
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                return SimulationFailure(request.QuestId, "quest.simulation.mcp_rejected", exception.Message,
                    $"quests/{request.QuestId}/simulation");
            }
        }
    }

    private static QuestSimulationResource SimulationFailure(string questId, string code, string message,
        string path) => new(false, questId, null, null, null, 0, 0,
        new Dictionary<string, string>(), [], [new AgentDiagnostic(code, message, path)]);

    public QuestOperationsPlanResult PreviewQuestOperations(QuestOperationsPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return new(false, request.BaseRevision, null, null,
                    [new AgentDiagnostic("quest.operations.mcp_schema_unsupported",
                        "Quest-operations MCP request schema must be v1.", "request/schemaVersion")]);
            if (!Guid.TryParse(request.QuestId, out var parsed) || parsed == Guid.Empty)
                return new(false, request.BaseRevision, null, null,
                    [new AgentDiagnostic("quest.operations.mcp_id_invalid", "Quest ID must be a non-empty UUID.",
                        "request/questId")]);
            var result = _questOperationsHandler.Handle(_history.State, new SetQuestOperationsCommand(
                CommandId.From(Guid.NewGuid()), new ProjectRevision(request.BaseRevision), QuestId.From(parsed),
                request.Profile, true));
            return new(result.Succeeded, request.BaseRevision, result.Patch, result.Inverse,
                result.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message,
                    $"quests/{request.QuestId}/operations")).ToArray());
        }
    }

    public ItemAdvancedResource QueryAdvancedItems(string? blueprintIdOrSearch = null, int limit = 50)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        lock (_gate)
        {
            var filter = blueprintIdOrSearch?.Trim();
            var items = _history.State.Project.Blueprints.Where(item => item.ItemAdvanced is not null)
                .Where(item => string.IsNullOrWhiteSpace(filter) ||
                    string.Equals(item.Id.ToString(), filter, StringComparison.OrdinalIgnoreCase) ||
                    item.Slug.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    item.DisplayName.Contains(filter, StringComparison.CurrentCultureIgnoreCase))
                .OrderBy(item => item.Slug, StringComparer.Ordinal).Take(limit)
                .Select(item =>
                {
                    var profile = item.ItemAdvanced!;
                    var requirements = profile.Requirements.Skills.Count + profile.Requirements.Traits.Count +
                                       profile.Requirements.Factions.Count +
                                       (profile.Requirements.MinimumLevel > 0 ? 1 : 0) +
                                       (profile.Requirements.MinimumLore > 0 ? 1 : 0) +
                                       (profile.Requirements.ScriptCondition is null ? 0 : 1);
                    return new ItemAdvancedResourceItem(item.Id.ToString(), item.Slug, item.DisplayName,
                        profile.Id.ToString("D"), profile.Economy.BaseCost, requirements,
                        profile.Identification.Levels.Count, profile.OnHitProcs.Count, profile.Weapon is not null,
                        profile.Armor is not null, profile.Ammo is not null, profile.Durability?.Enabled == true,
                        profile.Container is not null, profile.LootContexts.Count, profile.Unique.Enabled,
                        profile.Recipes.Count, profile.Generator?.Enabled == true, profile.RequiredAtRuntime);
                }).ToArray();
            return new(1, _history.State.Revision.Value, filter, items);
        }
    }

    public ItemAdvancedPlanResult PreviewAdvancedItem(ItemAdvancedPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return new(false, request.BaseRevision, null, null,
                    [new AgentDiagnostic("item.advanced.mcp_schema_unsupported",
                        "Advanced-item MCP request schema must be v1.", "request/schemaVersion")]);
            BlueprintId blueprintId;
            try { blueprintId = BlueprintId.Parse(request.BlueprintId); }
            catch (Exception exception) when (exception is FormatException or ArgumentException)
            {
                return new(false, request.BaseRevision, null, null,
                    [new AgentDiagnostic("item.advanced.mcp_blueprint_invalid", exception.Message,
                        "request/blueprintId")]);
            }
            var result = _itemAdvancedHandler.Handle(_history.State, new SetItemAdvancedProfileCommand(
                CommandId.From(Guid.NewGuid()), new ProjectRevision(request.BaseRevision), blueprintId,
                request.Profile, true));
            return new(result.Succeeded, request.BaseRevision, result.Patch, result.Inverse,
                result.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message,
                    $"blueprints/{blueprintId}/itemAdvanced")).ToArray());
        }
    }

    public ItemFoundationResource QueryItemFoundations(string? blueprintIdOrSearch = null, int limit = 50)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        lock (_gate)
        {
            var filter = blueprintIdOrSearch?.Trim();
            var items = _history.State.Project.Blueprints
                .Where(item => item.ItemFoundation is not null)
                .Where(item => string.IsNullOrWhiteSpace(filter) ||
                    string.Equals(item.Id.ToString(), filter, StringComparison.OrdinalIgnoreCase) ||
                    item.Slug.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    item.DisplayName.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
                    item.ItemFoundation!.Type.Slug.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.Slug, StringComparer.Ordinal).Take(limit)
                .Select(item =>
                {
                    var profile = item.ItemFoundation!;
                    return new ItemFoundationResourceItem(item.Id.ToString(), item.Slug, item.DisplayName,
                        profile.Id.ToString("D"), profile.SchemaVersion, profile.Provenance, profile.RequiredAtRuntime,
                        profile.TypeId.ToString("D"), profile.Type.Slug, profile.DefinitionVersion,
                        profile.Texts.Select(text => text.Locale).Order(StringComparer.Ordinal).ToArray(),
                        profile.Slots.Order(StringComparer.Ordinal).ToArray(),
                        profile.AllowedInstanceOverrides.Order(StringComparer.Ordinal).ToArray(),
                        profile.Creation is null ? null : new ItemCreationResource(
                            profile.Creation.Id.ToString("D"), profile.Creation.RequiredAtRuntime,
                            profile.Creation.Wizard.GenerationRules.Order(StringComparer.Ordinal).ToArray(),
                            profile.Creation.Appearance.MeshAssetIds.Order(StringComparer.Ordinal).ToArray(),
                            profile.Creation.Appearance.MaterialAssetIds.Order(StringComparer.Ordinal).ToArray(),
                            profile.Creation.Appearance.Sockets.Order(StringComparer.Ordinal).ToArray(),
                            profile.Creation.Appearance.IconAssetId,
                            profile.Creation.Appearance.VisualEffectAssetIds.Order(StringComparer.Ordinal).ToArray(),
                            profile.Creation.Appearance.EquippedPreviewAssetId,
                            profile.Creation.Inventory.Mass, profile.Creation.Inventory.Volume,
                            profile.Creation.Inventory.GridWidth, profile.Creation.Inventory.GridHeight,
                            profile.Creation.Inventory.StackMassFactor, profile.Creation.Inventory.StackVolumeFactor,
                            profile.Creation.Stack.Maximum, profile.Creation.Stack.CompatibilityKey,
                            profile.Creation.Stack.Replicated, profile.Creation.Usage.TotalCharges,
                            profile.Creation.Usage.CurrentCharges, profile.Creation.Usage.UsesPerRest,
                            profile.Creation.Usage.UsesRemaining, profile.Creation.Usage.CooldownMilliseconds,
                            profile.Creation.Usage.Durability, profile.Creation.Usage.Consumable,
                            profile.Creation.Usage.RechargeRule),
                        profile.Gameplay is null ? null : new ItemGameplayResource(
                            profile.Gameplay.Id.ToString("D"), profile.Gameplay.RequiredAtRuntime,
                            new Dictionary<string, bool>(StringComparer.Ordinal)
                            {
                                ["destruction"] = profile.Gameplay.Critical.ProtectDestruction,
                                ["sale"] = profile.Gameplay.Critical.ProtectSale,
                                ["drop"] = profile.Gameplay.Critical.ProtectDrop,
                                ["theft"] = profile.Gameplay.Critical.ProtectTheft,
                                ["consumption"] = profile.Gameplay.Critical.ProtectConsumption,
                                ["cleanup"] = profile.Gameplay.Critical.ProtectCleanup,
                            }, profile.Gameplay.Critical.AllowAuditedGameMasterOverride,
                            profile.Gameplay.Transfer.AllowedDestinations.ToDictionary(
                                entry => entry.Key.ToString(), entry => entry.Value, StringComparer.Ordinal),
                            profile.Gameplay.Transfer.RequiredPermissions.Order(StringComparer.Ordinal).ToArray(),
                            profile.Gameplay.Modifiers.Select(entry => entry.Kind).Order(StringComparer.Ordinal).ToArray(),
                            profile.Gameplay.Ability?.AbilityDefinitionId.ToString("D"),
                            profile.Gameplay.Equipment.SlotDefinitions.Select(entry => entry.Id)
                                .Order(StringComparer.Ordinal).ToArray(),
                            profile.Gameplay.Equipment.RequiredSlots.Order(StringComparer.Ordinal).ToArray()),
                        profile.Operations is null ? null : new ItemOperationsResource(
                            profile.Operations.Id.ToString("D"), profile.Operations.RequiredAtRuntime,
                            profile.Operations.Loot.Id.ToString("D"), profile.Operations.Loot.Entries.Count,
                            profile.Operations.Inspector.Revision,
                            profile.Operations.Inspector.Placements.Count,
                            profile.Operations.Events.Bindings.Select(entry => entry.Kind.ToString())
                                .Order(StringComparer.Ordinal).ToArray()));
                }).ToArray();
            return new(1, _history.State.Revision.Value, filter, items);
        }
    }

    public ItemFoundationPlanResult PreviewItemFoundation(ItemFoundationPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return new(false, request.BaseRevision, null, null,
                    [new AgentDiagnostic("item.foundation.mcp_schema_unsupported",
                        "Item-foundation MCP request schema must be v1.", "request/schemaVersion")]);
            BlueprintId blueprintId;
            try
            {
                blueprintId = BlueprintId.Parse(request.BlueprintId);
            }
            catch (Exception exception) when (exception is FormatException or ArgumentException)
            {
                return new(false, request.BaseRevision, null, null,
                    [new AgentDiagnostic("item.foundation.mcp_blueprint_invalid", exception.Message,
                        "request/blueprintId")]);
            }
            var result = _itemFoundationHandler.Handle(_history.State, new SetItemFoundationCommand(
                CommandId.From(Guid.NewGuid()), new ProjectRevision(request.BaseRevision), blueprintId,
                request.Profile, request.AcceptTypeMigration, DryRun: true));
            return new(result.Succeeded, request.BaseRevision, result.Patch, result.Inverse,
                result.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message,
                    $"blueprints/{blueprintId}/itemFoundation")).ToArray());
        }
    }

    public CreatureFoundationResource QueryCreatureFoundations(string? blueprintIdOrSearch = null, int limit = 50)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        lock (_gate)
        {
            var filter = blueprintIdOrSearch?.Trim();
            var items = _history.State.Project.Blueprints
                .Where(item => item.CreatureFoundation is not null)
                .Where(item => string.IsNullOrWhiteSpace(filter) ||
                    string.Equals(item.Id.ToString(), filter, StringComparison.OrdinalIgnoreCase) ||
                    item.Slug.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    item.DisplayName.Contains(filter, StringComparison.CurrentCultureIgnoreCase))
                .OrderBy(item => item.Slug, StringComparer.Ordinal)
                .Take(limit)
                .Select(item =>
                {
                    var profile = item.CreatureFoundation!;
                    var authoring = profile.AuthoringComposition;
                    var rules = profile.RulesComposition;
                    var interaction = profile.InteractionComposition;
                    var eventAi = profile.EventAiComposition;
                    return new CreatureFoundationResourceItem(item.Id.ToString(), item.Slug, item.DisplayName,
                        profile.Id.ToString("D"), profile.SchemaVersion, profile.Provenance, profile.RequiredAtRuntime,
                        profile.Movement.Mode.ToString(),
                        profile.Resources.ToDictionary(resource => resource.ResourceId, resource => resource.Current,
                            StringComparer.Ordinal), profile.Spawned.EventName, profile.Spawned.Handler,
                        profile.AuthoringCompositionRequiredAtRuntime, authoring?.EncounterRole.ToString(),
                        authoring?.ThreatRating, authoring?.Species.SpeciesId, authoring?.Appearance.MeshAssetId,
                        authoring?.AnimationGraph.Id.ToString("D"), authoring?.AnimationGraph.States.Count ?? 0,
                        profile.RulesCompositionRequiredAtRuntime, rules?.InitialState.LifeState.ToString(),
                        rules?.InitialState.PoseId,
                        rules is null ? 0 : rules.Attributes.BaseValues.Count + rules.Attributes.Derived.Count,
                        rules?.Defenses.Avoidance, rules?.Defenses.Armor, rules?.Abilities.Count ?? 0,
                        rules?.Inventory.Containers.Sum(container =>
                            container.Items.Sum(entry => entry.Quantity)) ?? 0,
                        profile.InteractionCompositionRequiredAtRuntime,
                        interaction?.Factions.PrimaryFactionId.ToString("D"),
                        interaction?.Factions.Memberships.Count ?? 0,
                        interaction?.Perception.Senses.Count ?? 0,
                        interaction?.DialogueBindings.Count ?? 0,
                        interaction?.Protection.Invulnerable ?? false,
                        interaction?.Protection.UsesNarrativeDeath ?? false,
                        interaction?.Protection.DialogueInterruptible ?? true,
                        interaction?.OnConversation.Handler,
                        profile.EventAiCompositionRequiredAtRuntime,
                        eventAi?.Controller.Kind.ToString(), eventAi?.Controller.DefinitionId,
                        eventAi?.Controller.Blackboard.Count ?? 0, eventAi?.Controller.AllowedActions.Count ?? 0,
                        eventAi?.Blocked.RecoveryOrder.Select(strategy => strategy.ToString()).ToArray(),
                        eventAi?.Perception.Detected.Handler, eventAi?.Perception.Lost.Handler,
                        eventAi?.PhysicalAttack.Declared.Handler, eventAi?.PhysicalAttack.Resolved.Handler,
                        eventAi?.Ability.Targeted.Handler, eventAi?.Ability.Impacted.Handler,
                        eventAi?.Blocked.Blocked.Handler);
                }).ToArray();
            return new(1, _history.State.Revision.Value, filter, items);
        }
    }

    public CreatureFoundationPlanResult PreviewCreatureFoundation(CreatureFoundationPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return new(false, request.BaseRevision, null, null,
                    [new AgentDiagnostic("creature.foundation.mcp_schema_unsupported",
                        "Creature-foundation MCP request schema must be v1.", "request/schemaVersion")]);
            BlueprintId blueprintId;
            try
            {
                blueprintId = BlueprintId.Parse(request.BlueprintId);
            }
            catch (Exception exception) when (exception is FormatException or ArgumentException)
            {
                return new(false, request.BaseRevision, null, null,
                    [new AgentDiagnostic("creature.foundation.mcp_blueprint_invalid", exception.Message,
                        "request/blueprintId")]);
            }
            var result = _creatureFoundationHandler.Handle(_history.State, new SetCreatureFoundationCommand(
                CommandId.From(Guid.NewGuid()), new ProjectRevision(request.BaseRevision), blueprintId,
                request.Profile, DryRun: true));
            return new(result.Succeeded, request.BaseRevision, result.Patch, result.Inverse,
                result.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message,
                    $"blueprints/{blueprintId}/creatureFoundation")).ToArray());
        }
    }

    public CreatureEventResource QueryCreatureEvents(string? blueprintIdOrSearch = null, int limit = 50)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        lock (_gate)
        {
            var filter = blueprintIdOrSearch?.Trim();
            var items = _history.State.Project.Blueprints
                .Where(item => item.CreatureEvents is not null)
                .Where(item => string.IsNullOrWhiteSpace(filter) ||
                    string.Equals(item.Id.ToString(), filter, StringComparison.OrdinalIgnoreCase) ||
                    item.Slug.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    item.DisplayName.Contains(filter, StringComparison.CurrentCultureIgnoreCase))
                .OrderBy(item => item.Slug, StringComparer.Ordinal)
                .Take(limit)
                .Select(item =>
                {
                    var profile = item.CreatureEvents!;
                    return new CreatureEventResourceItem(
                        item.Id.ToString(), item.Slug, item.DisplayName, profile.Id.ToString("D"),
                        profile.SchemaVersion, profile.Provenance, profile.RequiredAtRuntime,
                        profile.CombatBoundary?.Mode.ToString(), CreatureEventValidator.EnumerateBindings(profile).Count,
                        profile.Heartbeat?.IntervalMilliseconds ?? 0, profile.Heartbeat?.BudgetMilliseconds ?? 0,
                        profile.Heartbeat?.SuspendOutsideInterest == true,
                        profile.CustomEvents.Select(custom => custom.QualifiedName).ToArray(),
                        profile.DamageDeath?.BeforeDamage.Handler,
                        profile.DamageDeath?.DamageApplied.Handler,
                        profile.DamageDeath?.Died.Handler,
                        profile.DamageDeath?.BeforeDamageBudgetMilliseconds ?? 0,
                        profile.DamageDeath?.MaximumAdjustmentRatio ?? 0);
                }).ToArray();
            return new CreatureEventResource(1, _history.State.Revision.Value, filter, items);
        }
    }

    public CreatureEventPlanResult PreviewCreatureEvents(CreatureEventPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return new(false, request.BaseRevision, null, null,
                    [new AgentDiagnostic("creature.events.mcp_schema_unsupported",
                        "Creature-event MCP request schema must be v1.", "request/schemaVersion")]);
            BlueprintId blueprintId;
            try
            {
                blueprintId = BlueprintId.Parse(request.BlueprintId);
            }
            catch (Exception exception) when (exception is FormatException or ArgumentException)
            {
                return new(false, request.BaseRevision, null, null,
                    [new AgentDiagnostic("creature.events.mcp_blueprint_invalid", exception.Message,
                        "request/blueprintId")]);
            }
            var result = _creatureEventsHandler.Handle(_history.State, new SetCreatureEventsCommand(
                CommandId.From(Guid.NewGuid()), new ProjectRevision(request.BaseRevision), blueprintId,
                request.Profile, DryRun: true));
            return new(result.Succeeded, request.BaseRevision, result.Patch, result.Inverse,
                result.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message,
                    $"blueprints/{blueprintId}/creatureEvents")).ToArray());
        }
    }

    public FactionResource QueryFactions(string? factionIdOrSearch = null, int limit = 50)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        lock (_gate)
        {
            var filter = factionIdOrSearch?.Trim();
            var catalog = _history.State.Project.Module.Factions;
            var profile = catalog.Advanced;
            var items = catalog.Factions
                .Where(item => string.IsNullOrWhiteSpace(filter) ||
                    string.Equals(item.Id.ToString("D"), filter, StringComparison.OrdinalIgnoreCase) ||
                    item.Slug.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    item.DisplayName.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
                    item.Tags.Any(tag => tag.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(item => item.Slug, StringComparer.Ordinal)
                .Take(limit)
                .Select(item =>
                {
                    var effective = FactionRuntimeResolver.EffectiveDefinition(catalog, item.Id);
                    var dynamic = profile?.DynamicFactions.SingleOrDefault(state => state.FactionId == item.Id);
                    return new FactionResourceItem(item.Id.ToString("D"), item.Slug, item.DisplayName,
                        item.ParentIds.Select(id => id.ToString("D")).ToArray(),
                        effective.Tags.OrderBy(tag => tag, StringComparer.Ordinal).ToArray(),
                        effective.Policies, profile?.Memberships.Count(member => member.FactionId == item.Id) ?? 0,
                        profile?.WitnessPolicies.Any(policy => policy.FactionId == item.Id) == true,
                        dynamic is not null, dynamic?.Kind.ToString(), dynamic?.Revision ?? 0);
                }).ToArray();
            var selectedIds = items.Select(item => Guid.Parse(item.FactionId)).ToHashSet();
            var relationships = catalog.Relationships.Where(item =>
                    selectedIds.Contains(item.SourceId) || selectedIds.Contains(item.TargetId))
                .OrderBy(item => item.SourceId).ThenBy(item => item.TargetId)
                .Select(item => new FactionRelationshipResource(item.SourceId.ToString("D"),
                    item.TargetId.ToString("D"), item.Score,
                    FactionValidator.ResolveBand(catalog, item.SourceId, item.TargetId),
                    item.Preset, item.Explanation)).ToArray();
            var bands = catalog.Bands.OrderByDescending(item => item.Priority)
                .ThenBy(item => item.Minimum).ThenBy(item => item.Id, StringComparer.Ordinal)
                .Select(item => new FactionDispositionBandResource(
                    item.Id, item.Minimum, item.Maximum, item.Priority)).ToArray();
            var personal = catalog.EffectivePersonalReputation.Where(item =>
                    selectedIds.Contains(item.SourceFactionId) || selectedIds.Contains(item.TargetFactionId))
                .OrderBy(item => item.SubjectKind).ThenBy(item => item.SubjectId, StringComparer.Ordinal)
                .ThenBy(item => item.Id)
                .Select(item => new FactionPersonalReputationResource(item.Id.ToString("D"),
                    item.SubjectKind.ToString(), item.SubjectId, item.SourceFactionId.ToString("D"),
                    item.TargetFactionId.ToString("D"), item.Delta, item.ExpiresAt?.ToString("O"), item.Cause))
                .ToArray();
            var reputation = catalog.Reputation;
            var globalReputation = (reputation?.Transactions ?? []).Where(item =>
                    selectedIds.Contains(item.SourceFactionId) || selectedIds.Contains(item.TargetFactionId))
                .OrderBy(item => item.OccurredAt).ThenBy(item => item.Id)
                .Select(item => new FactionGlobalReputationResource(item.Id.ToString("D"),
                    item.Scope.ToString(), item.ScopeId, item.SourceFactionId.ToString("D"),
                    item.TargetFactionId.ToString("D"), item.Delta, item.Cause, item.OccurredAt.ToString("O")))
                .ToArray();
            var heatmapHistory = (reputation?.HeatmapHistory ?? []).OrderBy(item => item.CapturedAt)
                .ThenBy(item => item.Id)
                .Select(snapshot => new FactionHeatmapSnapshotResource(snapshot.Id.ToString("D"), snapshot.Label,
                    snapshot.CapturedAt.ToString("O"), snapshot.Relationships.Where(item =>
                            selectedIds.Contains(item.SourceId) || selectedIds.Contains(item.TargetId))
                        .OrderBy(item => item.SourceId).ThenBy(item => item.TargetId)
                        .Select(item => new FactionRelationshipResource(item.SourceId.ToString("D"),
                            item.TargetId.ToString("D"), item.Score,
                            FactionValidator.ResolveBand(catalog, item.SourceId, item.TargetId, item.Score),
                            item.Preset, item.Explanation)).ToArray()))
                .ToArray();
            var reaction = reputation?.Reaction is { } policy
                ? new FactionReactionPolicyResource(policy.Id.ToString("D"), policy.DefaultScope.ToString(),
                    policy.DefaultScopeId, policy.ApplyDisguise, policy.ApplyCrimes, policy.ApplyDialogue,
                    policy.ApplyZoneRules, policy.AllowGameMasterOverride)
                : null;
            return new(1, _history.State.Revision.Value, filter, profile?.Id.ToString("D"),
                profile?.ServerAuthoritative == true, items, relationships, bands, personal,
                reputation?.Id.ToString("D"), catalog.RequireReputationRuntime, globalReputation,
                heatmapHistory, reaction);
        }
    }

    public FactionPlanResult PreviewFactions(FactionPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return new(false, request.BaseRevision, null, null,
                    [new AgentDiagnostic("faction.mcp_schema_unsupported",
                        "Faction MCP request schema must be v1.", "request/schemaVersion")]);
            var result = _factionHandler.Handle(_history.State, new SetFactionCatalogCommand(
                CommandId.From(Guid.NewGuid()), new ProjectRevision(request.BaseRevision), request.Catalog,
                DryRun: true));
            return new(result.Succeeded, request.BaseRevision, result.Patch, result.Inverse,
                result.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message,
                    "module/factions")).ToArray());
        }
    }

    public DialoguePresentationResource QueryDialoguePresentations(string? dialogueIdOrSearch = null, int limit = 50)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        lock (_gate)
        {
            var filter = dialogueIdOrSearch?.Trim();
            var items = _history.State.Project.Dialogues
                .Where(item => item.Presentation is not null && (string.IsNullOrWhiteSpace(filter) ||
                    string.Equals(item.Id.ToString(), filter, StringComparison.OrdinalIgnoreCase) ||
                    item.Slug.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    item.DisplayName.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
                    item.Presentation.Bookmarks.Any(bookmark =>
                        bookmark.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase)) ||
                    item.Presentation.VariantSets.Any(set =>
                        set.Key.Contains(filter, StringComparison.OrdinalIgnoreCase) || set.Variants.Any(variant =>
                            variant.Text.Key.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                            variant.Text.Fallback.Contains(filter, StringComparison.CurrentCultureIgnoreCase)))))
                .OrderBy(item => item.Slug, StringComparer.Ordinal).Take(limit)
                .Select(item => new DialoguePresentationResourceItem(item.Id.ToString(), item.Slug,
                    item.DisplayName, item.Presentation!.Id.ToString("D"), item.Presentation.RequiredAtRuntime,
                    item.Presentation.ServerAuthoritativeInterjections, item.Presentation.AnimationCues.Count,
                    item.Presentation.VoiceCues.Count, item.Presentation.CameraShots.Count,
                    item.Presentation.Bookmarks.Count, item.Presentation.InterjectionRules.Count,
                    item.Presentation.TimedChoices.Count, item.Presentation.VariantSets.Count,
                    item.Presentation.VariantSets.Sum(set => set.Variants.Count)))
                .ToArray();
            return new(1, _history.State.Revision.Value, filter, items);
        }
    }

    public DialogueGraphResource QueryDialogues(string? dialogueIdOrSearch = null, int limit = 50)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        lock (_gate)
        {
            var filter = dialogueIdOrSearch?.Trim();
            var items = _history.State.Project.Dialogues
                .Where(item => string.IsNullOrWhiteSpace(filter) ||
                    string.Equals(item.Id.ToString(), filter, StringComparison.OrdinalIgnoreCase) ||
                    item.Slug.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    item.DisplayName.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
                    item.Nodes.Any(node =>
                        node.Text.Key.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        node.Text.Fallback.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
                        node.Role.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        (node.SubgraphKey?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)) ||
                    (item.Authoring?.Participants.Any(binding =>
                         binding.Role.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                         binding.Value.Contains(filter, StringComparison.OrdinalIgnoreCase)) ?? false) ||
                    (item.Authoring?.Nodes.Any(node =>
                         node.Tokens.Any(token => token.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
                         node.Notes.Intent.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
                         node.Notes.Source.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                         node.Notes.AuthorMention.Contains(filter, StringComparison.OrdinalIgnoreCase)) ?? false) ||
                    (item.Experience is { } experience &&
                        (experience.Search.Pattern.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
                         experience.MemorySlots.Any(slot =>
                             slot.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                             slot.ScopeId.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
                         experience.SkillChecks.Any(check =>
                             check.SkillId.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                             (check.OpponentId?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false))) ||
                    (item.Production is { } production &&
                        (production.Subgraphs.Any(contract =>
                             contract.Key.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                             contract.RequiredVariables.Any(value => value.Contains(filter,
                                 StringComparison.OrdinalIgnoreCase))) ||
                         production.Consequences.Any(value =>
                             value.TargetId.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                             value.Explanation.Contains(filter, StringComparison.CurrentCultureIgnoreCase)) ||
                         production.AiPlans.Any(value =>
                             value.Synopsis.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
                             value.ContextCitations.Any(citation => citation.Contains(filter,
                                 StringComparison.OrdinalIgnoreCase)))))))
                .OrderBy(item => item.Slug, StringComparer.Ordinal)
                .Take(limit)
                .Select(item => new DialogueGraphResourceItem(
                    item.Id.ToString(), item.SchemaVersion, item.Provenance, item.Slug, item.DisplayName,
                    item.EntryNodeId.ToString(), item.Metadata, item.Participants.Count, item.EntryPoints.Count,
                    item.Nodes.Select(node => new DialogueGraphNodeResource(
                        node.Id.ToString(), node.Role.ToString(), node.ParticipantId.ToString("D"),
                        node.SpeakerBlueprintId?.ToString(), node.SubgraphKey, node.Text.Key, node.Text.Fallback,
                        node.Text.Variants.Count, node.OnEnterActions.Count,
                        node.Choices.Count, item.Edges.Count(edge => edge.TargetNodeId == node.Id))).ToArray(),
                    item.Edges.Select(edge =>
                    {
                        var choice = item.Nodes.Single(node => node.Id == edge.SourceNodeId).Choices
                            .Single(candidate => candidate.Id == edge.Id);
                        return new DialogueGraphEdgeResource(
                            edge.Id.ToString(), edge.SourceNodeId.ToString(), edge.TargetNodeId.ToString(),
                            edge.Kind.ToString(), edge.Order, edge.Priority, choice.ConditionCombination.ToString(),
                            choice.Conditions.Count, choice.Actions.Count);
                    }).ToArray(),
                    MapDialogueAuthoringResource(item.Authoring),
                    MapDialogueExperienceResource(item.Experience),
                    MapDialogueProductionResource(item.Production)))
                .ToArray();
            return new DialogueGraphResource(1, _history.State.Revision.Value, filter, items);
        }
    }

    public DialoguePresentationPlanResult PreviewDialoguePresentation(DialoguePresentationPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return new(false, request.BaseRevision, null, null,
                    [new AgentDiagnostic("dialogue.presentation.mcp_schema_unsupported",
                        "Dialogue presentation MCP request schema must be v1.", "request/schemaVersion")]);
            if (!Guid.TryParse(request.DialogueId, out var parsed) || parsed == Guid.Empty)
                return new(false, request.BaseRevision, null, null,
                    [new AgentDiagnostic("dialogue.presentation.mcp_id_invalid",
                        "Dialogue ID must be a non-empty UUID.", "request/dialogueId")]);
            var result = _dialoguePresentationHandler.Handle(_history.State,
                new SetDialoguePresentationCommand(CommandId.From(Guid.NewGuid()),
                    new ProjectRevision(request.BaseRevision), DialogueId.From(parsed), request.Presentation, true));
            return new(result.Succeeded, request.BaseRevision, result.Patch, result.Inverse,
                result.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message,
                    $"dialogues/{request.DialogueId}/presentation")).ToArray());
        }
    }

    public LoreIndexResource GetLoreIndex()
    {
        lock (_gate)
        {
            return new LoreIndexResource(
                1,
                _loreIndexHistory.State.Revision.Value,
                _loreIndexHistory.State.Index,
                _loreLoadDiagnostics.ToArray());
        }
    }

    public LoreContextResource QueryLoreIndex(string query, int limit = 8)
    {
        if (limit is < 1 or > 50)
        {
            return new LoreContextResource(
                1,
                _loreIndexHistory.State.Revision.Value,
                query ?? string.Empty,
                string.Empty,
                new string('0', 64),
                [],
                0,
                false,
                [new AgentDiagnostic("assistant.lore.query_limit_invalid", "Lore query limit must be between 1 and 50.")]);
        }
        lock (_gate)
        {
            if (_loreLoadDiagnostics.Count > 0)
            {
                return new LoreContextResource(
                    1,
                    _loreIndexHistory.State.Revision.Value,
                    query ?? string.Empty,
                    string.Empty,
                    new string('0', 64),
                    [],
                    0,
                    false,
                    _loreLoadDiagnostics.ToArray());
            }
            var packet = LoreContextAssembler.Assemble(
                _loreIndexHistory.State.Index,
                _history.State.Project,
                query ?? string.Empty,
                limit);
            return new LoreContextResource(
                1,
                _loreIndexHistory.State.Revision.Value,
                packet.Query,
                packet.Content,
                packet.ContentSha256,
                packet.SourceIds,
                packet.EntryCount,
                packet.Truncated,
                []);
        }
    }

    public LoreIndexPlanResult PlanLoreIndex(LoreIndexPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1 || request.BaseRevision < 0)
            {
                return LoreIndexFailure(
                    _loreIndexHistory.State.Revision.Value,
                    "assistant.lore.request_invalid",
                    "Lore plan request schema or base revision is invalid.");
            }
            var planId = Guid.NewGuid();
            var transactionId = Guid.NewGuid();
            var command = new SetLoreIndexCommand(
                CommandId.From(Guid.NewGuid()),
                transactionId,
                planId,
                new LoreIndexRevision(request.BaseRevision),
                request.Index,
                request.Permission,
                DryRun: true);
            var preview = _loreIndexHandler.Handle(_loreIndexHistory.State, command, _history.State.Project);
            if (!preview.Succeeded || preview.Patch is null || preview.Inverse is null)
            {
                return new LoreIndexPlanResult(
                    false,
                    null,
                    transactionId.ToString("D"),
                    request.BaseRevision,
                    null,
                    null,
                    preview.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, "assistant/lore-index")).ToArray(),
                    false,
                    false,
                    []);
            }
            var planKey = planId.ToString("D");
            var plan = new StoredLoreIndexPlan(
                planKey,
                command,
                preview.Patch,
                preview.Inverse,
                [new LoreIndexAuditEntry(planKey, transactionId.ToString("D"), "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _loreIndexPlans.Add(planKey, plan);
            return ToResult(plan);
        }
    }

    public LoreIndexPlanResult ApplyLoreIndexPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_loreIndexPlans.TryGetValue(planId, out var plan))
            {
                return LoreIndexFailure(
                    _loreIndexHistory.State.Revision.Value,
                    "assistant.lore.plan_missing",
                    $"Lore plan '{planId}' does not exist.");
            }
            if (plan.Applied)
            {
                return LoreIndexFailure(
                    plan.Command.BaseRevision.Value,
                    "assistant.lore.plan_already_applied",
                    $"Lore plan '{planId}' is already applied.");
            }
            var command = plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
            var result = _loreIndexHandler.Handle(_loreIndexHistory.State, command, _history.State.Project);
            if (!result.Succeeded)
            {
                plan.Audit.Add(new LoreIndexAuditEntry(
                    plan.PlanId,
                    plan.Command.TransactionId.ToString("D"),
                    "apply",
                    _loreIndexHistory.State.Revision.Value,
                    _clock.UtcNow,
                    false));
                return WithFailure(plan, result.Diagnostics.Select(item =>
                    new AgentDiagnostic(item.Code, item.Message, "assistant/lore-index")).ToArray());
            }
            var saved = LoreIndexStore.Save(_session.RootDirectory, result.State.Index);
            if (!saved.Succeeded)
            {
                plan.Audit.Add(new LoreIndexAuditEntry(
                    plan.PlanId,
                    plan.Command.TransactionId.ToString("D"),
                    "apply",
                    _loreIndexHistory.State.Revision.Value,
                    _clock.UtcNow,
                    false));
                return WithFailure(plan, saved.Diagnostics.Select(item =>
                    new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
            }
            _loreIndexHistory.Record(result);
            _loreLoadDiagnostics.Clear();
            plan.Applied = true;
            plan.AppliedRevision = _loreIndexHistory.State.Revision.Value;
            plan.Audit.Add(new LoreIndexAuditEntry(
                plan.PlanId,
                plan.Command.TransactionId.ToString("D"),
                "apply",
                plan.AppliedRevision.Value,
                _clock.UtcNow,
                true));
            return ToResult(plan);
        }
    }

    public LoreIndexPlanResult RevertLoreIndexPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_loreIndexPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
            {
                return LoreIndexFailure(
                    _loreIndexHistory.State.Revision.Value,
                    "assistant.lore.plan_not_applied",
                    $"Lore plan '{planId}' is not currently applied.");
            }
            if (plan.AppliedRevision != _loreIndexHistory.State.Revision.Value || !_loreIndexHistory.Undo())
            {
                return LoreIndexFailure(
                    _loreIndexHistory.State.Revision.Value,
                    "assistant.lore.revert_conflict",
                    "Lore plan is no longer the latest applied lore mutation.");
            }
            var saved = LoreIndexStore.Save(_session.RootDirectory, _loreIndexHistory.State.Index);
            if (!saved.Succeeded)
            {
                _loreIndexHistory.Redo();
                return WithFailure(plan, saved.Diagnostics.Select(item =>
                    new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
            }
            plan.Reverted = true;
            plan.Audit.Add(new LoreIndexAuditEntry(
                plan.PlanId,
                plan.Command.TransactionId.ToString("D"),
                "revert",
                _loreIndexHistory.State.Revision.Value,
                _clock.UtcNow,
                true));
            return ToResult(plan);
        }
    }

    public AssistedAreaPlanResource GetAssistedAreaPlans()
    {
        lock (_gate)
        {
            return new AssistedAreaPlanResource(
                1,
                _areaPlanHistory.State.Revision.Value,
                _areaPlanHistory.State.Catalog,
                _areaPlanLoadDiagnostics.ToArray());
        }
    }

    public AssistedAreaPlanProposalResource ProposeAssistedAreaPlan(AssistedAreaPlanProposalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SchemaVersion != 1)
            return new(false, null, "no-change", string.Empty, 0, 0, 0, false,
                [new AgentDiagnostic("assistant.area_plan.request_invalid", "Area-plan proposal schema is invalid.", "assistant/area-plans")]);
        var snapshot = CaptureAssistedProposalSnapshot();
        var profile = AssistantWorkspaceProfile.CreateDefault();
        var proposal = AssistedAreaPlanProposalService.ProposeAsync(
            request.Brief,
            snapshot.Project,
            snapshot.LoreIndex,
            new DeterministicAssistantProvider(profile.ActiveProvider),
            profile.ActiveProvider,
            networkAuthorized: false,
            projectContentAuthorized: false).GetAwaiter().GetResult();
        return new AssistedAreaPlanProposalResource(
            proposal.Succeeded,
            proposal.Plan,
            proposal.SemanticDiff,
            proposal.PromptSha256,
            proposal.InputTokens,
            proposal.OutputTokens,
            proposal.EstimatedCostUsd,
            GeometryApplied: false,
            proposal.Diagnostics.Select(item =>
                new AgentDiagnostic(item.Code, item.Message, "assistant/area-plans")).ToArray());
    }

    public AssistedAreaPlanPlanResult PlanAssistedAreaPlans(AssistedAreaPlanPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1 || request.BaseRevision < 0)
                return AssistedAreaPlanFailure(_areaPlanHistory.State.Revision.Value,
                    "assistant.area_plan.request_invalid", "Area-plan request schema or base revision is invalid.");
            var planId = Guid.NewGuid();
            var transactionId = Guid.NewGuid();
            var command = new SetAssistedAreaPlansCommand(
                CommandId.From(Guid.NewGuid()), transactionId, planId,
                new AssistedAreaPlanRevision(request.BaseRevision), request.Catalog, request.Permission, DryRun: true);
            var preview = _areaPlanHandler.Handle(_areaPlanHistory.State, command, _history.State.Project);
            if (!preview.Succeeded || preview.Patch is null || preview.Inverse is null)
                return new(false, null, transactionId.ToString("D"), request.BaseRevision, null, null,
                    preview.Diagnostics.Select(item =>
                        new AgentDiagnostic(item.Code, item.Message, "assistant/area-plans")).ToArray(),
                    false, false, []);
            var key = planId.ToString("D");
            var stored = new StoredAssistedAreaPlan(
                key, command, preview.Patch, preview.Inverse,
                [new AssistedAreaPlanAuditEntry(key, transactionId.ToString("D"), "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _areaPlans.Add(key, stored);
            return ToResult(stored);
        }
    }

    public AssistedAreaPlanPlanResult ApplyAssistedAreaPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_areaPlans.TryGetValue(planId, out var plan))
                return AssistedAreaPlanFailure(_areaPlanHistory.State.Revision.Value,
                    "assistant.area_plan.plan_missing", $"Area plan '{planId}' does not exist.");
            if (plan.Applied)
                return AssistedAreaPlanFailure(plan.Command.BaseRevision.Value,
                    "assistant.area_plan.plan_already_applied", $"Area plan '{planId}' is already applied.");
            var result = _areaPlanHandler.Handle(_areaPlanHistory.State,
                plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false },
                _history.State.Project);
            if (!result.Succeeded)
            {
                plan.Audit.Add(new(plan.PlanId, plan.Command.TransactionId.ToString("D"), "apply",
                    _areaPlanHistory.State.Revision.Value, _clock.UtcNow, false));
                return WithFailure(plan, result.Diagnostics.Select(item =>
                    new AgentDiagnostic(item.Code, item.Message, "assistant/area-plans")).ToArray());
            }
            var saved = AssistedAreaPlanStore.Save(_session.RootDirectory, result.State.Catalog);
            if (!saved.Succeeded)
            {
                plan.Audit.Add(new(plan.PlanId, plan.Command.TransactionId.ToString("D"), "apply",
                    _areaPlanHistory.State.Revision.Value, _clock.UtcNow, false));
                return WithFailure(plan, saved.Diagnostics.Select(item =>
                    new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
            }
            _areaPlanHistory.Record(result);
            _areaPlanLoadDiagnostics.Clear();
            plan.Applied = true;
            plan.AppliedRevision = _areaPlanHistory.State.Revision.Value;
            plan.Audit.Add(new(plan.PlanId, plan.Command.TransactionId.ToString("D"), "apply",
                plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public AssistedAreaPlanPlanResult RevertAssistedAreaPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_areaPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return AssistedAreaPlanFailure(_areaPlanHistory.State.Revision.Value,
                    "assistant.area_plan.plan_not_applied", $"Area plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _areaPlanHistory.State.Revision.Value || !_areaPlanHistory.Undo())
                return AssistedAreaPlanFailure(_areaPlanHistory.State.Revision.Value,
                    "assistant.area_plan.revert_conflict", "Area plan is no longer the latest applied area-plan mutation.");
            var saved = AssistedAreaPlanStore.Save(_session.RootDirectory, _areaPlanHistory.State.Catalog);
            if (!saved.Succeeded)
            {
                _areaPlanHistory.Redo();
                return WithFailure(plan, saved.Diagnostics.Select(item =>
                    new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
            }
            plan.Reverted = true;
            plan.Audit.Add(new(plan.PlanId, plan.Command.TransactionId.ToString("D"), "revert",
                _areaPlanHistory.State.Revision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public AssistedAreaBuildPlanResult PlanAssistedAreaBuild(AssistedAreaBuildPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1 || request.BaseRevision < 0 ||
                !Guid.TryParse(request.AssistedAreaPlanId, out var assistedAreaPlanId))
                return AssistedAreaBuildFailure(_history.State.Revision.Value,
                    "assistant.area_build.request_invalid", "Area-build request schema, revision or plan ID is invalid.");
            var executionPlanId = Guid.NewGuid();
            var transactionId = Guid.NewGuid();
            var command = new BuildAssistedAreaPlanCommand(
                CommandId.From(Guid.NewGuid()), transactionId, executionPlanId,
                new ProjectRevision(request.BaseRevision), assistedAreaPlanId, request.Permission, DryRun: true);
            var preview = _areaBuildHandler.Handle(_history.State, command, _areaPlanHistory.State.Catalog);
            if (!preview.Succeeded || preview.Command.Patch is null || preview.Command.Inverse is null)
                return new(false, null, transactionId.ToString("D"), request.AssistedAreaPlanId,
                    request.BaseRevision, null, null, [], "no-change", 0, 0, false,
                    preview.Diagnostics.Select(Map).ToArray(), false, false, []);
            var key = executionPlanId.ToString("D");
            var stored = new StoredAssistedAreaBuildPlan(
                key,
                command,
                preview.Command.Patch,
                preview.Command.Inverse,
                preview.EntityIds.Select(item => item.ToString()).ToArray(),
                preview.SemanticDiff,
                preview.NavigationDiagnosticsBefore,
                preview.NavigationDiagnosticsAfter,
                preview.LightingPass,
                [new AssistedAreaPlanAuditEntry(
                    key, transactionId.ToString("D"), "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _areaBuildPlans.Add(key, stored);
            return ToResult(stored);
        }
    }

    public AssistedAreaBuildPlanResult ApplyAssistedAreaBuild(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_areaBuildPlans.TryGetValue(planId, out var plan))
                return AssistedAreaBuildFailure(_history.State.Revision.Value,
                    "assistant.area_build.plan_missing", $"Area-build plan '{planId}' does not exist.");
            if (plan.Applied)
                return AssistedAreaBuildFailure(plan.Command.BaseRevision.Value,
                    "assistant.area_build.plan_already_applied", $"Area-build plan '{planId}' is already applied.");
            var result = _areaBuildHandler.Handle(
                _history.State,
                plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false },
                _areaPlanHistory.State.Catalog);
            if (!result.Succeeded)
            {
                plan.Audit.Add(new(plan.PlanId, plan.Command.TransactionId.ToString("D"), "apply",
                    _history.State.Revision.Value, _clock.UtcNow, false));
                return WithFailure(plan, result.Diagnostics.Select(Map).ToArray());
            }
            var saved = _session.Save(result.Command.State.Project);
            if (!saved.Succeeded)
            {
                plan.Audit.Add(new(plan.PlanId, plan.Command.TransactionId.ToString("D"), "apply",
                    _history.State.Revision.Value, _clock.UtcNow, false));
                return WithFailure(plan, saved.Diagnostics.Select(item =>
                    new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
            }
            _history.Record(result.Command);
            plan.Applied = true;
            plan.AppliedRevision = _history.State.Revision.Value;
            plan.Audit.Add(new(plan.PlanId, plan.Command.TransactionId.ToString("D"), "apply",
                plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public AssistedAreaBuildPlanResult RevertAssistedAreaBuild(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_areaBuildPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return AssistedAreaBuildFailure(_history.State.Revision.Value,
                    "assistant.area_build.plan_not_applied", $"Area-build plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _history.State.Revision.Value)
                return AssistedAreaBuildFailure(plan.Command.BaseRevision.Value,
                    "assistant.area_build.revert_conflict", "Only the latest project mutation can be reverted safely.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return AssistedAreaBuildFailure(plan.Command.BaseRevision.Value,
                    "assistant.area_build.inverse_unavailable", "The assisted build inverse is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return WithFailure(plan, saved.Diagnostics.Select(item =>
                    new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
            }
            plan.Reverted = true;
            plan.Audit.Add(new(plan.PlanId, plan.Command.TransactionId.ToString("D"), "revert",
                undone.State.Revision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public IReadOnlyList<AgentDiagnostic> GetDiagnostics()
    {
        lock (_gate)
        {
            return ProjectDiagnosticCatalog.Build(_history.State.Project)
                .Select(Map)
                .ToArray();
        }
    }

    public AgentSpatialContextResource GetBoundedSpatialContext(AgentSpatialContextQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        lock (_gate)
        {
            return GetBoundedSpatialContextCore(query);
        }
    }

    private AgentSpatialContextResource GetBoundedSpatialContextCore(AgentSpatialContextQuery query)
    {
        var project = _history.State.Project;
        var revision = _history.State.Revision.Value;
        var loaded = AssistantWorkspaceProfileStore.Load(_session.RootDirectory);
        var workflow = loaded.Profile.AgentWorkflow;
        var diagnostics = loaded.Diagnostics.Select(item =>
            new AgentDiagnostic(item.Code, item.Message, item.Path)).ToList();
        var areaSelectors = (query.AreaIds ?? []).Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var entitySelectors = (query.EntityIds ?? []).Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        AgentSpatialContextResource Empty() => new(
            AgentReferenceGraphBuilder.StableGuid(
                $"spatial-context|{project.Id}|{revision}|empty|{query.MaximumEntities}|{query.MaximumReferences}"),
            1,
            "openrpg-forge:mcp-spatial-context-v1",
            revision,
            query.Permission,
            query.MaximumEntities,
            query.MaximumReferences,
            areaSelectors.Order(StringComparer.Ordinal).ToArray(),
            entitySelectors.Order(StringComparer.Ordinal).ToArray(),
            [],
            [],
            diagnostics);

        if (query.SchemaVersion != 1)
        {
            diagnostics.Add(new("assistant.spatial.schema_unsupported",
                "Spatial context schema must be 1.", "query/schemaVersion"));
            return Empty();
        }
        if (query.MaximumEntities is < 1 or > 64 || query.MaximumReferences is < 0 or > 256 ||
            areaSelectors.Length + entitySelectors.Length > 128)
        {
            diagnostics.Add(new("assistant.spatial.limit_invalid",
                "Spatial context requires 1..64 entities, 0..256 references and at most 128 selectors.",
                "query/limits"));
            return Empty();
        }
        if (areaSelectors.Length == 0 && entitySelectors.Length == 0)
        {
            diagnostics.Add(new("assistant.spatial.selection_required",
                "At least one explicit area or entity UUID is required.", "query/selection"));
            return Empty();
        }
        if (!Enum.TryParse<AssistantPermissionLevel>(query.Permission, true, out var permission) ||
            !Enum.IsDefined(permission) || permission > workflow.MaximumPermission)
        {
            diagnostics.Add(new("assistant.spatial.permission_denied",
                "Spatial context permission must be defined and cannot exceed the workspace maximum.",
                "query/permission"));
            return Empty();
        }

        var parsedAreaIds = new List<Guid>();
        var parsedEntityIds = new List<Guid>();
        foreach (var selector in areaSelectors)
        {
            if (!Guid.TryParse(selector, out var parsed) || parsed == Guid.Empty)
                diagnostics.Add(new("assistant.spatial.selector_invalid",
                    $"Area selector '{selector}' must be a UUID.", "query/areaIds"));
            else parsedAreaIds.Add(parsed);
        }
        foreach (var selector in entitySelectors)
        {
            if (!Guid.TryParse(selector, out var parsed) || parsed == Guid.Empty)
                diagnostics.Add(new("assistant.spatial.selector_invalid",
                    $"Entity selector '{selector}' must be a UUID.", "query/entityIds"));
            else parsedEntityIds.Add(parsed);
        }
        if (diagnostics.Any(item => item.Code == "assistant.spatial.selector_invalid"))
            return Empty();

        var locations = project.Areas.SelectMany(area => area.Entities.Select(entity =>
                (Area: area, Entity: entity)))
            .ToDictionary(item => item.Entity.Id.Value);
        var areasById = project.Areas.ToDictionary(item => item.Id.Value);
        foreach (var areaId in parsedAreaIds.Where(id => !areasById.ContainsKey(id)))
            diagnostics.Add(new("assistant.spatial.selector_missing",
                $"Area selector '{areaId:D}' does not resolve in this workspace.", "query/areaIds"));
        foreach (var entityId in parsedEntityIds.Where(id => !locations.ContainsKey(id)))
            diagnostics.Add(new("assistant.spatial.selector_missing",
                $"Entity selector '{entityId:D}' does not resolve in this workspace.", "query/entityIds"));
        if (diagnostics.Any(item => item.Code == "assistant.spatial.selector_missing"))
            return Empty();

        var explicitEntities = parsedEntityIds.Distinct().Order().Select(id => locations[id]);
        var areaEntities = parsedAreaIds.Distinct().Order().Select(id => areasById[id])
            .SelectMany(area => area.Entities.OrderBy(item => item.Id.Value)
                .Select(entity => (Area: area, Entity: entity)));
        var selected = explicitEntities.Concat(areaEntities)
            .DistinctBy(item => item.Entity.Id)
            .Take(Math.Min(query.MaximumEntities, workflow.MaximumContextItems))
            .OrderBy(item => $"areas/{item.Area.Id}/entities/{item.Entity.Id}", StringComparer.Ordinal)
            .ThenBy(item => item.Entity.Id.Value)
            .ToArray();
        var privacy = workflow.EffectiveGovernance.BudgetPrivacy;
        var blueprints = project.Blueprints.ToDictionary(item => item.Id);
        var items = selected.Select(item =>
        {
            var blueprint = blueprints[item.Entity.BlueprintId];
            var hasAsset = AssetBlueprintFactory.TryGetAssetId(blueprint, out var assetId);
            var transform = item.Entity.Transform;
            return new AgentSpatialEntityResource(
                item.Area.Id.ToString(),
                item.Area.DisplayName,
                item.Entity.Id.ToString(),
                blueprint.DisplayName,
                blueprint.Id.ToString(),
                hasAsset ? assetId?.ToString() : null,
                item.Entity.Tags.Where(tag =>
                        !AgentExecutionGovernanceValidator.IsSensitive(tag, privacy))
                    .Order(StringComparer.Ordinal).ToArray(),
                new AgentSpatialTransformResource(
                    new(transform.Position.X, transform.Position.Y, transform.Position.Z),
                    new(transform.Rotation.X, transform.Rotation.Y, transform.Rotation.Z, transform.Rotation.W),
                    new(transform.Scale.X, transform.Scale.Y, transform.Scale.Z)),
                $"areas/{item.Area.Id}/entities/{item.Entity.Id}");
        }).ToArray();

        var selectedIds = selected.Select(item => item.Entity.Id.Value)
            .Concat(parsedAreaIds).ToHashSet();
        var references = query.MaximumReferences == 0
            ? []
            : AgentReferenceGraphBuilder.Build(project,
                    maximumDepth: workflow.References.MaximumDepth,
                    maximumNodes: workflow.References.MaximumNodes)
                .Edges.Where(edge => selectedIds.Contains(edge.SourceId) || selectedIds.Contains(edge.TargetId))
                .OrderBy(edge => edge.DocumentPath, StringComparer.Ordinal)
                .ThenBy(edge => edge.Id)
                .Take(query.MaximumReferences)
                .Select(edge => new AgentSpatialReferenceResource(
                    edge.Id.ToString("D"), edge.SourceId.ToString("D"), edge.TargetId.ToString("D"),
                    edge.Usage.ToString(), edge.DocumentPath))
                .ToArray();
        return new AgentSpatialContextResource(
            AgentReferenceGraphBuilder.StableGuid(
                $"spatial-context|{project.Id}|{revision}|" +
                $"{string.Join(',', areaSelectors.Order(StringComparer.Ordinal))}|" +
                $"{string.Join(',', entitySelectors.Order(StringComparer.Ordinal))}|" +
                $"{query.MaximumEntities}|{query.MaximumReferences}"),
            1,
            "openrpg-forge:mcp-spatial-context-v1",
            revision,
            permission.ToString(),
            query.MaximumEntities,
            query.MaximumReferences,
            areaSelectors.Order(StringComparer.Ordinal).ToArray(),
            entitySelectors.Order(StringComparer.Ordinal).ToArray(),
            items,
            references,
            diagnostics);
    }

    public AgentStructuredContextResource GetStructuredAgentContext(AgentContextQuery? query = null)
    {
        lock (_gate)
        {
            query ??= new AgentContextQuery();
            var project = _history.State.Project;
            var revision = _history.State.Revision.Value;
            var loaded = AssistantWorkspaceProfileStore.Load(_session.RootDirectory);
            var workflow = loaded.Profile.AgentWorkflow;
            var diagnostics = loaded.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToList();
            var requested = Math.Clamp(query.MaximumItems, 1, workflow.MaximumContextItems);
            var selectedAreaIds = new List<string>();
            var selectedEntityIds = new List<string>();
            foreach (var selection in query.SelectionObjectIds ?? [])
            {
                if (!Guid.TryParse(selection, out var selectedId))
                    continue;
                if (project.Areas.Any(item => item.Id.Value == selectedId))
                    selectedAreaIds.Add(selectedId.ToString("D"));
                if (project.Areas.SelectMany(item => item.Entities).Any(item => item.Id.Value == selectedId))
                    selectedEntityIds.Add(selectedId.ToString("D"));
            }
            var spatial = selectedAreaIds.Count == 0 && selectedEntityIds.Count == 0
                ? null
                : GetBoundedSpatialContextCore(new AgentSpatialContextQuery(
                    1,
                    Math.Min(requested, 64),
                    Math.Min(requested * 4, 256),
                    selectedAreaIds,
                    selectedEntityIds,
                    AssistantPermissionLevel.ReadOnly.ToString()));
            var graph = AgentReferenceGraphBuilder.Build(project, maximumDepth: workflow.References.MaximumDepth,
                maximumNodes: workflow.References.MaximumNodes);
            var graphById = graph.Nodes.ToDictionary(item => item.Id);
            var blueprintNames = project.Blueprints.ToDictionary(item => item.Id, item => item.DisplayName);
            var raw = new Dictionary<AgentContextResourceKind, IReadOnlyList<AgentContextItem>>
            {
                [AgentContextResourceKind.Project] =
                    [ContextItem(project.Id.Value, "project", project.Title, "project", "project.json")],
                [AgentContextResourceKind.World] =
                    [ContextItem(project.Module.Id.Value, "world", project.Module.DisplayName, "module", project.Module.Metadata.Provenance)],
                [AgentContextResourceKind.Areas] = project.Areas.Select(area =>
                    ContextItem(area.Id.Value, "area", area.DisplayName, $"areas/{area.Id}", area.World.Provenance)).ToArray(),
                [AgentContextResourceKind.Entities] = project.Areas.SelectMany(area => area.Entities.Select(entity =>
                    ContextItem(entity.Id.Value, "entity", blueprintNames.GetValueOrDefault(entity.BlueprintId, entity.Id.ToString()),
                        $"areas/{area.Id}/entities/{entity.Id}", $"blueprint:{entity.BlueprintId}"))).ToArray(),
                [AgentContextResourceKind.Dialogues] = project.Dialogues.Select(dialogue =>
                    ContextItem(dialogue.Id.Value, "dialogue", dialogue.DisplayName, $"dialogues/{dialogue.Id}", dialogue.Provenance)).ToArray(),
                [AgentContextResourceKind.Quests] = project.Quests.Select(quest =>
                    ContextItem(quest.Id.Value, "quest", quest.DisplayName, $"quests/{quest.Id}", quest.Provenance)).ToArray(),
                [AgentContextResourceKind.Scripts] = EnumerateCuratedScripts(_session.RootDirectory),
                [AgentContextResourceKind.Assets] = project.Blueprints.Select(blueprint =>
                    ContextItem(blueprint.Id.Value, "asset", blueprint.DisplayName, $"blueprints/{blueprint.Id}", blueprint.Provenance.Source)).ToArray(),
                [AgentContextResourceKind.Diagnostics] = GetDiagnostics().Select((diagnostic, index) =>
                    ContextItem(AgentReferenceGraphBuilder.StableGuid($"diagnostic|{diagnostic.Code}|{diagnostic.Path}|{index}"),
                        "diagnostic", diagnostic.Code, diagnostic.Path ?? "diagnostics", diagnostic.Provenance ?? "project-validator")).ToArray(),
                [AgentContextResourceKind.Selection] = ResolveSelection(query.SelectionObjectIds, graphById, diagnostics),
            };

            var remaining = Math.Max(0, requested - (spatial?.Items.Count ?? 0));
            var sections = new List<AgentContextSection>();
            foreach (var kind in workflow.ContextResources)
            {
                var all = raw.GetValueOrDefault(kind, []);
                var items = all.OrderBy(item => item.DocumentPath, StringComparer.Ordinal).ThenBy(item => item.Id)
                    .Take(remaining).ToArray();
                remaining -= items.Length;
                sections.Add(new AgentContextSection(kind, all.Count, items));
            }
            var total = sections.Sum(section => section.Items.Count) + (spatial?.Items.Count ?? 0);
            return new AgentStructuredContextResource(
                AgentReferenceGraphBuilder.StableGuid($"context|{project.Id}|{revision}|{requested}"),
                AgentSemanticWorkflow.CurrentSchemaVersion, workflow.Provenance, revision,
                workflow.MaximumPermission, total, sections, spatial, diagnostics);
        }
    }

    public AgentReferenceGraphResource QueryAgentReferenceGraph(
        string? impactTargetId = null,
        int? maximumDepth = null,
        int? maximumNodes = null)
    {
        lock (_gate)
        {
            var workflow = AssistantWorkspaceProfileStore.Load(_session.RootDirectory).Profile.AgentWorkflow;
            var diagnostics = new List<AgentDiagnostic>();
            Guid? target = null;
            if (!string.IsNullOrWhiteSpace(impactTargetId))
            {
                if (!Guid.TryParse(impactTargetId, out var parsed))
                    diagnostics.Add(new("assistant.reference_graph.target_invalid", "Impact target must be a UUID.", "impactTargetId"));
                else target = parsed;
            }
            var depth = Math.Clamp(maximumDepth ?? workflow.References.MaximumDepth, 1, workflow.References.MaximumDepth);
            var nodeLimit = Math.Clamp(maximumNodes ?? workflow.References.MaximumNodes, 10, workflow.References.MaximumNodes);
            var graph = AgentReferenceGraphBuilder.Build(_history.State.Project, target, depth, nodeLimit);
            if (target is { } id && graph.Nodes.All(item => item.Id != id))
                diagnostics.Add(new("assistant.reference_graph.target_missing", $"Impact target '{id:D}' is not present.", "impactTargetId"));
            return new AgentReferenceGraphResource(
                AgentReferenceGraphBuilder.StableGuid($"reference-graph|{_history.State.Project.Id}|{_history.State.Revision.Value}"),
                AgentSemanticWorkflow.CurrentSchemaVersion, workflow.Provenance, _history.State.Revision.Value, target,
                graph.Nodes.Select(item => new AgentReferenceNode(item.Id, item.Kind.ToString(), item.DisplayName, item.DocumentPath)).ToArray(),
                graph.Edges.Select(item => new AgentReferenceEdge(item.Id, item.SourceId, item.TargetId,
                    item.Usage.ToString(), item.DocumentPath)).ToArray(), graph.ImpactedObjectIds, diagnostics);
        }
    }

    public AgentSemanticToolCatalogResource GetSemanticToolCatalog()
    {
        lock (_gate)
        {
            var loaded = AssistantWorkspaceProfileStore.Load(_session.RootDirectory);
            var workflow = loaded.Profile.AgentWorkflow;
            return new(workflow.Id, workflow.SchemaVersion, workflow.Provenance, workflow.MaximumPermission,
                workflow.Tools.OrderBy(item => item.Name, StringComparer.Ordinal).ToArray(),
                loaded.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
        }
    }

    public AgentChangePlanPreviewResult PreviewAgentChangePlan(AgentChangePlanPreviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            var project = _history.State.Project;
            var revision = _history.State.Revision.Value;
            var before = ProjectFingerprint(project);
            var workflow = AssistantWorkspaceProfileStore.Load(_session.RootDirectory).Profile.AgentWorkflow;
            var diagnostics = new List<AgentDiagnostic>();
            if (request.SchemaVersion != StructuredChangePlan.CurrentSchemaVersion)
                diagnostics.Add(new("assistant.change_plan.schema_unsupported", "Change-plan request schema must be 1.", "schemaVersion"));
            if (request.Plan.BaseRevision != revision)
                diagnostics.Add(new("assistant.change_plan.revision_conflict",
                    $"Plan revision {request.Plan.BaseRevision} does not match current revision {revision}.", "plan/baseRevision"));
            diagnostics.AddRange(AgentSemanticWorkflowValidator.ValidatePlan(request.Plan, workflow)
                .Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)));
            var graph = AgentReferenceGraphBuilder.Build(project, maximumDepth: workflow.References.MaximumDepth,
                maximumNodes: workflow.References.MaximumNodes);
            var targets = graph.Nodes.ToDictionary(item => item.Id);
            foreach (var command in request.Plan.Commands.Where(command => !targets.ContainsKey(command.TargetObjectId)))
                diagnostics.Add(new("assistant.change_plan.target_missing",
                    $"Target '{command.TargetObjectId:D}' is not present in the project graph.", $"plan/commands/{command.Id:D}/targetObjectId"));
            var diff = diagnostics.Count == 0
                ? request.Plan.Commands.OrderBy(item => item.Id).Select(command =>
                    $"{command.ToolName} -> {targets[command.TargetObjectId].Kind}:{targets[command.TargetObjectId].DisplayName} " +
                    $"({string.Join(", ", command.Arguments.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => $"{item.Key}={item.Value}"))})")
                    .ToArray()
                : [];
            var after = ProjectFingerprint(_history.State.Project);
            if (!string.Equals(before, after, StringComparison.Ordinal) || _history.State.Revision.Value != revision)
                diagnostics.Add(new("assistant.change_plan.dry_run_write_detected", "Dry-run changed project state.", "dryRun"));
            return new(diagnostics.Count == 0, request.Plan.Id, request.Plan.TransactionId,
                request.Plan.BaseRevision, revision, diff, before, after, diagnostics, true);
        }
    }

    public AgentChangeTransactionResult BeginAgentChangeTransaction(AgentChangeTransactionBeginRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            var plan = request.Plan;
            var revision = _history.State.Revision.Value;
            var diagnostics = new List<AgentDiagnostic>();
            if (request.SchemaVersion != 1)
                diagnostics.Add(new("assistant.transaction.schema_unsupported", "Agent transaction schema must be 1.", "schemaVersion"));
            if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.UserId))
                diagnostics.Add(new("assistant.transaction.actor_required", "Client and user identities are required.", "actor"));
            if (plan.TransactionId == Guid.Empty || _agentTransactions.ContainsKey(plan.TransactionId))
                diagnostics.Add(new("assistant.transaction.identity_invalid", "Transaction ID must be non-empty and unique.", "plan/transactionId"));

            var loaded = AssistantWorkspaceProfileStore.Load(_session.RootDirectory);
            var workflow = loaded.Profile.AgentWorkflow;
            diagnostics.AddRange(loaded.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)));
            diagnostics.AddRange(AgentSemanticWorkflowValidator.ValidatePlan(plan, workflow).Select(Map));
            if (plan.BaseRevision != revision)
                diagnostics.Add(new("assistant.transaction.revision_conflict",
                    $"Plan revision {plan.BaseRevision} does not match current revision {revision}.", "plan/baseRevision"));

            var paths = plan.Commands.Select(command => AgentCommandDocumentPath(command)).ToArray();
            var providerKey = string.IsNullOrWhiteSpace(request.Provider)
                ? loaded.Profile.ActiveProvider.Key
                : request.Provider.Trim();
            var authorization = AgentExecutionGovernanceValidator.Authorize(workflow, plan,
                new AgentExecutionAuthorizationRequest(providerKey, request.ProjectContentOptIn,
                    request.HumanConfirmed, request.InputTokens, request.OutputTokens, request.EstimatedCost,
                    request.DurationSeconds, paths));
            diagnostics.AddRange(authorization.Diagnostics.Select(Map));
            if (diagnostics.Count > 0)
                return AgentTransactionFailure(plan.TransactionId, plan.Id, Guid.Empty, AgentTransactionStage.Rejected,
                    plan.BaseRevision, revision, diagnostics);

            var fingerprint = ProjectFingerprint(_history.State.Project);
            var rollbackId = Guid.NewGuid();
            var stored = new StoredAgentChangeTransaction(request, providerKey, rollbackId, fingerprint);
            var audit = CreateAgentAudit(stored, AgentTransactionStage.Begun, revision, fingerprint, true);
            var auditSave = AppendAgentAudit(audit);
            if (!auditSave.Succeeded)
                return AgentTransactionFailure(plan.TransactionId, plan.Id, rollbackId, AgentTransactionStage.Rejected,
                    plan.BaseRevision, revision, auditSave.Diagnostics.Select(Map).ToArray());
            stored.Audit.Add(audit);
            _agentTransactions.Add(plan.TransactionId, stored);
            return ToResult(stored);
        }
    }

    public AgentChangeTransactionResult PreviewAgentChangeTransaction(string transactionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);
        lock (_gate)
        {
            if (!Guid.TryParse(transactionId, out var id) || !_agentTransactions.TryGetValue(id, out var stored))
                return AgentTransactionFailure(id, Guid.Empty, Guid.Empty, AgentTransactionStage.Rejected,
                    GetCurrentRevision(), GetCurrentRevision(),
                    [new("assistant.transaction.missing", "Agent transaction does not exist.", "transactionId")]);
            if (stored.Stage != AgentTransactionStage.Begun)
                return AgentTransactionStateFailure(stored, "assistant.transaction.preview_state_invalid",
                    "Only a begun transaction can be previewed.");
            if (stored.Request.Plan.BaseRevision != _history.State.Revision.Value ||
                !string.Equals(stored.BeforeHash, ProjectFingerprint(_history.State.Project), StringComparison.Ordinal))
                return RejectAgentTransaction(stored, "assistant.transaction.revision_conflict",
                    "Project revision or hash changed before preview.");

            var prepared = PrepareAgentTransaction(stored);
            if (!prepared.Succeeded)
                return RejectAgentTransaction(stored, prepared.Diagnostics);
            stored.Command = prepared.Command;
            stored.PlaceCommand = prepared.PlaceCommand;
            stored.SemanticDiff = prepared.SemanticDiff;
            stored.Stage = AgentTransactionStage.Previewed;
            var audit = CreateAgentAudit(stored, AgentTransactionStage.Previewed,
                _history.State.Revision.Value, prepared.SemanticDiff!.AfterHash, true);
            var saved = AppendAgentAudit(audit);
            if (!saved.Succeeded)
            {
                stored.Stage = AgentTransactionStage.Rejected;
                return AgentTransactionFailure(stored.Request.Plan.TransactionId, stored.Request.Plan.Id,
                    stored.RollbackId, stored.Stage, stored.Request.Plan.BaseRevision, _history.State.Revision.Value,
                    saved.Diagnostics.Select(Map).ToArray(), stored.SemanticDiff, stored.Audit);
            }
            stored.Audit.Add(audit);
            return ToResult(stored);
        }
    }

    public AgentChangeTransactionResult CommitAgentChangeTransaction(string transactionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);
        lock (_gate)
        {
            if (!Guid.TryParse(transactionId, out var id) || !_agentTransactions.TryGetValue(id, out var stored))
                return AgentTransactionFailure(id, Guid.Empty, Guid.Empty, AgentTransactionStage.Rejected,
                    GetCurrentRevision(), GetCurrentRevision(),
                    [new("assistant.transaction.missing", "Agent transaction does not exist.", "transactionId")]);
            if (stored.Stage != AgentTransactionStage.Previewed ||
                stored.Command is null && stored.PlaceCommand is null || stored.SemanticDiff is null)
                return AgentTransactionStateFailure(stored, "assistant.transaction.commit_state_invalid",
                    "Commit requires a successful preview of the same transaction.");
            if (stored.Request.Plan.BaseRevision != _history.State.Revision.Value ||
                !string.Equals(stored.BeforeHash, ProjectFingerprint(_history.State.Project), StringComparison.Ordinal))
                return RejectAgentTransaction(stored, "assistant.transaction.revision_conflict",
                    "Project revision or hash changed before commit.");

            CommandResult result;
            if (stored.Command is { } moduleCommand)
            {
                result = _moduleConfigurationHandler.Handle(_history.State, moduleCommand with
                {
                    CommandId = CommandId.From(Guid.NewGuid()),
                    DryRun = false,
                });
            }
            else
            {
                var place = stored.PlaceCommand!;
                result = _placeEntityHandler.Handle(_history.State, new PlaceEntityCommand(
                    CommandId.From(Guid.NewGuid()),
                    place.BaseRevision,
                    place.AreaId,
                    place.EntityId,
                    place.BlueprintId,
                    place.Transform,
                    dryRun: false,
                    place.Tags,
                    place.Overrides));
            }
            if (!result.Succeeded)
                return RejectAgentTransaction(stored, result.Diagnostics.Select(Map).ToArray());
            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
                return RejectAgentTransaction(stored, saved.Diagnostics.Select(item =>
                    new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
            _history.Record(result);
            stored.Stage = AgentTransactionStage.Committed;
            stored.AppliedRevision = result.NewRevision!.Value.Value;
            var outputHash = ProjectFingerprint(_history.State.Project);
            stored.SemanticDiff = stored.SemanticDiff with { AfterHash = outputHash };
            var audit = CreateAgentAudit(stored, AgentTransactionStage.Committed,
                stored.AppliedRevision.Value, outputHash, true);
            var auditSaved = AppendAgentAudit(audit);
            if (!auditSaved.Succeeded)
            {
                var undone = _history.Undo();
                if (undone.Succeeded) _session.Save(undone.State.Project);
                stored.Stage = AgentTransactionStage.Rejected;
                stored.AppliedRevision = null;
                return AgentTransactionFailure(stored.Request.Plan.TransactionId, stored.Request.Plan.Id,
                    stored.RollbackId, stored.Stage, stored.Request.Plan.BaseRevision, _history.State.Revision.Value,
                    auditSaved.Diagnostics.Select(Map).ToArray(), stored.SemanticDiff, stored.Audit);
            }
            stored.Audit.Add(audit);
            return ToResult(stored);
        }
    }

    public AgentChangeTransactionResult RollbackAgentChangeTransaction(string transactionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);
        lock (_gate)
        {
            if (!Guid.TryParse(transactionId, out var id) || !_agentTransactions.TryGetValue(id, out var stored))
                return AgentTransactionFailure(id, Guid.Empty, Guid.Empty, AgentTransactionStage.Rejected,
                    GetCurrentRevision(), GetCurrentRevision(),
                    [new("assistant.transaction.missing", "Agent transaction does not exist.", "transactionId")]);
            if (stored.Stage != AgentTransactionStage.Committed || stored.AppliedRevision != _history.State.Revision.Value)
                return AgentTransactionStateFailure(stored, "assistant.transaction.rollback_state_invalid",
                    "Rollback is allowed only for the latest committed agent transaction.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return AgentTransactionStateFailure(stored, "assistant.transaction.rollback_unavailable",
                    "The canonical inverse is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return AgentTransactionStateFailure(stored, "assistant.transaction.rollback_save_failed",
                    "The rolled-back project could not be persisted.");
            }
            stored.Stage = AgentTransactionStage.RolledBack;
            var outputHash = ProjectFingerprint(_history.State.Project);
            var audit = CreateAgentAudit(stored, AgentTransactionStage.RolledBack,
                _history.State.Revision.Value, outputHash, true);
            var auditSaved = AppendAgentAudit(audit);
            if (!auditSaved.Succeeded)
            {
                var redone = _history.Redo();
                if (redone.Succeeded) _session.Save(redone.State.Project);
                stored.Stage = AgentTransactionStage.Committed;
                return AgentTransactionFailure(stored.Request.Plan.TransactionId, stored.Request.Plan.Id,
                    stored.RollbackId, stored.Stage, stored.Request.Plan.BaseRevision, _history.State.Revision.Value,
                    auditSaved.Diagnostics.Select(Map).ToArray(), stored.SemanticDiff, stored.Audit);
            }
            stored.Audit.Add(audit);
            return ToResult(stored);
        }
    }

    public AgentAiAuditResource GetAgentAiAudit()
    {
        lock (_gate)
        {
            return new(AgentAiAuditEntry.CurrentSchemaVersion,
                _agentAudit.OrderBy(item => item.OccurredAt).ThenBy(item => item.Id).ToArray(),
                _agentAuditLoadDiagnostics.ToArray());
        }
    }

    public AgentStructuredResultValidationResource ValidateAgentStructuredResult(
        AgentStructuredResultValidationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return new(false, false, string.Empty, [],
                    [new("assistant.result.request_schema_unsupported", "Structured-result request schema must be 1.", "schemaVersion")]);
            var loaded = AssistantWorkspaceProfileStore.Load(_session.RootDirectory);
            var prompt = loaded.Profile.Prompts.SingleOrDefault(item =>
                string.Equals(item.Key, request.PromptKey, StringComparison.Ordinal));
            if (prompt is null)
                return new(false, false, string.Empty, [],
                    [new("assistant.result.prompt_missing", $"Prompt '{request.PromptKey}' is not configured.", "promptKey")]);
            var rendered = new AssistantPromptRenderResult(prompt.Key, prompt.Version, string.Empty, string.Empty,
                prompt.ResponseJsonSchema, new string('0', 64));
            var providerRequest = new AssistantProviderRequest(request.PlanId, request.TransactionId, rendered,
                request.Permission, loaded.Profile.ActiveProvider.MaximumOutputTokens,
                loaded.Profile.NetworkAllowed, loaded.Profile.ShareProjectContent);
            var response = new AssistantProviderResponse(request.ProviderKey, request.Model, request.Content,
                request.InputTokens, request.OutputTokens, request.EstimatedCost, request.Provenance);
            var result = AgentResponseSafetyService.ValidateStructuredResult(
                prompt, response, providerRequest, loaded.Profile.AgentWorkflow);
            return new(result.Succeeded, result.Repaired, result.NormalizedJson,
                result.ToolCalls.Select(call => new AgentStructuredToolCallResource(
                    call.ToolName, call.TargetObjectId,
                    call.Arguments.ToDictionary(item => item.Key, item => item.Value.GetRawText(), StringComparer.Ordinal)))
                    .ToArray(),
                result.Diagnostics.Select(Map).ToArray());
        }
    }

    public AgentOfflineReadinessResource GetAgentOfflineReadiness()
    {
        lock (_gate)
        {
            var loaded = AssistantWorkspaceProfileStore.Load(_session.RootDirectory);
            var readiness = AgentResponseSafetyService.EvaluateOfflineReadiness(
                loaded.Profile,
                loaded.FileMissing,
                loaded.Diagnostics.Select(item => new DomainDiagnostic(
                    item.Code, item.Message, item.Path ?? "assistant/profile")).ToArray());
            return new(readiness.Ready, readiness.ProfileWasMissing, readiness.ManualAuthoringAvailable,
                readiness.NetworkRequired, readiness.AiMetadataRequiredByBuild, readiness.AiMetadataRequiredByRuntime,
                readiness.ActiveProvider, readiness.Diagnostics.Select(Map).ToArray());
        }
    }

    private static AgentContextItem ContextItem(Guid id, string kind, string name, string path, string provenance) =>
        new(id, kind, name, path, provenance);

    private static IReadOnlyList<AgentContextItem> EnumerateCuratedScripts(string workspaceRoot)
    {
        var root = Path.Combine(workspaceRoot, "scripts");
        if (!Directory.Exists(root)) return [];
        return Directory.EnumerateFiles(root, "*.forge", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => ContextItem(AgentReferenceGraphBuilder.StableGuid($"script|{Path.GetFileName(path)}"),
                "script", Path.GetFileNameWithoutExtension(path), $"scripts/{Path.GetFileName(path)}", "curated-forgescript-metadata"))
            .ToArray();
    }

    private static IReadOnlyList<AgentContextItem> ResolveSelection(
        IReadOnlyList<string>? ids,
        IReadOnlyDictionary<Guid, AgentReferenceGraphNode> graph,
        ICollection<AgentDiagnostic> diagnostics)
    {
        var values = new List<AgentContextItem>();
        foreach (var raw in ids ?? [])
        {
            if (!Guid.TryParse(raw, out var id) || !graph.TryGetValue(id, out var node))
            {
                diagnostics.Add(new("assistant.context.selection_missing", $"Selection '{raw}' is unknown.", "selectionObjectIds"));
                continue;
            }
            values.Add(ContextItem(node.Id, node.Kind.ToString(), node.DisplayName, node.DocumentPath, "canonical-selection"));
        }
        return values;
    }

    private static string ProjectFingerprint(Forge.Domain.Projects.ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project)))).ToLowerInvariant();

    private AgentTransactionPreparation PrepareAgentTransaction(StoredAgentChangeTransaction stored)
    {
        var commands = stored.Request.Plan.Commands;
        if (commands.All(command => string.Equals(
                command.ToolName, "project.set_variable", StringComparison.Ordinal)))
        {
            return PrepareProjectVariableBatch(stored);
        }

        if (commands.Count == 1 && string.Equals(
                commands[0].ToolName, "area.add_entity", StringComparison.Ordinal))
        {
            return PrepareAreaEntityPlacement(stored, commands[0]);
        }

        return new(false, null, null,
            [new AgentDiagnostic(
                "assistant.transaction.adapter_missing",
                "An atomic transaction must contain project.set_variable commands or one area.add_entity command.",
                "plan/commands")]);
    }

    private AgentTransactionPreparation PrepareAreaEntityPlacement(
        StoredAgentChangeTransaction stored,
        AgentSemanticCommand semantic)
    {
        var diagnostics = new List<AgentDiagnostic>();
        AgentAreaEntityPlacement? placement = semantic.AreaEntityPlacement;
        if (placement is null)
        {
            placement = ParseAreaEntityPlacement(semantic, diagnostics);
        }

        if (placement is null) return new(false, null, null, diagnostics);
        if (placement.SchemaVersion != AgentAreaEntityPlacement.CurrentSchemaVersion)
        {
            diagnostics.Add(new AgentDiagnostic(
                "assistant.transaction.placement_schema_unsupported",
                "Area entity placement schema must be 1.",
                $"plan/commands/{semantic.Id:D}/areaEntityPlacement/schemaVersion"));
        }
        if (placement.AreaId != semantic.TargetObjectId)
        {
            diagnostics.Add(new AgentDiagnostic(
                "assistant.transaction.target_invalid",
                "area.add_entity must target the same canonical area UUID carried by its typed arguments.",
                $"plan/commands/{semantic.Id:D}/targetObjectId"));
        }
        if (diagnostics.Count > 0) return new(false, null, null, diagnostics);

        PlaceEntityCommand command;
        try
        {
            command = new PlaceEntityCommand(
                CommandId.From(Guid.NewGuid()),
                new ProjectRevision(stored.Request.Plan.BaseRevision),
                AreaId.From(placement.AreaId),
                EntityId.From(placement.EntityId),
                BlueprintId.From(placement.BlueprintId),
                placement.Transform,
                dryRun: true,
                placement.Tags,
                placement.Overrides);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException)
        {
            return new(false, null, null,
                [new AgentDiagnostic("assistant.transaction.placement_invalid", exception.Message,
                    $"plan/commands/{semantic.Id:D}/areaEntityPlacement")]);
        }

        var preview = _placeEntityHandler.Handle(_history.State, command);
        if (!preview.Succeeded)
            return new(false, null, null, preview.Diagnostics.Select(Map).ToArray());
        var candidate = _placeEntityHandler.Handle(_history.State, new PlaceEntityCommand(
            command.CommandId,
            command.BaseRevision,
            command.AreaId,
            command.EntityId,
            command.BlueprintId,
            command.Transform,
            dryRun: false,
            command.Tags,
            command.Overrides));
        if (!candidate.Succeeded)
            return new(false, null, null, candidate.Diagnostics.Select(Map).ToArray());

        var path = $"areas/{command.AreaId}/entities/{command.EntityId}";
        var entry = new AgentSemanticDiffEntry(
            AgentReferenceGraphBuilder.StableGuid($"agent-diff|{stored.Request.Plan.TransactionId:D}|{command.EntityId}"),
            AgentSemanticDiffCategory.Objects,
            AgentSemanticChangeKind.Added,
            command.EntityId.Value,
            path,
            "transform",
            string.Empty,
            FormatTransform(command.Transform),
            $"added entity '{command.EntityId}' from blueprint '{command.BlueprintId}'");
        var diff = new AgentSemanticDiff(
            AgentReferenceGraphBuilder.StableGuid($"agent-diff|{stored.Request.Plan.TransactionId:D}"),
            AgentSemanticDiff.CurrentSchemaVersion,
            stored.Request.Plan.TransactionId,
            stored.BeforeHash,
            ProjectFingerprint(candidate.State.Project),
            [entry]);
        return new(true, null, diff, [], command);
    }

    private static AgentAreaEntityPlacement? ParseAreaEntityPlacement(
        AgentSemanticCommand semantic,
        ICollection<AgentDiagnostic> diagnostics)
    {
        var arguments = semantic.Arguments;
        if (!TryGuid(arguments, "areaId", out var areaId) ||
            !TryGuid(arguments, "entityId", out var entityId) ||
            !TryGuid(arguments, "blueprintId", out var blueprintId) ||
            !TryFloat(arguments, "positionX", out var px) ||
            !TryFloat(arguments, "positionY", out var py) ||
            !TryFloat(arguments, "positionZ", out var pz) ||
            !TryFloat(arguments, "rotationX", out var rx) ||
            !TryFloat(arguments, "rotationY", out var ry) ||
            !TryFloat(arguments, "rotationZ", out var rz) ||
            !TryFloat(arguments, "rotationW", out var rw) ||
            !TryFloat(arguments, "scaleX", out var sx) ||
            !TryFloat(arguments, "scaleY", out var sy) ||
            !TryFloat(arguments, "scaleZ", out var sz))
        {
            diagnostics.Add(new AgentDiagnostic(
                "assistant.transaction.placement_arguments_invalid",
                "area.add_entity requires areaId, entityId, blueprintId and every position, quaternion and scale component.",
                $"plan/commands/{semantic.Id:D}/arguments"));
            return null;
        }

        try
        {
            var tags = arguments.GetValueOrDefault("tags", string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var overrides = arguments
                .Where(item => item.Key.StartsWith("override.", StringComparison.Ordinal))
                .Select(item => new EntityPropertyOverride(
                    item.Key["override.".Length..], EntityPropertyValueKind.String, item.Value))
                .ToArray();
            return new AgentAreaEntityPlacement(
                AgentAreaEntityPlacement.CurrentSchemaVersion,
                areaId,
                entityId,
                blueprintId,
                new TransformValue(
                    new Vector3Value(px, py, pz),
                    new QuaternionValue(rx, ry, rz, rw),
                    new Vector3Value(sx, sy, sz)),
                tags,
                new EntityPropertyOverrides(EntityPropertyOverrides.CurrentSchemaVersion, overrides));
        }
        catch (ArgumentException exception)
        {
            diagnostics.Add(new AgentDiagnostic(
                "assistant.transaction.placement_arguments_invalid",
                exception.Message,
                $"plan/commands/{semantic.Id:D}/arguments"));
            return null;
        }
    }

    private static bool TryGuid(IReadOnlyDictionary<string, string> arguments, string key, out Guid value)
    {
        value = Guid.Empty;
        return arguments.TryGetValue(key, out var raw) && Guid.TryParse(raw, out value) && value != Guid.Empty;
    }

    private static bool TryFloat(IReadOnlyDictionary<string, string> arguments, string key, out float value)
    {
        value = 0;
        return arguments.TryGetValue(key, out var raw) &&
               float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
               float.IsFinite(value);
    }

    private static string FormatTransform(TransformValue transform) =>
        string.Create(CultureInfo.InvariantCulture,
            $"position=({transform.Position.X:G9},{transform.Position.Y:G9},{transform.Position.Z:G9});" +
            $"rotation=({transform.Rotation.X:G9},{transform.Rotation.Y:G9},{transform.Rotation.Z:G9},{transform.Rotation.W:G9});" +
            $"scale=({transform.Scale.X:G9},{transform.Scale.Y:G9},{transform.Scale.Z:G9})");

    private AgentTransactionPreparation PrepareProjectVariableBatch(StoredAgentChangeTransaction stored)
    {
        var plan = stored.Request.Plan;
        var project = _history.State.Project;
        var variables = project.Module.Variables.ToDictionary(item => item.Key, StringComparer.Ordinal);
        var before = project.Module.Variables.ToDictionary(item => item.Key, StringComparer.Ordinal);
        var diagnostics = new List<AgentDiagnostic>();
        foreach (var semantic in plan.Commands.OrderBy(item => item.Id))
        {
            if (!string.Equals(semantic.ToolName, "project.set_variable", StringComparison.Ordinal))
            {
                diagnostics.Add(new("assistant.transaction.adapter_missing",
                    $"Tool '{semantic.ToolName}' has no atomic batch adapter; use its specialized MCP workflow.",
                    $"plan/commands/{semantic.Id:D}/toolName"));
                continue;
            }
            if (semantic.TargetObjectId != project.Id.Value && semantic.TargetObjectId != project.Module.Id.Value)
            {
                diagnostics.Add(new("assistant.transaction.target_invalid",
                    "Project-variable commands must target the canonical project or module UUID.",
                    $"plan/commands/{semantic.Id:D}/targetObjectId"));
                continue;
            }
            var arguments = semantic.Arguments;
            if (!arguments.TryGetValue("key", out var key) || string.IsNullOrWhiteSpace(key))
            {
                diagnostics.Add(new("assistant.transaction.variable_key_required", "Variable key is required.",
                    $"plan/commands/{semantic.Id:D}/arguments/key"));
                continue;
            }
            key = key.Trim();
            var operation = arguments.GetValueOrDefault("operation", "upsert").Trim().ToLowerInvariant();
            if (operation == "remove")
            {
                if (!variables.Remove(key))
                    diagnostics.Add(new("assistant.transaction.variable_missing", $"Variable '{key}' does not exist.",
                        $"module/variables/{key}"));
                continue;
            }
            if (operation != "upsert" ||
                !Enum.TryParse<ProjectVariableValueKind>(arguments.GetValueOrDefault("kind"), true, out var kind) ||
                !Enum.IsDefined(kind) ||
                !Enum.TryParse<ProjectVariableScope>(arguments.GetValueOrDefault("scope"), true, out var scope) ||
                !Enum.IsDefined(scope) ||
                !Enum.TryParse<ProjectVariableReplicationMode>(arguments.GetValueOrDefault("replication", "None"), true,
                    out var replication) || !Enum.IsDefined(replication) ||
                !arguments.TryGetValue("value", out var value))
            {
                diagnostics.Add(new("assistant.transaction.variable_arguments_invalid",
                    "Variable operation, kind, scope, replication and value must be valid.",
                    $"plan/commands/{semantic.Id:D}/arguments"));
                continue;
            }
            try
            {
                var id = variables.TryGetValue(key, out var existing)
                    ? existing.Id
                    : ProjectVariableId.From(arguments.TryGetValue("variableId", out var rawId) && Guid.TryParse(rawId, out var parsedId)
                        ? parsedId
                        : semantic.Id);
                variables[key] = new ProjectVariableDefinition(
                    id,
                    key,
                    kind,
                    scope,
                    value,
                    arguments.GetValueOrDefault("description", string.Empty),
                    plan.Provenance,
                    ProjectVariableDefinition.CurrentSchemaVersion,
                    replication,
                    ParseBoolean(arguments.GetValueOrDefault("debugVisible"), true),
                    ParseBoolean(arguments.GetValueOrDefault("requiredAtRuntime"), false));
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException)
            {
                diagnostics.Add(new("assistant.transaction.variable_invalid", exception.Message,
                    $"module/variables/{key}"));
            }
        }
        if (diagnostics.Count > 0) return new(false, null, null, diagnostics);

        var command = new SetModuleConfigurationCommand(CommandId.From(Guid.NewGuid()),
            new ProjectRevision(plan.BaseRevision), project.Module.Metadata,
            variables.Values.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray(), false);
        var candidate = _moduleConfigurationHandler.Handle(_history.State, command);
        if (!candidate.Succeeded)
            return new(false, null, null, candidate.Diagnostics.Select(Map).ToArray());
        var after = candidate.State.Project.Module.Variables.ToDictionary(item => item.Key, StringComparer.Ordinal);
        var touchedKeys = before.Keys.Concat(after.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        var privacy = AssistantWorkspaceProfileStore.Load(_session.RootDirectory).Profile.AgentWorkflow
            .EffectiveGovernance.BudgetPrivacy;
        var entries = new List<AgentSemanticDiffEntry>();
        foreach (var key in touchedKeys)
        {
            before.TryGetValue(key, out var oldValue);
            after.TryGetValue(key, out var newValue);
            if (Equals(oldValue, newValue)) continue;
            var change = oldValue is null ? AgentSemanticChangeKind.Added :
                newValue is null ? AgentSemanticChangeKind.Removed : AgentSemanticChangeKind.Modified;
            var objectId = newValue?.Id.Value ?? oldValue!.Id.Value;
            var redact = AgentExecutionGovernanceValidator.IsSensitive(key, privacy);
            entries.Add(new AgentSemanticDiffEntry(
                AgentReferenceGraphBuilder.StableGuid($"agent-diff|{plan.TransactionId:D}|{key}|{change}"),
                AgentSemanticDiffCategory.States,
                change,
                objectId,
                $"module/variables/{key}",
                "defaultValue",
                oldValue is null ? string.Empty : redact ? "[REDACTED]" : oldValue.DefaultValue,
                newValue is null ? string.Empty : redact ? "[REDACTED]" : newValue.DefaultValue,
                $"{change.ToString().ToLowerInvariant()} project variable '{key}'"));
        }
        if (entries.Count == 0)
            return new(false, null, null,
                [new("assistant.transaction.no_change", "The transaction produces no semantic change.", "plan/commands")]);
        var diff = new AgentSemanticDiff(
            AgentReferenceGraphBuilder.StableGuid($"agent-diff|{plan.TransactionId:D}"),
            AgentSemanticDiff.CurrentSchemaVersion,
            plan.TransactionId,
            stored.BeforeHash,
            ProjectFingerprint(candidate.State.Project),
            entries);
        return new(true, command, diff, []);
    }

    private static bool ParseBoolean(string? value, bool fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback :
        bool.TryParse(value, out var parsed) ? parsed :
        throw new ArgumentException($"'{value}' is not a boolean value.");

    private static string AgentCommandDocumentPath(AgentSemanticCommand command)
    {
        if (string.Equals(command.ToolName, "area.add_entity", StringComparison.Ordinal))
        {
            var entityId = command.AreaEntityPlacement?.EntityId.ToString("D") ??
                           command.Arguments.GetValueOrDefault("entityId", command.Id.ToString("D"));
            return $"areas/{command.TargetObjectId:D}/entities/{entityId}";
        }
        if (string.Equals(command.ToolName, "project.set_variable", StringComparison.Ordinal) &&
            command.Arguments.TryGetValue("key", out var key))
            return $"module/variables/{key.Trim()}";
        return $"{command.ToolName.Replace('.', '/')}/{command.TargetObjectId:D}";
    }

    private AgentAiAuditEntry CreateAgentAudit(
        StoredAgentChangeTransaction stored,
        AgentTransactionStage stage,
        long revision,
        string outputHash,
        bool succeeded) =>
        new(
            Guid.NewGuid(),
            AgentAiAuditEntry.CurrentSchemaVersion,
            stored.Request.Plan.TransactionId,
            stored.Request.Plan.Id,
            stored.RollbackId,
            stage,
            stored.Request.ClientId.Trim(),
            stored.Request.UserId.Trim(),
            string.IsNullOrWhiteSpace(stored.ProviderKey) ? null : stored.ProviderKey,
            string.IsNullOrWhiteSpace(stored.Request.Model) ? null : stored.Request.Model.Trim(),
            stored.Request.Plan.Commands.OrderBy(item => item.Id).Select(item => item.ToolName).ToArray(),
            AgentPlanFingerprint(stored.Request.Plan),
            outputHash,
            stored.Request.Plan.Tests.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).ToArray(),
            stored.Request.Plan.Provenance,
            revision,
            _clock.UtcNow,
            succeeded);

    private ProjectSessionSaveResult AppendAgentAudit(AgentAiAuditEntry entry)
    {
        var updated = _agentAudit.Append(entry).ToArray();
        var result = AgentAiAuditStore.Save(_session.RootDirectory, updated);
        if (result.Succeeded) _agentAudit.Add(entry);
        return result;
    }

    private AgentChangeTransactionResult RejectAgentTransaction(
        StoredAgentChangeTransaction stored,
        string code,
        string message) => RejectAgentTransaction(stored, [new AgentDiagnostic(code, message, "transaction")]);

    private AgentChangeTransactionResult RejectAgentTransaction(
        StoredAgentChangeTransaction stored,
        IReadOnlyList<AgentDiagnostic> diagnostics)
    {
        stored.Stage = AgentTransactionStage.Rejected;
        var audit = CreateAgentAudit(stored, AgentTransactionStage.Rejected, _history.State.Revision.Value,
            ProjectFingerprint(_history.State.Project), false);
        var saved = AppendAgentAudit(audit);
        if (saved.Succeeded) stored.Audit.Add(audit);
        return AgentTransactionFailure(stored.Request.Plan.TransactionId, stored.Request.Plan.Id, stored.RollbackId,
            stored.Stage, stored.Request.Plan.BaseRevision, _history.State.Revision.Value,
            diagnostics.Concat(saved.Diagnostics.Select(Map)).ToArray(), stored.SemanticDiff, stored.Audit);
    }

    private AgentChangeTransactionResult AgentTransactionStateFailure(
        StoredAgentChangeTransaction stored,
        string code,
        string message)
    {
        var audit = CreateAgentAudit(stored, AgentTransactionStage.Rejected, _history.State.Revision.Value,
            ProjectFingerprint(_history.State.Project), false);
        var saved = AppendAgentAudit(audit);
        if (saved.Succeeded) stored.Audit.Add(audit);
        return AgentTransactionFailure(stored.Request.Plan.TransactionId, stored.Request.Plan.Id, stored.RollbackId,
            stored.Stage, stored.Request.Plan.BaseRevision, _history.State.Revision.Value,
            new[] { new AgentDiagnostic(code, message, "transaction") }.Concat(saved.Diagnostics.Select(Map)).ToArray(),
            stored.SemanticDiff, stored.Audit);
    }

    private static AgentChangeTransactionResult ToResult(StoredAgentChangeTransaction stored) =>
        new(true, stored.Request.Plan.TransactionId, stored.Request.Plan.Id, stored.RollbackId, stored.Stage,
            stored.Request.Plan.BaseRevision, stored.AppliedRevision ?? stored.Request.Plan.BaseRevision,
            stored.SemanticDiff, stored.Audit.ToArray(), []);

    private static AgentChangeTransactionResult AgentTransactionFailure(
        Guid transactionId,
        Guid planId,
        Guid rollbackId,
        AgentTransactionStage stage,
        long baseRevision,
        long currentRevision,
        IReadOnlyList<AgentDiagnostic> diagnostics,
        AgentSemanticDiff? diff = null,
        IReadOnlyList<AgentAiAuditEntry>? audit = null) =>
        new(false, transactionId, planId, rollbackId, stage, baseRevision, currentRevision, diff, audit ?? [], diagnostics);

    private static string AgentPlanFingerprint(StructuredChangePlan plan)
    {
        var value = new StringBuilder()
            .Append(plan.Id).Append('|').Append(plan.TransactionId).Append('|').Append(plan.SchemaVersion).Append('|')
            .Append(plan.Provenance).Append('|').Append(plan.BaseRevision).Append('|').Append(plan.Permission).Append('|')
            .Append(plan.Goal);
        foreach (var command in plan.Commands.OrderBy(item => item.Id))
        {
            value.Append("|C|").Append(command.Id).Append('|').Append(command.ToolName).Append('|').Append(command.TargetObjectId);
            foreach (var argument in command.Arguments.OrderBy(item => item.Key, StringComparer.Ordinal))
                value.Append('|').Append(argument.Key).Append('=').Append(argument.Value);
        }
        foreach (var item in plan.Assumptions) value.Append("|A|").Append(item);
        foreach (var item in plan.AffectedObjects.Order()) value.Append("|O|").Append(item);
        foreach (var item in plan.Risks) value.Append("|R|").Append(item);
        foreach (var item in plan.Tests) value.Append("|T|").Append(item);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.ToString()))).ToLowerInvariant();
    }

    public GeneratedDocumentationResource CheckGeneratedDocumentation()
    {
        var repositoryRoot = FindRepositoryRoot(_session.RootDirectory);
        var result = ForgeDocumentationGenerator.Generate(repositoryRoot, "docs/generated", checkOnly: true);
        return new GeneratedDocumentationResource(1, result.Current,
            result.Files.Select(file => $"docs/generated/{file.RelativePath}").ToArray(), result.ChangedFiles);
    }

    public ProjectHealthReport GetProjectHealth(bool testsPassed = false)
    {
        lock (_gate)
        {
            var evidence = testsPassed
                ? new[] { new ProjectHealthSignal("tests", ProjectHealthCategory.Tests,
                    ProjectHealthSeverity.Information, "health.tests.mcp_pass", "MCP test evidence passed.",
                    "health/tests/mcp", true) }
                : [];
            return ProjectHealthReportService.Evaluate(_history.State.Project, _clock.UtcNow, evidence);
        }
    }

    public WorkspaceQualityLabReport RunQualityLab()
    {
        lock (_gate)
        {
            var options = WorkspaceQualityLabOptions.Default with { WritePseudoLocalizationSnapshot = false };
            return new WorkspaceQualityLabService(new NativeForgeScriptRuntime())
                .Run(_session.RootDirectory, _history.State.Project, options);
        }
    }

    public AssistantScenarioTestReport RunAssistantTestAutomation()
    {
        lock (_gate)
        {
            var loaded = AssistantWorkspaceProfileStore.Load(_session.RootDirectory);
            if (!loaded.Succeeded)
                throw new InvalidOperationException(loaded.Diagnostics[0].Code);
            return AssistantScenarioTestService.Run(_history.State.Project, loaded.Profile.TestAutomation);
        }
    }

    public WorkspacePerformanceLabReport RunPerformanceLab()
    {
        lock (_gate)
        {
            return new WorkspacePerformanceLabService().Run(_session.RootDirectory, _history.State.Project,
                new WorkspacePerformanceLabOptions(false));
        }
    }

    public WorkspaceGovernanceLabReport RunGovernanceLab()
    {
        lock (_gate)
        {
            return new WorkspaceGovernanceLabService(new NativeForgeScriptRuntime())
                .Run(_session.RootDirectory, _history.State.Project, new WorkspaceGovernanceLabOptions(false));
        }
    }

    private static string FindRepositoryRoot(string start)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(start));
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OpenRPGForge.sln"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("OpenRPG Forge repository root was not found.");
    }

    public RuntimePolicyResource GetRuntimePolicies(string? areaId = null)
    {
        lock (_gate)
        {
            var project = _history.State.Project;
            var selected = string.IsNullOrWhiteSpace(areaId)
                ? project.Areas.Single(item => item.Id == project.Module.EntryAreaId)
                : project.Areas.Single(item => item.Id.Value == Guid.Parse(areaId));
            return new RuntimePolicyResource(1, _history.State.Revision.Value, selected.Id.ToString(),
                project.Module.RuntimePolicies, selected.RuntimePolicies);
        }
    }

    public ProjectGameplayEventResource QueryProjectGameplayEvents(string? idOrSearch = null)
    {
        lock (_gate)
        {
            var profile = _history.State.Project.Module.RuntimePolicies.GameplayEvents;
            var filter = idOrSearch?.Trim();
            var idMatch = string.IsNullOrWhiteSpace(filter) ||
                          string.Equals(profile.Id.ToString("D"), filter, StringComparison.OrdinalIgnoreCase);
            var bindings = profile.Bindings.Where(binding => idMatch ||
                binding.EventName.Contains(filter!, StringComparison.OrdinalIgnoreCase) ||
                binding.Handler.Contains(filter!, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(binding.Id.ToString("D"), filter, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (!idMatch && bindings.Length == 0)
                return new(false, _history.State.Revision.Value, profile.Id.ToString("D"), profile.SchemaVersion,
                    profile.Provenance, profile.Enabled, profile.RequiredAtRuntime, profile.UseDownedState,
                    profile.AutoRespawn, profile.ReplicateEquipmentAndProgression, [],
                    [new AgentDiagnostic("project.events.not_found",
                        "No project gameplay event matches the filter.",
                        "module/runtimePolicies/gameplayEvents")]);
            return new(true, _history.State.Revision.Value, profile.Id.ToString("D"), profile.SchemaVersion,
                profile.Provenance, profile.Enabled, profile.RequiredAtRuntime, profile.UseDownedState,
                profile.AutoRespawn, profile.ReplicateEquipmentAndProgression, bindings, []);
        }
    }

    public ProjectWorldResource QueryProjectWorld(string? idOrSearch = null)
    {
        lock (_gate)
        {
            var profile = _history.State.Project.Module.RuntimePolicies.World;
            var filter = idOrSearch?.Trim();
            var profileMatch = string.IsNullOrWhiteSpace(filter) ||
                string.Equals(profile.Id.ToString("D"), filter, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(profile.Clock.Id.ToString("D"), filter, StringComparison.OrdinalIgnoreCase) ||
                profile.Clock.AreaTimeZoneOffsets.Keys.Any(area => area.Contains(filter!, StringComparison.OrdinalIgnoreCase));
            var events = profile.CustomEvents.Where(schema => profileMatch ||
                string.Equals(schema.Id.ToString("D"), filter, StringComparison.OrdinalIgnoreCase) ||
                $"{schema.Namespace}.{schema.Name}".Contains(filter!, StringComparison.OrdinalIgnoreCase) ||
                schema.Handler.Contains(filter!, StringComparison.OrdinalIgnoreCase) ||
                schema.Fields.Any(field => field.Key.Contains(filter!, StringComparison.OrdinalIgnoreCase) ||
                    field.Value.Contains(filter!, StringComparison.OrdinalIgnoreCase))).ToArray();
            if (!profileMatch && events.Length == 0)
                return new(false, _history.State.Revision.Value, profile, [],
                [
                    new AgentDiagnostic("project_world.not_found", "No project world configuration matches the filter.",
                        "module/runtimePolicies/world"),
                ]);
            return new(true, _history.State.Revision.Value, profile, events, []);
        }
    }

    public RuntimePolicyPlanResult PlanRuntimePolicies(RuntimePolicyPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
            {
                return RuntimePolicyFailure(request.BaseRevision, "runtime_policy_plan.schema_unsupported",
                    "Runtime policy plan schema must be 1.");
            }

            SetRuntimePoliciesCommand command;
            try
            {
                command = new SetRuntimePoliciesCommand(CommandId.From(Guid.NewGuid()),
                    new ProjectRevision(request.BaseRevision), request.Module, AreaId.From(Guid.Parse(request.AreaId)),
                    request.Area, true);
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                return RuntimePolicyFailure(request.BaseRevision, "runtime_policy_plan.invalid", exception.Message);
            }

            var preview = _runtimePoliciesHandler.Handle(_history.State, command);
            if (!preview.Succeeded)
            {
                return new RuntimePolicyPlanResult(false, null, request.BaseRevision, preview.Patch,
                    preview.Inverse, preview.Diagnostics.Select(Map).ToArray(), false, false, []);
            }

            var planId = Guid.NewGuid().ToString("D");
            var plan = new StoredRuntimePolicyPlan(planId, command, preview.Patch!, preview.Inverse!,
                [new RuntimePolicyAuditEntry(planId, "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _runtimePolicyPlans.Add(planId, plan);
            return ToResult(plan);
        }
    }

    public RuntimePolicyPlanResult ApplyRuntimePolicyPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_runtimePolicyPlans.TryGetValue(planId, out var plan))
            {
                return RuntimePolicyFailure(GetCurrentRevision(), "runtime_policy_plan.missing",
                    $"Runtime policy plan '{planId}' does not exist.");
            }

            if (plan.Applied)
            {
                return RuntimePolicyFailure(plan.Command.BaseRevision.Value, "runtime_policy_plan.already_applied",
                    $"Runtime policy plan '{planId}' is already applied.");
            }

            var command = plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
            var result = _runtimePoliciesHandler.Handle(_history.State, command);
            if (!result.Succeeded)
            {
                plan.Audit.Add(new RuntimePolicyAuditEntry(planId, "apply", _history.State.Revision.Value,
                    _clock.UtcNow, false));
                return ToResult(plan) with { Succeeded = false, Diagnostics = result.Diagnostics.Select(Map).ToArray() };
            }

            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
            {
                plan.Audit.Add(new RuntimePolicyAuditEntry(planId, "apply", _history.State.Revision.Value,
                    _clock.UtcNow, false));
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = saved.Diagnostics.Select(item =>
                        new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
                };
            }

            _history.Record(result);
            plan.Applied = true;
            plan.AppliedRevision = result.NewRevision!.Value.Value;
            plan.Audit.Add(new RuntimePolicyAuditEntry(planId, "apply", plan.AppliedRevision.Value,
                _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public RuntimePolicyPlanResult RevertRuntimePolicyPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_runtimePolicyPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
            {
                return RuntimePolicyFailure(GetCurrentRevision(), "runtime_policy_plan.not_applied",
                    $"Runtime policy plan '{planId}' is not currently applied.");
            }

            if (plan.AppliedRevision != _history.State.Revision.Value)
            {
                return RuntimePolicyFailure(plan.Command.BaseRevision.Value, "runtime_policy_plan.revision_conflict",
                    "Only the most recently applied plan can be reverted without overwriting later work.");
            }

            var undone = _history.Undo();
            if (!undone.Succeeded)
            {
                return RuntimePolicyFailure(plan.Command.BaseRevision.Value,
                    "runtime_policy_plan.inverse_unavailable", "The inverse operation is unavailable.");
            }

            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return RuntimePolicyFailure(plan.Command.BaseRevision.Value, "runtime_policy_plan.save_failed",
                    "The reverted project could not be saved.");
            }

            plan.Reverted = true;
            plan.Audit.Add(new RuntimePolicyAuditEntry(planId, "revert", undone.State.Revision.Value,
                _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public CinematicResource GetCinematics(string? areaId = null)
    {
        lock (_gate)
        {
            var project = _history.State.Project;
            var selected = string.IsNullOrWhiteSpace(areaId)
                ? project.Areas.Single(item => item.Id == project.Module.EntryAreaId)
                : project.Areas.Single(item => item.Id.Value == Guid.Parse(areaId));
            return new CinematicResource(1, _history.State.Revision.Value, selected.Id.ToString(),
                project.Module.RuntimePolicies.Cinematics, selected.RuntimePolicies.Cinematics);
        }
    }

    public CinematicPlanResult PlanCinematics(CinematicPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return CinematicFailure(request.BaseRevision, "cinematic_plan.schema_unsupported",
                    "Cinematic plan schema must be 1.");
            SetCinematicProfilesCommand command;
            try
            {
                command = new SetCinematicProfilesCommand(CommandId.From(Guid.NewGuid()),
                    new ProjectRevision(request.BaseRevision), request.Module, AreaId.From(Guid.Parse(request.AreaId)),
                    request.Area, true);
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                return CinematicFailure(request.BaseRevision, "cinematic_plan.invalid", exception.Message);
            }

            var preview = _cinematicHandler.Handle(_history.State, command);
            if (!preview.Succeeded)
                return new CinematicPlanResult(false, null, request.BaseRevision, preview.Patch, preview.Inverse,
                    preview.Diagnostics.Select(Map).ToArray(), false, false, []);
            var planId = Guid.NewGuid().ToString("D");
            var plan = new StoredCinematicPlan(planId, command, preview.Patch!, preview.Inverse!,
                [new CinematicAuditEntry(planId, "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _cinematicPlans.Add(planId, plan);
            return ToResult(plan);
        }
    }

    public CinematicPlanResult ApplyCinematicPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_cinematicPlans.TryGetValue(planId, out var plan))
                return CinematicFailure(GetCurrentRevision(), "cinematic_plan.missing", $"Cinematic plan '{planId}' does not exist.");
            if (plan.Applied)
                return CinematicFailure(plan.Command.BaseRevision.Value, "cinematic_plan.already_applied",
                    $"Cinematic plan '{planId}' is already applied.");
            var command = plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
            var result = _cinematicHandler.Handle(_history.State, command);
            if (!result.Succeeded)
            {
                plan.Audit.Add(new CinematicAuditEntry(planId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return ToResult(plan) with { Succeeded = false, Diagnostics = result.Diagnostics.Select(Map).ToArray() };
            }
            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = saved.Diagnostics.Select(item =>
                    new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray()
                };
            _history.Record(result);
            plan.Applied = true;
            plan.AppliedRevision = result.NewRevision!.Value.Value;
            plan.Audit.Add(new CinematicAuditEntry(planId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public CinematicPlanResult RevertCinematicPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_cinematicPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return CinematicFailure(GetCurrentRevision(), "cinematic_plan.not_applied",
                    $"Cinematic plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _history.State.Revision.Value)
                return CinematicFailure(plan.Command.BaseRevision.Value, "cinematic_plan.revision_conflict",
                    "Only the most recently applied plan can be reverted without overwriting later work.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return CinematicFailure(plan.Command.BaseRevision.Value, "cinematic_plan.inverse_unavailable",
                    "The inverse operation is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return CinematicFailure(plan.Command.BaseRevision.Value, "cinematic_plan.save_failed",
                    "The reverted project could not be saved.");
            }
            plan.Reverted = true;
            plan.Audit.Add(new CinematicAuditEntry(planId, "revert", undone.State.Revision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public GameplayControlResource GetGameplayControls()
    {
        lock (_gate)
        {
            return new GameplayControlResource(1, _history.State.Revision.Value,
                _history.State.Project.Module.RuntimePolicies.Gameplay);
        }
    }

    public GameplayFoundationResource QueryGameplayFoundation(string? query = null, string? kind = null)
    {
        lock (_gate)
        {
            var foundation = _history.State.Project.Module.RuntimePolicies.Gameplay.Foundation;
            var items = new List<GameplayFoundationItem>
            {
                new(foundation.GroundedMovement.Id, "movement", "grounded", "Déplacement au sol",
                    foundation.GroundedMovement.Provenance),
                new(foundation.Interaction.Id, "interaction", "interaction", "Système d'interaction",
                    foundation.Interaction.Provenance),
                new(foundation.Cameras.Id, "camera-catalog", "cameras", "Caméras configurables",
                    foundation.Cameras.Provenance),
                new(foundation.Input.Id, "input-profile", "input", "Contrôles et input mapping",
                    foundation.Input.Provenance),
                new(foundation.Save.Id, "save-policy", "save", "Sauvegarde et chargement",
                    foundation.Save.Provenance),
            };
            items.AddRange(foundation.Interaction.Verbs.Select(item =>
                new GameplayFoundationItem(item.Id, "interaction-verb", item.Key, item.Label,
                    foundation.Interaction.Provenance)));
            items.AddRange(foundation.Cameras.Profiles.Select(item =>
                new GameplayFoundationItem(item.Id, "camera", item.Key, item.DisplayName,
                    foundation.Cameras.Provenance)));
            items.AddRange(foundation.Input.Actions.Select(item =>
                new GameplayFoundationItem(item.Id, "input-action", item.Key, item.Label,
                    foundation.Input.Provenance)));
            items.AddRange(foundation.Input.Bindings.Select(item =>
                new GameplayFoundationItem(item.Id, "input-binding", item.ActionKey,
                    $"{item.Device}:{item.Binding}", foundation.Input.Provenance)));
            var simulation = _history.State.Project.Module.RuntimePolicies.Gameplay.Simulation;
            items.Add(new GameplayFoundationItem(simulation.Id, "simulation", "simulation",
                "Simulation gameplay 3D", simulation.Provenance));
            items.Add(new GameplayFoundationItem(simulation.Flight.Id, "simulation-flight", "flight",
                $"Vol 3D - {simulation.Flight.MaximumSpeed:0.#} m/s - {simulation.Flight.MaximumAltitude:0.#} m",
                simulation.Flight.Provenance));
            items.Add(new GameplayFoundationItem(simulation.Projectiles.Id, "simulation-projectile", "projectiles",
                $"Projectiles {simulation.Projectiles.Mode} - VFX {simulation.Projectiles.VfxAssetId}",
                simulation.Projectiles.Provenance));
            items.Add(new GameplayFoundationItem(simulation.Physics.Id, "simulation-physics", "physics",
                $"Physique - {simulation.Physics.Bodies.Count} corps - {simulation.Physics.Joints.Count} liaisons",
                simulation.Physics.Provenance));
            items.AddRange(simulation.Physics.Bodies.Select(item => new GameplayFoundationItem(item.Id,
                "simulation-physics-body", item.Name, $"{item.Mode} - {item.NetworkPolicy}", item.Provenance)));
            items.AddRange(simulation.Physics.Joints.Select(item => new GameplayFoundationItem(item.Id,
                "simulation-physics-joint", item.Name, $"{item.Mode} - {item.BodyAId} -> {item.BodyBId}",
                item.Provenance)));
            items.Add(new GameplayFoundationItem(simulation.Targeting.Id, "simulation-targeting", "targeting",
                $"Ciblage {simulation.Targeting.Mode} - {simulation.Targeting.MaximumRange:0.#} m",
                simulation.Targeting.Provenance));
            items.Add(new GameplayFoundationItem(simulation.WorldTime.Id, "simulation-world-time", "world-time",
                $"Temps monde - {simulation.WorldTime.Calendar.HoursPerDay} h/jour - " +
                $"{simulation.WorldTime.Schedules.Count} planifications", simulation.WorldTime.Provenance));
            items.AddRange(simulation.WorldTime.Schedules.Select(item => new GameplayFoundationItem(item.Id,
                "simulation-world-schedule", item.Key, $"{item.MinuteOfDay} min - {item.EventName}",
                item.Provenance)));
            var session = _history.State.Project.Module.RuntimePolicies.Gameplay.Session;
            items.Add(new GameplayFoundationItem(session.Id, "session", "session",
                "Session gameplay, sauvegarde et replay", session.Provenance));
            items.Add(new GameplayFoundationItem(session.Persistence.Id, "session-persistence", "persistence",
                $"{session.Persistence.Scope} - {session.Persistence.Authority} - {session.Persistence.ConflictPolicy}",
                session.Persistence.Provenance));
            items.AddRange(session.Checkpoints.Select(item => new GameplayFoundationItem(item.Id,
                "session-checkpoint", item.WaypointId,
                $"{item.PartyPolicy} - cout {item.CurrencyCost} - rayon {item.SafeRadius:0.#}", item.Provenance)));
            items.Add(new GameplayFoundationItem(session.Shell.Id, "session-shell", "runtime-shell",
                $"{session.Shell.KitId} - {session.Shell.SkinId} - {session.Shell.Scale:0.##}x",
                session.Shell.Provenance));
            items.AddRange(session.ControlLocks.Contexts.Select(item => new GameplayFoundationItem(item.Id,
                "session-control", item.OwnerId,
                $"camera {item.CameraMode} - priorite {item.Priority}", item.Provenance)));
            items.Add(new GameplayFoundationItem(session.Replay.Id, "session-replay", "replay",
                $"headless={session.Replay.HeadlessReplay} - {session.Replay.MaximumFrames} frames",
                session.Replay.Provenance));
            var normalizedQuery = query?.Trim() ?? string.Empty;
            var normalizedKind = kind?.Trim().ToLowerInvariant() ?? string.Empty;
            var filtered = items.Where(item => string.IsNullOrEmpty(normalizedKind) || item.Kind == normalizedKind)
                .Where(item => string.IsNullOrEmpty(normalizedQuery) ||
                    item.Key.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                    item.Label.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.Kind, StringComparer.Ordinal).ThenBy(item => item.Key, StringComparer.Ordinal)
                .Take(200).ToArray();
            return new(1, _history.State.Revision.Value, normalizedQuery, normalizedKind, filtered);
        }
    }

    public GameplayControlPlanResult PlanGameplayControls(GameplayControlPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return GameplayControlFailure(request.BaseRevision, "gameplay_control_plan.schema_unsupported",
                    "Gameplay control plan schema must be 1.");
            var command = new SetGameplayControlProfileCommand(CommandId.From(Guid.NewGuid()),
                new ProjectRevision(request.BaseRevision), request.Profile, true);
            var preview = _gameplayControlHandler.Handle(_history.State, command);
            if (!preview.Succeeded)
                return new GameplayControlPlanResult(false, null, request.BaseRevision, preview.Patch,
                    preview.Inverse, preview.Diagnostics.Select(Map).ToArray(), false, false, []);
            var planId = Guid.NewGuid().ToString("D");
            var plan = new StoredGameplayControlPlan(planId, command, preview.Patch!, preview.Inverse!,
                [new GameplayControlAuditEntry(planId, "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _gameplayControlPlans.Add(planId, plan);
            return ToResult(plan);
        }
    }

    public GameplayControlPlanResult ApplyGameplayControlPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_gameplayControlPlans.TryGetValue(planId, out var plan))
                return GameplayControlFailure(GetCurrentRevision(), "gameplay_control_plan.missing",
                    $"Gameplay control plan '{planId}' does not exist.");
            if (plan.Applied)
                return GameplayControlFailure(plan.Command.BaseRevision.Value,
                    "gameplay_control_plan.already_applied", $"Gameplay control plan '{planId}' is already applied.");
            var command = plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
            var result = _gameplayControlHandler.Handle(_history.State, command);
            if (!result.Succeeded)
            {
                plan.Audit.Add(new GameplayControlAuditEntry(planId, "apply", _history.State.Revision.Value,
                    _clock.UtcNow, false));
                return ToResult(plan) with { Succeeded = false, Diagnostics = result.Diagnostics.Select(Map).ToArray() };
            }

            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = saved.Diagnostics.Select(item =>
                        new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
                };
            _history.Record(result);
            plan.Applied = true;
            plan.AppliedRevision = result.NewRevision!.Value.Value;
            plan.Audit.Add(new GameplayControlAuditEntry(planId, "apply", plan.AppliedRevision.Value,
                _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public GameplayControlPlanResult RevertGameplayControlPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_gameplayControlPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return GameplayControlFailure(GetCurrentRevision(), "gameplay_control_plan.not_applied",
                    $"Gameplay control plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _history.State.Revision.Value)
                return GameplayControlFailure(plan.Command.BaseRevision.Value,
                    "gameplay_control_plan.revision_conflict",
                    "Only the most recently applied plan can be reverted without overwriting later work.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return GameplayControlFailure(plan.Command.BaseRevision.Value,
                    "gameplay_control_plan.inverse_unavailable", "The inverse operation is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return GameplayControlFailure(plan.Command.BaseRevision.Value, "gameplay_control_plan.save_failed",
                    "The reverted project could not be saved.");
            }

            plan.Reverted = true;
            plan.Audit.Add(new GameplayControlAuditEntry(planId, "revert", undone.State.Revision.Value,
                _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public ProjectVariableResource QueryProjectVariables(string? query = null, string? scope = null)
    {
        lock (_gate)
        {
            ProjectVariableScope? parsedScope = null;
            if (!string.IsNullOrWhiteSpace(scope))
            {
                if (!Enum.TryParse<ProjectVariableScope>(scope, true, out var value) || !Enum.IsDefined(value))
                    throw new ArgumentException($"Unknown project variable scope '{scope}'.", nameof(scope));
                parsedScope = value;
            }
            var normalized = query?.Trim() ?? string.Empty;
            var service = new ProjectBlackboardScriptService(_history.State.Project.Module.Variables);
            var ids = service.Query(normalized, parsedScope).Select(item => item.Id).ToHashSet();
            var variables = _history.State.Project.Module.Variables.Where(item => ids.Contains(item.Id)).ToArray();
            return new ProjectVariableResource(1, _history.State.Revision.Value, normalized,
                parsedScope?.ToString(), variables);
        }
    }

    public ProjectFoundationResource GetProjectFoundation()
    {
        lock (_gate)
        {
            var metadata = _history.State.Project.Module.Metadata;
            return new ProjectFoundationResource(1, _history.State.Revision.Value, metadata,
                metadata.Foundation, metadata.Foundation?.Identity);
        }
    }

    public ProjectFoundationPlanResult PlanProjectFoundation(ProjectFoundationPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion == 2)
            {
                if (request.MetadataPatch is null && request.Foundation is null)
                    return ProjectFoundationFailure(request.BaseRevision,
                        "project_foundation_plan.patch_required",
                        "Schema 2 requires a typed metadata patch or foundation.");
                var patchCommand = new PatchModuleMetadataCommand(CommandId.From(Guid.NewGuid()),
                    new ProjectRevision(request.BaseRevision), request.MetadataPatch, request.Foundation, true);
                var patchPreview = _moduleMetadataPatchHandler.Handle(_history.State, patchCommand);
                if (!patchPreview.Succeeded)
                    return new(false, null, request.BaseRevision, patchPreview.Patch, patchPreview.Inverse,
                        patchPreview.Diagnostics.Select(Map).ToArray(), false, false, []);
                var projection = _moduleMetadataPatchHandler.Handle(_history.State,
                    patchCommand with { DryRun = false });
                if (!projection.Succeeded)
                    return new(false, null, request.BaseRevision, projection.Patch, projection.Inverse,
                        projection.Diagnostics.Select(Map).ToArray(), false, false, []);
                var patchPlanId = Guid.NewGuid().ToString("D");
                var changedPaths = (request.MetadataPatch?.RelativePaths ?? []).ToList();
                if (request.Foundation is not null) changedPaths.Add("module/metadata/foundation");
                var review = ModuleMetadataLocalizationReviewBuilder.Build(projection.State.Project);
                var patchPlan = new StoredProjectMetadataPatchPlan(patchPlanId, patchCommand,
                    patchPreview.Patch!, patchPreview.Inverse!, changedPaths, review,
                    [new ProjectFoundationAuditEntry(patchPlanId, "planned", request.BaseRevision,
                        _clock.UtcNow, true)]);
                _projectMetadataPatchPlans.Add(patchPlanId, patchPlan);
                return ToResult(patchPlan);
            }
            if (request.SchemaVersion != 1)
                return ProjectFoundationFailure(request.BaseRevision, "project_foundation_plan.schema_unsupported",
                    "Project foundation plan schema must be 1 or 2.");
            if (request.Foundation is null)
                return ProjectFoundationFailure(request.BaseRevision, "project_foundation_plan.foundation_required",
                    "Schema 1 requires a complete project foundation.");
            var command = new SetProjectFoundationCommand(CommandId.From(Guid.NewGuid()),
                new ProjectRevision(request.BaseRevision), request.Foundation, true);
            var preview = _projectFoundationHandler.Handle(_history.State, command);
            if (!preview.Succeeded)
                return new(false, null, request.BaseRevision, preview.Patch, preview.Inverse,
                    preview.Diagnostics.Select(Map).ToArray(), false, false, []);
            var planId = Guid.NewGuid().ToString("D");
            var plan = new StoredProjectFoundationPlan(planId, command, preview.Patch!, preview.Inverse!,
                [new ProjectFoundationAuditEntry(planId, "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _projectFoundationPlans.Add(planId, plan);
            return ToResult(plan);
        }
    }

    public ProjectFoundationPlanResult ApplyProjectFoundationPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (_projectMetadataPatchPlans.TryGetValue(planId, out var patchPlan))
                return ApplyProjectMetadataPatchPlan(patchPlan);
            if (!_projectFoundationPlans.TryGetValue(planId, out var plan))
                return ProjectFoundationFailure(GetCurrentRevision(), "project_foundation_plan.missing",
                    $"Project foundation plan '{planId}' does not exist.");
            if (plan.Applied)
                return ProjectFoundationFailure(plan.Command.BaseRevision.Value,
                    "project_foundation_plan.already_applied",
                    $"Project foundation plan '{planId}' is already applied.");
            var command = plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
            var result = _projectFoundationHandler.Handle(_history.State, command);
            if (!result.Succeeded)
            {
                plan.Audit.Add(new(planId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return ToResult(plan) with { Succeeded = false, Diagnostics = result.Diagnostics.Select(Map).ToArray() };
            }
            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = saved.Diagnostics.Select(item =>
                        new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
                };
            _history.Record(result);
            plan.Applied = true;
            plan.AppliedRevision = result.NewRevision!.Value.Value;
            plan.Audit.Add(new(planId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public ProjectFoundationPlanResult RevertProjectFoundationPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (_projectMetadataPatchPlans.TryGetValue(planId, out var patchPlan))
                return RevertProjectMetadataPatchPlan(patchPlan);
            if (!_projectFoundationPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return ProjectFoundationFailure(GetCurrentRevision(), "project_foundation_plan.not_applied",
                    $"Project foundation plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _history.State.Revision.Value)
                return ProjectFoundationFailure(plan.Command.BaseRevision.Value,
                    "project_foundation_plan.revision_conflict",
                    "Only the most recently applied plan can be reverted without overwriting later work.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return ProjectFoundationFailure(plan.Command.BaseRevision.Value,
                    "project_foundation_plan.inverse_unavailable", "The inverse operation is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return ProjectFoundationFailure(plan.Command.BaseRevision.Value,
                    "project_foundation_plan.save_failed", "The reverted project could not be saved.");
            }
            plan.Reverted = true;
            plan.Audit.Add(new(planId, "revert", undone.State.Revision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    private ProjectFoundationPlanResult ApplyProjectMetadataPatchPlan(StoredProjectMetadataPatchPlan plan)
    {
        if (plan.Applied)
            return ProjectFoundationFailure(plan.Command.BaseRevision.Value,
                "project_foundation_plan.already_applied",
                $"Project foundation plan '{plan.PlanId}' is already applied.");
        var result = _moduleMetadataPatchHandler.Handle(_history.State,
            plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false });
        if (!result.Succeeded)
        {
            plan.Audit.Add(new(plan.PlanId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
            return ToResult(plan) with
            {
                Succeeded = false,
                Diagnostics = result.Diagnostics.Select(Map).ToArray(),
            };
        }
        var saved = _session.Save(result.State.Project);
        if (!saved.Succeeded)
            return ToResult(plan) with
            {
                Succeeded = false,
                Diagnostics = saved.Diagnostics.Select(item =>
                    new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
            };
        _history.Record(result);
        plan.Applied = true;
        plan.AppliedRevision = result.NewRevision!.Value.Value;
        plan.Audit.Add(new(plan.PlanId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
        return ToResult(plan);
    }

    private ProjectFoundationPlanResult RevertProjectMetadataPatchPlan(StoredProjectMetadataPatchPlan plan)
    {
        if (!plan.Applied || plan.Reverted)
            return ProjectFoundationFailure(GetCurrentRevision(), "project_foundation_plan.not_applied",
                $"Project foundation plan '{plan.PlanId}' is not currently applied.");
        if (plan.AppliedRevision != _history.State.Revision.Value)
            return ProjectFoundationFailure(plan.Command.BaseRevision.Value,
                "project_foundation_plan.revision_conflict",
                "Only the most recently applied plan can be reverted without overwriting later work.");
        var undone = _history.Undo();
        if (!undone.Succeeded)
            return ProjectFoundationFailure(plan.Command.BaseRevision.Value,
                "project_foundation_plan.inverse_unavailable", "The inverse operation is unavailable.");
        var saved = _session.Save(undone.State.Project);
        if (!saved.Succeeded)
        {
            _history.Redo();
            return ProjectFoundationFailure(plan.Command.BaseRevision.Value,
                "project_foundation_plan.save_failed", "The reverted project could not be saved.");
        }
        plan.Reverted = true;
        plan.Audit.Add(new(plan.PlanId, "revert", undone.State.Revision.Value, _clock.UtcNow, true));
        return ToResult(plan);
    }

    public EntityScaleResource GetEntityScales(string? areaId = null, IReadOnlyList<string>? entityIds = null)
    {
        lock (_gate)
        {
            AreaId? filterArea = null;
            if (!string.IsNullOrWhiteSpace(areaId))
            {
                if (!Guid.TryParse(areaId, out var parsedArea))
                    return new(1, GetCurrentRevision(), areaId, [],
                        [new("entity_scale.area_id_invalid", "Area ID must be a UUID.", "request/areaId")]);
                filterArea = AreaId.From(parsedArea);
            }

            var requestedIds = new HashSet<EntityId>();
            foreach (var value in entityIds ?? [])
            {
                if (!Guid.TryParse(value, out var parsedEntity))
                    return new(1, GetCurrentRevision(), areaId, [],
                        [new("entity_scale.entity_id_invalid", $"Entity ID '{value}' must be a UUID.", "request/entityIds")]);
                requestedIds.Add(EntityId.From(parsedEntity));
            }

            var items = _history.State.Project.Areas
                .Where(area => filterArea is null || area.Id == filterArea)
                .SelectMany(area => area.Entities
                    .Where(entity => requestedIds.Count == 0 || requestedIds.Contains(entity.Id))
                    .Select(entity => new EntityScaleItem(entity.Id.ToString(), entity.Transform.Scale,
                        $"areas/{area.Id}/entities/{entity.Id}/scale")))
                .OrderBy(item => item.DocumentPath, StringComparer.Ordinal)
                .ToArray();
            return new(1, GetCurrentRevision(), areaId, items, []);
        }
    }

    public EntityScalePlanResult PlanEntityScale(EntityScalePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return EntityScaleFailure(request.BaseRevision, "entity_scale_plan.schema_unsupported",
                    "Entity scale plan schema must be 1.");
            if (!Guid.TryParse(request.AreaId, out var areaValue))
                return EntityScaleFailure(request.BaseRevision, "entity_scale_plan.area_id_invalid",
                    "Area ID must be a UUID.");
            var entityIds = new List<EntityId>();
            foreach (var value in request.EntityIds)
            {
                if (!Guid.TryParse(value, out var parsed))
                    return EntityScaleFailure(request.BaseRevision, "entity_scale_plan.entity_id_invalid",
                        $"Entity ID '{value}' must be a UUID.");
                entityIds.Add(EntityId.From(parsed));
            }
            var command = new ScaleEntitiesCommand(CommandId.From(Guid.NewGuid()),
                new ProjectRevision(request.BaseRevision), AreaId.From(areaValue), entityIds,
                request.Scale, request.Multiply, true);
            var preview = _scaleEntitiesHandler.Handle(_history.State, command);
            if (!preview.Succeeded)
                return new(false, null, request.BaseRevision, preview.Patch, preview.Inverse,
                    preview.Diagnostics.Select(Map).ToArray(), false, false, []);
            var planId = Guid.NewGuid().ToString("D");
            var plan = new StoredEntityScalePlan(planId, command, preview.Patch!, preview.Inverse!,
                [new EntityScaleAuditEntry(planId, "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _entityScalePlans.Add(planId, plan);
            return ToResult(plan);
        }
    }

    public EntityScalePlanResult ApplyEntityScalePlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_entityScalePlans.TryGetValue(planId, out var plan))
                return EntityScaleFailure(GetCurrentRevision(), "entity_scale_plan.missing",
                    $"Entity scale plan '{planId}' does not exist.");
            if (plan.Applied)
                return EntityScaleFailure(plan.Command.BaseRevision.Value, "entity_scale_plan.already_applied",
                    $"Entity scale plan '{planId}' is already applied.");
            var command = plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
            var result = _scaleEntitiesHandler.Handle(_history.State, command);
            if (!result.Succeeded)
            {
                plan.Audit.Add(new(planId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return ToResult(plan) with { Succeeded = false, Diagnostics = result.Diagnostics.Select(Map).ToArray() };
            }
            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = saved.Diagnostics.Select(item =>
                        new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
                };
            _history.Record(result);
            plan.Applied = true;
            plan.AppliedRevision = result.NewRevision!.Value.Value;
            plan.Audit.Add(new(planId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public EntityScalePlanResult RevertEntityScalePlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_entityScalePlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return EntityScaleFailure(GetCurrentRevision(), "entity_scale_plan.not_applied",
                    $"Entity scale plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _history.State.Revision.Value)
                return EntityScaleFailure(plan.Command.BaseRevision.Value, "entity_scale_plan.revision_conflict",
                    "Only the most recently applied plan can be reverted without overwriting later work.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return EntityScaleFailure(plan.Command.BaseRevision.Value, "entity_scale_plan.inverse_unavailable",
                    "The inverse operation is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return EntityScaleFailure(plan.Command.BaseRevision.Value, "entity_scale_plan.save_failed",
                    "The reverted project could not be saved.");
            }
            plan.Reverted = true;
            plan.Audit.Add(new(planId, "revert", undone.State.Revision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public EntityTranslationPlanResult PlanEntityTranslation(EntityTranslationPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return EntityTranslationFailure(request.BaseRevision,
                    "entity_translation_plan.schema_unsupported", "Entity translation plan schema must be 1.");
            if (!Guid.TryParse(request.AreaId, out var areaValue))
                return EntityTranslationFailure(request.BaseRevision,
                    "entity_translation_plan.area_id_invalid", "Area ID must be a UUID.");
            var entityIds = new List<EntityId>();
            foreach (var value in request.EntityIds)
            {
                if (!Guid.TryParse(value, out var parsed))
                    return EntityTranslationFailure(request.BaseRevision,
                        "entity_translation_plan.entity_id_invalid", $"Entity ID '{value}' must be a UUID.");
                entityIds.Add(EntityId.From(parsed));
            }

            var command = new TranslateEntitiesCommand(
                CommandId.From(Guid.NewGuid()),
                new ProjectRevision(request.BaseRevision),
                AreaId.From(areaValue),
                entityIds,
                request.Delta,
                DryRun: true);
            var preview = _translateEntitiesHandler.Handle(_history.State, command);
            if (!preview.Succeeded)
                return new(false, null, request.BaseRevision, preview.Patch, preview.Inverse,
                    preview.Diagnostics.Select(Map).ToArray(), false, false, []);

            var planId = Guid.NewGuid().ToString("D");
            var plan = new StoredEntityTranslationPlan(planId, command, preview.Patch!, preview.Inverse!,
                [new EntityTranslationAuditEntry(planId, "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _entityTranslationPlans.Add(planId, plan);
            return ToResult(plan);
        }
    }

    public EntityTranslationPlanResult ApplyEntityTranslationPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_entityTranslationPlans.TryGetValue(planId, out var plan))
                return EntityTranslationFailure(GetCurrentRevision(), "entity_translation_plan.missing",
                    $"Entity translation plan '{planId}' does not exist.");
            if (plan.Applied)
                return EntityTranslationFailure(plan.Command.BaseRevision.Value,
                    "entity_translation_plan.already_applied",
                    $"Entity translation plan '{planId}' is already applied.");

            var command = plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
            var result = _translateEntitiesHandler.Handle(_history.State, command);
            if (!result.Succeeded)
            {
                plan.Audit.Add(new(planId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = result.Diagnostics.Select(Map).ToArray(),
                };
            }

            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = saved.Diagnostics.Select(item =>
                        new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
                };
            _history.Record(result);
            plan.Applied = true;
            plan.AppliedRevision = result.NewRevision!.Value.Value;
            plan.Audit.Add(new(planId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public EntityTranslationPlanResult RevertEntityTranslationPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_entityTranslationPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return EntityTranslationFailure(GetCurrentRevision(), "entity_translation_plan.not_applied",
                    $"Entity translation plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _history.State.Revision.Value)
                return EntityTranslationFailure(plan.Command.BaseRevision.Value,
                    "entity_translation_plan.revision_conflict",
                    "Only the most recently applied plan can be reverted without overwriting later work.");

            var undone = _history.Undo();
            if (!undone.Succeeded)
                return EntityTranslationFailure(plan.Command.BaseRevision.Value,
                    "entity_translation_plan.inverse_unavailable", "The inverse operation is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return EntityTranslationFailure(plan.Command.BaseRevision.Value,
                    "entity_translation_plan.save_failed", "The reverted project could not be saved.");
            }
            plan.Reverted = true;
            plan.Audit.Add(new(planId, "revert", undone.State.Revision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public AreaFoundationResource GetAreaFoundation(string areaId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(areaId);
        lock (_gate)
        {
            if (!Guid.TryParse(areaId, out var parsed))
                return new(1, GetCurrentRevision(), areaId, null, null, null,
                    [new("area_foundation.area_id_invalid", "Area ID must be a UUID.")]);
            var area = _history.State.Project.Areas.SingleOrDefault(item => item.Id.Value == parsed);
            return area is null
                ? new(1, GetCurrentRevision(), areaId, null, null, null,
                    [new("area_foundation.area_missing", $"Area '{areaId}' does not exist.")])
                : new(1, GetCurrentRevision(), areaId, area.DisplayName, area.Foundation,
                    AreaFoundationScriptService.Read(area), []);
        }
    }

    public AreaFoundationPlanResult PlanAreaFoundation(AreaFoundationPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return AreaFoundationFailure(request.BaseRevision, "area_foundation_plan.schema_unsupported",
                    "Area foundation plan schema must be 1.");
            if (!Guid.TryParse(request.AreaId, out var areaValue))
                return AreaFoundationFailure(request.BaseRevision, "area_foundation_plan.area_id_invalid",
                    "Area ID must be a UUID.");
            var command = new SetAreaFoundationCommand(CommandId.From(Guid.NewGuid()),
                new ProjectRevision(request.BaseRevision), AreaId.From(areaValue), request.Foundation, true);
            var preview = _areaFoundationHandler.Handle(_history.State, command);
            if (!preview.Succeeded)
                return new(false, null, request.BaseRevision, preview.Patch, preview.Inverse,
                    preview.Diagnostics.Select(Map).ToArray(), false, false, []);
            var planId = Guid.NewGuid().ToString("D");
            var plan = new StoredAreaFoundationPlan(planId, command, preview.Patch!, preview.Inverse!,
                [new AreaFoundationAuditEntry(planId, "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _areaFoundationPlans.Add(planId, plan);
            return ToResult(plan);
        }
    }

    public AreaFoundationPlanResult ApplyAreaFoundationPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_areaFoundationPlans.TryGetValue(planId, out var plan))
                return AreaFoundationFailure(GetCurrentRevision(), "area_foundation_plan.missing",
                    $"Area foundation plan '{planId}' does not exist.");
            if (plan.Applied)
                return AreaFoundationFailure(plan.Command.BaseRevision.Value,
                    "area_foundation_plan.already_applied", $"Area foundation plan '{planId}' is already applied.");
            var command = plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
            var result = _areaFoundationHandler.Handle(_history.State, command);
            if (!result.Succeeded)
            {
                plan.Audit.Add(new(planId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return ToResult(plan) with { Succeeded = false, Diagnostics = result.Diagnostics.Select(Map).ToArray() };
            }
            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = saved.Diagnostics.Select(item =>
                        new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
                };
            _history.Record(result);
            plan.Applied = true;
            plan.AppliedRevision = result.NewRevision!.Value.Value;
            plan.Audit.Add(new(planId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public AreaFoundationPlanResult RevertAreaFoundationPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_areaFoundationPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return AreaFoundationFailure(GetCurrentRevision(), "area_foundation_plan.not_applied",
                    $"Area foundation plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _history.State.Revision.Value)
                return AreaFoundationFailure(plan.Command.BaseRevision.Value,
                    "area_foundation_plan.revision_conflict",
                    "Only the most recently applied plan can be reverted without overwriting later work.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return AreaFoundationFailure(plan.Command.BaseRevision.Value,
                    "area_foundation_plan.inverse_unavailable", "The inverse operation is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return AreaFoundationFailure(plan.Command.BaseRevision.Value,
                    "area_foundation_plan.save_failed", "The reverted project could not be saved.");
            }
            plan.Reverted = true;
            plan.Audit.Add(new(planId, "revert", undone.State.Revision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public AreaBiomeResource GetAreaBiomes(string areaId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(areaId);
        lock (_gate)
        {
            if (!Guid.TryParse(areaId, out var parsed))
                return new(2, GetCurrentRevision(), areaId, null, [],
                    [new("area_biomes.area_id_invalid", "Area ID must be a UUID.", "request/areaId")]);
            var area = _history.State.Project.Areas.SingleOrDefault(item => item.Id.Value == parsed);
            return area is null
                ? new(2, GetCurrentRevision(), areaId, null, [],
                    [new("area_biomes.area_missing", $"Area '{areaId}' does not exist.", $"areas/{areaId}")])
                : new(2, GetCurrentRevision(), areaId, area.DisplayName,
                    area.World.Biomes.Select(item =>
                        new AreaBiomeLayerResource(item, BiomeScatterAuthoring.Read(item))).ToArray(), []);
        }
    }

    public AreaBiomePlanResult PlanAreaBiomes(AreaBiomePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 2)
                return AreaBiomeFailure(request.BaseRevision, "area_biomes_plan.schema_unsupported",
                    "Area biome plan schema must be 2.");
            if (!Guid.TryParse(request.AreaId, out var areaValue))
                return AreaBiomeFailure(request.BaseRevision, "area_biomes_plan.area_id_invalid",
                    "Area ID must be a UUID.");
            var command = new PatchAreaBiomesCommand(CommandId.From(Guid.NewGuid()),
                new ProjectRevision(request.BaseRevision), AreaId.From(areaValue), request.Patches, true);
            var preview = _areaBiomeHandler.Handle(_history.State, command);
            if (!preview.Succeeded)
                return new(false, null, request.BaseRevision, preview.Patch, preview.Inverse,
                    preview.Diagnostics.Select(Map).ToArray(), false, false, []);
            var planId = Guid.NewGuid().ToString("D");
            var plan = new StoredAreaBiomePlan(planId, command, preview.Patch!, preview.Inverse!,
                [new AreaBiomeAuditEntry(planId, "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _areaBiomePlans.Add(planId, plan);
            return ToResult(plan);
        }
    }

    public AreaBiomePlanResult ApplyAreaBiomePlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_areaBiomePlans.TryGetValue(planId, out var plan))
                return AreaBiomeFailure(GetCurrentRevision(), "area_biomes_plan.missing",
                    $"Area biome plan '{planId}' does not exist.");
            if (plan.Applied)
                return AreaBiomeFailure(plan.Command.BaseRevision.Value, "area_biomes_plan.already_applied",
                    $"Area biome plan '{planId}' is already applied.");
            var result = _areaBiomeHandler.Handle(_history.State,
                plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false });
            if (!result.Succeeded)
            {
                plan.Audit.Add(new(planId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = result.Diagnostics.Select(Map).ToArray(),
                };
            }
            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = saved.Diagnostics.Select(item =>
                        new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
                };
            _history.Record(result);
            plan.Applied = true;
            plan.AppliedRevision = result.NewRevision!.Value.Value;
            plan.Audit.Add(new(planId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public AreaBiomePlanResult RevertAreaBiomePlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_areaBiomePlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return AreaBiomeFailure(GetCurrentRevision(), "area_biomes_plan.not_applied",
                    $"Area biome plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _history.State.Revision.Value)
                return AreaBiomeFailure(plan.Command.BaseRevision.Value, "area_biomes_plan.revision_conflict",
                    "Only the most recently applied plan can be reverted without overwriting later work.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return AreaBiomeFailure(plan.Command.BaseRevision.Value, "area_biomes_plan.inverse_unavailable",
                    "The inverse operation is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return AreaBiomeFailure(plan.Command.BaseRevision.Value, "area_biomes_plan.save_failed",
                    "The reverted project could not be saved.");
            }
            plan.Reverted = true;
            plan.Audit.Add(new(planId, "revert", undone.State.Revision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public AreaEnvironmentResource GetAreaEnvironment(string areaId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(areaId);
        lock (_gate)
        {
            if (!Guid.TryParse(areaId, out var parsed))
                return new(1, GetCurrentRevision(), areaId, null, null, null,
                    [new("area_environment.area_id_invalid", "Area ID must be a UUID.", "request/areaId")]);
            var area = _history.State.Project.Areas.SingleOrDefault(item => item.Id.Value == parsed);
            return area is null
                ? new(1, GetCurrentRevision(), areaId, null, null, null,
                    [new("area_environment.area_missing", $"Area '{areaId}' does not exist.",
                        $"areas/{areaId}")])
                : new(1, GetCurrentRevision(), areaId, area.DisplayName, area.Environment,
                    AreaEnvironmentScriptService.Read(area), []);
        }
    }

    public AreaEnvironmentPlanResult PlanAreaEnvironment(AreaEnvironmentPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion == 2)
            {
                if (request.Patch is null)
                    return AreaEnvironmentFailure(request.BaseRevision, "area_environment_plan.patch_required",
                        "Schema 2 requires a typed partial environment patch.");
                if (!Guid.TryParse(request.AreaId, out var patchAreaValue))
                    return AreaEnvironmentFailure(request.BaseRevision, "area_environment_plan.area_id_invalid",
                        "Area ID must be a UUID.");
                var patchCommand = new PatchAreaEnvironmentCommand(CommandId.From(Guid.NewGuid()),
                    new ProjectRevision(request.BaseRevision), AreaId.From(patchAreaValue), request.Patch, true);
                var patchPreview = _areaEnvironmentPatchHandler.Handle(_history.State, patchCommand);
                if (!patchPreview.Succeeded)
                    return new(false, null, request.BaseRevision, patchPreview.Patch, patchPreview.Inverse,
                        patchPreview.Diagnostics.Select(Map).ToArray(), false, false, []);
                var patchPlanId = Guid.NewGuid().ToString("D");
                var changedPaths = request.Patch.RelativePaths
                    .Select(path => $"areas/{request.AreaId}/{path}").ToArray();
                var patchPlan = new StoredAreaEnvironmentPatchPlan(patchPlanId, patchCommand,
                    patchPreview.Patch!, patchPreview.Inverse!, changedPaths,
                    [new AreaEnvironmentAuditEntry(patchPlanId, "planned", request.BaseRevision, _clock.UtcNow, true)]);
                _areaEnvironmentPatchPlans.Add(patchPlanId, patchPlan);
                return ToResult(patchPlan);
            }
            if (request.SchemaVersion != 1)
                return AreaEnvironmentFailure(request.BaseRevision, "area_environment_plan.schema_unsupported",
                    "Area environment plan schema must be 1 or 2.");
            if (request.Environment is null)
                return AreaEnvironmentFailure(request.BaseRevision, "area_environment_plan.environment_required",
                    "Schema 1 requires a complete environment profile.");
            if (!Guid.TryParse(request.AreaId, out var areaValue))
                return AreaEnvironmentFailure(request.BaseRevision, "area_environment_plan.area_id_invalid",
                    "Area ID must be a UUID.");
            var command = new SetAreaEnvironmentCommand(CommandId.From(Guid.NewGuid()),
                new ProjectRevision(request.BaseRevision), AreaId.From(areaValue), request.Environment, true);
            var preview = _areaEnvironmentHandler.Handle(_history.State, command);
            if (!preview.Succeeded)
                return new(false, null, request.BaseRevision, preview.Patch, preview.Inverse,
                    preview.Diagnostics.Select(Map).ToArray(), false, false, []);
            var planId = Guid.NewGuid().ToString("D");
            var plan = new StoredAreaEnvironmentPlan(planId, command, preview.Patch!, preview.Inverse!,
                [new AreaEnvironmentAuditEntry(planId, "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _areaEnvironmentPlans.Add(planId, plan);
            return ToResult(plan);
        }
    }

    public AreaEnvironmentPlanResult ApplyAreaEnvironmentPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (_areaEnvironmentPatchPlans.TryGetValue(planId, out var patchPlan))
                return ApplyAreaEnvironmentPatchPlan(patchPlan);
            if (!_areaEnvironmentPlans.TryGetValue(planId, out var plan))
                return AreaEnvironmentFailure(GetCurrentRevision(), "area_environment_plan.missing",
                    $"Area environment plan '{planId}' does not exist.");
            if (plan.Applied)
                return AreaEnvironmentFailure(plan.Command.BaseRevision.Value,
                    "area_environment_plan.already_applied",
                    $"Area environment plan '{planId}' is already applied.");
            var result = _areaEnvironmentHandler.Handle(_history.State,
                plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false });
            if (!result.Succeeded)
            {
                plan.Audit.Add(new(planId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = result.Diagnostics.Select(Map).ToArray(),
                };
            }
            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = saved.Diagnostics.Select(item =>
                        new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
                };
            _history.Record(result);
            plan.Applied = true;
            plan.AppliedRevision = result.NewRevision!.Value.Value;
            plan.Audit.Add(new(planId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public AreaEnvironmentPlanResult RevertAreaEnvironmentPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (_areaEnvironmentPatchPlans.TryGetValue(planId, out var patchPlan))
                return RevertAreaEnvironmentPatchPlan(patchPlan);
            if (!_areaEnvironmentPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return AreaEnvironmentFailure(GetCurrentRevision(), "area_environment_plan.not_applied",
                    $"Area environment plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _history.State.Revision.Value)
                return AreaEnvironmentFailure(plan.Command.BaseRevision.Value,
                    "area_environment_plan.revision_conflict",
                    "Only the most recently applied plan can be reverted without overwriting later work.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return AreaEnvironmentFailure(plan.Command.BaseRevision.Value,
                    "area_environment_plan.inverse_unavailable", "The inverse operation is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return AreaEnvironmentFailure(plan.Command.BaseRevision.Value,
                    "area_environment_plan.save_failed", "The reverted project could not be saved.");
            }
            plan.Reverted = true;
            plan.Audit.Add(new(planId, "revert", undone.State.Revision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    private AreaEnvironmentPlanResult ApplyAreaEnvironmentPatchPlan(StoredAreaEnvironmentPatchPlan plan)
    {
        if (plan.Applied)
            return AreaEnvironmentFailure(plan.Command.BaseRevision.Value,
                "area_environment_plan.already_applied",
                $"Area environment plan '{plan.PlanId}' is already applied.");
        var result = _areaEnvironmentPatchHandler.Handle(_history.State,
            plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false });
        if (!result.Succeeded)
        {
            plan.Audit.Add(new(plan.PlanId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
            return ToResult(plan) with
            {
                Succeeded = false,
                Diagnostics = result.Diagnostics.Select(Map).ToArray(),
            };
        }
        var saved = _session.Save(result.State.Project);
        if (!saved.Succeeded)
            return ToResult(plan) with
            {
                Succeeded = false,
                Diagnostics = saved.Diagnostics.Select(item =>
                    new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
            };
        _history.Record(result);
        plan.Applied = true;
        plan.AppliedRevision = result.NewRevision!.Value.Value;
        plan.Audit.Add(new(plan.PlanId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
        return ToResult(plan);
    }

    private AreaEnvironmentPlanResult RevertAreaEnvironmentPatchPlan(StoredAreaEnvironmentPatchPlan plan)
    {
        if (!plan.Applied || plan.Reverted)
            return AreaEnvironmentFailure(GetCurrentRevision(), "area_environment_plan.not_applied",
                $"Area environment plan '{plan.PlanId}' is not currently applied.");
        if (plan.AppliedRevision != _history.State.Revision.Value)
            return AreaEnvironmentFailure(plan.Command.BaseRevision.Value,
                "area_environment_plan.revision_conflict",
                "Only the most recently applied plan can be reverted without overwriting later work.");
        var undone = _history.Undo();
        if (!undone.Succeeded)
            return AreaEnvironmentFailure(plan.Command.BaseRevision.Value,
                "area_environment_plan.inverse_unavailable", "The inverse operation is unavailable.");
        var saved = _session.Save(undone.State.Project);
        if (!saved.Succeeded)
        {
            _history.Redo();
            return AreaEnvironmentFailure(plan.Command.BaseRevision.Value,
                "area_environment_plan.save_failed", "The reverted project could not be saved.");
        }
        plan.Reverted = true;
        plan.Audit.Add(new(plan.PlanId, "revert", undone.State.Revision.Value, _clock.UtcNow, true));
        return ToResult(plan);
    }

    public AreaExperienceAuthoringResource GetAreaExperienceAuthoring(string areaId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(areaId);
        lock (_gate)
        {
            if (!Guid.TryParse(areaId, out var parsed))
                return new(1, GetCurrentRevision(), areaId, null, null, null,
                    [new("area_experience.area_id_invalid", "Area ID must be a UUID.", "request/areaId")]);
            var area = _history.State.Project.Areas.SingleOrDefault(item => item.Id.Value == parsed);
            return area is null
                ? new(1, GetCurrentRevision(), areaId, null, null, null,
                    [new("area_experience.area_missing", $"Area '{areaId}' does not exist.", $"areas/{areaId}")])
                : new(1, GetCurrentRevision(), areaId, area.DisplayName, area.Authoring,
                    AreaExperienceAuthoringScriptService.Read(area), []);
        }
    }

    public AreaExperienceAuthoringPlanResult PlanAreaExperienceAuthoring(
        AreaExperienceAuthoringPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return AreaExperienceAuthoringFailure(request.BaseRevision,
                    "area_experience_plan.schema_unsupported", "Area experience plan schema must be 1.");
            if (!Guid.TryParse(request.AreaId, out var areaValue))
                return AreaExperienceAuthoringFailure(request.BaseRevision,
                    "area_experience_plan.area_id_invalid", "Area ID must be a UUID.");
            var command = new SetAreaAuthoringCommand(CommandId.From(Guid.NewGuid()),
                new ProjectRevision(request.BaseRevision), AreaId.From(areaValue), request.Authoring, true);
            var preview = _areaAuthoringHandler.Handle(_history.State, command);
            if (!preview.Succeeded)
                return new(false, null, request.BaseRevision, preview.Patch, preview.Inverse,
                    preview.Diagnostics.Select(Map).ToArray(), false, false, []);
            var planId = Guid.NewGuid().ToString("D");
            var plan = new StoredAreaExperienceAuthoringPlan(planId, command, preview.Patch!, preview.Inverse!,
                [new AreaExperienceAuthoringAuditEntry(planId, "planned", request.BaseRevision,
                    _clock.UtcNow, true)]);
            _areaExperienceAuthoringPlans.Add(planId, plan);
            return ToResult(plan);
        }
    }

    public AreaExperienceAuthoringPlanResult ApplyAreaExperienceAuthoringPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_areaExperienceAuthoringPlans.TryGetValue(planId, out var plan))
                return AreaExperienceAuthoringFailure(GetCurrentRevision(), "area_experience_plan.missing",
                    $"Area experience plan '{planId}' does not exist.");
            if (plan.Applied)
                return AreaExperienceAuthoringFailure(plan.Command.BaseRevision.Value,
                    "area_experience_plan.already_applied", $"Area experience plan '{planId}' is already applied.");
            var result = _areaAuthoringHandler.Handle(_history.State,
                plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false });
            if (!result.Succeeded)
            {
                plan.Audit.Add(new(planId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = result.Diagnostics.Select(Map).ToArray(),
                };
            }
            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = saved.Diagnostics.Select(item =>
                        new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
                };
            _history.Record(result);
            plan.Applied = true;
            plan.AppliedRevision = result.NewRevision!.Value.Value;
            plan.Audit.Add(new(planId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public AreaExperienceAuthoringPlanResult RevertAreaExperienceAuthoringPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_areaExperienceAuthoringPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return AreaExperienceAuthoringFailure(GetCurrentRevision(), "area_experience_plan.not_applied",
                    $"Area experience plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _history.State.Revision.Value)
                return AreaExperienceAuthoringFailure(plan.Command.BaseRevision.Value,
                    "area_experience_plan.revision_conflict",
                    "Only the most recently applied plan can be reverted without overwriting later work.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return AreaExperienceAuthoringFailure(plan.Command.BaseRevision.Value,
                    "area_experience_plan.inverse_unavailable", "The inverse operation is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return AreaExperienceAuthoringFailure(plan.Command.BaseRevision.Value,
                    "area_experience_plan.save_failed", "The reverted project could not be saved.");
            }
            plan.Reverted = true;
            plan.Audit.Add(new(planId, "revert", undone.State.Revision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public AreaRuntimeResource GetAreaRuntime(string areaId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(areaId);
        lock (_gate)
        {
            if (!Guid.TryParse(areaId, out var parsed))
                return new(1, GetCurrentRevision(), areaId, null, null, [], null, null,
                    [new("area_runtime.area_id_invalid", "Area ID must be a UUID.", "request/areaId")]);
            var id = AreaId.From(parsed);
            var area = _history.State.Project.Areas.SingleOrDefault(item => item.Id == id);
            if (area is null)
                return new(1, GetCurrentRevision(), areaId, null, null, [], null, null,
                    [new("area_runtime.area_missing", $"Area '{areaId}' does not exist.", $"areas/{areaId}")]);
            try
            {
                return new(1, GetCurrentRevision(), areaId, area.DisplayName,
                    AreaOperationsScriptService.ReadNavigation(_history.State.Project, id),
                    AreaOperationsScriptService.QueryPortals(_history.State.Project, id), area.Playtest,
                    AreaOperationsScriptService.Validate(_history.State.Project, id), []);
            }
            catch (InvalidOperationException exception)
            {
                return new(1, GetCurrentRevision(), areaId, area.DisplayName, null,
                    AreaOperationsScriptService.QueryPortals(_history.State.Project, id), area.Playtest, null,
                    [new("area_runtime.navigation_unavailable", exception.Message,
                        $"areas/{areaId}/navigation")]);
            }
        }
    }

    public AreaPlaytestPlanResult PlanAreaPlaytest(AreaPlaytestPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return AreaPlaytestFailure(request.BaseRevision, "area_playtest_plan.schema_unsupported",
                    "Area Play From Here plan schema must be 1.");
            if (!Guid.TryParse(request.AreaId, out var areaValue))
                return AreaPlaytestFailure(request.BaseRevision, "area_playtest_plan.area_id_invalid",
                    "Area ID must be a UUID.");
            var command = new SetAreaPlaytestCommand(CommandId.From(Guid.NewGuid()),
                new ProjectRevision(request.BaseRevision), AreaId.From(areaValue), request.Profile, true);
            var preview = _areaPlaytestHandler.Handle(_history.State, command);
            if (!preview.Succeeded)
                return new(false, null, request.BaseRevision, preview.Patch, preview.Inverse,
                    preview.Diagnostics.Select(Map).ToArray(), false, false, []);
            var planId = Guid.NewGuid().ToString("D");
            var plan = new StoredAreaPlaytestPlan(planId, command, preview.Patch!, preview.Inverse!,
                [new AreaPlaytestAuditEntry(planId, "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _areaPlaytestPlans.Add(planId, plan);
            return ToResult(plan);
        }
    }

    public AreaPlaytestPlanResult ApplyAreaPlaytestPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_areaPlaytestPlans.TryGetValue(planId, out var plan))
                return AreaPlaytestFailure(GetCurrentRevision(), "area_playtest_plan.missing",
                    $"Area Play From Here plan '{planId}' does not exist.");
            if (plan.Applied)
                return AreaPlaytestFailure(plan.Command.BaseRevision.Value, "area_playtest_plan.already_applied",
                    $"Area Play From Here plan '{planId}' is already applied.");
            var command = plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
            var result = _areaPlaytestHandler.Handle(_history.State, command);
            if (!result.Succeeded)
            {
                plan.Audit.Add(new(planId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return ToResult(plan) with { Succeeded = false, Diagnostics = result.Diagnostics.Select(Map).ToArray() };
            }
            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = saved.Diagnostics.Select(item =>
                        new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
                };
            _history.Record(result);
            plan.Applied = true;
            plan.AppliedRevision = result.NewRevision!.Value.Value;
            plan.Audit.Add(new(planId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public AreaPlaytestPlanResult RevertAreaPlaytestPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_areaPlaytestPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return AreaPlaytestFailure(GetCurrentRevision(), "area_playtest_plan.not_applied",
                    $"Area Play From Here plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _history.State.Revision.Value)
                return AreaPlaytestFailure(plan.Command.BaseRevision.Value,
                    "area_playtest_plan.revision_conflict",
                    "Only the most recently applied plan can be reverted without overwriting later work.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return AreaPlaytestFailure(plan.Command.BaseRevision.Value,
                    "area_playtest_plan.inverse_unavailable", "The inverse operation is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return AreaPlaytestFailure(plan.Command.BaseRevision.Value,
                    "area_playtest_plan.save_failed", "The reverted project could not be saved.");
            }
            plan.Reverted = true;
            plan.Audit.Add(new(planId, "revert", undone.State.Revision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public ProjectVariablePlanResult PlanProjectVariables(ProjectVariablePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return ProjectVariableFailure(request.BaseRevision, "project_variable_plan.schema_unsupported",
                    "Project variable plan schema must be 1.");
            var command = new SetModuleConfigurationCommand(CommandId.From(Guid.NewGuid()),
                new ProjectRevision(request.BaseRevision), _history.State.Project.Module.Metadata,
                request.Variables, true);
            var preview = _moduleConfigurationHandler.Handle(_history.State, command);
            if (!preview.Succeeded)
                return new(false, null, request.BaseRevision, preview.Patch, preview.Inverse,
                    preview.Diagnostics.Select(Map).ToArray(), false, false, []);
            var planId = Guid.NewGuid().ToString("D");
            var plan = new StoredProjectVariablePlan(planId, command, preview.Patch!, preview.Inverse!,
                [new ProjectVariableAuditEntry(planId, "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _projectVariablePlans.Add(planId, plan);
            return ToResult(plan);
        }
    }

    public ProjectDocumentationResource QueryProjectDocumentation(string? localeOrSearch = null)
    {
        lock (_gate)
        {
            var metadata = _history.State.Project.Module.Metadata;
            var filter = localeOrSearch?.Trim() ?? string.Empty;
            var localizations = metadata.Localizations.Where(item => filter.Length == 0 ||
                item.Locale.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                item.Summary.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                ProjectDocumentationValidator.Pages(item).Any(page =>
                    page.Key.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    page.Value.Contains(filter, StringComparison.OrdinalIgnoreCase))).ToArray();
            if (localizations.Length == 0)
                return new(false, _history.State.Revision.Value, metadata.DefaultLocale, [],
                [
                    new AgentDiagnostic("project_documentation.not_found",
                        "No localized player documentation matches the filter.",
                        "module/metadata/localizations"),
                ]);
            return new(true, _history.State.Revision.Value, metadata.DefaultLocale, localizations, []);
        }
    }

    public ProjectDocumentationPlanResult PlanProjectDocumentation(ProjectDocumentationPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return ProjectDocumentationFailure(request.BaseRevision,
                    "project_documentation_plan.schema_unsupported", "Project documentation plan schema must be 1.");
            var current = _history.State.Project.Module.Metadata;
            ProjectMetadata metadata;
            try
            {
                metadata = new ProjectMetadata(current.Id, request.DefaultLocale, request.Localizations,
                    current.Author, current.Version, current.License, current.ImagePath, current.Tags,
                    current.Classification, "mcp:r052", current.SchemaVersion, current.Foundation);
            }
            catch (ArgumentException exception)
            {
                return ProjectDocumentationFailure(request.BaseRevision,
                    "project_documentation_plan.invalid", exception.Message);
            }
            var command = new SetModuleConfigurationCommand(CommandId.From(Guid.NewGuid()),
                new ProjectRevision(request.BaseRevision), metadata, _history.State.Project.Module.Variables, true);
            var preview = _moduleConfigurationHandler.Handle(_history.State, command);
            if (!preview.Succeeded)
                return new(false, null, request.BaseRevision, preview.Patch, preview.Inverse,
                    preview.Diagnostics.Select(Map).ToArray(), false, false, []);
            var planId = Guid.NewGuid().ToString("D");
            var plan = new StoredProjectDocumentationPlan(planId, command, preview.Patch!, preview.Inverse!,
                [new ProjectDocumentationAuditEntry(planId, "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _projectDocumentationPlans.Add(planId, plan);
            return ToResult(plan);
        }
    }

    public ProjectDocumentationPlanResult ApplyProjectDocumentationPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_projectDocumentationPlans.TryGetValue(planId, out var plan))
                return ProjectDocumentationFailure(GetCurrentRevision(), "project_documentation_plan.missing",
                    $"Project documentation plan '{planId}' does not exist.");
            if (plan.Applied)
                return ProjectDocumentationFailure(plan.Command.BaseRevision.Value,
                    "project_documentation_plan.already_applied",
                    $"Project documentation plan '{planId}' is already applied.");
            var command = plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
            var result = _moduleConfigurationHandler.Handle(_history.State, command);
            if (!result.Succeeded)
            {
                plan.Audit.Add(new(planId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return ToResult(plan) with { Succeeded = false, Diagnostics = result.Diagnostics.Select(Map).ToArray() };
            }
            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = saved.Diagnostics.Select(item =>
                        new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
                };
            _history.Record(result);
            plan.Applied = true;
            plan.AppliedRevision = result.NewRevision!.Value.Value;
            plan.Audit.Add(new(planId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public ProjectDocumentationPlanResult RevertProjectDocumentationPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_projectDocumentationPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return ProjectDocumentationFailure(GetCurrentRevision(), "project_documentation_plan.not_applied",
                    $"Project documentation plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _history.State.Revision.Value)
                return ProjectDocumentationFailure(plan.Command.BaseRevision.Value,
                    "project_documentation_plan.revision_conflict",
                    "Only the most recently applied plan can be reverted without overwriting later work.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return ProjectDocumentationFailure(plan.Command.BaseRevision.Value,
                    "project_documentation_plan.inverse_unavailable", "The inverse operation is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return ProjectDocumentationFailure(plan.Command.BaseRevision.Value,
                    "project_documentation_plan.save_failed", "The reverted project could not be saved.");
            }
            plan.Reverted = true;
            plan.Audit.Add(new(planId, "revert", undone.State.Revision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public ProjectVariablePlanResult ApplyProjectVariablePlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_projectVariablePlans.TryGetValue(planId, out var plan))
                return ProjectVariableFailure(GetCurrentRevision(), "project_variable_plan.missing",
                    $"Project variable plan '{planId}' does not exist.");
            if (plan.Applied)
                return ProjectVariableFailure(plan.Command.BaseRevision.Value, "project_variable_plan.already_applied",
                    $"Project variable plan '{planId}' is already applied.");
            var command = plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
            var result = _moduleConfigurationHandler.Handle(_history.State, command);
            if (!result.Succeeded)
            {
                plan.Audit.Add(new(planId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return ToResult(plan) with { Succeeded = false, Diagnostics = result.Diagnostics.Select(Map).ToArray() };
            }
            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = saved.Diagnostics.Select(item =>
                        new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
                };
            _history.Record(result);
            plan.Applied = true;
            plan.AppliedRevision = result.NewRevision!.Value.Value;
            plan.Audit.Add(new(planId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public ProjectVariablePlanResult RevertProjectVariablePlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_projectVariablePlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return ProjectVariableFailure(GetCurrentRevision(), "project_variable_plan.not_applied",
                    $"Project variable plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _history.State.Revision.Value)
                return ProjectVariableFailure(plan.Command.BaseRevision.Value,
                    "project_variable_plan.revision_conflict",
                    "Only the most recently applied plan can be reverted without overwriting later work.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return ProjectVariableFailure(plan.Command.BaseRevision.Value,
                    "project_variable_plan.inverse_unavailable", "The inverse operation is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return ProjectVariableFailure(plan.Command.BaseRevision.Value, "project_variable_plan.save_failed",
                    "The reverted project could not be saved.");
            }
            plan.Reverted = true;
            plan.Audit.Add(new(planId, "revert", undone.State.Revision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public RulesetPluginResource QueryRulesetPlugins(string? query = null)
    {
        lock (_gate)
        {
            var catalog = _history.State.Project.Module.RuntimePolicies.Rules.Plugins;
            var normalized = query?.Trim() ?? string.Empty;
            var items = catalog.Packages.Where(item => normalized.Length == 0 ||
                    item.Id.ToString("D").Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                    item.Key.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                    item.ContentPackageId.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                    item.Attributes.Concat(item.Effects).Concat(item.Abilities).Concat(item.Inventories)
                        .Concat(item.ProgressionTracks).Concat(item.UiPanels)
                        .Any(value => value.Contains(normalized, StringComparison.OrdinalIgnoreCase)))
                .Select(item => new RulesetPluginItem(item.Id, item.SchemaVersion, item.Key, item.Version,
                    item.ContentPackageId, item.Attributes.Count, item.Effects.Count, item.Abilities.Count,
                    item.Inventories.Count, item.ProgressionTracks.Count, item.UiPanels.Count, item.Provenance))
                .ToArray();
            return new(1, _history.State.Revision.Value, normalized, catalog.ActivePackageKey, items);
        }
    }

    private static ProjectVariablePlanResult ProjectVariableFailure(long revision, string code, string message) =>
        new(false, null, revision, null, null, [new AgentDiagnostic(code, message)], false, false, []);

    private static ProjectVariablePlanResult ToResult(StoredProjectVariablePlan plan) => new(true, plan.PlanId,
        plan.Command.BaseRevision.Value, plan.Diff, plan.Inverse, [], plan.Applied, plan.Reverted,
        plan.Audit.ToArray());

    private static ProjectDocumentationPlanResult ProjectDocumentationFailure(
        long revision, string code, string message) =>
        new(false, null, revision, null, null, [new AgentDiagnostic(code, message)], false, false, []);

    private static ProjectDocumentationPlanResult ToResult(StoredProjectDocumentationPlan plan) =>
        new(true, plan.PlanId, plan.Command.BaseRevision.Value, plan.Diff, plan.Inverse, [], plan.Applied,
            plan.Reverted, plan.Audit.ToArray());

    private static ProjectFoundationPlanResult ProjectFoundationFailure(long revision, string code, string message) =>
        new(false, null, revision, null, null, [new AgentDiagnostic(code, message)], false, false, []);

    private static ProjectFoundationPlanResult ToResult(StoredProjectFoundationPlan plan) => new(true, plan.PlanId,
        plan.Command.BaseRevision.Value, plan.Diff, plan.Inverse, [], plan.Applied, plan.Reverted,
        plan.Audit.ToArray());

    private static ProjectFoundationPlanResult ToResult(StoredProjectMetadataPatchPlan plan) =>
        new ProjectFoundationPlanResult(true, plan.PlanId, plan.Command.BaseRevision.Value, plan.Diff,
            plan.Inverse, [], plan.Applied, plan.Reverted, plan.Audit.ToArray())
        {
            ChangedPaths = plan.ChangedPaths,
            LocalizationReview = plan.LocalizationReview,
        };

    private static AreaFoundationPlanResult AreaFoundationFailure(long revision, string code, string message) =>
        new(false, null, revision, null, null, [new AgentDiagnostic(code, message)], false, false, []);

    private static AreaFoundationPlanResult ToResult(StoredAreaFoundationPlan plan) => new(true, plan.PlanId,
        plan.Command.BaseRevision.Value, plan.Diff, plan.Inverse, [], plan.Applied, plan.Reverted,
        plan.Audit.ToArray());

    private static AreaBiomePlanResult AreaBiomeFailure(long revision, string code, string message) =>
        new(false, null, revision, null, null, [new AgentDiagnostic(code, message)], false, false, []);

    private static AreaBiomePlanResult ToResult(StoredAreaBiomePlan plan) => new(true, plan.PlanId,
        plan.Command.BaseRevision.Value, plan.Diff, plan.Inverse, [], plan.Applied, plan.Reverted,
        plan.Audit.ToArray());

    private static AreaEnvironmentPlanResult AreaEnvironmentFailure(long revision, string code, string message) =>
        new(false, null, revision, null, null, [new AgentDiagnostic(code, message)], false, false, []);

    private static AreaEnvironmentPlanResult ToResult(StoredAreaEnvironmentPlan plan) => new(true, plan.PlanId,
        plan.Command.BaseRevision.Value, plan.Diff, plan.Inverse, [], plan.Applied, plan.Reverted,
        plan.Audit.ToArray());

    private static AreaEnvironmentPlanResult ToResult(StoredAreaEnvironmentPatchPlan plan) =>
        new AreaEnvironmentPlanResult(true, plan.PlanId, plan.Command.BaseRevision.Value, plan.Diff, plan.Inverse,
            [], plan.Applied, plan.Reverted, plan.Audit.ToArray())
        {
            ChangedPaths = plan.ChangedPaths,
        };

    private static AreaExperienceAuthoringPlanResult AreaExperienceAuthoringFailure(
        long revision, string code, string message) =>
        new(false, null, revision, null, null, [new AgentDiagnostic(code, message)], false, false, []);

    private static AreaExperienceAuthoringPlanResult ToResult(StoredAreaExperienceAuthoringPlan plan) =>
        new(true, plan.PlanId, plan.Command.BaseRevision.Value, plan.Diff, plan.Inverse, [], plan.Applied,
            plan.Reverted, plan.Audit.ToArray());

    private static AreaPlaytestPlanResult AreaPlaytestFailure(long revision, string code, string message) =>
        new(false, null, revision, null, null, [new AgentDiagnostic(code, message)], false, false, []);

    private static AreaPlaytestPlanResult ToResult(StoredAreaPlaytestPlan plan) => new(true, plan.PlanId,
        plan.Command.BaseRevision.Value, plan.Diff, plan.Inverse, [], plan.Applied, plan.Reverted,
        plan.Audit.ToArray());

    private static EntityScalePlanResult EntityScaleFailure(long revision, string code, string message) =>
        new(false, null, revision, null, null, [new AgentDiagnostic(code, message)], false, false, []);

    private static EntityScalePlanResult ToResult(StoredEntityScalePlan plan) => new(true, plan.PlanId,
        plan.Command.BaseRevision.Value, plan.Diff, plan.Inverse, [], plan.Applied, plan.Reverted,
        plan.Audit.ToArray());

    private static EntityTranslationPlanResult EntityTranslationFailure(
        long revision,
        string code,
        string message) =>
        new(false, null, revision, null, null, [new AgentDiagnostic(code, message)], false, false, []);

    private static EntityTranslationPlanResult ToResult(StoredEntityTranslationPlan plan) =>
        new(true, plan.PlanId, plan.Command.BaseRevision.Value, plan.Diff, plan.Inverse, [], plan.Applied,
            plan.Reverted, plan.Audit.ToArray());

    public RulesProfileResource GetRules(string? areaId = null)
    {
        lock (_gate)
        {
            var project = _history.State.Project;
            var area = string.IsNullOrWhiteSpace(areaId)
                ? project.Areas.Single(item => item.Id == project.Module.EntryAreaId)
                : project.Areas.Single(item => item.Id == AreaId.Parse(areaId));
            return new RulesProfileResource(1, _history.State.Revision.Value, area.Id.ToString(),
                project.Module.RuntimePolicies.Rules, area.RuntimePolicies.Rules);
        }
    }

    public RulesProfilePlanResult PlanRules(RulesProfilePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return RulesProfileFailure(request.BaseRevision, "rules_plan.schema_unsupported",
                    "Rules plan schema must be 1.");
            AreaId areaId;
            try { areaId = AreaId.Parse(request.AreaId); }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                return RulesProfileFailure(request.BaseRevision, "rules_plan.area_invalid", exception.Message);
            }
            var command = new SetRulesProfilesCommand(CommandId.From(Guid.NewGuid()),
                new ProjectRevision(request.BaseRevision), request.Module, areaId, request.Area, true);
            var preview = _rulesProfilesHandler.Handle(_history.State, command);
            if (!preview.Succeeded)
                return new RulesProfilePlanResult(false, null, request.BaseRevision, preview.Patch,
                    preview.Inverse, preview.Diagnostics.Select(Map).ToArray(), false, false, []);
            var planId = Guid.NewGuid().ToString("D");
            var plan = new StoredRulesProfilePlan(planId, command, preview.Patch!, preview.Inverse!,
                [new RulesProfileAuditEntry(planId, "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _rulesProfilePlans.Add(planId, plan);
            return ToResult(plan);
        }
    }

    public RulesProfilePlanResult ApplyRulesPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_rulesProfilePlans.TryGetValue(planId, out var plan))
                return RulesProfileFailure(GetCurrentRevision(), "rules_plan.missing",
                    $"Rules plan '{planId}' does not exist.");
            if (plan.Applied)
                return RulesProfileFailure(plan.Command.BaseRevision.Value, "rules_plan.already_applied",
                    $"Rules plan '{planId}' is already applied.");
            var command = plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
            var result = _rulesProfilesHandler.Handle(_history.State, command);
            if (!result.Succeeded)
            {
                plan.Audit.Add(new RulesProfileAuditEntry(planId, "apply", _history.State.Revision.Value,
                    _clock.UtcNow, false));
                return ToResult(plan) with { Succeeded = false, Diagnostics = result.Diagnostics.Select(Map).ToArray() };
            }
            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = saved.Diagnostics.Select(item =>
                        new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
                };
            _history.Record(result);
            plan.Applied = true;
            plan.AppliedRevision = result.NewRevision!.Value.Value;
            plan.Audit.Add(new RulesProfileAuditEntry(planId, "apply", plan.AppliedRevision.Value,
                _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public RulesProfilePlanResult RevertRulesPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_rulesProfilePlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return RulesProfileFailure(GetCurrentRevision(), "rules_plan.not_applied",
                    $"Rules plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _history.State.Revision.Value)
                return RulesProfileFailure(plan.Command.BaseRevision.Value, "rules_plan.revision_conflict",
                    "Only the most recently applied plan can be reverted without overwriting later work.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return RulesProfileFailure(plan.Command.BaseRevision.Value, "rules_plan.inverse_unavailable",
                    "The inverse operation is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return RulesProfileFailure(plan.Command.BaseRevision.Value, "rules_plan.save_failed",
                    "The reverted project could not be saved.");
            }
            plan.Reverted = true;
            plan.Audit.Add(new RulesProfileAuditEntry(planId, "revert", undone.State.Revision.Value,
                _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public AreaExperienceResource GetAreaExperience(string? areaId = null)
    {
        lock (_gate)
        {
            var project = _history.State.Project;
            var area = string.IsNullOrWhiteSpace(areaId)
                ? project.Areas.Single(item => item.Id == project.Module.EntryAreaId)
                : project.Areas.Single(item => item.Id == AreaId.Parse(areaId));
            return new AreaExperienceResource(1, _history.State.Revision.Value, area.Id.ToString(),
                area.RuntimePolicies.Experience);
        }
    }

    public AreaExperiencePlanResult PlanAreaExperience(AreaExperiencePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return AreaExperienceFailure(request.BaseRevision, "area_experience_plan.schema_unsupported",
                    "Area experience plan schema must be 1.");
            AreaId areaId;
            try { areaId = AreaId.Parse(request.AreaId); }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                return AreaExperienceFailure(request.BaseRevision, "area_experience_plan.area_invalid",
                    exception.Message);
            }
            var command = new SetAreaExperienceProfileCommand(CommandId.From(Guid.NewGuid()),
                new ProjectRevision(request.BaseRevision), areaId, request.Profile, true);
            var preview = _areaExperienceHandler.Handle(_history.State, command);
            if (!preview.Succeeded)
                return new AreaExperiencePlanResult(false, null, request.BaseRevision, preview.Patch,
                    preview.Inverse, preview.Diagnostics.Select(Map).ToArray(), false, false, []);
            var planId = Guid.NewGuid().ToString("D");
            var plan = new StoredAreaExperiencePlan(planId, command, preview.Patch!, preview.Inverse!,
                [new AreaExperienceAuditEntry(planId, "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _areaExperiencePlans.Add(planId, plan);
            return ToResult(plan);
        }
    }

    public AreaExperiencePlanResult ApplyAreaExperiencePlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_areaExperiencePlans.TryGetValue(planId, out var plan))
                return AreaExperienceFailure(GetCurrentRevision(), "area_experience_plan.missing",
                    $"Area experience plan '{planId}' does not exist.");
            if (plan.Applied)
                return AreaExperienceFailure(plan.Command.BaseRevision.Value, "area_experience_plan.already_applied",
                    $"Area experience plan '{planId}' is already applied.");
            var result = _areaExperienceHandler.Handle(_history.State,
                plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false });
            if (!result.Succeeded)
            {
                plan.Audit.Add(new AreaExperienceAuditEntry(planId, "apply", _history.State.Revision.Value,
                    _clock.UtcNow, false));
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = result.Diagnostics.Select(Map).ToArray(),
                };
            }
            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = saved.Diagnostics.Select(item =>
                        new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
                };
            _history.Record(result);
            plan.Applied = true;
            plan.AppliedRevision = result.NewRevision!.Value.Value;
            plan.Audit.Add(new AreaExperienceAuditEntry(planId, "apply", plan.AppliedRevision.Value,
                _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public AreaExperiencePlanResult RevertAreaExperiencePlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_areaExperiencePlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return AreaExperienceFailure(GetCurrentRevision(), "area_experience_plan.not_applied",
                    $"Area experience plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _history.State.Revision.Value)
                return AreaExperienceFailure(plan.Command.BaseRevision.Value,
                    "area_experience_plan.revision_conflict",
                    "Only the most recently applied plan can be reverted without overwriting later work.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return AreaExperienceFailure(plan.Command.BaseRevision.Value,
                    "area_experience_plan.inverse_unavailable", "The inverse operation is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return AreaExperienceFailure(plan.Command.BaseRevision.Value,
                    "area_experience_plan.save_failed", "The reverted project could not be saved.");
            }
            plan.Reverted = true;
            plan.Audit.Add(new AreaExperienceAuditEntry(planId, "revert", undone.State.Revision.Value,
                _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public AudioEmitterResource GetAudioEmitters(string? areaId = null, string? emitterId = null, string? search = null)
    {
        lock (_gate)
        {
            var project = _history.State.Project;
            var area = string.IsNullOrWhiteSpace(areaId)
                ? project.Areas.Single(item => item.Id == project.Module.EntryAreaId)
                : project.Areas.Single(item => item.Id == AreaId.Parse(areaId));
            var source = area.Environment.Audio.Emitters;
            var definitions = source.Definitions.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(emitterId))
            {
                if (!Guid.TryParse(emitterId, out var id))
                    return new AudioEmitterResource(1, _history.State.Revision.Value, area.Id.ToString(),
                        AudioEmitterCatalog.Empty);
                definitions = definitions.Where(item => item.Id == id);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var query = search.Trim();
                definitions = definitions.Where(item =>
                    item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    item.ClipSet.Clips.Any(clip => clip.ResourcePath.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        clip.Transcript.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        clip.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase))));
            }
            var materialized = definitions.ToArray();
            var definitionIds = materialized.Select(item => item.Id).ToHashSet();
            return new AudioEmitterResource(1, _history.State.Revision.Value, area.Id.ToString(),
                new AudioEmitterCatalog(source.SchemaVersion, materialized,
                    source.Instances.Where(item => definitionIds.Contains(item.DefinitionId)).ToArray()));
        }
    }

    public ContentPackageResource GetContentPackages(string? packageId = null, string? search = null)
    {
        lock (_gate)
        {
            var catalog = _history.State.Project.Module.Content.Packages;
            IEnumerable<ContentPackageDefinition> packages = catalog.Packages;
            if (!string.IsNullOrWhiteSpace(packageId))
            {
                packages = Guid.TryParse(packageId, out var id)
                    ? packages.Where(item => item.Id == id)
                    : [];
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var query = search.Trim();
                packages = packages.Where(item =>
                    item.PackageNamespace.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    item.Sources.Any(source =>
                        source.LogicalResourceId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        source.RelativePath.Contains(query, StringComparison.OrdinalIgnoreCase)));
            }
            return new ContentPackageResource(ContentPackageCatalog.CurrentSchemaVersion,
                _history.State.Revision.Value, catalog.Id,
                packages.OrderBy(item => item.PackageNamespace, StringComparer.Ordinal).ToArray());
        }
    }

    public ContentPackagePlanResult PlanContentPackages(ContentPackagePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != ContentPackageCatalog.CurrentSchemaVersion)
                return new(false, request.BaseRevision, null, null,
                    [new AgentDiagnostic("content_package_plan.schema_unsupported",
                        "Content package plan schema must be 1.", "module/content/packages/schemaVersion")], true);
            var packageDiagnostics = ContentPackageCatalogValidator.Validate(
                request.Catalog, _history.State.Project.Module.Id.Value);
            if (packageDiagnostics.Count > 0)
                return new(false, request.BaseRevision, null, null,
                    packageDiagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path,
                        SubjectUuid: ExtractUuid(item.Message), Source: "ContentPackageCatalogValidator",
                        Provenance: request.Catalog.Provenance)).ToArray(), true);
            var current = _history.State.Project.Module.Content;
            var configuration = new ContentWorkspaceConfiguration(current.Id, current.Batch, current.DataTables,
                current.MergePolicy, current.Provenance, current.SchemaVersion, request.Catalog);
            var command = new SetContentWorkspaceCommand(CommandId.From(Guid.NewGuid()),
                new ProjectRevision(request.BaseRevision), configuration, DryRun: true);
            var preview = _contentWorkspaceHandler.Handle(_history.State, command);
            return new ContentPackagePlanResult(preview.Succeeded, request.BaseRevision, preview.Patch,
                preview.Inverse, preview.Diagnostics.Select(Map).ToArray(), true);
        }
    }

    public LocalizationResource GetLocalization(string? key = null, string? search = null)
    {
        lock (_gate)
        {
            var catalog = _history.State.Project.Module.Localization;
            IEnumerable<LocalizationMessage> messages = catalog.Messages;
            if (!string.IsNullOrWhiteSpace(key))
                messages = messages.Where(item => string.Equals(item.Key, key.Trim(), StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(search))
            {
                var query = search.Trim();
                messages = messages.Where(item => item.Key.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    item.Fallback.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    item.Translations.Any(pair => pair.Key.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        pair.Value.Contains(query, StringComparison.OrdinalIgnoreCase)));
            }

            return new LocalizationResource(catalog.SchemaVersion, _history.State.Revision.Value, catalog.Id.Value,
                catalog.DefaultLocale, catalog.PackageNamespace, catalog.LicenseSpdx, catalog.ContentSha256,
                messages.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray());
        }
    }

    public LocalizationPlanResult PlanLocalization(LocalizationPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != LocalizationCatalog.CurrentSchemaVersion)
                return new(false, request.BaseRevision, null, null,
                    [new AgentDiagnostic("localization_plan.schema_unsupported", "Localization plan schema must be 1.",
                        "module/localization/schemaVersion")], true);
            var diagnostics = LocalizationCatalogValidator.Validate(request.Catalog, _history.State.Project.Module.Id.Value);
            if (diagnostics.Count > 0)
                return new(false, request.BaseRevision, null, null,
                    diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path,
                        Source: nameof(LocalizationCatalogValidator), Provenance: request.Catalog.Provenance)).ToArray(), true);
            var command = new SetLocalizationCatalogCommand(CommandId.From(Guid.NewGuid()),
                new ProjectRevision(request.BaseRevision), request.Catalog, DryRun: true);
            var preview = _localizationHandler.Handle(_history.State, command);
            return new LocalizationPlanResult(preview.Succeeded, request.BaseRevision, preview.Patch, preview.Inverse,
                preview.Diagnostics.Select(Map).ToArray(), true);
        }
    }

    public UnifiedContentResourceResult GetContentResources(UnifiedContentResourceQuery? query = null)
    {
        query ??= new UnifiedContentResourceQuery();
        lock (_gate)
        {
            var diagnostics = new List<AgentDiagnostic>();
            AssetCatalog? catalog = null;
            var repositoryRoot = AssetCatalogLoader.FindRepositoryRoot(_session.RootDirectory) ??
                AssetCatalogLoader.FindRepositoryRoot(AppContext.BaseDirectory);
            if (repositoryRoot is not null)
            {
                var load = AssetCatalogLoader.Load(repositoryRoot);
                catalog = load.Catalog;
                diagnostics.AddRange(load.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)));
            }
            else
            {
                diagnostics.Add(new AgentDiagnostic(AssetCatalogLoader.CatalogMissingCode,
                    "Asset Vault catalog is unavailable; project resources remain searchable.", "asset_manifests/catalogs"));
            }

            ContentResourceKind? kind = null;
            if (!string.IsNullOrWhiteSpace(query.Kind))
            {
                if (!Enum.TryParse<ContentResourceKind>(query.Kind, ignoreCase: true, out var parsedKind))
                    return new UnifiedContentResourceResult(1, _history.State.Revision.Value, 0, [], [], [], [], [],
                        [new AgentDiagnostic("content_browser.kind_invalid", $"Unknown content kind '{query.Kind}'.",
                            "query/kind")]);
                kind = parsedKind;
            }
            var browser = new UnifiedContentBrowser(_history.State.Project, catalog);
            IReadOnlyList<UnifiedContentBrowserItem> items;
            try
            {
                items = browser.Search(new UnifiedContentBrowserQuery(query.Text, query.PackageNamespace, kind,
                    query.LicenseSpdx, query.Status, query.MaximumResults));
            }
            catch (ArgumentOutOfRangeException exception)
            {
                return new UnifiedContentResourceResult(1, _history.State.Revision.Value, browser.Items.Count,
                    browser.PackageNamespaces, browser.Kinds.Select(item => item.ToString()).ToArray(), browser.Licenses,
                    browser.Statuses, [], [new AgentDiagnostic("content_browser.limit_invalid", exception.Message,
                        "query/maximumResults")]);
            }

            return new UnifiedContentResourceResult(1, _history.State.Revision.Value, browser.Items.Count,
                browser.PackageNamespaces, browser.Kinds.Select(item => item.ToString()).ToArray(), browser.Licenses,
                browser.Statuses, items, diagnostics);
        }
    }

    public ContentResourceAliasPlanResult PlanContentResourceAliases(ContentResourceAliasPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return ResourceAliasFailure(request.BaseRevision, "content_resource_plan.schema_unsupported",
                    "Content resource plan schema must be 1.", "module/content/resources/schemaVersion");
            var current = _history.State.Project.Module.Content;
            if (!current.Packages.Packages.Any(package => package.Sources.Any(item => item.Id == request.ResourceId) ||
                    package.Assets.Any(item => item.Id == request.ResourceId)))
                return ResourceAliasFailure(request.BaseRevision, "content_resource_plan.resource_missing",
                    $"Content resource '{request.ResourceId:D}' does not exist.",
                    $"module/content/resources/{request.ResourceId:D}");

            var packages = current.Packages.Packages.Select(package =>
            {
                var sources = package.Sources.Select(item => item.Id == request.ResourceId
                    ? item with { Aliases = request.Aliases ?? [], References = request.References ?? [] }
                    : item).ToArray();
                var assets = package.Assets.Select(item => item.Id == request.ResourceId
                    ? item with { Aliases = request.Aliases ?? [], References = request.References ?? [] }
                    : item).ToArray();
                var draft = new ContentPackageDefinition(package.Id, package.SchemaVersion, package.Provenance,
                    package.PackageNamespace, package.Version, package.LicenseSpdx, new string('0', 64), package.Priority,
                    package.SealedResources, package.Dependencies, sources, assets);
                return new ContentPackageDefinition(draft.Id, draft.SchemaVersion, draft.Provenance,
                    draft.PackageNamespace, draft.Version, draft.LicenseSpdx,
                    ContentPackageCompiler.ComputePackageHash(draft), draft.Priority, draft.SealedResources,
                    draft.Dependencies, draft.Sources, draft.Assets);
            }).ToArray();
            var catalog = new ContentPackageCatalog(current.Packages.Id, current.Packages.SchemaVersion,
                current.Packages.Provenance, current.Packages.ConflictPolicy, packages);
            var diagnostics = ContentPackageCatalogValidator.Validate(catalog, _history.State.Project.Module.Id.Value);
            if (diagnostics.Count > 0)
                return new ContentResourceAliasPlanResult(false, request.BaseRevision, null, null,
                    diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path,
                        SubjectUuid: request.ResourceId.ToString("D"), Source: nameof(ContentPackageCatalogValidator),
                        Provenance: catalog.Provenance)).ToArray(), true);
            var configuration = new ContentWorkspaceConfiguration(current.Id, current.Batch, current.DataTables,
                current.MergePolicy, current.Provenance, current.SchemaVersion, catalog);
            var command = new SetContentWorkspaceCommand(CommandId.From(Guid.NewGuid()),
                new ProjectRevision(request.BaseRevision), configuration, DryRun: true);
            var preview = _contentWorkspaceHandler.Handle(_history.State, command);
            return new ContentResourceAliasPlanResult(preview.Succeeded, request.BaseRevision, preview.Patch,
                preview.Inverse, preview.Diagnostics.Select(Map).ToArray(), true);
        }
    }

    public AssetGovernanceResource GetAssetGovernance(AssetGovernanceQuery? query = null)
    {
        query ??= new AssetGovernanceQuery();
        lock (_gate)
        {
            var loaded = AssetGovernanceStore.Load(_session.RootDirectory);
            var diagnostics = loaded.Diagnostics.Select(item =>
                new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray();
            if (!loaded.Succeeded)
                return new(AssetGovernanceRecord.CurrentSchemaVersion, 0, 0, [], diagnostics);
            if (query.MaximumResults is < 1 or > 300)
                return new(AssetGovernanceRecord.CurrentSchemaVersion, 0, loaded.Records.Count, [],
                    [new AgentDiagnostic("asset.governance.limit_invalid", "Maximum results must be between 1 and 300.", "query/maximumResults")]);
            IEnumerable<AssetGovernanceRecord> records = loaded.Records;
            if (!string.IsNullOrWhiteSpace(query.AssetId))
                records = records.Where(item => item.AssetId.ToString().Contains(query.AssetId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(query.Origin))
                records = records.Where(item => item.IpBoundary.Origin.ToString().Equals(query.Origin.Trim(), StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(query.QuarantineStatus))
                records = records.Where(item => item.Quarantine.Status.ToString().Equals(query.QuarantineStatus.Trim(), StringComparison.OrdinalIgnoreCase));
            return new(AssetGovernanceRecord.CurrentSchemaVersion, 0, loaded.Records.Count,
                records.OrderBy(item => item.AssetId.ToString(), StringComparer.Ordinal).Take(query.MaximumResults).ToArray(), diagnostics);
        }
    }

    public AssetGovernancePlanResult PlanAssetGovernance(AssetGovernancePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != AssetGovernanceRecord.CurrentSchemaVersion)
                return new(false, request.BaseRevision, null, null,
                    [new AgentDiagnostic("asset.governance.plan.schema_unsupported", "Asset governance plan schema must be 1.", "assets/governance/schemaVersion")], true);
            var loaded = AssetGovernanceStore.Load(_session.RootDirectory);
            if (!loaded.Succeeded)
                return new(false, request.BaseRevision, null, null,
                    loaded.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(), true);
            var state = new AssetGovernanceState(loaded.Records, new AssetGovernanceRevision(0));
            var command = new SetAssetGovernanceCommand(CommandId.From(Guid.NewGuid()),
                new AssetGovernanceRevision(request.BaseRevision), request.Record, DryRun: true);
            var preview = new SetAssetGovernanceHandler(_clock).Handle(state, command);
            return new(preview.Succeeded, request.BaseRevision, preview.Patch, preview.Inverse,
                preview.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, preview.Patch?.Path ?? "assets/governance")).ToArray(), true);
        }
    }

    public QualityFoundationResource GetQualityFoundation()
    {
        lock (_gate)
        {
            var loaded = QualityFoundationProfileStore.Load(_session.RootDirectory, _history.State.Project.Id.Value);
            return new(QualityFoundationProfile.CurrentSchemaVersion, 0, loaded.Profile,
                loaded.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
        }
    }

    public CanonicalValidationResource ValidateCanonicalProject()
    {
        lock (_gate)
        {
            var loaded = QualityFoundationProfileStore.Load(
                _session.RootDirectory, _history.State.Project.Id.Value);
            if (!loaded.Succeeded)
                return new(1, false, string.Empty, new Dictionary<string, int>(),
                    loaded.Diagnostics.Select(item => new CanonicalValidationDiagnosticResource(
                        Guid.Empty.ToString("D"), "schemas", item.Code, item.Message,
                        item.Path ?? "quality/foundation",
                        _history.State.Project.Id.ToString(), "quality-profile-load")).ToArray());
            var report = WorkspaceCanonicalValidationService.Run(
                _session.RootDirectory, _history.State.Project, loaded.Profile);
            return new(report.SchemaVersion, report.Passed, report.Fingerprint, report.CategoryCounts,
                report.Diagnostics.Select(item => new CanonicalValidationDiagnosticResource(
                    item.Id.ToString("D"), item.Category, item.Code, item.Message, item.Path,
                    item.SubjectUuid?.ToString("D"), item.Provenance)).ToArray());
        }
    }

    public QualityFoundationPlanResult PlanQualityFoundation(QualityFoundationPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != QualityFoundationProfile.CurrentSchemaVersion)
                return new(false, request.BaseRevision, null, null,
                    [new AgentDiagnostic("qa.foundation.plan.schema_unsupported",
                        "QA foundation plan schema must be 1.", "quality/foundation/schemaVersion")], true);
            var loaded = QualityFoundationProfileStore.Load(_session.RootDirectory, _history.State.Project.Id.Value);
            if (!loaded.Succeeded)
                return new(false, request.BaseRevision, null, null,
                    loaded.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(), true);
            var state = new QualityFoundationState(loaded.Profile, new QualityFoundationRevision(0));
            var command = new SetQualityFoundationCommand(CommandId.From(Guid.NewGuid()),
                new QualityFoundationRevision(request.BaseRevision), request.Profile, DryRun: true);
            var preview = new SetQualityFoundationHandler(_clock).Handle(state, command);
            return new(preview.Succeeded, request.BaseRevision, preview.Patch, preview.Inverse,
                preview.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message,
                    preview.Patch?.Path ?? "quality/foundation")).ToArray(), true);
        }
    }

    private static ContentResourceAliasPlanResult ResourceAliasFailure(
        long baseRevision, string code, string message, string path) =>
        new(false, baseRevision, null, null, [new AgentDiagnostic(code, message, path)], true);

    private static string? ExtractUuid(string message)
    {
        const string marker = "[UUID ";
        var start = message.LastIndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return null;
        start += marker.Length;
        var end = message.IndexOf(']', start);
        return end > start ? message[start..end] : null;
    }

    public AudioEmitterPlanResult PlanAudioEmitters(AudioEmitterPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return AudioEmitterFailure(request.BaseRevision, "audio_emitter_plan.schema_unsupported",
                    "Audio emitter plan schema must be 1.");
            AreaId areaId;
            try { areaId = AreaId.Parse(request.AreaId); }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                return AudioEmitterFailure(request.BaseRevision, "audio_emitter_plan.area_invalid", exception.Message);
            }
            var area = _history.State.Project.Areas.SingleOrDefault(item => item.Id == areaId);
            if (area is null)
                return AudioEmitterFailure(request.BaseRevision, "audio_emitter_plan.area_missing", "Area does not exist.");
            var command = new SetAreaEnvironmentCommand(CommandId.From(Guid.NewGuid()),
                new ProjectRevision(request.BaseRevision), areaId,
                WithAudioEmitters(area.Environment, request.Catalog), DryRun: true);
            var preview = _areaEnvironmentHandler.Handle(_history.State, command);
            if (!preview.Succeeded)
                return new AudioEmitterPlanResult(false, null, request.BaseRevision, preview.Patch,
                    preview.Inverse, preview.Diagnostics.Select(Map).ToArray(), false, false, []);
            var planId = Guid.NewGuid().ToString("D");
            var plan = new StoredAudioEmitterPlan(planId, command, preview.Patch!, preview.Inverse!,
                [new AudioEmitterAuditEntry(planId, "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _audioEmitterPlans.Add(planId, plan);
            return ToResult(plan);
        }
    }

    public AudioEmitterPlanResult ApplyAudioEmitterPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_audioEmitterPlans.TryGetValue(planId, out var plan))
                return AudioEmitterFailure(GetCurrentRevision(), "audio_emitter_plan.missing",
                    $"Audio emitter plan '{planId}' does not exist.");
            if (plan.Applied)
                return AudioEmitterFailure(plan.Command.BaseRevision.Value, "audio_emitter_plan.already_applied",
                    $"Audio emitter plan '{planId}' is already applied.");
            var result = _areaEnvironmentHandler.Handle(_history.State,
                plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false });
            if (!result.Succeeded)
            {
                plan.Audit.Add(new AudioEmitterAuditEntry(planId, "apply", _history.State.Revision.Value,
                    _clock.UtcNow, false));
                return ToResult(plan) with { Succeeded = false, Diagnostics = result.Diagnostics.Select(Map).ToArray() };
            }
            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = saved.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
                };
            _history.Record(result);
            plan.Applied = true;
            plan.AppliedRevision = result.NewRevision!.Value.Value;
            plan.AppliedCommandId = result.Audit.CommandId.Value;
            plan.Audit.Add(new AudioEmitterAuditEntry(planId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public AudioEmitterPlanResult RevertAudioEmitterPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_audioEmitterPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return AudioEmitterFailure(GetCurrentRevision(), "audio_emitter_plan.not_applied",
                    $"Audio emitter plan '{planId}' is not currently applied.");
            if (_history.Journal.LastOrDefault(entry => entry.IsApplied)?.Id != plan.AppliedCommandId)
                return AudioEmitterFailure(plan.Command.BaseRevision.Value, "audio_emitter_plan.revision_conflict",
                    "Only the most recently applied plan can be reverted without overwriting later work.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return AudioEmitterFailure(plan.Command.BaseRevision.Value, "audio_emitter_plan.inverse_unavailable",
                    "The inverse operation is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return AudioEmitterFailure(plan.Command.BaseRevision.Value, "audio_emitter_plan.save_failed",
                    "The reverted project could not be saved.");
            }
            plan.Reverted = true;
            plan.Audit.Add(new AudioEmitterAuditEntry(planId, "revert", undone.State.Revision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public RuntimeExtensionResource GetRuntimeExtensions()
    {
        lock (_gate)
            return new RuntimeExtensionResource(1, _history.State.Revision.Value,
                _history.State.Project.Module.RuntimePolicies.Extensions);
    }

    public PostLaunchCapabilityResource GetPostLaunchCapabilities(string? section = null)
    {
        lock (_gate)
        {
            var profile = _history.State.Project.Module.RuntimePolicies.Extensions.PostLaunch;
            var normalized = string.IsNullOrWhiteSpace(section) ? "all" : section.Trim().ToLowerInvariant();
            if (normalized is not ("all" or "gameplay" or "director" or "network" or "creator" or "production"))
                throw new ArgumentOutOfRangeException(nameof(section), "Unknown post-launch capability section.");
            var summary = normalized switch
            {
                "gameplay" => $"zero-g={profile.Gameplay.ZeroGravity.Enabled};vehicles={profile.Gameplay.Vehicles.Enabled};mounts={profile.Gameplay.Mounts.Enabled};achievements={profile.Gameplay.Achievements.Count}",
                "director" => $"groups={profile.LiveDirector.SavedGroups.Count};recording={profile.LiveDirector.SessionRecording.Enabled};rest={profile.LiveDirector.ForcedRest.Enabled};levels={profile.LiveDirector.LevelAdjustment.Enabled}",
                "network" => $"voice={profile.Network.Voice.Enabled};shards={profile.Network.Shards.Count};reload={profile.Network.HotReload.Enabled};extensions={profile.Network.ServerExtensions.Enabled};protocol={profile.Network.Protocol.ProtocolFamily}",
                "creator" => $"dynamic-areas={profile.CreatorExperience.DynamicAreas.Enabled};party={profile.CreatorExperience.PartyControl.Enabled};tracks={profile.CreatorExperience.ProgressionTracks.MaximumTracks};graphics={profile.CreatorExperience.Graphics.AntiAliasing}",
                "production" => $"media={profile.Production.MediaGeneration.Enabled};soak={profile.Production.SoakTests.Enabled};duration={profile.Production.SoakTests.DurationMinutes}",
                _ => $"gameplay=5;director=5;network=5;creator=9;production=2;schema={profile.SchemaVersion}",
            };
            return new PostLaunchCapabilityResource(1, _history.State.Revision.Value, normalized, summary, profile);
        }
    }

    public PostLaunchPreviewResult PreviewPostLaunchCapability(PostLaunchPreviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return PostLaunchFailure("post_launch.schema_unsupported", "Expected post-launch schema v1.");
            var profile = _history.State.Project.Module.RuntimePolicies.Extensions.PostLaunch;
            try
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(request.Kind);
                return request.Kind.Trim().ToLowerInvariant() switch
                {
                    "mobility" => PreviewMobility(profile, request.Arguments),
                    "protocol" => PreviewProtocol(profile, request.Arguments),
                    "media" => PreviewProduction(profile, request.Arguments, "image"),
                    "soak" => PreviewProduction(profile, request.Arguments, "soak"),
                    _ => PostLaunchFailure("post_launch.kind_unknown", $"Unknown preview kind '{request.Kind}'."),
                };
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
            {
                return PostLaunchFailure("post_launch.arguments_invalid", exception.Message);
            }
        }
    }

    public RuntimeExtensionPlanResult PlanRuntimeExtensions(RuntimeExtensionPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return RuntimeExtensionFailure(request.BaseRevision, "runtime_extension_plan.schema_unsupported",
                    "Runtime extension plan schema must be 1.");
            var command = new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()),
                new ProjectRevision(request.BaseRevision), request.Profile, true);
            var preview = _runtimeExtensionsHandler.Handle(_history.State, command);
            if (!preview.Succeeded)
                return new RuntimeExtensionPlanResult(false, null, request.BaseRevision, preview.Patch,
                    preview.Inverse, preview.Diagnostics.Select(Map).ToArray(), false, false, []);
            var planId = Guid.NewGuid().ToString("D");
            var plan = new StoredRuntimeExtensionPlan(planId, command, preview.Patch!, preview.Inverse!,
                [new RuntimeExtensionAuditEntry(planId, "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _runtimeExtensionPlans.Add(planId, plan);
            return ToResult(plan);
        }
    }

    public RuntimeExtensionPlanResult ApplyRuntimeExtensionPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_runtimeExtensionPlans.TryGetValue(planId, out var plan))
                return RuntimeExtensionFailure(GetCurrentRevision(), "runtime_extension_plan.missing",
                    $"Runtime extension plan '{planId}' does not exist.");
            if (plan.Applied)
                return RuntimeExtensionFailure(plan.Command.BaseRevision.Value,
                    "runtime_extension_plan.already_applied", $"Runtime extension plan '{planId}' is already applied.");
            var result = _runtimeExtensionsHandler.Handle(_history.State,
                plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false });
            if (!result.Succeeded)
            {
                plan.Audit.Add(new RuntimeExtensionAuditEntry(planId, "apply", _history.State.Revision.Value,
                    _clock.UtcNow, false));
                return ToResult(plan) with { Succeeded = false, Diagnostics = result.Diagnostics.Select(Map).ToArray() };
            }
            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = saved.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message,
                        item.Path)).ToArray(),
                };
            _history.Record(result);
            plan.Applied = true;
            plan.AppliedRevision = result.NewRevision!.Value.Value;
            plan.Audit.Add(new RuntimeExtensionAuditEntry(planId, "apply", plan.AppliedRevision.Value,
                _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public RuntimeExtensionPlanResult RevertRuntimeExtensionPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_runtimeExtensionPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return RuntimeExtensionFailure(GetCurrentRevision(), "runtime_extension_plan.not_applied",
                    $"Runtime extension plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _history.State.Revision.Value)
                return RuntimeExtensionFailure(plan.Command.BaseRevision.Value,
                    "runtime_extension_plan.revision_conflict",
                    "Only the most recently applied plan can be reverted without overwriting later work.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return RuntimeExtensionFailure(plan.Command.BaseRevision.Value,
                    "runtime_extension_plan.inverse_unavailable", "The inverse operation is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return RuntimeExtensionFailure(plan.Command.BaseRevision.Value,
                    "runtime_extension_plan.save_failed", "The reverted project could not be saved.");
            }
            plan.Reverted = true;
            plan.Audit.Add(new RuntimeExtensionAuditEntry(planId, "revert", undone.State.Revision.Value,
                _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public BlueprintPaletteResource QueryBlueprintPalette(string? query = null, int limit = 100)
    {
        if (limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(limit));
        lock (_gate)
        {
            var normalized = query?.Trim() ?? string.Empty;
            var project = _history.State.Project;
            var items = project.Blueprints
                .Where(item => string.IsNullOrEmpty(normalized) ||
                    item.Id.ToString().Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                    item.Slug.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                    item.DisplayName.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                    item.Authoring.Tags.Any(tag => tag.Contains(normalized, StringComparison.OrdinalIgnoreCase)) ||
                    item.Authoring.CategoryPaths.Any(path => path.Contains(normalized, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(item => item.Slug, StringComparer.Ordinal)
                .Take(limit)
                .Select(item => new BlueprintPaletteResourceItem(
                    item.Id.ToString(), item.Slug, item.DisplayName, item.Authoring.Version,
                    item.Authoring.ManifestPath, item.Provenance.LicenseSpdx, item.Provenance.ContentHash,
                    item.Provenance.Source, item.Authoring.CategoryPaths, item.Authoring.Tags,
                    item.Authoring.PromotedDefaults.Select(value => value.Key).ToArray(),
                    project.Areas.SelectMany(area => area.Entities.Where(entity => entity.BlueprintId == item.Id)
                        .Select(entity => new BlueprintInstanceMigrationImpact(entity.Id,
                            $"areas/{area.Id}/entities/{entity.Id}", entity.BlueprintVersionRequirement,
                            "current"))).ToArray()))
                .ToArray();
            return new BlueprintPaletteResource(1, _history.State.Revision.Value, normalized, items);
        }
    }

    public BlueprintUpdatePlanResult PlanBlueprintUpdate(BlueprintUpdatePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return BlueprintUpdateFailure(request.BaseRevision, "blueprint_update_plan.schema_unsupported",
                    "Blueprint update plan schema must be 1.");
            try
            {
                var blueprintId = BlueprintId.Parse(request.BlueprintId);
                var sourceId = EntityId.Parse(request.SourceEntityId);
                var excluded = request.ExcludedInstanceIds.Select(EntityId.Parse).ToArray();
                if (!Enum.TryParse<BlueprintVersionBump>(request.VersionBump, true, out var bump) ||
                    !Enum.IsDefined(bump))
                    return BlueprintUpdateFailure(request.BaseRevision, "blueprint_update_plan.bump_invalid",
                        $"Unknown semantic version bump '{request.VersionBump}'.");
                var command = new UpdateBlueprintFromEntityCommand(CommandId.From(Guid.NewGuid()),
                    new ProjectRevision(request.BaseRevision), blueprintId, sourceId, request.PromotedOverrideKeys,
                    bump, excluded, true, request.CategoryPaths);
                var plan = BlueprintUpdatePlanner.Build(_history.State.Project, blueprintId, sourceId,
                    request.PromotedOverrideKeys, bump, excluded);
                var preview = _blueprintUpdateHandler.Handle(_history.State, command);
                if (!preview.Succeeded)
                    return new(false, null, request.BaseRevision, plan.PreviousVersion, plan.TargetVersion,
                        plan.Instances, preview.Patch, preview.Inverse, preview.Diagnostics.Select(Map).ToArray(),
                        false, false, []);
                var planId = Guid.NewGuid().ToString("D");
                var stored = new StoredBlueprintUpdatePlan(planId, command, plan, preview.Patch!, preview.Inverse!,
                    [new BlueprintUpdateAuditEntry(planId, "planned", request.BaseRevision, _clock.UtcNow, true)]);
                _blueprintUpdatePlans.Add(planId, stored);
                return ToResult(stored);
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException or BlueprintUpdatePlanException)
            {
                var code = exception is BlueprintUpdatePlanException planException
                    ? planException.Code
                    : "blueprint_update_plan.request_invalid";
                return BlueprintUpdateFailure(request.BaseRevision, code, exception.Message);
            }
        }
    }

    public BlueprintUpdatePlanResult ApplyBlueprintUpdatePlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_blueprintUpdatePlans.TryGetValue(planId, out var plan))
                return BlueprintUpdateFailure(_history.State.Revision.Value, "blueprint_update_plan.missing",
                    $"Blueprint update plan '{planId}' does not exist.");
            if (plan.Applied)
                return BlueprintUpdateFailure(plan.Command.BaseRevision.Value, "blueprint_update_plan.already_applied",
                    $"Blueprint update plan '{planId}' is already applied.");
            var command = plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
            var result = _blueprintUpdateHandler.Handle(_history.State, command);
            if (!result.Succeeded)
            {
                plan.Audit.Add(new(planId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return ToResult(plan) with { Succeeded = false, Diagnostics = result.Diagnostics.Select(Map).ToArray() };
            }
            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = saved.Diagnostics.Select(item =>
                        new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
                };
            _history.Record(result);
            plan.Applied = true;
            plan.AppliedRevision = result.NewRevision!.Value.Value;
            plan.Audit.Add(new(planId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public BlueprintUpdatePlanResult RevertBlueprintUpdatePlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_blueprintUpdatePlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return BlueprintUpdateFailure(_history.State.Revision.Value, "blueprint_update_plan.not_applied",
                    $"Blueprint update plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _history.State.Revision.Value)
                return BlueprintUpdateFailure(plan.Command.BaseRevision.Value, "blueprint_update_plan.revision_conflict",
                    "Only the most recently applied blueprint plan can be reverted.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return BlueprintUpdateFailure(plan.Command.BaseRevision.Value, "blueprint_update_plan.inverse_unavailable",
                    "The inverse blueprint operation is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return BlueprintUpdateFailure(plan.Command.BaseRevision.Value, "blueprint_update_plan.save_failed",
                    "The reverted blueprint project could not be saved.");
            }
            plan.Reverted = true;
            plan.Audit.Add(new(planId, "revert", undone.State.Revision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    private static BlueprintUpdatePlanResult ToResult(StoredBlueprintUpdatePlan plan) => new(
        true, plan.PlanId, plan.Command.BaseRevision.Value, plan.Plan.PreviousVersion, plan.Plan.TargetVersion,
        plan.Plan.Instances, plan.Diff, plan.Inverse, [], plan.Applied, plan.Reverted, plan.Audit.ToArray());

    private static BlueprintUpdatePlanResult BlueprintUpdateFailure(long revision, string code, string message) =>
        new(false, null, revision, null, null, [], null, null,
            [new AgentDiagnostic(code, message, "blueprintUpdatePlan")], false, false, []);

    public BlueprintPrefabResource QueryBlueprintPrefabs(string? query = null, int limit = 100)
    {
        if (limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(limit));
        lock (_gate)
        {
            var normalized = query?.Trim() ?? string.Empty;
            var items = _history.State.Project.Blueprints
                .Where(item => item.Authoring.PrefabVariant is not null ||
                               item.Authoring.PrefabComposition is not null || item.Authoring.AssetBudget is not null)
                .Where(item => string.IsNullOrEmpty(normalized) ||
                    item.Id.ToString().Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                    item.Slug.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                    item.Authoring.AuthorCollections.Any(value =>
                        value.Contains(normalized, StringComparison.OrdinalIgnoreCase)) ||
                    item.Authoring.PrefabVariant?.BaseBlueprintId.ToString()
                        .Contains(normalized, StringComparison.OrdinalIgnoreCase) == true)
                .OrderBy(item => item.Slug, StringComparer.Ordinal).Take(limit).Select(MapBlueprintPrefab).ToArray();
            return new BlueprintPrefabResource(1, _history.State.Revision.Value, normalized, items);
        }
    }

    public BlueprintPrefabPlanResult PlanBlueprintPrefab(BlueprintPrefabPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return BlueprintPrefabFailure(request.BaseRevision, "blueprint_prefab_plan.schema_unsupported",
                    "Blueprint prefab plan schema must be 1.");
            try
            {
                var command = BuildBlueprintPrefabCommand(request);
                var preview = _blueprintPrefabHandler.Handle(_history.State, command);
                if (!preview.Succeeded)
                    return new(false, null, request.BaseRevision, null, preview.Patch, preview.Inverse,
                        preview.Diagnostics.Select(Map).ToArray(), false, false, []);
                var materialized = _blueprintPrefabHandler.Handle(_history.State,
                    command with { DryRun = false });
                if (!materialized.Succeeded)
                    return new(false, null, request.BaseRevision, null, materialized.Patch, materialized.Inverse,
                        materialized.Diagnostics.Select(Map).ToArray(), false, false, []);
                var configured = materialized.State.Project.Blueprints.Single(item => item.Id == command.BlueprintId);
                var planId = Guid.NewGuid().ToString("D");
                var stored = new StoredBlueprintPrefabPlan(planId, command, MapBlueprintPrefab(configured),
                    preview.Patch!, preview.Inverse!,
                    [new BlueprintPrefabAuditEntry(planId, "planned", request.BaseRevision, _clock.UtcNow, true)]);
                _blueprintPrefabPlans.Add(planId, stored);
                return ToResult(stored);
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
            {
                return BlueprintPrefabFailure(request.BaseRevision, "blueprint_prefab_plan.request_invalid",
                    exception.Message);
            }
        }
    }

    public BlueprintPrefabPlanResult ApplyBlueprintPrefabPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_blueprintPrefabPlans.TryGetValue(planId, out var plan))
                return BlueprintPrefabFailure(_history.State.Revision.Value, "blueprint_prefab_plan.missing",
                    $"Blueprint prefab plan '{planId}' does not exist.");
            if (plan.Applied)
                return BlueprintPrefabFailure(plan.Command.BaseRevision.Value, "blueprint_prefab_plan.already_applied",
                    $"Blueprint prefab plan '{planId}' is already applied.");
            var command = plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
            var result = _blueprintPrefabHandler.Handle(_history.State, command);
            if (!result.Succeeded)
            {
                plan.Audit.Add(new(planId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return ToResult(plan) with { Succeeded = false, Diagnostics = result.Diagnostics.Select(Map).ToArray() };
            }
            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = saved.Diagnostics.Select(item =>
                        new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
                };
            _history.Record(result);
            plan.Applied = true;
            plan.AppliedRevision = result.NewRevision!.Value.Value;
            plan.Audit.Add(new(planId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public BlueprintPrefabPlanResult RevertBlueprintPrefabPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_blueprintPrefabPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return BlueprintPrefabFailure(_history.State.Revision.Value, "blueprint_prefab_plan.not_applied",
                    $"Blueprint prefab plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _history.State.Revision.Value)
                return BlueprintPrefabFailure(plan.Command.BaseRevision.Value,
                    "blueprint_prefab_plan.revision_conflict",
                    "Only the most recently applied blueprint prefab plan can be reverted.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return BlueprintPrefabFailure(plan.Command.BaseRevision.Value,
                    "blueprint_prefab_plan.inverse_unavailable", "The inverse prefab operation is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return BlueprintPrefabFailure(plan.Command.BaseRevision.Value, "blueprint_prefab_plan.save_failed",
                    "The reverted blueprint prefab project could not be saved.");
            }
            plan.Reverted = true;
            plan.Audit.Add(new(planId, "revert", undone.State.Revision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    private ConfigureBlueprintPrefabCommand BuildBlueprintPrefabCommand(BlueprintPrefabPlanRequest request)
    {
        var blueprintId = BlueprintId.Parse(request.BlueprintId);
        PrefabVariantDefinition? variant = null;
        if (!string.IsNullOrWhiteSpace(request.VariantBaseBlueprintId))
            variant = new PrefabVariantDefinition(Guid.NewGuid(), PrefabVariantDefinition.CurrentSchemaVersion,
                "mcp:r058", BlueprintId.Parse(request.VariantBaseBlueprintId),
                request.VariantOverrides.Select(item => new PrefabVariantOverride(
                    Enum.Parse<PrefabVariantOverrideKind>(item.Kind, true), item.Key, item.Value)).ToArray(),
                request.VariantRequired);
        else if (request.VariantOverrides.Count > 0)
            throw new ArgumentException("Variant overrides require VariantBaseBlueprintId.");
        PrefabCompositionDefinition? composition = null;
        if (request.CompositionNodes.Count > 0)
            composition = new PrefabCompositionDefinition(Guid.NewGuid(),
                PrefabCompositionDefinition.CurrentSchemaVersion, "mcp:r058",
                request.CompositionNodes.Select(item => new PrefabCompositionNode(Guid.Parse(item.NodeId),
                    BlueprintId.Parse(item.BlueprintId), string.IsNullOrWhiteSpace(item.ParentNodeId)
                        ? null : Guid.Parse(item.ParentNodeId), item.Socket,
                    item.Transform ?? new TransformValue(new Vector3Value(item.X, item.Y, item.Z),
                        QuaternionValue.Identity, Vector3Value.One), item.Tags, item.Overrides)).ToArray(),
                request.AssemblyRules, request.Parameters,
                request.AssemblyScripts, request.CompositionRequired);
        BlueprintAssetBudgetProfile? budget = null;
        if (request.AssetMetrics is not null || request.PlatformBudgets.Count > 0)
        {
            if (request.AssetMetrics is null || request.PlatformBudgets.Count == 0)
                throw new ArgumentException("Asset metrics and platform budgets are required together.");
            var metrics = request.AssetMetrics;
            budget = new BlueprintAssetBudgetProfile(Guid.NewGuid(),
                BlueprintAssetBudgetProfile.CurrentSchemaVersion, "mcp:r058",
                new AssetBudgetMetrics(metrics.TriangleCount, metrics.MaterialCount, metrics.TextureCount,
                    metrics.MemoryBytes, metrics.AnimationCount, metrics.LodLevelCount,
                    metrics.CollisionShapeCount), request.PlatformBudgets.Select(item => new AssetPlatformBudget(
                    Enum.Parse<AssetTargetPlatform>(item.Platform, true), item.MaximumTriangles,
                    item.MaximumMaterials, item.MaximumTextures, item.MaximumMemoryBytes, item.MaximumAnimations,
                    item.MaximumLodLevels, item.MaximumCollisionShapes, item.GateBuild)).ToArray(),
                request.AssetBudgetRequired);
        }
        return new ConfigureBlueprintPrefabCommand(CommandId.From(Guid.NewGuid()),
            new ProjectRevision(request.BaseRevision), blueprintId, variant, composition, budget,
            request.AuthorCollections, true);
    }

    private static BlueprintPrefabResourceItem MapBlueprintPrefab(BlueprintDefinition item)
    {
        var variant = item.Authoring.PrefabVariant;
        var composition = item.Authoring.PrefabComposition;
        var budget = item.Authoring.AssetBudget;
        return new BlueprintPrefabResourceItem(item.Id.ToString(), item.Slug, item.Authoring.AuthorCollections,
            variant?.BaseBlueprintId.ToString(), variant?.Overrides.Select(value =>
                new AgentPrefabVariantOverride(value.Kind.ToString(), value.Key, value.Value)).ToArray() ?? [],
            variant?.RequiredAtRuntime ?? false, composition?.Nodes.Select(value =>
                new AgentPrefabCompositionNode(value.Id.ToString("D"), value.BlueprintId.ToString(),
                    value.ParentNodeId?.ToString("D"), value.Socket, value.Transform.Position.X,
                    value.Transform.Position.Y, value.Transform.Position.Z, value.Transform, value.Tags,
                    value.Overrides)).ToArray() ?? [],
            composition?.AssemblyRules ?? [], composition?.Parameters ?? [], composition?.AssemblyScripts ?? [],
            composition?.RequiredAtRuntime ?? false, budget is null ? null : new AgentAssetBudgetMetrics(
                budget.Metrics.TriangleCount, budget.Metrics.MaterialCount, budget.Metrics.TextureCount,
                budget.Metrics.MemoryBytes, budget.Metrics.AnimationCount, budget.Metrics.LodLevelCount,
                budget.Metrics.CollisionShapeCount), budget?.Platforms.Select(value =>
                new AgentAssetPlatformBudget(value.Platform.ToString(), value.MaximumTriangles,
                    value.MaximumMaterials, value.MaximumTextures, value.MaximumMemoryBytes,
                    value.MaximumAnimations, value.MaximumLodLevels, value.MaximumCollisionShapes,
                    value.GateBuild)).ToArray() ?? [], budget?.RequiredAtRuntime ?? false);
    }

    private static BlueprintPrefabPlanResult ToResult(StoredBlueprintPrefabPlan plan) => new(
        true, plan.PlanId, plan.Command.BaseRevision.Value, plan.Preview, plan.Diff, plan.Inverse, [], plan.Applied,
        plan.Reverted, plan.Audit.ToArray());

    private static BlueprintPrefabPlanResult BlueprintPrefabFailure(long revision, string code, string message) =>
        new(false, null, revision, null, null, null,
            [new AgentDiagnostic(code, message, "blueprintPrefabPlan")], false, false, []);

    public EditorProductivityResource QueryEditorProductivity(string? helpQuery = null)
    {
        lock (_gate)
        {
            var profile = LoadEditorProductivityProfile();
            var catalog = EditorWorkspaceProfileStore.TryLoadCatalog(_session.RootDirectory);
            var profiles = catalog?.Profiles ?? [profile];
            var help = EditorContextualHelpCatalog.Search(helpQuery ?? string.Empty).Select(MapHelpTopic).ToArray();
            var diagnostics = EditorWorkspaceProjectValidator.Validate(profile, _history.State.Project)
                .Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray();
            return new(1, _history.State.Revision.Value, MapEditorProductivity(profile),
                profiles.Select(MapEditorProductivity).ToArray(), help, diagnostics);
        }
    }

    public EditorProductivityPlanResult PlanEditorProductivity(EditorProductivityPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return EditorProductivityFailure(request.BaseRevision, "editor_productivity_plan.schema_unsupported",
                    "Editor productivity plan schema must be 1.");
            if (request.BaseRevision != _history.State.Revision.Value)
                return EditorProductivityFailure(request.BaseRevision, "editor_productivity_plan.revision_conflict",
                    "Editor productivity plan base revision is stale.");
            try
            {
                var original = LoadEditorProductivityProfile();
                var candidate = BuildEditorProductivityProfile(original, request);
                var diagnostics = EditorWorkspaceProjectValidator.Validate(candidate, _history.State.Project);
                if (diagnostics.Count > 0)
                    return new(false, null, request.BaseRevision, null, null, null,
                        diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
                        false, false, []);
                var planId = Guid.NewGuid().ToString("D");
                var diff = new ProjectPatch("replace", ".forge/editor-profile.json",
                    $"Configure editor productivity profile '{candidate.Name}'.");
                var inverse = new ProjectPatch("replace", ".forge/editor-profile.json",
                    $"Restore editor productivity profile '{original.Name}'.");
                var stored = new StoredEditorProductivityPlan(planId, request.BaseRevision, original, candidate,
                    diff, inverse, [new(planId, "planned", request.BaseRevision, _clock.UtcNow, true)]);
                _editorProductivityPlans.Add(planId, stored);
                return ToResult(stored);
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidDataException)
            {
                return EditorProductivityFailure(request.BaseRevision, "editor_productivity_plan.request_invalid",
                    exception.Message);
            }
        }
    }

    public EditorProductivityPlanResult ApplyEditorProductivityPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_editorProductivityPlans.TryGetValue(planId, out var plan))
                return EditorProductivityFailure(_history.State.Revision.Value, "editor_productivity_plan.missing",
                    $"Editor productivity plan '{planId}' does not exist.");
            if (plan.Applied)
                return EditorProductivityFailure(plan.BaseRevision, "editor_productivity_plan.already_applied",
                    $"Editor productivity plan '{planId}' is already applied.");
            if (plan.BaseRevision != _history.State.Revision.Value)
                return EditorProductivityFailure(plan.BaseRevision, "editor_productivity_plan.revision_conflict",
                    "Editor productivity plan base revision is stale.");
            try
            {
                EditorWorkspaceProfileStore.Save(_session.RootDirectory, plan.Candidate);
                plan.Applied = true;
                plan.Audit.Add(new(planId, "apply", _history.State.Revision.Value, _clock.UtcNow, true));
                return ToResult(plan);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                plan.Audit.Add(new(planId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = [new("editor_productivity_plan.save_failed", exception.Message,
                        ".forge/editor-profile.json")]
                };
            }
        }
    }

    public EditorProductivityPlanResult RevertEditorProductivityPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_editorProductivityPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return EditorProductivityFailure(_history.State.Revision.Value,
                    "editor_productivity_plan.not_applied", $"Editor productivity plan '{planId}' is not applied.");
            try
            {
                EditorWorkspaceProfileStore.Save(_session.RootDirectory, plan.Original);
                plan.Reverted = true;
                plan.Audit.Add(new(planId, "revert", _history.State.Revision.Value, _clock.UtcNow, true));
                return ToResult(plan);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = [new("editor_productivity_plan.revert_failed", exception.Message,
                        ".forge/editor-profile.json")]
                };
            }
        }
    }

    private EditorWorkspaceProfile LoadEditorProductivityProfile() =>
        EditorWorkspaceProfileStore.TryLoad(_session.RootDirectory) ??
        EditorWorkspaceProfile.CreateDefault(EditorRole.Builder, _history.State.Project.Module.EntryAreaId);

    private static EditorWorkspaceProfile BuildEditorProductivityProfile(EditorWorkspaceProfile source,
        EditorProductivityPlanRequest request)
    {
        var role = Enum.Parse<EditorRole>(request.Role, true);
        var identity = role == source.Role ? source : EditorWorkspaceProfile.CreateDefault(role,
            string.IsNullOrWhiteSpace(request.ActiveAreaId) ? source.ActiveAreaId : AreaId.Parse(request.ActiveAreaId));
        var layers = request.VisibleLayerIds.Select(Guid.Parse).ToArray();
        var volumes = request.VisibleVolumeKinds.Select(value => Enum.Parse<EditorVolumeKind>(value, true)).ToArray();
        var filter = new EditorDisplayFilter(source.DisplayFilter.VisibleKinds, source.DisplayFilter.RequiredTags,
            source.DisplayFilter.Author, source.DisplayFilter.State, source.DisplayFilter.IsolateSelection,
            source.DisplayFilter.GhostHidden, layers, volumes, identity.DisplayFilter.Id, 1, "mcp:r059");
        var repeated = new EditorRepeatedPlacementProfile(identity.RepeatedPlacementProfile.Id, 1, "mcp:r059", true,
            request.BrushMode, request.ContinuousStroke,
            Enum.Parse<EditorPlacementDistribution>(request.Distribution, true),
            Enum.Parse<EditorPlacementAlignment>(request.Alignment, true), (float)request.Spacing,
            request.Columns, request.Seed, (float)request.SurfaceOffset);
        var multi = new EditorMultiAreaProfile(identity.MultiArea.Id, 1, "mcp:r059", request.SplitView,
            Enum.Parse<EditorSplitAxis>(request.SplitAxis, true), (float)request.SplitRatio, true, true);
        var help = new EditorContextualHelpProfile(identity.Help.Id, 1, "mcp:r059", request.ContextualHelp,
            request.SourcedAssistant, source.Help.Locale, request.ActiveHelpTopic);
        var active = string.IsNullOrWhiteSpace(request.ActiveAreaId) ? source.ActiveAreaId : AreaId.Parse(request.ActiveAreaId);
        var comparison = string.IsNullOrWhiteSpace(request.ComparisonAreaId) ? source.ComparisonAreaId : AreaId.Parse(request.ComparisonAreaId);
        var tabs = source.OpenAreaTabs.Concat(new[] { active, comparison }.OfType<AreaId>()).Distinct().Take(8).ToArray();
        return new(identity.Id, role.ToString(), role, filter, true, tabs, active, comparison, request.ContextualHelp,
            "mcp:r059", EditorWorkspaceProfile.CurrentSchemaVersion, source.UiScale, source.TextScale,
            source.PrimaryFontFamily, source.FallbackFontFamilies, source.PersonalFavoriteBlueprintIds,
            source.RecentBlueprintIds, source.Shell, repeated, multi, help,
            EditorRolePolicy.CreateDefault(identity.Id.Value, role));
    }

    private static EditorProductivityProfileItem MapEditorProductivity(EditorWorkspaceProfile profile) => new(
        profile.Id.ToString(), profile.Name, profile.Role.ToString(),
        profile.DisplayFilter.VisibleKinds.Select(value => value.ToString()).ToArray(),
        profile.DisplayFilter.VisibleLayerIds.Select(value => value.ToString("D")).ToArray(),
        profile.DisplayFilter.VisibleVolumeKinds.Select(value => value.ToString()).ToArray(),
        profile.DisplayFilter.IsolateSelection, profile.DisplayFilter.GhostHidden,
        profile.RepeatedPlacementProfile.BrushMode, profile.RepeatedPlacementProfile.ContinuousStroke,
        profile.RepeatedPlacementProfile.Distribution.ToString(), profile.RepeatedPlacementProfile.Alignment.ToString(),
        profile.RepeatedPlacementProfile.Spacing, profile.MultiArea.SplitViewEnabled,
        profile.MultiArea.SplitAxis.ToString(), profile.ActiveAreaId?.ToString(), profile.ComparisonAreaId?.ToString(),
        profile.Help.Enabled, profile.Help.SourcedAssistantEnabled,
        profile.RolePolicy.VisiblePanels.Select(value => value.ToString()).ToArray(),
        Enum.GetValues<EditorPermission>().Where(value => value != EditorPermission.None && profile.RolePolicy.Allows(value))
            .Select(value => value.ToString()).ToArray());

    private static EditorHelpTopicItem MapHelpTopic(EditorHelpTopic topic) => new(topic.Key, topic.Title,
        topic.Description, topic.Dependencies, topic.ForgeScriptExamples, topic.Citations);

    private static EditorProductivityPlanResult ToResult(StoredEditorProductivityPlan plan) => new(true,
        plan.PlanId, plan.BaseRevision, MapEditorProductivity(plan.Candidate), plan.Diff, plan.Inverse, [],
        plan.Applied, plan.Reverted, plan.Audit.ToArray());

    private static EditorProductivityPlanResult EditorProductivityFailure(long revision, string code, string message) =>
        new(false, null, revision, null, null, null, [new(code, message, "editorProductivityPlan")], false, false, []);

    public DialoguePlanResult PlanDialogue(DialoguePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != DialoguePlanSchema.CurrentVersion)
            {
                return DialogueFailure(
                    request.BaseRevision,
                    "dialogue_plan.schema_unsupported",
                    $"Dialogue plan schema {request.SchemaVersion} is unsupported; expected {DialoguePlanSchema.CurrentVersion}.");
            }

            DialogueDefinition dialogue;
            try
            {
                dialogue = MapDialogue(request);
            }
            catch (Exception exception) when (
                exception is ArgumentException or FormatException or OverflowException)
            {
                return DialogueFailure(request.BaseRevision, "dialogue_plan.invalid", exception.Message);
            }

            var command = new SetDialogueCommand(
                CommandId.From(Guid.NewGuid()),
                new ProjectRevision(request.BaseRevision),
                dialogue,
                DryRun: true);
            var preview = _dialogueHandler.Handle(_history.State, command);
            if (!preview.Succeeded)
            {
                return new DialoguePlanResult(
                    false,
                    null,
                    request.BaseRevision,
                    preview.Patch,
                    preview.Inverse,
                    preview.Diagnostics.Select(Map).ToArray(),
                    false,
                    false,
                    []);
            }

            var planId = Guid.NewGuid().ToString("D");
            var audit = new List<PlanAuditEntry>
            {
                new(planId, "planned", request.BaseRevision, _clock.UtcNow, true),
            };
            _dialoguePlans.Add(
                planId,
                new StoredDialoguePlan(planId, dialogue, request.BaseRevision, preview.Patch!, preview.Inverse!, audit));
            return ToResult(_dialoguePlans[planId]);
        }
    }

    public DialoguePlanResult ApplyDialoguePlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_dialoguePlans.TryGetValue(planId, out var plan))
            {
                return DialogueFailure(GetCurrentRevision(), "dialogue_plan.missing", $"Dialogue plan '{planId}' does not exist.");
            }

            if (plan.Applied)
            {
                return DialogueFailure(plan.BaseRevision, "dialogue_plan.already_applied", $"Dialogue plan '{planId}' is already applied.");
            }

            var command = new SetDialogueCommand(
                CommandId.From(Guid.NewGuid()),
                new ProjectRevision(plan.BaseRevision),
                plan.Dialogue,
                DryRun: false);
            var result = _dialogueHandler.Handle(_history.State, command);
            if (!result.Succeeded)
            {
                plan.Audit.Add(new PlanAuditEntry(planId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return WithFailure(plan, result.Diagnostics.Select(Map).ToArray());
            }

            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
            {
                plan.Audit.Add(new PlanAuditEntry(planId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return WithFailure(
                    plan,
                    saved.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
            }

            _history.Record(result);
            plan.Applied = true;
            plan.AppliedRevision = result.NewRevision!.Value.Value;
            plan.Audit.Add(new PlanAuditEntry(planId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public DialoguePlanResult RevertDialoguePlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_dialoguePlans.TryGetValue(planId, out var plan))
            {
                return DialogueFailure(GetCurrentRevision(), "dialogue_plan.missing", $"Dialogue plan '{planId}' does not exist.");
            }

            if (!plan.Applied || plan.Reverted)
            {
                return DialogueFailure(plan.BaseRevision, "dialogue_plan.not_applied", $"Dialogue plan '{planId}' is not currently applied.");
            }

            if (plan.AppliedRevision != _history.State.Revision.Value)
            {
                return DialogueFailure(
                    plan.BaseRevision,
                    "dialogue_plan.revision_conflict",
                    "Only the most recently applied plan can be reverted without overwriting later work.");
            }

            var undone = _history.Undo();
            if (!undone.Succeeded)
            {
                return DialogueFailure(plan.BaseRevision, "dialogue_plan.inverse_unavailable", "The inverse patch is unavailable.");
            }

            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                plan.Audit.Add(new PlanAuditEntry(planId, "revert", _history.State.Revision.Value, _clock.UtcNow, false));
                return WithFailure(
                    plan,
                    saved.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
            }

            plan.Reverted = true;
            plan.Audit.Add(new PlanAuditEntry(planId, "revert", undone.State.Revision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public ScriptBindingPlanResult PlanScriptBinding(ScriptBindingPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
            {
                return BindingFailure(request.BaseRevision, "script_binding.schema_unsupported", "Script binding schema must be 1.");
            }

            if (!AllowedScriptBindings.Contains(request.Binding))
            {
                return BindingFailure(
                    request.BaseRevision,
                    "script_binding.binding_invalid",
                    "Binding must be scriptPath, phaseTwoScript or deathScript.");
            }

            if (!IsSafeForgeScriptPath(request.RelativeScriptPath))
            {
                return BindingFailure(
                    request.BaseRevision,
                    "script_binding.path_unsafe",
                    "The script path must be a relative .forge path without traversal segments.");
            }

            try
            {
                var areaId = AreaId.From(Guid.Parse(request.AreaId));
                var entityId = EntityId.From(Guid.Parse(request.EntityId));
                var entity = _history.State.Project.Areas
                    .SingleOrDefault(area => area.Id == areaId)?
                    .Entities.SingleOrDefault(candidate => candidate.Id == entityId);
                var existing = entity?.Overrides.Properties ?? [];
                var properties = existing
                    .Where(item => !string.Equals(item.Key, request.Binding, StringComparison.Ordinal))
                    .Append(new EntityPropertyOverride(
                        request.Binding,
                        EntityPropertyValueKind.String,
                        request.RelativeScriptPath.Replace('\\', '/')))
                    .ToArray();
                var command = new SetEntityOverridesCommand(
                    CommandId.From(Guid.NewGuid()),
                    new ProjectRevision(request.BaseRevision),
                    areaId,
                    entityId,
                    new EntityPropertyOverrides(EntityPropertyOverrides.CurrentSchemaVersion, properties),
                    DryRun: true);
                var result = _overridesHandler.Handle(_history.State, command);
                return new ScriptBindingPlanResult(
                    result.Succeeded,
                    result.Succeeded ? Guid.NewGuid().ToString("D") : null,
                    request.BaseRevision,
                    result.Patch,
                    result.Inverse,
                    result.Diagnostics.Select(Map).ToArray());
            }
            catch (Exception exception) when (
                exception is ArgumentException or FormatException or InvalidOperationException)
            {
                return BindingFailure(request.BaseRevision, "script_binding.invalid", exception.Message);
            }
        }
    }

    private static DialogueDefinition MapDialogue(DialoguePlanRequest request)
    {
        var dialogue = new DialogueDefinition(
            DialogueId.From(Guid.Parse(request.DialogueId)),
            request.Slug,
            request.DisplayName,
            DialogueNodeId.From(Guid.Parse(request.EntryNodeId)),
            request.Nodes.Select(MapNode).ToArray());
        if (request.Authoring is not null) dialogue = dialogue.WithAuthoring(MapDialogueAuthoring(request.Authoring));
        if (request.Experience is not null) dialogue = dialogue.WithExperience(MapDialogueExperience(request.Experience));
        return request.Production is null ? dialogue : dialogue.WithProduction(MapDialogueProduction(request.Production));
    }

    private static DialogueAuthoringProfile MapDialogueAuthoring(DialogueAuthoringInput input) => new(
        Guid.Parse(input.AuthoringId),
        DialogueAuthoringProfile.CurrentSchemaVersion,
        "agent:mcp-dialogue-plan",
        input.Enabled,
        input.RequiredAtRuntime,
        input.Participants.Select(item => new DialogueParticipantBinding(
            Guid.Parse(item.BindingId), DialogueParticipantBinding.CurrentSchemaVersion,
            "agent:mcp-dialogue-plan", item.Role,
            Enum.Parse<DialogueParticipantBindingKind>(item.Kind, ignoreCase: true), item.Value,
            item.FallbackRole)).ToArray(),
        input.Nodes.Select(item => new DialogueNodeAuthoring(
            Guid.Parse(item.AuthoringNodeId), DialogueNodeAuthoring.CurrentSchemaVersion,
            "agent:mcp-dialogue-plan", DialogueNodeId.Parse(item.NodeId), item.SpeakerRole,
            Enum.Parse<DialogueChoiceEvaluationMode>(item.EvaluationMode, ignoreCase: true),
            item.Tokens.Select(token => new DialogueTextToken(Guid.Parse(token.TokenId),
                DialogueTextToken.CurrentSchemaVersion, "agent:mcp-dialogue-plan", token.Name,
                Enum.Parse<DialogueTextTokenType>(token.Type, ignoreCase: true), token.Required,
                token.DefaultValue)).ToArray(),
            item.ChoiceEvaluations.Select(value => new DialogueChoiceEvaluation(
                Guid.Parse(value.EvaluationId), DialogueChoiceEvaluation.CurrentSchemaVersion,
                "agent:mcp-dialogue-plan", DialogueChoiceId.Parse(value.ChoiceId), value.Weight)).ToArray(),
            item.WizardEntries.Select(value => new DialogueWizardEntry(Guid.Parse(value.EntryId),
                DialogueWizardEntry.CurrentSchemaVersion, "agent:mcp-dialogue-plan",
                Enum.Parse<DialogueWizardEntryKind>(value.Kind, ignoreCase: true), Guid.Parse(value.TargetId),
                value.SourceDescription)).ToArray(),
            new DialogueNodeEditorialNotes(Guid.Parse(item.Notes.NotesId),
                DialogueNodeEditorialNotes.CurrentSchemaVersion, "agent:mcp-dialogue-plan", item.Notes.Intent,
                item.Notes.Emotion, item.Notes.Source,
                Enum.Parse<DialogueNodeEditorialStatus>(item.Notes.Status, ignoreCase: true), item.Notes.Todos,
                item.Notes.AuthorMention))).ToArray());

    private static DialogueAuthoringResource? MapDialogueAuthoringResource(DialogueAuthoringProfile? profile) =>
        profile is null
            ? null
            : new DialogueAuthoringResource(
                profile.Id.ToString("D"), profile.SchemaVersion, profile.Provenance, profile.Enabled,
                profile.RequiredAtRuntime,
                profile.Participants.Select(item => new DialogueParticipantBindingResource(
                    item.Id.ToString("D"), item.Role, item.Kind.ToString(), item.Value,
                    item.FallbackRole)).ToArray(),
                profile.Nodes.Select(item => new DialogueNodeAuthoringResource(
                    item.Id.ToString("D"), item.NodeId.ToString(), item.SpeakerRole,
                    item.EvaluationMode.ToString(),
                    item.Tokens.Select(token => $"{token.Name}:{token.Type}:{token.Required}").ToArray(),
                    item.ChoiceEvaluations.Select(value => $"{value.ChoiceId}:{value.Weight:R}").ToArray(),
                    item.WizardEntries.Select(value => value.SourceDescription).ToArray(), item.Notes.Intent,
                    item.Notes.Emotion, item.Notes.Source, item.Notes.Status.ToString(), item.Notes.Todos,
                    item.Notes.AuthorMention)).ToArray());

    private static DialogueExperienceProfile MapDialogueExperience(DialogueExperienceInput input) => new(
        Guid.Parse(input.ExperienceId), DialogueExperienceProfile.CurrentSchemaVersion,
        "agent:mcp-dialogue-plan", input.Enabled, input.RequiredAtRuntime,
        new DialogueLifecyclePolicy(Guid.Parse(input.Lifecycle.LifecycleId),
            DialogueLifecyclePolicy.CurrentSchemaVersion, "agent:mcp-dialogue-plan",
            input.Lifecycle.CompletedActions.Select(MapAction).ToArray(),
            input.Lifecycle.AbortedActions.Select(MapAction).ToArray(), input.Lifecycle.RollbackOnAbort,
            input.Lifecycle.CreateResumeState),
        new DialogueSearchProfile(Guid.Parse(input.Search.SearchId), DialogueSearchProfile.CurrentSchemaVersion,
            "agent:mcp-dialogue-plan", input.Search.Pattern, input.Search.UseRegex, input.Search.IgnoreCase,
            input.Search.SpeakerRoles,
            input.Search.ConditionKinds.Select(item => Enum.Parse<DialogueConditionKind>(item, true)).ToArray(),
            input.Search.ActionKinds.Select(item => Enum.Parse<DialogueActionKind>(item, true)).ToArray(),
            input.Search.ReplacementPreview),
        new DialogueReviewProfile(Guid.Parse(input.Review.ReviewId), DialogueReviewProfile.CurrentSchemaVersion,
            "agent:mcp-dialogue-plan",
            input.Review.Badges.Select(item => Enum.Parse<DialogueReviewBadge>(item, true)).ToArray(),
            input.Review.SpeakerRoles, input.Review.ReviewMode),
        input.MemorySlots.Select(item => new DialogueMemorySlot(Guid.Parse(item.SlotId),
            DialogueMemorySlot.CurrentSchemaVersion, "agent:mcp-dialogue-plan",
            Enum.Parse<DialogueMemoryScope>(item.Scope, true), item.ScopeId, item.Name,
            Enum.Parse<DialogueTextTokenType>(item.Type, true),
            Enum.Parse<DialogueMemoryDuration>(item.Duration, true), item.DefaultValue)).ToArray(),
        input.SkillChecks.Select(item => new DialogueSkillCheck(Guid.Parse(item.CheckId),
            DialogueSkillCheck.CurrentSchemaVersion, "agent:mcp-dialogue-plan", DialogueNodeId.Parse(item.NodeId),
            item.SkillId, item.Difficulty, item.OpponentId, item.Advantage,
            Enum.Parse<DialogueSkillCheckVisibility>(item.Visibility, true),
            DialogueNodeId.Parse(item.SuccessNodeId), DialogueNodeId.Parse(item.FailureNodeId))).ToArray());

    private static DialogueExperienceResource? MapDialogueExperienceResource(DialogueExperienceProfile? profile) =>
        profile is null
            ? null
            : new DialogueExperienceResource(profile.Id.ToString("D"), profile.SchemaVersion, profile.Provenance,
                profile.Enabled, profile.RequiredAtRuntime, profile.Lifecycle.CompletedActions.Count,
                profile.Lifecycle.AbortedActions.Count, profile.Lifecycle.RollbackOnAbort,
                profile.Lifecycle.CreateResumeState, profile.Search.Pattern, profile.Search.UseRegex,
                [.. profile.Search.SpeakerRoles.Select(item => $"speaker:{item}"),
                    .. profile.Search.ConditionKinds.Select(item => $"condition:{item}"),
                    .. profile.Search.ActionKinds.Select(item => $"action:{item}")],
                profile.Review.Badges.Select(item => item.ToString()).ToArray(), profile.Review.ReviewMode,
                profile.MemorySlots.Select(item =>
                    $"{item.Scope}:{item.ScopeId}:{item.Name}:{item.Type}:{item.Duration}").ToArray(),
                profile.SkillChecks.Select(item =>
                    $"{item.NodeId}:{item.SkillId}:{item.Difficulty:R}:{item.Visibility}").ToArray());

    private static DialogueProductionProfile MapDialogueProduction(DialogueProductionInput input)
    {
        const string provenance = "agent:mcp-dialogue-plan";
        return new DialogueProductionProfile(Guid.Parse(input.ProductionId), 1, provenance, input.Enabled,
            input.RequiredAtRuntime,
            input.Subgraphs.Select(item => new DialogueSubgraphContract(Guid.Parse(item.ContractId), 1, provenance,
                item.Key, item.Version,
                item.Parameters.Select(value => new DialogueSubgraphParameter(Guid.Parse(value.ParameterId), 1,
                    provenance, value.Name, Enum.Parse<DialogueTextTokenType>(value.Type, true),
                    Enum.Parse<DialogueSubgraphParameterDirection>(value.Direction, true), value.Required,
                    value.DefaultValue)).ToArray(), item.RequiredVariables,
                item.Tests.Select(value => new DialogueSubgraphTestCase(Guid.Parse(value.TestId), 1, provenance,
                    value.Name, value.Inputs, value.ExpectedOutputs)).ToArray())).ToArray(),
            input.Invocations.Select(item => new DialogueSubgraphInvocation(Guid.Parse(item.InvocationId), 1,
                provenance, DialogueNodeId.Parse(item.NodeId), Guid.Parse(item.ContractId), item.InputBindings,
                item.OutputBindings)).ToArray(),
            input.Consequences.Select(item => new DialogueTypedConsequence(Guid.Parse(item.ConsequenceId), 1,
                provenance, DialogueNodeId.Parse(item.NodeId),
                Enum.Parse<DialogueConsequenceTargetKind>(item.Kind, true), item.TargetId, item.Operation, item.Value,
                item.Explanation)).ToArray(),
            new DialogueCoveragePolicy(Guid.Parse(input.Coverage.CoverageId), 1, provenance,
                input.Coverage.MaximumDepth, input.Coverage.RequiredStates,
                input.Coverage.Evidence.Select(item => new DialogueCoverageEvidence(Guid.Parse(item.EvidenceId), 1,
                    provenance, item.Name, Enum.Parse<DialogueCoverageEvidenceKind>(item.Kind, true),
                    item.VisitedNodeIds.Select(DialogueNodeId.Parse).ToArray(), item.State)).ToArray()),
            new DialogueCollaborationProfile(Guid.Parse(input.Collaboration.CollaborationId), 1, provenance,
                input.Collaboration.Revisions.Select(item => new DialogueNodeRevision(Guid.Parse(item.RevisionId), 1,
                    provenance, DialogueNodeId.Parse(item.NodeId), item.Revision, item.AuthorId, item.CanonicalJson,
                    item.At)).ToArray(),
                input.Collaboration.Locks.Select(item => new DialogueNodeLock(Guid.Parse(item.LockId), 1, provenance,
                    DialogueNodeId.Parse(item.NodeId), item.OwnerId, item.ExpiresAt)).ToArray(),
                input.Collaboration.Comments.Select(item => new DialogueNodeComment(Guid.Parse(item.CommentId), 1,
                    provenance, DialogueNodeId.Parse(item.NodeId), item.AuthorId, item.Text, item.Resolved,
                    item.At)).ToArray(),
                input.Collaboration.Conflicts.Select(item => new DialogueSemanticConflict(Guid.Parse(item.ConflictId),
                    1, provenance, DialogueNodeId.Parse(item.NodeId), item.Field, item.BaseValue, item.OursValue,
                    item.TheirsValue, Enum.Parse<DialogueConflictResolution>(item.Resolution, true), item.ManualValue,
                    item.Resolved)).ToArray()),
            input.AiPlans.Select(item => new DialogueAiPlan(Guid.Parse(item.AiId), 1, provenance,
                Guid.Parse(item.TransactionId), Guid.Parse(item.PlanId),
                Enum.Parse<DialogueAiAuthorizationLevel>(item.Authorization, true), item.Synopsis,
                item.RequiredBeats, item.StyleRules, item.ContextCitations, item.SemanticDiff, item.Lint,
                item.PreviewOnly, item.Applied)).ToArray());
    }

    private static DialogueProductionResource? MapDialogueProductionResource(DialogueProductionProfile? profile) =>
        profile is null ? null : new DialogueProductionResource(profile.Id.ToString("D"), profile.SchemaVersion,
            profile.Provenance, profile.Enabled, profile.RequiredAtRuntime,
            profile.Subgraphs.Select(item =>
                $"{item.Key}:v{item.Version}:parameters={item.Parameters.Count}:tests={item.Tests.Count}").ToArray(),
            profile.Consequences.Select(item =>
                $"{item.Kind}:{item.TargetId}:{item.Operation}:{item.Value}").ToArray(),
            [
                $"paths:max-depth={profile.Coverage.MaximumDepth}",
                $"states:{profile.Coverage.RequiredStates.Count}",
                $"evidence:{profile.Coverage.Evidence.Count}",
            ],
            [
                $"revisions:{profile.Collaboration.Revisions.Count}",
                $"locks:{profile.Collaboration.Locks.Count}",
                $"comments:{profile.Collaboration.Comments.Count}",
                $"conflicts:{profile.Collaboration.Conflicts.Count}",
            ],
            profile.AiPlans.Select(item =>
                $"{item.PlanId:D}:{item.Authorization}:preview={item.PreviewOnly}:applied={item.Applied}").ToArray());

    private static DialogueNode MapNode(DialogueNodeInput node) => new(
        DialogueNodeId.From(Guid.Parse(node.NodeId)),
        string.IsNullOrWhiteSpace(node.SpeakerBlueprintId)
            ? null
            : BlueprintId.From(Guid.Parse(node.SpeakerBlueprintId)),
        new LocalizedText(node.LocalizationKey, node.FallbackText,
            (node.Variants ?? []).Select(item => MapVariant(node.LocalizationKey, item)).ToArray(),
            "agent:mcp-dialogue-plan"),
        (node.OnEnterActions ?? []).Select(MapAction).ToArray(),
        (node.Choices ?? []).Select(MapChoice).ToArray(),
        string.IsNullOrWhiteSpace(node.Role)
            ? DialogueNodeRole.Unspecified
            : Enum.Parse<DialogueNodeRole>(node.Role, ignoreCase: true),
        string.IsNullOrWhiteSpace(node.ParticipantId) ? Guid.Empty : Guid.Parse(node.ParticipantId),
        node.SubgraphKey);

    private static DialogueChoice MapChoice(DialogueChoiceInput choice) => new(
        DialogueChoiceId.From(Guid.Parse(choice.ChoiceId)),
        new LocalizedText(choice.LocalizationKey, choice.FallbackText,
            (choice.Variants ?? []).Select(item => MapVariant(choice.LocalizationKey, item)).ToArray(),
            "agent:mcp-dialogue-plan"),
        (choice.Conditions ?? []).Select(MapCondition).ToArray(),
        (choice.Actions ?? []).Select(MapAction).ToArray(),
        string.IsNullOrWhiteSpace(choice.TargetNodeId)
            ? null
            : DialogueNodeId.From(Guid.Parse(choice.TargetNodeId)),
        string.IsNullOrWhiteSpace(choice.EdgeKind)
            ? DialogueEdgeKind.Flow
            : Enum.Parse<DialogueEdgeKind>(choice.EdgeKind, ignoreCase: true),
        choice.Priority,
        string.IsNullOrWhiteSpace(choice.ConditionCombination)
            ? DialogueConditionCombination.All
            : Enum.Parse<DialogueConditionCombination>(choice.ConditionCombination, ignoreCase: true));

    private static DialogueCondition MapCondition(DialogueConditionInput condition) => new(
        Enum.Parse<DialogueConditionKind>(condition.Kind, ignoreCase: true),
        condition.Key,
        condition.ExpectedValue,
        condition.Negated,
        condition.EventBinding ?? "on-choice-visible",
        condition.ForgeScriptApiVersion,
        "agent:mcp-dialogue-plan");

    private static DialogueAction MapAction(DialogueActionInput action) => new(
        Enum.Parse<DialogueActionKind>(action.Kind, ignoreCase: true),
        action.Key,
        action.Value,
        action.Required,
        "agent:mcp-dialogue-plan");

    private static LocalizedTextVariant MapVariant(string textKey, DialogueLocalizedTextVariantInput variant) =>
        LocalizedTextVariant.Create(textKey, variant.Locale, variant.Text, variant.Gender,
            Enum.Parse<DialoguePluralCategory>(variant.PluralCategory, ignoreCase: true),
            "agent:mcp-dialogue-plan");

    private static bool IsSafeForgeScriptPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) ||
            !string.Equals(Path.GetExtension(path), ".forge", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return path.Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .All(segment => segment is not "." and not "..");
    }

    private static AgentDiagnostic Map(CommandDiagnostic diagnostic) => new(diagnostic.Code, diagnostic.Message);

    private static AgentDiagnostic Map(DomainDiagnostic diagnostic) =>
        new(diagnostic.Code, diagnostic.Message, diagnostic.Path);

    private static AgentDiagnostic Map(ProjectSessionDiagnostic diagnostic) =>
        new(diagnostic.Code, diagnostic.Message, diagnostic.Path);

    private static AgentDiagnostic MapScript(ForgeScriptDiagnostic diagnostic) => new(
        "assistant.script." + diagnostic.Severity.ToLowerInvariant(),
        diagnostic.Message,
        diagnostic.Source);

    private static AssetAvailability? ParseAssetAvailability(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<AssetAvailability>(value, ignoreCase: true, out var availability) &&
            Enum.IsDefined(availability)
                ? availability
                : throw new ArgumentException(
                    $"Unknown asset availability '{value}'. Expected CatalogOnly, OriginalAvailable or ExtractedAvailable.",
                    nameof(value));
    }

    private static AgentDiagnostic Map(ActionableDiagnostic diagnostic) => new(
        diagnostic.Code,
        diagnostic.Message,
        diagnostic.DocumentPath,
        diagnostic.Id.ToString(),
        diagnostic.SchemaVersion,
        diagnostic.Severity.ToString(),
        diagnostic.SubjectUuid?.ToString("D"),
        diagnostic.Property,
        diagnostic.Source,
        diagnostic.QuickFixes.Select(fix => new AgentQuickFix(
            fix.Id.ToString(),
            fix.SchemaVersion,
            fix.Label,
            fix.Description,
            fix.Kind.ToString(),
            fix.AreaId.ToString(),
            fix.EntityId?.ToString(),
            fix.Provenance)).ToArray(),
        diagnostic.Provenance);

    private AgentAssetInstallAuditEntry AssetInstallAudit(
        string planId,
        string action,
        long revision,
        bool succeeded,
        AssetCatalogItem item) =>
        new(planId, action, revision, _clock.UtcNow, succeeded, item.Id.ToString(), item.LicenseSpdx);

    private IReadOnlyList<AgentDiagnostic> RestoreAssetInstallMaterialization(StoredAssetInstallPlan plan)
    {
        var diagnostics = new List<AgentDiagnostic>();
        if (plan.ManifestFileMissing)
        {
            var manifestPath = Path.Combine(
                _session.RootDirectory,
                AssetPackageManifestStore.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            try
            {
                if (File.Exists(manifestPath)) File.Delete(manifestPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(new AgentDiagnostic(
                    AssetPackageManifestStore.WriteFailedCode,
                    exception.Message,
                    AssetPackageManifestStore.RelativePath));
            }
        }
        else
        {
            var restored = AssetPackageManifestStore.Save(_session.RootDirectory, plan.PreviousManifests);
            diagnostics.AddRange(restored.Diagnostics.Select(item =>
                new AgentDiagnostic(item.Code, item.Message, item.Path)));
        }

        if (plan.PackageExecution is { } execution)
        {
            var reverted = LocalAssetPackageInstaller.Revert(_session.RootDirectory, execution);
            diagnostics.AddRange(reverted.Diagnostics.Select(item =>
                new AgentDiagnostic(item.Code, item.Message, item.Path)));
            if (reverted.Succeeded) plan.PackageExecution = null;
        }

        return diagnostics;
    }

    private static AgentAssetInstallPlanResult AssetInstallFailure(
        long revision,
        string? assetId,
        string code,
        string message) =>
        new(false, null, revision, assetId, null, null, null,
            [new AgentDiagnostic(code, message)], false, false, []);

    private static AgentAssetInstallPlanResult ToResult(StoredAssetInstallPlan plan) => new(
        true,
        plan.PlanId,
        plan.Command.BaseRevision.Value,
        plan.Command.Item.Id.ToString(),
        plan.Command.Item.LicenseSpdx,
        plan.Diff,
        plan.Inverse,
        [],
        plan.Applied,
        plan.Reverted,
        plan.Audit.ToArray(),
        plan.LocalPackage?.Reused ?? true);

    private static AssistedDialogueApplyPlanResult AssistedDialogueFailure(
        long revision,
        string code,
        string message) => new(
        false,
        null,
        null,
        null,
        revision,
        null,
        null,
        "no-change",
        [],
        [],
        [],
        0,
        [new AgentDiagnostic(code, message, "assistant/dialogue")],
        false,
        false,
        []);

    private static AssistedDialogueApplyPlanResult WithFailure(
        StoredAssistedDialoguePlan plan,
        IReadOnlyList<AgentDiagnostic> diagnostics) => ToResult(plan) with
        {
            Succeeded = false,
            Diagnostics = diagnostics,
        };

    private static AssistedDialogueApplyPlanResult ToResult(StoredAssistedDialoguePlan plan) => new(
        true,
        plan.PlanId,
        plan.Command.TransactionId.ToString("D"),
        plan.Command.Proposal.Dialogue.Id.ToString(),
        plan.Command.BaseRevision.Value,
        plan.Diff,
        plan.Inverse,
        plan.Command.Proposal.SemanticDiff,
        plan.Command.Proposal.Citations,
        plan.Command.Proposal.LocalizationPlaceholders,
        plan.Command.Proposal.Lint.Select(item => item.Code).ToArray(),
        plan.Command.Proposal.EstimatedCostUsd,
        [],
        plan.Applied,
        plan.Reverted,
        plan.Audit.ToArray());

    private static AssistedQuestApplyPlanResult AssistedQuestFailure(
        long revision,
        string code,
        string message) => new(
        false,
        null,
        null,
        null,
        revision,
        null,
        null,
        "no-change",
        [],
        [],
        [],
        0,
        [new AgentDiagnostic(code, message, "assistant/quest")],
        false,
        false,
        []);

    private static AssistedQuestApplyPlanResult WithFailure(
        StoredAssistedQuestPlan plan,
        IReadOnlyList<AgentDiagnostic> diagnostics) => ToResult(plan) with
        {
            Succeeded = false,
            Diagnostics = diagnostics,
        };

    private static AssistedQuestApplyPlanResult ToResult(StoredAssistedQuestPlan plan) => new(
        true,
        plan.PlanId,
        plan.Command.TransactionId.ToString("D"),
        plan.Command.Proposal.Quest.Id.ToString(),
        plan.Command.BaseRevision.Value,
        plan.Diff,
        plan.Inverse,
        plan.Command.Proposal.SemanticDiff,
        plan.Command.Proposal.Citations,
        plan.Command.Proposal.Lint.Select(item => item.Code).ToArray(),
        plan.Command.Proposal.TestScenarios.Select(item => item.Kind.ToString()).ToArray(),
        plan.Command.Proposal.EstimatedCostUsd,
        [],
        plan.Applied,
        plan.Reverted,
        plan.Audit.ToArray());

    private static AssistedBossApplyPlanResult AssistedBossFailure(
        long revision,
        string code,
        string message) => new(
        false,
        null,
        null,
        null,
        revision,
        null,
        null,
        "no-change",
        [],
        [],
        0,
        false,
        0,
        [new AgentDiagnostic(code, message, "assistant/boss")],
        false,
        false,
        []);

    private static AssistedBossApplyPlanResult WithFailure(
        StoredAssistedBossPlan plan,
        IReadOnlyList<AgentDiagnostic> diagnostics) => ToResult(plan) with
        {
            Succeeded = false,
            Diagnostics = diagnostics,
        };

    private static AssistedBossApplyPlanResult ToResult(StoredAssistedBossPlan plan) => new(
        true,
        plan.PlanId,
        plan.Command.TransactionId.ToString("D"),
        plan.Command.Proposal.Boss.Id.ToString("D"),
        plan.Command.BaseRevision.Value,
        plan.Diff,
        plan.Inverse,
        plan.Command.Proposal.SemanticDiff,
        plan.Command.Proposal.Citations,
        plan.Command.Proposal.Diagnostics.Select(item => item.Code).ToArray(),
        plan.Command.Proposal.Balance.Runs,
        plan.Command.Proposal.Balance.Passed,
        plan.Command.Proposal.EstimatedCostUsd,
        [],
        plan.Applied,
        plan.Reverted,
        plan.Audit.ToArray());

    private static AssistedAbilityApplyPlanResult AssistedAbilityFailure(
        long revision,
        string code,
        string message) => new(
        false,
        null,
        null,
        null,
        null,
        revision,
        null,
        null,
        "no-change",
        [],
        [],
        [],
        0,
        false,
        0,
        0,
        [new AgentDiagnostic(code, message, "assistant/ability")],
        false,
        false,
        []);

    private static AssistedAbilityApplyPlanResult WithFailure(
        StoredAssistedAbilityPlan plan,
        IReadOnlyList<AgentDiagnostic> diagnostics) => ToResult(plan) with
        {
            Succeeded = false,
            Diagnostics = diagnostics,
        };

    private static AssistedAbilityApplyPlanResult ToResult(StoredAssistedAbilityPlan plan) => new(
        true,
        plan.PlanId,
        plan.Command.TransactionId.ToString("D"),
        plan.Command.Proposal.Orchestration.Id.ToString("D"),
        plan.Command.Proposal.Orchestration.OwnerBlueprintId.ToString(),
        plan.Command.BaseRevision.Value,
        plan.Diff,
        plan.Inverse,
        plan.Command.Proposal.SemanticDiff,
        plan.Command.Proposal.Citations,
        plan.Command.Proposal.Diagnostics.Select(item => item.Code).ToArray(),
        plan.Command.Proposal.Tests.Select(item => item.Kind.ToString()).ToArray(),
        plan.Command.Proposal.Tests.Count(item => item.Passed),
        plan.Command.Proposal.Balance.Passed,
        plan.Command.Proposal.Balance.PowerScore,
        plan.Command.Proposal.EstimatedCostUsd,
        [],
        plan.Applied,
        plan.Reverted,
        plan.Audit.ToArray());

    private static AssistedBlenderCreationApplyPlanResult AssistedBlenderFailure(
        long revision,
        string code,
        string message) => new(
        false, null, null, null, null, revision, [], "no-change", null, null, [], [], [], 0, 0,
        [new AgentDiagnostic(code, message, "assistant/blender")], false, false, []);

    private static AssistedBlenderCreationApplyPlanResult WithFailure(
        StoredAssistedBlenderCreationPlan plan,
        IReadOnlyList<AgentDiagnostic> diagnostics) => ToResult(plan) with
        {
            Succeeded = false,
            Diagnostics = diagnostics,
        };

    private static AssistedBlenderCreationApplyPlanResult ToResult(StoredAssistedBlenderCreationPlan plan) => new(
        true,
        plan.PlanId,
        plan.Command.TransactionId.ToString("D"),
        plan.Command.Proposal.Definition.Id.ToString("D"),
        plan.Command.Proposal.Configuration.AssetId.ToString(),
        plan.Command.BaseRevision.Value,
        plan.FileDiff,
        plan.Command.Proposal.SemanticDiff,
        plan.Command.Proposal.RecipeRelativePath,
        plan.Command.Proposal.Definition.OutputRelativePath,
        plan.Command.Proposal.Citations,
        plan.Command.Proposal.Diagnostics,
        plan.Command.Proposal.Tests.Select(item => item.Id).ToArray(),
        plan.Command.Proposal.Tests.Count(item => item.Passed),
        plan.Command.Proposal.EstimatedCostUsd,
        [],
        plan.Applied,
        plan.Reverted,
        plan.Audit.ToArray());

    private static AssistedModel3DGenerationApplyPlanResult AssistedModel3DFailure(
        long revision,
        string code,
        string message) => new(
        false, null, null, null, null, revision, [], "no-change", null, null, null, [], [], [], 0, 0,
        null, null, null, [new AgentDiagnostic(code, message, "assistant/model3d")], false, false, []);

    private static AssistedModel3DGenerationApplyPlanResult WithFailure(
        StoredAssistedModel3DGenerationPlan plan,
        IReadOnlyList<AgentDiagnostic> diagnostics) => ToResult(plan) with
        {
            Succeeded = false,
            Diagnostics = diagnostics,
        };

    private static AssistedModel3DGenerationApplyPlanResult ToResult(StoredAssistedModel3DGenerationPlan plan) => new(
        true,
        plan.PlanId,
        plan.Command.TransactionId.ToString("D"),
        plan.Command.Proposal.Receipt.Id.ToString("D"),
        plan.Command.Proposal.Configuration.AssetId.ToString(),
        plan.Command.BaseRevision.Value,
        plan.FileDiff,
        plan.Command.Proposal.SemanticDiff,
        plan.Command.Proposal.Receipt.SourceRelativePath,
        plan.Command.Proposal.ReceiptRelativePath,
        plan.Command.Proposal.Receipt.OutputRelativePath,
        plan.Command.Proposal.Citations,
        plan.Command.Proposal.Diagnostics,
        plan.Command.Proposal.Tests.Select(item => item.Id).ToArray(),
        plan.Command.Proposal.Tests.Count(item => item.Passed),
        plan.Command.Proposal.EstimatedCostUsd,
        plan.Command.Proposal.Receipt.Provider.Key,
        plan.Command.Proposal.Receipt.LicenseSpdx,
        plan.Command.Proposal.Receipt.ProviderProvenance,
        [],
        plan.Applied,
        plan.Reverted,
        plan.Audit.ToArray());

    private static ExternalWorkerSecurityPlanResult ExternalWorkerSecurityFailure(
        long revision,
        string code,
        string message) =>
        new(false, null, null, revision, null, null, null, 0,
            [new AgentDiagnostic(code, message, "assets/worker-security")], false, false, []);

    private static ExternalWorkerSecurityPlanResult ToResult(StoredExternalWorkerSecurityPlan plan) =>
        new(true, plan.PlanId, plan.TransactionId, plan.Command.BaseRevision.Value, plan.AppliedRevision,
            plan.Diff, plan.Inverse, 0, [], plan.Applied, plan.Reverted, plan.Audit.ToArray());

    private (ProjectDefinition Project, LoreIndexDefinition LoreIndex, string RootDirectory, long AssetRevision)
        CaptureAssistedProposalSnapshot()
    {
        lock (_gate)
        {
            return (_history.State.Project, _loreIndexHistory.State.Index, _session.RootDirectory,
                _assistedBlenderState.Revision.Value);
        }
    }

    private static IReadOnlySet<string> AvailableForgeScripts(string rootDirectory)
    {
        var root = Path.Combine(rootDirectory, "scripts");
        return Directory.Exists(root)
            ? Directory.GetFiles(root, "*.forge", SearchOption.TopDirectoryOnly)
                .Select(path => Path.GetRelativePath(rootDirectory, path).Replace('\\', '/'))
                .ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
    }

    private static AssistedForgeScriptApplyPlanResult AssistedForgeScriptFailure(
        long revision,
        string code,
        string message) => new(
        false,
        null,
        null,
        null,
        null,
        revision,
        null,
        "no-change",
        string.Empty,
        string.Empty,
        [],
        [new AgentDiagnostic(code, message, "assistant/script")],
        0,
        false,
        false,
        []);

    private static AssistedForgeScriptApplyPlanResult WithFailure(
        StoredAssistedForgeScriptPlan plan,
        IReadOnlyList<AgentDiagnostic> diagnostics) => ToResult(plan) with
        {
            Succeeded = false,
            Diagnostics = diagnostics,
        };

    private static AssistedForgeScriptApplyPlanResult ToResult(StoredAssistedForgeScriptPlan plan) => new(
        true,
        plan.PlanId,
        plan.Command.TransactionId.ToString("D"),
        plan.Command.Proposal.ScriptId.ToString("D"),
        plan.Command.Proposal.RelativePath,
        plan.Command.BaseRevision,
        plan.Patch,
        plan.Command.Proposal.SemanticDiff,
        plan.Command.Proposal.ForgeScriptApiVersion,
        plan.Command.Proposal.EventBinding,
        plan.Command.Proposal.Tests.Select(item => item.Id).ToArray(),
        plan.Command.Proposal.Diagnostics.Select(MapScript).ToArray(),
        plan.Command.Proposal.EstimatedCostUsd,
        plan.Applied,
        plan.Reverted,
        plan.Audit.ToArray());

    private static DialoguePlanResult DialogueFailure(long revision, string code, string message) => new(
        false,
        null,
        revision,
        null,
        null,
        [new AgentDiagnostic(code, message)],
        false,
        false,
        []);

    private static LoreIndexPlanResult LoreIndexFailure(long revision, string code, string message) => new(
        false,
        null,
        null,
        revision,
        null,
        null,
        [new AgentDiagnostic(code, message, "assistant/lore-index")],
        false,
        false,
        []);

    private static LoreIndexPlanResult WithFailure(
        StoredLoreIndexPlan plan,
        IReadOnlyList<AgentDiagnostic> diagnostics) => new(
        false,
        plan.PlanId,
        plan.Command.TransactionId.ToString("D"),
        plan.Command.BaseRevision.Value,
        plan.Diff,
        plan.Inverse,
        diagnostics,
        plan.Applied,
        plan.Reverted,
        plan.Audit.ToArray());

    private static LoreIndexPlanResult ToResult(StoredLoreIndexPlan plan) => new(
        true,
        plan.PlanId,
        plan.Command.TransactionId.ToString("D"),
        plan.Command.BaseRevision.Value,
        plan.Diff,
        plan.Inverse,
        [],
        plan.Applied,
        plan.Reverted,
        plan.Audit.ToArray());

    private static AssistedAreaPlanPlanResult AssistedAreaPlanFailure(
        long revision,
        string code,
        string message) => new(
        false,
        null,
        null,
        revision,
        null,
        null,
        [new AgentDiagnostic(code, message, "assistant/area-plans")],
        false,
        false,
        []);

    private static AssistedAreaPlanPlanResult WithFailure(
        StoredAssistedAreaPlan plan,
        IReadOnlyList<AgentDiagnostic> diagnostics) => new(
        false,
        plan.PlanId,
        plan.Command.TransactionId.ToString("D"),
        plan.Command.BaseRevision.Value,
        plan.Diff,
        plan.Inverse,
        diagnostics,
        plan.Applied,
        plan.Reverted,
        plan.Audit.ToArray());

    private static AssistedAreaPlanPlanResult ToResult(StoredAssistedAreaPlan plan) => new(
        true,
        plan.PlanId,
        plan.Command.TransactionId.ToString("D"),
        plan.Command.BaseRevision.Value,
        plan.Diff,
        plan.Inverse,
        [],
        plan.Applied,
        plan.Reverted,
        plan.Audit.ToArray());

    private static AssistedAreaBuildPlanResult AssistedAreaBuildFailure(
        long revision,
        string code,
        string message) => new(
        false,
        null,
        null,
        null,
        revision,
        null,
        null,
        [],
        "no-change",
        0,
        0,
        false,
        [new AgentDiagnostic(code, message, "assistant/area-build")],
        false,
        false,
        []);

    private static AssistedAreaBuildPlanResult WithFailure(
        StoredAssistedAreaBuildPlan plan,
        IReadOnlyList<AgentDiagnostic> diagnostics) => ToResult(plan) with
        {
            Succeeded = false,
            Diagnostics = diagnostics,
        };

    private static AssistedAreaBuildPlanResult ToResult(StoredAssistedAreaBuildPlan plan) => new(
        true,
        plan.PlanId,
        plan.Command.TransactionId.ToString("D"),
        plan.Command.AssistedAreaPlanId.ToString("D"),
        plan.Command.BaseRevision.Value,
        plan.Diff,
        plan.Inverse,
        plan.EntityIds,
        plan.SemanticDiff,
        plan.NavigationDiagnosticsBefore,
        plan.NavigationDiagnosticsAfter,
        plan.LightingPass,
        [],
        plan.Applied,
        plan.Reverted,
        plan.Audit.ToArray());

    private static DialoguePlanResult WithFailure(
        StoredDialoguePlan plan,
        IReadOnlyList<AgentDiagnostic> diagnostics) => new(
        false,
        plan.PlanId,
        plan.BaseRevision,
        plan.Diff,
        plan.Inverse,
        diagnostics,
        plan.Applied,
        plan.Reverted,
        plan.Audit.ToArray());

    private static DialoguePlanResult ToResult(StoredDialoguePlan plan) => new(
        true,
        plan.PlanId,
        plan.BaseRevision,
        plan.Diff,
        plan.Inverse,
        [],
        plan.Applied,
        plan.Reverted,
        plan.Audit.ToArray());

    private static ScriptBindingPlanResult BindingFailure(long revision, string code, string message) => new(
        false,
        null,
        revision,
        null,
        null,
        [new AgentDiagnostic(code, message)]);

    private static RuntimePolicyPlanResult RuntimePolicyFailure(long revision, string code, string message) => new(
        false,
        null,
        revision,
        null,
        null,
        [new AgentDiagnostic(code, message)],
        false,
        false,
        []);

    private static RuntimePolicyPlanResult ToResult(StoredRuntimePolicyPlan plan) => new(
        true,
        plan.PlanId,
        plan.Command.BaseRevision.Value,
        plan.Diff,
        plan.Inverse,
        [],
        plan.Applied,
        plan.Reverted,
        plan.Audit.ToArray());

    private static CinematicPlanResult CinematicFailure(long revision, string code, string message) => new(
        false, null, revision, null, null, [new AgentDiagnostic(code, message)], false, false, []);

    private static CinematicPlanResult ToResult(StoredCinematicPlan plan) => new(true, plan.PlanId,
        plan.Command.BaseRevision.Value, plan.Diff, plan.Inverse, [], plan.Applied, plan.Reverted, plan.Audit.ToArray());

    private static GameplayControlPlanResult GameplayControlFailure(long revision, string code, string message) =>
        new(false, null, revision, null, null, [new AgentDiagnostic(code, message)], false, false, []);

    private static GameplayControlPlanResult ToResult(StoredGameplayControlPlan plan) => new(true, plan.PlanId,
        plan.Command.BaseRevision.Value, plan.Diff, plan.Inverse, [], plan.Applied, plan.Reverted,
        plan.Audit.ToArray());

    private static RulesProfilePlanResult RulesProfileFailure(long revision, string code, string message) =>
        new(false, null, revision, null, null, [new AgentDiagnostic(code, message)], false, false, []);

    private static RulesProfilePlanResult ToResult(StoredRulesProfilePlan plan) => new(true, plan.PlanId,
        plan.Command.BaseRevision.Value, plan.Diff, plan.Inverse, [], plan.Applied, plan.Reverted,
        plan.Audit.ToArray());

    private static AreaExperiencePlanResult AreaExperienceFailure(long revision, string code, string message) =>
        new(false, null, revision, null, null, [new AgentDiagnostic(code, message)], false, false, []);

    private static AreaExperiencePlanResult ToResult(StoredAreaExperiencePlan plan) => new(true, plan.PlanId,
        plan.Command.BaseRevision.Value, plan.Diff, plan.Inverse, [], plan.Applied, plan.Reverted,
        plan.Audit.ToArray());

    private static AudioEmitterPlanResult AudioEmitterFailure(long revision, string code, string message) =>
        new(false, null, revision, null, null, [new AgentDiagnostic(code, message)], false, false, []);

    private static AudioEmitterPlanResult ToResult(StoredAudioEmitterPlan plan) => new(true, plan.PlanId,
        plan.Command.BaseRevision.Value, plan.Diff, plan.Inverse, [], plan.Applied, plan.Reverted,
        plan.Audit.ToArray());

    private static AreaEnvironmentProfile WithAudioEmitters(
        AreaEnvironmentProfile source,
        AudioEmitterCatalog catalog)
    {
        var audio = source.Audio;
        var updatedAudio = new AreaAudioProfile(audio.DayMusic, audio.NightMusic, audio.CombatMusic,
            audio.Ambience, audio.ReverbPreset, audio.TransitionSeconds, audio.Playlist, audio.OrderMode,
            audio.IntervalSeconds, audio.IntervalVariationSeconds, audio.RelativeVolume, audio.Spatial,
            audio.RuntimeControl, audio.AdaptiveLayers, audio.Orchestration, catalog, audio.Authoring);
        return new AreaEnvironmentProfile(source.SchemaVersion, source.Classification, source.Tags,
            source.Lighting, source.Fog, updatedAudio, source.Rendering, source.Sky, source.Wind,
            source.Weather, source.Water, source.Reverb);
    }

    private static RuntimeExtensionPlanResult RuntimeExtensionFailure(long revision, string code, string message) =>
        new(false, null, revision, null, null, [new AgentDiagnostic(code, message)], false, false, []);

    private static PostLaunchPreviewResult PreviewMobility(PostLaunchCapabilityProfile profile,
        IReadOnlyDictionary<string, string> arguments)
    {
        var result = PostLaunchCapabilityResolver.ResolveMobility(profile.Gameplay,
            bool.Parse(arguments.GetValueOrDefault("insideZeroGravity", "false")),
            float.Parse(arguments.GetValueOrDefault("inputMagnitude", "0"), System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(arguments.GetValueOrDefault("requestedSeats", "0"), System.Globalization.CultureInfo.InvariantCulture),
            bool.Parse(arguments.GetValueOrDefault("requestMount", "false")));
        return new(true, result.Diagnostic,
            $"zero-g={result.ZeroGravityActive};acceleration={result.AppliedAcceleration.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)};vehicle={result.VehicleAccepted};mount={result.MountAccepted}", []);
    }

    private static PostLaunchPreviewResult PreviewProtocol(PostLaunchCapabilityProfile profile,
        IReadOnlyDictionary<string, string> arguments)
    {
        var capabilities = arguments.GetValueOrDefault("capabilities", string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = PostLaunchCapabilityResolver.NegotiateProtocol(profile.Network,
            arguments.GetValueOrDefault("platform", "windows"),
            int.Parse(arguments.GetValueOrDefault("minimumVersion", "1"), System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(arguments.GetValueOrDefault("maximumVersion", "1"), System.Globalization.CultureInfo.InvariantCulture),
            capabilities, bool.Parse(arguments.GetValueOrDefault("signedExtension", "false")));
        return new(result.Compatible, result.Diagnostic,
            $"compatible={result.Compatible};version={result.NegotiatedVersion};extension={result.ExtensionAllowed};capabilities={string.Join(',', result.GrantedCapabilities)}",
            result.Compatible ? [] : [new AgentDiagnostic("post_launch.protocol_incompatible", result.Diagnostic)]);
    }

    private static PostLaunchPreviewResult PreviewProduction(PostLaunchCapabilityProfile profile,
        IReadOnlyDictionary<string, string> arguments, string kind)
    {
        var result = PostLaunchCapabilityResolver.PlanProduction(profile.Production, kind,
            int.Parse(arguments.GetValueOrDefault("workUnits", "1"), System.Globalization.CultureInfo.InvariantCulture),
            long.Parse(arguments.GetValueOrDefault("memoryGrowthMegabytes", "0"), System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(arguments.GetValueOrDefault("handleGrowth", "0"), System.Globalization.CultureInfo.InvariantCulture),
            double.Parse(arguments.GetValueOrDefault("p95LatencyMilliseconds", "0"), System.Globalization.CultureInfo.InvariantCulture));
        return new(result.Accepted, result.Diagnostic,
            $"kind={result.Kind};accepted={result.Accepted};work-units={result.WorkUnits};review={result.RequiresReview}",
            result.Accepted ? [] : [new AgentDiagnostic("post_launch.production_denied", result.Diagnostic)]);
    }

    private static PostLaunchPreviewResult PostLaunchFailure(string code, string message) =>
        new(false, code, "no changes", [new AgentDiagnostic(code, message)]);

    private static RuntimeExtensionPlanResult ToResult(StoredRuntimeExtensionPlan plan) => new(true, plan.PlanId,
        plan.Command.BaseRevision.Value, plan.Diff, plan.Inverse, [], plan.Applied, plan.Reverted,
        plan.Audit.ToArray());

    private sealed class StoredAssistedDialoguePlan(
        string planId,
        ApplyAssistedDialogueCommand command,
        ProjectPatch diff,
        ProjectPatch inverse,
        List<PlanAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public ApplyAssistedDialogueCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<PlanAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredAssistedQuestPlan(
        string planId,
        ApplyAssistedQuestCommand command,
        ProjectPatch diff,
        ProjectPatch inverse,
        List<PlanAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public ApplyAssistedQuestCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<PlanAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredAssistedBossPlan(
        string planId,
        ApplyAssistedBossCommand command,
        ProjectPatch diff,
        ProjectPatch inverse,
        List<PlanAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public ApplyAssistedBossCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<PlanAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredAssistedAbilityPlan(
        string planId,
        ApplyAssistedAbilityCommand command,
        ProjectPatch diff,
        ProjectPatch inverse,
        List<PlanAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public ApplyAssistedAbilityCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<PlanAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredAssistedBlenderCreationPlan(
        string planId,
        ApplyAssistedBlenderCreationCommand command,
        IReadOnlyList<AssistedBlenderFilePatch> fileDiff,
        IReadOnlyList<AssetExternalEditConfiguration> beforeConfigurations,
        List<PlanAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public ApplyAssistedBlenderCreationCommand Command { get; } = command;
        public IReadOnlyList<AssistedBlenderFilePatch> FileDiff { get; set; } = fileDiff;
        public IReadOnlyList<AssetExternalEditConfiguration> BeforeConfigurations { get; } = beforeConfigurations;
        public List<PlanAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredAssistedModel3DGenerationPlan(
        string planId,
        ApplyAssistedModel3DGenerationCommand command,
        IReadOnlyList<AssistedModel3DFilePatch> fileDiff,
        IReadOnlyList<AssetExternalEditConfiguration> beforeConfigurations,
        List<PlanAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public ApplyAssistedModel3DGenerationCommand Command { get; } = command;
        public IReadOnlyList<AssistedModel3DFilePatch> FileDiff { get; set; } = fileDiff;
        public IReadOnlyList<AssetExternalEditConfiguration> BeforeConfigurations { get; } = beforeConfigurations;
        public List<PlanAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredExternalWorkerSecurityPlan(
        string planId,
        string transactionId,
        SetExternalWorkerSecurityPolicyCommand command,
        ProjectPatch diff,
        ProjectPatch inverse,
        List<PlanAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public string TransactionId { get; } = transactionId;
        public SetExternalWorkerSecurityPolicyCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<PlanAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredAssistedForgeScriptPlan(
        string planId,
        ApplyAssistedForgeScriptCommand command,
        AssistedForgeScriptFilePatch patch,
        List<PlanAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public ApplyAssistedForgeScriptCommand Command { get; } = command;
        public AssistedForgeScriptFilePatch Patch { get; } = patch;
        public List<PlanAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
    }

    private sealed class StoredDialoguePlan(
        string planId,
        DialogueDefinition dialogue,
        long baseRevision,
        ProjectPatch diff,
        ProjectPatch inverse,
        List<PlanAuditEntry> audit)
    {
        public string PlanId { get; } = planId;

        public DialogueDefinition Dialogue { get; } = dialogue;

        public long BaseRevision { get; } = baseRevision;

        public ProjectPatch Diff { get; } = diff;

        public ProjectPatch Inverse { get; } = inverse;

        public List<PlanAuditEntry> Audit { get; } = audit;

        public bool Applied { get; set; }

        public bool Reverted { get; set; }

        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredLoreIndexPlan(
        string planId,
        SetLoreIndexCommand command,
        ProjectPatch diff,
        ProjectPatch inverse,
        List<LoreIndexAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public SetLoreIndexCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<LoreIndexAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredAssistedAreaPlan(
        string planId,
        SetAssistedAreaPlansCommand command,
        ProjectPatch diff,
        ProjectPatch inverse,
        List<AssistedAreaPlanAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public SetAssistedAreaPlansCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<AssistedAreaPlanAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredAssistedAreaBuildPlan(
        string planId,
        BuildAssistedAreaPlanCommand command,
        ProjectPatch diff,
        ProjectPatch inverse,
        IReadOnlyList<string> entityIds,
        string semanticDiff,
        int navigationDiagnosticsBefore,
        int navigationDiagnosticsAfter,
        bool lightingPass,
        List<AssistedAreaPlanAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public BuildAssistedAreaPlanCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public IReadOnlyList<string> EntityIds { get; } = entityIds;
        public string SemanticDiff { get; } = semanticDiff;
        public int NavigationDiagnosticsBefore { get; } = navigationDiagnosticsBefore;
        public int NavigationDiagnosticsAfter { get; } = navigationDiagnosticsAfter;
        public bool LightingPass { get; } = lightingPass;
        public List<AssistedAreaPlanAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredAssetInstallPlan(
        string planId,
        InstallAssetBlueprintCommand command,
        ProjectPatch diff,
        ProjectPatch inverse,
        List<AgentAssetInstallAuditEntry> audit,
        string vaultRoot,
        LocalAssetPackageInstallPlan? localPackage,
        IReadOnlyList<AssetPackageManifest> previousManifests,
        bool manifestFileMissing)
    {
        public string PlanId { get; } = planId;
        public InstallAssetBlueprintCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<AgentAssetInstallAuditEntry> Audit { get; } = audit;
        public string VaultRoot { get; } = vaultRoot;
        public LocalAssetPackageInstallPlan? LocalPackage { get; } = localPackage;
        public IReadOnlyList<AssetPackageManifest> PreviousManifests { get; } = previousManifests;
        public bool ManifestFileMissing { get; } = manifestFileMissing;
        public LocalAssetPackageApplyResult? PackageExecution { get; set; }
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredBlueprintUpdatePlan(
        string planId,
        UpdateBlueprintFromEntityCommand command,
        BlueprintUpdatePlan plan,
        ProjectPatch diff,
        ProjectPatch inverse,
        List<BlueprintUpdateAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public UpdateBlueprintFromEntityCommand Command { get; } = command;
        public BlueprintUpdatePlan Plan { get; } = plan;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<BlueprintUpdateAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredBlueprintPrefabPlan(
        string planId,
        ConfigureBlueprintPrefabCommand command,
        BlueprintPrefabResourceItem preview,
        ProjectPatch diff,
        ProjectPatch inverse,
        List<BlueprintPrefabAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public ConfigureBlueprintPrefabCommand Command { get; } = command;
        public BlueprintPrefabResourceItem Preview { get; } = preview;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<BlueprintPrefabAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredEditorProductivityPlan(string planId, long baseRevision,
        EditorWorkspaceProfile original, EditorWorkspaceProfile candidate, ProjectPatch diff, ProjectPatch inverse,
        List<EditorProductivityAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public long BaseRevision { get; } = baseRevision;
        public EditorWorkspaceProfile Original { get; } = original;
        public EditorWorkspaceProfile Candidate { get; } = candidate;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<EditorProductivityAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
    }

    private sealed class StoredRuntimePolicyPlan(
        string planId,
        SetRuntimePoliciesCommand command,
        ProjectPatch diff,
        ProjectPatch inverse,
        List<RuntimePolicyAuditEntry> audit)
    {
        public string PlanId { get; } = planId;

        public SetRuntimePoliciesCommand Command { get; } = command;

        public ProjectPatch Diff { get; } = diff;

        public ProjectPatch Inverse { get; } = inverse;

        public List<RuntimePolicyAuditEntry> Audit { get; } = audit;

        public bool Applied { get; set; }

        public bool Reverted { get; set; }

        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredCinematicPlan(string planId, SetCinematicProfilesCommand command,
        ProjectPatch diff, ProjectPatch inverse, List<CinematicAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public SetCinematicProfilesCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<CinematicAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredGameplayControlPlan(string planId, SetGameplayControlProfileCommand command,
        ProjectPatch diff, ProjectPatch inverse, List<GameplayControlAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public SetGameplayControlProfileCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<GameplayControlAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredProjectVariablePlan(string planId, SetModuleConfigurationCommand command,
        ProjectPatch diff, ProjectPatch inverse, List<ProjectVariableAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public SetModuleConfigurationCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<ProjectVariableAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredProjectDocumentationPlan(string planId, SetModuleConfigurationCommand command,
        ProjectPatch diff, ProjectPatch inverse, List<ProjectDocumentationAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public SetModuleConfigurationCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<ProjectDocumentationAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredProjectMetadataPatchPlan(string planId, PatchModuleMetadataCommand command,
        ProjectPatch diff, ProjectPatch inverse, IReadOnlyList<string> changedPaths,
        IReadOnlyList<ModuleMetadataLocalizationReview> localizationReview,
        List<ProjectFoundationAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public PatchModuleMetadataCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public IReadOnlyList<string> ChangedPaths { get; } = changedPaths;
        public IReadOnlyList<ModuleMetadataLocalizationReview> LocalizationReview { get; } = localizationReview;
        public List<ProjectFoundationAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredProjectFoundationPlan(string planId, SetProjectFoundationCommand command,
        ProjectPatch diff, ProjectPatch inverse, List<ProjectFoundationAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public SetProjectFoundationCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<ProjectFoundationAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredAreaFoundationPlan(string planId, SetAreaFoundationCommand command,
        ProjectPatch diff, ProjectPatch inverse, List<AreaFoundationAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public SetAreaFoundationCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<AreaFoundationAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredAreaBiomePlan(string planId, PatchAreaBiomesCommand command,
        ProjectPatch diff, ProjectPatch inverse, List<AreaBiomeAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public PatchAreaBiomesCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<AreaBiomeAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredAreaEnvironmentPatchPlan(string planId, PatchAreaEnvironmentCommand command,
        ProjectPatch diff, ProjectPatch inverse, IReadOnlyList<string> changedPaths,
        List<AreaEnvironmentAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public PatchAreaEnvironmentCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public IReadOnlyList<string> ChangedPaths { get; } = changedPaths;
        public List<AreaEnvironmentAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredAreaEnvironmentPlan(string planId, SetAreaEnvironmentCommand command,
        ProjectPatch diff, ProjectPatch inverse, List<AreaEnvironmentAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public SetAreaEnvironmentCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<AreaEnvironmentAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredAreaExperienceAuthoringPlan(string planId, SetAreaAuthoringCommand command,
        ProjectPatch diff, ProjectPatch inverse, List<AreaExperienceAuthoringAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public SetAreaAuthoringCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<AreaExperienceAuthoringAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredAreaPlaytestPlan(string planId, SetAreaPlaytestCommand command,
        ProjectPatch diff, ProjectPatch inverse, List<AreaPlaytestAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public SetAreaPlaytestCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<AreaPlaytestAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredEntityScalePlan(string planId, ScaleEntitiesCommand command,
        ProjectPatch diff, ProjectPatch inverse, List<EntityScaleAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public ScaleEntitiesCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<EntityScaleAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredEntityTranslationPlan(
        string planId,
        TranslateEntitiesCommand command,
        ProjectPatch diff,
        ProjectPatch inverse,
        List<EntityTranslationAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public TranslateEntitiesCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<EntityTranslationAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredWaypointPlan(
        string planId,
        SetNavigationTopologyCommand command,
        ProjectPatch diff,
        ProjectPatch inverse,
        List<WaypointPlanAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public SetNavigationTopologyCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<WaypointPlanAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
        public long? RevertedRevision { get; set; }
    }

    private sealed class StoredRulesProfilePlan(string planId, SetRulesProfilesCommand command,
        ProjectPatch diff, ProjectPatch inverse, List<RulesProfileAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public SetRulesProfilesCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<RulesProfileAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredAreaExperiencePlan(string planId, SetAreaExperienceProfileCommand command,
        ProjectPatch diff, ProjectPatch inverse, List<AreaExperienceAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public SetAreaExperienceProfileCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<AreaExperienceAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredAudioEmitterPlan(string planId, SetAreaEnvironmentCommand command,
        ProjectPatch diff, ProjectPatch inverse, List<AudioEmitterAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public SetAreaEnvironmentCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<AudioEmitterAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
        public Guid? AppliedCommandId { get; set; }
    }

    private sealed class StoredRuntimeExtensionPlan(string planId, SetRuntimeExtensionsCommand command,
        ProjectPatch diff, ProjectPatch inverse, List<RuntimeExtensionAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public SetRuntimeExtensionsCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<RuntimeExtensionAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredAgentChangeTransaction(
        AgentChangeTransactionBeginRequest request,
        string providerKey,
        Guid rollbackId,
        string beforeHash)
    {
        public AgentChangeTransactionBeginRequest Request { get; } = request;
        public string ProviderKey { get; } = providerKey;
        public Guid RollbackId { get; } = rollbackId;
        public string BeforeHash { get; } = beforeHash;
        public AgentTransactionStage Stage { get; set; } = AgentTransactionStage.Begun;
        public SetModuleConfigurationCommand? Command { get; set; }
        public PlaceEntityCommand? PlaceCommand { get; set; }
        public AgentSemanticDiff? SemanticDiff { get; set; }
        public long? AppliedRevision { get; set; }
        public List<AgentAiAuditEntry> Audit { get; } = [];
    }

    private sealed class StoredProjectCompatibilityPlan(
        string planId, long baseRevision, WorkspaceBuildProfile profile, IReadOnlyList<string> diff)
    {
        public string PlanId { get; } = planId;
        public long BaseRevision { get; } = baseRevision;
        public WorkspaceBuildProfile Profile { get; } = profile;
        public IReadOnlyList<string> Diff { get; } = diff;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed record AgentTransactionPreparation(
        bool Succeeded,
        SetModuleConfigurationCommand? Command,
        AgentSemanticDiff? SemanticDiff,
        IReadOnlyList<AgentDiagnostic> Diagnostics,
        PlaceEntityCommand? PlaceCommand = null);
}

public sealed record ForgeProjectAgentOpenResult(
    ForgeProjectAgentService? Service,
    IReadOnlyList<AgentDiagnostic> Diagnostics);
