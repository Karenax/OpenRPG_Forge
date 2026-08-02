using System.Text.Json;
using Forge.Domain.Areas;
using Forge.Domain.Navigation;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Navigation;

public static class NavigationBakeStore
{
    public const string CurrentFormatVersion = "1.0.0";

    private static readonly JsonSerializerOptions Options = ProjectJsonSerializer.CreateOptions();

    public static string GetRelativePath(AreaId areaId, NavigationAgentProfileId agentId)
    {
        ArgumentNullException.ThrowIfNull(areaId);
        ArgumentNullException.ThrowIfNull(agentId);
        return $".forge/navigation/{areaId}-{agentId}.json";
    }

    public static NavigationSurface? TryLoad(
        string workspacePath,
        AreaId areaId,
        NavigationAgentProfileId agentId)
    {
        var path = Resolve(workspacePath, areaId, agentId);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var document = JsonSerializer.Deserialize<NavigationBakeDocument>(File.ReadAllText(path), Options);
            return document is not null &&
                string.Equals(document.FormatVersion, CurrentFormatVersion, StringComparison.Ordinal)
                ? document.Surface
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or
            ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    public static void Save(string workspacePath, NavigationSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        var path = Resolve(workspacePath, surface.AreaId, surface.AgentProfileId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Navigation bake path has no parent directory."));
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(
                new NavigationBakeDocument(CurrentFormatVersion, surface), Options));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static string Resolve(string workspacePath, AreaId areaId, NavigationAgentProfileId agentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        return Path.Combine(
            Path.GetFullPath(workspacePath),
            GetRelativePath(areaId, agentId).Replace('/', Path.DirectorySeparatorChar));
    }

    private sealed record NavigationBakeDocument(string FormatVersion, NavigationSurface Surface);
}
