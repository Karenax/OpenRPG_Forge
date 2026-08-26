using System.Text;
using System.Text.Json;
using Forge.Domain.WorldGeneration;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Workspaces;

public static class WorldGenerationRecipeStore
{
    public const string RelativePath = "world/generation/recipes.json";
    public const string ReadFailedCode = "world_generation.recipe.read_failed";
    public const string WriteFailedCode = "world_generation.recipe.write_failed";

    public static WorldGenerationRecipeLoadResult Load(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var path = ResolvePath(workspaceRoot);
        if (!File.Exists(path)) return new(WorldGenerationRecipeCatalog.CreateDefault(), true, []);
        try
        {
            return new(WorldGenerationRecipeJsonSerializer.Deserialize(File.ReadAllText(path)), false, []);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
        {
            return new(WorldGenerationRecipeCatalog.CreateDefault(), false,
                [new ProjectSessionDiagnostic(ReadFailedCode, exception.Message, path)]);
        }
    }

    public static ProjectSessionSaveResult Save(string workspaceRoot, WorldGenerationRecipeCatalog catalog)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(catalog);
        var path = ResolvePath(workspaceRoot);
        try
        {
            var directory = Path.GetDirectoryName(path)
                ?? throw new IOException("The world-generation recipe path has no parent directory.");
            Directory.CreateDirectory(directory);
            AtomicWorkspaceFile.Write(path, WorldGenerationRecipeJsonSerializer.Serialize(catalog));
            return new(true, []);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
        {
            return new(false, [new ProjectSessionDiagnostic(WriteFailedCode, exception.Message, path)]);
        }
    }

    private static string ResolvePath(string workspaceRoot) =>
        Path.Combine(Path.GetFullPath(workspaceRoot), "world", "generation", "recipes.json");
}

public sealed record WorldGenerationRecipeLoadResult(
    WorldGenerationRecipeCatalog Catalog,
    bool FileMissing,
    IReadOnlyList<ProjectSessionDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}

internal static class AtomicWorkspaceFile
{
    internal static void Write(string targetPath, string content)
    {
        var directory = Path.GetDirectoryName(targetPath)
            ?? throw new IOException("The workspace path has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{Path.GetRandomFileName()}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
