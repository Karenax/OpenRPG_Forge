using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;

namespace Forge.RpgKit.Domain.Creatures;

public enum CreatureMovementMode { Grounded, Flying, Swimming, Climbing, Vehicle, ZeroG }
public enum CreatureSpawnAuthority { Local, Host, Server }
public enum EncounterRole { Minion, Skirmisher, Defender, Controller, Support, Boss, NonCombatant }
public enum CreatureInitialLifeState { Alive, Unconscious, Dead, Sleeping, Seated, Frozen }
public enum AttributeFormulaOperation { Sum, Product, Ratio }
public enum SenseKind { Vision, Hearing, Smell, Magic }

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

public sealed record TraitSet(string Id, IReadOnlySet<string> Traits);
public sealed record SpeciesDefinition(string Id, IReadOnlyList<string> ParentSpeciesIds, IReadOnlyList<TraitSet> TraitSets, IReadOnlyList<string> Variants, string RequiredSkeletonId);
public sealed record CreatureVisualProfile(string ArchetypeId, string MeshId, string RigId, IReadOnlyDictionary<string, double> Morphs, IReadOnlyDictionary<string, string> BodySlots, IReadOnlyList<string> Materials, IReadOnlyList<string> Palettes, IReadOnlyList<string> Attachments, string PresetId);
public sealed record AnimationGraphProfile(string Id, IReadOnlyList<string> LocomotionStates, IReadOnlyList<string> CombatStances, IReadOnlyList<string> Emotes, IReadOnlyList<string> AdditiveLayers, string RetargetProfileId);
public sealed record CreatureInitialState(CreatureInitialLifeState LifeState, string? AnimationLoop, string? PoseId, bool Invisible);
public sealed record AttributeModifier(string Source, double Value);
public sealed record DerivedAttributeFormula(string AttributeId, AttributeFormulaOperation Operation, IReadOnlyList<string> Dependencies, double Constant);
public sealed record AttributeSet(IReadOnlyDictionary<string, double> BaseValues, IReadOnlyDictionary<string, IReadOnlyList<AttributeModifier>> Modifiers, IReadOnlyList<DerivedAttributeFormula> Derived);
public sealed record DefenseModel(double Avoidance, double Armor, IReadOnlyDictionary<string, double> Resistances, IReadOnlyDictionary<string, double> Saves, IReadOnlySet<string> Immunities, double FlatMitigation);
public sealed record CreatureAbilityInstance(string AbilityId, int Level, int Charges, double CooldownSeconds, IReadOnlyList<string> Conditions, IReadOnlySet<string> AiTags, string ScriptId);
public sealed record InventoryContainer(string Id, int SlotCount, double MaximumWeight, double MaximumVolume, IReadOnlyList<string> ItemIds);
public sealed record CreatureInventory(IReadOnlyList<InventoryContainer> Containers, IReadOnlyDictionary<string, string> EquipmentSlots, string OwnerId, bool Persistent);
public sealed record FactionMembership(string FactionId, double Weight, string Rank, double PersonalReputation, bool Inherited);
public sealed record CreatureFactionProfile(string PrimaryFactionId, IReadOnlyList<FactionMembership> Memberships);
public sealed record SenseProfile(SenseKind Kind, double Range, double ConeDegrees, bool UsesOcclusion, IReadOnlySet<string> StimulusTags);
public sealed record CreaturePerceptionProfile(IReadOnlyList<SenseProfile> Senses, double MemorySeconds);

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
    CreatureScriptBindings Scripts,
    SpeciesDefinition? Species = null,
    CreatureVisualProfile? Visual = null,
    AnimationGraphProfile? Animations = null,
    CreatureInitialState? InitialState = null,
    AttributeSet? Attributes = null,
    DefenseModel? Defenses = null,
    IReadOnlyList<CreatureAbilityInstance>? Abilities = null,
    CreatureInventory? Inventory = null,
    CreatureFactionProfile? Factions = null,
    CreaturePerceptionProfile? Perception = null);

public sealed record Stimulus(string Id, string Tag, Forge.Domain.Spatial.Vector3Value Position, double Intensity);
public sealed record PerceptionResult(string StimulusId, SenseKind Sense, double Confidence, Forge.Domain.Spatial.Vector3Value Position, double RememberUntilSeconds);

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
