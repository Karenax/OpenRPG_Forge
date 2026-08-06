using Forge.Domain.Assistant;

namespace Forge.Application.Assistant;

public sealed record LoreIndexState(LoreIndexDefinition Index, LoreIndexRevision Revision)
{
    public LoreIndexState(LoreIndexDefinition? index = null)
        : this(index ?? LoreIndexDefinition.CreateDefault(), new LoreIndexRevision(0))
    {
    }
}
