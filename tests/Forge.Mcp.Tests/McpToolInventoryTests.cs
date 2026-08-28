using Forge.McpHost;

namespace Forge.Mcp.Tests;

public sealed class McpToolInventoryTests
{
    [Fact]
    public void CheckedManifestMatchesEveryCompiledTool()
    {
        var validation = McpToolInventory.Validate();

        Assert.True(validation.Succeeded,
            $"{validation.Error} Missing: {string.Join(", ", validation.Missing)}; " +
            $"unexpected: {string.Join(", ", validation.Unexpected)}");
        Assert.Equal(271, validation.Discovered.Count);
        Assert.Contains("GetWorldGenerationRecipes", validation.Discovered);
        Assert.Contains("PlanDialogueVoiceImport", validation.Discovered);
        Assert.Contains("forge_get_network_assurance", validation.Discovered);
    }
}
