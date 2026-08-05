using System.Text.Json;
using System.Text.Json.Nodes;
using Forge.Domain.Areas;
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
