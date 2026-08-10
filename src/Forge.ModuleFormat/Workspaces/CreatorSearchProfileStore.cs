using System.Text;
using System.Text.Json;
using Forge.Domain.Authoring;

namespace Forge.ModuleFormat.Workspaces;

public static class CreatorSearchProfileStore
{
    public const string RelativePath = ".forge/editor-search.json";
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static CreatorSearchProfile Load(string workspaceRoot, Guid projectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var path = Path.Combine(Path.GetFullPath(workspaceRoot), RelativePath);
        if (!File.Exists(path)) return CreatorSearchProfile.CreateDefault(projectId);
        return JsonSerializer.Deserialize<CreatorSearchProfile>(File.ReadAllText(path, Encoding.UTF8), Options)
            ?? throw new InvalidDataException("Creator search profile is empty.");
    }

    public static void Save(string workspaceRoot, CreatorSearchProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(profile);
        var path = Path.Combine(Path.GetFullPath(workspaceRoot), RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporary,
            JsonSerializer.Serialize(profile, Options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n",
            new UTF8Encoding(false));
        File.Move(temporary, path, overwrite: true);
    }
}
