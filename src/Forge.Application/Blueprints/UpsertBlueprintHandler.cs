using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;

namespace Forge.Application.Blueprints;

public sealed class UpsertBlueprintHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string InvalidSlugCode = "blueprint.slug_invalid";
    public const string DuplicateSlugCode = "blueprint.slug_duplicate";
    public const string ReadOnlyCode = "blueprint.palette.read_only";

    private const string CommandName = "upsert_blueprint";
    private readonly IClock _clock;

    public UpsertBlueprintHandler(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public CommandResult Handle(ProjectState state, UpsertBlueprintCommand command)
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

        var slug = command.Slug.Trim();
        if (string.IsNullOrWhiteSpace(slug) ||
            !string.Equals(slug, SlugGenerator.FromDisplayName(slug, "blueprint"), StringComparison.Ordinal))
        {
            return Failure(state, command, InvalidSlugCode, $"Blueprint slug '{command.Slug}' is not canonical.");
        }

        if (state.Project.Blueprints.Any(blueprint =>
                blueprint.Id != command.BlueprintId && string.Equals(blueprint.Slug, slug, StringComparison.Ordinal)))
        {
            return Failure(state, command, DuplicateSlugCode, $"Blueprint slug '{slug}' already exists.");
        }

        var existing = state.Project.Blueprints.FirstOrDefault(blueprint => blueprint.Id == command.BlueprintId);
        if (existing?.IsReadOnly() == true)
        {
            return Failure(
                state,
                command,
                ReadOnlyCode,
                $"Installed blueprint '{existing.Slug}' is read-only and cannot be edited in place.");
        }

        var operation = existing is null ? "add" : "replace";
        var path = $"blueprints/{command.BlueprintId}";
        var patch = new ProjectPatch(operation, path, $"{(existing is null ? "Create" : "Update")} blueprint '{slug}'.");
        var inverse = new ProjectPatch(
            existing is null ? "remove" : "replace",
            path,
            existing is null ? $"Remove blueprint '{slug}'." : $"Restore blueprint '{existing.Slug}'.");
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

        var updated = new BlueprintDefinition(
            command.BlueprintId,
            slug,
            command.DisplayName,
            command.Kind,
            existing?.Combat,
            existing?.PaletteOrigin ?? BlueprintPaletteOrigin.ProjectContent,
            existing?.IsFavorite ?? false,
            existing?.Provenance);
        var blueprints = existing is null
            ? [.. state.Project.Blueprints, updated]
            : state.Project.Blueprints.Select(blueprint => blueprint.Id == updated.Id ? updated : blueprint).ToArray();
        var updatedProject = ReplaceBlueprints(state.Project, blueprints);
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

    private static ProjectDefinition ReplaceBlueprints(
        ProjectDefinition project,
        IReadOnlyList<BlueprintDefinition> blueprints) =>
        new(
            project.Id,
            project.Title,
            project.Module,
            blueprints,
            project.Areas,
            project.Dialogues,
            project.Quests);

    private CommandResult Failure(
        ProjectState state,
        UpsertBlueprintCommand command,
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

    private AuditEntry CreateAudit(UpsertBlueprintCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);
}
