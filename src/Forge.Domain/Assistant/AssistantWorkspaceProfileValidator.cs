using System.Text.Json;
using System.Text.RegularExpressions;
using Forge.Domain.Validation;

namespace Forge.Domain.Assistant;

public static partial class AssistantWorkspaceProfileValidator
{
    public const string SchemaUnsupportedCode = "assistant.profile.schema_unsupported";
    public const string IdentityInvalidCode = "assistant.profile.identity_invalid";
    public const string ProviderInvalidCode = "assistant.profile.provider_invalid";
    public const string ActiveProviderInvalidCode = "assistant.profile.active_provider_invalid";
    public const string NetworkPolicyInvalidCode = "assistant.profile.network_policy_invalid";
    public const string PromptInvalidCode = "assistant.profile.prompt_invalid";
    public const string PromptSchemaInvalidCode = "assistant.profile.prompt_schema_invalid";
    public const string PromptTestInvalidCode = "assistant.profile.prompt_test_invalid";
    public const string GovernanceMissingCode = "assistant.profile.governance_missing";

    private static readonly Regex SafeKey = SafeKeyRegex();
    private static readonly Regex SafeEnvironmentVariable = SafeEnvironmentVariableRegex();
    private static readonly Regex SemanticVersion = SemanticVersionRegex();

    public static IReadOnlyList<DomainDiagnostic> Validate(AssistantWorkspaceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<DomainDiagnostic>();
        if (profile.SchemaVersion != AssistantWorkspaceProfile.CurrentSchemaVersion)
        {
            diagnostics.Add(new(SchemaUnsupportedCode, "Assistant profile schema is unsupported.", "assistant/schemaVersion"));
        }
        if (profile.Id == Guid.Empty || profile.ActiveProviderId == Guid.Empty)
        {
            diagnostics.Add(new(IdentityInvalidCode, "Assistant profile and active provider UUIDs are required.", "assistant/id"));
        }
        if (string.IsNullOrWhiteSpace(profile.Provenance))
        {
            diagnostics.Add(new(GovernanceMissingCode, "Assistant profile provenance is required.", "assistant/provenance"));
        }
        if (profile.Providers.Count == 0 ||
            profile.Providers.GroupBy(provider => provider.Id).Any(group => group.Count() > 1) ||
            profile.Providers.GroupBy(provider => provider.Key, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            diagnostics.Add(new(ProviderInvalidCode, "Assistant providers require unique UUIDs and keys.", "assistant/providers"));
        }
        foreach (var provider in profile.Providers)
        {
            ValidateProvider(provider, diagnostics);
        }
        var active = profile.Providers.FirstOrDefault(provider => provider.Id == profile.ActiveProviderId);
        if (active is null || !active.Enabled)
        {
            diagnostics.Add(new(ActiveProviderInvalidCode, "Active assistant provider must exist and be enabled.", "assistant/activeProviderId"));
        }
        else if (active.Kind != AssistantProviderKind.DeterministicOffline && !profile.NetworkAllowed)
        {
            diagnostics.Add(new(NetworkPolicyInvalidCode, "A network provider cannot be active while network access is disabled.", "assistant/networkAllowed"));
        }
        if (profile.Prompts.Count == 0 ||
            profile.Prompts.GroupBy(prompt => prompt.Id).Any(group => group.Count() > 1) ||
            profile.Prompts.GroupBy(prompt => (prompt.Key, prompt.Version)).Any(group => group.Count() > 1))
        {
            diagnostics.Add(new(PromptInvalidCode, "Versioned prompts require unique UUIDs and key/version pairs.", "assistant/prompts"));
        }
        foreach (var prompt in profile.Prompts)
        {
            ValidatePrompt(prompt, diagnostics);
        }
        return diagnostics;
    }

    private static void ValidateProvider(AssistantProviderDefinition provider, ICollection<DomainDiagnostic> diagnostics)
    {
        var root = $"assistant/providers/{provider.Id:D}";
        var endpointValid = provider.Kind == AssistantProviderKind.DeterministicOffline
            ? string.IsNullOrEmpty(provider.Endpoint) && string.IsNullOrEmpty(provider.CredentialEnvironmentVariable)
            : Uri.TryCreate(provider.Endpoint, UriKind.Absolute, out var endpoint) &&
              endpoint.Scheme is "http" or "https" &&
              (!provider.LocalOnly || endpoint.IsLoopback);
        if (provider.Id == Guid.Empty ||
            provider.SchemaVersion != AssistantProviderDefinition.CurrentSchemaVersion ||
            !SafeKey.IsMatch(provider.Key) ||
            string.IsNullOrWhiteSpace(provider.DisplayName) ||
            string.IsNullOrWhiteSpace(provider.Model) ||
            !endpointValid ||
            (!string.IsNullOrEmpty(provider.CredentialEnvironmentVariable) &&
             !SafeEnvironmentVariable.IsMatch(provider.CredentialEnvironmentVariable)) ||
            provider.MaximumInputTokens is < 256 or > 1_000_000 ||
            provider.MaximumOutputTokens is < 64 or > 128_000 ||
            provider.InputCostPerMillionTokens < 0 ||
            provider.OutputCostPerMillionTokens < 0 ||
            string.IsNullOrWhiteSpace(provider.Provenance))
        {
            diagnostics.Add(new(ProviderInvalidCode, "Assistant provider endpoint, model, budgets or governance are invalid.", root));
        }
    }

    private static void ValidatePrompt(AssistantPromptDefinition prompt, ICollection<DomainDiagnostic> diagnostics)
    {
        var root = $"assistant/prompts/{prompt.Id:D}";
        if (prompt.Id == Guid.Empty ||
            prompt.SchemaVersion != AssistantPromptDefinition.CurrentSchemaVersion ||
            !SafeKey.IsMatch(prompt.Key) ||
            !SemanticVersion.IsMatch(prompt.Version) ||
            string.IsNullOrWhiteSpace(prompt.DisplayName) ||
            string.IsNullOrWhiteSpace(prompt.SystemTemplate) ||
            string.IsNullOrWhiteSpace(prompt.UserTemplate) ||
            prompt.RequiredCapabilities.Count == 0 ||
            prompt.Changelog.Count == 0 ||
            prompt.Changelog.All(change => change.Version != prompt.Version) ||
            string.IsNullOrWhiteSpace(prompt.Provenance))
        {
            diagnostics.Add(new(PromptInvalidCode, "Prompt identity, version, templates, capabilities or changelog are invalid.", root));
        }
        try
        {
            using var schema = JsonDocument.Parse(prompt.ResponseJsonSchema);
            if (schema.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Response schema root must be an object.");
            }
        }
        catch (JsonException)
        {
            diagnostics.Add(new(PromptSchemaInvalidCode, "Prompt response schema must be valid JSON object metadata.", $"{root}/responseJsonSchema"));
        }
        if (prompt.Tests.Count == 0 ||
            prompt.Tests.Any(test => test.Id == Guid.Empty || string.IsNullOrWhiteSpace(test.Name) ||
                                     test.Variables.Count == 0 || test.ExpectedFragments.Count == 0) ||
            prompt.Tests.GroupBy(test => test.Id).Any(group => group.Count() > 1))
        {
            diagnostics.Add(new(PromptTestInvalidCode, "Prompt tests require unique UUIDs, variables and expected fragments.", $"{root}/tests"));
        }
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{1,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeKeyRegex();

    [GeneratedRegex("^[A-Z_][A-Z0-9_]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeEnvironmentVariableRegex();

    [GeneratedRegex("^[0-9]+\\.[0-9]+\\.[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SemanticVersionRegex();
}
