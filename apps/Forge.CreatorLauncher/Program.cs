using System.Diagnostics;
using System.Text;

namespace Forge.CreatorLauncher;

internal static class Program
{
    private const string EngineRelativePath = "engine/OpenRPG_Forge_Editor.exe";
    private const string ToolsetRelativePath = "workspace/apps/Forge.Toolset";
    private const string RuntimeProjectRelativePath = "workspace/apps/Forge.Runtime/project.godot";
    private const string ContextRelativePath = "workspace/PROJECT_CONTEXT.md";
    private const string InstanceMutexName = @"Local\OpenRPGForge.Creator.v1";

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var root = AppContext.BaseDirectory;
        try
        {
            var layout = ValidateLayout(root);
            if (args.Contains("--verify", StringComparer.Ordinal))
            {
                return 0;
            }

            using var instanceMutex = new Mutex(initiallyOwned: true, InstanceMutexName, out var ownsInstance);
            if (!ownsInstance)
            {
                MessageBox.Show(
                    "OpenRPG Forge est déjà ouvert.",
                    "OpenRPG Forge",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return 0;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = layout.EnginePath,
                WorkingDirectory = root,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add("--rendering-method");
            startInfo.ArgumentList.Add("forward_plus");
            startInfo.ArgumentList.Add("--maximized");
            startInfo.ArgumentList.Add("--single-window");
            startInfo.ArgumentList.Add("--editor");
            startInfo.ArgumentList.Add("--path");
            startInfo.ArgumentList.Add(layout.ToolsetPath);
            startInfo.ArgumentList.Add("--");
            startInfo.ArgumentList.Add("--creator-shell");
            var process = Process.Start(startInfo) ??
                throw new InvalidOperationException("OpenRPG Forge Creator could not start its editor process.");
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Exception exception)
        {
            WriteFailure(root, exception);
            if (!args.Contains("--verify", StringComparer.Ordinal))
            {
                MessageBox.Show(
                    "OpenRPG Forge Creator ne peut pas démarrer.\n\n" + exception.Message +
                    "\n\nConsultez launcher-error.log à côté de l'application.",
                    "OpenRPG Forge Creator",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            return 1;
        }
    }

    private static CreatorLayout ValidateLayout(string root)
    {
        var enginePath = RequiredFile(root, EngineRelativePath);
        var toolsetPath = RequiredDirectory(root, ToolsetRelativePath);
        RequiredFile(root, $"{ToolsetRelativePath}/project.godot");
        RequiredFile(root, RuntimeProjectRelativePath);
        RequiredFile(root, ContextRelativePath);
        RequiredFile(root, "workspace/AGENTS.md");
        RequiredFile(root, "workspace/apps/Forge.Runtime/content/crypt-of-ashes/project.json");
        return new CreatorLayout(enginePath, toolsetPath);
    }

    private static string RequiredFile(string root, string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Fichier requis absent : {relativePath}", path);
        }

        return path;
    }

    private static string RequiredDirectory(string root, string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Dossier requis absent : {relativePath}");
        }

        return path;
    }

    private static void WriteFailure(string root, Exception exception)
    {
        try
        {
            var text = new StringBuilder()
                .AppendLine("OpenRPG Forge Creator launcher failure")
                .AppendLine(DateTimeOffset.UtcNow.ToString("O"))
                .AppendLine(exception.ToString())
                .ToString();
            File.WriteAllText(Path.Combine(root, "launcher-error.log"), text, new UTF8Encoding(false));
        }
        catch
        {
            // The original launch failure remains the actionable result even on read-only media.
        }
    }

    private sealed record CreatorLayout(string EnginePath, string ToolsetPath);
}
