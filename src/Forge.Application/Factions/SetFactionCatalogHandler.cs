using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Factions;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Factions;

public sealed class SetFactionCatalogHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";

    private readonly IClock _clock;

    public SetFactionCatalogHandler(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public CommandResult Handle(ProjectState state, SetFactionCatalogCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
            return Failure(state, command, RevisionConflictCode,
                "The project revision changed before factions could be applied.");

        var diagnostics = FactionValidator.Validate(command.Catalog);
        if (diagnostics.Count > 0)
            return Invalid(state, command, diagnostics);

        var current = state.Project.Module.Factions;
        var advanced = command.Catalog.Advanced;
        var patch = new ProjectPatch("replace", "module/factions",
            $"Set {command.Catalog.Factions.Count} faction(s), {advanced?.Memberships.Count ?? 0} membership(s), " +
            $"{advanced?.WitnessPolicies.Count ?? 0} witness policy/policies, and " +
            $"{advanced?.DynamicFactions.Count ?? 0} dynamic faction(s).");
        var inverse = new ProjectPatch("replace", "module/factions",
            $"Restore {current.Factions.Count} faction(s) and the previous advanced profile.");
        if (command.DryRun)
            return new(true, state, command.BaseRevision, null, [], patch, inverse, Audit(command, false));

        var module = state.Project.Module.WithFactions(command.Catalog);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title, module,
            state.Project.Blueprints, state.Project.Areas, state.Project.Dialogues, state.Project.Quests);
        var projectDiagnostics = ProjectValidator.Validate(project);
        if (projectDiagnostics.Count > 0)
            return new(false, state, command.BaseRevision, null,
                projectDiagnostics.Select(item => new CommandDiagnostic(item.Code,
                    $"{item.Path}: {item.Message}")).ToArray(), null, null, Audit(command, false));

        var revision = state.Revision.Next();
        return new(true, new ProjectState(project, revision), command.BaseRevision, revision, [], patch, inverse,
            Audit(command, true));
    }

    private CommandResult Invalid(
        ProjectState state,
        SetFactionCatalogCommand command,
        IReadOnlyList<FactionDiagnostic> diagnostics) =>
        new(false, state, command.BaseRevision, null,
            diagnostics.Select(item => new CommandDiagnostic(item.Code, $"{item.Path}: {item.Message}")).ToArray(),
            null, null, Audit(command, false));

    private CommandResult Failure(
        ProjectState state,
        SetFactionCatalogCommand command,
        string code,
        string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            Audit(command, false));

    private AuditEntry Audit(SetFactionCatalogCommand command, bool applied) =>
        new(command.CommandId, "set_faction_catalog", _clock.UtcNow, applied);
}
