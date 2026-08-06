using Forge.Application.Assistant;
using Forge.Application.Commands;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Assistant;
using Forge.Domain.Projects;

namespace Forge.Application.Tests.Assistant;

public sealed class LoreIndexWorkflowTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RetrievalUsesCanonicalSourcesAndProducesBoundedHashedContext()
    {
        var project = Project();
        var entry = Entry(project, "braise-ancienne", "Le Serment de braise protège la Crypte.");
        var index = new LoreIndexDefinition(
            Guid.Parse("b1230000-0000-4000-8000-000000000001"),
            1,
            true,
            2_000,
            [entry],
            "test:g12-b");

        var retrieval = LoreRetrievalIndex.Build(index, project);
        var hits = retrieval.Query("crypte braise");
        var packet = LoreContextAssembler.Assemble(index, project, "crypte braise");

        Assert.Single(hits);
        Assert.Equal(entry.Id, hits[0].EntryId);
        Assert.Contains($"project:{project.Id}", hits[0].SourceIds);
        Assert.Equal(1, packet.EntryCount);
        Assert.Equal(64, packet.ContentSha256.Length);
        Assert.Contains("Serment de braise", packet.Content, StringComparison.Ordinal);
        Assert.False(packet.Truncated);
    }

    [Fact]
    public void UnresolvedProjectSourceHasStableDiagnostic()
    {
        var project = Project();
        var entry = new LoreEntryDefinition(
            Guid.NewGuid(),
            1,
            LoreEntryKind.Character,
            "elyra",
            "Elyra",
            "Gardienne de la Crypte.",
            ["dialogue:00000000-0000-4000-8000-000000000099"],
            [],
            ["character"],
            "test:g12-b");
        var index = new LoreIndexDefinition(Guid.NewGuid(), 1, true, 2_000, [entry], "test:g12-b");

        var diagnostics = LoreIndexProjectValidator.Validate(index, project);

        Assert.Contains(diagnostics, item => item.Code == LoreIndexProjectValidator.SourceUnresolvedCode);
    }

    [Fact]
    public void CommandRequiresExactDryRunTransactionAndSupportsHistory()
    {
        var project = Project();
        var history = new LoreIndexCommandHistory();
        var handler = new SetLoreIndexHandler(new FixedClock(Timestamp));
        var planId = Guid.Parse("b1230000-0000-4000-8000-000000000020");
        var transactionId = Guid.Parse("b1230000-0000-4000-8000-000000000021");
        var configured = new LoreIndexDefinition(
            history.State.Index.Id,
            1,
            true,
            4_000,
            [Entry(project, "serment-braise", "Le Serment de braise est la loi ancienne.")],
            "test:g12-b");
        SetLoreIndexCommand Command(bool dryRun, LoreIndexDefinition? candidate = null) => new(
            CommandId.From(Guid.NewGuid()),
            transactionId,
            planId,
            history.State.Revision,
            candidate ?? configured,
            AssistantPermissionLevel.Configure,
            dryRun);

        var refused = handler.Handle(history.State, Command(dryRun: false), project);
        var preview = handler.Handle(history.State, Command(dryRun: true), project);
        var changed = new LoreIndexDefinition(configured.Id, 1, true, 4_001, configured.Entries, "test:g12-b");
        var mismatch = handler.Handle(history.State, Command(dryRun: false, changed), project);
        var applied = handler.Handle(history.State, Command(dryRun: false), project);

        Assert.False(refused.Succeeded);
        Assert.Equal(SetLoreIndexHandler.PlanNotPreviewedCode, refused.Diagnostics[0].Code);
        Assert.True(preview.Succeeded);
        Assert.False(mismatch.Succeeded);
        Assert.True(applied.Succeeded);
        Assert.Equal(transactionId, applied.TransactionId);
        Assert.True(applied.Audit.Applied);
        history.Record(applied);
        Assert.Single(history.State.Index.Entries);
        Assert.True(history.Undo());
        Assert.Empty(history.State.Index.Entries);
        Assert.True(history.Redo());
        Assert.Single(history.State.Index.Entries);
    }

    private static ProjectDefinition Project()
    {
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("b1230000-0000-4000-8000-000000000030")),
            "crypte",
            "Crypte des Cendres",
            []);
        return new ProjectDefinition(
            ProjectId.From(Guid.Parse("b1230000-0000-4000-8000-000000000031")),
            "Chroniques de la Braise",
            [],
            [area]);
    }

    private static LoreEntryDefinition Entry(ProjectDefinition project, string key, string content) =>
        new(
            Guid.Parse("b1230000-0000-4000-8000-000000000040"),
            1,
            LoreEntryKind.WorldBible,
            key,
            "Serment de braise",
            content,
            [$"project:{project.Id}"],
            [],
            ["braise", "canon"],
            "test:g12-b");
}
