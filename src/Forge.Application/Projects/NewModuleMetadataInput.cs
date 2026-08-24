using Forge.Domain.Modules;

namespace Forge.Application.Projects;

public sealed record NewModuleMetadataInput(
    string DefaultLocale,
    IReadOnlyList<ProjectLocalizedMetadata> Localizations,
    string Author,
    string Version,
    string License,
    string? ImagePath,
    IReadOnlyList<string> Tags,
    string Classification);
