using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Spatial;

public sealed class PlacementValidator
{
    public const string MissingAreaCode = "placement.area_missing";
    public const string MissingBlueprintCode = "placement.blueprint_missing";
    public const string OutsideBoundsCode = "placement.outside_bounds";
    public const string OccupiedCode = "placement.cell_occupied";
    public const string SocketMissingCode = "placement.socket_missing";
    public const string BudgetExceededCode = "placement.budget_exceeded";
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

        if (!string.IsNullOrWhiteSpace(request.RequiredSocket) &&
            !(request.AvailableSockets ?? []).Contains(request.RequiredSocket.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return Invalid(
                SocketMissingCode,
                $"Socket requis '{request.RequiredSocket.Trim()}' indisponible. Choisissez un raccord compatible.",
                request.Position,
                corrected,
                ["Sélectionner un socket compatible", "Retirer la contrainte de socket"]);
        }

        if (request.AvailableBudgetBytes is not null && request.BlueprintEstimatedBytes is not null &&
            (request.AvailableBudgetBytes < 0 || request.BlueprintEstimatedBytes < 0 ||
             request.BlueprintEstimatedBytes > request.AvailableBudgetBytes))
        {
            return Invalid(
                BudgetExceededCode,
                $"Budget dépassé : {request.BlueprintEstimatedBytes} octets requis pour {request.AvailableBudgetBytes} disponibles.",
                request.Position,
                corrected,
                ["Choisir une variante plus légère", "Augmenter le budget de zone"]);
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
                bounded,
                ["Appliquer la position corrigée dans les limites"]);
        }

        var collisionRadius = Math.Max(0.35f, request.GridSize * 0.45f);
        var occupied = area.Entities
            .Where(entity => entity.Id != request.IgnoredEntityId)
            .FirstOrDefault(entity => DistanceOnGround(entity.Transform.Position, corrected) < collisionRadius);
        if (occupied is not null)
        {
            var suggested = request.AutoCorrect
                ? FindNearestFree(area.Entities, corrected, request.GridSize, collisionRadius,
                    request.IgnoredEntityId, xExtent, zExtent)
                : null;
            return Invalid(
                OccupiedCode,
                suggested is null
                    ? "Cette cellule est déjà occupée. Choisissez une cellule libre."
                    : "Cette cellule est déjà occupée. Correction proposée : " +
                      $"X {suggested.Value.X:0.##}, Z {suggested.Value.Z:0.##}.",
                request.Position,
                suggested ?? corrected,
                suggested is null ? ["Choisir une cellule libre"] : ["Appliquer la cellule libre proposée"]);
        }

        var wasSnapped = corrected != request.Position;
        return new PlacementValidationResult(
            true,
            PlacementValidationResult.ValidCode,
            wasSnapped ? "Position valide · alignée automatiquement sur la grille." : "Position valide.",
            request.Position,
            corrected,
            wasSnapped ? ["Position alignée sur la grille"] : []);
    }

    private static Vector3Value? FindNearestFree(
        IReadOnlyList<Forge.Domain.Entities.EntityInstance> entities,
        Vector3Value origin,
        float gridSize,
        float collisionRadius,
        Forge.Domain.Entities.EntityId? ignored,
        float xExtent,
        float zExtent)
    {
        for (var ring = 1; ring <= 8; ring++)
        {
            var offsets = new[]
            {
                (ring, 0), (0, ring), (-ring, 0), (0, -ring),
                (ring, ring), (-ring, ring), (-ring, -ring), (ring, -ring),
            };
            foreach (var (x, z) in offsets)
            {
                var candidate = new Vector3Value(origin.X + (x * gridSize), origin.Y, origin.Z + (z * gridSize));
                if (MathF.Abs(candidate.X) > xExtent || MathF.Abs(candidate.Z) > zExtent) continue;
                if (entities.Where(entity => entity.Id != ignored)
                    .All(entity => DistanceOnGround(entity.Transform.Position, candidate) >= collisionRadius))
                    return candidate;
            }
        }

        return null;
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
        Vector3Value corrected,
        IReadOnlyList<string>? corrections = null) => new(false, code, message, requested, corrected, corrections ?? []);
}
