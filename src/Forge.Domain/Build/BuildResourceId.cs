using System.Security.Cryptography;
using System.Text;

namespace Forge.Domain.Build;

public sealed record BuildResourceId
{
    public Guid Value { get; }

    private BuildResourceId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A build resource ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public static BuildResourceId From(Guid value) => new(value);

    public static BuildResourceId Parse(string value) => From(Guid.Parse(value));

    public static BuildResourceId ForPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalized = path.Replace('\\', '/').Trim().ToLowerInvariant();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return From(new Guid(bytes[..16]));
    }

    public override string ToString() => Value.ToString("D");
}
