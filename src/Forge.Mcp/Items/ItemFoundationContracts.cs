using Forge.Application.Commands;
using Forge.Domain.Items;

namespace Forge.Mcp.Items;

public sealed record ItemFoundationResource(
    int SchemaVersion,
    long ProjectRevision,
    string? Filter,
    IReadOnlyList<ItemFoundationResourceItem> Items);

public sealed record ItemFoundationResourceItem(
    string BlueprintId,
    string BlueprintSlug,
    string DisplayName,
    string ProfileId,
    int ProfileSchemaVersion,
    string Provenance,
    bool RequiredAtRuntime,
    string TypeId,
    string TypeSlug,
    int DefinitionVersion,
    IReadOnlyList<string> Locales,
    IReadOnlyList<string> Slots,
    IReadOnlyList<string> AllowedInstanceOverrides);

public sealed record ItemFoundationPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string BlueprintId,
    ItemFoundationProfile Profile,
    bool AcceptTypeMigration = false);

public sealed record ItemFoundationPlanResult(
    bool Succeeded,
    long BaseRevision,
    ProjectPatch? SemanticDiff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics);
