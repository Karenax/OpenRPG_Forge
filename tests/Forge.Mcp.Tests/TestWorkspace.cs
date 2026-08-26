using Forge.Mcp.Projects;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Mcp.Tests;

internal sealed class TestWorkspace : IDisposable
{
    public string RootDirectory { get; }

    public ForgeProjectAgentService Project { get; }

    public TestWorkspace(Func<ProjectDefinition, ProjectDefinition>? mutate = null)
    {
        RootDirectory = Path.Combine(Path.GetTempPath(), "openrpg-forge-mcp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootDirectory);
        var repositoryRoot = FindRepositoryRoot();
        var sample = Path.Combine(repositoryRoot, "apps", "Forge.Runtime", "content", "crypt-of-ashes");
        File.Copy(Path.Combine(sample, "forge.project.json"), Path.Combine(RootDirectory, "forge.project.json"));
        File.Copy(Path.Combine(sample, "project.json"), Path.Combine(RootDirectory, "project.json"));
        var areaDirectory = Path.Combine(RootDirectory, "areas");
        Directory.CreateDirectory(areaDirectory);
        foreach (var areaDocument in Directory.EnumerateFiles(Path.Combine(sample, "areas"), "*.json"))
        {
            File.Copy(areaDocument, Path.Combine(areaDirectory, Path.GetFileName(areaDocument)));
        }
        if (mutate is not null)
        {
            var opened = ProjectSession.Open(RootDirectory).Session
                ?? throw new InvalidOperationException("The MCP fixture could not be opened for mutation.");
            var saved = opened.Save(mutate(opened.Project));
            if (!saved.Succeeded) throw new InvalidOperationException("The MCP fixture mutation could not be saved.");
        }
        Project = ForgeProjectAgentService.Open(RootDirectory).Service
            ?? throw new InvalidOperationException("The MCP test project did not open.");
    }

    public void Dispose()
    {
        if (Directory.Exists(RootDirectory))
        {
            Directory.Delete(RootDirectory, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        var configured = Environment.GetEnvironmentVariable("OPENRPG_FORGE_REPOSITORY_ROOT");
        if (!string.IsNullOrWhiteSpace(configured) &&
            File.Exists(Path.Combine(configured, "OpenRPGForge.sln")))
        {
            return Path.GetFullPath(configured);
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenRPGForge.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the OpenRPG Forge repository root.");
    }
}
