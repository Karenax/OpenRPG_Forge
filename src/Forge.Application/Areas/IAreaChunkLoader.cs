namespace Forge.Application.Areas;

public interface IAreaChunkLoader
{
    Task<AreaChunkPayload> LoadAsync(string resourcePath, CancellationToken cancellationToken);
}
