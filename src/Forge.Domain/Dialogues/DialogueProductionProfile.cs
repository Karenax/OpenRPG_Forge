using System.Text.Json;
using Forge.Domain.Validation;

namespace Forge.Domain.Dialogues;

public enum DialogueSubgraphParameterDirection { Input, Output }
public enum DialogueConsequenceTargetKind { Quest, Faction, Item, World }
public enum DialogueCoverageEvidenceKind { Test, Session }
public enum DialogueConflictResolution { Ours, Theirs, Manual }
public enum DialogueAiAuthorizationLevel { SuggestOnly, Draft, AuthorApply }

public sealed record DialogueSubgraphParameter(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Name,
    DialogueTextTokenType Type,
    DialogueSubgraphParameterDirection Direction,
    bool Required,
    string? DefaultValue = null)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record DialogueSubgraphTestCase(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Name,
    IReadOnlyDictionary<string, string> Inputs,
    IReadOnlyDictionary<string, string> ExpectedOutputs)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record DialogueSubgraphContract(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Key,
    int Version,
    IReadOnlyList<DialogueSubgraphParameter> Parameters,
    IReadOnlyList<string> RequiredVariables,
    IReadOnlyList<DialogueSubgraphTestCase> Tests)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record DialogueSubgraphInvocation(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    DialogueNodeId NodeId,
    Guid ContractId,
    IReadOnlyDictionary<string, string> InputBindings,
    IReadOnlyDictionary<string, string> OutputBindings)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record DialogueTypedConsequence(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    DialogueNodeId NodeId,
    DialogueConsequenceTargetKind Kind,
    string TargetId,
    string Operation,
    string Value,
    string Explanation)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record DialogueCoverageEvidence(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Name,
    DialogueCoverageEvidenceKind Kind,
    IReadOnlyList<DialogueNodeId> VisitedNodes,
    IReadOnlyDictionary<string, string> State)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record DialogueCoveragePolicy(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    int MaximumDepth,
    IReadOnlyDictionary<string, string> RequiredStates,
    IReadOnlyList<DialogueCoverageEvidence> Evidence)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record DialogueNodeRevision(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    DialogueNodeId NodeId,
    long Revision,
    string AuthorId,
    string CanonicalJson,
    DateTimeOffset At)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record DialogueNodeLock(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    DialogueNodeId NodeId,
    string OwnerId,
    DateTimeOffset ExpiresAt)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record DialogueNodeComment(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    DialogueNodeId NodeId,
    string AuthorId,
    string Text,
    bool Resolved,
    DateTimeOffset At)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record DialogueSemanticConflict(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    DialogueNodeId NodeId,
    string Field,
    string BaseValue,
    string OursValue,
    string TheirsValue,
    DialogueConflictResolution Resolution,
    string? ManualValue,
    bool Resolved)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record DialogueCollaborationProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    IReadOnlyList<DialogueNodeRevision> Revisions,
    IReadOnlyList<DialogueNodeLock> Locks,
    IReadOnlyList<DialogueNodeComment> Comments,
    IReadOnlyList<DialogueSemanticConflict> Conflicts)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record DialogueAiPlan(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    Guid TransactionId,
    Guid PlanId,
    DialogueAiAuthorizationLevel Authorization,
    string Synopsis,
    IReadOnlyList<string> RequiredBeats,
    IReadOnlyList<string> StyleRules,
    IReadOnlyList<string> ContextCitations,
    string SemanticDiff,
    IReadOnlyList<string> Lint,
    bool PreviewOnly,
    bool Applied)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record DialogueProductionProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool Enabled,
    bool RequiredAtRuntime,
    IReadOnlyList<DialogueSubgraphContract> Subgraphs,
    IReadOnlyList<DialogueSubgraphInvocation> Invocations,
    IReadOnlyList<DialogueTypedConsequence> Consequences,
    DialogueCoveragePolicy Coverage,
    DialogueCollaborationProfile Collaboration,
    IReadOnlyList<DialogueAiPlan> AiPlans)
{
    public const int CurrentSchemaVersion = 1;

    public static DialogueProductionProfile CreateDefault(DialogueDefinition dialogue, string provenance)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        return new(
            StableId($"{dialogue.Id}:production"), CurrentSchemaVersion, provenance, true, false, [], [], [],
            new(StableId($"{dialogue.Id}:production:coverage"), DialogueCoveragePolicy.CurrentSchemaVersion,
                provenance, 64, new Dictionary<string, string>(StringComparer.Ordinal), []),
            new(StableId($"{dialogue.Id}:production:collaboration"),
                DialogueCollaborationProfile.CurrentSchemaVersion, provenance, [], [], [], []),
            []);
    }

    internal static Guid StableId(string value) =>
        new(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))[..16]);
}

public sealed record DialogueSubgraphTestResult(Guid TestId, bool Passed, IReadOnlyList<string> Diagnostics);
public sealed record DialogueImpactAnalysis(
    DialogueTypedConsequence Consequence,
    IReadOnlyList<string> AffectedResources);
public sealed record DialogueCoveragePath(
    IReadOnlyList<DialogueNodeId> Nodes,
    IReadOnlyDictionary<string, string> RequiredState,
    bool Reachable,
    string Reason);
public sealed record DialogueCoverageReport(
    IReadOnlyList<DialogueCoveragePath> Paths,
    IReadOnlySet<DialogueNodeId> CoveredNodes,
    IReadOnlySet<DialogueNodeId> UncoveredNodes,
    IReadOnlyList<DialogueCoveragePath> ImpossiblePaths,
    double Ratio);
public sealed record DialogueMergePreview(
    DialogueNodeId NodeId,
    string CanonicalJson,
    string SemanticDiff,
    bool CanApply);

public static class DialogueProductionRuntimeService
{
    public static DialogueSubgraphTestResult TestSubgraph(
        DialogueProductionProfile profile,
        Guid contractId,
        Guid testId)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var contract = profile.Subgraphs.SingleOrDefault(item => item.Id == contractId)
            ?? throw new KeyNotFoundException("DIALOGUE_SUBGRAPH_CONTRACT_MISSING");
        var test = contract.Tests.SingleOrDefault(item => item.Id == testId)
            ?? throw new KeyNotFoundException("DIALOGUE_SUBGRAPH_TEST_MISSING");
        var diagnostics = new List<string>();
        foreach (var parameter in contract.Parameters.Where(item =>
                     item.Direction == DialogueSubgraphParameterDirection.Input && item.Required))
        {
            if (!test.Inputs.TryGetValue(parameter.Name, out var value) ||
                !Matches(parameter.Type, value)) diagnostics.Add($"input:{parameter.Name}:invalid");
        }
        foreach (var parameter in contract.Parameters.Where(item =>
                     item.Direction == DialogueSubgraphParameterDirection.Output && item.Required))
        {
            if (!test.ExpectedOutputs.TryGetValue(parameter.Name, out var value) ||
                !Matches(parameter.Type, value)) diagnostics.Add($"output:{parameter.Name}:invalid");
        }
        return new(test.Id, diagnostics.Count == 0, diagnostics);
    }

    public static IReadOnlyList<DialogueImpactAnalysis> AnalyzeImpact(
        DialogueProductionProfile profile,
        IReadOnlyDictionary<string, IReadOnlyList<string>> reverseDependencies) =>
        profile.Consequences
            .Select(item => new DialogueImpactAnalysis(item,
                reverseDependencies.GetValueOrDefault(item.TargetId) ?? []))
            .OrderBy(item => item.Consequence.Kind)
            .ThenBy(item => item.Consequence.TargetId, StringComparer.Ordinal)
            .ToArray();

    public static DialogueCoverageReport MeasureCoverage(DialogueDefinition dialogue)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        var profile = dialogue.Production ?? throw new InvalidOperationException("DIALOGUE_PRODUCTION_MISSING");
        var nodes = dialogue.Nodes.ToDictionary(item => item.Id);
        var paths = new List<DialogueCoveragePath>();
        Walk(dialogue.EntryNodeId, [], new Dictionary<string, string>(profile.Coverage.RequiredStates,
            StringComparer.Ordinal), new HashSet<DialogueNodeId>(), 0);
        var all = nodes.Keys.ToHashSet();
        var covered = profile.Coverage.Evidence.SelectMany(item => item.VisitedNodes).Where(all.Contains).ToHashSet();
        return new(paths, covered, all.Except(covered).ToHashSet(),
            paths.Where(item => !item.Reachable).ToArray(), all.Count == 0 ? 1 : (double)covered.Count / all.Count);

        void Walk(DialogueNodeId id, IReadOnlyList<DialogueNodeId> prefix,
            IReadOnlyDictionary<string, string> state, IReadOnlySet<DialogueNodeId> seen, int depth)
        {
            var path = prefix.Append(id).ToArray();
            if (depth > profile.Coverage.MaximumDepth)
            {
                paths.Add(new(path, state, false, "maximum-depth"));
                return;
            }
            if (!nodes.TryGetValue(id, out var node))
            {
                paths.Add(new(path, state, false, "missing-node"));
                return;
            }
            if (seen.Contains(id))
            {
                paths.Add(new(path, state, false, "cycle"));
                return;
            }
            if (node.Choices.Count == 0)
            {
                paths.Add(new(path, state, true, "terminal"));
                return;
            }
            var nextSeen = seen.Append(id).ToHashSet();
            foreach (var choice in node.Choices)
            {
                var nextState = new Dictionary<string, string>(state, StringComparer.Ordinal);
                var contradiction = false;
                foreach (var condition in choice.Conditions)
                {
                    var key = $"condition:{condition.Kind}:{condition.Key}";
                    var expected = condition.Negated ? $"!{condition.ExpectedValue}" : condition.ExpectedValue;
                    if (nextState.TryGetValue(key, out var existing) && existing != expected) contradiction = true;
                    nextState[key] = expected;
                }
                if (contradiction)
                {
                    paths.Add(new(path, nextState, false, "contradictory-state"));
                    continue;
                }
                if (choice.TargetNodeId is null)
                    paths.Add(new(path, nextState, true, "terminal-choice"));
                else
                    Walk(choice.TargetNodeId, path, nextState, nextSeen, depth + 1);
            }
        }
    }

    public static bool CanEdit(DialogueNodeLock? nodeLock, string authorId, DateTimeOffset at) =>
        nodeLock is null || nodeLock.ExpiresAt <= at ||
        string.Equals(nodeLock.OwnerId, authorId, StringComparison.Ordinal);

    public static DialogueMergePreview ResolveConflict(DialogueSemanticConflict conflict)
    {
        ArgumentNullException.ThrowIfNull(conflict);
        var selected = conflict.Resolution switch
        {
            DialogueConflictResolution.Ours => conflict.OursValue,
            DialogueConflictResolution.Theirs => conflict.TheirsValue,
            DialogueConflictResolution.Manual when !string.IsNullOrWhiteSpace(conflict.ManualValue) =>
                conflict.ManualValue,
            _ => string.Empty,
        };
        return new(conflict.NodeId, selected,
            $"node:{conflict.NodeId}|field:{conflict.Field}|resolution:{conflict.Resolution}",
            conflict.Resolved && selected.Length > 0 && IsJson(selected));
    }

    public static DialogueAiPlan PreviewAi(DialogueAiPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var lint = new List<string>();
        if (string.IsNullOrWhiteSpace(plan.Synopsis)) lint.Add("dialogue.ai.synopsis_missing");
        if (plan.ContextCitations.Count == 0) lint.Add("dialogue.ai.citations_missing");
        if (plan.StyleRules.Count == 0) lint.Add("dialogue.ai.style_missing");
        if (plan.RequiredBeats.Count == 0) lint.Add("dialogue.ai.beats_missing");
        return plan with { PreviewOnly = true, Applied = false, Lint = lint };
    }

    public static DialogueProductionProfile ApplyAi(DialogueProductionProfile profile, Guid planId)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var plan = profile.AiPlans.SingleOrDefault(item => item.PlanId == planId)
            ?? throw new KeyNotFoundException("DIALOGUE_AI_PLAN_MISSING");
        if (plan.Applied) throw new InvalidOperationException("DIALOGUE_AI_PLAN_ALREADY_APPLIED");
        var preview = PreviewAi(plan);
        if (preview.Authorization != DialogueAiAuthorizationLevel.AuthorApply || preview.Lint.Count > 0 ||
            preview.Applied || !preview.PreviewOnly)
            throw new InvalidOperationException("DIALOGUE_AI_PLAN_NOT_APPLICABLE");
        var applied = preview with { PreviewOnly = false, Applied = true };
        return profile with { AiPlans = profile.AiPlans.Select(item => item.PlanId == planId ? applied : item).ToArray() };
    }

    private static bool Matches(DialogueTextTokenType type, string value) => type switch
    {
        DialogueTextTokenType.Integer => long.TryParse(value, out _),
        DialogueTextTokenType.Decimal => decimal.TryParse(value,
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _),
        DialogueTextTokenType.Boolean => bool.TryParse(value, out _),
        _ => !string.IsNullOrWhiteSpace(value),
    };

    internal static bool IsJson(string value)
    {
        try { using var _ = JsonDocument.Parse(value); return true; }
        catch (JsonException) { return false; }
    }
}

public static class DialogueProductionProfileValidator
{
    public const string RequiredMissingCode = "dialogue.production.required_missing";
    public const string IdentityCode = "dialogue.production.identity_invalid";
    public const string SubgraphCode = "dialogue.production.subgraph_invalid";
    public const string ConsequenceCode = "dialogue.production.consequence_invalid";
    public const string CoverageCode = "dialogue.production.coverage_invalid";
    public const string CollaborationCode = "dialogue.production.collaboration_invalid";
    public const string AiCode = "dialogue.production.ai_invalid";

    public static IReadOnlyList<DomainDiagnostic> Validate(DialogueDefinition dialogue)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        var root = $"dialogues/{dialogue.Id}/production";
        var profile = dialogue.Production;
        if (profile is null)
        {
            return dialogue.Metadata.TryGetValue("production.required", out var required) &&
                   bool.TryParse(required, out var parsed) && parsed
                ? [new(RequiredMissingCode, "Required dialogue production profile is missing.", root)]
                : [];
        }
        var result = new List<DomainDiagnostic>();
        void Add(string code, string message, string path) => result.Add(new(code, message, path));
        if (profile.Id == Guid.Empty || profile.SchemaVersion != DialogueProductionProfile.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(profile.Provenance))
            Add(IdentityCode, "Production profile requires a stable identity, supported schema and provenance.", root);

        var nodeIds = dialogue.Nodes.Select(item => item.Id).ToHashSet();
        var contractIds = profile.Subgraphs.Select(item => item.Id).ToHashSet();
        if (profile.Subgraphs.Select(item => item.Id).Distinct().Count() != profile.Subgraphs.Count ||
            profile.Subgraphs.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() != profile.Subgraphs.Count ||
            profile.Subgraphs.Any(item => item.Id == Guid.Empty || item.SchemaVersion != 1 ||
                string.IsNullOrWhiteSpace(item.Provenance) || string.IsNullOrWhiteSpace(item.Key) || item.Version <= 0 ||
                item.Parameters.Select(value => value.Id).Distinct().Count() != item.Parameters.Count ||
                item.Parameters.Select(value => (value.Direction, value.Name)).Distinct().Count() != item.Parameters.Count ||
                item.Parameters.Any(value => value.Id == Guid.Empty || value.SchemaVersion != 1 ||
                    string.IsNullOrWhiteSpace(value.Provenance) || string.IsNullOrWhiteSpace(value.Name) ||
                    !Enum.IsDefined(value.Type) || !Enum.IsDefined(value.Direction)) ||
                item.RequiredVariables.Any(value => string.IsNullOrWhiteSpace(value) || !value.Contains(':')) ||
                item.Tests.Count == 0 || item.Tests.Any(test => test.Id == Guid.Empty || test.SchemaVersion != 1 ||
                    string.IsNullOrWhiteSpace(test.Provenance) || string.IsNullOrWhiteSpace(test.Name))) ||
            profile.Invocations.Select(item => item.Id).Distinct().Count() != profile.Invocations.Count ||
            profile.Invocations.Any(item => item.Id == Guid.Empty || item.SchemaVersion != 1 ||
                string.IsNullOrWhiteSpace(item.Provenance) || !nodeIds.Contains(item.NodeId) ||
                !contractIds.Contains(item.ContractId) ||
                dialogue.Nodes.Single(node => node.Id == item.NodeId) is not { Role: DialogueNodeRole.Subgraph } node ||
                !string.Equals(node.SubgraphKey,
                    profile.Subgraphs.Single(contract => contract.Id == item.ContractId).Key,
                    StringComparison.Ordinal)) ||
            profile.Invocations.Any(invocation =>
            {
                var contract = profile.Subgraphs.Single(item => item.Id == invocation.ContractId);
                return contract.Parameters.Where(item => item.Required).Any(parameter =>
                    parameter.Direction == DialogueSubgraphParameterDirection.Input
                        ? !invocation.InputBindings.ContainsKey(parameter.Name)
                        : !invocation.OutputBindings.ContainsKey(parameter.Name));
            }))
            Add(SubgraphCode, "Sub-dialogues require unique versioned contracts, typed bindings and isolated tests.",
                $"{root}/subgraphs");

        if (profile.Consequences.Select(item => item.Id).Distinct().Count() != profile.Consequences.Count ||
            profile.Consequences.Any(item => item.Id == Guid.Empty || item.SchemaVersion != 1 ||
                string.IsNullOrWhiteSpace(item.Provenance) || !nodeIds.Contains(item.NodeId) ||
                !Enum.IsDefined(item.Kind) || string.IsNullOrWhiteSpace(item.TargetId) ||
                string.IsNullOrWhiteSpace(item.Operation) || string.IsNullOrWhiteSpace(item.Value) ||
                string.IsNullOrWhiteSpace(item.Explanation)))
            Add(ConsequenceCode, "Consequences require a resolved node and typed quest, faction, item or world impact.",
                $"{root}/consequences");

        var coverage = profile.Coverage;
        if (coverage.Id == Guid.Empty || coverage.SchemaVersion != 1 || string.IsNullOrWhiteSpace(coverage.Provenance) ||
            coverage.MaximumDepth is < 1 or > 256 || coverage.RequiredStates.Any(item =>
                string.IsNullOrWhiteSpace(item.Key) || !item.Key.Contains(':') || string.IsNullOrWhiteSpace(item.Value)) ||
            coverage.Evidence.Select(item => item.Id).Distinct().Count() != coverage.Evidence.Count ||
            coverage.Evidence.Any(item => item.Id == Guid.Empty || item.SchemaVersion != 1 ||
                string.IsNullOrWhiteSpace(item.Provenance) || string.IsNullOrWhiteSpace(item.Name) ||
                !Enum.IsDefined(item.Kind) || item.VisitedNodes.Count == 0 || item.VisitedNodes.Any(id => !nodeIds.Contains(id))))
            Add(CoverageCode, "Coverage requires bounded paths, namespaced state and resolved test or session evidence.",
                $"{root}/coverage");

        var collaboration = profile.Collaboration;
        var collaborationIds = collaboration.Revisions.Select(item => item.Id)
            .Concat(collaboration.Locks.Select(item => item.Id))
            .Concat(collaboration.Comments.Select(item => item.Id))
            .Concat(collaboration.Conflicts.Select(item => item.Id)).ToArray();
        if (collaboration.Id == Guid.Empty || collaboration.SchemaVersion != 1 ||
            string.IsNullOrWhiteSpace(collaboration.Provenance) ||
            collaborationIds.Contains(Guid.Empty) || collaborationIds.Distinct().Count() != collaborationIds.Length ||
            collaboration.Revisions.Any(item => item.SchemaVersion != 1 || !nodeIds.Contains(item.NodeId) ||
                item.Revision < 0 || string.IsNullOrWhiteSpace(item.AuthorId) ||
                string.IsNullOrWhiteSpace(item.Provenance) || !DialogueProductionRuntimeService.IsJson(item.CanonicalJson)) ||
            collaboration.Locks.Any(item => item.SchemaVersion != 1 || !nodeIds.Contains(item.NodeId) ||
                string.IsNullOrWhiteSpace(item.OwnerId) || string.IsNullOrWhiteSpace(item.Provenance)) ||
            collaboration.Comments.Any(item => item.SchemaVersion != 1 || !nodeIds.Contains(item.NodeId) ||
                string.IsNullOrWhiteSpace(item.AuthorId) || string.IsNullOrWhiteSpace(item.Text) ||
                string.IsNullOrWhiteSpace(item.Provenance)) ||
            collaboration.Conflicts.Any(item => item.SchemaVersion != 1 || !nodeIds.Contains(item.NodeId) ||
                string.IsNullOrWhiteSpace(item.Field) || !Enum.IsDefined(item.Resolution) ||
                string.IsNullOrWhiteSpace(item.Provenance) ||
                (item.Resolved && !DialogueProductionRuntimeService.ResolveConflict(item).CanApply)))
            Add(CollaborationCode, "Collaboration requires canonical node JSON, optional locks, comments and resolved semantic conflicts.",
                $"{root}/collaboration");

        if (profile.AiPlans.Select(item => item.Id).Distinct().Count() != profile.AiPlans.Count ||
            profile.AiPlans.Select(item => item.PlanId).Distinct().Count() != profile.AiPlans.Count ||
            profile.AiPlans.Any(item => item.Id == Guid.Empty || item.SchemaVersion != 1 ||
                string.IsNullOrWhiteSpace(item.Provenance) || item.TransactionId == Guid.Empty || item.PlanId == Guid.Empty ||
                !Enum.IsDefined(item.Authorization) || string.IsNullOrWhiteSpace(item.Synopsis) ||
                item.RequiredBeats.Count == 0 || item.StyleRules.Count == 0 || item.ContextCitations.Count == 0 ||
                item.ContextCitations.Any(string.IsNullOrWhiteSpace) || string.IsNullOrWhiteSpace(item.SemanticDiff) ||
                (item.Applied && item.PreviewOnly) ||
                (item.Applied && item.Authorization != DialogueAiAuthorizationLevel.AuthorApply)))
            Add(AiCode, "AI plans require cited context, style, lintable preview, semantic diff and authorized transaction.",
                $"{root}/aiPlans");
        return result;
    }
}
