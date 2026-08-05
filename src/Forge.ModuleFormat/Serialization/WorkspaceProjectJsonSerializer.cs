using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.ModuleFormat.Serialization;

internal static class WorkspaceProjectJsonSerializer
{
    internal const string CurrentFormatVersion = "1.14.0";
    internal const string PreQualityAssuranceFormatVersion = "1.13.0";
    internal const string PreOperationalReadinessFormatVersion = "1.12.0";
    internal const string PreModernToolingFormatVersion = "1.11.0";
    internal const string PreRuntimeExtensionsFormatVersion = "1.10.0";
    internal const string PreAreaExperienceFormatVersion = "1.9.0";
    internal const string PreRulesProfileFormatVersion = "1.8.0";
    internal const string PreGameplayControlFormatVersion = "1.7.0";
    internal const string PreCinematicFormatVersion = "1.6.0";
    internal const string PreRuntimePolicyFormatVersion = "1.5.0";
    internal const string PreWorldProfileFormatVersion = "1.4.0";
    internal const string PreClimateProfileFormatVersion = "1.3.0";
    internal const string PreRenderingProfileFormatVersion = "1.2.0";
    internal const string PreArtisticStyleFormatVersion = "1.1.0";
    internal const string PreContentWorkspaceFormatVersion = "1.0.0";
    internal const string PreLocalizationFormatVersion = "0.9.0";
    internal const string IncrementalBuildFormatVersion = "0.8.1";
    internal const string PaletteProvenanceFormatVersion = "0.8.0";
    internal const string IndependentAreasFormatVersion = "0.7.0";

    private static readonly JsonSerializerOptions Options = ProjectJsonSerializer.CreateOptions();

    internal static WorkspaceProjectBundle Serialize(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        EnsureValid(project);
        var areaDocuments = new Dictionary<string, string>(StringComparer.Ordinal);
        var references = new List<AreaDocumentReference>();
        foreach (var area in project.Areas)
        {
            var json = AreaJsonSerializer.Serialize(area);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))
                .ToLowerInvariant()[..16];
            var path = $"areas/{area.Id}-{hash}.json";
            areaDocuments.Add(path, json);
            references.Add(new AreaDocumentReference(area.Id, area.Slug, path));
        }

        var document = new WorkspaceProjectDocument(
            CurrentFormatVersion,
            new WorkspaceProjectData(
                project.Id,
                project.Title,
                project.Module,
                project.Blueprints,
                references,
                project.Dialogues,
                project.Quests));
        return new WorkspaceProjectBundle(
            project,
            JsonSerializer.Serialize(document, Options),
            areaDocuments);
    }

    internal static ProjectDefinition Deserialize(string json, Func<string, string> readAreaDocument)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ArgumentNullException.ThrowIfNull(readAreaDocument);
        using var parsed = JsonDocument.Parse(json);
        if (!parsed.RootElement.TryGetProperty("formatVersion", out var versionElement))
        {
            throw new JsonException("The project document does not declare a formatVersion.");
        }

        var version = versionElement.GetString();
        if (!string.Equals(version, CurrentFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(version, PreQualityAssuranceFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(version, PreOperationalReadinessFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(version, PreModernToolingFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(version, PreRuntimeExtensionsFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(version, PreAreaExperienceFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(version, PreRulesProfileFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(version, PreGameplayControlFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(version, PreCinematicFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(version, PreRuntimePolicyFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(version, PreWorldProfileFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(version, PreClimateProfileFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(version, PreRenderingProfileFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(version, PreArtisticStyleFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(version, PreContentWorkspaceFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(version, PreLocalizationFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(version, IncrementalBuildFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(version, PaletteProvenanceFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(version, IndependentAreasFormatVersion, StringComparison.Ordinal))
        {
            return ProjectJsonSerializer.Deserialize(json);
        }

        var document = JsonSerializer.Deserialize<WorkspaceProjectDocument>(json, Options)
            ?? throw new JsonException("The workspace project index is empty.");
        if (document.Project.Areas.Select(reference => reference.Id).Distinct().Count() !=
            document.Project.Areas.Count)
        {
            throw new JsonException("The workspace project index contains duplicate area IDs.");
        }

        if (document.Project.Areas.Select(reference => reference.DocumentPath)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() != document.Project.Areas.Count)
        {
            throw new JsonException("The workspace project index contains duplicate area document paths.");
        }

        var areas = new List<AreaDefinition>();
        foreach (var reference in document.Project.Areas)
        {
            if (!IsSafeAreaDocumentPath(reference.DocumentPath))
            {
                throw new JsonException(
                    $"Area document path '{reference.DocumentPath}' must be a safe relative JSON path under areas/.");
            }

            var area = AreaJsonSerializer.Deserialize(readAreaDocument(reference.DocumentPath));
            if (area.Id != reference.Id || !string.Equals(area.Slug, reference.Slug, StringComparison.Ordinal))
            {
                throw new JsonException(
                    $"Area document '{reference.DocumentPath}' identity does not match its project index reference.");
            }

            areas.Add(area);
        }

        var project = new ProjectDefinition(
            document.Project.Id,
            document.Project.Title,
            document.Project.Module,
            document.Project.Blueprints,
            areas,
            document.Project.Dialogues ?? [],
            document.Project.Quests ?? []);
        EnsureValid(project);
        return project;
    }

    internal static bool IsSafeAreaDocumentPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\'))
        {
            return false;
        }

        var segments = path.Split('/');
        return segments.Length == 2 &&
            string.Equals(segments[0], "areas", StringComparison.Ordinal) &&
            segments[1] is not "." and not ".." &&
            string.Equals(Path.GetExtension(segments[1]), ".json", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureValid(ProjectDefinition project)
    {
        var diagnostics = ProjectValidator.Validate(project);
        if (diagnostics.Count > 0)
        {
            throw new ProjectValidationException(diagnostics);
        }
    }
}
