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

    [Fact]
    public void TranslatorBundleImportsAtomicallyAndRejectsStaleOrUnknownContent()
    {
        var catalog = LocalizationCatalogExtractor.Extract(CreateState().Project).Catalog;
        var bundle = LocalizationTranslationExchange.Export(catalog, "en-US");
        var translated = bundle with
        {
            Units = bundle.Units.Select(unit => unit with { Translation = $"EN {unit.Source}" }).ToArray(),
        };

        var imported = LocalizationTranslationExchange.Import(catalog, translated);
        var stale = LocalizationTranslationExchange.Import(catalog,
            translated with { CatalogContentSha256 = new string('0', 64) });
        var unknown = LocalizationTranslationExchange.Import(catalog,
            translated with
            {
                Units = [.. translated.Units, new LocalizationTranslationUnit("unknown.key", "x", "y", "test")],
            });

        Assert.True(imported.Succeeded);
        Assert.Equal("EN Bienvenue", imported.Catalog!.Resolve("dialogue.welcome", "en-US"));
        Assert.Contains(LocalizationTranslationExchange.HashCode, stale.Diagnostics);
        Assert.Contains(unknown.Diagnostics, item => item.StartsWith(LocalizationTranslationExchange.KeyCode));
        Assert.Same(catalog, stale.Catalog ?? catalog);
    }

    [Fact]
    public void ForgeScriptLocalizationHandleIsOpaqueTypedAndReadOnly()
    {
        var catalog = LocalizationCatalogExtractor.Extract(CreateState().Project).Catalog;
        var service = new LocalizationScriptService(catalog);
        var handle = Assert.IsType<Forge.Scripting.ForgeScriptOpaqueHandle>(service.Find("dialogue.welcome"));

        var snapshot = service.Read(handle, "fr");

        Assert.Equal(Forge.Scripting.ForgeScriptHandleKind.LocalizationMessage, handle.Kind);
        Assert.Equal("Bienvenue", snapshot.Text);
        Assert.Equal(catalog.ContentSha256, snapshot.ContentSha256);
        Assert.Throws<InvalidOperationException>(() => service.Read(
            Forge.Scripting.ForgeScriptOpaqueHandle.Create(Forge.Scripting.ForgeScriptHandleKind.Asset, 1, 1), "fr"));
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
