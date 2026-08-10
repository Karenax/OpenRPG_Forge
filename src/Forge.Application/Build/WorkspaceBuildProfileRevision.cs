namespace Forge.Application.Build;

public readonly record struct WorkspaceBuildProfileRevision(long Value)
{
    public WorkspaceBuildProfileRevision Next() => new(checked(Value + 1));
}
