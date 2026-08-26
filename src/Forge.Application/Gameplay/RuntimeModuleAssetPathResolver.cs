namespace Forge.Application.Gameplay;

public static class RuntimeModuleAssetPathResolver
{
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
    };

    public static string? ResolveImage(string? moduleContentRoot, string? authoredPath)
    {
        if (string.IsNullOrWhiteSpace(moduleContentRoot) || string.IsNullOrWhiteSpace(authoredPath)) return null;

        try
        {
            var relative = authoredPath.Trim();
            if (relative.IndexOf('\0') >= 0 || Path.IsPathFullyQualified(relative) ||
                relative.Contains("://", StringComparison.Ordinal) ||
                Uri.TryCreate(relative, UriKind.Absolute, out _))
                return null;
            if (!SupportedImageExtensions.Contains(Path.GetExtension(relative))) return null;

            var rootInfo = new DirectoryInfo(Path.GetFullPath(moduleContentRoot));
            if (!rootInfo.Exists) return null;
            var resolvedRoot = rootInfo.LinkTarget is null
                ? rootInfo.FullName
                : rootInfo.ResolveLinkTarget(returnFinalTarget: true)?.FullName;
            if (string.IsNullOrWhiteSpace(resolvedRoot)) return null;
            resolvedRoot = Path.GetFullPath(resolvedRoot);

            var segments = relative
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0 || segments.Any(segment => segment is "." or "..")) return null;

            var current = resolvedRoot;
            for (var index = 0; index < segments.Length; index++)
            {
                current = Path.GetFullPath(Path.Combine(current, segments[index]));
                if (!IsContained(resolvedRoot, current)) return null;

                FileSystemInfo info = index == segments.Length - 1
                    ? new FileInfo(current)
                    : new DirectoryInfo(current);
                if (!info.Exists) return null;
                if (info.LinkTarget is null) continue;

                var target = info.ResolveLinkTarget(returnFinalTarget: true);
                if (target is null) return null;
                current = Path.GetFullPath(target.FullName);
                if (!IsContained(resolvedRoot, current)) return null;
            }

            return File.Exists(current) && IsContained(resolvedRoot, current) ? current : null;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or
                                          NotSupportedException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsContained(string root, string candidate)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(root, candidate, comparison)) return true;
        var prefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, comparison);
    }
}
