using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;
using Forge.RpgKit.Domain.Creatures;

namespace Forge.RpgKit.Application.Creatures;

public sealed record CreatureCatalogState(long Revision, IReadOnlyList<CreatureDefinition> Creatures);
public sealed record CreatureWizardRequest(
    Guid CommandId, long BaseRevision, Guid CreatureId, BlueprintId BlueprintId, string Slug,
    string DisplayName, CreatureAuthoringProfile Authoring, string AppearanceId, string PerceptionProfileId,
    string CombatProfileId, string InventoryProfileId, bool DryRun,
    SpeciesDefinition? Species = null,
    CreatureVisualProfile? Visual = null,
    AnimationGraphProfile? Animations = null);
public sealed record CreatureAuthoringResult(
    bool Succeeded, CreatureCatalogState State, long? NewRevision, string? DiagnosticCode,
    string Patch, string Inverse, bool Applied);

public static class CreatureAuthoringService
{
    public static CreatureAuthoringResult CreateFromWizard(CreatureCatalogState state, CreatureWizardRequest request)
    {
        if (request.CommandId == Guid.Empty) return Failure(state, "RPG_CREATURE_COMMAND_UUID_REQUIRED");
        if (request.BaseRevision != state.Revision) return Failure(state, "RPG_CREATURE_REVISION_CONFLICT");
        if (state.Creatures.Any(item => item.Id == request.CreatureId || item.Identity.Slug == request.Slug)) return Failure(state, "RPG_CREATURE_DUPLICATE");
        var definition = new CreatureDefinition(request.CreatureId, 1, "wizard:creature", request.BlueprintId,
            new(request.Slug, new(request.DisplayName, new Dictionary<string, string>()), [], "iel", new("", new Dictionary<string, string>()), "", new HashSet<string>()),
            request.Authoring, TransformValue.Identity, [new("health", Math.Max(1, request.Authoring.ThreatRating * 10), Math.Max(1, request.Authoring.ThreatRating * 10), 0, true)],
            new(CreatureMovementMode.Grounded, 5, 10, 8, 0, 0, 45), request.AppearanceId, request.PerceptionProfileId,
            request.CombatProfileId, null, request.InventoryProfileId, new("creature.spawned", new Dictionary<string, string>()),
            request.Species, request.Visual, request.Animations);
        try { CreatureFactory.Validate(definition); } catch (InvalidOperationException exception) { return Failure(state, exception.Message); }
        var patch = $"add creatures/{definition.Id}"; var inverse = $"remove creatures/{definition.Id}";
        if (request.DryRun) return new(true, state, null, null, patch, inverse, false);
        var next = new CreatureCatalogState(state.Revision + 1, [.. state.Creatures, definition]);
        return new(true, next, next.Revision, null, patch, inverse, true);
    }

    private static CreatureAuthoringResult Failure(CreatureCatalogState state, string code) => new(false, state, null, code, "", "", false);
}
