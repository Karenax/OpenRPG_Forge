using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;

namespace Forge.Domain.Tests.Projects;

public sealed class ProjectFoundationProfileTests
{
    private static readonly Guid FoundationId = Guid.Parse("d0240000-0000-4000-8000-000000000001");
    private static readonly ProjectId ProjectIdValue =
        ProjectId.From(Guid.Parse("d0240000-0000-4000-8000-000000000002"));
    private static readonly AreaId AreaIdValue =
        AreaId.From(Guid.Parse("d0240000-0000-4000-8000-000000000003"));
    private static readonly EntityId SpawnId =
        EntityId.From(Guid.Parse("d0240000-0000-4000-8000-000000000004"));

    [Fact]
    public void RpgFoundationCarriesStableIdentityChoicesAndPlayerStart()
    {
        var profile = ProjectFoundationProfile.CreateRpgDefault(
            FoundationId, ProjectIdValue, "ember-road", AreaIdValue, SpawnId, "studio.ember-road");

        Assert.Empty(ProjectFoundationValidator.Validate(profile));
        Assert.Equal(ProjectIdValue, profile.Identity.ProjectId);
        Assert.Equal("studio.ember-road", profile.Identity.PackageNamespace);
        Assert.Equal(["forge-core", "forge-rpg-kit"], profile.Kits);
        Assert.True(profile.RequiresInitialArea);
        Assert.True(Assert.Single(profile.PlayerStarts).RequiredAtRuntime);
    }

    [Fact]
    public void InvalidIdentityChoicesAndMissingRpgStartProduceStableDiagnostics()
    {
        var profile = ProjectFoundationProfile.CreateRpgDefault(
            FoundationId, ProjectIdValue, "ember-road", AreaIdValue, SpawnId) with
        {
            Id = Guid.Empty,
            Identity = new(ProjectIdValue, "Bad Slug", "invalid namespace", "future"),
            Kits = ["forge-core", "forge-core"],
            RequiresInitialArea = false,
            PlayerStarts = [],
        };

        var diagnostics = ProjectFoundationValidator.Validate(profile);

        Assert.Contains(diagnostics, item => item.Code == ProjectFoundationValidator.IdentityCode);
        Assert.Contains(diagnostics, item => item.Code == ProjectFoundationValidator.ChoicesCode);
        Assert.Contains(diagnostics, item => item.Code == ProjectFoundationValidator.InitialAreaCode);
    }

    [Fact]
    public void ResolverSelectsSpecificPlayerBeforeDefaultAndReportsRequiredMiss()
    {
        var profile = ProjectFoundationProfile.CreateRpgDefault(
            FoundationId, ProjectIdValue, "ember-road", AreaIdValue, SpawnId);
        var defaultStart = profile.PlayerStarts[0];
        var specific = defaultStart with
        {
            Id = Guid.Parse("d0240000-0000-4000-8000-000000000005"),
            Key = "veteran",
            PlayerSelectionRule = "player:karen",
            Priority = 200,
        };
        profile = profile with { PlayerStarts = [defaultStart, specific] };

        var selected = PlayerStartProfileResolver.Resolve(profile,
            new("karen", "campaign", "adventurer", new HashSet<string>()));
        var missing = PlayerStartProfileResolver.Resolve(profile,
            new("other", "arena", "adventurer", new HashSet<string>()));
        var optional = PlayerStartProfileResolver.Resolve(null,
            new("other", "arena", "adventurer", new HashSet<string>()));

        Assert.Equal(specific.Id, Assert.IsType<PlayerStartProfile>(selected.Profile).Id);
        Assert.False(missing.Succeeded);
        Assert.Equal(ProjectFoundationValidator.PlayerStartReferenceCode, missing.Diagnostic?.Code);
        Assert.True(optional.Succeeded);
        Assert.Null(optional.Profile);
    }
}
