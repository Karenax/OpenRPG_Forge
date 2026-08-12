using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Assistant;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.RpgKit.Domain.Quests;
using CoreQuestDefinition = Forge.Domain.Quests.QuestDefinition;
using RpgQuestAuthorNotes = Forge.RpgKit.Domain.Quests.QuestAuthorNotes;
using RpgQuestDefinition = Forge.RpgKit.Domain.Quests.QuestDefinition;
using RpgQuestEditorialStatus = Forge.RpgKit.Domain.Quests.QuestEditorialStatus;
using RpgQuestObjectiveKind = Forge.RpgKit.Domain.Quests.QuestObjectiveKind;
using RpgQuestParticipantBindingKind = Forge.RpgKit.Domain.Quests.QuestParticipantBindingKind;

namespace Forge.RpgKit.Application.Quests;

public sealed record AssistedQuestBrief(
    Guid QuestId,
    string Slug,
    string DisplayName,
    Guid ZoneId,
    Guid ParticipantBlueprintId,
    string Synopsis,
    IReadOnlyList<string> ObjectiveLabels,
    IReadOnlyList<string> ContextCitations,
    IReadOnlyList<string> StyleRules,
    string LocalizationPrefix,
    string AuthorId,
    int Seed,
    double ProgressionReward);

public sealed record AssistedQuestProposal(
    bool Succeeded,
    CoreQuestDefinition Quest,
    RpgQuestDefinition AdvancedQuest,
    IReadOnlyList<string> Citations,
    IReadOnlyList<QuestDiagnostic> Lint,
    IReadOnlyList<string> DependencyWarnings,
    IReadOnlyList<string> StyleViolations,
    IReadOnlyList<QuestTestScenario> TestScenarios,
    string SemanticDiff,
    string PromptSha256,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCostUsd,
    string ProviderKey,
    string Provenance,
    bool ApprovedForApplication)
{
    public bool CanApply => Succeeded && Lint.Count == 0 && DependencyWarnings.Count == 0 &&
                            StyleViolations.Count == 0 && TestScenarios.Count >= 5;

    public AssistedQuestProposal WithApproval(bool approved, string provenance) => this with
    {
        ApprovedForApplication = approved,
        Provenance = string.IsNullOrWhiteSpace(provenance) ? Provenance : provenance.Trim(),
    };
}

public static class AssistedQuestCreationService
{
    public static async Task<AssistedQuestProposal> ProposeAsync(
        AssistedQuestBrief brief,
        ProjectDefinition project,
        LoreIndexDefinition lore,
        IAssistantProvider provider,
        AssistantProviderDefinition providerDefinition,
        bool networkAuthorized,
        bool projectContentAuthorized,
        CancellationToken cancellationToken = default)
    {
        ValidateBrief(brief, project);
        var lorePacket = LoreContextAssembler.Assemble(lore, project,
            string.Join(' ', brief.ObjectiveLabels.Prepend(brief.Synopsis)), 8);
        const string system = "Propose a bounded OpenRPG Forge quest. Never emit executable code or mutate the project.";
        var user = JsonSerializer.Serialize(new
        {
            brief.Synopsis,
            objectives = brief.ObjectiveLabels,
            brief.StyleRules,
            brief.ContextCitations,
            lore = projectContentAuthorized ? lorePacket.Content : "[project content withheld]",
        });
        const string schema = "{\"type\":\"object\",\"required\":[\"states\",\"objectives\",\"rewards\"],\"properties\":{\"states\":{\"type\":\"array\"},\"objectives\":{\"type\":\"array\"},\"rewards\":{\"type\":\"object\"}}}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', system, user, schema))))
            .ToLowerInvariant();
        var response = await provider.GenerateAsync(new AssistantProviderRequest(
            brief.QuestId, Guid.NewGuid(),
            new AssistantPromptRenderResult("quest-creation", "1.0.0", system, user, schema, hash),
            AssistantPermissionLevel.Propose, Math.Min(2048, providerDefinition.MaximumOutputTokens),
            networkAuthorized, projectContentAuthorized), cancellationToken).ConfigureAwait(false);
        return Generate(brief, project, response.ProviderKey, response.EstimatedCost, response.Provenance,
            hash, response.InputTokens, response.OutputTokens);
    }

    public static AssistedQuestProposal Generate(
        AssistedQuestBrief brief,
        ProjectDefinition project,
        string providerKey = "offline-ci",
        decimal estimatedCostUsd = 0,
        string provenance = "assistant:deterministic",
        string promptSha256 = "offline",
        int inputTokens = 0,
        int outputTokens = 0)
    {
        ValidateBrief(brief, project);
        var zoneRef = $"zone:{brief.ZoneId:D}";
        var participantRef = $"blueprint:{brief.ParticipantBlueprintId:D}";
        var objectives = brief.ObjectiveLabels.Select((label, index) => new QuestObjective(
            StableId(brief.QuestId, $"objective:{brief.Seed}:{index}"), RpgQuestObjectiveKind.Boolean,
            label.Trim(), 1, new HashSet<string>(), null, TimeSpan.Zero, new HashSet<string>(), null, false)).ToArray();
        var sources = brief.StyleRules.Concat(brief.ContextCitations).ToArray();
        var advanced = new RpgQuestDefinition(
            brief.QuestId, 1, provenance, brief.Slug, brief.DisplayName,
            [zoneRef], [new QuestParticipant("guide", RpgQuestParticipantBindingKind.Blueprint, participantRef,
                new HashSet<string>(), true)], [], [], objectives,
            ["transition:offer", "transition:complete", "transition:failure"],
            new QuestRewardBundle(brief.ProgressionReward, new Dictionary<Guid, int>(),
                new Dictionary<string, decimal>(), new Dictionary<string, double>(), new HashSet<string>(), []),
            new RpgQuestAuthorNotes(brief.Synopsis, string.Join("; ", brief.ContextCitations), brief.AuthorId,
                RpgQuestEditorialStatus.Review, ["generated-review-required"], [], sources, ["assistant-preview"]));
        var context = new QuestValidationContext(
            new HashSet<string> { zoneRef }, new HashSet<string> { participantRef }, new HashSet<string>(),
            new HashSet<Guid>(), new HashSet<string>(advanced.TransitionIds), new HashSet<Guid>(),
            new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), 12);
        var aiBrief = new QuestAiBrief(brief.Synopsis, ["dormant", "active", "completed", "failed"],
            ["guide"], [zoneRef, participantRef, .. advanced.TransitionIds], brief.ContextCitations,
            brief.StyleRules, brief.AuthorId);
        var draft = QuestQualityService.PreviewAi(aiBrief, context, _ => advanced);
        var stateIds = new[] { "dormant", "active", "completed", "failed" }
            .ToDictionary(key => key, key => QuestStateId.From(StableId(brief.QuestId, $"state:{brief.Seed}:{key}")));
        var states = new[]
        {
            State("dormant", "Non commencée", false), State("active", "En cours", false),
            State("completed", "Terminée", true), State("failed", "Échouée", true),
        };
        var transitions = new[]
        {
            Transition("offer", "dormant", "active", "quest.offer", "true", "quest.active", "true"),
            Transition("complete", "active", "completed", "quest.objectives", "complete", "quest.reward.progression", brief.ProgressionReward.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Transition("failure", "active", "failed", "quest.failed", "true", "quest.failure.recorded", "true"),
        };
        var core = new CoreQuestDefinition(QuestId.From(brief.QuestId), brief.Slug, brief.DisplayName,
            stateIds["dormant"], states, transitions);
        var coreLint = QuestValidator.Validate(core)
            .Select(item => new QuestDiagnostic(item.Code, item.Path, item.Message));
        var lint = draft.Lint.Concat(coreLint).ToArray();
        var scenarios = QuestQualityService.GenerateTests(advanced);
        return new(lint.Length == 0 && draft.DependencyWarnings.Count == 0 && draft.StyleViolations.Count == 0,
            core, advanced, draft.Citations, lint, draft.DependencyWarnings, draft.StyleViolations, scenarios,
            $"add-quest:{core.Id}|states:4|objectives:{objectives.Length}|transitions:3|rewards:1|failure-path:1|tests:{scenarios.Count}",
            promptSha256, inputTokens, outputTokens, estimatedCostUsd, providerKey, provenance, false);

        QuestStateDefinition State(string key, string name, bool terminal) => new(stateIds[key], name,
            new QuestJournalEntry($"{brief.LocalizationPrefix}.{key}", $"{name} · {brief.Synopsis}"), terminal);
        QuestTransition Transition(string discriminator, string from, string to, string conditionKey,
            string expected, string actionKey, string value) => new(
            QuestTransitionId.From(StableId(brief.QuestId, $"transition:{brief.Seed}:{discriminator}")),
            stateIds[from], stateIds[to],
            [new QuestCondition(QuestConditionKind.VariableEquals, conditionKey, expected)],
            [new Forge.Domain.Quests.QuestAction(QuestActionKind.SetVariable, actionKey, value)]);
    }

    private static void ValidateBrief(AssistedQuestBrief brief, ProjectDefinition project)
    {
        if (brief.QuestId == Guid.Empty || string.IsNullOrWhiteSpace(brief.Slug) ||
            string.IsNullOrWhiteSpace(brief.DisplayName) || string.IsNullOrWhiteSpace(brief.Synopsis) ||
            string.IsNullOrWhiteSpace(brief.LocalizationPrefix) || string.IsNullOrWhiteSpace(brief.AuthorId) ||
            brief.ObjectiveLabels.Count == 0 || brief.ObjectiveLabels.Count > 12 ||
            brief.ContextCitations.Count == 0 || brief.StyleRules.Count == 0 ||
            brief.ProgressionReward < 0 || !double.IsFinite(brief.ProgressionReward))
            throw new ArgumentException("Assisted quest identity, objectives, citations, style and reward are invalid.");
        if (project.Areas.All(item => item.Id.Value != brief.ZoneId))
            throw new ArgumentException("Assisted quest zone does not resolve.");
        var participant = project.Blueprints.SingleOrDefault(item => item.Id.Value == brief.ParticipantBlueprintId);
        if (participant is null || participant.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn))
            throw new ArgumentException("Assisted quest participant must resolve to a creature or player blueprint.");
    }

    private static Guid StableId(Guid root, string discriminator)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{root:D}:{discriminator}"));
        Span<byte> bytes = stackalloc byte[16]; hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x40); bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes);
    }
}
