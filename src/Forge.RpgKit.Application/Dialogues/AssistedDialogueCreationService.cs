using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Assistant;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Projects;
using Forge.RpgKit.Domain.Dialogues;
using CoreDialogueAction = Forge.Domain.Dialogues.DialogueAction;
using CoreDialogueActionKind = Forge.Domain.Dialogues.DialogueActionKind;
using CoreDialogueNode = Forge.Domain.Dialogues.DialogueNode;
using RpgDialogueActionKind = Forge.RpgKit.Domain.Dialogues.DialogueActionKind;
using RpgDialogueNode = Forge.RpgKit.Domain.Dialogues.DialogueNode;

namespace Forge.RpgKit.Application.Dialogues;

public sealed record AssistedDialogueBrief(
    Guid DialogueId,
    string Slug,
    string DisplayName,
    Guid SpeakerBlueprintId,
    string Synopsis,
    IReadOnlyList<string> RequiredBeats,
    IReadOnlyList<string> StyleRules,
    IReadOnlyList<string> ContextCitations,
    string Locale,
    string LocalizationPrefix,
    string AuthorId,
    int Seed,
    int NodeCount);

public sealed record AssistedDialogueProposal(
    bool Succeeded,
    DialogueDefinition Dialogue,
    DialogueGraph Graph,
    IReadOnlyList<string> Citations,
    IReadOnlyList<DialogueDiagnostic> Lint,
    IReadOnlyList<string> StyleViolations,
    IReadOnlyList<string> LocalizationPlaceholders,
    string SemanticDiff,
    string PromptSha256,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCostUsd,
    string ProviderKey,
    string Provenance,
    bool ApprovedForApplication)
{
    public bool CanApply => Succeeded && Lint.Count == 0 && StyleViolations.Count == 0;

    public AssistedDialogueProposal WithApproval(bool approved, string provenance) =>
        this with
        {
            ApprovedForApplication = approved,
            Provenance = string.IsNullOrWhiteSpace(provenance) ? Provenance : provenance.Trim(),
        };
}

public static class AssistedDialogueCreationService
{
    public const string PromptKey = "dialogue-creation";
    public const string PromptVersion = "1.0.0";

    public static async Task<AssistedDialogueProposal> ProposeAsync(
        AssistedDialogueBrief brief,
        ProjectDefinition project,
        LoreIndexDefinition lore,
        IAssistantProvider provider,
        AssistantProviderDefinition providerDefinition,
        bool networkAuthorized,
        bool projectContentAuthorized,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(brief);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(lore);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(providerDefinition);
        ValidateBrief(brief, project);
        var lorePacket = LoreContextAssembler.Assemble(
            lore,
            project,
            string.Join(' ', brief.RequiredBeats.Prepend(brief.Synopsis)),
            8);
        const string system =
            "Propose a bounded OpenRPG Forge dialogue graph. Never emit executable code or mutate the project.";
        var user = JsonSerializer.Serialize(new
        {
            brief.Synopsis,
            brief.RequiredBeats,
            brief.StyleRules,
            brief.ContextCitations,
            brief.Locale,
            brief.NodeCount,
            lore = projectContentAuthorized ? lorePacket.Content : "[project content withheld]",
        });
        const string schema =
            "{\"type\":\"object\",\"required\":[\"summary\",\"nodes\"],\"properties\":{\"summary\":{\"type\":\"string\"},\"nodes\":{\"type\":\"array\"}}}";
        var hash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join('\n', system, user, schema)))).ToLowerInvariant();
        var prompt = new AssistantPromptRenderResult(PromptKey, PromptVersion, system, user, schema, hash);
        var response = await provider.GenerateAsync(new AssistantProviderRequest(
            brief.DialogueId,
            Guid.NewGuid(),
            prompt,
            AssistantPermissionLevel.Propose,
            Math.Min(2048, providerDefinition.MaximumOutputTokens),
            networkAuthorized,
            projectContentAuthorized), cancellationToken).ConfigureAwait(false);
        return Generate(brief, project, response.ProviderKey, response.EstimatedCost, response.Provenance,
            hash, response.InputTokens, response.OutputTokens);
    }

    public static AssistedDialogueProposal Generate(
        AssistedDialogueBrief brief,
        ProjectDefinition project,
        string providerKey = "offline-ci",
        decimal estimatedCostUsd = 0,
        string provenance = "assistant:deterministic",
        string promptSha256 = "offline",
        int inputTokens = 0,
        int outputTokens = 0)
    {
        ValidateBrief(brief, project);
        var speakerId = BlueprintId.From(brief.SpeakerBlueprintId);
        var nodeIds = Enumerable.Range(0, brief.NodeCount)
            .Select(index => StableId(brief.DialogueId, $"node:{brief.Seed}:{index}"))
            .ToArray();
        var source = string.Join('|', brief.StyleRules.Concat(brief.ContextCitations));
        var graphNodes = new Dictionary<Guid, RpgDialogueNode>();
        var coreNodes = new List<CoreDialogueNode>();
        var placeholders = new List<string>();
        for (var index = 0; index < nodeIds.Length; index++)
        {
            var beat = brief.RequiredBeats[index % brief.RequiredBeats.Count].Trim();
            var localizationKey = $"{brief.LocalizationPrefix}.node-{index + 1}";
            var text = $"{beat} — {{{{player_name}}}}";
            placeholders.Add($"{localizationKey}:{{{{player_name}}}}");
            var rpgEdges = index == nodeIds.Length - 1
                ? []
                : new[]
                {
                    new DialogueEdge(
                        nodeIds[index + 1],
                        index,
                        1,
                        index == 0
                            ? DialogueGraphService.BuildExpression(DialogueExpressionKind.Variable,
                                "assistant.dialogue.ready")
                            : null,
                        index == 0
                            ? [DialogueGraphService.BuildAction(RpgDialogueActionKind.SetVariable,
                                "assistant.dialogue.started", "true")]
                            : []),
                };
            graphNodes.Add(nodeIds[index], new RpgDialogueNode(
                nodeIds[index],
                "speaker",
                text,
                [new DialogueToken("player_name", DialogueTokenType.EntityName, true, "voyageur")],
                DialogueEvaluationMode.Manual,
                rpgEdges,
                new DialogueNodeNotes(beat, "direct", source, DialogueEditorialStatus.Review, [], brief.AuthorId)));
            IReadOnlyList<DialogueChoice> choices = index == nodeIds.Length - 1
                ? []
                :
                [
                    new DialogueChoice(
                        DialogueChoiceId.From(StableId(brief.DialogueId, $"choice:{brief.Seed}:{index}")),
                        new LocalizedText($"{brief.LocalizationPrefix}.choice-{index + 1}", "Continuer"),
                        index == 0
                            ? [new DialogueCondition(DialogueConditionKind.VariableEquals,
                                "assistant.dialogue.ready", "true")]
                            : [],
                        index == 0
                            ? [new CoreDialogueAction(CoreDialogueActionKind.SetVariable,
                                "assistant.dialogue.started", "true")]
                            : [],
                        DialogueNodeId.From(nodeIds[index + 1]))
                ];
            coreNodes.Add(new CoreDialogueNode(
                DialogueNodeId.From(nodeIds[index]),
                speakerId,
                new LocalizedText(localizationKey, text),
                [],
                choices));
        }
        var graph = new DialogueGraph(
            brief.DialogueId,
            1,
            provenance,
            brief.Slug,
            nodeIds[0],
            [new ParticipantBinding("speaker", ParticipantBindingKind.Entity,
                brief.SpeakerBlueprintId.ToString("D"), null)],
            graphNodes);
        var aiBrief = new DialogueAiBrief(
            brief.Synopsis,
            brief.RequiredBeats,
            brief.StyleRules,
            brief.ContextCitations,
            brief.Locale,
            brief.AuthorId);
        var draft = DialogueAuthoringService.PreviewAi(aiBrief, _ => graph);
        var dialogue = new DialogueDefinition(
            DialogueId.From(brief.DialogueId),
            brief.Slug,
            brief.DisplayName,
            DialogueNodeId.From(nodeIds[0]),
            coreNodes);
        var coreLint = DialogueValidator.Validate(dialogue)
            .Select(item => new DialogueDiagnostic(item.Code, item.Path, item.Message));
        var lint = draft.Lint.Concat(coreLint).ToArray();
        return new AssistedDialogueProposal(
            lint.Length == 0 && draft.StyleViolations.Count == 0,
            dialogue,
            graph,
            draft.Citations,
            lint,
            draft.StyleViolations,
            placeholders,
            $"add-dialogue:{dialogue.Id}|nodes:{dialogue.Nodes.Count}|conditions:1|actions:1|citations:{draft.Citations.Count}|localization:{placeholders.Count}",
            promptSha256,
            inputTokens,
            outputTokens,
            estimatedCostUsd,
            providerKey,
            provenance,
            false);
    }

    private static void ValidateBrief(AssistedDialogueBrief brief, ProjectDefinition project)
    {
        if (brief.DialogueId == Guid.Empty || string.IsNullOrWhiteSpace(brief.Slug) ||
            string.IsNullOrWhiteSpace(brief.DisplayName) || string.IsNullOrWhiteSpace(brief.Synopsis) ||
            string.IsNullOrWhiteSpace(brief.Locale) || string.IsNullOrWhiteSpace(brief.LocalizationPrefix) ||
            string.IsNullOrWhiteSpace(brief.AuthorId))
            throw new ArgumentException("Assisted dialogue identity, synopsis, locale and author are required.");
        if (brief.NodeCount is < 3 or > 12 || brief.RequiredBeats.Count == 0 ||
            brief.StyleRules.Count == 0 || brief.ContextCitations.Count == 0)
            throw new ArgumentException("Assisted dialogue requires 3-12 nodes, beats, style rules and citations.");
        var speaker = project.Blueprints.SingleOrDefault(item => item.Id.Value == brief.SpeakerBlueprintId);
        if (speaker is null || speaker.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn))
            throw new ArgumentException("Assisted dialogue speaker must resolve to a creature or player blueprint.");
    }

    private static Guid StableId(Guid root, string discriminator)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{root:D}:{discriminator}"));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes);
    }
}
