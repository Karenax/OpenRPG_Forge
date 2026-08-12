using Forge.Domain.Dialogues;

namespace Forge.Domain.Tests.Dialogues;

public sealed class DialogueAuthoringProfileTests
{
    [Fact]
    public void Canonical_profile_validates_tokens_participants_evaluation_notes_and_all_modes()
    {
        var dialogue = CreateDialogue();
        var profile = DialogueAuthoringProfile.CreateDefault(dialogue, "author:r068");
        var entry = profile.Nodes.Single(item => item.NodeId == dialogue.EntryNodeId);
        var token = new DialogueTextToken(Guid.NewGuid(), 1, "author:r068", "hero", DialogueTextTokenType.EntityName,
            true, null);
        profile = profile with
        {
            Nodes = profile.Nodes.Select(item => item.NodeId == dialogue.EntryNodeId
                ? item with { Tokens = [token], EvaluationMode = DialogueChoiceEvaluationMode.Weighted }
                : item).ToArray(),
        };
        dialogue = dialogue.WithAuthoring(profile);

        Assert.Empty(DialogueAuthoringProfileValidator.Validate(dialogue));
        Assert.Equal(5, Enum.GetValues<DialogueChoiceEvaluationMode>().Length);
        Assert.Equal(4, Enum.GetValues<DialogueParticipantBindingKind>().Length);
        Assert.Equal(6, Enum.GetValues<DialogueTextTokenType>().Length);
        Assert.Equal(2, entry.ChoiceEvaluations.Count);
    }

    [Fact]
    public void Invalid_authoring_reports_stable_localized_diagnostics()
    {
        var dialogue = CreateDialogue();
        var profile = DialogueAuthoringProfile.CreateDefault(dialogue, "author:r068");
        var entry = profile.Nodes.Single(item => item.NodeId == dialogue.EntryNodeId);
        var invalid = entry with
        {
            Tokens = [new(Guid.Empty, 99, "", "missing", DialogueTextTokenType.Integer, true, "not-an-int")],
            ChoiceEvaluations = [new(Guid.Empty, 99, "", DialogueChoiceId.From(Guid.NewGuid()), -1)],
            WizardEntries = [new(Guid.Empty, 99, "", DialogueWizardEntryKind.Action, Guid.NewGuid(), "opaque")],
            Notes = entry.Notes with { Intent = "", AuthorMention = "" },
        };
        profile = profile with
        {
            Participants = [profile.Participants[0] with { FallbackRole = "missing" }],
            Nodes = profile.Nodes.Select(item => item.NodeId == dialogue.EntryNodeId ? invalid : item).ToArray(),
        };
        var diagnostics = DialogueAuthoringProfileValidator.Validate(dialogue.WithAuthoring(profile));

        Assert.Contains(diagnostics, item => item.Code == DialogueAuthoringProfileValidator.ParticipantCode &&
                                             item.Path.EndsWith("/participants", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == DialogueAuthoringProfileValidator.TokenCode &&
                                             item.Path.Contains(dialogue.EntryNodeId.ToString(), StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == DialogueAuthoringProfileValidator.EvaluationCode);
        Assert.Contains(diagnostics, item => item.Code == DialogueAuthoringProfileValidator.WizardCode);
        Assert.Contains(diagnostics, item => item.Code == DialogueAuthoringProfileValidator.NotesCode);
    }

    [Fact]
    public void Optional_absence_is_safe_and_required_absence_is_diagnostic()
    {
        var optional = CreateDialogue();
        var required = new DialogueDefinition(optional.Id, optional.Slug, optional.DisplayName,
            optional.EntryNodeId, optional.Nodes, optional.Presentation, optional.SchemaVersion, optional.Provenance,
            new Dictionary<string, string> { ["kind"] = "conversation", ["authoring.required"] = "true" },
            optional.Participants, optional.EntryPoints, optional.Edges);

        Assert.Empty(DialogueAuthoringProfileValidator.Validate(optional));
        Assert.Equal(DialogueAuthoringProfileValidator.RequiredMissingCode,
            Assert.Single(DialogueAuthoringProfileValidator.Validate(required)).Code);
    }

    [Fact]
    public void Visual_wizard_builds_typed_values_with_visible_source()
    {
        var condition = DialogueScriptWizard.BuildCondition(DialogueConditionKind.HasItem, "ash-key", "true",
            false, "author:r068");
        var action = DialogueScriptWizard.BuildAction(DialogueActionKind.StartQuest, "crypt", "active", true,
            "author:r068");

        Assert.Equal(condition.Value.Id, condition.Source.TargetId);
        Assert.Equal(DialogueWizardEntryKind.Condition, condition.Source.Kind);
        Assert.StartsWith("builder:condition:HasItem", condition.Source.SourceDescription);
        Assert.Equal(action.Value.Id, action.Source.TargetId);
        Assert.Equal(DialogueWizardEntryKind.Action, action.Source.Kind);
        Assert.StartsWith("builder:action:StartQuest", action.Source.SourceDescription);
    }

    internal static DialogueDefinition CreateDialogue()
    {
        var entry = DialogueNodeId.Parse("d0680000-0000-4000-8000-000000000001");
        var first = DialogueNodeId.Parse("d0680000-0000-4000-8000-000000000002");
        var second = DialogueNodeId.Parse("d0680000-0000-4000-8000-000000000003");
        return new DialogueDefinition(DialogueId.Parse("d0680000-0000-4000-8000-000000000010"),
            "r068", "R068", entry,
            [
                new(entry, null, new("dialogue.r068.entry", "Bienvenue, {{hero}}."), choices:
                [
                    new(DialogueChoiceId.Parse("d0680000-0000-4000-8000-000000000020"),
                        new("dialogue.r068.first", "Premier"), targetNodeId: first, priority: 0),
                    new(DialogueChoiceId.Parse("d0680000-0000-4000-8000-000000000021"),
                        new("dialogue.r068.second", "Second"), targetNodeId: second, priority: 1),
                ], role: DialogueNodeRole.PlayerChoice),
                new(first, null, new("dialogue.r068.end.first", "Fin A"), role: DialogueNodeRole.End),
                new(second, null, new("dialogue.r068.end.second", "Fin B"), role: DialogueNodeRole.End),
            ], provenance: "author:r068");
    }
}
