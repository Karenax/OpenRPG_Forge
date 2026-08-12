using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Assistant;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Forge.Application.Assistant;

public sealed class SetAssistantWorkspaceProfileHandler
{
    public const string RevisionConflictCode = "assistant.profile.revision_conflict";
    public const string PlanRequiredCode = "assistant.profile.plan_required";
    public const string PlanNotPreviewedCode = "assistant.profile.plan_not_previewed";
    public const string PermissionDeniedCode = "assistant.profile.permission_denied";
    private const string CommandName = "set_assistant_workspace_profile";
    private readonly IClock _clock;
    private readonly Dictionary<Guid, (AssistantWorkspaceProfileRevision Revision, string Fingerprint)> _previewedPlans = [];

    public SetAssistantWorkspaceProfileHandler(IClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public AssistantWorkspaceProfileCommandResult Handle(
        AssistantWorkspaceProfileState state,
        SetAssistantWorkspaceProfileCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
        {
            return Failure(state, command, RevisionConflictCode, "Assistant profile changed before this command was applied.");
        }
        if (command.PlanId == Guid.Empty)
        {
            return Failure(state, command, PlanRequiredCode, "Assistant configuration requires a non-empty preview plan ID.");
        }
        if (command.Permission < AssistantPermissionLevel.Configure ||
            command.Permission > state.Profile.MaximumPermission && !command.DryRun)
        {
            return Failure(state, command, PermissionDeniedCode, "Assistant configuration permission is insufficient.");
        }
        var diagnostics = AssistantWorkspaceProfileValidator.Validate(command.Profile)
            .Select(item => new CommandDiagnostic(item.Code, item.Message))
            .ToArray();
        if (diagnostics.Length > 0)
        {
            return new(false, state, command.BaseRevision, null, command.PlanId, diagnostics, null, null,
                Audit(command, false));
        }

        var patch = new ProjectPatch(
            "replace",
            "assistant/profile",
            $"Set assistant provider '{command.Profile.ActiveProvider.Key}' and {command.Profile.Prompts.Count} versioned prompt(s).");
        var inverse = new ProjectPatch(
            "replace",
            "assistant/profile",
            $"Restore assistant provider '{state.Profile.ActiveProvider.Key}'.");
        if (command.DryRun)
        {
            lock (_previewedPlans)
            {
                if (_previewedPlans.Count >= 128)
                {
                    _previewedPlans.Clear();
                }
                _previewedPlans[command.PlanId] = (command.BaseRevision, Fingerprint(command.Profile));
            }
            return new(true, state, command.BaseRevision, null, command.PlanId, [], patch, inverse,
                Audit(command, false));
        }

        lock (_previewedPlans)
        {
            if (!_previewedPlans.TryGetValue(command.PlanId, out var preview) ||
                preview.Revision != command.BaseRevision ||
                !string.Equals(preview.Fingerprint, Fingerprint(command.Profile), StringComparison.Ordinal))
            {
                return Failure(state, command, PlanNotPreviewedCode,
                    "Assistant configuration must match a successful dry-run plan at the current revision.");
            }
            _previewedPlans.Remove(command.PlanId);
        }

        var revision = state.Revision.Next();
        return new(true, new AssistantWorkspaceProfileState(command.Profile, revision), command.BaseRevision,
            revision, command.PlanId, [], patch, inverse, Audit(command, true));
    }

    private AssistantWorkspaceProfileCommandResult Failure(
        AssistantWorkspaceProfileState state,
        SetAssistantWorkspaceProfileCommand command,
        string code,
        string message) =>
        new(false, state, command.BaseRevision, null, command.PlanId,
            [new CommandDiagnostic(code, message)], null, null, Audit(command, false));

    private AuditEntry Audit(SetAssistantWorkspaceProfileCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);

    private static string Fingerprint(AssistantWorkspaceProfile profile)
    {
        var value = new StringBuilder()
            .Append(profile.Id).Append('|').Append(profile.SchemaVersion).Append('|')
            .Append(profile.ActiveProviderId).Append('|').Append(profile.MaximumPermission).Append('|')
            .Append(profile.NetworkAllowed).Append('|').Append(profile.ShareProjectContent).Append('|')
            .Append(profile.Provenance).Append('|').Append(profile.AgentWorkflow.Id).Append('|')
            .Append(profile.AgentWorkflow.SchemaVersion).Append('|').Append(profile.AgentWorkflow.Provenance).Append('|')
            .Append(profile.AgentWorkflow.MaximumPermission).Append('|')
            .Append(profile.AgentWorkflow.MaximumContextItems).Append('|')
            .Append(string.Join(',', profile.AgentWorkflow.ContextResources)).Append('|')
            .Append(profile.AgentWorkflow.References).Append('|').Append(profile.AgentWorkflow.Plans).Append('|')
            .Append(profile.AgentWorkflow.DryRun);
        var testing = profile.TestAutomation;
        value.Append("|X|").Append(testing.Id).Append('|').Append(testing.SchemaVersion).Append('|')
            .Append(testing.Provenance).Append('|').Append(testing.Enabled).Append('|')
            .Append(testing.RequiredAtRuntime).Append('|').Append(testing.MaximumScenarioSteps).Append('|')
            .Append(testing.MaximumDialoguePaths).Append('|').Append(testing.MaximumQuestPaths).Append('|')
            .Append(testing.DetectToneInconsistencies).Append('|').Append(testing.DetectRewardExploits).Append('|')
            .Append(testing.VerifyMultiplayerDeterminism).Append('|').Append(testing.VerifySaveResume).Append('|')
            .Append(string.Join(',', testing.AllowedGameplayActions));
        var combat = profile.CombatSimulation;
        value.Append("|C|").Append(combat.Id).Append('|').Append(combat.SchemaVersion).Append('|')
            .Append(combat.Provenance).Append('|').Append(combat.Enabled).Append('|')
            .Append(combat.RunsPerMatchup).Append('|').Append(combat.FirstSeed).Append('|')
            .Append(combat.MaximumRounds).Append('|').Append(combat.RoundSeconds.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(combat.IncludePathing).Append('|')
            .Append(combat.MovementMetersPerSecond.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(string.Join(',', combat.PhaseHealthThresholds.Select(item => item.ToString("R", CultureInfo.InvariantCulture)))).Append('|')
            .Append(string.Join(',', combat.ArchetypeBlueprintIds.Select(item => item.ToString())));
        foreach (var tool in profile.AgentWorkflow.Tools)
            value.Append("|W|").Append(tool.Id).Append('|').Append(tool.Name).Append('|')
                .Append(tool.DomainCommand).Append('|').Append(tool.TargetKind).Append('|')
                .Append(tool.RequiredPermission).Append('|').Append(tool.Provenance);
        var governance = profile.AgentWorkflow.EffectiveGovernance;
        value.Append("|G|").Append(governance.Id).Append('|').Append(governance.SchemaVersion).Append('|')
            .Append(governance.Provenance).Append('|').Append(governance.Transactions).Append('|')
            .Append(governance.SemanticDiff.StableOrdering).Append('|')
            .Append(governance.SemanticDiff.IncludeBeforeAfterHashes).Append('|')
            .Append(string.Join(',', governance.SemanticDiff.Categories)).Append('|')
            .Append(governance.Authorization.DenyByDefault).Append('|')
            .Append(governance.Authorization.RequireHumanConfirmation).Append('|')
            .Append(governance.Authorization.AllowBoundedAutonomy).Append('|')
            .Append(string.Join(',', governance.Authorization.AllowedTools)).Append('|')
            .Append(string.Join(',', governance.Authorization.AllowedTargetKinds)).Append('|')
            .Append(string.Join(',', governance.Authorization.AllowedPathPrefixes)).Append('|')
            .Append(governance.Authorization.MaximumCommands).Append('|')
            .Append(governance.Authorization.MaximumDurationSeconds).Append('|')
            .Append(governance.Audit).Append('|')
            .Append(governance.BudgetPrivacy.ProjectClassification).Append('|')
            .Append(governance.BudgetPrivacy.CloudProjectContentOptIn).Append('|')
            .Append(governance.BudgetPrivacy.RedactSensitiveValues).Append('|')
            .Append(governance.BudgetPrivacy.ProhibitSecretLogging).Append('|')
            .Append(string.Join(',', governance.BudgetPrivacy.SensitiveArgumentKeys));
        foreach (var providerPolicy in governance.BudgetPrivacy.Providers.OrderBy(item => item.ProviderKey, StringComparer.Ordinal))
            value.Append("|B|").Append(providerPolicy.Id).Append('|').Append(providerPolicy.ProviderKey).Append('|')
                .Append(providerPolicy.Cloud).Append('|').Append(providerPolicy.AllowProjectContent).Append('|')
                .Append(providerPolicy.MaximumClassification).Append('|').Append(providerPolicy.MaximumInputTokens).Append('|')
                .Append(providerPolicy.MaximumOutputTokens).Append('|')
                .Append(providerPolicy.MaximumCostPerRequest.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(providerPolicy.Provenance);
        var safety = profile.AgentWorkflow.EffectiveSafety;
        value.Append("|S|").Append(safety.Id).Append('|').Append(safety.SchemaVersion).Append('|')
            .Append(safety.Provenance).Append('|').Append(safety.StructuredOutput.RequireJsonSchema).Append('|')
            .Append(safety.StructuredOutput.RequireToolCallsForMutation).Append('|')
            .Append(safety.StructuredOutput.AllowBoundedSyntaxRepair).Append('|')
            .Append(safety.StructuredOutput.RejectAmbiguousMutation).Append('|')
            .Append(safety.StructuredOutput.RejectUnknownProperties).Append('|')
            .Append(safety.StructuredOutput.MaximumResponseCharacters).Append('|')
            .Append(safety.StructuredOutput.MaximumToolCalls).Append('|')
            .Append(safety.PromptInjection.TreatProjectContentAsData).Append('|')
            .Append(safety.PromptInjection.RequireProvenanceLabels).Append('|')
            .Append(safety.PromptInjection.EnforceToolAllowlist).Append('|')
            .Append(safety.PromptInjection.ProhibitHiddenEscalation).Append('|')
            .Append(safety.PromptInjection.RequireRiskConfirmation).Append('|')
            .Append(safety.PromptInjection.MaximumUntrustedCharacters).Append('|')
            .Append(string.Join(',', safety.PromptInjection.SuspiciousInstructionPatterns)).Append('|')
            .Append(safety.Offline);
        foreach (var provider in profile.Providers)
        {
            value.Append("|P|").Append(provider.Id).Append('|').Append(provider.SchemaVersion).Append('|')
                .Append(provider.Key).Append('|').Append(provider.DisplayName).Append('|').Append(provider.Kind).Append('|')
                .Append(provider.Endpoint).Append('|').Append(provider.Model).Append('|')
                .Append(provider.CredentialEnvironmentVariable).Append('|').Append(provider.Enabled).Append('|')
                .Append(provider.LocalOnly).Append('|').Append(provider.MaximumInputTokens).Append('|')
                .Append(provider.MaximumOutputTokens).Append('|')
                .Append(provider.InputCostPerMillionTokens.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(provider.OutputCostPerMillionTokens.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(provider.Provenance);
        }
        foreach (var prompt in profile.Prompts)
        {
            value.Append("|T|").Append(prompt.Id).Append('|').Append(prompt.SchemaVersion).Append('|')
                .Append(prompt.Key).Append('|').Append(prompt.Version).Append('|').Append(prompt.DisplayName).Append('|')
                .Append(prompt.SystemTemplate).Append('|').Append(prompt.UserTemplate).Append('|')
                .Append(prompt.ResponseJsonSchema).Append('|').Append(string.Join(',', prompt.RequiredCapabilities))
                .Append('|').Append(prompt.Provenance);
            foreach (var test in prompt.Tests)
            {
                value.Append("|E|").Append(test.Id).Append('|').Append(test.Name);
                foreach (var variable in test.Variables) value.Append('|').Append(variable.Key).Append('=').Append(variable.Value);
                value.Append('|').Append(string.Join(',', test.ExpectedFragments));
            }
            foreach (var change in prompt.Changelog) value.Append("|C|").Append(change.Version).Append('|').Append(change.Summary);
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.ToString()))).ToLowerInvariant();
    }
}
