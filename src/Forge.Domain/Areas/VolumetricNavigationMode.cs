namespace Forge.Domain.Areas;

[Flags]
public enum VolumetricNavigationMode
{
    None = 0,
    Flight = 1,
    Swim = 2,
    Space = 4,
}
