using Forge.Domain.Dialogues;

namespace Forge.Domain.Tests.Dialogues;

public sealed class DialogueExperienceProfileTests
{
    [Fact]
    public void R069Canonical_profile_validates_all_five_capabilities()
    {
        var dialogue = DialogueAuthoringProfileTests.CreateDialogue();
        var end = dialogue.Nodes[1].Id;
        var action = new DialogueAction(DialogueActionKind.SetVariable, "dialogue.completed", "true");
        var profile = DialogueExperienceProfile.CreateDefault(dialogue, "author:r069") with
        {
            RequiredAtRuntime = true,
            Lifecycle = DialogueExperienceProfile.CreateDefault(dialogue, "author:r069").Lifecycle with
            {
                CompletedActions = [action],
                AbortedActions = [new(DialogueActionKind.RunScript, "dialogue-aborted.forge", "reason")],
                RollbackOnAbort = true,
            },
            Search = DialogueExperienceProfile.CreateDefault(dialogue, "author:r069").Search with
            {
                Pattern = "Bienvenue|Fin",
                UseRegex = true,
                ReplacementPreview = "Réplique",
                SpeakerRoles = [],
            },
            MemorySlots =
            [
                new(Guid.Parse("d0690000-0000-4000-8000-000000000101"), 1, "author:r069",
                    DialogueMemoryScope.Player, "player:current", "trust", DialogueTextTokenType.Integer,
                    DialogueMemoryDuration.Campaign, "0"),
            ],
            SkillChecks =
            [
                new(Guid.Parse("d0690000-0000-4000-8000-000000000102"), 1, "author:r069",
                    dialogue.EntryNodeId, "persuasion", 12, "elyra", true,
                    DialogueSkillCheckVisibility.FullExplanation, end, dialogue.Nodes[2].Id),
            ],
        };

        var configured = dialogue.WithExperience(profile);

        Assert.Empty(DialogueExperienceProfileValidator.Validate(configured));
        Assert.Equal(6, Enum.GetValues<DialogueReviewBadge>().Length);
        Assert.Equal(5, Enum.GetValues<DialogueMemoryScope>().Length);
        Assert.Equal(5, Enum.GetValues<DialogueMemoryDuration>().Length);
        Assert.Equal(4, Enum.GetValues<DialogueSkillCheckVisibility>().Length);
    }

    [Fact]
    public void R069Runtime_is_deterministic_preview_only_and_preserves_resume_memory()
    {
        var dialogue = DialogueAuthoringProfileTests.CreateDialogue();
        var profile = DialogueExperienceProfile.CreateDefault(dialogue, "author:r069") with
        {
            Lifecycle = DialogueExperienceProfile.CreateDefault(dialogue, "author:r069").Lifecycle with
            {
                RollbackOnAbort = true,
            },
            Search = DialogueExperienceProfile.CreateDefault(dialogue, "author:r069").Search with
            {
                Pattern = "Bienvenue",
                SpeakerRoles = [],
                ReplacementPreview = "Salut",
            },
            MemorySlots =
            [
                new(Guid.Parse("d0690000-0000-4000-8000-000000000201"), 1, "author:r069",
                    DialogueMemoryScope.Party, "party:one", "trust", DialogueTextTokenType.Integer,
                    DialogueMemoryDuration.Campaign, "0"),
            ],
            SkillChecks =
            [
                new(Guid.Parse("d0690000-0000-4000-8000-000000000202"), 1, "author:r069",
                    dialogue.EntryNodeId, "persuasion", 12, "elyra", true,
                    DialogueSkillCheckVisibility.FullExplanation, dialogue.Nodes[1].Id, dialogue.Nodes[2].Id),
            ],
        };
        dialogue = dialogue.WithExperience(profile);
        var state = DialogueExperienceRuntimeService.Write(profile, DialogueMemoryState.Empty,
            profile.MemorySlots[0].Id, "4");

        var preview = DialogueExperienceRuntimeService.PreviewReplace([dialogue], profile.Search);
        var first = DialogueExperienceRuntimeService.ResolveSkillCheck(profile.SkillChecks[0], 5, 2, 42);
        var repeated = DialogueExperienceRuntimeService.ResolveSkillCheck(profile.SkillChecks[0], 5, 2, 42);
        var aborted = DialogueExperienceRuntimeService.Abort(dialogue, "player", dialogue.EntryNodeId,
            DialogueAbortReason.CombatStarted, state, 42, DateTimeOffset.UnixEpoch);

        Assert.Single(preview.Hits);
        Assert.Equal("Salut, {{hero}}.", Assert.Single(preview.Replacements).Value);
        Assert.Contains("preview-only:true", preview.SemanticDiff);
        Assert.Equal("Bienvenue, {{hero}}.", dialogue.Nodes[0].Text.Fallback);
        Assert.Equal(first, repeated);
        Assert.Contains("compétence 5", first.Explanation);
        Assert.True(aborted.RolledBack);
        Assert.Equal("4", aborted.ResumeState!.MemorySnapshot["Party:party:one:trust:Campaign"]);
    }

    [Fact]
    public void R069Invalid_profile_reports_stable_localized_diagnostics()
    {
        var dialogue = DialogueAuthoringProfileTests.CreateDialogue();
        var profile = DialogueExperienceProfile.CreateDefault(dialogue, "author:r069");
        profile = profile with
        {
            Search = profile.Search with { Pattern = "(", UseRegex = true },
            MemorySlots =
            [new(Guid.Empty, 99, "", DialogueMemoryScope.Player, "", "", DialogueTextTokenType.Integer,
                DialogueMemoryDuration.Campaign, "bad")],
            SkillChecks =
            [new(Guid.Empty, 99, "", DialogueNodeId.From(Guid.NewGuid()), "", double.NaN, null, false,
                DialogueSkillCheckVisibility.Hidden, DialogueNodeId.From(Guid.NewGuid()),
                DialogueNodeId.From(Guid.NewGuid()))],
        };

        var diagnostics = DialogueExperienceProfileValidator.Validate(dialogue.WithExperience(profile));

        Assert.Contains(diagnostics, item => item.Code == DialogueExperienceProfileValidator.SearchCode &&
                                             item.Path.Contains("/search", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == DialogueExperienceProfileValidator.MemoryCode &&
                                             item.Path.EndsWith("/memorySlots", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == DialogueExperienceProfileValidator.SkillCheckCode &&
                                             item.Path.EndsWith("/skillChecks", StringComparison.Ordinal));
    }

    [Fact]
    public void R069Optional_absence_is_safe_and_required_absence_is_diagnostic()
    {
        var optional = DialogueAuthoringProfileTests.CreateDialogue();
        var required = new DialogueDefinition(optional.Id, optional.Slug, optional.DisplayName,
            optional.EntryNodeId, optional.Nodes, optional.Presentation, optional.SchemaVersion, optional.Provenance,
            new Dictionary<string, string> { ["kind"] = "conversation", ["experience.required"] = "true" },
            optional.Participants, optional.EntryPoints, optional.Edges, optional.Authoring);

        Assert.Empty(DialogueExperienceProfileValidator.Validate(optional));
        Assert.Equal(DialogueExperienceProfileValidator.RequiredMissingCode,
            Assert.Single(DialogueExperienceProfileValidator.Validate(required)).Code);
    }
}
