using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Mcp.Creatures;

namespace Forge.Mcp.Projects;

public sealed partial class ForgeProjectAgentService
{
    private readonly Dictionary<string, StoredCreatureAuthoringPlan> _creatureAuthoringPlans =
        new(StringComparer.Ordinal);

    public CreatureAuthoringPlanResult PlanCreatureAuthoring(CreatureAuthoringPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != CreatureAuthoringPlanRequest.CurrentSchemaVersion ||
                request.BaseRevision < 0 || !Enum.IsDefined(request.Facet))
            {
                return CreatureAuthoringFailure(request.BaseRevision, request.BlueprintId, request.Facet,
                    "creature.authoring.request_invalid",
                    "Creature authoring requires schema v1, a valid revision and a known facet.");
            }

            BlueprintId blueprintId;
            try
            {
                blueprintId = BlueprintId.Parse(request.BlueprintId);
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                return CreatureAuthoringFailure(request.BaseRevision, request.BlueprintId, request.Facet,
                    "creature.authoring.blueprint_invalid", exception.Message);
            }

            var payloadCount = new object?[]
            {
                request.Foundation, request.Presentation, request.Progression, request.RoleBehavior,
            }.Count(value => value is not null);
            var matchingPayload = request.Facet switch
            {
                CreatureAuthoringFacet.Foundation => request.Foundation is not null,
                CreatureAuthoringFacet.Presentation => request.Presentation is not null,
                CreatureAuthoringFacet.Progression => request.Progression is not null,
                CreatureAuthoringFacet.RoleBehavior => request.RoleBehavior is not null,
                _ => false,
            };
            if (payloadCount != 1 || !matchingPayload)
            {
                return CreatureAuthoringFailure(request.BaseRevision, request.BlueprintId, request.Facet,
                    "creature.authoring.payload_invalid",
                    "Exactly one profile matching the requested creature-authoring facet is required.");
            }

            var preview = ExecuteCreatureAuthoring(request, blueprintId, dryRun: true);
            if (!preview.Succeeded || preview.Patch is null || preview.Inverse is null)
            {
                return new CreatureAuthoringPlanResult(
                    false, null, request.BaseRevision, blueprintId.ToString(), request.Facet,
                    preview.Patch, preview.Inverse, preview.Diagnostics.Select(Map).ToArray(), false, false, []);
            }

            var planId = Guid.NewGuid().ToString("D");
            var plan = new StoredCreatureAuthoringPlan(
                planId, request, blueprintId, preview.Patch, preview.Inverse,
                [CreatureAuthoringAudit(planId, "planned", request.BaseRevision, true, blueprintId, request.Facet)]);
            _creatureAuthoringPlans.Add(planId, plan);
            return ToResult(plan);
        }
    }

    public CreatureAuthoringPlanResult ApplyCreatureAuthoringPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_creatureAuthoringPlans.TryGetValue(planId, out var plan))
                return CreatureAuthoringFailure(_history.State.Revision.Value, null, null,
                    "creature.authoring.plan_missing", $"Creature authoring plan '{planId}' does not exist.");
            if (plan.Applied)
                return CreatureAuthoringFailure(plan.Request.BaseRevision, plan.BlueprintId.ToString(), plan.Request.Facet,
                    "creature.authoring.already_applied", $"Creature authoring plan '{planId}' is already applied.");

            var result = ExecuteCreatureAuthoring(plan.Request, plan.BlueprintId, dryRun: false);
            if (!result.Succeeded)
            {
                plan.Audit.Add(CreatureAuthoringAudit(planId, "apply", _history.State.Revision.Value, false,
                    plan.BlueprintId, plan.Request.Facet));
                return ToResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = result.Diagnostics.Select(Map).ToArray(),
                };
            }

            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
            {
                plan.Audit.Add(CreatureAuthoringAudit(planId, "apply", _history.State.Revision.Value, false,
                    plan.BlueprintId, plan.Request.Facet));
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
            plan.AppliedCommandId = result.Audit.CommandId.Value;
            plan.Audit.Add(CreatureAuthoringAudit(planId, "apply", plan.AppliedRevision.Value, true,
                plan.BlueprintId, plan.Request.Facet));
            return ToResult(plan);
        }
    }

    public CreatureAuthoringPlanResult RevertCreatureAuthoringPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_creatureAuthoringPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return CreatureAuthoringFailure(_history.State.Revision.Value, null, null,
                    "creature.authoring.not_applied", $"Creature authoring plan '{planId}' is not currently applied.");
            var latestAppliedCommandId = _history.Journal.LastOrDefault(entry => entry.IsApplied)?.Id;
            if (latestAppliedCommandId != plan.AppliedCommandId)
                return CreatureAuthoringFailure(plan.Request.BaseRevision, plan.BlueprintId.ToString(), plan.Request.Facet,
                    "creature.authoring.revision_conflict",
                    "Only the latest creature authoring mutation can be reverted safely.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return CreatureAuthoringFailure(plan.Request.BaseRevision, plan.BlueprintId.ToString(), plan.Request.Facet,
                    "creature.authoring.inverse_unavailable", "The canonical inverse is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return CreatureAuthoringFailure(plan.Request.BaseRevision, plan.BlueprintId.ToString(), plan.Request.Facet,
                    "creature.authoring.save_failed", "The reverted project could not be saved.");
            }

            plan.Reverted = true;
            plan.RevertedRevision = undone.State.Revision.Value;
            plan.Audit.Add(CreatureAuthoringAudit(planId, "revert", undone.State.Revision.Value, true,
                plan.BlueprintId, plan.Request.Facet));
            return ToResult(plan);
        }
    }

    public CreatureAuthoringPlanResult RedoCreatureAuthoringPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_creatureAuthoringPlans.TryGetValue(planId, out var plan) || !plan.Applied || !plan.Reverted)
                return CreatureAuthoringFailure(_history.State.Revision.Value, null, null,
                    "creature.authoring.not_reverted",
                    $"Creature authoring plan '{planId}' is not currently reverted.");
            var nextRedoCommandId = _history.Journal.FirstOrDefault(entry => !entry.IsApplied)?.Id;
            if (nextRedoCommandId != plan.AppliedCommandId)
                return CreatureAuthoringFailure(plan.Request.BaseRevision, plan.BlueprintId.ToString(),
                    plan.Request.Facet, "creature.authoring.redo_conflict",
                    "Only the next canonical creature authoring mutation can be redone safely.");
            var redone = _history.Redo();
            if (!redone.Succeeded)
                return CreatureAuthoringFailure(plan.Request.BaseRevision, plan.BlueprintId.ToString(),
                    plan.Request.Facet, "creature.authoring.redo_unavailable",
                    "The canonical redo is unavailable.");
            var saved = _session.Save(redone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Undo();
                return CreatureAuthoringFailure(plan.Request.BaseRevision, plan.BlueprintId.ToString(),
                    plan.Request.Facet, "creature.authoring.save_failed",
                    "The redone project could not be saved.");
            }

            plan.Reverted = false;
            plan.AppliedRevision = redone.State.Revision.Value;
            plan.RevertedRevision = null;
            plan.Audit.Add(CreatureAuthoringAudit(planId, "redo", redone.State.Revision.Value, true,
                plan.BlueprintId, plan.Request.Facet));
            return ToResult(plan);
        }
    }

    private CommandResult ExecuteCreatureAuthoring(
        CreatureAuthoringPlanRequest request,
        BlueprintId blueprintId,
        bool dryRun) => request.Facet switch
        {
            CreatureAuthoringFacet.Foundation => new SetCreatureFoundationHandler(_clock).Handle(
                _history.State, new SetCreatureFoundationCommand(CommandId.From(Guid.NewGuid()),
                    new ProjectRevision(request.BaseRevision), blueprintId, request.Foundation, dryRun)),
            CreatureAuthoringFacet.Presentation => new SetCreaturePresentationHandler(_clock).Handle(
                _history.State, new SetCreaturePresentationCommand(CommandId.From(Guid.NewGuid()),
                    new ProjectRevision(request.BaseRevision), blueprintId, request.Presentation, dryRun)),
            CreatureAuthoringFacet.Progression => new SetCreatureProgressionHandler(_clock).Handle(
                _history.State, new SetCreatureProgressionCommand(CommandId.From(Guid.NewGuid()),
                    new ProjectRevision(request.BaseRevision), blueprintId, request.Progression, dryRun)),
            CreatureAuthoringFacet.RoleBehavior => new SetCreatureRoleBehaviorHandler(_clock).Handle(
                _history.State, new SetCreatureRoleBehaviorCommand(CommandId.From(Guid.NewGuid()),
                    new ProjectRevision(request.BaseRevision), blueprintId, request.RoleBehavior, dryRun)),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Facet)),
        };

    private CreatureAuthoringAuditEntry CreatureAuthoringAudit(
        string planId,
        string action,
        long revision,
        bool succeeded,
        BlueprintId blueprintId,
        CreatureAuthoringFacet facet) =>
        new(planId, action, revision, _clock.UtcNow, succeeded, blueprintId.ToString(), facet);

    private static CreatureAuthoringPlanResult CreatureAuthoringFailure(
        long revision,
        string? blueprintId,
        CreatureAuthoringFacet? facet,
        string code,
        string message) =>
        new(false, null, revision, blueprintId, facet, null, null,
            [new AgentDiagnostic(code, message)], false, false, []);

    private static CreatureAuthoringPlanResult ToResult(StoredCreatureAuthoringPlan plan) => new(
        true,
        plan.PlanId,
        plan.Request.BaseRevision,
        plan.BlueprintId.ToString(),
        plan.Request.Facet,
        plan.Diff,
        plan.Inverse,
        [],
        plan.Applied,
        plan.Reverted,
        plan.Audit.ToArray());

    private sealed class StoredCreatureAuthoringPlan(
        string planId,
        CreatureAuthoringPlanRequest request,
        BlueprintId blueprintId,
        ProjectPatch diff,
        ProjectPatch inverse,
        List<CreatureAuthoringAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public CreatureAuthoringPlanRequest Request { get; } = request;
        public BlueprintId BlueprintId { get; } = blueprintId;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<CreatureAuthoringAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
        public Guid? AppliedCommandId { get; set; }
        public long? RevertedRevision { get; set; }
    }
}
