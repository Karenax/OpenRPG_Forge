using System.Text.Json;
using System.Text.Json.Nodes;
using Forge.Domain.Areas;
using Forge.Domain.Modules;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Tests.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Workspaces;

public sealed class ProjectSessionTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(),
        "openrpg-forge-tests",
        Guid.NewGuid().ToString("N"));

    public ProjectSessionTests()
    {
        Directory.CreateDirectory(_temporaryRoot);
    }

    [Fact]
    public void Open_LoadsAValidForgeProjectDirectory()
    {
        WriteWorkspace(TestProjectFactory.CreateValid());

        var result = ProjectSession.Open(_temporaryRoot);

        Assert.True(result.Succeeded, string.Join(" | ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        var session = Assert.IsType<ProjectSession>(result.Session);
        Assert.Equal("OpenRPG Forge test project", session.Project.Title);
        Assert.Equal(
            Path.Combine(_temporaryRoot, "content", "project.json"),
            session.ProjectFilePath);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Open_ReturnsADiagnosticWhenTheManifestIsMissing()
    {
        var result = ProjectSession.Open(_temporaryRoot);

        Assert.False(result.Succeeded);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ProjectSession.ManifestMissingCode, diagnostic.Code);
        Assert.Null(result.Session);
    }

    [Fact]
    public void Open_RejectsAProjectFileOutsideTheWorkspace()
    {
        WriteManifest("../outside.json");

        var result = ProjectSession.Open(_temporaryRoot);

        Assert.False(result.Succeeded);
        Assert.Equal(ProjectSession.ProjectPathUnsafeCode, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Open_RejectsAnUnsupportedWorkspaceVersion()
    {
        WriteManifest("content/project.json", formatVersion: "99.0.0");

        var result = ProjectSession.Open(_temporaryRoot);

        Assert.False(result.Succeeded);
        Assert.Equal(
            ProjectSession.ManifestVersionUnsupportedCode,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Save_ReplacesTheProjectFileAndCanBeReopened()
    {
        WriteWorkspace(TestProjectFactory.CreateValid());
        var session = Assert.IsType<ProjectSession>(ProjectSession.Open(_temporaryRoot).Session);
        var updated = WithTitle(session.Project, "Crypte de test sauvegardée");

        var result = session.Save(updated);
        var reopened = ProjectSession.Open(_temporaryRoot);

        Assert.True(result.Succeeded);
        Assert.Equal("Crypte de test sauvegardée", session.Project.Title);
        Assert.Equal("Crypte de test sauvegardée", reopened.Session?.Project.Title);
        Assert.Empty(Directory.EnumerateFiles(_temporaryRoot, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void Save_RejectsAStaleConcurrentSessionWithoutChangingTheWorkspace()
    {
        WriteWorkspace(TestProjectFactory.CreateValid());
        var first = Assert.IsType<ProjectSession>(ProjectSession.Open(_temporaryRoot).Session);
        var stale = Assert.IsType<ProjectSession>(ProjectSession.Open(_temporaryRoot).Session);

        var firstResult = first.Save(WithTitle(first.Project, "Première sauvegarde"));
        var afterFirstSave = File.ReadAllText(first.ProjectFilePath);
        var staleResult = stale.Save(WithTitle(stale.Project, "Écrasement obsolète"));

        Assert.True(firstResult.Succeeded);
        Assert.False(staleResult.Succeeded);
        Assert.Equal(ProjectSession.RevisionConflictCode, Assert.Single(staleResult.Diagnostics).Code);
        Assert.Equal(afterFirstSave, File.ReadAllText(first.ProjectFilePath));
        Assert.Equal("Première sauvegarde", Assert.IsType<ProjectSession>(
            ProjectSession.Open(_temporaryRoot).Session).Project.Title);
    }

    [Fact]
    public void Save_PersistsTheWorkspaceRevisionAcrossReopen()
    {
        WriteWorkspace(TestProjectFactory.CreateValid());
        var first = Assert.IsType<ProjectSession>(ProjectSession.Open(_temporaryRoot).Session);

        Assert.Equal(0, first.Revision);
        Assert.True(first.Save(WithTitle(first.Project, "Révision persistée")).Succeeded);
        Assert.Equal(1, first.Revision);

        var reopened = Assert.IsType<ProjectSession>(ProjectSession.Open(_temporaryRoot).Session);
        Assert.Equal(1, reopened.Revision);
        Assert.True(reopened.Save(WithTitle(reopened.Project, "Révision suivante")).Succeeded);
        Assert.Equal(2, reopened.Revision);
        Assert.True(File.Exists(Path.Combine(
            _temporaryRoot,
            ProjectSession.RevisionStateRelativePath.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void Save_RejectsAManualProjectEditMadeAfterOpen()
    {
        WriteWorkspace(TestProjectFactory.CreateValid());
        var session = Assert.IsType<ProjectSession>(ProjectSession.Open(_temporaryRoot).Session);
        var manuallyEdited = WithTitle(session.Project, "Modification externe");
        File.WriteAllText(session.ProjectFilePath, ProjectJsonSerializer.Serialize(manuallyEdited));
        var externalBytes = File.ReadAllText(session.ProjectFilePath);

        var result = session.Save(WithTitle(session.Project, "Écrasement Studio"));

        Assert.False(result.Succeeded);
        Assert.Equal(ProjectSession.RevisionConflictCode, Assert.Single(result.Diagnostics).Code);
        Assert.Equal(externalBytes, File.ReadAllText(session.ProjectFilePath));
    }

    [Fact]
    public void Open_RollsBackAnInterruptedMultiDocumentSave()
    {
        WriteWorkspace(TestProjectFactory.CreateValid());
        var session = Assert.IsType<ProjectSession>(ProjectSession.Open(_temporaryRoot).Session);
        var originalProjectJson = File.ReadAllText(session.ProjectFilePath);
        ProjectWorkspaceTransaction.TestWriteProbe = appliedDocuments =>
        {
            if (appliedDocuments == 2)
            {
                throw new ProjectWorkspaceSimulatedCrashException("Simulated process interruption.");
            }
        };

        try
        {
            Assert.Throws<ProjectWorkspaceSimulatedCrashException>(() =>
                session.Save(WithTitle(session.Project, "Sauvegarde interrompue")));
        }
        finally
        {
            ProjectWorkspaceTransaction.TestWriteProbe = null;
        }

        var reopened = ProjectSession.Open(_temporaryRoot);

        Assert.True(reopened.Succeeded, string.Join(" | ", reopened.Diagnostics.Select(item => item.Message)));
        Assert.Equal("OpenRPG Forge test project", reopened.Session!.Project.Title);
        Assert.Equal(originalProjectJson, File.ReadAllText(session.ProjectFilePath));
        Assert.False(File.Exists(Path.Combine(
            _temporaryRoot,
            ProjectSession.RevisionStateRelativePath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.False(ProjectWorkspaceTransaction.HasPending(_temporaryRoot));
    }

    [Fact]
    public void Save_LeavesTheExistingFileUntouchedWhenValidationFails()
    {
        WriteWorkspace(TestProjectFactory.CreateValid());
        var session = Assert.IsType<ProjectSession>(ProjectSession.Open(_temporaryRoot).Session);
        var original = File.ReadAllText(session.ProjectFilePath);
        var invalid = new ProjectDefinition(
            session.Project.Id,
            session.Project.Title,
            [],
            session.Project.Areas);

        var result = session.Save(invalid);

        Assert.False(result.Succeeded);
        Assert.Equal(ProjectSession.ProjectInvalidCode, Assert.Single(result.Diagnostics).Code);
        Assert.Equal(original, File.ReadAllText(session.ProjectFilePath));
        Assert.Empty(Directory.EnumerateFiles(_temporaryRoot, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void OpenForMigration_AllowsOnlyExplicitDiagnosticsAndSavesAValidatedRepair()
    {
        var targetDirectory = Path.Combine(_temporaryRoot, "migration-repair");
        var created = ProjectSession.Create(targetDirectory, TestProjectFactory.CreateValid());
        var createdSession = Assert.IsType<ProjectSession>(created.Session);
        var areaPath = Assert.Single(Directory.EnumerateFiles(
            Path.Combine(targetDirectory, "areas"), "*.json", SearchOption.TopDirectoryOnly));
        var areaDocument = JsonNode.Parse(File.ReadAllText(areaPath))!.AsObject();
        areaDocument["area"]!["navigation"]!["anchors"]!["requiredAtRuntime"] = true;
        File.WriteAllText(areaPath, areaDocument.ToJsonString());

        var strictOpen = ProjectSession.Open(targetDirectory);
        var migrationOpen = ProjectSession.OpenForMigration(targetDirectory,
            new HashSet<string>([AnchorCatalogValidator.RequiredContentCode], StringComparer.Ordinal));
        var migrationSession = Assert.IsType<ProjectSession>(migrationOpen.Session);
        var area = Assert.Single(migrationSession.Project.Areas);
        var anchors = area.Navigation.Anchors;
        var repairedAnchors = new AnchorCatalog(anchors.Id, anchors.SchemaVersion, anchors.Provenance,
            area.Id, false, anchors.Definitions, anchors.Instances);
        var repairedArea = area.WithNavigation(area.Navigation.WithAnchors(repairedAnchors));
        var repairedProject = new ProjectDefinition(
            migrationSession.Project.Id,
            migrationSession.Project.Title,
            migrationSession.Project.Module,
            migrationSession.Project.Blueprints,
            [repairedArea],
            migrationSession.Project.Dialogues,
            migrationSession.Project.Quests);

        var saved = migrationSession.Save(repairedProject);
        var reopened = ProjectSession.Open(targetDirectory);

        Assert.False(strictOpen.Succeeded);
        Assert.True(migrationOpen.Succeeded);
        Assert.True(saved.Succeeded, string.Join(" | ", saved.Diagnostics.Select(item => item.Message)));
        Assert.True(reopened.Succeeded);
        Assert.False(Assert.Single(reopened.Session!.Project.Areas).Navigation.Anchors.RequiredAtRuntime);
        Assert.True(createdSession.Revision <= migrationSession.Revision);
    }

    [Fact]
    public void Create_WritesANewWorkspaceThatCanBeOpened()
    {
        var targetDirectory = Path.Combine(_temporaryRoot, "new-module");

        var result = ProjectSession.Create(targetDirectory, TestProjectFactory.CreateValid());

        Assert.True(result.Succeeded);
        var session = Assert.IsType<ProjectSession>(result.Session);
        Assert.Equal(targetDirectory, session.RootDirectory);
        Assert.True(File.Exists(Path.Combine(targetDirectory, ProjectSession.ManifestFileName)));
        Assert.True(File.Exists(Path.Combine(targetDirectory, ProjectSession.DefaultProjectFileName)));
        Assert.Equal(ProjectSession.CurrentProjectFormatVersion, ReadProjectFormatVersion(session.ProjectFilePath));
        var areaPath = Assert.Single(Directory.EnumerateFiles(
            Path.Combine(targetDirectory, "areas"),
            "*.json",
            SearchOption.TopDirectoryOnly));
        Assert.Equal(AreaJsonSerializer.CurrentFormatVersion, ReadProjectFormatVersion(areaPath));
        Assert.Empty(Directory.EnumerateFiles(targetDirectory, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void Create_RoundTripsAValidatedCoreProjectWithoutAnAreaDocument()
    {
        var targetDirectory = Path.Combine(_temporaryRoot, "core-menu");
        var projectId = ProjectId.From(Guid.Parse("d0250000-0000-4000-8000-000000000001"));
        var moduleId = ModuleId.From(Guid.Parse("d0250000-0000-4000-8000-000000000002"));
        var foundation = ProjectFoundationProfile.CreateCoreDefault(
            Guid.Parse("d0250000-0000-4000-8000-000000000003"),
            projectId,
            "menu-principal",
            ProjectTemplateKind.CoreMenu);
        var metadata = new ProjectMetadata(
            ProjectMetadataId.From(moduleId.Value),
            "fr",
            [new ProjectLocalizedMetadata("fr", "Menu principal", "Interface", "Projet sans monde 3D.", string.Empty)],
            "Atelier Forge",
            "1.0.0",
            "CC0-1.0",
            null,
            ["interface"],
            "Tout public",
            "studio:ux0-e-test",
            foundation: foundation);
        var module = new ModuleDefinition(
            moduleId,
            "menu-principal",
            "Menu principal",
            "Projet sans monde 3D.",
            AreaId.From(Guid.Parse("d0250000-0000-4000-8000-000000000004")),
            metadata);
        var project = new ProjectDefinition(projectId, "Menu principal", module, [], []);

        var created = ProjectSession.Create(targetDirectory, project);
        var reopened = ProjectSession.Open(targetDirectory);

        Assert.True(created.Succeeded, string.Join(" | ", created.Diagnostics.Select(item => item.Message)));
        Assert.True(reopened.Succeeded, string.Join(" | ", reopened.Diagnostics.Select(item => item.Message)));
        var restored = Assert.IsType<ProjectSession>(reopened.Session).Project;
        Assert.Empty(restored.Areas);
        Assert.Empty(restored.Blueprints);
        Assert.Equal(ProjectTemplateKind.CoreMenu, restored.Module.Metadata.Foundation?.Template);
        Assert.Equal(projectId, restored.Module.Metadata.Foundation?.Identity.ProjectId);
        Assert.False(Directory.Exists(Path.Combine(targetDirectory, "areas")) &&
                     Directory.EnumerateFiles(Path.Combine(targetDirectory, "areas"), "*.json").Any());
    }

    [Fact]
    public void Save_MigratesLegacyAggregateToIndependentAreaDocuments()
    {
        WriteWorkspace(TestProjectFactory.CreateValid());
        var session = Assert.IsType<ProjectSession>(ProjectSession.Open(_temporaryRoot).Session);

        var result = session.Save(session.Project);
        var projectJson = File.ReadAllText(session.ProjectFilePath);
        using var projectDocument = JsonDocument.Parse(projectJson);
        var areaReference = Assert.Single(projectDocument.RootElement
            .GetProperty("project")
            .GetProperty("areas")
            .EnumerateArray());
        var relativeAreaPath = areaReference.GetProperty("documentPath").GetString()!;

        Assert.True(result.Succeeded);
        Assert.Equal(ProjectSession.CurrentProjectFormatVersion, ReadProjectFormatVersion(session.ProjectFilePath));
        Assert.True(File.Exists(Path.Combine(
            Path.GetDirectoryName(session.ProjectFilePath)!,
            relativeAreaPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(ProjectSession.Open(_temporaryRoot).Succeeded);
    }

    [Fact]
    public void Open_MigratesPreRenderingWorkspaceAndAreaDocuments()
    {
        var created = ProjectSession.Create(
            Path.Combine(_temporaryRoot, "pre-rendering"),
            TestProjectFactory.CreateValid());
        var session = Assert.IsType<ProjectSession>(created.Session);
        var projectDocument = JsonNode.Parse(File.ReadAllText(session.ProjectFilePath))!.AsObject();
        projectDocument["formatVersion"] = "1.2.0";
        File.WriteAllText(session.ProjectFilePath, projectDocument.ToJsonString());
        var areaPath = Assert.Single(Directory.EnumerateFiles(
            Path.Combine(session.RootDirectory, "areas"),
            "*.json"));
        var areaDocument = JsonNode.Parse(File.ReadAllText(areaPath))!.AsObject();
        areaDocument["formatVersion"] = AreaJsonSerializer.PreRenderingProfileFormatVersion;
        var environment = areaDocument["area"]!["environment"]!.AsObject();
        environment["schemaVersion"] = 1;
        environment.Remove("rendering");
        environment.Remove("sky");
        File.WriteAllText(areaPath, areaDocument.ToJsonString());

        var reopened = ProjectSession.Open(session.RootDirectory);
        var migrated = Assert.IsType<ProjectSession>(reopened.Session);
        var restoredEnvironment = Assert.Single(migrated.Project.Areas).Environment;

        Assert.True(reopened.Succeeded);
        Assert.Equal(AreaEnvironmentProfile.CurrentSchemaVersion, restoredEnvironment.SchemaVersion);
        Assert.Equal(PbrMaterialWorkflow.MetallicRoughness, restoredEnvironment.Rendering.MaterialWorkflow);
        Assert.Equal(SkyProfileMode.Procedural, restoredEnvironment.Sky.Mode);
    }

    [Fact]
    public void Open_MigratesPreClimateWorkspaceAndAreaDocuments()
    {
        var created = ProjectSession.Create(
            Path.Combine(_temporaryRoot, "pre-climate"),
            TestProjectFactory.CreateValid());
        var session = Assert.IsType<ProjectSession>(created.Session);
        var projectDocument = JsonNode.Parse(File.ReadAllText(session.ProjectFilePath))!.AsObject();
        projectDocument["formatVersion"] = "1.3.0";
        File.WriteAllText(session.ProjectFilePath, projectDocument.ToJsonString());
        var areaPath = Assert.Single(Directory.EnumerateFiles(
            Path.Combine(session.RootDirectory, "areas"),
            "*.json"));
        var areaDocument = JsonNode.Parse(File.ReadAllText(areaPath))!.AsObject();
        areaDocument["formatVersion"] = AreaJsonSerializer.PreClimateProfileFormatVersion;
        var environment = areaDocument["area"]!["environment"]!.AsObject();
        environment["schemaVersion"] = 2;
        environment.Remove("wind");
        environment.Remove("weather");
        environment.Remove("water");
        environment.Remove("reverb");
        File.WriteAllText(areaPath, areaDocument.ToJsonString());

        var reopened = ProjectSession.Open(session.RootDirectory);
        var migrated = Assert.IsType<ProjectSession>(reopened.Session);
        var restored = Assert.Single(migrated.Project.Areas).Environment;

        Assert.True(reopened.Succeeded);
        Assert.Equal(AreaEnvironmentProfile.CurrentSchemaVersion, restored.SchemaVersion);
        Assert.Equal(WindProfile.CurrentSchemaVersion, restored.Wind.SchemaVersion);
        Assert.Equal(WeatherProfile.CurrentSchemaVersion, restored.Weather.SchemaVersion);
        Assert.Empty(restored.Water.Bodies);
        Assert.Empty(restored.Reverb.Zones);
    }

    [Fact]
    public void Open_MigratesPreWorldWorkspaceAndAreaDocumentsDeterministically()
    {
        var created = ProjectSession.Create(
            Path.Combine(_temporaryRoot, "pre-world"),
            TestProjectFactory.CreateValid());
        var session = Assert.IsType<ProjectSession>(created.Session);
        var projectDocument = JsonNode.Parse(File.ReadAllText(session.ProjectFilePath))!.AsObject();
        projectDocument["formatVersion"] = "1.4.0";
        File.WriteAllText(session.ProjectFilePath, projectDocument.ToJsonString());
        var areaPath = Assert.Single(Directory.EnumerateFiles(Path.Combine(session.RootDirectory, "areas"), "*.json"));
        var areaDocument = JsonNode.Parse(File.ReadAllText(areaPath))!.AsObject();
        areaDocument["formatVersion"] = AreaJsonSerializer.PreWorldProfileFormatVersion;
        areaDocument["area"]!.AsObject().Remove("world");
        File.WriteAllText(areaPath, areaDocument.ToJsonString());

        var first = ProjectSession.Open(session.RootDirectory);
        var second = ProjectSession.Open(session.RootDirectory);
        var firstWorld = Assert.Single(Assert.IsType<ProjectSession>(first.Session).Project.Areas).World;
        var secondWorld = Assert.Single(Assert.IsType<ProjectSession>(second.Session).Project.Areas).World;

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(firstWorld.Id, secondWorld.Id);
        Assert.Equal(firstWorld.Terrain.Id, secondWorld.Terrain.Id);
        Assert.Equal(TerrainProviderKind.None, firstWorld.Terrain.Provider);
    }

    [Fact]
    public void Open_MigratesPreRuntimePolicyWorkspaceAndAreaDocumentsDeterministically()
    {
        var created = ProjectSession.Create(
            Path.Combine(_temporaryRoot, "pre-runtime-policy"),
            TestProjectFactory.CreateValid());
        var session = Assert.IsType<ProjectSession>(created.Session);
        var projectDocument = JsonNode.Parse(File.ReadAllText(session.ProjectFilePath))!.AsObject();
        projectDocument["formatVersion"] = "1.5.0";
        projectDocument["project"]!["module"]!.AsObject().Remove("runtimePolicies");
        File.WriteAllText(session.ProjectFilePath, projectDocument.ToJsonString());
        var areaPath = Assert.Single(Directory.EnumerateFiles(Path.Combine(session.RootDirectory, "areas"), "*.json"));
        var areaDocument = JsonNode.Parse(File.ReadAllText(areaPath))!.AsObject();
        areaDocument["formatVersion"] = AreaJsonSerializer.PreRuntimePolicyFormatVersion;
        areaDocument["area"]!.AsObject().Remove("runtimePolicies");
        File.WriteAllText(areaPath, areaDocument.ToJsonString());

        var first = ProjectSession.Open(session.RootDirectory);
        var second = ProjectSession.Open(session.RootDirectory);
        var firstProject = Assert.IsType<ProjectSession>(first.Session).Project;
        var secondProject = Assert.IsType<ProjectSession>(second.Session).Project;

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(firstProject.Module.RuntimePolicies.Id, secondProject.Module.RuntimePolicies.Id);
        Assert.False(firstProject.Module.RuntimePolicies.Rest.Enabled);
        Assert.Equal(firstProject.Areas[0].RuntimePolicies.Id, secondProject.Areas[0].RuntimePolicies.Id);
        Assert.Empty(firstProject.Areas[0].RuntimePolicies.Heartbeats);
    }

    [Fact]
    public void Open_ReturnsLocalizedDiagnosticWhenAreaDocumentIsMissing()
    {
        var created = ProjectSession.Create(
            Path.Combine(_temporaryRoot, "missing-area"),
            TestProjectFactory.CreateValid());
        var session = Assert.IsType<ProjectSession>(created.Session);
        var areaPath = Assert.Single(Directory.EnumerateFiles(
            Path.Combine(session.RootDirectory, "areas"),
            "*.json"));
        File.Delete(areaPath);

        var reopened = ProjectSession.Open(session.RootDirectory);

        Assert.False(reopened.Succeeded);
        Assert.Equal(ProjectSession.AreaMissingCode, Assert.Single(reopened.Diagnostics).Code);
    }

    [Fact]
    public void Create_RejectsANonEmptyTargetWithoutChangingIt()
    {
        Directory.CreateDirectory(_temporaryRoot);
        var sentinelPath = Path.Combine(_temporaryRoot, "keep.txt");
        File.WriteAllText(sentinelPath, "user content");

        var result = ProjectSession.Create(_temporaryRoot, TestProjectFactory.CreateValid());

        Assert.False(result.Succeeded);
        Assert.Equal(ProjectSession.DirectoryNotEmptyCode, Assert.Single(result.Diagnostics).Code);
        Assert.Equal("user content", File.ReadAllText(sentinelPath));
        Assert.Null(result.Session);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot))
        {
            Directory.Delete(_temporaryRoot, recursive: true);
        }
    }

    private void WriteWorkspace(ProjectDefinition project)
    {
        WriteManifest("content/project.json");
        var contentDirectory = Path.Combine(_temporaryRoot, "content");
        Directory.CreateDirectory(contentDirectory);
        File.WriteAllText(
            Path.Combine(contentDirectory, "project.json"),
            ProjectJsonSerializer.Serialize(project));
    }

    private void WriteManifest(string projectFile, string? formatVersion = null)
    {
        var manifest = new ProjectWorkspaceManifest(
            formatVersion ?? ProjectSession.CurrentManifestVersion,
            projectFile);
        File.WriteAllText(
            Path.Combine(_temporaryRoot, ProjectSession.ManifestFileName),
            JsonSerializer.Serialize(
                manifest,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                }));
    }

    private static ProjectDefinition WithTitle(ProjectDefinition project, string title) =>
        new(
            project.Id,
            title,
            project.Module,
            project.Blueprints,
            project.Areas,
            project.Dialogues,
            project.Quests);

    private static string ReadProjectFormatVersion(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("formatVersion").GetString()!;
    }
}
