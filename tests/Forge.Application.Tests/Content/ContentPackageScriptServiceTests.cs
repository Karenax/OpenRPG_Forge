using Forge.Application.Content;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Content;
using Forge.Domain.Modules;
using Forge.Scripting;

namespace Forge.Application.Tests.Content;

public sealed class ContentPackageScriptServiceTests
{
    [Fact]
    public void Reads_only_through_typed_opaque_handle_and_rejects_wrong_kind()
    {
        var package = Package();
        var catalog = new ContentPackageCatalog(Guid.NewGuid(), 1, "tests", ContentConflictPolicy.HighestPriority, [package]);
        var service = new ContentPackageScriptService(catalog);

        var handle = Assert.IsType<ForgeScriptOpaqueHandle>(service.Find("crypt.core"));
        var snapshot = service.Read(handle);

        Assert.Equal(ForgeScriptHandleKind.ContentPackage, handle.Kind);
        Assert.Equal(package.Id, snapshot.Id);
        Assert.Equal("1.0.0", snapshot.Version);
        Assert.Null(service.Find("missing"));
        Assert.Throws<InvalidOperationException>(() => service.Read(
            ForgeScriptOpaqueHandle.Create(ForgeScriptHandleKind.Entity, 1, 1)));
    }

    [Fact]
    public void Canonical_command_dry_run_apply_undo_redo_has_one_semantic_result()
    {
        var state = CreateState();
        var current = state.Project.Module.Content;
        var catalog = new ContentPackageCatalog(state.Project.Module.Id.Value, 1, "tests",
            ContentConflictPolicy.HighestPriority, [Package()]);
        var configuration = new ContentWorkspaceConfiguration(current.Id, current.Batch, current.DataTables,
            current.MergePolicy, current.Provenance, current.SchemaVersion, catalog);
        var command = new SetContentWorkspaceCommand(CommandId.From(Guid.NewGuid()), state.Revision,
            configuration, DryRun: false);
        var handler = new SetContentWorkspaceHandler(new FixedClock(DateTimeOffset.UnixEpoch));

        var preview = handler.Handle(state, command with { DryRun = true });
        var applied = handler.Handle(state, command);
        var history = new ProjectCommandHistory(state);
        history.Record(applied);

        Assert.True(preview.Succeeded);
        Assert.Equal(preview.Patch, applied.Patch);
        Assert.Contains("1 content package", preview.Patch!.Summary, StringComparison.Ordinal);
        Assert.Single(applied.State.Project.Module.Content.Packages.Packages);
        Assert.True(history.Undo().Succeeded);
        Assert.Empty(history.State.Project.Module.Content.Packages.Packages);
        Assert.True(history.Redo().Succeeded);
        Assert.Single(history.State.Project.Module.Content.Packages.Packages);
    }

    private static ContentPackageDefinition Package()
    {
        var source = new ContentPackageSource(Guid.NewGuid(), 1, "tests", "creature.warden",
            "data/warden.json", ContentSourceKind.Json, true, "CC0-1.0", new string('a', 64), 4096);
        return new ContentPackageDefinition(Guid.NewGuid(), 1, "tests", "crypt.core", "1.0.0",
            "CC0-1.0", new string('b', 64), 5, [], [], [source]);
    }

    private static ProjectState CreateState()
    {
        var area = new AreaDefinition(AreaId.From(Guid.NewGuid()), "start", "Start", []);
        var module = new ModuleDefinition(ModuleId.From(Guid.NewGuid()), "r016", "R016", "Test", area.Id);
        return new ProjectState(new Forge.Domain.Projects.ProjectDefinition(
            Forge.Domain.Projects.ProjectId.From(Guid.NewGuid()), "R016", module, [], [area]),
            new ProjectRevision(3));
    }
}
