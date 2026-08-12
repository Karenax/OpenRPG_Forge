using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;

namespace Forge.Application.Creatures;

public sealed record CreatureAuthoringCompositionHandle(string BlueprintId);

public sealed record CreatureAuthoringCompositionScriptView(
    string BlueprintId,
    string ProfileId,
    string ArchetypeId,
    string RulesetId,
    string EncounterRole,
    double ThreatRating,
    string SpeciesId,
    string MeshAssetId,
    string RigId,
    string AnimationGraphId,
    IReadOnlyList<string> AnimationStates);

public static class CreatureAuthoringCompositionScriptService
{
    public static CreatureAuthoringCompositionHandle CreateHandle(BlueprintId blueprintId)
    {
        ArgumentNullException.ThrowIfNull(blueprintId);
        return new(blueprintId.ToString());
    }

    public static CreatureAuthoringCompositionScriptView? Read(
        ProjectDefinition project,
        CreatureAuthoringCompositionHandle handle)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(handle);
        BlueprintId blueprintId;
        try { blueprintId = BlueprintId.Parse(handle.BlueprintId); }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            throw new KeyNotFoundException("CREATURE_AUTHORING_HANDLE_INVALID", exception);
        }
        var blueprint = project.Blueprints.SingleOrDefault(item => item.Id == blueprintId);
        var foundation = blueprint?.CreatureFoundation;
        if (foundation is null) return null;
        var resolved = CreatureAuthoringCompositionRuntimeResolver.Resolve(
            foundation.AuthoringComposition, foundation.AuthoringCompositionRequiredAtRuntime);
        if (!resolved.Succeeded || resolved.Profile is null) return null;
        var profile = resolved.Profile;
        return new(blueprint!.Id.ToString(), profile.Id.ToString("D"), profile.ArchetypeId, profile.RulesetId,
            profile.EncounterRole.ToString(), profile.ThreatRating, profile.Species.SpeciesId,
            profile.Appearance.MeshAssetId, profile.RigId, profile.AnimationGraph.Id.ToString("D"),
            profile.AnimationGraph.States.Select(item => item.StateId).ToArray());
    }
}
