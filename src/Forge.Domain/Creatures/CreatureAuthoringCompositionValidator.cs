namespace Forge.Domain.Creatures;

public static class CreatureAuthoringCompositionValidator
{
    public const string MissingRequiredCode = "creature.authoring.required_missing";

    public static IReadOnlyList<CreatureFoundationDiagnostic> Validate(
        CreatureAuthoringCompositionProfile profile,
        string path = "creatureFoundation/authoringComposition")
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<CreatureFoundationDiagnostic>();
        Add(profile.Id == Guid.Empty, "creature.authoring.uuid_required", path,
            "A stable creature-authoring UUID is required.");
        Add(profile.SchemaVersion != CreatureAuthoringCompositionProfile.CurrentSchemaVersion,
            "creature.authoring.schema_unsupported", path, "The creature-authoring schema is unsupported.");
        Add(string.IsNullOrWhiteSpace(profile.Provenance), "creature.authoring.provenance_required", path,
            "Creature-authoring provenance is required.");
        Add(!Valid(profile.AssetId, profile.ArchetypeId, profile.RulesetId, profile.RigId, profile.FactionId,
                profile.VisualKitId) || !Enum.IsDefined(profile.EncounterRole) ||
            !double.IsFinite(profile.ThreatRating) || profile.ThreatRating is < 0 or > 1000 ||
            profile.CombatTags.Count is < 1 or > 32 || profile.CombatTags.Any(item => !Valid(item)),
            "creature.authoring.wizard_invalid", $"{path}/wizard",
            "Wizard archetype, ruleset, rig, faction, role, threat and combat tags are invalid.");

        var species = profile.Species;
        Add(!Valid(species.SpeciesId, species.RequiredSkeletonId) || species.ParentSpeciesIds.Count > 8 ||
            species.ParentSpeciesIds.Contains(species.SpeciesId, StringComparer.Ordinal) ||
            species.ParentSpeciesIds.Any(item => !Valid(item)) || species.Variants.Any(item => !Valid(item)) ||
            HasDuplicate(species.TraitSets.Select(item => item.Id)) || species.TraitSets.Count is < 1 or > 32 ||
            species.TraitSets.Any(item => !Valid(item.Id) || item.Traits.Count == 0 || item.Traits.Any(value => !Valid(value))),
            "creature.authoring.species_invalid", $"{path}/species",
            "Species inheritance, trait sets, variants and required skeleton are invalid.");

        var appearance = profile.Appearance;
        Add(!Valid(appearance.VisualArchetypeId, appearance.MeshAssetId, appearance.RigId, appearance.PresetId) ||
            appearance.Morphs.Count > 64 || appearance.Morphs.Any(item => !Valid(item.Key) ||
                !double.IsFinite(item.Value) || item.Value is < -1 or > 1) ||
            appearance.BodySlots.Any(item => !Valid(item.Key, item.Value)) ||
            appearance.MaterialIds.Any(item => !Valid(item)) || appearance.PaletteIds.Any(item => !Valid(item)) ||
            appearance.AttachmentIds.Any(item => !Valid(item)) ||
            !string.Equals(profile.RigId, appearance.RigId, StringComparison.Ordinal) ||
            !string.Equals(profile.RigId, species.RequiredSkeletonId, StringComparison.Ordinal),
            "creature.authoring.appearance_invalid", $"{path}/appearance",
            "Appearance requires compatible archetype, mesh, rig, morph, material, palette and preset references.");

        var graph = profile.AnimationGraph;
        var stateIds = graph.States.Select(item => item.StateId).ToHashSet(StringComparer.Ordinal);
        var categories = graph.States.Select(item => item.Category).ToHashSet(StringComparer.Ordinal);
        Add(graph.Id == Guid.Empty || graph.SchemaVersion != CreatureAnimationGraphProfile.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(graph.Provenance) || !Valid(graph.RetargetProfileId) ||
            graph.States.Count is < 4 or > 128 || HasDuplicate(graph.States.Select(item => item.Id)) ||
            HasDuplicate(graph.States.Select(item => item.StateId)) ||
            graph.States.Any(item => item.Id == Guid.Empty || !Valid(item.StateId, item.ClipAssetId) ||
                item.Category is not ("locomotion" or "combat" or "emote" or "additive")) ||
            !new[] { "locomotion", "combat", "emote", "additive" }.All(categories.Contains) ||
            HasDuplicate(graph.Transitions.Select(item => item.Id)) ||
            graph.Transitions.Any(item => item.Id == Guid.Empty || !stateIds.Contains(item.FromStateId) ||
                !stateIds.Contains(item.ToStateId) || !Valid(item.ConditionId)),
            "creature.authoring.animation_graph_invalid", $"{path}/animationGraph",
            "Animation graph requires stable states and transitions for locomotion, combat, emotes, additive layers and retargeting.");
        Add(profile.Overrides.Any(item => !Valid(item.Key) || string.IsNullOrWhiteSpace(item.Value)),
            "creature.authoring.override_invalid", $"{path}/overrides",
            "Creature-authoring overrides require canonical keys and explicit values.");
        return diagnostics;

        void Add(bool condition, string code, string diagnosticPath, string message)
        {
            if (condition) diagnostics.Add(new(code, diagnosticPath, message));
        }
    }

    private static bool Valid(params string[] values) => values.All(CreaturePresentationProfile.IsCanonicalIdentifier);

    private static bool HasDuplicate<T>(IEnumerable<T> values) where T : notnull
    {
        var items = values.ToArray();
        return items.Distinct().Count() != items.Length;
    }
}
public sealed record CreatureAuthoringCompositionRuntimeResult(
    CreatureAuthoringCompositionProfile? Profile,
    bool UsedFallback,
    IReadOnlyList<CreatureFoundationDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}

public static class CreatureAuthoringCompositionRuntimeResolver
{
    public static CreatureAuthoringCompositionRuntimeResult Resolve(
        CreatureAuthoringCompositionProfile? profile,
        bool required)
    {
        if (profile is null)
            return new(null, !required, required
                ? [new CreatureFoundationDiagnostic(CreatureAuthoringCompositionValidator.MissingRequiredCode,
                    "creatureFoundation/authoringComposition", "A required creature-authoring profile is missing.")]
                : []);
        var diagnostics = CreatureAuthoringCompositionValidator.Validate(profile);
        return new(diagnostics.Count == 0 ? profile : null, false, diagnostics);
    }
}
