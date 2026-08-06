using Forge.Domain.Assistant;

namespace Forge.Application.Assistant;

public sealed class OpenAiCompatibleAssistantProviderFactory : IAssistantProviderFactory
{
    private readonly HttpClient _httpClient;
    private readonly Func<string, string?> _readEnvironmentVariable;
    private readonly TimeSpan _requestTimeout;

    public OpenAiCompatibleAssistantProviderFactory(
        HttpClient httpClient,
        Func<string, string?> readEnvironmentVariable,
        TimeSpan requestTimeout)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _readEnvironmentVariable = readEnvironmentVariable ?? throw new ArgumentNullException(nameof(readEnvironmentVariable));
        if (requestTimeout <= TimeSpan.Zero || requestTimeout > TimeSpan.FromMinutes(5))
        {
            throw new ArgumentOutOfRangeException(nameof(requestTimeout));
        }
        _requestTimeout = requestTimeout;
    }

    public AssistantProviderKind Kind => AssistantProviderKind.OpenAiCompatible;

    public IAssistantProvider Create(AssistantProviderDefinition definition) =>
        new OpenAiCompatibleAssistantProvider(
            definition,
            _httpClient,
            _readEnvironmentVariable,
            _requestTimeout);
}
