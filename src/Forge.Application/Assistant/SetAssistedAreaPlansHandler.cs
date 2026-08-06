using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Assistant;
using Forge.Domain.Projects;

namespace Forge.Application.Assistant;

public sealed class SetAssistedAreaPlansHandler
{
    public const string RevisionConflictCode = "assistant.area_plan.revision_conflict";
    public const string TransactionRequiredCode = "assistant.area_plan.transaction_required";
    public const string PlanRequiredCode = "assistant.area_plan.plan_required";
    public const string PlanNotPreviewedCode = "assistant.area_plan.plan_not_previewed";
    public const string PermissionDeniedCode = "assistant.area_plan.permission_denied";
    private const string CommandName = "set_assisted_area_plans";
    private readonly IClock _clock;
    private readonly Dictionary<Guid, (AssistedAreaPlanRevision Revision, Guid TransactionId, string Fingerprint)> _previewedPlans = [];

    public SetAssistedAreaPlansHandler(IClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public AssistedAreaPlanCommandResult Handle(
        AssistedAreaPlanState state,
        SetAssistedAreaPlansCommand command,
        ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(project);
        if (command.BaseRevision != state.Revision)
            return Failure(state, command, RevisionConflictCode, "Area plans changed before this command was applied.");
        if (command.TransactionId == Guid.Empty)
            return Failure(state, command, TransactionRequiredCode, "Area-plan configuration requires a transaction ID.");
        if (command.PlanId == Guid.Empty)
            return Failure(state, command, PlanRequiredCode, "Area-plan configuration requires a preview plan ID.");
        var requiredPermission = command.Catalog.Plans.Any(item => item.ApprovedForConstruction)
            ? AssistantPermissionLevel.ApplyApprovedPlan
            : AssistantPermissionLevel.Configure;
        if (command.Permission < requiredPermission)
            return Failure(state, command, PermissionDeniedCode, $"Area-plan configuration requires {requiredPermission} permission.");
        var diagnostics = AssistedAreaPlanProjectValidator.Validate(command.Catalog, project)
            .Select(item => new CommandDiagnostic(item.Code, item.Message)).ToArray();
        if (diagnostics.Length > 0)
            return new(false, state, command.BaseRevision, null, command.TransactionId, command.PlanId,
                diagnostics, null, null, Audit(command, false));

        var patch = new ProjectPatch("replace", "assistant/area-plans",
            $"Set {command.Catalog.Plans.Count} governed assisted area plan or plans; geometry remains unchanged.");
        var inverse = new ProjectPatch("replace", "assistant/area-plans",
            $"Restore {state.Catalog.Plans.Count} governed assisted area plan or plans.");
        var fingerprint = Fingerprint(command.Catalog);
        if (command.DryRun)
        {
            lock (_previewedPlans)
            {
                if (_previewedPlans.Count >= 128) _previewedPlans.Clear();
                _previewedPlans[command.PlanId] = (command.BaseRevision, command.TransactionId, fingerprint);
            }
            return new(true, state, command.BaseRevision, null, command.TransactionId, command.PlanId,
                [], patch, inverse, Audit(command, false));
        }
        lock (_previewedPlans)
        {
            if (!_previewedPlans.TryGetValue(command.PlanId, out var preview) ||
                preview.Revision != command.BaseRevision || preview.TransactionId != command.TransactionId ||
                !string.Equals(preview.Fingerprint, fingerprint, StringComparison.Ordinal))
                return Failure(state, command, PlanNotPreviewedCode,
                    "Area-plan configuration must match its successful dry-run transaction and current revision.");
            _previewedPlans.Remove(command.PlanId);
        }
        var revision = state.Revision.Next();
        return new(true, new AssistedAreaPlanState(command.Catalog, revision), command.BaseRevision, revision,
            command.TransactionId, command.PlanId, [], patch, inverse, Audit(command, true));
    }

    private AssistedAreaPlanCommandResult Failure(
        AssistedAreaPlanState state,
        SetAssistedAreaPlansCommand command,
        string code,
        string message) =>
        new(false, state, command.BaseRevision, null, command.TransactionId, command.PlanId,
            [new CommandDiagnostic(code, message)], null, null, Audit(command, false));

    private AuditEntry Audit(SetAssistedAreaPlansCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);

    private static string Fingerprint(AssistedAreaPlanCatalog catalog)
    {
        var value = new StringBuilder().Append(catalog.Id).Append('|').Append(catalog.SchemaVersion)
            .Append('|').Append(catalog.Provenance);
        foreach (var plan in catalog.Plans)
        {
            value.Append("|P|").Append(plan.Id).Append('|').Append(plan.SchemaVersion).Append('|')
                .Append(plan.AreaId).Append('|').Append(plan.Title).Append('|').Append(plan.Archetype).Append('|')
                .Append(plan.Seed).Append('|').Append(plan.WidthMeters).Append('|').Append(plan.DepthMeters).Append('|')
                .Append(string.Join(',', plan.KitBlueprintIds)).Append('|').Append(string.Join(',', plan.Constraints))
                .Append('|').Append(string.Join(',', plan.Objectives)).Append('|').Append(string.Join(',', plan.LoreSourceIds))
                .Append('|').Append(plan.ApprovedForConstruction).Append('|').Append(plan.EstimatedCostUsd)
                .Append('|').Append(plan.ProviderKey).Append('|').Append(plan.Provenance);
            foreach (var node in plan.Nodes)
                value.Append("|N|").Append(node.Id).Append('|').Append(node.Kind).Append('|').Append(node.Label)
                    .Append('|').Append(node.X).Append('|').Append(node.Z).Append('|').Append(node.Width).Append('|')
                    .Append(node.Depth).Append('|').Append(string.Join(',', node.ContentBlueprintIds)).Append('|')
                    .Append(string.Join(',', node.Tags));
            foreach (var link in plan.Links)
                value.Append("|L|").Append(link.Id).Append('|').Append(link.FromNodeId).Append('|')
                    .Append(link.ToNodeId).Append('|').Append(link.Traversal).Append('|').Append(link.Required);
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.ToString()))).ToLowerInvariant();
    }
}
