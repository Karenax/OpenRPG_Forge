using Forge.Domain.Authoring;
using Forge.Domain.Spatial;

namespace Forge.Domain.Tests.Authoring;

public sealed class EditorCameraProfileTests
{
    private static readonly EditorCameraProfileId ProfileId =
        EditorCameraProfileId.From(Guid.Parse("ca000000-0000-4000-8000-000000000001"));

    [Fact]
    public void DefaultProfileIsStableVersionedAndPerspective()
    {
        var profile = EditorCameraProfile.CreateDefault(ProfileId);

        Assert.Equal(ProfileId, profile.Id);
        Assert.Equal(EditorCameraProfile.CurrentSchemaVersion, profile.SchemaVersion);
        Assert.Equal(EditorCameraProjection.Perspective, profile.Pose.Projection);
        Assert.InRange(profile.Pose.PitchDegrees, -89.5f, 89.5f);
        Assert.Empty(profile.Bookmarks);
        Assert.Equal(EditorCameraPilotMode.Fly, profile.Input.PilotMode);
        Assert.Equal("W", profile.Input.Bindings["forward"]);
    }

    [Fact]
    public void ProfileRejectsDuplicateBookmarkIdsAndInvalidSpeed()
    {
        var pose = EditorCameraProfile.CreateDefault(ProfileId).Pose;
        var bookmarkId = EditorCameraBookmarkId.From(Guid.Parse("cb000000-0000-4000-8000-000000000001"));
        var bookmark = new EditorCameraBookmark(bookmarkId, "Vue", pose);

        Assert.Throws<ArgumentException>(() => new EditorCameraProfile(
            ProfileId,
            EditorCameraProfile.CurrentSchemaVersion,
            1,
            0.25f,
            pose,
            [bookmark, bookmark]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EditorCameraProfile(
            ProfileId,
            EditorCameraProfile.CurrentSchemaVersion,
            0,
            0.25f,
            pose));
    }

    [Fact]
    public void PoseNormalizesYawAndRejectsUnsafeRanges()
    {
        var pose = new EditorCameraPose(Vector3Value.Zero, 450, 30, 12);

        Assert.Equal(90, pose.YawDegrees);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EditorCameraPose(Vector3Value.Zero, 0, 90, 12));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EditorCameraPose(Vector3Value.Zero, 0, 30, 0.1f));
    }
}
