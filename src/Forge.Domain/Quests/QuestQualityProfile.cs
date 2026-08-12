using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Dialogues;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Domain.Quests;

public enum QuestQualityTestKind { Nominal, Failure, Repetition, Multiplayer, SaveResume }
public enum QuestQualityFixtureKind { State, Transition, Participant, Zone, Prop, Asset }
public enum QuestQualityTestActionKind { Start, CompleteObjectives, FailObjective, RepeatTransition, SubmitClientIntent, SaveSnapshot, ReloadSnapshot }
public enum QuestQualityAssertionKind { StateEquals, ObjectiveProgress, RewardGranted, HistoryContains, AuthorityAccepted, PersistenceRoundTrip }
public enum QuestAiDependencyKind { State, Transition, Participant, Zone, Prop, Asset }
public enum QuestAiAuthorizationLevel { Propose, Configure, ApplyApprovedPlan }

public sealed record QuestQualityFixture(
    Guid Id, int SchemaVersion, string Provenance, int DisplayOrder,
    QuestQualityFixtureKind Kind, string ReferenceId, bool Required);

public sealed record QuestQualityTestStep(
    Guid Id, int SchemaVersion, string Provenance, int DisplayOrder,
    QuestQualityTestActionKind Action, IReadOnlyDictionary<string, string> Parameters);

public sealed record QuestQualityTestEdge(
    Guid Id, int SchemaVersion, string Provenance, int DisplayOrder, Guid FromStepId, Guid ToStepId);

public sealed record QuestQualityTestAssertion(
    Guid Id, int SchemaVersion, string Provenance, int DisplayOrder,
    QuestQualityAssertionKind Kind, string Path, string Expected);

public sealed record QuestQualityTestScenario(
    Guid Id, int SchemaVersion, string Provenance, int DisplayOrder, QuestQualityTestKind Kind,
    LocalizedText Label, int Seed, IReadOnlyList<QuestQualityFixture> Fixtures,
    IReadOnlyList<QuestQualityTestStep> Steps, IReadOnlyList<QuestQualityTestEdge> Edges,
    IReadOnlyList<QuestQualityTestAssertion> Assertions);

public sealed record QuestQualityTestRun(
    Guid ScenarioId, bool Passed, IReadOnlyList<string> Failures,
    IReadOnlyDictionary<string, string> FinalState);

public sealed record QuestAiDependency(
    Guid Id, int SchemaVersion, string Provenance, int DisplayOrder,
    QuestAiDependencyKind Kind, string ReferenceId);

public sealed record QuestAiLintItem(
    Guid Id, int SchemaVersion, string Provenance, int DisplayOrder,
    string Code, string Path, string Message);

public sealed record QuestAiGenerationPlan(
    Guid Id, int SchemaVersion, string Provenance, Guid TransactionId,
    LocalizedText Synopsis, IReadOnlyList<QuestStateId> ProposedStateIds,
    IReadOnlyList<Guid> ParticipantIds, IReadOnlyList<QuestAiDependency> Dependencies,
    IReadOnlyList<string> ContextCitations, IReadOnlyList<string> StyleRules,
    IReadOnlyList<QuestAiLintItem> Lint, string SemanticDiff,
    QuestAiAuthorizationLevel Authorization, string RequestedBy,
    string ApprovedBy, DateTimeOffset? ApprovedAt, bool Applied);

public sealed record QuestQualityProfile(
    Guid Id, int SchemaVersion, string Provenance, bool Enabled, bool RequiredAtRuntime,
    IReadOnlyList<QuestQualityTestScenario> TestScenarios, QuestAiGenerationPlan AiPlan);

public static class QuestQualityRuntimeService
{
    public static IReadOnlyList<QuestQualityTestScenario> GenerateTests(QuestDefinition quest, int seed = 20260811)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var transition = quest.Transitions.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id.Value)
            .FirstOrDefault();
        var fixtures = new List<(QuestQualityFixtureKind Kind, string Reference)>
        {
            (QuestQualityFixtureKind.State, quest.InitialStateId.ToString()),
        };
        if (transition is not null)
            fixtures.Add((QuestQualityFixtureKind.Transition, transition.Id.ToString()));
        if (quest.Authoring is { } authoring)
        {
            fixtures.AddRange(authoring.Participants.OrderBy(item => item.DisplayOrder)
                .Select(item => (QuestQualityFixtureKind.Participant, item.Id.ToString())));
            fixtures.AddRange(authoring.Wizard.ZoneIds.Order(StringComparer.Ordinal)
                .Select(item => (QuestQualityFixtureKind.Zone, item)));
            fixtures.AddRange(authoring.Wizard.PropIds.Order(StringComparer.Ordinal)
                .Select(item => (QuestQualityFixtureKind.Prop, item)));
        }

        QuestQualityTestScenario Make(QuestQualityTestKind kind, int offset,
            IReadOnlyList<(QuestQualityTestActionKind Action, IReadOnlyDictionary<string, string> Parameters)> actions,
            IReadOnlyList<(QuestQualityAssertionKind Kind, string Path, string Expected)> assertions)
        {
            var scenarioKey = $"{quest.Id}:quality:{kind}";
            var steps = actions.Select((item, index) => new QuestQualityTestStep(
                StableId($"{scenarioKey}:step:{index}"), 1, "generated:r073", index + 1,
                item.Action, item.Parameters)).ToArray();
            var edges = steps.Zip(steps.Skip(1), (from, to) => (from, to)).Select((pair, index) =>
                new QuestQualityTestEdge(StableId($"{scenarioKey}:edge:{index}"), 1, "generated:r073",
                    index + 1, pair.from.Id, pair.to.Id)).ToArray();
            var scenarioFixtures = fixtures.Select((item, index) => new QuestQualityFixture(
                StableId($"{scenarioKey}:fixture:{index}:{item.Kind}:{item.Reference}"), 1, "generated:r073",
                index + 1, item.Kind, item.Reference, true)).ToArray();
            var typedAssertions = assertions.Select((item, index) => new QuestQualityTestAssertion(
                StableId($"{scenarioKey}:assertion:{index}"), 1, "generated:r073", index + 1,
                item.Kind, item.Path, item.Expected)).ToArray();
            return new QuestQualityTestScenario(StableId(scenarioKey), 1, "generated:r073", (int)kind + 1,
                kind, new LocalizedText($"quest.{quest.Slug}.test.{kind.ToString().ToLowerInvariant()}",
                    kind.ToString(), provenance: "generated:r073"), seed + offset, scenarioFixtures, steps, edges,
                typedAssertions);
        }

        static IReadOnlyDictionary<string, string> Parameters(params (string Key, string Value)[] pairs) =>
            pairs.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);

        var transitionId = transition?.Id.ToString() ?? "transition:none";
        return
        [
            Make(QuestQualityTestKind.Nominal, 0,
                [(QuestQualityTestActionKind.Start, Parameters(("scope", "player"))),
                 (QuestQualityTestActionKind.CompleteObjectives, Parameters(("transition", transitionId)))],
                [(QuestQualityAssertionKind.StateEquals, "quest/state", "completed"),
                 (QuestQualityAssertionKind.RewardGranted, "quest/rewards", "true")]),
            Make(QuestQualityTestKind.Failure, 1,
                [(QuestQualityTestActionKind.Start, Parameters(("scope", "player"))),
                 (QuestQualityTestActionKind.FailObjective, Parameters(("required", "true")))],
                [(QuestQualityAssertionKind.StateEquals, "quest/state", "failed")]),
            Make(QuestQualityTestKind.Repetition, 2,
                [(QuestQualityTestActionKind.Start, Parameters(("scope", "player"))),
                 (QuestQualityTestActionKind.RepeatTransition, Parameters(("transition", transitionId), ("count", "2")))],
                [(QuestQualityAssertionKind.HistoryContains, "quest/history/idempotent", "true")]),
            Make(QuestQualityTestKind.Multiplayer, 3,
                [(QuestQualityTestActionKind.Start, Parameters(("scope", "party"))),
                 (QuestQualityTestActionKind.SubmitClientIntent, Parameters(("authority", "server")))],
                [(QuestQualityAssertionKind.AuthorityAccepted, "quest/authority", "server")]),
            Make(QuestQualityTestKind.SaveResume, 4,
                [(QuestQualityTestActionKind.Start, Parameters(("scope", "player"))),
                 (QuestQualityTestActionKind.SaveSnapshot, Parameters(("point", "mid-objective"))),
                 (QuestQualityTestActionKind.ReloadSnapshot, Parameters(("source", "save")))],
                [(QuestQualityAssertionKind.PersistenceRoundTrip, "quest/snapshot", "equal")]),
        ];
    }

    public static QuestQualityTestRun Run(
        QuestQualityTestScenario scenario,
        Func<QuestQualityTestStep, IReadOnlyDictionary<string, string>, IReadOnlyDictionary<string, string>> execute)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(execute);
        IReadOnlyDictionary<string, string> state = new Dictionary<string, string>();
        foreach (var step in scenario.Steps.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id))
            state = execute(step, state);
        var failures = scenario.Assertions.OrderBy(item => item.DisplayOrder).Where(assertion =>
                state.GetValueOrDefault(assertion.Path) != assertion.Expected)
            .Select(assertion => $"{assertion.Kind}:{assertion.Path}:expected={assertion.Expected}:actual={state.GetValueOrDefault(assertion.Path)}")
            .ToArray();
        return new(scenario.Id, failures.Length == 0, failures, state);
    }

    public static QuestAiGenerationPlan CreateAiPlan(
        QuestDefinition quest, LocalizedText synopsis, IReadOnlyList<QuestAiDependency> dependencies,
        IReadOnlyList<string> citations, IReadOnlyList<string> styleRules, string requestedBy,
        Guid transactionId, string semanticDiff)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return new(StableId($"{quest.Id}:quality:ai-plan"), 1, "assistant:r073", transactionId, synopsis,
            quest.States.OrderBy(item => item.DisplayOrder).Select(item => item.Id).ToArray(),
            quest.Authoring?.Participants.OrderBy(item => item.DisplayOrder).Select(item => item.Id).ToArray() ?? [],
            dependencies, citations, styleRules, [], semanticDiff, QuestAiAuthorizationLevel.Propose,
            requestedBy, string.Empty, null, false);
    }

    public static QuestAiGenerationPlan Approve(QuestAiGenerationPlan plan, string reviewerId, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Applied || string.IsNullOrWhiteSpace(reviewerId) || plan.Lint.Count != 0)
            throw new InvalidOperationException("QUEST_AI_PLAN_CANNOT_BE_APPROVED");
        return plan with
        {
            Authorization = QuestAiAuthorizationLevel.ApplyApprovedPlan,
            ApprovedBy = reviewerId.Trim(),
            ApprovedAt = at,
            SemanticDiff = plan.SemanticDiff + $"|approved-by:{reviewerId.Trim()}",
        };
    }

    public static QuestAiGenerationPlan MarkApplied(QuestAiGenerationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Applied || plan.Authorization != QuestAiAuthorizationLevel.ApplyApprovedPlan ||
            string.IsNullOrWhiteSpace(plan.ApprovedBy) || plan.ApprovedAt is null)
            throw new InvalidOperationException("QUEST_AI_PLAN_REQUIRES_APPROVED_TRANSACTION");
        return plan with { Applied = true, SemanticDiff = plan.SemanticDiff.Replace("preview:", "apply:", StringComparison.Ordinal) };
    }

    internal static Guid StableId(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes);
    }
}

public static class QuestQualityProfileValidator
{
    public const string RequiredMissingCode = "quest.quality.required_missing";
    public const string IdentityInvalidCode = "quest.quality.identity_invalid";
    public const string ScenarioInvalidCode = "quest.quality.scenario_invalid";
    public const string GraphInvalidCode = "quest.quality.graph_invalid";
    public const string AssertionInvalidCode = "quest.quality.assertion_invalid";
    public const string AiPlanInvalidCode = "quest.quality.ai_plan_invalid";
    public const string ReferenceMissingCode = "quest.quality.reference_missing";

    public static IReadOnlyList<DomainDiagnostic> Validate(QuestDefinition quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var profile = quest.Quality;
        var path = $"quests/{quest.Id}/quality";
        if (profile is null) return [];
        if (!profile.Enabled)
            return profile.RequiredAtRuntime
                ? [new(RequiredMissingCode, "Required quest quality profile is disabled.", path)]
                : [];
        var diagnostics = new List<DomainDiagnostic>();
        if (profile.Id == Guid.Empty || profile.SchemaVersion != 1 || string.IsNullOrWhiteSpace(profile.Provenance))
            diagnostics.Add(new(IdentityInvalidCode, "Quest quality identity, schema or provenance is invalid.", path));

        var allKinds = Enum.GetValues<QuestQualityTestKind>();
        var scenarioIdentity = profile.TestScenarios.Select(item => (item.Id, item.DisplayOrder)).ToArray();
        if (profile.TestScenarios.Count != allKinds.Length ||
            !allKinds.All(kind => profile.TestScenarios.Count(item => item.Kind == kind) == 1) ||
            HasDuplicateIdentityOrOrder(scenarioIdentity))
            diagnostics.Add(new(ScenarioInvalidCode,
                "Generated tests require one deterministic nominal, failure, repetition, multiplayer and save-resume scenario.",
                $"{path}/testScenarios"));

        var stateIds = quest.States.Select(item => item.Id.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var transitionIds = quest.Transitions.Select(item => item.Id.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var participants = quest.Authoring?.Participants.Select(item => item.Id.ToString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        var zones = quest.Authoring?.Wizard.ZoneIds.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        var props = quest.Authoring?.Wizard.PropIds.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        foreach (var scenario in profile.TestScenarios)
        {
            var scenarioPath = $"{path}/testScenarios/{scenario.Id}";
            if (scenario.Id == Guid.Empty || scenario.SchemaVersion != 1 || string.IsNullOrWhiteSpace(scenario.Provenance) ||
                scenario.DisplayOrder < 1 || scenario.Label.Id == Guid.Empty ||
                string.IsNullOrWhiteSpace(scenario.Label.Key) || string.IsNullOrWhiteSpace(scenario.Label.Fallback) ||
                scenario.Seed <= 0 || scenario.Steps.Count == 0 || scenario.Assertions.Count == 0 ||
                HasDuplicateIdentityOrOrder(scenario.Steps.Select(item => (item.Id, item.DisplayOrder))) ||
                HasDuplicateIdentityOrOrder(scenario.Assertions.Select(item => (item.Id, item.DisplayOrder))) ||
                HasDuplicateIdentityOrOrder(scenario.Fixtures.Select(item => (item.Id, item.DisplayOrder))))
                diagnostics.Add(new(ScenarioInvalidCode,
                    "Generated scenario identity, localization, seed, fixtures, steps or assertions is invalid.", scenarioPath));

            var stepIds = scenario.Steps.Select(item => item.Id).ToHashSet();
            if (scenario.Steps.Any(item => item.Id == Guid.Empty || item.SchemaVersion != 1 ||
                    string.IsNullOrWhiteSpace(item.Provenance) || item.DisplayOrder < 1 ||
                    item.Parameters.Count == 0 || item.Parameters.Any(pair =>
                        string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))) ||
                scenario.Edges.Count != Math.Max(0, scenario.Steps.Count - 1) ||
                HasDuplicateIdentityOrOrder(scenario.Edges.Select(item => (item.Id, item.DisplayOrder))) ||
                scenario.Edges.Any(item => item.Id == Guid.Empty || item.SchemaVersion != 1 ||
                    string.IsNullOrWhiteSpace(item.Provenance) || !stepIds.Contains(item.FromStepId) ||
                    !stepIds.Contains(item.ToStepId) || item.FromStepId == item.ToStepId))
                diagnostics.Add(new(GraphInvalidCode,
                    "Generated test nodes and edges require typed actions, parameters and resolved UUIDs.", $"{scenarioPath}/graph"));

            if (scenario.Assertions.Any(item => item.Id == Guid.Empty || item.SchemaVersion != 1 ||
                    string.IsNullOrWhiteSpace(item.Provenance) || item.DisplayOrder < 1 ||
                    string.IsNullOrWhiteSpace(item.Path) || string.IsNullOrWhiteSpace(item.Expected)))
                diagnostics.Add(new(AssertionInvalidCode,
                    "Generated test assertions require typed paths and expected values.", $"{scenarioPath}/assertions"));

            foreach (var fixture in scenario.Fixtures.Where(item => item.Required && !Resolves(
                         item.Kind, item.ReferenceId, stateIds, transitionIds, participants, zones, props)))
                diagnostics.Add(new(ReferenceMissingCode,
                    $"Required {fixture.Kind} fixture '{fixture.ReferenceId}' does not resolve.",
                    $"{scenarioPath}/fixtures/{fixture.Id}/referenceId"));
        }

        var plan = profile.AiPlan;
        var planPath = $"{path}/aiPlan";
        if (plan.Id == Guid.Empty || plan.TransactionId == Guid.Empty || plan.SchemaVersion != 1 ||
            string.IsNullOrWhiteSpace(plan.Provenance) || plan.Synopsis.Id == Guid.Empty ||
            string.IsNullOrWhiteSpace(plan.Synopsis.Key) || string.IsNullOrWhiteSpace(plan.Synopsis.Fallback) ||
            plan.ProposedStateIds.Count == 0 || plan.ProposedStateIds.Distinct().Count() != plan.ProposedStateIds.Count ||
            plan.ProposedStateIds.Any(id => !quest.States.Any(state => state.Id == id)) ||
            plan.ParticipantIds.Distinct().Count() != plan.ParticipantIds.Count ||
            plan.ParticipantIds.Any(id => quest.Authoring?.Participants.All(item => item.Id != id) != false) ||
            plan.Dependencies.Count == 0 || HasDuplicateIdentityOrOrder(plan.Dependencies.Select(item => (item.Id, item.DisplayOrder))) ||
            plan.ContextCitations.Count == 0 || plan.ContextCitations.Any(string.IsNullOrWhiteSpace) ||
            plan.StyleRules.Count == 0 || plan.StyleRules.Any(string.IsNullOrWhiteSpace) ||
            string.IsNullOrWhiteSpace(plan.SemanticDiff) || string.IsNullOrWhiteSpace(plan.RequestedBy) ||
            plan.Lint.Any(item => item.Id == Guid.Empty || item.SchemaVersion != 1 ||
                string.IsNullOrWhiteSpace(item.Provenance) || item.DisplayOrder < 1 ||
                string.IsNullOrWhiteSpace(item.Code) || string.IsNullOrWhiteSpace(item.Path) ||
                string.IsNullOrWhiteSpace(item.Message)) ||
            plan.Applied && (plan.Authorization != QuestAiAuthorizationLevel.ApplyApprovedPlan ||
                             string.IsNullOrWhiteSpace(plan.ApprovedBy) || plan.ApprovedAt is null))
            diagnostics.Add(new(AiPlanInvalidCode,
                "AI quest plan requires identity, transaction, synopsis, states, participants, dependencies, citations, style, lint and human authorization.",
                planPath));

        foreach (var dependency in plan.Dependencies.Where(item => !Resolves(
                     item.Kind, item.ReferenceId, stateIds, transitionIds, participants, zones, props)))
            diagnostics.Add(new(ReferenceMissingCode,
                $"AI dependency '{dependency.ReferenceId}' does not resolve as {dependency.Kind}.",
                $"{planPath}/dependencies/{dependency.Id}/referenceId"));

        var allIds = profile.TestScenarios.SelectMany(item =>
                new[] { item.Id }.Concat(item.Fixtures.Select(node => node.Id)).Concat(item.Steps.Select(node => node.Id))
                    .Concat(item.Edges.Select(node => node.Id)).Concat(item.Assertions.Select(node => node.Id)))
            .Concat([profile.Id, plan.Id]).Concat(plan.Dependencies.Select(item => item.Id))
            .Concat(plan.Lint.Select(item => item.Id)).ToArray();
        if (allIds.Any(id => id == Guid.Empty) || allIds.Distinct().Count() != allIds.Length)
            diagnostics.Add(new(IdentityInvalidCode, "Quest quality UUIDs must be non-empty and unique.", path));
        return diagnostics;
    }

    public static IReadOnlyList<DomainDiagnostic> ValidateReferences(ProjectDefinition project, QuestDefinition quest)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(quest);
        var profile = quest.Quality;
        if (profile is null || !profile.Enabled) return [];
        var areaRefs = project.Areas.SelectMany(item => new[] { item.Id.ToString(), item.Slug })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var references = profile.TestScenarios.SelectMany(item => item.Fixtures.Select(fixture =>
                (fixture.Kind, fixture.ReferenceId, Path: $"quests/{quest.Id}/quality/testScenarios/{item.Id}/fixtures/{fixture.Id}/referenceId")))
            .Concat(profile.AiPlan.Dependencies.Select(item =>
                ((QuestQualityFixtureKind)item.Kind, item.ReferenceId,
                    $"quests/{quest.Id}/quality/aiPlan/dependencies/{item.Id}/referenceId")));
        return references.Where(item => item.Item1 == QuestQualityFixtureKind.Zone && !areaRefs.Contains(item.Item2))
            .Select(item => new DomainDiagnostic(ReferenceMissingCode,
                $"Quest quality zone '{item.Item2}' does not resolve in the project.", item.Item3)).ToArray();
    }

    private static bool Resolves(QuestQualityFixtureKind kind, string value,
        IReadOnlySet<string> states, IReadOnlySet<string> transitions, IReadOnlySet<string> participants,
        IReadOnlySet<string> zones, IReadOnlySet<string> props) =>
        !string.IsNullOrWhiteSpace(value) && kind switch
        {
            QuestQualityFixtureKind.State => states.Contains(value),
            QuestQualityFixtureKind.Transition => transitions.Contains(value),
            QuestQualityFixtureKind.Participant => participants.Contains(value),
            QuestQualityFixtureKind.Zone => zones.Contains(value),
            QuestQualityFixtureKind.Prop => props.Contains(value),
            QuestQualityFixtureKind.Asset => value.StartsWith("asset:", StringComparison.Ordinal),
            _ => false,
        };

    private static bool Resolves(QuestAiDependencyKind kind, string value,
        IReadOnlySet<string> states, IReadOnlySet<string> transitions, IReadOnlySet<string> participants,
        IReadOnlySet<string> zones, IReadOnlySet<string> props) =>
        Resolves((QuestQualityFixtureKind)kind, value, states, transitions, participants, zones, props);

    private static bool HasDuplicateIdentityOrOrder(IEnumerable<(Guid Id, int Order)> values)
    {
        var materialized = values.ToArray();
        return materialized.Select(item => item.Id).Distinct().Count() != materialized.Length ||
               materialized.Select(item => item.Order).Distinct().Count() != materialized.Length;
    }
}
