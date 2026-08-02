using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Modules;
using Forge.Domain.Projects;

namespace Forge.Application.Tests.Projects;

public sealed class SetModuleConfigurationHandlerTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 8, 2, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Handle_AppliesMetadataAndVariablesWithDryRunUndoRedo()
    {
        var state = CreateState();
        var handler = new SetModuleConfigurationHandler(new FixedClock(Timestamp));
        var command = CreateCommand(state);

        var dryRun = handler.Handle(state, command with { DryRun = true });
        var applied = handler.Handle(state, command);
        var history = new ProjectCommandHistory(state);
        history.Record(applied);

        Assert.True(dryRun.Succeeded);
        Assert.Same(state, dryRun.State);
        Assert.False(dryRun.Audit.Applied);
        Assert.Equal("Atelier Forge", applied.State.Project.Module.Metadata.Author);
        Assert.Equal(2, applied.State.Project.Module.Metadata.Localizations.Count);
        Assert.Equal(2, applied.State.Project.Module.Variables.Count);
        Assert.Equal(Timestamp, applied.Audit.Timestamp);
        Assert.True(history.Undo().Succeeded);
        Assert.Equal("migration-default", history.State.Project.Module.Metadata.Provenance);
        Assert.Empty(history.State.Project.Module.Variables);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal("1.1.0", history.State.Project.Module.Metadata.Version);
    }

    [Fact]
    public void Handle_RejectsRevisionAndDuplicateVariableKeysWithoutMutation()
    {
        var state = CreateState();
        var handler = new SetModuleConfigurationHandler(new FixedClock(Timestamp));
        var command = CreateCommand(state);
        var duplicateKey = new ProjectVariableDefinition(
            ProjectVariableId.From(Guid.Parse("ca000000-0000-4000-8000-000000000003")),
            command.Variables[0].Key,
            command.Variables[0].Kind,
            command.Variables[0].Scope,
            command.Variables[0].DefaultValue,
            command.Variables[0].Description,
            command.Variables[0].Provenance);
        var duplicate = command with { Variables = [command.Variables[0], duplicateKey] };

        var revision = handler.Handle(state, command with { BaseRevision = state.Revision.Next() });
        var duplicateResult = handler.Handle(state, duplicate);

        Assert.Equal(SetModuleConfigurationHandler.RevisionConflictCode, Assert.Single(revision.Diagnostics).Code);
        Assert.Equal(SetModuleConfigurationHandler.DuplicateVariableKeyCode,
            Assert.Single(duplicateResult.Diagnostics).Code);
        Assert.Same(state, revision.State);
        Assert.Same(state, duplicateResult.State);
    }

    [Fact]
    public void SafeRenameModule_PreservesMetadataAndVariableSchema()
    {
        var state = CreateState();
        var configuration = new SetModuleConfigurationHandler(new FixedClock(Timestamp))
            .Handle(state, CreateCommand(state));
        var renamed = new Forge.Application.Creator.SafeRenameHandler(new FixedClock(Timestamp)).Handle(
            configuration.State,
            new Forge.Application.Creator.SafeRenameCommand(
                CommandId.From(Guid.Parse("c9000000-0000-4000-8000-000000000002")),
                configuration.State.Revision,
                Forge.Application.Creator.CreatorContentKind.Module,
                configuration.State.Project.Module.Id.ToString(),
                "Module renommé",
                "module-renomme",
                DryRun: false));

        Assert.True(renamed.Succeeded);
        Assert.Equal("Atelier Forge", renamed.State.Project.Module.Metadata.Author);
        Assert.Equal(2, renamed.State.Project.Module.Variables.Count);
    }

    private static SetModuleConfigurationCommand CreateCommand(ProjectState state)
    {
        var metadata = new ProjectMetadata(
            ProjectMetadataId.From(Guid.Parse("c9000000-0000-4000-8000-000000000003")),
            "fr",
            [
                new ProjectLocalizedMetadata("fr", "Module test", "Résumé", "Description", "Équipe"),
                new ProjectLocalizedMetadata("en", "Test module", "Summary", "Description", "Team"),
            ],
            "Atelier Forge",
            "1.1.0",
            "CC0-1.0",
            "assets/module-cover.png",
            ["aventure"],
            "Tout public",
            "creator-command");
        return new SetModuleConfigurationCommand(
            CommandId.From(Guid.Parse("c9000000-0000-4000-8000-000000000001")),
            state.Revision,
            metadata,
            [
                new ProjectVariableDefinition(
                    ProjectVariableId.From(Guid.Parse("ca000000-0000-4000-8000-000000000001")),
                    "world.completed",
                    ProjectVariableValueKind.Boolean,
                    ProjectVariableScope.Save,
                    "false",
                    "Module terminé",
                    "creator-command"),
                new ProjectVariableDefinition(
                    ProjectVariableId.From(Guid.Parse("ca000000-0000-4000-8000-000000000002")),
                    "session.wave",
                    ProjectVariableValueKind.Integer,
                    ProjectVariableScope.Session,
                    "0",
                    "Vague courante",
                    "creator-command"),
            ],
            DryRun: false);
    }

    private static ProjectState CreateState()
    {
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("cb000000-0000-4000-8000-000000000001")),
            "area",
            "Area",
            []);
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("cc000000-0000-4000-8000-000000000001")),
            "Configuration test",
            [],
            [area]);
        return new ProjectState(project, new ProjectRevision(3));
    }
}
