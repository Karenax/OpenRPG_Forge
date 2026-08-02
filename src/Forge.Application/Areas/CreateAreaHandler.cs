using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Areas;

public sealed class CreateAreaHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string NameRequiredCode = "area.name_required";
    public const string DuplicateIdCode = "area.id_duplicate";
    public const string DuplicateSlugCode = "area.slug_duplicate";
    public const string SpawnRequiredCode = "area.template.spawn_required";
    public const string SpawnBlueprintMissingCode = "area.template.spawn_blueprint_missing";

    private const string CommandName = "create_area";
    private readonly IClock _clock;

    public CreateAreaHandler(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public CommandResult Handle(ProjectState state, CreateAreaCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
        {
            return Failure(state, command, RevisionConflictCode,
                $"Command revision {command.BaseRevision} does not match project revision {state.Revision}.");
        }

        if (string.IsNullOrWhiteSpace(command.DisplayName))
        {
            return Failure(state, command, NameRequiredCode, "An area display name is required.");
        }

        if (state.Project.Areas.Any(area => area.Id == command.AreaId))
        {
            return Failure(state, command, DuplicateIdCode, $"Area '{command.AreaId}' already exists.");
        }

        var slug = SlugGenerator.FromDisplayName(command.DisplayName, "area");
        if (state.Project.Areas.Any(area => string.Equals(area.Slug, slug, StringComparison.Ordinal)))
        {
            return Failure(state, command, DuplicateSlugCode, $"Area slug '{slug}' already exists.");
        }

        var entities = new List<EntityInstance>();
        if (command.Template == AreaTemplateKind.Playable)
        {
            if (command.SpawnBlueprintId is null || command.SpawnEntityId is null)
            {
                return Failure(state, command, SpawnRequiredCode,
                    "The playable template requires a player spawn blueprint and entity ID.");
            }

            var spawnBlueprint = state.Project.Blueprints.FirstOrDefault(blueprint =>
                blueprint.Id == command.SpawnBlueprintId && blueprint.Kind == BlueprintKind.PlayerSpawn);
            if (spawnBlueprint is null)
            {
                return Failure(state, command, SpawnBlueprintMissingCode,
                    $"Player spawn blueprint '{command.SpawnBlueprintId}' does not exist.");
            }

            if (state.Project.Areas.SelectMany(area => area.Entities)
                .Any(entity => entity.Id == command.SpawnEntityId))
            {
                return Failure(state, command, SpawnRequiredCode,
                    $"Entity '{command.SpawnEntityId}' already exists.");
            }

            entities.Add(new EntityInstance(
                command.SpawnEntityId,
                spawnBlueprint.Id,
                new TransformValue(
                    new Vector3Value(0, 0.8f, 0),
                    QuaternionValue.Identity,
                    Vector3Value.One),
                ["player", "area-anchor"]));
        }

        var area = new AreaDefinition(
            command.AreaId,
            slug,
            command.DisplayName,
            entities,
            command.Kind,
            command.Dimensions);
        var updatedProject = new ProjectDefinition(
            state.Project.Id,
            state.Project.Title,
            state.Project.Module,
            state.Project.Blueprints,
            [.. state.Project.Areas, area],
            state.Project.Dialogues,
            state.Project.Quests);
        var path = $"areas/{area.Id}";
        var patch = new ProjectPatch("add", path, $"Create area '{area.DisplayName}'.");
        var inverse = new ProjectPatch("remove", path, $"Remove area '{area.DisplayName}'.");
        if (command.DryRun)
        {
            return Success(state, command, patch, inverse, applied: false);
        }

        var revision = state.Revision.Next();
        return new CommandResult(
            true,
            new ProjectState(updatedProject, revision),
            command.BaseRevision,
            revision,
            [],
            patch,
            inverse,
            CreateAudit(command, true));
    }

    private CommandResult Success(
        ProjectState state,
        CreateAreaCommand command,
        ProjectPatch patch,
        ProjectPatch inverse,
        bool applied) =>
        new(true, state, command.BaseRevision, null, [], patch, inverse, CreateAudit(command, applied));

    private CommandResult Failure(
        ProjectState state,
        CreateAreaCommand command,
        string code,
        string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            CreateAudit(command, false));

    private AuditEntry CreateAudit(CreateAreaCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);
}
