using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;

namespace Forge.Application.Creatures;

public sealed record CreatureInteractionCompositionHandle(string BlueprintId);

public sealed record CreatureInteractionMembershipScriptView(
    string FactionId,
    double Weight,
    string Rank,
    double PersonalReputation,
    bool Inherited);

public sealed record CreatureInteractionSenseScriptView(
    string Kind,
    double Range,
    double ConeDegrees,
    bool UsesOcclusion,
    IReadOnlyList<string> StimulusTags);

public sealed record CreatureInteractionCompositionScriptView(
    string BlueprintId,
    string ProfileId,
    string PrimaryFactionId,
    IReadOnlyList<CreatureInteractionMembershipScriptView> Memberships,
    IReadOnlyList<CreatureInteractionSenseScriptView> Senses,
    double PerceptionMemorySeconds,
    IReadOnlyList<string> DialogueIds,
    bool Invulnerable,
    bool UsesNarrativeDeath,
    bool DialogueInterruptible,
    IReadOnlyList<string> GameMasterPermissions,
    string OnConversationHandler);

public static class CreatureInteractionCompositionScriptService
{
    public static CreatureInteractionCompositionHandle CreateHandle(BlueprintId blueprintId)
    {
        ArgumentNullException.ThrowIfNull(blueprintId);
        return new(blueprintId.ToString());
    }

    public static CreatureInteractionCompositionScriptView? Read(
        ProjectDefinition project,
        CreatureInteractionCompositionHandle handle)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(handle);
        BlueprintId blueprintId;
        try { blueprintId = BlueprintId.Parse(handle.BlueprintId); }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            throw new KeyNotFoundException("CREATURE_INTERACTION_HANDLE_INVALID", exception);
        }
        var blueprint = project.Blueprints.SingleOrDefault(item => item.Id == blueprintId);
        var foundation = blueprint?.CreatureFoundation;
        if (foundation is null) return null;
        var resolved = CreatureInteractionCompositionRuntimeResolver.Resolve(
            foundation.InteractionComposition, foundation.InteractionCompositionRequiredAtRuntime);
        if (!resolved.Succeeded || resolved.Profile is null) return null;
        var profile = resolved.Profile;
        return new(blueprint!.Id.ToString(), profile.Id.ToString("D"), profile.Factions.PrimaryFactionId.ToString("D"),
            profile.Factions.Memberships.Select(item => new CreatureInteractionMembershipScriptView(
                item.FactionId.ToString("D"), item.Weight, item.Rank, item.PersonalReputation, item.Inherited)).ToArray(),
            profile.Perception.Senses.Select(item => new CreatureInteractionSenseScriptView(item.Kind.ToString(),
                item.Range, item.ConeDegrees, item.UsesOcclusion, item.StimulusTags)).ToArray(),
            profile.Perception.MemorySeconds, profile.DialogueBindings.Select(item => item.DialogueId.ToString()).ToArray(),
            profile.Protection.Invulnerable, profile.Protection.UsesNarrativeDeath,
            profile.Protection.DialogueInterruptible, profile.Protection.GameMasterPermissions,
            profile.OnConversation.Handler);
    }
}
