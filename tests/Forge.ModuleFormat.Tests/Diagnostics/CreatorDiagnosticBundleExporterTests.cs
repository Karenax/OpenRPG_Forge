using System.IO.Compression;
using System.Text.Json;
using Forge.Application.Creator;
using Forge.ModuleFormat.Diagnostics;

namespace Forge.ModuleFormat.Tests.Diagnostics;

public sealed class CreatorDiagnosticBundleExporterTests
{
    [Fact]
    public void ExportIsDeterministicAtomicStructuredAndSanitized()
    {
        var workspace = CreateWorkspace();
        try
        {
            var diagnostics = new[]
            {
                CreatorDiagnostic.Create(CreatorDiagnosticSeverity.Warning, "area.navigation.portal_missing",
                    "Portail introuvable", "areas/100/portals/200", "build:42"),
                CreatorDiagnostic.Create(CreatorDiagnosticSeverity.Error, "project.reference.missing",
                    "Référence introuvable", "project.json/references/0", "build:42"),
            };

            var first = CreatorDiagnosticBundleExporter.Export(workspace, diagnostics,
                "runtime ready\npassword=hunter2\nplayerId=Karen");
            var second = CreatorDiagnosticBundleExporter.Export(workspace, diagnostics.Reverse().ToArray(),
                "runtime ready\r\npassword=hunter2\r\nplayerId=Karen");

            Assert.Equal(first.Id, second.Id);
            Assert.Equal(first.Sha256, second.Sha256);
            Assert.True(second.Reused);
            Assert.Equal(2, first.DiagnosticCount);
            Assert.True(first.IncludesRuntimeLog);
            Assert.StartsWith(".forge/diagnostics/diagnostics-", first.RelativePath, StringComparison.Ordinal);
            using var archive = ZipFile.OpenRead(Path.Combine(workspace,
                first.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            Assert.Equal(["diagnostics.json", "manifest.json", "runtime.log"],
                archive.Entries.Select(item => item.FullName).Order(StringComparer.Ordinal));
            using var logReader = new StreamReader(archive.GetEntry("runtime.log")!.Open());
            var log = logReader.ReadToEnd();
            Assert.DoesNotContain("hunter2", log, StringComparison.Ordinal);
            Assert.DoesNotContain("Karen", log, StringComparison.Ordinal);
            Assert.Contains("password=[REDACTED]", log, StringComparison.Ordinal);
            Assert.Contains("playerId=[PLAYER_REDACTED]", log, StringComparison.Ordinal);
            using var manifest = JsonDocument.Parse(archive.GetEntry("manifest.json")!.Open());
            Assert.Equal(first.Id, manifest.RootElement.GetProperty("id").GetGuid());
            Assert.Equal(1, manifest.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(2, manifest.RootElement.GetProperty("diagnosticCount").GetInt32());
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public void ExportRejectsUnsafeRelativeDirectoryBeforeWriting()
    {
        var workspace = CreateWorkspace();
        try
        {
            var exception = Assert.Throws<ArgumentException>(() => CreatorDiagnosticBundleExporter.Export(
                workspace, [], null, "../outside"));
            Assert.Contains(CreatorDiagnosticBundleExporter.UnsafePathCode, exception.Message, StringComparison.Ordinal);
            Assert.Empty(Directory.EnumerateFileSystemEntries(workspace));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    private static string CreateWorkspace()
    {
        var path = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "diagnostic-bundle-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
