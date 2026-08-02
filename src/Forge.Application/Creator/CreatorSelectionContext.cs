namespace Forge.Application.Creator;

public sealed class CreatorSelectionContext
{
    private CreatorSelection[] _current = [];

    public IReadOnlyList<CreatorSelection> Current => _current;

    public bool IsLocked { get; private set; }

    public event Action? Changed;

    public bool Replace(CreatorSelection selection) => Replace([selection]);

    public bool Replace(IEnumerable<CreatorSelection> selections)
    {
        ArgumentNullException.ThrowIfNull(selections);
        if (IsLocked)
        {
            return false;
        }

        var next = selections
            .DistinctBy(selection => (selection.Kind, selection.Id))
            .ToArray();
        if (_current.SequenceEqual(next))
        {
            return false;
        }

        _current = next;
        Changed?.Invoke();
        return true;
    }

    public bool Clear()
    {
        if (IsLocked || _current.Length == 0)
        {
            return false;
        }

        _current = [];
        Changed?.Invoke();
        return true;
    }

    public void SetLocked(bool locked)
    {
        if (IsLocked == locked)
        {
            return;
        }

        IsLocked = locked;
        Changed?.Invoke();
    }
}
