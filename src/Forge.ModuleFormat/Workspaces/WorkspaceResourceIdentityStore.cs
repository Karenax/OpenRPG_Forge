using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Forge.ModuleFormat.Workspaces;

public static class WorkspaceResourceIdentityStore
{
    public const string RelativePath = ".forge/resource-identities.json";
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static Guid Resolve(string workspaceRoot, string relativePath)
    {
        var normalized = Normalize(relativePath);
        var identities = Load(workspaceRoot);
        return identities.TryGetValue(normalized, out var id) ? id : Stable(normalized);
    }

    public static void Migrate(string workspaceRoot, string previousPath, string newPath, Guid id)
    {
        var identities = Load(workspaceRoot).ToDictionary(item => item.Key, item => item.Value,
            StringComparer.OrdinalIgnoreCase);
        identities.Remove(Normalize(previousPath));
        identities[Normalize(newPath)] = id;
        Save(workspaceRoot, identities);
    }

    private static IReadOnlyDictionary<string, Guid> Load(string workspaceRoot)
    {
        var path = Path.Combine(Path.GetFullPath(workspaceRoot), RelativePath);
        if (!File.Exists(path)) return new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        return JsonSerializer.Deserialize<Dictionary<string, Guid>>(File.ReadAllText(path, Encoding.UTF8), Options)
            ?? throw new InvalidDataException("Resource identity store is empty.");
    }

    private static void Save(string workspaceRoot, IReadOnlyDictionary<string, Guid> identities)
    {
        var path = Path.Combine(Path.GetFullPath(workspaceRoot), RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporary,
            JsonSerializer.Serialize(identities.OrderBy(item => item.Key, StringComparer.Ordinal)
                .ToDictionary(item => item.Key, item => item.Value), Options) + "\n", new UTF8Encoding(false));
        File.Move(temporary, path, overwrite: true);
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');
    private static Guid Stable(string path)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:resource:{path.ToLowerInvariant()}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }
}
