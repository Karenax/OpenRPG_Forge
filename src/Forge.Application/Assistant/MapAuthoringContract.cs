using System.Security.Cryptography;
using System.Text;

namespace Forge.Application.Assistant;

public static class MapAuthoringContract
{
    public const string Version = "2.2.0";
    public const string ResourceName = "OpenRPGForge.Assistant.MapAuthoringContract.md";

    private static readonly Lazy<string> EmbeddedContent = new(LoadContent);
    private static readonly Lazy<string> EmbeddedContentSha256 = new(() =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Content))).ToLowerInvariant());

    public static string Content => EmbeddedContent.Value;

    public static string ContentSha256 => EmbeddedContentSha256.Value;

    private static string LoadContent()
    {
        using var stream = typeof(MapAuthoringContract).Assembly.GetManifestResourceStream(ResourceName) ??
            throw new InvalidOperationException($"Embedded map-authoring contract '{ResourceName}' is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var content = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (content.Length == 0)
            throw new InvalidOperationException("Embedded map-authoring contract is empty.");
        return content;
    }
}
