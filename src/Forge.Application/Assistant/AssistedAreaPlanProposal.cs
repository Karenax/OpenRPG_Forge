using Forge.Application.Commands;
using Forge.Domain.Assistant;

namespace Forge.Application.Assistant;

public sealed record AssistedAreaPlanProposal(
    bool Succeeded,
    AssistedAreaPlanDefinition? Plan,
    string SemanticDiff,
    IReadOnlyList<CommandDiagnostic> Diagnostics,
    string PromptSha256,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCostUsd,
    string ProviderProvenance);
