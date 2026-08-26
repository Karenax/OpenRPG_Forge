using Forge.Application.Commands;
using Forge.Domain.Modules;
using Forge.Domain.Projects;

namespace Forge.Mcp.Projects;

public sealed record ProjectFoundationResource(
    int SchemaVersion,
    long Revision,
    ProjectMetadata Metadata,
    ProjectFoundationProfile? Foundation,
    ProjectTechnicalIdentity? Identity);

public sealed record ProjectFoundationPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    ProjectFoundationProfile? Foundation = null,
    ModuleMetadataPatch? MetadataPatch = null);

public sealed record ProjectFoundationPlanResult(
    bool Succeeded,
    string? PlanId,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<ProjectFoundationAuditEntry> Audit)
{
    public IReadOnlyList<string> ChangedPaths { get; init; } = [];
    public IReadOnlyList<ModuleMetadataLocalizationReview> LocalizationReview { get; init; } = [];
}

public sealed record ModuleMetadataLocalizationReview(
    string MessageKey,
    string Locale,
    string Text,
    int EstimatedCharacters,
    int CharacterBudget,
    bool OverflowRisk,
    string PseudoText,
    int PseudoEstimatedCharacters,
    bool PseudoOverflowRisk,
    IReadOnlyList<string> QualityCodes);

public sealed record ProjectFoundationAuditEntry(
    string PlanId,
    string Action,
    long Revision,
    DateTimeOffset OccurredAt,
    bool Succeeded);
