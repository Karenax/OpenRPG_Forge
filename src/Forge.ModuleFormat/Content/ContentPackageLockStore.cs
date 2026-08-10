using System.Text;
using System.Text.Json;
using Forge.Domain.Content;

namespace Forge.ModuleFormat.Content;

public sealed class ContentPackageLockStore
{
    public const string RelativePath = "content/packages.lock.json";
    private readonly string _workspaceRoot;

    public ContentPackageLockStore(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        _workspaceRoot = System.IO.Path.GetFullPath(workspaceRoot);
    }

    public string Path => System.IO.Path.Combine(_workspaceRoot, RelativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));

    public void Save(Guid moduleId, IReadOnlyList<ContentPackageLockEntry> entries)
    {
        var content = ContentPackageCompiler.SerializeLock(moduleId, entries);
        var directory = System.IO.Path.GetDirectoryName(Path)!;
        Directory.CreateDirectory(directory);
        var temporary = System.IO.Path.Combine(directory, $".{System.IO.Path.GetFileName(Path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            File.Move(temporary, Path, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public JsonDocument Load() => JsonDocument.Parse(File.ReadAllText(Path));
}
