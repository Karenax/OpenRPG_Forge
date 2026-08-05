using System.Text.Json;
using System.Security.Cryptography;
using Forge.Application.Assets;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Application.Validation;
using Forge.Domain.Assets;
using Forge.Domain.Blueprints;
using Forge.Domain.Content;
using Forge.Domain.Modules;
using Forge.Domain.Navigation;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Assets;
using Forge.ModuleFormat.Content;
using Forge.ModuleFormat.Distribution;
using Forge.ModuleFormat.Documentation;
using Forge.ModuleFormat.Diagnostics;
using Forge.ModuleFormat.Navigation;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Quality;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Content;
using Forge.Scripting;

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

        if (arguments.Count >= 1 && arguments[0] == "content")
        {
            return RunContent(arguments.Skip(1).ToArray());
        }

        if (arguments.Count == 2 && arguments[0] == "docs" && arguments[1] is "generate" or "check")
        {
            return RunDocumentation(arguments[1] == "check");
        }

        if (arguments.Count >= 2 && arguments[0] == "readiness")
        {
            return RunReadiness(arguments.Skip(1).ToArray());
        }

        if (arguments.Count == 3 && arguments[0] == "quality" && arguments[1] == "run")
        {
            return RunQualityLab(arguments[2]);
        }

        if (arguments.Count == 3 && arguments[0] == "performance" && arguments[1] == "run")
        {
            return RunPerformanceLab(arguments[2]);
        }

        if (arguments.Count == 3 && arguments[0] == "workspace" && arguments[1] == "migrate")
        {
            return MigrateWorkspace(arguments[2]);
        }

        if (arguments.Count == 2 && arguments[0] == "author-original")
        {
            return AuthorOriginalModule(arguments[1]);
        }

        Console.Error.WriteLine(
            "Usage: forge package <export|validate|load|analyze|compliance|keygen|sign|verify-signature|delta|apply-delta|bundle|spatial|diagnose> ... | " +
            "forge assets <list|prepare|roundtrip> ... | " +
            "forge content <batch|export-blueprints|merge-table> ... | " +
            "forge docs <generate|check> | " +
            "forge readiness <validate|health|crash|recovery|sync-plan> ... | " +
            "forge quality run <workspace> | " +
            "forge performance run <workspace> | " +
            "forge workspace migrate <target-directory> | forge author-original <target-directory>");
        return 2;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

static int RunPerformanceLab(string workspaceDirectory)
{
    var opened = ProjectSession.Open(workspaceDirectory);
    if (!opened.Succeeded || opened.Session is null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { valid = false, diagnostics = opened.Diagnostics }));
        return 1;
    }
    var report = new WorkspacePerformanceLabService()
        .Run(opened.Session.RootDirectory, opened.Session.Project);
    Console.WriteLine(JsonSerializer.Serialize(new { valid = report.Passed, report }));
    if (report.Passed)
        Console.WriteLine("OPENRPG_FORGE_PERFORMANCE_LAB_PASS " +
            $"network_scenarios={report.Network.Scenarios.Count} visual_cases={report.VisualReviews.Cases.Count} " +
            $"benchmarks={report.Benchmarks.Measurements.Count} budgets={report.Benchmarks.Budgets.Count} " +
            $"spans={report.TraceSpans.Count} artifacts=written fingerprint={report.Fingerprint}");
    return report.Passed ? 0 : 1;
}

static int RunQualityLab(string workspaceDirectory)
{
    var opened = ProjectSession.Open(workspaceDirectory);
    if (!opened.Succeeded || opened.Session is null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { valid = false, diagnostics = opened.Diagnostics }));
        return 1;
    }
    var report = new WorkspaceQualityLabService(new NativeForgeScriptRuntime())
        .Run(opened.Session.RootDirectory, opened.Session.Project);
    Console.WriteLine(JsonSerializer.Serialize(new { valid = report.Passed, report }));
    if (report.Passed)
        Console.WriteLine("OPENRPG_FORGE_QUALITY_LAB_PASS " +
            $"locales={report.Localization.Coverage.Count} scripts={report.Scripts.Tests.Count} " +
            $"dialogues={report.Dialogues.Count} quests={report.Quests.Count} " +
            $"encounter_runs={report.Encounters.TotalRuns} pseudo=snapshot fingerprint={report.Fingerprint}");
    return report.Passed ? 0 : 1;
}

static int RunReadiness(IReadOnlyList<string> arguments)
{
    if (arguments.Count < 2)
    {
        Console.Error.WriteLine("Usage: forge readiness <validate|health|crash|recovery|sync-plan> <workspace> ...");
        return 2;
    }
    var opened = ProjectSession.Open(arguments[1]);
    if (!opened.Succeeded || opened.Session is null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { valid = false, diagnostics = opened.Diagnostics }));
        return 1;
    }
    var project = opened.Session.Project;
    var readiness = project.Module.RuntimePolicies.Extensions.OperationalReadiness;
    if (arguments[0] == "validate")
    {
        var diagnostics = OperationalReadinessValidator.Validate(readiness);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            valid = diagnostics.Count == 0,
            diagnostics,
            tasks = readiness.TaskManifests.Count,
            manifests = readiness.TaskManifests
        }));
        if (diagnostics.Count == 0)
            Console.WriteLine($"OPENRPG_FORGE_READINESS_VALIDATION_PASS tasks={readiness.TaskManifests.Count} schema=1");
        return diagnostics.Count == 0 ? 0 : 1;
    }
    if (arguments[0] == "health")
    {
        var options = ParseOptions(arguments.Skip(2).ToArray());
        var evidence = ParseBoolean(options.GetValueOrDefault("tests-passed"), false)
            ? new[] { new ProjectHealthSignal("tests", ProjectHealthCategory.Tests,
                ProjectHealthSeverity.Information, "health.tests.cli_pass", "CLI test evidence passed.",
                "health/tests/cli", true) }
            : [];
        var report = ProjectHealthReportService.Evaluate(project, DateTimeOffset.UtcNow, evidence);
        Console.WriteLine(JsonSerializer.Serialize(new { valid = report.BuildAllowed, report }));
        Console.WriteLine($"OPENRPG_FORGE_PROJECT_HEALTH_PASS status={report.Status.ToString().ToLowerInvariant()} " +
            $"domains={report.Domains.Count} build={report.BuildAllowed.ToString().ToLowerInvariant()} fingerprint={report.Fingerprint}");
        return report.BuildAllowed ? 0 : 1;
    }
    if (arguments[0] == "crash" && arguments.Count >= 4)
    {
        var options = ParseOptions(arguments.Skip(4).ToArray());
        var version = options.GetValueOrDefault("version") ?? throw new ArgumentException("Crash capture requires --version.");
        var packageHash = options.GetValueOrDefault("package-sha") ?? throw new ArgumentException("Crash capture requires --package-sha.");
        var reproduction = Guid.Parse(options.GetValueOrDefault("reproduction-id") ?? Guid.NewGuid().ToString("D"));
        var logPath = Path.GetFullPath(arguments[2]);
        if (!File.Exists(logPath)) throw new FileNotFoundException("Crash log was not found.", logPath);
        var result = new CrashArtifactBuilder(readiness.CrashArtifacts).Capture(new CrashArtifactRequest(
            reproduction, DateTimeOffset.UtcNow, version, packageHash, File.ReadLines(logPath).ToArray(),
            Path.GetFullPath(arguments[3])));
        Console.WriteLine(JsonSerializer.Serialize(new { valid = true, result }));
        Console.WriteLine($"OPENRPG_FORGE_CRASH_ARTIFACT_PASS reproduction={result.ReproductionId:D} " +
            $"bytes={result.Bytes} logs={result.SanitizedLogLines} sha256={result.Sha256}");
        return 0;
    }
    if (arguments[0] == "recovery")
    {
        var options = ParseOptions(arguments.Skip(2).ToArray());
        var quarantine = ParseBoolean(options.GetValueOrDefault("quarantine"), false);
        if (quarantine && !readiness.Recovery.QuarantineCorruptFiles)
            throw new InvalidOperationException("Recovery policy forbids quarantine.");
        var inspection = new RecoverySnapshotStore(readiness.Recovery.RetainedSnapshots)
            .Inspect(arguments[1], quarantine);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            valid = inspection.All(item =>
            item.Status is RecoverySnapshotStatus.Valid or RecoverySnapshotStatus.Quarantined),
            inspection
        }));
        Console.WriteLine($"OPENRPG_FORGE_RECOVERY_INSPECTION_PASS snapshots={inspection.Count} quarantine={quarantine.ToString().ToLowerInvariant()}");
        return 0;
    }
    if (arguments[0] == "sync-plan" && arguments.Count == 4)
    {
        var manifest = JsonSerializer.Deserialize<ContentRepositoryManifest>(File.ReadAllText(arguments[2]),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new JsonException("Content repository manifest is empty.");
        var report = new ContentRepositorySyncService(readiness.ContentSynchronization,
            Path.GetFullPath(arguments[3])).Plan(manifest);
        Console.WriteLine(JsonSerializer.Serialize(report));
        if (report.Valid)
            Console.WriteLine($"OPENRPG_FORGE_CONTENT_SYNC_PLAN_PASS resources={report.Items.Count} reused={report.ReusedBytes} fingerprint={report.Fingerprint}");
        return report.Valid ? 0 : 1;
    }
    Console.Error.WriteLine("Usage: forge readiness validate <workspace> | readiness health <workspace> [--tests-passed true] | " +
        "readiness crash <workspace> <log> <output> --version <v> --package-sha <sha256> [--reproduction-id <uuid>] | " +
        "readiness recovery <workspace> [--quarantine true] | readiness sync-plan <workspace> <manifest.json> <user-storage>");
    return 2;
}

static int RunDocumentation(bool checkOnly)
{
    var repositoryRoot = FindRepositoryRoot(Environment.CurrentDirectory);
    var result = ForgeDocumentationGenerator.Generate(repositoryRoot, "docs/generated", checkOnly);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        valid = !checkOnly || result.Current,
        mode = checkOnly ? "check" : "generate",
        files = result.Files.Select(file => file.RelativePath),
        changed = result.ChangedFiles,
    }));
    if (checkOnly && !result.Current) return 1;
    Console.WriteLine($"OPENRPG_FORGE_DOCUMENTATION_PASS mode={(checkOnly ? "check" : "generate")} files={result.Files.Count} source=metadata-deterministic");
    return 0;
}

static string FindRepositoryRoot(string start)
{
    var directory = new DirectoryInfo(Path.GetFullPath(start));
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "OpenRPGForge.sln"))) return directory.FullName;
        directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("OpenRPG Forge repository root was not found.");
}

static int MigrateWorkspace(string targetDirectory)
{
    var opened = ProjectSession.Open(targetDirectory);
    if (!opened.Succeeded || opened.Session is null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { valid = false, diagnostics = opened.Diagnostics }));
        return 1;
    }

    var saved = opened.Session.Save(opened.Session.Project);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        valid = saved.Succeeded,
        format = ProjectSession.CurrentProjectFormatVersion,
        areas = opened.Session.Project.Areas.Count,
        diagnostics = saved.Diagnostics,
    }));
    if (saved.Succeeded)
    {
        Console.WriteLine(
            $"OPENRPG_FORGE_WORKSPACE_MIGRATED format={ProjectSession.CurrentProjectFormatVersion} " +
            $"areas={opened.Session.Project.Areas.Count}");
    }

    return saved.Succeeded ? 0 : 1;
}

static int RunContent(IReadOnlyList<string> arguments)
{
    if (arguments.Count >= 4 && arguments[0] == "batch" &&
        arguments[1] is "import" or "export")
    {
        var options = ParseOptions(arguments.Skip(4).ToArray());
        var operation = arguments[1] == "import" ? ContentBatchOperation.Import : ContentBatchOperation.Export;
        var request = new ContentBatchTransferRequest(
            operation,
            arguments[2],
            arguments[3],
            options.GetValueOrDefault("glob") ?? "*",
            ParseCsv(options.GetValueOrDefault("extensions") ?? ".json,.forge,.glb,.png,.ogg"),
            ParseCollisionPolicy(options.GetValueOrDefault("collision") ?? "fail"),
            options.GetValueOrDefault("provenance") ?? "cli-author",
            options.GetValueOrDefault("license") ?? "UNLICENSED",
            ParseBoolean(options.GetValueOrDefault("dry-run"), defaultValue: true));
        var report = ContentBatchTransferService.Execute(request);
        Console.WriteLine(JsonSerializer.Serialize(report));
        if (report.Valid)
        {
            Console.WriteLine(
                $"OPENRPG_FORGE_CONTENT_BATCH_PASS operation={operation.ToString().ToLowerInvariant()} " +
                $"dryrun={report.DryRun.ToString().ToLowerInvariant()} files={report.Entries.Count} " +
                $"added={report.Added} replaced={report.Replaced} skipped={report.Skipped} preserved={report.PreservedBoth}");
        }

        return report.Valid ? 0 : 1;
    }

    if (arguments.Count >= 3 && arguments[0] == "export-blueprints")
    {
        var options = ParseOptions(arguments.Skip(3).ToArray());
        if (!options.TryGetValue("ids", out var idsValue))
        {
            throw new ArgumentException("Blueprint export requires --ids <uuid,uuid>.");
        }

        var opened = ProjectSession.Open(arguments[1]);
        if (!opened.Succeeded || opened.Session is null)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { valid = false, diagnostics = opened.Diagnostics }));
            return 1;
        }

        var ids = ParseCsv(idsValue).Select(BlueprintId.Parse).ToArray();
        var result = BlueprintExportPackageBuilder.Build(
            opened.Session.Project,
            ids,
            arguments[2],
            ParseBoolean(options.GetValueOrDefault("previews"), defaultValue: true));
        Console.WriteLine(JsonSerializer.Serialize(new { valid = true, result }));
        Console.WriteLine(
            $"OPENRPG_FORGE_BLUEPRINT_EXPORT_PASS blueprints={result.BlueprintCount} " +
            $"dependencies={result.DependencyCount} previews={result.PreviewCount} sha256={result.ContentSha256}");
        return 0;
    }

    if (arguments.Count >= 5 && arguments[0] == "merge-table")
    {
        var options = ParseOptions(arguments.Skip(5).ToArray());
        var baseProject = OpenProject(arguments[1]);
        var ourProject = OpenProject(arguments[2]);
        var theirProject = OpenProject(arguments[3]);
        var tableId = DataTableId.Parse(arguments[4]);
        var policy = ParseMergePolicy(options.GetValueOrDefault("policy") ?? "manual");
        var result = DataTableThreeWayMerger.Merge(
            baseProject.Module.Content.DataTables.Single(table => table.Id == tableId),
            ourProject.Module.Content.DataTables.Single(table => table.Id == tableId),
            theirProject.Module.Content.DataTables.Single(table => table.Id == tableId),
            policy);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            valid = true,
            tableId = tableId.ToString(),
            policy = policy.ToString(),
            rows = result.Merged.Rows.Count,
            conflicts = result.Conflicts.Select(conflict => new
            {
                id = conflict.ConflictId,
                rowId = conflict.RowId.ToString(),
                conflict.Summary,
                ours = conflict.Ours?.Values,
                theirs = conflict.Theirs?.Values,
            }),
        }));
        Console.WriteLine(
            $"OPENRPG_FORGE_CONTENT_MERGE_PASS rows={result.Merged.Rows.Count} " +
            $"conflicts={result.Conflicts.Count} policy={policy.ToString().ToLowerInvariant()}");
        return 0;
    }

    Console.Error.WriteLine(
        "Usage: forge content batch <import|export> <source> <destination> " +
        "[--glob <pattern>] [--extensions <.json,.forge>] [--collision <fail|skip|replace|preserve-both>] " +
        "[--provenance <source>] [--license <spdx>] [--dry-run <true|false>] | " +
        "forge content export-blueprints <workspace> <output.orpblueprints> --ids <uuid,uuid> [--previews <true|false>] | " +
        "forge content merge-table <base-workspace> <ours-workspace> <theirs-workspace> <table-uuid> " +
        "[--policy <manual|ours|theirs|preserve-both>]");
    return 2;
}

static Forge.Domain.Projects.ProjectDefinition OpenProject(string workspace)
{
    var opened = ProjectSession.Open(workspace);
    if (!opened.Succeeded || opened.Session is null)
    {
        throw new InvalidDataException(
            $"Content workspace '{workspace}' cannot be opened: {string.Join(", ", opened.Diagnostics.Select(item => item.Message))}");
    }

    return opened.Session.Project;
}

static ContentBatchCollisionPolicy ParseCollisionPolicy(string value) => value.Trim().ToLowerInvariant() switch
{
    "fail" => ContentBatchCollisionPolicy.Fail,
    "skip" => ContentBatchCollisionPolicy.Skip,
    "replace" => ContentBatchCollisionPolicy.Replace,
    "preserve-both" or "preserveboth" => ContentBatchCollisionPolicy.PreserveBoth,
    _ => throw new ArgumentException($"Unknown content collision policy '{value}'."),
};

static ContentMergePolicy ParseMergePolicy(string value) => value.Trim().ToLowerInvariant() switch
{
    "manual" => ContentMergePolicy.Manual,
    "ours" or "prefer-ours" => ContentMergePolicy.PreferOurs,
    "theirs" or "prefer-theirs" => ContentMergePolicy.PreferTheirs,
    "preserve-both" or "preserveboth" => ContentMergePolicy.PreserveBoth,
    _ => throw new ArgumentException($"Unknown content merge policy '{value}'."),
};

static IReadOnlyList<string> ParseCsv(string value) =>
    value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

static bool ParseBoolean(string? value, bool defaultValue) => string.IsNullOrWhiteSpace(value)
    ? defaultValue
    : bool.TryParse(value, out var parsed)
        ? parsed
        : throw new ArgumentException($"'{value}' is not a valid boolean value.");

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

    if (arguments.Count == 4 && arguments[0] == "sign")
    {
        var signature = ModulePackageSignatureService.Sign(arguments[1], arguments[2], arguments[3]);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            valid = true,
            signature.FormatVersion,
            signature.Algorithm,
            signature.PackageSha256,
            signature.KeyFingerprint,
            output = Path.GetFullPath(arguments[3]),
        }));
        Console.WriteLine(
            $"OPENRPG_FORGE_PACKAGE_SIGNATURE_PASS key={signature.KeyFingerprint} hash={signature.PackageSha256}");
        return 0;
    }

    if (arguments.Count == 3 && arguments[0] == "keygen")
    {
        var fingerprint = ModulePackageSignatureService.GenerateKeyPair(arguments[1], arguments[2]);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            valid = true,
            fingerprint,
            privateKey = Path.GetFullPath(arguments[1]),
            publicKey = Path.GetFullPath(arguments[2]),
        }));
        Console.WriteLine($"OPENRPG_FORGE_SIGNING_KEYGEN_PASS key={fingerprint}");
        return 0;
    }

    if (arguments.Count == 4 && arguments[0] == "verify-signature")
    {
        var opened = ProjectSession.Open(arguments[1]);
        if (!opened.Succeeded || opened.Session is null)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { valid = false, diagnostics = opened.Diagnostics }));
            return 1;
        }

        var result = ModulePackageSignatureService.Verify(
            arguments[2],
            arguments[3],
            opened.Session.Project.Module.Distribution.Release.Signing);
        Console.WriteLine(JsonSerializer.Serialize(result));
        if (result.IsValid)
        {
            Console.WriteLine(
                $"OPENRPG_FORGE_PACKAGE_SIGNATURE_VERIFY_PASS trusted={result.IsTrusted.ToString().ToLowerInvariant()} " +
                $"key={result.KeyFingerprint}");
        }

        return result.IsValid ? 0 : 1;
    }

    if (arguments.Count == 5 && arguments[0] == "delta")
    {
        var opened = ProjectSession.Open(arguments[1]);
        if (!opened.Succeeded || opened.Session is null)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { valid = false, diagnostics = opened.Diagnostics }));
            return 1;
        }

        var result = ModuleDeltaPackageBuilder.Create(
            arguments[2],
            arguments[3],
            arguments[4],
            opened.Session.Project.Module.Distribution.Release.Delta);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            valid = true,
            result.Manifest.BasePackageSha256,
            result.Manifest.TargetPackageSha256,
            result.IncludedChunks,
            result.ReusedChunks,
            result.IncludedBytes,
            output = result.DeltaPath,
        }));
        Console.WriteLine(
            $"OPENRPG_FORGE_PACKAGE_DELTA_PASS included={result.IncludedChunks} reused={result.ReusedChunks} " +
            $"bytes={result.IncludedBytes}");
        return 0;
    }

    if (arguments.Count is 4 or 6 && arguments[0] == "apply-delta")
    {
        var resumeCache = arguments.Count == 6 && arguments[4] == "--resume-cache" ? arguments[5] : null;
        var manifest = ModuleDeltaPackageBuilder.Apply(arguments[1], arguments[2], arguments[3], resumeCache);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            valid = true,
            manifest.BasePackageSha256,
            manifest.TargetPackageSha256,
            manifest.TargetBytes,
            output = Path.GetFullPath(arguments[3]),
        }));
        Console.WriteLine(
            $"OPENRPG_FORGE_PACKAGE_DELTA_APPLY_PASS hash={manifest.TargetPackageSha256} bytes={manifest.TargetBytes}");
        return 0;
    }

    if (arguments.Count >= 8 && arguments[0] == "bundle")
    {
        var opened = ProjectSession.Open(arguments[1]);
        if (!opened.Succeeded || opened.Session is null)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { valid = false, diagnostics = opened.Diagnostics }));
            return 1;
        }

        var options = ParseOptions(arguments.Skip(4).ToArray());
        var target = ParseTarget(options.GetValueOrDefault("target") ?? "editor");
        var platform = ParsePlatform(options.GetValueOrDefault("platform") ?? "windows-x64");
        var result = ModuleReleaseBundleBuilder.Build(
            arguments[2],
            arguments[3],
            target,
            platform,
            opened.Session.Project.Module.Distribution.Release,
            options.GetValueOrDefault("signature"),
            options.GetValueOrDefault("delta"));
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            valid = true,
            result.Manifest.Channel,
            result.Manifest.Target,
            result.Manifest.Platform,
            result.Manifest.PackageSha256,
            result.FileCount,
            result.BundleBytes,
            output = result.BundlePath,
        }));
        Console.WriteLine(
            $"OPENRPG_FORGE_RELEASE_BUNDLE_PASS channel={result.Manifest.Channel.ToString().ToLowerInvariant()} " +
            $"target={target.ToString().ToLowerInvariant()} platform={platform.ToString().ToLowerInvariant()} " +
            $"files={result.FileCount}");
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
            release = new
            {
                id = session.Project.Module.Distribution.Release.Id.ToString(),
                channel = session.Project.Module.Distribution.Release.Channel.ToString(),
                platforms = session.Project.Module.Distribution.Release.Platforms.Select(platform => platform.ToString()),
                signing = session.Project.Module.Distribution.Release.Signing.Mode.ToString(),
                delta = session.Project.Module.Distribution.Release.Delta.Enabled,
                windows = session.Project.Module.Distribution.Release.WindowsPackaging.Mode.ToString(),
            },
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
        "sign <module.orpmod> <private-key.pem> <signature.json> | " +
        "keygen <private-key.pem> <public-key.pem> | " +
        "verify-signature <workspace> <module.orpmod> <signature.json> | " +
        "delta <workspace> <base.orpmod> <target.orpmod> <output.orpdelta> | " +
        "apply-delta <base.orpmod> <delta.orpdelta> <output.orpmod> [--resume-cache <directory>] | " +
        "bundle <workspace> <module.orpmod> <output.zip> --target <profile> --platform <platform> " +
        "[--signature <signature.json>] [--delta <delta.orpdelta>] | " +
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

static ModuleDistributionPlatform ParsePlatform(string value)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(value);
    var normalized = value.Replace("-", string.Empty, StringComparison.Ordinal)
        .Replace("_", string.Empty, StringComparison.Ordinal);
    return Enum.TryParse<ModuleDistributionPlatform>(normalized, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
        ? parsed
        : throw new ArgumentException(
            $"Unknown platform '{value}'. Expected WindowsX64, LinuxX64 or MacOSArm64.",
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
