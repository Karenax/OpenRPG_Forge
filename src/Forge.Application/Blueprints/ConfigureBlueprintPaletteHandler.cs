using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;

namespace Forge.Application.Blueprints;

public sealed class ConfigureBlueprintPaletteHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string MissingBlueprintCode = "blueprint.palette.missing";
    public const string ReadOnlyCode = "blueprint.palette.read_only";
    public const string UnsupportedSchemaCode = "blueprint.provenance.schema_unsupported";
    public const string InstalledSourceCode = "blueprint.provenance.installed_source_invalid";
    public const string InstalledLicenseCode = "blueprint.provenance.installed_license_invalid";
    public const string InstalledHashCode = "blueprint.provenance.installed_hash_invalid";

    private const string CommandName = "configure_blueprint_palette";
    private readonly IClock _clock;

    public ConfigureBlueprintPaletteHandler(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public CommandResult Handle(ProjectState state, ConfigureBlueprintPaletteCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);

        if (command.BaseRevision != state.Revision)
        {
            return Failure(state, command, RevisionConflictCode, "The project revision changed before the palette update.");
        }

        var existing = state.Project.Blueprints.FirstOrDefault(candidate => candidate.Id == command.BlueprintId);
        if (existing is null)
        {
            return Failure(state, command, MissingBlueprintCode, $"Blueprint '{command.BlueprintId}' does not exist.");
        }

        if (existing.IsReadOnly() &&
            (existing.PaletteOrigin != command.Origin || !HasSameProvenance(existing.Provenance, command.Provenance)))
        {
            return Failure(
                state,
                command,
                ReadOnlyCode,
                $"Installed blueprint '{existing.Slug}' is read-only; only its favorite flag can change.");
        }

        var validation = Validate(command);
        if (validation is not null)
        {
            return Failure(state, command, validation.Value.Code, validation.Value.Message);
        }

        var path = $"blueprints/{existing.Id}/palette";
        var patch = new ProjectPatch(
            "replace",
            path,
            $"Classify blueprint '{existing.Slug}' as {command.Origin} and record provenance schema {command.Provenance.SchemaVersion}.");
        var inverse = new ProjectPatch(
            "replace",
            path,
            $"Restore palette origin {existing.PaletteOrigin} for blueprint '{existing.Slug}'.");
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
            existing.Id,
            existing.Slug,
            existing.DisplayName,
            existing.Kind,
            existing.Combat,
            command.Origin,
            command.IsFavorite,
            command.Provenance,
            existing.CreaturePresentation,
            existing.CreatureProgression,
            existing.CreatureLoot,
            existing.CreatureRoleBehavior);
        var project = ReplaceBlueprint(state.Project, updated);
        var revision = state.Revision.Next();
        return new CommandResult(
            true,
            new ProjectState(project, revision),
            command.BaseRevision,
            revision,
            [],
            patch,
            inverse,
            CreateAudit(command, true));
    }

    private static (string Code, string Message)? Validate(ConfigureBlueprintPaletteCommand command)
    {
        if (command.Provenance.SchemaVersion != BlueprintProvenance.CurrentSchemaVersion)
        {
            return (
                UnsupportedSchemaCode,
                $"Provenance schema {command.Provenance.SchemaVersion} is not supported.");
        }

        if (command.Origin != BlueprintPaletteOrigin.InstalledPack)
        {
            return null;
        }

        if (!command.Provenance.Source.StartsWith("pack://", StringComparison.Ordinal))
        {
            return (InstalledSourceCode, "Installed content provenance must use a pack:// source.");
        }

        if (string.Equals(command.Provenance.LicenseSpdx, "UNLICENSED", StringComparison.OrdinalIgnoreCase))
        {
            return (InstalledLicenseCode, "Installed content must declare a distributable SPDX license.");
        }

        if (command.Provenance.ContentHash.Length != 64 ||
            command.Provenance.ContentHash.Any(character => !Uri.IsHexDigit(character)))
        {
            return (InstalledHashCode, "Installed content must declare a 64-character SHA-256 hash.");
        }

        return null;
    }

    private static bool HasSameProvenance(BlueprintProvenance left, BlueprintProvenance right) =>
        left.SchemaVersion == right.SchemaVersion &&
        string.Equals(left.Source, right.Source, StringComparison.Ordinal) &&
        string.Equals(left.Author, right.Author, StringComparison.Ordinal) &&
        string.Equals(left.LicenseSpdx, right.LicenseSpdx, StringComparison.Ordinal) &&
        string.Equals(left.ContentHash, right.ContentHash, StringComparison.Ordinal) &&
        left.IsAiGenerated == right.IsAiGenerated &&
        left.Modifications.SequenceEqual(right.Modifications, StringComparer.Ordinal);

    private static ProjectDefinition ReplaceBlueprint(ProjectDefinition project, BlueprintDefinition updated) =>
        new(
            project.Id,
            project.Title,
            project.Module,
            project.Blueprints.Select(candidate => candidate.Id == updated.Id ? updated : candidate).ToArray(),
            project.Areas,
            project.Dialogues,
            project.Quests);

    private CommandResult Failure(
        ProjectState state,
        ConfigureBlueprintPaletteCommand command,
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

    private AuditEntry CreateAudit(ConfigureBlueprintPaletteCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);
}
