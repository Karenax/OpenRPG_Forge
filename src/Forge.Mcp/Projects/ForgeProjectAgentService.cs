using Forge.Application.Commands;
using Forge.Application.Assistant;
using Forge.Application.Assets;
using Forge.Application.Dialogues;
using Forge.Application.Entities;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Application.Validation;
using Forge.Domain.Areas;
using Forge.Domain.Assistant;
using Forge.Domain.Assets;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Rules;
using Forge.Domain.Sequences;
using Forge.Domain.Validation;
using Forge.Mcp.Dialogues;
using Forge.Mcp.Assets;
using Forge.Mcp.Scripts;
using Forge.ModuleFormat.Workspaces;
using Forge.ModuleFormat.Quality;
using Forge.Scripting;
using Forge.ModuleFormat.Assets;
using Forge.ModuleFormat.Documentation;

namespace Forge.Mcp.Projects;

public sealed class ForgeProjectAgentService
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
    private readonly SetEntityOverridesHandler _overridesHandler;
    private readonly SetRuntimePoliciesHandler _runtimePoliciesHandler;
    private readonly SetCinematicProfilesHandler _cinematicHandler;
    private readonly SetGameplayControlProfileHandler _gameplayControlHandler;
    private readonly SetRulesProfilesHandler _rulesProfilesHandler;
    private readonly SetAreaExperienceProfileHandler _areaExperienceHandler;
    private readonly SetRuntimeExtensionsHandler _runtimeExtensionsHandler;
    private readonly InstallAssetBlueprintHandler _assetInstallHandler;
    private readonly SetLoreIndexHandler _loreIndexHandler;
    private readonly LoreIndexCommandHistory _loreIndexHistory;
    private readonly List<AgentDiagnostic> _loreLoadDiagnostics;
    private readonly SetAssistedAreaPlansHandler _areaPlanHandler;
    private readonly AssistedAreaPlanCommandHistory _areaPlanHistory;
    private readonly List<AgentDiagnostic> _areaPlanLoadDiagnostics;
    private readonly Dictionary<string, StoredDialoguePlan> _dialoguePlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredRuntimePolicyPlan> _runtimePolicyPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredCinematicPlan> _cinematicPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredGameplayControlPlan> _gameplayControlPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredRulesProfilePlan> _rulesProfilePlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredAreaExperiencePlan> _areaExperiencePlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredRuntimeExtensionPlan> _runtimeExtensionPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredAssetInstallPlan> _assetInstallPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredLoreIndexPlan> _loreIndexPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredAssistedAreaPlan> _areaPlans = new(StringComparer.Ordinal);
    private readonly IClock _clock;

    private ForgeProjectAgentService(ProjectSession session, IClock clock)
    {
        _session = session;
        _clock = clock;
        _history = new ProjectCommandHistory(new ProjectState(session.Project, new ProjectRevision(0)));
        _dialogueHandler = new SetDialogueHandler(clock);
        _overridesHandler = new SetEntityOverridesHandler(clock);
        _runtimePoliciesHandler = new SetRuntimePoliciesHandler(clock);
        _cinematicHandler = new SetCinematicProfilesHandler(clock);
        _gameplayControlHandler = new SetGameplayControlProfileHandler(clock);
        _rulesProfilesHandler = new SetRulesProfilesHandler(clock);
        _areaExperienceHandler = new SetAreaExperienceProfileHandler(clock);
        _runtimeExtensionsHandler = new SetRuntimeExtensionsHandler(clock);
        _assetInstallHandler = new InstallAssetBlueprintHandler(clock);
        _loreIndexHandler = new SetLoreIndexHandler(clock);
        var loreIndex = LoreIndexStore.Load(session.RootDirectory);
        _loreIndexHistory = new LoreIndexCommandHistory(loreIndex.Index);
        _loreLoadDiagnostics = loreIndex.Diagnostics.Select(item =>
            new AgentDiagnostic(item.Code, item.Message, item.Path)).ToList();
        _areaPlanHandler = new SetAssistedAreaPlansHandler(clock);
        var areaPlans = AssistedAreaPlanStore.Load(session.RootDirectory);
        _areaPlanHistory = new AssistedAreaPlanCommandHistory(areaPlans.Catalog);
        _areaPlanLoadDiagnostics = areaPlans.Diagnostics.Select(item =>
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
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return new(false, null, "no-change", string.Empty, 0, 0, 0, false,
                    [new AgentDiagnostic("assistant.area_plan.request_invalid", "Area-plan proposal schema is invalid.", "assistant/area-plans")]);
            var profile = AssistantWorkspaceProfile.CreateDefault();
            var proposal = AssistedAreaPlanProposalService.ProposeAsync(
                request.Brief,
                _history.State.Project,
                _loreIndexHistory.State.Index,
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

    public IReadOnlyList<AgentDiagnostic> GetDiagnostics()
    {
        lock (_gate)
        {
            return ProjectDiagnosticCatalog.Build(_history.State.Project)
                .Select(Map)
                .ToArray();
        }
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

    public RuntimeExtensionResource GetRuntimeExtensions()
    {
        lock (_gate)
            return new RuntimeExtensionResource(1, _history.State.Revision.Value,
                _history.State.Project.Module.RuntimePolicies.Extensions);
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

    public AgentAssetCatalogResult GetAssets(AgentAssetCatalogQuery? query = null)
    {
        query ??= new AgentAssetCatalogQuery();
        lock (_gate)
        {
            var repositoryRoot = AssetCatalogLoader.FindRepositoryRoot(_session.RootDirectory) ??
                AssetCatalogLoader.FindRepositoryRoot(AppContext.BaseDirectory);
            if (repositoryRoot is null)
            {
                return new AgentAssetCatalogResult(
                    1,
                    0,
                    0,
                    [],
                    [],
                    [],
                    [new AgentDiagnostic(
                        AssetCatalogLoader.CatalogMissingCode,
                        "The OpenRPG Forge Asset Vault catalogs could not be located.",
                        "asset_manifests/catalogs")]);
            }

            var load = AssetCatalogLoader.Load(repositoryRoot);
            if (load.Catalog is null)
            {
                return new AgentAssetCatalogResult(
                    1,
                    0,
                    0,
                    [],
                    [],
                    [],
                    load.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
            }

            var browser = new AssetCatalogBrowser(load.Catalog);
            var items = browser.Search(new AssetCatalogQuery(
                query.Text,
                query.Source,
                query.Category,
                query.LicenseSpdx,
                ParseAssetAvailability(query.Availability),
                query.MaximumResults,
                query.Installed));
            return new AgentAssetCatalogResult(
                load.Catalog.SchemaVersion,
                load.Catalog.Items.Count,
                load.Catalog.Items.Count(item => item.Availability != AssetAvailability.CatalogOnly),
                browser.Sources,
                browser.Categories,
                items.Select(item => new AgentAssetCatalogItem(
                    item.Id.ToString(),
                    item.SchemaVersion,
                    item.ExternalId,
                    item.DisplayName,
                    item.Source,
                    item.SourceAssetId,
                    item.SourcePage,
                    item.LicenseSpdx,
                    item.LicenseVerified,
                    item.ContentSha256,
                    item.TotalBytes,
                    item.Categories,
                    item.Tags,
                    item.CatalogStatus,
                    item.Availability.ToString(),
                    item.RelativeModelPath,
                    item.RelativePreviewPath,
                    item.ThumbnailUrl,
                    item.CanPlanImport,
                    item.Provenance,
                    item.Author,
                    item.AttributionRequired,
                    item.AttributionText,
                    item.Installed,
                    item.InstalledPackIds)).ToArray(),
                load.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
        }
    }

    public AgentAssetInstallPlanResult PlanAssetInstall(AgentAssetInstallPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return AssetInstallFailure(request.BaseRevision, request.AssetId,
                    "asset_install_plan.schema_unsupported", "Asset install plan schema must be 1.");

            AssetCatalogItemId assetId;
            try
            {
                assetId = AssetCatalogItemId.Parse(request.AssetId);
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                return AssetInstallFailure(request.BaseRevision, request.AssetId,
                    "asset_install_plan.asset_invalid", exception.Message);
            }

            var repositoryRoot = AssetCatalogLoader.FindRepositoryRoot(_session.RootDirectory) ??
                AssetCatalogLoader.FindRepositoryRoot(AppContext.BaseDirectory);
            if (repositoryRoot is null)
                return AssetInstallFailure(request.BaseRevision, request.AssetId,
                    AssetCatalogLoader.CatalogMissingCode,
                    "The OpenRPG Forge Asset Vault catalogs could not be located.");
            var catalogLoad = AssetCatalogLoader.Load(repositoryRoot);
            if (catalogLoad.Catalog is null)
            {
                var diagnostic = catalogLoad.Diagnostics.FirstOrDefault();
                return AssetInstallFailure(request.BaseRevision, request.AssetId,
                    diagnostic?.Code ?? AssetCatalogLoader.CatalogInvalidCode,
                    diagnostic?.Message ?? "The Asset Vault catalog is invalid.");
            }

            var item = catalogLoad.Catalog.Items.SingleOrDefault(candidate => candidate.Id == assetId);
            if (item is null)
                return AssetInstallFailure(request.BaseRevision, request.AssetId,
                    "asset_install_plan.asset_missing", $"Catalog asset '{request.AssetId}' does not exist.");
            var manifestLoad = AssetPackageManifestStore.Load(_session.RootDirectory);
            if (!manifestLoad.Succeeded)
            {
                var diagnostic = manifestLoad.Diagnostics[0];
                return AssetInstallFailure(request.BaseRevision, request.AssetId,
                    diagnostic.Code, diagnostic.Message);
            }

            var manifest = manifestLoad.Manifests.SingleOrDefault(candidate => candidate.AssetId == assetId);
            if (manifest is null)
                return AssetInstallFailure(request.BaseRevision, request.AssetId,
                    "asset_install_plan.manifest_missing",
                    $"Catalog asset '{item.ExternalId}' has no governed package manifest in this workspace.");
            var command = new InstallAssetBlueprintCommand(
                CommandId.From(Guid.NewGuid()),
                new ProjectRevision(request.BaseRevision),
                item,
                manifest,
                DryRun: true);
            var preview = _assetInstallHandler.Handle(_history.State, command);
            if (!preview.Succeeded)
                return new AgentAssetInstallPlanResult(
                    false, null, request.BaseRevision, item.Id.ToString(), item.LicenseSpdx,
                    preview.Patch, preview.Inverse, preview.Diagnostics.Select(Map).ToArray(), false, false, []);

            var artifact = GovernedAssetModelVerifier.Verify(_session.RootDirectory, manifest);
            if (!artifact.Succeeded)
                return AssetInstallFailure(request.BaseRevision, item.Id.ToString(),
                    artifact.DiagnosticCode!, artifact.DiagnosticMessage!);

            var planId = Guid.NewGuid().ToString("D");
            var plan = new StoredAssetInstallPlan(
                planId,
                command,
                preview.Patch!,
                preview.Inverse!,
                [AssetInstallAudit(planId, "planned", request.BaseRevision, true, item)]);
            _assetInstallPlans.Add(planId, plan);
            return ToResult(plan);
        }
    }

    public AgentAssetInstallPlanResult ApplyAssetInstallPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_assetInstallPlans.TryGetValue(planId, out var plan))
                return AssetInstallFailure(_history.State.Revision.Value, null,
                    "asset_install_plan.missing", $"Asset install plan '{planId}' does not exist.");
            if (plan.Applied)
                return AssetInstallFailure(plan.Command.BaseRevision.Value, plan.Command.Item.Id.ToString(),
                    "asset_install_plan.already_applied", $"Asset install plan '{planId}' is already applied.");

            var command = plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
            var result = _assetInstallHandler.Handle(_history.State, command);
            if (!result.Succeeded)
            {
                plan.Audit.Add(AssetInstallAudit(planId, "apply", _history.State.Revision.Value, false,
                    plan.Command.Item));
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = result.Diagnostics.Select(Map).ToArray(),
                };
            }

            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
            {
                plan.Audit.Add(AssetInstallAudit(planId, "apply", _history.State.Revision.Value, false,
                    plan.Command.Item));
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
            plan.Audit.Add(AssetInstallAudit(planId, "apply", plan.AppliedRevision.Value, true,
                plan.Command.Item));
            return ToResult(plan);
        }
    }

    public AgentAssetInstallPlanResult RevertAssetInstallPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_assetInstallPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return AssetInstallFailure(_history.State.Revision.Value, null,
                    "asset_install_plan.not_applied", $"Asset install plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _history.State.Revision.Value)
                return AssetInstallFailure(plan.Command.BaseRevision.Value, plan.Command.Item.Id.ToString(),
                    "asset_install_plan.revision_conflict",
                    "Only the most recently applied plan can be reverted without overwriting later work.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return AssetInstallFailure(plan.Command.BaseRevision.Value, plan.Command.Item.Id.ToString(),
                    "asset_install_plan.inverse_unavailable", "The inverse operation is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return AssetInstallFailure(plan.Command.BaseRevision.Value, plan.Command.Item.Id.ToString(),
                    "asset_install_plan.save_failed", "The reverted project could not be saved.");
            }

            plan.Reverted = true;
            plan.Audit.Add(AssetInstallAudit(planId, "revert", undone.State.Revision.Value, true,
                plan.Command.Item));
            return ToResult(plan);
        }
    }

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

    private static DialogueDefinition MapDialogue(DialoguePlanRequest request) => new(
        DialogueId.From(Guid.Parse(request.DialogueId)),
        request.Slug,
        request.DisplayName,
        DialogueNodeId.From(Guid.Parse(request.EntryNodeId)),
        request.Nodes.Select(MapNode).ToArray());

    private static DialogueNode MapNode(DialogueNodeInput node) => new(
        DialogueNodeId.From(Guid.Parse(node.NodeId)),
        string.IsNullOrWhiteSpace(node.SpeakerBlueprintId)
            ? null
            : BlueprintId.From(Guid.Parse(node.SpeakerBlueprintId)),
        new LocalizedText(node.LocalizationKey, node.FallbackText),
        (node.OnEnterActions ?? []).Select(MapAction).ToArray(),
        (node.Choices ?? []).Select(MapChoice).ToArray());

    private static DialogueChoice MapChoice(DialogueChoiceInput choice) => new(
        DialogueChoiceId.From(Guid.Parse(choice.ChoiceId)),
        new LocalizedText(choice.LocalizationKey, choice.FallbackText),
        (choice.Conditions ?? []).Select(MapCondition).ToArray(),
        (choice.Actions ?? []).Select(MapAction).ToArray(),
        string.IsNullOrWhiteSpace(choice.TargetNodeId)
            ? null
            : DialogueNodeId.From(Guid.Parse(choice.TargetNodeId)));

    private static DialogueCondition MapCondition(DialogueConditionInput condition) => new(
        Enum.Parse<DialogueConditionKind>(condition.Kind, ignoreCase: true),
        condition.Key,
        condition.ExpectedValue);

    private static DialogueAction MapAction(DialogueActionInput action) => new(
        Enum.Parse<DialogueActionKind>(action.Kind, ignoreCase: true),
        action.Key,
        action.Value);

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

    private static RuntimeExtensionPlanResult RuntimeExtensionFailure(long revision, string code, string message) =>
        new(false, null, revision, null, null, [new AgentDiagnostic(code, message)], false, false, []);

    private static RuntimeExtensionPlanResult ToResult(StoredRuntimeExtensionPlan plan) => new(true, plan.PlanId,
        plan.Command.BaseRevision.Value, plan.Diff, plan.Inverse, [], plan.Applied, plan.Reverted,
        plan.Audit.ToArray());

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

    private sealed class StoredAssetInstallPlan(
        string planId,
        InstallAssetBlueprintCommand command,
        ProjectPatch diff,
        ProjectPatch inverse,
        List<AgentAssetInstallAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public InstallAssetBlueprintCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<AgentAssetInstallAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
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
}

public sealed record ForgeProjectAgentOpenResult(
    ForgeProjectAgentService? Service,
    IReadOnlyList<AgentDiagnostic> Diagnostics);
