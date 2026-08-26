using Forge.Domain.Validation;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;

namespace Forge.Domain.Assistant;

public enum AgentContextResourceKind
{
    Project,
    World,
    Areas,
    Entities,
    Dialogues,
    Quests,
    Scripts,
    Assets,
    Diagnostics,
    Selection,
}

public sealed record AgentReferenceGraphPolicy(
    bool IncludeReverseReferences,
    bool IncludeImpactReports,
    bool RequireTypedReferences,
    bool StableOrdering,
    int MaximumDepth,
    int MaximumNodes);

public sealed record AgentSemanticToolDefinition(
    Guid Id,
    string Name,
    string DomainCommand,
    string TargetKind,
    AssistantPermissionLevel RequiredPermission,
    string Provenance);

public sealed record AgentChangePlanRequirements(
    bool RequireGoal,
    bool RequireCommands,
    bool RequireAssumptions,
    bool RequireAffectedObjects,
    bool RequireRisks,
    bool RequireTests);

public sealed record AgentDryRunPolicy(
    bool ValidateRevision,
    bool ValidateCommandBatch,
    bool ReturnSemanticDiff,
    bool ProhibitWrites,
    int MaximumCommands);

public sealed record AgentSemanticWorkflow(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    AssistantPermissionLevel MaximumPermission,
    IReadOnlyList<AgentContextResourceKind> ContextResources,
    int MaximumContextItems,
    AgentReferenceGraphPolicy References,
    IReadOnlyList<AgentSemanticToolDefinition> Tools,
    AgentChangePlanRequirements Plans,
    AgentDryRunPolicy DryRun,
    AgentExecutionGovernance? Governance = null,
    AgentResponseSafetyProfile? Safety = null)
{
    public const int CurrentSchemaVersion = 1;
    public static readonly Guid DefaultId = Guid.Parse("a2000000-0000-4000-8000-000000000001");

    public static AgentSemanticWorkflow CreateDefault() => new(
        DefaultId,
        CurrentSchemaVersion,
        "openrpg-forge:r020-agent-workflow",
        AssistantPermissionLevel.ApplyApprovedPlan,
        Enum.GetValues<AgentContextResourceKind>(),
        200,
        new(true, true, true, true, 4, 500),
        [
            Tool("a2000000-0000-4000-8000-000000000101", "area.add_entity", "PlaceEntityCommand", "area", AssistantPermissionLevel.ApplyApprovedPlan),
            Tool("a2000000-0000-4000-8000-000000000102", "dialogue.connect", "SetDialogueCommand", "dialogue", AssistantPermissionLevel.ApplyApprovedPlan),
            Tool("a2000000-0000-4000-8000-000000000103", "quest.set_transition", "SetQuestCommand", "quest", AssistantPermissionLevel.ApplyApprovedPlan),
            Tool("a2000000-0000-4000-8000-000000000104", "blueprint.upsert", "UpsertBlueprintCommand", "blueprint", AssistantPermissionLevel.ApplyApprovedPlan),
            Tool("a2000000-0000-4000-8000-000000000105", "project.set_variable", "SetModuleConfigurationCommand", "project", AssistantPermissionLevel.Configure),
        ],
        new(true, true, true, true, true, true),
        new(true, true, true, true, 50),
        AgentExecutionGovernance.CreateDefault(),
        AgentResponseSafetyProfile.CreateDefault());

    public AgentExecutionGovernance EffectiveGovernance => Governance ?? AgentExecutionGovernance.CreateDefault();
    public AgentResponseSafetyProfile EffectiveSafety => Safety ?? AgentResponseSafetyProfile.CreateDefault();

    private static AgentSemanticToolDefinition Tool(
        string id,
        string name,
        string command,
        string target,
        AssistantPermissionLevel permission) =>
        new(Guid.Parse(id), name, command, target, permission, "openrpg-forge:r020-agent-workflow");
}

public sealed record AgentSemanticCommand(
    Guid Id,
    string ToolName,
    Guid TargetObjectId,
    IReadOnlyDictionary<string, string> Arguments,
    AgentAreaEntityPlacement? AreaEntityPlacement = null);

public sealed record AgentAreaEntityPlacement(
    int SchemaVersion,
    Guid AreaId,
    Guid EntityId,
    Guid BlueprintId,
    TransformValue Transform,
    IReadOnlyList<string>? Tags = null,
    EntityPropertyOverrides? Overrides = null)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record StructuredChangePlan(
    Guid Id,
    Guid TransactionId,
    int SchemaVersion,
    string Provenance,
    long BaseRevision,
    AssistantPermissionLevel Permission,
    string Goal,
    IReadOnlyList<AgentSemanticCommand> Commands,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<Guid> AffectedObjects,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> Tests)
{
    public const int CurrentSchemaVersion = 1;
}

public static class AgentSemanticWorkflowValidator
{
    public const string IdentityInvalidCode = "assistant.workflow.identity_invalid";
    public const string SchemaUnsupportedCode = "assistant.workflow.schema_unsupported";
    public const string ContextInvalidCode = "assistant.workflow.context_invalid";
    public const string ReferencePolicyInvalidCode = "assistant.workflow.reference_policy_invalid";
    public const string ToolInvalidCode = "assistant.workflow.tool_invalid";
    public const string PlanPolicyInvalidCode = "assistant.workflow.plan_policy_invalid";
    public const string DryRunPolicyInvalidCode = "assistant.workflow.dry_run_policy_invalid";
    public const string ChangePlanInvalidCode = "assistant.change_plan.invalid";
    public const string ChangePlanToolUnknownCode = "assistant.change_plan.tool_unknown";
    public const string ChangePlanPermissionDeniedCode = "assistant.change_plan.permission_denied";

    private static readonly string[] RequiredTools =
        ["area.add_entity", "blueprint.upsert", "dialogue.connect", "project.set_variable", "quest.set_transition"];

    public static IReadOnlyList<DomainDiagnostic> Validate(AgentSemanticWorkflow workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        var root = $"assistant/workflow/{workflow.Id:D}";
        var diagnostics = new List<DomainDiagnostic>();
        if (workflow.Id == Guid.Empty || string.IsNullOrWhiteSpace(workflow.Provenance))
            diagnostics.Add(new(IdentityInvalidCode, "Agent workflow UUID and provenance are required.", root));
        if (workflow.SchemaVersion != AgentSemanticWorkflow.CurrentSchemaVersion)
            diagnostics.Add(new(SchemaUnsupportedCode, "Agent workflow schema must be 1.", $"{root}/schemaVersion"));
        var contexts = workflow.ContextResources.Distinct().ToArray();
        if (contexts.Length != Enum.GetValues<AgentContextResourceKind>().Length ||
            workflow.MaximumContextItems is < 1 or > 300 ||
            workflow.MaximumPermission < AssistantPermissionLevel.Propose)
            diagnostics.Add(new(ContextInvalidCode,
                "All structured context surfaces and a 1..300 item bound are required.", $"{root}/contextResources"));
        if (!workflow.References.IncludeReverseReferences || !workflow.References.IncludeImpactReports ||
            !workflow.References.RequireTypedReferences || !workflow.References.StableOrdering ||
            workflow.References.MaximumDepth is < 1 or > 8 || workflow.References.MaximumNodes is < 10 or > 1_000)
            diagnostics.Add(new(ReferencePolicyInvalidCode,
                "Reference graph requires reverse impact, typed edges, stable order and bounded depth/volume.",
                $"{root}/references"));
        var tools = workflow.Tools ?? [];
        if (tools.Count == 0 || tools.GroupBy(item => item.Id).Any(group => group.Count() > 1) ||
            tools.GroupBy(item => item.Name, StringComparer.Ordinal).Any(group => group.Count() > 1) ||
            RequiredTools.Any(required => tools.All(item => !string.Equals(item.Name, required, StringComparison.Ordinal))) ||
            tools.Any(item => item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.Name) ||
                string.IsNullOrWhiteSpace(item.DomainCommand) || string.IsNullOrWhiteSpace(item.TargetKind) ||
                string.IsNullOrWhiteSpace(item.Provenance)))
            diagnostics.Add(new(ToolInvalidCode,
                "Semantic tools require stable UUIDs, unique names, domain commands, targets and provenance.",
                $"{root}/tools"));
        if (!workflow.Plans.RequireGoal || !workflow.Plans.RequireCommands || !workflow.Plans.RequireAssumptions ||
            !workflow.Plans.RequireAffectedObjects || !workflow.Plans.RequireRisks || !workflow.Plans.RequireTests)
            diagnostics.Add(new(PlanPolicyInvalidCode,
                "Change plans must require goals, commands, assumptions, affected objects, risks and tests.",
                $"{root}/plans"));
        if (!workflow.DryRun.ValidateRevision || !workflow.DryRun.ValidateCommandBatch ||
            !workflow.DryRun.ReturnSemanticDiff || !workflow.DryRun.ProhibitWrites ||
            workflow.DryRun.MaximumCommands is < 1 or > 100)
            diagnostics.Add(new(DryRunPolicyInvalidCode,
                "Dry-run must validate revision and batch, return a diff, prohibit writes and remain bounded.",
                $"{root}/dryRun"));
        diagnostics.AddRange(AgentExecutionGovernanceValidator.Validate(workflow.EffectiveGovernance, workflow));
        diagnostics.AddRange(AgentResponseSafetyProfileValidator.Validate(workflow.EffectiveSafety));
        return diagnostics;
    }

    public static IReadOnlyList<DomainDiagnostic> ValidatePlan(
        StructuredChangePlan plan,
        AgentSemanticWorkflow workflow)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(workflow);
        var root = $"assistant/changePlans/{plan.Id:D}";
        var diagnostics = new List<DomainDiagnostic>();
        var required = workflow.Plans;
        if (plan.Id == Guid.Empty || plan.TransactionId == Guid.Empty ||
            plan.SchemaVersion != StructuredChangePlan.CurrentSchemaVersion || plan.BaseRevision < 0 ||
            string.IsNullOrWhiteSpace(plan.Provenance) || required.RequireGoal && string.IsNullOrWhiteSpace(plan.Goal) ||
            required.RequireCommands && plan.Commands.Count == 0 ||
            required.RequireAssumptions && Clean(plan.Assumptions).Count == 0 ||
            required.RequireAffectedObjects && plan.AffectedObjects.Count == 0 ||
            required.RequireRisks && Clean(plan.Risks).Count == 0 ||
            required.RequireTests && Clean(plan.Tests).Count == 0 ||
            plan.Commands.Count > workflow.DryRun.MaximumCommands ||
            plan.Commands.Any(item => item.Id == Guid.Empty || item.TargetObjectId == Guid.Empty) ||
            plan.Commands.GroupBy(item => item.Id).Any(group => group.Count() > 1) ||
            plan.AffectedObjects.Contains(Guid.Empty) || plan.AffectedObjects.Distinct().Count() != plan.AffectedObjects.Count ||
            plan.Commands.Any(item => !plan.AffectedObjects.Contains(item.TargetObjectId)))
            diagnostics.Add(new(ChangePlanInvalidCode,
                "Change plan identity, revision, six required sections, unique targets and command bounds are invalid.", root));
        var tools = workflow.Tools.ToDictionary(item => item.Name, StringComparer.Ordinal);
        foreach (var command in plan.Commands)
        {
            if (!tools.TryGetValue(command.ToolName, out var tool))
                diagnostics.Add(new(ChangePlanToolUnknownCode,
                    $"Semantic tool '{command.ToolName}' is not registered.", $"{root}/commands/{command.Id:D}/toolName"));
            else if (plan.Permission < tool.RequiredPermission || plan.Permission > workflow.MaximumPermission)
                diagnostics.Add(new(ChangePlanPermissionDeniedCode,
                    $"Permission '{plan.Permission}' cannot use '{tool.Name}'.", $"{root}/commands/{command.Id:D}/permission"));
        }
        return diagnostics;
    }

    private static IReadOnlyList<string> Clean(IReadOnlyList<string>? values) =>
        (values ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToArray();
}
