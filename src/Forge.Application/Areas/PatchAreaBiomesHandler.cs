using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;

namespace Forge.Application.Areas;

public sealed class PatchAreaBiomesHandler
{
    private readonly IClock _clock;
    private readonly SetAreaWorldProfileHandler _worldHandler;

    public PatchAreaBiomesHandler(IClock clock)
    {
        _clock = clock;
        _worldHandler = new SetAreaWorldProfileHandler(clock);
    }

    public CommandResult Handle(ProjectState state, PatchAreaBiomesCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
            return Fail(state, command, "command.revision_conflict", "Project revision changed.");
        var area = state.Project.Areas.SingleOrDefault(item => item.Id == command.AreaId);
        if (area is null)
            return Fail(state, command, "area.biomes.area_missing", "Area does not exist.");

        try
        {
            var world = BiomeScatterAuthoring.Apply(area.World, command.Patches);
            var result = _worldHandler.Handle(state,
                new SetAreaWorldProfileCommand(command.CommandId, command.BaseRevision, command.AreaId, world,
                    command.DryRun));
            if (!result.Succeeded) return result;
            var path = command.Patches.Count == 1
                ? $"areas/{command.AreaId}/world/biomes/{command.Patches[0].BiomeId:D}"
                : $"areas/{command.AreaId}/world/biomes";
            var fields = command.Patches.SelectMany(item => item.Fields).Distinct().Order().Select(Describe);
            var patch = new ProjectPatch("patch", path, $"Patch biome scatter fields: {string.Join(", ", fields)}.");
            var inverse = new ProjectPatch("patch", path, "Restore the previous biome scatter fields.");
            return result with
            {
                Patch = patch,
                Inverse = inverse,
                Audit = new AuditEntry(command.CommandId, "patch_area_biomes", _clock.UtcNow,
                    result.Succeeded && !command.DryRun),
            };
        }
        catch (ArgumentException exception)
        {
            return Fail(state, command, "area.biomes.patch_invalid", exception.Message);
        }
    }

    private static string Describe(BiomeScatterPatchField field) => field switch
    {
        BiomeScatterPatchField.ExclusionTags => "exclusion tags",
        _ => field.ToString().ToLowerInvariant(),
    };

    private CommandResult Fail(ProjectState state, PatchAreaBiomesCommand command, string code, string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            new AuditEntry(command.CommandId, "patch_area_biomes", _clock.UtcNow, false));
}
