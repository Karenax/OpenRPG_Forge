using Forge.Application.Commands;
using Forge.Application.Creator;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Creator;

public sealed class CreatorSearchAndRenameTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 8, 2, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SearchIndex_FindsCanonicalContentDiagnosticsAndStructuredTags()
    {
        var context = CreateContext();
        var diagnostic = CreatorDiagnostic.Create(
            CreatorDiagnosticSeverity.Warning,
            "area.portal.review",
            "Vérifier le passage secret d'Elyra.",
            $"areas/{context.SecondAreaId}/portals/review");
        var index = CreatorSearchIndex.Build(context.State.Project, [diagnostic]);

        var accentInsensitive = index.Search(new CreatorSearchQuery("eclaireuse elyra"));
        var taggedEntities = index.Search(new CreatorSearchQuery("", CreatorContentKind.Entity, "champion"));
        var diagnostics = index.Search(new CreatorSearchQuery("passage secret", CreatorContentKind.Diagnostic));

        Assert.Contains(accentInsensitive, result => result.Document.Kind == CreatorContentKind.Blueprint);
        Assert.Contains(accentInsensitive, result => result.Document.Kind == CreatorContentKind.Entity);
        Assert.Single(taggedEntities);
        Assert.Equal(context.FirstEntityId.ToString(), taggedEntities[0].Document.Id);
        Assert.Equal(diagnostic.Id, Assert.Single(diagnostics).Document.Id);
        Assert.All(accentInsensitive, result => Assert.True(result.Score > 0));
    }

    [Fact]
    public void ReferenceIndex_ReportsInverseUsesAcrossCreatorAggregates()
    {
        var context = CreateContext();
        var project = context.State.Project;
        var index = CreatorReferenceIndex.Build(project);
        var dialogue = Assert.Single(project.Dialogues);
        var quest = Assert.Single(project.Quests);

        var blueprintReferences = index.Find(CreatorContentKind.Blueprint, context.BlueprintId.ToString());
        var entryAreaReferences = index.Find(CreatorContentKind.Area, context.FirstAreaId.ToString());
        var destinationEntityReferences = index.Find(CreatorContentKind.Entity, context.SecondEntityId.ToString());
        var dialogueTargetReferences = index.Find(
            CreatorContentKind.DialogueNode,
            dialogue.Nodes[1].Id.ToString());
        var terminalStateReferences = index.Find(
            CreatorContentKind.QuestState,
            quest.States[1].Id.ToString());

        Assert.Equal(4, blueprintReferences.Count);
        Assert.Contains(blueprintReferences, reference => reference.Usage == CreatorReferenceUsageKind.EntityBlueprint);
        Assert.Contains(blueprintReferences, reference => reference.Usage == CreatorReferenceUsageKind.DialogueSpeakerBlueprint);
        Assert.Contains(entryAreaReferences, reference => reference.Usage == CreatorReferenceUsageKind.ModuleEntryArea);
        Assert.Contains(destinationEntityReferences, reference => reference.Usage == CreatorReferenceUsageKind.PortalDestinationEntity);
        Assert.Contains(dialogueTargetReferences, reference => reference.Usage == CreatorReferenceUsageKind.DialogueChoiceTargetNode);
        Assert.Contains(terminalStateReferences, reference => reference.Usage == CreatorReferenceUsageKind.QuestTransitionToState);
    }

    [Fact]
    public void SafeRename_PreviewsThenRenamesBlueprintWithoutChangingReferencesAndSupportsHistory()
    {
        var context = CreateContext();
        var handler = new SafeRenameHandler(new FixedClock(Timestamp));
        var command = CreateCommand(
            context.State,
            CreatorContentKind.Blueprint,
            context.BlueprintId.ToString(),
            "Gardienne Elyra",
            "gardienne-elyra");

        var preview = SafeRenamePreview.Create(
            context.State.Project,
            command.TargetKind,
            command.TargetId,
            command.DisplayName,
            command.Slug);
        var dryRun = handler.Handle(context.State, command with { DryRun = true });
        var applied = handler.Handle(context.State, command);
        var history = new ProjectCommandHistory(context.State);
        history.Record(applied);

        Assert.NotNull(preview);
        Assert.Equal(4, preview.PreservedReferenceCount);
        Assert.True(dryRun.Succeeded);
        Assert.Same(context.State, dryRun.State);
        Assert.False(dryRun.Audit.Applied);
        var renamed = Assert.Single(applied.State.Project.Blueprints, blueprint => blueprint.Id == context.BlueprintId);
        Assert.Equal("gardienne-elyra", renamed.Slug);
        Assert.Equal("Gardienne Elyra", renamed.DisplayName);
        Assert.Equal(context.BlueprintId, renamed.Id);
        Assert.Equal(4, CreatorReferenceIndex.Build(applied.State.Project)
            .Find(CreatorContentKind.Blueprint, context.BlueprintId.ToString()).Count);
        Assert.Contains("preserving 4 reference(s)", applied.Patch!.Summary, StringComparison.Ordinal);
        Assert.True(history.Undo().Succeeded);
        Assert.Equal("Éclaireuse Elyra", history.State.Project.Blueprints.Single().DisplayName);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal("Gardienne Elyra", history.State.Project.Blueprints.Single().DisplayName);
    }

    [Fact]
    public void SafeRename_RejectsRevisionUnsupportedTargetDuplicateAndInvalidSlugs()
    {
        var context = CreateContext();
        var handler = new SafeRenameHandler(new FixedClock(Timestamp));
        var areaCommand = CreateCommand(
            context.State,
            CreatorContentKind.Area,
            context.SecondAreaId.ToString(),
            "Citadelle",
            "citadelle");

        var revision = handler.Handle(context.State, areaCommand with
        {
            BaseRevision = context.State.Revision.Next(),
        });
        var unsupported = handler.Handle(context.State, areaCommand with
        {
            TargetKind = CreatorContentKind.Entity,
            TargetId = context.FirstEntityId.ToString(),
        });
        var duplicate = handler.Handle(context.State, areaCommand with { Slug = "clairiere" });
        var invalid = handler.Handle(context.State, areaCommand with { Slug = "Citadelle Haute" });

        Assert.Equal(SafeRenameHandler.RevisionConflictCode, Assert.Single(revision.Diagnostics).Code);
        Assert.Equal(SafeRenameHandler.UnsupportedTargetCode, Assert.Single(unsupported.Diagnostics).Code);
        Assert.Equal(SafeRenameHandler.DuplicateSlugCode, Assert.Single(duplicate.Diagnostics).Code);
        Assert.Equal(SafeRenameHandler.InvalidSlugCode, Assert.Single(invalid.Diagnostics).Code);
        Assert.All([revision, unsupported, duplicate, invalid], result => Assert.Same(context.State, result.State));
    }

    [Theory]
    [InlineData(CreatorContentKind.Project, "Projet Horizon", null)]
    [InlineData(CreatorContentKind.Module, "Module Horizon", "module-horizon")]
    [InlineData(CreatorContentKind.Area, "Clairière Haute", "clairiere-haute")]
    [InlineData(CreatorContentKind.Dialogue, "Conseil d'Elyra", "conseil-elyra")]
    [InlineData(CreatorContentKind.Quest, "Serment d'Elyra", "serment-elyra")]
    public void SafeRename_SupportsEveryAggregateRoot(
        CreatorContentKind kind,
        string displayName,
        string? slug)
    {
        var context = CreateContext();
        var project = context.State.Project;
        var targetId = kind switch
        {
            CreatorContentKind.Project => project.Id.ToString(),
            CreatorContentKind.Module => project.Module.Id.ToString(),
            CreatorContentKind.Area => context.FirstAreaId.ToString(),
            CreatorContentKind.Dialogue => project.Dialogues.Single().Id.ToString(),
            CreatorContentKind.Quest => project.Quests.Single().Id.ToString(),
            _ => throw new InvalidOperationException(),
        };
        var result = new SafeRenameHandler(new FixedClock(Timestamp)).Handle(
            context.State,
            CreateCommand(context.State, kind, targetId, displayName, slug));

        Assert.True(result.Succeeded);
        var document = Assert.Single(
            CreatorSearchIndex.Build(result.State.Project).Search(
                new CreatorSearchQuery(displayName, kind)));
        Assert.Equal(targetId, document.Document.Id);
        Assert.Empty(Forge.Domain.Validation.ProjectValidator.Validate(result.State.Project));
    }

    private static SafeRenameCommand CreateCommand(
        ProjectState state,
        CreatorContentKind kind,
        string targetId,
        string displayName,
        string? slug) =>
        new(
            CommandId.From(Guid.Parse("ab000000-0000-4000-8000-000000000001")),
            state.Revision,
            kind,
            targetId,
            displayName,
            slug,
            DryRun: false);

    private static SearchTestContext CreateContext()
    {
        var blueprintId = BlueprintId.From(Guid.Parse("ab100000-0000-4000-8000-000000000001"));
        var blueprint = new BlueprintDefinition(
            blueprintId,
            "eclaireuse-elyra",
            "Éclaireuse Elyra",
            BlueprintKind.Creature);
        var firstAreaId = AreaId.From(Guid.Parse("ab200000-0000-4000-8000-000000000001"));
        var secondAreaId = AreaId.From(Guid.Parse("ab200000-0000-4000-8000-000000000002"));
        var firstEntityId = EntityId.From(Guid.Parse("ab300000-0000-4000-8000-000000000001"));
        var secondEntityId = EntityId.From(Guid.Parse("ab300000-0000-4000-8000-000000000002"));
        var firstEntity = new EntityInstance(
            firstEntityId,
            blueprintId,
            new TransformValue(Vector3Value.Zero, QuaternionValue.Identity, Vector3Value.One),
            ["champion", "elyra"]);
        var secondEntity = new EntityInstance(
            secondEntityId,
            blueprintId,
            new TransformValue(Vector3Value.Zero, QuaternionValue.Identity, Vector3Value.One));
        var portal = new PortalLink(
            PortalId.From(Guid.Parse("ab400000-0000-4000-8000-000000000001")),
            "Passage d'Elyra",
            firstEntityId,
            secondAreaId,
            secondEntityId,
            bidirectional: false,
            preloadDestination: true);
        var firstArea = new AreaDefinition(
            firstAreaId,
            "clairiere",
            "Clairière",
            [firstEntity],
            portals: [portal]);
        var secondArea = new AreaDefinition(
            secondAreaId,
            "citadelle",
            "Citadelle",
            [secondEntity]);

        var firstNodeId = DialogueNodeId.From(Guid.Parse("ab500000-0000-4000-8000-000000000001"));
        var secondNodeId = DialogueNodeId.From(Guid.Parse("ab500000-0000-4000-8000-000000000002"));
        var choice = new DialogueChoice(
            DialogueChoiceId.From(Guid.Parse("ab600000-0000-4000-8000-000000000001")),
            new LocalizedText("dialogue.elyra.choice", "Suivre Elyra"),
            targetNodeId: secondNodeId);
        var dialogue = new DialogueDefinition(
            DialogueId.From(Guid.Parse("ab700000-0000-4000-8000-000000000001")),
            "rencontre-elyra",
            "Rencontre avec Elyra",
            firstNodeId,
            [
                new DialogueNode(
                    firstNodeId,
                    blueprintId,
                    new LocalizedText("dialogue.elyra.intro", "Bienvenue, voyageur."),
                    choices: [choice]),
                new DialogueNode(
                    secondNodeId,
                    blueprintId,
                    new LocalizedText("dialogue.elyra.end", "La route est ouverte.")),
            ]);

        var initialStateId = QuestStateId.From(Guid.Parse("ab800000-0000-4000-8000-000000000001"));
        var terminalStateId = QuestStateId.From(Guid.Parse("ab800000-0000-4000-8000-000000000002"));
        var quest = new QuestDefinition(
            QuestId.From(Guid.Parse("ab900000-0000-4000-8000-000000000001")),
            "serment-elyra",
            "Le serment d'Elyra",
            initialStateId,
            [
                new QuestStateDefinition(
                    initialStateId,
                    "En cours",
                    new QuestJournalEntry("quest.elyra.active", "Rejoindre Elyra.")),
                new QuestStateDefinition(
                    terminalStateId,
                    "Accomplie",
                    new QuestJournalEntry("quest.elyra.done", "Elyra est rejointe."),
                    isTerminal: true),
            ],
            [new QuestTransition(
                QuestTransitionId.From(Guid.Parse("aba00000-0000-4000-8000-000000000001")),
                initialStateId,
                terminalStateId)]);

        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("abb00000-0000-4000-8000-000000000001")),
            "Projet Elyra",
            [blueprint],
            [firstArea, secondArea],
            [dialogue],
            [quest]);
        return new SearchTestContext(
            new ProjectState(project, new ProjectRevision(12)),
            blueprintId,
            firstAreaId,
            secondAreaId,
            firstEntityId,
            secondEntityId);
    }

    private sealed record SearchTestContext(
        ProjectState State,
        BlueprintId BlueprintId,
        AreaId FirstAreaId,
        AreaId SecondAreaId,
        EntityId FirstEntityId,
        EntityId SecondEntityId);
}
