using System.Text;
using Forge.ModuleFormat.Documentation;

namespace Forge.ModuleFormat.Tests.Documentation;

public sealed class ForgeDocumentationGeneratorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "openrpg-forge-docs-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Generator_writes_six_deterministic_catalogs_and_check_detects_drift()
    {
        var tracker = Path.Combine(_root, "tracking", "project"); Directory.CreateDirectory(tracker);
        File.WriteAllText(Path.Combine(tracker, "AURORA_PARITY_COVERAGE.csv"),
            "id;domain;name;priority;layer;status;source;evidence;tests;depends;gate;notes\n" +
            "\"ORF-TEST-001\";\"TEST\";\"Generated docs\";\"P1\";\"Tooling\";\"Tested\";\"source\";\"evidence\";\"tests\";\"\";\"G11\";\"note\"\n", Encoding.UTF8);

        var generated = ForgeDocumentationGenerator.Generate(_root, "docs/generated", checkOnly: false);
        var current = ForgeDocumentationGenerator.Generate(_root, "docs/generated", checkOnly: true);
        File.AppendAllText(Path.Combine(_root, "docs", "generated", "coverage.md"), "drift", Encoding.UTF8);
        var stale = ForgeDocumentationGenerator.Generate(_root, "docs/generated", checkOnly: true);

        Assert.Equal(6, generated.Files.Count);
        Assert.True(current.Current);
        Assert.False(stale.Current);
        Assert.Contains("coverage.md", stale.ChangedFiles);
        Assert.Throws<ArgumentException>(() => ForgeDocumentationGenerator.Generate(_root, "../outside", false));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
