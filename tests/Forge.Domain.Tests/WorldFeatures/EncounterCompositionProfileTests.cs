using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;

namespace Forge.Domain.Tests.WorldFeatures;

public sealed class EncounterCompositionProfileTests
{
    [Fact]
    public void Canonical_wizard_difficulty_counts_selection_and_activation_validate()
    {
        var encounter = Encounter();

        var resolution = EncounterCompositionProfileValidator.Resolve(encounter);

        Assert.True(resolution.Available);
        Assert.Empty(resolution.Diagnostics);
        Assert.Equal(36, encounter.Composition!.Difficulty.EffectiveBudget);
        Assert.Equal((2, 4), (encounter.Composition.Groups[0].Minimum,
            encounter.Composition.Groups[0].Maximum));
        Assert.Equal("party:forge-generic", encounter.Composition.Selection.PartyAssessmentProviderId);
        Assert.Equal(EncounterActivationMode.PopulationMaintained, encounter.Composition.Activation.Mode);
    }

    [Fact]
    public void Invalid_composition_reports_stable_diagnostics_and_paths()
    {
        var encounter = Encounter();
        var source = encounter.Composition!;
        var invalid = source with
        {
            Id = Guid.Empty,
            Slug = string.Empty,
            Difficulty = source.Difficulty with { BaseBudget = 0, ExpectedPartySize = 0 },
            Groups = [source.Groups[0] with
            {
                Minimum = 5,
                Maximum = 2,
                CandidateIds = [Guid.NewGuid()],
            }],
            Selection = source.Selection with { PartyAssessmentProviderId = "party:other", Strategy = string.Empty },
            Activation = source.Activation with
            {
                Mode = EncounterActivationMode.Script,
                IntervalSeconds = -1,
                ScriptId = null,
            },
            Overrides = new Dictionary<string, string> { [string.Empty] = string.Empty },
        };

        var diagnostics = EncounterCompositionProfileValidator.Validate(invalid, encounter.Foundation,
            "encounters/test/composition");
        var codes = diagnostics.Select(item => item.Code).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("world.encounter.composition_identity_invalid", codes);
        Assert.Contains("world.encounter.difficulty_invalid", codes);
        Assert.Contains("world.encounter.count_range_invalid", codes);
        Assert.Contains("world.encounter.selection_invalid", codes);
        Assert.Contains("world.encounter.activation_invalid", codes);
        Assert.Contains("world.encounter.composition_override_invalid", codes);
        Assert.All(diagnostics, item => Assert.StartsWith("encounters/test/composition", item.Path));
    }

    [Fact]
    public void Group_and_effective_budgets_must_fit_canonical_spawn_budget()
    {
        var encounter = Encounter();
        var invalid = encounter.Composition! with
        {
            Groups = [encounter.Composition.Groups[0] with { Budget = 37 }],
        };

        var diagnostic = Assert.Single(EncounterCompositionProfileValidator.Validate(invalid,
            encounter.Foundation, "encounters/test/composition"),
            item => item.Code == "world.encounter.composition_budget_invalid");

        Assert.Equal("encounters/test/composition/groups", diagnostic.Path);
    }

    [Fact]
    public void All_seven_activation_modes_are_data_driven_and_missing_profile_is_explicit()
    {
        var encounter = Encounter();
        foreach (var mode in Enum.GetValues<EncounterActivationMode>())
        {
            var activation = encounter.Composition!.Activation with
            {
                Mode = mode,
                IntervalSeconds = mode == EncounterActivationMode.Timed ? 30 : 0,
                ScriptId = mode == EncounterActivationMode.Script ? "scripts/encounter_policy" : null,
            };
            var candidate = encounter.Composition with { Activation = activation };
            Assert.Empty(EncounterCompositionProfileValidator.Validate(candidate, encounter.Foundation,
                "encounters/test/composition"));
        }

        var optional = EncounterCompositionProfileValidator.Resolve(encounter with
        { Composition = null, CompositionRequiredAtRuntime = false });
        var required = EncounterCompositionProfileValidator.Resolve(encounter with
        { Composition = null, CompositionRequiredAtRuntime = true });

        Assert.True(optional.OptionalFallback);
        Assert.Empty(optional.Diagnostics);
        Assert.Equal("world.encounter.composition_required", Assert.Single(required.Diagnostics).Code);
    }

    internal static EncounterWorldProfile Encounter()
    {
        var encounterId = Guid.Parse("83000000-0000-4000-8000-000000000001");
        var candidateId = Guid.Parse("83000000-0000-4000-8000-000000000002");
        return new(encounterId, 1, "test:r083",
            AreaId.From(Guid.Parse("83000000-0000-4000-8000-000000000003")), null, null, null, null,
            new(EncounterWorldProfile.VisibilityMode.BehindCover, 10, true, "vfx:spawn"),
            new(30, 30, 120, true, true),
            new(new(EncounterWorldProfile.VolumeShape.ExtrudedPolygon2D,
                    [new(-4, 0, -4), new(4, 0, -4), new(4, 0, 4), new(-4, 0, 4)], Vector3Value.Zero,
                    4, 8, ["player"], ["party"], true, 18, "scripts/encounter_activate", true),
                [new(candidateId, BlueprintId.From(Guid.Parse("83000000-0000-4000-8000-000000000004")),
                    EncounterWorldProfile.CandidateRole.Controller, 3, 8, [], ["ember"], 4)],
                36, ["anchor:test"], ["reward:test"], 12),
            new(Guid.Parse("83000000-0000-4000-8000-000000000010"), 1, "test:r083", "ash-ambush",
                "Ash ambush", "ambush", "ash-crypt", EncounterWorldProfile.CandidateRole.Controller,
                new(Guid.Parse("83000000-0000-4000-8000-000000000011"), "ruleset:forge",
                    "party:forge-generic", EncounterDifficultyBand.Hard, 4, 12, 20, 1.2, 1.5),
                [new(Guid.Parse("83000000-0000-4000-8000-000000000012"), "wave:warden", 2, 4, 36,
                    [candidateId])],
                new(Guid.Parse("83000000-0000-4000-8000-000000000013"), "party:forge-generic",
                    "challenge-rating", 42, true),
                new(Guid.Parse("83000000-0000-4000-8000-000000000014"),
                    EncounterActivationMode.PopulationMaintained, 30, null, EncounterSpawnDistribution.Weighted),
                new Dictionary<string, string> { ["selection.fallback"] = "lowest-cost" }), true);
    }
}
