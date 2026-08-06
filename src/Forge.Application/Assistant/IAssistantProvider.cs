namespace Forge.Application.Assistant;

public interface IAssistantProvider
{
    Task<AssistantProviderResponse> GenerateAsync(
        AssistantProviderRequest request,
        CancellationToken cancellationToken = default);
}
