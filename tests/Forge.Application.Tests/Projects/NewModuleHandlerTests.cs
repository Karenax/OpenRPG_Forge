using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Modules;
using Forge.Domain.Projects;

namespace Forge.Application.Tests.Projects;

public sealed class NewModuleHandlerTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 8, 2, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Handle_CreatesAPlayableModuleAggregate()
    {
        var handler = new NewModuleHandler(new FixedClock(Timestamp));
        var command = CreateCommand();

        var result = handler.Handle(command);

        Assert.True(result.Succeeded);
        Assert.True(result.Audit.Applied);
        Assert.Equal(Timestamp, result.Audit.Timestamp);
        var project = Assert.IsType<ProjectState>(result.State).Project;
        Assert.Equal("les-iles-brisees", project.Module.Slug);
        Assert.Equal("archipel-oublie", project.Areas.Single().Slug);
        Assert.Equal(project.Areas.Single().Id, project.Module.EntryAreaId);
        Assert.Equal(2, project.Blueprints.Count);
        Assert.Contains(project.Blueprints, blueprint => blueprint.Kind == BlueprintKind.PlayerSpawn);
        Assert.Contains(project.Areas.Single().Entities, entity => entity.Tags.Contains("floor", StringComparer.Ordinal));
        Assert.Contains(project.Areas.Single().Entities, entity => entity.Tags.Contains("player", StringComparer.Ordinal));
        var foundation = Assert.IsType<ProjectFoundationProfile>(project.Module.Metadata.Foundation);
        Assert.Equal(ProjectTemplateKind.Rpg, foundation.Template);
        Assert.Equal(project.Id, foundation.Identity.ProjectId);
        Assert.Equal(project.Module.Slug, foundation.Identity.Slug);
        Assert.Equal("module.les-iles-brisees", foundation.Identity.PackageNamespace);
        Assert.Equal(project.Areas.Single().Id, Assert.Single(foundation.PlayerStarts).AreaId);
    }

    [Fact]
    public void Handle_DryRunBuildsThePreviewWithoutApplyingTheAudit()
    {
        var handler = new NewModuleHandler(new FixedClock(Timestamp));

        var result = handler.Handle(CreateCommand(dryRun: true));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.State);
        Assert.False(result.Audit.Applied);
        Assert.Equal("add", result.Patch?.Operation);
        Assert.Equal("remove", result.Inverse?.Operation);
    }

    [Fact]
    public void Handle_CreatesAnExteriorAreaWithRequestedDimensions()
    {
        var handler = new NewModuleHandler(new FixedClock(Timestamp));
        var command = CreateCommand() with
        {
            AreaKind = AreaKind.Exterior,
            Dimensions = new AreaDimensions(128, 96),
        };

        var result = handler.Handle(command);

        Assert.True(result.Succeeded);
        var area = Assert.IsType<ProjectState>(result.State).Project.Areas.Single();
        Assert.Equal(AreaKind.Exterior, area.Kind);
        Assert.Equal(new AreaDimensions(128, 96), area.Dimensions);
        Assert.True(area.World.Terrain.Enabled);
    }

    [Fact]
    public void Handle_RejectsAnEmptyTitle()
    {
        var handler = new NewModuleHandler(new FixedClock(Timestamp));

        var result = handler.Handle(CreateCommand(title: " "));

        Assert.False(result.Succeeded);
        Assert.Equal(NewModuleHandler.TitleRequiredCode, Assert.Single(result.Diagnostics).Code);
        Assert.Null(result.State);
    }

    [Fact]
    public void Handle_RejectsANonZeroInitialRevision()
    {
        var handler = new NewModuleHandler(new FixedClock(Timestamp));

        var result = handler.Handle(CreateCommand(revision: new ProjectRevision(2)));

        Assert.False(result.Succeeded);
        Assert.Equal(NewModuleHandler.RevisionConflictCode, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Handle_CreatesAValidatedCoreMenuWithoutAreaOrPlayerStart()
    {
        var handler = new NewModuleHandler(new FixedClock(Timestamp));
        var command = CreateCommand() with
        {
            Template = ProjectTemplateKind.CoreMenu,
            CreateInitialArea = false,
            AreaDisplayName = string.Empty,
            Kits = ["forge-core"],
            RulesProvider = "forge.core",
            PackageNamespace = "studio.menu",
        };

        var result = handler.Handle(command);

        Assert.True(result.Succeeded, string.Join(" | ", result.Diagnostics.Select(item => item.Message)));
        var project = Assert.IsType<ProjectState>(result.State).Project;
        Assert.Empty(project.Areas);
        Assert.Empty(project.Blueprints);
        var foundation = Assert.IsType<ProjectFoundationProfile>(project.Module.Metadata.Foundation);
        Assert.False(foundation.RequiresInitialArea);
        Assert.Empty(foundation.PlayerStarts);
        Assert.Empty(Forge.Domain.Validation.ProjectValidator.Validate(project));
    }

    [Fact]
    public void Handle_RejectsInvalidFoundationBeforeCreationWithLocalizedPath()
    {
        var handler = new NewModuleHandler(new FixedClock(Timestamp));

        var result = handler.Handle(CreateCommand() with { PackageNamespace = "bad namespace" });

        Assert.False(result.Succeeded);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ProjectFoundationValidator.IdentityCode, diagnostic.Code);
        Assert.Contains("Path: module/metadata/foundation", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Handle_CreatesLocalizedMetadataAndAnOrientedPlayerStartInOnePreviewableCommand()
    {
        var handler = new NewModuleHandler(new FixedClock(Timestamp));
        var metadata = new NewModuleMetadataInput(
            "fr",
            [
                new ProjectLocalizedMetadata(
                    "fr", "Les Îles Brisées", "Aventure maritime", "Un archipel à reconstruire.",
                    "Atelier Forge", rules: "Règles génériques", controls: "Clavier ou manette"),
                new ProjectLocalizedMetadata(
                    "en", "The Shattered Isles", "A sea adventure", "Rebuild a forgotten archipelago.",
                    "Forge Workshop"),
            ],
            "Atelier Forge",
            "1.2.0",
            "CC-BY-4.0",
            "assets/project-cover.png",
            ["aventure", "coopération"],
            "Tout public");
        var command = CreateCommand(dryRun: true) with
        {
            Metadata = metadata,
            PlayerOrientationDegrees = 135,
        };

        var preview = handler.Handle(command);
        var applied = handler.Handle(command with { DryRun = false });

        Assert.True(preview.Succeeded);
        Assert.False(preview.Audit.Applied);
        Assert.True(applied.Succeeded);
        var project = Assert.IsType<ProjectState>(applied.State).Project;
        Assert.Equal("fr", project.Module.Metadata.DefaultLocale);
        Assert.Equal(2, project.Module.Metadata.Localizations.Count);
        Assert.Equal("The Shattered Isles", project.Module.Metadata.Localizations.Single(item => item.Locale == "en").Name);
        Assert.Equal("Atelier Forge", project.Module.Metadata.Author);
        Assert.Equal("1.2.0", project.Module.Metadata.Version);
        Assert.Equal(135, Assert.Single(project.Module.Metadata.Foundation!.PlayerStarts).OrientationDegrees);
        Assert.Equal(project.Id, project.Module.Metadata.Foundation.Identity.ProjectId);
        Assert.Equal(project.Module.Slug, project.Module.Metadata.Foundation.Identity.Slug);
    }

    private static NewModuleCommand CreateCommand(
        bool dryRun = false,
        string title = "Les Îles Brisées",
        ProjectRevision? revision = null) =>
        new(
            CommandId.From(Guid.Parse("8ff97f8b-f9b4-486a-84d5-9defe849f9ef")),
            revision ?? new ProjectRevision(0),
            ProjectId.From(Guid.Parse("4d410f21-2d47-4f08-b5d0-2d2e5e43796a")),
            ModuleId.From(Guid.Parse("0a8d1557-b84b-424b-8857-e8f0a7ac2765")),
            AreaId.From(Guid.Parse("ea40d07f-3e30-46a5-a39d-f4f14a25d23a")),
            BlueprintId.From(Guid.Parse("ba1e3949-af2a-409a-af27-8c6698694111")),
            EntityId.From(Guid.Parse("50542d7e-d54f-409b-8156-06cca92b2ea6")),
            BlueprintId.From(Guid.Parse("46d891e5-a116-48df-8b2e-0fdebf16bc52")),
            EntityId.From(Guid.Parse("72dd2ac1-381c-4652-a8ff-5d2be82dcb61")),
            title,
            "Un archipel à reconstruire.",
            "Archipel oublié",
            dryRun);
}
