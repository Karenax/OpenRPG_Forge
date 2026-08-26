using Forge.Application.Gameplay;

namespace Forge.ModuleFormat.Tests.RuntimePresentation;

public sealed class RuntimeModuleAssetPathResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"openrpg-portrait-root-{Guid.NewGuid():N}");
    private readonly string _outside = Path.Combine(Path.GetTempPath(), $"openrpg-portrait-outside-{Guid.NewGuid():N}");

    public RuntimeModuleAssetPathResolverTests()
    {
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(_outside);
    }

    [Fact]
    public void ResolvesExistingImageOnlyInsideCanonicalModuleRoot()
    {
        var portraits = Directory.CreateDirectory(Path.Combine(_root, "portraits"));
        var image = Path.Combine(portraits.FullName, "hero.png");
        File.WriteAllBytes(image, [1, 2, 3]);

        var resolved = RuntimeModuleAssetPathResolver.ResolveImage(_root, "portraits/hero.png");

        Assert.Equal(Path.GetFullPath(image), resolved);
    }

    [Theory]
    [InlineData("../escape.png")]
    [InlineData("res://portraits/hero.png")]
    [InlineData("user://portraits/hero.png")]
    [InlineData("https://example.test/hero.png")]
    [InlineData("portraits/hero.txt")]
    [InlineData("portraits/hero.png\0ignored")]
    public void RejectsTraversalSchemesUnsupportedExtensionsAndMalformedPaths(string authoredPath)
    {
        Assert.Null(RuntimeModuleAssetPathResolver.ResolveImage(_root, authoredPath));
    }

    [Fact]
    public void RejectsAbsolutePathOutsideModuleRoot()
    {
        var outsideImage = Path.Combine(_outside, "outside.png");
        File.WriteAllBytes(outsideImage, [1, 2, 3]);

        Assert.Null(RuntimeModuleAssetPathResolver.ResolveImage(_root, outsideImage));
    }

    [Fact]
    public void RejectsFileSymlinkEscapingModuleRoot()
    {
        var outsideImage = Path.Combine(_outside, "outside.png");
        File.WriteAllBytes(outsideImage, [1, 2, 3]);
        var link = Path.Combine(_root, "linked.png");
        try
        {
            File.CreateSymbolicLink(link, outsideImage);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            return;
        }

        Assert.Null(RuntimeModuleAssetPathResolver.ResolveImage(_root, "linked.png"));
    }

    [Fact]
    public void RejectsDirectoryLinkEscapingModuleRoot()
    {
        var outsideImage = Path.Combine(_outside, "outside.png");
        File.WriteAllBytes(outsideImage, [1, 2, 3]);
        var link = Path.Combine(_root, "portraits");
        try
        {
            Directory.CreateSymbolicLink(link, _outside);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            return;
        }

        Assert.Null(RuntimeModuleAssetPathResolver.ResolveImage(_root, "portraits/outside.png"));
    }

    public void Dispose()
    {
        TryDelete(_root);
        TryDelete(_outside);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Test cleanup must not hide the security assertion result.
        }
    }
}
