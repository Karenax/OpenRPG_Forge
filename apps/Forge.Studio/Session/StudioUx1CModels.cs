using Forge.Application.Areas;
using Forge.Domain.Areas;
using Forge.Domain.RuntimePolicies;

namespace Forge.Studio.Session;

internal sealed record StudioLoadingMediaOption(
    string DisplayName,
    string Resource,
    LoadingMediaKind Kind,
    bool Available);

internal sealed record StudioAreaLoadingCatalog(
    IReadOnlyList<StudioLoadingMediaOption> Images,
    IReadOnlyList<StudioLoadingMediaOption> Videos,
    IReadOnlyList<string> RequiredResourceLabels)
{
    internal int RequiredResourceCount => RequiredResourceLabels.Count;
}

internal sealed record StudioAreaLoadingDraft(
    AreaId AreaId,
    string Title,
    string ImageResource,
    string VideoResource,
    IReadOnlyList<string> FrenchTips,
    IReadOnlyList<string> EnglishTips,
    IReadOnlyDictionary<string, IReadOnlyList<string>> PreservedLocalizedTips,
    bool ShowProgress,
    string AccessibleFallback,
    bool PrefetchRequiredResources,
    int MinimumVisibleMilliseconds);

internal sealed record StudioAreaLoadingPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    LoadingPresentationFrame? StandardFrame = null,
    LoadingPresentationFrame? ReducedMotionFrame = null,
    int RequiredResourceCount = 0);

internal sealed record StudioAreaLoadingResult(bool Succeeded, string Message);

internal sealed record StudioUx1CSmokeResult(
    bool Succeeded,
    bool Preview,
    bool DryRun,
    bool ImageAndVideo,
    bool ReducedMotion,
    bool AccessibleFallback,
    bool RealStreaming,
    bool MinimumDuration,
    bool OtherExperiencePreserved,
    bool WorldProfilePreserved,
    bool NeighborPreserved,
    bool UndoRedo,
    bool Persistence,
    bool InvalidBlocked,
    bool HumanDiagnostics);
