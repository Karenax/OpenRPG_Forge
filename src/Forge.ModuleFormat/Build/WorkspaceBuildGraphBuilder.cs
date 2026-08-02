using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Build;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Build;

public static class WorkspaceBuildGraphBuilder
{
    public static WorkspaceBuildGraph Build(
        string workspacePath,
        ProjectDefinition project,
        string projectPath,
        string? ignoredFilePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        var root = Path.GetFullPath(workspacePath);
        var canonicalProjectPath = Path.GetFullPath(projectPath);
        var canonicalIgnoredPath = ignoredFilePath is null ? null : Path.GetFullPath(ignoredFilePath);
        var projectDirectory = Path.GetDirectoryName(canonicalProjectPath)
            ?? throw new InvalidDataException("The workspace project document has no parent directory.");
        var bundle = WorkspaceProjectJsonSerializer.Serialize(project);
        var content = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["project.json"] = Encoding.UTF8.GetBytes(bundle.ProjectJson),
        };
        foreach (var areaDocument in bundle.AreaDocuments)
        {
            content.Add(areaDocument.Key, Encoding.UTF8.GetBytes(areaDocument.Value));
        }

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .Select(Path.GetFullPath)
                     .Where(file => !string.Equals(file, canonicalProjectPath, StringComparison.OrdinalIgnoreCase))
                     .Where(file => !string.Equals(file, canonicalIgnoredPath, StringComparison.OrdinalIgnoreCase))
                     .Where(file => !IsWorkspaceAreaDocument(projectDirectory, file))
                     .Where(file => !IsIgnoredWorkspaceFile(root, file))
                     .OrderBy(file => file, StringComparer.OrdinalIgnoreCase))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            content[relative] = File.ReadAllBytes(file);
        }

        var externalPackages = ReadExternalPackages(project);
        var externalNodes = externalPackages.Select(package => new BuildDependencyNode(
            BuildResourceId.ForPath(package.Path),
            package.Path,
            BuildResourceKind.ExternalPackage,
            package.ContentHash,
            package.LicenseSpdx,
            package.Provenance)).ToArray();
        var projectId = BuildResourceId.ForPath("project.json");
        var idsByPath = content.Keys.ToDictionary(path => path, BuildResourceId.ForPath, StringComparer.Ordinal);
        var contentNodes = new List<BuildDependencyNode>();
        foreach (var item in content.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var dependencies = new List<BuildResourceId>();
            var kind = KindFor(item.Key);
            if (kind == BuildResourceKind.ProjectDocument)
            {
                dependencies.AddRange(externalNodes.Select(node => node.Id));
            }
            else if (kind == BuildResourceKind.AreaDocument)
            {
                dependencies.Add(projectId);
                var area = project.Areas.Single(candidate =>
                    item.Key.StartsWith($"areas/{candidate.Id}-", StringComparison.Ordinal));
                dependencies.AddRange(area.Entities
                    .SelectMany(entity => entity.Overrides.Properties)
                    .Where(property => property.Value.EndsWith(".forge", StringComparison.OrdinalIgnoreCase))
                    .Select(property => property.Value.Replace('\\', '/'))
                    .Where(idsByPath.ContainsKey)
                    .Select(path => idsByPath[path]));
            }

            contentNodes.Add(new BuildDependencyNode(
                idsByPath[item.Key],
                item.Key,
                kind,
                Sha256(item.Value),
                "PROJECT",
                "workspace-canonical",
                dependencies));
        }

        var packagePath = $"package/{project.Module.Slug}.orpmod";
        var allInputs = contentNodes.Cast<BuildDependencyNode>().Concat(externalNodes).ToArray();
        var packageHash = Sha256(Encoding.UTF8.GetBytes(string.Join(
            "\n",
            allInputs.OrderBy(node => node.Path, StringComparer.Ordinal)
                .Select(node => $"{node.Path}:{node.ContentSha256}"))));
        var packageNode = new BuildDependencyNode(
            BuildResourceId.ForPath(packagePath),
            packagePath,
            BuildResourceKind.ModulePackage,
            packageHash,
            project.Module.Metadata.License,
            "generated-package",
            allInputs.Select(node => node.Id).ToArray());
        var graph = new BuildDependencyGraph(
            BuildResourceId.ForPath($"build-graph/{project.Id}"),
            BuildDependencyGraph.CurrentSchemaVersion,
            "workspace-derived",
            [.. externalNodes, .. contentNodes, packageNode]);
        return new WorkspaceBuildGraph(
            graph,
            externalPackages.Select(package => package.ManifestReference).ToArray());
    }

    private static IReadOnlyList<ExternalPackage> ReadExternalPackages(ProjectDefinition project) =>
        project.Blueprints
            .Where(blueprint => project.Areas.SelectMany(area => area.Entities)
                .Any(entity => entity.BlueprintId == blueprint.Id) ||
                project.Dialogues.SelectMany(dialogue => dialogue.Nodes)
                    .Any(node => node.SpeakerBlueprintId == blueprint.Id))
            .Where(blueprint => blueprint.PaletteOrigin == BlueprintPaletteOrigin.InstalledPack)
            .Select(blueprint => ParseExternalPackage(blueprint.Provenance))
            .GroupBy(package => package.Path, StringComparer.Ordinal)
            .Select(group =>
            {
                var variants = group.Select(item => (item.ContentHash, item.LicenseSpdx)).Distinct().ToArray();
                if (variants.Length != 1)
                {
                    throw new InvalidDataException($"Installed package '{group.Key}' has conflicting provenance.");
                }

                return group.First();
            })
            .OrderBy(package => package.ManifestReference, StringComparer.Ordinal)
            .ToArray();

    private static ExternalPackage ParseExternalPackage(BlueprintProvenance provenance)
    {
        const string prefix = "pack://";
        if (!provenance.Source.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Installed palette provenance must use pack://slug/version/resource.");
        }

        var segments = provenance.Source[prefix.Length..].Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3 || string.IsNullOrWhiteSpace(segments[0]) || string.IsNullOrWhiteSpace(segments[1]))
        {
            throw new InvalidDataException("Installed palette provenance must identify package, version and resource.");
        }

        return new ExternalPackage(
            $"pack://{segments[0]}/{segments[1]}",
            $"{segments[0]}@{segments[1]}",
            provenance.ContentHash,
            provenance.LicenseSpdx,
            provenance.Source);
    }

    private static BuildResourceKind KindFor(string path) => path switch
    {
        "project.json" => BuildResourceKind.ProjectDocument,
        _ when path.StartsWith("areas/", StringComparison.Ordinal) => BuildResourceKind.AreaDocument,
        _ when path.EndsWith(".forge", StringComparison.OrdinalIgnoreCase) => BuildResourceKind.Script,
        _ => BuildResourceKind.Asset,
    };

    private static bool IsIgnoredWorkspaceFile(string workspaceRoot, string file)
    {
        var segments = Path.GetRelativePath(workspaceRoot, file)
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(segment => segment is ".forge" or ".godot" or "bin" or "obj" or "artifacts" or "tmp" or "native") ||
               string.Equals(Path.GetFileName(file), "forge.project.json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWorkspaceAreaDocument(string projectDirectory, string file)
    {
        var relative = Path.GetRelativePath(projectDirectory, file).Replace('\\', '/');
        return relative.StartsWith("areas/", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Path.GetExtension(relative), ".json", StringComparison.OrdinalIgnoreCase);
    }

    private static string Sha256(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private sealed record ExternalPackage(
        string Path,
        string ManifestReference,
        string ContentHash,
        string LicenseSpdx,
        string Provenance);
}
