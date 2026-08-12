using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Application.Companions;
using Forge.RpgKit.Domain.Companions;

namespace Forge.RpgKit.Domain.Tests.Companions;

public sealed class CompanionFoundationServiceTests
{
    [Fact]
    public void R074Foundation_resolves_dialogue_contexts_and_optional_required_runtime_contracts()
    {
        var profile = Profile();

        Assert.NotNull(CompanionFoundationService.Resolve(profile).Foundation);
        Assert.Equal(RecruitDialogueContext.NotRecruited,
            CompanionFoundationService.DialogueContext(profile, string.Empty, "player:one", true));
        Assert.Equal(RecruitDialogueContext.RecruitedBySpeaker,
            CompanionFoundationService.DialogueContext(profile, "player:one", "player:one", true));
        Assert.True(CompanionFoundationService.Resolve(profile with
        {
            Foundation = null,
            FoundationRequiredAtRuntime = false,
        }).OptionalFallback);
        Assert.Equal("RPG_COMPANION_FOUNDATION_REQUIRED",
            Assert.Single(CompanionFoundationService.Resolve(profile with { Foundation = null }).Diagnostics).Code);
    }

    [Fact]
    public void R074Recruit_and_dismiss_are_atomic_deterministic_previewable_and_preserve_state()
    {
        var profile = Profile();
        var component = CompanionFoundationService.CreateComponent(profile, string.Empty, TransformValue.Identity);
        var party = CompanionFoundationService.CreatePartyState(profile, "party:one", [],
            new Dictionary<string, decimal> { ["crown"] = 10 },
            new HashSet<string>(["quest:open"], StringComparer.Ordinal), .5);
        var original = new CompanionWorldState(component, party,
            new Dictionary<string, string> { [component.InventoryId] = component.Id.ToString("D") },
            TransformValue.Identity);

        var preview = CompanionFoundationService.Recruit(profile, original, "player:one", true);
        var applied = CompanionFoundationService.Recruit(profile, original, "player:one", false);
        Assert.True(preview.Result.Committed);
        Assert.Equal(preview.TransactionId, applied.TransactionId);
        Assert.Equal(preview.Result.SemanticDiff, applied.Result.SemanticDiff);
        Assert.Equal(preview.Result.State.Companion.OwnerId, applied.Result.State.Companion.OwnerId);
        Assert.Equal(preview.Result.State.Party.Funds["crown"], applied.Result.State.Party.Funds["crown"]);
        var rejectedInput = original with { Party = party with { Capacity = 0 } };
        Assert.Same(rejectedInput,
            CompanionFoundationService.Recruit(profile, rejectedInput, "player:one", true).Result.State);

        var position = new TransformValue(new(8, 0, 2), QuaternionValue.Identity, Vector3Value.One);
        var dismissed = CompanionFoundationService.Dismiss(profile, applied.Result.State, "player:one", position,
            false);
        Assert.True(dismissed.Result.Committed);
        Assert.Equal(position, dismissed.Result.State.DismissPosition);
        Assert.Equal(CompanionPersistence.Campaign, dismissed.Result.State.Companion.Persistence);
        Assert.Equal(component.Id.ToString(), dismissed.Result.State.InventoryOwners[component.InventoryId]);
    }

    [Fact]
    public void R074PartyPolicy_bounds_companions_roles_summons_pets_allies_and_server_population()
    {
        var profile = Profile();
        var component = CompanionFoundationService.CreateComponent(profile, "player:one", TransformValue.Identity)
            with
        { CommandIds = new HashSet<string>(["follow", "role:Support"], StringComparer.Ordinal) };

        Assert.Empty(CompanionFoundationService.ValidateParty(profile, new([component], 1, 1, 1)));
        var diagnostics = CompanionFoundationService.ValidateParty(profile,
            new([component, component with { Id = Guid.NewGuid() }, component with { Id = Guid.NewGuid() }], 1, 1, 1));
        Assert.Contains(diagnostics, item => item.Code == "RPG_PARTY_CAPACITY_EXCEEDED");
        Assert.Contains(diagnostics, item => item.Code == "RPG_PARTY_ROLE_LIMIT_EXCEEDED");
    }

    internal static CompanionWorldProfile Profile()
    {
        var id = Guid.Parse("74000000-0000-4000-8000-000000000001");
        var dialogue = Guid.Parse("74000000-0000-4000-8000-000000000002");
        return new(id, 1, "test:r074",
            BlueprintId.From(Guid.Parse("74000000-0000-4000-8000-000000000003")),
            new(dialogue, "full", "limited", "greeting", "full", "deny"),
            new("wedge", 1, 1.5, true, true, true),
            new(CompanionWorldProfile.ProgressionMode.PlayerDirected, 4, 20, true, true, [5, 10, 15]),
            new(.5, .6, 0, .75, ["friend"], ["joined"]),
            new([], 1, 90, [], true, dialogue), null,
            new(true, true, 4, "sight", "traps", true),
            new(["heal"], [], .3, "lowest-health", true),
            new(true, true, true, true, "owner-or-gm"), new(true, true, "owner", true, 120),
            null, false,
            new(Guid.Parse("74000000-0000-4000-8000-000000000010"), 1, "test:r074",
                new(Guid.Parse("74000000-0000-4000-8000-000000000011"), 1, "test:r074", true,
                    ["quest:open"], 2, "crown", .25, ["relation:increase"], true, true,
                    "scripts/companion_recruit"),
                new(Guid.Parse("74000000-0000-4000-8000-000000000012"), 1, "test:r074", dialogue,
                    "not-recruited", ["quest:open"], "offer.join", "offer.refuse", ["relation:increase"]),
                new(Guid.Parse("74000000-0000-4000-8000-000000000013"), 1, "test:r074", dialogue,
                    "recruited-by-speaker", ["follow", "hold"], true, true, true, "companion.depart"),
                new(Guid.Parse("74000000-0000-4000-8000-000000000014"), 1, "test:r074", true, true, true,
                    true, CompanionWorldProfile.CompanionPersistence.Campaign, "inventory:companion", "party.safe",
                    true),
                new(Guid.Parse("74000000-0000-4000-8000-000000000015"), 1, "test:r074", 2, 1, 1, 1, 5,
                    new Dictionary<string, int> { ["Support"] = 1 })),
            true);
    }
}
