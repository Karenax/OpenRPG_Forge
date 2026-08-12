using Forge.Application.Commands;
using Forge.Application.Entities;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;

namespace Forge.Application.Creator;

public sealed record CrossAreaCopyRequest(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    AreaId SourceAreaId,
    AreaId TargetAreaId,
    IReadOnlyList<EntityId> EntityIds,
    Vector3Value PositionOffset,
    bool DryRun = false)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed class CrossAreaCopyService
{
    public const string InvalidRequestCode = "editor.multi_area.copy_invalid";
    public const string SameAreaCode = "editor.multi_area.copy_same_area";

    private readonly IClock _clock;

    public CrossAreaCopyService(IClock clock) => _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Copy(ProjectState state, CrossAreaCopyRequest request)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        if (request.Id == Guid.Empty || request.SchemaVersion != CrossAreaCopyRequest.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(request.Provenance) || request.EntityIds.Count == 0 ||
            request.EntityIds.Distinct().Count() != request.EntityIds.Count)
            return Failure(state, request, InvalidRequestCode, "Cross-area copy identity, provenance and selection are required.");
        if (request.SourceAreaId == request.TargetAreaId)
            return Failure(state, request, SameAreaCode, "Cross-area copy requires two different areas.");

        EntityClipboardPayload payload;
        try
        {
            payload = new SemanticClipboard().CopyEntities(state.Project, request.SourceAreaId, request.EntityIds,
                request.Provenance);
        }
        catch (ArgumentException exception)
        {
            return Failure(state, request, InvalidRequestCode, exception.Message);
        }

        var mapping = payload.Entities.ToDictionary(entity => entity.Id, _ => EntityId.From(Guid.NewGuid()));
        var command = new PasteEntitiesCommand(CommandId.From(request.Id), state.Revision, request.TargetAreaId,
            payload, mapping, request.PositionOffset, request.DryRun);
        return new PasteEntitiesHandler(_clock).Handle(state, command);
    }

    private CommandResult Failure(ProjectState state, CrossAreaCopyRequest request, string code, string message) =>
        new(false, state, state.Revision, null, [new CommandDiagnostic(code, message)], null, null,
            new AuditEntry(CommandId.From(request.Id == Guid.Empty ? Guid.NewGuid() : request.Id),
                "cross_area_copy", _clock.UtcNow, false));
}
