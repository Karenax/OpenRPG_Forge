using System.Text.RegularExpressions;
using Forge.Studio.Core.Verification;

namespace Forge.Studio.Core.Tests.Verification;

public sealed class StudioSmokeSelectionTests
{
    [Fact]
    public void MissingScopePreservesTheHistoricalFullSmoke()
    {
        Assert.True(StudioSmokeSelection.TryCreate(null, "ux1-aa", "zones", out var selection, out var error));

        Assert.Empty(error);
        Assert.Equal(StudioSmokeScope.Full, selection.Scope);
        Assert.True(selection.Includes("ux0-a"));
        Assert.True(selection.Includes("ux9-o"));
        Assert.True(selection.Includes("ux10"));
        Assert.Equal(180, StudioSmokeSelection.KnownRouteCount);
    }

    [Fact]
    public void RouteRunsTheEditingBaselineAndOnlyTheCanonicalRequestedRoute()
    {
        Assert.True(StudioSmokeSelection.TryCreate("Route", "ux1-aa-verbs", "zones", out var selection, out _));

        Assert.Equal("ux1-aa", selection.Route);
        Assert.Equal("ux1", selection.Wave);
        Assert.True(selection.Includes("edit"));
        Assert.True(selection.Includes("ux1-aa"));
        Assert.False(selection.Includes("ux1-a"));
        Assert.False(selection.Includes("ux2-a"));
    }

    [Fact]
    public void WaveRunsEveryRouteInTheRequestedUxWave()
    {
        Assert.True(StudioSmokeSelection.TryCreate("Wave", "ux6-o-analysis", "scripts", out var selection, out _));

        Assert.Equal("ux6", selection.Wave);
        Assert.True(selection.Includes("edit"));
        Assert.True(selection.Includes("ux6-a"));
        Assert.True(selection.Includes("ux6-z"));
        Assert.False(selection.Includes("ux7-a"));
    }

    [Theory]
    [InlineData("ux2-area-034-lod", "ux2-env-j")]
    [InlineData("ux2-wpt-012", "ux2-nav-l")]
    [InlineData("ux2-encounter-018", "ux2-lw-k")]
    [InlineData("ux5-f-cinematics", "ux5-b")]
    [InlineData("ux10-d-creative", "ux10")]
    public void HistoricalEvidenceAliasesResolveToTheImplementedSmokeRoute(string evidence, string expected)
    {
        Assert.Equal(expected, StudioSmokeSelection.CanonicalizeRoute(evidence));
    }

    [Fact]
    public void NonUxEvidenceUsesTheEditingBaseline()
    {
        Assert.True(StudioSmokeSelection.TryCreate("Route", "gizmo", "zones", out var selection, out _));

        Assert.Equal("edit", selection.Route);
        Assert.True(selection.Includes("edit"));
        Assert.False(selection.Includes("ux0-a"));
    }

    [Theory]
    [InlineData("Nearby", "ux1-a")]
    [InlineData("Route", "ux11-a")]
    [InlineData("Route", "ux3-z")]
    public void InvalidScopeOrUxRouteFailsClosed(string scope, string evidence)
    {
        Assert.False(StudioSmokeSelection.TryCreate(scope, evidence, "zones", out _, out var error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void RouteInventoryMatchesEveryStudioRootSmokeBranch()
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null &&
               !File.Exists(Path.Combine(repository.FullName, "OpenRPGForge.sln")))
        {
            repository = repository.Parent;
        }

        Assert.NotNull(repository);
        var studioRootPath = Path.Combine(repository.FullName, "apps", "Forge.Studio", "StudioRoot.cs");
        var source = File.ReadAllText(studioRootPath);
        var routes = Regex.Matches(source, "ShouldRunSmoke\\(\\\"(?<route>[^\\\"]+)\\\"\\)")
            .Select(match => match.Groups["route"].Value)
            .ToArray();

        Assert.Equal(routes.Length, routes.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(StudioSmokeSelection.KnownRouteCount, routes.Length);
        Assert.All(routes, route => Assert.Equal(route, StudioSmokeSelection.CanonicalizeRoute(route)));
    }
}
