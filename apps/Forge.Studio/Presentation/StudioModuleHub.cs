using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioModuleHub : Control
{
    private readonly Label _defaultTitle;
    private readonly Label _defaultDetails;
    private readonly Label _status;
    private readonly Button _continueButton;

    internal event Action? ContinueRequested;

    internal event Action? BrowseRequested;

    internal event Action? CreateRequested;

    internal StudioModuleHub()
    {
        Name = "ModuleHub";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var background = new ColorRect
        {
            Color = StudioStyle.Background,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var margin = StudioStyle.Margin(40, 30, 40, 28);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(margin);

        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        content.AddThemeConstantOverride("separation", 10);
        margin.AddChild(content);

        var brand = new HBoxContainer();
        brand.AddThemeConstantOverride("separation", 8);
        content.AddChild(brand);
        var mark = StudioStyle.Panel("BrandMark", StudioStyle.Accent, 10, 0);
        mark.CustomMinimumSize = new Vector2(38, 38);
        var markLabel = StudioStyle.Label("OF", 14, StudioStyle.Background);
        markLabel.HorizontalAlignment = HorizontalAlignment.Center;
        markLabel.VerticalAlignment = VerticalAlignment.Center;
        mark.AddChild(markLabel);
        brand.AddChild(mark);
        var brandText = new VBoxContainer();
        brandText.AddChild(StudioStyle.Label("OpenRPG Forge Studio", 18));
        brandText.AddChild(StudioStyle.Label("L'atelier autonome pour retoucher vos modules", 11, StudioStyle.Muted));
        brand.AddChild(brandText);

        content.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
        content.AddChild(StudioStyle.Label("Que voulez-vous retoucher ?", 27));
        content.AddChild(StudioStyle.Label(
            "Reprenez directement un monde existant. La création, la 3D, les dialogues et les tests " +
            "restent réunis dans une seule application.",
            13,
            StudioStyle.Muted,
            wrap: true));

        var cards = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        cards.AddThemeConstantOverride("separation", 10);
        content.AddChild(cards);

        var continueCard = CreateCard(out var continueColumn);
        continueCard.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        continueCard.SizeFlagsStretchRatio = 1.35f;
        cards.AddChild(continueCard);
        continueColumn.AddChild(StudioStyle.SectionLabel("CONTINUER"));
        _defaultTitle = StudioStyle.Label("Forgotten Forest", 20);
        continueColumn.AddChild(_defaultTitle);
        _defaultDetails = StudioStyle.Label("Vérification du module…", 11, StudioStyle.Muted, wrap: true);
        continueColumn.AddChild(_defaultDetails);
        continueColumn.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        _continueButton = StudioStyle.Button("Ouvrir dans le Studio", primary: true);
        _continueButton.Pressed += () => ContinueRequested?.Invoke();
        continueColumn.AddChild(_continueButton);

        var otherCard = CreateCard(out var otherColumn);
        otherCard.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        cards.AddChild(otherCard);
        otherColumn.AddChild(StudioStyle.SectionLabel("AUTRE MODULE"));
        otherColumn.AddChild(StudioStyle.Label("Ouvrir un workspace Forge", 18));
        otherColumn.AddChild(StudioStyle.Label(
            "Choisissez le dossier qui contient forge.project.json. Le Studio le contrôle avant de " +
            "l'afficher et ne modifie rien pendant l'ouverture.",
            11,
            StudioStyle.Muted,
            wrap: true));
        otherColumn.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        var browse = StudioStyle.Button("Choisir un dossier…");
        browse.Pressed += () => BrowseRequested?.Invoke();
        otherColumn.AddChild(browse);
        var create = StudioStyle.Button("Créer un nouveau module");
        create.TooltipText = "Ouvrir l’assistant en cinq étapes dans cette même fenêtre.";
        create.Pressed += () => CreateRequested?.Invoke();
        otherColumn.AddChild(create);

        _status = StudioStyle.Label(string.Empty, 11, StudioStyle.Warning, wrap: true);
        _status.Visible = false;
        content.AddChild(_status);

        var footer = new HBoxContainer();
        footer.AddChild(StudioStyle.Label(
            "Application autonome · Godot 4.7.1 .NET · aucun chrome de l'éditeur Godot",
            10,
            StudioStyle.Muted));
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        footer.AddChild(StudioStyle.Label("Migration progressive · données préservées", 10, StudioStyle.Success));
        content.AddChild(footer);
    }

    internal void SetDefaultModule(string title, string details, bool available)
    {
        _defaultTitle.Text = title;
        _defaultDetails.Text = details;
        _continueButton.Disabled = !available;
    }

    internal void ShowStatus(string text, bool error)
    {
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Success);
        _status.Visible = !string.IsNullOrWhiteSpace(text);
    }

    private static PanelContainer CreateCard(out VBoxContainer content)
    {
        var card = StudioStyle.Panel("ModuleCard", StudioStyle.Surface, 12);
        card.CustomMinimumSize = new Vector2(0, 250);
        var margin = StudioStyle.Margin(14, 12, 14, 12);
        card.AddChild(margin);
        content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 7);
        margin.AddChild(content);
        return card;
    }
}
