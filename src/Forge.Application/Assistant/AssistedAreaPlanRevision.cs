namespace Forge.Application.Assistant;

public readonly record struct AssistedAreaPlanRevision(long Value)
{
    public AssistedAreaPlanRevision Next() => new(checked(Value + 1));
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
