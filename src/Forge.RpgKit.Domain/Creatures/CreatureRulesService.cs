namespace Forge.RpgKit.Domain.Creatures;

public static class CreatureRulesService
{
    public static IReadOnlyList<CreatureValidationDiagnostic> Validate(CreatureDefinition definition)
    {
        var root = $"creatures/{definition.Id}"; var diagnostics = new List<CreatureValidationDiagnostic>();
        if (definition.Defenses is null || definition.Defenses.Avoidance < 0 || definition.Defenses.Armor < 0 || definition.Defenses.FlatMitigation < 0 || definition.Defenses.Resistances.Values.Any(value => value is < 0 or > 1)) diagnostics.Add(new("RPG_CREATURE_DEFENSE_INVALID", $"{root}/defenses", "Defense model is invalid."));
        if (definition.Abilities is null || definition.Abilities.Select(item => item.AbilityId).Distinct().Count() != definition.Abilities.Count || definition.Abilities.Any(item => string.IsNullOrWhiteSpace(item.AbilityId) || item.Level < 0 || item.Charges < 0 || item.CooldownSeconds < 0 || string.IsNullOrWhiteSpace(item.ScriptId))) diagnostics.Add(new("RPG_CREATURE_ABILITIES_INVALID", $"{root}/abilities", "Ability instances are invalid."));
        if (definition.Inventory is null || string.IsNullOrWhiteSpace(definition.Inventory.OwnerId) || definition.Inventory.Containers.Select(item => item.Id).Distinct().Count() != definition.Inventory.Containers.Count || definition.Inventory.Containers.Any(item => item.SlotCount < item.ItemIds.Count || item.MaximumWeight < 0 || item.MaximumVolume < 0)) diagnostics.Add(new("RPG_CREATURE_INVENTORY_INVALID", $"{root}/inventory", "Inventory containers or equipment slots are invalid."));
        if (definition.Factions is null || string.IsNullOrWhiteSpace(definition.Factions.PrimaryFactionId) || !definition.Factions.Memberships.Any(item => item.FactionId == definition.Factions.PrimaryFactionId) || definition.Factions.Memberships.Any(item => item.Weight is < 0 or > 1)) diagnostics.Add(new("RPG_CREATURE_FACTION_PROFILE_INVALID", $"{root}/factions", "Faction memberships are invalid."));
        if (definition.Perception is null || definition.Perception.MemorySeconds < 0 || definition.Perception.Senses.Select(item => item.Kind).Distinct().Count() != definition.Perception.Senses.Count || definition.Perception.Senses.Any(item => item.Range <= 0 || item.ConeDegrees is <= 0 or > 360)) diagnostics.Add(new("RPG_CREATURE_PERCEPTION_INVALID", $"{root}/perception", "Sense profiles are invalid."));
        return diagnostics;
    }

    public static double Mitigate(DefenseModel defense, string damageType, double incoming)
    {
        if (defense.Immunities.Contains(damageType)) return 0;
        var resistance = defense.Resistances.GetValueOrDefault(damageType);
        return Math.Max(0, incoming * (1 - resistance) - defense.Armor - defense.FlatMitigation);
    }

    public static IReadOnlyList<PerceptionResult> Detect(CreaturePerceptionProfile profile, Forge.Domain.Spatial.Vector3Value origin, IReadOnlyList<Stimulus> stimuli, double nowSeconds)
    {
        var results = new List<PerceptionResult>();
        foreach (var stimulus in stimuli)
            foreach (var sense in profile.Senses.Where(item => item.StimulusTags.Contains(stimulus.Tag)))
            {
                var dx = stimulus.Position.X - origin.X; var dy = stimulus.Position.Y - origin.Y; var dz = stimulus.Position.Z - origin.Z;
                var distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (distance <= sense.Range) results.Add(new(stimulus.Id, sense.Kind, Math.Clamp(stimulus.Intensity * (1 - distance / sense.Range), 0, 1), stimulus.Position, nowSeconds + profile.MemorySeconds));
            }
        return results.OrderByDescending(item => item.Confidence).ThenBy(item => item.StimulusId, StringComparer.Ordinal).ToArray();
    }
}
