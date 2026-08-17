using Forge.Domain.Build;
using Forge.Mcp.Build;
using Forge.ModuleFormat.Build;

namespace Forge.Mcp.Tests.Build;

public sealed class BuildAgentServiceTests
{
    [Fact]
    public void CompatibilityReadPreviewApplyAndRevertAreFilteredAtomicAndShellFree()
    {
        using var workspace = new TestWorkspace();
        var original = workspace.Project.QueryProjectCompatibility();
        Assert.True(original.Found);
        Assert.False(workspace.Project.QueryProjectCompatibility("does-not-exist").Found);

        var preview = workspace.Project.PreviewProjectCompatibility(new ProjectCompatibilityPlanRequest(
            1, 0, "0.6.34", "1.29.0", true, true));

        Assert.True(preview.Succeeded);
        Assert.False(preview.Applied);
        Assert.NotNull(preview.PlanId);
        Assert.Contains(preview.SemanticDiff, item => item.StartsWith("minimumRuntimeVersion:", StringComparison.Ordinal));
        Assert.False(File.Exists(Path.Combine(workspace.RootDirectory, WorkspaceBuildProfileStore.RelativePath)));

        var applied = workspace.Project.ApplyProjectCompatibilityPlan(preview.PlanId!);
        Assert.True(applied.Succeeded);
        Assert.True(applied.Applied);
        Assert.Equal("0.6.34", workspace.Project.QueryProjectCompatibility("0.6.34").MinimumRuntimeVersion);
        Assert.True(File.Exists(Path.Combine(workspace.RootDirectory, WorkspaceBuildProfileStore.RelativePath)));

        var reverted = workspace.Project.RevertProjectCompatibilityPlan(preview.PlanId!);
        Assert.True(reverted.Succeeded);
        Assert.True(reverted.Reverted);
        Assert.Equal(original.MinimumRuntimeVersion,
            workspace.Project.QueryProjectCompatibility(original.Id).MinimumRuntimeVersion);
    }

    [Fact]
    public void ReadValidatePlanBuildAndTestAreStructuredAndShellFree()
    {
        using var workspace = new TestWorkspace();
        var output = Path.Combine(Path.GetTempPath(), "openrpg-forge-mcp-output", Guid.NewGuid().ToString("N"), "module.orpmod");
        try
        {
            var profile = workspace.Project.GetBuildProfile();
            var validation = workspace.Project.ValidateBuild();
            var plan = workspace.Project.PlanBuild();
            var cache = workspace.Project.InspectBuildCache();
            var normalization = workspace.Project.PreviewBuildNormalization();
            var migration = workspace.Project.PreviewWorkspaceMigration();
            var references = workspace.Project.GetBuildReferences();
            var semanticBeforeBuild = workspace.Project.GetBuildSemanticDiff();
            var build = workspace.Project.BuildModule(output);
            var semanticAfterBuild = workspace.Project.GetBuildSemanticDiff();
            var test = workspace.Project.TestBuild();

            Assert.Equal(WorkspaceBuildProfile.CurrentSchemaVersion, profile.SchemaVersion);
            Assert.True(profile.FileMissing);
            Assert.True(profile.Deterministic);
            Assert.True(profile.Incremental);
            Assert.Equal(WorkspaceBuildGovernanceProfile.CurrentSchemaVersion, profile.Governance.SchemaVersion);
            Assert.Equal(5, profile.Governance.FixtureKinds.Count);
            Assert.Equal("0.6.34", profile.Governance.CoreVersion);
            Assert.Equal(".github/workflows/ci.yml", profile.Governance.CiWorkflow);
            Assert.Equal("docs/ADR/ADR-TEMPLATE.md", profile.Governance.AdrTemplate);
            Assert.Equal(WorkspaceBuildCollaborationProfile.CurrentSchemaVersion,
                profile.Collaboration.SchemaVersion);
            Assert.Contains("references-forward-reverse-rebuildable", profile.Collaboration.Guarantees);
            Assert.True(validation.Valid);
            Assert.NotEmpty(plan.Resources);
            Assert.True(cache.Succeeded);
            Assert.True(cache.DryRun);
            Assert.True(normalization.Succeeded);
            Assert.True(normalization.DryRun);
            Assert.True(migration.Succeeded);
            Assert.True(migration.DryRun);
            Assert.True(migration.Idempotent);
            Assert.NotEmpty(references.Nodes);
            Assert.Contains(references.Nodes, node => node.Dependents.Count > 0);
            Assert.True(semanticBeforeBuild.Succeeded);
            Assert.NotEmpty(semanticBeforeBuild.Changes);
            Assert.True(build.Succeeded);
            Assert.True(File.Exists(output));
            Assert.True(semanticAfterBuild.Succeeded);
            Assert.Empty(semanticAfterBuild.Changes);
            Assert.True(test.Succeeded);
            Assert.True(test.Reproducible);
        }
        finally
        {
            var directory = Path.GetDirectoryName(output)!;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
