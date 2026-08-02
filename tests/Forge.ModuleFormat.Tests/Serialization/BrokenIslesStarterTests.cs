using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class BrokenIslesStarterTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(),
        $"openrpg-forge-broken-isles-{Guid.NewGuid():N}");

    [Fact]
    public void Starter_IsTheCanonicalOutputOfNewModuleAndPackagesCleanly()
    {
        var fixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "broken-isles-starter");
        var open = ProjectSession.Open(fixtureDirectory);

        Assert.Empty(open.Diagnostics);
        var session = Assert.IsType<ProjectSession>(open.Session);
        Assert.Equal("les-iles-brisees", session.Project.Module.Slug);
        Assert.Equal(2, session.Project.Blueprints.Count);
        Assert.All(session.Project.Blueprints, blueprint => Assert.Null(blueprint.Combat));

        var generated = new NewModuleHandler(new FixedClock()).Handle(CreateCommand());
        Assert.True(generated.Succeeded);
        var generatedProject = Assert.IsType<ProjectState>(generated.State).Project;
        Assert.Equal(
            NormalizeNewLines(ProjectJsonSerializer.Serialize(session.Project)),
            NormalizeNewLines(ProjectJsonSerializer.Serialize(generatedProject)));

        Directory.CreateDirectory(_temporaryRoot);
        var packagePath = Path.Combine(_temporaryRoot, "les-iles-brisees.orpmod");
        var export = ModulePackager.Export(fixtureDirectory, packagePath);
        var validation = ModulePackageValidator.Validate(packagePath);
        var load = ModulePackageLoader.Load(packagePath, Path.Combine(_temporaryRoot, "cache"));

        Assert.Equal("les-iles-brisees", export.Manifest.Slug);
        Assert.True(validation.IsValid);
        Assert.Equal(
            NormalizeNewLines(ProjectJsonSerializer.Serialize(generatedProject)),
            NormalizeNewLines(ProjectJsonSerializer.Serialize(load.Project)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot))
        {
            Directory.Delete(_temporaryRoot, recursive: true);
        }
    }

    private static NewModuleCommand CreateCommand() =>
        new(
            CommandId.From(Guid.Parse("8ff97f8b-f9b4-486a-84d5-9defe849f9ef")),
            new ProjectRevision(0),
            ProjectId.From(Guid.Parse("4d410f21-2d47-4f08-b5d0-2d2e5e43796a")),
            ModuleId.From(Guid.Parse("0a8d1557-b84b-424b-8857-e8f0a7ac2765")),
            AreaId.From(Guid.Parse("ea40d07f-3e30-46a5-a39d-f4f14a25d23a")),
            BlueprintId.From(Guid.Parse("ba1e3949-af2a-409a-af27-8c6698694111")),
            EntityId.From(Guid.Parse("50542d7e-d54f-409b-8156-06cca92b2ea6")),
            BlueprintId.From(Guid.Parse("46d891e5-a116-48df-8b2e-0fdebf16bc52")),
            EntityId.From(Guid.Parse("72dd2ac1-381c-4652-a8ff-5d2be82dcb61")),
            "Les Îles Brisées",
            "Un archipel à reconstruire.",
            "Archipel oublié",
            DryRun: false);

    private static string NormalizeNewLines(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 8, 2, 14, 0, 0, TimeSpan.Zero);
    }
}
