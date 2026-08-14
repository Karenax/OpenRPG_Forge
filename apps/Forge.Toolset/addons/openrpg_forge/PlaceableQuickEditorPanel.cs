#if TOOLS
using Godot;

namespace Forge.Toolset.Editor;

internal static class PlaceableQuickEditorPanel
{
    private static readonly Color MutedText = new("aa9bc7");
    private static readonly Color SectionText = new("d8c8ff");
    private static readonly Color Ember = new("ff9858");

    internal static void Attach(
        VBoxContainer worldWorkspace,
        VBoxContainer editing,
        Control preview)
    {
        ArgumentNullException.ThrowIfNull(worldWorkspace);
        ArgumentNullException.ThrowIfNull(editing);
        ArgumentNullException.ThrowIfNull(preview);

        var paletteRow = editing.GetNode<HBoxContainer>("PaletteRow");
        var entityRow = editing.GetNode<HBoxContainer>("EntityRow");
        var scaleRow = editing.GetNode<HBoxContainer>("ScaleRow");
        var clipboardRow = editing.GetNode<HBoxContainer>("ClipboardRow");
        var overridesRow = editing.GetNode<HBoxContainer>("OverridesRow");
        var historyRow = editing.GetNode<HBoxContainer>("HistoryRow");

        var builder = new HSplitContainer
        {
            Name = "PlaceableBuilder",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SplitOffsets = [306],
        };
        worldWorkspace.AddChild(builder);
        worldWorkspace.MoveChild(builder, 0);

        var panel = new PanelContainer
        {
            Name = "PlaceableQuickEditor",
            CustomMinimumSize = new Vector2(294, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", Card("100d1b", "5a4078", 1, 10));
        builder.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        panel.AddChild(margin);

        editing.Reparent(margin, keepGlobalTransform: false);
        editing.CustomMinimumSize = new Vector2(266, 0);
        editing.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        editing.AddThemeConstantOverride("separation", 10);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        editing.AddChild(header);
        editing.MoveChild(header, 0);
        var heading = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        heading.AddThemeConstantOverride("separation", 1);
        header.AddChild(heading);
        var title = new Label { Text = "PLAÇABLES" };
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", Ember);
        heading.AddChild(title);
        var subtitle = new Label { Text = "Choisir · placer · transformer" };
        subtitle.AddThemeColorOverride("font_color", MutedText);
        heading.AddChild(subtitle);
        var simpleBadge = new Label { Text = "SIMPLE" };
        simpleBadge.AddThemeColorOverride("font_color", new Color("83d6ae"));
        simpleBadge.AddThemeStyleboxOverride("normal", Card("16261f", "386a55", 1, 7));
        simpleBadge.AddThemeConstantOverride("outline_size", 0);
        header.AddChild(simpleBadge);

        var tabs = new TabContainer
        {
            Name = "PlaceableQuickTabs",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            ClipTabs = false,
        };
        editing.AddChild(tabs);

        var assets = ScrollableTab(tabs, "ASSETS");
        var scene = ScrollableTab(tabs, "SCÈNE");
        var inspector = ScrollableTab(tabs, "INSPECTEUR");

        var placeButton = BuildAssetsTab(assets, paletteRow, tabs, editing);
        BuildSceneTab(scene, entityRow, scaleRow);
        BuildInspectorTab(inspector, overridesRow, clipboardRow);
        tabs.TabChanged += index => placeButton.Visible = index == 0;
        BuildHistoryFooter(editing, historyRow);

        GD.Print("OPENRPG_FORGE_TOOLSET_PLACEABLE_QUICK_EDITOR_READY tabs=assets-scene-inspector " +
                 "modes=move-rotate-scale advanced=collapsed history=undo-redo");

        foreach (var oldRow in new Control[]
                 {
                     paletteRow, entityRow, scaleRow, clipboardRow, overridesRow, historyRow,
                 })
        {
            oldRow.QueueFree();
        }

        preview.Reparent(builder, keepGlobalTransform: false);
        preview.CustomMinimumSize = new Vector2(520, 320);
        preview.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        preview.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
    }

    private static Button BuildAssetsTab(
        VBoxContainer tab,
        Node paletteRow,
        TabContainer tabs,
        VBoxContainer actionHost)
    {
        AddSection(tab, "AJOUTER UN PLAÇABLE", "Choisissez un asset puis placez-le directement dans la zone.");

        var palette = paletteRow.GetNode<OptionButton>("BlueprintPalette");
        MoveTo(palette, tab);
        palette.CustomMinimumSize = new Vector2(0, 40);
        palette.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        palette.TooltipText = "Blueprint ou asset à placer dans la zone active.";

        AddSection(tab, "POSITION DE DÉPART", "Le gizmo permet ensuite d’ajuster l’objet dans le viewport.");
        var positionGrid = new GridContainer { Columns = 2 };
        positionGrid.AddThemeConstantOverride("h_separation", 8);
        positionGrid.AddThemeConstantOverride("v_separation", 6);
        tab.AddChild(positionGrid);
        MoveField(paletteRow.GetNode<SpinBox>("PositionX"), positionGrid, "X");
        MoveField(paletteRow.GetNode<SpinBox>("PositionZ"), positionGrid, "Z");

        var gridRow = new HBoxContainer();
        gridRow.AddThemeConstantOverride("separation", 8);
        tab.AddChild(gridRow);
        var gridLabel = new Label
        {
            Text = "Snap",
            CustomMinimumSize = new Vector2(62, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        gridLabel.AddThemeColorOverride("font_color", SectionText);
        gridRow.AddChild(gridLabel);
        var gridSize = paletteRow.GetNode<SpinBox>("GridSize");
        MoveTo(gridSize, gridRow);
        gridSize.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        gridSize.CustomMinimumSize = new Vector2(0, 38);
        gridSize.TooltipText = "Pas de déplacement et de placement sur la grille.";

        var place = paletteRow.GetNode<Button>("PlaceButton");
        MoveTo(place, actionHost);
        place.Text = "＋ Placer dans la scène";
        place.CustomMinimumSize = new Vector2(0, 44);
        place.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        place.TooltipText = "Place l’asset dans la zone active. La commande peut être annulée.";
        place.AddThemeColorOverride("font_color", Colors.White);
        place.AddThemeStyleboxOverride("normal", Card("d84f32", "ff8b63", 1, 8));
        place.AddThemeStyleboxOverride("hover", Card("ed603f", "ffad8f", 1, 8));
        place.Pressed += () => tabs.CurrentTab = 1;

        tab.AddChild(Hint("Après le placement, l’onglet Scène s’ouvre pour déplacer, tourner ou redimensionner l’objet."));
        return place;
    }

    private static void BuildSceneTab(VBoxContainer tab, Node entityRow, Node scaleRow)
    {
        AddSection(tab, "OBJET SÉLECTIONNÉ", "Cliquez aussi directement sur un objet dans le viewport.");
        var picker = entityRow.GetNode<OptionButton>("EntityPicker");
        MoveTo(picker, tab);
        picker.CustomMinimumSize = new Vector2(0, 40);
        picker.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        AddSection(tab, "TRANSFORMATION", "Un seul geste principal à la fois, comme dans la référence.");
        var modes = new HBoxContainer();
        modes.AddThemeConstantOverride("separation", 5);
        tab.AddChild(modes);
        var moveMode = ModeButton("Déplacer", true);
        var rotateMode = ModeButton("Tourner", false);
        var scaleMode = ModeButton("Taille", false);
        modes.AddChild(moveMode);
        modes.AddChild(rotateMode);
        modes.AddChild(scaleMode);

        var movePanel = new VBoxContainer();
        movePanel.AddThemeConstantOverride("separation", 7);
        tab.AddChild(movePanel);
        var movement = new GridContainer { Columns = 2 };
        movement.AddThemeConstantOverride("h_separation", 6);
        movement.AddThemeConstantOverride("v_separation", 6);
        movePanel.AddChild(movement);
        MoveAction(entityRow.GetNode<Button>("MoveXNegative"), movement, "X −");
        MoveAction(entityRow.GetNode<Button>("MoveXPositive"), movement, "X +");
        MoveAction(entityRow.GetNode<Button>("MoveZNegative"), movement, "Z −");
        MoveAction(entityRow.GetNode<Button>("MoveZPositive"), movement, "Z +");
        movePanel.AddChild(Hint("Glissez l’objet dans le viewport pour un placement libre avec snap."));

        var rotatePanel = new VBoxContainer { Visible = false };
        rotatePanel.AddThemeConstantOverride("separation", 7);
        tab.AddChild(rotatePanel);
        var rotate = entityRow.GetNode<Button>("RotateButton");
        MoveTo(rotate, rotatePanel);
        rotate.Text = "↻ Tourner de 90°";
        rotate.CustomMinimumSize = new Vector2(0, 42);
        rotate.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        rotatePanel.AddChild(Hint("La rotation utilise le pivot et le pas configurés dans l’espace de travail."));

        var scalePanel = new VBoxContainer { Visible = false };
        scalePanel.AddThemeConstantOverride("separation", 7);
        tab.AddChild(scalePanel);
        var uniform = scaleRow.GetNode<CheckButton>("UniformScale");
        MoveTo(uniform, scalePanel);
        uniform.Text = "Conserver les proportions";
        var scaleGrid = new GridContainer { Columns = 2 };
        scaleGrid.AddThemeConstantOverride("h_separation", 8);
        scaleGrid.AddThemeConstantOverride("v_separation", 6);
        scalePanel.AddChild(scaleGrid);
        MoveField(scaleRow.GetNode<SpinBox>("ScaleX"), scaleGrid, "X");
        MoveField(scaleRow.GetNode<SpinBox>("ScaleY"), scaleGrid, "Y");
        MoveField(scaleRow.GetNode<SpinBox>("ScaleZ"), scaleGrid, "Z");
        var scaleActions = new HBoxContainer();
        scaleActions.AddThemeConstantOverride("separation", 5);
        scalePanel.AddChild(scaleActions);
        MoveAction(scaleRow.GetNode<Button>("ScaleDown"), scaleActions, "− 10 %");
        MoveAction(scaleRow.GetNode<Button>("ScaleUp"), scaleActions, "+ 10 %");
        var applyScale = scaleRow.GetNode<Button>("ApplyScale");
        MoveTo(applyScale, scalePanel);
        applyScale.Text = "Appliquer la taille";
        applyScale.CustomMinimumSize = new Vector2(0, 40);
        applyScale.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var scaleHelp = scaleRow.GetNode<Label>("ScaleHelp");
        MoveTo(scaleHelp, scalePanel);
        scaleHelp.Text = "Ctrl + molette dans le viewport · gizmo X/Y/Z/XYZ";
        scaleHelp.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        scaleHelp.AddThemeColorOverride("font_color", MutedText);

        ConnectMode(moveMode, movePanel, rotateMode, rotatePanel, scaleMode, scalePanel);
        ConnectMode(rotateMode, rotatePanel, moveMode, movePanel, scaleMode, scalePanel);
        ConnectMode(scaleMode, scalePanel, moveMode, movePanel, rotateMode, rotatePanel);

        var separator = new HSeparator();
        tab.AddChild(separator);
        var delete = entityRow.GetNode<Button>("DeleteButton");
        MoveTo(delete, tab);
        delete.Text = "Supprimer de la scène";
        delete.CustomMinimumSize = new Vector2(0, 40);
        delete.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        delete.AddThemeColorOverride("font_color", new Color("ff9b9b"));
        delete.TooltipText = "Supprime l’instance sélectionnée. Utilisez Annuler pour la restaurer.";
    }

    private static void BuildInspectorTab(VBoxContainer tab, Node overridesRow, Node clipboardRow)
    {
        AddSection(tab, "PROPRIÉTÉS DU PLAÇABLE", "Les réglages techniques restent accessibles sans encombrer le geste principal.");
        var summary = overridesRow.GetNode<Label>("OverridesSummary");
        MoveTo(summary, tab);
        summary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        summary.AddThemeColorOverride("font_color", MutedText);
        var overrides = overridesRow.GetNode<Button>("OverridesButton");
        MoveTo(overrides, tab);
        overrides.Text = "Modifier les propriétés avancées";
        overrides.CustomMinimumSize = new Vector2(0, 42);
        overrides.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var advancedToggle = new CheckButton
        {
            Text = "Outils de copie avancés",
            TooltipText = "Afficher les commandes de copie sémantique et de duplication.",
        };
        tab.AddChild(advancedToggle);
        var advanced = new VBoxContainer { Visible = false };
        advanced.AddThemeConstantOverride("separation", 7);
        tab.AddChild(advanced);
        advancedToggle.Toggled += visible => advanced.Visible = visible;

        var clipboardSummary = clipboardRow.GetNode<Label>("ClipboardSummary");
        MoveTo(clipboardSummary, advanced);
        clipboardSummary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        clipboardSummary.AddThemeColorOverride("font_color", MutedText);
        var firstActions = new HBoxContainer();
        firstActions.AddThemeConstantOverride("separation", 5);
        advanced.AddChild(firstActions);
        MoveAction(clipboardRow.GetNode<Button>("CopyButton"), firstActions, "Copier");
        MoveAction(clipboardRow.GetNode<Button>("CutButton"), firstActions, "Couper");
        var secondActions = new HBoxContainer();
        secondActions.AddThemeConstantOverride("separation", 5);
        advanced.AddChild(secondActions);
        MoveAction(clipboardRow.GetNode<Button>("PasteButton"), secondActions, "Coller");
        MoveAction(clipboardRow.GetNode<Button>("DuplicateButton"), secondActions, "Dupliquer");
        var independent = clipboardRow.GetNode<CheckButton>("IndependentCopy");
        MoveTo(independent, advanced);
        independent.Text = "Créer une copie indépendante";
    }

    private static void BuildHistoryFooter(VBoxContainer editing, Node historyRow)
    {
        var separator = new HSeparator();
        editing.AddChild(separator);
        var history = new HBoxContainer { Name = "QuickHistory" };
        history.AddThemeConstantOverride("separation", 5);
        editing.AddChild(history);
        MoveAction(historyRow.GetNode<Button>("UndoButton"), history, "↶ Annuler");
        MoveAction(historyRow.GetNode<Button>("RedoButton"), history, "↷ Rétablir");
        var revision = historyRow.GetNode<Label>("Revision");
        MoveTo(revision, editing);
        revision.HorizontalAlignment = HorizontalAlignment.Center;
        revision.AddThemeColorOverride("font_color", MutedText);
    }

    private static VBoxContainer ScrollableTab(TabContainer tabs, string name)
    {
        var scroll = new ScrollContainer
        {
            Name = name,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        tabs.AddChild(scroll);
        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        content.AddThemeConstantOverride("separation", 9);
        scroll.AddChild(content);
        return content;
    }

    private static void AddSection(VBoxContainer parent, string title, string description)
    {
        var heading = new Label { Text = title };
        heading.AddThemeFontSizeOverride("font_size", 13);
        heading.AddThemeColorOverride("font_color", SectionText);
        parent.AddChild(heading);
        var help = Hint(description);
        parent.AddChild(help);
    }

    private static Label Hint(string text)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        label.AddThemeColorOverride("font_color", MutedText);
        return label;
    }

    private static Button ModeButton(string text, bool active)
    {
        var button = new Button
        {
            Text = text,
            ToggleMode = true,
            ButtonPressed = active,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 36),
        };
        return button;
    }

    private static void ConnectMode(
        Button activeButton,
        Control activePanel,
        Button firstOtherButton,
        Control firstOtherPanel,
        Button secondOtherButton,
        Control secondOtherPanel)
    {
        activeButton.Pressed += () =>
        {
            activeButton.SetPressedNoSignal(true);
            firstOtherButton.SetPressedNoSignal(false);
            secondOtherButton.SetPressedNoSignal(false);
            activePanel.Visible = true;
            firstOtherPanel.Visible = false;
            secondOtherPanel.Visible = false;
        };
    }

    private static void MoveField(SpinBox field, GridContainer parent, string labelText)
    {
        var label = new Label
        {
            Text = labelText,
            CustomMinimumSize = new Vector2(26, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.AddThemeColorOverride("font_color", SectionText);
        parent.AddChild(label);
        MoveTo(field, parent);
        field.CustomMinimumSize = new Vector2(0, 38);
        field.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
    }

    private static void MoveAction(Button button, Container parent, string text)
    {
        MoveTo(button, parent);
        button.Text = text;
        button.CustomMinimumSize = new Vector2(0, 38);
        button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
    }

    private static void MoveTo(Control control, Node parent)
    {
        control.Owner = null;
        control.Reparent(parent, keepGlobalTransform: false);
    }

    private static StyleBoxFlat Card(string background, string border, int borderWidth, int radius) => new()
    {
        BgColor = new Color(background),
        BorderColor = new Color(border),
        BorderWidthLeft = borderWidth,
        BorderWidthTop = borderWidth,
        BorderWidthRight = borderWidth,
        BorderWidthBottom = borderWidth,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
    };
}
#endif
