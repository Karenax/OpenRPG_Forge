namespace Forge.Domain.Authoring;

public sealed record EditorCameraBookmark
{
    public EditorCameraBookmarkId Id { get; }

    public string Name { get; }

    public EditorCameraPose Pose { get; }

    public EditorCameraBookmark(EditorCameraBookmarkId id, string name, EditorCameraPose pose)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(pose);
        Id = id;
        Name = name.Trim();
        Pose = pose;
    }
}
