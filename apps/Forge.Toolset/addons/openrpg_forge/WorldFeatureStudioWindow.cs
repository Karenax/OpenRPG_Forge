#if TOOLS
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Godot;

namespace Forge.Toolset.Editor;

[Tool]
public partial class WorldFeatureStudioWindow : Window
{
    private readonly Dictionary<string, LineEdit> _text = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SpinBox> _number = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CheckBox> _check = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OptionButton> _choice = new(StringComparer.Ordinal);
    private Func<ProjectState?>? _load;
    private Func<WorldFeatureCatalog, bool, CommandResult>? _mutate;
    private Action? _undo;
    private Action? _redo;
    private Action<Window>? _applyIcon;
    private RichTextLabel? _summary;
    private RichTextLabel? _feedback;
    private TabContainer? _tabs;
    private string? _capturePath;
    private double _captureDelay;
    private bool _captureOpened;
    private bool _captureScrolled;
    private int _captureTab = 6;
    private string _captureMarker = "R037_COMPANION";
    private bool _lastSucceeded;

    public Guid LastCommandId { get; private set; }
    public string LastPatchSummary { get; private set; } = string.Empty;
    public string LastDiagnostics { get; private set; } = string.Empty;
    public int SectionCount => 8;

    public void Initialize(Func<ProjectState?> load, Func<WorldFeatureCatalog, bool, CommandResult> mutate,
        Action undo, Action redo, Action<Window> applyIcon)
    {
        _load = load;
        _mutate = mutate;
        _undo = undo;
        _redo = redo;
        _applyIcon = applyIcon;
    }

    public override void _Ready()
    {
        Title = "Studio du monde vivant · OpenRPG Forge";
        MinSize = new(1180, 760);
        MaxSize = new(1540, 940);
        Size = new(1540, 940);
        CloseRequested += Hide;
        Build();
        Refresh();
        var captureArgument = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--r037-companion-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r074-companion-foundation-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r075-companion-control-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r076-companion-experience-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r081-door-access-events-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r082-door-lock-events-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r083-encounter-composition-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r084-encounter-scenario-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r085-encounter-statistics-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r040-door-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r041-door-nav-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r042-encounter-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r045-merchant-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r091-merchant-foundation-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r092-merchant-experience-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r094-placeable-security-events-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r095-placeable-composition-events-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r093-placeable-operations-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r046-placeable-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r047-placeable-events-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r096-trigger-advanced-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r048-trigger-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r049-trigger-validation-ui-capture=", StringComparison.Ordinal));
        if (captureArgument is not null)
        {
            var companionExperienceCapture = captureArgument.StartsWith(
                "--r076-companion-experience-ui-capture=", StringComparison.Ordinal);
            var companionControlCapture = captureArgument.StartsWith(
                "--r075-companion-control-ui-capture=", StringComparison.Ordinal);
            var companionFoundationCapture = captureArgument.StartsWith(
                "--r074-companion-foundation-ui-capture=", StringComparison.Ordinal);
            var doorAccessCapture = captureArgument.StartsWith(
                "--r081-door-access-events-ui-capture=", StringComparison.Ordinal);
            var doorLockEventsCapture = captureArgument.StartsWith(
                "--r082-door-lock-events-ui-capture=", StringComparison.Ordinal);
            var encounterCompositionCapture = captureArgument.StartsWith(
                "--r083-encounter-composition-ui-capture=", StringComparison.Ordinal);
            var encounterScenarioCapture = captureArgument.StartsWith(
                "--r084-encounter-scenario-ui-capture=", StringComparison.Ordinal);
            var encounterStatisticsCapture = captureArgument.StartsWith(
                "--r085-encounter-statistics-ui-capture=", StringComparison.Ordinal);
            var triggerValidationCapture = captureArgument.StartsWith("--r049-trigger-validation-ui-capture=", StringComparison.Ordinal);
            var triggerAdvancedCapture = captureArgument.StartsWith("--r096-trigger-advanced-ui-capture=", StringComparison.Ordinal);
            var triggerCapture = captureArgument.StartsWith("--r048-trigger-ui-capture=", StringComparison.Ordinal);
            var placeableEventsCapture = captureArgument.StartsWith("--r047-placeable-events-ui-capture=", StringComparison.Ordinal);
            var placeableCapture = captureArgument.StartsWith("--r046-placeable-ui-capture=", StringComparison.Ordinal);
            var merchantCapture = captureArgument.StartsWith("--r045-merchant-ui-capture=", StringComparison.Ordinal);
            var merchantFoundationCapture = captureArgument.StartsWith(
                "--r091-merchant-foundation-ui-capture=", StringComparison.Ordinal);
            var merchantExperienceCapture = captureArgument.StartsWith(
                "--r092-merchant-experience-ui-capture=", StringComparison.Ordinal);
            var placeableOperationsCapture = captureArgument.StartsWith(
                "--r093-placeable-operations-ui-capture=", StringComparison.Ordinal);
            var placeableSecurityEventsCapture = captureArgument.StartsWith(
                "--r094-placeable-security-events-ui-capture=", StringComparison.Ordinal);
            var placeableCompositionEventsCapture = captureArgument.StartsWith(
                "--r095-placeable-composition-events-ui-capture=", StringComparison.Ordinal);
            var encounterCapture = captureArgument.StartsWith("--r042-encounter-ui-capture=", StringComparison.Ordinal);
            var doorNavigationCapture = captureArgument.StartsWith("--r041-door-nav-ui-capture=", StringComparison.Ordinal);
            var doorCapture = doorAccessCapture || doorLockEventsCapture || doorNavigationCapture ||
                captureArgument.StartsWith("--r040-door-ui-capture=", StringComparison.Ordinal);
            var prefix = companionExperienceCapture ? "--r076-companion-experience-ui-capture=" :
                companionControlCapture ? "--r075-companion-control-ui-capture=" :
                companionFoundationCapture ? "--r074-companion-foundation-ui-capture=" :
                encounterStatisticsCapture ? "--r085-encounter-statistics-ui-capture=" :
                encounterScenarioCapture ? "--r084-encounter-scenario-ui-capture=" :
                encounterCompositionCapture ? "--r083-encounter-composition-ui-capture=" :
                doorLockEventsCapture ? "--r082-door-lock-events-ui-capture=" :
                doorAccessCapture ? "--r081-door-access-events-ui-capture=" :
                triggerAdvancedCapture ? "--r096-trigger-advanced-ui-capture=" :
                triggerValidationCapture ? "--r049-trigger-validation-ui-capture=" :
                triggerCapture ? "--r048-trigger-ui-capture=" :
                placeableCompositionEventsCapture ? "--r095-placeable-composition-events-ui-capture=" :
                placeableSecurityEventsCapture ? "--r094-placeable-security-events-ui-capture=" :
                placeableOperationsCapture ? "--r093-placeable-operations-ui-capture=" :
                placeableEventsCapture ? "--r047-placeable-events-ui-capture=" :
                placeableCapture ? "--r046-placeable-ui-capture=" :
                merchantFoundationCapture ? "--r091-merchant-foundation-ui-capture=" :
                merchantExperienceCapture ? "--r092-merchant-experience-ui-capture=" :
                merchantCapture ? "--r045-merchant-ui-capture=" :
                encounterCapture ? "--r042-encounter-ui-capture=" :
                doorNavigationCapture ? "--r041-door-nav-ui-capture=" :
                doorCapture ? "--r040-door-ui-capture=" : "--r037-companion-ui-capture=";
            _capturePath = captureArgument[prefix.Length..];
            _captureTab = triggerAdvancedCapture || triggerValidationCapture || triggerCapture ? 2 : placeableCompositionEventsCapture || placeableSecurityEventsCapture || placeableOperationsCapture || placeableEventsCapture || placeableCapture ? 0 : merchantExperienceCapture || merchantFoundationCapture || merchantCapture ? 5 : encounterStatisticsCapture || encounterScenarioCapture || encounterCompositionCapture || encounterCapture ? 3 : doorCapture ? 1 : 6;
            _captureMarker = companionExperienceCapture ? "R076_COMPANION_EXPERIENCE" :
                companionControlCapture ? "R075_COMPANION_CONTROL" :
                companionFoundationCapture ? "R074_COMPANION_FOUNDATION" :
                encounterStatisticsCapture ? "R085_ENCOUNTER_STATISTICS" :
                encounterScenarioCapture ? "R084_ENCOUNTER_SCENARIO" :
                encounterCompositionCapture ? "R083_ENCOUNTER_COMPOSITION" :
                doorLockEventsCapture ? "R082_DOOR_LOCK_EVENTS" :
                doorAccessCapture ? "R081_DOOR_ACCESS_EVENTS" :
                triggerAdvancedCapture ? "R096_TRIGGER_ADVANCED" : triggerValidationCapture ? "R049_TRIGGER_VALIDATION" : triggerCapture ? "R048_TRIGGER" : placeableCompositionEventsCapture ? "R095_PLACEABLE_COMPOSITION_EVENTS" : placeableSecurityEventsCapture ? "R094_PLACEABLE_SECURITY_EVENTS" : placeableOperationsCapture ? "R093_PLACEABLE_OPERATIONS" : placeableEventsCapture ? "R047_PLACEABLE_EVENTS" : placeableCapture ? "R046_PLACEABLE" : merchantExperienceCapture ? "R092_MERCHANT_EXPERIENCE" : merchantFoundationCapture ? "R091_MERCHANT_FOUNDATION" : merchantCapture ? "R045_MERCHANT" : encounterCapture ? "R042_ENCOUNTER" : doorNavigationCapture ? "R041_DOOR_NAV" :
                doorCapture ? "R040_DOOR" : "R037_COMPANION";
            SetProcess(true);
        }
    }

    public override void _Process(double delta)
    {
        if (_capturePath is null) return;
        _captureDelay += delta;
        if (!_captureOpened && _captureDelay >= 1)
        {
            Open();
            ConfigureForSmokeTest();
            if (_tabs is not null) _tabs.CurrentTab = _captureTab;
            _captureOpened = true;
        }
        if (!_captureOpened || _captureDelay < 2.5) return;
        if ((_captureMarker is "R075_COMPANION_CONTROL" or "R076_COMPANION_EXPERIENCE" or
            "R081_DOOR_ACCESS_EVENTS" or "R082_DOOR_LOCK_EVENTS" or "R083_ENCOUNTER_COMPOSITION" or
            "R084_ENCOUNTER_SCENARIO" or "R085_ENCOUNTER_STATISTICS" or "R091_MERCHANT_FOUNDATION" or
            "R092_MERCHANT_EXPERIENCE" or "R093_PLACEABLE_OPERATIONS" or "R094_PLACEABLE_SECURITY_EVENTS" or
            "R095_PLACEABLE_COMPOSITION_EVENTS" or "R096_TRIGGER_ADVANCED") && !_captureScrolled &&
            _tabs?.GetChild(_captureTab) is ScrollContainer captureScroll)
        {
            captureScroll.ScrollVertical = _captureMarker switch
            {
                "R076_COMPANION_EXPERIENCE" => 4500,
                "R081_DOOR_ACCESS_EVENTS" => 1450,
                "R082_DOOR_LOCK_EVENTS" => 4000,
                "R083_ENCOUNTER_COMPOSITION" => 350,
                "R084_ENCOUNTER_SCENARIO" => 2400,
                "R085_ENCOUNTER_STATISTICS" => 4150,
                "R091_MERCHANT_FOUNDATION" => 0,
                "R092_MERCHANT_EXPERIENCE" => 3250,
                "R093_PLACEABLE_OPERATIONS" => 850,
                "R094_PLACEABLE_SECURITY_EVENTS" => 4100,
                "R095_PLACEABLE_COMPOSITION_EVENTS" => 6600,
                "R096_TRIGGER_ADVANCED" => 2400,
                _ => 1500,
            };
            if (_captureMarker == "R076_COMPANION_EXPERIENCE") _summary?.ScrollToLine(18);
            if (_captureMarker == "R084_ENCOUNTER_SCENARIO") _summary?.ScrollToLine(12);
            if (_captureMarker == "R085_ENCOUNTER_STATISTICS") _summary?.ScrollToLine(16);
            if (_captureMarker == "R091_MERCHANT_FOUNDATION") _summary?.ScrollToLine(20);
            if (_captureMarker == "R092_MERCHANT_EXPERIENCE") _summary?.ScrollToLine(24);
            if (_captureMarker == "R093_PLACEABLE_OPERATIONS") _summary?.ScrollToLine(1);
            if (_captureMarker == "R094_PLACEABLE_SECURITY_EVENTS") _summary?.ScrollToLine(6);
            if (_captureMarker == "R095_PLACEABLE_COMPOSITION_EVENTS") _summary?.ScrollToLine(11);
            if (_captureMarker == "R096_TRIGGER_ADVANCED") _summary?.ScrollToLine(30);
            _captureScrolled = true;
            return;
        }
        var destination = Path.GetFullPath(_capturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var texture = GetViewport().GetTexture();
        var image = texture?.GetImage();
        var error = image is null ? Error.Unavailable : image.SavePng(destination);
        GD.Print($"OPENRPG_FORGE_{_captureMarker}_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} path={destination}");
        _capturePath = null;
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    public void Open()
    {
        Refresh();
        PopupCentered();
        _applyIcon?.Invoke(this);
    }

    public void Refresh()
    {
        var state = _load?.Invoke();
        if (state is null || _choice.Count == 0) return;
        PopulateBlueprints(_choice["placeable.target"], state, BlueprintKind.Placeable);
        PopulateBlueprints(_choice["door.target"], state, BlueprintKind.Door);
        PopulateBlueprints(_choice["trigger.target"], state, BlueprintKind.Trigger);
        PopulateCreatures(_choice["merchant.target"], state);
        PopulateBlueprints(_choice["merchant.stockItem"], state, BlueprintKind.Item);
        PopulateBlueprints(_choice["placeable.lootItem"], state, BlueprintKind.Item);
        Populate(_choice["placeable.attachmentBlueprint"], state.Project.Blueprints
            .Select(item => (item.DisplayName, item.Id.ToString())));
        PopulateCreatures(_choice["companion.target"], state);
        PopulateCreatures(_choice["encounter.candidate"], state);
        PopulateAreas(_choice["trigger.area"], state);
        PopulateAreas(_choice["encounter.area"], state);
        PopulateAreas(_choice["vehicle.area"], state);
        var dialogue = state.Project.Dialogues.FirstOrDefault()?.Id.Value.ToString("D") ?? string.Empty;
        var quest = state.Project.Quests.FirstOrDefault()?.Id.Value.ToString("D") ?? string.Empty;
        foreach (var key in new[] { "placeable.dialogue", "door.dialogue", "trigger.dialogue", "companion.dialogue" })
            if (string.IsNullOrWhiteSpace(_text[key].Text)) _text[key].Text = dialogue;
        if (string.IsNullOrWhiteSpace(_text["merchant.dialogue"].Text)) _text["merchant.dialogue"].Text = dialogue;
        foreach (var key in new[] { "trigger.quest", "companion.quest", "encounter.waveQuest" })
            if (string.IsNullOrWhiteSpace(_text[key].Text)) _text[key].Text = quest;
        if (string.IsNullOrWhiteSpace(_text["companion.areas"].Text))
            _text["companion.areas"].Text = string.Join(",", state.Project.Areas.Select(item => item.Id.ToString()));
        RebuildSummary();
    }

    public bool ConfigureForSmokeTest()
    {
        Refresh();
        var ashWatchFaction = _load?.Invoke()?.Project.Module.Factions.Factions
            .SingleOrDefault(item => item.Slug == "ash-watch");
        if (_choice["placeable.target"].ItemCount == 0 || _choice["door.target"].ItemCount == 0 ||
            _choice["trigger.target"].ItemCount == 0 || _choice["merchant.target"].ItemCount == 0 ||
            _choice["companion.target"].ItemCount == 0 || _choice["encounter.area"].ItemCount == 0 ||
            _choice["encounter.candidate"].ItemCount == 0 || _choice["placeable.lootItem"].ItemCount == 0 ||
            _choice["placeable.attachmentBlueprint"].ItemCount == 0 || ashWatchFaction is null)
            return false;
        _text["placeable.faction"].Text = "ash-watch";
        _text["placeable.key"].Text = "key:ember";
        _check["placeable.foundationRequired"].ButtonPressed = true;
        _check["placeable.onUsedRequired"].ButtonPressed = true;
        _check["placeable.operationsRequired"].ButtonPressed = true;
        _check["placeable.securityEventsRequired"].ButtonPressed = true;
        _choice["placeable.initialState"].Select(2);
        _choice["placeable.lockState"].Select(1);
        _choice["placeable.lootTiming"].Select((int)PlaceableAuthoredLootSpawnTiming.Open);
        _text["placeable.apiVersion"].Text = "1.0.0";
        _text["placeable.eventBinding"].Text = "InteractionExecuted";
        _text["placeable.locks"].Text = "lock:crypt-pillar";
        _text["placeable.traps"].Text = "trap:ember";
        _text["placeable.inventories"].Text = "inventory:crypt-pillar";
        _text["placeable.physicsProfiles"].Text = "physics:rigid-placeable";
        _text["placeable.slug"].Text = "crypt-pillar";
        _text["placeable.name"].Text = "Pilier des cendres";
        _text["placeable.asset"].Text = "assets/placeables/crypt-pillar.glb";
        _choice["placeable.role"].Select(0);
        _number["placeable.positionX"].Value = 1.25;
        _number["placeable.positionY"].Value = 0;
        _number["placeable.positionZ"].Value = -2.5;
        _check["placeable.physics"].ButtonPressed = true;
        _number["placeable.mass"].Value = 12;
        _text["door.loading"].Text = "assets/loading/crypt-of-ashes.png";
        _number["door.health"].Value = 40;
        _check["door.accessRequired"].ButtonPressed = true;
        _text["door.accessAsset"].Text = "assets/doors/ash-gate.glb";
        _choice["door.lockState"].Select((int)DoorAccessLockState.Locked);
        _text["door.credentials"].Text = "key:ember";
        _text["door.lockSkill"].Text = "mechanics";
        _number["door.lockDifficulty"].Value = 12;
        _check["door.consumeCredential"].ButtonPressed = true;
        _text["door.rejectionFr"].Text = "La porte des cendres reste verrouillée.";
        _text["door.trapPayload"].Text = "damage:ember-burst";
        _text["door.trapSides"].Text = "front,back";
        _text["door.trapEvents"].Text = "interact,portal-use-requested";
        _text["trigger.key"].Text = "key:ember";
        _check["trigger.foundationRequired"].ButtonPressed = true;
        _text["trigger.slug"].Text = "crypt-threshold";
        _text["trigger.conditions"].Text = "crypt.open";
        _text["trigger.portalAnchor"].Text = "anchor:crypt-exit";
        _text["trigger.onEnterScript"].Text = "scripts/trigger_enter";
        _text["trigger.onExitScript"].Text = "scripts/trigger_exit";
        _check["trigger.validationRequired"].ButtonPressed = true;
        _text["trigger.knownTargets"].Text = "encounter:crypt-warden";
        _text["trigger.knownScripts"].Text = "scripts/trigger_click,scripts/trigger_enter,scripts/trigger_exit";
        _text["trigger.accessibleLayers"].Text = "characters";
        _text["trigger.queryFactions"].Text = ashWatchFaction.Id.ToString("D");
        _check["trigger.rest"].ButtonPressed = true;
        _check["trigger.director"].ButtonPressed = true;
        _text["encounter.faction"].Text = "ash-hostile";
        _number["encounter.alert"].Value = 2;
        _choice["encounter.difficultyBand"].Select((int)EncounterDifficultyBand.Hard);
        _choice["encounter.activationMode"].Select((int)EncounterActivationMode.PopulationMaintained);
        _choice["encounter.distribution"].Select((int)EncounterSpawnDistribution.Weighted);
        _check["encounter.scenarioRequired"].ButtonPressed = true;
        _text["encounter.anchorId"].Text = "anchor:crypt-north";
        _number["encounter.anchorPositionX"].Value = 2;
        _number["encounter.anchorPositionZ"].Value = 4;
        _number["encounter.anchorCapacity"].Value = 8;
        _check["encounter.anchorNavigable"].ButtonPressed = true;
        _check["encounter.anchorFallback"].ButtonPressed = true;
        _choice["encounter.anchorVisibility"].Select((int)EncounterWorldProfile.VisibilityMode.BehindCover);
        _choice["encounter.activatorAuthority"].Select((int)EncounterNetworkAuthority.Server);
        _text["encounter.activatorTags"].Text = "party";
        _choice["encounter.waveCompletion"].Select((int)EncounterWaveCompletionPolicy.ObjectiveCompleted);
        _check["encounter.statisticsRequired"].ButtonPressed = true;
        _number["encounter.statisticsRuns"].Value = 64;
        _number["encounter.statisticsSeed"].Value = 840;
        _text["encounter.statisticsPercentiles"].Text = "0.5,0.9,0.99";
        _text["merchant.currency"].Text = "ember-crown";
        _check["merchant.foundationRequired"].ButtonPressed = true;
        _check["merchant.wizard"].ButtonPressed = true;
        _text["merchant.storeSlug"].Text = "crypt-vigil-market";
        _text["merchant.displayKey"].Text = "merchant.crypt-vigil.name";
        _text["merchant.dialogue"].Text = _load?.Invoke()?.Project.Dialogues.FirstOrDefault()?.Id.Value.ToString("D") ?? string.Empty;
        _text["merchant.fixture"].Text = "merchant-crypt-vigil-nominal";
        _check["merchant.ledger"].ButtonPressed = true;
        _check["merchant.transactionsRequired"].ButtonPressed = true;
        _text["merchant.vendorInteraction"].Text = "interaction:trade";
        _text["merchant.dialogueChoice"].Text = "choice:trade";
        _text["merchant.variant"].Text = "standard";
        _choice["merchant.supply"].Select((int)MerchantSupplyPolicy.Infinite);
        _number["merchant.stockQuantity"].Value = 12;
        _number["merchant.unitPrice"].Value = 5;
        _text["merchant.supplier"].Text = "supplier:ember-guild";
        _number["merchant.stockRestock"].Value = 12;
        _number["merchant.displayOrder"].Value = 1;
        _text["merchant.hiddenConditions"].Text = "quest:market-closed";
        _text["merchant.locales"].Text = "fr-FR,en-US";
        _text["merchant.restrictions"].Text = "quest:merchant-open";
        _text["merchant.onOpen"].Text = "scripts/merchant_open";
        _number["merchant.buyMultiplier"].Value = 1.2;
        _number["merchant.sellMultiplier"].Value = .6;
        _text["merchant.buyCurve"].Text = "0:1,100:1.1";
        _text["merchant.sellCurve"].Text = "0:1,100:0.9";
        _text["merchant.currencyRates"].Text = "ember-crown=1";
        _number["merchant.reputationFactor"].Value = .1;
        _number["merchant.scarcityFactor"].Value = .2;
        _number["merchant.conditionFactor"].Value = .2;
        _number["merchant.negotiationFactor"].Value = .1;
        _number["merchant.minimumPrice"].Value = 1;
        _number["merchant.maximumPrice"].Value = 500;
        _text["merchant.whitelistTypes"].Text = "relic,consumable";
        _text["merchant.whitelistTags"].Text = "merchant-approved";
        _text["merchant.blacklistTags"].Text = "stolen,cursed";
        _number["merchant.minimumQuality"].Value = 1;
        _number["merchant.maximumQuality"].Value = 5;
        _text["merchant.provenances"].Text = "author:forge";
        _number["merchant.minimumCondition"].Value = .5;
        _text["merchant.owner"].Text = string.Empty;
        _text["merchant.customRule"].Text = "scripts/merchant_rule.forge";
        _check["merchant.experienceRequired"].ButtonPressed = true;
        _text["merchant.apiVersion"].Text = "1.0.0";
        _text["merchant.onOpened"].Text = "StoreSessionOpened";
        _text["merchant.onClosed"].Text = "StoreSessionClosed";
        _check["merchant.includeCustomer"].ButtonPressed = true;
        _check["merchant.includeVendor"].ButtonPressed = true;
        _check["merchant.includePriceContext"].ButtonPressed = true;
        _check["merchant.includeTransactionSummary"].ButtonPressed = true;
        _check["merchant.uiKeyboard"].ButtonPressed = true;
        _check["merchant.uiGamepad"].ButtonPressed = true;
        _check["merchant.uiCompare"].ButtonPressed = true;
        _check["merchant.uiFilters"].ButtonPressed = true;
        _check["merchant.uiCart"].ButtonPressed = true;
        _check["merchant.uiConfirmation"].ButtonPressed = true;
        _check["merchant.uiScreenReader"].ButtonPressed = true;
        _check["merchant.uiHighContrast"].ButtonPressed = true;
        _text["merchant.uiColumns"].Text = "name,compare,price,quantity";
        _text["companion.formation"].Text = "wedge";
        _check["companion.direct"].ButtonPressed = true;
        _check["companion.recruitDialogue"].ButtonPressed = true;
        _check["companion.recruitScript"].ButtonPressed = true;
        _check["companion.foundationRequired"].ButtonPressed = true;
        _check["companion.controlRequired"].ButtonPressed = true;
        _check["companion.experienceRequired"].ButtonPressed = true;
        _text["companion.recruitScriptId"].Text = "scripts/companion_recruit";
        _text["companion.recruitConditions"].Text = "quest:crypt-open";
        _text["companion.recruitConsequences"].Text = "relation:companion-increase";
        _text["companion.commands"].Text =
            "move,follow,hold,rally,guard,interact,attack,defend,focus-fire,protect,disengage,use-ability";
        _choice["companion.ownership"].Select(1);
        _text["companion.controllers"].Text = "player:owner";
        _text["companion.allowedOwnership"].Text = "party,shared,game-master";
        _text["companion.movementCommands"].Text = "move,follow,hold,rally,guard,interact";
        _text["companion.tacticalCommands"].Text =
            "attack,defend,focus-fire,protect,disengage,use-ability";
        _text["companion.stances"].Text = "passive,defensive,balanced,aggressive";
        _text["companion.combatAbilities"].Text = "heal,shield";
        _text["companion.equipmentOwner"].Text = "player:owner";
        _choice["companion.deathMode"].Select(0);
        _number["companion.recovery"].Value = 30;
        _text["companion.resurrectionResource"].Text = "resource:ember";
        _text["companion.replacementCreature"].Text = "creature:elyra-heir";
        _text["companion.memorial"].Text = "memorial:elyra";
        _check["companion.dropInventory"].ButtonPressed = true;
        _number["companion.navigateThreshold"].Value = 10;
        _number["companion.portalThreshold"].Value = 30;
        _number["companion.teleportThreshold"].Value = 60;
        _text["companion.safeAnchorTags"].Text = "party.safe";
        foreach (var key in new[] { "portraits", "health", "resources", "statuses", "quickCommands", "reorder", "keyboard", "gamepad", "screenReader", "highContrast" })
            _check[$"companion.hud.{key}"].ButtonPressed = true;
        _text["companion.hud.resources"].Text = "health,focus";
        _text["companion.contexts"].Text = "recruit,owner";
        _choice["companion.persistence"].Select(2);
        _check["companion.crossZone"].ButtonPressed = (_load?.Invoke()?.Project.Areas.Count ?? 0) > 1;
        _text["script.tag"].Text = "door.ash";
        _text["script.event"].Text = "opened";
        _text["script.route"].Text = "scripts/door_ash_opened.forge";
        RebuildSummary();
        return true;
    }

    public bool PreviewForSmokeTest() { Mutate(true); return _lastSucceeded; }
    public bool ApplyForSmokeTest() { Mutate(false); return _lastSucceeded; }

    private void Build()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 20);
        AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);
        var title = new Label { Text = "STUDIO DU MONDE VIVANT" };
        title.AddThemeFontSizeOverride("font_size", 25);
        title.AddThemeColorOverride("font_color", new("ff9858"));
        root.AddChild(title);
        root.AddChild(new Label
        {
            Text = "Placeables, portes, triggers, rencontres, routes, marchands, compagnons et scripts — un seul document canonique, prévisualisable et annulable.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        var split = new HSplitContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, SplitOffsets = [990] };
        root.AddChild(split);
        var tabsPanel = Panel("CONFIGURATION SANS JSON");
        split.AddChild(tabsPanel);
        var tabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _tabs = tabs;
        tabsPanel.GetChild<VBoxContainer>(0).AddChild(tabs);
        BuildPlaceables(Tab(tabs, "PLACEABLES"));
        BuildDoors(Tab(tabs, "PORTES"));
        BuildTriggers(Tab(tabs, "TRIGGERS"));
        BuildEncounters(Tab(tabs, "RENCONTRES"));
        BuildVehicles(Tab(tabs, "ROUTES"));
        BuildMerchants(Tab(tabs, "MARCHANDS"));
        BuildCompanions(Tab(tabs, "COMPAGNONS"));
        BuildScripts(Tab(tabs, "SCRIPTS"));
        var previewPanel = Panel("APERÇU SÉMANTIQUE");
        split.AddChild(previewPanel);
        var preview = previewPanel.GetChild<VBoxContainer>(0);
        _summary = new RichTextLabel
        {
            BbcodeEnabled = true,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new(360, 480),
        };
        preview.AddChild(_summary);
        preview.AddChild(Button("Actualiser l’aperçu", RebuildSummary));
        _feedback = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new(360, 150) };
        preview.AddChild(_feedback);
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        root.AddChild(actions);
        actions.AddChild(Button("Prévisualiser", () => Mutate(true)));
        actions.AddChild(Button("Appliquer", () => Mutate(false), true));
        actions.AddChild(Button("Annuler", () => { _undo?.Invoke(); Refresh(); }));
        actions.AddChild(Button("Rétablir", () => { _redo?.Invoke(); Refresh(); }));
        actions.AddChild(Button("Fermer", Hide));
    }

    private void BuildPlaceables(VBoxContainer form)
    {
        Section(form, "FONDATION PLACEABLE", "Blueprint composable, role exclusif, apparence et transform independant du moteur.");
        Section(form, "ONUSED ET VALIDATION", "Evenement type et controles collision, navigation, animation, locks, traps, inventaire, scripts, physique et budgets.");
        Toggle(form, "placeable.onUsedRequired", "OnUsed requis au runtime", true);
        Text(form, "placeable.apiVersion", "API ForgeScript", "1.0.0");
        Text(form, "placeable.eventBinding", "Binding evenement", "InteractionExecuted");
        Text(form, "placeable.beforeScript", "Script avant", "scripts/placeable_before_used");
        Text(form, "placeable.afterScript", "Script apres", "scripts/placeable_after_used");
        Toggle(form, "placeable.hasLock", "Composant lock", true);
        Text(form, "placeable.locks", "References locks", "lock:crypt-pillar");
        Toggle(form, "placeable.hasTrap", "Composant trap", true);
        Text(form, "placeable.traps", "References traps", "trap:ember");
        Toggle(form, "placeable.hasInventory", "Composant inventaire", true);
        Text(form, "placeable.inventories", "References inventaires", "inventory:crypt-pillar");
        Text(form, "placeable.physicsProfiles", "Profils physiques", "physics:rigid-placeable");
        Number(form, "placeable.drawBudget", "Budget draw calls", 1, 100000, 8, 1);
        Number(form, "placeable.scriptBudget", "Budget scripts", 0, 100000, 8, 1);
        Section(form, "R093 · CONTENEUR", "Capacité, ownership, accès, persistance, loot, UI, réplication et inventaire référencé.");
        Toggle(form, "placeable.operationsRequired", "Opérations P1 requises au runtime", true);
        Number(form, "placeable.containerSlots", "Capacité en slots", 1, 100000, 24, 1);
        Number(form, "placeable.containerMass", "Capacité en masse", 0, 1000000, 160, 1);
        Text(form, "placeable.containerOwnership", "Ownership", "party");
        Text(form, "placeable.containerAccess", "Permissions d'accès", "container.open,container.deposit,container.withdraw");
        Toggle(form, "placeable.containerPersistent", "Persistance du contenu", true);
        Text(form, "placeable.containerLoot", "Politique de loot", "loot:crypt-pillar");
        Text(form, "placeable.containerUi", "Profil d'interface", "ui:placeable-chest");
        Toggle(form, "placeable.containerReplicated", "Réplication serveur", true);
        Text(form, "placeable.containerInventory", "Inventaire référencé", "inventory:crypt-pillar");
        Section(form, "MACHINE D'ÉTATS VISUELLE", "Idle, open, closed, broken, active et custom avec transitions prévisualisables.");
        Choice(form, "placeable.initialState", "État initial", ["Idle", "Open", "Closed", "Broken", "Active", "Custom"]);
        Text(form, "placeable.animationIdle", "Animation idle", "animation:idle");
        Text(form, "placeable.animationOpen", "Animation open", "animation:open");
        Text(form, "placeable.animationClosed", "Animation closed", "animation:closed");
        Text(form, "placeable.animationBroken", "Animation broken", "animation:broken");
        Text(form, "placeable.animationActive", "Animation active", "animation:active");
        Text(form, "placeable.animationCustom", "Animation custom", "animation:custom");
        Text(form, "placeable.openCondition", "Condition closed → open", "quest:crypt-open");
        Number(form, "placeable.transitionDuration", "Durée de transition", 0, 3600, 1, .05);
        Section(form, "DESTRUCTIBILITÉ", "Santé, armure, résistances, étapes visuelles, destruction, débris et réparation.");
        Number(form, "placeable.healthMaximum", "Santé maximale", 1, 1000000, 100, 1);
        Number(form, "placeable.healthCurrent", "Santé initiale", 0, 1000000, 100, 1);
        Number(form, "placeable.armor", "Armure", 0, 1000000, 10, 1);
        Number(form, "placeable.fireResistance", "Résistance feu", 0, 1, .25, .05);
        Number(form, "placeable.forceResistance", "Résistance force", 0, 1, .5, .05);
        Text(form, "placeable.damageStages", "Étapes visuelles", "0.75,0.5,0.25");
        Text(form, "placeable.destructionEffect", "Effet de destruction", "vfx:ember-shatter");
        Text(form, "placeable.debris", "Débris", "debris:crypt-pillar");
        Toggle(form, "placeable.repairable", "Réparable", true);
        Number(form, "placeable.repairCost", "Coût de réparation", 0, 1000000, 20, 1);
        Section(form, "PROTECTION PLOT", "Actions protégées, permissions, feedback et override MJ audité.");
        Text(form, "placeable.protectedActions", "Actions protégées", "destroy,move");
        Text(form, "placeable.protectionPermissions", "Permissions", "placeable.plot.override");
        Text(form, "placeable.protectionFeedback", "Feedback", "Cet objet appartient à l'histoire.");
        Toggle(form, "placeable.gmOverride", "Override MJ autorisé", true);
        Text(form, "placeable.protectionAudit", "Binding d'audit", "PlaceableProtectionOverride");
        Section(form, "VERROUILLAGE", "État, clés, challenge, code, hacking, relock, jam et permissions.");
        Choice(form, "placeable.lockState", "État initial du verrou", ["Unlocked", "Locked", "Jammed", "Broken"]);
        Text(form, "placeable.lockCredentials", "Clés et credentials", "key:ember");
        Text(form, "placeable.lockSkill", "Compétence", "mechanics");
        Number(form, "placeable.lockDifficulty", "Difficulté", 0, 1000000, 12, 1);
        Text(form, "placeable.lockCode", "Hash du code", "sha256:042");
        Toggle(form, "placeable.lockHackable", "Hacking autorisé", true);
        Toggle(form, "placeable.lockRelockable", "Reverrouillable", true);
        Toggle(form, "placeable.lockCanJam", "Peut être bloqué", true);
        Text(form, "placeable.lockPermissions", "Permissions du verrou", "placeable.lock.admin");
        Section(form, "R094 · PIÈGE", "Trigger, payload, détection, désarmement, reset, ownership, VFX et politique amicale.");
        Toggle(form, "placeable.securityEventsRequired", "Sécurité et événements requis au runtime", true);
        Text(form, "placeable.trapTrigger", "Déclencheur", "container:open");
        Text(form, "placeable.trapPayload", "Payload", "damage:ember-burst");
        Number(form, "placeable.trapDetection", "Difficulté de détection", 0, 1000000, 10, 1);
        Number(form, "placeable.trapDisarm", "Difficulté de désarmement", 0, 1000000, 12, 1);
        Number(form, "placeable.trapReset", "Délai de reset", 0, 1000000, 30, 1);
        Text(form, "placeable.trapOwner", "Ownership du piège", "ash-watch");
        Text(form, "placeable.trapVfx", "Effet visuel", "vfx:ember-burst");
        Text(form, "placeable.trapFriendly", "Politique amicale", "ash-watch");
        Toggle(form, "placeable.trapArmed", "Piège armé", true);
        Toggle(form, "placeable.trapOneShot", "Usage unique", true);
        Section(form, "TRÉSOR INITIAL", "LootTable, timing init/open/reset, seed et état généré persistant.");
        Target(form, "placeable.lootItem", "Blueprint objet de loot");
        Text(form, "placeable.lootGroup", "Groupe de loot", "crypt-relic");
        Number(form, "placeable.lootWeight", "Poids", .01, 1000000, 1, .01);
        Number(form, "placeable.lootMinimum", "Quantité minimale", 0, 1000000, 1, 1);
        Number(form, "placeable.lootMaximum", "Quantité maximale", 0, 1000000, 2, 1);
        Number(form, "placeable.lootQuality", "Qualité minimale", 0, 1000000, 1, 1);
        Text(form, "placeable.lootCondition", "Condition", "quest:crypt-open");
        Choice(form, "placeable.lootTiming", "Timing", ["Initialize", "Open", "Reset"]);
        Number(form, "placeable.lootSeed", "Seed", int.MinValue, int.MaxValue, 42, 1);
        Toggle(form, "placeable.lootPersistent", "Persister l'état généré", true);
        Section(form, "ONOPEN ET ONCLOSE", "Bindings typés avec acteur, résultat, ancien état et autorité.");
        Text(form, "placeable.lifecycleApi", "API ForgeScript", "1.0.0");
        Text(form, "placeable.openedBinding", "Binding ContainerOpened", "ContainerOpened");
        Text(form, "placeable.closedBinding", "Binding ContainerClosed", "ContainerClosed");
        Toggle(form, "placeable.containerEventActor", "Inclure l'acteur", true);
        Toggle(form, "placeable.containerEventResult", "Inclure le résultat", true);
        Toggle(form, "placeable.containerEventPrevious", "Inclure l'ancien état", true);
        Toggle(form, "placeable.containerEventAuthority", "Inclure l'autorité", true);
        Section(form, "ONLOCK ET ONUNLOCK", "Avant/après avec méthode, credential, acteur et veto contrôlé.");
        Text(form, "placeable.lockChangingBinding", "Binding LockStateChanging", "LockStateChanging");
        Text(form, "placeable.lockChangedBinding", "Binding LockStateChanged", "LockStateChanged");
        Toggle(form, "placeable.lockEventMethod", "Inclure la méthode", true);
        Toggle(form, "placeable.lockEventCredential", "Inclure le credential", true);
        Toggle(form, "placeable.lockEventActor", "Inclure l'acteur", true);
        Toggle(form, "placeable.lockEventVeto", "Veto contrôlé", true);
        Section(form, "ONDISTURBED", "ContainerContentsChanged avec delta transactionnel, cause, révision et acteur.");
        Text(form, "placeable.contentsBinding", "Binding ContainerContentsChanged", "ContainerContentsChanged");
        Toggle(form, "placeable.contentsDelta", "Inclure le delta transactionnel", true);
        Toggle(form, "placeable.contentsCause", "Inclure la cause", true);
        Toggle(form, "placeable.contentsRevision", "Inclure les révisions", true);
        Toggle(form, "placeable.contentsActor", "Inclure l'acteur", true);
        Section(form, "R095 · DÉGÂTS ET DESTRUCTION",
            "DamageApplied et EntityDestroyed partagent le payload d'entité uniforme.");
        Toggle(form, "placeable.compositionEventsRequired", "Composition et événements requis au runtime", true);
        Text(form, "placeable.damageApi", "API ForgeScript", "1.0.0");
        Text(form, "placeable.damagedBinding", "Binding DamageApplied", "DamageApplied");
        Text(form, "placeable.destroyedBinding", "Binding EntityDestroyed", "EntityDestroyed");
        Toggle(form, "placeable.damageSource", "Inclure la source", true);
        Toggle(form, "placeable.damageAmount", "Inclure le montant", true);
        Toggle(form, "placeable.damageType", "Inclure le type de dégâts", true);
        Toggle(form, "placeable.damagePrevious", "Inclure la santé précédente", true);
        Toggle(form, "placeable.damageCurrent", "Inclure la santé courante", true);
        Toggle(form, "placeable.damageContext", "Inclure le contexte", true);
        Section(form, "SOCKETS ET ASSEMBLAGE",
            "Socket typé, snap local, capacité, blueprint enfant et prefab composé réversible.");
        Text(form, "placeable.socketId", "Identifiant du socket", "lever");
        Text(form, "placeable.socketKind", "Type du socket", "mechanism");
        Number(form, "placeable.socketX", "Position locale X", -1000000, 1000000, 0, .1);
        Number(form, "placeable.socketY", "Position locale Y", -1000000, 1000000, 1.2, .1);
        Number(form, "placeable.socketZ", "Position locale Z", -1000000, 1000000, 0, .1);
        Text(form, "placeable.socketTags", "Tags acceptés", "lever,vfx,prop");
        Toggle(form, "placeable.socketSnap", "Snap local activé", true);
        Number(form, "placeable.socketCapacity", "Capacité du socket", 1, 1000, 2, 1);
        Target(form, "placeable.attachmentBlueprint", "Blueprint enfant");
        Text(form, "placeable.attachmentTags", "Tags de l'attachement", "lever,prop");
        Text(form, "placeable.attachmentPrefab", "Prefab composé", "prefab:crypt-pillar-lever");
        Number(form, "placeable.attachmentX", "Position auteur X avant snap", -1000000, 1000000, 9, .1);
        Number(form, "placeable.attachmentY", "Position auteur Y avant snap", -1000000, 1000000, 9, .1);
        Number(form, "placeable.attachmentZ", "Position auteur Z avant snap", -1000000, 1000000, 9, .1);
        Section(form, "INTERACTION MULTI-VERBES",
            "Inspect, Use, Open, Talk, Take, Push, Climb et custom sont filtrés puis ordonnés par contexte.");
        Text(form, "placeable.standardVerbs", "Verbes standards",
            "inspect,use,open,talk,take,push,climb");
        Text(form, "placeable.verbOrder", "Ordre prioritaire",
            "push,take,custom-ring,inspect,use,open,talk,climb");
        Text(form, "placeable.verbContexts", "Contextes (verbe=tag ou !tag)",
            "open=unlocked,talk=interlocutor,take=portable,push=!anchored,climb=climbable");
        Text(form, "placeable.customVerb", "Identifiant du verbe custom", "custom-ring");
        Text(form, "placeable.customVerbLabel", "Label du verbe custom", "placeable.ring");
        Number(form, "placeable.customVerbPriority", "Priorité du verbe custom", -100000, 100000, 6, 1);
        Text(form, "placeable.customVerbRequired", "Contexte requis custom", "ash-rune");
        Text(form, "placeable.customVerbForbidden", "Contexte interdit custom", "");
        Text(form, "placeable.customVerbBinding", "Binding du verbe custom", "RingRequested");
        Target(form, "placeable.target", "Blueprint placeable");
        Toggle(form, "placeable.foundationRequired", "Fondation requise au runtime", true);
        Text(form, "placeable.slug", "Slug stable", "crypt-pillar");
        Text(form, "placeable.locale", "Locale", "fr-FR");
        Text(form, "placeable.name", "Nom localise", "Pilier des cendres");
        Text(form, "placeable.prompt", "Prompt localise", "Examiner");
        Text(form, "placeable.asset", "Asset visuel", "assets/placeables/crypt-pillar.glb");
        Text(form, "placeable.mesh", "Mesh", "mesh:crypt-pillar");
        Text(form, "placeable.icon", "Icone", "assets/icons/crypt-pillar.png");
        Text(form, "placeable.portrait", "Portrait", "assets/portraits/crypt-pillar.png");
        Text(form, "placeable.tags", "Tags", "crypt,ash,architecture");
        Text(form, "placeable.notes", "Notes auteur", "Pilier interactif de la crypte");
        Choice(form, "placeable.role", "Role", ["Objet utilisable", "Decor statique optimise"]);
        Section(form, "INTERACTION, COLLISION ET COMPOSANTS", "Verbe, focus, autorite, collision et scripts sont valides avant le build.");
        Text(form, "placeable.verb", "Label du verbe", "placeable.examine");
        Text(form, "placeable.event", "Evenement OnUsed", "placeable-used");
        Number(form, "placeable.distance", "Distance", .1, 100, 2.5, .1);
        Number(form, "placeable.angle", "Angle", 1, 360, 120, 1);
        Toggle(form, "placeable.focus", "Focus requis", true);
        Number(form, "placeable.duration", "Duree", 0, 3600, 1, .1);
        Text(form, "placeable.authority", "Autorite", "server");
        Text(form, "placeable.conditions", "Conditions", "quest:crypt-open");
        Toggle(form, "placeable.collision", "Collision active", true);
        Toggle(form, "placeable.navObstacle", "Obstacle navigation", true);
        Text(form, "placeable.script", "Script OnUsed", "scripts/placeable_used");
        Section(form, "TRANSFORM PRECIS", "Coordonnees numeriques, pivot, snap, parentage, surface et animation partagent le document canonique.");
        Number(form, "placeable.positionX", "Position X", -1000000, 1000000, 0, .05);
        Number(form, "placeable.positionY", "Position Y", -1000000, 1000000, 0, .05);
        Number(form, "placeable.positionZ", "Position Z", -1000000, 1000000, 0, .05);
        Number(form, "placeable.rotationX", "Rotation X", -36000, 36000, 0, 1);
        Number(form, "placeable.rotationY", "Rotation Y", -36000, 36000, 45, 1);
        Number(form, "placeable.rotationZ", "Rotation Z", -36000, 36000, 0, 1);
        Number(form, "placeable.scaleX", "Echelle X", .01, 100, 1, .05);
        Number(form, "placeable.scaleY", "Echelle Y", .01, 100, 1, .05);
        Number(form, "placeable.scaleZ", "Echelle Z", .01, 100, 1, .05);
        Number(form, "placeable.pivotX", "Pivot X", -1000000, 1000000, 0, .05);
        Number(form, "placeable.pivotY", "Pivot Y", -1000000, 1000000, .5, .05);
        Number(form, "placeable.pivotZ", "Pivot Z", -1000000, 1000000, 0, .05);
        Number(form, "placeable.snap", "Snap", 0, 10000, .25, .05);
        Text(form, "placeable.parent", "Parent UUID optionnel", string.Empty);
        Toggle(form, "placeable.alignSurface", "Aligner sur la surface", true);
        Text(form, "placeable.transformAnimation", "Animation de transform", "animation:crypt-pillar-rise");
        Text(form, "placeable.overrides", "Overrides explicites", "material=ash-stone,variant=lit");
        Info(form, "Le viewport affiche pivot et snap ; Previsualiser n'ecrit rien, Appliquer conserve undo/redo.");
        Section(form, "SOCIAL, EVENEMENTS ET PHYSIQUE", "Les comportements monde vivant restent dans le meme profil canonique.");
        Text(form, "placeable.faction", "Faction / ownership", "ash-watch");
        Text(form, "placeable.dialogue", "Dialogue lié (UUID)", string.Empty);
        Text(form, "placeable.key", "Credential / clé", "key:ember");
        Choice(form, "placeable.consume", "Politique de clé", ["Conserver", "Consommer", "Charges", "Partagée", "Permission virtuelle"]);
        Text(form, "placeable.attack", "Script attaque physique", "scripts/placeable_attack");
        Text(form, "placeable.ability", "Script capacité reçue", "scripts/placeable_ability");
        Text(form, "placeable.custom", "Namespace événement utilisateur", "placeable.ash");
        Number(form, "placeable.interval", "Heartbeat (secondes)", 0.1, 3600, 5, .5);
        Toggle(form, "placeable.physics", "Physique dynamique et réplication serveur", true);
        Number(form, "placeable.mass", "Masse", .1, 100000, 12, .5);
    }

    private void BuildDoors(VBoxContainer form)
    {
        Target(form, "door.target", "Blueprint porte");
        Choice(form, "door.placement", "Placement", ["Socket architecture", "Placement libre"]);
        Text(form, "door.socket", "Socket architecture", "socket:crypt-arch");
        Section(form, "NAVIGATION DYNAMIQUE", "Obstacle et lien suivent l'état et la progression de l'animation.");
        Text(form, "door.navObstacle", "Obstacle navigation", "nav-obstacle:crypt-door");
        Text(form, "door.navLink", "Lien navigation", "nav-link:crypt-door");
        Number(form, "door.openingPassable", "Ouverture praticable à", 0, 1, .75, .05);
        Number(form, "door.closingBlocked", "Fermeture bloquante à", 0, 1, .25, .05);
        Toggle(form, "door.navVisualize", "Visualiser obstacle et lien dans le viewport", true);
        Choice(form, "door.portalKind", "Destination typée", ["Porte", "Trigger", "Waypoint", "Point de départ"]);
        Text(form, "door.spawnAnchor", "Ancre de destination", "anchor:crypt-exit");
        Text(form, "door.permission", "Permission de transition", "portal.travel");
        Toggle(form, "door.oneWay", "Transition unidirectionnelle", false);
        Number(form, "door.health", "Points de structure", 1, 100000, 40, 1);
        Number(form, "door.hardness", "Dureté", 0, 10000, 4, .5);
        Text(form, "door.loading", "Image de chargement gouvernée", "assets/loading/crypt-of-ashes.png");
        Choice(form, "door.assembly", "Assemblage", ["Simple", "Double", "Herse", "Ascenseur", "Sas"]);
        Text(form, "door.faction", "Faction / ownership", "ash-watch");
        Text(form, "door.dialogue", "Dialogue lié (UUID)", string.Empty);
        Text(form, "door.damageScript", "Script dégâts / destruction", "scripts/door_damage");
        Text(form, "door.attackScript", "Script attaque physique", "scripts/door_attack");
        Text(form, "door.abilityScript", "Script capacité reçue", "scripts/door_ability");
        Text(form, "door.custom", "Namespace événement utilisateur", "door.ash");
        Section(form, "VERROU PARTAGÉ", "Credentials, compétence, reverrouillage et consommation utilisent le même contrat que les placeables.");
        Toggle(form, "door.accessRequired", "Accès et événements requis au runtime", true);
        Text(form, "door.accessAsset", "Asset de porte", "assets/doors/ash-gate.glb");
        Number(form, "door.accessX", "Position X", -100000, 100000, 0, .25);
        Number(form, "door.accessY", "Position Y", -100000, 100000, 0, .25);
        Number(form, "door.accessZ", "Position Z", -100000, 100000, 0, .25);
        Number(form, "door.accessScale", "Échelle uniforme", .01, 1000, 1, .05);
        Choice(form, "door.lockState", "État du verrou", ["Déverrouillé", "Verrouillé", "Enrayé", "Brisé"]);
        Text(form, "door.credentials", "Credentials / clés", "key:ember");
        Text(form, "door.lockSkill", "Compétence", "mechanics");
        Number(form, "door.lockDifficulty", "Difficulté", 0, 100000, 12, 1);
        Text(form, "door.lockPermissions", "Permissions", "door.unlock");
        Toggle(form, "door.consumeCredential", "Consommer la clé utilisée", true);
        Toggle(form, "door.relockable", "Reverrouillage autorisé", true);
        Toggle(form, "door.hackable", "Piratage / crochetage autorisé", true);
        Toggle(form, "door.canJam", "Le verrou peut s’enrayer", true);
        Section(form, "ÉCHEC D’OUVERTURE", "Reason code, textes localisés, son, animation et script restent liés au même événement typé.");
        Text(form, "door.rejectionFr", "Texte fr-FR", "La porte reste verrouillée.");
        Text(form, "door.rejectionEn", "Texte en-US", "The door remains locked.");
        Text(form, "door.rejectionSound", "Son", "audio:door-locked");
        Text(form, "door.rejectionAnimation", "Animation", "animation:rattle");
        Text(form, "door.rejectionScript", "Script", "scripts/door_open_rejected");
        Section(form, "PIÈGE DE PORTE", "Détection, désamorçage, payload, reset, ownership et filtre côté/événement sont prévisualisables.");
        Text(form, "door.trapTrigger", "Trigger", "door:open");
        Text(form, "door.trapPayload", "Payload", "damage:ember-burst");
        Number(form, "door.trapDetection", "Difficulté de détection", 0, 100000, 10, 1);
        Number(form, "door.trapDisarm", "Difficulté de désamorçage", 0, 100000, 12, 1);
        Number(form, "door.trapReset", "Reset (secondes)", 0, 86400, 30, 1);
        Text(form, "door.trapOwner", "Propriétaire", "ash-watch");
        Text(form, "door.trapVfx", "Effet visuel", "vfx:ember-burst");
        Text(form, "door.trapFriendly", "Faction ignorée", "ash-watch");
        Text(form, "door.trapSides", "Côtés", "front,back");
        Text(form, "door.trapEvents", "Événements", "interact,portal-use-requested");
        Toggle(form, "door.trapArmed", "Piège armé", true);
        Toggle(form, "door.trapOneShot", "Usage unique", false);
        Section(form, "PORTAIL ET ÉTATS", "Les événements avant/après sont bornés aux cycles utiles et émis après transition terminale.");
        Text(form, "door.apiVersion", "Version ForgeScript", "1.0.0");
        Text(form, "door.portalRequested", "Avant transition", "PortalUseRequested");
        Text(form, "door.portalCompleted", "Après transition", "PortalUseCompleted");
        Text(form, "door.openedBinding", "Porte ouverte", "DoorOpened");
        Text(form, "door.closedBinding", "Porte fermée", "DoorClosed");
        Section(form, "ÉVÉNEMENTS DE VERROU", "Méthode, acteur et credential accompagnent chaque transition terminale verrouillée/déverrouillée.");
        Toggle(form, "door.lockEventsRequired", "Événements de verrou requis au runtime", true);
        Text(form, "door.lockedBinding", "Porte verrouillée", "DoorLocked");
        Text(form, "door.unlockedBinding", "Porte déverrouillée", "DoorUnlocked");
        Text(form, "door.accessOverrides", "Overrides explicites", "failure.priority=lock-before-trap");
        Info(form, "Le viewport conserve le placement canonique ; Prévisualiser n’écrit rien et Appliquer reste annulable.");
    }

    private void BuildTriggers(VBoxContainer form)
    {
        Target(form, "trigger.target", "Blueprint trigger");
        Target(form, "trigger.area", "Zone propriétaire");
        Section(form, "VOLUME CANONIQUE", "Polygone 2D extrude ou forme 3D, sommets, trous, snap, hauteur, transform et overlay runtime.");
        Toggle(form, "trigger.foundationRequired", "Fondation requise au runtime", true);
        Text(form, "trigger.slug", "Identifiant lisible", "crypt-threshold");
        Choice(form, "trigger.shape", "Forme", ["Polygone 2D extrude", "Boite 3D", "Sphere 3D", "Capsule 3D", "Enveloppe convexe 3D"]);
        Number(form, "trigger.height", "Hauteur", .1, 10000, 3, .25);
        Number(form, "trigger.snap", "Pas de grille", 0, 1000, .25, .05);
        Toggle(form, "trigger.visualize", "Afficher l'overlay dans le viewport", true);
        Section(form, "VALIDATION PRE-BUILD", "Auto-intersections, references, scripts, portail et accessibilite sont controles sur le document canonique.");
        Toggle(form, "trigger.validationRequired", "Contexte de validation requis au runtime", true);
        Text(form, "trigger.knownTargets", "Cibles resolues", "encounter:crypt-warden");
        Text(form, "trigger.knownScripts", "Scripts resolus", "scripts/trigger_click,scripts/trigger_enter,scripts/trigger_exit");
        Text(form, "trigger.accessibleLayers", "Layers accessibles", "characters");
        Section(form, "FILTRES, ACTIONS ET TRANSITION", "Entites, tags, layers, conditions, cooldown, fire policy et actions typees.");
        Text(form, "trigger.entityKinds", "Types d'entite", "creature");
        Text(form, "trigger.requiredTags", "Tags requis", "player");
        Text(form, "trigger.excludedTags", "Tags exclus", "undead");
        Text(form, "trigger.layers", "Layers", "characters");
        Text(form, "trigger.conditions", "Conditions", "crypt.open");
        Number(form, "trigger.cooldown", "Cooldown (secondes)", 0, 86400, 5, .5);
        Choice(form, "trigger.fire", "Politique de tir", ["Une fois", "Une fois par entite", "Repeter", "Pendant la presence"]);
        Text(form, "trigger.actionKind", "Action typee", "start-encounter");
        Text(form, "trigger.actionTarget", "Cible de l'action", "encounter:crypt-warden");
        Toggle(form, "trigger.portal", "Transition inter-zone", true);
        Choice(form, "trigger.portalKind", "Destination typee", ["Porte", "Trigger", "Waypoint", "Point de depart"]);
        Text(form, "trigger.portalAnchor", "Ancre d'arrivee", "anchor:crypt-exit");
        Section(form, "EVENEMENTS FORGESCRIPT", "VolumeEntered transporte entite, point, direction, layers et state ; VolumeExited ajoute duree et raison.");
        Text(form, "trigger.apiVersion", "Version API ForgeScript", "1.0.0");
        Text(form, "trigger.onEnterScript", "Script apres OnEnter", "scripts/trigger_enter");
        Text(form, "trigger.onExitScript", "Script apres OnExit", "scripts/trigger_exit");
        Number(form, "trigger.discovery", "Difficulté de découverte", 0, 100, 10, 1);
        Text(form, "trigger.key", "Credential requis", "key:ember");
        Text(form, "trigger.click", "Script OnClick", "scripts/trigger_click");
        Number(form, "trigger.interval", "Heartbeat occupé (secondes)", .1, 3600, 2, .5);
        Text(form, "trigger.custom", "Namespace événement utilisateur", "trigger.ash");
        Toggle(form, "trigger.rest", "Volume de repos sûr", true);
        Text(form, "trigger.quest", "Quête de piste (UUID)", string.Empty);
        Text(form, "trigger.dialogue", "Dialogue d’interjection (UUID)", string.Empty);
        Toggle(form, "trigger.director", "Autoriser simulation manuelle auditée", true);
        Section(form, "R096 · PIÈGE DE VOLUME",
            "Détection, désarmement, payload, reset, ownership et visualisation MJ restent canoniques.");
        Toggle(form, "trigger.advancedRequired", "Capacités avancées requises au runtime", true);
        Text(form, "trigger.trapTrigger", "Déclencheur du piège", "enter");
        Text(form, "trigger.trapPayload", "Payload", "damage:ember-burst");
        Number(form, "trigger.trapDetection", "Difficulté de détection", 0, 1000000, 10, 1);
        Number(form, "trigger.trapDisarm", "Difficulté de désarmement", 0, 1000000, 12, 1);
        Number(form, "trigger.trapReset", "Délai de reset", 0, 1000000, 30, 1);
        Text(form, "trigger.trapOwner", "Propriétaire", "ash-watch");
        Text(form, "trigger.trapVfx", "Effet visuel", "vfx:ember-burst");
        Text(form, "trigger.trapFriendly", "Politique amicale", "ash-watch");
        Text(form, "trigger.trapColor", "Couleur overlay MJ", "#ff7a45");
        Toggle(form, "trigger.trapArmed", "Piège armé", true);
        Toggle(form, "trigger.trapOneShot", "Usage unique", false);
        Toggle(form, "trigger.trapGm", "Visualisation MJ", true);
        Text(form, "trigger.trapBindings", "Bindings détecté/désarmé/tiré/reset",
            "TrapDetected,TrapDisarmed,TrapTriggered,TrapReset");
        Section(form, "REQUÊTE DE CIBLE TYPÉE",
            "Joueurs, tags, factions, composants, propriétaire, équipe et prédicat sandbox.");
        Toggle(form, "trigger.queryPlayers", "Joueurs uniquement", true);
        Text(form, "trigger.queryRequired", "Tags requis", "player");
        Text(form, "trigger.queryExcluded", "Tags exclus", "undead");
        Text(form, "trigger.queryFactions", "Factions (UUID)", "11111111-1111-4111-8111-111111111111");
        Text(form, "trigger.queryComponents", "Composants requis", "inventory,health");
        Text(form, "trigger.queryOwner", "Propriétaire", "party");
        Text(form, "trigger.queryTeam", "Équipe", "heroes");
        Text(form, "trigger.queryPredicate", "Prédicat sandbox", "quest==active");
        Section(form, "BIBLIOTHÈQUE DE 10 VOLUMES",
            "Damage, Heal, Gravity, Water, Climb, NoBuild, Camera, Audio, Weather et Checkpoint.");
        Number(form, "trigger.volumeDamage", "Damage · santé", -100000, 100000, -10, 1);
        Number(form, "trigger.volumeHeal", "Heal · santé", -100000, 100000, 10, 1);
        Number(form, "trigger.volumeGravity", "Gravity · facteur", -1000, 1000, .5, .1);
        Number(form, "trigger.volumeWater", "Water · mouvement", -1000, 1000, .6, .1);
        Number(form, "trigger.volumeClimb", "Climb · autorisé", 0, 1, 1, 1);
        Number(form, "trigger.volumeNoBuild", "NoBuild · construction", 0, 1, 0, 1);
        Number(form, "trigger.volumeCamera", "Camera · priorité", 0, 1000, 1, 1);
        Number(form, "trigger.volumeAudio", "Audio · intensité", 0, 1000, 1, .1);
        Number(form, "trigger.volumeWeather", "Weather · intensité", 0, 1000, 1, .1);
        Number(form, "trigger.volumeCheckpoint", "Checkpoint · actif", 0, 1, 1, 1);
        Section(form, "OVERLAY D’OCCUPATION",
            "Occupants, derniers tirs, cooldowns, filtres et trace d’événements bornée.");
        Toggle(form, "trigger.debugOccupants", "Afficher les occupants", true);
        Toggle(form, "trigger.debugFires", "Afficher les derniers tirs", true);
        Toggle(form, "trigger.debugCooldowns", "Afficher les cooldowns", true);
        Toggle(form, "trigger.debugFilters", "Afficher les filtres", true);
        Toggle(form, "trigger.debugTrace", "Afficher la trace", true);
        Number(form, "trigger.debugMaximum", "Entrées de trace maximales", 1, 1000, 100, 1);
        Info(form, "Prévisualiser n’écrit rien ; Appliquer reste atomique, annulable et persistant.");
    }

    private void BuildEncounters(VBoxContainer form)
    {
        Target(form, "encounter.area", "Zone de rencontre");
        Section(form, "ASSISTANT DE RENCONTRE",
            "Rôle, thème, difficulté, groupes, budget, activation et distribution partagent un contrat canonique.");
        Toggle(form, "encounter.compositionRequired", "Composition P1 requise au runtime", true);
        Text(form, "encounter.slug", "Identifiant lisible", "ash-warden-ambush");
        Text(form, "encounter.displayName", "Nom affiché", "Embuscade du gardien");
        Text(form, "encounter.category", "Catégorie", "ambush");
        Text(form, "encounter.theme", "Thème", "ash-crypt");
        Section(form, "DIFFICULTÉ & BUDGET",
            "Le ruleset et le fournisseur d'évaluation du groupe restent interchangeables et explicites.");
        Choice(form, "encounter.difficultyBand", "Difficulté",
            ["Narrative", "Facile", "Standard", "Difficile", "Extrême", "Personnalisée"]);
        Text(form, "encounter.ruleset", "Ruleset", "ruleset:forge");
        Text(form, "encounter.partyProvider", "Évaluateur de groupe", "party:forge-generic");
        Number(form, "encounter.partySize", "Taille de groupe estimée", 1, 128, 4, 1);
        Number(form, "encounter.partyPower", "Puissance estimée", 0, 100000, 12, .5);
        Number(form, "encounter.baseBudget", "Budget de base", .1, 100000, 20, .5);
        Number(form, "encounter.partyMultiplier", "Multiplicateur de taille", .01, 100, 1.2, .05);
        Number(form, "encounter.difficultyMultiplier", "Multiplicateur de difficulté", .01, 100, 1.5, .05);
        Section(form, "GROUPE & SÉLECTION",
            "Les minimums, maximums et candidats sont validés contre le budget et le plafond serveur.");
        Text(form, "encounter.groupSlug", "Identifiant du groupe", "wave:warden");
        Number(form, "encounter.groupMinimum", "Nombre minimum", 0, 10000, 2, 1);
        Number(form, "encounter.groupMaximum", "Nombre maximum", 0, 10000, 4, 1);
        Number(form, "encounter.groupBudget", "Budget du groupe", .1, 100000, 36, .5);
        Text(form, "encounter.selectionStrategy", "Stratégie de sélection", "challenge-rating");
        Number(form, "encounter.selectionSeed", "Graine déterministe", int.MinValue, int.MaxValue, 42, 1);
        Toggle(form, "encounter.previewComposition", "Prévisualiser la composition", true);
        Section(form, "ACTIVATION & DISTRIBUTION",
            "Usage unique, répétition, minuterie, vagues, persistance, population maintenue ou script.");
        Choice(form, "encounter.activationMode", "Mode d'activation",
            ["Une fois", "Répéter", "Minuterie", "Vagues", "Persistante", "Population maintenue", "Script"]);
        Number(form, "encounter.activationInterval", "Intervalle d'activation (secondes)", 0, 86400, 30, .5);
        Text(form, "encounter.activationPolicyScript", "Script de politique", string.Empty);
        Choice(form, "encounter.distribution", "Distribution", ["Séquentielle", "Aléatoire", "Pondérée", "Régulière"]);
        Text(form, "encounter.compositionOverrides", "Overrides explicites", "selection.fallback=lowest-cost");
        Section(form, "POINT DE SPAWN MANUEL",
            "Transform, capacite, navigation, visibilite et fallback sont persistés et validés avant le build.");
        Toggle(form, "encounter.scenarioRequired", "Scenario P1 requis au runtime", true);
        Text(form, "encounter.anchorId", "Ancre canonique", "anchor:crypt-north");
        Number(form, "encounter.anchorPositionX", "Position X", -1000000, 1000000, 2, .25);
        Number(form, "encounter.anchorPositionY", "Position Y", -1000000, 1000000, 0, .25);
        Number(form, "encounter.anchorPositionZ", "Position Z", -1000000, 1000000, 4, .25);
        Number(form, "encounter.anchorCapacity", "Capacite", 1, 10000, 8, 1);
        Toggle(form, "encounter.anchorNavigable", "Navigation validee", true);
        Choice(form, "encounter.anchorVisibility", "Visibilite",
            ["Cache", "Derriere couverture", "Portail", "Effet", "Visible autorise"]);
        Toggle(form, "encounter.anchorFallback", "Ancre de repli", true);
        Number(form, "encounter.anchorWeight", "Poids de distribution", 0, 100000, 10, .5);
        Section(form, "RESTRICTIONS JOUEUR & AUTORITE",
            "La requete distingue joueur, invocation, compagnon, MJ, spectateur et role reseau.");
        Toggle(form, "encounter.playersOnly", "Joueurs uniquement", true);
        Toggle(form, "encounter.ignoreSummons", "Ignorer les invocations", true);
        Toggle(form, "encounter.ignoreCompanions", "Ignorer les compagnons", true);
        Toggle(form, "encounter.ignoreGameMasters", "Ignorer les maitres de jeu", true);
        Toggle(form, "encounter.ignoreSpectators", "Ignorer les spectateurs", true);
        Choice(form, "encounter.activatorAuthority", "Autorite requise", ["Locale", "Hote", "Serveur"]);
        Text(form, "encounter.activatorTags", "Tags requis", "party");
        Section(form, "CYCLE DE VIE FORGESCRIPT",
            "EncounterEntered, EncounterExited et EncounterExhausted exposent activateur, duree et bilan complet.");
        Text(form, "encounter.lifecycleApi", "Version API ForgeScript", "1.0.0");
        Text(form, "encounter.enteredBinding", "Binding entree", "EncounterEntered");
        Text(form, "encounter.enteredScript", "Script entree", "scripts/encounter_entered");
        Text(form, "encounter.exitedBinding", "Binding sortie", "EncounterExited");
        Text(form, "encounter.exitedScript", "Script sortie", "scripts/encounter_exited");
        Text(form, "encounter.exhaustedBinding", "Binding epuisement", "EncounterExhausted");
        Text(form, "encounter.exhaustedScript", "Script epuisement", "scripts/encounter_exhausted");
        Section(form, "PHASES & VAGUES",
            "Le graphe relie conditions, groupes, delai, objectif, voix et transition de maniere deterministe.");
        Text(form, "encounter.phaseName", "Nom de phase", "Assaut du gardien");
        Text(form, "encounter.phaseConditions", "Conditions d'entree", "area.occupied");
        Number(form, "encounter.waveDelay", "Delai vague terminale", 0, 86400, 2, .5);
        Choice(form, "encounter.waveCompletion", "Fin de vague",
            ["Tous vaincus", "Objectif accompli", "Minuterie", "Script"]);
        Text(form, "encounter.waveCompletionTarget", "Cible script", "scripts/encounter_wave_complete");
        Text(form, "encounter.waveQuest", "Quete objectif (UUID)", string.Empty);
        Text(form, "encounter.waveObjective", "Objectif", "crypt-warden-defeated");
        Text(form, "encounter.waveVoice", "Repere vocal", "audio:encounter-warden-fallen");
        Text(form, "encounter.scenarioOverrides", "Overrides scenario", "graph.layout=left-to-right");
        Section(form, "PREVISUALISATION STATISTIQUE",
            "Simuler N runs reproductibles et comparer composition, cout, duree, recompenses, variances et percentiles.");
        Toggle(form, "encounter.statisticsRequired", "Statistiques P1 requises au runtime", true);
        Number(form, "encounter.statisticsRuns", "Nombre de runs", 1, 100000, 64, 1);
        Number(form, "encounter.statisticsSeed", "Seed de base", int.MinValue, int.MaxValue, 840, 1);
        Text(form, "encounter.durationEstimator", "Estimateur de duree", "duration:actors-linear");
        Number(form, "encounter.secondsPerActor", "Secondes par acteur", .01, 100000, 5, .25);
        Text(form, "encounter.rewardEstimator", "Estimateur de recompense", "reward:cost-linear");
        Number(form, "encounter.rewardPerCost", "Recompense par cout", 0, 100000, .5, .05);
        Text(form, "encounter.statisticsPercentiles", "Percentiles", "0.5,0.9,0.99");
        Toggle(form, "encounter.statisticsDetails", "Inclure le detail des runs", true);
        Text(form, "encounter.statisticsOverrides", "Overrides statistiques", "report.format=distribution");
        Section(form, "VOLUME D'ACTIVATION", "Géométrie 2D/3D, filtres, ligne de vue et script partagent un contrat canonique.");
        Choice(form, "encounter.shape", "Forme", ["Polygone 2D extrudé", "Boîte 3D", "Sphère 3D", "Capsule 3D"]);
        Number(form, "encounter.height", "Hauteur", .5, 1000, 4, .5);
        Number(form, "encounter.radius", "Rayon", .5, 1000, 8, .5);
        Text(form, "encounter.tags", "Tags d'entités requis", "player");
        Text(form, "encounter.allowedFactions", "Factions autorisées", "party");
        Toggle(form, "encounter.lineOfSight", "Exiger une ligne de vue", true);
        Number(form, "encounter.threat", "Rayon de menace", 0, 10000, 18, .5);
        Text(form, "encounter.activationScript", "Script d'activation", "scripts/encounter_activate");
        Toggle(form, "encounter.visualize", "Visualiser le volume dans le viewport", true);
        Section(form, "POOL DE CRÉATURES", "Blueprints pondérés, rôles, coûts, conditions, variantes et limites réseau.");
        Target(form, "encounter.candidate", "Blueprint créature candidate");
        Choice(form, "encounter.role", "Rôle", ["Brute", "Escarmouche", "Contrôle", "Soutien", "Boss", "Ambiant"]);
        Number(form, "encounter.weight", "Poids", .5, 10000, 3, .5);
        Number(form, "encounter.cost", "Coût de budget", .5, 10000, 8, .5);
        Number(form, "encounter.maximumAlive", "Maximum vivant", 1, 1000, 4, 1);
        Text(form, "encounter.conditions", "Conditions", "area.occupied");
        Text(form, "encounter.variants", "Variantes", "ember");
        Number(form, "encounter.budget", "Budget de spawn", 1, 100000, 40, 1);
        Text(form, "encounter.anchors", "Ancres de spawn", "anchor:crypt-north");
        Text(form, "encounter.rewards", "Récompenses", "reward:ember-shard");
        Number(form, "encounter.networkCap", "Cap d'acteurs réseau", 1, 10000, 12, 1);
        Number(form, "encounter.respawn", "Délai de respawn (secondes)", 0, 86400, 30, 1);
        Number(form, "encounter.cycles", "Cycles maximum", 0, 100, 3, 1);
        Text(form, "encounter.faction", "Faction de spawn", "ash-hostile");
        Number(form, "encounter.interval", "Heartbeat (secondes)", .1, 3600, 5, .5);
        Number(form, "encounter.alert", "Niveau d’alerte des renforts", 0, 100, 2, 1);
        Number(form, "encounter.cap", "Cap de renforts", 1, 1000, 4, 1);
        Choice(form, "encounter.visibility", "Spawn hors vue", ["Caché", "Derrière couverture", "Portail", "Effet", "Visible autorisé"]);
        Number(form, "encounter.cleanup", "Nettoyage abandon (secondes)", 0, 86400, 120, 1);
    }

    private void BuildVehicles(VBoxContainer form)
    {
        Target(form, "vehicle.area", "Zone de route");
        Number(form, "vehicle.speed", "Limite de vitesse", .1, 500, 8, .5);
        Number(form, "vehicle.width", "Largeur de voie", .5, 100, 3, .5);
        Number(form, "vehicle.stop", "Arrêt terminal (secondes)", 0, 3600, 5, .5);
        Toggle(form, "vehicle.overtake", "Autoriser le dépassement", false);
        Choice(form, "vehicle.sync", "Synchronisation", ["Snapshots serveur", "Prédiction bornée", "Locale"]);
        Info(form, "La route initiale relie deux points éditables dans le viewport ; lanes, vitesse, arrêt et politique réseau sont persistés.");
    }

    private void BuildMerchants(VBoxContainer form)
    {
        Section(form, "STORE DEFINITION INDÉPENDANTE", "Un magasin canonique peut être référencé par plusieurs vendeurs, dialogues, interfaces et canaux.");
        Target(form, "merchant.target", "Blueprint du marchand");
        Toggle(form, "merchant.foundationRequired", "Fondation magasin P1 requise au runtime", true);
        Text(form, "merchant.storeSlug", "Slug du magasin indépendant", "crypt-vigil-market");
        Text(form, "merchant.displayKey", "Clé de nom localisée", "merchant.crypt-vigil.name");
        Text(form, "merchant.dialogue", "Dialogue du vendeur (UUID)", string.Empty);
        Toggle(form, "merchant.wizard", "Wizard : définition + vendeur + dialogue + interaction + tests", true);
        Text(form, "merchant.fixture", "Fixture de test générée", "merchant-crypt-vigil-nominal");
        Text(form, "merchant.currency", "Monnaie principale", "ember-crown");
        Section(form, "STOCK, BINDINGS ET VALIDATION", "Stock fini ou infini, variantes, fournisseurs, réapprovisionnement, ordre et conditions cachées.");
        Toggle(form, "merchant.transactionsRequired", "Transactions requises au runtime", true);
        Text(form, "merchant.vendorInteraction", "Interaction marchand", "interaction:trade");
        Text(form, "merchant.dialogueChoice", "Choix de dialogue", "choice:trade");
        Target(form, "merchant.stockItem", "Blueprint item en stock");
        Text(form, "merchant.variant", "Variante", "standard");
        Choice(form, "merchant.supply", "Politique de stock", ["Infini", "Fini", "Généré", "Fabriqué", "Serveur partagé", "Personnel"]);
        Number(form, "merchant.stockQuantity", "Quantite", 0, 1000000, 12, 1);
        Number(form, "merchant.unitPrice", "Prix unitaire", 0, 100000000, 5, 1);
        Text(form, "merchant.supplier", "Fournisseur", "supplier:ember-guild");
        Number(form, "merchant.stockRestock", "Réapprovisionnement de l'entrée (heures monde)", 0, 100000, 12, .5);
        Number(form, "merchant.displayOrder", "Ordre d'affichage", 0, 100000, 1, 1);
        Text(form, "merchant.hiddenConditions", "Conditions de masquage", "quest:market-closed");
        Text(form, "merchant.locales", "Locales", "fr-FR,en-US");
        Text(form, "merchant.restrictions", "Restrictions", "quest:merchant-open");
        Text(form, "merchant.onOpen", "Script onOpen", "scripts/merchant_open");
        Number(form, "merchant.buyerCapacity", "Capacite acheteur", 1, 1000000, 100, 1);
        Number(form, "merchant.sellerCapacity", "Capacite vendeur", 1, 1000000, 1000, 1);
        Section(form, "MARKUP, MARKDOWN & COURBES", "Prix d'achat/vente par courbes, monnaie, réputation, rareté, état et négociation.");
        Number(form, "merchant.buyMultiplier", "Markup d'achat", 0, 100, 1.2, .05);
        Number(form, "merchant.sellMultiplier", "Markdown de vente", 0, 100, .6, .05);
        Text(form, "merchant.buyCurve", "Courbe achat (valeur:multiplicateur)", "0:1,100:1.1");
        Text(form, "merchant.sellCurve", "Courbe vente (valeur:multiplicateur)", "0:1,100:0.9");
        Text(form, "merchant.currencyRates", "Taux de monnaies", "ember-crown=1");
        Number(form, "merchant.reputationFactor", "Facteur réputation", 0, 10, .1, .05);
        Number(form, "merchant.scarcityFactor", "Facteur rareté", 0, 10, .2, .05);
        Number(form, "merchant.conditionFactor", "Facteur état", 0, 10, .2, .05);
        Number(form, "merchant.negotiationFactor", "Facteur négociation", 0, 10, .1, .05);
        Number(form, "merchant.minimumPrice", "Prix minimal", 0, 100000000, 1, 1);
        Number(form, "merchant.maximumPrice", "Prix maximal", 0, 100000000, 500, 1);
        Section(form, "WHITELIST & BLACKLIST", "Filtrer type, tags, qualité, provenance, état, propriétaire et règle personnalisée.");
        Text(form, "merchant.whitelistTypes", "Types autorisés", "relic,consumable");
        Text(form, "merchant.whitelistTags", "Tags requis", "merchant-approved");
        Text(form, "merchant.blacklistTags", "Tags interdits", "stolen,cursed");
        Number(form, "merchant.minimumQuality", "Qualité minimale", 0, 100, 1, 1);
        Number(form, "merchant.maximumQuality", "Qualité maximale", 0, 100, 5, 1);
        Text(form, "merchant.provenances", "Provenances autorisées", "author:forge");
        Number(form, "merchant.minimumCondition", "État minimal", 0, 1, .5, .05);
        Text(form, "merchant.owner", "Propriétaire requis", string.Empty);
        Text(form, "merchant.customRule", "Règle ForgeScript personnalisée", "scripts/merchant_rule.forge");
        Section(form, "SESSIONS ONOPEN & ONCLOSE", "Émettre des événements typés avec client, vendeur, contexte de prix et bilan transactionnel.");
        Toggle(form, "merchant.experienceRequired", "Expérience boutique P1 requise au runtime", true);
        Text(form, "merchant.apiVersion", "Version API ForgeScript", "1.0.0");
        Text(form, "merchant.onOpened", "Binding ouverture", "StoreSessionOpened");
        Text(form, "merchant.onClosed", "Binding fermeture", "StoreSessionClosed");
        Toggle(form, "merchant.includeCustomer", "Inclure le client", true);
        Toggle(form, "merchant.includeVendor", "Inclure le vendeur", true);
        Toggle(form, "merchant.includePriceContext", "Inclure le contexte de prix", true);
        Toggle(form, "merchant.includeTransactionSummary", "Inclure le bilan transactionnel", true);
        Section(form, "INTERFACE DE BOUTIQUE", "Navigation clavier/manette, comparaison, filtres, panier, confirmation et accessibilité.");
        Toggle(form, "merchant.uiKeyboard", "Navigation clavier", true);
        Toggle(form, "merchant.uiGamepad", "Navigation manette", true);
        Toggle(form, "merchant.uiCompare", "Comparaison d'objets", true);
        Toggle(form, "merchant.uiFilters", "Filtres de catalogue", true);
        Toggle(form, "merchant.uiCart", "Panier", true);
        Toggle(form, "merchant.uiConfirmation", "Confirmation avant achat", true);
        Toggle(form, "merchant.uiScreenReader", "Labels lecteur d'écran", true);
        Toggle(form, "merchant.uiHighContrast", "Contraste élevé", true);
        Text(form, "merchant.uiColumns", "Colonnes configurables", "name,compare,price,quantity");
        Section(form, "TRANSACTIONS ATOMIQUES", "Valider fonds, propriété, stock et capacité avant une mutation unique avec rollback exact.");
        Info(form, "Previsualiser n'ecrit rien ; Appliquer passe par la commande canonique et conserve undo/redo.");
        Number(form, "merchant.balance", "Trésorerie", 0, 100000000, 1000, 10);
        Number(form, "merchant.cap", "Prix maximal d’achat", 0, 100000000, 500, 10);
        Toggle(form, "merchant.stolen", "Accepter les biens volés hors juridiction", false);
        Number(form, "merchant.restock", "Réapprovisionnement (heures monde)", .1, 10000, 24, 1);
        Number(form, "merchant.discount", "Remise de négociation maximale (%)", 0, 100, 20, 1);
        Toggle(form, "merchant.market", "Marché dynamique régional", true);
        Text(form, "merchant.service", "Service vendu", "repair");
        Toggle(form, "merchant.ledger", "Journal économique et anomalies", true);
    }

    private void BuildCompanions(VBoxContainer form)
    {
        Section(form, "VALIDATION DU COMPAGNON", "Un contrat canonique unique, inspectable et testable avant le build.");
        Target(form, "companion.target", "Blueprint du compagnon");
        Toggle(form, "companion.foundationRequired", "Fondation P1 obligatoire au runtime", true);
        Toggle(form, "companion.controlRequired", "Contrôle P1 obligatoire au runtime", true);
        Toggle(form, "companion.experienceRequired", "Expérience P1 obligatoire au runtime", true);
        var preview = new PanelContainer { CustomMinimumSize = new(0, 86) };
        var previewRow = new HBoxContainer();
        previewRow.AddThemeConstantOverride("separation", 14);
        preview.AddChild(previewRow);
        previewRow.AddChild(new ColorRect { Color = new("ff9858"), CustomMinimumSize = new(58, 58) });
        var previewText = new Label
        {
            Text = "APERÇU DE RÈGLES\nRecrutement gouverné · ordres bornés · survie · suivi inter-zones",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        previewText.AddThemeColorOverride("font_color", new("dccbef"));
        previewRow.AddChild(previewText);
        form.AddChild(preview);

        Section(form, "RECRUTEMENT & PROPRIÉTÉ", "Les chemins de recrutement et l’autorité sont validés ensemble.");
        Toggle(form, "companion.recruitDialogue", "Autoriser le recrutement par dialogue", true);
        Toggle(form, "companion.recruitScript", "Autoriser le recrutement par ForgeScript", true);
        Text(form, "companion.recruitScriptId", "Script de recrutement", "scripts/companion_recruit");
        Text(form, "companion.currency", "Monnaie", "ember-crown");
        Number(form, "companion.cost", "Coût", 0, 100000000, 25, 1);
        Number(form, "companion.reputation", "Réputation minimale", -1, 1, .25, .05);
        Text(form, "companion.recruitConditions", "Conditions d’offre", "quest:crypt-open");
        Text(form, "companion.recruitConsequences", "Conséquences offre/refus", "relation:companion-increase");
        Text(form, "companion.offerNode", "Nœud d’offre", "offer.join");
        Text(form, "companion.refusalNode", "Nœud de refus", "offer.refuse");
        Text(form, "companion.departureAction", "Action de départ", "companion.depart");
        Choice(form, "companion.ownership", "Propriété", ["Groupe", "Individuelle", "Partagée", "IA seule", "Maître de jeu"]);
        Text(form, "companion.authority", "Autorité", "server");
        Toggle(form, "companion.transfer", "Autoriser le transfert de propriété", true);
        Text(form, "companion.controllers", "Contrôleurs autorisés", "player:owner");
        Text(form, "companion.allowedOwnership", "Transferts autorisés", "party,shared,game-master");
        Number(form, "companion.ownershipRevision", "Révision de propriété", 0, 1000000, 1, 1);

        Section(form, "CAPACITÉ DU GROUPE", "Compagnons, invocations, familiers, alliés temporaires, rôles et plafond serveur.");
        Number(form, "companion.companionSlots", "Slots compagnons", 1, 64, 3, 1);
        Number(form, "companion.summonSlots", "Slots invocations", 0, 64, 1, 1);
        Number(form, "companion.petSlots", "Slots familiers", 0, 64, 1, 1);
        Number(form, "companion.temporaryAllySlots", "Slots alliés temporaires", 0, 64, 1, 1);
        Number(form, "companion.serverActorLimit", "Plafond acteurs serveur", 1, 1024, 8, 1);
        Text(form, "companion.roleLimits", "Limites de rôles (Rôle:nombre)", "Support:2");

        Section(form, "COMMANDES, ÉQUIPEMENT & CYCLE DE VIE", "Les bindings sont stables, uniques et validés avant exécution.");
        Text(form, "companion.commands", "Commandes (virgules)",
            "move,follow,hold,rally,guard,interact,attack,defend,focus-fire,protect,disengage,use-ability");
        Text(form, "companion.inventory", "Inventaire", "companion.inventory");
        Choice(form, "companion.persistence", "Persistance", ["Session", "Zone", "Campagne", "Permanente"]);
        Text(form, "companion.dismissAnchor", "Ancre de renvoi", "party.safe");
        Text(form, "companion.equipment", "Politique d’équipement", "companion.standard");
        Text(form, "companion.slots", "Slots d’équipement", "main-hand,off-hand,armor");
        Choice(form, "companion.deathMode", "Politique de mort", ["Corps récupérable", "À terre", "Mort permanente", "Résurrection", "Remplacement", "Mémorial"]);
        Text(form, "companion.deathPolicy", "Binding de mort", "companion.recoverable");
        Number(form, "companion.recovery", "Récupération (secondes)", 0, 86400, 30, 1);
        Text(form, "companion.contexts", "Contextes de dialogue", "recruit,owner");

        Section(form, "ORDRES DE DÉPLACEMENT & COMBAT",
            "Move, Follow, Hold, Rally, Guard et Interact partagent l'autorité avec les ordres tactiques.");
        Text(form, "companion.movementCommands", "Ordres de déplacement",
            "move,follow,hold,rally,guard,interact");
        Text(form, "companion.waypointQuery", "Requête de waypoints", "party-waypoints");
        Text(form, "companion.tacticalCommands", "Ordres tactiques",
            "attack,defend,focus-fire,protect,disengage,use-ability");
        Text(form, "companion.stances", "Postures", "passive,defensive,balanced,aggressive");
        Text(form, "companion.combatAbilities", "Capacités tactiques", "heal,shield");
        Number(form, "companion.priorityMin", "Priorité minimale", 0, 1000, 0, 1);
        Number(form, "companion.priorityMax", "Priorité maximale", 0, 1000, 100, 1);

        Section(form, "IA AUTONOME",
            "Autonomie, risque, ressources, tir allié et rôle restent bornés par un plan gouverné.");
        Number(form, "companion.aiAutonomy", "Autonomie", 0, 1, .6, .05);
        Number(form, "companion.aiRisk", "Tolérance au risque", 0, 1, .4, .05);
        Number(form, "companion.aiResources", "Usage des ressources", 0, 1, .5, .05);
        Number(form, "companion.followDistance", "Distance de suivi", 0, 100, 3, .5);
        Toggle(form, "companion.friendlyFire", "Autoriser le tir allié", false);
        Choice(form, "companion.aiRole", "Rôle tactique",
            ["Support", "Defender", "Controller", "Skirmisher", "Minion", "Boss", "NonCombatant"]);
        Text(form, "companion.aiAuthorization", "Niveau d'autorisation", "owner-or-gm");

        Section(form, "ÉQUIPEMENT DU COMPAGNON",
            "Permissions, objets verrouillés, loadout suggéré, comparaison et propriétaire sont validés ensemble.");
        Choice(form, "companion.equipmentPermission", "Permission", ["Complet", "Équiper", "Suggérer", "Voir", "Aucun"]);
        Text(form, "companion.lockedItems", "Objets verrouillés (UUID)",
            "75000000-0000-4000-8000-000000000012");
        Text(form, "companion.suggestedLoadout", "Loadout suggéré (slot=UUID)",
            "main-hand=75000000-0000-4000-8000-000000000013");
        Toggle(form, "companion.allowCompare", "Autoriser la comparaison", true);
        Text(form, "companion.equipmentOwner", "Propriétaire de l'inventaire", "player:owner");

        Section(form, "SURVIE DU COMPAGNON",
            "À terre, mort permanente, corps récupérable, résurrection, remplacement et mémorial partagent un contrat typé.");
        Text(form, "companion.resurrectionResource", "Ressource de résurrection", "resource:ember");
        Text(form, "companion.replacementCreature", "Créature de remplacement", "creature:elyra-heir");
        Text(form, "companion.memorial", "Mémorial", "memorial:elyra");
        Toggle(form, "companion.dropInventory", "Déposer l’inventaire à la mort", true);

        Section(form, "COHÉSION & RATTRAPAGE",
            "Navigation, suivi de portail puis téléportation sûre utilisent des seuils ordonnés et un audit.");
        Number(form, "companion.navigateThreshold", "Seuil navigation (m)", 0, 10000, 10, 1);
        Number(form, "companion.portalThreshold", "Seuil portail (m)", 0, 10000, 30, 1);
        Number(form, "companion.teleportThreshold", "Seuil téléportation (m)", 0, 10000, 60, 1);
        Text(form, "companion.safeAnchorTags", "Tags d’ancres sûres", "party.safe");

        Section(form, "HUD DU GROUPE",
            "Portraits, ressources, statuts, commandes rapides et réordonnancement restent accessibles au clavier et à la manette.");
        Toggle(form, "companion.hud.portraits", "Portraits", true);
        Toggle(form, "companion.hud.health", "Santé", true);
        Toggle(form, "companion.hud.resources", "Ressources", true);
        Toggle(form, "companion.hud.statuses", "Statuts", true);
        Toggle(form, "companion.hud.quickCommands", "Commandes rapides", true);
        Toggle(form, "companion.hud.reorder", "Réordonner le groupe", true);
        Toggle(form, "companion.hud.keyboard", "Navigation clavier", true);
        Toggle(form, "companion.hud.gamepad", "Navigation manette", true);
        Toggle(form, "companion.hud.screenReader", "Libellés lecteur d’écran", true);
        Toggle(form, "companion.hud.highContrast", "Contraste élevé", true);
        Text(form, "companion.hud.resources", "Ressources visibles", "health,focus");

        Section(form, "NAVIGATION INTER-ZONES", "Les destinations doivent appartenir au graphe de portails ou disposer d’un repli sûr.");
        Toggle(form, "companion.crossZone", "Activer le suivi inter-zones", false);
        Text(form, "companion.areas", "Zones autorisées (UUID, virgules)", string.Empty);
        Toggle(form, "companion.followPortals", "Suivre les portails authored", true);
        Toggle(form, "companion.safeFallback", "Autoriser un repli sûr", true);
        Text(form, "companion.fallbackAnchor", "Ancre de repli", "party.safe");
        Number(form, "companion.timeout", "Timeout de navigation (secondes)", .1, 3600, 15, .5);
        Toggle(form, "companion.required", "Validation obligatoire au runtime", true);

        Section(form, "SOCIAL, FORMATION & PROGRESSION", "Les comportements déjà livrés restent dans le même profil canonique.");
        Text(form, "companion.dialogue", "Dialogue social / banter (UUID)", string.Empty);
        Text(form, "companion.formation", "Formation", "wedge");
        Number(form, "companion.slot", "Slot de formation", 0, 32, 1, 1);
        Number(form, "companion.level", "Niveau actuel", 1, 100, 4, 1);
        Number(form, "companion.loyalty", "Loyauté", -1, 1, .5, .05);
        Text(form, "companion.quest", "Quête personnelle (UUID)", string.Empty);
        Toggle(form, "companion.traps", "Détecter et désamorcer les pièges", true);
        Text(form, "companion.abilities", "Capacités autorisées (virgules)", "heal,shield");
        Toggle(form, "companion.direct", "Contrôle direct / possession", true);
        Toggle(form, "companion.split", "Réunion du groupe et synchro des quêtes", true);
    }

    private void BuildScripts(VBoxContainer form)
    {
        Text(form, "script.rig", "Rig caméra", "rig.dialogue");
        Text(form, "script.shot", "Plan de cutscene", "shot.door");
        Number(form, "script.duration", "Durée du plan", .1, 3600, 3, .1);
        Toggle(form, "script.lock", "Verrouiller le contrôle joueur pendant le plan", true);
        Text(form, "script.tag", "Tag explicite de blueprint", "door.ash");
        Text(form, "script.event", "Événement routé", "opened");
        Text(form, "script.route", "Script appelé", "scripts/door_ash_opened.forge");
        Text(form, "script.flag", "Feature flag nommé", "rest.enabled");
        Toggle(form, "script.flagDefault", "Valeur par défaut du flag", true);
        Text(form, "script.ai", "Réglage IA nommé", "combat.aggression");
        Choice(form, "script.aiDefault", "Valeur IA par défaut", ["Passive", "Équilibrée", "Agressive"]);
    }

    private WorldFeatureCatalog BuildCatalog()
    {
        var state = _load?.Invoke() ?? throw new InvalidOperationException("Aucun projet Forge ouvert.");
        var current = state.Project.Module.WorldFeatures;
        var moduleId = state.Project.Module.Id.Value;
        var placeables = current.Placeables;
        if (SelectedBlueprint("placeable.target") is { } placeableId)
        {
            var consumption = (PlaceableWorldProfile.CredentialConsumption)_choice["placeable.consume"].Selected;
            var staticRole = _choice["placeable.role"].Selected == 1;
            var foundation = new PlaceableWorldProfile.FoundationPolicy(
                Stable(moduleId, $"placeable-foundation:{placeableId}"), 1, "toolset:r046",
                _text["placeable.slug"].Text.Trim(),
                [new(_text["placeable.locale"].Text.Trim(), _text["placeable.name"].Text.Trim(),
                    _text["placeable.prompt"].Text.Trim())], Csv(_text["placeable.tags"].Text),
                _text["placeable.notes"].Text.Trim(),
                new(_text["placeable.asset"].Text.Trim(), [_text["placeable.mesh"].Text.Trim()], [],
                    _text["placeable.icon"].Text.Trim(), _text["placeable.portrait"].Text.Trim(), []),
                new("box", new(1, 1.5f, .5f), _check["placeable.collision"].ButtonPressed,
                    _check["placeable.navObstacle"].ButtonPressed, "world"),
                staticRole ? null : new(
                    [new(Stable(moduleId, $"placeable-verb:{placeableId}"), _text["placeable.verb"].Text.Trim(),
                        _text["placeable.event"].Text.Trim(), string.Join("&&", Csv(_text["placeable.conditions"].Text)))],
                    _number["placeable.distance"].Value, _number["placeable.angle"].Value,
                    _check["placeable.focus"].ButtonPressed, _text["placeable.prompt"].Text.Trim(),
                    _number["placeable.duration"].Value, _text["placeable.authority"].Text.Trim(),
                    Csv(_text["placeable.conditions"].Text)),
                staticRole ? new(true, true, true, true, false) : null,
                new(true, _check["placeable.hasInventory"].ButtonPressed, _check["placeable.hasLock"].ButtonPressed,
                    _check["placeable.hasTrap"].ButtonPressed, _check["placeable.physics"].ButtonPressed,
                    new Dictionary<string, string> { ["onUsed"] = _text["placeable.script"].Text.Trim() }),
                new(new((float)_number["placeable.positionX"].Value, (float)_number["placeable.positionY"].Value,
                        (float)_number["placeable.positionZ"].Value),
                    new((float)_number["placeable.rotationX"].Value, (float)_number["placeable.rotationY"].Value,
                        (float)_number["placeable.rotationZ"].Value),
                    new((float)_number["placeable.scaleX"].Value, (float)_number["placeable.scaleY"].Value,
                        (float)_number["placeable.scaleZ"].Value),
                    new((float)_number["placeable.pivotX"].Value, (float)_number["placeable.pivotY"].Value,
                        (float)_number["placeable.pivotZ"].Value), _number["placeable.snap"].Value,
                    GuidOrNull(_text["placeable.parent"].Text), _check["placeable.alignSurface"].ButtonPressed,
                    Null(_text["placeable.transformAnimation"].Text)), KeyValues(_text["placeable.overrides"].Text),
                staticRole ? null : new(Stable(moduleId, $"placeable-on-used:{placeableId}"), 1, "toolset:r047",
                    _text["placeable.apiVersion"].Text.Trim(), _text["placeable.eventBinding"].Text.Trim(),
                    Null(_text["placeable.beforeScript"].Text), Null(_text["placeable.afterScript"].Text)),
                new(Csv(_text["placeable.transformAnimation"].Text), Csv(_text["placeable.locks"].Text),
                    Csv(_text["placeable.traps"].Text), Csv(_text["placeable.inventories"].Text),
                    Csv(_text["placeable.physicsProfiles"].Text), (int)_number["placeable.drawBudget"].Value,
                    (int)_number["placeable.scriptBudget"].Value),
                !staticRole && _check["placeable.onUsedRequired"].ButtonPressed);
            var stateNames = new[] { "idle", "open", "closed", "broken", "active", "custom" };
            var stateAnimations = new[]
            {
                _text["placeable.animationIdle"].Text.Trim(),
                _text["placeable.animationOpen"].Text.Trim(),
                _text["placeable.animationClosed"].Text.Trim(),
                _text["placeable.animationBroken"].Text.Trim(),
                _text["placeable.animationActive"].Text.Trim(),
                _text["placeable.animationCustom"].Text.Trim(),
            };
            var operations = new PlaceableOperationsProfile(
                Stable(moduleId, $"placeable-operations:{placeableId}"), 1, "toolset:r093",
                new(Stable(moduleId, $"placeable-container:{placeableId}"),
                    (int)_number["placeable.containerSlots"].Value,
                    _number["placeable.containerMass"].Value,
                    _text["placeable.containerOwnership"].Text.Trim(),
                    Csv(_text["placeable.containerAccess"].Text),
                    _check["placeable.containerPersistent"].ButtonPressed,
                    _text["placeable.containerLoot"].Text.Trim(),
                    _text["placeable.containerUi"].Text.Trim(),
                    _check["placeable.containerReplicated"].ButtonPressed,
                    _text["placeable.containerInventory"].Text.Trim()),
                new(Stable(moduleId, $"placeable-state-machine:{placeableId}"),
                    stateNames[_choice["placeable.initialState"].Selected],
                    stateNames.Select((stateName, index) => new PlaceableVisualStateProfile(
                        Stable(moduleId, $"placeable-state:{placeableId}:{stateName}"), stateName,
                        stateAnimations[index], $"visual:{stateName}", stateName != "broken")).ToArray(),
                    [
                        new(Stable(moduleId, $"placeable-transition:{placeableId}:open"), "closed", "open", "open",
                            _number["placeable.transitionDuration"].Value,
                            Null(_text["placeable.openCondition"].Text)),
                        new(Stable(moduleId, $"placeable-transition:{placeableId}:close"), "open", "closed", "close",
                            _number["placeable.transitionDuration"].Value, null),
                        new(Stable(moduleId, $"placeable-transition:{placeableId}:destroyed"), "closed", "broken",
                            "destroyed", .25, null),
                    ]),
                new(Stable(moduleId, $"placeable-damageable:{placeableId}"),
                    _number["placeable.healthMaximum"].Value,
                    _number["placeable.healthCurrent"].Value,
                    _number["placeable.armor"].Value,
                    new Dictionary<string, double>
                    {
                        ["fire"] = _number["placeable.fireResistance"].Value,
                        ["force"] = _number["placeable.forceResistance"].Value,
                    }, DoubleCsv(_text["placeable.damageStages"].Text),
                    _text["placeable.destructionEffect"].Text.Trim(),
                    _text["placeable.debris"].Text.Trim(),
                    _check["placeable.repairable"].ButtonPressed,
                    _number["placeable.repairCost"].Value),
                new(Stable(moduleId, $"placeable-protection:{placeableId}"),
                    Csv(_text["placeable.protectedActions"].Text),
                    Csv(_text["placeable.protectionPermissions"].Text),
                    _text["placeable.protectionFeedback"].Text.Trim(),
                    _check["placeable.gmOverride"].ButtonPressed,
                    _text["placeable.protectionAudit"].Text.Trim()),
                new(Stable(moduleId, $"placeable-lock:{placeableId}"),
                    (PlaceableAuthoredLockState)_choice["placeable.lockState"].Selected,
                    Csv(_text["placeable.lockCredentials"].Text),
                    Null(_text["placeable.lockSkill"].Text),
                    _number["placeable.lockDifficulty"].Value,
                    Null(_text["placeable.lockCode"].Text),
                    _check["placeable.lockHackable"].ButtonPressed,
                    _check["placeable.lockRelockable"].ButtonPressed,
                    _check["placeable.lockCanJam"].ButtonPressed,
                    Csv(_text["placeable.lockPermissions"].Text)));
            var lootItemId = SelectedBlueprint("placeable.lootItem");
            var securityEvents = lootItemId is null ? null : new PlaceableSecurityEventsProfile(
                Stable(moduleId, $"placeable-security-events:{placeableId}"), 1, "toolset:r094",
                new(Stable(moduleId, $"placeable-trap:{placeableId}"),
                    _text["placeable.trapTrigger"].Text.Trim(),
                    _text["placeable.trapPayload"].Text.Trim(),
                    _number["placeable.trapDetection"].Value,
                    _number["placeable.trapDisarm"].Value,
                    _number["placeable.trapReset"].Value,
                    _text["placeable.trapOwner"].Text.Trim(),
                    _text["placeable.trapVfx"].Text.Trim(),
                    _text["placeable.trapFriendly"].Text.Trim(),
                    _check["placeable.trapArmed"].ButtonPressed,
                    _check["placeable.trapOneShot"].ButtonPressed),
                new(Stable(moduleId, $"placeable-loot-spawner:{placeableId}"),
                    Stable(moduleId, $"placeable-loot-table:{placeableId}"),
                    (PlaceableAuthoredLootSpawnTiming)_choice["placeable.lootTiming"].Selected,
                    (int)_number["placeable.lootSeed"].Value,
                    _check["placeable.lootPersistent"].ButtonPressed, [], 0,
                    new(Stable(moduleId, $"placeable-loot-table:{placeableId}"), 1, "toolset:r094",
                        [new(lootItemId.Value, _text["placeable.lootGroup"].Text.Trim(),
                            _number["placeable.lootWeight"].Value,
                            (int)_number["placeable.lootMinimum"].Value,
                            (int)_number["placeable.lootMaximum"].Value,
                            (int)_number["placeable.lootQuality"].Value,
                            _text["placeable.lootCondition"].Text.Trim(), true)])),
                new(Stable(moduleId, $"placeable-container-events:{placeableId}"),
                    _text["placeable.lifecycleApi"].Text.Trim(),
                    _text["placeable.openedBinding"].Text.Trim(),
                    _text["placeable.closedBinding"].Text.Trim(),
                    _check["placeable.containerEventActor"].ButtonPressed,
                    _check["placeable.containerEventResult"].ButtonPressed,
                    _check["placeable.containerEventPrevious"].ButtonPressed,
                    _check["placeable.containerEventAuthority"].ButtonPressed),
                new(Stable(moduleId, $"placeable-lock-events:{placeableId}"),
                    _text["placeable.lifecycleApi"].Text.Trim(),
                    _text["placeable.lockChangingBinding"].Text.Trim(),
                    _text["placeable.lockChangedBinding"].Text.Trim(),
                    _check["placeable.lockEventMethod"].ButtonPressed,
                    _check["placeable.lockEventCredential"].ButtonPressed,
                    _check["placeable.lockEventActor"].ButtonPressed,
                    _check["placeable.lockEventVeto"].ButtonPressed),
                new(Stable(moduleId, $"placeable-contents-event:{placeableId}"),
                    _text["placeable.lifecycleApi"].Text.Trim(),
                    _text["placeable.contentsBinding"].Text.Trim(),
                    _check["placeable.contentsDelta"].ButtonPressed,
                    _check["placeable.contentsCause"].ButtonPressed,
                    _check["placeable.contentsRevision"].ButtonPressed,
                    _check["placeable.contentsActor"].ButtonPressed));
            var attachmentBlueprintId = SelectedBlueprint("placeable.attachmentBlueprint");
            var verbOrder = Csv(_text["placeable.verbOrder"].Text);
            var verbContexts = KeyValues(_text["placeable.verbContexts"].Text);
            PlaceableContextualVerbProfile PlaceableVerb(string verbId, string label, int priority,
                IReadOnlyList<string> required, IReadOnlyList<string> forbidden, string binding)
                => new(Stable(moduleId, $"placeable-verb:{placeableId}:{verbId}"), verbId, label,
                    priority, required, forbidden, binding);
            var standardVerbs = Csv(_text["placeable.standardVerbs"].Text)
                .Select(verbId => PlaceableVerb(verbId, $"placeable.{verbId}",
                    Priority(verbId, verbOrder, 0), Context(verbId, verbContexts, false),
                    Context(verbId, verbContexts, true),
                    $"{char.ToUpperInvariant(verbId[0])}{verbId[1..]}Requested"))
                .ToList();
            var customVerbId = _text["placeable.customVerb"].Text.Trim();
            standardVerbs.Add(PlaceableVerb(customVerbId,
                _text["placeable.customVerbLabel"].Text.Trim(),
                Priority(customVerbId, verbOrder, (int)_number["placeable.customVerbPriority"].Value),
                Csv(_text["placeable.customVerbRequired"].Text),
                Csv(_text["placeable.customVerbForbidden"].Text),
                _text["placeable.customVerbBinding"].Text.Trim()));
            var compositionEvents = attachmentBlueprintId is null ? null : new PlaceableCompositionEventsProfile(
                Stable(moduleId, $"placeable-composition-events:{placeableId}"), 1, "toolset:r095",
                new(Stable(moduleId, $"placeable-damage-events:{placeableId}"),
                    _text["placeable.damageApi"].Text.Trim(),
                    _text["placeable.damagedBinding"].Text.Trim(),
                    _text["placeable.destroyedBinding"].Text.Trim(),
                    _check["placeable.damageSource"].ButtonPressed,
                    _check["placeable.damageAmount"].ButtonPressed,
                    _check["placeable.damageType"].ButtonPressed,
                    _check["placeable.damagePrevious"].ButtonPressed,
                    _check["placeable.damageCurrent"].ButtonPressed,
                    _check["placeable.damageContext"].ButtonPressed),
                [
                    new(Stable(moduleId, $"placeable-socket:{placeableId}"),
                        _text["placeable.socketId"].Text.Trim(), _text["placeable.socketKind"].Text.Trim(),
                        new(new((float)_number["placeable.socketX"].Value,
                                (float)_number["placeable.socketY"].Value,
                                (float)_number["placeable.socketZ"].Value),
                            Vector3Value.Zero, Vector3Value.One, Vector3Value.Zero, .1, null, false, null),
                        Csv(_text["placeable.socketTags"].Text),
                        _check["placeable.socketSnap"].ButtonPressed,
                        (int)_number["placeable.socketCapacity"].Value),
                ],
                [
                    new(Stable(moduleId, $"placeable-attachment:{placeableId}"),
                        _text["placeable.socketId"].Text.Trim(), attachmentBlueprintId,
                        Csv(_text["placeable.attachmentTags"].Text),
                        new(new((float)_number["placeable.attachmentX"].Value,
                                (float)_number["placeable.attachmentY"].Value,
                                (float)_number["placeable.attachmentZ"].Value),
                            Vector3Value.Zero, Vector3Value.One, Vector3Value.Zero, .1, null, false, null),
                        Null(_text["placeable.attachmentPrefab"].Text)),
                ],
                new(Stable(moduleId, $"placeable-multi-verb:{placeableId}"), standardVerbs));
            var profile = new PlaceableWorldProfile(Stable(moduleId, $"placeable:{placeableId}"), 1, "toolset:g13-n-o", placeableId,
                Null(_text["placeable.faction"].Text), GuidOrNull(_text["placeable.dialogue"].Text),
                string.IsNullOrWhiteSpace(_text["placeable.key"].Text) ? null : new(_text["placeable.key"].Text.Trim(), consumption,
                    consumption == PlaceableWorldProfile.CredentialConsumption.Charge ? 3 : 1,
                    consumption == PlaceableWorldProfile.CredentialConsumption.VirtualPermission ? _text["placeable.key"].Text.Trim() : null, true),
                new([
                    new("physical-attack-resolved", _text["placeable.attack"].Text.Trim(), false, true),
                    new("ability-impacted", _text["placeable.ability"].Text.Trim(), false, true),
                    new("heartbeat", "scripts/placeable_heartbeat", false, true),
                    new($"custom:{_text["placeable.custom"].Text.Trim()}:signal", "scripts/placeable_custom", false, true),
                ], new(true, _number["placeable.interval"].Value, 12, true, _text["placeable.custom"].Text.Trim())),
                _check["placeable.physics"].ButtonPressed ? new(PlaceableWorldProfile.BodyMode.Rigid,
                    _number["placeable.mass"].Value, ["lock-y"], true, 4, true, "server") : null,
                foundation, _check["placeable.foundationRequired"].ButtonPressed,
                operations, _check["placeable.operationsRequired"].ButtonPressed,
                securityEvents, _check["placeable.securityEventsRequired"].ButtonPressed,
                compositionEvents, _check["placeable.compositionEventsRequired"].ButtonPressed);
            placeables = Upsert(placeables, profile, item => item.BlueprintId == placeableId);
        }

        var doors = current.Doors;
        if (SelectedBlueprint("door.target") is { } doorId)
        {
            var assemblyKind = (DoorWorldProfile.AssemblyKind)_choice["door.assembly"].Selected;
            var placement = (DoorWorldProfile.PlacementMode)_choice["door.placement"].Selected;
            var destinationKind = (DoorWorldProfile.DestinationKind)_choice["door.portalKind"].Selected;
            var profileId = Stable(moduleId, $"door:{doorId}");
            var accessScale = (float)_number["door.accessScale"].Value;
            var accessEvents = new DoorAccessEventProfile(Stable(profileId, "access-events"), 1, "toolset:r081",
                _text["door.accessAsset"].Text.Trim(),
                new TransformValue(
                    new((float)_number["door.accessX"].Value, (float)_number["door.accessY"].Value,
                        (float)_number["door.accessZ"].Value), QuaternionValue.Identity,
                    new(accessScale, accessScale, accessScale)),
                new(Stable(profileId, "lock"), (DoorAccessLockState)_choice["door.lockState"].Selected,
                    Csv(_text["door.credentials"].Text), _text["door.lockSkill"].Text.Trim(),
                    _number["door.lockDifficulty"].Value, null, _check["door.hackable"].ButtonPressed,
                    _check["door.relockable"].ButtonPressed, _check["door.canJam"].ButtonPressed,
                    _check["door.consumeCredential"].ButtonPressed, Csv(_text["door.lockPermissions"].Text)),
                new(Stable(profileId, "open-rejected"), _text["door.apiVersion"].Text.Trim(),
                    "DoorOpenRejected",
                    [new("fr-FR", _text["door.rejectionFr"].Text.Trim()),
                     new("en-US", _text["door.rejectionEn"].Text.Trim())],
                    _text["door.rejectionSound"].Text.Trim(), _text["door.rejectionAnimation"].Text.Trim(),
                    _text["door.rejectionScript"].Text.Trim()),
                new(Stable(profileId, "trap"), _text["door.trapTrigger"].Text.Trim(),
                    _text["door.trapPayload"].Text.Trim(), _number["door.trapDetection"].Value,
                    _number["door.trapDisarm"].Value, _number["door.trapReset"].Value,
                    _text["door.trapOwner"].Text.Trim(), _text["door.trapVfx"].Text.Trim(),
                    _text["door.trapFriendly"].Text.Trim(), _check["door.trapArmed"].ButtonPressed,
                    _check["door.trapOneShot"].ButtonPressed, Csv(_text["door.trapSides"].Text),
                    Csv(_text["door.trapEvents"].Text)),
                new(Stable(profileId, "portal-events"), _text["door.apiVersion"].Text.Trim(),
                    _text["door.portalRequested"].Text.Trim(), _text["door.portalCompleted"].Text.Trim()),
                new(Stable(profileId, "state-events"), _text["door.apiVersion"].Text.Trim(),
                    _text["door.openedBinding"].Text.Trim(), _text["door.closedBinding"].Text.Trim()),
                KeyValues(_text["door.accessOverrides"].Text),
                new(Stable(profileId, "lock-events"), 1, "toolset:r082", _text["door.apiVersion"].Text.Trim(),
                    _text["door.lockedBinding"].Text.Trim(), _text["door.unlockedBinding"].Text.Trim()),
                _check["door.lockEventsRequired"].ButtonPressed);
            var profile = new DoorWorldProfile(profileId, 1, "toolset:g13-p-q", doorId,
                new(_number["door.health"].Value, _number["door.hardness"].Value, false, "door.damaged", "door.breached", true,
                    "assets/debris/door.glb", "service.repair-door"),
                new(_text["door.loading"].Text.Trim(), "loading.area.title", "loading.area.tip", true, .5),
                [new("damage-applied", _text["door.damageScript"].Text.Trim(), false, true),
                 new("entity-destroyed", _text["door.damageScript"].Text.Trim(), false, true),
                 new("physical-attack-resolved", _text["door.attackScript"].Text.Trim(), false, true),
                 new("ability-impacted", _text["door.abilityScript"].Text.Trim(), false, true),
                 new("heartbeat", "scripts/door_heartbeat", false, true),
                 new($"custom:{_text["door.custom"].Text.Trim()}:signal", "scripts/door_custom", false, true)],
                GuidOrNull(_text["door.dialogue"].Text), Null(_text["door.faction"].Text),
                new(assemblyKind, [new(Stable(profileId, "panel-a"), "primary", -1, 0, false),
                    new(Stable(profileId, "panel-b"), "secondary", 1, .1, true)], "open-safely", assemblyKind == DoorWorldProfile.AssemblyKind.Airlock, "close-and-restore"),
                new(placement,
                    placement == DoorWorldProfile.PlacementMode.ArchitectureSocket
                        ? _text["door.socket"].Text.Trim() : null,
                    placement == DoorWorldProfile.PlacementMode.ArchitectureSocket
                        ? new("kit.crypt", "arched", new Dictionary<string, string> { ["door"] = "material.ash" }, 0, 0, 0)
                        : null,
                    "closed",
                    [
                        new("open", DoorWorldProfile.StateKind.Open, "animation:open", false, true),
                        new("closed", DoorWorldProfile.StateKind.Closed, "animation:closed", true, true),
                        new("opening", DoorWorldProfile.StateKind.Opening, "animation:opening", false, false),
                        new("closing", DoorWorldProfile.StateKind.Closing, "animation:closing", true, false),
                        new("locked", DoorWorldProfile.StateKind.Locked, "animation:locked", true, true),
                        new("jammed", DoorWorldProfile.StateKind.Jammed, "animation:jammed", true, true),
                        new("broken", DoorWorldProfile.StateKind.Broken, "animation:broken", false, false),
                        new("disabled", DoorWorldProfile.StateKind.Disabled, "animation:disabled", true, false),
                        new("custom", DoorWorldProfile.StateKind.Custom, "animation:custom", true, true),
                    ],
                    [
                        new("closed", "opening", "interact", 1, null),
                        new("opening", "open", "animation-complete", 0, null),
                        new("open", "closing", "interact", 1, null),
                        new("closing", "closed", "animation-complete", 0, null),
                    ],
                    new(2.5, "both", 1, 2, 1, "server", 2, "door.unavailable"),
                    new(destinationKind, Stable(profileId, "destination"), _text["door.spawnAnchor"].Text.Trim(),
                        180, true, [_text["door.permission"].Text.Trim()]),
                    [],
                    new Dictionary<string, string> { ["interaction.prompt"] = "door.open" },
                    new(_text["door.navObstacle"].Text.Trim(), _text["door.navLink"].Text.Trim(),
                        ["closed", "locked", "jammed", "disabled", "custom"], ["open", "broken"],
                        _number["door.openingPassable"].Value, _number["door.closingBlocked"].Value,
                        _check["door.navVisualize"].ButtonPressed)), accessEvents,
                _check["door.accessRequired"].ButtonPressed);
            doors = Upsert(doors, profile, item => item.BlueprintId == doorId);
        }

        var triggerArea = SelectedArea("trigger.area");
        var triggers = current.Triggers;
        if (SelectedBlueprint("trigger.target") is { } triggerId && triggerArea is not null)
        {
            var profileId = Stable(moduleId, $"trigger:{triggerId}");
            var dialogueId = GuidOrNull(_text["trigger.dialogue"].Text);
            var questId = GuidOrNull(_text["trigger.quest"].Text);
            var foundation = new TriggerWorldProfile.FoundationPolicy(
                Stable(profileId, "foundation"), 1, "toolset:r048", _text["trigger.slug"].Text.Trim(),
                new((TriggerWorldProfile.ShapeKind)_choice["trigger.shape"].Selected,
                    [new(-2, 0, -2), new(2, 0, -2), new(2, 0, 2), new(-2, 0, 2)],
                    [[new(-.5f, 0, -.5f), new(.5f, 0, -.5f), new(0, 0, .5f)]],
                    new(4, (float)_number["trigger.height"].Value, 4), _number["trigger.height"].Value,
                    _number["trigger.snap"].Value, _check["trigger.visualize"].ButtonPressed),
                new(Csv(_text["trigger.entityKinds"].Text), Csv(_text["trigger.requiredTags"].Text),
                    Csv(_text["trigger.excludedTags"].Text), Csv(_text["trigger.layers"].Text)),
                Csv(_text["trigger.conditions"].Text), _number["trigger.cooldown"].Value,
                (TriggerWorldProfile.FirePolicy)_choice["trigger.fire"].Selected,
                [new(Stable(profileId, "action"), _text["trigger.actionKind"].Text.Trim(),
                    _text["trigger.actionTarget"].Text.Trim(), new Dictionary<string, string> { ["source"] = "toolset" })],
                _check["trigger.portal"].ButtonPressed
                    ? new((TriggerWorldProfile.DestinationKind)_choice["trigger.portalKind"].Selected,
                        Stable(profileId, "destination"), _text["trigger.portalAnchor"].Text.Trim(),
                        new(0, 180, 0), true, ["portal.travel"])
                    : null,
                new(Vector3Value.Zero, Vector3Value.Zero, Vector3Value.One, null),
                new Dictionary<string, string> { ["editor.overlay"] = "volume" },
                new(Stable(profileId, "on-enter"), 1, "toolset:r048", _text["trigger.apiVersion"].Text.Trim(),
                    "VolumeEntered", null, Null(_text["trigger.onEnterScript"].Text)),
                new(Stable(profileId, "on-exit"), 1, "toolset:r048", _text["trigger.apiVersion"].Text.Trim(),
                    "VolumeExited", null, Null(_text["trigger.onExitScript"].Text)),
                new([Stable(profileId, "destination")], Csv(_text["trigger.knownTargets"].Text),
                    Csv(_text["trigger.knownScripts"].Text), Csv(_text["trigger.accessibleLayers"].Text)),
                _check["trigger.validationRequired"].ButtonPressed);
            var trapBindings = Csv(_text["trigger.trapBindings"].Text);
            if (trapBindings.Count != 4)
                throw new InvalidOperationException("Quatre bindings de piège sont requis.");
            var advanced = new TriggerAdvancedProfile(
                Stable(profileId, "advanced"), 1, "toolset:r096", _text["trigger.apiVersion"].Text.Trim(),
                new(Stable(profileId, "advanced-trap"), _text["trigger.trapTrigger"].Text.Trim(),
                    _text["trigger.trapPayload"].Text.Trim(), _number["trigger.trapDetection"].Value,
                    _number["trigger.trapDisarm"].Value, _number["trigger.trapReset"].Value,
                    _text["trigger.trapOwner"].Text.Trim(), _text["trigger.trapVfx"].Text.Trim(),
                    _text["trigger.trapFriendly"].Text.Trim(), _check["trigger.trapArmed"].ButtonPressed,
                    _check["trigger.trapOneShot"].ButtonPressed, _check["trigger.trapGm"].ButtonPressed,
                    _text["trigger.trapColor"].Text.Trim(), trapBindings[0], trapBindings[1], trapBindings[2],
                    trapBindings[3]),
                new(Stable(profileId, "advanced-query"), _check["trigger.queryPlayers"].ButtonPressed,
                    Csv(_text["trigger.queryRequired"].Text), Csv(_text["trigger.queryExcluded"].Text),
                    GuidCsv(_text["trigger.queryFactions"].Text), Csv(_text["trigger.queryComponents"].Text),
                    Null(_text["trigger.queryOwner"].Text), Null(_text["trigger.queryTeam"].Text),
                    Null(_text["trigger.queryPredicate"].Text)),
                TriggerGameplayVolumes(profileId),
                new(Stable(profileId, "advanced-occupancy"), true,
                    _check["trigger.debugOccupants"].ButtonPressed, _check["trigger.debugFires"].ButtonPressed,
                    _check["trigger.debugCooldowns"].ButtonPressed, _check["trigger.debugFilters"].ButtonPressed,
                    _check["trigger.debugTrace"].ButtonPressed, (int)_number["trigger.debugMaximum"].Value));
            var profile = new TriggerWorldProfile(profileId, 1, "toolset:g13-r-s", triggerId, triggerArea,
                new(["sight", "search"], (int)_number["trigger.discovery"].Value, Stable(profileId, "reveal"), "feedback.secret", true,
                    TriggerWorldProfile.DiscoveryScope.Party),
                new("inspect", "cursor.secret", "outline.gold", "trigger.inspect", _text["trigger.click"].Text.Trim(), true, "player"),
                new(_number["trigger.interval"].Value, 30, true, true), _text["trigger.custom"].Text.Trim(),
                string.IsNullOrWhiteSpace(_text["trigger.key"].Text) ? null : new(_text["trigger.key"].Text.Trim(), false, "trigger.denied"),
                new(_check["trigger.rest"].ButtonPressed, "danger.nearby", true, true, "rest.allowed", "rest.denied"),
                new("tracking", 10, "authored-tracks", 45, 2, "false-trail", questId),
                dialogueId is null ? null : new([], 4, true, 60, "party-vote", dialogueId.Value),
                new(true, true, _check["trigger.director"].ButtonPressed, true, true), foundation,
                _check["trigger.foundationRequired"].ButtonPressed, advanced,
                _check["trigger.advancedRequired"].ButtonPressed);
            triggers = Upsert(triggers, profile, item => item.BlueprintId == triggerId);
        }

        var routeArea = SelectedArea("vehicle.area");
        var routes = current.VehicleRoutes;
        VehicleRouteProfile? route = null;
        if (routeArea is not null)
        {
            var routeId = Stable(moduleId, $"vehicle:{routeArea}");
            var pointA = Stable(routeId, "start");
            var pointB = Stable(routeId, "end");
            route = new(routeId, 1, "toolset:g13-v", routeArea,
                [new(pointA, Vector3Value.Zero, 0, _number["vehicle.width"].Value),
                 new(pointB, new(20, 0, 0), 0, _number["vehicle.width"].Value)],
                [new("primary", 0, _number["vehicle.speed"].Value, "forward")],
                [new(Stable(routeId, "stop"), pointB, _number["vehicle.stop"].Value, "authored-stop")],
                _number["vehicle.speed"].Value, _check["vehicle.overtake"].ButtonPressed,
                _choice["vehicle.sync"].Selected switch { 1 => "bounded-prediction", 2 => "local", _ => "server-snapshots" });
            routes = Upsert(routes, route, item => item.Id == routeId);
        }

        var encounterArea = SelectedArea("encounter.area");
        var encounters = current.Encounters;
        if (encounterArea is not null && SelectedBlueprint("encounter.candidate") is { } encounterCandidateId)
        {
            var encounterId = Stable(moduleId, $"encounter:{encounterArea}");
            var routeId = route?.Id ?? Stable(moduleId, $"reinforcement:{encounterArea}");
            var encounterGroupId = Stable(encounterId, "group");
            var encounterWaveTwoId = Stable(encounterId, "wave:two");
            var waveCompletion = (EncounterWaveCompletionPolicy)_choice["encounter.waveCompletion"].Selected;
            var profile = new EncounterWorldProfile(encounterId, 1, "toolset:g13-t-u", encounterArea,
                new(_number["encounter.respawn"].Value, (int)_number["encounter.cycles"].Value, "area.occupied", true, true, .5),
                Null(_text["encounter.faction"].Text), new(true, _number["encounter.interval"].Value, 12, "encounter.authored"),
                new((int)_number["encounter.alert"].Value, _text["encounter.faction"].Text.Trim(), routeId,
                    (int)_number["encounter.cap"].Value, 3, "boss.dead"),
                new((EncounterWorldProfile.VisibilityMode)_choice["encounter.visibility"].Selected, 10, true, "vfx.spawn"),
                new(30, 30, _number["encounter.cleanup"].Value, true, true),
                new(
                    new((EncounterWorldProfile.VolumeShape)_choice["encounter.shape"].Selected,
                        [new(-4, 0, -4), new(4, 0, -4), new(4, 0, 4), new(-4, 0, 4)], Vector3Value.Zero,
                        _number["encounter.height"].Value, _number["encounter.radius"].Value,
                        Csv(_text["encounter.tags"].Text), Csv(_text["encounter.allowedFactions"].Text),
                        _check["encounter.lineOfSight"].ButtonPressed, _number["encounter.threat"].Value,
                        Null(_text["encounter.activationScript"].Text), _check["encounter.visualize"].ButtonPressed),
                    [new(Stable(encounterId, $"candidate:{encounterCandidateId}"), encounterCandidateId,
                        (EncounterWorldProfile.CandidateRole)_choice["encounter.role"].Selected,
                        _number["encounter.weight"].Value, _number["encounter.cost"].Value,
                        Csv(_text["encounter.conditions"].Text), Csv(_text["encounter.variants"].Text),
                        (int)_number["encounter.maximumAlive"].Value)],
                    _number["encounter.budget"].Value, Csv(_text["encounter.anchors"].Text),
                    Csv(_text["encounter.rewards"].Text), (int)_number["encounter.networkCap"].Value),
                new(Stable(encounterId, "composition"), 1, "toolset:r083",
                    _text["encounter.slug"].Text.Trim(), _text["encounter.displayName"].Text.Trim(),
                    _text["encounter.category"].Text.Trim(), _text["encounter.theme"].Text.Trim(),
                    (EncounterWorldProfile.CandidateRole)_choice["encounter.role"].Selected,
                    new(Stable(encounterId, "difficulty"), _text["encounter.ruleset"].Text.Trim(),
                        _text["encounter.partyProvider"].Text.Trim(),
                        (EncounterDifficultyBand)_choice["encounter.difficultyBand"].Selected,
                        (int)_number["encounter.partySize"].Value, _number["encounter.partyPower"].Value,
                        _number["encounter.baseBudget"].Value, _number["encounter.partyMultiplier"].Value,
                        _number["encounter.difficultyMultiplier"].Value),
                    [new(encounterGroupId, _text["encounter.groupSlug"].Text.Trim(),
                        (int)_number["encounter.groupMinimum"].Value,
                        (int)_number["encounter.groupMaximum"].Value, _number["encounter.groupBudget"].Value,
                        [Stable(encounterId, $"candidate:{encounterCandidateId}")])],
                    new(Stable(encounterId, "selection"), _text["encounter.partyProvider"].Text.Trim(),
                        _text["encounter.selectionStrategy"].Text.Trim(),
                        (int)_number["encounter.selectionSeed"].Value,
                        _check["encounter.previewComposition"].ButtonPressed),
                    new(Stable(encounterId, "activation"),
                        (EncounterActivationMode)_choice["encounter.activationMode"].Selected,
                        _number["encounter.activationInterval"].Value,
                        Null(_text["encounter.activationPolicyScript"].Text),
                        (EncounterSpawnDistribution)_choice["encounter.distribution"].Selected),
                    KeyValues(_text["encounter.compositionOverrides"].Text)),
                _check["encounter.compositionRequired"].ButtonPressed,
                new(Stable(encounterId, "scenario"), 1, "toolset:r084",
                    [new(Stable(encounterId, "spawn-anchor:manual"), _text["encounter.anchorId"].Text.Trim(),
                        new(new((float)_number["encounter.anchorPositionX"].Value,
                                (float)_number["encounter.anchorPositionY"].Value,
                                (float)_number["encounter.anchorPositionZ"].Value),
                            QuaternionValue.Identity, Vector3Value.One),
                        (int)_number["encounter.anchorCapacity"].Value,
                        _check["encounter.anchorNavigable"].ButtonPressed,
                        (EncounterWorldProfile.VisibilityMode)_choice["encounter.anchorVisibility"].Selected,
                        _check["encounter.anchorFallback"].ButtonPressed,
                        _number["encounter.anchorWeight"].Value)],
                    (EncounterSpawnDistribution)_choice["encounter.distribution"].Selected,
                    new(Stable(encounterId, "activator"), _check["encounter.playersOnly"].ButtonPressed,
                        _check["encounter.ignoreSummons"].ButtonPressed,
                        _check["encounter.ignoreCompanions"].ButtonPressed,
                        _check["encounter.ignoreGameMasters"].ButtonPressed,
                        _check["encounter.ignoreSpectators"].ButtonPressed,
                        (EncounterNetworkAuthority)_choice["encounter.activatorAuthority"].Selected,
                        Csv(_text["encounter.activatorTags"].Text)),
                    new(Stable(encounterId, "lifecycle"), _text["encounter.lifecycleApi"].Text.Trim(),
                        new(Stable(encounterId, "event:entered"), _text["encounter.enteredBinding"].Text.Trim(),
                            Null(_text["encounter.enteredScript"].Text)),
                        new(Stable(encounterId, "event:exited"), _text["encounter.exitedBinding"].Text.Trim(),
                            Null(_text["encounter.exitedScript"].Text)),
                        new(Stable(encounterId, "event:exhausted"),
                            _text["encounter.exhaustedBinding"].Text.Trim(),
                            Null(_text["encounter.exhaustedScript"].Text))),
                    [new(Stable(encounterId, "phase:one"), 1, _text["encounter.phaseName"].Text.Trim(),
                        Csv(_text["encounter.phaseConditions"].Text),
                        [new(Stable(encounterId, "wave:one"), 1, [encounterGroupId], 0,
                                EncounterWaveCompletionPolicy.AllDefeated, null, null, null,
                                "audio:encounter-wave-ready", encounterWaveTwoId),
                            new(encounterWaveTwoId, 2, [encounterGroupId],
                                _number["encounter.waveDelay"].Value, waveCompletion,
                                waveCompletion == EncounterWaveCompletionPolicy.Script
                                    ? Null(_text["encounter.waveCompletionTarget"].Text)
                                    : null,
                                waveCompletion == EncounterWaveCompletionPolicy.ObjectiveCompleted
                                    ? GuidOrNull(_text["encounter.waveQuest"].Text)
                                    : null,
                                waveCompletion == EncounterWaveCompletionPolicy.ObjectiveCompleted
                                    ? Null(_text["encounter.waveObjective"].Text)
                                    : null,
                                Null(_text["encounter.waveVoice"].Text), null)], [])],
                    KeyValues(_text["encounter.scenarioOverrides"].Text)),
                _check["encounter.scenarioRequired"].ButtonPressed,
                new(Stable(encounterId, "statistics"), 1, "toolset:r085",
                    (int)_number["encounter.statisticsRuns"].Value,
                    (int)_number["encounter.statisticsSeed"].Value,
                    _text["encounter.durationEstimator"].Text.Trim(),
                    _number["encounter.secondsPerActor"].Value,
                    _text["encounter.rewardEstimator"].Text.Trim(),
                    _number["encounter.rewardPerCost"].Value,
                    DoubleCsv(_text["encounter.statisticsPercentiles"].Text),
                    _check["encounter.statisticsDetails"].ButtonPressed,
                    KeyValues(_text["encounter.statisticsOverrides"].Text)),
                _check["encounter.statisticsRequired"].ButtonPressed);
            encounters = Upsert(encounters, profile, item => item.AreaId == encounterArea);
        }

        var merchants = current.Merchants;
        if (SelectedBlueprint("merchant.target") is { } merchantId)
        {
            var existingMerchant = merchants.FirstOrDefault(item => item.BlueprintId == merchantId);
            var currency = _text["merchant.currency"].Text.Trim();
            var balance = (decimal)_number["merchant.balance"].Value;
            var cap = (decimal)_number["merchant.cap"].Value;
            var stockItem = SelectedBlueprint("merchant.stockItem");
            var restrictions = Csv(_text["merchant.restrictions"].Text);
            MerchantWorldProfile.TransactionPolicy? transactions = null;
            if (stockItem is not null)
            {
                var variant = _text["merchant.variant"].Text.Trim();
                var transactionStock = UpsertPreservingOrder(
                    existingMerchant?.Transactions?.Stock ?? [],
                    new MerchantWorldProfile.StockEntry(
                        Stable(moduleId, $"merchant-stock:{merchantId}:{stockItem}"), stockItem, variant,
                        (int)_number["merchant.stockQuantity"].Value,
                        (decimal)_number["merchant.unitPrice"].Value, currency, restrictions),
                    item => item.ItemBlueprintId == stockItem &&
                            string.Equals(item.VariantId, variant, StringComparison.OrdinalIgnoreCase));
                var transactionScripts = existingMerchant?.Transactions?.Scripts
                    .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal)
                    ?? new Dictionary<string, string>(StringComparer.Ordinal);
                transactionScripts["onOpen"] = _text["merchant.onOpen"].Text.Trim();
                transactions = new MerchantWorldProfile.TransactionPolicy(
                    Stable(moduleId, $"merchant-transactions:{merchantId}"), 1, "toolset:r045",
                    _text["merchant.vendorInteraction"].Text.Trim(), _text["merchant.dialogueChoice"].Text.Trim(),
                    transactionStock, Csv(_text["merchant.locales"].Text), restrictions, transactionScripts,
                    (int)_number["merchant.buyerCapacity"].Value,
                    (int)_number["merchant.sellerCapacity"].Value, true);
            }
            var dialogueId = GuidOrNull(_text["merchant.dialogue"].Text);
            MerchantFoundationProfile? foundation = null;
            if (stockItem is not null && dialogueId is not null)
            {
                var variant = _text["merchant.variant"].Text.Trim();
                var vendor = new MerchantVendorBindingProfile(
                    Stable(moduleId, $"merchant-vendor:{merchantId}"), merchantId, dialogueId.Value,
                    _text["merchant.dialogueChoice"].Text.Trim(),
                    _text["merchant.vendorInteraction"].Text.Trim(), "npc");
                var vendors = UpsertPreservingOrder(existingMerchant?.Foundation?.Vendors ?? [], vendor,
                    item => item.VendorBlueprintId == merchantId);
                var foundationStock = UpsertPreservingOrder(
                    existingMerchant?.Foundation?.Stock ?? [],
                    new MerchantStockEntryProfile(
                        Stable(moduleId, $"merchant-foundation-stock:{merchantId}:{stockItem}"), stockItem, variant,
                        (MerchantSupplyPolicy)_choice["merchant.supply"].Selected,
                        (int)_number["merchant.stockQuantity"].Value, Null(_text["merchant.supplier"].Text),
                        _number["merchant.stockRestock"].Value, (int)_number["merchant.displayOrder"].Value,
                        Csv(_text["merchant.hiddenConditions"].Text),
                        (decimal)_number["merchant.unitPrice"].Value, currency),
                    item => item.ItemBlueprintId == stockItem &&
                            string.Equals(item.VariantId, variant, StringComparison.OrdinalIgnoreCase));
                var currencyRates = existingMerchant?.Foundation?.Pricing.CurrencyRates
                    .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal)
                    ?? new Dictionary<string, decimal>(StringComparer.Ordinal);
                foreach (var item in DecimalValues(_text["merchant.currencyRates"].Text))
                    currencyRates[item.Key] = item.Value;
                foundation = new MerchantFoundationProfile(
                    Stable(moduleId, $"merchant-foundation:{merchantId}"), 1, "toolset:r091",
                    _text["merchant.storeSlug"].Text.Trim(), _text["merchant.displayKey"].Text.Trim(),
                    Csv(_text["merchant.locales"].Text), vendors,
                    new(Stable(moduleId, $"merchant-wizard:{merchantId}"), 1, "toolset:r091",
                        _check["merchant.wizard"].ButtonPressed, _check["merchant.wizard"].ButtonPressed,
                        _check["merchant.wizard"].ButtonPressed, _check["merchant.wizard"].ButtonPressed,
                        _check["merchant.wizard"].ButtonPressed, _text["merchant.fixture"].Text.Trim()),
                    foundationStock,
                    new(Stable(moduleId, $"merchant-pricing:{merchantId}"), 1, "toolset:r091",
                        (decimal)_number["merchant.buyMultiplier"].Value,
                        (decimal)_number["merchant.sellMultiplier"].Value,
                        PriceCurve(_text["merchant.buyCurve"].Text), PriceCurve(_text["merchant.sellCurve"].Text),
                        currencyRates,
                        (decimal)_number["merchant.reputationFactor"].Value,
                        (decimal)_number["merchant.scarcityFactor"].Value,
                        (decimal)_number["merchant.conditionFactor"].Value,
                        (decimal)_number["merchant.negotiationFactor"].Value,
                        (decimal)_number["merchant.minimumPrice"].Value,
                        (decimal)_number["merchant.maximumPrice"].Value),
                    new(Stable(moduleId, $"merchant-query:{merchantId}"), 1, "toolset:r091",
                        Csv(_text["merchant.whitelistTypes"].Text), Csv(_text["merchant.whitelistTags"].Text),
                        Csv(_text["merchant.blacklistTags"].Text),
                        (int)_number["merchant.minimumQuality"].Value,
                        (int)_number["merchant.maximumQuality"].Value,
                        Csv(_text["merchant.provenances"].Text), _number["merchant.minimumCondition"].Value,
                        Null(_text["merchant.owner"].Text), Null(_text["merchant.customRule"].Text)));
            }
            var experience = new MerchantStoreExperienceProfile(
                Stable(moduleId, $"merchant-experience:{merchantId}"), 1, "toolset:r092",
                new(Stable(moduleId, $"merchant-sessions:{merchantId}"), 1, "toolset:r092",
                    _text["merchant.apiVersion"].Text.Trim(), _text["merchant.onOpened"].Text.Trim(),
                    _text["merchant.onClosed"].Text.Trim(), _check["merchant.includeCustomer"].ButtonPressed,
                    _check["merchant.includeVendor"].ButtonPressed,
                    _check["merchant.includePriceContext"].ButtonPressed,
                    _check["merchant.includeTransactionSummary"].ButtonPressed),
                new(Stable(moduleId, $"merchant-store-ui:{merchantId}"), 1, "toolset:r092",
                    _check["merchant.uiKeyboard"].ButtonPressed, _check["merchant.uiGamepad"].ButtonPressed,
                    _check["merchant.uiCompare"].ButtonPressed, _check["merchant.uiFilters"].ButtonPressed,
                    _check["merchant.uiCart"].ButtonPressed, _check["merchant.uiConfirmation"].ButtonPressed,
                    _check["merchant.uiScreenReader"].ButtonPressed,
                    _check["merchant.uiHighContrast"].ButtonPressed, Csv(_text["merchant.uiColumns"].Text)));
            var currencies = UpsertPreservingOrder(existingMerchant?.Currencies ?? [],
                new MerchantWorldProfile.CurrencyDefinition(currency, $"currency.{currency}", 1, true, false),
                item => string.Equals(item.Id, currency, StringComparison.Ordinal));
            var treasury = UpsertPreservingOrder(existingMerchant?.Treasury ?? [],
                new MerchantWorldProfile.TreasuryAccount(
                    currency, balance, 0, 0, balance / 10, Math.Max(balance, 1) * 2),
                item => string.Equals(item.CurrencyId, currency, StringComparison.Ordinal));
            var serviceKind = _text["merchant.service"].Text.Trim();
            var services = UpsertPreservingOrder(existingMerchant?.Services ?? [],
                new MerchantWorldProfile.ServiceEntry(
                    Stable(moduleId, $"merchant-service:{merchantId}"), serviceKind,
                    $"service.{serviceKind}", currency, Math.Min(cap, 25),
                    $"service.{serviceKind}", "true"),
                item => string.Equals(item.Kind, serviceKind, StringComparison.OrdinalIgnoreCase));
            var profile = new MerchantWorldProfile(Stable(moduleId, $"merchant:{merchantId}"), 1, "toolset:g13-w-x", merchantId,
                currencies, treasury,
                new(cap, cap * 2, new Dictionary<string, decimal>(), -1, true),
                new(_check["merchant.stolen"].ButtonPressed, ["authored-jurisdiction"], false, false, .25, "crime.stolen-goods"),
                new(_number["merchant.restock"].Value, "market.restock", route?.Id, true, true),
                new("bargain", 10, _number["merchant.discount"].Value / 100, .2, 60, true),
                _check["merchant.market"].ButtonPressed ? new("authored-region", .25, .5, new Dictionary<string, double>(), true) : null,
                services,
                new(_check["merchant.ledger"].ButtonPressed, 90, Math.Max(100, cap), true, true, true),
                transactions, _check["merchant.transactionsRequired"].ButtonPressed,
                foundation, _check["merchant.foundationRequired"].ButtonPressed,
                experience, _check["merchant.experienceRequired"].ButtonPressed);
            merchants = Upsert(merchants, profile, item => item.BlueprintId == merchantId);
        }

        var companions = current.Companions;
        if (SelectedBlueprint("companion.target") is { } companionId && GuidOrNull(_text["companion.dialogue"].Text) is { } companionDialogue)
        {
            var questId = GuidOrNull(_text["companion.quest"].Text);
            var profileId = Stable(moduleId, $"companion:{companionId}");
            var areaIds = Csv(_text["companion.areas"].Text).Select(AreaId.Parse).ToArray();
            var contexts = Csv(_text["companion.contexts"].Text)
                .Select(context => new CompanionWorldProfile.DialogueContextValidation(
                    context, companionDialogue, $"dialogue.{context}"))
                .ToArray();
            var ownership = _choice["companion.ownership"].Selected switch
            {
                1 => "individual",
                2 => "shared",
                3 => "ai-only",
                4 => "game-master",
                _ => "party",
            };
            var deathMode = _choice["companion.deathMode"].Selected switch
            {
                1 => "downed",
                2 => "permanent-death",
                3 => "resurrection",
                4 => "replacement",
                5 => "memorial",
                _ => "recoverable-corpse",
            };
            var persistence = _choice["companion.persistence"].Selected switch
            {
                0 => CompanionWorldProfile.CompanionPersistence.Session,
                1 => CompanionWorldProfile.CompanionPersistence.Area,
                3 => CompanionWorldProfile.CompanionPersistence.Permanent,
                _ => CompanionWorldProfile.CompanionPersistence.Campaign,
            };
            var equipmentPermission = _choice["companion.equipmentPermission"].Selected switch
            {
                1 => "equip",
                2 => "suggest",
                3 => "view",
                4 => "none",
                _ => "full",
            };
            var recruitConditions = Csv(_text["companion.recruitConditions"].Text);
            var recruitConsequences = Csv(_text["companion.recruitConsequences"].Text);
            var commandIds = Csv(_text["companion.commands"].Text);
            var profile = new CompanionWorldProfile(profileId, 1, "toolset:r037", companionId,
                new(companionDialogue, "full", "limited", "greeting", "full", "deny"),
                new(_text["companion.formation"].Text.Trim(), (int)_number["companion.slot"].Value, 1.5, true, true, true),
                new(CompanionWorldProfile.ProgressionMode.PlayerDirected, (int)_number["companion.level"].Value, 40, true, true, [5, 10, 20]),
                new(.5, .5, 0, _number["companion.loyalty"].Value, ["authored"], ["joined-party"]),
                new([], 2, 90, [], true, companionDialogue),
                questId is null ? null : new(questId.Value, "loyalty>=0", "departed", 0, "party-reacts"),
                new(_check["companion.traps"].ButtonPressed, _check["companion.traps"].ButtonPressed, 5, "sight", "traps", true),
                new(Csv(_text["companion.abilities"].Text), [], .35, "lowest-health", true),
                new(_check["companion.direct"].ButtonPressed, true, true, true, "owner-or-gm"),
                new(true, _check["companion.split"].ButtonPressed, "owner-instance", _check["companion.split"].ButtonPressed, 120),
                new(Stable(profileId, "validation"), 1, "toolset:r037",
                    new(_check["companion.recruitDialogue"].ButtonPressed, _check["companion.recruitScript"].ButtonPressed,
                        _text["companion.recruitScriptId"].Text.Trim(), _text["companion.currency"].Text.Trim(),
                        (decimal)_number["companion.cost"].Value, _number["companion.reputation"].Value),
                    new(ownership, _text["companion.authority"].Text.Trim(), _check["companion.transfer"].ButtonPressed),
                    commandIds,
                    new(_text["companion.inventory"].Text.Trim(), _text["companion.equipment"].Text.Trim(),
                        Csv(_text["companion.slots"].Text)),
                    new(_text["companion.deathPolicy"].Text.Trim(), deathMode, _number["companion.recovery"].Value),
                    contexts,
                    new(_check["companion.crossZone"].ButtonPressed, areaIds,
                        _check["companion.followPortals"].ButtonPressed, _check["companion.safeFallback"].ButtonPressed,
                        _text["companion.fallbackAnchor"].Text.Trim(), _number["companion.timeout"].Value)),
                _check["companion.required"].ButtonPressed,
                new(Stable(profileId, "foundation"), 1, "toolset:r074",
                    new(Stable(profileId, "recruitable"), 1, "toolset:r074", true, recruitConditions,
                        (decimal)_number["companion.cost"].Value, _text["companion.currency"].Text.Trim(),
                        _number["companion.reputation"].Value, recruitConsequences,
                        _check["companion.recruitDialogue"].ButtonPressed,
                        _check["companion.recruitScript"].ButtonPressed,
                        _text["companion.recruitScriptId"].Text.Trim()),
                    new(Stable(profileId, "pre-recruitment"), 1, "toolset:r074", companionDialogue,
                        "not-recruited", recruitConditions, _text["companion.offerNode"].Text.Trim(),
                        _text["companion.refusalNode"].Text.Trim(), recruitConsequences),
                    new(Stable(profileId, "owner-dialogue"), 1, "toolset:r074", companionDialogue,
                        "recruited-by-speaker", commandIds, true, true, true,
                        _text["companion.departureAction"].Text.Trim()),
                    new(Stable(profileId, "membership"), 1, "toolset:r074", true, true, true, true,
                        persistence, _text["companion.inventory"].Text.Trim(),
                        _text["companion.dismissAnchor"].Text.Trim(), true),
                    new(Stable(profileId, "party"), 1, "toolset:r074",
                        (int)_number["companion.companionSlots"].Value,
                        (int)_number["companion.summonSlots"].Value,
                        (int)_number["companion.petSlots"].Value,
                        (int)_number["companion.temporaryAllySlots"].Value,
                        (int)_number["companion.serverActorLimit"].Value,
                        KeyedCounts(_text["companion.roleLimits"].Text))),
                _check["companion.foundationRequired"].ButtonPressed,
                new(Stable(profileId, "control"), 1, "toolset:r075",
                    new(Stable(profileId, "ownership"), 1, "toolset:r075", ownership,
                        Csv(_text["companion.controllers"].Text), _check["companion.transfer"].ButtonPressed,
                        Csv(_text["companion.allowedOwnership"].Text),
                        (long)_number["companion.ownershipRevision"].Value),
                    new(Stable(profileId, "movement"), 1, "toolset:r075",
                        Csv(_text["companion.movementCommands"].Text), _text["companion.formation"].Text.Trim(),
                        _text["companion.waypointQuery"].Text.Trim(),
                        (int)_number["companion.priorityMin"].Value,
                        (int)_number["companion.priorityMax"].Value),
                    new(Stable(profileId, "tactical"), 1, "toolset:r075",
                        Csv(_text["companion.tacticalCommands"].Text), Csv(_text["companion.stances"].Text),
                        Csv(_text["companion.combatAbilities"].Text),
                        (int)_number["companion.priorityMin"].Value,
                        (int)_number["companion.priorityMax"].Value),
                    new(Stable(profileId, "ai-control"), 1, "toolset:r075",
                        _number["companion.aiAutonomy"].Value, _number["companion.aiRisk"].Value,
                        _number["companion.aiResources"].Value, _check["companion.friendlyFire"].ButtonPressed,
                        _choice["companion.aiRole"].GetItemText(_choice["companion.aiRole"].Selected),
                        _number["companion.followDistance"].Value,
                        Csv(_text["companion.combatAbilities"].Text), Stable(profileId, "ai-plan"),
                        Stable(profileId, "ai-transaction"),
                        "replace:/companion/control/ai|source:toolset|mode:governed",
                        _text["companion.aiAuthorization"].Text.Trim()),
                    new(Stable(profileId, "equipment-control"), 1, "toolset:r075", equipmentPermission,
                        GuidCsv(_text["companion.lockedItems"].Text),
                        GuidValues(_text["companion.suggestedLoadout"].Text),
                        _check["companion.allowCompare"].ButtonPressed,
                        _text["companion.equipmentOwner"].Text.Trim())),
                _check["companion.controlRequired"].ButtonPressed,
                new(Stable(profileId, "experience"), 1, "toolset:r076",
                    new(Stable(profileId, "death-experience"), 1, "toolset:r076", deathMode,
                        _number["companion.recovery"].Value,
                        Null(_text["companion.resurrectionResource"].Text),
                        Null(_text["companion.replacementCreature"].Text),
                        Null(_text["companion.memorial"].Text),
                        _check["companion.dropInventory"].ButtonPressed),
                    new(Stable(profileId, "cohesion-experience"), 1, "toolset:r076",
                        _number["companion.navigateThreshold"].Value,
                        _number["companion.portalThreshold"].Value,
                        _number["companion.teleportThreshold"].Value,
                        _check["companion.followPortals"].ButtonPressed,
                        _check["companion.safeFallback"].ButtonPressed,
                        Csv(_text["companion.safeAnchorTags"].Text)),
                    new(Stable(profileId, "party-hud"), 1, "toolset:r076",
                        _check["companion.hud.portraits"].ButtonPressed,
                        _check["companion.hud.health"].ButtonPressed,
                        _check["companion.hud.resources"].ButtonPressed,
                        _check["companion.hud.statuses"].ButtonPressed,
                        _check["companion.hud.quickCommands"].ButtonPressed,
                        _check["companion.hud.reorder"].ButtonPressed,
                        _check["companion.hud.keyboard"].ButtonPressed,
                        _check["companion.hud.gamepad"].ButtonPressed,
                        _check["companion.hud.screenReader"].ButtonPressed,
                        _check["companion.hud.highContrast"].ButtonPressed,
                        Csv(_text["companion.hud.resources"].Text))),
                _check["companion.experienceRequired"].ButtonPressed);
            companions = Upsert(companions, profile, item => item.BlueprintId == companionId);
        }

        var scripts = new ScriptAutomationProfile(moduleId, 1, "toolset:g13-aa-ab",
            new([new(Stable(moduleId, "camera-shot"), _text["script.rig"].Text.Trim(), _text["script.shot"].Text.Trim(),
                _number["script.duration"].Value, .4, _check["script.lock"].ButtonPressed, true)],
                "restore-camera-and-control", true, true),
            new(true, true, [new(_text["script.tag"].Text.Trim(), _text["script.event"].Text.Trim(), _text["script.route"].Text.Trim(), true)]),
            [new(_text["script.flag"].Text.Trim(), ScriptAutomationProfile.SettingKind.Boolean,
                _check["script.flagDefault"].ButtonPressed ? "true" : "false", ScriptAutomationProfile.FlagScope.Module,
                "Authored module feature flag.", 1)],
            [new(_text["script.ai"].Text.Trim(), ScriptAutomationProfile.SettingKind.Choice,
                _choice["script.aiDefault"].Selected switch { 0 => "passive", 2 => "aggressive", _ => "balanced" },
                ["passive", "balanced", "aggressive"], "Named AI behavior preview.", true)]);

        return new(moduleId, 1, "toolset:g13-n-ab", placeables, doors, triggers, encounters, routes, merchants, companions, scripts);
    }

    private void Mutate(bool dryRun)
    {
        _lastSucceeded = false;
        LastCommandId = Guid.Empty;
        LastPatchSummary = LastDiagnostics = string.Empty;
        if (_mutate is null || _feedback is null) return;
        try
        {
            var result = _mutate(BuildCatalog(), dryRun);
            LastCommandId = result.Audit.CommandId.Value;
            LastPatchSummary = result.Patch?.Summary ?? string.Empty;
            LastDiagnostics = string.Join(" | ", result.Diagnostics.Select(item => $"{item.Code}:{item.Message}"));
            _lastSucceeded = result.Succeeded;
            _feedback.Text = result.Succeeded
                ? dryRun
                    ? $"[color=#ffbd7a]APERÇU — aucune donnée modifiée[/color]\n{result.Patch?.Summary}\nInverse : {result.Inverse?.Summary}"
                    : $"[color=#8ce0bd]MONDE VIVANT ENREGISTRÉ[/color]\n{result.Patch?.Summary}\nRévision {result.NewRevision}"
                : $"[color=#ff7777]TRANSACTION REFUSÉE[/color]\n{LastDiagnostics}";
            if (!dryRun && result.Succeeded) Refresh();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException)
        {
            LastDiagnostics = exception.Message;
            _feedback.Text = $"[color=#ff7777]CONFIGURATION INVALIDE[/color]\n{exception.Message}";
        }
    }

    private void RebuildSummary()
    {
        if (_summary is null) return;
        try
        {
            var catalog = BuildCatalog();
            var events = catalog.Placeables.Sum(item => item.Reactive.Events.Count) + catalog.Doors.Sum(item => item.Events.Count);
            _summary.Text = "[color=#ff9858][b]COUVERTURE DU MODULE[/b][/color]\n\n" +
                $"[b]Placeables[/b] · {catalog.Placeables.Count} profil(s) · {catalog.Placeables.Count(item => item.Foundation is not null)} fondation(s) · " +
                $"{catalog.Placeables.Count(item => item.Foundation?.Interaction is not null)} utilisable(s) · " +
                $"{catalog.Placeables.Count(item => item.Foundation?.StaticProp is not null)} décor(s) · {catalog.Placeables.Count(item => item.Physics is not null)} physique(s)\n" +
                $"Conteneur P1 · {catalog.Placeables.FirstOrDefault()?.Operations?.Container?.CapacitySlots ?? 0} slots · " +
                $"{catalog.Placeables.FirstOrDefault()?.Operations?.Container?.CapacityMass ?? 0:0.##} masse · " +
                $"ownership {catalog.Placeables.FirstOrDefault()?.Operations?.Container?.OwnershipPolicy ?? "absent"} · " +
                $"persisté/répliqué {(catalog.Placeables.FirstOrDefault()?.Operations?.Container is { Persistent: true, Replicated: true } ? "oui" : "non")}\n" +
                $"États visuels · {catalog.Placeables.FirstOrDefault()?.Operations?.StateMachine?.InitialStateId ?? "absent"} initial · " +
                $"{string.Join(',', catalog.Placeables.FirstOrDefault()?.Operations?.StateMachine?.States.Select(item => item.StateId) ?? [])} · " +
                $"{catalog.Placeables.FirstOrDefault()?.Operations?.StateMachine?.Transitions.Count ?? 0} transition(s)\n" +
                $"Destructibilité · {catalog.Placeables.FirstOrDefault()?.Operations?.Damageable?.MaximumHealth ?? 0:0.##} HP · " +
                $"armure {catalog.Placeables.FirstOrDefault()?.Operations?.Damageable?.Armor ?? 0:0.##} · résistances/étapes/débris/réparation\n" +
                $"Protection · {string.Join(',', catalog.Placeables.FirstOrDefault()?.Operations?.Protection?.ProtectedActions ?? [])} · " +
                $"override MJ audité {catalog.Placeables.FirstOrDefault()?.Operations?.Protection?.AuditBinding ?? "absent"}\n" +
                $"Verrou · {catalog.Placeables.FirstOrDefault()?.Operations?.Lock?.InitialState.ToString() ?? "absent"} · " +
                $"clé/skill/code/hacking/relock/jam/permissions\n" +
                $"Piège R094 · {catalog.Placeables.FirstOrDefault()?.SecurityEvents?.Trap?.PayloadId ?? "absent"} · " +
                $"detect {catalog.Placeables.FirstOrDefault()?.SecurityEvents?.Trap?.DetectionDifficulty ?? 0:0.##} / disarm " +
                $"{catalog.Placeables.FirstOrDefault()?.SecurityEvents?.Trap?.DisarmDifficulty ?? 0:0.##} · reset/owner/VFX/friendly/one-shot\n" +
                $"Trésor · {catalog.Placeables.FirstOrDefault()?.SecurityEvents?.Loot?.Timing.ToString() ?? "absent"} · " +
                $"seed {catalog.Placeables.FirstOrDefault()?.SecurityEvents?.Loot?.Seed ?? 0} · " +
                $"{catalog.Placeables.FirstOrDefault()?.SecurityEvents?.Loot?.LootTable.Entries.Count ?? 0} entrée(s) · état persisté\n" +
                $"Conteneur · {catalog.Placeables.FirstOrDefault()?.SecurityEvents?.ContainerEvents?.OpenedBinding ?? "absent"}/" +
                $"{catalog.Placeables.FirstOrDefault()?.SecurityEvents?.ContainerEvents?.ClosedBinding ?? "absent"} · acteur/résultat/ancien état/autorité\n" +
                $"Verrou events · {catalog.Placeables.FirstOrDefault()?.SecurityEvents?.LockEvents?.ChangingBinding ?? "absent"} → " +
                $"{catalog.Placeables.FirstOrDefault()?.SecurityEvents?.LockEvents?.ChangedBinding ?? "absent"} · méthode/credential/acteur/veto\n" +
                $"OnDisturbed · {catalog.Placeables.FirstOrDefault()?.SecurityEvents?.ContentsChanged?.EventBinding ?? "absent"} · " +
                $"delta transactionnel/cause/révision/acteur\n" +
                $"Dégâts R095 · {catalog.Placeables.FirstOrDefault()?.CompositionEvents?.DamageEvents.DamagedBinding ?? "absent"}/" +
                $"{catalog.Placeables.FirstOrDefault()?.CompositionEvents?.DamageEvents.DestroyedBinding ?? "absent"} · " +
                $"source/montant/type/santé/contexte\n" +
                $"Sockets · {catalog.Placeables.FirstOrDefault()?.CompositionEvents?.Sockets.Count ?? 0} · " +
                $"{catalog.Placeables.FirstOrDefault()?.CompositionEvents?.Attachments.Count ?? 0} attachement(s) · " +
                $"snap/capacité/prefab/patch inverse\n" +
                $"Multi-verbes · {string.Join(',', catalog.Placeables.FirstOrDefault()?.CompositionEvents?.MultiVerb.Verbs.Select(item => item.VerbId) ?? [])} · " +
                $"contexte/priorité/bindings\n" +
                $"[b]Portes[/b] · {catalog.Doors.Count} profil(s) · {catalog.Doors.Count(item => item.Assembly is not null)} assemblage(s) · " +
                $"{catalog.Doors.Count(item => item.AccessEvents is not null)} accès/événements P1\n" +
                $"Accès porte · verrou {catalog.Doors.FirstOrDefault()?.AccessEvents?.Lock.State.ToString() ?? "legacy"} · " +
                $"{catalog.Doors.FirstOrDefault()?.AccessEvents?.Lock.CredentialIds.Count ?? 0} credential(s) · " +
                $"consommation {(catalog.Doors.FirstOrDefault()?.AccessEvents?.Lock.ConsumeCredential == true ? "oui" : "non")}\n" +
                $"Échec · {catalog.Doors.FirstOrDefault()?.AccessEvents?.Rejection.Texts.Count ?? 0} langue(s) · " +
                $"son/animation/script · {catalog.Doors.FirstOrDefault()?.AccessEvents?.Rejection.EventBinding ?? "absent"}\n" +
                $"Piège · {catalog.Doors.FirstOrDefault()?.AccessEvents?.Trap.PayloadId ?? "absent"} · côtés " +
                $"{string.Join(',', catalog.Doors.FirstOrDefault()?.AccessEvents?.Trap.Sides ?? [])} · reset " +
                $"{catalog.Doors.FirstOrDefault()?.AccessEvents?.Trap.ResetDelaySeconds ?? 0:0.##} s\n" +
                $"Portail · {catalog.Doors.FirstOrDefault()?.AccessEvents?.PortalUse.RequestedBinding ?? "absent"} → " +
                $"{catalog.Doors.FirstOrDefault()?.AccessEvents?.PortalUse.CompletedBinding ?? "absent"} · états " +
                $"{catalog.Doors.FirstOrDefault()?.AccessEvents?.StateEvents.OpenedBinding ?? "absent"}/" +
                $"{catalog.Doors.FirstOrDefault()?.AccessEvents?.StateEvents.ClosedBinding ?? "absent"}\n" +
                $"Verrou · {catalog.Doors.FirstOrDefault()?.AccessEvents?.LockStateEvents?.LockedBinding ?? "absent"}/" +
                $"{catalog.Doors.FirstOrDefault()?.AccessEvents?.LockStateEvents?.UnlockedBinding ?? "absent"} · méthode/acteur/credential\n" +
                $"[b]Triggers[/b] · {catalog.Triggers.Count} profil(s) · {catalog.Triggers.Count(item => item.Foundation is not null)} volume(s) canonique(s) · " +
                $"{catalog.Triggers.Sum(item => item.Foundation?.Actions.Count ?? 0)} action(s) · {catalog.Triggers.Count(item => item.Foundation?.Portal is not null)} transition(s)\n" +
                $"Piège R096 · {catalog.Triggers.FirstOrDefault()?.Advanced?.Trap.PayloadId ?? "absent"} · " +
                $"détection {catalog.Triggers.FirstOrDefault()?.Advanced?.Trap.DetectionDifficulty ?? 0:0.##} / désarmement " +
                $"{catalog.Triggers.FirstOrDefault()?.Advanced?.Trap.DisarmDifficulty ?? 0:0.##} · reset/owner/VFX/MJ\n" +
                $"Cibles · joueurs/tags/factions/composants/propriétaire/équipe/prédicat · " +
                $"{catalog.Triggers.FirstOrDefault()?.Advanced?.TargetFilter.RequiredTags.Count ?? 0} tag(s) requis\n" +
                $"Volumes gameplay · {catalog.Triggers.FirstOrDefault()?.Advanced?.GameplayVolumes.Count ?? 0}/10 · " +
                $"{string.Join(',', catalog.Triggers.FirstOrDefault()?.Advanced?.GameplayVolumes.Select(item => item.Kind) ?? [])}\n" +
                $"Occupation · occupants/tirs/cooldowns/filtres/trace · " +
                $"{catalog.Triggers.FirstOrDefault()?.Advanced?.OccupancyDebug.MaximumTraceEntries ?? 0} entrée(s)\n" +
                $"[b]Rencontres[/b] · {catalog.Encounters.Count} profil(s) · " +
                $"{catalog.Encounters.Count(item => item.Composition is not null)} composition(s) P1 · respawn, renforts, visibilité et nettoyage\n" +
                $"Assistant · {catalog.Encounters.FirstOrDefault()?.Composition?.PrimaryRole.ToString() ?? "legacy"} · " +
                $"{catalog.Encounters.FirstOrDefault()?.Composition?.Theme ?? "sans thème"} · " +
                $"{catalog.Encounters.FirstOrDefault()?.Composition?.Difficulty.Band.ToString() ?? "sans difficulté"} · " +
                $"budget {catalog.Encounters.FirstOrDefault()?.Composition?.Difficulty.EffectiveBudget ?? 0:0.##}\n" +
                $"Groupe · {catalog.Encounters.FirstOrDefault()?.Composition?.Groups.FirstOrDefault()?.Minimum ?? 0}–" +
                $"{catalog.Encounters.FirstOrDefault()?.Composition?.Groups.FirstOrDefault()?.Maximum ?? 0} · " +
                $"sélection {catalog.Encounters.FirstOrDefault()?.Composition?.Selection.PartyAssessmentProviderId ?? "legacy"}/" +
                $"{catalog.Encounters.FirstOrDefault()?.Composition?.Selection.Strategy ?? "legacy"} · " +
                $"activation {catalog.Encounters.FirstOrDefault()?.Composition?.Activation.Mode.ToString() ?? "legacy"}\n" +
                $"Spawn manuel · {catalog.Encounters.FirstOrDefault()?.Scenario?.SpawnAnchors.FirstOrDefault()?.AnchorId ?? "absent"} · " +
                $"capacité {catalog.Encounters.FirstOrDefault()?.Scenario?.SpawnAnchors.FirstOrDefault()?.Capacity ?? 0} · " +
                $"fallback {(catalog.Encounters.FirstOrDefault()?.Scenario?.SpawnAnchors.FirstOrDefault()?.IsFallback == true ? "oui" : "non")} · " +
                $"{catalog.Encounters.FirstOrDefault()?.Scenario?.Distribution.ToString() ?? "legacy"}\n" +
                $"Activation joueur · autorité {catalog.Encounters.FirstOrDefault()?.Scenario?.Activator.RequiredAuthority.ToString() ?? "legacy"} · " +
                $"joueur seul {(catalog.Encounters.FirstOrDefault()?.Scenario?.Activator.PlayersOnly == true ? "oui" : "non")} · " +
                $"invocations/compagnons/MJ/spectateurs filtrés\n" +
                $"Cycle de vie · {catalog.Encounters.FirstOrDefault()?.Scenario?.Lifecycle.Entered.Binding ?? "absent"} / " +
                $"{catalog.Encounters.FirstOrDefault()?.Scenario?.Lifecycle.Exited.Binding ?? "absent"} / " +
                $"{catalog.Encounters.FirstOrDefault()?.Scenario?.Lifecycle.Exhausted.Binding ?? "absent"}\n" +
                $"Graphe · {catalog.Encounters.FirstOrDefault()?.Scenario?.Phases.Count ?? 0} phase(s) · " +
                $"{catalog.Encounters.FirstOrDefault()?.Scenario?.Phases.Sum(phase => phase.Waves.Count) ?? 0} vague(s) · " +
                $"{catalog.Encounters.FirstOrDefault()?.Scenario?.Phases.FirstOrDefault()?.Waves.LastOrDefault()?.Completion.ToString() ?? "legacy"} · " +
                $"{catalog.Encounters.FirstOrDefault()?.Scenario?.Phases.FirstOrDefault()?.Waves.LastOrDefault()?.VoiceCueId ?? "sans voix"}\n" +
                $"Statistiques · {catalog.Encounters.FirstOrDefault()?.Statistics?.Runs ?? 0} runs · seed " +
                $"{catalog.Encounters.FirstOrDefault()?.Statistics?.BaseSeed ?? 0} · " +
                $"{catalog.Encounters.FirstOrDefault()?.Statistics?.DurationEstimatorId ?? "sans durée"} / " +
                $"{catalog.Encounters.FirstOrDefault()?.Statistics?.RewardEstimatorId ?? "sans récompense"} · p " +
                $"{string.Join(',', catalog.Encounters.FirstOrDefault()?.Statistics?.Percentiles ?? [])}\n" +
                $"[b]Routes[/b] · {catalog.VehicleRoutes.Count} · {catalog.VehicleRoutes.Sum(item => item.Lanes.Count)} lane(s)\n" +
                $"[b]Marchands[/b] · {catalog.Merchants.Count} · monnaies, trésorerie, services et audit\n" +
                $"Store indépendant · {catalog.Merchants.FirstOrDefault()?.Foundation?.Slug ?? "legacy"} · " +
                $"{catalog.Merchants.FirstOrDefault()?.Foundation?.Vendors.Count ?? 0} vendeur(s)/canal(aux)\n" +
                $"Wizard · {(catalog.Merchants.FirstOrDefault()?.Foundation?.Wizard.CreateTests == true ? "store-vendor-dialogue-interaction-tests" : "legacy")} · " +
                $"fixture {catalog.Merchants.FirstOrDefault()?.Foundation?.Wizard.TestFixtureId ?? "absente"}\n" +
                $"Inventaire · {catalog.Merchants.FirstOrDefault()?.Foundation?.Stock.Count ?? 0} entrée(s) · " +
                $"{catalog.Merchants.FirstOrDefault()?.Foundation?.Stock.FirstOrDefault()?.Supply.ToString() ?? "legacy"} · fournisseur/restock/ordre/masquage\n" +
                $"Tarification · achat ×{catalog.Merchants.FirstOrDefault()?.Foundation?.Pricing.BuyMultiplier ?? 0:0.##} · " +
                $"vente ×{catalog.Merchants.FirstOrDefault()?.Foundation?.Pricing.SellMultiplier ?? 0:0.##} · courbes " +
                $"{catalog.Merchants.FirstOrDefault()?.Foundation?.Pricing.BuyCurve.Count ?? 0}/" +
                $"{catalog.Merchants.FirstOrDefault()?.Foundation?.Pricing.SellCurve.Count ?? 0}\n" +
                $"Restrictions · types/tags/qualité/provenance/état/propriétaire/règle · " +
                $"white {catalog.Merchants.FirstOrDefault()?.Foundation?.Restrictions.WhitelistTags.Count ?? 0} / " +
                $"black {catalog.Merchants.FirstOrDefault()?.Foundation?.Restrictions.BlacklistTags.Count ?? 0}\n" +
                $"Sessions · {catalog.Merchants.FirstOrDefault()?.Experience?.Sessions.OpenEventBinding ?? "legacy"} / " +
                $"{catalog.Merchants.FirstOrDefault()?.Experience?.Sessions.CloseEventBinding ?? "legacy"} · " +
                $"API {catalog.Merchants.FirstOrDefault()?.Experience?.Sessions.ForgeScriptApiVersion ?? "absente"} · " +
                $"client/vendeur/prix/bilan\n" +
                $"UI boutique · clavier/manette/comparaison/filtres/panier/confirmation · " +
                $"accessibilité lecteur-écran/contraste · colonnes " +
                $"{string.Join(',', catalog.Merchants.FirstOrDefault()?.Experience?.Ui.Columns ?? [])}\n" +
                $"[b]Compagnons[/b] · {catalog.Companions.Count} · {catalog.Companions.Count(item => item.Validation is not null)} validé(s) · " +
                $"{catalog.Companions.Count(item => item.Foundation is not null)} fondation(s) P1 · " +
                $"{catalog.Companions.Count(item => item.Control is not null)} contrôle(s) P1 · " +
                $"{catalog.Companions.Count(item => item.Experience is not null)} expérience(s) P1 · " +
                $"{catalog.Companions.Sum(item => item.Validation?.CommandIds.Count ?? 0)} commande(s)\n" +
                $"Contextes · {catalog.Companions.FirstOrDefault()?.Foundation?.PreRecruitment.ContextId ?? "legacy"} → " +
                $"{catalog.Companions.FirstOrDefault()?.Foundation?.OwnerDialogue.ContextId ?? "legacy"}\n" +
                $"Adhésion · {(catalog.Companions.FirstOrDefault()?.Foundation?.Membership.Atomic == true ? "atomique" : "legacy")} · " +
                $"capacité / propriété / inventaire / position / persistance\n" +
                $"Groupe · C{catalog.Companions.FirstOrDefault()?.Foundation?.Party.CompanionSlots ?? 0} · " +
                $"I{catalog.Companions.FirstOrDefault()?.Foundation?.Party.SummonSlots ?? 0} · " +
                $"F{catalog.Companions.FirstOrDefault()?.Foundation?.Party.PetSlots ?? 0} · " +
                $"A{catalog.Companions.FirstOrDefault()?.Foundation?.Party.TemporaryAllySlots ?? 0} · " +
                $"rôles {RoleCounts(catalog.Companions.FirstOrDefault()?.Foundation?.Party.RoleLimits)} · " +
                $"serveur {catalog.Companions.FirstOrDefault()?.Foundation?.Party.ServerActorLimit ?? 0}\n" +
                $"Contrôle · {catalog.Companions.FirstOrDefault()?.Control?.Ownership.Kind ?? "legacy"} · " +
                $"transferts {string.Join(',', catalog.Companions.FirstOrDefault()?.Control?.Ownership.AllowedTargetKinds ?? [])} · " +
                $"rév {catalog.Companions.FirstOrDefault()?.Control?.Ownership.Revision ?? 0}\n" +
                $"Ordres · {catalog.Companions.FirstOrDefault()?.Control?.Movement.CommandKinds.Count ?? 0} déplacement · " +
                $"{catalog.Companions.FirstOrDefault()?.Control?.Tactical.CommandKinds.Count ?? 0} tactiques · " +
                $"priorité {catalog.Companions.FirstOrDefault()?.Control?.Movement.MinimumPriority ?? 0}–" +
                $"{catalog.Companions.FirstOrDefault()?.Control?.Movement.MaximumPriority ?? 0}\n" +
                $"IA · {catalog.Companions.FirstOrDefault()?.Control?.Ai.Role ?? "legacy"} · " +
                $"A{catalog.Companions.FirstOrDefault()?.Control?.Ai.Autonomy ?? 0:0.##} " +
                $"R{catalog.Companions.FirstOrDefault()?.Control?.Ai.RiskTolerance ?? 0:0.##} " +
                $"U{catalog.Companions.FirstOrDefault()?.Control?.Ai.ResourceUsage ?? 0:0.##} · " +
                $"distance {catalog.Companions.FirstOrDefault()?.Control?.Ai.FollowDistance ?? 0:0.##}\n" +
                $"Équipement · {catalog.Companions.FirstOrDefault()?.Control?.Equipment.Permission ?? "legacy"} · " +
                $"{catalog.Companions.FirstOrDefault()?.Control?.Equipment.LockedItemIds.Count ?? 0} verrou(s) · " +
                $"{catalog.Companions.FirstOrDefault()?.Control?.Equipment.SuggestedLoadout.Count ?? 0} suggestion(s) · " +
                $"propriétaire {catalog.Companions.FirstOrDefault()?.Control?.Equipment.InventoryOwnerId ?? "aucun"}\n" +
                $"Survie · {catalog.Companions.FirstOrDefault()?.Experience?.Death.Mode ?? "legacy"} · " +
                $"récupération {catalog.Companions.FirstOrDefault()?.Experience?.Death.DownedDurationSeconds ?? 0:0.##} s · " +
                $"inventaire {(catalog.Companions.FirstOrDefault()?.Experience?.Death.DropInventory == true ? "déposé" : "conservé")}\n" +
                $"Cohésion · seuils {catalog.Companions.FirstOrDefault()?.Experience?.Cohesion.NavigateThreshold ?? 0:0.##}/" +
                $"{catalog.Companions.FirstOrDefault()?.Experience?.Cohesion.PortalThreshold ?? 0:0.##}/" +
                $"{catalog.Companions.FirstOrDefault()?.Experience?.Cohesion.TeleportThreshold ?? 0:0.##} · " +
                $"ancres {string.Join(',', catalog.Companions.FirstOrDefault()?.Experience?.Cohesion.SafeAnchorTags ?? [])}\n" +
                $"HUD groupe · portraits/santé/ressources/statuts/commandes · " +
                $"entrées {(catalog.Companions.FirstOrDefault()?.Experience?.Hud.Keyboard == true ? "clavier" : "-")}/" +
                $"{(catalog.Companions.FirstOrDefault()?.Experience?.Hud.Gamepad == true ? "manette" : "-")} · " +
                $"accessibilité {(catalog.Companions.FirstOrDefault()?.Experience?.Hud.ScreenReaderLabels == true ? "lecteur-écran" : "-")}/" +
                $"{(catalog.Companions.FirstOrDefault()?.Experience?.Hud.HighContrast == true ? "contraste" : "-")}\n" +
                $"[b]ForgeScript[/b] · {events} binding(s) monde · {catalog.Scripts.Camera.Shots.Count} plan(s) · " +
                $"{catalog.Scripts.TagRouting.Routes.Count} route(s) · {catalog.Scripts.ModuleFlags.Count} flag(s) · {catalog.Scripts.AiSettings.Count} réglage(s) IA\n\n" +
                "[color=#8ce0bd]Prêt pour prévisualisation transactionnelle.[/color]";
        }
        catch (Exception exception)
        {
            _summary.Text = $"[color=#ff7777]Configuration incomplète[/color]\n{exception.Message}";
        }
    }

    private IReadOnlyList<TriggerGameplayVolumeProfile> TriggerGameplayVolumes(Guid profileId)
    {
        var parameters = new Dictionary<TriggerGameplayVolumeKind, (string Key, string Field)>
        {
            [TriggerGameplayVolumeKind.Damage] = ("health", "trigger.volumeDamage"),
            [TriggerGameplayVolumeKind.Heal] = ("health", "trigger.volumeHeal"),
            [TriggerGameplayVolumeKind.Gravity] = ("gravity", "trigger.volumeGravity"),
            [TriggerGameplayVolumeKind.Water] = ("movement", "trigger.volumeWater"),
            [TriggerGameplayVolumeKind.Climb] = ("climb", "trigger.volumeClimb"),
            [TriggerGameplayVolumeKind.NoBuild] = ("build", "trigger.volumeNoBuild"),
            [TriggerGameplayVolumeKind.Camera] = ("camera", "trigger.volumeCamera"),
            [TriggerGameplayVolumeKind.Audio] = ("audio", "trigger.volumeAudio"),
            [TriggerGameplayVolumeKind.Weather] = ("weather", "trigger.volumeWeather"),
            [TriggerGameplayVolumeKind.Checkpoint] = ("checkpoint", "trigger.volumeCheckpoint"),
        };
        return parameters.Select(item => new TriggerGameplayVolumeProfile(
            Stable(profileId, $"advanced-volume-{item.Key}"), item.Key,
            new Dictionary<string, double> { [item.Value.Key] = _number[item.Value.Field].Value },
            new Dictionary<string, string>(), $"{item.Key}VolumeEntered", $"{item.Key}VolumeExited", true))
            .ToArray();
    }

    private static IReadOnlyList<T> Upsert<T>(IReadOnlyList<T> source, T value, Func<T, bool> match) =>
        [.. source.Where(item => !match(item)), value];

    private static IReadOnlyList<T> UpsertPreservingOrder<T>(
        IReadOnlyList<T> source,
        T value,
        Func<T, bool> match)
    {
        var result = new List<T>(source.Count + 1);
        var replaced = false;
        foreach (var item in source)
        {
            if (match(item))
            {
                if (!replaced) result.Add(value);
                replaced = true;
            }
            else
            {
                result.Add(item);
            }
        }
        if (!replaced) result.Add(value);
        return result;
    }

    private BlueprintId? SelectedBlueprint(string key)
    {
        var option = _choice[key];
        return option.ItemCount == 0 || option.Selected < 0 ? null : BlueprintId.Parse(option.GetItemMetadata(option.Selected).AsString());
    }

    private AreaId? SelectedArea(string key)
    {
        var option = _choice[key];
        return option.ItemCount == 0 || option.Selected < 0 ? null : AreaId.Parse(option.GetItemMetadata(option.Selected).AsString());
    }

    private static Guid Stable(Guid owner, string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{owner:D}:{key}"));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guid);
        guid[7] = (byte)((guid[7] & 0x0f) | 0x40);
        guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
        return new Guid(guid);
    }

    private static Guid Stable(Guid owner, Guid key) => Stable(owner, key.ToString("D"));
    private static Guid? GuidOrNull(string value) => string.IsNullOrWhiteSpace(value) ? null : Guid.Parse(value.Trim());
    private static string? Null(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static IReadOnlyList<string> Csv(string value) => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToArray();
    private static IReadOnlyList<double> DoubleCsv(string value) => value
        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Select(item => double.Parse(item, System.Globalization.CultureInfo.InvariantCulture)).Distinct().ToArray();
    private static IReadOnlyDictionary<string, string> KeyValues(string value) => Csv(value)
        .Select(item => item.Split('=', 2, StringSplitOptions.TrimEntries))
        .Where(item => item.Length == 2 && item.All(part => !string.IsNullOrWhiteSpace(part)))
        .GroupBy(item => item[0], StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Last()[1], StringComparer.Ordinal);
    private static int Priority(string verbId, IReadOnlyList<string> order, int fallback)
    {
        for (var index = 0; index < order.Count; index++)
            if (string.Equals(order[index], verbId, StringComparison.OrdinalIgnoreCase))
                return order.Count - index;
        return fallback;
    }

    private static IReadOnlyList<string> Context(string verbId,
        IReadOnlyDictionary<string, string> contexts, bool forbidden)
    {
        if (!contexts.TryGetValue(verbId, out var value)) return [];
        return value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(item => item.StartsWith('!') == forbidden)
            .Select(item => forbidden ? item[1..] : item)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
    private static IReadOnlyDictionary<string, decimal> DecimalValues(string value) => Csv(value)
        .Select(item => item.Split('=', 2, StringSplitOptions.TrimEntries))
        .Where(item => item.Length == 2 && item.All(part => !string.IsNullOrWhiteSpace(part)))
        .GroupBy(item => item[0], StringComparer.Ordinal)
        .ToDictionary(group => group.Key,
            group => decimal.Parse(group.Last()[1], System.Globalization.CultureInfo.InvariantCulture),
            StringComparer.Ordinal);
    private static IReadOnlyList<MerchantPriceCurvePoint> PriceCurve(string value) => Csv(value)
        .Select(item => item.Split(':', 2, StringSplitOptions.TrimEntries))
        .Where(item => item.Length == 2)
        .Select(item => new MerchantPriceCurvePoint(
            decimal.Parse(item[0], System.Globalization.CultureInfo.InvariantCulture),
            decimal.Parse(item[1], System.Globalization.CultureInfo.InvariantCulture)))
        .OrderBy(item => item.BaseValue)
        .ToArray();
    private static IReadOnlyList<Guid> GuidCsv(string value) => Csv(value).Select(Guid.Parse).Distinct().ToArray();
    private static IReadOnlyDictionary<string, Guid> GuidValues(string value) => Csv(value)
        .Select(item => item.Split('=', 2, StringSplitOptions.TrimEntries))
        .Where(item => item.Length == 2 && !string.IsNullOrWhiteSpace(item[0]))
        .GroupBy(item => item[0], StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => Guid.Parse(group.Last()[1]), StringComparer.Ordinal);
    private static IReadOnlyDictionary<string, int> KeyedCounts(string value) => Csv(value)
        .Select(item => item.Split(':', 2, StringSplitOptions.TrimEntries))
        .Where(item => item.Length == 2 && !string.IsNullOrWhiteSpace(item[0]) &&
                       int.TryParse(item[1], out var count) && count >= 0)
        .GroupBy(item => item[0], StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => int.Parse(group.Last()[1]), StringComparer.Ordinal);
    private static string RoleCounts(IReadOnlyDictionary<string, int>? values) => values is null
        ? "aucun"
        : string.Join(',', values.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => $"{item.Key}:{item.Value}"));

    private static void PopulateBlueprints(OptionButton option, ProjectState state, BlueprintKind kind) =>
        Populate(option, state.Project.Blueprints.Where(item => item.Kind == kind).Select(item => (item.DisplayName, item.Id.ToString())));

    private static void PopulateCreatures(OptionButton option, ProjectState state) =>
        Populate(option, state.Project.Blueprints.Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
            .Select(item => (item.DisplayName, item.Id.ToString())));

    private static void PopulateAreas(OptionButton option, ProjectState state) =>
        Populate(option, state.Project.Areas.Select(item => (item.DisplayName, item.Id.ToString())));

    private static void Populate(OptionButton option, IEnumerable<(string Name, string Id)> values)
    {
        var selected = option.ItemCount > 0 && option.Selected >= 0 ? option.GetItemMetadata(option.Selected).AsString() : null;
        option.Clear();
        foreach (var value in values.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            option.AddItem(value.Name);
            option.SetItemMetadata(option.ItemCount - 1, value.Id);
        }
        if (selected is not null)
            for (var index = 0; index < option.ItemCount; index++)
                if (string.Equals(option.GetItemMetadata(index).AsString(), selected, StringComparison.OrdinalIgnoreCase))
                    option.Select(index);
    }

    private static VBoxContainer Tab(TabContainer tabs, string name)
    {
        var scroll = new ScrollContainer
        {
            Name = name,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            ClipContents = true,
        };
        tabs.AddChild(scroll);
        var form = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(form);
        return form;
    }

    private void Target(VBoxContainer form, string key, string label)
    {
        var option = new OptionButton();
        _choice.Add(key, option);
        form.AddChild(Labeled(label, option));
    }

    private void Text(VBoxContainer form, string key, string label, string value)
    {
        var field = new LineEdit { Text = value };
        field.TextChanged += _ => RebuildSummary();
        _text.Add(key, field);
        form.AddChild(Labeled(label, field));
    }

    private void Number(VBoxContainer form, string key, string label, double min, double max, double value, double step)
    {
        var field = new SpinBox { MinValue = min, MaxValue = max, Step = step, Value = value };
        field.ValueChanged += _ => RebuildSummary();
        _number.Add(key, field);
        form.AddChild(Labeled(label, field));
    }

    private void Toggle(VBoxContainer form, string key, string label, bool value)
    {
        var field = new CheckBox { Text = label, ButtonPressed = value };
        field.Toggled += _ => RebuildSummary();
        _check.Add(key, field);
        form.AddChild(field);
    }

    private void Choice(VBoxContainer form, string key, string label, IReadOnlyList<string> values)
    {
        var field = new OptionButton();
        foreach (var value in values) field.AddItem(value);
        field.ItemSelected += _ => RebuildSummary();
        _choice.Add(key, field);
        form.AddChild(Labeled(label, field));
    }

    private static void Info(VBoxContainer form, string text)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeColorOverride("font_color", new("a99bc1"));
        form.AddChild(label);
    }

    private static void Section(VBoxContainer form, string title, string subtitle)
    {
        var heading = new Label { Text = title };
        heading.AddThemeFontSizeOverride("font_size", 16);
        heading.AddThemeColorOverride("font_color", new("ffad70"));
        form.AddChild(heading);
        Info(form, subtitle);
    }

    private static Control Labeled(string label, Control field)
    {
        var row = new VBoxContainer();
        var title = new Label { Text = label };
        title.AddThemeColorOverride("font_color", new("cdbbe9"));
        row.AddChild(title);
        row.AddChild(field);
        return row;
    }

    private static PanelContainer Panel(string title)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 9);
        panel.AddChild(box);
        var label = new Label { Text = title };
        label.AddThemeColorOverride("font_color", new("ffad70"));
        box.AddChild(label);
        return panel;
    }

    private static Button Button(string text, Action action, bool primary = false)
    {
        var button = new Button { Text = text, CustomMinimumSize = new(130, 36) };
        if (primary) button.AddThemeColorOverride("font_color", new("fff4ec"));
        button.Pressed += action;
        return button;
    }
}
#endif
