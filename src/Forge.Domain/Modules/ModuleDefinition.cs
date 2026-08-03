using Forge.Domain.Areas;
using Forge.Domain.Localization;

namespace Forge.Domain.Modules;

public sealed record ModuleDefinition
{
    public ModuleId Id { get; }

    public string Slug { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public AreaId EntryAreaId { get; }

    public ProjectMetadata Metadata { get; }

    public IReadOnlyList<ProjectVariableDefinition> Variables { get; }

    public ModuleDistributionConfiguration Distribution { get; }

    public LocalizationCatalog Localization { get; }

    public ModuleDefinition(
        ModuleId id,
        string slug,
        string displayName,
        string description,
        AreaId entryAreaId,
        ProjectMetadata? metadata = null,
        IReadOnlyList<ProjectVariableDefinition>? variables = null,
        ModuleDistributionConfiguration? distribution = null,
        LocalizationCatalog? localization = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(entryAreaId);

        var materializedVariables = (variables ?? []).ToArray();
        if (materializedVariables.Select(variable => variable.Id).Distinct().Count() != materializedVariables.Length)
        {
            throw new ArgumentException("A module cannot contain duplicate project variable IDs.", nameof(variables));
        }

        if (materializedVariables.Select(variable => variable.Key)
            .Distinct(StringComparer.Ordinal).Count() != materializedVariables.Length)
        {
            throw new ArgumentException("A module cannot contain duplicate project variable keys.", nameof(variables));
        }

        Id = id;
        Slug = slug;
        DisplayName = displayName;
        Description = description;
        EntryAreaId = entryAreaId;
        Metadata = metadata ?? ProjectMetadata.CreateDefault(id, displayName, description);
        Variables = materializedVariables.OrderBy(variable => variable.Key, StringComparer.Ordinal).ToArray();
        Distribution = distribution ?? ModuleDistributionConfiguration.CreateDefault(id);
        Localization = localization ?? LocalizationCatalog.CreateEmpty(id.Value, Metadata.DefaultLocale);
    }
}
