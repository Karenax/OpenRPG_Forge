using System.Globalization;
using System.Text.RegularExpressions;
using Forge.Domain.Blueprints;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioMerchantAuthoringOverlayUx4T : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private StudioMerchantEvidenceSourceSnapshotUx4T? _sourceSnapshot;
    private Guid? _evidenceMerchantId;
    private bool _usesRealModule;
    private bool _usesFallback;
    private bool _previewDryRun;
    private bool _resourcesReleased = true;
    private bool _sourceRestored = true;
    private bool _applied;
    private bool _updating;
    private string _lastFailure = "none";
    private IReadOnlyList<StudioMerchantChoiceUx4T> _choices = [];
    private readonly Dictionary<string, BlueprintId> _itemReferences = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BlueprintId> _vendorReferences = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Guid> _dialogueReferences = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Guid> _routeReferences = new(StringComparer.Ordinal);
    private StudioMerchantDraftUx4T? _draft;
    private StudioMerchantPreviewUx4T? _previewResult;

    private readonly OptionButton _merchant;
    private readonly LineEdit _slug;
    private readonly LineEdit _displayNameKey;
    private readonly LineEdit _locales;
    private readonly TextEdit _vendors;
    private readonly LineEdit _wizard;
    private readonly TextEdit _foundationStock;
    private readonly LineEdit _pricing;
    private readonly LineEdit _buyCurve;
    private readonly LineEdit _sellCurve;
    private readonly LineEdit _restrictions;
    private readonly TextEdit _currencies;
    private readonly TextEdit _treasury;
    private readonly LineEdit _purchase;
    private readonly LineEdit _stolen;
    private readonly LineEdit _restock;
    private readonly LineEdit _negotiation;
    private readonly LineEdit _market;
    private readonly TextEdit _services;
    private readonly LineEdit _transaction;
    private readonly TextEdit _transactionStock;
    private readonly TextEdit _transactionScripts;
    private readonly LineEdit _sessions;
    private readonly LineEdit _storeUi;
    private readonly LineEdit _ledger;
    private readonly VBoxContainer _diagnostics;
    private readonly RichTextLabel _summary;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    internal event Action? MerchantChanged;
    internal event Action<string, bool>? StatusChanged;
    internal bool IsOpen => Visible;
    internal int MerchantChoiceCount => _choices.Count;
    internal bool HasExplicitSelection => _merchant.Selected > 0 && _draft is not null;
    internal bool CanonicalPreviewReady => _previewResult?.CanApply == true;
    internal bool ShowsStoreWizard => _draft?.Profile.Foundation is
    { Vendors.Count: > 0, Wizard.CreateStoreDefinition: true, Wizard.CreateTests: true };
    internal bool ShowsStockPricing => _draft?.Profile.Foundation is
    { Stock.Count: > 0, Pricing.BuyCurve.Count: > 0 };
    internal bool ShowsEconomy => _draft?.Profile is { Currencies.Count: > 0, Treasury.Count: > 0 };
    internal bool ShowsTransactions => _draft?.Profile.Transactions is { Stock.Count: > 0, ServerAuthoritative: true };
    internal bool ShowsExperience => _draft?.Profile.Experience?.Ui is
    { KeyboardNavigation: true, GamepadNavigation: true, ScreenReaderLabels: true };
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', VisibleText(this));
            return text.Contains("Auteur de marchand complet", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Boutique et assistant", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Économie et marché", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Transactions et événements", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Interface et audit", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsGuid(text) && !text.Contains(".json", StringComparison.OrdinalIgnoreCase);
        }
    }
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealModuleEvidence => _usesRealModule;
    internal bool UsesCanonicalFallbackEvidence => _usesFallback;
    internal bool EvidencePreviewDryRun => _previewDryRun;
    internal bool EvidenceSourceUntouched => _sourceSnapshot is not null && SourceMatches(_sourceSnapshot);
    internal bool EvidenceResourcesReleased => _resourcesReleased;
    internal bool SourceSessionRestored => _sourceRestored;
    internal string LastEvidenceFailure => _lastFailure;

    internal StudioMerchantAuthoringOverlayUx4T(StudioSessionController session)
    {
        _session = session; _sourceSession = session; Name = "StudioMerchantAuthoringOverlayUx4T";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); Visible = false; MouseFilter = MouseFilterEnum.Stop;
        var veil = new ColorRect { Color = new Color(0, 0, 0, .74f) };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(veil);
        var dialog = StudioStyle.Panel("MerchantAuthoringDialogUx4T", StudioStyle.Chrome, 14, 2);
        dialog.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, margin: 24); AddChild(dialog);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 7); dialog.AddChild(root);
        var header = new HBoxContainer(); var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("AUTEUR DE MARCHAND COMPLET · UX4-T", 14, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label(
            "Une commande catalogue · aperçu sec · les identités techniques restent internes.", 9, StudioStyle.Muted));
        heading.AddChild(StudioStyle.Label(
            "Boutique et assistant · Stock et prix · Économie et marché · Transactions et événements · Interface et audit",
            8, StudioStyle.Muted, wrap: true));
        header.AddChild(heading); var close = StudioStyle.Button("Fermer", compact: true);
        close.CustomMinimumSize = new Vector2(76, 34); close.Pressed += Close; header.AddChild(close); root.AddChild(header);
        _merchant = Choice(root, "Marchand à éditer ou création explicite");

        var tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 350) };
        var storeTab = Tab(tabs, "Boutique et assistant");
        var identity = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _slug = Field(identity, "Slug canonique", "echoppe-des-braises");
        _displayNameKey = Field(identity, "Libellé humain ou clé localisée", "merchant.ember-shop.name");
        storeTab.AddChild(identity);
        _locales = Field(storeTab, "Langues", "fr,en");
        _vendors = Multi(storeTab,
            "Vendeurs · créature humaine | dialogue humain | choix | interaction | canal", 112);
        _wizard = Field(storeTab,
            "Assistant · définition | vendeur | choix dialogue | interaction | tests | fixture", string.Empty);

        var stockTab = Tab(tabs, "Stock et prix");
        _foundationStock = Multi(stockTab,
            "Stock complet · objet humain | variante | supply | quantité | fournisseur | restock h | ordre | conditions | valeur | monnaie",
            126);
        _pricing = Field(stockTab,
            "Prix · achat | vente | réputation | rareté | état | négociation | min | max | taux monnaie", string.Empty);
        _buyCurve = Field(stockTab, "Courbe d’achat · base=coefficient", "0=1,100=1.1");
        _sellCurve = Field(stockTab, "Courbe de vente · base=coefficient", "0=1,100=0.9");
        _restrictions = Field(stockTab,
            "Restrictions · types | tags requis | tags bloqués | qualité min | max | provenances | état min | propriétaire | règle script",
            string.Empty);

        var economyTab = Tab(tabs, "Économie et marché");
        _currencies = Multi(economyTab,
            "Monnaies · identifiant | libellé | taux | négociable | adossée réputation", 82);
        _treasury = Multi(economyTab,
            "Trésorerie · monnaie | solde | crédit | dette | recharge | plafond", 82);
        _purchase = Field(economyTab,
            "Achats · plafond objet | plafond transaction | catégories | relation min | fonds requis", string.Empty);
        _stolen = Field(economyTab,
            "Biens volés · acceptés | juridictions | preuve | rôle receleur | prime risque | événement crime", string.Empty);
        _restock = Field(economyTab,
            "Réapprovisionnement · heures monde | événement | route fournisseur humaine | demande | persistant", string.Empty);
        _negotiation = Field(economyTab,
            "Négociation · compétence | difficulté | remise max | majoration max | cooldown s | serveur", string.Empty);
        _market = Field(economyTab,
            "Marché · actif | région | offre | demande | événements | override admin", string.Empty);
        _services = Multi(economyTab,
            "Services · type | libellé | monnaie | prix | action | exigence", 82);

        var transactionTab = Tab(tabs, "Transactions et événements");
        _transaction = Field(transactionTab,
            "Transaction · interaction | choix dialogue | langues | restrictions | capacité acheteur | vendeur | serveur", string.Empty);
        _transactionStock = Multi(transactionTab,
            "Stock transactionnel · objet humain | variante | quantité | prix | monnaie | restrictions", 104);
        _transactionScripts = Multi(transactionTab,
            "Scripts optionnels · événement | chemin .forge réel", 72);
        _sessions = Field(transactionTab,
            "Sessions · API | ouverture | fermeture | client | vendeur | prix | bilan", string.Empty);
        transactionTab.AddChild(StudioStyle.Label(
            "L’aperçu ouvre et ferme une session copiée puis simule une transaction atomique sans toucher aux portefeuilles réels.",
            9, StudioStyle.Muted, wrap: true));

        var interfaceTab = Tab(tabs, "Interface et audit");
        _storeUi = Field(interfaceTab,
            "Boutique · clavier | manette | comparaison | filtres | panier | confirmation | lecteur écran | contraste | colonnes",
            string.Empty);
        _ledger = Field(interfaceTab,
            "Audit économique · actif | rétention jours | seuil anomalie | acteur | objet | source et sink", string.Empty);
        interfaceTab.AddChild(StudioStyle.Label(
            "Le Studio préserve la configuration d’interface et le ledger. Aucun panneau runtime, paiement ou crime live n’est déclenché ici.",
            9, StudioStyle.Muted, wrap: true));
        tabs.CurrentTab = 0; root.AddChild(tabs);

        _diagnostics = new VBoxContainer(); root.AddChild(_diagnostics);
        _summary = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new Vector2(0, 128),
            FitContent = false, SelectionEnabled = true }; root.AddChild(_summary);
        root.AddChild(StudioStyle.Label(
            "Prévisualiser ne modifie rien. Appliquer remplace le marchand dans le catalogue en une commande annulable.",
            9, StudioStyle.Muted, wrap: true));
        var footer = new HBoxContainer(); _preview = StudioStyle.Button("Prévisualiser", primary: true, compact: true);
        _apply = StudioStyle.Button("Appliquer", compact: true); _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true); _save = StudioStyle.Button("Enregistrer", compact: true);
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        { button.CustomMinimumSize = new Vector2(button == _preview ? 132 : 104, 34); footer.AddChild(button); }
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill }); root.AddChild(footer);
        ConnectEvents(); ClearControls(); SetEditingEnabled(false);
    }

    internal void Open()
    { if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence(); _session = _sourceSession; OpenBound(); }
    internal void RestoreSourceSurfaceAfterSmoke() { _session = _sourceSession; OpenBound(); }
    internal void HideSourceSurfaceAfterSmoke() { Visible = false; }
    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceRoot is null && ReferenceEquals(_session, _sourceSession)) return;
        _session.CancelMerchantUx4T(); _draft = null; _previewResult = null; Visible = false;
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence();
    }
    private void OpenBound()
    {
        _session.CancelMerchantUx4T(); _choices = _session.MerchantChoicesUx4T; _updating = true; _merchant.Clear();
        _merchant.AddItem("Choisir un marchand…"); _merchant.AddItem("Créer un nouveau marchand…");
        foreach (var choice in _choices) _merchant.AddItem(choice.Label); _merchant.Select(0); _updating = false;
        ClearControls(); Visible = true; SetEditingEnabled(false);
    }
    private void SelectionChanged(long _)
    {
        if (_updating) return; _session.CancelMerchantUx4T(); _previewResult = null; _apply.Disabled = true;
        if (_merchant.Selected <= 0) { _draft = null; ClearControls(); SetEditingEnabled(false); return; }
        _draft = _merchant.Selected == 1 ? _session.CreateMerchantDraftUx4T() :
            _session.CreateMerchantDraftUx4T(_choices[_merchant.Selected - 2].MerchantId);
        if (_draft is null)
        { ClearControls(); SetEditingEnabled(false); RenderEmpty("Vendeur, objet ou dialogue manquant pour composer une boutique."); return; }
        _draft = _session.CompleteMerchantDraftUx4T(_draft); Populate(_draft); SetEditingEnabled(true);
        RenderEmpty("Complétez les cinq onglets puis prévisualisez le catalogue marchand.");
    }

    private void Populate(StudioMerchantDraftUx4T draft)
    {
        _updating = true; _itemReferences.Clear(); _vendorReferences.Clear(); _dialogueReferences.Clear();
        _routeReferences.Clear();
        foreach (var item in _session.MerchantItemChoicesUx4T) _itemReferences[item.Label] = item.BlueprintId;
        foreach (var vendor in _session.MerchantVendorChoicesUx4T) _vendorReferences[vendor.Label] = vendor.BlueprintId;
        foreach (var dialogue in _session.MerchantDialogueChoicesUx4T) _dialogueReferences[dialogue.Label] = dialogue.DialogueId;
        if (_session.Project is { } project)
        {
            var routes = project.Module.WorldFeatures.VehicleRoutes.OrderBy(item => item.Id).ToArray();
            for (var index = 0; index < routes.Length; index++)
            {
                var area = project.Areas.SingleOrDefault(item => item.Id == routes[index].AreaId);
                _routeReferences[$"Route {index + 1} · {area?.DisplayName ?? "zone héritée"}"] = routes[index].Id;
            }
        }
        var profile = draft.Profile; var foundation = profile.Foundation!; var transactions = profile.Transactions!;
        var experience = profile.Experience!;
        _slug.Text = foundation.Slug; _displayNameKey.Text = foundation.DisplayNameKey;
        _locales.Text = Csv(foundation.Locales);
        _vendors.Text = string.Join('\n', foundation.Vendors.Select(item => Join(
            VendorReferenceLabel(item.VendorBlueprintId), DialogueReferenceLabel(item.DialogueId),
            item.DialogueChoiceId, item.InteractionId, item.Channel)));
        var wizard = foundation.Wizard;
        _wizard.Text = Join(wizard.CreateStoreDefinition, wizard.CreateVendorBinding,
            wizard.CreateDialogueChoice, wizard.CreateInteraction, wizard.CreateTests, wizard.TestFixtureId);
        _foundationStock.Text = string.Join('\n', foundation.Stock.Select(item => Join(
            ItemReferenceLabel(item.ItemBlueprintId), item.VariantId, item.Supply, item.Quantity,
            item.SupplierId ?? string.Empty, item.RestockWorldHours, item.DisplayOrder,
            Csv(item.HiddenConditions), item.BaseValue, item.CurrencyId)));
        var pricing = foundation.Pricing;
        _pricing.Text = Join(pricing.BuyMultiplier, pricing.SellMultiplier, pricing.ReputationFactor,
            pricing.ScarcityFactor, pricing.ConditionFactor, pricing.NegotiationFactor,
            pricing.MinimumPrice, pricing.MaximumPrice, Pairs(pricing.CurrencyRates));
        _buyCurve.Text = Curve(pricing.BuyCurve); _sellCurve.Text = Curve(pricing.SellCurve);
        var restrictions = foundation.Restrictions;
        _restrictions.Text = Join(Csv(restrictions.WhitelistTypes), Csv(restrictions.WhitelistTags),
            Csv(restrictions.BlacklistTags), restrictions.MinimumQuality, restrictions.MaximumQuality,
            Csv(restrictions.Provenances), restrictions.MinimumCondition, restrictions.OwnerId ?? string.Empty,
            restrictions.CustomRuleBinding ?? string.Empty);
        _currencies.Text = string.Join('\n', profile.Currencies.Select(item => Join(item.Id,
            item.DisplayNameKey, item.ExchangeRate, item.Tradeable, item.ReputationBacked)));
        _treasury.Text = string.Join('\n', profile.Treasury.Select(item => Join(item.CurrencyId,
            item.Balance, item.CreditLimit, item.Debt, item.ReplenishAmount, item.MaximumBalance)));
        _purchase.Text = Join(profile.Purchase.MaximumPerItem, profile.Purchase.MaximumPerTransaction,
            Pairs(profile.Purchase.CategoryCaps), profile.Purchase.MinimumRelationship,
            profile.Purchase.RequireTreasuryFunds);
        _stolen.Text = Join(profile.StolenGoods.Accepted, Csv(profile.StolenGoods.Jurisdictions),
            profile.StolenGoods.RequireProof, profile.StolenGoods.FenceRoleRequired,
            profile.StolenGoods.RiskPremium, profile.StolenGoods.CrimeEventId);
        _restock.Text = Join(profile.Restock.IntervalWorldHours, profile.Restock.EventId,
            RouteReferenceLabel(profile.Restock.SupplierRouteId), profile.Restock.DemandAware,
            profile.Restock.Persistent);
        _negotiation.Text = Join(profile.Negotiation.SkillId, profile.Negotiation.Difficulty,
            profile.Negotiation.MaximumDiscount, profile.Negotiation.MaximumMarkup,
            profile.Negotiation.CooldownSeconds, profile.Negotiation.ServerAuthoritative);
        _market.Text = profile.Market is null ? "false |  | 0 | 0 |  | false" : Join(true,
            profile.Market.RegionId, profile.Market.SupplyWeight, profile.Market.DemandWeight,
            Pairs(profile.Market.EventModifiers), profile.Market.AdminOverrideAllowed);
        _services.Text = string.Join('\n', profile.Services.Select(item => Join(item.Kind, item.LabelKey,
            item.CurrencyId, item.Price, item.ActionId, item.Requirement)));
        _transaction.Text = Join(transactions.VendorInteractionId, transactions.DialogueChoiceId,
            Csv(transactions.Locales), Csv(transactions.RestrictionIds), transactions.BuyerCapacity,
            transactions.SellerCapacity, transactions.ServerAuthoritative);
        _transactionStock.Text = string.Join('\n', transactions.Stock.Select(item => Join(
            ItemReferenceLabel(item.ItemBlueprintId), item.VariantId, item.Quantity, item.UnitPrice,
            item.CurrencyId, Csv(item.RestrictionIds))));
        _transactionScripts.Text = string.Join('\n', transactions.Scripts.OrderBy(item => item.Key,
            StringComparer.Ordinal).Select(item => Join(item.Key, item.Value)));
        var sessions = experience.Sessions;
        _sessions.Text = Join(sessions.ForgeScriptApiVersion, sessions.OpenEventBinding,
            sessions.CloseEventBinding, sessions.IncludeCustomer, sessions.IncludeVendor,
            sessions.IncludePriceContext, sessions.IncludeTransactionSummary);
        var ui = experience.Ui;
        _storeUi.Text = Join(ui.KeyboardNavigation, ui.GamepadNavigation, ui.Compare, ui.Filters,
            ui.Cart, ui.Confirmation, ui.ScreenReaderLabels, ui.HighContrast, Csv(ui.Columns));
        _ledger.Text = Join(profile.Ledger.Enabled, profile.Ledger.RetentionDays,
            profile.Ledger.AnomalyThreshold, profile.Ledger.RecordActor, profile.Ledger.RecordItem,
            profile.Ledger.RecordSourceAndSink);
        _updating = false;
    }

    private StudioMerchantDraftUx4T? ReadDraft(out string error)
    {
        error = string.Empty; var source = _draft;
        if (source is null) { error = "Choisissez un marchand."; return null; }
        try
        {
            var profile = source.Profile; var oldFoundation = profile.Foundation!;
            var vendorLines = Lines(_vendors.Text); if (vendorLines.Count == 0)
                throw new FormatException("Au moins un vendeur est requis.");
            var vendors = vendorLines.Select((line, index) =>
            {
                var p = Parts(line, 5); var vendor = ResolveVendor(p[0]); var dialogue = ResolveDialogue(p[1]);
                var existing = oldFoundation.Vendors.FirstOrDefault(item => item.VendorBlueprintId == vendor &&
                    item.DialogueId == dialogue && item.InteractionId == p[3]);
                return new MerchantVendorBindingProfile(existing?.Id ??
                    StudioSessionController.StableMerchantGuidUx4T(source.MerchantId, $"vendor:{index}"),
                    vendor, dialogue, p[2], p[3], p[4]);
            }).ToArray();
            var wizardParts = Parts(_wizard.Text, 6); var oldWizard = oldFoundation.Wizard;
            var wizard = oldWizard with { CreateStoreDefinition = Bool(wizardParts[0]),
                CreateVendorBinding = Bool(wizardParts[1]), CreateDialogueChoice = Bool(wizardParts[2]),
                CreateInteraction = Bool(wizardParts[3]), CreateTests = Bool(wizardParts[4]),
                TestFixtureId = wizardParts[5] };
            var stock = Lines(_foundationStock.Text).Select((line, index) =>
            {
                var p = Parts(line, 10); var item = ResolveItem(p[0]); var variant = p[1];
                var existing = oldFoundation.Stock.FirstOrDefault(value => value.ItemBlueprintId == item &&
                    value.VariantId == variant);
                return new MerchantStockEntryProfile(existing?.Id ??
                    StudioSessionController.StableMerchantGuidUx4T(source.MerchantId, $"stock:{index}"),
                    item, variant, Enum.Parse<MerchantSupplyPolicy>(p[2], true), Int(p[3]), Empty(p[4]),
                    Double(p[5]), Int(p[6]), CsvValues(p[7]), Decimal(p[8]), p[9]);
            }).ToArray();
            var price = Parts(_pricing.Text, 9); var oldPricing = oldFoundation.Pricing;
            var pricing = oldPricing with { BuyMultiplier = Decimal(price[0]), SellMultiplier = Decimal(price[1]),
                ReputationFactor = Decimal(price[2]), ScarcityFactor = Decimal(price[3]),
                ConditionFactor = Decimal(price[4]), NegotiationFactor = Decimal(price[5]),
                MinimumPrice = Decimal(price[6]), MaximumPrice = Decimal(price[7]),
                CurrencyRates = DecimalPairs(price[8]), BuyCurve = ParseCurve(_buyCurve.Text),
                SellCurve = ParseCurve(_sellCurve.Text) };
            var query = Parts(_restrictions.Text, 9); var oldQuery = oldFoundation.Restrictions;
            var restrictions = oldQuery with { WhitelistTypes = CsvValues(query[0]),
                WhitelistTags = CsvValues(query[1]), BlacklistTags = CsvValues(query[2]),
                MinimumQuality = Int(query[3]), MaximumQuality = Int(query[4]),
                Provenances = CsvValues(query[5]), MinimumCondition = Double(query[6]),
                OwnerId = Empty(query[7]), CustomRuleBinding = Empty(query[8]) };
            var foundation = oldFoundation with { Slug = _slug.Text.Trim(),
                DisplayNameKey = _displayNameKey.Text.Trim(), Locales = CsvValues(_locales.Text),
                Vendors = vendors, Wizard = wizard, Stock = stock, Pricing = pricing,
                Restrictions = restrictions };

            var currencies = Lines(_currencies.Text).Select(line => { var p = Parts(line, 5);
                return new MerchantWorldProfile.CurrencyDefinition(p[0], p[1], Decimal(p[2]),
                    Bool(p[3]), Bool(p[4])); }).ToArray();
            var treasury = Lines(_treasury.Text).Select(line => { var p = Parts(line, 6);
                return new MerchantWorldProfile.TreasuryAccount(p[0], Decimal(p[1]), Decimal(p[2]),
                    Decimal(p[3]), Decimal(p[4]), Decimal(p[5])); }).ToArray();
            var purchase = Parts(_purchase.Text, 5);
            var purchasePolicy = new MerchantWorldProfile.PurchasePolicy(Decimal(purchase[0]),
                Decimal(purchase[1]), DecimalPairs(purchase[2]), Double(purchase[3]), Bool(purchase[4]));
            var stolen = Parts(_stolen.Text, 6);
            var stolenPolicy = new MerchantWorldProfile.StolenGoodsPolicy(Bool(stolen[0]),
                CsvValues(stolen[1]), Bool(stolen[2]), Bool(stolen[3]), Double(stolen[4]), stolen[5]);
            var restock = Parts(_restock.Text, 5);
            var restockPolicy = new MerchantWorldProfile.RestockPolicy(Double(restock[0]), restock[1],
                ResolveRoute(restock[2]), Bool(restock[3]), Bool(restock[4]));
            var negotiation = Parts(_negotiation.Text, 6);
            var negotiationPolicy = new MerchantWorldProfile.NegotiationPolicy(negotiation[0],
                Int(negotiation[1]), Double(negotiation[2]), Double(negotiation[3]),
                Double(negotiation[4]), Bool(negotiation[5]));
            var market = Parts(_market.Text, 6);
            MerchantWorldProfile.MarketPolicy? marketPolicy = Bool(market[0])
                ? new(market[1], Double(market[2]), Double(market[3]), DoublePairs(market[4]), Bool(market[5]))
                : null;
            var services = Lines(_services.Text).Select((line, index) => { var p = Parts(line, 6);
                var existing = profile.Services.FirstOrDefault(item => item.Kind == p[0] && item.ActionId == p[4]);
                return new MerchantWorldProfile.ServiceEntry(existing?.Id ??
                    StudioSessionController.StableMerchantGuidUx4T(source.MerchantId, $"service:{index}"),
                    p[0], p[1], p[2], Decimal(p[3]), p[4], p[5]); }).ToArray();
            var transaction = Parts(_transaction.Text, 7); var oldTransactions = profile.Transactions!;
            var transactionStock = Lines(_transactionStock.Text).Select((line, index) =>
            {
                var p = Parts(line, 6); var item = ResolveItem(p[0]); var variant = p[1];
                var existing = oldTransactions.Stock.FirstOrDefault(value => value.ItemBlueprintId == item &&
                    value.VariantId == variant);
                return new MerchantWorldProfile.StockEntry(existing?.Id ??
                    StudioSessionController.StableMerchantGuidUx4T(source.MerchantId, $"transaction-stock:{index}"),
                    item, variant, Int(p[2]), Decimal(p[3]), p[4], CsvValues(p[5]));
            }).ToArray();
            var scripts = Lines(_transactionScripts.Text).Select(line => Parts(line, 2))
                .ToDictionary(item => item[0], item => item[1], StringComparer.Ordinal);
            var transactions = oldTransactions with { VendorInteractionId = transaction[0],
                DialogueChoiceId = transaction[1], Locales = CsvValues(transaction[2]),
                RestrictionIds = CsvValues(transaction[3]), BuyerCapacity = Int(transaction[4]),
                SellerCapacity = Int(transaction[5]), ServerAuthoritative = Bool(transaction[6]),
                Stock = transactionStock, Scripts = scripts };
            var sessionParts = Parts(_sessions.Text, 7); var oldExperience = profile.Experience!;
            var sessions = oldExperience.Sessions with { ForgeScriptApiVersion = sessionParts[0],
                OpenEventBinding = sessionParts[1], CloseEventBinding = sessionParts[2],
                IncludeCustomer = Bool(sessionParts[3]), IncludeVendor = Bool(sessionParts[4]),
                IncludePriceContext = Bool(sessionParts[5]), IncludeTransactionSummary = Bool(sessionParts[6]) };
            var ui = Parts(_storeUi.Text, 9);
            var storeUi = oldExperience.Ui with { KeyboardNavigation = Bool(ui[0]),
                GamepadNavigation = Bool(ui[1]), Compare = Bool(ui[2]), Filters = Bool(ui[3]),
                Cart = Bool(ui[4]), Confirmation = Bool(ui[5]), ScreenReaderLabels = Bool(ui[6]),
                HighContrast = Bool(ui[7]), Columns = CsvValues(ui[8]) };
            var experience = oldExperience with { Sessions = sessions, Ui = storeUi };
            var ledger = Parts(_ledger.Text, 6);
            var ledgerPolicy = new MerchantWorldProfile.LedgerPolicy(Bool(ledger[0]), Int(ledger[1]),
                Decimal(ledger[2]), Bool(ledger[3]), Bool(ledger[4]), Bool(ledger[5]));
            var rebuilt = profile with { BlueprintId = vendors[0].VendorBlueprintId, Currencies = currencies,
                Treasury = treasury, Purchase = purchasePolicy, StolenGoods = stolenPolicy,
                Restock = restockPolicy, Negotiation = negotiationPolicy, Market = marketPolicy,
                Services = services, Ledger = ledgerPolicy, Transactions = transactions,
                TransactionsRequiredAtRuntime = true, Foundation = foundation,
                FoundationRequiredAtRuntime = true, Experience = experience,
                ExperienceRequiredAtRuntime = true };
            return source with { Profile = rebuilt };
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
        { error = exception.Message; return null; }
    }

    private void Preview()
    {
        var draft = ReadDraft(out var error); if (draft is null) { SetStatus($"Brouillon invalide : {error}", true); return; }
        _draft = draft; _previewResult = _session.PreviewMerchantUx4T(draft); _previewDryRun = _previewResult.Succeeded;
        RenderPreview(_previewResult); _apply.Disabled = !_previewResult.CanApply;
        SetStatus(_previewResult.Message, !_previewResult.Succeeded);
    }
    private void Apply()
    {
        var draft = ReadDraft(out var error); if (draft is null) { SetStatus(error, true); return; }
        var result = _session.ApplyApprovedMerchantUx4T(draft); _applied = result.Succeeded;
        SetStatus(result.Message, !result.Succeeded); if (!result.Succeeded) return;
        MerchantChanged?.Invoke(); _previewResult = null; _apply.Disabled = true;
    }
    private void Undo() { var result = _session.UndoMerchantUx4T(); SetStatus(result.Message, !result.Succeeded); if (result.Succeeded) Reload(); }
    private void Redo() { var result = _session.RedoMerchantUx4T(); SetStatus(result.Message, !result.Succeeded); if (result.Succeeded) Reload(); }
    private void Save() { var result = _session.Save(); SetStatus(result.Message, !result.Succeeded); }
    private void Reload()
    {
        if (_draft is null) return; _draft = _session.CreateMerchantDraftUx4T(_draft.MerchantId);
        if (_draft is not null) { _draft = _session.CompleteMerchantDraftUx4T(_draft); Populate(_draft); }
        _previewResult = null; _apply.Disabled = true; MerchantChanged?.Invoke();
    }
    private void RenderPreview(StudioMerchantPreviewUx4T result)
    {
        foreach (var child in _diagnostics.GetChildren()) child.QueueFree();
        foreach (var diagnostic in result.Diagnostics.Take(5))
            _diagnostics.AddChild(StudioStyle.Label($"{diagnostic.HumanMessage} · {diagnostic.Path}",
                9, StudioStyle.Danger, wrap: true));
        _summary.Text = $"[b]{(result.CanApply ? "APERÇU CANONIQUE · MARCHAND COMPLET" : "APERÇU REFUSÉ")}[/b]\n" +
            $"{result.Message}\n{result.PatchSummary}\nInverse : {result.InverseSummary}\n" +
            $"Boutique : {result.StoreSummary}\nÉconomie : {result.EconomySummary}\n" +
            $"Transactions : {result.TransactionSummary}\nInterface : {result.ExperienceSummary}\n" +
            $"{result.RuntimeBoundary}\n[color={(_previewDryRun ? "#8ce0bd" : "#ff7777")}]" +
            $"Aperçu sans mutation : {_previewDryRun.ToString().ToLowerInvariant()}[/color]";
    }
    private void RenderEmpty(string text)
    { foreach (var child in _diagnostics.GetChildren()) child.QueueFree(); _summary.Text = text; }
    private void ClearControls()
    {
        _updating = true; foreach (var field in LineFields()) field.Text = string.Empty;
        foreach (var field in MultiFields()) field.Text = string.Empty;
        _updating = false; RenderEmpty("Choisissez explicitement un marchand ou sa création.");
    }
    private void SetEditingEnabled(bool enabled)
    {
        _preview.Disabled = !enabled; _apply.Disabled = !enabled || !CanonicalPreviewReady;
        _undo.Disabled = !_session.CanUndo; _redo.Disabled = !_session.CanRedo;
        _save.Disabled = _session.Project is null;
    }
    private void InvalidatePreview()
    { if (_updating) return; _session.CancelMerchantUx4T(); _previewResult = null; _apply.Disabled = true; }
    private void ConnectEvents()
    {
        _merchant.ItemSelected += SelectionChanged; _preview.Pressed += Preview; _apply.Pressed += Apply;
        _undo.Pressed += Undo; _redo.Pressed += Redo; _save.Pressed += Save;
        foreach (var field in LineFields()) field.TextChanged += _ => InvalidatePreview();
        foreach (var field in MultiFields()) field.TextChanged += InvalidatePreview;
    }

    internal bool PrepareEvidence(string mode)
    {
        _lastFailure = "none"; _previewResult = null;
        if ((_evidenceSession is not null || _evidenceRoot is not null) && !ReleaseEvidence()) return false;
        if (!TryOpenEvidence()) return false;
        var index = _choices.ToList().FindIndex(item => item.MerchantId == _evidenceMerchantId);
        if (index < 0) { _lastFailure = "merchant_missing"; Close(); return false; }
        _updating = true; _merchant.Select(index + 2); _updating = false; SelectionChanged(0);
        if (_draft is null) { _lastFailure = "draft"; Close(); return false; }
        _draft = _session.CompleteMerchantDraftUx4T(_draft); Populate(_draft);
        _displayNameKey.Text = $"{_displayNameKey.Text} · preuve isolée";
        var project = _session.Project!; var history = _session.History!; var revision = history.State.Revision;
        var journal = history.Journal.ToArray(); var dirty = _session.IsDirty; Preview();
        _previewDryRun = ReferenceEquals(project, _session.Project) && ReferenceEquals(history, _session.History) &&
                         history.State.Revision == revision && history.Journal.SequenceEqual(journal) &&
                         _session.IsDirty == dirty && EvidenceSourceUntouched && CanonicalPreviewReady;
        _summary.Text = $"[b]{(_usesRealModule ? "MODULE RÉEL COPIÉ" : "FALLBACK CANONIQUE TEMPORAIRE")}[/b] · " +
                        $"session isolée, source strictement inchangée.\n{_summary.Text}";
        var ready = _previewDryRun && HasExplicitSelection && ShowsStoreWizard && ShowsStockPricing &&
                    ShowsEconomy && ShowsTransactions && ShowsExperience && ShowsHumanSurface &&
                    UsesIsolatedEvidence && EvidenceSourceUntouched && (_usesRealModule ^ _usesFallback);
        if (!ready)
        {
            _lastFailure = $"ready:preview={CanonicalPreviewReady}:selection={HasExplicitSelection}:" +
                           $"wizard={ShowsStoreWizard}:stock={ShowsStockPricing}:economy={ShowsEconomy}:" +
                           $"transaction={ShowsTransactions}:experience={ShowsExperience}:human={ShowsHumanSurface}:" +
                           $"isolated={UsesIsolatedEvidence}:source={EvidenceSourceUntouched}:origin={_usesRealModule ^ _usesFallback}";
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4T_EVIDENCE_FAILED failure={_lastFailure}"); Close(); return false;
        }
        SetStatus("Preuve UX4-T isolée prête · aucun commerce live exécuté.", false); return true;
    }
    internal bool RunUiTransactionEvidence()
    {
        var snapshot = CaptureSource(); if (snapshot is null) return false; StudioSessionController? reopened = null;
        try
        {
            if (!PrepareEvidence("ux4-t-merchant-authoring") || _evidenceSession is null ||
                !ReferenceEquals(_session, _evidenceSession) || !CanonicalPreviewReady) return false;
            var transaction = _session; var original = transaction.Project; if (original is null) return false;
            var originalJson = ProjectJsonSerializer.Serialize(original); Apply(); var appliedProject = transaction.Project;
            var applied = _applied && appliedProject is not null &&
                          ProjectJsonSerializer.Serialize(appliedProject) != originalJson;
            var appliedJson = appliedProject is null ? string.Empty : ProjectJsonSerializer.Serialize(appliedProject);
            if (applied) Undo(); var undone = applied && ProjectJsonSerializer.Serialize(transaction.Project!) == originalJson;
            if (undone) Redo(); var redone = undone && ProjectJsonSerializer.Serialize(transaction.Project!) == appliedJson;
            var saved = redone && transaction.Save().Succeeded; var persisted = false;
            if (saved && transaction.Session is { } workspace)
            {
                reopened = new(); persisted = reopened.Open(workspace.RootDirectory).Succeeded &&
                    ProjectJsonSerializer.Serialize(reopened.Project!) == appliedJson;
            }
            var sourceUntouched = SourceMatches(snapshot); reopened?.Close(); reopened = null; Close();
            return applied && undone && redone && saved && persisted && sourceUntouched &&
                   EvidenceResourcesReleased && SourceSessionRestored;
        }
        catch (Exception exception)
        { GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4T_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}"); return false; }
        finally
        {
            reopened?.Close(); if (_evidenceSession is not null || _evidenceRoot is not null) Close();
            _sourceSession.RestoreMerchantApprovalUx4T(snapshot.Approval);
        }
    }
    internal void CloseEvidence() => Close();
    private bool TryOpenEvidence()
    {
        var snapshot = CaptureSource(); var evidence = _sourceSession.CreateUx4TEvidenceProject();
        if (snapshot is null || evidence is null) return false;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4t-evidence-" + Guid.NewGuid().ToString("N"));
        var session = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(root, evidence.Project).Succeeded || !session.Open(root).Succeeded)
            { session.Close(); _resourcesReleased = DeleteEvidenceRoot(root); return false; }
            _sourceSnapshot = snapshot; _evidenceSession = session; _evidenceRoot = root;
            _evidenceMerchantId = evidence.MerchantId; _session = session;
            _usesRealModule = evidence.UsesRealModule; _usesFallback = evidence.UsesCanonicalFallback;
            _previewDryRun = false; _resourcesReleased = false; _sourceRestored = false; OpenBound();
            return SourceMatches(snapshot) && (_usesRealModule ^ _usesFallback);
        }
        catch
        { session.Close(); _resourcesReleased = DeleteEvidenceRoot(root); _session = _sourceSession; _sourceRestored = true; return false; }
    }
    private StudioMerchantEvidenceSourceSnapshotUx4T? CaptureSource()
    {
        var project = _sourceSession.Project; var history = _sourceSession.History;
        return project is null || history is null ? null : new(project, history, history.State.Revision,
            history.Journal.ToArray(), _sourceSession.IsDirty, history.CanUndo, history.CanRedo,
            _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(),
            _sourceSession.SelectedEntityId, _sourceSession.CaptureMerchantApprovalUx4T());
    }
    private bool SourceMatches(StudioMerchantEvidenceSourceSnapshotUx4T snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) && ReferenceEquals(snapshot.History, history) &&
               history is not null && history.State.Revision == snapshot.Revision &&
               history.Journal.SequenceEqual(snapshot.Journal) && history.CanUndo == snapshot.CanUndo &&
               history.CanRedo == snapshot.CanRedo && _sourceSession.IsDirty == snapshot.Dirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.Selection) &&
               _sourceSession.SelectedEntityId == snapshot.ActiveEntity &&
               _sourceSession.CaptureMerchantApprovalUx4T() == snapshot.Approval;
    }
    private bool ReleaseEvidence()
    {
        var session = _evidenceSession; var root = _evidenceRoot; _evidenceSession = null; _evidenceRoot = null;
        _sourceSnapshot = null; _evidenceMerchantId = null; _session = _sourceSession; _usesRealModule = false;
        _usesFallback = false; _previewDryRun = false; var released = true;
        try { session?.Close(); } catch { released = false; }
        released &= DeleteEvidenceRoot(root); _resourcesReleased = released; _sourceRestored = true; return released;
    }

    private IEnumerable<LineEdit> LineFields() => [_slug, _displayNameKey, _locales, _wizard, _pricing,
        _buyCurve, _sellCurve, _restrictions, _purchase, _stolen, _restock, _negotiation, _market,
        _transaction, _sessions, _storeUi, _ledger];
    private IEnumerable<TextEdit> MultiFields() => [_vendors, _foundationStock, _currencies, _treasury,
        _services, _transactionStock, _transactionScripts];
    private string ItemReferenceLabel(BlueprintId id)
    {
        var choice = _session.MerchantItemChoicesUx4T.FirstOrDefault(item => item.BlueprintId == id);
        var label = choice?.Label ?? $"Objet hérité protégé {_itemReferences.Count + 1}";
        _itemReferences[label] = id; return label;
    }
    private string VendorReferenceLabel(BlueprintId id)
    {
        var choice = _session.MerchantVendorChoicesUx4T.FirstOrDefault(item => item.BlueprintId == id);
        var label = choice?.Label ?? $"Vendeur hérité protégé {_vendorReferences.Count + 1}";
        _vendorReferences[label] = id; return label;
    }
    private string DialogueReferenceLabel(Guid id)
    {
        var choice = _session.MerchantDialogueChoicesUx4T.FirstOrDefault(item => item.DialogueId == id);
        var label = choice?.Label ?? $"Dialogue hérité protégé {_dialogueReferences.Count + 1}";
        _dialogueReferences[label] = id; return label;
    }
    private string RouteReferenceLabel(Guid? id)
    {
        if (id is null) return string.Empty;
        var known = _routeReferences.FirstOrDefault(item => item.Value == id.Value);
        if (!string.IsNullOrWhiteSpace(known.Key)) return known.Key;
        var label = $"Route héritée protégée {_routeReferences.Count + 1}"; _routeReferences[label] = id.Value; return label;
    }
    private BlueprintId ResolveItem(string label) => _itemReferences.TryGetValue(label, out var id)
        ? id : throw new FormatException($"Objet inconnu : {label}");
    private BlueprintId ResolveVendor(string label) => _vendorReferences.TryGetValue(label, out var id)
        ? id : throw new FormatException($"Vendeur inconnu : {label}");
    private Guid ResolveDialogue(string label) => _dialogueReferences.TryGetValue(label, out var id)
        ? id : throw new FormatException($"Dialogue inconnu : {label}");
    private Guid? ResolveRoute(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return null;
        return _routeReferences.TryGetValue(label, out var id) ? id :
            throw new FormatException($"Route inconnue : {label}");
    }
    private static VBoxContainer Tab(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer { Name = title, SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto, FollowFocus = true };
        var form = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 7); scroll.AddChild(form); tabs.AddChild(scroll); return form;
    }
    private static OptionButton Choice(Container parent, string label)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(StudioStyle.Label(label, 8, StudioStyle.Muted));
        var result = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(result); parent.AddChild(box); return result;
    }
    private static LineEdit Field(Container parent, string label, string placeholder)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(StudioStyle.Label(label, 8, StudioStyle.Muted, wrap: true));
        var result = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(result); parent.AddChild(box); return result;
    }
    private static TextEdit Multi(Container parent, string label, float height)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(StudioStyle.Label(label, 8, StudioStyle.Muted, wrap: true));
        var result = new TextEdit { CustomMinimumSize = new Vector2(0, height),
            SizeFlagsHorizontal = SizeFlags.ExpandFill, WrapMode = TextEdit.LineWrappingMode.Boundary };
        box.AddChild(result); parent.AddChild(box); return result;
    }
    private static IReadOnlyList<string> Lines(string value) => value.Replace("\r", string.Empty,
        StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static string[] Parts(string value, int count)
    {
        var parts = value.Split('|').Select(item => item.Trim()).ToArray();
        if (parts.Length != count) throw new FormatException($"{count} colonnes attendues, {parts.Length} reçues.");
        return parts;
    }
    private static IReadOnlyList<string> CsvValues(string value) => value.Split(',',
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToArray();
    private static string Csv(IEnumerable<string> values) => string.Join(',', values);
    private static string Pairs<T>(IReadOnlyDictionary<string, T> values) => string.Join(',', values
        .OrderBy(item => item.Key, StringComparer.Ordinal).Select(item =>
            $"{item.Key}={Convert.ToString(item.Value, CultureInfo.InvariantCulture)}"));
    private static Dictionary<string, decimal> DecimalPairs(string value) => PairValues(value)
        .ToDictionary(item => item.Key, item => Decimal(item.Value), StringComparer.Ordinal);
    private static Dictionary<string, double> DoublePairs(string value) => PairValues(value)
        .ToDictionary(item => item.Key, item => Double(item.Value), StringComparer.Ordinal);
    private static IEnumerable<KeyValuePair<string, string>> PairValues(string value)
    {
        foreach (var item in CsvValues(value))
        {
            var separator = item.IndexOf('=');
            if (separator <= 0) throw new FormatException($"Paire clé=valeur invalide : {item}");
            yield return new(item[..separator].Trim(), item[(separator + 1)..].Trim());
        }
    }
    private static IReadOnlyList<MerchantPriceCurvePoint> ParseCurve(string value) => PairValues(value)
        .Select(item => new MerchantPriceCurvePoint(Decimal(item.Key), Decimal(item.Value)))
        .OrderBy(item => item.BaseValue).ToArray();
    private static string Curve(IEnumerable<MerchantPriceCurvePoint> values) => string.Join(',', values
        .Select(item => $"{item.BaseValue.ToString(CultureInfo.InvariantCulture)}=" +
                        item.Multiplier.ToString(CultureInfo.InvariantCulture)));
    private static int Int(string value) => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    private static double Double(string value) => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    private static decimal Decimal(string value) => decimal.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    private static bool Bool(string value) => bool.Parse(value);
    private static string? Empty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Join(params object?[] values) => string.Join(" | ", values.Select(value => value switch
    { bool boolean => boolean.ToString().ToLowerInvariant(), IFormattable formattable =>
        formattable.ToString(null, CultureInfo.InvariantCulture), _ => value?.ToString() ?? string.Empty }));
    private static IEnumerable<string> VisibleText(Node node)
    {
        if (node is Label label && label.Visible) yield return label.Text;
        if (node is Button button && button.Visible) yield return button.Text;
        foreach (var child in node.GetChildren()) foreach (var text in VisibleText(child)) yield return text;
    }
    private static bool ContainsGuid(string value) => Regex.IsMatch(value,
        "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}");
    private static bool DeleteEvidenceRoot(string? root)
    {
        if (string.IsNullOrWhiteSpace(root)) return true;
        try { if (Directory.Exists(root)) Directory.Delete(root, true); return !Directory.Exists(root); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return false; }
    }
    private void SetStatus(string message, bool error) => StatusChanged?.Invoke(message, error);
}
