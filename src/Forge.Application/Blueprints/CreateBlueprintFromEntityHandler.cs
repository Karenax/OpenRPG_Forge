using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;

namespace Forge.Application.Blueprints;

public sealed class CreateBlueprintFromEntityHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string MissingAreaCode = "command.area_missing";
    public const string MissingEntityCode = "command.entity_missing";
    public const string MissingSourceBlueprintCode = "command.blueprint_missing";
    public const string DuplicateBlueprintCode = "blueprint.id_duplicate";
    public const string DuplicateSlugCode = "blueprint.slug_duplicate";
    public const string InvalidSlugCode = "blueprint.slug_invalid";

    private const string CommandName = "create_blueprint_from_entity";
    private readonly IClock _clock;

    public CreateBlueprintFromEntityHandler(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public CommandResult Handle(ProjectState state, CreateBlueprintFromEntityCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);

        if (command.BaseRevision != state.Revision)
        {
            return Failure(
                state,
                command,
                RevisionConflictCode,
                $"Command revision {command.BaseRevision} does not match project revision {state.Revision}.");
        }

        var area = state.Project.Areas.FirstOrDefault(candidate => candidate.Id == command.AreaId);
        if (area is null)
        {
            return Failure(state, command, MissingAreaCode, $"Area '{command.AreaId}' does not exist.");
        }

        var entity = area.Entities.FirstOrDefault(candidate => candidate.Id == command.EntityId);
        if (entity is null)
        {
            return Failure(state, command, MissingEntityCode, $"Entity '{command.EntityId}' does not exist.");
        }

        var source = state.Project.Blueprints.FirstOrDefault(blueprint => blueprint.Id == entity.BlueprintId);
        if (source is null)
        {
            return Failure(
                state,
                command,
                MissingSourceBlueprintCode,
                $"Source blueprint '{entity.BlueprintId}' does not exist.");
        }

        if (state.Project.Blueprints.Any(blueprint => blueprint.Id == command.NewBlueprintId))
        {
            return Failure(
                state,
                command,
                DuplicateBlueprintCode,
                $"Blueprint '{command.NewBlueprintId}' already exists.");
        }

        var slug = command.Slug.Trim();
        if (string.IsNullOrWhiteSpace(slug) ||
            !string.Equals(slug, SlugGenerator.FromDisplayName(slug, "blueprint"), StringComparison.Ordinal))
        {
            return Failure(state, command, InvalidSlugCode, $"Blueprint slug '{command.Slug}' is not canonical.");
        }

        if (state.Project.Blueprints.Any(blueprint => string.Equals(blueprint.Slug, slug, StringComparison.Ordinal)))
        {
            return Failure(state, command, DuplicateSlugCode, $"Blueprint slug '{slug}' already exists.");
        }

        var path = $"blueprints/{command.NewBlueprintId}";
        var patch = new ProjectPatch(
            "add",
            path,
            $"Create blueprint '{slug}' from entity '{entity.Id}' and retarget that instance.");
        var inverse = new ProjectPatch(
            "remove",
            path,
            $"Remove blueprint '{slug}' and restore instance reference '{source.Id}'.");
        if (command.DryRun)
        {
            return new CommandResult(
                true,
                state,
                command.BaseRevision,
                null,
                [],
                patch,
                inverse,
                CreateAudit(command, false));
        }

        var derived = new BlueprintDefinition(
            command.NewBlueprintId,
            slug,
            command.DisplayName,
            source.Kind,
            source.Combat,
            BlueprintPaletteOrigin.ProjectContent,
            false,
            new BlueprintProvenance(
                BlueprintProvenance.CurrentSchemaVersion,
                $"project://derived/{source.Id}",
                source.Provenance.Author,
                source.Provenance.LicenseSpdx,
                source.Provenance.ContentHash,
                source.Provenance.IsAiGenerated,
                [.. source.Provenance.Modifications, $"Derived from blueprint {source.Id}"]),
            itemAdvanced: source.ItemAdvanced);
        var retargeted = new EntityInstance(
            entity.Id,
            derived.Id,
            entity.Transform,
            entity.Tags,
            entity.Overrides);
        var updatedArea = area.WithEntities(
            area.Entities.Select(candidate => candidate.Id == retargeted.Id ? retargeted : candidate).ToArray());
        var updatedProject = new ProjectDefinition(
            state.Project.Id,
            state.Project.Title,
            state.Project.Module,
            [.. state.Project.Blueprints, derived],
            state.Project.Areas.Select(candidate => candidate.Id == area.Id ? updatedArea : candidate).ToArray(),
            state.Project.Dialogues,
            state.Project.Quests);
        var newRevision = state.Revision.Next();
        return new CommandResult(
            true,
            new ProjectState(updatedProject, newRevision),
            command.BaseRevision,
            newRevision,
            [],
            patch,
            inverse,
            CreateAudit(command, true));
    }

    private CommandResult Failure(
        ProjectState state,
        CreateBlueprintFromEntityCommand command,
        string code,
        string message) =>
        new(
            false,
            state,
            command.BaseRevision,
            null,
            [new CommandDiagnostic(code, message)],
            null,
            null,
            CreateAudit(command, false));

    private AuditEntry CreateAudit(CreateBlueprintFromEntityCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);
}
