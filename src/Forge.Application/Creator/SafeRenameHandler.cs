using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.Domain.Validation;

namespace Forge.Application.Creator;

public sealed class SafeRenameHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string UnsupportedTargetCode = "rename.target_unsupported";
    public const string MissingTargetCode = "rename.target_missing";
    public const string NameRequiredCode = "rename.name_required";
    public const string SlugRequiredCode = "rename.slug_required";
    public const string InvalidSlugCode = "rename.slug_invalid";
    public const string DuplicateSlugCode = "rename.slug_duplicate";

    private const string CommandName = "safe_rename";
    private readonly IClock _clock;

    public SafeRenameHandler(IClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public CommandResult Handle(ProjectState state, SafeRenameCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
        {
            return Failure(state, command, RevisionConflictCode,
                $"Command revision {command.BaseRevision} does not match project revision {state.Revision}.");
        }

        if (!IsSupported(command.TargetKind))
        {
            return Failure(state, command, UnsupportedTargetCode,
                $"Content kind '{command.TargetKind}' cannot be renamed safely yet.");
        }

        var targetId = command.TargetId.Trim();
        var target = SafeRenamePreview.FindTarget(state.Project, command.TargetKind, targetId);
        if (target is null)
        {
            return Failure(state, command, MissingTargetCode,
                $"{command.TargetKind} '{targetId}' does not exist.");
        }

        var displayName = command.DisplayName.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return Failure(state, command, NameRequiredCode, "A display name is required.");
        }

        string? slug = null;
        if (command.TargetKind != CreatorContentKind.Project)
        {
            slug = command.Slug?.Trim();
            if (string.IsNullOrWhiteSpace(slug))
            {
                return Failure(state, command, SlugRequiredCode, "A slug is required for this content kind.");
            }

            if (!string.Equals(slug, SlugGenerator.FromDisplayName(slug, "content"), StringComparison.Ordinal))
            {
                return Failure(state, command, InvalidSlugCode, $"Slug '{command.Slug}' is not canonical.");
            }

            if (HasDuplicateSlug(state.Project, command.TargetKind, targetId, slug))
            {
                return Failure(state, command, DuplicateSlugCode,
                    $"Slug '{slug}' already exists for another {command.TargetKind}.");
            }
        }

        var references = CreatorReferenceIndex.Build(state.Project).Find(command.TargetKind, targetId);
        var updatedProject = Rename(state.Project, command.TargetKind, targetId, displayName, slug);
        var diagnostics = ProjectValidator.Validate(updatedProject);
        if (diagnostics.Count > 0)
        {
            return Failure(state, command, diagnostics[0].Code, diagnostics[0].Message);
        }

        var patch = new ProjectPatch(
            "replace",
            target.Value.DocumentPath,
            $"Rename '{target.Value.DisplayName}' to '{displayName}' while preserving {references.Count} reference(s).");
        var inverse = new ProjectPatch(
            "replace",
            target.Value.DocumentPath,
            $"Restore name '{target.Value.DisplayName}' and slug '{target.Value.Slug ?? "n/a"}'.");
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

    public static bool IsSupported(CreatorContentKind kind) => kind is
        CreatorContentKind.Project or
        CreatorContentKind.Module or
        CreatorContentKind.Area or
        CreatorContentKind.Blueprint or
        CreatorContentKind.Dialogue or
        CreatorContentKind.Quest;

    private static bool HasDuplicateSlug(
        ProjectDefinition project,
        CreatorContentKind kind,
        string targetId,
        string slug) => kind switch
        {
            CreatorContentKind.Area => project.Areas.Any(area =>
                area.Id.ToString() != targetId && string.Equals(area.Slug, slug, StringComparison.Ordinal)),
            CreatorContentKind.Blueprint => project.Blueprints.Any(blueprint =>
                blueprint.Id.ToString() != targetId && string.Equals(blueprint.Slug, slug, StringComparison.Ordinal)),
            CreatorContentKind.Dialogue => project.Dialogues.Any(dialogue =>
                dialogue.Id.ToString() != targetId && string.Equals(dialogue.Slug, slug, StringComparison.Ordinal)),
            CreatorContentKind.Quest => project.Quests.Any(quest =>
                quest.Id.ToString() != targetId && string.Equals(quest.Slug, slug, StringComparison.Ordinal)),
            _ => false,
        };

    private static ProjectDefinition Rename(
        ProjectDefinition project,
        CreatorContentKind kind,
        string targetId,
        string displayName,
        string? slug)
    {
        var module = kind == CreatorContentKind.Module
            ? new ModuleDefinition(
                project.Module.Id,
                slug!,
                displayName,
                project.Module.Description,
                project.Module.EntryAreaId,
                project.Module.Metadata,
                project.Module.Variables)
            : project.Module;
        var areas = kind == CreatorContentKind.Area
            ? project.Areas.Select(area => area.Id.ToString() == targetId
                ? new AreaDefinition(
                    area.Id,
                    slug!,
                    displayName,
                    area.Entities,
                    area.Kind,
                    area.Dimensions,
                    area.Portals,
                    area.SchemaVersion)
                : area).ToArray()
            : project.Areas;
        var blueprints = kind == CreatorContentKind.Blueprint
            ? project.Blueprints.Select(blueprint => blueprint.Id.ToString() == targetId
                ? new BlueprintDefinition(
                    blueprint.Id,
                    slug!,
                    displayName,
                    blueprint.Kind,
                    blueprint.Combat,
                    blueprint.PaletteOrigin,
                    blueprint.IsFavorite,
                    blueprint.Provenance)
                : blueprint).ToArray()
            : project.Blueprints;
        var dialogues = kind == CreatorContentKind.Dialogue
            ? project.Dialogues.Select(dialogue => dialogue.Id.ToString() == targetId
                ? new DialogueDefinition(dialogue.Id, slug!, displayName, dialogue.EntryNodeId, dialogue.Nodes)
                : dialogue).ToArray()
            : project.Dialogues;
        var quests = kind == CreatorContentKind.Quest
            ? project.Quests.Select(quest => quest.Id.ToString() == targetId
                ? new QuestDefinition(quest.Id, slug!, displayName, quest.InitialStateId, quest.States, quest.Transitions)
                : quest).ToArray()
            : project.Quests;
        return new ProjectDefinition(
            project.Id,
            kind == CreatorContentKind.Project ? displayName : project.Title,
            module,
            blueprints,
            areas,
            dialogues,
            quests);
    }

    private CommandResult Failure(
        ProjectState state,
        SafeRenameCommand command,
        string code,
        string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            CreateAudit(command, false));

    private AuditEntry CreateAudit(SafeRenameCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);
}
