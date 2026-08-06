using Forge.Application.Commands;
using Forge.Domain.Factions;

namespace Forge.Mcp.Factions;

public sealed record FactionResource(
    int SchemaVersion,
    long ProjectRevision,
    string? Filter,
    string? AdvancedProfileId,
    bool ServerAuthoritative,
    IReadOnlyList<FactionResourceItem> Items);

public sealed record FactionResourceItem(
    string FactionId,
    string Slug,
    string DisplayName,
    IReadOnlyList<string> ParentIds,
    IReadOnlyList<string> EffectiveTags,
    IReadOnlyDictionary<string, string> EffectivePolicies,
    int MembershipCount,
    bool HasWitnessPolicy,
    bool Dynamic,
    string? DynamicKind,
    long DynamicRevision);

public sealed record FactionPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    FactionCatalog Catalog);

public sealed record FactionPlanResult(
    bool Succeeded,
    long BaseRevision,
    ProjectPatch? SemanticDiff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics);
