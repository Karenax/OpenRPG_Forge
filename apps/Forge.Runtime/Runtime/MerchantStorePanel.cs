using Forge.RpgKit.Domain.Merchants;
using Godot;

namespace Forge.Runtime;

public partial class MerchantStorePanel : PanelContainer
{
    public void Configure(StoreDefinition store, StoreUiConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(configuration);
        Name = "MerchantStorePanel";
        CustomMinimumSize = new Vector2(720, 480);
        SetMeta("store_id", store.Id.ToString("D"));
        SetMeta("keyboard", configuration.Keyboard);
        SetMeta("gamepad", configuration.Gamepad);
        SetMeta("screen_reader_labels", configuration.ScreenReaderLabels);
        SetMeta("high_contrast", configuration.HighContrast);

        var layout = new VBoxContainer { Name = "StoreLayout" };
        AddChild(layout);
        var title = new Label { Name = "StoreTitle", Text = store.DisplayName };
        layout.AddChild(title);

        var filters = new LineEdit
        {
            Name = "StoreFilters",
            PlaceholderText = "Filtrer les objets",
            TooltipText = "Filtrer le catalogue de la boutique",
            FocusMode = FocusModeEnum.All,
            Editable = configuration.Filters,
        };
        layout.AddChild(filters);

        var columns = new HBoxContainer { Name = "StoreColumns" };
        foreach (var column in configuration.Columns)
            columns.AddChild(new Label { Name = $"Column_{column}", Text = column });
        layout.AddChild(columns);

        var stock = new ItemList
        {
            Name = "StoreStock",
            TooltipText = "Catalogue des objets disponibles",
            FocusMode = FocusModeEnum.All,
            SelectMode = ItemList.SelectModeEnum.Multi,
        };
        foreach (var entry in store.Stock.OrderBy(item => item.DisplayOrder))
            stock.AddItem($"{entry.VariantId} · {entry.UnitPrice:0.##}");
        layout.AddChild(stock);

        var actions = new HBoxContainer { Name = "StoreActions" };
        actions.AddChild(Button("CompareButton", "Comparer", configuration.Compare));
        actions.AddChild(Button("AddToCartButton", "Ajouter au panier", configuration.Cart));
        actions.AddChild(Button("ConfirmButton", "Confirmer", configuration.Confirmation));
        layout.AddChild(actions);
        layout.AddChild(new Label { Name = "CartSummary", Text = "Panier · 0 objet · total 0" });

        if (configuration.HighContrast)
        {
            title.AddThemeColorOverride("font_color", Colors.White);
            filters.AddThemeColorOverride("font_color", Colors.White);
            filters.AddThemeColorOverride("font_placeholder_color", Colors.LightGray);
        }
    }

    private static Button Button(string name, string text, bool enabled) => new()
    {
        Name = name,
        Text = text,
        TooltipText = text,
        FocusMode = FocusModeEnum.All,
        Disabled = !enabled,
    };
}
