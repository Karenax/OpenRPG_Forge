using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;

namespace Forge.RpgKit.Domain.Creatures;

public enum CreatureMovementMode { Grounded, Flying, Swimming, Climbing, Vehicle, ZeroG }
public enum CreatureSpawnAuthority { Local, Host, Server }
public enum EncounterRole { Minion, Skirmisher, Defender, Controller, Support, Boss, NonCombatant }

public sealed record LocalizedCreatureText(string DefaultText, IReadOnlyDictionary<string, string> Translations);

public sealed record CreatureIdentity(
    string Slug,
    LocalizedCreatureText Name,
    IReadOnlyList<string> Aliases,
    string Pronouns,
    LocalizedCreatureText Biography,
    string AuthorNotes,
    IReadOnlySet<string> NarrativeTags);

public sealed record ResourcePool(string Id, double Maximum, double Current, double RegenerationPerSecond, bool Persistent);

public sealed record CreatureMovementProfile(
    CreatureMovementMode Mode,
    double MaximumSpeed,
    double Acceleration,
    double Deceleration,
    double MinimumAltitude,
    double MaximumAltitude,
    double MaximumSlopeDegrees);

public sealed record CreatureScriptBindings(
    string? OnSpawn,
    IReadOnlyDictionary<string, string> Events);

public sealed record CreatureAuthoringProfile(
    string ArchetypeId,
    string RulesetId,
    string RigId,
    string FactionId,
    EncounterRole Role,
    double ThreatRating,
    string VisualKitId,
    IReadOnlySet<string> CombatTags);

public sealed record CreatureDefinition(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    BlueprintId BlueprintId,
    CreatureIdentity Identity,
    CreatureAuthoringProfile Authoring,
    TransformValue DefaultTransform,
    IReadOnlyList<ResourcePool> Resources,
    CreatureMovementProfile Movement,
    string AppearanceId,
    string PerceptionProfileId,
    string CombatProfileId,
    string? DialogueId,
    string InventoryProfileId,
    CreatureScriptBindings Scripts);

public sealed record CreatureSpawnContext(
    Guid SpawnId,
    string AreaId,
    TransformValue Transform,
    int Seed,
    CreatureSpawnAuthority Authority,
    string Cause);

public sealed record CreatureSpawned(
    Guid EventId,
    Guid CreatureDefinitionId,
    BlueprintId BlueprintId,
    CreatureSpawnContext Context,
    IReadOnlyDictionary<string, double> ResourceState);

public sealed record DamageRequest(Guid TransactionId, Guid TargetId, string? SourceId, double Amount, string DamageType, string Cause);
public sealed record DamageApplied(Guid EventId, Guid TransactionId, Guid TargetId, string? SourceId, double Requested, double Applied, double RemainingHealth, string DamageType, string Cause);
public sealed record EntityDied(Guid EventId, Guid TargetId, string? KillerId, IReadOnlyList<string> Contributors, string Cause, TransformValue Location, IReadOnlyList<string> LootIds, string Policy);
public sealed record CreatureValidationDiagnostic(string Code, string Path, string Message);
public sealed record CreatureValidationContext(
    IReadOnlySet<string> AvailableRigs,
    IReadOnlySet<string> AvailableAppearances,
    IReadOnlySet<string> AvailableCombatProfiles,
    IReadOnlySet<string> AvailableInventoryProfiles,
    IReadOnlySet<string> AvailableFactions,
    IReadOnlySet<string> AvailableScripts,
    int MaximumResources,
    double MaximumThreatRating);
