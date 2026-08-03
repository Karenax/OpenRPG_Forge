using Forge.Application.Commands;
using Forge.Application.Localization;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Dialogues;
using Forge.Domain.Modules;
using Forge.Domain.Projects;

namespace Forge.Application.Tests.Localization;

public sealed class LocalizationWorkflowTests
{
    [Fact]
    public void ExtractPreservesTranslationsAndCommandSupportsDryRunUndoRedo()
    {
        var state = CreateState();
        var first = LocalizationCatalogExtractor.Extract(state.Project);
        var translated = new Forge.Domain.Localization.LocalizationCatalog(
            first.Catalog.Id,
            first.Catalog.DefaultLocale,
            [.. first.Catalog.Locales, "en"],
            first.Catalog.Messages.Select(message => message.Key == "dialogue.welcome"
                ? new Forge.Domain.Localization.LocalizationMessage(
                    message.Key, message.Fallback, new Dictionary<string, string> { ["en"] = "Welcome" }, message.Provenance)
                : message).ToArray());
        var handler = new SetLocalizationCatalogHandler(new FixedClock(DateTimeOffset.UnixEpoch));
        var command = new SetLocalizationCatalogCommand(
            CommandId.From(Guid.Parse("d2000000-0000-4000-8000-000000000001")),
            state.Revision,
            translated,
            DryRun: false);

        var dryRun = handler.Handle(state, command with { DryRun = true });
        var applied = handler.Handle(state, command);
        var history = new ProjectCommandHistory(state);
        history.Record(applied);

        Assert.Empty(first.Diagnostics);
        Assert.Contains(first.Catalog.Messages, message => message.Key == "module.name");
        Assert.Contains(first.Catalog.Messages, message => message.Key == "dialogue.welcome");
        Assert.True(dryRun.Succeeded);
        Assert.Same(state, dryRun.State);
        Assert.Equal("Welcome", applied.State.Project.Module.Localization.Resolve("dialogue.welcome", "en"));
        Assert.True(history.Undo().Succeeded);
        Assert.Empty(history.State.Project.Module.Localization.Messages);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal("Welcome", history.State.Project.Module.Localization.Resolve("dialogue.welcome", "en"));
    }

    [Fact]
    public void PseudoLocalizationPreservesPlaceholders()
    {
        Assert.Equal("⟦Hëllô {player} ···⟧", PseudoLocalization.Transform("Hello {player}"));
    }

    private static ProjectState CreateState()
    {
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("d2000000-0000-4000-8000-000000000002")), "start", "Start", []);
        var module = new ModuleDefinition(
            ModuleId.From(Guid.Parse("d2000000-0000-4000-8000-000000000003")),
            "localized-module", "Module localisé", "Description", area.Id,
            new ProjectMetadata(
                ProjectMetadataId.From(Guid.Parse("d2000000-0000-4000-8000-000000000004")),
                "fr",
                [new ProjectLocalizedMetadata("fr", "Module localisé", "Résumé", "Description", "Équipe")],
                "Forge", "1.0.0", "CC0-1.0", null, [], "Test", "test"));
        var nodeId = DialogueNodeId.From(Guid.Parse("d2000000-0000-4000-8000-000000000005"));
        var dialogue = new DialogueDefinition(
            DialogueId.From(Guid.Parse("d2000000-0000-4000-8000-000000000006")),
            "welcome", "Welcome", nodeId,
            [new DialogueNode(nodeId, null, new LocalizedText("dialogue.welcome", "Bienvenue"))]);
        return new ProjectState(new ProjectDefinition(
            ProjectId.From(Guid.Parse("d2000000-0000-4000-8000-000000000007")),
            "Localization", module, [], [area], [dialogue]), new ProjectRevision(4));
    }
}
