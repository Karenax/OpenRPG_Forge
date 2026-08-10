using Forge.Domain.Validation;

namespace Forge.Domain.Assistant;

public enum AgentProjectClassification
{
    Public,
    Internal,
    Confidential,
    Restricted,
}

public enum AgentSemanticDiffCategory
{
    Objects,
    References,
    States,
    Assets,
    GeneratedFiles,
}

public enum AgentSemanticChangeKind
{
    Added,
    Removed,
    Modified,
}

public enum AgentTransactionStage
{
    Begun,
    Previewed,
    Committed,
    RolledBack,
    Rejected,
}

public sealed record AgentSemanticDiffEntry(
    Guid Id,
    AgentSemanticDiffCategory Category,
    AgentSemanticChangeKind Change,
    Guid ObjectId,
    string DocumentPath,
    string Property,
    string BeforeValue,
    string AfterValue,
    string Summary);

public sealed record AgentSemanticDiff(
    Guid Id,
    int SchemaVersion,
    Guid TransactionId,
    string BeforeHash,
    string AfterHash,
    IReadOnlyList<AgentSemanticDiffEntry> Entries)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record AgentAiAuditEntry(
    Guid Id,
    int SchemaVersion,
    Guid TransactionId,
    Guid PlanId,
    Guid RollbackId,
    AgentTransactionStage Stage,
    string ClientId,
    string UserId,
    string? Provider,
    string? Model,
    IReadOnlyList<string> Commands,
    string InputHash,
    string OutputHash,
    IReadOnlyList<string> Tests,
    string Provenance,
    long Revision,
    DateTimeOffset OccurredAt,
    bool Succeeded)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record AgentTransactionPolicy(
    bool RequireOptimisticRevision,
    bool RequirePreview,
    bool AtomicCommit,
    bool AllowRollback,
    bool RequireAudit,
    int MaximumCommands);

public sealed record AgentSemanticDiffPolicy(
    bool StableOrdering,
    bool IncludeBeforeAfterHashes,
    IReadOnlyList<AgentSemanticDiffCategory> Categories);

public sealed record AgentAuthorizationPolicy(
    bool DenyByDefault,
    bool RequireHumanConfirmation,
    bool AllowBoundedAutonomy,
    IReadOnlyList<string> AllowedTools,
    IReadOnlyList<string> AllowedTargetKinds,
    IReadOnlyList<string> AllowedPathPrefixes,
    int MaximumCommands,
    int MaximumDurationSeconds);

public sealed record AgentAuditPolicy(
    bool RecordClient,
    bool RecordOptionalProviderAndModel,
    bool RecordUser,
    bool RecordPlanAndCommands,
    bool RecordHashesAndTests,
    bool RecordProvenance,
    bool RecordRollbackId,
    bool RedactSensitiveValues);

public sealed record AgentProviderBudgetPolicy(
    Guid Id,
    string ProviderKey,
    bool Cloud,
    bool AllowProjectContent,
    AgentProjectClassification MaximumClassification,
    int MaximumInputTokens,
    int MaximumOutputTokens,
    decimal MaximumCostPerRequest,
    string Provenance);

public sealed record AgentBudgetPrivacyPolicy(
    AgentProjectClassification ProjectClassification,
    bool CloudProjectContentOptIn,
    bool RedactSensitiveValues,
    bool ProhibitSecretLogging,
    IReadOnlyList<string> SensitiveArgumentKeys,
    IReadOnlyList<AgentProviderBudgetPolicy> Providers);

public sealed record AgentExecutionGovernance(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    AgentTransactionPolicy Transactions,
    AgentSemanticDiffPolicy SemanticDiff,
    AgentAuthorizationPolicy Authorization,
    AgentAuditPolicy Audit,
    AgentBudgetPrivacyPolicy BudgetPrivacy)
{
    public const int CurrentSchemaVersion = 1;
    public static readonly Guid DefaultId = Guid.Parse("a2100000-0000-4000-8000-000000000001");

    public static AgentExecutionGovernance CreateDefault() => new(
        DefaultId,
        CurrentSchemaVersion,
        "openrpg-forge:r021-agent-governance",
        new(true, true, true, true, true, 20),
        new(true, true, Enum.GetValues<AgentSemanticDiffCategory>()),
        new(
            true,
            true,
            false,
            ["area.add_entity", "blueprint.upsert", "dialogue.connect", "project.set_variable", "quest.set_transition"],
            ["area", "blueprint", "dialogue", "project", "quest"],
            ["areas/", "blueprints/", "dialogues/", "module/", "quests/"],
            20,
            60),
        new(true, true, true, true, true, true, true, true),
        new(
            AgentProjectClassification.Internal,
            false,
            true,
            true,
            ["api-key", "apikey", "authorization", "credential", "password", "secret", "token"],
            [
                Provider("a2100000-0000-4000-8000-000000000101", "offline-ci", false, true,
                    AgentProjectClassification.Restricted, 4096, 1024, 0m),
                Provider("a2100000-0000-4000-8000-000000000102", "ollama", false, true,
                    AgentProjectClassification.Restricted, 32768, 4096, 0m),
                Provider("a2100000-0000-4000-8000-000000000103", "openai-compatible", true, false,
                    AgentProjectClassification.Internal, 32768, 4096, 1m),
            ]));

    private static AgentProviderBudgetPolicy Provider(
        string id,
        string key,
        bool cloud,
        bool allowProjectContent,
        AgentProjectClassification classification,
        int inputTokens,
        int outputTokens,
        decimal cost) =>
        new(Guid.Parse(id), key, cloud, allowProjectContent, classification, inputTokens, outputTokens, cost,
            "openrpg-forge:r021-agent-governance");
}

public sealed record AgentExecutionAuthorizationRequest(
    string ProviderKey,
    bool ProjectContentOptIn,
    bool HumanConfirmed,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCost,
    int DurationSeconds,
    IReadOnlyList<string> DocumentPaths);

public sealed record AgentExecutionAuthorizationResult(
    bool Succeeded,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class AgentExecutionGovernanceValidator
{
    public const string IdentityInvalidCode = "assistant.governance.identity_invalid";
    public const string SchemaUnsupportedCode = "assistant.governance.schema_unsupported";
    public const string TransactionInvalidCode = "assistant.governance.transaction_invalid";
    public const string SemanticDiffInvalidCode = "assistant.governance.semantic_diff_invalid";
    public const string AuthorizationInvalidCode = "assistant.governance.authorization_invalid";
    public const string AuditInvalidCode = "assistant.governance.audit_invalid";
    public const string BudgetPrivacyInvalidCode = "assistant.governance.budget_privacy_invalid";
    public const string PermissionDeniedCode = "assistant.governance.permission_denied";
    public const string ScopeDeniedCode = "assistant.governance.scope_denied";
    public const string BudgetExceededCode = "assistant.governance.budget_exceeded";
    public const string PrivacyDeniedCode = "assistant.governance.privacy_denied";

    public static IReadOnlyList<DomainDiagnostic> Validate(
        AgentExecutionGovernance governance,
        AgentSemanticWorkflow workflow)
    {
        ArgumentNullException.ThrowIfNull(governance);
        ArgumentNullException.ThrowIfNull(workflow);
        var root = $"assistant/governance/{governance.Id:D}";
        var diagnostics = new List<DomainDiagnostic>();
        if (governance.Id == Guid.Empty || string.IsNullOrWhiteSpace(governance.Provenance))
            diagnostics.Add(new(IdentityInvalidCode, "Agent governance UUID and provenance are required.", root));
        if (governance.SchemaVersion != AgentExecutionGovernance.CurrentSchemaVersion)
            diagnostics.Add(new(SchemaUnsupportedCode, "Agent governance schema must be 1.", $"{root}/schemaVersion"));
        if (!governance.Transactions.RequireOptimisticRevision || !governance.Transactions.RequirePreview ||
            !governance.Transactions.AtomicCommit || !governance.Transactions.AllowRollback ||
            !governance.Transactions.RequireAudit || governance.Transactions.MaximumCommands is < 1 or > 100)
            diagnostics.Add(new(TransactionInvalidCode,
                "Transactions require optimistic revision, preview, atomic commit, rollback, audit and a 1..100 command bound.",
                $"{root}/transactions"));
        if (!governance.SemanticDiff.StableOrdering || !governance.SemanticDiff.IncludeBeforeAfterHashes ||
            governance.SemanticDiff.Categories.Distinct().Count() != Enum.GetValues<AgentSemanticDiffCategory>().Length)
            diagnostics.Add(new(SemanticDiffInvalidCode,
                "Semantic diff requires stable ordering, hashes and all five domain categories.",
                $"{root}/semanticDiff"));

        var authorization = governance.Authorization;
        var workflowTools = workflow.Tools.Select(item => item.Name).ToHashSet(StringComparer.Ordinal);
        if (!authorization.DenyByDefault || !authorization.RequireHumanConfirmation ||
            authorization.MaximumCommands is < 1 or > 100 || authorization.MaximumDurationSeconds is < 1 or > 3_600 ||
            authorization.AllowedTools.Count == 0 || authorization.AllowedTools.Distinct(StringComparer.Ordinal).Count() != authorization.AllowedTools.Count ||
            authorization.AllowedTools.Any(tool => !workflowTools.Contains(tool)) ||
            authorization.AllowedTargetKinds.Count == 0 || authorization.AllowedTargetKinds.Any(string.IsNullOrWhiteSpace) ||
            authorization.AllowedPathPrefixes.Count == 0 || authorization.AllowedPathPrefixes.Any(path =>
                string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains("..", StringComparison.Ordinal)))
            diagnostics.Add(new(AuthorizationInvalidCode,
                "Authorization must deny by default and bound known tools, targets, relative paths, command count and duration.",
                $"{root}/authorization"));

        var audit = governance.Audit;
        if (!audit.RecordClient || !audit.RecordOptionalProviderAndModel || !audit.RecordUser ||
            !audit.RecordPlanAndCommands || !audit.RecordHashesAndTests || !audit.RecordProvenance ||
            !audit.RecordRollbackId || !audit.RedactSensitiveValues)
            diagnostics.Add(new(AuditInvalidCode,
                "AI audit must record identity, plan, commands, hashes, tests, provenance and rollback while redacting sensitive values.",
                $"{root}/audit"));

        var privacy = governance.BudgetPrivacy;
        var providers = privacy.Providers ?? [];
        if (!privacy.RedactSensitiveValues || !privacy.ProhibitSecretLogging ||
            privacy.SensitiveArgumentKeys.Count == 0 || privacy.SensitiveArgumentKeys.Any(string.IsNullOrWhiteSpace) ||
            providers.Count == 0 || providers.GroupBy(item => item.Id).Any(group => group.Count() > 1) ||
            providers.GroupBy(item => item.ProviderKey, StringComparer.Ordinal).Any(group => group.Count() > 1) ||
            providers.Any(item => item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.ProviderKey) ||
                string.IsNullOrWhiteSpace(item.Provenance) || item.MaximumInputTokens is < 1 or > 1_000_000 ||
                item.MaximumOutputTokens is < 1 or > 1_000_000 || item.MaximumCostPerRequest < 0))
            diagnostics.Add(new(BudgetPrivacyInvalidCode,
                "Budget/privacy requires provider policies, token and cost caps, redaction and secret-log prohibition.",
                $"{root}/budgetPrivacy"));
        return diagnostics;
    }

    public static AgentExecutionAuthorizationResult Authorize(
        AgentSemanticWorkflow workflow,
        StructuredChangePlan plan,
        AgentExecutionAuthorizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(request);
        var governance = workflow.EffectiveGovernance;
        var diagnostics = Validate(governance, workflow).ToList();
        var root = $"assistant/changePlans/{plan.Id:D}";
        var authorization = governance.Authorization;
        if (plan.Permission is AssistantPermissionLevel.ReadOnly or AssistantPermissionLevel.Propose or AssistantPermissionLevel.Configure ||
            plan.Permission > workflow.MaximumPermission ||
            plan.Permission == AssistantPermissionLevel.ApplyApprovedPlan && (!request.HumanConfirmed || !authorization.RequireHumanConfirmation) ||
            plan.Permission == AssistantPermissionLevel.BoundedAutonomous && !authorization.AllowBoundedAutonomy)
            diagnostics.Add(new(PermissionDeniedCode,
                "Mutation requires a confirmed approved plan or explicitly enabled bounded autonomy.", $"{root}/permission"));
        if (plan.Commands.Count > Math.Min(authorization.MaximumCommands, governance.Transactions.MaximumCommands) ||
            request.DurationSeconds is < 0 || request.DurationSeconds > authorization.MaximumDurationSeconds ||
            plan.Commands.Any(command => !authorization.AllowedTools.Contains(command.ToolName, StringComparer.Ordinal)) ||
            plan.Commands.Any(command =>
            {
                var tool = workflow.Tools.SingleOrDefault(item => string.Equals(item.Name, command.ToolName, StringComparison.Ordinal));
                return tool is null || !authorization.AllowedTargetKinds.Contains(tool.TargetKind, StringComparer.Ordinal);
            }) || request.DocumentPaths.Any(path => !IsAllowedPath(path, authorization.AllowedPathPrefixes)))
            diagnostics.Add(new(ScopeDeniedCode,
                "Agent tool, target, count, path or duration exceeds the configured bounded scope.", root));

        var provider = governance.BudgetPrivacy.Providers.SingleOrDefault(item =>
            string.Equals(item.ProviderKey, request.ProviderKey, StringComparison.Ordinal));
        if (provider is null || request.InputTokens is < 0 || request.OutputTokens is < 0 || request.EstimatedCost < 0)
            diagnostics.Add(new(BudgetExceededCode, "Provider is unknown or its token/cost budget is exceeded.",
                $"{root}/budget"));
        else
        {
            if (request.InputTokens > provider.MaximumInputTokens || request.OutputTokens > provider.MaximumOutputTokens ||
                request.EstimatedCost > provider.MaximumCostPerRequest)
                diagnostics.Add(new(BudgetExceededCode, "Provider token or cost budget is exceeded.", $"{root}/budget"));
            if (governance.BudgetPrivacy.ProjectClassification > provider.MaximumClassification ||
                provider.Cloud && (!governance.BudgetPrivacy.CloudProjectContentOptIn || !request.ProjectContentOptIn) ||
                request.ProjectContentOptIn && !provider.AllowProjectContent)
                diagnostics.Add(new(PrivacyDeniedCode,
                    "Project classification or explicit content opt-in forbids this provider request.",
                    $"{root}/privacy"));
        }
        return new(diagnostics.Count == 0, diagnostics);
    }

    public static bool IsSensitive(string key, AgentBudgetPrivacyPolicy privacy) =>
        privacy.SensitiveArgumentKeys.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static bool IsAllowedPath(string path, IReadOnlyList<string> prefixes)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains("..", StringComparison.Ordinal))
            return false;
        var normalized = path.Replace('\\', '/');
        return prefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.Ordinal));
    }
}
