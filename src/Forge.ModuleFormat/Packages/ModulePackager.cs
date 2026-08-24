using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Forge.Domain.Assets;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.ModuleFormat.Audio;
using Forge.ModuleFormat.Content;
using Forge.ModuleFormat.Quality;
using Forge.Scripting;

namespace Forge.ModuleFormat.Packages;

public static class ModulePackager
{
    private static readonly DateTimeOffset DeterministicTimestamp = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static ModulePackageExportResult Export(
        string workspacePath,
        string destinationPath,
        string? moduleVersion = null,
        string? runtimeMinimumVersion = null,
        string forgeScriptApiVersion = "1.0.0",
        ModuleTargetKind target = ModuleTargetKind.Editor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        var workspaceRoot = Path.GetFullPath(workspacePath);
        var destination = Path.GetFullPath(destinationPath);
        if (!Directory.Exists(workspaceRoot))
        {
            throw new DirectoryNotFoundException($"Module workspace '{workspaceRoot}' does not exist.");
        }

        if (!string.Equals(Path.GetExtension(destination), ".orpmod", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Module destination must use the .orpmod extension.", nameof(destinationPath));
        }

        ProjectDefinition project;
        string projectPath;
        if (File.Exists(Path.Combine(workspaceRoot, ProjectSession.ManifestFileName)))
        {
            var opened = ProjectSession.Open(workspaceRoot);
            if (!opened.Succeeded || opened.Session is null)
            {
                throw new InvalidDataException(
                    "Module workspace is invalid: " + string.Join("; ", opened.Diagnostics.Select(item =>
                        $"{item.Code} at {item.Path}: {item.Message}")));
            }

            project = opened.Session.Project;
            projectPath = opened.Session.ProjectFilePath;
        }
        else
        {
            projectPath = Path.Combine(workspaceRoot, ProjectSession.DefaultProjectFileName);
            if (!File.Exists(projectPath))
            {
                throw new FileNotFoundException("Module workspace does not contain project.json.", projectPath);
            }

            var bareProjectDirectory = Path.GetDirectoryName(projectPath)
                ?? throw new InvalidDataException("The workspace project document has no parent directory.");
            project = WorkspaceProjectJsonSerializer.Deserialize(
                File.ReadAllText(projectPath),
                relativePath => ReadBareAreaDocument(bareProjectDirectory, relativePath));
        }

        var buildProfileLoad = WorkspaceBuildProfileStore.Load(workspaceRoot);
        if (!buildProfileLoad.Succeeded)
        {
            throw new InvalidDataException(
                $"Build profile is invalid: {string.Join("; ", buildProfileLoad.Diagnostics.Select(item => item.Message))}");
        }

        var assistantProfileLoad = AssistantWorkspaceProfileStore.Load(workspaceRoot);
        if (!assistantProfileLoad.Succeeded)
        {
            throw new InvalidDataException(
                "Assistant test automation profile is invalid before build: " + string.Join("; ",
                    assistantProfileLoad.Diagnostics.Select(item => $"{item.Code} at {item.Path}: {item.Message}")));
        }
        var compatibility = buildProfileLoad.Profile.Compatibility;
        runtimeMinimumVersion ??= compatibility.MinimumRuntimeVersion;

        var qualityProfileLoad = QualityFoundationProfileStore.Load(workspaceRoot, project.Id.Value);
        if (!qualityProfileLoad.Succeeded)
        {
            throw new InvalidDataException(
                "Canonical validation profile is invalid: " + string.Join("; ",
                    qualityProfileLoad.Diagnostics.Select(item => $"{item.Code} at {item.Path}: {item.Message}")));
        }
        var canonicalValidation = WorkspaceCanonicalValidationService.Run(
            workspaceRoot, project, qualityProfileLoad.Profile);
        var blockingValidationDiagnostics = WorkspaceCanonicalValidationService.BlockingDiagnostics(
            project, qualityProfileLoad.Profile, canonicalValidation);
        if (blockingValidationDiagnostics.Count > 0)
        {
            throw new InvalidOperationException(
                "Canonical validation failed before build: " + string.Join("; ",
                    blockingValidationDiagnostics.Select(item =>
                        $"{item.Code} at {item.Path} [{item.Id:D}]: {item.Message}")));
        }

        var complianceDiagnostics = DistributionComplianceValidator.Validate(project, target);
        if (complianceDiagnostics.Count > 0)
        {
            throw new InvalidOperationException(
                $"Target '{target}' is not distribution compliant: " +
                string.Join("; ", complianceDiagnostics.Select(diagnostic =>
                    $"{diagnostic.Code} at {diagnostic.Path}: {diagnostic.Message}")));
        }

        ValidateScriptPermissionPolicy(workspaceRoot, project);
        ValidateCreaturePresentationAssets(workspaceRoot, project);
        var contentCompilation = ContentPackageCompiler.Compile(
            workspaceRoot, project.Module.Id.Value, project.Module.Content.Packages);
        if (!contentCompilation.Success)
        {
            throw new InvalidOperationException(
                "Content package compilation failed: " + string.Join("; ", contentCompilation.Diagnostics.Select(item =>
                    $"{item.Code} at {item.Path}: {item.Message}")));
        }
        var audioDiagnostics = WorkspaceAudioValidator.Validate(workspaceRoot, project);
        if (audioDiagnostics.Count > 0)
        {
            throw new InvalidOperationException(
                "Audio distribution validation failed: " + string.Join("; ", audioDiagnostics.Select(item =>
                    $"{item.Code} at {item.Path}: {item.Message}")));
        }

        ValidateAssetGovernance(workspaceRoot, target);

        var bundle = WorkspaceProjectJsonSerializer.Serialize(project);
        var entries = new List<(string Path, byte[] Content)>
        {
            ("content/project.json", Encoding.UTF8.GetBytes(bundle.ProjectJson)),
            (DistributionNoticeBuilder.PackagePath,
                Encoding.UTF8.GetBytes(DistributionNoticeBuilder.Build(project, target))),
        };
        entries.AddRange(bundle.AreaDocuments.Select(item =>
            ($"content/{item.Key}", Encoding.UTF8.GetBytes(item.Value))));
        entries.AddRange(contentCompilation.GeneratedArtifacts.Select(item => (item.PackagePath, item.Content)));
        var projectDirectory = Path.GetDirectoryName(projectPath)
            ?? throw new InvalidDataException("The workspace project index has no parent directory.");
        var workspaceFiles = Directory.EnumerateFiles(workspaceRoot, "*", SearchOption.AllDirectories)
                     .Select(Path.GetFullPath)
                     .Where(file => !string.Equals(file, projectPath, StringComparison.OrdinalIgnoreCase))
                     .Where(file => !string.Equals(file, destination, StringComparison.OrdinalIgnoreCase))
                     .Where(file => !IsWorkspaceAreaDocument(projectDirectory, file))
                     .Where(file => !IsIgnoredWorkspaceFile(workspaceRoot, file))
                     .ToArray();
        var packageMaterialization = WorkspaceFileMaterializer.Materialize(workspaceRoot, workspaceFiles);
        foreach (var file in packageMaterialization.Results)
        {
            var packagePath = $"content/{file.RelativePath}";
            if (!ModulePackageValidator.IsSafeArchivePath(packagePath))
            {
                throw new InvalidOperationException($"Workspace file '{file.RelativePath}' does not map to a safe package path.");
            }

            if (entries.Any(entry => string.Equals(entry.Path, packagePath, StringComparison.Ordinal)))
            {
                continue;
            }

            entries.Add((packagePath, file.Content));
        }

        var buildGraph = WorkspaceBuildGraphBuilder.Build(workspaceRoot, project, projectPath, destination);
        var previousBuildGraph = WorkspaceBuildStateStore.TryLoad(workspaceRoot);
        var buildPlan = WorkspaceIncrementalBuildPlanner.Plan(previousBuildGraph, buildGraph.Graph);
        var hash = ModuleContentHash.Compute(entries);
        var manifest = ModuleManifest.Create(
            project,
            moduleVersion ?? project.Module.Metadata.Version,
            runtimeMinimumVersion,
            forgeScriptApiVersion,
            hash,
            buildGraph.AutomaticDependencies,
            target,
            compatibility.RequiredAtRuntime);
        var manifestDiagnostics = ModuleManifestValidator.Validate(manifest);
        if (manifestDiagnostics.Count > 0)
        {
            throw new InvalidOperationException(
                $"Module manifest is invalid: {string.Join("; ", manifestDiagnostics.Select(d => d.Message))}");
        }

        if (buildPlan.RebuiltResources.Count == 0 && File.Exists(destination))
        {
            var existing = ModulePackageValidator.Validate(destination);
            if (existing.IsValid && existing.Manifest is not null &&
                string.Equals(ModuleManifestJson.Serialize(existing.Manifest), ModuleManifestJson.Serialize(manifest),
                    StringComparison.Ordinal))
            {
                WorkspaceBuildStateStore.Save(workspaceRoot, buildGraph.Graph);
                WorkspaceBuildCollaborationService.SaveSemanticSnapshot(workspaceRoot,
                    WorkspaceBuildCollaborationService.CreateSemanticSnapshot(project, buildGraph.Graph));
                return new ModulePackageExportResult(
                    destination,
                    manifest,
                    entries.Count + 1,
                    new FileInfo(destination).Length,
                    buildGraph.Graph,
                    buildPlan,
                    packageMaterialization.Metrics);
            }
        }

        var destinationDirectory = Path.GetDirectoryName(destination)
            ?? throw new InvalidOperationException("Module destination has no parent directory.");
        Directory.CreateDirectory(destinationDirectory);
        var temporaryPath = Path.Combine(
            destinationDirectory,
            $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var archive = ZipFile.Open(temporaryPath, ZipArchiveMode.Create))
            {
                WriteEntry(archive, "manifest.json", Encoding.UTF8.GetBytes(ModuleManifestJson.Serialize(manifest)));
                foreach (var entry in entries.OrderBy(entry => entry.Path, StringComparer.Ordinal))
                {
                    WriteEntry(archive, entry.Path, entry.Content);
                }
            }

            File.Move(temporaryPath, destination, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }

        var validation = ModulePackageValidator.Validate(destination);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                $"Exported module failed validation: {string.Join("; ", validation.Diagnostics.Select(d => d.Message))}");
        }

        WorkspaceBuildStateStore.Save(workspaceRoot, buildGraph.Graph);
        WorkspaceBuildCollaborationService.SaveSemanticSnapshot(workspaceRoot,
            WorkspaceBuildCollaborationService.CreateSemanticSnapshot(project, buildGraph.Graph));

        return new ModulePackageExportResult(
            destination,
            manifest,
            entries.Count + 1,
            new FileInfo(destination).Length,
            buildGraph.Graph,
            buildPlan,
            packageMaterialization.Metrics);
    }

    private static bool IsIgnoredWorkspaceFile(string workspaceRoot, string file)
    {
        var segments = Path.GetRelativePath(workspaceRoot, file)
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(segment => segment is ".forge" or ".godot" or "bin" or "obj" or "artifacts" or "tmp" or "native") ||
               string.Equals(Path.GetExtension(file), ".import", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(Path.GetFileName(file), "forge.project.json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWorkspaceAreaDocument(string projectDirectory, string file)
    {
        var relative = Path.GetRelativePath(projectDirectory, file).Replace('\\', '/');
        return relative.StartsWith("areas/", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Path.GetExtension(relative), ".json", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadBareAreaDocument(string projectDirectory, string relativePath)
    {
        if (!WorkspaceProjectJsonSerializer.IsSafeAreaDocumentPath(relativePath))
        {
            throw new InvalidDataException($"Area document path '{relativePath}' is unsafe.");
        }

        var path = Path.GetFullPath(Path.Combine(
            projectDirectory,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var containedRoot = Path.GetFullPath(projectDirectory).TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        if (!path.StartsWith(containedRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            throw new FileNotFoundException($"Area document '{relativePath}' is missing.", path);
        }

        return File.ReadAllText(path);
    }

    private static void ValidateCreaturePresentationAssets(string workspaceRoot, ProjectDefinition project)
    {
        var containedRoot = Path.GetFullPath(workspaceRoot).TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        foreach (var blueprint in project.Blueprints.Where(item => item.CreaturePresentation is not null))
        {
            foreach (var portrait in blueprint.CreaturePresentation!.Portraits)
            {
                var path = Path.GetFullPath(Path.Combine(workspaceRoot,
                    portrait.AssetPath.Replace('/', Path.DirectorySeparatorChar)));
                if (!path.StartsWith(containedRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                {
                    throw new InvalidOperationException(
                        $"Creature portrait '{portrait.AssetPath}' for blueprint '{blueprint.Id}' is missing or unsafe.");
                }

                var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))
                    .ToLowerInvariant();
                if (!string.Equals(hash, portrait.ContentSha256, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Creature portrait '{portrait.AssetPath}' for blueprint '{blueprint.Id}' does not match its SHA-256.");
                }
            }
        }
    }

    private static void ValidateScriptPermissionPolicy(string workspaceRoot, ProjectDefinition project)
    {
        var store = new ForgeScriptModulePermissionStore(workspaceRoot);
        if (!File.Exists(store.Path))
            return;
        ForgeScriptModulePermissionPolicy policy;
        try
        {
            policy = store.Load();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or JsonException)
        {
            throw new InvalidOperationException(
                $"ForgeScript module permission validation failed: {exception.Message}", exception);
        }
        if (policy.ModuleId != project.Module.Id.Value)
        {
            throw new InvalidOperationException(
                "ForgeScript module permission validation failed: script.permission.module_mismatch");
        }
    }

    private static void ValidateAssetGovernance(string workspaceRoot, ModuleTargetKind target)
    {
        if (target is ModuleTargetKind.Editor or ModuleTargetKind.Test) return;
        var loaded = AssetGovernanceStore.Load(workspaceRoot);
        if (!loaded.Succeeded)
            throw new InvalidOperationException("Asset governance validation failed: " +
                string.Join("; ", loaded.Diagnostics.Select(item => $"{item.Code} at {item.Path}: {item.Message}")));
        if (loaded.FileMissing) return;
        var diagnostics = loaded.Records.SelectMany(record => AssetGovernanceValidator.Validate(record)).ToArray();
        if (diagnostics.Length > 0)
            throw new InvalidOperationException("Asset governance validation failed: " +
                string.Join("; ", diagnostics.Select(item => $"{item.Code} at {item.Path}: {item.Message}")));
    }

    private static void WriteEntry(ZipArchive archive, string path, byte[] content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        entry.LastWriteTime = DeterministicTimestamp;
        using var stream = entry.Open();
        stream.Write(content);
    }
}
