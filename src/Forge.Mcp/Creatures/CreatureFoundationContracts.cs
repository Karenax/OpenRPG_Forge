using Forge.Application.Commands;
using Forge.Domain.Creatures;

namespace Forge.Mcp.Creatures;

public sealed record CreatureFoundationResource(
    int SchemaVersion,
    long ProjectRevision,
    string? Filter,
    IReadOnlyList<CreatureFoundationResourceItem> Items);

public sealed record CreatureFoundationResourceItem(
    string BlueprintId,
    string BlueprintSlug,
    string DisplayName,
    string ProfileId,
    int ProfileSchemaVersion,
    string Provenance,
    bool RequiredAtRuntime,
    string MovementMode,
    IReadOnlyDictionary<string, double> ResourceState,
    string SpawnEvent,
    string SpawnHandler,
    bool AuthoringCompositionRequiredAtRuntime = false,
    string? EncounterRole = null,
    double? ThreatRating = null,
    string? SpeciesId = null,
    string? MeshAssetId = null,
    string? AnimationGraphId = null,
    int AnimationStateCount = 0,
    bool RulesCompositionRequiredAtRuntime = false,
    string? InitialLifeState = null,
    string? PoseId = null,
    int AttributeCount = 0,
    double? Avoidance = null,
    double? Armor = null,
    int AbilityCount = 0,
    int InventoryItemCount = 0,
    bool InteractionCompositionRequiredAtRuntime = false,
    string? PrimaryFactionId = null,
    int FactionMembershipCount = 0,
    int PerceptionSenseCount = 0,
    int DialogueBindingCount = 0,
    bool Invulnerable = false,
    bool UsesNarrativeDeath = false,
    bool DialogueInterruptible = true,
    string? OnConversationHandler = null,
    bool EventAiCompositionRequiredAtRuntime = false,
    string? AiControllerKind = null,
    string? AiControllerDefinitionId = null,
    int AiBlackboardCount = 0,
    int AiAllowedActionCount = 0,
    IReadOnlyList<string>? MovementRecoveryOrder = null,
    string? OnPerceptionDetectedHandler = null,
    string? OnPerceptionLostHandler = null,
    string? BeforePhysicalAttackedHandler = null,
    string? OnPhysicalAttackedHandler = null,
    string? BeforeAbilityTargetedHandler = null,
    string? OnSpellCastAtHandler = null,
    string? OnBlockedHandler = null);

public sealed record CreatureFoundationPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string BlueprintId,
    CreatureFoundationProfile Profile);

public sealed record CreatureFoundationPlanResult(
    bool Succeeded,
    long BaseRevision,
    ProjectPatch? SemanticDiff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics);
