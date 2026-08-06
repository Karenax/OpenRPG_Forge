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
            .Append(profile.Provenance);
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
