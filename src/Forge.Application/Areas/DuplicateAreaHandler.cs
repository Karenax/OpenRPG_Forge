using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Areas;

public sealed class DuplicateAreaHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string MissingSourceAreaCode = "area.clone.source_missing";
    public const string NameRequiredCode = "area.name_required";
    public const string DuplicateAreaCode = "area.id_duplicate";
    public const string DuplicateSlugCode = "area.slug_duplicate";
    public const string InvalidEntityMappingCode = "area.clone.entity_mapping_invalid";
    public const string InvalidPortalMappingCode = "area.clone.portal_mapping_invalid";

    private const string CommandName = "duplicate_area";
    private readonly IClock _clock;

    public DuplicateAreaHandler(IClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public CommandResult Handle(ProjectState state, DuplicateAreaCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
        {
            return Failure(state, command, RevisionConflictCode,
                $"Command revision {command.BaseRevision} does not match project revision {state.Revision}.");
        }

        var source = state.Project.Areas.FirstOrDefault(area => area.Id == command.SourceAreaId);
        if (source is null)
        {
            return Failure(state, command, MissingSourceAreaCode,
                $"Source area '{command.SourceAreaId}' does not exist.");
        }

        if (string.IsNullOrWhiteSpace(command.DisplayName))
        {
            return Failure(state, command, NameRequiredCode, "An area display name is required.");
        }

        if (state.Project.Areas.Any(area => area.Id == command.DestinationAreaId))
        {
            return Failure(state, command, DuplicateAreaCode,
                $"Area '{command.DestinationAreaId}' already exists.");
        }

        var slug = SlugGenerator.FromDisplayName(command.DisplayName, "area-copy");
        if (state.Project.Areas.Any(area => string.Equals(area.Slug, slug, StringComparison.Ordinal)))
        {
            return Failure(state, command, DuplicateSlugCode, $"Area slug '{slug}' already exists.");
        }

        if (!TryCreateMaps(state.Project, source, command, out var entityMap, out var portalMap, out var mappingFailure))
        {
            return Failure(state, command, mappingFailure!.Value.Code, mappingFailure.Value.Message);
        }

        var entities = command.Mode == AreaCloneMode.Template
            ? []
            : source.Entities.Select(entity => new EntityInstance(
                entityMap![entity.Id],
                entity.BlueprintId,
                entity.Transform,
                entity.Tags,
                entity.Overrides)).ToArray();
        var portals = command.Mode == AreaCloneMode.Template
            ? []
            : source.Portals
                .Where(portal => portal.DestinationAreaId == source.Id ||
                    command.ExternalPortalPolicy == ExternalPortalPolicy.Preserve)
                .Select(portal => new PortalLink(
                    portalMap![portal.Id],
                    portal.DisplayName,
                    entityMap![portal.SourceEntityId],
                    portal.DestinationAreaId == source.Id ? command.DestinationAreaId : portal.DestinationAreaId,
                    portal.DestinationAreaId == source.Id
                        ? entityMap![portal.DestinationEntityId]
                        : portal.DestinationEntityId,
                    portal.Bidirectional,
                    portal.PreloadDestination))
                .ToArray();
        var clone = new AreaDefinition(
            command.DestinationAreaId,
            slug,
            command.DisplayName,
            entities,
            source.Kind,
            source.Dimensions,
            portals);
        var updatedProject = new ProjectDefinition(
            state.Project.Id,
            state.Project.Title,
            state.Project.Module,
            state.Project.Blueprints,
            [.. state.Project.Areas, clone],
            state.Project.Dialogues,
            state.Project.Quests);
        var diagnostics = ProjectValidator.Validate(updatedProject);
        if (diagnostics.Count > 0)
        {
            var diagnostic = diagnostics[0];
            return Failure(state, command, diagnostic.Code, diagnostic.Message);
        }

        var path = $"areas/{clone.Id}";
        var patch = new ProjectPatch(
            "add",
            path,
            $"Duplicate area '{source.DisplayName}' as '{clone.DisplayName}' with {entities.Length} entity instance(s).");
        var inverse = new ProjectPatch("remove", path, $"Remove duplicated area '{clone.DisplayName}'.");
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

    private static bool TryCreateMaps(
        ProjectDefinition project,
        AreaDefinition source,
        DuplicateAreaCommand command,
        out IReadOnlyDictionary<EntityId, EntityId>? entityMap,
        out IReadOnlyDictionary<PortalId, PortalId>? portalMap,
        out (string Code, string Message)? failure)
    {
        entityMap = null;
        portalMap = null;
        failure = null;
        var expectedEntities = command.Mode == AreaCloneMode.Full
            ? source.Entities.Select(entity => entity.Id).ToHashSet()
            : [];
        if (command.EntityIdMap.Count != expectedEntities.Count ||
            command.EntityIdMap.Select(item => item.SourceId).Distinct().Count() != expectedEntities.Count ||
            command.EntityIdMap.Select(item => item.DestinationId).Distinct().Count() != expectedEntities.Count ||
            command.EntityIdMap.Any(item => !expectedEntities.Contains(item.SourceId)))
        {
            failure = (InvalidEntityMappingCode,
                "The entity mapping must contain one unique destination for every cloned entity.");
            return false;
        }

        var existingEntityIds = project.Areas.SelectMany(area => area.Entities).Select(entity => entity.Id).ToHashSet();
        if (command.EntityIdMap.Any(item => existingEntityIds.Contains(item.DestinationId)))
        {
            failure = (InvalidEntityMappingCode, "At least one cloned entity ID already exists in the project.");
            return false;
        }

        var expectedPortals = command.Mode == AreaCloneMode.Full
            ? source.Portals.Select(portal => portal.Id).ToHashSet()
            : [];
        if (command.PortalIdMap.Count != expectedPortals.Count ||
            command.PortalIdMap.Select(item => item.SourceId).Distinct().Count() != expectedPortals.Count ||
            command.PortalIdMap.Select(item => item.DestinationId).Distinct().Count() != expectedPortals.Count ||
            command.PortalIdMap.Any(item => !expectedPortals.Contains(item.SourceId)))
        {
            failure = (InvalidPortalMappingCode,
                "The portal mapping must contain one unique destination for every source portal.");
            return false;
        }

        var existingPortalIds = project.Areas.SelectMany(area => area.Portals).Select(portal => portal.Id).ToHashSet();
        if (command.PortalIdMap.Any(item => existingPortalIds.Contains(item.DestinationId)))
        {
            failure = (InvalidPortalMappingCode, "At least one cloned portal ID already exists in the project.");
            return false;
        }

        entityMap = command.EntityIdMap.ToDictionary(item => item.SourceId, item => item.DestinationId);
        portalMap = command.PortalIdMap.ToDictionary(item => item.SourceId, item => item.DestinationId);
        return true;
    }

    private CommandResult Success(
        ProjectState state,
        DuplicateAreaCommand command,
        ProjectPatch patch,
        ProjectPatch inverse,
        bool applied) =>
        new(true, state, command.BaseRevision, null, [], patch, inverse, CreateAudit(command, applied));

    private CommandResult Failure(
        ProjectState state,
        DuplicateAreaCommand command,
        string code,
        string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            CreateAudit(command, false));

    private AuditEntry CreateAudit(DuplicateAreaCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);
}
