using System.Text.Json;
using Forge.Application.Assets;
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

        Console.Error.WriteLine(
            "Usage: forge package <export|validate|load|analyze|compliance|spatial|diagnose> ... | " +
            "forge assets list <repository-or-workspace> [filters]");
        return 2;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

static int RunAssets(IReadOnlyList<string> arguments)
{
    if (arguments.Count < 2 || arguments[0] != "list")
    {
        Console.Error.WriteLine(
            "Usage: forge assets list <repository-or-workspace> [--query <text>] [--source <source>] " +
            "[--category <category>] [--license <spdx>] [--availability <catalog|original|extracted>] " +
            "[--limit <count>] [--vault <directory>]");
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
