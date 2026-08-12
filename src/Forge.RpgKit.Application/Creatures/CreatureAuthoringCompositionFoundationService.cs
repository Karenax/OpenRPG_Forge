using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;
using Forge.RpgKit.Domain.Creatures;
using DomainRole = Forge.Domain.Creatures.CreatureAuthoringEncounterRole;
using RpgRole = Forge.RpgKit.Domain.Creatures.EncounterRole;

namespace Forge.RpgKit.Application.Creatures;

public sealed record CreatureAuthoringCompositionFoundationResult(
    CreatureDefinition? Creature,
    bool UsedFallback,
    IReadOnlyList<CreatureFoundationDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}
public static class CreatureAuthoringCompositionFoundationService
{
    public static CreatureAuthoringCompositionFoundationResult Resolve(
        ProjectDefinition project,
        BlueprintId blueprintId)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(blueprintId);
        var blueprint = project.Blueprints.SingleOrDefault(item => item.Id == blueprintId);
        if (blueprint?.CreatureFoundation is null)
            return new(null, true, []);
        var foundation = blueprint.CreatureFoundation;
        var resolved = CreatureAuthoringCompositionRuntimeResolver.Resolve(
            foundation.AuthoringComposition,
            foundation.AuthoringCompositionRequiredAtRuntime);
        if (!resolved.Succeeded || resolved.Profile is null)
            return new(null, resolved.UsedFallback, resolved.Diagnostics);

        var profile = resolved.Profile;
        var species = new SpeciesDefinition(profile.Species.SpeciesId, profile.Species.ParentSpeciesIds,
            profile.Species.TraitSets.Select(item => new TraitSet(item.Id, item.Traits.ToHashSet(StringComparer.Ordinal))).ToArray(),
            profile.Species.Variants, profile.Species.RequiredSkeletonId);
        var visual = new CreatureVisualProfile(profile.Appearance.VisualArchetypeId, profile.Appearance.MeshAssetId,
            profile.Appearance.RigId, profile.Appearance.Morphs, profile.Appearance.BodySlots,
            profile.Appearance.MaterialIds, profile.Appearance.PaletteIds, profile.Appearance.AttachmentIds,
            profile.Appearance.PresetId);
        var animation = new AnimationGraphProfile(profile.AnimationGraph.Id.ToString("D"),
            profile.AnimationGraph.States.Where(item => item.Category == "locomotion").Select(item => item.StateId).ToArray(),
            profile.AnimationGraph.States.Where(item => item.Category == "combat").Select(item => item.StateId).ToArray(),
            profile.AnimationGraph.States.Where(item => item.Category == "emote").Select(item => item.StateId).ToArray(),
            profile.AnimationGraph.States.Where(item => item.Category == "additive").Select(item => item.StateId).ToArray(),
            profile.AnimationGraph.RetargetProfileId);
        var role = Enum.Parse<RpgRole>(profile.EncounterRole.ToString());
        var authoring = new CreatureAuthoringProfile(profile.ArchetypeId, profile.RulesetId, profile.RigId,
            profile.FactionId, role, profile.ThreatRating, profile.VisualKitId,
            profile.CombatTags.ToHashSet(StringComparer.Ordinal));
        var request = new CreatureWizardRequest(profile.Id, 0, profile.Id, blueprint.Id,
            foundation.Identity.Slug, foundation.Identity.Names.GetValueOrDefault("fr", blueprint.DisplayName), authoring,
            foundation.References.AppearanceId, foundation.References.PerceptionProfileId,
            foundation.References.CombatProfileId, foundation.References.InventoryProfileId, false,
            species, visual, animation);
        var authored = CreatureAuthoringService.CreateFromWizard(new CreatureCatalogState(0, []), request);
        if (!authored.Succeeded)
            return new(null, false,
                [new CreatureFoundationDiagnostic(authored.DiagnosticCode ?? "RPG_CREATURE_AUTHORING_FAILED",
                    $"blueprints/{blueprint.Id}/creatureFoundation/authoringComposition",
                    "The RPG Kit rejected the canonical creature-authoring profile.")]);
        var creature = authored.State.Creatures.Single();
        var rpgDiagnostics = CreatureCompositionService.ValidateAuthoringComposition(creature);
        return new(rpgDiagnostics.Count == 0 ? creature : null, false,
            rpgDiagnostics.Select(item => new CreatureFoundationDiagnostic(item.Code, item.Path, item.Message)).ToArray());
    }
}
