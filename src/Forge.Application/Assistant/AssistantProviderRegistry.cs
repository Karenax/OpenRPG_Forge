using Forge.Domain.Assistant;

namespace Forge.Application.Assistant;

public sealed class AssistantProviderRegistry
{
    public const string ProviderUnavailableCode = "assistant.provider.unavailable";
    private readonly IReadOnlyDictionary<AssistantProviderKind, IAssistantProviderFactory> _factories;

    public AssistantProviderRegistry(IEnumerable<IAssistantProviderFactory>? factories = null)
    {
        var materialized = (factories ?? [new DeterministicAssistantProviderFactory()]).ToArray();
        if (materialized.GroupBy(factory => factory.Kind).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Assistant provider factories must be unique by kind.", nameof(factories));
        }
        _factories = materialized.ToDictionary(factory => factory.Kind);
    }

    public IAssistantProvider Resolve(AssistantProviderDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!_factories.TryGetValue(definition.Kind, out var factory))
        {
            throw new InvalidOperationException($"{ProviderUnavailableCode}: provider kind '{definition.Kind}' is not installed.");
        }
        return factory.Create(definition);
    }
}
