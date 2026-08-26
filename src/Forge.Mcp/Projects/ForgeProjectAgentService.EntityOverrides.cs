using Forge.Application.Commands;
using Forge.Application.Entities;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;

namespace Forge.Mcp.Projects;

public sealed partial class ForgeProjectAgentService
{
    private readonly Dictionary<string, StoredEntityOverridesPlan> _entityOverridesPlans =
        new(StringComparer.Ordinal);

    public EntityOverridesPlanResult PlanEntityOverrides(EntityOverridesPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != EntityOverridesPlanRequest.CurrentSchemaVersion)
                return EntityOverridesFailure(request.BaseRevision, "entity_overrides_plan.schema_unsupported",
                    $"Entity override plan schema must be {EntityOverridesPlanRequest.CurrentSchemaVersion}.");
            if (!Guid.TryParse(request.AreaId, out var areaValue))
                return EntityOverridesFailure(request.BaseRevision, "entity_overrides_plan.area_id_invalid",
                    "Area ID must be a UUID.");
            if (!Guid.TryParse(request.EntityId, out var entityValue))
                return EntityOverridesFailure(request.BaseRevision, "entity_overrides_plan.entity_id_invalid",
                    "Entity ID must be a UUID.");
            if (request.Overrides is null)
                return EntityOverridesFailure(request.BaseRevision, "entity_overrides_plan.overrides_missing",
                    "Replacement overrides are required.");

            var command = new SetEntityOverridesCommand(
                CommandId.From(Guid.NewGuid()),
                new ProjectRevision(request.BaseRevision),
                AreaId.From(areaValue),
                EntityId.From(entityValue),
                request.Overrides,
                true);
            var preview = _overridesHandler.Handle(_history.State, command);
            if (!preview.Succeeded)
            {
                return new EntityOverridesPlanResult(false, null, request.BaseRevision, preview.Patch, preview.Inverse,
                    preview.Diagnostics.Select(Map).ToArray(), false, false, []);
            }

            var planId = Guid.NewGuid().ToString("D");
            var plan = new StoredEntityOverridesPlan(
                planId,
                command,
                preview.Patch!,
                preview.Inverse!,
                [new EntityOverridesAuditEntry(planId, "planned", request.BaseRevision, _clock.UtcNow, true)]);
            _entityOverridesPlans.Add(planId, plan);
            return ToEntityOverridesResult(plan);
        }
    }

    public EntityOverridesPlanResult ApplyEntityOverridesPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_entityOverridesPlans.TryGetValue(planId, out var plan))
                return EntityOverridesFailure(GetCurrentRevision(), "entity_overrides_plan.missing",
                    $"Entity override plan '{planId}' does not exist.");
            if (plan.Applied)
                return EntityOverridesFailure(plan.Command.BaseRevision.Value,
                    "entity_overrides_plan.already_applied", $"Entity override plan '{planId}' is already applied.");

            var command = plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
            var result = _overridesHandler.Handle(_history.State, command);
            if (!result.Succeeded)
            {
                plan.Audit.Add(new(planId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return ToEntityOverridesResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = result.Diagnostics.Select(Map).ToArray(),
                };
            }

            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
            {
                return ToEntityOverridesResult(plan) with
                {
                    Succeeded = false,
                    Diagnostics = saved.Diagnostics.Select(item =>
                        new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray(),
                };
            }

            _history.Record(result);
            plan.Applied = true;
            plan.AppliedRevision = result.NewRevision!.Value.Value;
            plan.Audit.Add(new(planId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToEntityOverridesResult(plan);
        }
    }

    public EntityOverridesPlanResult RevertEntityOverridesPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_entityOverridesPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return EntityOverridesFailure(GetCurrentRevision(), "entity_overrides_plan.not_applied",
                    $"Entity override plan '{planId}' is not currently applied.");
            if (plan.AppliedRevision != _history.State.Revision.Value)
                return EntityOverridesFailure(plan.Command.BaseRevision.Value,
                    "entity_overrides_plan.revision_conflict",
                    "Only the most recently applied override plan can be reverted without overwriting later work.");

            var undone = _history.Undo();
            if (!undone.Succeeded)
                return EntityOverridesFailure(plan.Command.BaseRevision.Value,
                    "entity_overrides_plan.inverse_unavailable", "The inverse operation is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                return EntityOverridesFailure(plan.Command.BaseRevision.Value,
                    "entity_overrides_plan.save_failed", "The reverted project could not be saved.");
            }

            plan.Reverted = true;
            plan.Audit.Add(new(planId, "revert", undone.State.Revision.Value, _clock.UtcNow, true));
            return ToEntityOverridesResult(plan);
        }
    }

    private static EntityOverridesPlanResult EntityOverridesFailure(long revision, string code, string message) =>
        new(false, null, revision, null, null, [new AgentDiagnostic(code, message)], false, false, []);

    private static EntityOverridesPlanResult ToEntityOverridesResult(StoredEntityOverridesPlan plan) =>
        new(true, plan.PlanId, plan.Command.BaseRevision.Value, plan.Diff, plan.Inverse, [],
            plan.Applied, plan.Reverted, plan.Audit.ToArray());

    private sealed class StoredEntityOverridesPlan(
        string planId,
        SetEntityOverridesCommand command,
        ProjectPatch diff,
        ProjectPatch inverse,
        List<EntityOverridesAuditEntry> audit)
    {
        public string PlanId { get; } = planId;
        public SetEntityOverridesCommand Command { get; } = command;
        public ProjectPatch Diff { get; } = diff;
        public ProjectPatch Inverse { get; } = inverse;
        public List<EntityOverridesAuditEntry> Audit { get; } = audit;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long? AppliedRevision { get; set; }
    }
}
