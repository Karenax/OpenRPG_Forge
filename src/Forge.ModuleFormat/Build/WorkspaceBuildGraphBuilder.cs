using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Build;
using Forge.Domain.Blueprints;
using Forge.Domain.Content;
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
        string? ignoredFilePath = null,
        int maximumParallelTasks = DeterministicBuildTaskScheduler.DefaultMaximumParallelism)
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

        var workspaceFiles = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .Select(Path.GetFullPath)
                     .Where(file => !string.Equals(file, canonicalProjectPath, StringComparison.OrdinalIgnoreCase))
                     .Where(file => !string.Equals(file, canonicalIgnoredPath, StringComparison.OrdinalIgnoreCase))
                     .Where(file => !IsWorkspaceAreaDocument(projectDirectory, file))
                     .Where(file => !IsIgnoredWorkspaceFile(root, file))
                     .ToArray();
        var materialization = WorkspaceFileMaterializer.Materialize(root, workspaceFiles, maximumParallelTasks);
        foreach (var file in materialization.Results)
        {
            content[file.RelativePath] = file.Content;
        }

        var usedBlueprints = ReadUsedBlueprints(project);
        var externalPackages = ReadExternalPackages(project, usedBlueprints);
        var externalNodes = externalPackages.Select(package => new BuildDependencyNode(
            BuildResourceId.ForPath(package.Path),
            package.Path,
            BuildResourceKind.ExternalPackage,
            package.ContentHash,
            package.LicenseSpdx,
            package.Provenance)).ToArray();
        var projectId = BuildResourceId.ForPath("project.json");
        var idsByPath = content.Keys.ToDictionary(path => path, BuildResourceId.ForPath, StringComparer.Ordinal);
        var usedLocalDependencies = LocalDependencies(usedBlueprints, idsByPath);
        var contentNodes = new List<BuildDependencyNode>();
        foreach (var item in content.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var dependencies = new List<BuildResourceId>();
            var kind = KindFor(item.Key);
            if (kind == BuildResourceKind.ProjectDocument)
            {
                dependencies.AddRange(externalNodes.Select(node => node.Id));
                dependencies.AddRange(usedLocalDependencies);
            }
            else if (kind == BuildResourceKind.AreaDocument)
            {
                dependencies.Add(projectId);
                var area = project.Areas.Single(candidate =>
                    item.Key.StartsWith($"areas/{candidate.Id}-", StringComparison.Ordinal));
                var areaBlueprintIds = area.Entities.Select(entity => entity.BlueprintId).ToHashSet();
                dependencies.AddRange(LocalDependencies(
                    usedBlueprints.Where(blueprint => areaBlueprintIds.Contains(blueprint.Id)), idsByPath));
                dependencies.AddRange(area.Entities
                    .SelectMany(entity => entity.Overrides.Properties)
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
                dependencies.Distinct().OrderBy(id => id.Value).ToArray()));
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
            externalPackages.Select(package => package.ManifestReference).ToArray(),
            materialization.Metrics);
    }

    private static IReadOnlyList<ExternalPackage> ReadExternalPackages(
        ProjectDefinition project,
        IReadOnlyList<BlueprintDefinition> usedBlueprints)
    {
        var packages = usedBlueprints
            .Where(blueprint => blueprint.PaletteOrigin == BlueprintPaletteOrigin.InstalledPack)
            .Select(blueprint => ParseExternalPackage(blueprint.Provenance))
            .ToDictionary(package => package.Path, StringComparer.Ordinal);
        var catalogByNamespace = project.Module.Content.Packages.Packages
            .ToDictionary(package => package.PackageNamespace, StringComparer.Ordinal);
        var queue = new Queue<ContentPackageDefinition>();
        foreach (var blueprint in usedBlueprints)
        {
            foreach (var dependency in blueprint.Authoring.Dependencies.Where(value =>
                         value.StartsWith("pack://", StringComparison.Ordinal)))
            {
                var packReference = ParsePackReference(dependency);
                if (catalogByNamespace.TryGetValue(packReference.Namespace, out var package) &&
                    string.Equals(package.Version, packReference.Version, StringComparison.Ordinal))
                    AddCatalogPackage(package);
            }
        }

        foreach (var root in packages.Values.ToArray())
        {
            var reference = ParsePackReference(root.Path);
            if (catalogByNamespace.TryGetValue(reference.Namespace, out var package) &&
                string.Equals(package.Version, reference.Version, StringComparison.Ordinal))
                queue.Enqueue(package);
        }

        var visited = new HashSet<Guid>();
        while (queue.TryDequeue(out var package))
        {
            if (!visited.Add(package.Id)) continue;
            AddCatalogPackage(package);
            foreach (var dependency in package.Dependencies.Where(item => item.Kind == ContentDependencyKind.Required))
            {
                if (!catalogByNamespace.TryGetValue(dependency.PackageNamespace, out var required))
                    throw new InvalidDataException(
                        $"Required package '{dependency.PackageNamespace}' is unavailable for '{package.PackageNamespace}'.");
                queue.Enqueue(required);
            }
        }

        return packages.Values
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

        void AddCatalogPackage(ContentPackageDefinition package)
        {
            var external = new ExternalPackage(
                $"pack://{package.PackageNamespace}/{package.Version}",
                $"{package.PackageNamespace}@{package.Version}",
                package.ContentSha256,
                package.LicenseSpdx,
                package.Provenance);
            packages.TryAdd(external.Path, external);
            queue.Enqueue(package);
        }
    }

    private static IReadOnlyList<BlueprintDefinition> ReadUsedBlueprints(ProjectDefinition project)
    {
        var byId = project.Blueprints.ToDictionary(blueprint => blueprint.Id);
        var byManifest = project.Blueprints.ToDictionary(
            blueprint => blueprint.Authoring.ManifestPath, blueprint => blueprint.Id, StringComparer.Ordinal);
        var used = project.Areas.SelectMany(area => area.Entities).Select(entity => entity.BlueprintId)
            .Concat(project.Dialogues.SelectMany(dialogue => dialogue.Nodes)
                .Where(node => node.SpeakerBlueprintId is not null)
                .Select(node => node.SpeakerBlueprintId!))
            .ToHashSet();
        var queue = new Queue<BlueprintId>(used);
        while (queue.TryDequeue(out var id))
        {
            if (!byId.TryGetValue(id, out var blueprint)) continue;
            foreach (var dependency in blueprint.Authoring.Dependencies)
            {
                if (byManifest.TryGetValue(dependency, out var dependencyId) && used.Add(dependencyId))
                    queue.Enqueue(dependencyId);
            }
        }
        return used.Where(byId.ContainsKey).Select(id => byId[id]).OrderBy(item => item.Id.Value).ToArray();
    }

    private static IReadOnlyList<BuildResourceId> LocalDependencies(
        IEnumerable<BlueprintDefinition> blueprints,
        IReadOnlyDictionary<string, BuildResourceId> idsByPath) => blueprints
        .SelectMany(blueprint => blueprint.Authoring.Dependencies
            .Append(blueprint.Authoring.ManifestPath)
            .Concat(blueprint.Authoring.PreviewPath is null ? [] : [blueprint.Authoring.PreviewPath]))
        .Where(path => !path.StartsWith("pack://", StringComparison.Ordinal))
        .Where(idsByPath.ContainsKey)
        .Select(path => idsByPath[path])
        .Distinct()
        .OrderBy(id => id.Value)
        .ToArray();

    private static (string Namespace, string Version) ParsePackReference(string value)
    {
        if (!value.StartsWith("pack://", StringComparison.Ordinal))
            throw new InvalidDataException("Package reference must use pack://namespace/version/resource.");
        var segments = value[7..].Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
            throw new InvalidDataException("Package reference must identify a namespace and version.");
        return (segments[0], segments[1]);
    }

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
        _ when IsScriptPath(path) => BuildResourceKind.Script,
        _ => BuildResourceKind.Asset,
    };

    private static bool IsScriptPath(string path) =>
        path.EndsWith(".forge", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".as", StringComparison.OrdinalIgnoreCase);

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
