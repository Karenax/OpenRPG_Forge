using System.Security.Cryptography;
using System.Text;

namespace Forge.ArchitectureGraph;

public sealed class SourceScanner
{
    private static readonly HashSet<string> AnalyzedSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cc", ".cfg", ".cpp", ".cs", ".csproj", ".cxx", ".gd", ".godot",
        ".h", ".hpp", ".json", ".md", ".props", ".py", ".sln", ".targets",
        ".toml", ".tres", ".tscn", ".yaml", ".yml",
    };

    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".forge", ".git", ".godot", ".idea", ".mypy_cache", ".pytest_cache", ".ruff_cache",
        ".tmp", ".venv", ".vs", ".vscode", "__pycache__", "artifacts", "bin",
        "build", "coverage", "dist", "generated", "node_modules", "obj", "old",
        "target", "tmp", "vendor", "venv",
    };

    private static readonly HashSet<string> ExcludedSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bin", ".db", ".key", ".model", ".onnx", ".p12", ".pem", ".sqlite",
        ".sqlite3",
    };

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly UTF8Encoding ReplacementUtf8 = new(false, false);

    public IReadOnlyList<SourceDocument> Discover(
        string root,
        IEnumerable<string>? excludedArtifacts = null)
    {
        var normalizedRoot = Path.GetFullPath(root);
        var artifactPaths = new HashSet<string>(StringComparer.Ordinal);
        if (excludedArtifacts is not null)
        {
            foreach (var artifact in excludedArtifacts)
            {
                if (TryRelativePath(normalizedRoot, artifact, out var relative))
                {
                    artifactPaths.Add(relative);
                }
            }
        }

        var documents = new List<SourceDocument>();
        var pending = new Stack<string>();
        pending.Push(normalizedRoot);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var child in Directory.EnumerateDirectories(directory)
                         .OrderByDescending(path => path, StringComparer.Ordinal))
            {
                if (!ExcludedDirectories.Contains(Path.GetFileName(child)))
                {
                    pending.Push(child);
                }
            }

            foreach (var file in Directory.EnumerateFiles(directory)
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                var relativePath = NormalizePath(Path.GetRelativePath(normalizedRoot, file));
                if (artifactPaths.Contains(relativePath) || IsExcludedFile(file))
                {
                    continue;
                }

                var suffix = Path.GetExtension(file);
                if (!AnalyzedSuffixes.Contains(suffix))
                {
                    continue;
                }

                var bytes = NormalizeLineEndings(File.ReadAllBytes(file));
                var invalidUtf8 = false;
                string text;
                try
                {
                    text = StrictUtf8.GetString(bytes);
                }
                catch (DecoderFallbackException)
                {
                    invalidUtf8 = true;
                    text = ReplacementUtf8.GetString(bytes);
                }

                if (text.Length > 0 && text[0] == '\uFEFF')
                {
                    text = text[1..];
                }

                documents.Add(new SourceDocument(
                    relativePath,
                    bytes,
                    text,
                    Convert.ToHexStringLower(SHA256.HashData(bytes)),
                    invalidUtf8));
            }
        }

        return documents.OrderBy(item => item.Path, StringComparer.Ordinal).ToArray();
    }

    private static byte[] NormalizeLineEndings(byte[] bytes)
    {
        if (!bytes.AsSpan().Contains((byte)'\r'))
        {
            return bytes;
        }

        using var normalized = new MemoryStream(bytes.Length);
        for (var index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != (byte)'\r')
            {
                normalized.WriteByte(bytes[index]);
                continue;
            }

            normalized.WriteByte((byte)'\n');
            if (index + 1 < bytes.Length && bytes[index + 1] == (byte)'\n')
            {
                index++;
            }
        }

        return normalized.ToArray();
    }

    public static IReadOnlyList<SourceManifestEntry> CreateManifest(
        IReadOnlyList<SourceDocument> sources)
    {
        return sources
            .Select(source => new SourceManifestEntry(
                source.Path,
                $"sha256:{source.Sha256}",
                source.Bytes.LongLength))
            .OrderBy(item => item.Path, StringComparer.Ordinal)
            .ToArray();
    }

    public static string ComputeManifestDigest(IReadOnlyList<SourceManifestEntry> manifest)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var entry in manifest)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(entry.Path));
            hash.AppendData([0]);
            hash.AppendData(Encoding.ASCII.GetBytes(entry.Sha256));
            hash.AppendData([0]);
            hash.AppendData(Encoding.ASCII.GetBytes(entry.Size.ToString(
                System.Globalization.CultureInfo.InvariantCulture)));
            hash.AppendData([0]);
        }

        return $"sha256:{Convert.ToHexStringLower(hash.GetHashAndReset())}";
    }

    public static string NormalizePath(string path) => path.Replace('\\', '/');

    private static bool TryRelativePath(string root, string path, out string relative)
    {
        var fullPath = Path.GetFullPath(path);
        var candidate = Path.GetRelativePath(root, fullPath);
        if (candidate == ".." || candidate.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            relative = string.Empty;
            return false;
        }

        relative = NormalizePath(candidate);
        return true;
    }

    private static bool IsExcludedFile(string path)
    {
        var name = Path.GetFileName(path);
        var suffix = Path.GetExtension(path);
        return name.Equals(".env", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith(".env.", StringComparison.OrdinalIgnoreCase)
            || ExcludedSuffixes.Contains(suffix)
            || name.EndsWith(".sqlite-journal", StringComparison.OrdinalIgnoreCase);
    }
}
