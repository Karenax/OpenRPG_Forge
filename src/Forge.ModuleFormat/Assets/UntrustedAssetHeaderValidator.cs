using System.Buffers.Binary;

namespace Forge.ModuleFormat.Assets;

public static class UntrustedAssetHeaderValidator
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static bool IsSupportedImage(ReadOnlySpan<byte> bytes, int maximumBytes)
    {
        if (maximumBytes < 64 || bytes.Length < 4 || bytes.Length > maximumBytes) return false;
        if (bytes.Length >= PngSignature.Length && bytes[..PngSignature.Length].SequenceEqual(PngSignature)) return true;
        if (bytes.Length >= 4 && bytes[0] == 0xff && bytes[1] == 0xd8 &&
            bytes[^2] == 0xff && bytes[^1] == 0xd9) return true;
        return bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8);
    }

    public static bool IsSupportedModel(ReadOnlySpan<byte> bytes, int maximumBytes)
    {
        if (maximumBytes < 64 || bytes.Length < 12 || bytes.Length > maximumBytes ||
            !bytes[..4].SequenceEqual("glTF"u8)) return false;
        var version = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..8]);
        var declaredLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..12]);
        return version == 2 && declaredLength == bytes.Length;
    }
}
