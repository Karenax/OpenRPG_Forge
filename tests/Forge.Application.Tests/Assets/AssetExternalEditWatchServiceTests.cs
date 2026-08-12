using Forge.Application.Assets;
using Forge.Domain.Assets;

namespace Forge.Application.Tests.Assets;

public sealed class AssetExternalEditWatchServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "OpenRPG_Forge", "external-edit-watch", Guid.NewGuid().ToString("N"));

    [Fact]
    public void WatcherProducesTypedDryRunMetadataDiffAndAcknowledgesChange()
    {
        var source = Path.Combine(_root, "normalized", "tree.obj");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.WriteAllText(source, "v 0 0 0\n");
        var configuration = AssetExternalEditCommandTests.Configuration() with
        {
            SourceRelativePath = "normalized/tree.obj",
            SourceContentSha256 = new string('a', 64),
        };
        var opened = AssetExternalEditWatchService.Open(_root, configuration);
        using var watcher = opened.Service;

        File.AppendAllText(source, "v 1 0 0\n");
        var plan = watcher!.Poll(dryRun: true);

        Assert.True(opened.Succeeded);
        Assert.NotNull(plan);
        Assert.True(plan.DryRun);
        Assert.Contains(plan.MetadataChanges, change => change.Field == "contentSha256");
        Assert.Contains(plan.MetadataChanges, change => change.Field == "bytes");
        Assert.Equal(configuration.Operations, plan.Operations);
        Assert.Equal(configuration.OutputRelativePath, plan.OutputRelativePath);
        Assert.False(File.Exists(Path.Combine(_root, configuration.OutputRelativePath)));
        watcher.Acknowledge();
        Assert.Null(watcher.Poll());
    }

    [Fact]
    public void MissingSourceReturnsStableLocalizedDiagnostic()
    {
        Directory.CreateDirectory(_root);

        var opened = AssetExternalEditWatchService.Open(_root, AssetExternalEditCommandTests.Configuration());

        Assert.False(opened.Succeeded);
        var diagnostic = Assert.Single(opened.Diagnostics);
        Assert.Equal(AssetExternalEditWatchService.SourceMissingCode, diagnostic.Code);
        Assert.EndsWith("/sourceRelativePath", diagnostic.Path, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
