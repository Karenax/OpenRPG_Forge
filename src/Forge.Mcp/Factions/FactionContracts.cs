using Forge.Application.Commands;
using Forge.Domain.Factions;

namespace Forge.Mcp.Factions;

public sealed record FactionResource(
    int SchemaVersion,
    long ProjectRevision,
    string? Filter,
    string? AdvancedProfileId,
    bool ServerAuthoritative,
    IReadOnlyList<FactionResourceItem> Items,
    IReadOnlyList<FactionRelationshipResource>? Relationships = null,
    IReadOnlyList<FactionDispositionBandResource>? Bands = null,
    IReadOnlyList<FactionPersonalReputationResource>? PersonalReputation = null,
    string? ReputationProfileId = null,
    bool ReputationRequiredAtRuntime = false,
    IReadOnlyList<FactionGlobalReputationResource>? GlobalReputation = null,
    IReadOnlyList<FactionHeatmapSnapshotResource>? HeatmapHistory = null,
    FactionReactionPolicyResource? Reaction = null);

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

public sealed record FactionRelationshipResource(
    string SourceFactionId,
    string TargetFactionId,
    double Score,
    string Disposition,
    string Preset,
    string Explanation);

public sealed record FactionDispositionBandResource(
    string Id,
    double Minimum,
    double Maximum,
    int Priority);

public sealed record FactionPersonalReputationResource(
    string Id,
    string SubjectKind,
    string SubjectId,
    string SourceFactionId,
    string TargetFactionId,
    double Delta,
    string? ExpiresAt,
    string Cause);

public sealed record FactionGlobalReputationResource(
    string Id,
    string Scope,
    string ScopeId,
    string SourceFactionId,
    string TargetFactionId,
    double Delta,
    string Cause,
    string OccurredAt);

public sealed record FactionHeatmapSnapshotResource(
    string Id,
    string Label,
    string CapturedAt,
    IReadOnlyList<FactionRelationshipResource> Relationships);

public sealed record FactionReactionPolicyResource(
    string Id,
    string DefaultScope,
    string DefaultScopeId,
    bool ApplyDisguise,
    bool ApplyCrimes,
    bool ApplyDialogue,
    bool ApplyZoneRules,
    bool AllowGameMasterOverride);

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
