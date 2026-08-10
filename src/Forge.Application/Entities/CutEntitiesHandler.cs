using Forge.Application.Commands;
using Forge.Application.Creator;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Entities;

public sealed class CutEntitiesHandler
{
    public const int CurrentSchemaVersion = 1;
    public const string SchemaCode = "clipboard.cut.schema_unsupported";
    public const string ProvenanceCode = "clipboard.cut.provenance_missing";
    public const string RevisionCode = "clipboard.cut.revision_conflict";
    public const string AreaCode = "clipboard.cut.area_missing";
    public const string SelectionCode = "clipboard.cut.selection_invalid";
    public const string ReferenceCode = "clipboard.cut.reference_blocked";

    private readonly IClock _clock;

    public CutEntitiesHandler(IClock clock) => _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CutEntitiesResult Handle(ProjectState state, CutEntitiesCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.SchemaVersion != CurrentSchemaVersion) return Failure(state, command, SchemaCode, "Schéma de coupe non pris en charge.");
        if (string.IsNullOrWhiteSpace(command.Provenance)) return Failure(state, command, ProvenanceCode, "La provenance de la coupe est requise.");
        if (command.BaseRevision != state.Revision) return Failure(state, command, RevisionCode, "La révision du projet a changé.");
        var area = state.Project.Areas.SingleOrDefault(candidate => candidate.Id == command.AreaId);
        if (area is null) return Failure(state, command, AreaCode, "La zone source n’existe plus.");
        var ids = command.EntityIds.Distinct().ToHashSet();
        if (ids.Count == 0 || ids.Count != command.EntityIds.Count || ids.Any(id => area.Entities.All(entity => entity.Id != id)))
            return Failure(state, command, SelectionCode, "La sélection à couper doit contenir des UUID uniques existants.");
        var entities = area.Entities.Where(entity => ids.Contains(entity.Id)).ToArray();
        var payload = new EntityClipboardPayload(area.Id, entities, command.CommandId.Value,
            command.Provenance, isCut: true);
        var updatedArea = area.WithEntities(area.Entities.Where(entity => !ids.Contains(entity.Id)).ToArray());
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title, state.Project.Module,
            state.Project.Blueprints, state.Project.Areas.Select(candidate => candidate.Id == area.Id ? updatedArea : candidate).ToArray(),
            state.Project.Dialogues, state.Project.Quests);
        var introduced = ProjectValidator.Validate(project).FirstOrDefault(diagnostic =>
            ProjectValidator.Validate(state.Project).All(previous => previous.Code != diagnostic.Code || previous.Path != diagnostic.Path));
        if (introduced is not null)
            return Failure(state, command, ReferenceCode, $"{introduced.Code} · {introduced.Message} · {introduced.Path}");

        var path = $"areas/{area.Id}/entities/{string.Join(',', ids.OrderBy(id => id.Value))}";
        var patch = new ProjectPatch("remove", path, $"Cut {entities.Length} entity or entities into semantic clipboard.");
        var inverse = new ProjectPatch("add", path, $"Restore {entities.Length} cut entity or entities.");
        var audit = new AuditEntry(command.CommandId, "cut_entities", _clock.UtcNow, !command.DryRun);
        if (command.DryRun)
            return new CutEntitiesResult(new CommandResult(true, state, command.BaseRevision, null, [], patch, inverse,
                audit with { Applied = false }), payload);
        var revision = state.Revision.Next();
        return new CutEntitiesResult(new CommandResult(true, new ProjectState(project, revision), command.BaseRevision,
            revision, [], patch, inverse, audit), payload);
    }

    private CutEntitiesResult Failure(ProjectState state, CutEntitiesCommand command, string code, string message) =>
        new(new CommandResult(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            new AuditEntry(command.CommandId, "cut_entities", _clock.UtcNow, false)), null);
}
