namespace Forge.Application.Gameplay;

public sealed class RuntimeInventoryState
{
    private readonly SortedDictionary<string, int> _authoredBaseline = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, int> _quantities = new(StringComparer.Ordinal);

    public void SetAuthoredBaseline(IEnumerable<RuntimeInventoryStack> stacks)
    {
        ArgumentNullException.ThrowIfNull(stacks);
        var replacement = Aggregate(stacks);
        _authoredBaseline.Clear();
        foreach (var item in replacement) _authoredBaseline.Add(item.Key, item.Value);
    }

    public void Grant(string slug, int quantity = 1)
    {
        var stack = new RuntimeInventoryStack(slug, quantity);
        _quantities[stack.Slug] = checked(_quantities.GetValueOrDefault(stack.Slug) + stack.Quantity);
    }

    public bool HasItem(string slug) =>
        !string.IsNullOrWhiteSpace(slug) &&
        (_authoredBaseline.GetValueOrDefault(slug.Trim()) > 0 ||
         _quantities.GetValueOrDefault(slug.Trim()) > 0);

    public IReadOnlyList<RuntimeInventoryStack> Snapshot() =>
        _quantities.Select(item => new RuntimeInventoryStack(item.Key, item.Value)).ToArray();

    public void Replace(IEnumerable<RuntimeInventoryStack> stacks)
    {
        ArgumentNullException.ThrowIfNull(stacks);
        var replacement = Aggregate(stacks);
        _quantities.Clear();
        foreach (var item in replacement) _quantities.Add(item.Key, item.Value);
    }

    private static SortedDictionary<string, int> Aggregate(IEnumerable<RuntimeInventoryStack> stacks)
    {
        var result = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var stack in stacks)
        {
            ArgumentNullException.ThrowIfNull(stack);
            result[stack.Slug] = checked(result.GetValueOrDefault(stack.Slug) + stack.Quantity);
        }

        return result;
    }
}
