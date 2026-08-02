namespace Forge.Domain.Navigation;

public readonly record struct NavigationCell(int X, int Z) : IComparable<NavigationCell>
{
    public int CompareTo(NavigationCell other)
    {
        var z = Z.CompareTo(other.Z);
        return z != 0 ? z : X.CompareTo(other.X);
    }
}
