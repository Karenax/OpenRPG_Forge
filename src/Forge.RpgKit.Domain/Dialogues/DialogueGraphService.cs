namespace Forge.RpgKit.Domain.Dialogues;

public enum DialogueTokenType { Text, Integer, Decimal, Boolean, EntityName, LocalizedText }
public enum ParticipantBindingKind { Role, Query, Tag, Entity }
public enum DialogueEvaluationMode { FirstValid, AllValid, Weighted, RandomDeterministic, Manual }
public enum DialogueExpressionKind { Equals, NotEquals, GreaterThan, LessThan, HasTag, HasItem, Variable, And, Or, Not }
public enum DialogueActionKind { SetVariable, GiveItem, TakeItem, ChangeReputation, StartQuest, CompleteQuest, EmitEvent }
public enum DialogueEditorialStatus { Draft, Review, Approved, Deprecated }
public sealed record DialogueToken(string Name, DialogueTokenType Type, bool Required, string? DefaultValue);
public sealed record ParticipantBinding(string Role, ParticipantBindingKind Kind, string Value, string? FallbackRole);
public sealed record DialogueExpression(DialogueExpressionKind Kind, string Left, string? Right, IReadOnlyList<DialogueExpression> Children, string SourceDescription);
public sealed record DialogueAction(DialogueActionKind Kind, string Target, string Value, string SourceDescription);
public sealed record DialogueNodeNotes(string Intent, string Emotion, string Source, DialogueEditorialStatus Status, IReadOnlyList<string> Todos, string AuthorMention);
public sealed record DialogueEdge(Guid TargetNodeId, int Priority, double Weight, DialogueExpression? Condition, IReadOnlyList<DialogueAction> Actions);
public sealed record DialogueNode(Guid Id, string SpeakerRole, string TextTemplate, IReadOnlyList<DialogueToken> Tokens, DialogueEvaluationMode EvaluationMode, IReadOnlyList<DialogueEdge> Edges, DialogueNodeNotes Notes);
public sealed record DialogueGraph(Guid Id, int SchemaVersion, string Provenance, string Slug, Guid EntryNodeId, IReadOnlyList<ParticipantBinding> Participants, IReadOnlyDictionary<Guid, DialogueNode> Nodes);
public sealed record DialogueContext(IReadOnlyDictionary<string, object> Values, IReadOnlyDictionary<string, string> ParticipantEntities, int Seed);
public sealed record DialogueDiagnostic(string Code, string Path, string Message);
public sealed record RenderedDialogueNode(Guid NodeId, string SpeakerEntityId, string Text, IReadOnlyList<DialogueEdge> AvailableEdges, IReadOnlyList<DialogueDiagnostic> Diagnostics);

public static class DialogueGraphService
{
    public static DialogueExpression BuildExpression(DialogueExpressionKind kind, string left, string? right = null, IReadOnlyList<DialogueExpression>? children = null) => new(kind, left.Trim(), right?.Trim(), children ?? [], $"builder:{kind}:{left}:{right}");
    public static DialogueAction BuildAction(DialogueActionKind kind, string target, string value) => new(kind, target.Trim(), value.Trim(), $"builder:{kind}:{target}:{value}");

    public static RenderedDialogueNode Render(DialogueGraph graph, Guid nodeId, DialogueContext context)
    {
        if (!graph.Nodes.TryGetValue(nodeId, out var node)) throw new KeyNotFoundException($"Dialogue node {nodeId} does not exist.");
        var diagnostics = new List<DialogueDiagnostic>(); var text = node.TextTemplate;
        foreach (var token in node.Tokens)
        {
            var marker = "{{" + token.Name + "}}"; object? raw = context.Values.GetValueOrDefault(token.Name) ?? token.DefaultValue;
            if (raw is null && token.Required) { diagnostics.Add(new("RPG_DIALOGUE_TOKEN_MISSING", $"dialogues/{graph.Id}/nodes/{node.Id}/tokens/{token.Name}", "Required token has no value.")); continue; }
            text = text.Replace(marker, Escape(Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture) ?? ""), StringComparison.Ordinal);
        }
        var speaker = ResolveSpeaker(graph.Participants, node.SpeakerRole, context.ParticipantEntities);
        if (speaker is null) diagnostics.Add(new("RPG_DIALOGUE_PARTICIPANT_MISSING", $"dialogues/{graph.Id}/nodes/{node.Id}/speaker", "Speaker binding cannot resolve."));
        var valid = node.Edges.Where(edge => edge.Condition is null || Evaluate(edge.Condition, context.Values)).OrderBy(edge => edge.Priority).ThenBy(edge => edge.TargetNodeId).ToArray();
        var selected = node.EvaluationMode switch
        {
            DialogueEvaluationMode.FirstValid => valid.Take(1).ToArray(),
            DialogueEvaluationMode.AllValid or DialogueEvaluationMode.Manual => valid,
            DialogueEvaluationMode.Weighted => PickWeighted(valid, context.Seed),
            DialogueEvaluationMode.RandomDeterministic => valid.Length == 0 ? [] : [valid[new Random(context.Seed).Next(valid.Length)]],
            _ => []
        };
        return new(node.Id, speaker ?? "", text, selected, diagnostics);
    }

    public static IReadOnlyList<DialogueDiagnostic> Validate(DialogueGraph graph)
    {
        var root = $"dialogues/{graph.Id}"; var result = new List<DialogueDiagnostic>();
        if (graph.Id == Guid.Empty || graph.SchemaVersion != 1 || string.IsNullOrWhiteSpace(graph.Provenance) || string.IsNullOrWhiteSpace(graph.Slug) || !graph.Nodes.ContainsKey(graph.EntryNodeId)) result.Add(new("RPG_DIALOGUE_IDENTITY_INVALID", root, "Dialogue identity and entry node are required."));
        if (graph.Participants.Select(item => item.Role).Distinct(StringComparer.Ordinal).Count() != graph.Participants.Count || graph.Participants.Any(item => string.IsNullOrWhiteSpace(item.Role) || string.IsNullOrWhiteSpace(item.Value))) result.Add(new("RPG_DIALOGUE_PARTICIPANT_INVALID", $"{root}/participants", "Participant roles and bindings must be unique and complete."));
        foreach (var node in graph.Nodes.Values)
        {
            var path = $"{root}/nodes/{node.Id}";
            if (node.Id == Guid.Empty || string.IsNullOrWhiteSpace(node.SpeakerRole) || string.IsNullOrWhiteSpace(node.TextTemplate) || !graph.Participants.Any(item => item.Role == node.SpeakerRole)) result.Add(new("RPG_DIALOGUE_NODE_INVALID", path, "Node speaker and text are required."));
            if (node.Tokens.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() != node.Tokens.Count || node.Tokens.Any(item => !node.TextTemplate.Contains("{{" + item.Name + "}}", StringComparison.Ordinal))) result.Add(new("RPG_DIALOGUE_TOKEN_INVALID", $"{path}/tokens", "Tokens must be unique and referenced by the template."));
            if (node.Edges.Any(edge => !graph.Nodes.ContainsKey(edge.TargetNodeId) || edge.Priority < 0 || edge.Weight < 0 || edge.Actions.Any(action => string.IsNullOrWhiteSpace(action.SourceDescription)))) result.Add(new("RPG_DIALOGUE_EDGE_INVALID", $"{path}/edges", "Edges, priorities, weights and generated action sources are invalid."));
            if (string.IsNullOrWhiteSpace(node.Notes.AuthorMention) || string.IsNullOrWhiteSpace(node.Notes.Intent)) result.Add(new("RPG_DIALOGUE_NOTES_INVALID", $"{path}/notes", "Author and intent notes are required."));
        }
        return result;
    }

    private static bool Evaluate(DialogueExpression expression, IReadOnlyDictionary<string, object> values)
    {
        var left = values.GetValueOrDefault(expression.Left); var right = expression.Right;
        return expression.Kind switch
        {
            DialogueExpressionKind.Equals => string.Equals(Convert.ToString(left), right, StringComparison.Ordinal),
            DialogueExpressionKind.NotEquals => !string.Equals(Convert.ToString(left), right, StringComparison.Ordinal),
            DialogueExpressionKind.GreaterThan => Convert.ToDouble(left) > Convert.ToDouble(right),
            DialogueExpressionKind.LessThan => Convert.ToDouble(left) < Convert.ToDouble(right),
            DialogueExpressionKind.HasTag or DialogueExpressionKind.HasItem => left is IEnumerable<string> set && set.Contains(right ?? "", StringComparer.Ordinal),
            DialogueExpressionKind.Variable => left is true,
            DialogueExpressionKind.And => expression.Children.All(child => Evaluate(child, values)),
            DialogueExpressionKind.Or => expression.Children.Any(child => Evaluate(child, values)),
            DialogueExpressionKind.Not => expression.Children.Count == 1 && !Evaluate(expression.Children[0], values),
            _ => false
        };
    }

    private static string? ResolveSpeaker(IReadOnlyList<ParticipantBinding> bindings, string role, IReadOnlyDictionary<string, string> entities)
    {
        var binding = bindings.FirstOrDefault(item => item.Role == role); if (binding is null) return null;
        if (entities.TryGetValue(role, out var entity)) return entity;
        return binding.FallbackRole is { } fallback && entities.TryGetValue(fallback, out entity) ? entity : null;
    }
    private static DialogueEdge[] PickWeighted(DialogueEdge[] edges, int seed) { var total = edges.Sum(item => item.Weight); if (total <= 0) return []; var value = new Random(seed).NextDouble() * total; foreach (var edge in edges) { value -= edge.Weight; if (value <= 0) return [edge]; } return [edges[^1]]; }
    private static string Escape(string value) => value.Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal).Replace("{", "&#123;", StringComparison.Ordinal).Replace("}", "&#125;", StringComparison.Ordinal);
}
