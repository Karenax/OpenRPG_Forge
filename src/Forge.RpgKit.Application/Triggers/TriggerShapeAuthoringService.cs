using Forge.RpgKit.Domain.Placeables;
using Forge.RpgKit.Domain.Triggers;

namespace Forge.RpgKit.Application.Triggers;

public sealed record TriggerShapeEditResult(TriggerShape Shape, string Preview, string Patch, string InversePatch, bool DryRun);

public static class TriggerShapeAuthoringService
{
    public static TriggerShapeEditResult MoveVertex(TriggerShape shape, int index, Vector3Value position, bool dryRun)
    {
        if (index < 0 || index >= shape.Vertices.Count) throw new InvalidOperationException("RPG_TRIGGER_VERTEX_NOT_FOUND");
        var previous = shape.Vertices[index]; var vertices = shape.Vertices.ToArray(); vertices[index] = position; var updated = TriggerRules.Snap(shape with { Vertices = vertices });
        return new(dryRun ? shape : updated, $"{updated.Kind}|vertex:{index}|position:{updated.Vertices[index].X},{updated.Vertices[index].Y},{updated.Vertices[index].Z}", $"replace:/shape/vertices/{index}", $"replace:/shape/vertices/{index}:{previous.X},{previous.Y},{previous.Z}", dryRun);
    }
}
