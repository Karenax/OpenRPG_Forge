using Forge.Domain.Quality;
using Forge.ModuleFormat.Quality;

namespace Forge.ModuleFormat.Tests.Quality;

public sealed class QualityFoundationProfileStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "openrpg-quality-foundation-tests",
        Guid.NewGuid().ToString("N"));
    private readonly Guid _owner = Guid.Parse("30000000-0000-4000-8000-000000000001");

    public QualityFoundationProfileStoreTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void MissingProfileDefaultsThenRoundTripsAsCanonicalUtf8WithoutBom()
    {
        var missing = QualityFoundationProfileStore.Load(_root, _owner);
        Assert.True(missing.FileMissing);
        Assert.True(missing.Succeeded);

        var unsorted = missing.Profile with
        {
            Tests = missing.Profile.Tests with { HeadlessScenarios = ["generic-core", "crypt-of-ashes"] },
            Logs = missing.Profile.Logs with { Categories = ["security", "build", "runtime"] },
        };
        Assert.True(QualityFoundationProfileStore.Save(_root, unsorted).Succeeded);
        var path = Path.Combine(_root, QualityFoundationProfileStore.RelativePath);
        var first = File.ReadAllBytes(path);
        var loaded = QualityFoundationProfileStore.Load(_root, _owner);

        Assert.True(loaded.Succeeded);
        Assert.False(loaded.FileMissing);
        Assert.Equal(["build", "runtime", "security"], loaded.Profile.Logs.Categories);
        Assert.Equal(unsorted.EffectiveValidation, loaded.Profile.EffectiveValidation);
        Assert.True(QualityFoundationProfileStore.Save(_root, loaded.Profile).Succeeded);
        Assert.Equal(first, File.ReadAllBytes(path));
        Assert.False(first.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }));
        Assert.Empty(Directory.EnumerateFiles(_root, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void InvalidJsonReturnsDefaultAndStableDiagnostic()
    {
        var path = Path.Combine(_root, QualityFoundationProfileStore.RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{invalid");

        var loaded = QualityFoundationProfileStore.Load(_root, _owner);

        Assert.False(loaded.Succeeded);
        Assert.False(loaded.FileMissing);
        Assert.Equal(QualityFoundationProfileStore.ReadFailedCode, Assert.Single(loaded.Diagnostics).Code);
        Assert.Empty(QualityFoundationProfileValidator.Validate(loaded.Profile));
    }

    [Fact]
    public void LegacyProfileWithoutValidationUsesSafeCanonicalDefault()
    {
        var profile = QualityFoundationProfile.CreateDefault(_owner);
        Assert.True(QualityFoundationProfileStore.Save(_root, profile).Succeeded);
        var path = Path.Combine(_root, QualityFoundationProfileStore.RelativePath);
        var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        document.Remove("validation");
        File.WriteAllText(path, document.ToJsonString());

        var loaded = QualityFoundationProfileStore.Load(_root, _owner);

        Assert.True(loaded.Succeeded);
        Assert.Equal(5, loaded.Profile.EffectiveValidation.Categories.Count);
        Assert.Empty(QualityFoundationProfileValidator.Validate(loaded.Profile));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
