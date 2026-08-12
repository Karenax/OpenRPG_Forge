namespace Forge.Domain.Creatures;

public static class CreatureRulesCompositionValidator
{
    public const string MissingRequiredCode = "creature.rules.required_missing";

    public static IReadOnlyList<CreatureFoundationDiagnostic> Validate(
        CreatureRulesCompositionProfile profile,
        string path = "creatureFoundation/rulesComposition")
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<CreatureFoundationDiagnostic>();
        Add(profile.Id == Guid.Empty, "creature.rules.uuid_required", path,
            "A stable creature-rules UUID is required.");
        Add(profile.SchemaVersion != CreatureRulesCompositionProfile.CurrentSchemaVersion,
            "creature.rules.schema_unsupported", path, "The creature-rules schema is unsupported.");
        Add(string.IsNullOrWhiteSpace(profile.Provenance), "creature.rules.provenance_required", path,
            "Creature-rules provenance is required.");
        Add(!Valid(profile.AssetId), "creature.rules.asset_invalid", $"{path}/assetId",
            "Creature rules require a canonical asset identity.");
        var componentIds = EnumerateComponentIds(profile).ToArray();
        Add(componentIds.Where(id => id != Guid.Empty).Distinct().Count() !=
            componentIds.Count(id => id != Guid.Empty), "creature.rules.component_uuid_duplicate", path,
            "Creature-rules profile, components, abilities, containers and entries require unique UUIDs.");

        var initial = profile.InitialState;
        Add(initial.Id == Guid.Empty || !Enum.IsDefined(initial.LifeState) ||
            initial.AnimationLoopAssetId is not null && !Valid(initial.AnimationLoopAssetId) ||
            initial.PoseId is not null && !Valid(initial.PoseId) ||
            initial.LifeState == CreatureRulesInitialLifeState.Dead && initial.PoseId is null,
            "creature.rules.initial_state_invalid", $"{path}/initialState/{initial.Id}",
            "Initial state requires a UUID, supported life state, compatible loop and a pose for dead creatures.");

        ValidateAttributes(profile.Attributes, path, diagnostics);
        ValidateDefenses(profile.Defenses, path, diagnostics);
        ValidateAbilities(profile.Abilities, path, diagnostics);
        ValidateInventory(profile.Inventory, path, diagnostics);
        Add(profile.Overrides.Any(item => !Valid(item.Key) || string.IsNullOrWhiteSpace(item.Value)),
            "creature.rules.override_invalid", $"{path}/overrides",
            "Creature-rules overrides require canonical keys and explicit values.");
        return diagnostics;

        void Add(bool condition, string code, string diagnosticPath, string message)
        {
            if (condition) diagnostics.Add(new(code, diagnosticPath, message));
        }
    }

    public static IReadOnlyDictionary<string, double> EvaluateAttributes(CreatureRulesAttributeSet attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        var profile = new CreatureRulesCompositionProfile(Guid.NewGuid(), 1, "evaluation", "asset.evaluation",
            Spatial.TransformValue.Identity,
            new(Guid.NewGuid(), CreatureRulesInitialLifeState.Alive, null, null, false), attributes,
            new(Guid.NewGuid(), 0, 0, new Dictionary<string, double>(), new Dictionary<string, double>(), [], 0),
            [], new(Guid.NewGuid(), [], new Dictionary<string, Guid>(), "owner.evaluation", false),
            new Dictionary<string, string>());
        var diagnostics = Validate(profile).Where(item => item.Path.Contains("/attributes", StringComparison.Ordinal)).ToArray();
        if (diagnostics.Length > 0) throw new InvalidOperationException(diagnostics[0].Code);

        var values = attributes.BaseValues.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        foreach (var modifier in attributes.Modifiers)
            values[modifier.AttributeId] = values.GetValueOrDefault(modifier.AttributeId) + modifier.Value;
        var formulas = attributes.Derived.ToDictionary(item => item.AttributeId, StringComparer.Ordinal);
        var pending = formulas.Keys.ToHashSet(StringComparer.Ordinal);
        while (pending.Count > 0)
        {
            var progressed = false;
            foreach (var attributeId in pending.Order(StringComparer.Ordinal).ToArray())
            {
                var formula = formulas[attributeId];
                if (formula.Dependencies.Any(dependency => !values.ContainsKey(dependency))) continue;
                values[attributeId] = formula.Operation switch
                {
                    CreatureRulesAttributeFormulaOperation.Sum =>
                        formula.Dependencies.Sum(dependency => values[dependency]) + formula.Constant,
                    CreatureRulesAttributeFormulaOperation.Product =>
                        formula.Dependencies.Aggregate(1d, (value, dependency) => value * values[dependency]) * formula.Constant,
                    CreatureRulesAttributeFormulaOperation.Ratio =>
                        values[formula.Dependencies[0]] / Math.Max(double.Epsilon, values[formula.Dependencies[1]]) +
                        formula.Constant,
                    _ => throw new InvalidOperationException("creature.rules.attribute_formula_unsupported"),
                };
                pending.Remove(attributeId);
                progressed = true;
            }
            if (!progressed) throw new InvalidOperationException("creature.rules.attribute_cycle");
        }
        return values;
    }

    public static double Mitigate(CreatureRulesDefenseProfile defenses, string damageType, double incoming)
    {
        ArgumentNullException.ThrowIfNull(defenses);
        if (!double.IsFinite(incoming) || incoming < 0) throw new ArgumentOutOfRangeException(nameof(incoming));
        var normalized = CreaturePresentationProfile.NormalizeIdentifier(damageType);
        if (defenses.Immunities.Contains(normalized, StringComparer.Ordinal)) return 0;
        return Math.Max(0, incoming * (1 - defenses.Resistances.GetValueOrDefault(normalized)) -
                           defenses.Armor - defenses.FlatMitigation);
    }

    private static void ValidateAttributes(CreatureRulesAttributeSet attributes, string path,
        ICollection<CreatureFoundationDiagnostic> diagnostics)
    {
        var ids = attributes.BaseValues.Keys.Concat(attributes.Derived.Select(item => item.AttributeId))
            .ToHashSet(StringComparer.Ordinal);
        var invalid = attributes.Id == Guid.Empty || !Valid(attributes.RulesetId) ||
                      attributes.BaseValues.Count is < 1 or > 128 ||
                      attributes.BaseValues.Any(item => !Valid(item.Key) || !double.IsFinite(item.Value)) ||
                      HasDuplicate(attributes.Modifiers.Select(item => item.Id)) ||
                      attributes.Modifiers.Any(item => item.Id == Guid.Empty || !ids.Contains(item.AttributeId) ||
                          !Valid(item.SourceId) || !double.IsFinite(item.Value)) ||
                      HasDuplicate(attributes.Derived.Select(item => item.Id)) ||
                      HasDuplicate(attributes.Derived.Select(item => item.AttributeId)) ||
                      attributes.Derived.Any(item => item.Id == Guid.Empty || !Valid(item.AttributeId) ||
                          !Enum.IsDefined(item.Operation) || item.Dependencies.Count == 0 ||
                          item.Dependencies.Contains(item.AttributeId, StringComparer.Ordinal) ||
                          item.Dependencies.Any(dependency => !ids.Contains(dependency)) ||
                          item.Operation == CreatureRulesAttributeFormulaOperation.Ratio && item.Dependencies.Count < 2 ||
                          !double.IsFinite(item.Constant)) || HasAttributeCycle(attributes.Derived);
        if (invalid)
            diagnostics.Add(new("creature.rules.attributes_invalid", $"{path}/attributes/{attributes.Id}",
                "AttributeSet requires a ruleset, finite bases, sourced modifiers and acyclic inspectable formulas."));
    }

    private static void ValidateDefenses(CreatureRulesDefenseProfile defenses, string path,
        ICollection<CreatureFoundationDiagnostic> diagnostics)
    {
        if (defenses.Id == Guid.Empty || !Finite(defenses.Avoidance, defenses.Armor, defenses.FlatMitigation) ||
            defenses.Avoidance < 0 || defenses.Armor < 0 || defenses.FlatMitigation < 0 ||
            defenses.Resistances.Any(item => !Valid(item.Key) || !double.IsFinite(item.Value) || item.Value is < 0 or > 1) ||
            defenses.Saves.Any(item => !Valid(item.Key) || !double.IsFinite(item.Value) || item.Value is < -1000 or > 1000) ||
            defenses.Immunities.Any(item => !Valid(item)))
            diagnostics.Add(new("creature.rules.defenses_invalid", $"{path}/defenses/{defenses.Id}",
                "DefenseModel requires finite avoidance, armor, resistances, saves, immunities and mitigation."));
    }

    private static void ValidateAbilities(IReadOnlyList<CreatureRulesAbilityInstance> abilities, string path,
        ICollection<CreatureFoundationDiagnostic> diagnostics)
    {
        if (abilities.Count > 64 || HasDuplicate(abilities.Select(item => item.Id)) ||
            HasDuplicate(abilities.Select(item => item.AbilityId)) || abilities.Any(item =>
                item.Id == Guid.Empty || !Valid(item.AbilityId, item.ScriptId) || item.Level is < 0 or > 1000 ||
                item.Charges is < 0 or > 9999 || !double.IsFinite(item.CooldownSeconds) ||
                item.CooldownSeconds is < 0 or > 86400 || item.Conditions.Any(value => !Valid(value)) ||
                item.AiTags.Any(value => !Valid(value))))
            diagnostics.Add(new("creature.rules.abilities_invalid", $"{path}/abilities",
                "Ability instances require unique UUIDs and IDs, bounded level, charges and cooldown, conditions, AI tags and script."));
    }

    private static void ValidateInventory(CreatureRulesInventoryProfile inventory, string path,
        ICollection<CreatureFoundationDiagnostic> diagnostics)
    {
        var entries = inventory.Containers.SelectMany(item => item.Items).ToArray();
        var entryIds = entries.Select(item => item.Id).ToHashSet();
        var invalid = inventory.Id == Guid.Empty || !Valid(inventory.OwnerId) ||
                      inventory.Containers.Count is < 1 or > 32 ||
                      HasDuplicate(inventory.Containers.Select(item => item.Id)) ||
                      HasDuplicate(inventory.Containers.Select(item => item.ContainerId)) ||
                      HasDuplicate(entries.Select(item => item.Id)) ||
                      inventory.Containers.Any(container => container.Id == Guid.Empty ||
                          !Valid(container.ContainerId) || container.SlotCount is < 0 or > 1024 ||
                          !Finite(container.MaximumWeight, container.MaximumVolume) ||
                          container.MaximumWeight < 0 || container.MaximumVolume < 0 ||
                          container.Items.Count > container.SlotCount || container.Items.Any(item =>
                              item.Id == Guid.Empty || item.ItemBlueprintId is null || item.Quantity is < 1 or > 9999 ||
                              !Finite(item.UnitWeight, item.UnitVolume) || item.UnitWeight < 0 || item.UnitVolume < 0) ||
                          container.Items.Sum(item => item.UnitWeight * item.Quantity) > container.MaximumWeight ||
                          container.Items.Sum(item => item.UnitVolume * item.Quantity) > container.MaximumVolume) ||
                      inventory.EquipmentSlots.Any(item => !Valid(item.Key) || item.Value == Guid.Empty ||
                          !entryIds.Contains(item.Value)) || inventory.EquipmentSlots.Values.Distinct().Count() !=
                      inventory.EquipmentSlots.Count;
        if (invalid)
            diagnostics.Add(new("creature.rules.inventory_invalid", $"{path}/inventory/{inventory.Id}",
                "Inventory requires unique containers and entries, bounded slots, weight and volume, ownership, persistence and resolvable equipment."));
    }

    private static bool HasAttributeCycle(IReadOnlyList<CreatureRulesDerivedAttribute> formulas)
    {
        var byId = formulas.ToDictionary(item => item.AttributeId, StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        return byId.Keys.Any(Visit);

        bool Visit(string id)
        {
            if (visited.Contains(id)) return false;
            if (!visiting.Add(id)) return true;
            if (byId.TryGetValue(id, out var formula) && formula.Dependencies.Where(byId.ContainsKey).Any(Visit))
                return true;
            visiting.Remove(id);
            visited.Add(id);
            return false;
        }
    }

    private static bool Valid(params string[] values) =>
        values.All(CreaturePresentationProfile.IsCanonicalIdentifier);

    private static bool HasDuplicate<T>(IEnumerable<T> values) where T : notnull
    {
        var items = values.ToArray();
        return items.Distinct().Count() != items.Length;
    }

    private static bool Finite(params double[] values) => values.All(double.IsFinite);

    public static IEnumerable<Guid> EnumerateComponentIds(CreatureRulesCompositionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        yield return profile.Id;
        yield return profile.InitialState.Id;
        yield return profile.Attributes.Id;
        foreach (var id in profile.Attributes.Modifiers.Select(item => item.Id)) yield return id;
        foreach (var id in profile.Attributes.Derived.Select(item => item.Id)) yield return id;
        yield return profile.Defenses.Id;
        foreach (var id in profile.Abilities.Select(item => item.Id)) yield return id;
        yield return profile.Inventory.Id;
        foreach (var container in profile.Inventory.Containers)
        {
            yield return container.Id;
            foreach (var id in container.Items.Select(item => item.Id)) yield return id;
        }
    }
}

public sealed record CreatureRulesCompositionRuntimeResult(
    CreatureRulesCompositionProfile? Profile,
    bool UsedFallback,
    IReadOnlyList<CreatureFoundationDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}

public static class CreatureRulesCompositionRuntimeResolver
{
    public static CreatureRulesCompositionRuntimeResult Resolve(CreatureRulesCompositionProfile? profile, bool required)
    {
        if (profile is null)
            return new(null, !required, required
                ? [new CreatureFoundationDiagnostic(CreatureRulesCompositionValidator.MissingRequiredCode,
                    "creatureFoundation/rulesComposition", "A required creature-rules profile is missing.")]
                : []);
        var diagnostics = CreatureRulesCompositionValidator.Validate(profile);
        return new(diagnostics.Count == 0 ? profile : null, false, diagnostics);
    }
}
