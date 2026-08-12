using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Creator;
using Forge.Application.Tests.Entities;
using Forge.Domain.Blueprints;
using Forge.Domain.Authoring;

namespace Forge.Application.Tests.Blueprints;

public sealed class BlueprintCatalogTests
{
    private static readonly BlueprintId NewBlueprintId = BlueprintId.From(Guid.Parse("a0000000-0000-4000-8000-000000000001"));

    [Fact]
    public void UpsertDryRunProducesSemanticPatchWithoutMutation()
    {
        var context = new EntityMutationTestContext();
        var handler = new UpsertBlueprintHandler(new FixedClock(EntityMutationTestContext.Timestamp));

        var result = handler.Handle(context.State, CreateUpsert(context, dryRun: true));

        Assert.True(result.Succeeded);
        Assert.False(result.Audit.Applied);
        Assert.Null(result.NewRevision);
        Assert.Same(context.State, result.State);
        Assert.Equal("add", result.Patch!.Operation);
        Assert.Equal($"blueprints/{NewBlueprintId}", result.Patch.Path);
    }

    [Fact]
    public void UpsertCreatesEditableBlueprintAndHistoryRestoresProject()
    {
        var context = new EntityMutationTestContext();
        var handler = new UpsertBlueprintHandler(new FixedClock(EntityMutationTestContext.Timestamp));
        var history = new ProjectCommandHistory(context.State);

        var result = handler.Handle(history.State, CreateUpsert(context));
        history.Record(result);

        var created = Assert.Single(history.State.Project.Blueprints, blueprint => blueprint.Id == NewBlueprintId);
        Assert.Equal("broken-pillar", created.Slug);
        Assert.Equal(BlueprintKind.Placeable, created.Kind);
        Assert.True(history.Undo().Succeeded);
        Assert.DoesNotContain(history.State.Project.Blueprints, blueprint => blueprint.Id == NewBlueprintId);
        Assert.True(history.Redo().Succeeded);
        Assert.Contains(history.State.Project.Blueprints, blueprint => blueprint.Id == NewBlueprintId);
    }

    [Fact]
    public void UpsertRejectsDuplicateAndNonCanonicalSlugs()
    {
        var context = new EntityMutationTestContext();
        var handler = new UpsertBlueprintHandler(new FixedClock(EntityMutationTestContext.Timestamp));
        var duplicate = CreateUpsert(context) with { Slug = "ash-pillar" };
        var invalid = CreateUpsert(context) with { Slug = "Broken Pillar" };

        Assert.Equal(UpsertBlueprintHandler.DuplicateSlugCode, Assert.Single(handler.Handle(context.State, duplicate).Diagnostics).Code);
        Assert.Equal(UpsertBlueprintHandler.InvalidSlugCode, Assert.Single(handler.Handle(context.State, invalid).Diagnostics).Code);
    }

    [Fact]
    public void CreateFromEntityClonesDefinitionAndRetargetsOnlySelectedInstance()
    {
        var context = new EntityMutationTestContext();
        var handler = new CreateBlueprintFromEntityHandler(new FixedClock(EntityMutationTestContext.Timestamp));
        var command = CreateFromEntity(context);

        var result = handler.Handle(context.State, command);

        Assert.True(result.Succeeded);
        var derived = Assert.Single(result.State.Project.Blueprints, blueprint => blueprint.Id == NewBlueprintId);
        Assert.Equal(BlueprintKind.Placeable, derived.Kind);
        var entity = Assert.Single(result.State.Project.Areas.Single().Entities);
        Assert.Equal(NewBlueprintId, entity.BlueprintId);
        Assert.Equal(context.State.Project.Areas.Single().Entities.Single().Overrides, entity.Overrides);
        Assert.Equal(2, result.State.Project.Blueprints.Count);
        Assert.Equal("add", result.Patch!.Operation);
    }

    [Fact]
    public void CreateFromEntitySupportsDryRunAndRejectsUnknownEntity()
    {
        var context = new EntityMutationTestContext();
        var handler = new CreateBlueprintFromEntityHandler(new FixedClock(EntityMutationTestContext.Timestamp));
        var dryRun = CreateFromEntity(context) with { DryRun = true };
        var missing = CreateFromEntity(context) with
        {
            EntityId = Forge.Domain.Entities.EntityId.From(Guid.Parse("b0000000-0000-4000-8000-000000000001")),
        };

        var preview = handler.Handle(context.State, dryRun);
        Assert.True(preview.Succeeded);
        Assert.Same(context.State, preview.State);
        Assert.False(preview.Audit.Applied);
        Assert.Equal(CreateBlueprintFromEntityHandler.MissingEntityCode, Assert.Single(handler.Handle(context.State, missing).Diagnostics).Code);
    }

    [Fact]
    public void CreateFromEntityPersistsAuthoringOptionsAndPinsRetargetedInstance()
    {
        var context = new EntityMutationTestContext();
        var handler = new CreateBlueprintFromEntityHandler(new FixedClock(EntityMutationTestContext.Timestamp));
        var authoring = new BlueprintAuthoringMetadata(
            1,
            "2.1.0",
            "project.crypt.pillar",
            $"content/blueprints/{NewBlueprintId}/broken-pillar.blueprint.json",
            ["scale", "tint"],
            ["pack://forge-core/1.0.0/material-stone"],
            $"content/blueprints/{NewBlueprintId}/broken-pillar.preview.png",
            131_072);

        var result = handler.Handle(context.State, CreateFromEntity(context) with { Authoring = authoring });

        Assert.True(result.Succeeded);
        Assert.Equal(authoring, result.State.Project.Blueprints.Single(item => item.Id == NewBlueprintId).Authoring);
        Assert.Equal("=2.1.0", result.State.Project.Areas.Single().Entities.Single().BlueprintVersionRequirement);
    }

    [Fact]
    public void ReferenceIndexRebuildsEntityReferencesFromCanonicalProject()
    {
        var context = new EntityMutationTestContext();

        var references = BlueprintReferenceIndex.Build(context.State.Project).Find(context.BlueprintId);

        var reference = Assert.Single(references);
        Assert.Equal(BlueprintReferenceKind.EntityInstance, reference.Kind);
        Assert.Equal(context.EntityId.ToString(), reference.SourceId);
        Assert.Equal($"areas/{context.AreaId}/entities/{context.EntityId}/blueprintId", reference.DocumentPath);
        Assert.Empty(BlueprintReferenceIndex.Build(context.State.Project).Find(NewBlueprintId));
    }

    [Fact]
    public void PaletteCatalogSeparatesOriginsFavoritesAndProvenanceSearch()
    {
        var context = new EntityMutationTestContext();
        var handler = new ConfigureBlueprintPaletteHandler(new FixedClock(EntityMutationTestContext.Timestamp));
        var provenance = GeneratedProvenance();
        var command = new ConfigureBlueprintPaletteCommand(
            CommandId.From(Guid.Parse("a3000000-0000-4000-8000-000000000001")),
            context.State.Revision,
            context.BlueprintId,
            BlueprintPaletteOrigin.Generated,
            true,
            provenance,
            DryRun: false);

        var result = handler.Handle(context.State, command);
        var catalog = BlueprintPaletteCatalog.Build(result.State.Project);

        Assert.True(result.Succeeded);
        Assert.Single(catalog.Filter(BlueprintPaletteView.Generated));
        Assert.Single(catalog.Filter(BlueprintPaletteView.Favorites));
        Assert.Empty(catalog.Filter(BlueprintPaletteView.ProjectContent));
        var searched = Assert.Single(catalog.Filter(BlueprintPaletteView.All, "CC0"));
        Assert.Equal("generator://creator/smoke", searched.Provenance.Source);
        Assert.False(searched.IsReadOnly);
    }

    [Fact]
    public void PaletteCatalogCombinesPersonalFavoritesAndOrdersRecents()
    {
        var context = new EntityMutationTestContext();
        var profile = EditorWorkspaceProfile.CreateDefault(Forge.Domain.Authoring.EditorRole.Artist);
        profile = EditorWorkspaceProfilePlanner.TogglePersonalFavorite(profile, context.BlueprintId);
        profile = EditorWorkspaceProfilePlanner.RecordRecent(profile, context.BlueprintId);

        var catalog = BlueprintPaletteCatalog.Build(context.State.Project, profile);

        Assert.Equal(context.BlueprintId, Assert.Single(catalog.Filter(BlueprintPaletteView.Favorites)).BlueprintId);
        Assert.Equal(context.BlueprintId, Assert.Single(catalog.Filter(BlueprintPaletteView.Recent)).BlueprintId);
    }

    [Fact]
    public void NamedPaletteCollectionsRemainOrderedBoundedAndPersistInWorkspaceProfile()
    {
        var context = new EntityMutationTestContext();
        var profile = EditorWorkspaceProfile.CreateDefault(EditorRole.Artist);
        var collection = new NamedPaletteCollection(
            Guid.Parse("a3100000-0000-4000-8000-000000000001"),
            1,
            "Décors de crypte",
            [context.BlueprintId],
            taskScoped: true,
            "test://palette/crypt");

        profile = EditorWorkspaceProfilePlanner.UpsertNamedPalette(profile, collection);
        var selected = BlueprintPaletteCatalog.Build(context.State.Project, profile)
            .FilterNamedCollection(profile.NamedPaletteCollections.Single());

        Assert.Equal(context.BlueprintId, Assert.Single(selected).BlueprintId);
        Assert.True(profile.NamedPaletteCollections.Single().TaskScoped);
        Assert.Empty(EditorWorkspaceProfilePlanner.RemoveNamedPalette(profile, collection.Id).NamedPaletteCollections);
    }

    [Fact]
    public void ContextualPaletteCombinesTypeKitStyleLicenseCompatibilityTagsAndFavorites()
    {
        var context = new EntityMutationTestContext();
        var original = context.State.Project.Blueprints.Single();
        var authoring = new BlueprintAuthoringMetadata(
            1, "1.0.0", "project.crypt", $"content/blueprints/{original.Id}/crypt.json",
            kit: "crypt-stone", styles: ["gothic", "ruined"],
            compatibility: ["medieval", "navigation-v1"], tags: ["dungeon", "stone"]);
        var candidate = new BlueprintDefinition(
            original.Id, original.Slug, original.DisplayName, original.Kind, original.Combat,
            original.PaletteOrigin, true,
            new BlueprintProvenance(1, "project://crypt", "Forge", "CC0-1.0", new string('c', 64)),
            original.CreaturePresentation, original.CreatureProgression, original.CreatureLoot,
            original.CreatureRoleBehavior, original.CreatureEvents, original.ItemAdvanced, authoring);
        var source = context.State.Project;
        var project = new Forge.Domain.Projects.ProjectDefinition(
            source.Id, source.Title, source.Module, [candidate], source.Areas, source.Dialogues, source.Quests);
        var filter = new EditorPaletteFilter(
            [original.Kind], ["crypt"], ["gothic", "ruined"], ["cc0"],
            ["medieval"], ["dungeon", "stone"], true);

        var item = Assert.Single(BlueprintPaletteCatalog.Build(project).Filter(BlueprintPaletteView.All, contextFilter: filter));

        Assert.Equal("crypt-stone", item.Kit);
        Assert.Empty(BlueprintPaletteCatalog.Build(project).Filter(
            BlueprintPaletteView.All, contextFilter: new EditorPaletteFilter([BlueprintKind.Item])));
        Assert.Empty(BlueprintPaletteCatalog.Build(project).Filter(
            BlueprintPaletteView.All, contextFilter: new EditorPaletteFilter(tags: ["forest"])));
    }

    [Fact]
    public void PaletteConfigurationSupportsDryRunUndoAndRedo()
    {
        var context = new EntityMutationTestContext();
        var handler = new ConfigureBlueprintPaletteHandler(new FixedClock(EntityMutationTestContext.Timestamp));
        var command = new ConfigureBlueprintPaletteCommand(
            CommandId.From(Guid.Parse("a3000000-0000-4000-8000-000000000002")),
            context.State.Revision,
            context.BlueprintId,
            BlueprintPaletteOrigin.Temporary,
            false,
            GeneratedProvenance(),
            DryRun: false);

        var preview = handler.Handle(context.State, command with { DryRun = true });
        var history = new ProjectCommandHistory(context.State);
        history.Record(handler.Handle(history.State, command));

        Assert.True(preview.Succeeded);
        Assert.Same(context.State, preview.State);
        Assert.Equal("replace", preview.Patch!.Operation);
        Assert.Equal(BlueprintPaletteOrigin.Temporary, history.State.Project.Blueprints.Single().PaletteOrigin);
        Assert.True(history.Undo().Succeeded);
        Assert.Equal(BlueprintPaletteOrigin.ProjectContent, history.State.Project.Blueprints.Single().PaletteOrigin);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(BlueprintPaletteOrigin.Temporary, history.State.Project.Blueprints.Single().PaletteOrigin);
    }

    [Fact]
    public void InstalledPackRequiresTrustedProvenanceAndBecomesReadOnly()
    {
        var context = new EntityMutationTestContext();
        var paletteHandler = new ConfigureBlueprintPaletteHandler(new FixedClock(EntityMutationTestContext.Timestamp));
        var invalid = new ConfigureBlueprintPaletteCommand(
            CommandId.From(Guid.Parse("a3000000-0000-4000-8000-000000000003")),
            context.State.Revision,
            context.BlueprintId,
            BlueprintPaletteOrigin.InstalledPack,
            false,
            GeneratedProvenance(),
            DryRun: false);
        var trusted = invalid with
        {
            Provenance = new BlueprintProvenance(
                BlueprintProvenance.CurrentSchemaVersion,
                "pack://forge-core/1.0.0/ash-pillar",
                "OpenRPG Forge",
                "CC0-1.0",
                new string('a', 64)),
        };

        var rejected = paletteHandler.Handle(context.State, invalid);
        var configured = paletteHandler.Handle(context.State, trusted);
        var rename = new UpsertBlueprintCommand(
            CommandId.From(Guid.Parse("a3000000-0000-4000-8000-000000000004")),
            configured.State.Revision,
            context.BlueprintId,
            "renamed-installed",
            "Renamed installed",
            BlueprintKind.Placeable,
            DryRun: false);
        var edit = new UpsertBlueprintHandler(new FixedClock(EntityMutationTestContext.Timestamp))
            .Handle(configured.State, rename);

        Assert.Equal(
            ConfigureBlueprintPaletteHandler.InstalledSourceCode,
            Assert.Single(rejected.Diagnostics).Code);
        Assert.True(configured.Succeeded);
        Assert.True(configured.State.Project.Blueprints.Single().IsReadOnly());
        Assert.Equal(UpsertBlueprintHandler.ReadOnlyCode, Assert.Single(edit.Diagnostics).Code);
    }

    private static UpsertBlueprintCommand CreateUpsert(EntityMutationTestContext context, bool dryRun = false) =>
        new(
            CommandId.From(Guid.Parse("a1000000-0000-4000-8000-000000000001")),
            context.State.Revision,
            NewBlueprintId,
            "broken-pillar",
            "Pilier brisé",
            BlueprintKind.Placeable,
            dryRun);

    private static CreateBlueprintFromEntityCommand CreateFromEntity(EntityMutationTestContext context) =>
        new(
            CommandId.From(Guid.Parse("a2000000-0000-4000-8000-000000000001")),
            context.State.Revision,
            context.AreaId,
            context.EntityId,
            NewBlueprintId,
            "broken-pillar",
            "Pilier brisé",
            DryRun: false);

    private static BlueprintProvenance GeneratedProvenance() =>
        new(
            BlueprintProvenance.CurrentSchemaVersion,
            "generator://creator/smoke",
            "OpenRPG Forge",
            "CC0-1.0",
            new string('b', 64),
            isAiGenerated: true,
            ["Generated from the author prompt"]);
}
