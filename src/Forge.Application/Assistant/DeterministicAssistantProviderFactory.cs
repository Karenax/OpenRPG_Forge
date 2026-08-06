using Forge.Domain.Assistant;

namespace Forge.Application.Assistant;

public sealed class DeterministicAssistantProviderFactory : IAssistantProviderFactory
{
    public AssistantProviderKind Kind => AssistantProviderKind.DeterministicOffline;

    public IAssistantProvider Create(AssistantProviderDefinition definition) =>
        new DeterministicAssistantProvider(definition);
}
