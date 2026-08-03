using System.Text.RegularExpressions;

namespace Forge.RpgKit.Domain.Dialogues;

public enum DialogueAbortReason { PlayerCancelled, ParticipantUnavailable, CombatStarted, AreaChanged, Scripted, Error }
public enum ConversationMemoryScope { Dialogue, Participant, Player, Party, World }
public enum ConversationMemoryDuration { Node, Session, Area, Campaign, Permanent }
public enum DialogueBadge { Condition, Action, Voice, Localization, Error, Link }
public enum SkillCheckVisibility { Hidden, DifficultyOnly, ResultOnly, FullExplanation }
public sealed record DialogueResumeState(Guid DialogueId, Guid NodeId, IReadOnlyDictionary<string, object> MemorySnapshot, int Seed);
public sealed record DialogueCompleted(Guid EventId, Guid DialogueId, string PlayerId, DateTimeOffset At, IReadOnlyList<DialogueAction> Actions);
public sealed record DialogueAborted(Guid EventId, Guid DialogueId, string PlayerId, DialogueAbortReason Reason, bool RolledBack, DialogueResumeState? ResumeState, DateTimeOffset At);
public sealed record DialogueSearchQuery(string Pattern, bool Regex, bool IgnoreCase, IReadOnlySet<string> SpeakerRoles, IReadOnlySet<DialogueExpressionKind> ConditionKinds, IReadOnlySet<DialogueActionKind> ActionKinds);
public sealed record DialogueSearchHit(Guid DialogueId, Guid NodeId, string Field, string Value, int Index, int Length);
public sealed record DialogueReplacePreview(IReadOnlyList<DialogueSearchHit> Hits, IReadOnlyDictionary<(Guid DialogueId, Guid NodeId), string> Replacements, string SemanticDiff);
public sealed record DialogueReviewFilter(IReadOnlySet<DialogueBadge> Badges, IReadOnlySet<string> SpeakerRoles, bool ReviewMode);
public sealed record DialogueReviewEntry(Guid DialogueId, Guid NodeId, IReadOnlySet<DialogueBadge> Badges, DialogueEditorialStatus Status);
public sealed record ConversationMemoryKey(ConversationMemoryScope Scope, string ScopeId, string Name);
public sealed record ConversationMemoryValue(DialogueTokenType Type, object Value, ConversationMemoryDuration Duration, DateTimeOffset WrittenAt, DateTimeOffset? ExpiresAt);
public sealed record ConversationMemory(IReadOnlyDictionary<ConversationMemoryKey, ConversationMemoryValue> Values);
public sealed record SkillCheckNode(Guid Id, string SkillId, double Difficulty, string? OpponentId, bool Advantage, SkillCheckVisibility Visibility, Guid SuccessNodeId, Guid FailureNodeId);
public sealed record SkillCheckResult(bool Success, double Roll, double Total, double Difficulty, string Explanation, Guid TargetNodeId);

public static class DialogueExperienceService
{
    public static DialogueCompleted Complete(DialogueGraph graph, string playerId, IReadOnlyList<DialogueAction> actions, DateTimeOffset at) => new(Guid.NewGuid(), graph.Id, playerId, at, actions);
    public static DialogueAborted Abort(DialogueGraph graph, string playerId, Guid currentNodeId, DialogueAbortReason reason, bool rollback, bool resumable, ConversationMemory memory, int seed, DateTimeOffset at) => new(Guid.NewGuid(), graph.Id, playerId, reason, rollback, resumable ? new(graph.Id, currentNodeId, memory.Values.ToDictionary(item => $"{item.Key.Scope}:{item.Key.ScopeId}:{item.Key.Name}", item => item.Value.Value), seed) : null, at);

    public static IReadOnlyList<DialogueSearchHit> Search(IReadOnlyList<DialogueGraph> graphs, DialogueSearchQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.Pattern) || query.Pattern.Length > 512) throw new ArgumentException("Search pattern is invalid.", nameof(query));
        var options = query.IgnoreCase ? RegexOptions.IgnoreCase | RegexOptions.CultureInvariant : RegexOptions.CultureInvariant;
        var pattern = query.Regex ? query.Pattern : Regex.Escape(query.Pattern); var regex = new Regex(pattern, options, TimeSpan.FromMilliseconds(100)); var hits = new List<DialogueSearchHit>();
        foreach (var graph in graphs.OrderBy(item => item.Slug, StringComparer.Ordinal)) foreach (var node in graph.Nodes.Values.OrderBy(item => item.Id))
        {
            if (query.SpeakerRoles.Count > 0 && !query.SpeakerRoles.Contains(node.SpeakerRole)) continue;
            if (query.ConditionKinds.Count > 0 && !node.Edges.Any(edge => edge.Condition is { } condition && query.ConditionKinds.Contains(condition.Kind))) continue;
            if (query.ActionKinds.Count > 0 && !node.Edges.SelectMany(edge => edge.Actions).Any(action => query.ActionKinds.Contains(action.Kind))) continue;
            foreach (Match match in regex.Matches(node.TextTemplate)) hits.Add(new(graph.Id, node.Id, "textTemplate", node.TextTemplate, match.Index, match.Length));
        }
        return hits;
    }

    public static DialogueReplacePreview PreviewReplace(IReadOnlyList<DialogueGraph> graphs, DialogueSearchQuery query, string replacement)
    {
        var hits = Search(graphs, query); var groups = hits.GroupBy(item => (item.DialogueId, item.NodeId)); var replacements = new Dictionary<(Guid, Guid), string>();
        foreach (var group in groups)
        {
            var source = group.First().Value; foreach (var hit in group.OrderByDescending(item => item.Index)) source = source.Remove(hit.Index, hit.Length).Insert(hit.Index, replacement); replacements[group.Key] = source;
        }
        return new(hits, replacements, $"replace-dialogue-text|hits:{hits.Count}|nodes:{replacements.Count}|preview-only:true");
    }

    public static IReadOnlyList<DialogueReviewEntry> Review(IReadOnlyList<DialogueGraph> graphs, DialogueReviewFilter filter)
    {
        return graphs.SelectMany(graph => graph.Nodes.Values.Select(node => new DialogueReviewEntry(graph.Id, node.Id, Badges(node), node.Notes.Status))).Where(entry => (filter.Badges.Count == 0 || filter.Badges.All(entry.Badges.Contains)) && (!filter.ReviewMode || entry.Status != DialogueEditorialStatus.Approved)).OrderBy(entry => entry.DialogueId).ThenBy(entry => entry.NodeId).ToArray();
    }

    public static ConversationMemory Write(ConversationMemory memory, ConversationMemoryKey key, ConversationMemoryValue value)
    {
        if (string.IsNullOrWhiteSpace(key.ScopeId) || string.IsNullOrWhiteSpace(key.Name) || !TypeMatches(value.Type, value.Value)) throw new ArgumentException("Memory key or typed value is invalid.");
        var values = memory.Values.ToDictionary(); values[key] = value; return new(values);
    }

    public static object? Read(ConversationMemory memory, ConversationMemoryKey key, DateTimeOffset at) => memory.Values.TryGetValue(key, out var value) && (value.ExpiresAt is null || value.ExpiresAt > at) ? value.Value : null;

    public static SkillCheckResult Resolve(SkillCheckNode check, double actorScore, double opponentScore, int seed)
    {
        var random = new Random(seed); var first = random.Next(1, 21); var second = random.Next(1, 21); var roll = check.Advantage ? Math.Max(first, second) : first; var total = roll + actorScore - opponentScore; var success = total >= check.Difficulty;
        var explanation = check.Visibility switch { SkillCheckVisibility.Hidden => "", SkillCheckVisibility.DifficultyOnly => $"Difficulty {check.Difficulty:0.##}", SkillCheckVisibility.ResultOnly => success ? "Success" : "Failure", _ => $"Roll {roll:0} + skill {actorScore:0.##} - opponent {opponentScore:0.##} = {total:0.##} vs {check.Difficulty:0.##}: {(success ? "success" : "failure")}" };
        return new(success, roll, total, check.Difficulty, explanation, success ? check.SuccessNodeId : check.FailureNodeId);
    }

    private static IReadOnlySet<DialogueBadge> Badges(DialogueNode node)
    {
        var badges = new HashSet<DialogueBadge>(); if (node.Edges.Any(edge => edge.Condition is not null)) badges.Add(DialogueBadge.Condition); if (node.Edges.Any(edge => edge.Actions.Count > 0)) badges.Add(DialogueBadge.Action); if (node.Tokens.Count > 0) badges.Add(DialogueBadge.Localization); if (node.Edges.Any(edge => edge.TargetNodeId != Guid.Empty)) badges.Add(DialogueBadge.Link); if (node.Notes.Todos.Any(todo => todo.Contains("voice", StringComparison.OrdinalIgnoreCase))) badges.Add(DialogueBadge.Voice); if (string.IsNullOrWhiteSpace(node.TextTemplate)) badges.Add(DialogueBadge.Error); return badges;
    }
    private static bool TypeMatches(DialogueTokenType type, object value) => type switch { DialogueTokenType.Text or DialogueTokenType.EntityName or DialogueTokenType.LocalizedText => value is string, DialogueTokenType.Integer => value is int or long, DialogueTokenType.Decimal => value is float or double or decimal, DialogueTokenType.Boolean => value is bool, _ => false };
}
