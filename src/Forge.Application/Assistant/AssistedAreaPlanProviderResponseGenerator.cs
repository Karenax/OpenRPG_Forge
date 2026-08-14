using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Forge.Application.Commands;
using Forge.Domain.Assistant;
using Forge.Domain.Projects;

namespace Forge.Application.Assistant;

public static partial class AssistedAreaPlanProviderResponseGenerator
{
    public const string ResponseInvalidCode = "assistant.area_plan.provider_response_invalid";
    private const int MaximumResponseCharacters = 262_144;
    private static readonly Regex SafeKey = SafeKeyRegex();
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static AssistedAreaPlanProposal Generate(
        AssistedAreaPlanBrief brief,
        ProjectDefinition project,
        AssistantProviderResponse response,
        string promptSha256,
        string mapContractSha256)
    {
        ArgumentNullException.ThrowIfNull(brief);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(response);

        var briefDiagnostic = AssistedAreaPlanGenerator.ValidateBrief(brief);
        if (briefDiagnostic is not null) return FailureDiagnostic(briefDiagnostic);
        if (string.IsNullOrWhiteSpace(response.Content) || response.Content.Length > MaximumResponseCharacters)
            return FailureMessage("Provider response is empty or exceeds the bounded map-response size.");

        ProviderAreaPlanResponse? document;
        try
        {
            document = JsonSerializer.Deserialize<ProviderAreaPlanResponse>(response.Content, SerializerOptions);
        }
        catch (JsonException)
        {
            return FailureMessage("Provider response is not a valid JSON map topology.");
        }

        if (document is null || string.IsNullOrWhiteSpace(document.Summary) || document.Summary.Length > 500 ||
            document.Nodes is null || document.Links is null)
            return FailureMessage("Provider response requires a bounded summary, nodes and links.");
        if (document.Nodes.Count != brief.NodeCount)
            return FailureMessage($"Provider response must contain exactly {brief.NodeCount} nodes.");

        var keys = new HashSet<string>(StringComparer.Ordinal);
        var nodeKinds = new Dictionary<string, AssistedAreaPlanNodeKind>(StringComparer.Ordinal);
        var allowedBlueprints = brief.KitBlueprintIds.ToHashSet();
        var nodes = new List<AssistedAreaPlanNode>(document.Nodes.Count);
        foreach (var input in document.Nodes)
        {
            if (input is null) return FailureMessage("Provider response contains a null node.");
            var key = (input.Key ?? string.Empty).Trim();
            if (!SafeKey.IsMatch(key) || !keys.Add(key))
                return FailureMessage("Provider node keys must be unique bounded lowercase tokens.");
            if (!Enum.TryParse<AssistedAreaPlanNodeKind>(input.Kind, ignoreCase: true, out var kind) ||
                !Enum.IsDefined(kind))
                return FailureMessage($"Provider node '{key}' has an unsupported kind.");
            if (input.X is null || input.Z is null || input.Width is null || input.Depth is null ||
                input.ContentBlueprintIds is null || input.Tags is null)
                return FailureMessage($"Provider node '{key}' is missing required geometry hints, content or tags.");

            var content = new List<Guid>(input.ContentBlueprintIds.Count);
            foreach (var value in input.ContentBlueprintIds)
            {
                if (!Guid.TryParse(value, out var blueprintId) || !allowedBlueprints.Contains(blueprintId))
                    return FailureMessage($"Provider node '{key}' references a blueprint outside the brief allowlist.");
                content.Add(blueprintId);
            }
            if (content.Distinct().Count() != content.Count)
                return FailureMessage($"Provider node '{key}' contains duplicate blueprint references.");

            nodeKinds.Add(key, kind);
            nodes.Add(new AssistedAreaPlanNode(
                AssistedAreaPlanGenerator.StableId(brief.PlanId, $"provider-node:{key}"),
                1,
                kind,
                input.Label ?? string.Empty,
                input.X.Value,
                input.Z.Value,
                input.Width.Value,
                input.Depth.Value,
                content,
                input.Tags));
        }

        if (nodes.Count(node => node.Kind == AssistedAreaPlanNodeKind.Entry) != 1 ||
            nodes.Count(node => node.Kind == AssistedAreaPlanNodeKind.Exit) != 1)
            return FailureMessage("Provider topology requires exactly one Entry and one Exit node.");
        if (nodes.Where(node => node.Kind is not (AssistedAreaPlanNodeKind.Entry or AssistedAreaPlanNodeKind.Exit))
            .SelectMany(node => node.ContentBlueprintIds).Count() == 0)
            return FailureMessage("Provider topology requires placeable content outside Entry and Exit.");

        var links = new List<AssistedAreaPlanLink>(document.Links.Count);
        var requiredEdges = new List<(string From, string To)>();
        var linkKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var input in document.Links)
        {
            if (input is null) return FailureMessage("Provider response contains a null link.");
            var from = (input.From ?? string.Empty).Trim();
            var to = (input.To ?? string.Empty).Trim();
            var traversal = (input.Traversal ?? string.Empty).Trim().ToLowerInvariant();
            if (!keys.Contains(from) || !keys.Contains(to) || string.Equals(from, to, StringComparison.Ordinal) ||
                input.Required is null)
                return FailureMessage("Provider links must resolve two distinct declared node keys.");
            var semanticKey = $"{from}:{to}:{traversal}";
            var reverseKey = $"{to}:{from}:{traversal}";
            if (traversal.Length == 0 || linkKeys.Contains(reverseKey) || !linkKeys.Add(semanticKey))
                return FailureMessage("Provider links require a non-empty traversal and unique endpoints.");
            links.Add(new AssistedAreaPlanLink(
                AssistedAreaPlanGenerator.StableId(brief.PlanId, $"provider-link:{semanticKey}"),
                1,
                AssistedAreaPlanGenerator.StableId(brief.PlanId, $"provider-node:{from}"),
                AssistedAreaPlanGenerator.StableId(brief.PlanId, $"provider-node:{to}"),
                traversal,
                input.Required.Value));
            if (input.Required.Value) requiredEdges.Add((from, to));
        }
        var entryKey = nodeKinds.Single(item => item.Value == AssistedAreaPlanNodeKind.Entry).Key;
        var exitKey = nodeKinds.Single(item => item.Value == AssistedAreaPlanNodeKind.Exit).Key;
        if (!Reachable(entryKey, requiredEdges).Contains(exitKey))
            return FailureMessage("Provider topology requires a connected Entry-to-Exit path using required links.");

        return AssistedAreaPlanGenerator.CreateProposal(
            brief,
            project,
            nodes,
            links,
            response.ProviderKey,
            response.EstimatedCost,
            response.Provenance,
            promptSha256,
            response.InputTokens,
            response.OutputTokens,
            mapContractSha256);

        AssistedAreaPlanProposal FailureMessage(string message) => FailureDiagnostic(
            new CommandDiagnostic(ResponseInvalidCode, message));
        AssistedAreaPlanProposal FailureDiagnostic(CommandDiagnostic diagnostic) => AssistedAreaPlanGenerator.Failure(
            diagnostic,
            promptSha256,
            response.InputTokens,
            response.OutputTokens,
            response.EstimatedCost,
            response.Provenance);
    }

    private sealed record ProviderAreaPlanResponse(
        string? Summary,
        IReadOnlyList<ProviderAreaPlanNode?>? Nodes,
        IReadOnlyList<ProviderAreaPlanLink?>? Links);

    private sealed record ProviderAreaPlanNode(
        string? Key,
        string? Kind,
        string? Label,
        float? X,
        float? Z,
        float? Width,
        float? Depth,
        IReadOnlyList<string>? ContentBlueprintIds,
        IReadOnlyList<string>? Tags);

    private sealed record ProviderAreaPlanLink(
        string? From,
        string? To,
        string? Traversal,
        bool? Required);

    private static HashSet<string> Reachable(string start, IReadOnlyList<(string From, string To)> edges)
    {
        var reached = new HashSet<string>(StringComparer.Ordinal) { start };
        var queue = new Queue<string>();
        queue.Enqueue(start);
        while (queue.TryDequeue(out var current))
        {
            foreach (var edge in edges.Where(edge => edge.From == current || edge.To == current))
            {
                var next = edge.From == current ? edge.To : edge.From;
                if (reached.Add(next)) queue.Enqueue(next);
            }
        }
        return reached;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeKeyRegex();
}
