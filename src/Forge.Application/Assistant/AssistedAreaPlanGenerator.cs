using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Domain.Assistant;
using Forge.Domain.Projects;

namespace Forge.Application.Assistant;

public static class AssistedAreaPlanGenerator
{
    public const string BriefInvalidCode = "assistant.area_plan.brief_invalid";

    public static AssistedAreaPlanProposal Generate(
        AssistedAreaPlanBrief brief,
        ProjectDefinition project,
        string providerKey,
        decimal estimatedCostUsd,
        string providerProvenance,
        string promptSha256,
        int inputTokens,
        int outputTokens)
    {
        ArgumentNullException.ThrowIfNull(brief);
        ArgumentNullException.ThrowIfNull(project);
        if (brief.PlanId == Guid.Empty || brief.AreaId == Guid.Empty ||
            string.IsNullOrWhiteSpace(brief.Title) || brief.NodeCount is < 4 or > 24 ||
            brief.WidthMeters is < 8 or > 2048 || brief.DepthMeters is < 8 or > 2048)
            return Failure("Area-plan brief identity, title, node count or dimensions are invalid.");
        if (brief.KitBlueprintIds.Count == 0 || brief.Constraints.Count == 0 || brief.Objectives.Count == 0)
            return Failure("Area-plan brief requires at least one kit, constraint and objective.");

        var kinds = Kinds(brief.Archetype, brief.NodeCount);
        var nodes = new List<AssistedAreaPlanNode>();
        for (var index = 0; index < brief.NodeCount; index++)
        {
            var (x, z) = Position(brief, index);
            var kind = kinds[index];
            var content = index == 0 || index == brief.NodeCount - 1
                ? Array.Empty<Guid>()
                : new[] { brief.KitBlueprintIds[(index - 1) % brief.KitBlueprintIds.Count] };
            nodes.Add(new AssistedAreaPlanNode(
                StableId(brief.PlanId, $"node:{index}"),
                1,
                kind,
                Label(brief.Archetype, kind, index),
                x,
                z,
                Math.Max(4, brief.WidthMeters / Math.Max(3, MathF.Ceiling(MathF.Sqrt(brief.NodeCount))) * 0.65f),
                Math.Max(4, brief.DepthMeters / Math.Max(3, MathF.Ceiling(MathF.Sqrt(brief.NodeCount))) * 0.65f),
                content,
                [brief.Archetype.ToString().ToLowerInvariant(), kind.ToString().ToLowerInvariant()]));
        }
        var links = new List<AssistedAreaPlanLink>();
        for (var index = 1; index < nodes.Count; index++)
        {
            links.Add(new AssistedAreaPlanLink(
                StableId(brief.PlanId, $"link:{index - 1}:{index}"),
                1,
                nodes[index - 1].Id,
                nodes[index].Id,
                brief.Archetype == AssistedAreaArchetype.Crypt ? "door" : "path",
                true));
        }
        if (nodes.Count >= 7)
        {
            links.Add(new AssistedAreaPlanLink(
                StableId(brief.PlanId, "link:optional-shortcut"),
                1,
                nodes[1].Id,
                nodes[^2].Id,
                brief.Archetype == AssistedAreaArchetype.Crypt ? "secret-door" : "side-path",
                false));
        }

        var plan = new AssistedAreaPlanDefinition(
            brief.PlanId,
            AssistedAreaPlanDefinition.CurrentSchemaVersion,
            brief.AreaId,
            brief.Title,
            brief.Archetype,
            brief.Seed,
            brief.WidthMeters,
            brief.DepthMeters,
            brief.KitBlueprintIds,
            brief.Constraints,
            brief.Objectives,
            nodes,
            links,
            brief.LoreSourceIds,
            approvedForConstruction: false,
            estimatedCostUsd,
            providerKey,
            $"{brief.Provenance}:{providerProvenance}:prompt-{promptSha256}");
        var catalog = new AssistedAreaPlanCatalog(
            Guid.Parse("b1240000-0000-4000-8000-000000000001"),
            1,
            [plan],
            "assistant:g12-c");
        var diagnostics = AssistedAreaPlanProjectValidator.Validate(catalog, project)
            .Select(item => new CommandDiagnostic(item.Code, item.Message)).ToArray();
        return new AssistedAreaPlanProposal(
            diagnostics.Length == 0,
            diagnostics.Length == 0 ? plan : null,
            $"plan:{plan.Id:D}|area:{plan.AreaId:D}|archetype:{plan.Archetype}|nodes:{nodes.Count}|links:{links.Count}|geometry-applied:false",
            diagnostics,
            promptSha256,
            inputTokens,
            outputTokens,
            estimatedCostUsd,
            providerProvenance);

        AssistedAreaPlanProposal Failure(string message) => new(
            false, null, "no-change", [new CommandDiagnostic(BriefInvalidCode, message)],
            promptSha256, inputTokens, outputTokens, estimatedCostUsd, providerProvenance);
    }

    private static IReadOnlyList<AssistedAreaPlanNodeKind> Kinds(AssistedAreaArchetype archetype, int count)
    {
        var middle = archetype == AssistedAreaArchetype.Crypt
            ? new[] { AssistedAreaPlanNodeKind.Exploration, AssistedAreaPlanNodeKind.Encounter, AssistedAreaPlanNodeKind.Rest, AssistedAreaPlanNodeKind.Objective }
            : new[] { AssistedAreaPlanNodeKind.Social, AssistedAreaPlanNodeKind.Service, AssistedAreaPlanNodeKind.Exploration, AssistedAreaPlanNodeKind.Objective };
        return Enumerable.Range(0, count).Select(index => index switch
        {
            0 => AssistedAreaPlanNodeKind.Entry,
            var last when last == count - 1 => AssistedAreaPlanNodeKind.Exit,
            _ => middle[(index - 1) % middle.Length],
        }).ToArray();
    }

    private static (float X, float Z) Position(AssistedAreaPlanBrief brief, int index)
    {
        if (brief.Archetype == AssistedAreaArchetype.Village)
        {
            var angle = (MathF.PI * 2 * index / brief.NodeCount) + ((brief.Seed % 360) * MathF.PI / 180f);
            return (MathF.Round(MathF.Cos(angle) * brief.WidthMeters * 0.32f, 2),
                MathF.Round(MathF.Sin(angle) * brief.DepthMeters * 0.32f, 2));
        }
        var columns = Math.Max(2, (int)MathF.Ceiling(MathF.Sqrt(brief.NodeCount)));
        var row = index / columns;
        var column = index % columns;
        if (row % 2 == 1) column = columns - 1 - column;
        var x = -brief.WidthMeters * 0.35f + column * brief.WidthMeters * 0.7f / Math.Max(1, columns - 1);
        var rows = Math.Max(2, (int)MathF.Ceiling(brief.NodeCount / (float)columns));
        var z = -brief.DepthMeters * 0.35f + row * brief.DepthMeters * 0.7f / Math.Max(1, rows - 1);
        return (MathF.Round(x, 2), MathF.Round(z, 2));
    }

    private static string Label(AssistedAreaArchetype archetype, AssistedAreaPlanNodeKind kind, int index) =>
        $"{(archetype == AssistedAreaArchetype.Crypt ? "Crypte" : "Village")} · {kind} {index + 1}";

    private static Guid StableId(Guid planId, string suffix)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{planId:D}:{suffix}"));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes);
    }
}
