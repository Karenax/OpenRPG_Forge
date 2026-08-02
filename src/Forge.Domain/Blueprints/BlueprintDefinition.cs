using Forge.Domain.Combat;

namespace Forge.Domain.Blueprints;

public sealed record BlueprintDefinition
{
    public BlueprintId Id { get; }

    public string Slug { get; }

    public string DisplayName { get; }

    public BlueprintKind Kind { get; }

    public CombatProfile? Combat { get; }

    public BlueprintDefinition(
        BlueprintId id,
        string slug,
        string displayName,
        BlueprintKind kind,
        CombatProfile? combat = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        Id = id;
        Slug = slug;
        DisplayName = displayName;
        Kind = kind;
        Combat = combat;
    }
}
