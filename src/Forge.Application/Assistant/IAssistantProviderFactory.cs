using Forge.Domain.Assistant;

namespace Forge.Application.Assistant;

public interface IAssistantProviderFactory
{
    AssistantProviderKind Kind { get; }
    IAssistantProvider Create(AssistantProviderDefinition definition);
}
