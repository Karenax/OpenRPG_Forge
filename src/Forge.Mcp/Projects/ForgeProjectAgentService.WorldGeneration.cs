using Forge.Application.Commands;
using Forge.Application.WorldGeneration;
using Forge.Domain.Assistant;
using Forge.Domain.WorldGeneration;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Mcp.Projects;

public sealed partial class ForgeProjectAgentService
{
    private WorldGenerationAgentState? _worldGenerationAgentState;

    public WorldGenerationRecipeResource GetWorldGenerationRecipes()
    {
        lock (_gate)
        {
            var state = WorldGenerationState;
            return new(
                1,
                state.History.State.Revision.Value,
                _history.State.Revision.Value,
                state.History.State.Catalog,
                state.LoadDiagnostics.ToArray());
        }
    }

    public WorldGenerationRecipePlanResult PlanWorldGenerationRecipes(WorldGenerationRecipePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            var state = WorldGenerationState;
            if (request.SchemaVersion != 1 || request.BaseRecipeRevision < 0 || request.BaseProjectRevision < 0)
                return RecipeFailure(request.BaseRecipeRevision, request.BaseProjectRevision,
                    "world_generation.request_invalid", "World-generation recipe request schema or revision is invalid.");
            if (request.BaseProjectRevision != _history.State.Revision.Value)
                return RecipeFailure(request.BaseRecipeRevision, request.BaseProjectRevision,
                    "world_generation.project_revision_conflict", "The project changed before the recipe plan was created.");

            var transactionId = Guid.NewGuid();
            var planId = Guid.NewGuid();
            var command = new SetWorldGenerationRecipesCommand(
                CommandId.From(Guid.NewGuid()),
                transactionId,
                planId,
                new WorldGenerationRecipeRevision(request.BaseRecipeRevision),
                request.Catalog,
                request.Permission,
                DryRun: true);
            var preview = state.Handler.Handle(state.History.State, command, _history.State.Project);
            if (!preview.Succeeded || preview.Patch is null || preview.Inverse is null)
                return new(false, null, transactionId.ToString("D"), request.BaseRecipeRevision, null,
                    request.BaseProjectRevision, preview.Patch, preview.Inverse,
                    preview.Diagnostics.Select(ToAgentDiagnostic).ToArray(), false, false, []);

            var key = planId.ToString("D");
            var stored = new StoredWorldGenerationRecipePlan(
                key,
                command,
                preview.Patch,
                preview.Inverse,
                [new(key, transactionId.ToString("D"), "planned", request.BaseRecipeRevision,
                    request.BaseProjectRevision, _clock.UtcNow, true)]);
            state.RecipePlans.Add(key, stored);
            return ToRecipeResult(stored);
        }
    }

    public WorldGenerationRecipePlanResult ApplyWorldGenerationRecipePlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            var state = WorldGenerationState;
            if (!state.RecipePlans.TryGetValue(planId, out var plan))
                return RecipeFailure(state.History.State.Revision.Value, _history.State.Revision.Value,
                    "world_generation.plan_missing", $"World-generation recipe plan '{planId}' does not exist.");
            if (plan.Applied)
                return RecipeFailure(plan.Command.BaseRevision.Value, _history.State.Revision.Value,
                    "world_generation.plan_already_applied", $"World-generation recipe plan '{planId}' is already applied.");
            if (plan.ProjectRevision != _history.State.Revision.Value)
                return RecipeFailure(plan.Command.BaseRevision.Value, plan.ProjectRevision,
                    "world_generation.project_revision_conflict", "The project changed after the recipe preview.");

            var result = state.Handler.Handle(
                state.History.State,
                plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false },
                _history.State.Project);
            if (!result.Succeeded)
            {
                plan.Audit.Add(new(plan.PlanId, plan.Command.TransactionId.ToString("D"), "apply",
                    state.History.State.Revision.Value, _history.State.Revision.Value, _clock.UtcNow, false));
                return WithRecipeFailure(plan, result.Diagnostics.Select(ToAgentDiagnostic).ToArray());
            }
            var saved = WorldGenerationRecipeStore.Save(
                _session.RootDirectory, result.State.Catalog, result.State.Revision);
            if (!saved.Succeeded)
            {
                plan.Audit.Add(new(plan.PlanId, plan.Command.TransactionId.ToString("D"), "apply",
                    state.History.State.Revision.Value, _history.State.Revision.Value, _clock.UtcNow, false));
                return WithRecipeFailure(plan, saved.Diagnostics.Select(item =>
                    new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
            }
            state.History.Record(result);
            plan.Applied = true;
            plan.AppliedRecipeRevision = state.History.State.Revision.Value;
            plan.Audit.Add(new(plan.PlanId, plan.Command.TransactionId.ToString("D"), "apply",
                plan.AppliedRecipeRevision.Value, _history.State.Revision.Value, _clock.UtcNow, true));
            return ToRecipeResult(plan);
        }
    }

    public WorldGenerationRecipePlanResult RevertWorldGenerationRecipePlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            var state = WorldGenerationState;
            if (!state.RecipePlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return RecipeFailure(state.History.State.Revision.Value, _history.State.Revision.Value,
                    "world_generation.plan_not_applied", $"World-generation recipe plan '{planId}' is not currently applied.");
            if (plan.AppliedRecipeRevision != state.History.State.Revision.Value)
                return RecipeFailure(plan.Command.BaseRevision.Value, _history.State.Revision.Value,
                    "world_generation.revert_conflict", "Only the latest world-generation recipe mutation can be reverted safely.");
            if (!state.History.Undo())
                return RecipeFailure(plan.Command.BaseRevision.Value, _history.State.Revision.Value,
                    "world_generation.inverse_unavailable", "The world-generation recipe inverse is unavailable.");
            var saved = WorldGenerationRecipeStore.Save(
                _session.RootDirectory, state.History.State.Catalog, state.History.State.Revision);
            if (!saved.Succeeded)
            {
                state.History.Redo();
                return WithRecipeFailure(plan, saved.Diagnostics.Select(item =>
                    new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
            }
            plan.Reverted = true;
            plan.Audit.Add(new(plan.PlanId, plan.Command.TransactionId.ToString("D"), "revert",
                state.History.State.Revision.Value, _history.State.Revision.Value, _clock.UtcNow, true));
            return ToRecipeResult(plan);
        }
    }

    public WorldGenerationExecutionPlanResource PreviewWorldGenerationExecution(
        WorldGenerationExecutionPreviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            var state = WorldGenerationState;
            if (request.SchemaVersion != 1 || request.BaseProjectRevision < 0 || request.BaseRecipeRevision < 0)
                return ExecutionFailure("world_generation.execution_request_invalid",
                    "World-generation execution request schema or revision is invalid.");
            if (request.BaseProjectRevision != _history.State.Revision.Value)
                return ExecutionFailure("world_generation.project_revision_conflict",
                    "The project changed before the generation execution was planned.");
            if (request.BaseRecipeRevision != state.History.State.Revision.Value)
                return ExecutionFailure("world_generation.recipe_revision_conflict",
                    "World-generation recipes changed before execution was planned.");
            if (!Guid.TryParse(request.RecipeId, out var recipeId))
                return ExecutionFailure("world_generation.recipe_id_invalid", "Recipe ID is invalid.");
            var recipe = state.History.State.Catalog.Find(recipeId);
            if (recipe is null)
                return ExecutionFailure("world_generation.recipe_missing", $"World-generation recipe '{request.RecipeId}' does not exist.");

            var transactionId = Guid.NewGuid();
            var planId = Guid.NewGuid();
            var plan = state.Planner.Build(
                recipe,
                _history.State.Revision.Value,
                state.History.State.Revision,
                transactionId,
                planId,
                request.Stages);
            var saved = WorldGenerationExecutionPlanStore.Save(_session.RootDirectory, plan);
            if (!saved.Succeeded)
                return new(false, plan,
                    saved.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
                    false, null);
            state.ExecutionPlans[planId] = plan;
            return new(plan.ReadyToApply, plan,
                plan.Diagnostics.Select(ToAgentDiagnostic).ToArray(), false, null);
        }
    }

    public WorldGenerationExecutionPlanResource QueueWorldGenerationExecutionPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            var state = WorldGenerationState;
            if (!Guid.TryParse(planId, out var parsedPlanId))
                return ExecutionFailure("world_generation.plan_id_invalid", "Execution plan ID is invalid.");
            var plan = ResolveExecutionPlan(state, parsedPlanId);
            if (plan is null)
                return ExecutionFailure("world_generation.execution_plan_missing", $"Execution plan '{planId}' does not exist.");
            if (plan.ProjectRevision != _history.State.Revision.Value)
                return ExecutionFailure("world_generation.project_revision_conflict",
                    "The project changed after the generation execution preview.");
            if (plan.RecipeRevision != state.History.State.Revision)
                return ExecutionFailure("world_generation.recipe_revision_conflict",
                    "World-generation recipes changed after the execution preview.");
            var recipe = state.History.State.Catalog.Find(plan.RecipeId);
            if (recipe is null || !string.Equals(recipe.Fingerprint(), plan.RecipeFingerprint, StringComparison.Ordinal))
                return ExecutionFailure("world_generation.recipe_changed",
                    "The execution plan no longer matches its world-generation recipe.");
            if (!recipe.ApprovedForGeneration || WorldGenerationRecipeValidator.Validate(recipe).Count > 0)
                return ExecutionFailure("world_generation.recipe_not_approved",
                    "The execution plan recipe is no longer valid and explicitly approved.");
            if (!plan.ReadyToApply)
                return new(false, plan, plan.Diagnostics.Concat(
                        [new WorldGenerationValidationIssue("world_generation.plan_not_ready",
                            "The generation recipe must be valid and explicitly approved before queuing.",
                            "world-generation/plan")])
                    .Select(ToAgentDiagnostic).ToArray(), false, null);

            var existing = WorldGenerationJobStore.Load(_session.RootDirectory, parsedPlanId);
            if (existing.Job is { } current && current.Status is not WorldGenerationJobStatus.Cancelled and not WorldGenerationJobStatus.Failed)
                return new(true, plan, current.Diagnostics.Select(ToAgentDiagnostic).ToArray(), true, current.Status);
            var job = WorldGenerationJob.Queue(plan, recipe, _clock.UtcNow);
            var saved = WorldGenerationJobStore.Save(_session.RootDirectory, job);
            if (!saved.Succeeded)
                return new(false, plan,
                    saved.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
                    false, null);
            return new(true, plan, [], true, job.Status);
        }
    }

    public IReadOnlyList<WorldGenerationJobResource> GetWorldGenerationJobs()
    {
        lock (_gate)
        {
            return WorldGenerationJobStore.List(_session.RootDirectory).Select(ToJobResource).ToArray();
        }
    }

    public WorldGenerationJobResource? GetWorldGenerationJob(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!Guid.TryParse(planId, out var parsed)) return null;
            return WorldGenerationJobStore.Load(_session.RootDirectory, parsed).Job is { } job
                ? ToJobResource(job)
                : null;
        }
    }

    public WorldGenerationExecutionPlanResource CancelWorldGenerationJob(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!Guid.TryParse(planId, out var parsed))
                return ExecutionFailure("world_generation.plan_id_invalid", "Execution plan ID is invalid.");
            var loaded = WorldGenerationJobStore.Load(_session.RootDirectory, parsed);
            if (loaded.Job is not { } job)
                return ExecutionFailure("world_generation.job_missing", $"World-generation job '{planId}' does not exist.");
            if (job.Status is WorldGenerationJobStatus.Succeeded or WorldGenerationJobStatus.Cancelled)
                return new(false, job.Plan,
                    [new AgentDiagnostic("world_generation.job_not_cancellable",
                        $"Job status '{job.Status}' cannot be cancelled.", "world-generation/job")],
                    true, job.Status);
            var cancelled = job.WithStatus(WorldGenerationJobStatus.Cancelled, _clock.UtcNow);
            var saved = WorldGenerationJobStore.Save(_session.RootDirectory, cancelled);
            return saved.Succeeded
                ? new(true, cancelled.Plan, [], true, cancelled.Status)
                : new(false, cancelled.Plan, saved.Diagnostics.Select(item =>
                    new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(), true, job.Status);
        }
    }

    public WorldGenerationPromotionPlanResult PlanWorldGenerationPromotion(
        WorldGenerationPromotionPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            var state = WorldGenerationState;
            if (request.SchemaVersion != 1 || request.BaseProjectRevision < 0 ||
                request.BaseRecipeRevision < 0 || !Guid.TryParse(request.JobPlanId, out var jobPlanId))
                return PromotionFailure(request.BaseProjectRevision, request.BaseRecipeRevision,
                    request.JobPlanId, "world_generation.promotion.request_invalid",
                    "Canonical-promotion request schema, revisions or job plan ID are invalid.");
            if (request.BaseProjectRevision != _history.State.Revision.Value)
                return PromotionFailure(request.BaseProjectRevision, request.BaseRecipeRevision,
                    request.JobPlanId, PromoteWorldGenerationHandler.RevisionConflictCode,
                    "Project changed before canonical-promotion preview.");
            if (request.BaseRecipeRevision != state.History.State.Revision.Value)
                return PromotionFailure(request.BaseProjectRevision, request.BaseRecipeRevision,
                    request.JobPlanId, PromoteWorldGenerationHandler.RecipeRevisionConflictCode,
                    "World-generation recipes changed before canonical-promotion preview.");
            var loaded = WorldGenerationJobStore.Load(_session.RootDirectory, jobPlanId);
            if (loaded.Job is not { } job)
                return PromotionFailure(request.BaseProjectRevision, request.BaseRecipeRevision,
                    request.JobPlanId, "world_generation.promotion.job_missing",
                    $"World-generation job '{request.JobPlanId}' does not exist.");
            var inspection = WorldGenerationPromotionStore.Inspect(_session.RootDirectory, job);
            if (!inspection.Succeeded)
                return PromotionFailure(request.BaseProjectRevision, request.BaseRecipeRevision,
                    request.JobPlanId, inspection.Diagnostics.Select(ToAgentDiagnostic).ToArray());
            var expectation = WorldGenerationPromotionStore.ValidateCanonicalExpectation(
                _session.RootDirectory, job.Recipe.AreaId, request.ExpectedCanonicalSceneSha256);
            if (!expectation.Succeeded)
                return PromotionFailure(request.BaseProjectRevision, request.BaseRecipeRevision,
                    request.JobPlanId, expectation.Diagnostics.Select(ToAgentDiagnostic).ToArray());

            var transactionId = Guid.NewGuid();
            var promotionPlanId = Guid.NewGuid();
            var command = new PromoteWorldGenerationCommand(
                CommandId.From(Guid.NewGuid()),
                transactionId,
                promotionPlanId,
                _history.State.Revision,
                state.History.State.Revision,
                job.PlanId,
                inspection.Report!,
                WorldGenerationPromotionStore.DerivedSceneRelativePath(job.PlanId),
                PromoteWorldGenerationHandler.CanonicalScenePath(job.Recipe.AreaId),
                request.ExpectedCanonicalSceneSha256,
                request.AcceptanceProvenance,
                request.Permission,
                DryRun: true);
            var preview = state.PromotionHandler.Handle(
                _history.State, state.History.State, job, command);
            if (!preview.Succeeded || preview.Command.Patch is null ||
                preview.Command.Inverse is null || preview.Promotion is null)
                return PromotionFailure(request.BaseProjectRevision, request.BaseRecipeRevision,
                    request.JobPlanId, preview.Command.Diagnostics.Select(ToAgentDiagnostic).ToArray());

            var key = promotionPlanId.ToString("D");
            var stored = new StoredWorldGenerationPromotionPlan(
                key,
                command,
                job,
                preview.Promotion,
                preview.Command.Patch,
                preview.Command.Inverse,
                [new(key, transactionId.ToString("D"), "planned", request.BaseRecipeRevision,
                    request.BaseProjectRevision, _clock.UtcNow, true)]);
            state.PromotionPlans.Add(key, stored);
            return ToPromotionResult(stored);
        }
    }

    public WorldGenerationPromotionPlanResult ApplyWorldGenerationPromotion(string promotionPlanId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(promotionPlanId);
        lock (_gate)
        {
            var state = WorldGenerationState;
            if (!state.PromotionPlans.TryGetValue(promotionPlanId, out var plan))
                return PromotionFailure(_history.State.Revision.Value, state.History.State.Revision.Value,
                    null, "world_generation.promotion.plan_missing",
                    $"Canonical-promotion plan '{promotionPlanId}' does not exist.");
            if (plan.Applied)
                return WithPromotionFailure(plan, "world_generation.promotion.plan_already_applied",
                    $"Canonical-promotion plan '{promotionPlanId}' is already applied.");
            if (plan.Command.BaseRevision != _history.State.Revision ||
                plan.Command.BaseRecipeRevision != state.History.State.Revision)
                return WithPromotionFailure(plan, PromoteWorldGenerationHandler.RevisionConflictCode,
                    "Project or recipe revision changed after canonical-promotion preview.");

            var currentJob = WorldGenerationJobStore.Load(_session.RootDirectory, plan.Job.PlanId).Job;
            if (currentJob is null)
                return WithPromotionFailure(plan, "world_generation.promotion.job_missing",
                    "The previewed world-generation job no longer exists.");
            var inspection = WorldGenerationPromotionStore.Inspect(_session.RootDirectory, currentJob);
            if (!inspection.Succeeded)
                return WithPromotionFailure(plan, inspection.Diagnostics.Select(ToAgentDiagnostic).ToArray());
            var command = plan.Command with
            {
                CommandId = CommandId.From(Guid.NewGuid()),
                ValidationReport = inspection.Report!,
                DryRun = false,
            };
            var applied = state.PromotionHandler.Handle(
                _history.State, state.History.State, currentJob, command);
            if (!applied.Succeeded || applied.Promotion is null)
                return WithPromotionFailure(plan,
                    applied.Command.Diagnostics.Select(ToAgentDiagnostic).ToArray());

            var written = WorldGenerationPromotionStore.Apply(
                _session.RootDirectory,
                applied.Promotion,
                plan.Command.ExpectedCanonicalSceneSha256);
            if (!written.Succeeded)
                return WithPromotionFailure(plan, written.Diagnostics.Select(ToAgentDiagnostic).ToArray());
            var saved = _session.Save(applied.Command.State.Project);
            if (!saved.Succeeded)
            {
                WorldGenerationPromotionStore.Restore(_session.RootDirectory, written.Patches, useAfter: false);
                return WithPromotionFailure(plan, saved.Diagnostics.Select(ToAgentDiagnostic).ToArray());
            }

            _history.Record(applied.Command);
            plan.Promotion = applied.Promotion;
            plan.Patches = written.Patches;
            plan.Applied = true;
            plan.AppliedProjectRevision = applied.Command.NewRevision!.Value.Value;
            plan.Audit.Add(new(plan.PlanId, plan.Command.TransactionId.ToString("D"), "apply",
                state.History.State.Revision.Value, plan.AppliedProjectRevision.Value, _clock.UtcNow, true));
            return ToPromotionResult(plan);
        }
    }

    public WorldGenerationPromotionPlanResult RevertWorldGenerationPromotion(string promotionPlanId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(promotionPlanId);
        lock (_gate)
        {
            var state = WorldGenerationState;
            if (!state.PromotionPlans.TryGetValue(promotionPlanId, out var plan) ||
                !plan.Applied || plan.Reverted || plan.Patches is null)
                return PromotionFailure(_history.State.Revision.Value, state.History.State.Revision.Value,
                    null, "world_generation.promotion.plan_not_applied",
                    $"Canonical-promotion plan '{promotionPlanId}' is not currently applied.");
            if (plan.AppliedProjectRevision != _history.State.Revision.Value)
                return WithPromotionFailure(plan, "world_generation.promotion.revert_conflict",
                    "Only the latest canonical promotion can be reverted safely.");

            var undone = _history.Undo();
            if (!undone.Succeeded)
                return WithPromotionFailure(plan, "world_generation.promotion.inverse_unavailable",
                    "Canonical-promotion project inverse is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return WithPromotionFailure(plan, saved.Diagnostics.Select(ToAgentDiagnostic).ToArray());
            }
            try
            {
                WorldGenerationPromotionStore.Restore(
                    _session.RootDirectory, plan.Patches, useAfter: false);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                var redone = _history.Redo();
                if (redone.Succeeded) _session.Save(redone.State.Project);
                return WithPromotionFailure(plan, WorldGenerationPromotionStore.WriteFailedCode,
                    exception.Message);
            }

            plan.Reverted = true;
            plan.Audit.Add(new(plan.PlanId, plan.Command.TransactionId.ToString("D"), "revert",
                state.History.State.Revision.Value, _history.State.Revision.Value, _clock.UtcNow, true));
            return ToPromotionResult(plan);
        }
    }

    public WorldGenerationCanonicalPromotionResource GetCanonicalWorldGenerationPromotion(string areaId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(areaId);
        lock (_gate)
        {
            if (!Guid.TryParse(areaId, out var parsed))
                return new(false, null, null,
                    [new AgentDiagnostic("world_generation.promotion.area_id_invalid",
                        "Canonical-promotion area ID is invalid.", "world-generation/promotion")]);
            var loaded = WorldGenerationPromotionStore.Load(_session.RootDirectory, parsed);
            if (!loaded.Succeeded)
                return new(false, null, loaded.SceneSha256,
                    loaded.Diagnostics.Select(ToAgentDiagnostic).ToArray());
            return new(true, loaded.Promotion, loaded.SceneSha256, []);
        }
    }

    private WorldGenerationAgentState WorldGenerationState
    {
        get
        {
            if (_worldGenerationAgentState is not null) return _worldGenerationAgentState;
            var loaded = WorldGenerationRecipeStore.Load(_session.RootDirectory);
            _worldGenerationAgentState = new(
                new SetWorldGenerationRecipesHandler(_clock),
                new PromoteWorldGenerationHandler(_clock),
                new WorldGenerationRecipeCommandHistory(loaded.Catalog, loaded.Revision),
                new WorldGenerationPlanner(),
                loaded.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToList());
            return _worldGenerationAgentState;
        }
    }

    private WorldGenerationExecutionPlan? ResolveExecutionPlan(WorldGenerationAgentState state, Guid planId)
    {
        if (state.ExecutionPlans.TryGetValue(planId, out var inMemory)) return inMemory;
        var loaded = WorldGenerationExecutionPlanStore.Load(_session.RootDirectory, planId);
        if (loaded.Plan is not { } persisted) return null;
        state.ExecutionPlans[planId] = persisted;
        return persisted;
    }

    private WorldGenerationRecipePlanResult RecipeFailure(
        long recipeRevision,
        long projectRevision,
        string code,
        string message) =>
        new(false, null, null, recipeRevision, null, projectRevision, null, null,
            [new AgentDiagnostic(code, message, "world-generation")], false, false, []);

    private static WorldGenerationExecutionPlanResource ExecutionFailure(string code, string message) =>
        new(false, null, [new AgentDiagnostic(code, message, "world-generation/execution")], false, null);

    private static AgentDiagnostic ToAgentDiagnostic(CommandDiagnostic diagnostic) =>
        new(diagnostic.Code, diagnostic.Message, "world-generation");

    private static AgentDiagnostic ToAgentDiagnostic(WorldGenerationValidationIssue diagnostic) =>
        new(diagnostic.Code, diagnostic.Message, diagnostic.Path);

    private static AgentDiagnostic ToAgentDiagnostic(ProjectSessionDiagnostic diagnostic) =>
        new(diagnostic.Code, diagnostic.Message, diagnostic.Path ?? "world-generation/promotion");

    private WorldGenerationRecipePlanResult ToRecipeResult(StoredWorldGenerationRecipePlan plan) => new(
        true,
        plan.PlanId,
        plan.Command.TransactionId.ToString("D"),
        plan.Command.BaseRevision.Value,
        plan.AppliedRecipeRevision,
        plan.ProjectRevision,
        plan.Diff,
        plan.Inverse,
        [],
        plan.Applied,
        plan.Reverted,
        plan.Audit.ToArray());

    private WorldGenerationRecipePlanResult WithRecipeFailure(
        StoredWorldGenerationRecipePlan plan,
        IReadOnlyList<AgentDiagnostic> diagnostics) => new(
        false,
        plan.PlanId,
        plan.Command.TransactionId.ToString("D"),
        plan.Command.BaseRevision.Value,
        plan.AppliedRecipeRevision,
        plan.ProjectRevision,
        plan.Diff,
        plan.Inverse,
        diagnostics,
        plan.Applied,
        plan.Reverted,
        plan.Audit.ToArray());

    private static WorldGenerationPromotionPlanResult PromotionFailure(
        long projectRevision,
        long recipeRevision,
        string? jobPlanId,
        string code,
        string message) =>
        PromotionFailure(projectRevision, recipeRevision, jobPlanId,
            [new AgentDiagnostic(code, message, "world-generation/promotion")]);

    private static WorldGenerationPromotionPlanResult PromotionFailure(
        long projectRevision,
        long recipeRevision,
        string? jobPlanId,
        IReadOnlyList<AgentDiagnostic> diagnostics) =>
        new(false, null, null, jobPlanId, projectRevision, null, recipeRevision,
            null, null, null, null, diagnostics, false, false, []);

    private static WorldGenerationPromotionPlanResult ToPromotionResult(
        StoredWorldGenerationPromotionPlan plan) =>
        new(true,
            plan.PlanId,
            plan.Command.TransactionId.ToString("D"),
            plan.Job.PlanId.ToString("D"),
            plan.Command.BaseRevision.Value,
            plan.AppliedProjectRevision,
            plan.Command.BaseRecipeRevision.Value,
            plan.Promotion.CanonicalSceneRelativePath,
            plan.Promotion.CanonicalSceneSha256,
            plan.Diff,
            plan.Inverse,
            [],
            plan.Applied,
            plan.Reverted,
            plan.Audit.ToArray());

    private static WorldGenerationPromotionPlanResult WithPromotionFailure(
        StoredWorldGenerationPromotionPlan plan,
        string code,
        string message) =>
        WithPromotionFailure(plan,
            [new AgentDiagnostic(code, message, "world-generation/promotion")]);

    private static WorldGenerationPromotionPlanResult WithPromotionFailure(
        StoredWorldGenerationPromotionPlan plan,
        IReadOnlyList<AgentDiagnostic> diagnostics) =>
        new(false,
            plan.PlanId,
            plan.Command.TransactionId.ToString("D"),
            plan.Job.PlanId.ToString("D"),
            plan.Command.BaseRevision.Value,
            plan.AppliedProjectRevision,
            plan.Command.BaseRecipeRevision.Value,
            plan.Promotion.CanonicalSceneRelativePath,
            plan.Promotion.CanonicalSceneSha256,
            plan.Diff,
            plan.Inverse,
            diagnostics,
            plan.Applied,
            plan.Reverted,
            plan.Audit.ToArray());

    private static WorldGenerationJobResource ToJobResource(WorldGenerationJob job) => new(
        job.JobId.ToString("D"),
        job.PlanId.ToString("D"),
        job.Recipe.Id.ToString("D"),
        job.Recipe.AreaId.ToString("D"),
        job.RecipeFingerprint,
        job.Plan.PlanFingerprint,
        job.ProjectRevision,
        job.RecipeRevision.Value,
        job.Status,
        job.CreatedAtUtc,
        job.UpdatedAtUtc,
        job.Diagnostics.Select(ToAgentDiagnostic).ToArray(),
        job.Audit);

    private sealed class WorldGenerationAgentState
    {
        public SetWorldGenerationRecipesHandler Handler { get; }
        public PromoteWorldGenerationHandler PromotionHandler { get; }
        public WorldGenerationRecipeCommandHistory History { get; }
        public WorldGenerationPlanner Planner { get; }
        public List<AgentDiagnostic> LoadDiagnostics { get; }
        public Dictionary<string, StoredWorldGenerationRecipePlan> RecipePlans { get; } = new(StringComparer.Ordinal);
        public Dictionary<Guid, WorldGenerationExecutionPlan> ExecutionPlans { get; } = [];
        public Dictionary<string, StoredWorldGenerationPromotionPlan> PromotionPlans { get; } =
            new(StringComparer.Ordinal);

        public WorldGenerationAgentState(
            SetWorldGenerationRecipesHandler handler,
            PromoteWorldGenerationHandler promotionHandler,
            WorldGenerationRecipeCommandHistory history,
            WorldGenerationPlanner planner,
            List<AgentDiagnostic> loadDiagnostics)
        {
            Handler = handler;
            PromotionHandler = promotionHandler;
            History = history;
            Planner = planner;
            LoadDiagnostics = loadDiagnostics;
        }
    }

    private sealed class StoredWorldGenerationPromotionPlan
    {
        public string PlanId { get; }
        public PromoteWorldGenerationCommand Command { get; }
        public WorldGenerationJob Job { get; }
        public WorldGenerationCanonicalPromotion Promotion { get; set; }
        public ProjectPatch Diff { get; }
        public ProjectPatch Inverse { get; }
        public List<WorldGenerationAuditEntry> Audit { get; }
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedProjectRevision { get; set; }
        public IReadOnlyList<WorldGenerationPromotionFilePatch>? Patches { get; set; }

        public StoredWorldGenerationPromotionPlan(
            string planId,
            PromoteWorldGenerationCommand command,
            WorldGenerationJob job,
            WorldGenerationCanonicalPromotion promotion,
            ProjectPatch diff,
            ProjectPatch inverse,
            List<WorldGenerationAuditEntry> audit)
        {
            PlanId = planId;
            Command = command;
            Job = job;
            Promotion = promotion;
            Diff = diff;
            Inverse = inverse;
            Audit = audit;
        }
    }

    private sealed class StoredWorldGenerationRecipePlan
    {
        public string PlanId { get; }
        public SetWorldGenerationRecipesCommand Command { get; }
        public ProjectPatch Diff { get; }
        public ProjectPatch Inverse { get; }
        public long ProjectRevision { get; }
        public List<WorldGenerationAuditEntry> Audit { get; }
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRecipeRevision { get; set; }

        public StoredWorldGenerationRecipePlan(
            string planId,
            SetWorldGenerationRecipesCommand command,
            ProjectPatch diff,
            ProjectPatch inverse,
            List<WorldGenerationAuditEntry> audit)
        {
            PlanId = planId;
            Command = command;
            Diff = diff;
            Inverse = inverse;
            ProjectRevision = audit[0].ProjectRevision;
            Audit = audit;
        }
    }
}
