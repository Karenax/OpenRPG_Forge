namespace Forge.Application.Creator;

public sealed class CreatorSelectionContext
{
    public const int CurrentSchemaVersion = 1;
    private CreatorSelection[] _current = [];

    public Guid Id { get; } = Guid.Parse("6f02b7d2-c3be-5bfd-9059-2f8d8d0b9a13");

    public int SchemaVersion => CurrentSchemaVersion;

    public string Provenance { get; private set; } = "creator-selection";

    public IReadOnlyList<CreatorSelection> Current => _current;

    public bool IsLocked { get; private set; }

    public CreatorSelection? Focused { get; private set; }

    public bool IsFramed { get; private set; }

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
        Focused = next.LastOrDefault();
        IsFramed = false;
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
        Focused = null;
        IsFramed = false;
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

    public bool Focus(CreatorSelection selection, string provenance = "creator-selection")
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (!_current.Contains(selection)) return false;
        Focused = selection;
        Provenance = provenance.Trim();
        Changed?.Invoke();
        return true;
    }

    public bool Frame(string provenance = "creator-selection")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (_current.Length == 0) return false;
        IsFramed = true;
        Provenance = provenance.Trim();
        Changed?.Invoke();
        return true;
    }
}
