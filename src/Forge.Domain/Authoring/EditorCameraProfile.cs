using Forge.Domain.Spatial;

namespace Forge.Domain.Authoring;

public sealed record EditorCameraProfile
{
    public const int CurrentSchemaVersion = 1;

    public EditorCameraProfileId Id { get; }

    public int SchemaVersion { get; }

    public float NavigationSpeed { get; }

    public float MouseSensitivity { get; }

    public EditorCameraPose Pose { get; }

    public IReadOnlyList<EditorCameraBookmark> Bookmarks { get; }

    public string Provenance { get; }

    public EditorCameraProfile(
        EditorCameraProfileId id,
        int schemaVersion,
        float navigationSpeed,
        float mouseSensitivity,
        EditorCameraPose pose,
        IReadOnlyList<EditorCameraBookmark>? bookmarks = null,
        string provenance = "creator-preferences")
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(pose);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), $"Camera profile schema {schemaVersion} is not supported.");
        }

        if (!float.IsFinite(navigationSpeed) || navigationSpeed is < 0.1f or > 20f)
        {
            throw new ArgumentOutOfRangeException(nameof(navigationSpeed), "Navigation speed must remain between 0.1 and 20.");
        }

        if (!float.IsFinite(mouseSensitivity) || mouseSensitivity is < 0.05f or > 5f)
        {
            throw new ArgumentOutOfRangeException(nameof(mouseSensitivity), "Mouse sensitivity must remain between 0.05 and 5.");
        }

        var materializedBookmarks = (bookmarks ?? []).ToArray();
        if (materializedBookmarks.Select(bookmark => bookmark.Id).Distinct().Count() != materializedBookmarks.Length)
        {
            throw new ArgumentException("A camera profile cannot contain duplicate bookmark IDs.", nameof(bookmarks));
        }

        Id = id;
        SchemaVersion = schemaVersion;
        NavigationSpeed = navigationSpeed;
        MouseSensitivity = mouseSensitivity;
        Pose = pose;
        Bookmarks = materializedBookmarks.OrderBy(bookmark => bookmark.Name, StringComparer.Ordinal).ToArray();
        Provenance = provenance.Trim();
    }

    public static EditorCameraProfile CreateDefault(EditorCameraProfileId id) =>
        new(
            id,
            CurrentSchemaVersion,
            1,
            0.25f,
            new EditorCameraPose(
                Vector3Value.Zero,
                38,
                36,
                26,
                EditorCameraProjection.Perspective,
                55,
                20));
}
