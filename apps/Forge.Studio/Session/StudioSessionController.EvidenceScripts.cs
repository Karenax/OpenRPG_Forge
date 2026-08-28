namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    internal static bool CopyForgeScriptsForEvidence(string sourceRoot, string destinationRoot)
    {
        try
        {
            var sourceScripts = Path.Combine(sourceRoot, "scripts");
            if (!Directory.Exists(sourceScripts)) return true;
            foreach (var sourcePath in Directory.EnumerateFiles(sourceScripts, "*.forge",
                         SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(sourceRoot, sourcePath);
                var destinationPath = Path.Combine(destinationRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                File.Copy(sourcePath, destinationPath, overwrite: true);
            }
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
