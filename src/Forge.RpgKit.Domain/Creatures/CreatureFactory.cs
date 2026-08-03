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
        if (string.IsNullOrWhiteSpace(definition.Authoring.ArchetypeId) || string.IsNullOrWhiteSpace(definition.Authoring.RulesetId) ||
            string.IsNullOrWhiteSpace(definition.Authoring.RigId) || string.IsNullOrWhiteSpace(definition.Authoring.FactionId) ||
            string.IsNullOrWhiteSpace(definition.Authoring.VisualKitId) || definition.Authoring.ThreatRating < 0)
            throw new InvalidOperationException("RPG_CREATURE_AUTHORING_PROFILE_INVALID");
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

    public static IReadOnlyList<CreatureValidationDiagnostic> Diagnose(CreatureDefinition definition, CreatureValidationContext context)
    {
        var diagnostics = new List<CreatureValidationDiagnostic>();
        try { Validate(definition); } catch (InvalidOperationException exception) { diagnostics.Add(new(exception.Message, $"creatures/{definition.Id}", "Creature canonical data is invalid.")); }
        if (!context.AvailableRigs.Contains(definition.Authoring.RigId)) diagnostics.Add(new("RPG_CREATURE_RIG_MISSING", $"creatures/{definition.Id}/authoring/rigId", "Rig is not available."));
        if (!context.AvailableAppearances.Contains(definition.AppearanceId)) diagnostics.Add(new("RPG_CREATURE_APPEARANCE_MISSING", $"creatures/{definition.Id}/appearanceId", "Appearance is not available."));
        if (!context.AvailableCombatProfiles.Contains(definition.CombatProfileId)) diagnostics.Add(new("RPG_CREATURE_COMBAT_PROFILE_MISSING", $"creatures/{definition.Id}/combatProfileId", "Combat profile is not available."));
        if (!context.AvailableInventoryProfiles.Contains(definition.InventoryProfileId)) diagnostics.Add(new("RPG_CREATURE_INVENTORY_PROFILE_MISSING", $"creatures/{definition.Id}/inventoryProfileId", "Inventory profile is not available."));
        if (!context.AvailableFactions.Contains(definition.Authoring.FactionId)) diagnostics.Add(new("RPG_CREATURE_FACTION_MISSING", $"creatures/{definition.Id}/authoring/factionId", "Faction is not available."));
        foreach (var binding in new[] { definition.Scripts.OnSpawn }.Concat(definition.Scripts.Events.Values).Where(value => !string.IsNullOrWhiteSpace(value)))
            if (!context.AvailableScripts.Contains(binding!)) diagnostics.Add(new("RPG_CREATURE_SCRIPT_MISSING", $"creatures/{definition.Id}/scripts", $"Script '{binding}' is not available."));
        if (definition.Resources.Count > context.MaximumResources) diagnostics.Add(new("RPG_CREATURE_RESOURCE_BUDGET_EXCEEDED", $"creatures/{definition.Id}/resources", "Resource pool budget is exceeded."));
        if (definition.Authoring.ThreatRating > context.MaximumThreatRating) diagnostics.Add(new("RPG_CREATURE_THREAT_BUDGET_EXCEEDED", $"creatures/{definition.Id}/authoring/threatRating", "Threat budget is exceeded."));
        return diagnostics;
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
