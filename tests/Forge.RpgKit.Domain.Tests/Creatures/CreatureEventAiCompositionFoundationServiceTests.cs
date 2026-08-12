using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.RpgKit.Application.Creatures;
using Forge.RpgKit.Domain.Creatures;

namespace Forge.RpgKit.Domain.Tests.Creatures;

public sealed class CreatureEventAiCompositionFoundationServiceTests
{
    [Fact]
    public void CanonicalProfileMapsToValidatedRpgKitControllerAndSevenBindings()
    {
        var source = CreatureFoundationProfile.CreateDefault(Guid.NewGuid(), "ash-warden", "Gardien", "test:r080");
        var profile = CreatureEventAiCompositionProfile.CreateDefault(Guid.NewGuid(), "test:r080");
        var foundation = new CreatureFoundationProfile(source.Id, source.SchemaVersion, source.Provenance,
            source.RequiredAtRuntime, source.Identity, source.DefaultTransform, source.Resources, source.Movement,
            source.References, source.Spawned, source.Overrides, eventAiComposition: profile,
            eventAiCompositionRequiredAtRuntime: true);
        var blueprintId = BlueprintId.From(Guid.Parse("a1801000-0000-4000-8000-000000000001"));
        var area = new AreaDefinition(AreaId.From(Guid.Parse("a1801000-0000-4000-8000-000000000010")),
            "crypt", "Crypte", []);
        var project = new ProjectDefinition(ProjectId.From(Guid.NewGuid()), "R080",
            [new BlueprintDefinition(blueprintId, source.Identity.Slug, "Gardien", BlueprintKind.Creature,
                creatureFoundation: foundation)], [area]);

        var result = CreatureEventAiCompositionFoundationService.Resolve(project, blueprintId);

        Assert.True(result.Succeeded);
        Assert.Equal(AiControllerKind.StateMachine, result.Controller!.Kind);
        Assert.Equal(2, result.Controller.Blackboard.Count);
        Assert.Equal(7, CreatureEventAiCompositionFoundationService.EnumerateBindings(result).Length);
        Assert.Equal("idle", CreatureAiService.Decide(result.Controller,
            new Dictionary<string, double> { ["patrol"] = 1 }).Action);
    }

    [Fact]
    public void OptionalAndRequiredMissingProfilesRemainExplicit()
    {
        var source = CreatureFoundationProfile.CreateDefault(Guid.NewGuid(), "ash-warden", "Gardien", "test:r080");
        ProjectDefinition Project(bool required)
        {
            var foundation = new CreatureFoundationProfile(source.Id, source.SchemaVersion, source.Provenance,
                source.RequiredAtRuntime, source.Identity, source.DefaultTransform, source.Resources,
                source.Movement, source.References, source.Spawned, source.Overrides,
                eventAiCompositionRequiredAtRuntime: required);
            var area = new AreaDefinition(AreaId.From(Guid.Parse("a1801000-0000-4000-8000-000000000011")),
                "crypt", "Crypte", []);
            return new ProjectDefinition(ProjectId.From(Guid.NewGuid()), "R080",
                [new BlueprintDefinition(BlueprintId.From(Guid.Parse("a1801000-0000-4000-8000-000000000002")),
                    source.Identity.Slug, "Gardien", BlueprintKind.Creature, creatureFoundation: foundation)], [area]);
        }
        var blueprintId = BlueprintId.From(Guid.Parse("a1801000-0000-4000-8000-000000000002"));

        var optional = CreatureEventAiCompositionFoundationService.Resolve(Project(false), blueprintId);
        var required = CreatureEventAiCompositionFoundationService.Resolve(Project(true), blueprintId);

        Assert.True(optional.Succeeded);
        Assert.True(optional.UsedFallback);
        Assert.Equal(CreatureEventAiCompositionValidator.MissingRequiredCode,
            Assert.Single(required.Diagnostics).Code);
    }
}
