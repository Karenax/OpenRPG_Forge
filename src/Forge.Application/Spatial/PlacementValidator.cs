using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Spatial;

public sealed class PlacementValidator
{
    public const string MissingAreaCode = "placement.area_missing";
    public const string MissingBlueprintCode = "placement.blueprint_missing";
    public const string OutsideBoundsCode = "placement.outside_bounds";
    public const string OccupiedCode = "placement.cell_occupied";
    public PlacementValidationResult Validate(
        ProjectDefinition project,
        PlacementValidationRequest request)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(request);
        if (!float.IsFinite(request.GridSize) || request.GridSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Grid size must be finite and greater than zero.");
        }

        var corrected = Snap(request.Position, request.GridSize);
        var area = project.Areas.FirstOrDefault(candidate => candidate.Id == request.AreaId);
        if (area is null)
        {
            return Invalid(MissingAreaCode, "La zone active n’existe plus.", request.Position, corrected);
        }

        if (!project.Blueprints.Any(candidate => candidate.Id == request.BlueprintId))
        {
            return Invalid(MissingBlueprintCode, "Le blueprint glissé n’existe plus dans le projet.", request.Position, corrected);
        }

        var xExtent = area.Dimensions.Width / 2f;
        var zExtent = area.Dimensions.Depth / 2f;
        if (MathF.Abs(corrected.X) > xExtent || MathF.Abs(corrected.Z) > zExtent)
        {
            var bounded = new Vector3Value(
                Math.Clamp(corrected.X, -xExtent, xExtent),
                corrected.Y,
                Math.Clamp(corrected.Z, -zExtent, zExtent));
            return Invalid(
                OutsideBoundsCode,
                $"Position hors de la zone {area.Dimensions.Width} × {area.Dimensions.Depth}. " +
                $"Correction proposée : X {bounded.X:0.##}, Z {bounded.Z:0.##}.",
                request.Position,
                bounded);
        }

        var collisionRadius = Math.Max(0.35f, request.GridSize * 0.45f);
        var occupied = area.Entities
            .Where(entity => entity.Id != request.IgnoredEntityId)
            .FirstOrDefault(entity => DistanceOnGround(entity.Transform.Position, corrected) < collisionRadius);
        if (occupied is not null)
        {
            return Invalid(
                OccupiedCode,
                $"Cellule occupée par l’entité {occupied.Id.ToString()[..8]}. Choisissez une cellule libre.",
                request.Position,
                corrected);
        }

        var wasSnapped = corrected != request.Position;
        return new PlacementValidationResult(
            true,
            PlacementValidationResult.ValidCode,
            wasSnapped ? "Position valide · alignée automatiquement sur la grille." : "Position valide.",
            request.Position,
            corrected);
    }

    private static Vector3Value Snap(Vector3Value position, float gridSize) => new(
        MathF.Round(position.X / gridSize) * gridSize,
        position.Y,
        MathF.Round(position.Z / gridSize) * gridSize);

    private static float DistanceOnGround(Vector3Value left, Vector3Value right)
    {
        var x = left.X - right.X;
        var z = left.Z - right.Z;
        return MathF.Sqrt((x * x) + (z * z));
    }

    private static PlacementValidationResult Invalid(
        string code,
        string message,
        Vector3Value requested,
        Vector3Value corrected) => new(false, code, message, requested, corrected);
}
