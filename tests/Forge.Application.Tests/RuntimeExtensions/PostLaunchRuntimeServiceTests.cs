using Forge.Application.RuntimeExtensions;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;

namespace Forge.Application.Tests.RuntimeExtensions;

public sealed class PostLaunchRuntimeServiceTests
{
    [Fact]
    public void Summary_exposes_typed_optional_capabilities_without_engine_dependency()
    {
        var project = CreateProject();
        var summary = PostLaunchRuntimeService.Summarize(project);

        Assert.True(summary.MediaGeneration);
        Assert.True(summary.SoakTests);
        Assert.Equal(1, summary.Achievements);
        Assert.Equal("test", summary.Provenance);
    }

    [Fact]
    public void Media_plan_requires_rights_stages_candidate_and_supports_explicit_fallback()
    {
        var project = CreateProject();
        var missingRights = PostLaunchRuntimeService.PlanMediaGeneration(project, Guid.NewGuid(), "image",
            "local", "", "", 1024, providerAvailable: true);
        var planned = PostLaunchRuntimeService.PlanMediaGeneration(project, Guid.NewGuid(), "image",
            "local", "CC0-1.0", "author-confirmed", 1024, providerAvailable: true);
        var fallback = PostLaunchRuntimeService.PlanMediaGeneration(project, Guid.NewGuid(), "audio",
            "local", "CC0-1.0", "author-confirmed", 1024, providerAvailable: false);

        Assert.Equal("POST_MEDIA_RIGHTS_REQUIRED", missingRights.Code);
        Assert.True(planned.Succeeded);
        Assert.StartsWith(".forge/media-staging/", planned.StagingPath, StringComparison.Ordinal);
        Assert.True(planned.RequiresReview);
        Assert.True(fallback.UsesModuleIndependentFallback);
    }

    private static ProjectDefinition CreateProject()
    {
        var id = ModuleId.From(Guid.Parse("42000000-0000-4000-8000-000000000001"));
        var module = new ModuleDefinition(id, "post-launch", "Post launch", string.Empty,
            Forge.Domain.Areas.AreaId.From(Guid.Parse("42000000-0000-4000-8000-000000000002")));
        var extensions = module.RuntimePolicies.Extensions;
        var source = extensions.PostLaunch;
        var media = new ProductionAutomationProfile(source.Production.Id, 1,
            source.Production.MediaGeneration with { Enabled = true, Providers = ["local"] },
            source.Production.SoakTests with { Enabled = true }, "test");
        var gameplay = new GameplayProductionProfile(source.Gameplay.Id, 1, source.Gameplay.ZeroGravity,
            source.Gameplay.Vehicles, source.Gameplay.Mounts,
            [new GameplayProductionProfile.AchievementDefinition(Guid.Parse("42000000-0000-4000-8000-000000000003"),
                "first", "module.started", 1, null, false)], source.Gameplay.CuratedPacks, "test");
        var post = new PostLaunchCapabilityProfile(source.Id, 1, gameplay, source.LiveDirector, source.Network,
            source.CreatorExperience, media, "test");
        var updatedExtensions = new ModuleRuntimeExtensionProfile(extensions.Id, 1, extensions.RuleSchemas,
            extensions.UiDocuments, extensions.JsonDocuments, extensions.SnapshotPolicy, "test",
            extensions.ModernTooling, extensions.OperationalReadiness, post);
        var policies = new Forge.Domain.RuntimePolicies.ModuleRuntimePolicyProfile(module.RuntimePolicies.Id, 1,
            module.RuntimePolicies.Heartbeats, module.RuntimePolicies.Rest, module.RuntimePolicies.SequenceAbort,
            "test", module.RuntimePolicies.Cinematics, module.RuntimePolicies.Gameplay,
            module.RuntimePolicies.Rules, updatedExtensions);
        var updated = module.WithRuntimePolicies(policies);
        return new ProjectDefinition(Forge.Domain.Projects.ProjectId.From(Guid.NewGuid()), "Post launch", updated,
            [], [], [], []);
    }
}
