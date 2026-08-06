namespace Forge.Application.Assets;

public readonly record struct ExternalWorkerSecurityPolicyRevision
{
    public long Value { get; }

    public ExternalWorkerSecurityPolicyRevision(long value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }

    public ExternalWorkerSecurityPolicyRevision Next() => new(checked(Value + 1));
}
