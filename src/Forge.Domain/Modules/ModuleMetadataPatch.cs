namespace Forge.Domain.Modules;

public enum ModuleMetadataPatchField
{
    DefaultLocale,
    Author,
    Version,
    License,
    ImagePath,
    Tags,
    Classification,
    Provenance,
}

public enum ProjectLocalizedMetadataPatchField
{
    Name,
    Summary,
    Description,
    Credits,
    Installation,
    Rules,
    Controls,
    Licenses,
    Warnings,
    Compatibility,
}

public sealed record ProjectLocalizedMetadataPatch(
    string Locale,
    IReadOnlyList<ProjectLocalizedMetadataPatchField> Fields,
    string? Name = null,
    string? Summary = null,
    string? Description = null,
    string? Credits = null,
    string? Installation = null,
    string? Rules = null,
    string? Controls = null,
    string? Licenses = null,
    string? Warnings = null,
    string? Compatibility = null,
    bool Remove = false);

public sealed record ModuleMetadataPatch(
    IReadOnlyList<ModuleMetadataPatchField> Fields,
    string? DefaultLocale = null,
    string? Author = null,
    string? Version = null,
    string? License = null,
    string? ImagePath = null,
    IReadOnlyList<string>? Tags = null,
    string? Classification = null,
    string? Provenance = null,
    IReadOnlyList<ProjectLocalizedMetadataPatch>? Localizations = null)
{
    public IReadOnlyList<string> RelativePaths
    {
        get
        {
            var paths = Fields.Select(field => $"module/metadata/{FieldPath(field)}").ToList();
            paths.AddRange((Localizations ?? []).SelectMany(localization => localization.Remove
                ? [$"module/metadata/localizations/{localization.Locale}"]
                : localization.Fields.Select(field =>
                    $"module/metadata/localizations/{localization.Locale}/{LocalizedFieldPath(field)}")));
            return paths;
        }
    }

    public static ProjectMetadata Apply(ProjectMetadata source, ModuleMetadataPatch patch)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentNullException.ThrowIfNull(patch.Fields);
        var localizedPatches = patch.Localizations ?? [];
        if (patch.Fields.Count == 0 && localizedPatches.Count == 0)
            throw new ArgumentException("At least one metadata field or localization patch is required.", nameof(patch));
        if (patch.Fields.Distinct().Count() != patch.Fields.Count)
            throw new ArgumentException("Metadata patch fields must be unique.", nameof(patch));
        if (localizedPatches.Select(item => item.Locale).Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
            localizedPatches.Count)
            throw new ArgumentException("A locale can only be patched once per command.", nameof(patch));

        var localizations = source.Localizations.ToDictionary(item => item.Locale,
            StringComparer.OrdinalIgnoreCase);
        foreach (var localizedPatch in localizedPatches)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(localizedPatch.Locale);
            ArgumentNullException.ThrowIfNull(localizedPatch.Fields);
            if (localizedPatch.Fields.Distinct().Count() != localizedPatch.Fields.Count ||
                (!localizedPatch.Remove && localizedPatch.Fields.Count == 0))
                throw new ArgumentException("Localized metadata patch fields must be non-empty and unique.",
                    nameof(patch));
            if (localizedPatch.Remove)
            {
                if (localizedPatch.Fields.Count > 0)
                    throw new ArgumentException("A removed locale cannot also patch fields.", nameof(patch));
                if (!localizations.Remove(localizedPatch.Locale))
                    throw new ArgumentException($"Locale '{localizedPatch.Locale}' does not exist.", nameof(patch));
                continue;
            }

            localizations.TryGetValue(localizedPatch.Locale, out var existing);
            var fields = localizedPatch.Fields.ToHashSet();
            var name = fields.Contains(ProjectLocalizedMetadataPatchField.Name)
                ? Required(localizedPatch.Name, nameof(localizedPatch.Name), allowEmpty: false)
                : existing?.Name ?? throw new ArgumentException(
                    $"New locale '{localizedPatch.Locale}' requires a name.", nameof(patch));
            string Value(ProjectLocalizedMetadataPatchField field, string? value, string? current, string name) =>
                fields.Contains(field) ? Required(value, name, allowEmpty: true) : current ?? string.Empty;
            localizations[localizedPatch.Locale] = new ProjectLocalizedMetadata(
                localizedPatch.Locale,
                name,
                Value(ProjectLocalizedMetadataPatchField.Summary, localizedPatch.Summary, existing?.Summary,
                    nameof(localizedPatch.Summary)),
                Value(ProjectLocalizedMetadataPatchField.Description, localizedPatch.Description,
                    existing?.Description, nameof(localizedPatch.Description)),
                Value(ProjectLocalizedMetadataPatchField.Credits, localizedPatch.Credits, existing?.Credits,
                    nameof(localizedPatch.Credits)),
                Value(ProjectLocalizedMetadataPatchField.Installation, localizedPatch.Installation,
                    existing?.Installation, nameof(localizedPatch.Installation)),
                Value(ProjectLocalizedMetadataPatchField.Rules, localizedPatch.Rules, existing?.Rules,
                    nameof(localizedPatch.Rules)),
                Value(ProjectLocalizedMetadataPatchField.Controls, localizedPatch.Controls, existing?.Controls,
                    nameof(localizedPatch.Controls)),
                Value(ProjectLocalizedMetadataPatchField.Licenses, localizedPatch.Licenses, existing?.Licenses,
                    nameof(localizedPatch.Licenses)),
                Value(ProjectLocalizedMetadataPatchField.Warnings, localizedPatch.Warnings, existing?.Warnings,
                    nameof(localizedPatch.Warnings)),
                Value(ProjectLocalizedMetadataPatchField.Compatibility, localizedPatch.Compatibility,
                    existing?.Compatibility, nameof(localizedPatch.Compatibility)));
        }

        var selected = patch.Fields.ToHashSet();
        var defaultLocale = selected.Contains(ModuleMetadataPatchField.DefaultLocale)
            ? Required(patch.DefaultLocale, nameof(patch.DefaultLocale), allowEmpty: false)
            : source.DefaultLocale;
        var author = selected.Contains(ModuleMetadataPatchField.Author)
            ? Required(patch.Author, nameof(patch.Author), allowEmpty: false)
            : source.Author;
        var version = selected.Contains(ModuleMetadataPatchField.Version)
            ? Required(patch.Version, nameof(patch.Version), allowEmpty: false)
            : source.Version;
        var license = selected.Contains(ModuleMetadataPatchField.License)
            ? Required(patch.License, nameof(patch.License), allowEmpty: false)
            : source.License;
        var imagePath = selected.Contains(ModuleMetadataPatchField.ImagePath) ? patch.ImagePath : source.ImagePath;
        var tags = selected.Contains(ModuleMetadataPatchField.Tags)
            ? patch.Tags ?? throw new ArgumentNullException(nameof(patch.Tags))
            : source.Tags;
        var classification = selected.Contains(ModuleMetadataPatchField.Classification)
            ? Required(patch.Classification, nameof(patch.Classification), allowEmpty: false)
            : source.Classification;
        var provenance = selected.Contains(ModuleMetadataPatchField.Provenance)
            ? Required(patch.Provenance, nameof(patch.Provenance), allowEmpty: false)
            : source.Provenance;
        return new ProjectMetadata(source.Id, defaultLocale, localizations.Values.ToArray(), author, version,
            license, imagePath, tags, classification, provenance, source.SchemaVersion, source.Foundation);
    }

    public static ModuleMetadataPatch Between(ProjectMetadata source, ProjectMetadata candidate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(candidate);
        var fields = new List<ModuleMetadataPatchField>();
        if (!string.Equals(source.DefaultLocale, candidate.DefaultLocale, StringComparison.OrdinalIgnoreCase))
            fields.Add(ModuleMetadataPatchField.DefaultLocale);
        if (source.Author != candidate.Author) fields.Add(ModuleMetadataPatchField.Author);
        if (source.Version != candidate.Version) fields.Add(ModuleMetadataPatchField.Version);
        if (source.License != candidate.License) fields.Add(ModuleMetadataPatchField.License);
        if (source.ImagePath != candidate.ImagePath) fields.Add(ModuleMetadataPatchField.ImagePath);
        if (!source.Tags.SequenceEqual(candidate.Tags, StringComparer.OrdinalIgnoreCase))
            fields.Add(ModuleMetadataPatchField.Tags);
        if (source.Classification != candidate.Classification) fields.Add(ModuleMetadataPatchField.Classification);
        if (source.Provenance != candidate.Provenance) fields.Add(ModuleMetadataPatchField.Provenance);

        var localized = new List<ProjectLocalizedMetadataPatch>();
        var sourceByLocale = source.Localizations.ToDictionary(item => item.Locale, StringComparer.OrdinalIgnoreCase);
        var candidateByLocale = candidate.Localizations.ToDictionary(item => item.Locale,
            StringComparer.OrdinalIgnoreCase);
        foreach (var removed in sourceByLocale.Keys.Where(locale => !candidateByLocale.ContainsKey(locale)))
            localized.Add(new(removed, [], Remove: true));
        foreach (var item in candidate.Localizations)
        {
            sourceByLocale.TryGetValue(item.Locale, out var previous);
            var changed = new List<ProjectLocalizedMetadataPatchField>();
            if (previous?.Name != item.Name) changed.Add(ProjectLocalizedMetadataPatchField.Name);
            if (previous?.Summary != item.Summary) changed.Add(ProjectLocalizedMetadataPatchField.Summary);
            if (previous?.Description != item.Description) changed.Add(ProjectLocalizedMetadataPatchField.Description);
            if (previous?.Credits != item.Credits) changed.Add(ProjectLocalizedMetadataPatchField.Credits);
            if (previous?.Installation != item.Installation) changed.Add(ProjectLocalizedMetadataPatchField.Installation);
            if (previous?.Rules != item.Rules) changed.Add(ProjectLocalizedMetadataPatchField.Rules);
            if (previous?.Controls != item.Controls) changed.Add(ProjectLocalizedMetadataPatchField.Controls);
            if (previous?.Licenses != item.Licenses) changed.Add(ProjectLocalizedMetadataPatchField.Licenses);
            if (previous?.Warnings != item.Warnings) changed.Add(ProjectLocalizedMetadataPatchField.Warnings);
            if (previous?.Compatibility != item.Compatibility)
                changed.Add(ProjectLocalizedMetadataPatchField.Compatibility);
            if (changed.Count > 0)
                localized.Add(new(item.Locale, changed, item.Name, item.Summary, item.Description, item.Credits,
                    item.Installation, item.Rules, item.Controls, item.Licenses, item.Warnings, item.Compatibility));
        }
        return new(fields, candidate.DefaultLocale, candidate.Author, candidate.Version, candidate.License,
            candidate.ImagePath, candidate.Tags, candidate.Classification, candidate.Provenance, localized);
    }

    private static string Required(string? value, string name, bool allowEmpty)
    {
        if (value is null || !allowEmpty && string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Metadata field '{name}' is required.", name);
        return value;
    }

    private static string FieldPath(ModuleMetadataPatchField field) => field switch
    {
        ModuleMetadataPatchField.DefaultLocale => "default-locale",
        ModuleMetadataPatchField.ImagePath => "image-path",
        _ => field.ToString().ToLowerInvariant(),
    };

    private static string LocalizedFieldPath(ProjectLocalizedMetadataPatchField field) =>
        field.ToString().ToLowerInvariant();
}
