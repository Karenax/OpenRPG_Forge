using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Forge.ModuleFormat.Packages;

internal static class ModuleContentHash
{
    internal static string Compute(IReadOnlyList<(string Path, byte[] Content)> entries)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var entry in entries.OrderBy(entry => entry.Path, StringComparer.Ordinal))
        {
            AppendPath(hash, entry.Path);
            hash.AppendData(entry.Content);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    internal static string Compute(IEnumerable<ZipArchiveEntry> entries)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var entry in entries
                     .Where(entry => !string.Equals(entry.FullName, "manifest.json", StringComparison.Ordinal))
                     .Where(entry => !entry.FullName.EndsWith("/", StringComparison.Ordinal))
                     .OrderBy(entry => entry.FullName, StringComparer.Ordinal))
        {
            AppendPath(hash, entry.FullName);
            using var stream = entry.Open();
            var buffer = new byte[64 * 1024];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                hash.AppendData(buffer, 0, read);
            }
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void AppendPath(IncrementalHash hash, string path)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(path));
        hash.AppendData([0]);
    }
}
