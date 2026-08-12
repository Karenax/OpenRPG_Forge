using Forge.Domain.Creatures;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Lifecycle;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.Domain.Tests.Creatures;

public sealed class CreatureFoundationProfileTests
{
    [Theory]
    [InlineData(CreatureFoundationMovementMode.Grounded)]
    [InlineData(CreatureFoundationMovementMode.Flying)]
    [InlineData(CreatureFoundationMovementMode.Swimming)]
    [InlineData(CreatureFoundationMovementMode.Climbing)]
    [InlineData(CreatureFoundationMovementMode.Vehicle)]
    [InlineData(CreatureFoundationMovementMode.ZeroG)]
    public void CanonicalFoundationValidatesAllMovementModesAndSpawnsAfterValidation(CreatureFoundationMovementMode mode)
    {
        var profile = Complete(mode);
        Assert.Empty(CreatureFoundationValidator.Validate(profile, "blueprints/warden/creatureFoundation"));
        var spawned = CreatureFoundationValidator.Spawn(profile, "warden",
            new(Guid.NewGuid(), "crypt", TransformValue.Identity, 4201, EventAuthorityScope.Server, "encounter.wave"),
            () => Guid.Parse("a1600000-0000-4000-8000-000000000099"));
        Assert.Equal(mode, profile.Movement.Mode);
        Assert.Equal(4201, spawned.Context.Seed);
        Assert.Equal(84, spawned.ResourceState["health"]);
    }

    [Fact]
    public void InvalidResourceMovementAndBindingProduceStableLocalizedPaths()
    {
        var source = Complete(CreatureFoundationMovementMode.Flying);
        var invalid = new CreatureFoundationProfile(Guid.Empty, 99, string.Empty, true,
            source.Identity, source.DefaultTransform,
            [new(Guid.Empty, "health", 20, 21, -1, true)],
            new(CreatureFoundationMovementMode.Flying, 0, 0, -1, 10, 5, 120), source.References,
            source.Spawned with { Id = Guid.Empty, Handler = "bad handler", Authority = EventAuthorityScope.Local },
            new Dictionary<string, string> { ["bad key"] = string.Empty });
        var diagnostics = CreatureFoundationValidator.Validate(invalid, "blueprints/warden/creatureFoundation");
        Assert.Contains(diagnostics, item => item.Code == "creature.foundation.uuid_required");
        Assert.Contains(diagnostics, item => item.Code == "creature.foundation.resource_invalid" && item.Path.Contains("resources"));
        Assert.Contains(diagnostics, item => item.Code == "creature.foundation.movement_invalid" && item.Path.EndsWith("movement"));
        Assert.Contains(diagnostics, item => item.Code == "creature.foundation.spawn_binding_invalid" && item.Path.EndsWith("spawned"));
    }

    [Fact]
    public void OptionalAbsenceIsSafeAndRequiredAbsenceIsDiagnosed()
    {
        Assert.True(CreatureFoundationRuntimeResolver.Resolve(null, false).Succeeded);
        Assert.Equal(CreatureFoundationValidator.MissingRequiredCode,
            Assert.Single(CreatureFoundationRuntimeResolver.Resolve(null, true).Diagnostics).Code);
    }

    [Fact]
    public void ProjectValidationRejectsInvalidAndDuplicateFoundationsBeforeBuild()
    {
        var shared = Complete();
        var first = new BlueprintDefinition(BlueprintId.From(Guid.NewGuid()), "ash-warden",
            "Gardien des cendres", BlueprintKind.Creature, creatureFoundation: shared);
        var invalidDuplicate = new CreatureFoundationProfile(shared.Id, shared.SchemaVersion, shared.Provenance,
            shared.RequiredAtRuntime, shared.Identity, shared.DefaultTransform, shared.Resources,
            shared.Movement with { MaximumSpeed = 0 }, shared.References, shared.Spawned, shared.Overrides);
        var second = new BlueprintDefinition(BlueprintId.From(Guid.NewGuid()), "ash-warden-echo",
            "Echo du gardien", BlueprintKind.Creature, creatureFoundation: invalidDuplicate);
        var area = new AreaDefinition(AreaId.From(Guid.NewGuid()), "crypt", "Crypte", []);
        var project = new ProjectDefinition(ProjectId.From(Guid.NewGuid()), "Fondations", [first, second], [area]);

        var diagnostics = ProjectValidator.Validate(project);

        Assert.Contains(diagnostics, item => item.Code == ProjectValidator.DuplicateCreatureFoundationCode);
        Assert.Contains(diagnostics, item => item.Code == "creature.foundation.movement_invalid" &&
                                             item.Path.Contains(second.Id.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public void ProjectValidationRejectsDuplicateAuthoringAndAnimationUuidsBeforeBuild()
    {
        var shared = Complete();
        var authoring = CreatureAuthoringCompositionProfile.CreateDefault(Guid.NewGuid(), "author:r077");
        CreatureFoundationProfile AddAuthoring(CreatureFoundationProfile source) => new(source.Id,
            source.SchemaVersion, source.Provenance, source.RequiredAtRuntime, source.Identity,
            source.DefaultTransform, source.Resources, source.Movement, source.References, source.Spawned,
            source.Overrides, authoring, true);
        var first = new BlueprintDefinition(BlueprintId.From(Guid.NewGuid()), "ash-warden",
            "Gardien", BlueprintKind.Creature, creatureFoundation: AddAuthoring(shared));
        var secondBase = CreatureFoundationProfile.CreateDefault(Guid.NewGuid(), "ember-echo", "Écho", "author:r077");
        var second = new BlueprintDefinition(BlueprintId.From(Guid.NewGuid()), "ember-echo",
            "Écho", BlueprintKind.Creature, creatureFoundation: AddAuthoring(secondBase));
        var project = new ProjectDefinition(ProjectId.From(Guid.NewGuid()), "R077", [first, second],
            [new AreaDefinition(AreaId.From(Guid.NewGuid()), "lab", "Lab", [])]);

        var diagnostics = ProjectValidator.Validate(project);

        Assert.Contains(diagnostics, item => item.Code == ProjectValidator.DuplicateCreatureAuthoringCode);
        Assert.Contains(diagnostics, item => item.Code == ProjectValidator.DuplicateCreatureAnimationCode);
    }

    public static CreatureFoundationProfile Complete(CreatureFoundationMovementMode mode = CreatureFoundationMovementMode.Flying)
    {
        var profile = CreatureFoundationProfile.CreateDefault(
            Guid.Parse("a1600000-0000-4000-8000-000000000001"), "ash-warden", "Gardien des cendres", "author:r038");
        return new(profile.Id, profile.SchemaVersion, profile.Provenance, true,
            new("ash-warden", new Dictionary<string, string> { ["fr"] = "Gardien des cendres", ["en"] = "Ash Warden" },
                ["gardien"], "il", new Dictionary<string, string> { ["fr"] = "Gardien de la crypte." },
                "Boss de démonstration", ["boss", "crypt"]),
            TransformValue.Identity,
            [new(Guid.Parse("a1600000-0000-4000-8000-000000000002"), "health", 100, 84, .5, true),
             new(Guid.Parse("a1600000-0000-4000-8000-000000000003"), "stamina", 40, 40, 2, false)],
            new(mode, 6, 12, 8, 0, mode == CreatureFoundationMovementMode.Flying ? 30 : 0, 50),
            new("appearance.ash-warden", "rig.humanoid", "perception.guard", "combat.boss", "dialogue.warden",
                "inventory.warden", "faction.ashes"),
            new(Guid.Parse("a1600000-0000-4000-8000-000000000004"), 1, "author:r038", "1.0",
                "creature.spawned", "OnSpawned", EventAuthorityScope.Server),
            new Dictionary<string, string> { ["spawn.policy"] = "validated" });
    }
}
