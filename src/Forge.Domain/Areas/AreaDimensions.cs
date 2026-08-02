namespace Forge.Domain.Areas;

public sealed record AreaDimensions
{
    public const int MinimumSize = 4;
    public const int MaximumSize = 4096;

    public static AreaDimensions Default { get; } = new(20, 20);

    public int Width { get; }

    public int Depth { get; }

    public AreaDimensions(int width, int depth)
    {
        if (width is < MinimumSize or > MaximumSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                $"Area width must be between {MinimumSize} and {MaximumSize}.");
        }

        if (depth is < MinimumSize or > MaximumSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(depth),
                $"Area depth must be between {MinimumSize} and {MaximumSize}.");
        }

        Width = width;
        Depth = depth;
    }
}
