using System.Text;
using Forge.Domain.Authoring;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Workspaces;

public static class EditorWorkspaceProfileStore
{
    public const string RelativePath = ".forge/editor-profile.json";

    public static void Save(string workspaceRoot, EditorWorkspaceProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(profile);
        var path = Path.Combine(Path.GetFullPath(workspaceRoot), RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporary, EditorWorkspaceProfileJsonSerializer.Serialize(profile) + Environment.NewLine,
            new UTF8Encoding(false));
        File.Move(temporary, path, overwrite: true);
    }

    public static EditorWorkspaceProfile? TryLoad(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var path = Path.Combine(Path.GetFullPath(workspaceRoot), RelativePath);
        return File.Exists(path)
            ? EditorWorkspaceProfileJsonSerializer.Deserialize(File.ReadAllText(path, Encoding.UTF8))
            : null;
    }
}
