using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;

namespace Forge.RpgKit.Domain.Creatures;

public enum CreatureMovementMode { Grounded, Flying, Swimming, Climbing, Vehicle, ZeroG }
public enum CreatureSpawnAuthority { Local, Host, Server }

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

public sealed record CreatureDefinition(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    BlueprintId BlueprintId,
    CreatureIdentity Identity,
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
