using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;
using Forge.RpgKit.Domain.Creatures;
using DomainFormula = Forge.Domain.Creatures.CreatureRulesAttributeFormulaOperation;
using DomainLifeState = Forge.Domain.Creatures.CreatureRulesInitialLifeState;
using RpgFormula = Forge.RpgKit.Domain.Creatures.AttributeFormulaOperation;
using RpgLifeState = Forge.RpgKit.Domain.Creatures.CreatureInitialLifeState;

namespace Forge.RpgKit.Application.Creatures;

public sealed record CreatureRulesCompositionFoundationResult(
    CreatureDefinition? Creature,
    bool UsedFallback,
    IReadOnlyList<CreatureFoundationDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}

public static class CreatureRulesCompositionFoundationService
{
    public static CreatureRulesCompositionFoundationResult Resolve(ProjectDefinition project, BlueprintId blueprintId)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(blueprintId);
        var blueprint = project.Blueprints.SingleOrDefault(item => item.Id == blueprintId);
        if (blueprint?.CreatureFoundation is null) return new(null, true, []);
        var foundation = blueprint.CreatureFoundation;
        var resolved = CreatureRulesCompositionRuntimeResolver.Resolve(
            foundation.RulesComposition, foundation.RulesCompositionRequiredAtRuntime);
        if (!resolved.Succeeded || resolved.Profile is null)
            return new(null, resolved.UsedFallback, resolved.Diagnostics);

        var authoring = CreatureAuthoringCompositionFoundationService.Resolve(project, blueprintId);
        if (!authoring.Succeeded || authoring.Creature is null)
            return new(null, authoring.UsedFallback, authoring.Diagnostics);
        var profile = resolved.Profile;
        var attributes = new AttributeSet(profile.Attributes.BaseValues,
            profile.Attributes.Modifiers.GroupBy(item => item.AttributeId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key,
                    group => (IReadOnlyList<AttributeModifier>)group.Select(item =>
                        new AttributeModifier(item.SourceId, item.Value)).ToArray(), StringComparer.Ordinal),
            profile.Attributes.Derived.Select(item => new DerivedAttributeFormula(item.AttributeId,
                MapFormula(item.Operation), item.Dependencies, item.Constant)).ToArray());
        var inventory = new CreatureInventory(profile.Inventory.Containers.Select(container =>
                new InventoryContainer(container.ContainerId, container.SlotCount, container.MaximumWeight,
                    container.MaximumVolume, container.Items.Select(item => item.ItemBlueprintId.ToString()).ToArray()))
            .ToArray(), profile.Inventory.EquipmentSlots.ToDictionary(item => item.Key,
                item => item.Value.ToString("D"), StringComparer.Ordinal), profile.Inventory.OwnerId,
            profile.Inventory.Persistent);
        var creature = authoring.Creature with
        {
            InitialState = new CreatureInitialState(MapLifeState(profile.InitialState.LifeState),
                profile.InitialState.AnimationLoopAssetId, profile.InitialState.PoseId,
                profile.InitialState.Invisible),
            Attributes = attributes,
            Defenses = new DefenseModel(profile.Defenses.Avoidance, profile.Defenses.Armor,
                profile.Defenses.Resistances, profile.Defenses.Saves,
                profile.Defenses.Immunities.ToHashSet(StringComparer.Ordinal), profile.Defenses.FlatMitigation),
            Abilities = profile.Abilities.Select(item => new CreatureAbilityInstance(item.AbilityId, item.Level,
                item.Charges, item.CooldownSeconds, item.Conditions, item.AiTags.ToHashSet(StringComparer.Ordinal),
                item.ScriptId)).ToArray(),
            Inventory = inventory,
        };
        var diagnostics = CreatureCompositionService.ValidateComposition(creature)
            .Concat(CreatureRulesService.ValidateCoreRules(creature)).ToArray();
        return new(diagnostics.Length == 0 ? creature : null, false,
            diagnostics.Select(item => new CreatureFoundationDiagnostic(item.Code, item.Path, item.Message)).ToArray());
    }

    private static RpgLifeState MapLifeState(DomainLifeState state) => state switch
    {
        DomainLifeState.Alive => RpgLifeState.Alive,
        DomainLifeState.Unconscious => RpgLifeState.Unconscious,
        DomainLifeState.Dead => RpgLifeState.Dead,
        DomainLifeState.Sleeping => RpgLifeState.Sleeping,
        DomainLifeState.Seated => RpgLifeState.Seated,
        DomainLifeState.Frozen => RpgLifeState.Frozen,
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private static RpgFormula MapFormula(DomainFormula operation) => operation switch
    {
        DomainFormula.Sum => RpgFormula.Sum,
        DomainFormula.Product => RpgFormula.Product,
        DomainFormula.Ratio => RpgFormula.Ratio,
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };
}
