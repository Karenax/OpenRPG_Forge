using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;

namespace Forge.Application.Entities;

public sealed record ManipulateEntitiesCommand(
    CommandId CommandId,
    int SchemaVersion,
    string Provenance,
    ProjectRevision BaseRevision,
    AreaId AreaId,
    IReadOnlyList<EntityId> EntityIds,
    EditorTransformTool Tool,
    Vector3Value Value,
    EditorCoordinateSpace CoordinateSpace,
    EditorPivotMode PivotMode,
    EditorTransformAlignment Alignment,
    float TranslationSnap,
    float RotationSnapDegrees,
    float ScaleSnap,
    bool DryRun);
