using Forge.Studio.Core.Navigation;

namespace Forge.Studio.Core.Tests.Navigation;

public sealed class StudioNavigationOverlaySnapshotTests
{
    [Fact]
    public void Snapshot_orders_human_primitives_without_engine_dependencies()
    {
        var snapshot = new StudioNavigationOverlaySnapshot(
            [new("marker:b", "Sortie", StudioNavigationOverlaySemantic.Anchor, new(2, 0, 0)),
             new("marker:a", "Départ", StudioNavigationOverlaySemantic.Anchor, new(0, 0, 0))],
            [new("segment:a", "Lien", StudioNavigationOverlaySemantic.OffMeshLink,
                new(0, 0, 0), new(2, 0, 0), Directed: true)],
            [new("surface:a", "Surface marchable", StudioNavigationOverlaySemantic.Surface,
                [new(0, 0, 0), new(2, 0, 0), new(0, 0, 2)])]);

        Assert.Equal(new[] { "marker:a", "marker:b" }, snapshot.Markers.Select(item => item.StableKey));
        Assert.True(snapshot.Segments[0].Directed);
        Assert.Equal(3, snapshot.Polygons[0].Points.Count);
    }

    [Fact]
    public void Snapshot_rejects_duplicate_keys_and_non_finite_points()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StudioNavigationPoint(double.NaN, 0, 0));
        Assert.Throws<ArgumentException>(() => new StudioNavigationOverlaySnapshot(
            [new("same", "Ancre", StudioNavigationOverlaySemantic.Anchor, new(0, 0, 0))],
            [new("same", "Lien", StudioNavigationOverlaySemantic.Route, new(0, 0, 0), new(1, 0, 0))],
            []));
    }
}
