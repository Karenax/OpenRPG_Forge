using System.Globalization;
using Forge.Domain.Dialogues;
using Forge.Domain.Validation;

namespace Forge.Application.Dialogues;

public sealed record DialogueAuthoringContext(
    IReadOnlyDictionary<string, object> TokenValues,
    IReadOnlyDictionary<string, string> ResolvedParticipants,
    int DeterministicSeed);

public sealed record RenderedDialogueAuthoringNode(
    DialogueNodeId NodeId,
    string SpeakerEntityId,
    string Text,
    IReadOnlyList<DialogueChoice> AvailableChoices,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class DialogueAuthoringRuntimeService
{
    public const string TokenMissingCode = "dialogue.authoring.token_missing";
    public const string TokenTypeCode = "dialogue.authoring.token_type_invalid";
    public const string ParticipantMissingCode = "dialogue.authoring.participant_missing";

    private static readonly string[] StandardTokens =
    [
        "player.name",
        "speaker.name",
        "party.leader.name",
        "world.time",
        "quest.title",
    ];

    public static IReadOnlyList<string> CompleteTokens(DialogueDefinition dialogue, string? prefix = null)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        var values = StandardTokens.Concat(dialogue.Authoring?.Nodes.SelectMany(item => item.Tokens)
                .Select(item => item.Name) ?? [])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
        return string.IsNullOrWhiteSpace(prefix)
            ? values.ToArray()
            : values.Where(item => item.StartsWith(prefix.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    public static RenderedDialogueAuthoringNode Render(
        DialogueDefinition dialogue,
        DialogueNodeId nodeId,
        DialogueAuthoringContext context,
        IDialogueConditionEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentNullException.ThrowIfNull(nodeId);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(evaluator);
        var node = dialogue.Nodes.SingleOrDefault(item => item.Id == nodeId)
            ?? throw new KeyNotFoundException("DIALOGUE_NODE_MISSING");
        var diagnostics = DialogueAuthoringProfileValidator.Validate(dialogue).ToList();
        if (dialogue.Authoring is null || !dialogue.Authoring.Enabled)
        {
            return new(node.Id, node.SpeakerBlueprintId?.ToString() ?? string.Empty, node.Text.Fallback,
                node.Choices.Where(choice => DialogueConditionLogic.Evaluate(choice, evaluator).Available).ToArray(),
                diagnostics);
        }

        var authoring = dialogue.Authoring.Nodes.Single(item => item.NodeId == node.Id);
        var path = $"dialogues/{dialogue.Id}/authoring/nodes/{node.Id}";
        var text = node.Text.Fallback;
        foreach (var token in authoring.Tokens)
        {
            var value = context.TokenValues.GetValueOrDefault(token.Name);
            if (value is null && token.DefaultValue is not null) value = token.DefaultValue;
            if (value is null)
            {
                if (token.Required)
                    diagnostics.Add(new DomainDiagnostic(TokenMissingCode,
                        $"Required token '{token.Name}' has no value.", $"{path}/tokens/{token.Id}"));
                continue;
            }
            if (!Matches(token.Type, value))
            {
                diagnostics.Add(new DomainDiagnostic(TokenTypeCode,
                    $"Token '{token.Name}' does not match {token.Type}.", $"{path}/tokens/{token.Id}"));
                continue;
            }
            text = text.Replace("{{" + token.Name + "}}", Escape(Convert.ToString(value,
                CultureInfo.InvariantCulture) ?? string.Empty), StringComparison.Ordinal);
        }

        var speaker = ResolveParticipant(dialogue.Authoring.Participants, authoring.SpeakerRole,
            context.ResolvedParticipants);
        if (speaker is null)
            diagnostics.Add(new DomainDiagnostic(ParticipantMissingCode,
                $"Participant role '{authoring.SpeakerRole}' cannot resolve.", $"{path}/speakerRole"));

        var valid = node.Choices.Select((choice, index) => (choice, index))
            .Where(item => DialogueConditionLogic.Evaluate(item.choice, evaluator).Available)
            .OrderBy(item => item.choice.Priority)
            .ThenBy(item => item.index)
            .Select(item => item.choice)
            .ToArray();
        var available = authoring.EvaluationMode switch
        {
            DialogueChoiceEvaluationMode.FirstValid => valid.Take(1).ToArray(),
            DialogueChoiceEvaluationMode.AllValid or DialogueChoiceEvaluationMode.Manual => valid,
            DialogueChoiceEvaluationMode.RandomDeterministic => valid.Length == 0
                ? []
                : [valid[new Random(context.DeterministicSeed).Next(valid.Length)]],
            DialogueChoiceEvaluationMode.Weighted => PickWeighted(valid, authoring.ChoiceEvaluations,
                context.DeterministicSeed),
            _ => [],
        };
        return new(node.Id, speaker ?? string.Empty, text, available, diagnostics);
    }

    private static DialogueChoice[] PickWeighted(
        IReadOnlyList<DialogueChoice> choices,
        IReadOnlyList<DialogueChoiceEvaluation> evaluations,
        int seed)
    {
        var weights = evaluations.ToDictionary(item => item.ChoiceId, item => item.Weight);
        var total = choices.Sum(item => weights.GetValueOrDefault(item.Id));
        if (total <= 0) return [];
        var cursor = new Random(seed).NextDouble() * total;
        foreach (var choice in choices)
        {
            cursor -= weights.GetValueOrDefault(choice.Id);
            if (cursor <= 0) return [choice];
        }
        return [choices[^1]];
    }

    private static string? ResolveParticipant(
        IReadOnlyList<DialogueParticipantBinding> bindings,
        string role,
        IReadOnlyDictionary<string, string> resolved)
    {
        var binding = bindings.SingleOrDefault(item => item.Role == role);
        if (binding is null) return null;
        var key = $"{binding.Kind.ToString().ToLowerInvariant()}:{binding.Value}";
        if (resolved.TryGetValue(key, out var entity)) return entity;
        if (resolved.TryGetValue($"role:{role}", out entity)) return entity;
        return binding.FallbackRole is { } fallback && resolved.TryGetValue($"role:{fallback}", out entity)
            ? entity
            : null;
    }

    private static bool Matches(DialogueTextTokenType type, object value) => type switch
    {
        DialogueTextTokenType.Text or DialogueTextTokenType.EntityName or DialogueTextTokenType.LocalizedText => value is string,
        DialogueTextTokenType.Integer => value is sbyte or byte or short or ushort or int or uint or long or ulong ||
                                     value is string integer && long.TryParse(integer, out _),
        DialogueTextTokenType.Decimal => value is float or double or decimal ||
                                     value is string number && double.TryParse(number, NumberStyles.Float,
                                         CultureInfo.InvariantCulture, out _),
        DialogueTextTokenType.Boolean => value is bool || value is string boolean && bool.TryParse(boolean, out _),
        _ => false,
    };

    private static string Escape(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("{", "&#123;", StringComparison.Ordinal)
        .Replace("}", "&#125;", StringComparison.Ordinal);
}
