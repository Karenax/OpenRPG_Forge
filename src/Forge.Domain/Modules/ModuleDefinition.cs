using Forge.Domain.Areas;

namespace Forge.Domain.Modules;

public sealed record ModuleDefinition
{
    public ModuleId Id { get; }

    public string Slug { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public AreaId EntryAreaId { get; }

    public ModuleDefinition(
        ModuleId id,
        string slug,
        string displayName,
        string description,
        AreaId entryAreaId)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(entryAreaId);

        Id = id;
        Slug = slug;
        DisplayName = displayName;
        Description = description;
        EntryAreaId = entryAreaId;
    }
}
