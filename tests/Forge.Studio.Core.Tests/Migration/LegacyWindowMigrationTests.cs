using System.Text.RegularExpressions;
using Forge.Studio.Core.Navigation;

namespace Forge.Studio.Core.Tests.Migration;

public sealed class LegacyWindowMigrationTests
{
    private static readonly Regex WindowDeclaration = new(
        @"partial\s+class\s+(?<name>\w+)\s*:\s*Window",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void MigrationMatrixCoversEveryLegacyWindowExactlyOnce()
    {
        var repositoryRoot = FindRepositoryRoot();
        var mappings = ReadMappings(repositoryRoot);
        var legacyWindows = Directory
            .EnumerateFiles(
                Path.Combine(repositoryRoot, "apps", "Forge.Toolset", "addons", "openrpg_forge"),
                "*.cs",
                SearchOption.AllDirectories)
            .SelectMany(path => WindowDeclaration.Matches(File.ReadAllText(path)))
            .Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(66, legacyWindows.Count);
        Assert.Equal(66, mappings.Count);
        Assert.Equal(66, mappings.Select(mapping => mapping.LegacyWindow).Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(legacyWindows.Except(mappings.Select(mapping => mapping.LegacyWindow), StringComparer.Ordinal));
        Assert.Empty(mappings.Select(mapping => mapping.LegacyWindow).Except(legacyWindows, StringComparer.Ordinal));
    }

    [Fact]
    public void EveryMigrationDestinationExistsInTheStudioCatalog()
    {
        var mappings = ReadMappings(FindRepositoryRoot());
        var pages = StudioPageCatalog.Pages.Select(page => page.Id).ToHashSet(StringComparer.Ordinal);

        Assert.All(mappings, mapping =>
        {
            Assert.Contains(mapping.PageId, pages);
            Assert.Equal(mapping.SectionId, StudioPageCatalog.SectionForPage(mapping.PageId).Id);
            Assert.Equal("Legacy", mapping.Status);
        });
    }

    private static IReadOnlyList<MigrationRow> ReadMappings(string repositoryRoot)
    {
        var path = Path.Combine(repositoryRoot, "tracking", "studio", "CREATOR_WINDOW_MIGRATION.csv");
        return File.ReadLines(path)
            .Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Split(';'))
            .Select(fields =>
            {
                Assert.Equal(7, fields.Length);
                return new MigrationRow(fields[0], fields[1], fields[2], fields[5]);
            })
            .ToArray();
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("The OpenRPG Forge repository root was not found.");
    }

    private sealed record MigrationRow(
        string LegacyWindow,
        string SectionId,
        string PageId,
        string Status);
}
