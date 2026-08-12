using Forge.Domain.Combat;
using Forge.Domain.Creatures;
using Forge.Domain.Items;

namespace Forge.Domain.Blueprints;

public sealed record BlueprintDefinition
{
    public BlueprintId Id { get; }

    public string Slug { get; }

    public string DisplayName { get; }

    public BlueprintKind Kind { get; }

    public CombatProfile? Combat { get; }

    public BlueprintPaletteOrigin PaletteOrigin { get; }

    public bool IsFavorite { get; }

    public BlueprintProvenance Provenance { get; }

    public CreaturePresentationProfile? CreaturePresentation { get; }

    public CreatureProgressionProfile? CreatureProgression { get; }

    public CreatureLootProfile? CreatureLoot { get; }

    public CreatureRoleBehaviorProfile? CreatureRoleBehavior { get; }

    public CreatureEventProfile? CreatureEvents { get; }

    public ItemAdvancedProfile? ItemAdvanced { get; }

    public BlueprintAuthoringMetadata Authoring { get; }

    public CreatureFoundationProfile? CreatureFoundation { get; }

    public ItemFoundationProfile? ItemFoundation { get; }

    public bool IsReadOnly() => PaletteOrigin == BlueprintPaletteOrigin.InstalledPack;

    public BlueprintDefinition(
        BlueprintId id,
        string slug,
        string displayName,
        BlueprintKind kind,
        CombatProfile? combat = null,
        BlueprintPaletteOrigin paletteOrigin = BlueprintPaletteOrigin.ProjectContent,
        bool isFavorite = false,
        BlueprintProvenance? provenance = null,
        CreaturePresentationProfile? creaturePresentation = null,
        CreatureProgressionProfile? creatureProgression = null,
        CreatureLootProfile? creatureLoot = null,
        CreatureRoleBehaviorProfile? creatureRoleBehavior = null,
        CreatureEventProfile? creatureEvents = null,
        ItemAdvancedProfile? itemAdvanced = null,
        BlueprintAuthoringMetadata? authoring = null,
        CreatureFoundationProfile? creatureFoundation = null,
        ItemFoundationProfile? itemFoundation = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        Id = id;
        Slug = slug;
        DisplayName = displayName;
        Kind = kind;
        Combat = combat;
        PaletteOrigin = paletteOrigin;
        IsFavorite = isFavorite;
        Provenance = provenance ?? BlueprintProvenance.CreateProjectDefault(id, slug);
        if (creaturePresentation is not null && kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn))
        {
            throw new ArgumentException(
                "Creature presentation can only be assigned to creature or player-spawn blueprints.",
                nameof(creaturePresentation));
        }

        CreaturePresentation = creaturePresentation;
        if (creatureProgression is not null && kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn))
        {
            throw new ArgumentException(
                "Creature progression can only be assigned to creature or player-spawn blueprints.",
                nameof(creatureProgression));
        }

        CreatureProgression = creatureProgression;
        if (creatureLoot is not null && kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn))
        {
            throw new ArgumentException(
                "Creature loot can only be assigned to creature or player-spawn blueprints.",
                nameof(creatureLoot));
        }

        CreatureLoot = creatureLoot;
        if (creatureRoleBehavior is not null && kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn))
        {
            throw new ArgumentException(
                "Creature role and behavior can only be assigned to creature or player-spawn blueprints.",
                nameof(creatureRoleBehavior));
        }

        CreatureRoleBehavior = creatureRoleBehavior;
        if (creatureEvents is not null && kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn))
        {
            throw new ArgumentException(
                "Creature events can only be assigned to creature or player-spawn blueprints.",
                nameof(creatureEvents));
        }

        CreatureEvents = creatureEvents;
        if (itemAdvanced is not null && kind != BlueprintKind.Item)
        {
            throw new ArgumentException(
                "Advanced item data can only be assigned to item blueprints.",
                nameof(itemAdvanced));
        }

        ItemAdvanced = itemAdvanced;
        Authoring = authoring ?? BlueprintAuthoringMetadata.CreateDefault(id, slug, paletteOrigin, Provenance);
        if (creatureFoundation is not null && kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn))
        {
            throw new ArgumentException(
                "Creature foundation can only be assigned to creature or player-spawn blueprints.",
                nameof(creatureFoundation));
        }

        CreatureFoundation = creatureFoundation;
        if (itemFoundation is not null && kind != BlueprintKind.Item)
        {
            throw new ArgumentException(
                "Item foundation can only be assigned to item blueprints.",
                nameof(itemFoundation));
        }

        ItemFoundation = itemFoundation;
    }

    public BlueprintDefinition WithItemAdvanced(ItemAdvancedProfile? profile) => new(
        Id, Slug, DisplayName, Kind, Combat, PaletteOrigin, IsFavorite, Provenance,
        CreaturePresentation, CreatureProgression, CreatureLoot, CreatureRoleBehavior, CreatureEvents, profile, Authoring,
        CreatureFoundation, ItemFoundation);

    public BlueprintDefinition WithCreatureFoundation(CreatureFoundationProfile? profile) => new(
        Id, Slug, DisplayName, Kind, Combat, PaletteOrigin, IsFavorite, Provenance,
        CreaturePresentation, CreatureProgression, CreatureLoot, CreatureRoleBehavior, CreatureEvents, ItemAdvanced,
        Authoring, profile, ItemFoundation);

    public BlueprintDefinition WithItemFoundation(ItemFoundationProfile? profile) => new(
        Id, Slug, DisplayName, Kind, Combat, PaletteOrigin, IsFavorite, Provenance,
        CreaturePresentation, CreatureProgression, CreatureLoot, CreatureRoleBehavior, CreatureEvents, ItemAdvanced,
        Authoring, CreatureFoundation, profile);

    public BlueprintDefinition WithPaletteUpdate(
        BlueprintAuthoringMetadata authoring,
        BlueprintProvenance provenance) => new(
        Id, Slug, DisplayName, Kind, Combat, PaletteOrigin, IsFavorite, provenance,
        CreaturePresentation, CreatureProgression, CreatureLoot, CreatureRoleBehavior, CreatureEvents, ItemAdvanced,
        authoring, CreatureFoundation, ItemFoundation);
}
