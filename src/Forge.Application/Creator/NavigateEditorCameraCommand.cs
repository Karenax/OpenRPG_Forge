using Forge.Application.Commands;
using Forge.Domain.Authoring;
using Forge.Domain.Spatial;

namespace Forge.Application.Creator;

public sealed record NavigateEditorCameraCommand(
    CommandId CommandId,
    EditorCameraRevision BaseRevision,
    EditorCameraOperation Operation,
    float X = 0,
    float Y = 0,
    Vector3Value? Target = null,
    float Radius = 1,
    EditorCameraView View = EditorCameraView.Perspective,
    EditorCameraBookmarkId? BookmarkId = null,
    string? BookmarkName = null,
    bool DryRun = false,
    float Z = 0,
    EditorCameraInputConfiguration? Input = null,
    EditorCameraPose? Pose = null);
