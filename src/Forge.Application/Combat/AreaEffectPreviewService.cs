using Forge.Domain.Entities;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Spatial;

namespace Forge.Application.Combat;

public sealed record AreaEffectTargetCandidate(
    EntityId Id,
    Vector3Value Position,
    float Cover,
    bool Targetable);

public sealed record AreaEffectPreviewRequest(
    AreaEffectShape Shape,
    Vector3Value Origin,
    Vector3Value Direction,
    float Range,
    float Radius,
    float Width,
    float AngleDegrees,
    bool ServerConfirmed,
    IReadOnlyList<AreaEffectTargetCandidate> Candidates);

public sealed record AreaEffectPreviewResult(
    bool IsValid,
    IReadOnlyList<EntityId> ValidTargets,
    IReadOnlyList<EntityId> BlockedTargets,
    string Color,
    string Code);

public sealed class AreaEffectPreviewService(AreaEffectIndicatorProfile profile)
{
    public const string SuccessCode = "aoe.preview.ok";
    public const string UnsupportedShapeCode = "aoe.preview.shape_unsupported";
    public const string OutOfRangeCode = "aoe.preview.range_invalid";
    public const string ConfirmationRequiredCode = "aoe.preview.server_confirmation_required";

    private readonly AreaEffectIndicatorProfile _profile = profile ?? throw new ArgumentNullException(nameof(profile));

    public AreaEffectPreviewResult Preview(AreaEffectPreviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_profile.Shapes.Contains(request.Shape)) return Invalid(UnsupportedShapeCode);
        if (request.Range is <= 0 || request.Range > _profile.MaxRange || request.Radius < 0 || request.Width < 0)
            return Invalid(OutOfRangeCode);
        if (_profile.RequireServerConfirmation && !request.ServerConfirmed) return Invalid(ConfirmationRequiredCode);

        var directionLength = MathF.Sqrt(request.Direction.X * request.Direction.X + request.Direction.Z * request.Direction.Z);
        var directionX = directionLength > 0.0001f ? request.Direction.X / directionLength : 0;
        var directionZ = directionLength > 0.0001f ? request.Direction.Z / directionLength : 1;
        var valid = new List<EntityId>();
        var blocked = new List<EntityId>();
        foreach (var candidate in request.Candidates.OrderBy(item => item.Id.Value))
        {
            if (!candidate.Targetable || MathF.Abs(candidate.Position.Y - request.Origin.Y) > _profile.MaxHeightDelta ||
                !Contains(request, candidate.Position, directionX, directionZ)) continue;
            if (_profile.EvaluateCover && candidate.Cover >= 0.75f) blocked.Add(candidate.Id);
            else valid.Add(candidate.Id);
        }

        return new AreaEffectPreviewResult(true, valid, blocked, _profile.ValidColor, SuccessCode);
    }

    private static bool Contains(AreaEffectPreviewRequest request, Vector3Value position, float directionX, float directionZ)
    {
        var dx = position.X - request.Origin.X;
        var dz = position.Z - request.Origin.Z;
        var distance = MathF.Sqrt(dx * dx + dz * dz);
        var forward = dx * directionX + dz * directionZ;
        var side = MathF.Abs(dx * -directionZ + dz * directionX);
        return request.Shape switch
        {
            AreaEffectShape.Circle => distance <= request.Radius,
            AreaEffectShape.Cone => distance <= request.Range && forward >= 0 &&
                (distance < 0.0001f || MathF.Acos(Math.Clamp(forward / distance, -1, 1)) <= request.AngleDegrees * MathF.PI / 360),
            AreaEffectShape.Rectangle => forward is >= 0 && forward <= request.Range && side <= request.Width / 2,
            AreaEffectShape.Line => forward is >= 0 && forward <= request.Range && side <= MathF.Max(request.Width / 2, 0.1f),
            _ => false,
        };
    }

    private AreaEffectPreviewResult Invalid(string code) => new(false, [], [], _profile.InvalidColor, code);
}
