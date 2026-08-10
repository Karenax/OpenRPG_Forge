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
        var tooling = source.ModernTooling;
        var qualitySource = tooling.QualityAssurance;
        var quality = new QualityAssuranceProfile(qualitySource.Id, 1, qualitySource.Network,
            qualitySource.Visual, qualitySource.Benchmarks, qualitySource.PlatformBudgets, qualitySource.Profiler,
            "test", qualitySource.Accessibility, qualitySource.SecurityFuzz,
            qualitySource.ClickableDiagnostics with { ActiveProfile = "build", Provenance = "test" },
            qualitySource.MaliciousFixtures with { MaximumArrayEntries = 32_768, Provenance = "test" },
            qualitySource.AuroraParityCoverage with { Provenance = "test" },
            qualitySource.AcceptanceGates with { Provenance = "test" });
        var modern = new ModernRuntimeToolingProfile(tooling.Id, 1, tooling.RuntimeCopy,
            tooling.AreaEffectIndicator, tooling.UiToolkit, tooling.Compiler, tooling.Documentation, "test", quality);
        var profile = new ModuleRuntimeExtensionProfile(source.Id, 1, [rule], [ui], [data],
            source.SnapshotPolicy, "test", modern);
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
        Assert.Equal("build", applied.State.Project.Module.RuntimePolicies.Extensions.ModernTooling
            .QualityAssurance.ClickableDiagnostics.ActiveProfile);
        Assert.Equal(32_768, applied.State.Project.Module.RuntimePolicies.Extensions.ModernTooling
            .QualityAssurance.MaliciousFixtures.MaximumArrayEntries);
        var history = new ProjectCommandHistory(context.State);
        history.Record(applied);
        Assert.True(history.Undo().Succeeded);
        Assert.Empty(history.State.Project.Module.RuntimePolicies.Extensions.UiDocuments);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal("classes.hero", Assert.Single(history.State.Project.Module.RuntimePolicies.Extensions.RuleSchemas).Key);
        Assert.Equal("build", history.State.Project.Module.RuntimePolicies.Extensions.ModernTooling
            .QualityAssurance.ClickableDiagnostics.ActiveProfile);
    }

    [Fact]
    public void Runtime_extensions_refuse_invalid_acceptance_governance_before_mutation()
    {
        var context = new PlaceEntityTestContext();
        var source = context.State.Project.Module.RuntimePolicies.Extensions;
        var tooling = source.ModernTooling;
        var qualitySource = tooling.QualityAssurance;
        var invalidQuality = new QualityAssuranceProfile(qualitySource.Id, 1, qualitySource.Network,
            qualitySource.Visual, qualitySource.Benchmarks, qualitySource.PlatformBudgets, qualitySource.Profiler,
            "test", qualitySource.Accessibility, qualitySource.SecurityFuzz,
            qualitySource.ClickableDiagnostics with { ActiveProfile = "unsafe" });
        var modern = new ModernRuntimeToolingProfile(tooling.Id, 1, tooling.RuntimeCopy,
            tooling.AreaEffectIndicator, tooling.UiToolkit, tooling.Compiler, tooling.Documentation, "test", invalidQuality);
        var profile = new ModuleRuntimeExtensionProfile(source.Id, 1, source.RuleSchemas, source.UiDocuments,
            source.JsonDocuments, source.SnapshotPolicy, "test", modern, source.OperationalReadiness, source.PostLaunch);

        var result = new SetRuntimeExtensionsHandler(new FixedClock(PlaceEntityTestContext.Timestamp)).Handle(
            context.State, new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()), context.State.Revision,
                profile, false));

        Assert.False(result.Succeeded);
        Assert.Same(context.State, result.State);
        Assert.Equal(Forge.Domain.Validation.QualityAcceptanceProfileValidator.DiagnosticsInvalidCode,
            Assert.Single(result.Diagnostics).Code);
    }
}
