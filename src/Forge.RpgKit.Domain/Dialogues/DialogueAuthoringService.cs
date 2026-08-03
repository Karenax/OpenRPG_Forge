namespace Forge.RpgKit.Domain.Dialogues;

public enum DialogueConsequenceKind { Quest, Faction, Item, World }
public enum DialogueConflictResolution { Ours, Theirs, Manual }
public sealed record SubDialogueContract(Guid DialogueId, int Version, IReadOnlyDictionary<string, DialogueTokenType> Inputs, IReadOnlyDictionary<string, DialogueTokenType> Outputs, IReadOnlySet<string> RequiredVariables);
public sealed record SubDialogueInvocation(Guid NodeId, SubDialogueContract Contract, IReadOnlyDictionary<string, object> Inputs, IReadOnlyDictionary<string, string> OutputBindings);
public sealed record DialogueConsequence(DialogueConsequenceKind Kind, string TargetId, string Operation, string Value, string Explanation);
public sealed record DialogueImpact(Guid SourceNodeId, DialogueConsequence Consequence, IReadOnlyList<string> AffectedResources);
public sealed record DialoguePath(IReadOnlyList<Guid> NodeIds, IReadOnlyList<DialogueExpression> RequiredConditions, bool Reachable, string Reason);
public sealed record DialogueCoverage(IReadOnlyList<DialoguePath> Paths, IReadOnlySet<Guid> CoveredNodes, IReadOnlySet<Guid> UncoveredNodes, IReadOnlyList<DialoguePath> ImpossiblePaths, double Ratio);
public sealed record DialogueNodeRevision(Guid NodeId, long Revision, string AuthorId, string CanonicalJson, DateTimeOffset At);
public sealed record DialogueLock(Guid NodeId, string OwnerId, DateTimeOffset ExpiresAt);
public sealed record DialogueComment(Guid Id, Guid NodeId, string AuthorId, string Text, bool Resolved, DateTimeOffset At);
public sealed record DialogueSemanticConflict(Guid NodeId, string Field, string BaseValue, string OursValue, string TheirsValue);
public sealed record DialogueMergePreview(IReadOnlyList<DialogueSemanticConflict> Conflicts, IReadOnlyDictionary<Guid, string> MergedNodes, string SemanticDiff, bool CanApply);
public sealed record DialogueAiBrief(string Synopsis, IReadOnlyList<string> RequiredBeats, IReadOnlyList<string> StyleRules, IReadOnlyList<string> ContextCitations, string Locale, string AuthorId);
public sealed record DialogueAiDraft(DialogueGraph Graph, IReadOnlyList<string> Citations, IReadOnlyList<DialogueDiagnostic> Lint, IReadOnlyList<string> StyleViolations, string SemanticDiff, bool CanApply, bool Applied);

public static class DialogueAuthoringService
{
    public static IReadOnlyList<DialogueDiagnostic> ValidateSubDialogue(SubDialogueInvocation invocation)
    {
        var root = $"dialogues/subdialogues/{invocation.Contract.DialogueId}"; var result = new List<DialogueDiagnostic>();
        if (invocation.Contract.DialogueId == Guid.Empty || invocation.Contract.Version <= 0) result.Add(new("RPG_SUBDIALOGUE_IDENTITY_INVALID", root, "Sub-dialogue identity and version are required."));
        foreach (var input in invocation.Contract.Inputs) if (!invocation.Inputs.TryGetValue(input.Key, out var value) || !Matches(input.Value, value)) result.Add(new("RPG_SUBDIALOGUE_INPUT_INVALID", $"{root}/inputs/{input.Key}", "Input is missing or has the wrong type."));
        foreach (var output in invocation.Contract.Outputs) if (!invocation.OutputBindings.TryGetValue(output.Key, out var binding) || string.IsNullOrWhiteSpace(binding)) result.Add(new("RPG_SUBDIALOGUE_OUTPUT_INVALID", $"{root}/outputs/{output.Key}", "Output binding is required."));
        return result;
    }

    public static IReadOnlyList<DialogueImpact> AnalyzeImpact(Guid nodeId, IReadOnlyList<DialogueConsequence> consequences, IReadOnlyDictionary<string, IReadOnlyList<string>> reverseDependencies) => consequences.Select(item => new DialogueImpact(nodeId, item, reverseDependencies.GetValueOrDefault(item.TargetId) ?? [])).OrderBy(item => item.Consequence.Kind).ThenBy(item => item.Consequence.TargetId, StringComparer.Ordinal).ToArray();

    public static DialogueCoverage MeasureCoverage(DialogueGraph graph, IReadOnlySet<Guid> exercisedNodes, int maximumDepth = 64)
    {
        var paths = new List<DialoguePath>(); Walk(graph.EntryNodeId, [], [], new HashSet<Guid>(), 0); var all = graph.Nodes.Keys.ToHashSet(); var covered = all.Where(exercisedNodes.Contains).ToHashSet(); var uncovered = all.Except(covered).ToHashSet();
        return new(paths, covered, uncovered, paths.Where(item => !item.Reachable).ToArray(), all.Count == 0 ? 1 : (double)covered.Count / all.Count);
        void Walk(Guid id, IReadOnlyList<Guid> prefix, IReadOnlyList<DialogueExpression> conditions, HashSet<Guid> seen, int depth)
        {
            var path = prefix.Append(id).ToArray(); if (depth > maximumDepth) { paths.Add(new(path, conditions, false, "maximum-depth")); return; }
            if (!graph.Nodes.TryGetValue(id, out var node)) { paths.Add(new(path, conditions, false, "missing-node")); return; }
            if (!seen.Add(id)) { paths.Add(new(path, conditions, false, "cycle")); return; }
            if (node.Edges.Count == 0) { paths.Add(new(path, conditions, true, "terminal")); return; }
            foreach (var edge in node.Edges) Walk(edge.TargetNodeId, path, edge.Condition is null ? conditions : conditions.Append(edge.Condition).ToArray(), new HashSet<Guid>(seen), depth + 1);
        }
    }

    public static DialogueMergePreview Merge(DialogueNodeRevision baseline, DialogueNodeRevision ours, DialogueNodeRevision theirs, DialogueConflictResolution resolution, string? manualJson = null)
    {
        if (baseline.NodeId != ours.NodeId || ours.NodeId != theirs.NodeId) throw new ArgumentException("Node identities do not match.");
        var changedOurs = ours.CanonicalJson != baseline.CanonicalJson; var changedTheirs = theirs.CanonicalJson != baseline.CanonicalJson; var conflict = changedOurs && changedTheirs && ours.CanonicalJson != theirs.CanonicalJson;
        var conflicts = conflict ? new[] { new DialogueSemanticConflict(ours.NodeId, "canonical-json", baseline.CanonicalJson, ours.CanonicalJson, theirs.CanonicalJson) } : [];
        var selected = resolution switch { DialogueConflictResolution.Ours => ours.CanonicalJson, DialogueConflictResolution.Theirs => theirs.CanonicalJson, DialogueConflictResolution.Manual when !string.IsNullOrWhiteSpace(manualJson) => manualJson, _ => changedOurs ? ours.CanonicalJson : theirs.CanonicalJson };
        var canApply = !conflict || resolution != DialogueConflictResolution.Manual || !string.IsNullOrWhiteSpace(manualJson);
        return new(conflicts, new Dictionary<Guid, string> { [ours.NodeId] = selected }, $"merge-node:{ours.NodeId}|conflicts:{conflicts.Length}|resolution:{resolution}", canApply);
    }

    public static bool CanEdit(DialogueLock? nodeLock, string authorId, DateTimeOffset at) => nodeLock is null || nodeLock.ExpiresAt <= at || nodeLock.OwnerId == authorId;

    public static DialogueAiDraft PreviewAi(DialogueAiBrief brief, Func<DialogueAiBrief, DialogueGraph> generator)
    {
        if (brief.ContextCitations.Count == 0 || brief.StyleRules.Count == 0 || string.IsNullOrWhiteSpace(brief.Synopsis)) throw new ArgumentException("AI brief requires synopsis, citations and style rules.");
        var graph = generator(brief); var lint = DialogueGraphService.Validate(graph); var violations = brief.StyleRules.Where(rule => graph.Nodes.Values.All(node => !node.Notes.Source.Contains(rule, StringComparison.OrdinalIgnoreCase))).Select(rule => $"unproven-style-rule:{rule}").ToArray();
        return new(graph, brief.ContextCitations, lint, violations, $"ai-preview:add-dialogue:{graph.Id}|nodes:{graph.Nodes.Count}|citations:{brief.ContextCitations.Count}", lint.Count == 0 && violations.Length == 0, false);
    }

    public static DialogueAiDraft ApplyAi(DialogueAiDraft preview)
    {
        if (!preview.CanApply || preview.Applied) throw new InvalidOperationException("AI draft is not applicable.");
        return preview with { Applied = true, SemanticDiff = preview.SemanticDiff.Replace("ai-preview:", "ai-apply:", StringComparison.Ordinal) };
    }

    private static bool Matches(DialogueTokenType type, object value) => type switch { DialogueTokenType.Text or DialogueTokenType.EntityName or DialogueTokenType.LocalizedText => value is string, DialogueTokenType.Integer => value is int or long, DialogueTokenType.Decimal => value is float or double or decimal, DialogueTokenType.Boolean => value is bool, _ => false };
}
