using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;

namespace Forge.Application.Creatures;

public sealed record CreatureRulesCompositionHandle(string BlueprintId);

public sealed record CreatureRulesCompositionScriptView(
    string BlueprintId,
    string ProfileId,
    string LifeState,
    string? PoseId,
    bool Invisible,
    IReadOnlyDictionary<string, double> Attributes,
    double Avoidance,
    double Armor,
    IReadOnlyList<string> AbilityIds,
    int InventoryItemCount,
    IReadOnlyDictionary<string, string> EquipmentSlots);

public static class CreatureRulesCompositionScriptService
{
    public static CreatureRulesCompositionHandle CreateHandle(BlueprintId blueprintId)
    {
        ArgumentNullException.ThrowIfNull(blueprintId);
        return new(blueprintId.ToString());
    }

    public static CreatureRulesCompositionScriptView? Read(
        ProjectDefinition project,
        CreatureRulesCompositionHandle handle)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(handle);
        BlueprintId blueprintId;
        try { blueprintId = BlueprintId.Parse(handle.BlueprintId); }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            throw new KeyNotFoundException("CREATURE_RULES_HANDLE_INVALID", exception);
        }
        var blueprint = project.Blueprints.SingleOrDefault(item => item.Id == blueprintId);
        var foundation = blueprint?.CreatureFoundation;
        if (foundation is null) return null;
        var resolved = CreatureRulesCompositionRuntimeResolver.Resolve(
            foundation.RulesComposition, foundation.RulesCompositionRequiredAtRuntime);
        if (!resolved.Succeeded || resolved.Profile is null) return null;
        var profile = resolved.Profile;
        return new(blueprint!.Id.ToString(), profile.Id.ToString("D"), profile.InitialState.LifeState.ToString(),
            profile.InitialState.PoseId, profile.InitialState.Invisible,
            CreatureRulesCompositionValidator.EvaluateAttributes(profile.Attributes),
            profile.Defenses.Avoidance, profile.Defenses.Armor,
            profile.Abilities.Select(item => item.AbilityId).ToArray(),
            profile.Inventory.Containers.Sum(item => item.Items.Sum(entry => entry.Quantity)),
            profile.Inventory.EquipmentSlots.ToDictionary(item => item.Key, item => item.Value.ToString("D"),
                StringComparer.Ordinal));
    }
}
