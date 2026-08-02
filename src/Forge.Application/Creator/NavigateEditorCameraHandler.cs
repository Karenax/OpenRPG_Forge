using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Authoring;
using Forge.Domain.Spatial;

namespace Forge.Application.Creator;

public sealed class NavigateEditorCameraHandler
{
    public const string RevisionConflictCode = "editor.camera.revision_conflict";
    public const string TargetMissingCode = "editor.camera.focus_target_missing";
    public const string BookmarkMissingCode = "editor.camera.bookmark_missing";
    public const string BookmarkNameMissingCode = "editor.camera.bookmark_name_missing";
    public const string ValueInvalidCode = "editor.camera.value_invalid";

    private const string CommandName = "navigate_editor_camera";
    private readonly IClock _clock;

    public NavigateEditorCameraHandler(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public EditorCameraCommandResult Handle(EditorCameraState state, NavigateEditorCameraCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
        {
            return Failure(state, command, RevisionConflictCode, "The camera profile changed before this command was applied.");
        }

        EditorCameraProfile updated;
        try
        {
            updated = Apply(state.Profile, command);
        }
        catch (CameraCommandException exception)
        {
            return Failure(state, command, exception.Code, exception.Message);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Failure(state, command, ValueInvalidCode, exception.Message);
        }

        var patch = new ProjectPatch(
            "replace",
            $"editor/camera/{state.Profile.Id}",
            $"Apply {command.Operation} to editor camera profile.");
        var inverse = new ProjectPatch(
            "replace",
            $"editor/camera/{state.Profile.Id}",
            $"Restore camera profile before {command.Operation}.");
        if (command.DryRun)
        {
            return new EditorCameraCommandResult(
                true,
                state,
                command.BaseRevision,
                null,
                [],
                patch,
                inverse,
                Audit(command, false));
        }

        var revision = state.Revision.Next();
        return new EditorCameraCommandResult(
            true,
            new EditorCameraState(updated, revision),
            command.BaseRevision,
            revision,
            [],
            patch,
            inverse,
            Audit(command, true));
    }

    private static EditorCameraProfile Apply(
        EditorCameraProfile profile,
        NavigateEditorCameraCommand command)
    {
        var pose = profile.Pose;
        return command.Operation switch
        {
            EditorCameraOperation.Orbit => WithPose(profile, new EditorCameraPose(
                pose.Pivot,
                pose.YawDegrees - (command.X * profile.MouseSensitivity),
                Math.Clamp(pose.PitchDegrees - (command.Y * profile.MouseSensitivity), -89f, 89f),
                pose.Distance,
                EditorCameraProjection.Perspective,
                pose.FieldOfView,
                pose.OrthographicSize)),
            EditorCameraOperation.Pan => WithPose(profile, Pan(pose, command.X, command.Y, profile.NavigationSpeed)),
            EditorCameraOperation.Zoom => WithPose(profile, Zoom(pose, command.X, profile.NavigationSpeed)),
            EditorCameraOperation.Focus => command.Target is null
                ? throw new CameraCommandException(TargetMissingCode, "A focus command requires a world-space target.")
                : WithPose(profile, new EditorCameraPose(
                    command.Target.Value,
                    pose.YawDegrees,
                    pose.PitchDegrees,
                    Math.Clamp(Math.Max(0.5f, command.Radius * 2.5f), 0.5f, 500f),
                    pose.Projection,
                    pose.FieldOfView,
                    Math.Clamp(Math.Max(0.5f, command.Radius * 2f), 0.5f, 1000f))),
            EditorCameraOperation.SetView => WithPose(profile, SetView(pose, command.View)),
            EditorCameraOperation.SetSpeed => WithSettings(profile, command.X, profile.MouseSensitivity),
            EditorCameraOperation.SetSensitivity => WithSettings(profile, profile.NavigationSpeed, command.X),
            EditorCameraOperation.SaveBookmark => SaveBookmark(profile, command),
            EditorCameraOperation.RecallBookmark => RecallBookmark(profile, command.BookmarkId),
            _ => throw new CameraCommandException(ValueInvalidCode, $"Camera operation '{command.Operation}' is not supported."),
        };
    }

    private static EditorCameraPose Pan(EditorCameraPose pose, float horizontal, float vertical, float speed)
    {
        var yaw = DegreesToRadians(pose.YawDegrees);
        var scale = Math.Max(0.02f, pose.Distance * 0.0025f) * speed;
        var rightX = MathF.Cos(yaw);
        var rightZ = -MathF.Sin(yaw);
        var forwardX = -MathF.Sin(yaw);
        var forwardZ = -MathF.Cos(yaw);
        var pivot = new Vector3Value(
            pose.Pivot.X + ((rightX * horizontal) + (forwardX * vertical)) * scale,
            pose.Pivot.Y,
            pose.Pivot.Z + ((rightZ * horizontal) + (forwardZ * vertical)) * scale);
        return new EditorCameraPose(
            pivot,
            pose.YawDegrees,
            pose.PitchDegrees,
            pose.Distance,
            pose.Projection,
            pose.FieldOfView,
            pose.OrthographicSize);
    }

    private static EditorCameraPose Zoom(EditorCameraPose pose, float steps, float speed)
    {
        if (!float.IsFinite(steps))
        {
            throw new CameraCommandException(ValueInvalidCode, "Zoom input must be finite.");
        }

        var factor = MathF.Pow(0.85f, steps * Math.Clamp(speed, 0.25f, 4f));
        return new EditorCameraPose(
            pose.Pivot,
            pose.YawDegrees,
            pose.PitchDegrees,
            Math.Clamp(pose.Distance * factor, 0.5f, 500f),
            pose.Projection,
            pose.FieldOfView,
            Math.Clamp(pose.OrthographicSize * factor, 0.5f, 1000f));
    }

    private static EditorCameraPose SetView(EditorCameraPose pose, EditorCameraView view)
    {
        var (yaw, pitch, projection) = view switch
        {
            EditorCameraView.Perspective => (38f, 36f, EditorCameraProjection.Perspective),
            EditorCameraView.Top => (0f, 89f, EditorCameraProjection.Orthographic),
            EditorCameraView.Front => (0f, 0f, EditorCameraProjection.Orthographic),
            EditorCameraView.Back => (180f, 0f, EditorCameraProjection.Orthographic),
            EditorCameraView.Left => (-90f, 0f, EditorCameraProjection.Orthographic),
            EditorCameraView.Right => (90f, 0f, EditorCameraProjection.Orthographic),
            _ => throw new CameraCommandException(ValueInvalidCode, $"Camera view '{view}' is not supported."),
        };
        return new EditorCameraPose(
            pose.Pivot,
            yaw,
            pitch,
            pose.Distance,
            projection,
            pose.FieldOfView,
            pose.OrthographicSize);
    }

    private static EditorCameraProfile SaveBookmark(
        EditorCameraProfile profile,
        NavigateEditorCameraCommand command)
    {
        if (command.BookmarkId is null)
        {
            throw new CameraCommandException(BookmarkMissingCode, "A bookmark command requires a stable bookmark ID.");
        }

        if (string.IsNullOrWhiteSpace(command.BookmarkName))
        {
            throw new CameraCommandException(BookmarkNameMissingCode, "A bookmark name is required.");
        }

        var bookmark = new EditorCameraBookmark(command.BookmarkId, command.BookmarkName, profile.Pose);
        var bookmarks = profile.Bookmarks.Any(existing => existing.Id == bookmark.Id)
            ? profile.Bookmarks.Select(existing => existing.Id == bookmark.Id ? bookmark : existing).ToArray()
            : [.. profile.Bookmarks, bookmark];
        return new EditorCameraProfile(
            profile.Id,
            profile.SchemaVersion,
            profile.NavigationSpeed,
            profile.MouseSensitivity,
            profile.Pose,
            bookmarks,
            profile.Provenance);
    }

    private static EditorCameraProfile RecallBookmark(
        EditorCameraProfile profile,
        EditorCameraBookmarkId? bookmarkId)
    {
        var bookmark = bookmarkId is null
            ? null
            : profile.Bookmarks.FirstOrDefault(candidate => candidate.Id == bookmarkId);
        if (bookmark is null)
        {
            throw new CameraCommandException(BookmarkMissingCode, "The requested camera bookmark does not exist.");
        }

        return WithPose(profile, bookmark.Pose);
    }

    private static EditorCameraProfile WithPose(EditorCameraProfile profile, EditorCameraPose pose) =>
        new(
            profile.Id,
            profile.SchemaVersion,
            profile.NavigationSpeed,
            profile.MouseSensitivity,
            pose,
            profile.Bookmarks,
            profile.Provenance);

    private static EditorCameraProfile WithSettings(
        EditorCameraProfile profile,
        float navigationSpeed,
        float sensitivity) =>
        new(
            profile.Id,
            profile.SchemaVersion,
            navigationSpeed,
            sensitivity,
            profile.Pose,
            profile.Bookmarks,
            profile.Provenance);

    private static float DegreesToRadians(float value) => value * (MathF.PI / 180f);

    private EditorCameraCommandResult Failure(
        EditorCameraState state,
        NavigateEditorCameraCommand command,
        string code,
        string message) =>
        new(
            false,
            state,
            command.BaseRevision,
            null,
            [new CommandDiagnostic(code, message)],
            null,
            null,
            Audit(command, false));

    private AuditEntry Audit(NavigateEditorCameraCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);

    private sealed class CameraCommandException(string code, string message) : Exception(message)
    {
        internal string Code { get; } = code;
    }
}
