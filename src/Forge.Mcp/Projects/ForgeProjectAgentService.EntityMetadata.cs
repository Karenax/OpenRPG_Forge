using Forge.Application.Commands;
using Forge.Application.Entities;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;

namespace Forge.Mcp.Projects;

public sealed partial class ForgeProjectAgentService
{
    private readonly Dictionary<string, StoredEntityTransformPlan> _entityTransformPlans =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredEntityTagsPlan> _entityTagsPlans =
        new(StringComparer.Ordinal);

    public EntityTransformPlanResult PlanEntityTransform(EntityTransformPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != EntityTransformPlanRequest.CurrentSchemaVersion)
                return EntityTransformFailure(request.BaseRevision, "entity_transform_plan.schema_unsupported",
                    $"Entity transform plan schema must be {EntityTransformPlanRequest.CurrentSchemaVersion}.");
            if (!Guid.TryParse(request.AreaId, out var areaValue))
                return EntityTransformFailure(request.BaseRevision, "entity_transform_plan.area_id_invalid",
                    "Area ID must be a UUID.");
            if (!Guid.TryParse(request.EntityId, out var entityValue))
                return EntityTransformFailure(request.BaseRevision, "entity_transform_plan.entity_id_invalid",
                    "Entity ID must be a UUID.");
            if (request.Transform is null)
                return EntityTransformFailure(request.BaseRevision, "entity_transform_plan.transform_missing",
                    "Replacement transform is required.");

            var command = new TransformEntityCommand(
                CommandId.From(Guid.NewGuid()),
                new ProjectRevision(request.BaseRevision),
                AreaId.From(areaValue),
                EntityId.From(entityValue),
                request.Transform,
                true);
            var preview = new TransformEntityHandler(_clock).Handle(_history.State, command);
            if (!preview.Succeeded)
                return new(false, null, request.BaseRevision, preview.Patch, preview.Inverse,
                    preview.Diagnostics.Select(Map).ToArray(), false, false, []);
            var planId = Guid.NewGuid().ToString("D");
            var plan = new StoredEntityTransformPlan(planId, command, preview.Patch!, preview.Inverse!,
                [new EntityTransformAuditEntry(planId, "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _entityTransformPlans.Add(planId, plan);
            return ToEntityTransformResult(plan);
        }
    }

    public EntityTransformPlanResult ApplyEntityTransformPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_entityTransformPlans.TryGetValue(planId, out var plan))
                return EntityTransformFailure(GetCurrentRevision(), "entity_transform_plan.missing",
                    $"Entity transform plan '{planId}' does not exist.");
            if (plan.Applied)
                return EntityTransformFailure(plan.Command.BaseRevision.Value,
                    "entity_transform_plan.already_applied", $"Entity transform plan '{planId}' is already applied.");
            var result = new TransformEntityHandler(_clock).Handle(_history.State,
                plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false });
            if (!result.Succeeded)
            {
                plan.Audit.Add(new(planId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return ToEntityTransformResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = result.Diagnostics.Select(Map).ToArray(),
                };
            }
            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
                return ToEntityTransformResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = saved.Diagnostics.Select(item =>
                        new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
                };
            _history.Record(result);
            plan.Applied = true;
            plan.AppliedRevision = result.NewRevision!.Value.Value;
            plan.Audit.Add(new(planId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToEntityTransformResult(plan);
        }
    }

    public EntityTransformPlanResult RevertEntityTransformPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_entityTransformPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return EntityTransformFailure(GetCurrentRevision(), "entity_transform_plan.not_applied",
                    $"Entity transform plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _history.State.Revision.Value)
                return EntityTransformFailure(plan.Command.BaseRevision.Value,
                    "entity_transform_plan.revision_conflict",
                    "Only the most recently applied transform plan can be reverted without overwriting later work.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return EntityTransformFailure(plan.Command.BaseRevision.Value,
                    "entity_transform_plan.inverse_unavailable", "The inverse operation is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return EntityTransformFailure(plan.Command.BaseRevision.Value,
                    "entity_transform_plan.save_failed", "The reverted project could not be saved.");
            }
            plan.Reverted = true;
            plan.Audit.Add(new(planId, "revert", undone.State.Revision.Value, _clock.UtcNow, true));
            return ToEntityTransformResult(plan);
        }
    }

    public EntityTagsPlanResult PlanEntityTags(EntityTagsPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != EntityTagsPlanRequest.CurrentSchemaVersion)
                return EntityTagsFailure(request.BaseRevision, "entity_tags_plan.schema_unsupported",
                    $"Entity tags plan schema must be {EntityTagsPlanRequest.CurrentSchemaVersion}.");
            if (!Guid.TryParse(request.AreaId, out var areaValue))
                return EntityTagsFailure(request.BaseRevision, "entity_tags_plan.area_id_invalid",
                    "Area ID must be a UUID.");
            if (!Guid.TryParse(request.EntityId, out var entityValue))
                return EntityTagsFailure(request.BaseRevision, "entity_tags_plan.entity_id_invalid",
                    "Entity ID must be a UUID.");
            if (request.Tags is null)
                return EntityTagsFailure(request.BaseRevision, "entity_tags_plan.tags_missing",
                    "Replacement tags are required.");

            var command = new SetEntityTagsCommand(
                CommandId.From(Guid.NewGuid()),
                new ProjectRevision(request.BaseRevision),
                AreaId.From(areaValue),
                EntityId.From(entityValue),
                request.Tags,
                true);
            var preview = new SetEntityTagsHandler(_clock).Handle(_history.State, command);
            if (!preview.Succeeded)
                return new(false, null, request.BaseRevision, preview.Patch, preview.Inverse,
                    preview.Diagnostics.Select(Map).ToArray(), false, false, []);
            var planId = Guid.NewGuid().ToString("D");
            var plan = new StoredEntityTagsPlan(planId, command, preview.Patch!, preview.Inverse!,
                [new EntityTagsAuditEntry(planId, "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _entityTagsPlans.Add(planId, plan);
            return ToEntityTagsResult(plan);
        }
    }

    public EntityTagsPlanResult ApplyEntityTagsPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_entityTagsPlans.TryGetValue(planId, out var plan))
                return EntityTagsFailure(GetCurrentRevision(), "entity_tags_plan.missing",
                    $"Entity tags plan '{planId}' does not exist.");
            if (plan.Applied)
                return EntityTagsFailure(plan.Command.BaseRevision.Value, "entity_tags_plan.already_applied",
                    $"Entity tags plan '{planId}' is already applied.");
            var result = new SetEntityTagsHandler(_clock).Handle(_history.State,
                plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false });
            if (!result.Succeeded)
            {
                plan.Audit.Add(new(planId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return ToEntityTagsResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = result.Diagnostics.Select(Map).ToArray(),
                };
            }
            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
                return ToEntityTagsResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = saved.Diagnostics.Select(item =>
                        new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
                };
            _history.Record(result);
            plan.Applied = true;
            plan.AppliedRevision = result.NewRevision!.Value.Value;
            plan.Audit.Add(new(planId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToEntityTagsResult(plan);
        }
    }

    public EntityTagsPlanResult RevertEntityTagsPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_entityTagsPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return EntityTagsFailure(GetCurrentRevision(), "entity_tags_plan.not_applied",
                    $"Entity tags plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _history.State.Revision.Value)
                return EntityTagsFailure(plan.Command.BaseRevision.Value, "entity_tags_plan.revision_conflict",
                    "Only the most recently applied tags plan can be reverted without overwriting later work.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return EntityTagsFailure(plan.Command.BaseRevision.Value,
                    "entity_tags_plan.inverse_unavailable", "The inverse operation is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return EntityTagsFailure(plan.Command.BaseRevision.Value,
                    "entity_tags_plan.save_failed", "The reverted project could not be saved.");
            }
            plan.Reverted = true;
            plan.Audit.Add(new(planId, "revert", undone.State.Revision.Value, _clock.UtcNow, true));
            return ToEntityTagsResult(plan);
        }
    }

    private static EntityTransformPlanResult EntityTransformFailure(long revision, string code, string message) =>
        new(false, null, revision, null, null, [new AgentDiagnostic(code, message)], false, false, []);

    private static EntityTagsPlanResult EntityTagsFailure(long revision, string code, string message) =>
        new(false, null, revision, null, null, [new AgentDiagnostic(code, message)], false, false, []);

    private static EntityTransformPlanResult ToEntityTransformResult(StoredEntityTransformPlan plan) =>
        new(true, plan.PlanId, plan.Command.BaseRevision.Value, plan.Diff, plan.Inverse, [],
            plan.Applied, plan.Reverted, plan.Audit.ToArray());

    private static EntityTagsPlanResult ToEntityTagsResult(StoredEntityTagsPlan plan) =>
        new(true, plan.PlanId, plan.Command.BaseRevision.Value, plan.Diff, plan.Inverse, [],
            plan.Applied, plan.Reverted, plan.Audit.ToArray());

    private sealed class StoredEntityTransformPlan(
        string planId,
        TransformEntityCommand command,
        ProjectPatch diff,
        ProjectPatch inverse,
        List<EntityTransformAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public TransformEntityCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<EntityTransformAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }

    private sealed class StoredEntityTagsPlan(
        string planId,
        SetEntityTagsCommand command,
        ProjectPatch diff,
        ProjectPatch inverse,
        List<EntityTagsAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public SetEntityTagsCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<EntityTagsAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }
}
