using Forge.Application.Gameplay;
using Godot;

namespace Forge.Runtime;

public enum RuntimeRpgMenuPage
{
    Inventory,
    Character,
    Journal,
}

/// <summary>
/// Player-facing RPG codex. It renders the read-only application projection of the loaded workspace.
/// </summary>
public partial class RuntimeRpgMenu : CanvasLayer
{
    private readonly Dictionary<RuntimeRpgMenuPage, Control> _pages = [];
    private readonly Dictionary<RuntimeRpgMenuPage, Button> _navButtons = [];
    private Control? _root;
    private Label? _journalTitle;
    private Label? _journalDescription;
    private Label? _journalActiveObjective;
    private Label? _journalSituation;
    private Label? _itemName;
    private Label? _itemKind;
    private Label? _itemStat;
    private Label? _itemDescription;
    private RuntimeRpgMenuPage _currentPage = RuntimeRpgMenuPage.Inventory;

    public string ModuleTitle { get; init; } = "OpenRPG Forge";

    public string AreaTitle { get; init; } = "Terres connues";

    public RuntimeModulePresentation? Presentation { get; set; }

    public bool IsOpen => _root?.Visible == true;

    public RuntimeRpgMenuPage CurrentPage => _currentPage;

    public event Action? MapRequested;

    public event Action? Closed;

    public override void _Ready()
    {
        if (Presentation is null)
            throw new InvalidOperationException("The RPG menu requires a module presentation.");
        Layer = 40;
        BuildShell();
        ShowPage(RuntimeRpgMenuPage.Inventory, false);
        Close();
    }

    public void Toggle(RuntimeRpgMenuPage page)
    {
        if (IsOpen && _currentPage == page) Close();
        else ShowPage(page);
    }

    public void ShowPage(RuntimeRpgMenuPage page, bool focus = true)
    {
        _currentPage = page;
        if (_root is not null) _root.Visible = true;
        foreach (var pair in _pages) pair.Value.Visible = pair.Key == page;
        foreach (var pair in _navButtons) ApplyNavState(pair.Value, pair.Key == page);
        if (focus && _navButtons.TryGetValue(page, out var button)) button.GrabFocus();
    }

    public void Close()
    {
        if (_root is not null) _root.Visible = false;
        Closed?.Invoke();
    }

    public void UpdateContext(string? objective, string? situation)
    {
        if (_journalActiveObjective is not null)
            _journalActiveObjective.Text = string.IsNullOrWhiteSpace(objective)
                ? "Aucun objectif actif"
                : objective;
        if (_journalSituation is not null)
            _journalSituation.Text = string.IsNullOrWhiteSpace(situation)
                ? "Aucune situation active n'est renseignée."
                : situation;
    }

    public void UpdatePresentation(RuntimeModulePresentation presentation)
    {
        Presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
        if (_root is null) return;
        var wasOpen = IsOpen;
        var page = _currentPage;
        _root.Free();
        _root = null;
        _pages.Clear();
        _navButtons.Clear();
        _journalTitle = null;
        _journalDescription = null;
        _journalActiveObjective = null;
        _journalSituation = null;
        _itemName = null;
        _itemKind = null;
        _itemStat = null;
        _itemDescription = null;
        BuildShell();
        ShowPage(page, false);
        if (!wasOpen && _root is not null) _root.Visible = false;
    }

    private void BuildShell()
    {
        _root = new Control { Name = "RpgCodex", MouseFilter = Control.MouseFilterEnum.Stop };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        var backdrop = new MenuBackdrop { MouseFilter = Control.MouseFilterEnum.Stop };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(backdrop);
        BuildTopNavigation(_root);

        var body = new Control
        {
            AnchorLeft = .028f,
            AnchorRight = .972f,
            AnchorTop = .105f,
            AnchorBottom = .94f,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _root.AddChild(body);

        AddPage(body, RuntimeRpgMenuPage.Inventory, "InventoryPage", BuildInventoryPage);
        AddPage(body, RuntimeRpgMenuPage.Character, "CharacterPage", BuildCharacterPage);
        AddPage(body, RuntimeRpgMenuPage.Journal, "JournalPage", BuildJournalPage);
        BuildFooter(_root);
    }

    private void AddPage(Control body, RuntimeRpgMenuPage page, string name, Action<Control> build)
    {
        var control = new Control { Name = name };
        control.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        body.AddChild(control);
        _pages[page] = control;
        build(control);
    }

    private void BuildTopNavigation(Control root)
    {
        var topShade = new ColorRect
        {
            Color = new Color("120d09f4"),
            AnchorRight = 1,
            OffsetBottom = 92,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        root.AddChild(topShade);

        var top = new HBoxContainer
        {
            AnchorRight = 1,
            OffsetLeft = 34,
            OffsetTop = 17,
            OffsetRight = -34,
            OffsetBottom = 76,
        };
        top.AddThemeConstantOverride("separation", 16);
        root.AddChild(top);

        var brand = Stack(1);
        brand.CustomMinimumSize = new Vector2(315, 0);
        brand.AddChild(Overline(ModuleTitle, RuntimeRpgUiTheme.Player));
        brand.AddChild(Title(AreaTitle, 15, RuntimeRpgUiTheme.Parchment));
        top.AddChild(brand);

        var navCenter = new CenterContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        top.AddChild(navCenter);
        var nav = new HBoxContainer();
        nav.AddThemeConstantOverride("separation", 6);
        navCenter.AddChild(nav);
        AddPageNav(nav, RuntimeRpgMenuPage.Inventory, "INVENTAIRE", "I");
        AddPageNav(nav, RuntimeRpgMenuPage.Character, "PERSONNAGE", "C");
        AddPageNav(nav, RuntimeRpgMenuPage.Journal, "JOURNAL", "J");
        var map = NavButton("CARTE", "M");
        map.Pressed += () => MapRequested?.Invoke();
        nav.AddChild(map);

        var resources = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(315, 0),
            Alignment = BoxContainer.AlignmentMode.End,
        };
        resources.AddThemeConstantOverride("separation", 10);
        top.AddChild(resources);
        if (Presentation!.Inventory.Currencies.Count == 0)
            resources.AddChild(Body("AUCUNE MONNAIE", 9, RuntimeRpgUiTheme.Quiet));
        else
            foreach (var currency in Presentation.Inventory.Currencies)
                resources.AddChild(Body($"◆ {currency.Amount:0.##} {currency.DisplayName}", 10,
                    RuntimeRpgUiTheme.Gold));
        var close = new Button { Text = "ESC  FERMER", CustomMinimumSize = new Vector2(104, 36) };
        RuntimeRpgUiTheme.StyleButton(close, quiet: true);
        close.Pressed += Close;
        resources.AddChild(close);
    }

    private static void BuildFooter(Control root)
    {
        var footer = Panel(new Color("17100cdb"), RuntimeRpgUiTheme.BorderMuted, 10, 4);
        footer.AnchorLeft = .028f;
        footer.AnchorRight = .972f;
        footer.AnchorTop = 1;
        footer.AnchorBottom = 1;
        footer.OffsetTop = -38;
        footer.OffsetBottom = -8;
        root.AddChild(footer);
        var hint = Body("I  SAC     ·     C  PERSONNAGE     ·     J  QUÊTES     ·     M  CARTE     ·     ÉCHAP  RETOUR AU JEU",
            8, RuntimeRpgUiTheme.Muted);
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        footer.AddChild(hint);
    }

    private void BuildInventoryPage(Control page)
    {
        var row = FullRow(page);
        row.AddThemeConstantOverride("separation", 14);

        var sidePanel = Panel(new Color("21170ff2"), RuntimeRpgUiTheme.BorderMuted, 18, 17);
        sidePanel.CustomMinimumSize = new Vector2(280, 0);
        row.AddChild(sidePanel);
        var side = Stack(10);
        sidePanel.AddChild(side);
        side.AddChild(Overline("GROUPE", RuntimeRpgUiTheme.Player));
        side.AddChild(Title("Inventaire", 29, RuntimeRpgUiTheme.Parchment));
        side.AddChild(BuildActorCard(Presentation!.Hero, RuntimeRpgUiTheme.Player));
        if (Presentation.Party.Count == 0)
            side.AddChild(EmptyState("Aucun compagnon dans le groupe."));
        else
            foreach (var member in Presentation.Party)
                side.AddChild(BuildActorCard(member, RuntimeRpgUiTheme.Green));
        side.AddChild(Divider());
        var weightPercent = Presentation.Inventory.MaximumWeight <= 0
            ? 0
            : Presentation.Inventory.Weight * 100 / Presentation.Inventory.MaximumWeight;
        side.AddChild(Overline("CHARGE", RuntimeRpgUiTheme.Quiet));
        side.AddChild(Progress(weightPercent, RuntimeRpgUiTheme.Player, 7));
        side.AddChild(Body(Presentation.Inventory.MaximumWeight <= 0
            ? "Capacité non renseignée"
            : $"{Presentation.Inventory.Weight:0.##} / {Presentation.Inventory.MaximumWeight:0.##}",
            9, RuntimeRpgUiTheme.Muted));

        var centerPanel = Panel(new Color("19110de8"), RuntimeRpgUiTheme.BorderMuted, 18, 17);
        centerPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(centerPanel);
        var center = Stack(12);
        centerPanel.AddChild(center);
        var header = new HBoxContainer();
        header.AddChild(Title("Objets du personnage", 25, RuntimeRpgUiTheme.Parchment));
        var countText = Presentation.Inventory.Capacity <= 0
            ? $"{Presentation.Inventory.Items.Count} OBJET(S)"
            : $"{Presentation.Inventory.OccupiedSlots} / {Presentation.Inventory.Capacity} EMPLACEMENTS";
        var count = Body(countText, 9, RuntimeRpgUiTheme.Muted);
        count.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        count.HorizontalAlignment = HorizontalAlignment.Right;
        header.AddChild(count);
        center.AddChild(header);

        var grid = new GridContainer { Columns = 5, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);
        center.AddChild(grid);
        foreach (var item in Presentation.Inventory.Items)
        {
            var cell = BuildItemCell(item);
            cell.Pressed += () => SelectItem(item);
            grid.AddChild(cell);
        }
        if (Presentation.Inventory.Items.Count == 0)
            center.AddChild(EmptyState("Inventaire vide — aucun objet n'est défini pour ce personnage."));
        for (var index = Presentation.Inventory.Items.Count; index < 20; index++)
            grid.AddChild(BuildEmptyItemCell());

        var summary = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        summary.AddThemeConstantOverride("separation", 8);
        summary.AddChild(CompactMetric("ÉQUIPÉS", Presentation.Inventory.Equipment.Count.ToString()));
        summary.AddChild(CompactMetric("OBJETS", Presentation.Inventory.Items.Sum(item => item.Quantity).ToString()));
        summary.AddChild(CompactMetric("MONNAIES", Presentation.Inventory.Currencies.Count.ToString()));
        center.AddChild(summary);

        var detailPanel = Panel(new Color("2a1d15f4"), RuntimeRpgUiTheme.Border, 20, 18);
        detailPanel.CustomMinimumSize = new Vector2(340, 0);
        row.AddChild(detailPanel);
        var detail = Stack(10);
        detailPanel.AddChild(detail);
        var selected = Presentation.Inventory.Items.FirstOrDefault();
        _itemKind = Overline(selected?.Type ?? "INVENTAIRE VIDE", RuntimeRpgUiTheme.Gold);
        _itemName = Title(selected?.Name ?? "Aucun objet sélectionné", 29, RuntimeRpgUiTheme.Parchment, true);
        _itemStat = Title(selected is null ? "AUCUNE DONNÉE" :
            $"QUANTITÉ {selected.Quantity} · VALEUR {selected.UnitValue:0.##}", 16, RuntimeRpgUiTheme.Gold);
        _itemDescription = Body(selected?.Description ?? "Aucun objet n'est présent dans le workspace.", 11,
            RuntimeRpgUiTheme.Muted, true);
        detail.AddChild(_itemKind);
        detail.AddChild(_itemName);
        var glyph = Title(selected is null ? "◇" : ItemGlyph(selected), 70, RuntimeRpgUiTheme.Gold);
        glyph.CustomMinimumSize = new Vector2(0, 140);
        glyph.HorizontalAlignment = HorizontalAlignment.Center;
        glyph.VerticalAlignment = VerticalAlignment.Center;
        glyph.AddThemeStyleboxOverride("normal", RuntimeRpgUiTheme.Inset());
        detail.AddChild(glyph);
        detail.AddChild(_itemStat);
        detail.AddChild(Divider());
        detail.AddChild(_itemDescription);
        detail.AddChild(Divider());
        var equipmentText = Presentation.Inventory.Equipment.Count == 0
            ? "Aucun équipement assigné."
            : string.Join('\n', Presentation.Inventory.Equipment.Select(item =>
                $"{item.Slot.ToUpperInvariant()} : {item.Item.Name}"));
        detail.AddChild(Body(equipmentText, 9, RuntimeRpgUiTheme.Muted, true));
    }

    private void BuildCharacterPage(Control page)
    {
        var row = FullRow(page);
        row.AddThemeConstantOverride("separation", 14);
        var hero = Presentation!.Hero;

        var identityPanel = Panel(new Color("21170ff2"), RuntimeRpgUiTheme.BorderMuted, 22, 20);
        identityPanel.CustomMinimumSize = new Vector2(360, 0);
        row.AddChild(identityPanel);
        var identity = Stack(12);
        identityPanel.AddChild(identity);
        var portrait = RuntimeRpgUiTheme.Portrait(hero.PortraitPath, 108, RuntimeRpgUiTheme.Gold,
            selected: true, fallbackText: Initials(hero.DisplayName));
        portrait.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        portrait.SetMeta("accessible_label", hero.PortraitAltText);
        identity.AddChild(portrait);
        identity.AddChild(Overline("PERSONNAGE ACTIF", RuntimeRpgUiTheme.Player));
        identity.AddChild(Title(hero.DisplayName, 34, RuntimeRpgUiTheme.Parchment));
        identity.AddChild(Title(hero.Role ?? "Rôle non renseigné", 15, RuntimeRpgUiTheme.Gold));
        identity.AddChild(Body(Presentation.Description ?? "Aucune description de module renseignée.", 10,
            RuntimeRpgUiTheme.Muted, true));
        identity.AddChild(Divider());
        identity.AddChild(Overline("RESSOURCES", RuntimeRpgUiTheme.Muted));
        if (hero.Resources.Count == 0)
            identity.AddChild(EmptyState("Aucune ressource définie."));
        else
            foreach (var resource in hero.Resources)
                identity.AddChild(Metric(resource.Id.ToUpperInvariant(),
                    $"{resource.Current:0.##} / {resource.Maximum:0.##}", RuntimeRpgUiTheme.Player));

        var attributesPanel = Panel(new Color("19110de6"), RuntimeRpgUiTheme.BorderMuted, 18, 18);
        attributesPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(attributesPanel);
        var attributes = Stack(11);
        attributesPanel.AddChild(attributes);
        attributes.AddChild(Title("Attributs et équipement", 27, RuntimeRpgUiTheme.Parchment));
        if (Presentation.Progression.Attributes.Count == 0)
            attributes.AddChild(EmptyState("Aucun attribut de règles n'est défini."));
        else
        {
            var grid = new GridContainer { Columns = 2 };
            grid.AddThemeConstantOverride("h_separation", 8);
            grid.AddThemeConstantOverride("v_separation", 8);
            attributes.AddChild(grid);
            foreach (var attribute in Presentation.Progression.Attributes)
                grid.AddChild(StatCard(attribute.DisplayName.ToUpperInvariant(), attribute.Value.ToString("0.##"),
                    RuntimeRpgUiTheme.Player));
        }
        attributes.AddChild(Divider());
        attributes.AddChild(Overline("ÉQUIPEMENT", RuntimeRpgUiTheme.Muted));
        if (Presentation.Inventory.Equipment.Count == 0)
            attributes.AddChild(EmptyState("Aucun équipement assigné."));
        else
            foreach (var equipment in Presentation.Inventory.Equipment)
                attributes.AddChild(Metric(equipment.Slot.ToUpperInvariant(), equipment.Item.Name,
                    RuntimeRpgUiTheme.Gold));

        var progressionPanel = Panel(new Color("2a1d15f2"), RuntimeRpgUiTheme.Border, 20, 20);
        progressionPanel.CustomMinimumSize = new Vector2(390, 0);
        row.AddChild(progressionPanel);
        var progression = Stack(13);
        progressionPanel.AddChild(progression);
        progression.AddChild(Overline("PROGRESSION", RuntimeRpgUiTheme.Gold));
        progression.AddChild(Title(Presentation.Progression.Level is null
            ? "Non renseignée"
            : $"Niveau {Presentation.Progression.Level}", 29, RuntimeRpgUiTheme.Parchment));
        if (Presentation.Progression.Tracks.Count == 0 &&
            Presentation.Progression.Skills.Count == 0 &&
            Presentation.Progression.Talents.Count == 0)
        {
            progression.AddChild(EmptyState("Aucune progression, compétence ou talent n'est défini."));
        }
        else
        {
            foreach (var track in Presentation.Progression.Tracks)
                progression.AddChild(Discipline(track.DisplayName,
                    $"Niveau {track.Level}/{track.MaximumLevel}", TrackProgress(track), RuntimeRpgUiTheme.Player));
            foreach (var skill in Presentation.Progression.Skills)
                progression.AddChild(Metric(skill.DisplayName, FormatNamedValue(skill), RuntimeRpgUiTheme.Green));
            foreach (var talent in Presentation.Progression.Talents)
                progression.AddChild(Metric(talent.DisplayName, FormatNamedValue(talent), RuntimeRpgUiTheme.Violet));
        }
    }

    private void BuildJournalPage(Control page)
    {
        var row = FullRow(page);
        row.AddThemeConstantOverride("separation", 14);
        var journal = Presentation!.Journal;

        var listPanel = Panel(new Color("21170ff2"), RuntimeRpgUiTheme.BorderMuted, 18, 18);
        listPanel.CustomMinimumSize = new Vector2(420, 0);
        row.AddChild(listPanel);
        var list = Stack(11);
        listPanel.AddChild(list);
        list.AddChild(Overline("CHRONIQUE", RuntimeRpgUiTheme.Player));
        list.AddChild(Title("Journal", 31, RuntimeRpgUiTheme.Parchment));
        list.AddChild(Body($"ACTIVES {journal.ActiveCount}   ·   TERMINÉES {journal.TerminalCount}   ·   TOTAL {journal.TotalCount}",
            9, RuntimeRpgUiTheme.Muted));
        if (journal.Entries.Count == 0)
            list.AddChild(EmptyState("Journal vide — aucune quête n'est définie dans ce module."));
        else
            foreach (var entry in journal.Entries)
                list.AddChild(QuestCard(entry));

        var articlePanel = Panel(new Color("19110dee"), RuntimeRpgUiTheme.Border, 32, 26);
        articlePanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(articlePanel);
        var article = Stack(13);
        articlePanel.AddChild(article);
        var selected = journal.ActiveEntry;
        article.AddChild(Overline(selected is null ? "AUCUNE QUÊTE SUIVIE" : selected.Category,
            RuntimeRpgUiTheme.Gold));
        _journalTitle = Title(selected?.Title ?? "Aucune quête suivie", 38,
            RuntimeRpgUiTheme.Parchment, true);
        _journalTitle.Name = "JournalQuestTitle";
        article.AddChild(_journalTitle);
        _journalDescription = Body(
            selected?.Description ?? "Acceptez puis suivez une quête pour afficher son journal.",
            11, RuntimeRpgUiTheme.Muted, true);
        _journalDescription.Name = "JournalQuestDescription";
        article.AddChild(_journalDescription);
        article.AddChild(Divider());
        article.AddChild(Overline("SITUATION", RuntimeRpgUiTheme.Player));
        _journalSituation = Body(selected?.Details ?? "Aucune situation active n'est renseignée.", 12,
            RuntimeRpgUiTheme.Parchment, true);
        _journalSituation.Name = "JournalQuestSituation";
        article.AddChild(_journalSituation);
        article.AddChild(Divider());
        article.AddChild(Overline("OBJECTIF ACTIF", RuntimeRpgUiTheme.Muted));
        _journalActiveObjective = Body(journal.ActiveObjective ?? "Aucun objectif actif", 12,
            RuntimeRpgUiTheme.Parchment, true);
        _journalActiveObjective.Name = "JournalActiveObjective";
        article.AddChild(_journalActiveObjective);
        article.AddChild(Divider());
        article.AddChild(Overline("ÉTAPES", RuntimeRpgUiTheme.Muted));
        if (selected is null || selected.Objectives.Count == 0)
            article.AddChild(EmptyState("Aucune étape n'est définie pour cette entrée."));
        else
            foreach (var objective in selected.Objectives)
                article.AddChild(JournalStep(objective, selected.IsTerminal));
    }

    private void SelectItem(RuntimeModulePresentation.InventoryItem item)
    {
        if (_itemName is not null) _itemName.Text = item.Name;
        if (_itemKind is not null) _itemKind.Text = item.Type.ToUpperInvariant();
        if (_itemStat is not null) _itemStat.Text = $"QUANTITÉ {item.Quantity} · VALEUR {item.UnitValue:0.##}";
        if (_itemDescription is not null) _itemDescription.Text = item.Description;
    }

    private void AddPageNav(HBoxContainer parent, RuntimeRpgMenuPage page, string text, string key)
    {
        var button = NavButton(text, key);
        button.Pressed += () => ShowPage(page);
        _navButtons[page] = button;
        parent.AddChild(button);
    }

    private static Button NavButton(string text, string key)
    {
        var button = new Button
        {
            Text = $"{text}   {key}",
            CustomMinimumSize = new Vector2(128, 38),
            FocusMode = Control.FocusModeEnum.All,
        };
        RuntimeRpgUiTheme.StyleButton(button, quiet: true);
        button.AddThemeFontSizeOverride("font_size", 10);
        return button;
    }

    private static void ApplyNavState(Button button, bool active)
    {
        button.AddThemeColorOverride("font_color", active ? RuntimeRpgUiTheme.Bone : RuntimeRpgUiTheme.Muted);
        button.AddThemeStyleboxOverride("normal", RuntimeRpgUiTheme.ButtonStyle(
            active ? new Color("3b291ee8") : new Color("17110eac"),
            active ? RuntimeRpgUiTheme.Gold : RuntimeRpgUiTheme.BorderMuted));
    }

    private static PanelContainer BuildActorCard(RuntimeModulePresentation.Actor actor, Color color)
    {
        var panel = Panel(new Color("241a14d1"), RuntimeRpgUiTheme.BorderMuted, 9, 7);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 9);
        panel.AddChild(row);
        var avatar = RuntimeRpgUiTheme.Portrait(actor.PortraitPath, 38, color,
            fallbackText: Initials(actor.DisplayName));
        avatar.SetMeta("accessible_label", actor.PortraitAltText);
        row.AddChild(avatar);
        var copy = Stack(1);
        copy.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        copy.AddChild(Body(actor.DisplayName, 10, RuntimeRpgUiTheme.Parchment));
        copy.AddChild(Body((actor.Role ?? "Rôle non renseigné").ToUpperInvariant(), 8, color));
        row.AddChild(copy);
        return panel;
    }

    private static Button BuildItemCell(RuntimeModulePresentation.InventoryItem item)
    {
        var button = new Button
        {
            Text = ItemGlyph(item),
            TooltipText = $"{item.Name}\n{item.Type} · quantité {item.Quantity}",
            CustomMinimumSize = new Vector2(92, 96),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.All,
        };
        RuntimeRpgUiTheme.StyleButton(button, quiet: true);
        button.AddThemeFontOverride("font", RuntimeRpgUiTheme.TitleFont);
        button.AddThemeFontSizeOverride("font_size", 27);
        button.AddThemeColorOverride("font_color", RuntimeRpgUiTheme.Gold);
        return button;
    }

    private static Button BuildEmptyItemCell()
    {
        var button = new Button
        {
            Disabled = true,
            CustomMinimumSize = new Vector2(92, 96),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        button.AddThemeStyleboxOverride("disabled", RuntimeRpgUiTheme.Frame(
            new Color("130f0ca6"), new Color("9b794f28"), 0, 0));
        return button;
    }

    private static PanelContainer QuestCard(RuntimeModulePresentation.QuestEntry entry)
    {
        var panel = Panel(entry.IsActive ? new Color("3a281edf") : new Color("1d1611d2"),
            entry.IsActive ? RuntimeRpgUiTheme.Gold : RuntimeRpgUiTheme.BorderMuted, 13, 11);
        var stack = Stack(4);
        panel.AddChild(stack);
        stack.AddChild(Overline(entry.Category, entry.IsActive ? RuntimeRpgUiTheme.Gold : RuntimeRpgUiTheme.Quiet));
        stack.AddChild(Title(entry.Title, 16, RuntimeRpgUiTheme.Parchment, true));
        stack.AddChild(Body(entry.State.ToUpperInvariant(), 8, RuntimeRpgUiTheme.Muted));
        return panel;
    }

    private static Control JournalStep(string objective, bool done)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        row.AddChild(Title(done ? "◆" : "◇", 15,
            done ? RuntimeRpgUiTheme.Gold : RuntimeRpgUiTheme.Quiet));
        var copy = Body(objective, 11, done ? RuntimeRpgUiTheme.Muted : RuntimeRpgUiTheme.Parchment, true);
        copy.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(copy);
        return row;
    }

    private static PanelContainer StatCard(string name, string value, Color color)
    {
        var panel = Panel(new Color("241a14d8"), color.Darkened(.38f), 12, 10);
        var row = new HBoxContainer();
        panel.AddChild(row);
        var label = Body(name, 9, RuntimeRpgUiTheme.Muted);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(label);
        row.AddChild(Title(value, 22, color));
        return panel;
    }

    private static Control Metric(string caption, string value, Color color)
    {
        var row = new HBoxContainer();
        var label = Body(caption, 9, RuntimeRpgUiTheme.Quiet);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(label);
        row.AddChild(Title(value, 12, color));
        return row;
    }

    private static Control Discipline(string name, string rank, double progress, Color color)
    {
        var stack = Stack(5);
        stack.AddChild(Metric(name, rank, color));
        stack.AddChild(Progress(progress, color, 7));
        return stack;
    }

    private static Label EmptyState(string text)
    {
        var label = Body(text, 10, RuntimeRpgUiTheme.Muted, true);
        label.AddThemeStyleboxOverride("normal", RuntimeRpgUiTheme.Inset());
        label.CustomMinimumSize = new Vector2(0, 52);
        label.VerticalAlignment = VerticalAlignment.Center;
        return label;
    }

    private static PanelContainer CompactMetric(string name, string value)
    {
        var panel = Panel(new Color("241a14d1"), RuntimeRpgUiTheme.BorderMuted, 12, 8);
        panel.CustomMinimumSize = new Vector2(120, 54);
        var stack = Stack(1);
        panel.AddChild(stack);
        var number = Title(value, 17, RuntimeRpgUiTheme.Parchment);
        number.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(number);
        var caption = Body(name, 8, RuntimeRpgUiTheme.Muted);
        caption.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(caption);
        return panel;
    }

    private static HBoxContainer FullRow(Control page)
    {
        var row = new HBoxContainer();
        row.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        page.AddChild(row);
        return row;
    }

    private static PanelContainer Panel(Color background, Color border, int horizontal = 12, int vertical = 10)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", RuntimeRpgUiTheme.Frame(background, border, horizontal, vertical));
        return panel;
    }

    private static VBoxContainer Stack(int separation)
    {
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", separation);
        return stack;
    }

    private static ColorRect Divider() => new()
    {
        Color = RuntimeRpgUiTheme.BorderMuted,
        CustomMinimumSize = new Vector2(0, 1),
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    private static Label Overline(string text, Color color)
    {
        var label = Body(text.ToUpperInvariant(), 9, color);
        label.AddThemeConstantOverride("outline_size", 1);
        return label;
    }

    private static Label Body(string text, int size, Color color, bool wrap = false)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off,
            ClipText = false,
        };
        RuntimeRpgUiTheme.StyleBody(label, size, color);
        return label;
    }

    private static Label Title(string text, int size, Color color, bool wrap = false)
    {
        var label = Body(text, size, color, wrap);
        RuntimeRpgUiTheme.StyleTitle(label, size, color);
        return label;
    }

    private static ProgressBar Progress(double value, Color color, int height)
    {
        var progress = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            Value = Math.Clamp(value, 0, 100),
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, height),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        progress.AddThemeStyleboxOverride("background", RuntimeRpgUiTheme.Bar(new Color("e5dac312"), 2));
        progress.AddThemeStyleboxOverride("fill", RuntimeRpgUiTheme.Bar(color, 2));
        return progress;
    }

    private static double TrackProgress(RuntimeModulePresentation.ProgressionTrack track) =>
        track.MaximumLevel <= 0 ? 0 : track.Level * 100d / track.MaximumLevel;

    private static string FormatNamedValue(RuntimeModulePresentation.NamedValue value) =>
        value.Maximum is null ? value.Value.ToString("0.##") : $"{value.Value:0.##}/{value.Maximum:0.##}";

    private static string ItemGlyph(RuntimeModulePresentation.InventoryItem item) => "◇";

    private static string Initials(string displayName)
    {
        var words = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Concat(words.Take(2).Select(word => char.ToUpperInvariant(word[0])));
    }

    private sealed partial class MenuBackdrop : Control
    {
        public override void _Draw()
        {
            var bounds = new Rect2(Vector2.Zero, Size);
            DrawRect(bounds, new Color("100b08fa"));
            DrawCircle(new Vector2(Size.X * .14f, Size.Y * .42f), Size.Y * .62f, new Color("7d56332d"));
            DrawCircle(new Vector2(Size.X * .88f, Size.Y * .58f), Size.Y * .72f, new Color("51351f2d"));
            DrawRect(new Rect2(0, 92, Size.X, 1), new Color("a9855745"));
        }
    }
}
