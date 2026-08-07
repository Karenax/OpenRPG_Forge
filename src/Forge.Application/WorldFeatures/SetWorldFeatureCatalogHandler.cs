using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.WorldFeatures;

public sealed class SetWorldFeatureCatalogHandler
{
    private readonly IClock _clock;

    public SetWorldFeatureCatalogHandler(IClock clock) =>
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, SetWorldFeatureCatalogCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
            return Failure(state, command, "command.revision_conflict", "The project revision changed.");
        if (command.Catalog.Id != state.Project.Module.Id.Value)
            return Failure(state, command, "world.catalog.id_mismatch", "The catalog ID must match the module ID.");

        var updatedModule = state.Project.Module.WithWorldFeatures(command.Catalog);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title, updatedModule,
            state.Project.Blueprints, state.Project.Areas, state.Project.Dialogues, state.Project.Quests);
        var diagnostics = ProjectValidator.Validate(project);
        if (diagnostics.Count > 0)
            return new(false, state, state.Revision, null,
                diagnostics.Select(item => new CommandDiagnostic(item.Code, $"{item.Path}: {item.Message}")).ToArray(),
                null, null, Audit(command, false));

        var current = state.Project.Module.WorldFeatures;
        var summary = $"placeables={command.Catalog.Placeables.Count};doors={command.Catalog.Doors.Count};" +
                      $"triggers={command.Catalog.Triggers.Count};encounters={command.Catalog.Encounters.Count};" +
                      $"routes={command.Catalog.VehicleRoutes.Count};merchants={command.Catalog.Merchants.Count};" +
                      $"companions={command.Catalog.Companions.Count};flags={command.Catalog.Scripts.ModuleFlags.Count};" +
                      $"ai={command.Catalog.Scripts.AiSettings.Count}";
        var inverseSummary = $"placeables={current.Placeables.Count};doors={current.Doors.Count};" +
                             $"triggers={current.Triggers.Count};encounters={current.Encounters.Count};" +
                             $"routes={current.VehicleRoutes.Count};merchants={current.Merchants.Count};" +
                             $"companions={current.Companions.Count}";
        var patch = new ProjectPatch("replace", "module/worldFeatures", summary);
        var inverse = new ProjectPatch("replace", "module/worldFeatures", inverseSummary);
        if (command.DryRun)
            return new(true, state, state.Revision, null, [], patch, inverse, Audit(command, false));

        var revision = state.Revision.Next();
        return new(true, new ProjectState(project, revision), state.Revision, revision, [], patch, inverse,
            Audit(command, true));
    }

    private CommandResult Failure(ProjectState state, SetWorldFeatureCatalogCommand command, string code,
        string message) => new(false, state, state.Revision, null, [new CommandDiagnostic(code, message)], null, null,
        Audit(command, false));

    private AuditEntry Audit(SetWorldFeatureCatalogCommand command, bool applied) =>
        new(command.CommandId, "set_world_feature_catalog", _clock.UtcNow, applied);
}
