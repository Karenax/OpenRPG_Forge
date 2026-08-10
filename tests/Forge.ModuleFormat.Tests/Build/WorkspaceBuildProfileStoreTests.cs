using Forge.Domain.Build;
using Forge.ModuleFormat.Build;

namespace Forge.ModuleFormat.Tests.Build;

public sealed class WorkspaceBuildProfileStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "openrpg-build-profile", Guid.NewGuid().ToString("N"));

    public WorkspaceBuildProfileStoreTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void MissingProfileUsesOptionalCanonicalDefault()
    {
        var result = WorkspaceBuildProfileStore.Load(_root);
        Assert.True(result.Succeeded);
        Assert.True(result.FileMissing);
        Assert.Equal(WorkspaceBuildProfile.DefaultId, result.Profile.Id);
    }

    [Fact]
    public void SaveCloseReloadAndSecondSaveAreExactAndDeterministic()
    {
        var profile = WorkspaceBuildProfile.CreateDefault();
        Assert.True(WorkspaceBuildProfileStore.Save(_root, profile).Succeeded);
        var path = Path.Combine(_root, WorkspaceBuildProfileStore.RelativePath);
        var first = File.ReadAllBytes(path);

        var loaded = WorkspaceBuildProfileStore.Load(_root);
        Assert.True(loaded.Succeeded);
        Assert.False(loaded.FileMissing);
        Assert.Equal(profile.Id, loaded.Profile.Id);
        Assert.Equal(profile.SchemaVersion, loaded.Profile.SchemaVersion);
        Assert.Equal(profile.Provenance, loaded.Profile.Provenance);
        Assert.Equal(profile.Layout, loaded.Profile.Layout);
        Assert.Equal(profile.CliOperations, loaded.Profile.CliOperations);
        Assert.Equal(profile.CachePath, loaded.Profile.CachePath);
        Assert.Equal(profile.Governance.Id, loaded.Profile.Governance.Id);
        Assert.Equal(profile.Governance.Worktrees, loaded.Profile.Governance.Worktrees with
        {
            IntegrationChecks = profile.Governance.Worktrees.IntegrationChecks,
        });
        Assert.Equal(profile.Governance.Worktrees.IntegrationChecks,
            loaded.Profile.Governance.Worktrees.IntegrationChecks);
        Assert.Equal(profile.Governance.ContinuousIntegration, loaded.Profile.Governance.ContinuousIntegration);
        Assert.Equal(profile.Governance.ReferenceFixtures.CatalogPath,
            loaded.Profile.Governance.ReferenceFixtures.CatalogPath);
        Assert.Equal(profile.Governance.ReferenceFixtures.Fixtures,
            loaded.Profile.Governance.ReferenceFixtures.Fixtures);
        Assert.Equal(profile.Governance.Versioning.CoreVersion, loaded.Profile.Governance.Versioning.CoreVersion);
        Assert.Equal(profile.Governance.Versioning.KitVersions, loaded.Profile.Governance.Versioning.KitVersions);
        Assert.Equal(profile.Governance.Versioning.SchemaVersions, loaded.Profile.Governance.Versioning.SchemaVersions);
        Assert.Equal(profile.Governance.Versioning.CompatibilityRules,
            loaded.Profile.Governance.Versioning.CompatibilityRules);
        Assert.Equal(profile.Governance.ArchitectureDecisions.AllowedStatuses,
            loaded.Profile.Governance.ArchitectureDecisions.AllowedStatuses);
        Assert.Equal(profile.Collaboration, loaded.Profile.Collaboration);
        Assert.Equal(profile.Compatibility, loaded.Profile.Compatibility);
        Assert.True(WorkspaceBuildProfileStore.Save(_root, loaded.Profile).Succeeded);
        Assert.Equal(first, File.ReadAllBytes(path));
    }

    [Fact]
    public void LegacyProfileWithoutOptionalGovernanceLoadsSafeDefault()
    {
        var profile = WorkspaceBuildProfile.CreateDefault();
        Assert.True(WorkspaceBuildProfileStore.Save(_root, profile).Succeeded);
        var path = Path.Combine(_root, WorkspaceBuildProfileStore.RelativePath);
        var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        document.Remove("governance");
        File.WriteAllText(path, document.ToJsonString());

        var loaded = WorkspaceBuildProfileStore.Load(_root);

        Assert.True(loaded.Succeeded);
        Assert.Equal(WorkspaceBuildGovernanceProfile.DefaultId, loaded.Profile.Governance.Id);
    }

    [Fact]
    public void LegacyProfileWithoutOptionalCollaborationLoadsSafeDefault()
    {
        var profile = WorkspaceBuildProfile.CreateDefault();
        Assert.True(WorkspaceBuildProfileStore.Save(_root, profile).Succeeded);
        var path = Path.Combine(_root, WorkspaceBuildProfileStore.RelativePath);
        var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        document.Remove("collaboration");
        File.WriteAllText(path, document.ToJsonString());

        var loaded = WorkspaceBuildProfileStore.Load(_root);

        Assert.True(loaded.Succeeded);
        Assert.Equal(WorkspaceBuildCollaborationProfile.DefaultId, loaded.Profile.Collaboration.Id);
        Assert.Empty(WorkspaceBuildCollaborationProfileValidator.Validate(loaded.Profile.Collaboration));
    }

    [Fact]
    public void LegacyProfileWithoutOptionalCompatibilityLoadsSafeDefault()
    {
        var profile = WorkspaceBuildProfile.CreateDefault();
        Assert.True(WorkspaceBuildProfileStore.Save(_root, profile).Succeeded);
        var path = Path.Combine(_root, WorkspaceBuildProfileStore.RelativePath);
        var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        document.Remove("compatibility");
        File.WriteAllText(path, document.ToJsonString());

        var loaded = WorkspaceBuildProfileStore.Load(_root);

        Assert.True(loaded.Succeeded);
        Assert.Equal(ProjectCompatibilityProfile.DefaultId, loaded.Profile.Compatibility.Id);
        Assert.Empty(ProjectCompatibilityProfileValidator.Validate(loaded.Profile.Compatibility));
    }

    [Fact]
    public void InvalidConfigurationIsLocalizedAndNeverWritten()
    {
        var source = WorkspaceBuildProfile.CreateDefault();
        var invalid = new WorkspaceBuildProfile(Guid.Empty, source.SchemaVersion, source.Provenance,
            source.RequireGitRepository, source.TextDocuments, source.ExplicitGeneratedFiles,
            source.ReproducibleAssets, source.Layout, source.CliOperations, source.StableOrdering,
            source.NormalizedTimestamps, source.LockedDependencies, source.ContentHashes,
            source.VerifyReproducibility, source.IncrementalBuild, source.CachePath, source.RebuildAffectedOnly);

        var result = WorkspaceBuildProfileStore.Save(_root, invalid);
        Assert.False(result.Succeeded);
        Assert.Equal(WorkspaceBuildProfileValidator.IdentityInvalidCode, Assert.Single(result.Diagnostics).Code);
        Assert.False(File.Exists(Path.Combine(_root, WorkspaceBuildProfileStore.RelativePath)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
