using System.Buffers.Binary;

namespace Forge.Application.Gameplay;

public static class AudioMediaStructureValidator
{
    public static bool Matches(string extension, ReadOnlySpan<byte> bytes)
    {
        if (extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase))
            return IsStructurallyValidMp3(bytes);
        if (extension.Equals(".wav", StringComparison.OrdinalIgnoreCase))
            return IsStructurallyValidWav(bytes);
        return false;
    }

    public static bool Matches(string extension, string mimeType, ReadOnlySpan<byte> bytes)
    {
        if (extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase))
            return IsMp3Mime(mimeType) && IsStructurallyValidMp3(bytes);
        if (extension.Equals(".wav", StringComparison.OrdinalIgnoreCase))
            return IsWavMime(mimeType) && IsStructurallyValidWav(bytes);
        return false;
    }

    private static bool IsMp3Mime(string value) =>
        value.Equals("audio/mpeg", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("audio/mp3", StringComparison.OrdinalIgnoreCase);

    private static bool IsWavMime(string value) =>
        value.Equals("audio/wav", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("audio/x-wav", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("audio/vnd.wave", StringComparison.OrdinalIgnoreCase);

    private static bool IsStructurallyValidMp3(ReadOnlySpan<byte> bytes)
    {
        var frameOffset = 0;
        if (bytes.Length >= 3 && bytes[..3].SequenceEqual("ID3"u8))
        {
            if (bytes.Length < 10 || bytes[3] is 0xff or 0 || bytes[4] == 0xff ||
                (bytes[6] | bytes[7] | bytes[8] | bytes[9]) >= 0x80)
                return false;
            var tagSize = (bytes[6] << 21) | (bytes[7] << 14) | (bytes[8] << 7) | bytes[9];
            var footerSize = bytes[3] == 4 && (bytes[5] & 0x10) != 0 ? 10 : 0;
            frameOffset = 10 + tagSize + footerSize;
            if (frameOffset < 10 || frameOffset > bytes.Length) return false;
        }

        return TryGetMpegFrameLength(bytes[frameOffset..], out var frameLength) &&
               frameLength <= bytes.Length - frameOffset;
    }

    private static bool TryGetMpegFrameLength(ReadOnlySpan<byte> bytes, out int frameLength)
    {
        frameLength = 0;
        if (bytes.Length < 4 || bytes[0] != 0xff || (bytes[1] & 0xe0) != 0xe0) return false;
        var versionBits = (bytes[1] >> 3) & 0x03;
        var layerBits = (bytes[1] >> 1) & 0x03;
        var bitrateIndex = (bytes[2] >> 4) & 0x0f;
        var sampleRateIndex = (bytes[2] >> 2) & 0x03;
        if (versionBits == 1 || layerBits == 0 || bitrateIndex is 0 or 15 || sampleRateIndex == 3 ||
            (bytes[3] & 0x03) == 2)
            return false;

        var version1 = versionBits == 3;
        var layer = 4 - layerBits;
        var bitrate = BitrateKbps(version1, layer, bitrateIndex) * 1_000;
        var baseSampleRate = sampleRateIndex switch { 0 => 44_100, 1 => 48_000, _ => 32_000 };
        var sampleRate = versionBits switch { 3 => baseSampleRate, 2 => baseSampleRate / 2, _ => baseSampleRate / 4 };
        var padding = (bytes[2] >> 1) & 1;
        if (bitrate <= 0 || sampleRate <= 0) return false;
        frameLength = layer switch
        {
            1 => ((12 * bitrate / sampleRate) + padding) * 4,
            2 => (144 * bitrate / sampleRate) + padding,
            _ => ((version1 ? 144 : 72) * bitrate / sampleRate) + padding,
        };
        return frameLength >= 4;
    }

    private static int BitrateKbps(bool version1, int layer, int index)
    {
        ReadOnlySpan<int> table = version1
            ? layer switch
            {
                1 => [0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448, 0],
                2 => [0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384, 0],
                _ => [0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0],
            }
            : layer == 1
                ? [0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256, 0]
                : [0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0];
        return table[index];
    }

    private static bool IsStructurallyValidWav(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12 || !bytes[..4].SequenceEqual("RIFF"u8) ||
            !bytes[8..12].SequenceEqual("WAVE"u8))
            return false;
        var declaredEnd = 8L + BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..8]);
        if (declaredEnd < 12 || declaredEnd > bytes.Length) return false;

        var hasValidFormat = false;
        var hasData = false;
        long offset = 12;
        while (offset + 8 <= declaredEnd)
        {
            var chunkOffset = checked((int)offset);
            var chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(chunkOffset + 4, 4));
            var contentStart = offset + 8;
            var contentEnd = contentStart + chunkSize;
            if (contentEnd > declaredEnd || contentEnd > bytes.Length) return false;
            var id = bytes.Slice(chunkOffset, 4);
            if (id.SequenceEqual("fmt "u8))
            {
                if (chunkSize < 16) return false;
                var format = bytes.Slice(checked((int)contentStart), 16);
                hasValidFormat = BinaryPrimitives.ReadUInt16LittleEndian(format) is 1 or 3 &&
                                 BinaryPrimitives.ReadUInt16LittleEndian(format[2..]) > 0 &&
                                 BinaryPrimitives.ReadUInt32LittleEndian(format[4..]) > 0 &&
                                 BinaryPrimitives.ReadUInt32LittleEndian(format[8..]) > 0 &&
                                 BinaryPrimitives.ReadUInt16LittleEndian(format[12..]) > 0 &&
                                 BinaryPrimitives.ReadUInt16LittleEndian(format[14..]) > 0;
            }
            else if (id.SequenceEqual("data"u8))
            {
                hasData = chunkSize > 0;
            }
            offset = contentEnd + (chunkSize & 1);
        }
        return hasValidFormat && hasData && offset <= declaredEnd;
    }
}
