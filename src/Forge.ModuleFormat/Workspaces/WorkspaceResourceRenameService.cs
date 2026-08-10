using System.Security.Cryptography;
using System.Text;

namespace Forge.ModuleFormat.Workspaces;

public sealed record WorkspaceResourceReferenceEdit(string RelativePath, string Before, string After);

public sealed record WorkspaceResourceRenamePreview(
    Guid Id,
    Guid ResourceId,
    int SchemaVersion,
    string Provenance,
    string WorkspaceRoot,
    string PreviousPath,
    string NewPath,
    string SourceSha256,
    IReadOnlyList<WorkspaceResourceReferenceEdit> ReferenceEdits)
{
    public const int CurrentSchemaVersion = 1;
}

public static class WorkspaceResourceRenameService
{
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".forge", ".json", ".md", ".txt", ".csv", ".yaml", ".yml", ".tscn" };

    public static WorkspaceResourceRenamePreview Preview(
        string workspaceRoot,
        string previousPath,
        string newPath,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(previousPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var root = Path.GetFullPath(workspaceRoot);
        var previous = Normalize(previousPath);
        var next = Normalize(newPath);
        ValidateRelative(previous);
        ValidateRelative(next);
        if (!string.Equals(Path.GetExtension(previous), Path.GetExtension(next), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Resource rename must preserve the file extension.");
        var source = Resolve(root, previous);
        var destination = Resolve(root, next);
        if (!File.Exists(source)) throw new FileNotFoundException("Resource to rename does not exist.", source);
        if (File.Exists(destination)) throw new IOException("Resource rename destination already exists.");

        var edits = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !string.Equals(path, source, StringComparison.OrdinalIgnoreCase))
            .Where(path => TextExtensions.Contains(Path.GetExtension(path)))
            .Where(path => !Path.GetRelativePath(root, path).StartsWith(".git", StringComparison.OrdinalIgnoreCase))
            .Select(path => (Path: path, Before: File.ReadAllText(path, Encoding.UTF8)))
            .Select(item => new WorkspaceResourceReferenceEdit(
                Normalize(Path.GetRelativePath(root, item.Path)), item.Before,
                item.Before.Replace(previous, next, StringComparison.Ordinal)
                    .Replace(previous.Replace('/', '\\'), next.Replace('/', '\\'), StringComparison.Ordinal)))
            .Where(edit => !string.Equals(edit.Before, edit.After, StringComparison.Ordinal))
            .OrderBy(edit => edit.RelativePath, StringComparer.Ordinal)
            .ToArray();
        return new WorkspaceResourceRenamePreview(Guid.NewGuid(),
            WorkspaceResourceIdentityStore.Resolve(root, previous), WorkspaceResourceRenamePreview.CurrentSchemaVersion,
            provenance.Trim(), root, previous, next, Hash(source), edits);
    }

    public static void Apply(WorkspaceResourceRenamePreview preview)
    {
        Validate(preview);
        var source = Resolve(preview.WorkspaceRoot, preview.PreviousPath);
        var destination = Resolve(preview.WorkspaceRoot, preview.NewPath);
        if (!File.Exists(source) || !string.Equals(Hash(source), preview.SourceSha256, StringComparison.Ordinal))
            throw new InvalidOperationException("Resource changed after rename preview.");
        if (File.Exists(destination)) throw new IOException("Resource rename destination already exists.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var written = new List<WorkspaceResourceReferenceEdit>();
        try
        {
            File.Move(source, destination);
            foreach (var edit in preview.ReferenceEdits)
            {
                var path = Resolve(preview.WorkspaceRoot, edit.RelativePath);
                if (!string.Equals(File.ReadAllText(path, Encoding.UTF8), edit.Before, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Referenced document changed after preview: {edit.RelativePath}");
                WriteAtomic(path, edit.After);
                written.Add(edit);
            }
            WorkspaceResourceIdentityStore.Migrate(preview.WorkspaceRoot, preview.PreviousPath, preview.NewPath,
                preview.ResourceId);
        }
        catch
        {
            foreach (var edit in written.AsEnumerable().Reverse())
                WriteAtomic(Resolve(preview.WorkspaceRoot, edit.RelativePath), edit.Before);
            if (File.Exists(destination) && !File.Exists(source)) File.Move(destination, source);
            throw;
        }
    }

    public static void Undo(WorkspaceResourceRenamePreview preview)
    {
        Validate(preview);
        var source = Resolve(preview.WorkspaceRoot, preview.PreviousPath);
        var destination = Resolve(preview.WorkspaceRoot, preview.NewPath);
        if (!File.Exists(destination) || File.Exists(source))
            throw new InvalidOperationException("Applied resource rename cannot be undone in the current workspace state.");
        foreach (var edit in preview.ReferenceEdits)
        {
            var path = Resolve(preview.WorkspaceRoot, edit.RelativePath);
            if (!string.Equals(File.ReadAllText(path, Encoding.UTF8), edit.After, StringComparison.Ordinal))
                throw new InvalidOperationException($"Referenced document changed after rename: {edit.RelativePath}");
        }
        File.Move(destination, source);
        foreach (var edit in preview.ReferenceEdits) WriteAtomic(Resolve(preview.WorkspaceRoot, edit.RelativePath), edit.Before);
        WorkspaceResourceIdentityStore.Migrate(preview.WorkspaceRoot, preview.NewPath, preview.PreviousPath,
            preview.ResourceId);
    }

    public static void Redo(WorkspaceResourceRenamePreview preview) => Apply(preview);

    private static void Validate(WorkspaceResourceRenamePreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if (preview.Id == Guid.Empty || preview.ResourceId == Guid.Empty ||
            preview.SchemaVersion != WorkspaceResourceRenamePreview.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(preview.Provenance))
            throw new ArgumentException("Resource rename preview UUID, schema and provenance are invalid.", nameof(preview));
        ValidateRelative(preview.PreviousPath);
        ValidateRelative(preview.NewPath);
    }

    private static void ValidateRelative(string path)
    {
        if (Path.IsPathRooted(path) || path.Split('/').Any(part => part is "" or "." or "..") ||
            !(path.StartsWith("scripts/", StringComparison.OrdinalIgnoreCase) ||
              path.StartsWith("assets/", StringComparison.OrdinalIgnoreCase) ||
              path.StartsWith("content/assets/", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Resource paths must stay below scripts, assets or content/assets.", nameof(path));
    }

    private static string Resolve(string root, string relative)
    {
        var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Resource path escapes the workspace.", nameof(relative));
        return full;
    }

    private static string Normalize(string path) => path.Replace('\\', '/').Trim();
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static void WriteAtomic(string path, string content)
    {
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporary, content, new UTF8Encoding(false));
        File.Move(temporary, path, overwrite: true);
    }
}
