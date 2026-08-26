namespace Forge.Application.Gameplay;

public sealed record RuntimeInventoryStack
{
    public string Slug { get; }

    public int Quantity { get; }

    public RuntimeInventoryStack(string slug, int quantity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);
        Slug = slug.Trim();
        Quantity = quantity;
    }
}
