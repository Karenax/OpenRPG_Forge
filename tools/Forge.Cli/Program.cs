using System.Text.Json;
using System.Security.Cryptography;
using Forge.Application.Assets;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Assets;
using Forge.Domain.Modules;
using Forge.Domain.Navigation;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Assets;
using Forge.ModuleFormat.Navigation;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Workspaces;

return Run(args);

static int Run(IReadOnlyList<string> arguments)
{
    try
    {
        if (arguments.Count >= 1 && arguments[0] == "package")
        {
            return RunPackage(arguments.Skip(1).ToArray());
        }

        if (arguments.Count >= 1 && arguments[0] == "assets")
        {
            return RunAssets(arguments.Skip(1).ToArray());
        }

        if (arguments.Count == 2 && arguments[0] == "author-original")
        {
            return AuthorOriginalModule(arguments[1]);
        }

        Console.Error.WriteLine(
            "Usage: forge package <export|validate|load|analyze|compliance|spatial|diagnose> ... | " +
            "forge assets <list|prepare|roundtrip> ... | forge author-original <target-directory>");
        return 2;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

static int AuthorOriginalModule(string targetDirectory)
{
    var authored = OriginalModuleAuthoringService.CreateVeilleeDesEclats();
    var fullPath = Path.GetFullPath(targetDirectory);
    ProjectSessionOpenResult created;
    if (Directory.Exists(fullPath) && File.Exists(Path.Combine(fullPath, ProjectSession.ManifestFileName)))
    {
        var opened = ProjectSession.Open(fullPath);
        if (!opened.Succeeded || opened.Session?.Project.Module.Slug != "la-veillee-des-eclats")
        {
            Console.WriteLine(JsonSerializer.Serialize(new { valid = false, diagnostics = opened.Diagnostics }));
            return 1;
        }

        var saved = opened.Session.Save(authored.State.Project);
        created = new ProjectSessionOpenResult(saved.Succeeded ? opened.Session : null, saved.Diagnostics);
    }
    else
    {
        created = ProjectSession.Create(fullPath, authored.State.Project);
    }
    if (!created.Succeeded || created.Session is null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { valid = false, diagnostics = created.Diagnostics }));
        return 1;
    }

    Console.WriteLine(JsonSerializer.Serialize(new
    {
        valid = true,
        slug = created.Session.Project.Module.Slug,
        revision = authored.State.Revision.Value,
        commands = authored.Patches.Count,
        areas = created.Session.Project.Areas.Count,
        entities = created.Session.Project.Areas.Sum(area => area.Entities.Count),
        blueprints = created.Session.Project.Blueprints.Count,
        dialogues = created.Session.Project.Dialogues.Count,
        quests = created.Session.Project.Quests.Count,
    }));
    Console.WriteLine($"OPENRPG_FORGE_ORIGINAL_MODULE_AUTHORED slug={created.Session.Project.Module.Slug} commands={authored.Patches.Count} revision={authored.State.Revision.Value}");
    return 0;
}

static int RunAssets(IReadOnlyList<string> arguments)
{
    if (arguments.Count >= 3 && arguments[0] == "prepare")
    {
        return PrepareAsset(arguments);
    }

    if (arguments.Count >= 3 && arguments[0] == "roundtrip")
    {
        return ConfigureAssetRoundTrip(arguments);
    }

    if (arguments.Count < 2 || arguments[0] != "list")
    {
        Console.Error.WriteLine(
            "Usage: forge assets list <repository-or-workspace> [--query <text>] [--source <source>] " +
            "[--category <category>] [--license <spdx>] [--availability <catalog|original|extracted>] " +
            "[--limit <count>] [--vault <directory>] | " +
            "forge assets prepare <repository-or-workspace> <workspace> --id <external-id> " +
            "[--kind <model|texture>] [--vfx <true|false>] [--vault <directory>] | " +
            "forge assets roundtrip <repository-or-workspace> <workspace> --id <external-id> " +
            "[--watch <true|false>] [--dry-run <true|false>] [--vault <directory>]");
        return 2;
    }

    var options = ParseOptions(arguments.Skip(2).ToArray());
    var start = Path.GetFullPath(arguments[1]);
    var repositoryRoot = AssetCatalogLoader.FindRepositoryRoot(start);
    if (repositoryRoot is null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            valid = false,
            diagnostics = new[] { new { code = AssetCatalogLoader.CatalogMissingCode, path = start } },
        }));
        return 1;
    }

    var load = AssetCatalogLoader.Load(repositoryRoot, options.GetValueOrDefault("vault"));
    if (load.Catalog is null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { valid = false, diagnostics = load.Diagnostics }));
        return 1;
    }

    var query = new AssetCatalogQuery(
        options.GetValueOrDefault("query"),
        options.GetValueOrDefault("source"),
        options.GetValueOrDefault("category"),
        options.GetValueOrDefault("license"),
        ParseAvailability(options.GetValueOrDefault("availability")),
        ParseLimit(options.GetValueOrDefault("limit")));
    var browser = new AssetCatalogBrowser(load.Catalog);
    var items = browser.Search(query);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        valid = load.Diagnostics.Count == 0,
        schemaVersion = load.Catalog.SchemaVersion,
        load.Catalog.CatalogDate,
        load.Catalog.LicensePolicy,
        catalogCount = load.Catalog.Items.Count,
        resultCount = items.Count,
        sources = browser.Sources,
        categories = browser.Categories,
        diagnostics = load.Diagnostics,
        items = items.Select(item => new
        {
            id = item.Id.ToString(),
            item.SchemaVersion,
            item.ExternalId,
            item.DisplayName,
            item.Source,
            item.SourceAssetId,
            item.SourcePage,
            item.LicenseSpdx,
            item.LicenseVerified,
            item.ContentSha256,
            item.TotalBytes,
            item.Categories,
            item.Tags,
            item.CatalogStatus,
            availability = item.Availability.ToString(),
            item.RelativeModelPath,
            item.RelativePreviewPath,
            item.ThumbnailUrl,
            item.CanPlanImport,
            item.Provenance,
        }),
    }));
    Console.WriteLine(
        $"OPENRPG_FORGE_ASSET_BROWSER_PASS catalog={load.Catalog.Items.Count} results={items.Count} " +
        $"sources={browser.Sources.Count} schema={load.Catalog.SchemaVersion}");
    return load.Diagnostics.Count == 0 ? 0 : 1;
}

static int PrepareAsset(IReadOnlyList<string> arguments)
{
    var options = ParseOptions(arguments.Skip(3).ToArray());
    if (!options.TryGetValue("id", out var externalId) || string.IsNullOrWhiteSpace(externalId))
    {
        throw new ArgumentException("Asset preparation requires --id <external-id>.");
    }

    var kind = options.GetValueOrDefault("kind")?.Trim().ToLowerInvariant() switch
    {
        "texture" => AssetProcessingKind.Texture,
        "model" or null => AssetProcessingKind.Model,
        var value => throw new ArgumentException($"Unknown asset processing kind '{value}'."),
    };
    var start = Path.GetFullPath(arguments[1]);
    var workspaceRoot = Path.GetFullPath(arguments[2]);
    var repositoryRoot = AssetCatalogLoader.FindRepositoryRoot(start)
        ?? throw new DirectoryNotFoundException("OpenRPG Forge repository root was not found.");
    var load = AssetCatalogLoader.Load(repositoryRoot, options.GetValueOrDefault("vault"));
    if (load.Catalog is null || load.Diagnostics.Count > 0)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { valid = false, diagnostics = load.Diagnostics }));
        return 1;
    }

    var item = load.Catalog.Items.SingleOrDefault(candidate =>
        string.Equals(candidate.ExternalId, externalId, StringComparison.OrdinalIgnoreCase));
    if (item is null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            valid = false,
            diagnostics = new[] { new { code = "asset.import.asset_missing", path = externalId } },
        }));
        return 1;
    }

    var loadedProfiles = AssetProcessingProfileStore.Load(workspaceRoot);
    if (!loadedProfiles.Succeeded)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { valid = false, diagnostics = loadedProfiles.Diagnostics }));
        return 1;
    }

    var state = new AssetProcessingProfileState(loadedProfiles.Profiles);
    var profile = state.Find(item.Id, kind) ?? AssetImportPlanner.CreateDefaultProfile(item, kind);
    if (state.Find(item.Id, kind) is null)
    {
        var command = new SetAssetProcessingProfileCommand(
            CommandId.From(Guid.NewGuid()),
            state.Revision,
            profile,
            DryRun: false);
        var authored = new SetAssetProcessingProfileHandler(new SystemClock()).Handle(state, command);
        if (!authored.Succeeded)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { valid = false, diagnostics = authored.Diagnostics }));
            return 1;
        }

        var saved = AssetProcessingProfileStore.Save(workspaceRoot, authored.State.Profiles);
        if (!saved.Succeeded)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { valid = false, diagnostics = saved.Diagnostics }));
            return 1;
        }

        profile = authored.State.Find(item.Id, kind)!;
    }

    var planned = AssetImportPlanner.Plan(item, profile);
    if (!planned.Succeeded || planned.Plan is null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { valid = false, diagnostics = planned.Diagnostics }));
        return 1;
    }

    var execution = AssetImportExecutor.Execute(planned.Plan, load.VaultRoot, workspaceRoot);
    AssetPackageManifest? packageManifest = null;
    if (execution.Succeeded)
    {
        var loadedManifests = AssetPackageManifestStore.Load(workspaceRoot);
        if (!loadedManifests.Succeeded)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { valid = false, diagnostics = loadedManifests.Diagnostics }));
            return 1;
        }

        var includeVfx = options.TryGetValue("vfx", out var vfxValue) &&
            bool.TryParse(vfxValue, out var parsedVfx) && parsedVfx;
        var prepared = execution.Execution!;
        packageManifest = AssetPackageManifestFactory.Create(
            item,
            profile,
            new AssetManifestArtifactInput(
                prepared.OutputRelativePath,
                prepared.OutputContentSha256,
                prepared.OutputBytes,
                prepared.Status == AssetImportExecutionStatus.CanonicalReady),
            AssetPackageManifestAuthoringOptions.Default with { IncludeVfx = includeVfx });
        var manifestState = new AssetPackageManifestState(loadedManifests.Manifests);
        var manifestCommand = new SetAssetPackageManifestCommand(
            CommandId.From(Guid.NewGuid()),
            manifestState.Revision,
            packageManifest,
            DryRun: false);
        var authoredManifest = new SetAssetPackageManifestHandler(new SystemClock())
            .Handle(manifestState, manifestCommand);
        if (!authoredManifest.Succeeded)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { valid = false, diagnostics = authoredManifest.Diagnostics }));
            return 1;
        }

        var savedManifest = AssetPackageManifestStore.Save(workspaceRoot, authoredManifest.State.Manifests);
        if (!savedManifest.Succeeded)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { valid = false, diagnostics = savedManifest.Diagnostics }));
            return 1;
        }
    }

    Console.WriteLine(JsonSerializer.Serialize(new
    {
        valid = execution.Succeeded,
        asset = item.ExternalId,
        kind = kind.ToString(),
        cacheKey = planned.Plan.CacheKey,
        execution = execution.Execution,
        manifest = packageManifest is null
            ? null
            : new
            {
                id = packageManifest.Id.ToString(),
                chunks = packageManifest.Chunks.Count,
                collision = packageManifest.Collision is not null,
                animation = packageManifest.Animation is not null,
                vfx = packageManifest.Vfx is not null,
                lod = packageManifest.Lod?.Levels.Count ?? 0,
            },
        diagnostics = execution.Diagnostics,
    }));
    if (execution.Succeeded)
    {
        Console.WriteLine(
            $"OPENRPG_FORGE_ASSET_PREPARE_PASS asset={item.ExternalId} kind={kind.ToString().ToLowerInvariant()} " +
            $"status={execution.Execution!.Status.ToString().ToLowerInvariant()} reused={execution.Execution.Reused.ToString().ToLowerInvariant()} " +
            $"manifest={packageManifest!.Id} chunks={packageManifest.Chunks.Count}");
    }

    return execution.Succeeded ? 0 : 1;
}

static int ConfigureAssetRoundTrip(IReadOnlyList<string> arguments)
{
    var options = ParseOptions(arguments.Skip(3).ToArray());
    if (!options.TryGetValue("id", out var externalId) || string.IsNullOrWhiteSpace(externalId))
    {
        throw new ArgumentException("Asset round-trip requires --id <external-id>.");
    }

    var start = Path.GetFullPath(arguments[1]);
    var workspaceRoot = Path.GetFullPath(arguments[2]);
    var repositoryRoot = AssetCatalogLoader.FindRepositoryRoot(start)
        ?? throw new DirectoryNotFoundException("OpenRPG Forge repository root was not found.");
    var load = AssetCatalogLoader.Load(repositoryRoot, options.GetValueOrDefault("vault"));
    if (load.Catalog is null || load.Diagnostics.Count > 0)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { valid = false, diagnostics = load.Diagnostics }));
        return 1;
    }

    var item = load.Catalog.Items.SingleOrDefault(candidate =>
        string.Equals(candidate.ExternalId, externalId, StringComparison.OrdinalIgnoreCase));
    if (item?.RelativeModelPath is null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            valid = false,
            diagnostics = new[] { new { code = "asset.external_edit.asset_missing", path = externalId } },
        }));
        return 1;
    }

    var manifests = AssetPackageManifestStore.Load(workspaceRoot);
    var edits = AssetExternalEditStore.Load(workspaceRoot);
    if (!manifests.Succeeded || !edits.Succeeded)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            valid = false,
            diagnostics = manifests.Diagnostics.Concat(edits.Diagnostics),
        }));
        return 1;
    }

    var manifest = manifests.Manifests.SingleOrDefault(candidate => candidate.AssetId == item.Id);
    if (manifest is null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            valid = false,
            diagnostics = new[]
            {
                new { code = "asset.external_edit.manifest_missing", path = $"assets/{item.Id}" },
            },
        }));
        return 1;
    }

    var sourcePath = Path.GetFullPath(Path.Combine(
        load.VaultRoot,
        item.RelativeModelPath.Replace('/', Path.DirectorySeparatorChar)));
    var relativeSourcePath = Path.GetRelativePath(Path.GetFullPath(load.VaultRoot), sourcePath);
    if (!File.Exists(sourcePath) ||
        relativeSourcePath.Equals("..", StringComparison.Ordinal) ||
        relativeSourcePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        Path.IsPathRooted(relativeSourcePath))
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            valid = false,
            diagnostics = new[] { new { code = "asset.external_edit.source_missing", path = item.RelativeModelPath } },
        }));
        return 1;
    }

    var watch = !options.TryGetValue("watch", out var watchValue) ||
        !bool.TryParse(watchValue, out var parsedWatch) || parsedWatch;
    var dryRun = options.TryGetValue("dry-run", out var dryRunValue) &&
        bool.TryParse(dryRunValue, out var parsedDryRun) && parsedDryRun;
    var configuration = AssetExternalEditFactory.CreateBlenderRoundTrip(
        item,
        manifest,
        HashFileSha256(sourcePath),
        watch);
    var history = new AssetExternalEditCommandHistory(edits.Configurations);
    var command = new SetAssetExternalEditCommand(
        CommandId.From(Guid.NewGuid()),
        history.State.Revision,
        configuration,
        dryRun);
    var authored = new SetAssetExternalEditHandler(new SystemClock()).Handle(history.State, command);
    if (!authored.Succeeded)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { valid = false, diagnostics = authored.Diagnostics }));
        return 1;
    }

    BlenderWorkerRequestSaveResult? request = null;
    if (!dryRun)
    {
        history.Record(authored);
        var saved = AssetExternalEditStore.Save(workspaceRoot, history.State.Configurations);
        if (!saved.Succeeded)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { valid = false, diagnostics = saved.Diagnostics }));
            return 1;
        }

        var lodRatios = manifest.Lod?.Levels
            .Where(level => level.Level > 0)
            .Select(level => level.ScreenRatio)
            .ToArray() ?? [];
        request = BlenderWorkerRequestStore.Save(
            workspaceRoot,
            configuration,
            BlenderWorkerOptions.Default with
            {
                LodRatios = lodRatios,
                GenerateCollision = manifest.Collision is not null,
            });
        if (!request.Succeeded)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { valid = false, diagnostics = request.Diagnostics }));
            return 1;
        }
    }

    Console.WriteLine(JsonSerializer.Serialize(new
    {
        valid = true,
        asset = item.ExternalId,
        configuration = configuration.Id.ToString(),
        manifest = configuration.ManifestId.ToString(),
        configuration.Tool,
        configuration.Operations,
        configuration.TargetPlatforms,
        configuration.WatchForChanges,
        dryRun,
        request = request?.RequestRelativePath,
        diagnostics = Array.Empty<object>(),
    }));
    Console.WriteLine(
        $"OPENRPG_FORGE_ASSET_ROUNDTRIP_PASS asset={item.ExternalId} dryrun={dryRun.ToString().ToLowerInvariant()} " +
        $"operations={configuration.Operations.Count} request={request?.RequestRelativePath ?? "none"}");
    return 0;
}

static int RunPackage(IReadOnlyList<string> arguments)
{
    if ((arguments.Count == 3 || arguments.Count == 5) && arguments[0] == "export")
    {
        var target = arguments.Count == 5 && arguments[3] == "--target"
            ? ParseTarget(arguments[4])
            : ModuleTargetKind.Editor;
        var result = ModulePackager.Export(arguments[1], arguments[2], target: target);
        Console.WriteLine(
            $"OPENRPG_FORGE_MODULE_EXPORT_PASS slug={result.Manifest.Slug} files={result.FileCount} " +
            $"hash={result.Manifest.ContentSha256} rebuilt={result.BuildPlan.RebuiltResources.Count} " +
            $"reused={result.BuildPlan.ReusedResources.Count} dependencies={result.Manifest.Dependencies.Count} " +
            $"target={result.Manifest.Target.ToString().ToLowerInvariant()} notices=generated");
        return 0;
    }

    if (arguments.Count == 2 && arguments[0] == "validate")
    {
        var result = ModulePackageValidator.Validate(arguments[1]);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            valid = result.IsValid,
            module = result.Manifest?.Slug,
            diagnostics = result.Diagnostics,
        }));
        if (result.IsValid)
        {
            Console.WriteLine($"OPENRPG_FORGE_MODULE_VALIDATION_PASS slug={result.Manifest!.Slug}");
        }

        return result.IsValid ? 0 : 1;
    }

    if (arguments.Count == 3 && arguments[0] == "load")
    {
        var result = ModulePackageLoader.Load(arguments[1], arguments[2]);
        Console.WriteLine(
            $"OPENRPG_FORGE_MODULE_LOAD_PASS slug={result.Manifest.Slug} " +
            $"cache_hit={result.CacheHit.ToString().ToLowerInvariant()}");
        return 0;
    }

    if (arguments.Count == 2 && arguments[0] == "analyze")
    {
        var opened = ProjectSession.Open(arguments[1]);
        if (!opened.Succeeded || opened.Session is null)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                valid = false,
                diagnostics = opened.Diagnostics,
            }));
            return 1;
        }

        var session = opened.Session;
        var build = WorkspaceBuildGraphBuilder.Build(
            session.RootDirectory,
            session.Project,
            session.ProjectFilePath);
        var plan = WorkspaceIncrementalBuildPlanner.Plan(
            WorkspaceBuildStateStore.TryLoad(session.RootDirectory),
            build.Graph);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            valid = true,
            schemaVersion = build.Graph.SchemaVersion,
            graphId = build.Graph.Id.ToString(),
            resources = build.Graph.Nodes.Select(node => new
            {
                id = node.Id.ToString(),
                node.Path,
                kind = node.Kind.ToString(),
                node.ContentSha256,
                dependencies = node.Dependencies.Select(dependency => dependency.ToString()),
            }),
            automaticDependencies = build.AutomaticDependencies,
            targetProfiles = session.Project.Module.Distribution.TargetProfiles.Select(profile => new
            {
                target = profile.Target.ToString(),
                profile.Enabled,
                profile.RequireApprovedLicenses,
                profile.OutputName,
                profile.SecretsProfile,
            }),
            approvedDependencies = session.Project.Module.Distribution.Dependencies.Select(dependency => new
            {
                id = dependency.Id.ToString(),
                dependency.Name,
                dependency.Version,
                dependency.LicenseSpdx,
                dependency.Owner,
                targets = dependency.RequiredForTargets.Select(target => target.ToString()),
            }),
            changed = plan.ChangedPaths,
            removed = plan.RemovedPaths,
            rebuilt = plan.RebuiltResources.Select(node => node.Path),
            reused = plan.ReusedResources.Select(node => node.Path),
        }));
        Console.WriteLine(
            $"OPENRPG_FORGE_BUILD_ANALYZE_PASS resources={build.Graph.Nodes.Count} " +
            $"rebuilt={plan.RebuiltResources.Count} reused={plan.ReusedResources.Count} " +
            $"dependencies={build.AutomaticDependencies.Count}");
        return 0;
    }

    if (arguments.Count == 3 && arguments[0] == "compliance")
    {
        var opened = ProjectSession.Open(arguments[1]);
        if (!opened.Succeeded || opened.Session is null)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                valid = false,
                diagnostics = opened.Diagnostics,
            }));
            return 1;
        }

        var target = ParseTarget(arguments[2]);
        var diagnostics = DistributionComplianceValidator.Validate(opened.Session.Project, target);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            valid = diagnostics.Count == 0,
            target = target.ToString(),
            diagnostics,
        }));
        if (diagnostics.Count == 0)
        {
            Console.WriteLine(
                $"OPENRPG_FORGE_DISTRIBUTION_COMPLIANCE_PASS target={target.ToString().ToLowerInvariant()}");
        }

        return diagnostics.Count == 0 ? 0 : 1;
    }

    if (arguments.Count == 2 && arguments[0] == "spatial")
    {
        var opened = ProjectSession.Open(arguments[1]);
        if (!opened.Succeeded || opened.Session is null)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { valid = false, diagnostics = opened.Diagnostics }));
            return 1;
        }

        var project = opened.Session.Project;
        var diagnostics = new List<DomainDiagnostic>();
        var surfaces = new List<NavigationSurface>();
        foreach (var area in project.Areas)
        {
            diagnostics.AddRange(AreaGeometryValidator.Validate(project, area));
            foreach (var analysis in AreaNavigationValidator.AnalyzeAll(project, area))
            {
                diagnostics.AddRange(analysis.Diagnostics);
                surfaces.Add(analysis.Surface);
                NavigationBakeStore.Save(opened.Session.RootDirectory, analysis.Surface);
            }
        }

        var distinct = diagnostics.Distinct().ToArray();
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            valid = distinct.Length == 0,
            areas = project.Areas.Count,
            profiles = surfaces.Count,
            cells = surfaces.Sum(surface => surface.WalkableCells.Count),
            offMeshEdges = surfaces.Sum(surface => surface.OffMeshEdges.Count),
            diagnostics = distinct,
        }));
        if (distinct.Length == 0)
        {
            Console.WriteLine(
                $"OPENRPG_FORGE_SPATIAL_VALIDATION_PASS areas={project.Areas.Count} profiles={surfaces.Count} " +
                $"cells={surfaces.Sum(surface => surface.WalkableCells.Count)} cache=versioned");
        }

        return distinct.Length == 0 ? 0 : 1;
    }

    if (arguments.Count == 2 && arguments[0] == "diagnose")
    {
        var opened = ProjectSession.Open(arguments[1]);
        if (!opened.Succeeded || opened.Session is null)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { valid = false, diagnostics = opened.Diagnostics }));
            return 1;
        }

        var diagnostics = ProjectDiagnosticCatalog.Build(opened.Session.Project);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            valid = diagnostics.All(diagnostic => diagnostic.Severity != DiagnosticSeverity.Error),
            schemaVersion = ActionableDiagnostic.CurrentSchemaVersion,
            diagnostics = diagnostics.Select(diagnostic => new
            {
                id = diagnostic.Id.ToString(),
                diagnostic.SchemaVersion,
                severity = diagnostic.Severity.ToString(),
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.DocumentPath,
                subjectUuid = diagnostic.SubjectUuid?.ToString("D"),
                diagnostic.Property,
                diagnostic.Source,
                diagnostic.Provenance,
                quickFixes = diagnostic.QuickFixes.Select(fix => new
                {
                    id = fix.Id.ToString(),
                    fix.SchemaVersion,
                    fix.Label,
                    fix.Description,
                    kind = fix.Kind.ToString(),
                    areaId = fix.AreaId.ToString(),
                    entityId = fix.EntityId?.ToString(),
                    fix.Provenance,
                }),
            }),
        }));
        Console.WriteLine(
            $"OPENRPG_FORGE_DIAGNOSTICS_PASS count={diagnostics.Count} " +
            $"actionable={diagnostics.Count(diagnostic => diagnostic.QuickFixes.Count > 0)}");
        return diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) ? 1 : 0;
    }

    Console.Error.WriteLine(
        "Usage: forge package export <workspace> <destination.orpmod> [--target <profile>] | " +
        "validate <module.orpmod> | load <module.orpmod> <cache> | analyze <workspace> | " +
        "compliance <workspace> <profile> | spatial <workspace> | diagnose <workspace>");
    return 2;
}

static ModuleTargetKind ParseTarget(string value)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(value);
    var normalized = value.Replace("-", string.Empty, StringComparison.Ordinal)
        .Replace("_", string.Empty, StringComparison.Ordinal);
    return Enum.TryParse<ModuleTargetKind>(normalized, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
        ? parsed
        : throw new ArgumentException(
            $"Unknown target '{value}'. Expected Editor, Client, DedicatedServer, Test or Shipping.",
            nameof(value));
}

static Dictionary<string, string> ParseOptions(IReadOnlyList<string> arguments)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var index = 0; index < arguments.Count; index++)
    {
        var argument = arguments[index];
        if (!argument.StartsWith("--", StringComparison.Ordinal) || index + 1 >= arguments.Count)
        {
            throw new ArgumentException($"Invalid asset browser option '{argument}'.");
        }

        result[argument[2..]] = arguments[++index];
    }

    return result;
}

static AssetAvailability? ParseAvailability(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return null;
    }

    return value.Trim().ToLowerInvariant() switch
    {
        "catalog" => AssetAvailability.CatalogOnly,
        "original" => AssetAvailability.OriginalAvailable,
        "extracted" => AssetAvailability.ExtractedAvailable,
        _ => throw new ArgumentException(
            $"Unknown asset availability '{value}'. Expected catalog, original or extracted."),
    };
}

static int ParseLimit(string? value) => string.IsNullOrWhiteSpace(value)
    ? 50
    : int.TryParse(value, out var parsed) && parsed is >= 1 and <= 10_000
        ? parsed
        : throw new ArgumentException("Asset result limit must be between 1 and 10000.");

static string HashFileSha256(string path)
{
    using var stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}
