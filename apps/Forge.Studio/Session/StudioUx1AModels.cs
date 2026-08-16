using Forge.Application.Areas;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;

namespace Forge.Studio.Session;

internal sealed record StudioAreaCreationDraft(
    AreaId AreaId,
    EntityId SpawnEntityId,
    string DisplayName,
    AreaKind Kind,
    int Width,
    int Depth,
    AreaTemplateKind Template,
    BlueprintId? SpawnBlueprintId,
    string KitKey,
    string TerrainProfile,
    string LightingProfile,
    string NavigationProfile,
    string StreamingProfile,
    string Biome,
    int Seed,
    int RuntimeBudgetMegabytes);

internal sealed record StudioAreaCreationPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    AreaDefinition? Candidate = null);

internal sealed record StudioAreaCreationResult(
    bool Succeeded,
    string Message,
    AreaId? AreaId = null);

internal sealed record StudioAreaPresetChoice(string Label, string Value);

internal sealed record StudioAreaSpawnChoice(BlueprintId Id, string Label);

internal sealed record StudioUx1ASmokeResult(
    bool Succeeded,
    bool WizardPreview,
    bool EmptyTemplate,
    bool PlayableTemplate,
    bool FoundationProfile,
    bool DryRun,
    bool InvalidBlocked,
    bool AtomicUndoRedo,
    bool IndependentDocument,
    bool PersistenceRoundTrip,
    bool StableIdentity,
    bool NeighborsPreserved,
    bool HumanDiagnostics);
