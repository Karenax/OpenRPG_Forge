using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Domain.Assistant;
using Forge.Domain.Projects;

namespace Forge.Application.Assistant;

public static class AssistedAreaPlanProposalService
{
    public static async Task<AssistedAreaPlanProposal> ProposeAsync(
        AssistedAreaPlanBrief brief,
        ProjectDefinition project,
        LoreIndexDefinition lore,
        IAssistantProvider provider,
        AssistantProviderDefinition providerDefinition,
        bool networkAuthorized,
        bool projectContentAuthorized,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var lorePacket = LoreContextAssembler.Assemble(lore, project, string.Join(' ', brief.Objectives), 8);
        var system = "You propose a bounded OpenRPG Forge area topology. Never emit geometry mutations or executable code.";
        var user = JsonSerializer.Serialize(new
        {
            brief.Title,
            archetype = brief.Archetype.ToString(),
            brief.NodeCount,
            brief.WidthMeters,
            brief.DepthMeters,
            constraints = brief.Constraints,
            objectives = brief.Objectives,
            lore = projectContentAuthorized ? lorePacket.Content : "[project content withheld]",
        });
        var schema = "{\"type\":\"object\",\"required\":[\"summary\",\"semanticDiff\"],\"properties\":{\"summary\":{\"type\":\"string\"},\"semanticDiff\":{\"type\":\"array\"}}}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', system, user, schema))))
            .ToLowerInvariant();
        var prompt = new AssistantPromptRenderResult("area-plan", "1.0.0", system, user, schema, hash);
        var response = await provider.GenerateAsync(new AssistantProviderRequest(
            brief.PlanId,
            Guid.NewGuid(),
            prompt,
            AssistantPermissionLevel.Propose,
            Math.Min(2048, providerDefinition.MaximumOutputTokens),
            networkAuthorized,
            projectContentAuthorized), cancellationToken).ConfigureAwait(false);
        return AssistedAreaPlanGenerator.Generate(
            brief,
            project,
            response.ProviderKey,
            response.EstimatedCost,
            response.Provenance,
            hash,
            response.InputTokens,
            response.OutputTokens);
    }
}
