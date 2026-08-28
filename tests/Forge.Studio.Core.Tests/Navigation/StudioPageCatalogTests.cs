using Forge.Studio.Core.Navigation;

namespace Forge.Studio.Core.Tests.Navigation;

public sealed class StudioPageCatalogTests
{
    [Fact]
    public void CatalogDefinesEightSectionsAndThirtySevenUniquePages()
    {
        Assert.Equal(8, StudioPageCatalog.Sections.Count);
        Assert.Equal(37, StudioPageCatalog.Pages.Count);
        Assert.Equal(
            StudioPageCatalog.Sections.Count,
            StudioPageCatalog.Sections.Select(section => section.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            StudioPageCatalog.Pages.Count,
            StudioPageCatalog.Pages.Select(page => page.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(StudioPageCatalog.Sections, section => Assert.NotEmpty(section.Pages));
    }

    [Fact]
    public void SpatialWorkAndSpecializedDocumentsAreBothFirstClassLayouts()
    {
        Assert.Contains(StudioPageCatalog.Pages, page => page.Layout == StudioPageLayout.Spatial);
        Assert.Contains(StudioPageCatalog.Pages, page => page.Layout == StudioPageLayout.Hybrid);
        Assert.Contains(StudioPageCatalog.Pages, page => page.Layout == StudioPageLayout.Specialized);
        Assert.Equal(StudioPageLayout.Spatial, StudioPageCatalog.GetPage("zones").Layout);
        Assert.Equal(StudioPageLayout.Specialized, StudioPageCatalog.GetPage("dialogues").Layout);
        Assert.Equal(StudioPageLayout.Specialized, StudioPageCatalog.GetPage("world-generation").Layout);
    }
}
