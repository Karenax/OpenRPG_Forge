using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Forge.Domain.Assistant;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.ModuleFormat.Serialization;
using Forge.RpgKit.Application.Quests;
using Forge.RpgKit.Domain.Quests;
using AdvancedQuestDefinition = Forge.RpgKit.Domain.Quests.QuestDefinition;
using CoreQuestDefinition = Forge.Domain.Quests.QuestDefinition;

namespace Forge.Mcp.Quests;

internal static class QuestJsonBoundaryMapper
{
    public const string InvalidJsonCode = "quest.json.invalid";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static bool TryMapJournal(
        ProjectDefinition project,
        QuestJournalPlanRequest request,
        out QuestJournalDomainPlanRequest? mapped,
        out AgentDiagnostic? diagnostic)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(request);
        mapped = null;
        diagnostic = null;
        try
        {
            var quest = MapQuest(project, request.Quest);
            if (!Guid.TryParse(request.QuestId, out var requestedId) || requestedId == Guid.Empty ||
                quest.Id.Value != requestedId)
            {
                diagnostic = Invalid(
                    "Quest ID must be a non-empty UUID matching the canonical JSON definition.",
                    "request/questId");
                return false;
            }

            mapped = new QuestJournalDomainPlanRequest(
                request.SchemaVersion,
                request.BaseRevision,
                request.QuestId,
                quest);
            return true;
        }
        catch (Exception exception) when (IsJsonBoundaryException(exception))
        {
            diagnostic = Invalid(
                "Quest JSON does not conform to the canonical quest schema v1.",
                "request/quest");
            return false;
        }
    }

    public static bool TryMapAssisted(
        ProjectDefinition project,
        AssistedQuestApplyPlanRequest request,
        out AssistedQuestDomainApplyPlanRequest? mapped,
        out AgentDiagnostic? diagnostic)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(request);
        mapped = null;
        diagnostic = null;
        try
        {
            if (!Enum.TryParse<AssistantPermissionLevel>(request.Permission, true, out var permission) ||
                !Enum.IsDefined(permission))
            {
                diagnostic = Invalid(
                    "Permission must be a defined assistant permission name.",
                    "request/permission");
                return false;
            }

            var proposal = JsonSerializer.Deserialize<AssistedQuestJsonProposal>(
                request.Proposal.GetRawText(), JsonOptions)
                ?? throw new JsonException("Assisted quest proposal is empty.");
            var quest = MapQuest(project, proposal.Quest);
            mapped = new AssistedQuestDomainApplyPlanRequest(
                request.SchemaVersion,
                request.BaseRevision,
                new AssistedQuestProposal(
                    proposal.Succeeded,
                    quest,
                    proposal.AdvancedQuest,
                    proposal.Citations ?? [],
                    proposal.Lint ?? [],
                    proposal.DependencyWarnings ?? [],
                    proposal.StyleViolations ?? [],
                    proposal.TestScenarios ?? [],
                    proposal.SemanticDiff ?? string.Empty,
                    proposal.PromptSha256 ?? string.Empty,
                    proposal.InputTokens,
                    proposal.OutputTokens,
                    proposal.EstimatedCostUsd,
                    proposal.ProviderKey ?? string.Empty,
                    proposal.Provenance ?? string.Empty,
                    proposal.ApprovedForApplication),
                permission);
            return true;
        }
        catch (Exception exception) when (IsJsonBoundaryException(exception))
        {
            diagnostic = Invalid(
                "Assisted quest JSON does not conform to the canonical proposal schema v1.",
                "request/proposal");
            return false;
        }
    }

    private static CoreQuestDefinition MapQuest(ProjectDefinition project, JsonElement questJson)
    {
        if (questJson.ValueKind != JsonValueKind.Object)
            throw new JsonException("Quest JSON must be an object.");

        var candidate = NormalizeValueObjects(JsonNode.Parse(questJson.GetRawText())
            ?? throw new JsonException("Quest JSON is empty."));
        var candidateObject = candidate.AsObject();
        var candidateId = candidateObject["id"]?.GetValue<string>();
        if (!Guid.TryParse(candidateId, out var parsedId) || parsedId == Guid.Empty)
            throw new JsonException("Quest ID is invalid.");

        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(project))?.AsObject()
            ?? throw new JsonException("Canonical project JSON is empty.");
        var projectObject = document["project"]?.AsObject()
            ?? throw new JsonException("Canonical project JSON has no project object.");
        var quests = projectObject["quests"]?.AsArray()
            ?? throw new JsonException("Canonical project JSON has no quest collection.");
        for (var index = quests.Count - 1; index >= 0; index--)
        {
            if (string.Equals(quests[index]?["id"]?.GetValue<string>(), candidateId,
                    StringComparison.OrdinalIgnoreCase))
                quests.RemoveAt(index);
        }
        quests.Add(candidate);

        var projected = ProjectJsonSerializer.Deserialize(document.ToJsonString());
        return projected.Quests.Single(item => item.Id.Value == parsedId);
    }

    private static JsonNode NormalizeValueObjects(JsonNode node)
    {
        if (node is JsonObject objectNode)
        {
            if (objectNode.Count == 1 && objectNode["value"] is JsonValue valueNode &&
                valueNode.TryGetValue<string>(out var value) && Guid.TryParse(value, out _))
                return JsonValue.Create(value)!;

            foreach (var property in objectNode.ToArray())
            {
                if (property.Value is not null)
                {
                    var normalized = NormalizeValueObjects(property.Value);
                    if (!ReferenceEquals(normalized, property.Value))
                        objectNode[property.Key] = normalized;
                }
            }
            return objectNode;
        }

        if (node is JsonArray arrayNode)
        {
            for (var index = 0; index < arrayNode.Count; index++)
            {
                if (arrayNode[index] is not null)
                {
                    var original = arrayNode[index]!;
                    var normalized = NormalizeValueObjects(original);
                    if (!ReferenceEquals(normalized, original))
                        arrayNode[index] = normalized;
                }
            }
        }
        return node;
    }

    private static bool IsJsonBoundaryException(Exception exception) =>
        exception is JsonException or NotSupportedException or ArgumentException or InvalidOperationException;

    private static AgentDiagnostic Invalid(string message, string path) =>
        new(InvalidJsonCode, message, path);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new StringReadOnlySetJsonConverter());
        return options;
    }

    private sealed class StringReadOnlySetJsonConverter : JsonConverter<IReadOnlySet<string>>
    {
        public override IReadOnlySet<string> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
            (JsonSerializer.Deserialize<string[]>(ref reader, options) ?? [])
            .ToHashSet(StringComparer.Ordinal);

        public override void Write(
            Utf8JsonWriter writer,
            IReadOnlySet<string> value,
            JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.Order(StringComparer.Ordinal).ToArray(), options);
    }

    private sealed record AssistedQuestJsonProposal(
        bool Succeeded,
        JsonElement Quest,
        AdvancedQuestDefinition AdvancedQuest,
        IReadOnlyList<string>? Citations,
        IReadOnlyList<QuestDiagnostic>? Lint,
        IReadOnlyList<string>? DependencyWarnings,
        IReadOnlyList<string>? StyleViolations,
        IReadOnlyList<QuestTestScenario>? TestScenarios,
        string? SemanticDiff,
        string? PromptSha256,
        int InputTokens,
        int OutputTokens,
        decimal EstimatedCostUsd,
        string? ProviderKey,
        string? Provenance,
        bool ApprovedForApplication);
}
