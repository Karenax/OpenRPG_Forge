using System.Text;
using System.Text.Json;
using Godot;

namespace Forge.Studio.Session;

internal sealed record StudioUserState(
    string? LastWorkspace,
    string SectionId,
    string PageId,
    string? AreaId)
{
    internal static StudioUserState Default { get; } = new(null, "world", "zones", null);
}

internal static class StudioUserStateStore
{
    private const string RelativePath = "user://studio-state.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    internal static bool TryLoad(out StudioUserState state, out string? error)
    {
        var path = ProjectSettings.GlobalizePath(RelativePath);
        if (!File.Exists(path))
        {
            state = StudioUserState.Default;
            error = null;
            return true;
        }

        try
        {
            state = JsonSerializer.Deserialize<StudioUserState>(File.ReadAllText(path), Options) ??
                StudioUserState.Default;
            error = null;
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            state = StudioUserState.Default;
            error = exception.Message;
            return false;
        }
    }

    internal static bool TrySave(StudioUserState state, out string? error)
    {
        ArgumentNullException.ThrowIfNull(state);
        var path = ProjectSettings.GlobalizePath(RelativePath);
        var directory = Path.GetDirectoryName(path)!;
        var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(temporary, JsonSerializer.Serialize(state, Options), new UTF8Encoding(false));
            File.Move(temporary, path, overwrite: true);
            error = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = exception.Message;
            return false;
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
