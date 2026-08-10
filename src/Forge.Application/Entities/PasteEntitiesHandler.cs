using Forge.Application.Commands;
using Forge.Application.Creator;
using Forge.Application.Projects;
using Forge.Application.Spatial;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
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
    public const string InvalidBlueprintMappingCode = "clipboard.blueprint_mapping_invalid";

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

        var sourceBlueprintIds = command.Payload.Entities.Select(entity => entity.BlueprintId).Distinct().ToArray();
        var blueprintMap = command.BlueprintIdMap ?? new Dictionary<BlueprintId, BlueprintId>();
        if (command.Mode == SemanticPasteMode.IndependentCopy &&
            (blueprintMap.Count != sourceBlueprintIds.Length ||
             sourceBlueprintIds.Any(id => !blueprintMap.ContainsKey(id)) ||
             blueprintMap.Values.Distinct().Count() != sourceBlueprintIds.Length ||
             blueprintMap.Values.Any(id => state.Project.Blueprints.Any(blueprint => blueprint.Id == id))))
        {
            return Failure(state, command, InvalidBlueprintMappingCode,
                "Independent copy requires one unique new blueprint UUID for every referenced blueprint.");
        }

        var updatedProject = state.Project;
        if (command.Mode == SemanticPasteMode.IndependentCopy)
        {
            var copies = sourceBlueprintIds.Select(sourceId => CopyBlueprint(
                state.Project.Blueprints.Single(blueprint => blueprint.Id == sourceId), blueprintMap[sourceId])).ToArray();
            updatedProject = new ProjectDefinition(updatedProject.Id, updatedProject.Title, updatedProject.Module,
                [.. updatedProject.Blueprints, .. copies], updatedProject.Areas, updatedProject.Dialogues, updatedProject.Quests);
        }
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
                command.Mode == SemanticPasteMode.IndependentCopy ? blueprintMap[source.BlueprintId] : source.BlueprintId,
                new TransformValue(validation.CorrectedPosition, source.Transform.Rotation, source.Transform.Scale),
                source.Tags,
                RemapOverrides(source.Overrides, command.EntityIdMap),
                source.BlueprintVersionRequirement);
            pasted.Add(copy);
            var currentTarget = updatedProject.Areas.Single(area => area.Id == command.TargetAreaId);
            var updatedArea = currentTarget.WithEntities([.. currentTarget.Entities, copy]);
            updatedProject = ReplaceArea(updatedProject, updatedArea);
        }

        var path = $"areas/{command.TargetAreaId}/entities";
        var patch = new ProjectPatch("add", path,
            $"Paste {pasted.Count} semantic clipboard entity instance(s) as {command.Mode}.");
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

    private static EntityPropertyOverrides RemapOverrides(
        EntityPropertyOverrides overrides,
        IReadOnlyDictionary<EntityId, EntityId> entityMap)
    {
        var changed = false;
        var properties = overrides.Properties.Select(property =>
        {
            if (property.Kind != EntityPropertyValueKind.String) return property;
            var value = entityMap.Aggregate(property.Value, (current, pair) =>
                current.Replace(pair.Key.ToString(), pair.Value.ToString(), StringComparison.OrdinalIgnoreCase));
            if (string.Equals(value, property.Value, StringComparison.Ordinal)) return property;
            changed = true;
            return new EntityPropertyOverride(property.Key, property.Kind, value);
        }).ToArray();
        return changed ? new EntityPropertyOverrides(overrides.SchemaVersion, properties) : overrides;
    }

    private static BlueprintDefinition CopyBlueprint(BlueprintDefinition source, BlueprintId id)
    {
        var suffix = id.ToString()[..8];
        var slug = $"{source.Slug}-copy-{suffix}";
        var provenance = new BlueprintProvenance(
            source.Provenance.SchemaVersion, source.Provenance.Source, source.Provenance.Author,
            source.Provenance.LicenseSpdx, source.Provenance.ContentHash, source.Provenance.IsAiGenerated,
            [.. source.Provenance.Modifications, $"Independent clipboard copy from {source.Id}"],
            source.Provenance.PermissionEvidence);
        var authoring = new BlueprintAuthoringMetadata(
            source.Authoring.SchemaVersion, source.Authoring.Version, $"project.clipboard.copy{suffix}",
            $"content/blueprints/{id}/{slug}.blueprint.json", source.Authoring.ExposedParameters,
            source.Authoring.Dependencies, $"content/blueprints/{id}/{slug}.preview.png",
            source.Authoring.PlatformByteBudget, source.Authoring.Kit, source.Authoring.Styles,
            source.Authoring.Compatibility, source.Authoring.Tags);
        return new BlueprintDefinition(id, slug, $"{source.DisplayName} (copie)", source.Kind, source.Combat,
            BlueprintPaletteOrigin.ProjectContent, false, provenance, source.CreaturePresentation,
            source.CreatureProgression, source.CreatureLoot, source.CreatureRoleBehavior, source.CreatureEvents,
            source.ItemAdvanced, authoring, source.CreatureFoundation, source.ItemFoundation);
    }

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
