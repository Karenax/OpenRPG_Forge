namespace Forge.Studio.Core.Navigation;

public sealed record StudioSectionDefinition(
    string Id,
    string Title,
    string ShortTitle,
    IReadOnlyList<StudioPageDefinition> Pages);
