using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.RuntimeExtensions;

namespace Forge.Application.Tests.Projects;

public sealed class SetRuntimeExtensionsHandlerTests
{
    [Fact]
    public void Runtime_extensions_preview_apply_undo_and_redo_the_same_canonical_profile()
    {
        var context = new PlaceEntityTestContext();
        var source = context.State.Project.Module.RuntimePolicies.Extensions;
        var rule = new DataDrivenRuleSchema(Guid.NewGuid(), 1, "classes.hero", RuleSchemaKind.Class,
            "{\"type\":\"object\"}", "{\"hitDie\":10}", true, "test");
        var widget = new ScriptableUiWidget(Guid.NewGuid(), "health-label", ScriptableUiWidgetKind.Label,
            null, "Health", "player.health", false, 0, "player");
        var ui = new ScriptableUiDocument(Guid.NewGuid(), 1, "hud", "Adventure HUD", "forge-dark", true,
            [widget], [], "test");
        var data = new RuntimeJsonDocumentDefinition(Guid.NewGuid(), 1, "module-state",
            "{\"type\":\"object\"}", "{\"chapter\":1}", 4096, 8, true, "1.0", "test");
        var profile = new ModuleRuntimeExtensionProfile(source.Id, 1, [rule], [ui], [data],
            source.SnapshotPolicy, "test");
        var handler = new SetRuntimeExtensionsHandler(new FixedClock(PlaceEntityTestContext.Timestamp));
        var command = new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()), context.State.Revision,
            profile, true);

        var preview = handler.Handle(context.State, command);
        Assert.True(preview.Succeeded);
        Assert.Equal("module/runtimePolicies/extensions", preview.Patch!.Path);
        Assert.Same(context.State, preview.State);

        var applied = handler.Handle(context.State, command with { DryRun = false });
        Assert.True(applied.Succeeded);
        Assert.Equal("hud", Assert.Single(applied.State.Project.Module.RuntimePolicies.Extensions.UiDocuments).Key);
        var history = new ProjectCommandHistory(context.State);
        history.Record(applied);
        Assert.True(history.Undo().Succeeded);
        Assert.Empty(history.State.Project.Module.RuntimePolicies.Extensions.UiDocuments);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal("classes.hero", Assert.Single(history.State.Project.Module.RuntimePolicies.Extensions.RuleSchemas).Key);
    }
}
