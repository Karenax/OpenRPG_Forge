using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Forge.Application.Gameplay;

public sealed record ContainedVoiceAudio(byte[] Bytes, string Extension);

public static class RuntimeModuleAssetPathResolver
{
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
    };

    private static readonly HashSet<string> SupportedAudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3",
        ".wav",
        ".ogg",
    };

    private static readonly HashSet<string> SupportedVoiceAudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3",
        ".wav",
    };

    public static string? ResolveImage(string? moduleContentRoot, string? authoredPath)
        => ResolveExistingFile(moduleContentRoot, authoredPath, SupportedImageExtensions);

    public static string? ResolveAudio(string? moduleContentRoot, string? authoredPath)
        => ResolveExistingFile(moduleContentRoot, authoredPath, SupportedAudioExtensions);

    public static string? ResolveVoiceAudio(string? moduleContentRoot, string? authoredPath)
        => ResolveExistingFile(moduleContentRoot, authoredPath, SupportedVoiceAudioExtensions);

    public static string? ResolveVoiceAudioDestination(string? moduleContentRoot, string? authoredPath)
        => ResolveWritableFile(moduleContentRoot, authoredPath, SupportedVoiceAudioExtensions);

    public static ContainedVoiceAudio? ReadVoiceAudio(
        string? moduleContentRoot,
        string? authoredPath,
        long maximumBytes)
    {
        if (maximumBytes < 1) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        var resolved = ResolveVoiceAudio(moduleContentRoot, authoredPath);
        if (resolved is null) return null;
        try
        {
            var root = ResolveRoot(moduleContentRoot!);
            if (root is null) return null;
            using var stream = new FileStream(resolved, FileMode.Open, FileAccess.Read, FileShare.Read);
            var handlePath = ResolveHandlePath(stream.SafeFileHandle, resolved);
            if (handlePath is null || !IsContained(root, handlePath) || stream.Length is < 1 ||
                stream.Length > maximumBytes || stream.Length > int.MaxValue)
                return null;
            var bytes = new byte[checked((int)stream.Length)];
            stream.ReadExactly(bytes);
            return new(bytes, Path.GetExtension(authoredPath!));
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or
                                          NotSupportedException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? ResolveExistingFile(
        string? moduleContentRoot,
        string? authoredPath,
        IReadOnlySet<string> supportedExtensions)
    {
        if (string.IsNullOrWhiteSpace(moduleContentRoot) || string.IsNullOrWhiteSpace(authoredPath)) return null;

        try
        {
            var relative = authoredPath.Trim();
            if (relative.IndexOf('\0') >= 0 || Path.IsPathFullyQualified(relative) ||
                relative.Contains("://", StringComparison.Ordinal) ||
                Uri.TryCreate(relative, UriKind.Absolute, out _))
                return null;
            if (!supportedExtensions.Contains(Path.GetExtension(relative))) return null;

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

    private static string? ResolveWritableFile(
        string? moduleContentRoot,
        string? authoredPath,
        IReadOnlySet<string> supportedExtensions)
    {
        if (string.IsNullOrWhiteSpace(moduleContentRoot) || string.IsNullOrWhiteSpace(authoredPath)) return null;

        try
        {
            var relative = authoredPath.Trim();
            if (relative.IndexOf('\0') >= 0 || Path.IsPathFullyQualified(relative) ||
                relative.Contains("://", StringComparison.Ordinal) ||
                Uri.TryCreate(relative, UriKind.Absolute, out _) ||
                !supportedExtensions.Contains(Path.GetExtension(relative)))
                return null;

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
            for (var index = 0; index < segments.Length - 1; index++)
            {
                current = Path.GetFullPath(Path.Combine(current, segments[index]));
                if (!IsContained(resolvedRoot, current)) return null;
                var info = new DirectoryInfo(current);
                if (!info.Exists) continue;
                if (info.LinkTarget is null) continue;
                var target = info.ResolveLinkTarget(returnFinalTarget: true);
                if (target is null) return null;
                current = Path.GetFullPath(target.FullName);
                if (!IsContained(resolvedRoot, current)) return null;
            }

            var candidate = Path.GetFullPath(Path.Combine(current, segments[^1]));
            if (!IsContained(resolvedRoot, candidate)) return null;
            if (Directory.Exists(candidate)) return null;
            if (File.Exists(candidate))
            {
                var info = new FileInfo(candidate);
                if (info.LinkTarget is not null)
                {
                    var target = info.ResolveLinkTarget(returnFinalTarget: true);
                    if (target is null || !IsContained(resolvedRoot, Path.GetFullPath(target.FullName))) return null;
                }
            }
            return candidate;
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

    private static string? ResolveRoot(string root)
    {
        var info = new DirectoryInfo(Path.GetFullPath(root));
        if (!info.Exists) return null;
        var resolved = info.LinkTarget is null ? info.FullName : info.ResolveLinkTarget(true)?.FullName;
        return string.IsNullOrWhiteSpace(resolved) ? null : Path.GetFullPath(resolved);
    }

    private static string? ResolveHandlePath(SafeFileHandle handle, string fallback)
    {
        if (OperatingSystem.IsWindows())
        {
            var capacity = 512;
            while (capacity <= 32_768)
            {
                var buffer = new StringBuilder(capacity);
                var length = GetFinalPathNameByHandle(handle, buffer, (uint)capacity, 0);
                if (length == 0) return null;
                if (length < capacity)
                {
                    var value = buffer.ToString();
                    if (value.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
                        value = @"\\" + value[8..];
                    else if (value.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
                        value = value[4..];
                    return Path.GetFullPath(value);
                }
                capacity = checked((int)length + 1);
            }
            return null;
        }
        if (OperatingSystem.IsLinux())
        {
            var target = new FileInfo($"/proc/self/fd/{handle.DangerousGetHandle().ToInt64()}")
                .ResolveLinkTarget(returnFinalTarget: true);
            return target is null ? null : Path.GetFullPath(target.FullName);
        }
        return Path.GetFullPath(fallback);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(
        SafeFileHandle file,
        StringBuilder path,
        uint pathLength,
        uint flags);
}
