using Forge.Domain.Areas;
using Forge.Domain.Build;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class StudioUx0IGlobalDataPersistenceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "openrpg-studio-ux0-i-persistence",
        Guid.NewGuid().ToString("N"));

    public StudioUx0IGlobalDataPersistenceTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void Ux0ISaveCloseReloadPreservesVariablesEightGuidePagesAndCachePresetExactly()
    {
        var areaId = AreaId.From(Guid.Parse("d0250000-0000-4000-8000-000000000440"));
        var moduleId = ModuleId.From(Guid.Parse("d0250000-0000-4000-8000-000000000441"));
        var guide = new ProjectLocalizedMetadata(
            "fr",
            "Aventure UX0-I",
            "Résumé",
            "Synopsis",
            "Crédits",
            "Installation",
            "Règles",
            "Contrôles",
            "Licences",
            "Avertissements",
            "Compatibilité");
        var metadata = new ProjectMetadata(
            ProjectMetadataId.From(moduleId.Value),
            "fr",
            [guide],
            "Atelier Forge",
            "1.0.0",
            "CC0-1.0",
            null,
            [],
            "Tout public",
            "studio:ux0-i");
        var variables = new[]
        {
            new ProjectVariableDefinition(
                ProjectVariableId.From(Guid.Parse("d0250000-0000-4000-8000-000000000442")),
                "world.chapter",
                ProjectVariableValueKind.Integer,
                ProjectVariableScope.Save,
                "1",
                "Chapitre courant",
                "studio:ux0-i",
                replication: ProjectVariableReplicationMode.AllClients,
                requiredAtRuntime: true),
            new ProjectVariableDefinition(
                ProjectVariableId.From(Guid.Parse("d0250000-0000-4000-8000-000000000443")),
                "server.phase",
                ProjectVariableValueKind.String,
                ProjectVariableScope.Server,
                "lobby",
                "Phase serveur",
                "studio:ux0-i",
                replication: ProjectVariableReplicationMode.ServerAuthority),
        };
        var area = new AreaDefinition(areaId, "studio-ux0-i", "Studio UX0-I", []);
        var module = new ModuleDefinition(
            moduleId,
            "studio-ux0-i",
            "Aventure UX0-I",
            guide.Description,
            areaId,
            metadata,
            variables);
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("d0250000-0000-4000-8000-000000000444")),
            "Aventure UX0-I",
            module,
            [],
            [area]);

        var firstProjectJson = ProjectJsonSerializer.Serialize(project);
        var restoredProject = ProjectJsonSerializer.Deserialize(firstProjectJson);
        var secondProjectJson = ProjectJsonSerializer.Serialize(restoredProject);
        var profile = WithCache(WorkspaceBuildProfile.CreateDefault(), ".forge/cache");
        Assert.True(WorkspaceBuildProfileStore.Save(_root, profile).Succeeded);
        var firstProfileBytes = File.ReadAllBytes(Path.Combine(_root, WorkspaceBuildProfileStore.RelativePath));
        var restoredProfile = WorkspaceBuildProfileStore.Load(_root);
        Assert.True(WorkspaceBuildProfileStore.Save(_root, restoredProfile.Profile).Succeeded);

        Assert.Equal(firstProjectJson, secondProjectJson);
        Assert.Equal(variables.Select(item => item.Id).OrderBy(item => item.ToString(), StringComparer.Ordinal),
            restoredProject.Module.Variables.Select(item => item.Id)
                .OrderBy(item => item.ToString(), StringComparer.Ordinal));
        var restoredGuide = restoredProject.Module.Metadata.GetDefaultLocalization();
        Assert.Equal(
            new[]
            {
                guide.Description, guide.Installation, guide.Rules, guide.Controls,
                guide.Credits, guide.Licenses, guide.Warnings, guide.Compatibility,
            },
            new[]
            {
                restoredGuide.Description, restoredGuide.Installation, restoredGuide.Rules, restoredGuide.Controls,
                restoredGuide.Credits, restoredGuide.Licenses, restoredGuide.Warnings,
                restoredGuide.Compatibility,
            });
        Assert.True(restoredProfile.Succeeded);
        Assert.Equal(".forge/cache", restoredProfile.Profile.CachePath);
        Assert.Equal(profile.Id, restoredProfile.Profile.Id);
        Assert.Equal(profile.SchemaVersion, restoredProfile.Profile.SchemaVersion);
        Assert.Equal(firstProfileBytes,
            File.ReadAllBytes(Path.Combine(_root, WorkspaceBuildProfileStore.RelativePath)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static WorkspaceBuildProfile WithCache(WorkspaceBuildProfile source, string cachePath)
    {
        var layout = source.Layout.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        layout["cache"] = cachePath;
        return new WorkspaceBuildProfile(
            source.Id,
            source.SchemaVersion,
            "studio:ux0-i",
            source.RequireGitRepository,
            source.TextDocuments,
            source.ExplicitGeneratedFiles,
            source.ReproducibleAssets,
            layout,
            source.CliOperations,
            source.StableOrdering,
            source.NormalizedTimestamps,
            source.LockedDependencies,
            source.ContentHashes,
            source.VerifyReproducibility,
            source.IncrementalBuild,
            cachePath,
            source.RebuildAffectedOnly,
            source.Governance,
            source.Collaboration,
            source.Compatibility);
    }
}
