using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Modules;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioLocalizedProjectDraft(
    string Locale,
    string Name,
    string Summary,
    string Description,
    string Credits,
    string Installation,
    string Rules,
    string Controls,
    string Licenses,
    string Warnings,
    string Compatibility)
{
    internal static StudioLocalizedProjectDraft From(ProjectLocalizedMetadata source) => new(
        source.Locale,
        source.Name,
        source.Summary,
        source.Description,
        source.Credits,
        source.Installation,
        source.Rules,
        source.Controls,
        source.Licenses,
        source.Warnings,
        source.Compatibility);

    internal ProjectLocalizedMetadata ToDomain() => new(
        Locale,
        Name,
        Summary,
        Description,
        Credits,
        Installation,
        Rules,
        Controls,
        Licenses,
        Warnings,
        Compatibility);
}

internal sealed record StudioNewProjectDraft(
    Guid DraftId,
    string ParentDirectory,
    ProjectTemplateKind Template,
    bool CreateInitialArea,
    string DefaultLocale,
    IReadOnlyList<StudioLocalizedProjectDraft> Localizations,
    string Author,
    string Version,
    string License,
    string? ImagePath,
    IReadOnlyList<string> Tags,
    string Classification,
    string AreaDisplayName,
    AreaKind AreaKind,
    AreaDimensions Dimensions,
    IReadOnlyList<string> Kits,
    IReadOnlyList<ModuleDistributionPlatform> TargetPlatforms,
    string RulesProvider,
    ProjectRenderPreset RenderPreset,
    ProjectNetworkMode NetworkMode,
    float PlayerOrientationDegrees,
    string GameMode,
    string EntryClass,
    string PlayerSelectionRule)
{
    internal StudioLocalizedProjectDraft DefaultLocalization => Localizations.First(item =>
        string.Equals(item.Locale, DefaultLocale, StringComparison.OrdinalIgnoreCase));

    internal static StudioNewProjectDraft CreateDefault(string parentDirectory) => new(
        Guid.NewGuid(),
        parentDirectory,
        ProjectTemplateKind.Rpg,
        true,
        "fr",
        [new StudioLocalizedProjectDraft(
            "fr",
            "Mon aventure",
            "Une nouvelle aventure OpenRPG Forge",
            "Décrivez ici le monde et la promesse faite aux joueurs.",
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            "OpenRPG Forge 1.x")],
        "Non renseigné",
        "0.1.0",
        "UNLICENSED",
        null,
        [],
        "Non classé",
        "Première zone",
        AreaKind.Exterior,
        new AreaDimensions(64, 64),
        ["forge-core", "forge-rpg-kit"],
        [ModuleDistributionPlatform.WindowsX64],
        "forge.generic",
        ProjectRenderPreset.Balanced,
        ProjectNetworkMode.Offline,
        0,
        "campaign",
        "adventurer",
        "default");
}

internal sealed record StudioProjectConfigurationDraft(
    string DefaultLocale,
    IReadOnlyList<StudioLocalizedProjectDraft> Localizations,
    string Author,
    string Version,
    string License,
    string? ImagePath,
    IReadOnlyList<string> Tags,
    string Classification,
    IReadOnlyList<string> Kits,
    IReadOnlyList<ModuleDistributionPlatform> TargetPlatforms,
    string RulesProvider,
    ProjectRenderPreset RenderPreset,
    ProjectNetworkMode NetworkMode,
    AreaId? PlayerStartAreaId,
    EntityId? PlayerStartEntityId,
    Guid? PlayerStartWaypointId,
    float PlayerOrientationDegrees,
    string GameMode,
    string EntryClass,
    string PlayerSelectionRule);

internal sealed record StudioProjectPreview(
    bool Succeeded,
    string Message,
    string Summary,
    ProjectDefinition? Project = null);

internal sealed record StudioProjectCreationResult(
    bool Succeeded,
    string Message,
    string? WorkspaceDirectory = null,
    ProjectDefinition? Project = null);

internal sealed record StudioUx0ESmokeResult(
    bool Succeeded,
    bool WizardPreview,
    bool InvalidRpgBlocked,
    bool CoreWithoutArea,
    bool LocalizedMetadata,
    bool StableIdentity,
    bool PlayerStart,
    bool OptionalWaypoint,
    bool AtomicUndoRedo,
    bool PersistenceRoundTrip,
    bool HumanDiagnostics);
