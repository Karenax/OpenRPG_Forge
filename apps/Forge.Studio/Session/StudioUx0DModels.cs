using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;

namespace Forge.Studio.Session;

internal sealed record StudioRepeatedPlacementRequest(
    BlueprintId BlueprintId,
    Vector3Value Origin,
    EditorRepeatedPlacementProfile Profile,
    int Count,
    bool ApplyCorrections = true,
    string? RequiredSocket = null,
    IReadOnlyList<string>? AvailableSockets = null,
    long? AvailableBudgetBytes = null,
    long? BlueprintEstimatedBytes = null,
    IReadOnlyList<Vector3Value>? BrushSamples = null);

internal sealed record StudioPlacementPosePreview(
    Vector3Value Position,
    bool IsValid,
    string Message,
    IReadOnlyList<string> Corrections);

internal sealed record StudioPlacementPreview(
    bool IsValid,
    string Message,
    IReadOnlyList<StudioPlacementPosePreview> Poses,
    int CorrectedCount);

internal sealed record StudioEntityBatchActionResult(
    bool Succeeded,
    string Message,
    int EntityCount = 0);

internal sealed record StudioClipboardState(
    bool HasContent,
    int EntityCount,
    bool IsCut,
    string Description)
{
    internal static StudioClipboardState Empty { get; } = new(false, 0, false, "Presse-papiers vide");
}
