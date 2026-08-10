using Forge.Domain.Spatial;

namespace Forge.Application.Spatial;

public sealed record PlacementValidationResult(
    bool IsValid,
    string Code,
    string Message,
    Vector3Value RequestedPosition,
    Vector3Value CorrectedPosition,
    IReadOnlyList<string>? Corrections = null)
{
    public const string ValidCode = "placement.valid";
}
