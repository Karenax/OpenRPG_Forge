using Forge.Application.Commands;
using Forge.Application.Creator;
using Forge.Application.Projects;
using Forge.Application.Spatial;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Entities;

public sealed class PasteEntitiesHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string UnsupportedClipboardCode = "clipboard.schema_unsupported";
    public const string MissingTargetAreaCode = "clipboard.target_area_missing";
    public const string InvalidMappingCode = "clipboard.entity_mapping_invalid";
    public const string DuplicateEntityCode = "clipboard.entity_duplicate";

    private const string CommandName = "paste_entities";
    private readonly IClock _clock;
    private readonly PlacementValidator _placementValidator = new();

    public PasteEntitiesHandler(IClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public CommandResult Handle(ProjectState state, PasteEntitiesCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
        {
            return Failure(state, command, RevisionConflictCode,
                $"Command revision {command.BaseRevision} does not match project revision {state.Revision}.");
        }

        if (command.Payload.SchemaVersion != SemanticClipboardPayload.CurrentSchemaVersion)
        {
            return Failure(state, command, UnsupportedClipboardCode,
                $"Clipboard schema '{command.Payload.SchemaVersion}' is not supported.");
        }

        var targetArea = state.Project.Areas.FirstOrDefault(area => area.Id == command.TargetAreaId);
        if (targetArea is null)
        {
            return Failure(state, command, MissingTargetAreaCode,
                $"Target area '{command.TargetAreaId}' does not exist.");
        }

        var sourceIds = command.Payload.Entities.Select(entity => entity.Id).ToHashSet();
        if (command.EntityIdMap.Count != sourceIds.Count ||
            sourceIds.Any(sourceId => !command.EntityIdMap.ContainsKey(sourceId)) ||
            command.EntityIdMap.Values.Distinct().Count() != sourceIds.Count)
        {
            return Failure(state, command, InvalidMappingCode,
                "The entity ID mapping must contain exactly one unique destination for every copied entity.");
        }

        var existingIds = state.Project.Areas.SelectMany(area => area.Entities).Select(entity => entity.Id).ToHashSet();
        if (command.EntityIdMap.Values.Any(existingIds.Contains))
        {
            return Failure(state, command, DuplicateEntityCode,
                "At least one pasted entity ID already exists in the project.");
        }

        var updatedProject = state.Project;
        var pasted = new List<EntityInstance>();
        foreach (var source in command.Payload.Entities)
        {
            var position = new Vector3Value(
                source.Transform.Position.X + command.PositionOffset.X,
                source.Transform.Position.Y + command.PositionOffset.Y,
                source.Transform.Position.Z + command.PositionOffset.Z);
            var validation = _placementValidator.Validate(
                updatedProject,
                new PlacementValidationRequest(command.TargetAreaId, source.BlueprintId, position, 0.25f));
            if (!validation.IsValid)
            {
                return Failure(state, command, validation.Code, validation.Message);
            }

            var copy = new EntityInstance(
                command.EntityIdMap[source.Id],
                source.BlueprintId,
                new TransformValue(validation.CorrectedPosition, source.Transform.Rotation, source.Transform.Scale),
                source.Tags,
                source.Overrides);
            pasted.Add(copy);
            var currentTarget = updatedProject.Areas.Single(area => area.Id == command.TargetAreaId);
            var updatedArea = currentTarget.WithEntities([.. currentTarget.Entities, copy]);
            updatedProject = ReplaceArea(updatedProject, updatedArea);
        }

        var path = $"areas/{command.TargetAreaId}/entities";
        var patch = new ProjectPatch("add", path, $"Paste {pasted.Count} semantic clipboard entity instance(s).");
        var inverse = new ProjectPatch("remove", path, $"Remove {pasted.Count} pasted entity instance(s).");
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

    private static ProjectDefinition ReplaceArea(ProjectDefinition project, AreaDefinition area) =>
        new(
            project.Id,
            project.Title,
            project.Module,
            project.Blueprints,
            project.Areas.Select(candidate => candidate.Id == area.Id ? area : candidate).ToArray(),
            project.Dialogues,
            project.Quests);

    private CommandResult Success(
        ProjectState state,
        PasteEntitiesCommand command,
        ProjectPatch patch,
        ProjectPatch inverse,
        bool applied) =>
        new(true, state, command.BaseRevision, null, [], patch, inverse, CreateAudit(command, applied));

    private CommandResult Failure(
        ProjectState state,
        PasteEntitiesCommand command,
        string code,
        string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            CreateAudit(command, false));

    private AuditEntry CreateAudit(PasteEntitiesCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);
}
