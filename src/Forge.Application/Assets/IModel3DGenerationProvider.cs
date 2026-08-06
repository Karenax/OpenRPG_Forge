namespace Forge.Application.Assets;

public interface IModel3DGenerationProvider
{
    Task<Model3DProviderResponse> GenerateAsync(
        Model3DProviderRequest request,
        CancellationToken cancellationToken = default);
}
