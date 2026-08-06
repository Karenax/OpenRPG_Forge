using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.ModuleFormat.Serialization;

public static class ProjectJsonSerializer
{
    public const string CurrentFormatVersion = "0.6.23";
    public const string PreCreaturePresentationFormatVersion = "0.6.22";
    public const string PreTerrainPaintFormatVersion = "0.6.21";
    public const string PreGovernanceQualityFormatVersion = "0.6.20";
    public const string PreQualityAssuranceFormatVersion = "0.6.19";
    public const string PreOperationalReadinessFormatVersion = "0.6.18";
    public const string PreModernToolingFormatVersion = "0.6.17";
    public const string PreRuntimeExtensionsFormatVersion = "0.6.16";
    public const string PreAreaExperienceFormatVersion = "0.6.15";
    public const string PreRulesProfileFormatVersion = "0.6.14";
    public const string PreGameplayControlFormatVersion = "0.6.13";
    public const string PreCinematicFormatVersion = "0.6.12";
    public const string PreRuntimePolicyFormatVersion = "0.6.11";
    public const string PreWorldProfileFormatVersion = "0.6.10";
    public const string PreClimateProfileFormatVersion = "0.6.9";
    public const string PreRenderingProfileFormatVersion = "0.6.8";
    public const string PreArtisticStyleFormatVersion = "0.6.7";
    public const string PreContentWorkspaceFormatVersion = "0.6.6";
    public const string PreLocalizationFormatVersion = "0.6.5";
    public const string PreNavigationFormatVersion = "0.6.4";
    public const string PreDistributionFormatVersion = "0.6.3";
    public const string PaletteProvenanceFormatVersion = "0.6.2";
    public const string SafeRenameFormatVersion = "0.6.1";
    public const string AreaMetadataFormatVersion = "0.6.0";
    public const string QuestFormatVersion = "0.5.0";
    public const string DialogueFormatVersion = "0.4.0";
    public const string EntityOverridesFormatVersion = "0.3.0";
    public const string ModuleAggregateFormatVersion = "0.2.0";
    public const string LegacyFormatVersion = "0.1.0";

    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static string Serialize(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        EnsureValid(project);

        return JsonSerializer.Serialize(
            new ProjectDocument(CurrentFormatVersion, ProjectData.FromDomain(project)),
            Options);
    }

    public static ProjectDefinition Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using var parsedDocument = JsonDocument.Parse(json);
        if (!parsedDocument.RootElement.TryGetProperty("formatVersion", out var formatVersionElement))
        {
            throw new JsonException("The project document does not declare a formatVersion.");
        }

        var formatVersion = formatVersionElement.GetString();
        ProjectDefinition project;
        if (string.Equals(formatVersion, CurrentFormatVersion, StringComparison.Ordinal) ||
            string.Equals(formatVersion, PreCreaturePresentationFormatVersion, StringComparison.Ordinal) ||
            string.Equals(formatVersion, PreTerrainPaintFormatVersion, StringComparison.Ordinal) ||
            string.Equals(formatVersion, PreGovernanceQualityFormatVersion, StringComparison.Ordinal) ||
            string.Equals(formatVersion, PreQualityAssuranceFormatVersion, StringComparison.Ordinal) ||
            string.Equals(formatVersion, PreOperationalReadinessFormatVersion, StringComparison.Ordinal) ||
            string.Equals(formatVersion, PreModernToolingFormatVersion, StringComparison.Ordinal) ||
            string.Equals(formatVersion, PreRuntimeExtensionsFormatVersion, StringComparison.Ordinal) ||
            string.Equals(formatVersion, PreAreaExperienceFormatVersion, StringComparison.Ordinal) ||
            string.Equals(formatVersion, PreRulesProfileFormatVersion, StringComparison.Ordinal) ||
            string.Equals(formatVersion, PreGameplayControlFormatVersion, StringComparison.Ordinal) ||
            string.Equals(formatVersion, PreCinematicFormatVersion, StringComparison.Ordinal) ||
            string.Equals(formatVersion, PreRuntimePolicyFormatVersion, StringComparison.Ordinal) ||
            string.Equals(formatVersion, PreWorldProfileFormatVersion, StringComparison.Ordinal) ||
            string.Equals(formatVersion, PreClimateProfileFormatVersion, StringComparison.Ordinal) ||
            string.Equals(formatVersion, PreRenderingProfileFormatVersion, StringComparison.Ordinal) ||
            string.Equals(formatVersion, PreArtisticStyleFormatVersion, StringComparison.Ordinal) ||
            string.Equals(formatVersion, PreContentWorkspaceFormatVersion, StringComparison.Ordinal) ||
            string.Equals(formatVersion, PreLocalizationFormatVersion, StringComparison.Ordinal) ||
            string.Equals(formatVersion, PreNavigationFormatVersion, StringComparison.Ordinal) ||
            string.Equals(formatVersion, PreDistributionFormatVersion, StringComparison.Ordinal) ||
            string.Equals(formatVersion, PaletteProvenanceFormatVersion, StringComparison.Ordinal) ||
            string.Equals(formatVersion, SafeRenameFormatVersion, StringComparison.Ordinal) ||
            string.Equals(formatVersion, AreaMetadataFormatVersion, StringComparison.Ordinal) ||
            string.Equals(formatVersion, QuestFormatVersion, StringComparison.Ordinal) ||
            string.Equals(formatVersion, DialogueFormatVersion, StringComparison.Ordinal) ||
            string.Equals(formatVersion, EntityOverridesFormatVersion, StringComparison.Ordinal) ||
            string.Equals(formatVersion, ModuleAggregateFormatVersion, StringComparison.Ordinal))
        {
            var document = JsonSerializer.Deserialize<ProjectDocument>(json, Options)
                ?? throw new JsonException("The project document is empty.");
            project = document.Project.ToDomain();
        }
        else if (string.Equals(formatVersion, LegacyFormatVersion, StringComparison.Ordinal))
        {
            var legacyDocument = JsonSerializer.Deserialize<LegacyProjectDocument>(json, Options)
                ?? throw new JsonException("The legacy project document is empty.");
            project = legacyDocument.Project.ToDomain();
        }
        else
        {
            throw new NotSupportedException(
                $"Project format '{formatVersion}' is not supported; expected '{CurrentFormatVersion}'.");
        }

        EnsureValid(project);
        return project;
    }

    private static void EnsureValid(ProjectDefinition project)
    {
        var diagnostics = ProjectValidator.Validate(project);
        if (diagnostics.Count > 0)
        {
            throw new ProjectValidationException(diagnostics);
        }
    }

    internal static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            WriteIndented = true,
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new AbilityIdJsonConverter());
        options.Converters.Add(new AssetCatalogItemIdJsonConverter());
        options.Converters.Add(new AssetPackageManifestIdJsonConverter());
        options.Converters.Add(new AssetExternalEditIdJsonConverter());
        options.Converters.Add(new ArtisticStyleProfileIdJsonConverter());
        options.Converters.Add(new AreaIdJsonConverter());
        options.Converters.Add(new AreaNavigationDefinitionIdJsonConverter());
        options.Converters.Add(new PortalIdJsonConverter());
        options.Converters.Add(new BlueprintIdJsonConverter());
        options.Converters.Add(new ContentWorkspaceConfigurationIdJsonConverter());
        options.Converters.Add(new DataColumnIdJsonConverter());
        options.Converters.Add(new DataRowIdJsonConverter());
        options.Converters.Add(new DataTableIdJsonConverter());
        options.Converters.Add(new DialogueChoiceIdJsonConverter());
        options.Converters.Add(new DialogueIdJsonConverter());
        options.Converters.Add(new DialogueNodeIdJsonConverter());
        options.Converters.Add(new EditorCameraBookmarkIdJsonConverter());
        options.Converters.Add(new EditorCameraProfileIdJsonConverter());
        options.Converters.Add(new EntityIdJsonConverter());
        options.Converters.Add(new LocalizationCatalogIdJsonConverter());
        options.Converters.Add(new ModuleIdJsonConverter());
        options.Converters.Add(new ModuleDistributionConfigurationIdJsonConverter());
        options.Converters.Add(new ModuleReleaseConfigurationIdJsonConverter());
        options.Converters.Add(new ModuleTargetProfileIdJsonConverter());
        options.Converters.Add(new NavigationAgentProfileIdJsonConverter());
        options.Converters.Add(new NavigationSurfaceIdJsonConverter());
        options.Converters.Add(new OffMeshNavigationLinkIdJsonConverter());
        options.Converters.Add(new ProjectMetadataIdJsonConverter());
        options.Converters.Add(new ProjectVariableIdJsonConverter());
        options.Converters.Add(new ProjectIdJsonConverter());
        options.Converters.Add(new QuestIdJsonConverter());
        options.Converters.Add(new QuestStateIdJsonConverter());
        options.Converters.Add(new QuestTransitionIdJsonConverter());
        options.Converters.Add(new ThirdPartyDependencyIdJsonConverter());
        options.Converters.Add(new Vector3ValueJsonConverter());
        options.Converters.Add(new QuaternionValueJsonConverter());
        options.Converters.Add(new TransformValueJsonConverter());

        return options;
    }
}
