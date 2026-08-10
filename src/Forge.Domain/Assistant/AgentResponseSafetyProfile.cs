using Forge.Domain.Validation;

namespace Forge.Domain.Assistant;

public sealed record AgentStructuredOutputPolicy(
    bool RequireJsonSchema,
    bool RequireToolCallsForMutation,
    bool AllowBoundedSyntaxRepair,
    bool RejectAmbiguousMutation,
    bool RejectUnknownProperties,
    int MaximumResponseCharacters,
    int MaximumToolCalls);

public sealed record AgentPromptInjectionPolicy(
    bool TreatProjectContentAsData,
    bool RequireProvenanceLabels,
    bool EnforceToolAllowlist,
    bool ProhibitHiddenEscalation,
    bool RequireRiskConfirmation,
    int MaximumUntrustedCharacters,
    IReadOnlyList<string> SuspiciousInstructionPatterns);

public sealed record AgentOfflineModePolicy(
    bool ManualAuthoringAvailable,
    bool LocalProvidersOptional,
    bool AiMetadataOptional,
    bool BuildRuntimeIndependent,
    bool NetworkDisabledByDefault,
    bool DeterministicProviderAvailable);

public sealed record AgentResponseSafetyProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    AgentStructuredOutputPolicy StructuredOutput,
    AgentPromptInjectionPolicy PromptInjection,
    AgentOfflineModePolicy Offline)
{
    public const int CurrentSchemaVersion = 1;
    public static readonly Guid DefaultId = Guid.Parse("a2200000-0000-4000-8000-000000000001");

    public static AgentResponseSafetyProfile CreateDefault() => new(
        DefaultId,
        CurrentSchemaVersion,
        "openrpg-forge:r022-agent-safety",
        new(true, true, true, true, true, 262_144, 50),
        new(
            true,
            true,
            true,
            true,
            true,
            65_536,
            [
                "ignore previous",
                "ignore all previous",
                "system prompt",
                "developer message",
                "reveal secret",
                "call shell",
                "execute python",
                "bypass permission",
            ]),
        new(true, true, true, true, true, true));
}

public static class AgentResponseSafetyProfileValidator
{
    public const string IdentityInvalidCode = "assistant.safety.identity_invalid";
    public const string SchemaUnsupportedCode = "assistant.safety.schema_unsupported";
    public const string StructuredOutputInvalidCode = "assistant.safety.structured_output_invalid";
    public const string PromptInjectionInvalidCode = "assistant.safety.prompt_injection_invalid";
    public const string OfflineInvalidCode = "assistant.safety.offline_invalid";

    public static IReadOnlyList<DomainDiagnostic> Validate(AgentResponseSafetyProfile safety)
    {
        ArgumentNullException.ThrowIfNull(safety);
        var root = $"assistant/safety/{safety.Id:D}";
        var diagnostics = new List<DomainDiagnostic>();
        if (safety.Id == Guid.Empty || string.IsNullOrWhiteSpace(safety.Provenance))
            diagnostics.Add(new(IdentityInvalidCode, "Agent safety UUID and provenance are required.", root));
        if (safety.SchemaVersion != AgentResponseSafetyProfile.CurrentSchemaVersion)
            diagnostics.Add(new(SchemaUnsupportedCode, "Agent safety schema must be 1.", $"{root}/schemaVersion"));
        var output = safety.StructuredOutput;
        if (!output.RequireJsonSchema || !output.RequireToolCallsForMutation || !output.AllowBoundedSyntaxRepair ||
            !output.RejectAmbiguousMutation || !output.RejectUnknownProperties ||
            output.MaximumResponseCharacters is < 1_024 or > 1_048_576 || output.MaximumToolCalls is < 1 or > 100)
            diagnostics.Add(new(StructuredOutputInvalidCode,
                "Structured output requires schema, bounded repair, unambiguous tool calls and response/tool limits.",
                $"{root}/structuredOutput"));
        var injection = safety.PromptInjection;
        if (!injection.TreatProjectContentAsData || !injection.RequireProvenanceLabels ||
            !injection.EnforceToolAllowlist || !injection.ProhibitHiddenEscalation ||
            !injection.RequireRiskConfirmation || injection.MaximumUntrustedCharacters is < 1_024 or > 1_048_576 ||
            injection.SuspiciousInstructionPatterns.Count == 0 ||
            injection.SuspiciousInstructionPatterns.Any(string.IsNullOrWhiteSpace) ||
            injection.SuspiciousInstructionPatterns.Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
            injection.SuspiciousInstructionPatterns.Count)
            diagnostics.Add(new(PromptInjectionInvalidCode,
                "Prompt safety must isolate labeled data, enforce tools/permission/confirmation and bound hostile content.",
                $"{root}/promptInjection"));
        var offline = safety.Offline;
        if (!offline.ManualAuthoringAvailable || !offline.LocalProvidersOptional || !offline.AiMetadataOptional ||
            !offline.BuildRuntimeIndependent || !offline.NetworkDisabledByDefault ||
            !offline.DeterministicProviderAvailable)
            diagnostics.Add(new(OfflineInvalidCode,
                "Offline mode must preserve manual authoring, optional local AI and independent build/runtime.",
                $"{root}/offline"));
        return diagnostics;
    }
}
