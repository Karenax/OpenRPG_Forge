using System.Security.Cryptography;
using System.Text;

namespace Forge.Domain.RuntimePolicies;

internal static class RuntimePolicyIdFactory
{
    internal static Guid Create(Guid ownerId, string discriminator)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:runtime-policy:{ownerId:D}:{discriminator}"));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[7] = (byte)((guidBytes[7] & 0x0f) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3f) | 0x80);
        return new Guid(guidBytes);
    }
}
