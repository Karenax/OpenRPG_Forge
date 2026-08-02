using System.Text.Json;
using Forge.ModuleFormat.Build;
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

        Console.Error.WriteLine("Usage: forge package <export|validate|load|analyze> ...");
        return 2;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

static int RunPackage(IReadOnlyList<string> arguments)
{
    if (arguments.Count == 3 && arguments[0] == "export")
    {
        var result = ModulePackager.Export(arguments[1], arguments[2]);
        Console.WriteLine(
            $"OPENRPG_FORGE_MODULE_EXPORT_PASS slug={result.Manifest.Slug} files={result.FileCount} " +
            $"hash={result.Manifest.ContentSha256} rebuilt={result.BuildPlan.RebuiltResources.Count} " +
            $"reused={result.BuildPlan.ReusedResources.Count} dependencies={result.Manifest.Dependencies.Count}");
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

    Console.Error.WriteLine(
        "Usage: forge package export <workspace> <destination.orpmod> | " +
        "validate <module.orpmod> | load <module.orpmod> <cache> | analyze <workspace>");
    return 2;
}
