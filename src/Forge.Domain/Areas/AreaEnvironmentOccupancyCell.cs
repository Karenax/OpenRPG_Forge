namespace Forge.Domain.Areas;

public sealed record AreaEnvironmentOccupancyCell
{
    public string Category { get; }
    public string Role { get; }
    public float X { get; }
    public float Z { get; }
    public int Count { get; }

    public AreaEnvironmentOccupancyCell(string category, string role, float x, float z, int count)
    {
        Category = (category ?? string.Empty).Trim().ToLowerInvariant();
        Role = (role ?? string.Empty).Trim().ToLowerInvariant();
        X = x;
        Z = z;
        Count = count;
    }
}
