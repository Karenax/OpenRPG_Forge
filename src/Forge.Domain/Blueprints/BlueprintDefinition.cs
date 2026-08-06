using Forge.Domain.Combat;
using Forge.Domain.Creatures;

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
        CreatureProgressionProfile? creatureProgression = null)
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
    }
}
