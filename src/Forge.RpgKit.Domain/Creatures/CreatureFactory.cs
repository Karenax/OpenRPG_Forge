namespace Forge.RpgKit.Domain.Creatures;

public static class CreatureFactory
{
    public static CreatureDefinition Validate(CreatureDefinition definition)
    {
        if (definition.Id == Guid.Empty) throw new InvalidOperationException("RPG_CREATURE_UUID_REQUIRED");
        if (definition.SchemaVersion != 1) throw new InvalidOperationException("RPG_CREATURE_SCHEMA_UNSUPPORTED");
        if (string.IsNullOrWhiteSpace(definition.Provenance)) throw new InvalidOperationException("RPG_CREATURE_PROVENANCE_REQUIRED");
        if (string.IsNullOrWhiteSpace(definition.Identity.Slug) || string.IsNullOrWhiteSpace(definition.Identity.Name.DefaultText))
            throw new InvalidOperationException("RPG_CREATURE_IDENTITY_INVALID");
        if (definition.Resources.Count == 0 || definition.Resources.Select(pool => pool.Id).Distinct(StringComparer.Ordinal).Count() != definition.Resources.Count ||
            definition.Resources.Any(pool => string.IsNullOrWhiteSpace(pool.Id) || pool.Maximum <= 0 || pool.Current < 0 || pool.Current > pool.Maximum || pool.RegenerationPerSecond < 0))
            throw new InvalidOperationException("RPG_CREATURE_RESOURCES_INVALID");
        if (definition.Movement.MaximumSpeed <= 0 || definition.Movement.Acceleration <= 0 || definition.Movement.Deceleration < 0 ||
            definition.Movement.MaximumAltitude < definition.Movement.MinimumAltitude || definition.Movement.MaximumSlopeDegrees is < 0 or > 90)
            throw new InvalidOperationException("RPG_CREATURE_MOVEMENT_INVALID");
        if (definition.Movement.Mode == CreatureMovementMode.Flying && definition.Movement.MaximumAltitude <= definition.Movement.MinimumAltitude)
            throw new InvalidOperationException("RPG_CREATURE_FLIGHT_ALTITUDE_INVALID");
        if (string.IsNullOrWhiteSpace(definition.AppearanceId) || string.IsNullOrWhiteSpace(definition.PerceptionProfileId) ||
            string.IsNullOrWhiteSpace(definition.CombatProfileId) || string.IsNullOrWhiteSpace(definition.InventoryProfileId))
            throw new InvalidOperationException("RPG_CREATURE_COMPONENT_REQUIRED");
        return definition;
    }

    public static CreatureSpawned Spawn(CreatureDefinition definition, CreatureSpawnContext context, Func<Guid> eventIdSource)
    {
        Validate(definition);
        if (context.SpawnId == Guid.Empty || string.IsNullOrWhiteSpace(context.AreaId) || string.IsNullOrWhiteSpace(context.Cause))
            throw new InvalidOperationException("RPG_CREATURE_SPAWN_CONTEXT_INVALID");
        var eventId = eventIdSource();
        if (eventId == Guid.Empty) throw new InvalidOperationException("RPG_CREATURE_SPAWN_EVENT_UUID_REQUIRED");
        return new CreatureSpawned(eventId, definition.Id, definition.BlueprintId, context,
            definition.Resources.ToDictionary(pool => pool.Id, pool => pool.Current, StringComparer.Ordinal));
    }
}
