using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Lifecycle;
using Forge.Domain.Projects;
using Forge.RpgKit.Domain.Creatures;
using DomainSense = Forge.Domain.Creatures.CreatureInteractionSenseKind;
using RpgSense = Forge.RpgKit.Domain.Creatures.SenseKind;

namespace Forge.RpgKit.Application.Creatures;

public sealed record CreatureInteractionCompositionFoundationResult(
    CreatureDefinition? Creature,
    IReadOnlyList<CreatureDialogueBinding> DialogueBindings,
    CreatureProtectionPolicy? Protection,
    EventBinding? OnConversation,
    bool UsedFallback,
    IReadOnlyList<CreatureFoundationDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}
public static class CreatureInteractionCompositionFoundationService
{
    public static CreatureInteractionCompositionFoundationResult Resolve(
        ProjectDefinition project,
        BlueprintId blueprintId)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(blueprintId);
        var blueprint = project.Blueprints.SingleOrDefault(item => item.Id == blueprintId);
        if (blueprint?.CreatureFoundation is null) return new(null, [], null, null, true, []);
        var foundation = blueprint.CreatureFoundation;
        var resolved = CreatureInteractionCompositionRuntimeResolver.Resolve(
            foundation.InteractionComposition, foundation.InteractionCompositionRequiredAtRuntime);
        if (!resolved.Succeeded || resolved.Profile is null)
            return new(null, [], null, null, resolved.UsedFallback, resolved.Diagnostics);

        var rules = CreatureRulesCompositionFoundationService.Resolve(project, blueprintId);
        if (!rules.Succeeded || rules.Creature is null)
            return new(null, [], null, null, rules.UsedFallback, rules.Diagnostics);
        var profile = resolved.Profile;
        var creature = rules.Creature with
        {
            Factions = new CreatureFactionProfile(profile.Factions.PrimaryFactionId.ToString("D"),
                profile.Factions.Memberships.Select(item => new FactionMembership(item.FactionId.ToString("D"),
                    item.Weight, item.Rank, item.PersonalReputation, item.Inherited)).ToArray()),
            Perception = new CreaturePerceptionProfile(profile.Perception.Senses.Select(item => new SenseProfile(
                MapSense(item.Kind), item.Range, item.ConeDegrees, item.UsesOcclusion,
                item.StimulusTags.ToHashSet(StringComparer.Ordinal))).ToArray(), profile.Perception.MemorySeconds),
        };
        var diagnostics = CreatureRulesService.Validate(creature);
        var bindings = profile.DialogueBindings.Select(item => new CreatureDialogueBinding(item.Context, item.Priority,
            item.DialogueId.ToString(), item.InterlocutorId, item.RequiredState, item.Fallback)).ToArray();
        var protection = new CreatureProtectionPolicy(profile.Protection.Invulnerable,
            profile.Protection.UsesNarrativeDeath, profile.Protection.DialogueInterruptible,
            profile.Protection.GameMasterPermissions.ToHashSet(StringComparer.Ordinal));
        return new(diagnostics.Count == 0 ? creature : null, bindings, protection, profile.OnConversation, false,
            diagnostics.Select(item => new CreatureFoundationDiagnostic(item.Code, item.Path, item.Message)).ToArray());
    }

    private static RpgSense MapSense(DomainSense kind) => kind switch
    {
        DomainSense.Vision => RpgSense.Vision,
        DomainSense.Hearing => RpgSense.Hearing,
        DomainSense.Smell => RpgSense.Smell,
        DomainSense.Magic => RpgSense.Magic,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
