namespace Forge.Application.Gameplay;

public static class RuntimePrimaryResourceSelector
{
    private static readonly string[] PreferredIds = ["focus", "stamina", "resource"];

    public static RuntimeModulePresentation.Resource? Select(
        IReadOnlyList<RuntimeModulePresentation.Resource> resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        foreach (var preferredId in PreferredIds)
        {
            var preferred = resources.FirstOrDefault(resource =>
                string.Equals(resource.Id, preferredId, StringComparison.OrdinalIgnoreCase));
            if (preferred is not null) return preferred;
        }

        return resources.FirstOrDefault(resource =>
            !string.Equals(resource.Id, "health", StringComparison.OrdinalIgnoreCase));
    }
}
