namespace Forge.Domain.Combat;

public sealed record LootDrop
{
    public string ItemSlug { get; }

    public int Quantity { get; }

    public double Chance { get; }

    public LootDrop(string itemSlug, int quantity, double chance = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemSlug);
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);
        if (!double.IsFinite(chance) || chance is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(chance), "Loot chance must be between 0 and 1.");
        }

        ItemSlug = itemSlug.Trim();
        Quantity = quantity;
        Chance = chance;
    }
}
