using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;

namespace Forge.Studio.Session;

internal sealed record StudioTriggerChoice(
    BlueprintId BlueprintId,
    string Label,
    bool HasFoundation);

internal sealed record StudioTriggerActionDraft(
    Guid Id,
    string Kind,
    string TargetId,
    IReadOnlyDictionary<string, string> Parameters);

internal sealed record StudioTriggerEventDraft(
    Guid Id,
    string ForgeScriptApiVersion,
    string EventBinding,
    string? BeforeScriptId,
    string? AfterScriptId);

internal sealed record StudioTriggerFoundationDraft(
    BlueprintId BlueprintId,
    AreaId AreaId,
    Guid ProfileId,
    Guid FoundationId,
    string Slug,
    TriggerWorldProfile.ShapeKind ShapeKind,
    IReadOnlyList<Vector3Value> Vertices,
    IReadOnlyList<IReadOnlyList<Vector3Value>> Holes,
    Vector3Value Size,
    double Height,
    double SnapIncrement,
    bool VisualizeInEditor,
    IReadOnlyList<string> EntityKinds,
    IReadOnlyList<string> RequiredTags,
    IReadOnlyList<string> ExcludedTags,
    IReadOnlyList<string> Layers,
    IReadOnlyList<string> Conditions,
    double CooldownSeconds,
    TriggerWorldProfile.FirePolicy FirePolicy,
    IReadOnlyList<StudioTriggerActionDraft> Actions,
    Vector3Value Position,
    Vector3Value RotationDegrees,
    Vector3Value Scale,
    StudioTriggerEventDraft OnEnter,
    StudioTriggerEventDraft OnExit,
    bool RequireRuntime,
    TriggerWorldProfile? OriginalProfile,
    TriggerWorldProfile.FoundationPolicy? OriginalFoundation);

internal sealed record StudioTriggerFoundationPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    IReadOnlyList<Vector3Value>? SnappedVertices = null,
    IReadOnlyList<IReadOnlyList<Vector3Value>>? SnappedHoles = null);

internal sealed record StudioUx1YActionResult(bool Succeeded, string Message);

internal sealed record StudioUx1YSmokeResult(
    bool Succeeded,
    bool SelectionExplicit,
    bool Geometry,
    bool DryRun,
    bool InvalidGeometryBlocked,
    bool InvalidFilterBlocked,
    bool InvalidBindingBlocked,
    bool StaleRevisionBlocked,
    bool TypedEvents,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool PortalPreserved,
    bool ValidationPreserved,
    bool AdvancedPreserved,
    bool NeighborPreserved,
    bool HumanDiagnostics);
