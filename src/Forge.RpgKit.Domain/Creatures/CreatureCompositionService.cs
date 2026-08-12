namespace Forge.RpgKit.Domain.Creatures;

public static class CreatureCompositionService
{
    public static IReadOnlyList<CreatureValidationDiagnostic> ValidateComposition(CreatureDefinition definition)
    {
        var diagnostics = ValidateAuthoringComposition(definition).ToList(); var root = $"creatures/{definition.Id}";
        if (definition.InitialState is null || definition.InitialState.LifeState is CreatureInitialLifeState.Dead && string.IsNullOrWhiteSpace(definition.InitialState.PoseId))
            diagnostics.Add(new("RPG_CREATURE_INITIAL_STATE_INVALID", $"{root}/initialState", "Initial life state or dead pose is invalid."));
        if (definition.Attributes is null || definition.Attributes.BaseValues.Count == 0 || definition.Attributes.BaseValues.Values.Any(value => !double.IsFinite(value)) || definition.Attributes.Derived.Any(formula => formula.Dependencies.Count == 0 || formula.Dependencies.Contains(formula.AttributeId, StringComparer.Ordinal)))
            diagnostics.Add(new("RPG_CREATURE_ATTRIBUTES_INVALID", $"{root}/attributes", "Attribute values or formulas are invalid."));
        return diagnostics;
    }

    public static IReadOnlyList<CreatureValidationDiagnostic> ValidateAuthoringComposition(CreatureDefinition definition)
    {
        var diagnostics = new List<CreatureValidationDiagnostic>(); var root = $"creatures/{definition.Id}";
        if (definition.Species is null || string.IsNullOrWhiteSpace(definition.Species.Id) || string.IsNullOrWhiteSpace(definition.Species.RequiredSkeletonId) || definition.Species.ParentSpeciesIds.Contains(definition.Species.Id, StringComparer.Ordinal) || definition.Species.TraitSets.Select(set => set.Id).Distinct().Count() != definition.Species.TraitSets.Count)
            diagnostics.Add(new("RPG_CREATURE_SPECIES_INVALID", $"{root}/species", "Species, inheritance, traits or skeleton requirement is invalid."));
        if (definition.Visual is null || string.IsNullOrWhiteSpace(definition.Visual.ArchetypeId) || string.IsNullOrWhiteSpace(definition.Visual.MeshId) || string.IsNullOrWhiteSpace(definition.Visual.RigId) || definition.Visual.Morphs.Values.Any(value => value is < -1 or > 1))
            diagnostics.Add(new("RPG_CREATURE_VISUAL_INVALID", $"{root}/visual", "Visual archetype, mesh, rig or morph is invalid."));
        if (definition.Animations is null || string.IsNullOrWhiteSpace(definition.Animations.Id) || definition.Animations.LocomotionStates.Count == 0 || string.IsNullOrWhiteSpace(definition.Animations.RetargetProfileId))
            diagnostics.Add(new("RPG_CREATURE_ANIMATION_GRAPH_INVALID", $"{root}/animations", "Animation graph lacks locomotion or retargeting."));
        return diagnostics;
    }

    public static IReadOnlyDictionary<string, double> EvaluateAttributes(AttributeSet attributes)
    {
        var values = attributes.BaseValues.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var pair in attributes.Modifiers) values[pair.Key] = values.GetValueOrDefault(pair.Key) + pair.Value.Sum(modifier => modifier.Value);
        foreach (var formula in attributes.Derived)
        {
            if (formula.Dependencies.Any(dependency => !values.ContainsKey(dependency))) throw new InvalidOperationException("RPG_CREATURE_ATTRIBUTE_DEPENDENCY_MISSING");
            values[formula.AttributeId] = formula.Operation switch
            {
                AttributeFormulaOperation.Sum => formula.Dependencies.Sum(dependency => values[dependency]) + formula.Constant,
                AttributeFormulaOperation.Product => formula.Dependencies.Aggregate(1d, (value, dependency) => value * values[dependency]) * formula.Constant,
                AttributeFormulaOperation.Ratio when formula.Dependencies.Count >= 2 => values[formula.Dependencies[0]] / Math.Max(double.Epsilon, values[formula.Dependencies[1]]) + formula.Constant,
                _ => throw new InvalidOperationException("RPG_CREATURE_ATTRIBUTE_FORMULA_UNSUPPORTED")
            };
        }
        return values;
    }
}
