#if TOOLS
using Godot;

namespace Forge.Toolset.Editor;

public partial class ForgeEditorPlugin
{
    private static string GetStartupWorkspaceDirectory(IReadOnlyList<string> userArguments)
    {
        const string workspacePrefix = "--workspace=";
        var requestedWorkspace = userArguments.FirstOrDefault(argument =>
            argument.StartsWith(workspacePrefix, StringComparison.Ordinal));
        if (requestedWorkspace is not null)
        {
            var path = requestedWorkspace[workspacePrefix.Length..].Trim();
            if (!string.IsNullOrWhiteSpace(path))
            {
                return Path.GetFullPath(path);
            }
        }

        var usesVerificationFixture = userArguments.Any(argument =>
            argument.Contains("smoke-test", StringComparison.Ordinal) ||
            argument.Contains("ui-capture=", StringComparison.Ordinal));
        return usesVerificationFixture
            ? GetBundledWorkspaceDirectory("crypt-of-ashes")
            : GetDefaultWorkspaceDirectory();
    }

    private static string GetDefaultWorkspaceDirectory() =>
        GetBundledWorkspaceDirectory("crypt-of-ashes");

    private static string GetBundledWorkspaceDirectory(string moduleSlug)
    {
        var toolsetDirectory = ProjectSettings.GlobalizePath("res://");
        return Path.GetFullPath(Path.Combine(
            toolsetDirectory,
            "..",
            "Forge.Runtime",
            "content",
            moduleSlug));
    }
}
#endif
