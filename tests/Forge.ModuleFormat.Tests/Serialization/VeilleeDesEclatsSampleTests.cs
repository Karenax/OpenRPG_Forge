using Forge.Domain.Blueprints;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class VeilleeDesEclatsSampleTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Original_sample_round_trips_exports_and_loads_as_a_distinct_authored_module()
    {
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "la-veillee-des-eclats"); var opened = ProjectSession.Open(fixture); Assert.True(opened.Succeeded); var project = opened.Session!.Project; Assert.Equal("la-veillee-des-eclats", project.Module.Slug); Assert.Equal(8, project.Blueprints.Count); Assert.Equal(8, Assert.Single(project.Areas).Entities.Count); Assert.Single(project.Dialogues); Assert.Single(project.Quests); Assert.Contains(project.Blueprints, item => item.Kind == BlueprintKind.Door);
        Directory.CreateDirectory(_temporaryRoot); var package = Path.Combine(_temporaryRoot, "la-veillee-des-eclats.orpmod"); var exported = ModulePackager.Export(fixture, package); Assert.Equal("la-veillee-des-eclats", exported.Manifest.Slug); var loaded = ModulePackageLoader.Load(package, Path.Combine(_temporaryRoot, "cache")); Assert.Equal("La Veillée des Éclats", loaded.Project.Title); Assert.Equal("parvis-des-astres", Assert.Single(loaded.Project.Areas).Slug);
    }

    public void Dispose() { if (Directory.Exists(_temporaryRoot)) Directory.Delete(_temporaryRoot, true); }
}
