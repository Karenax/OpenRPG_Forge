namespace Forge.Application.Areas;

public sealed record AreaChunkPayload(string ResourcePath, ReadOnlyMemory<byte> Content,
    IReadOnlyList<string> Dependencies)
{
    public int SizeBytes => Content.Length;
}
