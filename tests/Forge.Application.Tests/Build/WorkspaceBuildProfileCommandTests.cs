using Forge.Application.Build;
using Forge.Application.Commands;
using Forge.Application.Tests.Entities;
using Forge.Domain.Build;

namespace Forge.Application.Tests.Build;

public sealed class WorkspaceBuildProfileCommandTests
{
    private readonly SetWorkspaceBuildProfileHandler _handler =
        new(new FixedClock(DateTimeOffset.Parse("2026-08-07T12:00:00Z")));

    [Fact]
    public void UiAndCliCanShareDryRunApplyUndoRedoCanonicalCommand()
    {
        var history = new WorkspaceBuildProfileCommandHistory();
        var updated = WithCache(history.State.Profile, ".forge-cache");

        var preview = _handler.Handle(history.State, Command(history.State.Revision, updated, true));
        Assert.True(preview.Succeeded);
        Assert.False(preview.Audit.Applied);
        Assert.Equal("build/profile", preview.Patch?.Path);
        Assert.Equal(".forge", history.State.Profile.CachePath);

        var applied = _handler.Handle(history.State, Command(history.State.Revision, updated, false));
        history.Record(applied);
        Assert.Equal(".forge-cache", history.State.Profile.CachePath);
        Assert.True(history.Undo());
        Assert.Equal(".forge", history.State.Profile.CachePath);
        Assert.True(history.Redo());
        Assert.Equal(".forge-cache", history.State.Profile.CachePath);
    }

    [Fact]
    public void InvalidProfileIsRejectedWithStableLocalizedDiagnostic()
    {
        var state = new WorkspaceBuildProfileState();
        var invalid = WithCache(state.Profile, "../outside");

        var result = _handler.Handle(state, Command(state.Revision, invalid, false));

        Assert.False(result.Succeeded);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(WorkspaceBuildProfileValidator.LayoutInvalidCode, diagnostic.Code);
        Assert.StartsWith("build/layout:", diagnostic.Message, StringComparison.Ordinal);
        Assert.Equal(state, result.State);
    }

    [Fact]
    public void GovernanceUsesTheSameDryRunApplyUndoRedoCommand()
    {
        var history = new WorkspaceBuildProfileCommandHistory();
        var governance = history.State.Profile.Governance with
        {
            Worktrees = history.State.Profile.Governance.Worktrees with { BranchPrefix = "feature/" },
        };
        var updated = WithGovernance(history.State.Profile, governance);

        var preview = _handler.Handle(history.State, Command(history.State.Revision, updated, true));
        Assert.True(preview.Succeeded);
        Assert.Equal("codex/", history.State.Profile.Governance.Worktrees.BranchPrefix);

        history.Record(_handler.Handle(history.State, Command(history.State.Revision, updated, false)));
        Assert.Equal("feature/", history.State.Profile.Governance.Worktrees.BranchPrefix);
        Assert.True(history.Undo());
        Assert.Equal("codex/", history.State.Profile.Governance.Worktrees.BranchPrefix);
        Assert.True(history.Redo());
        Assert.Equal("feature/", history.State.Profile.Governance.Worktrees.BranchPrefix);
    }

    [Fact]
    public void CollaborationUsesTheSameDryRunApplyUndoRedoCommand()
    {
        var history = new WorkspaceBuildProfileCommandHistory();
        var collaboration = history.State.Profile.Collaboration with
        {
            Cache = history.State.Profile.Collaboration.Cache with { MaximumEntries = 90_000 },
        };
        var updated = WithCollaboration(history.State.Profile, collaboration);

        var preview = _handler.Handle(history.State, Command(history.State.Revision, updated, true));
        Assert.True(preview.Succeeded);
        Assert.Equal(100_000, history.State.Profile.Collaboration.Cache.MaximumEntries);

        history.Record(_handler.Handle(history.State, Command(history.State.Revision, updated, false)));
        Assert.Equal(90_000, history.State.Profile.Collaboration.Cache.MaximumEntries);
        Assert.True(history.Undo());
        Assert.Equal(100_000, history.State.Profile.Collaboration.Cache.MaximumEntries);
        Assert.True(history.Redo());
        Assert.Equal(90_000, history.State.Profile.Collaboration.Cache.MaximumEntries);
    }

    [Fact]
    public void CompatibilityUsesTheSameDryRunApplyUndoRedoCommandAndScriptReadIsSafe()
    {
        var history = new WorkspaceBuildProfileCommandHistory();
        var compatibility = history.State.Profile.Compatibility with { MinimumRuntimeVersion = "0.6.33" };
        var updated = WithCompatibility(history.State.Profile, compatibility);

        var preview = _handler.Handle(history.State, Command(history.State.Revision, updated, true));
        Assert.True(preview.Succeeded);
        Assert.Equal("0.1.0", history.State.Profile.Compatibility.MinimumRuntimeVersion);

        history.Record(_handler.Handle(history.State, Command(history.State.Revision, updated, false)));
        var handle = new ProjectCompatibilityScriptService().Read(history.State.Profile);
        Assert.Equal("0.6.33", handle.MinimumRuntimeVersion);
        Assert.True(handle.AutomaticMigrations);
        Assert.True(history.Undo());
        Assert.Equal("0.1.0", history.State.Profile.Compatibility.MinimumRuntimeVersion);
        Assert.True(history.Redo());
        Assert.Equal("0.6.33", history.State.Profile.Compatibility.MinimumRuntimeVersion);
    }

    private static SetWorkspaceBuildProfileCommand Command(
        WorkspaceBuildProfileRevision revision, WorkspaceBuildProfile profile, bool dryRun) =>
        new(CommandId.From(Guid.NewGuid()), revision, profile, dryRun);

    private static WorkspaceBuildProfile WithCache(WorkspaceBuildProfile source, string cachePath)
    {
        var layout = source.Layout.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        layout["cache"] = cachePath;
        return new(source.Id, source.SchemaVersion, "test:r001", source.RequireGitRepository,
            source.TextDocuments, source.ExplicitGeneratedFiles, source.ReproducibleAssets, layout,
            source.CliOperations, source.StableOrdering, source.NormalizedTimestamps, source.LockedDependencies,
            source.ContentHashes, source.VerifyReproducibility, source.IncrementalBuild, cachePath,
            source.RebuildAffectedOnly, source.Governance, source.Collaboration);
    }

    private static WorkspaceBuildProfile WithGovernance(
        WorkspaceBuildProfile source,
        WorkspaceBuildGovernanceProfile governance) =>
        new(source.Id, source.SchemaVersion, "test:r002", source.RequireGitRepository,
            source.TextDocuments, source.ExplicitGeneratedFiles, source.ReproducibleAssets, source.Layout,
            source.CliOperations, source.StableOrdering, source.NormalizedTimestamps, source.LockedDependencies,
            source.ContentHashes, source.VerifyReproducibility, source.IncrementalBuild, source.CachePath,
            source.RebuildAffectedOnly, governance, source.Collaboration);

    private static WorkspaceBuildProfile WithCollaboration(
        WorkspaceBuildProfile source,
        WorkspaceBuildCollaborationProfile collaboration) =>
        new(source.Id, source.SchemaVersion, "test:r023", source.RequireGitRepository,
            source.TextDocuments, source.ExplicitGeneratedFiles, source.ReproducibleAssets, source.Layout,
            source.CliOperations, source.StableOrdering, source.NormalizedTimestamps, source.LockedDependencies,
            source.ContentHashes, source.VerifyReproducibility, source.IncrementalBuild, source.CachePath,
            source.RebuildAffectedOnly, source.Governance, collaboration, source.Compatibility);

    private static WorkspaceBuildProfile WithCompatibility(
        WorkspaceBuildProfile source,
        ProjectCompatibilityProfile compatibility) =>
        new(source.Id, source.SchemaVersion, "test:r026", source.RequireGitRepository,
            source.TextDocuments, source.ExplicitGeneratedFiles, source.ReproducibleAssets, source.Layout,
            source.CliOperations, source.StableOrdering, source.NormalizedTimestamps, source.LockedDependencies,
            source.ContentHashes, source.VerifyReproducibility, source.IncrementalBuild, source.CachePath,
            source.RebuildAffectedOnly, source.Governance, source.Collaboration, compatibility);
}
