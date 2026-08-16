namespace Forge.Studio.Core.Navigation;

public sealed record StudioPageDefinition(
    string Id,
    string Title,
    string Subtitle,
    StudioPageLayout Layout,
    string PrimaryAction,
    string LeftPanelTitle,
    string CenterTitle,
    string RightPanelTitle);
