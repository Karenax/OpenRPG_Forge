using Forge.Domain.Assistant;

namespace Forge.Application.Assistant;

public sealed record AssistedAreaPlanState(AssistedAreaPlanCatalog Catalog, AssistedAreaPlanRevision Revision)
{
    public AssistedAreaPlanState(AssistedAreaPlanCatalog? catalog = null)
        : this(catalog ?? AssistedAreaPlanCatalog.CreateDefault(), new AssistedAreaPlanRevision(0))
    {
    }
}
