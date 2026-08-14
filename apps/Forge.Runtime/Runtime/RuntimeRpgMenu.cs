using Godot;

namespace Forge.Runtime;

public enum RuntimeRpgMenuPage
{
    Inventory,
    Character,
    Journal,
}

/// <summary>
/// Player-facing RPG codex. It deliberately owns presentation only: gameplay data
/// remains in GameRoot and can be refreshed through UpdateContext.
/// </summary>
public partial class RuntimeRpgMenu : CanvasLayer
{
    private const string ElyraPortrait = "res://assets/ui/portraits/elyra.png";
    private const string BromPortrait = "res://assets/ui/portraits/brom.png";
    private const string NahlaPortrait = "res://assets/ui/portraits/nahla.png";

    private readonly Dictionary<RuntimeRpgMenuPage, Control> _pages = [];
    private readonly Dictionary<RuntimeRpgMenuPage, Button> _navButtons = [];
    private Control? _root;
    private Label? _journalObjective;
    private Label? _journalSituation;
    private Label? _itemName;
    private Label? _itemKind;
    private Label? _itemStat;
    private Label? _itemDescription;
    private RuntimeRpgMenuPage _currentPage = RuntimeRpgMenuPage.Inventory;

    public string ModuleTitle { get; init; } = "OpenRPG Forge";

    public string AreaTitle { get; init; } = "Terres connues";

    public bool IsOpen => _root?.Visible == true;

    public RuntimeRpgMenuPage CurrentPage => _currentPage;

    public event Action? MapRequested;

    public event Action? Closed;

    public override void _Ready()
    {
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

    public void UpdateContext(string objective, string situation)
    {
        if (_journalObjective is not null)
            _journalObjective.Text = string.IsNullOrWhiteSpace(objective) ? "Explorer les terres connues" : objective;
        if (_journalSituation is not null)
            _journalSituation.Text = string.IsNullOrWhiteSpace(situation)
                ? "Le chemin reste ouvert. Choisissez la prochaine étape de votre voyage."
                : situation;
    }

    private void BuildShell()
    {
        _root = new Control
        {
            Name = "RpgCodex",
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
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

        var inventory = new Control { Name = "InventoryPage" };
        inventory.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        body.AddChild(inventory);
        _pages[RuntimeRpgMenuPage.Inventory] = inventory;
        BuildInventoryPage(inventory);

        var character = new Control { Name = "CharacterPage" };
        character.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        body.AddChild(character);
        _pages[RuntimeRpgMenuPage.Character] = character;
        BuildCharacterPage(character);

        var journal = new Control { Name = "JournalPage" };
        journal.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        body.AddChild(journal);
        _pages[RuntimeRpgMenuPage.Journal] = journal;
        BuildJournalPage(journal);

        BuildFooter(_root);
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
        var rule = new ColorRect
        {
            Color = RuntimeRpgUiTheme.BorderMuted,
            AnchorRight = 1,
            OffsetTop = 91,
            OffsetBottom = 92,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        root.AddChild(rule);

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

        var brand = new HBoxContainer { CustomMinimumSize = new Vector2(315, 0) };
        brand.AddThemeConstantOverride("separation", 13);
        top.AddChild(brand);
        var sigil = Title("◇", 28, RuntimeRpgUiTheme.Player);
        sigil.CustomMinimumSize = new Vector2(44, 44);
        sigil.HorizontalAlignment = HorizontalAlignment.Center;
        sigil.VerticalAlignment = VerticalAlignment.Center;
        sigil.AddThemeStyleboxOverride("normal", RuntimeRpgUiTheme.Frame(
            new Color("241a14df"), RuntimeRpgUiTheme.Bronze, 0, 0));
        brand.AddChild(sigil);
        var brandCopy = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        brandCopy.AddThemeConstantOverride("separation", 1);
        brandCopy.AddChild(Body("CHRONIQUES", 13, RuntimeRpgUiTheme.Bone));
        brandCopy.AddChild(Title(AreaTitle, 11, RuntimeRpgUiTheme.Muted));
        brand.AddChild(brandCopy);

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
        resources.AddThemeConstantOverride("separation", 11);
        top.AddChild(resources);
        var coin = Body("◆  2 480", 11, RuntimeRpgUiTheme.Gold);
        coin.VerticalAlignment = VerticalAlignment.Center;
        resources.AddChild(coin);
        var crystal = Body("◇  12", 11, RuntimeRpgUiTheme.Player);
        crystal.VerticalAlignment = VerticalAlignment.Center;
        resources.AddChild(crystal);
        var close = new Button
        {
            Text = "ESC  FERMER",
            CustomMinimumSize = new Vector2(104, 36),
            FocusMode = Control.FocusModeEnum.All,
        };
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
        footer.MouseFilter = Control.MouseFilterEnum.Ignore;
        root.AddChild(footer);
        var hint = Body("I  SAC     ·     C  PERSONNAGE     ·     J  QUÊTES     ·     M  CARTE     ·     ÉCHAP  RETOUR AU JEU",
            8, RuntimeRpgUiTheme.Muted);
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        hint.VerticalAlignment = VerticalAlignment.Center;
        footer.AddChild(hint);
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

    private void BuildInventoryPage(Control page)
    {
        var row = FullRow(page);
        row.AddThemeConstantOverride("separation", 14);

        var sidebar = Panel(new Color("21170ff2"), RuntimeRpgUiTheme.BorderMuted, 18, 17);
        sidebar.CustomMinimumSize = new Vector2(270, 0);
        row.AddChild(sidebar);
        var side = Stack(11);
        sidebar.AddChild(side);
        side.AddChild(Overline("GROUPE ACTIF", RuntimeRpgUiTheme.Player));
        side.AddChild(Title("Inventaire", 30, RuntimeRpgUiTheme.Parchment));
        side.AddChild(Body("Objets partagés et équipement de l'équipe.", 10, RuntimeRpgUiTheme.Muted, true));
        side.AddChild(Divider());
        foreach (var member in new[]
                 {
                     ("H", "Héros", "Aventurier", RuntimeRpgUiTheme.Player),
                     ("E", "Elyra", "Lame-scribe", RuntimeRpgUiTheme.Green),
                     ("B", "Brom", "Sentinelle", RuntimeRpgUiTheme.Gold),
                     ("N", "Nahla", "Arcaniste", RuntimeRpgUiTheme.Violet),
                 })
            side.AddChild(BuildCompactMember(member.Item1, member.Item2, member.Item3, member.Item4));
        side.AddChild(Divider());
        side.AddChild(Body("CHARGE", 8, RuntimeRpgUiTheme.Quiet));
        side.AddChild(Progress(63, RuntimeRpgUiTheme.Player, 7));
        side.AddChild(Body("63 / 100   ·   VALEUR 2 480", 9, RuntimeRpgUiTheme.Muted));
        side.AddChild(Divider());
        foreach (var filter in new[] { "TOUT", "ARMES", "ARMURES", "CONSOMMABLES", "QUÊTE" })
        {
            var button = new Button { Text = filter, CustomMinimumSize = new Vector2(0, 30) };
            RuntimeRpgUiTheme.StyleButton(button, quiet: true);
            button.Alignment = HorizontalAlignment.Left;
            side.AddChild(button);
        }

        var centerPanel = Panel(new Color("19110de8"), RuntimeRpgUiTheme.BorderMuted, 18, 17);
        centerPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(centerPanel);
        var center = Stack(12);
        centerPanel.AddChild(center);
        var inventoryHeader = new HBoxContainer();
        inventoryHeader.AddChild(Title("Sac du voyageur", 25, RuntimeRpgUiTheme.Parchment));
        var count = Body("24 / 60 EMPLACEMENTS", 9, RuntimeRpgUiTheme.Muted);
        count.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        count.HorizontalAlignment = HorizontalAlignment.Right;
        inventoryHeader.AddChild(count);
        center.AddChild(inventoryHeader);
        var search = new LineEdit
        {
            PlaceholderText = "Rechercher un objet...",
            CustomMinimumSize = new Vector2(0, 36),
            ClearButtonEnabled = true,
        };
        search.AddThemeFontOverride("font", RuntimeRpgUiTheme.BodyFont);
        search.AddThemeFontSizeOverride("font_size", 10);
        search.AddThemeStyleboxOverride("normal", RuntimeRpgUiTheme.SearchField());
        search.AddThemeColorOverride("font_color", RuntimeRpgUiTheme.Bone);
        search.AddThemeColorOverride("font_placeholder_color", RuntimeRpgUiTheme.Quiet);
        center.AddChild(search);
        var grid = new GridContainer { Columns = 6, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);
        center.AddChild(grid);

        var items = new[]
        {
            new InventoryItem("Lame d'aurore", "ÉPÉE RARE", "⚔\uFE0E", "24 – 31 DÉGÂTS", "Une lame claire qui retient la chaleur du premier soleil.", RuntimeRpgUiTheme.Gold),
            new InventoryItem("Arc des lisières", "ARC FIN", "⌁", "18 – 26 DÉGÂTS", "Un bois souple gravé de repères de chasse.", RuntimeRpgUiTheme.Green),
            new InventoryItem("Fiole d'éther", "CONSOMMABLE", "◊", "+45 CONCENTRATION", "Rend immédiatement de la concentration.", RuntimeRpgUiTheme.Player),
            new InventoryItem("Cendre vive", "COMPOSANT", "✦", "ALCHIMIE", "Une braise froide utilisée dans les enchantements.", RuntimeRpgUiTheme.Health),
            new InventoryItem("Anneau ancien", "BIJOU", "○", "+8 VOLONTÉ", "Son inscription est presque effacée.", RuntimeRpgUiTheme.Violet),
            new InventoryItem("Clé de braise", "OBJET DE QUÊTE", "◇", "PASSAGE SCELLÉ", "La clé pulse lorsqu'elle approche de la porte.", RuntimeRpgUiTheme.Ember),
            new InventoryItem("Cape pâle", "MANTEAU", "△", "+5 ARMURE", "Une cape légère, faite pour les marches brumeuses.", RuntimeRpgUiTheme.Sky),
            new InventoryItem("Ration", "PROVISION", "□", "+20 VITALITÉ", "Une ration simple pour les longues routes.", RuntimeRpgUiTheme.Green),
            new InventoryItem("Grimoire fendu", "FOCUS", "▤", "+12 PUISSANCE", "Des formules incomplètes courent entre les pages.", RuntimeRpgUiTheme.Violet),
            new InventoryItem("Bombe solaire", "OUTIL", "☼", "42 DÉGÂTS", "Éclate dans un bref halo aveuglant.", RuntimeRpgUiTheme.Gold),
            new InventoryItem("Talisman", "CHARME", "✧", "+6 RÉSISTANCE", "Un talisman tressé de fils de cuivre.", RuntimeRpgUiTheme.Ember),
            new InventoryItem("Cristal clair", "MATÉRIAU", "◆", "ARTISANAT", "Un cristal pur à la résonance stable.", RuntimeRpgUiTheme.Player),
        };
        foreach (var item in items)
        {
            var cell = BuildItemCell(item);
            cell.Pressed += () => SelectItem(item);
            grid.AddChild(cell);
        }
        for (var index = items.Length; index < 30; index++) grid.AddChild(BuildEmptyItemCell());

        center.AddChild(Divider());
        var bagSummary = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        bagSummary.AddThemeConstantOverride("separation", 8);
        bagSummary.AddChild(CompactMetric("ÉQUIPÉS", "9"));
        bagSummary.AddChild(CompactMetric("CONSOMMABLES", "6"));
        bagSummary.AddChild(CompactMetric("QUÊTE", "3"));
        center.AddChild(bagSummary);

        var detailPanel = Panel(new Color("2a1d15f4"), RuntimeRpgUiTheme.Border, 20, 18);
        detailPanel.CustomMinimumSize = new Vector2(350, 0);
        row.AddChild(detailPanel);
        var detail = Stack(10);
        detailPanel.AddChild(detail);
        _itemKind = Overline("ÉPÉE RARE", RuntimeRpgUiTheme.Gold);
        detail.AddChild(_itemKind);
        _itemName = Title("Lame d'aurore", 30, RuntimeRpgUiTheme.Parchment, true);
        detail.AddChild(_itemName);
        var itemIcon = Title("⚔\uFE0E", 70, RuntimeRpgUiTheme.Gold);
        itemIcon.CustomMinimumSize = new Vector2(0, 150);
        itemIcon.HorizontalAlignment = HorizontalAlignment.Center;
        itemIcon.VerticalAlignment = VerticalAlignment.Center;
        itemIcon.AddThemeStyleboxOverride("normal", RuntimeRpgUiTheme.Inset());
        detail.AddChild(itemIcon);
        _itemStat = Title("24 – 31 DÉGÂTS", 18, RuntimeRpgUiTheme.Gold);
        detail.AddChild(_itemStat);
        detail.AddChild(Body("+7% chance critique   ·   +4 adresse", 10, RuntimeRpgUiTheme.Player, true));
        detail.AddChild(Divider());
        _itemDescription = Body("Une lame claire qui retient la chaleur du premier soleil.", 11,
            RuntimeRpgUiTheme.Muted, true);
        detail.AddChild(_itemDescription);
        var comparison = Panel(new Color("241a14d8"), RuntimeRpgUiTheme.BorderMuted, 12, 9);
        comparison.AddChild(Body("ÉQUIPÉ : Lame de voyage\nDégâts 19 – 24   ↑ amélioration", 9,
            RuntimeRpgUiTheme.Muted, true));
        detail.AddChild(comparison);
        var equip = new Button { Text = "ÉQUIPER", CustomMinimumSize = new Vector2(0, 44) };
        RuntimeRpgUiTheme.StyleButton(equip);
        equip.AddThemeColorOverride("font_color", RuntimeRpgUiTheme.Bone);
        equip.AddThemeStyleboxOverride("normal", RuntimeRpgUiTheme.ButtonStyle(
            RuntimeRpgUiTheme.Burgundy.Darkened(.16f), RuntimeRpgUiTheme.Gold));
        detail.AddChild(equip);
    }

    private void BuildCharacterPage(Control page)
    {
        var row = FullRow(page);
        row.AddThemeConstantOverride("separation", 14);

        var identityPanel = Panel(new Color("21170ff2"), RuntimeRpgUiTheme.BorderMuted, 22, 20);
        identityPanel.CustomMinimumSize = new Vector2(380, 0);
        row.AddChild(identityPanel);
        var identity = Stack(12);
        identityPanel.AddChild(identity);
        var heroPortrait = RuntimeRpgUiTheme.Portrait(ElyraPortrait, 108, RuntimeRpgUiTheme.Gold, selected: true);
        heroPortrait.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        identity.AddChild(heroPortrait);
        identity.AddChild(Overline("PERSONNAGE ACTIF", RuntimeRpgUiTheme.Player));
        identity.AddChild(Title("Le Voyageur", 34, RuntimeRpgUiTheme.Parchment));
        identity.AddChild(Title("Veilleur des lisières", 16, RuntimeRpgUiTheme.Gold));
        identity.AddChild(Body("« Le chemin se révèle à ceux qui avancent malgré la brume. »", 12,
            RuntimeRpgUiTheme.Muted, true));
        identity.AddChild(Divider());
        var origin = new HBoxContainer();
        origin.AddChild(InfoBlock("ORIGINE", "Clairval"));
        origin.AddChild(InfoBlock("VOIE", "Lame astrale"));
        identity.AddChild(origin);
        identity.AddChild(Divider());
        identity.AddChild(Overline("ATTRIBUTS", RuntimeRpgUiTheme.Muted));
        var stats = new GridContainer { Columns = 2 };
        stats.AddThemeConstantOverride("h_separation", 8);
        stats.AddThemeConstantOverride("v_separation", 8);
        identity.AddChild(stats);
        foreach (var stat in new[]
                 {
                     ("FORCE", "14", RuntimeRpgUiTheme.Health), ("ADRESSE", "17", RuntimeRpgUiTheme.Player),
                     ("VIGUEUR", "15", RuntimeRpgUiTheme.Green), ("ESPRIT", "12", RuntimeRpgUiTheme.Violet),
                     ("VOLONTÉ", "16", RuntimeRpgUiTheme.Gold), ("PRÉSENCE", "11", RuntimeRpgUiTheme.Sky),
                 })
            stats.AddChild(StatCard(stat.Item1, stat.Item2, stat.Item3));

        var equipmentPanel = Panel(new Color("19110de6"), RuntimeRpgUiTheme.BorderMuted, 18, 18);
        equipmentPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(equipmentPanel);
        var equipment = Stack(11);
        equipmentPanel.AddChild(equipment);
        var equipmentTitle = Title("Équipement", 27, RuntimeRpgUiTheme.Parchment);
        equipmentTitle.HorizontalAlignment = HorizontalAlignment.Center;
        equipment.AddChild(equipmentTitle);
        var equipmentGrid = new GridContainer { Columns = 3, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        equipmentGrid.AddThemeConstantOverride("h_separation", 16);
        equipmentGrid.AddThemeConstantOverride("v_separation", 14);
        equipment.AddChild(equipmentGrid);
        var slots = new[]
        {
            ("◇", "AMULETTE"), ("△", "TÊTE"), ("○", "ANNEAU"),
            ("⚔\uFE0E", "ARME"), ("♙", "ARMURE"), ("⌁", "SECONDAIRE"),
            ("□", "GANTS"), ("▽", "BOTTES"), ("✧", "TALISMAN"),
        };
        foreach (var slot in slots) equipmentGrid.AddChild(EquipmentSlot(slot.Item1, slot.Item2));
        var defenses = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        defenses.AddThemeConstantOverride("separation", 8);
        equipment.AddChild(defenses);
        defenses.AddChild(CompactMetric("ARMURE", "42"));
        defenses.AddChild(CompactMetric("INITIATIVE", "+8"));
        defenses.AddChild(CompactMetric("VITESSE", "6 m"));

        equipment.AddChild(Divider());
        var combat = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        combat.AddThemeConstantOverride("separation", 8);
        combat.AddChild(CompactMetric("PRÉCISION", "+12"));
        combat.AddChild(CompactMetric("CRITIQUE", "11%"));
        combat.AddChild(CompactMetric("ESQUIVE", "18"));
        combat.AddChild(CompactMetric("RÉSISTANCE", "23"));
        equipment.AddChild(combat);
        var equipmentHint = Body("Les valeurs incluent l'équipement et les effets actuellement actifs.", 9,
            RuntimeRpgUiTheme.Quiet, true);
        equipmentHint.HorizontalAlignment = HorizontalAlignment.Center;
        equipment.AddChild(equipmentHint);

        var disciplinePanel = Panel(new Color("2a1d15f2"), RuntimeRpgUiTheme.Border, 20, 20);
        disciplinePanel.CustomMinimumSize = new Vector2(390, 0);
        row.AddChild(disciplinePanel);
        var disciplines = Stack(13);
        disciplinePanel.AddChild(disciplines);
        disciplines.AddChild(Overline("MAÎTRISE", RuntimeRpgUiTheme.Gold));
        disciplines.AddChild(Title("Disciplines", 29, RuntimeRpgUiTheme.Parchment));
        disciplines.AddChild(Body("Chaque discipline ouvre de nouveaux talents actifs et passifs.", 10,
            RuntimeRpgUiTheme.Muted, true));
        disciplines.AddChild(Divider());
        disciplines.AddChild(Discipline("Lame astrale", "Rang 4", 78, RuntimeRpgUiTheme.Player));
        disciplines.AddChild(Discipline("Survie", "Rang 3", 56, RuntimeRpgUiTheme.Green));
        disciplines.AddChild(Discipline("Diplomatie", "Rang 2", 38, RuntimeRpgUiTheme.Gold));
        disciplines.AddChild(Discipline("Arcanes", "Rang 2", 31, RuntimeRpgUiTheme.Violet));
        disciplines.AddChild(Divider());
        var talents = new Button { Text = "OUVRIR LES TALENTS  ›", CustomMinimumSize = new Vector2(0, 42) };
        RuntimeRpgUiTheme.StyleButton(talents);
        disciplines.AddChild(talents);
    }

    private void BuildJournalPage(Control page)
    {
        var row = FullRow(page);
        row.AddThemeConstantOverride("separation", 14);

        var listPanel = Panel(new Color("21170ff2"), RuntimeRpgUiTheme.BorderMuted, 18, 18);
        listPanel.CustomMinimumSize = new Vector2(420, 0);
        row.AddChild(listPanel);
        var list = Stack(11);
        listPanel.AddChild(list);
        list.AddChild(Overline("CHRONIQUE", RuntimeRpgUiTheme.Player));
        list.AddChild(Title("Journal", 31, RuntimeRpgUiTheme.Parchment));
        var search = new LineEdit { PlaceholderText = "Rechercher une quête...", CustomMinimumSize = new Vector2(0, 36) };
        search.AddThemeFontOverride("font", RuntimeRpgUiTheme.BodyFont);
        search.AddThemeStyleboxOverride("normal", RuntimeRpgUiTheme.SearchField());
        search.AddThemeColorOverride("font_color", RuntimeRpgUiTheme.Bone);
        search.AddThemeColorOverride("font_placeholder_color", RuntimeRpgUiTheme.Quiet);
        list.AddChild(search);
        var tabs = new HBoxContainer();
        foreach (var tab in new[] { "ACTIVES  3", "TERMINÉES  8", "RUMEURS  2" })
        {
            var button = new Button { Text = tab, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            RuntimeRpgUiTheme.StyleButton(button, quiet: true);
            tabs.AddChild(button);
        }
        list.AddChild(tabs);
        list.AddChild(QuestCard("Les braises oubliées", "QUÊTE PRINCIPALE", "Passage scellé", true));
        list.AddChild(QuestCard("L'appel de la lisière", "QUÊTE DE RÉGION", "Forêt oubliée", false));
        list.AddChild(QuestCard("Un remède pour Clairval", "QUÊTE SECONDAIRE", "Clairval", false));

        var articlePanel = Panel(new Color("19110dee"), RuntimeRpgUiTheme.Border, 32, 26);
        articlePanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(articlePanel);
        var article = Stack(13);
        articlePanel.AddChild(article);
        var heading = new HBoxContainer();
        var titleStack = Stack(4);
        titleStack.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleStack.AddChild(Overline("QUÊTE PRINCIPALE  ·  ACTE I", RuntimeRpgUiTheme.Gold));
        _journalObjective = Title("Les braises oubliées", 38, RuntimeRpgUiTheme.Parchment, true);
        titleStack.AddChild(_journalObjective);
        heading.AddChild(titleStack);
        var track = new Button { Text = "SUIVIE  ◆", CustomMinimumSize = new Vector2(104, 34) };
        RuntimeRpgUiTheme.StyleButton(track);
        heading.AddChild(track);
        article.AddChild(heading);
        article.AddChild(Title("« Une chaleur ancienne dort sous les pierres. »", 16,
            RuntimeRpgUiTheme.Muted, true));
        article.AddChild(Divider());
        article.AddChild(Overline("SITUATION", RuntimeRpgUiTheme.Player));
        _journalSituation = Body("Elyra pense que la clé de braise peut ouvrir le passage scellé. " +
            "Le gardien attend au-delà de la porte.", 12, RuntimeRpgUiTheme.Parchment, true);
        article.AddChild(_journalSituation);
        article.AddChild(Divider());
        article.AddChild(Overline("ÉTAPES", RuntimeRpgUiTheme.Muted));
        article.AddChild(JournalStep("Parler à Elyra", "Comprendre l'origine du sceau.", true));
        article.AddChild(JournalStep("Retrouver la clé de braise", "Explorer les galeries de la crypte.", false));
        article.AddChild(JournalStep("Ouvrir le passage", "Utiliser la clé sur la porte des Cendres.", false));
        article.AddChild(JournalStep("Vaincre le gardien", "Libérer la route vers la lumière.", false));
        article.AddChild(Divider());
        article.AddChild(Overline("RÉCOMPENSES", RuntimeRpgUiTheme.Gold));
        var rewards = new HBoxContainer();
        rewards.AddThemeConstantOverride("separation", 10);
        rewards.AddChild(Reward("480", "EXPÉRIENCE", RuntimeRpgUiTheme.Player));
        rewards.AddChild(Reward("◆ 320", "OR", RuntimeRpgUiTheme.Gold));
        rewards.AddChild(Reward("Lame claire", "ÉQUIPEMENT", RuntimeRpgUiTheme.Violet));
        article.AddChild(rewards);
        article.AddChild(Divider());
        article.AddChild(Overline("NOTE DU VOYAGEUR", RuntimeRpgUiTheme.Player));
        var note = Panel(new Color("2b1e16d8"), RuntimeRpgUiTheme.BorderMuted, 15, 12);
        note.AddChild(Body("La pierre autour du sceau est chaude, mais aucune flamme ne brûle ici. " +
            "Elyra reconnaît les mêmes runes sur la clé retrouvée dans les galeries.", 10,
            RuntimeRpgUiTheme.Muted, true));
        article.AddChild(note);
    }

    private void SelectItem(InventoryItem item)
    {
        if (_itemName is not null) _itemName.Text = item.Name;
        if (_itemKind is not null)
        {
            _itemKind.Text = item.Kind;
            _itemKind.AddThemeColorOverride("font_color", item.Color);
        }
        if (_itemStat is not null)
        {
            _itemStat.Text = item.Stat;
            _itemStat.AddThemeColorOverride("font_color", item.Color);
        }
        if (_itemDescription is not null) _itemDescription.Text = item.Description;
    }

    private static HBoxContainer FullRow(Control page)
    {
        var row = new HBoxContainer();
        row.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        page.AddChild(row);
        return row;
    }

    private static PanelContainer BuildCompactMember(string initial, string name, string role, Color color)
    {
        var panel = Panel(new Color("241a14d1"), RuntimeRpgUiTheme.BorderMuted, 9, 7);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 9);
        panel.AddChild(row);
        var portraitPath = initial switch
        {
            "B" => BromPortrait,
            "N" => NahlaPortrait,
            _ => ElyraPortrait,
        };
        var avatar = RuntimeRpgUiTheme.Portrait(portraitPath, 38, color);
        row.AddChild(avatar);
        var copy = Stack(1);
        copy.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        copy.AddChild(Body(name, 10, RuntimeRpgUiTheme.Parchment));
        copy.AddChild(Body(role.ToUpperInvariant(), 8, color));
        row.AddChild(copy);
        return panel;
    }

    private static Button BuildItemCell(InventoryItem item)
    {
        var button = new Button
        {
            Text = item.Glyph,
            TooltipText = $"{item.Name}\n{item.Kind}",
            CustomMinimumSize = new Vector2(92, 96),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.All,
        };
        RuntimeRpgUiTheme.StyleButton(button, quiet: true);
        button.AddThemeFontOverride("font", RuntimeRpgUiTheme.TitleFont);
        button.AddThemeFontSizeOverride("font_size", 27);
        button.AddThemeColorOverride("font_color", item.Color);
        button.AddThemeStyleboxOverride("normal", RuntimeRpgUiTheme.Frame(
            new Color("1f1712e2"), item.Color.Darkened(.32f), 0, 0));
        button.AddThemeStyleboxOverride("hover", RuntimeRpgUiTheme.Frame(
            new Color("3a291de8"), item.Color, 0, 0));
        return button;
    }

    private static Button BuildEmptyItemCell()
    {
        var button = new Button
        {
            Text = "",
            Disabled = true,
            CustomMinimumSize = new Vector2(92, 96),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        button.AddThemeStyleboxOverride("disabled", RuntimeRpgUiTheme.Frame(
            new Color("130f0ca6"), new Color("9b794f28"), 0, 0));
        return button;
    }

    private static PanelContainer StatCard(string name, string value, Color color)
    {
        var panel = Panel(new Color("241a14d8"), color.Darkened(.38f), 12, 10);
        panel.CustomMinimumSize = new Vector2(158, 70);
        var row = new HBoxContainer();
        panel.AddChild(row);
        var label = Body(name, 9, RuntimeRpgUiTheme.Muted);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        label.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(label);
        var number = Title(value, 24, color);
        number.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(number);
        return panel;
    }

    private static PanelContainer EquipmentSlot(string glyph, string name)
    {
        var panel = Panel(new Color("1f1712d8"), RuntimeRpgUiTheme.BorderMuted, 8, 7);
        panel.CustomMinimumSize = new Vector2(128, 126);
        var stack = Stack(4);
        panel.AddChild(stack);
        var icon = Title(glyph, 34, RuntimeRpgUiTheme.Gold);
        icon.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        icon.VerticalAlignment = VerticalAlignment.Center;
        stack.AddChild(icon);
        var caption = Body(name, 8, RuntimeRpgUiTheme.Muted);
        caption.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(caption);
        return panel;
    }

    private static PanelContainer CompactMetric(string name, string value)
    {
        var panel = Panel(new Color("241a14d1"), RuntimeRpgUiTheme.BorderMuted, 12, 8);
        panel.CustomMinimumSize = new Vector2(130, 54);
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

    private static Control Discipline(string name, string rank, double progress, Color color)
    {
        var stack = Stack(5);
        var header = new HBoxContainer();
        var title = Title(name, 15, RuntimeRpgUiTheme.Parchment);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(title);
        header.AddChild(Body(rank.ToUpperInvariant(), 9, color));
        stack.AddChild(header);
        stack.AddChild(Progress(progress, color, 7));
        stack.AddChild(Body($"{progress:0}% vers le prochain rang", 8, RuntimeRpgUiTheme.Quiet));
        return stack;
    }

    private static PanelContainer QuestCard(string name, string kind, string place, bool active)
    {
        var panel = Panel(active ? new Color("3a281edf") : new Color("1d1611d2"),
            active ? RuntimeRpgUiTheme.Gold : RuntimeRpgUiTheme.BorderMuted, 13, 11);
        panel.CustomMinimumSize = new Vector2(0, 92);
        var stack = Stack(4);
        panel.AddChild(stack);
        stack.AddChild(Overline(kind, active ? RuntimeRpgUiTheme.Gold : RuntimeRpgUiTheme.Quiet));
        stack.AddChild(Title(name, 16, RuntimeRpgUiTheme.Parchment, true));
        stack.AddChild(Body(place.ToUpperInvariant(), 8, RuntimeRpgUiTheme.Muted));
        return panel;
    }

    private static Control JournalStep(string title, string description, bool done)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        var marker = Title(done ? "◆" : "◇", 15, done ? RuntimeRpgUiTheme.Gold : RuntimeRpgUiTheme.Quiet);
        marker.CustomMinimumSize = new Vector2(25, 40);
        marker.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(marker);
        var copy = Stack(2);
        copy.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        copy.AddChild(Title(title, 14, done ? RuntimeRpgUiTheme.Muted : RuntimeRpgUiTheme.Parchment));
        copy.AddChild(Body(description, 9, RuntimeRpgUiTheme.Muted, true));
        row.AddChild(copy);
        return row;
    }

    private static PanelContainer Reward(string value, string caption, Color color)
    {
        var panel = Panel(new Color("241a14d1"), color.Darkened(.38f), 13, 10);
        panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var stack = Stack(2);
        panel.AddChild(stack);
        var valueLabel = Title(value, 17, color);
        valueLabel.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(valueLabel);
        var captionLabel = Body(caption, 8, RuntimeRpgUiTheme.Muted);
        captionLabel.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(captionLabel);
        return panel;
    }

    private static VBoxContainer InfoBlock(string caption, string value)
    {
        var stack = Stack(2);
        stack.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        stack.AddChild(Body(caption, 8, RuntimeRpgUiTheme.Quiet));
        stack.AddChild(Title(value, 15, RuntimeRpgUiTheme.Parchment));
        return stack;
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
        label.AddThemeColorOverride("font_outline_color", new Color("001a205c"));
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
            Value = value,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, height),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        progress.AddThemeStyleboxOverride("background", RuntimeRpgUiTheme.Bar(new Color("e5dac312"), 2));
        progress.AddThemeStyleboxOverride("fill", RuntimeRpgUiTheme.Bar(color, 2));
        return progress;
    }

    private sealed record InventoryItem(string Name, string Kind, string Glyph, string Stat,
        string Description, Color Color);

    private sealed partial class MenuBackdrop : Control
    {
        public override void _Draw()
        {
            var bounds = new Rect2(Vector2.Zero, Size);
            DrawRect(bounds, new Color("100b08fa"));
            DrawCircle(new Vector2(Size.X * .14f, Size.Y * .42f), Size.Y * .62f, new Color("7d56332d"));
            DrawCircle(new Vector2(Size.X * .88f, Size.Y * .58f), Size.Y * .72f, new Color("51351f2d"));
            DrawRect(new Rect2(0, 92, Size.X, 1), new Color("a9855745"));
            DrawRect(new Rect2(0, Size.Y - 22, Size.X, 22), new Color("030201a8"));
            for (var index = 0; index < 64; index++)
            {
                var x = (index * 137) % Math.Max(1, (int)Size.X);
                var y = 92 + ((index * 83) % Math.Max(1, (int)Size.Y - 114));
                DrawCircle(new Vector2(x, y), .65f, new Color("e5c99a20"));
            }
        }
    }
}
