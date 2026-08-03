using Forge.RpgKit.Domain.Placeables;

namespace Forge.RpgKit.Application.Placeables;

public sealed record PlaceableProjectState(long Revision, IReadOnlyList<PlaceableDefinition> Definitions);
public sealed record PlaceableMutationResult(PlaceableProjectState State, PlaceableDefinition Definition, string Patch, string InversePatch, bool DryRun);

public static class PlaceableAuthoringService
{
    public static PlaceableMutationResult Upsert(PlaceableProjectState state, PlaceableDefinition definition, long expectedRevision, bool dryRun)
    {
        if (state.Revision != expectedRevision) throw new InvalidOperationException("RPG_PLACEABLE_REVISION_CONFLICT");
        var diagnostics = PlaceableRules.Validate(definition); if (diagnostics.Count > 0) throw new InvalidOperationException(diagnostics[0].Code);
        var previous = state.Definitions.FirstOrDefault(item => item.Id == definition.Id); var definitions = state.Definitions.Where(item => item.Id != definition.Id).Append(definition).OrderBy(item => item.Slug, StringComparer.Ordinal).ToArray();
        var next = dryRun ? state : new PlaceableProjectState(state.Revision + 1, definitions);
        return new(next, definition, $"upsert:/placeables/{definition.Id}", previous is null ? $"remove:/placeables/{definition.Id}" : $"restore:/placeables/{definition.Id}", dryRun);
    }

    public static PlaceableMutationResult Transform(PlaceableProjectState state, Guid id, PlaceableTransform transform, long expectedRevision, bool dryRun)
    {
        var current = state.Definitions.FirstOrDefault(item => item.Id == id) ?? throw new InvalidOperationException("RPG_PLACEABLE_NOT_FOUND");
        return Upsert(state, current with { Transform = PlaceableRules.Snap(transform) }, expectedRevision, dryRun);
    }
}
