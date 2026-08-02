using System.IO.Compression;
using System.Text;
using Forge.ModuleFormat.Packages;

namespace Forge.ModuleFormat.Tests.Packages;

public sealed class ModulePackageTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(),
        "openrpg-forge-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void ExportValidateAndLoad_RoundTripsAtomicallyThroughCache()
    {
        var workspace = CreateWorkspace();
        var package = Path.Combine(_temporaryRoot, "crypt-of-ashes.orpmod");

        var export = ModulePackager.Export(workspace, package);
        var validation = ModulePackageValidator.Validate(package);
        var firstLoad = ModulePackageLoader.Load(package, Path.Combine(_temporaryRoot, "cache"));
        var secondLoad = ModulePackageLoader.Load(package, Path.Combine(_temporaryRoot, "cache"));

        Assert.True(validation.IsValid, JoinDiagnostics(validation));
        Assert.Equal("crypt-of-ashes", validation.Manifest!.Slug);
        Assert.Equal("crypt-of-ashes", firstLoad.Project.Module.Slug);
        Assert.False(firstLoad.CacheHit);
        Assert.True(secondLoad.CacheHit);
        Assert.Equal(firstLoad.CachePath, secondLoad.CachePath);
        Assert.True(File.Exists(Path.Combine(firstLoad.CachePath, "content", "project.json")));
        Assert.True(File.Exists(Path.Combine(firstLoad.CachePath, "content", "scripts", "boss.forge")));
        Assert.Equal(new FileInfo(package).Length, export.ArchiveBytes);
        Assert.Empty(Directory.EnumerateFiles(_temporaryRoot, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void Validate_RejectsPathTraversalFixture()
    {
        var package = ExportValidPackage();
        AddEntry(package, "../escape.json", "{}");

        var validation = ModulePackageValidator.Validate(package);

        Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Code == ModulePackageValidator.UnsafePathCode);
    }

    [Fact]
    public void Validate_RejectsZipBombRatioFixture()
    {
        var package = ExportValidPackage();
        AddEntry(package, "content/assets/repetitive.txt", new string('0', 1024 * 1024));

        var validation = ModulePackageValidator.Validate(
            package,
            new ModulePackageLimits(MaximumCompressionRatio: 20));

        Assert.Contains(validation.Diagnostics, diagnostic =>
            diagnostic.Code == ModulePackageValidator.CompressionRatioCode);
    }

    [Fact]
    public void Validate_RejectsNativeDllFixture()
    {
        var package = ExportValidPackage();
        AddEntry(package, "content/plugins/hostile.dll", "MZ-hostile");

        var validation = ModulePackageValidator.Validate(package);

        Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Code == ModulePackageValidator.ForbiddenFileCode);
    }

    [Fact]
    public void Validate_RejectsHashMismatchFixture()
    {
        var package = ExportValidPackage();
        ReplaceEntry(package, "content/scripts/boss.forge", "void main() { say(\"tampered\"); }");

        var validation = ModulePackageValidator.Validate(package);

        Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Code == ModulePackageValidator.HashMismatchCode);
    }

    [Fact]
    public void Validate_RejectsDuplicateBlueprintIdsFixture()
    {
        var package = ExportValidPackage();
        using (var archive = ZipFile.Open(package, ZipArchiveMode.Update))
        {
            var projectEntry = archive.GetEntry("content/project.json")!;
            string json;
            using (var reader = new StreamReader(projectEntry.Open()))
            {
                json = reader.ReadToEnd();
            }

            projectEntry.Delete();
            json = json.Replace(
                "10000000-0000-4000-8000-000000000002",
                "10000000-0000-4000-8000-000000000001",
                StringComparison.Ordinal);
            var replacement = archive.CreateEntry("content/project.json", CompressionLevel.Optimal);
            using var writer = new StreamWriter(replacement.Open(), new UTF8Encoding(false));
            writer.Write(json);
        }

        var validation = ModulePackageValidator.Validate(package);

        Assert.Contains(validation.Diagnostics, diagnostic => diagnostic.Code == ModulePackageValidator.InvalidProjectCode);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot))
        {
            Directory.Delete(_temporaryRoot, true);
        }
    }

    private string ExportValidPackage()
    {
        var package = Path.Combine(_temporaryRoot, $"{Guid.NewGuid():N}.orpmod");
        ModulePackager.Export(CreateWorkspace(), package);
        return package;
    }

    private string CreateWorkspace()
    {
        var workspace = Path.Combine(_temporaryRoot, $"workspace-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(workspace, "scripts"));
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "crypt-of-ashes.project.json"),
            Path.Combine(workspace, "project.json"));
        File.WriteAllText(
            Path.Combine(workspace, "scripts", "boss.forge"),
            "void main() { say(\"Braise\"); }",
            new UTF8Encoding(false));
        return workspace;
    }

    private static void AddEntry(string package, string path, string content)
    {
        using var archive = ZipFile.Open(package, ZipArchiveMode.Update);
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static void ReplaceEntry(string package, string path, string content)
    {
        using var archive = ZipFile.Open(package, ZipArchiveMode.Update);
        archive.GetEntry(path)!.Delete();
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string JoinDiagnostics(ModulePackageValidationResult validation) =>
        string.Join(Environment.NewLine, validation.Diagnostics.Select(diagnostic => diagnostic.Message));
}
