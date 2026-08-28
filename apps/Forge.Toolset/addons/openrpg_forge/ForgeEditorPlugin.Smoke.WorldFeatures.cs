#if TOOLS
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Areas;
using Forge.Application.Assistant;
using Forge.Application.Assets;
using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Content;
using Forge.Application.Creator;
using Forge.Application.Creatures;
using Forge.Application.Dialogues;
using Forge.Application.Entities;
using Forge.Application.Factions;
using Forge.Application.Gameplay;
using Forge.Application.Items;
using Forge.Application.Modules;
using Forge.Application.Rules;
using Forge.Application.Localization;
using Forge.Application.Lifecycle;
using Forge.Application.Navigation;
using Forge.Application.Sequences;
using Forge.Application.Projects;
using Forge.Application.Quests;
using Forge.Application.Quality;
using Forge.Application.Spatial;
using Forge.Application.Time;
using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Audio;
using Forge.Domain.Assistant;
using Forge.Domain.Assets;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;
using Forge.Domain.Build;
using Forge.Domain.Combat;
using Forge.Domain.Content;
using Forge.Domain.Creatures;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Factions;
using Forge.Domain.Gameplay;
using Forge.Domain.Localization;
using Forge.Domain.Lifecycle;
using Forge.Domain.Items;
using Forge.Domain.Modules;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Quality;
using Forge.Domain.Quests;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Rules;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.Domain.WorldFeatures;
using Forge.EngineAdapter.Assets;
using Forge.EngineAdapter.Audio;
using Forge.EngineAdapter.Rendering;
using Forge.EngineAdapter.World;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Assets;
using Forge.ModuleFormat.Content;
using Forge.ModuleFormat.Distribution;
using Forge.ModuleFormat.Diagnostics;
using Forge.ModuleFormat.Launcher;
using Forge.ModuleFormat.Navigation;
using Forge.ModuleFormat.Playtesting;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Quality;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Content;
using Forge.Network.Replication;
using Forge.RpgKit.Application.Abilities;
using Forge.RpgKit.Application.Bosses;
using Forge.RpgKit.Application.Companions;
using Forge.RpgKit.Application.Combat;
using Forge.RpgKit.Application.Creatures;
using Forge.RpgKit.Application.Dialogues;
using Forge.RpgKit.Application.Doors;
using Forge.RpgKit.Application.Encounters;
using Forge.RpgKit.Application.Factions;
using Forge.RpgKit.Application.Merchants;
using Forge.RpgKit.Application.Placeables;
using Forge.RpgKit.Application.Quests;
using Forge.RpgKit.Application.Triggers;
using Forge.RpgKit.Domain.Merchants;
using Forge.RpgKit.Domain.Companions;
using Forge.RpgKit.Domain.Factions;
using RpgLockState = Forge.RpgKit.Domain.Placeables.LockState;
using RpgActivator = Forge.RpgKit.Domain.Encounters.Activator;
using RpgNetworkAuthority = Forge.RpgKit.Domain.Encounters.NetworkAuthority;
using RpgQueryableEntity = Forge.RpgKit.Domain.Triggers.QueryableEntity;
using RpgTriggerEventTrace = Forge.RpgKit.Domain.Triggers.TriggerEventTrace;
using RpgTriggerRuntimeState = Forge.RpgKit.Domain.Triggers.TriggerRuntimeState;
using Forge.Scripting;
using Godot;

namespace Forge.Toolset.Editor;

public partial class ForgeEditorPlugin
{
    private bool RunWorldFeatureStudioSmokeTest()
    {
        if (_history is null || _worldFeatureStudioWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13N_AB_WORLD_FEATURES_FAILED missing_workspace");
            return false;
        }

        var original = _history.State.Project.Module.WorldFeatures;
        var appliedToHistory = false;
        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "world-features-smoke", Guid.NewGuid().ToString("N"));
        try
        {
            _worldFeatureStudioWindow.Open();
            var visible = _worldFeatureStudioWindow.Visible;
            _worldFeatureStudioWindow.Hide();
            var configured = visible && _worldFeatureStudioWindow.ConfigureForSmokeTest();
            var previewed = configured && _worldFeatureStudioWindow.PreviewForSmokeTest();
            var applied = previewed && _worldFeatureStudioWindow.LastCommandId != Guid.Empty &&
                          _worldFeatureStudioWindow.LastPatchSummary.Contains("placeables=", StringComparison.Ordinal) &&
                          _worldFeatureStudioWindow.ApplyForSmokeTest();
            appliedToHistory = applied;
            if (!applied)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13N_AB_WORLD_FEATURES_FAILED preview_apply_ui " +
                    _worldFeatureStudioWindow.LastDiagnostics);
                return false;
            }

            var catalog = _history.State.Project.Module.WorldFeatures;
            if (catalog.Placeables.Count == 0 || catalog.Doors.Count == 0 || catalog.Triggers.Count == 0 ||
                catalog.Encounters.Count == 0 || catalog.VehicleRoutes.Count == 0 || catalog.Merchants.Count == 0 ||
                catalog.Companions.Count == 0 || catalog.Scripts.Camera.Shots.Count == 0 ||
                catalog.Scripts.TagRouting.Routes.Count == 0 || catalog.Scripts.ModuleFlags.Count == 0 ||
                catalog.Scripts.AiSettings.Count == 0 || _worldFeatureStudioWindow.SectionCount != 8)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13N_AB_WORLD_FEATURES_FAILED canonical_counts");
                return false;
            }

            var placeableAccess = WorldFeatureRuntimeResolver.ResolvePlaceableAccess(catalog.Placeables[0],
                new HashSet<string> { "key:ember" }, new HashSet<string>(), 3);
            var placeableFoundation = WorldFeatureRuntimeResolver.ResolvePlaceableFoundation(catalog.Placeables[0]);
            var missingRequiredPlaceable = WorldFeatureRuntimeResolver.ResolvePlaceableFoundation(
                catalog.Placeables[0] with { Foundation = null, FoundationRequiredAtRuntime = true });
            var placeableUse = WorldFeatureRuntimeResolver.ExecutePlaceableUse(catalog.Placeables[0], Guid.NewGuid(),
                catalog.Placeables[0].Foundation!.Interaction!.Verbs[0].Id, "smoke-player", new Vector3Value(1, 0, 2),
                "success", DateTimeOffset.UnixEpoch);
            string? missingOnUsedDiagnostic = null;
            try
            {
                WorldFeatureRuntimeResolver.ExecutePlaceableUse(catalog.Placeables[0] with
                {
                    Foundation = catalog.Placeables[0].Foundation! with { OnUsed = null, OnUsedRequiredAtRuntime = true },
                }, Guid.NewGuid(), catalog.Placeables[0].Foundation!.Interaction!.Verbs[0].Id, "smoke-player",
                    Vector3Value.Zero, "success", DateTimeOffset.UnixEpoch);
            }
            catch (InvalidOperationException exception)
            {
                missingOnUsedDiagnostic = exception.Message;
            }
            var placeableHandle = new WorldFeatureScriptService().Query(_history.State, "placeable",
                catalog.Placeables[0].Id.ToString("D"), 1).Single();
            var doorDamage = WorldFeatureRuntimeResolver.ApplyDoorDamage(catalog.Doors[0],
                catalog.Doors[0].Damage!.MaximumHealth, 100);
            var doorInteraction = WorldFeatureRuntimeResolver.InteractWithDoor(catalog.Doors[0], "closed", "front",
                new HashSet<string> { "portal.travel" }, 0, new HashSet<string>());
            var doorNavigationClosed = WorldFeatureRuntimeResolver.SynchronizeDoorNavigation(catalog.Doors[0], "closed", 1);
            var doorNavigationOpen = WorldFeatureRuntimeResolver.SynchronizeDoorNavigation(catalog.Doors[0], "opening", .8);
            var doorProfile = catalog.Doors[0];
            var doorAccess = DoorAccessEventFoundationService.Resolve(doorProfile);
            var doorCredentials = new HashSet<string>(["key:ember", "badge:watch"], StringComparer.Ordinal);
            var doorUnlock = DoorAccessEventFoundationService.Unlock(doorProfile,
                new("smoke-player", doorCredentials, new HashSet<string>(), null, 0, false, false), doorCredentials);
            var doorRelock = DoorAccessEventFoundationService.Relock(doorProfile, false, "smoke-player");
            var doorRejection = DoorAccessEventFoundationService.Reject(doorProfile, "smoke-player", "locked");
            var doorTrapIgnored = DoorAccessEventFoundationService.TriggerTrap(doorProfile, "ceiling", "interact",
                "outsider");
            var doorTrapTriggered = DoorAccessEventFoundationService.TriggerTrap(doorProfile, "front", "interact",
                "outsider");
            var doorPortalVetoed = DoorAccessEventFoundationService.RequestPortalUse(doorProfile, "smoke-player",
                "click", new HashSet<string>(["quest:open"]), _ => "party-not-ready");
            var doorPortalAccepted = DoorAccessEventFoundationService.RequestPortalUse(doorProfile, "smoke-player",
                "click", new HashSet<string>(["quest:open"]));
            var doorOpened = DoorAccessEventFoundationService.CompleteStateTransition(doorProfile, "smoke-player",
                "opening", "open", "animation-complete", DateTimeOffset.UnixEpoch);
            var doorClosed = DoorAccessEventFoundationService.CompleteStateTransition(doorProfile, "smoke-player",
                "closing", "closed", "animation-complete", DateTimeOffset.UnixEpoch);
            var doorTypedHandle = new DoorAccessEventScriptService().Get(_history.State, doorProfile.Id);
            var doorGenericHandle = new WorldFeatureScriptService().Query(_history.State, "door",
                doorProfile.Id.ToString("D"), 1).Single();
            var doorOptional = DoorAccessEventFoundationService.Resolve(doorProfile with
            { AccessEvents = null, AccessEventsRequiredAtRuntime = false });
            var doorRequired = DoorAccessEventFoundationService.Resolve(doorProfile with { AccessEvents = null });
            var doorLockEvents = DoorAccessEventFoundationService.ResolveLockEvents(doorProfile);
            var doorUnlockedEvent = DoorAccessEventFoundationService.CompleteLockTransition(doorProfile,
                "smoke-player", "credential", "key:ember", DoorAccessLockState.Locked,
                DoorAccessLockState.Unlocked, DateTimeOffset.UnixEpoch);
            var doorLockedEvent = DoorAccessEventFoundationService.CompleteLockTransition(doorProfile,
                "smoke-player", "relock", null, DoorAccessLockState.Unlocked, DoorAccessLockState.Locked,
                DateTimeOffset.UnixEpoch);
            var doorOptionalLockEvents = DoorAccessEventFoundationService.ResolveLockEvents(doorProfile with
            {
                AccessEvents = doorProfile.AccessEvents! with
                {
                    LockStateEvents = null,
                    LockStateEventsRequiredAtRuntime = false,
                },
            });
            var doorRequiredLockEvents = DoorAccessEventFoundationService.ResolveLockEvents(doorProfile with
            {
                AccessEvents = doorProfile.AccessEvents! with { LockStateEvents = null },
            });
            if (!doorAccess.Available || !doorUnlock.LockResult.Succeeded || !doorUnlock.CredentialConsumed ||
                doorUnlock.RemainingCredentials.Contains("key:ember") || doorRelock.Lock.State != RpgLockState.Locked ||
                doorRejection.Texts.Count != 2 || doorTrapIgnored.Succeeded || !doorTrapTriggered.Succeeded ||
                !doorPortalVetoed.Request.Vetoed || doorPortalVetoed.Completed is not null ||
                doorPortalAccepted.Completed?.RequestEventId != doorPortalAccepted.Request.EventId ||
                doorOpened.Name != "DoorOpened" || doorClosed.Name != "DoorClosed" ||
                doorTypedHandle.Lock.CredentialIds.Count != 1 ||
                doorTypedHandle.PortalUse.BeforeOrRequested != "PortalUseRequested" ||
                doorGenericHandle.Properties["accessEvents"] != bool.TrueString ||
                !doorOptional.OptionalFallback ||
                doorRequired.Diagnostics.All(item => item.Code != "world.door.access_events_required"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R081_DOOR_ACCESS_EVENTS_FAILED runtime_resolution");
                return false;
            }
            if (!doorLockEvents.Available || doorUnlockedEvent.Name != "DoorUnlocked" ||
                doorUnlockedEvent.ActorId != "smoke-player" || doorUnlockedEvent.Method != "credential" ||
                doorUnlockedEvent.Credential != "key:ember" || doorLockedEvent.Name != "DoorLocked" ||
                doorTypedHandle.LockStateEvents?.LockedBinding != "DoorLocked" ||
                doorGenericHandle.Properties["doorUnlocked"] != "DoorUnlocked" ||
                !doorOptionalLockEvents.OptionalFallback ||
                doorRequiredLockEvents.Diagnostics.All(item => item.Code != "world.door.lock_events_required"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R082_DOOR_LOCK_EVENTS_FAILED runtime_resolution");
                return false;
            }
            var encounterProfile = catalog.Encounters[0];
            var encounterComposition = EncounterCompositionFoundationService.Resolve(encounterProfile);
            var encounterDefinition = EncounterCompositionFoundationService.CreateDefinition(encounterProfile);
            var encounterCompositionPreview = EncounterCompositionFoundationService.Preview(encounterProfile, 4, 12,
                new HashSet<string>(["party"], StringComparer.Ordinal));
            var repeatedEncounterCompositionPreview = EncounterCompositionFoundationService.Preview(
                encounterProfile, 4, 12, new HashSet<string>(["party"], StringComparer.Ordinal));
            var encounterAuthoringDiagnostics = EncounterCompositionFoundationService.ValidateAuthoring(
                encounterProfile,
                new(encounterDefinition.Candidates.Select(item => item.CreatureId).ToHashSet(),
                    encounterDefinition.Candidates.Select(item => item.BlueprintId).ToHashSet(StringComparer.Ordinal),
                    new HashSet<string>([encounterDefinition.ZoneId], StringComparer.Ordinal),
                    encounterDefinition.SpawnAnchorIds.ToHashSet(StringComparer.Ordinal),
                    new HashSet<string>([encounterDefinition.Volume.ActivationScript!], StringComparer.Ordinal),
                    encounterDefinition.RewardIds.ToHashSet(StringComparer.Ordinal), 128, 100000));
            var encounterTypedHandle = new EncounterCompositionScriptService().Get(_history.State,
                encounterProfile.Id);
            var encounterGenericHandle = new WorldFeatureScriptService().Query(_history.State, "encounter",
                encounterProfile.Id.ToString("D"), 1).Single();
            var optionalEncounterComposition = EncounterCompositionFoundationService.Resolve(encounterProfile with
            { Composition = null, CompositionRequiredAtRuntime = false });
            var requiredEncounterComposition = EncounterCompositionFoundationService.Resolve(encounterProfile with
            { Composition = null, CompositionRequiredAtRuntime = true });
            if (!encounterComposition.Available || encounterCompositionPreview.Diagnostics.Count != 0 ||
                encounterAuthoringDiagnostics.Count != 0 || encounterCompositionPreview.Composition.TotalCount is < 2 or > 4 ||
                encounterCompositionPreview.Composition.TotalCost > encounterProfile.Composition!.Difficulty.EffectiveBudget ||
                !encounterCompositionPreview.Composition.Candidates.Select(item => item.CreatureId).SequenceEqual(
                    repeatedEncounterCompositionPreview.Composition.Candidates.Select(item => item.CreatureId)) ||
                encounterCompositionPreview.Composition.Summary !=
                repeatedEncounterCompositionPreview.Composition.Summary ||
                encounterTypedHandle.Difficulty.EffectiveBudget is < 36 or > 40 ||
                encounterTypedHandle.Groups.Count != 1 ||
                encounterTypedHandle.Activation.Mode != nameof(EncounterActivationMode.PopulationMaintained) ||
                encounterGenericHandle.Properties["composition"] != bool.TrueString ||
                encounterGenericHandle.Properties["partyProvider"] != "party:forge-generic" ||
                !optionalEncounterComposition.OptionalFallback ||
                requiredEncounterComposition.Diagnostics.All(item =>
                    item.Code != "world.encounter.composition_required"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R083_ENCOUNTER_COMPOSITION_FAILED runtime_resolution");
                return false;
            }
            var encounterScenario = EncounterCompositionFoundationService.ResolveScenario(encounterProfile);
            var encounterScenarioPolicy = EncounterCompositionFoundationService.CreateRuntimePolicy(encounterProfile);
            var acceptedEncounterActivator = new RpgActivator("toolset-player", true, false, false, false, false,
                RpgNetworkAuthority.Server, new HashSet<string>(["party"], StringComparer.Ordinal));
            var deniedEncounterActivator = acceptedEncounterActivator with { IsCompanion = true };
            var encounterScenarioPreview = EncounterCompositionFoundationService.PreviewScenario(encounterProfile,
                acceptedEncounterActivator, 4, 12, new HashSet<string>(["party"], StringComparer.Ordinal),
                DateTimeOffset.UnixEpoch, TimeSpan.FromSeconds(9));
            var encounterScenarioHandle = new EncounterScenarioScriptService().Get(_history.State,
                encounterProfile.Id);
            var optionalEncounterScenario = EncounterCompositionFoundationService.ResolveScenario(encounterProfile with
            { Scenario = null, ScenarioRequiredAtRuntime = false });
            var requiredEncounterScenario = EncounterCompositionFoundationService.ResolveScenario(encounterProfile with
            { Scenario = null, ScenarioRequiredAtRuntime = true });
            if (!encounterScenario.Available || encounterScenarioPreview.Diagnostics.Count != 0 ||
                encounterScenarioPolicy.Anchors.Count != 1 ||
                encounterScenarioPolicy.Anchors[0].Transform.Position != new Vector3Value(2, 0, 4) ||
                !encounterScenarioPreview.ActivatorAccepted ||
                Forge.RpgKit.Domain.Encounters.EncounterCompositionService.CanActivate(deniedEncounterActivator,
                    encounterScenarioPolicy.Activators) ||
                encounterScenarioPreview.Exited.DurationInside != TimeSpan.FromSeconds(9) ||
                encounterScenarioPreview.Exhausted.Cause != "waves-completed" ||
                encounterScenarioPreview.Exhausted.Kills <= 0 || encounterScenarioPreview.Exhausted.Waves != 2 ||
                encounterScenarioPreview.Exhausted.RewardIds?.Count != 1 ||
                encounterScenarioPreview.Phases.Count != 1 || encounterScenarioPreview.Phases[0].Waves.Count != 2 ||
                encounterScenarioPreview.Phases[0].Waves[0].NextWaveId !=
                encounterScenarioPreview.Phases[0].Waves[1].Id ||
                encounterScenarioPreview.Phases[0].Waves[1].VoiceCueId != "audio:encounter-warden-fallen" ||
                encounterScenarioHandle.Activator.RequiredAuthority != nameof(EncounterNetworkAuthority.Server) ||
                encounterScenarioHandle.Lifecycle.ExhaustedBinding != "EncounterExhausted" ||
                encounterGenericHandle.Properties["scenario"] != bool.TrueString ||
                encounterGenericHandle.Properties["waves"] != "2" ||
                !optionalEncounterScenario.OptionalFallback ||
                requiredEncounterScenario.Diagnostics.All(item => item.Code != "world.encounter.scenario_required"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R084_ENCOUNTER_SCENARIO_FAILED runtime_resolution");
                return false;
            }
            var encounterStatistics = EncounterCompositionFoundationService.ResolveStatistics(encounterProfile);
            var encounterStatisticsPreview = EncounterCompositionFoundationService.PreviewStatistics(encounterProfile);
            var repeatedEncounterStatisticsPreview = EncounterCompositionFoundationService.PreviewStatistics(
                encounterProfile);
            var encounterStatisticsHandle = new EncounterStatisticsScriptService().Get(_history.State,
                encounterProfile.Id);
            var optionalEncounterStatistics = EncounterCompositionFoundationService.ResolveStatistics(
                encounterProfile with { Statistics = null, StatisticsRequiredAtRuntime = false });
            var requiredEncounterStatistics = EncounterCompositionFoundationService.ResolveStatistics(
                encounterProfile with { Statistics = null, StatisticsRequiredAtRuntime = true });
            if (!encounterStatistics.Available || encounterStatisticsPreview.Statistics.Runs != 64 ||
                !encounterStatisticsPreview.Statistics.Results.Select(item => item.Seed)
                    .SequenceEqual(Enumerable.Range(840, 64)) ||
                encounterStatisticsPreview.Summary != repeatedEncounterStatisticsPreview.Summary ||
                encounterStatisticsPreview.Statistics.CompositionTotals.Count == 0 ||
                encounterStatisticsPreview.Statistics.MeanCost <= 0 ||
                encounterStatisticsPreview.Statistics.MeanDurationSeconds <= 0 ||
                encounterStatisticsPreview.Statistics.MeanRewardValue <= 0 ||
                encounterStatisticsPreview.Statistics.CostVariance < 0 ||
                encounterStatisticsPreview.Statistics.DurationVariance < 0 ||
                encounterStatisticsPreview.Statistics.RewardVariance < 0 ||
                encounterStatisticsPreview.Statistics.Percentiles?.Count != 3 ||
                encounterStatisticsHandle.Runs != 64 || encounterStatisticsHandle.BaseSeed != 840 ||
                encounterGenericHandle.Properties["statistics"] != bool.TrueString ||
                encounterGenericHandle.Properties["statisticsPercentiles"] != "0.5,0.9,0.99" ||
                !optionalEncounterStatistics.OptionalFallback ||
                requiredEncounterStatistics.Diagnostics.All(item =>
                    item.Code != "world.encounter.statistics_required"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R085_ENCOUNTER_STATISTICS_FAILED runtime_resolution");
                return false;
            }
            var trigger = WorldFeatureRuntimeResolver.EvaluateTrigger(catalog.Triggers[0], 100,
                new HashSet<string> { "key:ember" }, new HashSet<string> { "player" }, false, 1, 0, true);
            var encounter = WorldFeatureRuntimeResolver.EvaluateEncounter(catalog.Encounters[0], 0, 5, false, false, 60);
            var encounterActivation = WorldFeatureRuntimeResolver.ActivateEncounter(catalog.Encounters[0],
                new HashSet<string> { "player" }, "party", true, 10, .5,
                new HashSet<string> { "area.occupied" }, new Dictionary<Guid, int>());
            var route = WorldFeatureRuntimeResolver.SampleVehicleRoute(catalog.VehicleRoutes[0], 1);
            var merchant = WorldFeatureRuntimeResolver.QuoteMerchant(catalog.Merchants[0],
                catalog.Merchants[0].Currencies[0].Id, 10, false, "outside", 0, 5, .1, .2);
            var merchantRuntime = new MerchantWorldTransactionService();
            var merchantResolution = merchantRuntime.Resolve(catalog.Merchants[0]);
            var merchantPolicy = catalog.Merchants[0].Transactions;
            var merchantTrade = merchantPolicy is null || merchantResolution.TransactionHandle is null
                ? null
                : merchantRuntime.Execute(merchantResolution.TransactionHandle, catalog.Merchants[0],
                    new("smoke-player", new Dictionary<string, decimal> { [merchantPolicy.Stock[0].CurrencyId] = 100 },
                        new Dictionary<Guid, int>(), merchantPolicy.BuyerCapacity),
                    new("smoke-store", new Dictionary<string, decimal> { [merchantPolicy.Stock[0].CurrencyId] = 1000 },
                        new Dictionary<Guid, int>(), merchantPolicy.SellerCapacity),
                    new(Guid.NewGuid(), TradeKind.Buy, "smoke-player", "smoke-store",
                        [new(merchantPolicy.Stock[0].ItemBlueprintId.Value, merchantPolicy.Stock[0].VariantId, 1,
                            merchantPolicy.Stock[0].UnitPrice, merchantPolicy.Stock[0].CurrencyId)], []));
            var missingRequiredMerchant = merchantRuntime.Resolve(catalog.Merchants[0] with
            { Transactions = null, TransactionsRequiredAtRuntime = true });
            var merchantHandle = new WorldFeatureScriptService().Query(_history.State, "merchant",
                catalog.Merchants[0].Id.ToString("D"), 1).Single();
            var merchantFoundationAdapter = new MerchantFoundationRuntimeAdapter();
            var merchantFoundationResolution = merchantFoundationAdapter.Resolve(catalog.Merchants[0]);
            var merchantFoundationHandle = merchantFoundationResolution.FoundationHandle;
            if (!merchantFoundationResolution.Available || merchantFoundationHandle is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R091_MERCHANT_FOUNDATION_FAILED runtime_resolution");
                return false;
            }
            var merchantStore = merchantFoundationAdapter.BuildStore(merchantFoundationHandle,
                catalog.Merchants[0]);
            var merchantFoundationContext = new MerchantValidationContext(
                merchantStore.Stock.Select(item => item.ItemDefinitionId).ToHashSet(),
                merchantStore.Vendors.Select(item => item.VendorId).ToHashSet(StringComparer.Ordinal),
                merchantStore.CurrencyIds,
                new HashSet<string>(StringComparer.Ordinal) { "scripts/merchant_rule.forge" },
                merchantStore.Stock.Where(item => item.SupplierId is not null)
                    .Select(item => item.SupplierId!).ToHashSet(StringComparer.Ordinal),
                100);
            var merchantWizard = merchantFoundationAdapter.PreviewWizard(merchantFoundationHandle,
                catalog.Merchants[0], merchantFoundationContext);
            var merchantFoundationStock = catalog.Merchants[0].Foundation!.Stock[0];
            var merchantPrice = merchantFoundationAdapter.Price(merchantFoundationHandle, catalog.Merchants[0],
                merchantFoundationStock.ItemBlueprintId.Value, merchantFoundationStock.BaseValue,
                new(merchantFoundationStock.CurrencyId, .5, .25, .9, .5, new HashSet<string>()));
            var merchantCandidate = new MerchantItemView(merchantFoundationStock.ItemBlueprintId.Value, "relic",
                new HashSet<string>(["merchant-approved"], StringComparer.Ordinal), 3, "author:forge", .9,
                string.Empty);
            var merchantCandidateAccepted = merchantFoundationAdapter.Matches(merchantFoundationHandle,
                catalog.Merchants[0], merchantCandidate,
                (binding, _) => binding == "scripts/merchant_rule.forge");
            var merchantCandidateBlocked = merchantFoundationAdapter.Matches(merchantFoundationHandle,
                catalog.Merchants[0], merchantCandidate with
                {
                    Tags = new HashSet<string>(["merchant-approved", "stolen"], StringComparer.Ordinal),
                }, (_, _) => true);
            var optionalMerchantFoundation = merchantFoundationAdapter.Resolve(catalog.Merchants[0] with
            { Foundation = null, FoundationRequiredAtRuntime = false });
            var requiredMerchantFoundation = merchantFoundationAdapter.Resolve(catalog.Merchants[0] with
            { Foundation = null, FoundationRequiredAtRuntime = true });
            if (merchantStore.Slug != "crypt-vigil-market" || merchantStore.Vendors.Count != 1 ||
                merchantStore.Stock.Count != 2 || merchantStore.Stock[0].Supply != SupplyPolicy.Infinite ||
                merchantStore.Stock[0].SupplierId != "supplier:ember-guild" ||
                merchantStore.Stock[0].RestockInterval != TimeSpan.FromHours(12) ||
                !merchantStore.Stock[0].HiddenConditions.Contains("quest:market-closed") ||
                !merchantWizard.CanApply || merchantPrice.BuyPrice is < 1 or > 500 ||
                merchantPrice.SellPrice is < 1 or > 500 || !merchantCandidateAccepted || merchantCandidateBlocked ||
                merchantHandle.Properties["foundation"] != bool.TrueString ||
                merchantHandle.Properties["storeSlug"] != "crypt-vigil-market" ||
                merchantHandle.Properties["wizard"] != "store-vendor-dialogue-interaction-tests" ||
                merchantHandle.Properties["foundationStock"] != "2" ||
                merchantHandle.Properties["supplyModes"] !=
                    $"{MerchantSupplyPolicy.Infinite},{MerchantSupplyPolicy.Finite}" ||
                merchantHandle.Properties["buyCurve"] != "2" || merchantHandle.Properties["sellCurve"] != "2" ||
                merchantHandle.Properties["customRule"] != "scripts/merchant_rule.forge" ||
                !optionalMerchantFoundation.OptionalFallback ||
                requiredMerchantFoundation.DiagnosticCode != "WORLD_MERCHANT_FOUNDATION_REQUIRED_MISSING")
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R091_MERCHANT_FOUNDATION_FAILED runtime_contract");
                return false;
            }
            var merchantExperienceAdapter = new MerchantStoreExperienceRuntimeAdapter();
            var merchantExperienceResolution = merchantExperienceAdapter.Resolve(catalog.Merchants[0]);
            var merchantExperienceHandle = merchantExperienceResolution.ExperienceHandle;
            if (!merchantExperienceResolution.Available || merchantExperienceHandle is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R092_MERCHANT_EXPERIENCE_FAILED runtime_resolution");
                return false;
            }
            var merchantStoreUi = merchantExperienceAdapter.Ui(merchantExperienceHandle, catalog.Merchants[0]);
            var merchantPriceContext = new StorePriceContext(merchantFoundationStock.CurrencyId, .5,
                new Dictionary<Guid, PricedItem>
                {
                    [merchantFoundationStock.ItemBlueprintId.Value] = merchantPrice,
                });
            var merchantOpenedAt = DateTimeOffset.Parse("2026-08-11T12:00:00Z");
            var merchantOpened = merchantExperienceAdapter.Open(merchantExperienceHandle, catalog.Merchants[0],
                "player:smoke", merchantStore.Vendors[0].VendorId, merchantPriceContext, merchantOpenedAt);
            var merchantSessionTransactionId = Guid.Parse("c2920000-0000-4000-8000-000000000020");
            var merchantClosed = merchantExperienceAdapter.Close(merchantExperienceHandle, catalog.Merchants[0],
                merchantOpened, [merchantSessionTransactionId], 250, 25, merchantOpenedAt.AddMinutes(2));
            var infiniteState = new TradeState(merchantStore,
                new("player:smoke", new Dictionary<string, decimal>
                { [merchantFoundationStock.CurrencyId] = 10_000 }, new Dictionary<Guid, int>(), 1000),
                new("store:smoke", new Dictionary<string, decimal>
                { [merchantFoundationStock.CurrencyId] = 1_000 }, new Dictionary<Guid, int>(), 1000));
            var infinitePurchase = MerchantService.Execute(infiniteState,
                new(Guid.Parse("c2920000-0000-4000-8000-000000000021"), TradeKind.Buy, "player:smoke",
                    "store:smoke",
                    [new(merchantFoundationStock.ItemBlueprintId.Value, merchantFoundationStock.VariantId, 50,
                        merchantFoundationStock.BaseValue, merchantFoundationStock.CurrencyId)], []));
            var optionalMerchantExperience = merchantExperienceAdapter.Resolve(catalog.Merchants[0] with
            { Experience = null, ExperienceRequiredAtRuntime = false });
            var requiredMerchantExperience = merchantExperienceAdapter.Resolve(catalog.Merchants[0] with
            { Experience = null, ExperienceRequiredAtRuntime = true });
            if (!infinitePurchase.Committed || infinitePurchase.State.Store.Stock[0].Quantity !=
                merchantStore.Stock[0].Quantity || infinitePurchase.State.Buyer.Items.GetValueOrDefault(
                    merchantFoundationStock.ItemBlueprintId.Value) != 50 ||
                merchantOpened.CustomerId != "player:smoke" || merchantOpened.PriceContext != merchantPriceContext ||
                merchantClosed.TransactionIds.Single() != merchantSessionTransactionId ||
                merchantClosed.BoughtTotal != 250 || merchantClosed.SoldTotal != 25 ||
                !merchantStoreUi.Keyboard || !merchantStoreUi.Gamepad || !merchantStoreUi.Compare ||
                !merchantStoreUi.Filters || !merchantStoreUi.Cart || !merchantStoreUi.Confirmation ||
                !merchantStoreUi.ScreenReaderLabels || !merchantStoreUi.HighContrast ||
                merchantStoreUi.Columns.Count != 4 || merchantHandle.Properties["experience"] != bool.TrueString ||
                merchantHandle.Properties["sessionApi"] != "1.0.0" ||
                merchantHandle.Properties["sessionOpened"] != "StoreSessionOpened" ||
                merchantHandle.Properties["sessionClosed"] != "StoreSessionClosed" ||
                merchantHandle.Properties["sessionPayload"] !=
                "customer-vendor-price-context-transaction-summary" ||
                merchantHandle.Properties["storeUi"] !=
                "keyboard-gamepad-compare-filters-cart-confirmation" ||
                merchantHandle.Properties["storeUiAccessibility"] != "screen-reader-high-contrast" ||
                !optionalMerchantExperience.OptionalFallback ||
                requiredMerchantExperience.DiagnosticCode != "WORLD_MERCHANT_EXPERIENCE_REQUIRED_MISSING")
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R092_MERCHANT_EXPERIENCE_FAILED runtime_contract");
                return false;
            }
            var placeableOperationsAdapter = new PlaceableOperationsRuntimeAdapter();
            var placeableOperationsResolution = placeableOperationsAdapter.Resolve(catalog.Placeables[0]);
            var placeableOperationsHandle = placeableOperationsResolution.Handle;
            if (!placeableOperationsResolution.Available || placeableOperationsHandle is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R093_PLACEABLE_OPERATIONS_FAILED runtime_resolution");
                return false;
            }
            var placeableOperationsRuntime = placeableOperationsAdapter.Build(placeableOperationsHandle,
                catalog.Placeables[0]);
            var placeableTransition = placeableOperationsAdapter.PreviewTransition(placeableOperationsHandle,
                catalog.Placeables[0], "closed", "open", new HashSet<string> { "quest:crypt-open" });
            var placeableDamage = placeableOperationsAdapter.Damage(placeableOperationsHandle, catalog.Placeables[0],
                80, "force");
            var placeableDestroyed = Forge.RpgKit.Domain.Placeables.PlaceableInteractionService.ApplyDamage(
                placeableDamage.Damageable, 500, "cold");
            var placeableRepaired = placeableOperationsAdapter.Repair(placeableOperationsHandle,
                catalog.Placeables[0], placeableDestroyed.Damageable, 20);
            var placeableBlocked = placeableOperationsAdapter.Authorize(placeableOperationsHandle,
                catalog.Placeables[0], "destroy", new HashSet<string>(), false, "player:smoke", string.Empty);
            var placeableGameMaster = placeableOperationsAdapter.Authorize(placeableOperationsHandle,
                catalog.Placeables[0], "destroy", new HashSet<string>(), true, "gm:smoke", "restore module");
            var placeableUnlocked = placeableOperationsAdapter.Unlock(placeableOperationsHandle,
                catalog.Placeables[0], new("player:smoke", new HashSet<string> { "key:ember" },
                    new HashSet<string>(), null, 0, false, false));
            var placeableJammed = placeableOperationsAdapter.Relock(placeableOperationsHandle,
                catalog.Placeables[0], placeableUnlocked.Lock, true, "player:smoke");
            var optionalPlaceableOperations = placeableOperationsAdapter.Resolve(catalog.Placeables[0] with
            { Operations = null, OperationsRequiredAtRuntime = false });
            var requiredPlaceableOperations = placeableOperationsAdapter.Resolve(catalog.Placeables[0] with
            { Operations = null, OperationsRequiredAtRuntime = true });
            if (placeableOperationsAdapter.Validate(placeableOperationsHandle, catalog.Placeables[0]).Count != 0 ||
                placeableOperationsRuntime.Profile.Container is not
                { CapacitySlots: 24, Persistent: true, Replicated: true } ||
                placeableOperationsRuntime.Profile.InventoryReferences.Single() != "inventory:crypt-pillar" ||
                placeableOperationsRuntime.Profile.StateMachine.States.Count != 6 ||
                placeableTransition.CurrentState != "open" || placeableDamage.Damageable.CurrentHealth != 70 ||
                !placeableDestroyed.Destroyed || placeableDestroyed.DebrisDefinitionId != "debris:crypt-pillar" ||
                placeableRepaired.CurrentHealth != 20 || placeableBlocked.Allowed || !placeableGameMaster.Allowed ||
                !placeableGameMaster.AuditEntry.Contains("restore module", StringComparison.Ordinal) ||
                !placeableUnlocked.Succeeded || placeableUnlocked.Method != "credential" ||
                placeableJammed.Lock.State != RpgLockState.Jammed ||
                placeableHandle.Properties["operations"] != bool.TrueString ||
                placeableHandle.Properties["initialVisualState"] != "closed" ||
                placeableHandle.Properties["protectionAudit"] != "PlaceableProtectionOverride" ||
                !optionalPlaceableOperations.OptionalFallback || requiredPlaceableOperations.DiagnosticCode !=
                "WORLD_PLACEABLE_OPERATIONS_REQUIRED_MISSING")
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R093_PLACEABLE_OPERATIONS_FAILED runtime_contract");
                return false;
            }
            var placeableSecurityEventsAdapter = new PlaceableSecurityEventsRuntimeAdapter();
            var placeableSecurityEventsResolution = placeableSecurityEventsAdapter.Resolve(catalog.Placeables[0]);
            var placeableSecurityEventsHandle = placeableSecurityEventsResolution.Handle;
            if (!placeableSecurityEventsResolution.Available || placeableSecurityEventsHandle is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R094_PLACEABLE_SECURITY_EVENTS_FAILED runtime_resolution");
                return false;
            }
            var placeableSecurityRuntime = placeableSecurityEventsAdapter.Build(placeableSecurityEventsHandle,
                catalog.Placeables[0]);
            var placeableTrapHidden = placeableSecurityEventsAdapter.Detect(placeableSecurityEventsHandle,
                catalog.Placeables[0], 9, "outsider");
            var placeableTrapDetected = placeableSecurityEventsAdapter.Detect(placeableSecurityEventsHandle,
                catalog.Placeables[0], 10, "outsider");
            var placeableTrapDisarmed = placeableSecurityEventsAdapter.Disarm(placeableSecurityEventsHandle,
                catalog.Placeables[0], 12);
            var placeableTrapTriggered = placeableSecurityEventsAdapter.Trigger(placeableSecurityEventsHandle,
                catalog.Placeables[0], "outsider");
            var placeableTrapReset = placeableSecurityEventsAdapter.Reset(placeableSecurityEventsHandle,
                catalog.Placeables[0], placeableTrapDisarmed.Trap);
            var placeableLootSkipped = placeableSecurityEventsAdapter.Spawn(placeableSecurityEventsHandle,
                catalog.Placeables[0], Forge.RpgKit.Domain.Placeables.LootSpawnTiming.Initialize,
                new HashSet<string> { "quest:crypt-open" });
            var placeableLootSpawned = placeableSecurityEventsAdapter.Spawn(placeableSecurityEventsHandle,
                catalog.Placeables[0], Forge.RpgKit.Domain.Placeables.LootSpawnTiming.Open,
                new HashSet<string> { "quest:crypt-open" });
            var placeableLootRestored = placeableSecurityEventsAdapter.Spawn(placeableSecurityEventsHandle,
                catalog.Placeables[0], Forge.RpgKit.Domain.Placeables.LootSpawnTiming.Open,
                new HashSet<string> { "quest:crypt-open" }, placeableLootSpawned.Spawner);
            var placeableContainerOpened = placeableSecurityEventsAdapter.ContainerStateChanged(
                placeableSecurityEventsHandle, catalog.Placeables[0], true, "player:smoke", "success",
                "closed", "server", DateTimeOffset.UnixEpoch);
            var placeableContainerClosed = placeableSecurityEventsAdapter.ContainerStateChanged(
                placeableSecurityEventsHandle, catalog.Placeables[0], false, "player:smoke", "success",
                "open", "server", DateTimeOffset.UnixEpoch);
            var placeableLockVetoed = placeableSecurityEventsAdapter.ChangeLockState(
                placeableSecurityEventsHandle, catalog.Placeables[0], RpgLockState.Unlocked, "credential",
                "key:ember", "player:smoke", _ => "quest-gate");
            var placeableLockChanged = placeableSecurityEventsAdapter.ChangeLockState(
                placeableSecurityEventsHandle, catalog.Placeables[0], RpgLockState.Unlocked, "credential",
                "key:ember", "player:smoke");
            var placeableLootItemId = placeableSecurityRuntime.LootTable.Entries.Single().ItemDefinitionId;
            var placeableContentsChanged = placeableSecurityEventsAdapter.ContentsChanged(
                placeableSecurityEventsHandle, catalog.Placeables[0], 4,
                [new(placeableLootItemId, -1, "take", "inventory:crypt-pillar", "player:smoke")],
                "player-loot", "player:smoke");
            var optionalPlaceableSecurityEvents = placeableSecurityEventsAdapter.Resolve(catalog.Placeables[0] with
            { SecurityEvents = null, SecurityEventsRequiredAtRuntime = false });
            var requiredPlaceableSecurityEvents = placeableSecurityEventsAdapter.Resolve(catalog.Placeables[0] with
            { SecurityEvents = null, SecurityEventsRequiredAtRuntime = true });
            if (placeableSecurityRuntime.Trap.PayloadId != "damage:ember-burst" ||
                placeableTrapHidden.Succeeded || !placeableTrapDetected.Succeeded ||
                placeableTrapDisarmed.Trap.Armed || !placeableTrapTriggered.Succeeded ||
                !placeableTrapReset.Armed || placeableLootSkipped.EventName != "LootSpawnSkipped" ||
                placeableLootSpawned.Results.Count != 1 || !placeableLootRestored.ReusedSavedState ||
                placeableContainerOpened.Binding != "ContainerOpened" ||
                placeableContainerOpened.Payload.PreviousState != "closed" ||
                placeableContainerOpened.Payload.Authority != "server" ||
                placeableContainerClosed.Binding != "ContainerClosed" ||
                !placeableLockVetoed.Changing.Payload.Vetoed || placeableLockVetoed.Changed is not null ||
                placeableLockChanged.Changing.Binding != "LockStateChanging" ||
                placeableLockChanged.Changed?.Binding != "LockStateChanged" ||
                placeableLockChanged.Changed?.Payload.Method != "credential" ||
                placeableContentsChanged.Binding != "ContainerContentsChanged" ||
                placeableContentsChanged.Payload.PreviousRevision != 4 ||
                placeableContentsChanged.Payload.CurrentRevision != 5 ||
                placeableContentsChanged.Payload.Delta.Single().QuantityDelta != -1 ||
                placeableHandle.Properties["securityEvents"] != bool.TrueString ||
                !placeableHandle.Properties["trap"].Contains("payload:damage:ember-burst", StringComparison.Ordinal) ||
                !placeableHandle.Properties["lootSpawner"].Contains("timing:Open", StringComparison.Ordinal) ||
                placeableHandle.Properties["containerOpened"] != "ContainerOpened" ||
                placeableHandle.Properties["lockStateChanging"] != "LockStateChanging" ||
                placeableHandle.Properties["contentsChangedPayload"] !=
                "transactional-delta-cause-revision-actor" ||
                !optionalPlaceableSecurityEvents.OptionalFallback ||
                requiredPlaceableSecurityEvents.DiagnosticCode !=
                "WORLD_PLACEABLE_SECURITY_EVENTS_REQUIRED_MISSING")
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R094_PLACEABLE_SECURITY_EVENTS_FAILED runtime_contract");
                return false;
            }
            var placeableCompositionAdapter = new PlaceableCompositionEventsRuntimeAdapter();
            var placeableCompositionResolution = placeableCompositionAdapter.Resolve(catalog.Placeables[0]);
            var placeableCompositionHandle = placeableCompositionResolution.Handle;
            if (!placeableCompositionResolution.Available || placeableCompositionHandle is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R095_PLACEABLE_COMPOSITION_EVENTS_FAILED runtime_resolution");
                return false;
            }
            var placeableCompositionRuntime = placeableCompositionAdapter.Build(placeableCompositionHandle,
                catalog.Placeables[0]);
            var placeableDamageEvent = placeableCompositionAdapter.Damage(placeableCompositionHandle,
                catalog.Placeables[0], "player:smoke", 20, "force", 50, 30,
                new Dictionary<string, string> { ["weapon"] = "hammer" });
            var placeableDestroyedEvent = placeableCompositionAdapter.Damage(placeableCompositionHandle,
                catalog.Placeables[0], "player:smoke", 30, "force", 30, 0);
            var placeableAttachment = placeableCompositionRuntime.Attachments.Single();
            var placeableAttached = placeableCompositionAdapter.Attach(placeableCompositionHandle,
                catalog.Placeables[0], placeableAttachment.Id);
            var placeableVerbs = placeableCompositionAdapter.ResolveVerbs(placeableCompositionHandle,
                catalog.Placeables[0], new HashSet<string>
                    { "unlocked", "interlocutor", "portable", "climbable", "ash-rune" });
            var placeableAnchoredVerbs = placeableCompositionAdapter.ResolveVerbs(placeableCompositionHandle,
                catalog.Placeables[0], new HashSet<string>
                    { "unlocked", "interlocutor", "portable", "climbable", "ash-rune", "anchored" });
            var optionalPlaceableComposition = placeableCompositionAdapter.Resolve(catalog.Placeables[0] with
            { CompositionEvents = null, CompositionEventsRequiredAtRuntime = false });
            var requiredPlaceableComposition = placeableCompositionAdapter.Resolve(catalog.Placeables[0] with
            { CompositionEvents = null, CompositionEventsRequiredAtRuntime = true });
            if (placeableDamageEvent.Binding != "DamageApplied" ||
                placeableDamageEvent.Payload.Name != "DamageApplied" ||
                placeableDamageEvent.Payload.EntityKind != "placeable" ||
                placeableDamageEvent.Payload.Context["weapon"] != "hammer" ||
                placeableDestroyedEvent.Binding != "EntityDestroyed" ||
                placeableDestroyedEvent.Payload.CurrentHealth != 0 ||
                placeableAttached.Attached.Transform != placeableCompositionRuntime.Sockets.Single().LocalTransform ||
                placeableAttached.Attached.PrefabId != "prefab:crypt-pillar-lever" ||
                !placeableAttached.Patch.Contains("attach:/sockets/lever", StringComparison.Ordinal) ||
                !placeableAttached.InversePatch.Contains("detach:/sockets/lever", StringComparison.Ordinal) ||
                placeableVerbs.Count != 8 || placeableVerbs[0].Id != "push" ||
                placeableVerbs[1].Id != "take" || placeableVerbs[2].Id != "custom-ring" ||
                placeableAnchoredVerbs.Any(item => item.Id == "push") ||
                placeableHandle.Properties["compositionEvents"] != bool.TrueString ||
                placeableHandle.Properties["damageApplied"] != "DamageApplied" ||
                placeableHandle.Properties["entityDestroyed"] != "EntityDestroyed" ||
                !placeableHandle.Properties["attachmentSockets"].Contains("lever:mechanism", StringComparison.Ordinal) ||
                !placeableHandle.Properties["multiVerbs"].Contains("custom-ring", StringComparison.Ordinal) ||
                !placeableHandle.Properties["verbBindings"].Contains("RingRequested", StringComparison.Ordinal) ||
                !optionalPlaceableComposition.OptionalFallback ||
                requiredPlaceableComposition.DiagnosticCode !=
                "WORLD_PLACEABLE_COMPOSITION_EVENTS_REQUIRED_MISSING")
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R095_PLACEABLE_COMPOSITION_EVENTS_FAILED runtime_contract");
                return false;
            }
            var triggerAdvancedAdapter = new TriggerAdvancedRuntimeAdapter();
            var triggerAdvancedResolution = triggerAdvancedAdapter.Resolve(catalog.Triggers[0]);
            var triggerAdvancedHandle = triggerAdvancedResolution.Handle;
            if (!triggerAdvancedResolution.Available || triggerAdvancedHandle is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R096_TRIGGER_ADVANCED_FAILED runtime_resolution");
                return false;
            }
            var triggerAdvancedRuntime = triggerAdvancedAdapter.Build(triggerAdvancedHandle, catalog.Triggers[0]);
            var triggerDetected = triggerAdvancedAdapter.Detect(triggerAdvancedHandle, catalog.Triggers[0], 10,
                "outsider");
            var triggerDisarmed = triggerAdvancedAdapter.Disarm(triggerAdvancedHandle, catalog.Triggers[0], 12);
            var triggerReset = triggerAdvancedAdapter.Reset(triggerAdvancedHandle, catalog.Triggers[0],
                triggerDisarmed.Trap);
            var triggerEntity = new RpgQueryableEntity("player:smoke", true,
                new HashSet<string> { "player", "hero" }, triggerAdvancedRuntime.TargetFilter.FactionIds,
                new HashSet<string> { "inventory", "health" }, "party", "heroes",
                new Dictionary<string, string> { ["quest"] = "active" });
            var triggerMatches = triggerAdvancedAdapter.Matches(triggerAdvancedHandle, catalog.Triggers[0],
                triggerEntity);
            var triggerDamageEffect = triggerAdvancedAdapter.Apply(triggerAdvancedHandle, catalog.Triggers[0],
                TriggerGameplayVolumeKind.Damage, triggerEntity.Id);
            var triggerNow = DateTimeOffset.UnixEpoch.AddSeconds(10);
            var triggerState = new RpgTriggerRuntimeState(
                new Dictionary<string, DateTimeOffset> { [triggerEntity.Id] = triggerNow.AddSeconds(-3) },
                new Dictionary<string, DateTimeOffset> { [triggerEntity.Id] = triggerNow.AddSeconds(-2) },
                new HashSet<string> { triggerEntity.Id }, false);
            var triggerOverlay = triggerAdvancedAdapter.Debug(triggerAdvancedHandle, catalog.Triggers[0],
                triggerState, triggerNow,
                [new RpgTriggerEventTrace(triggerNow, "DamageVolumeEntered", triggerEntity.Id, "fired")]);
            var triggerScriptHandle = new WorldFeatureScriptService().Query(_history.State, "trigger",
                catalog.Triggers[0].Id.ToString("D"), 1).Single();
            var optionalTriggerAdvanced = triggerAdvancedAdapter.Resolve(catalog.Triggers[0] with
            { Advanced = null, AdvancedRequiredAtRuntime = false });
            var requiredTriggerAdvanced = triggerAdvancedAdapter.Resolve(catalog.Triggers[0] with
            { Advanced = null, AdvancedRequiredAtRuntime = true });
            if (triggerAdvancedRuntime.GameplayVolumes.Count != 10 || !triggerDetected.Succeeded ||
                !triggerDisarmed.Succeeded || triggerDisarmed.Trap.Armed || !triggerReset.Trap.Armed ||
                !triggerMatches || triggerDamageEffect.EventName != "DamageVolumeEntered" ||
                triggerDamageEffect.Changes["health"] != "-10" ||
                triggerOverlay.Occupants.Single() != triggerEntity.Id ||
                triggerOverlay.CooldownsRemaining[triggerEntity.Id] <= TimeSpan.Zero ||
                !triggerOverlay.FilterSummary.Contains("required:player", StringComparison.Ordinal) ||
                triggerOverlay.EventTrace.Single().EventName != "DamageVolumeEntered" ||
                triggerScriptHandle.Properties["advanced"] != bool.TrueString ||
                !triggerScriptHandle.Properties["trapBindings"].Contains("TrapTriggered", StringComparison.Ordinal) ||
                !triggerScriptHandle.Properties["gameplayVolumes"].Contains("Checkpoint", StringComparison.Ordinal) ||
                !triggerScriptHandle.Properties["occupancyDebug"].Contains("maximum:100", StringComparison.Ordinal) ||
                !optionalTriggerAdvanced.OptionalFallback || requiredTriggerAdvanced.DiagnosticCode !=
                "WORLD_TRIGGER_ADVANCED_REQUIRED_MISSING")
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R096_TRIGGER_ADVANCED_FAILED runtime_contract");
                return false;
            }
            var companion = WorldFeatureRuntimeResolver.EvaluateCompanion(catalog.Companions[0], "owner", .2,
                true, 2, true, true);
            var companionValidation = CompanionValidationRuntimeResolver.Resolve(catalog.Companions[0],
                catalog.Companions[0].Validation!.CrossZoneNavigation.AllowedAreaIds.FirstOrDefault());
            var missingRequiredValidation = CompanionValidationRuntimeResolver.Resolve(
                catalog.Companions[0] with { Validation = null, ValidationRequiredAtRuntime = true });
            var companionHandle = new WorldFeatureScriptService().Query(_history.State, "companion",
                catalog.Companions[0].Id.ToString("D"), 1).Single();
            var companionProfile = catalog.Companions[0];
            var companionFoundation = CompanionFoundationService.Resolve(companionProfile);
            var companionComponent = CompanionFoundationService.CreateComponent(companionProfile, string.Empty,
                TransformValue.Identity);
            var foundationPolicy = companionFoundation.Foundation!;
            var companionParty = CompanionFoundationService.CreatePartyState(companionProfile, "party:smoke", [],
                new Dictionary<string, decimal> { [foundationPolicy.Recruitable.CurrencyId] = 100 },
                foundationPolicy.Recruitable.Conditions.ToHashSet(StringComparer.Ordinal), 1);
            var companionWorld = new CompanionWorldState(companionComponent, companionParty,
                new Dictionary<string, string>
                {
                    [companionComponent.InventoryId] = companionComponent.Id.ToString("D"),
                }, TransformValue.Identity);
            var companionRecruitPreview = CompanionFoundationService.Recruit(companionProfile, companionWorld,
                "player:smoke", true);
            var companionRecruitApply = CompanionFoundationService.Recruit(companionProfile, companionWorld,
                "player:smoke", false);
            var companionDismiss = CompanionFoundationService.Dismiss(companionProfile,
                companionRecruitApply.Result.State, "player:smoke",
                new TransformValue(new Vector3Value(2, 0, 3), QuaternionValue.Identity, Vector3Value.One), false);
            var optionalCompanionFoundation = CompanionFoundationService.Resolve(companionProfile with
            { Foundation = null, FoundationRequiredAtRuntime = false });
            var requiredCompanionFoundation = CompanionFoundationService.Resolve(companionProfile with
            { Foundation = null, FoundationRequiredAtRuntime = true });
            var companionPartyDiagnostics = CompanionFoundationService.ValidateParty(companionProfile,
                new PartyRoster([companionComponent], 1, 1, 1));
            var companionControl = CompanionControlFoundationService.Resolve(companionProfile);
            var controlProfile = companionControl.Profile!;
            var ownershipPreview = CompanionControlFoundationService.Transfer(companionProfile,
                companionComponent.Id, "party", new HashSet<string>(["party:smoke"], StringComparer.Ordinal),
                "player:owner", "toolset-preview", DateTimeOffset.UnixEpoch, true);
            var ownershipApply = CompanionControlFoundationService.Transfer(companionProfile,
                companionComponent.Id, "party", new HashSet<string>(["party:smoke"], StringComparer.Ordinal),
                "player:owner", "toolset-preview", DateTimeOffset.UnixEpoch, false);
            var movementOrder = CompanionControlFoundationService.IssueMovement(companionProfile, "move",
                "player:owner", null, new Vector3Value(4, 0, 8), 20, []);
            var tacticalOrder = CompanionControlFoundationService.IssueTactical(companionProfile, "focus-fire",
                "player:owner", "enemy:warden", "aggressive", null, 50, movementOrder.Queue);
            var suggestedEquipment = controlProfile.Equipment.SuggestedLoadout["main-hand"];
            var equipmentChange = CompanionControlFoundationService.Equip(companionProfile,
                new Dictionary<string, Guid>(), suggestedEquipment, "main-hand", "player:owner");
            var deniedEquipment = CompanionControlFoundationService.Equip(companionProfile,
                new Dictionary<string, Guid>(), suggestedEquipment, "main-hand", "intruder");
            var optionalCompanionControl = CompanionControlFoundationService.Resolve(companionProfile with
            { Control = null, ControlRequiredAtRuntime = false });
            var requiredCompanionControl = CompanionControlFoundationService.Resolve(companionProfile with
            { Control = null, ControlRequiredAtRuntime = true });
            var companionExperience = CompanionExperienceFoundationService.Resolve(companionProfile);
            var deathTransitions = new[]
            {
                "downed", "permanent-death", "recoverable-corpse", "resurrection", "replacement", "memorial",
            }.Select(mode => CompanionExperienceFoundationService.Defeat(companionProfile with
            {
                Experience = companionProfile.Experience! with
                {
                    Death = companionProfile.Experience.Death with { Mode = mode },
                },
            }, DateTimeOffset.UnixEpoch)).ToArray();
            var cohesionAnchors = new List<(string Id, Vector3Value Position, IReadOnlySet<string> Tags, bool Navigable)>
            {
                ("anchor:party-safe", new Vector3Value(2, 0, 2),
                    new HashSet<string>(["party.safe"], StringComparer.Ordinal), true),
            };
            var navigationRecovery = CompanionExperienceFoundationService.Recover(companionProfile,
                new CohesionContext(15, true, false, false, false, cohesionAnchors));
            var portalRecovery = CompanionExperienceFoundationService.Recover(companionProfile,
                new CohesionContext(40, false, true, false, false, cohesionAnchors));
            var teleportRecovery = CompanionExperienceFoundationService.Recover(companionProfile,
                new CohesionContext(80, false, false, false, false, cohesionAnchors));
            var blockedRecovery = CompanionExperienceFoundationService.Recover(companionProfile,
                new CohesionContext(80, false, false, true, false, cohesionAnchors));
            var partyHud = CompanionExperienceFoundationService.CreateHud(companionProfile);
            var optionalCompanionExperience = CompanionExperienceFoundationService.Resolve(companionProfile with
            { Experience = null, ExperienceRequiredAtRuntime = false });
            var requiredCompanionExperience = CompanionExperienceFoundationService.Resolve(companionProfile with
            { Experience = null, ExperienceRequiredAtRuntime = true });
            var scripts = WorldFeatureRuntimeResolver.ResolveScript(catalog.Scripts, "door.ash", "opened",
                cameraSequence: true);
            var placeableRuntimeValid = placeableAccess.Allowed && placeableFoundation.Available &&
                                        placeableFoundation.SnappedTransform?.Position ==
                                        new Vector3Value(1.25f, 0, -2.5f) &&
                                        placeableFoundation.Overlay.Contains("pivot=0,0.5,0", StringComparison.Ordinal) &&
                                        missingRequiredPlaceable.DiagnosticCode ==
                                        "WORLD_PLACEABLE_FOUNDATION_REQUIRED_MISSING" &&
                                        placeableUse.EventBinding == "InteractionExecuted" &&
                                        placeableUse.ActorId == "smoke-player" &&
                                        placeableUse.TargetPoint == new Vector3Value(1, 0, 2) &&
                                        placeableUse.Result == "success" &&
                                        missingOnUsedDiagnostic == "WORLD_PLACEABLE_ON_USED_REQUIRED_MISSING" &&
                                        placeableHandle.Properties["foundation"] == bool.TrueString &&
                                        placeableHandle.Properties["role"] == "interactable" &&
                                        placeableHandle.Properties["onUsed"] == bool.TrueString &&
                                        placeableHandle.Properties["validation"] == bool.TrueString;
            var doorRuntimeValid = doorDamage.Breached && doorInteraction.Accepted &&
                                   doorInteraction.CurrentState == "opening" &&
                                   catalog.Doors[0].Foundation?.States.Count == 9 &&
                                   catalog.Doors[0].Foundation?.Links.Count == 0 &&
                                   doorNavigationClosed.ObstacleEnabled && !doorNavigationClosed.LinkEnabled &&
                                   !doorNavigationOpen.ObstacleEnabled && doorNavigationOpen.LinkEnabled &&
                                   doorNavigationClosed.Visualization.Contains("blocked", StringComparison.Ordinal);
            var worldRuntimeValid = trigger.Fired && encounter.Spawn && encounterActivation.Activated &&
                                    catalog.Encounters[0].Foundation?.Candidates.Count == 1 && route.AtStop &&
                                    merchant.Accepted && merchantResolution.Available && merchantTrade?.Committed == true &&
                                    missingRequiredMerchant.DiagnosticCode ==
                                    "WORLD_MERCHANT_TRANSACTIONS_REQUIRED_MISSING" &&
                                    merchantHandle.Properties["transactions"] == bool.TrueString &&
                                    merchantHandle.Properties["stock"] == "2" && scripts.ScriptId is not null &&
                                    scripts.CameraControlLocked;
            var companionRuntimeValid = companion.DirectControl && companionValidation.Available &&
                                        companionValidation.DestinationAllowed &&
                                        missingRequiredValidation.Diagnostics.Any(item =>
                                            item.Code == "world.companion.validation_required") &&
                                        companionHandle.Properties["validation"] == bool.TrueString &&
                                        companionHandle.Properties["commands"] == "12";
            var companionFoundationValid = companionHandle.Properties["foundation"] == bool.TrueString &&
                                           companionHandle.Properties["preRecruitmentContext"] == "not-recruited" &&
                                           companionHandle.Properties["ownerContext"] == "recruited-by-speaker" &&
                                           companionHandle.Properties["companionSlots"] == "3" &&
                                           companionRecruitPreview.Result.Committed &&
                                           companionRecruitApply.Result.Committed &&
                                           companionRecruitPreview.TransactionId == companionRecruitApply.TransactionId &&
                                           companionDismiss.Result.Committed &&
                                           optionalCompanionFoundation.OptionalFallback &&
                                           requiredCompanionFoundation.Diagnostics.Any(item =>
                                               item.Code == "RPG_COMPANION_FOUNDATION_REQUIRED") &&
                                           companionPartyDiagnostics.Count == 0;
            var companionControlValid = companionHandle.Properties["control"] == bool.TrueString &&
                                        companionHandle.Properties["controlOwnership"] == "individual" &&
                                        companionHandle.Properties["movementCommands"] == "6" &&
                                        companionHandle.Properties["tacticalCommands"] == "6" &&
                                        companionHandle.Properties["aiRole"] == "Support" &&
                                        companionHandle.Properties["equipmentPermission"] == "full" &&
                                        ownershipPreview.PlanId == ownershipApply.PlanId &&
                                        ownershipPreview.TransactionId == ownershipApply.TransactionId &&
                                        ownershipApply.Current.Kind == CompanionOwnershipKind.Party &&
                                        movementOrder.Accepted && tacticalOrder.Accepted && equipmentChange.Accepted &&
                                        !deniedEquipment.Accepted &&
                                        CompanionControlService.ValidateAi(
                                            CompanionControlFoundationService.CreateAi(companionProfile)).Count == 0 &&
                                        optionalCompanionControl.OptionalFallback &&
                                        requiredCompanionControl.Diagnostics.Any(item =>
                                            item.Code == "RPG_COMPANION_CONTROL_REQUIRED");
            var companionExperienceValid = companionExperience.Profile is not null && deathTransitions.Length == 6 &&
                                           deathTransitions[0].State.Downed &&
                                           deathTransitions[1].EventName == "CompanionPermanentlyDied" &&
                                           deathTransitions[2].State.CorpseAvailable &&
                                           deathTransitions[3].Effects.Contains(
                                               "requires:resource:ember", StringComparer.Ordinal) &&
                                           deathTransitions[4].State.ReplacementId == "creature:elyra-heir" &&
                                           deathTransitions[5].State.Memorialized &&
                                           navigationRecovery.Kind == CohesionRecoveryKind.Navigate &&
                                           portalRecovery.Kind == CohesionRecoveryKind.FollowPortal &&
                                           teleportRecovery.Kind == CohesionRecoveryKind.SafeTeleport &&
                                           blockedRecovery.Kind == CohesionRecoveryKind.None &&
                                           partyHud is
                                           {
                                               Portraits: true, Health: true, Resources: true, Statuses: true,
                                               QuickCommands: true, Reorder: true, Keyboard: true, Gamepad: true,
                                               ScreenReaderLabels: true, HighContrast: true
                                           } &&
                                           CompanionExperienceFoundationService.Validate(companionProfile).Count == 0 &&
                                           optionalCompanionExperience.OptionalFallback &&
                                           requiredCompanionExperience.Diagnostics.Any(item =>
                                               item.Code == "RPG_COMPANION_EXPERIENCE_REQUIRED") &&
                                           companionHandle.Properties["experience"] == bool.TrueString &&
                                           companionHandle.Properties["deathMode"] == "recoverable-corpse" &&
                                           companionHandle.Properties["cohesionThresholds"] == "10,30,60" &&
                                           companionHandle.Properties["partyHudInputs"] == "keyboard-gamepad" &&
                                           companionHandle.Properties["partyHudAccessibility"] ==
                                           "screen-reader-high-contrast";
            if (!placeableRuntimeValid || !doorRuntimeValid || !worldRuntimeValid || !companionRuntimeValid ||
                !companionFoundationValid || !companionControlValid || !companionExperienceValid)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13N_AB_WORLD_FEATURES_FAILED runtime_resolution " +
                            $"placeable={placeableRuntimeValid.ToString().ToLowerInvariant()} " +
                            $"door={doorRuntimeValid.ToString().ToLowerInvariant()} " +
                            $"world={worldRuntimeValid.ToString().ToLowerInvariant()} " +
                            $"companion={companionRuntimeValid.ToString().ToLowerInvariant()} " +
                            $"foundation={companionFoundationValid.ToString().ToLowerInvariant()} " +
                            $"control={companionControlValid.ToString().ToLowerInvariant()} " +
                            $"experience={companionExperienceValid.ToString().ToLowerInvariant()}");
                if (!doorRuntimeValid)
                {
                    GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13N_AB_WORLD_FEATURES_DOOR_DIAGNOSTIC " +
                                $"breached={doorDamage.Breached.ToString().ToLowerInvariant()} " +
                                $"accepted={doorInteraction.Accepted.ToString().ToLowerInvariant()} " +
                                $"state={doorInteraction.CurrentState} " +
                                $"states={catalog.Doors[0].Foundation?.States.Count ?? -1} " +
                                $"links={catalog.Doors[0].Foundation?.Links.Count ?? -1} " +
                                $"closed_obstacle={doorNavigationClosed.ObstacleEnabled.ToString().ToLowerInvariant()} " +
                                $"closed_link={doorNavigationClosed.LinkEnabled.ToString().ToLowerInvariant()} " +
                                $"open_obstacle={doorNavigationOpen.ObstacleEnabled.ToString().ToLowerInvariant()} " +
                                $"open_link={doorNavigationOpen.LinkEnabled.ToString().ToLowerInvariant()} " +
                                $"closed_visualization={doorNavigationClosed.Visualization}");
                }
                return false;
            }

            var validCompanion = catalog.Companions[0];
            var invalidCompanion = validCompanion with
            {
                Validation = validCompanion.Validation! with { CommandIds = ["follow", "follow"] },
            };
            var invalidCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion, catalog.Provenance,
                catalog.Placeables, catalog.Doors, catalog.Triggers, catalog.Encounters, catalog.VehicleRoutes,
                catalog.Merchants, [invalidCompanion], catalog.Scripts);
            var invalidPreview = _worldFeatureHandler.Handle(_history.State, new SetWorldFeatureCatalogCommand(
                CommandId.From(Guid.NewGuid()), _history.State.Revision, invalidCatalog, true));
            if (invalidPreview.Succeeded || invalidPreview.Diagnostics.All(item => item.Code != "world.companion.commands_invalid"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R037_COMPANION_FAILED invalid_prebuild_validation");
                return false;
            }

            var invalidFoundationCompanion = validCompanion with
            {
                Foundation = validCompanion.Foundation! with
                {
                    Recruitable = validCompanion.Foundation!.Recruitable with { Enabled = false, Cost = -1 },
                    Party = validCompanion.Foundation.Party with { CompanionSlots = 0, ServerActorLimit = 0 },
                },
            };
            var invalidFoundationCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion,
                catalog.Provenance, catalog.Placeables, catalog.Doors, catalog.Triggers, catalog.Encounters,
                catalog.VehicleRoutes, catalog.Merchants, [invalidFoundationCompanion], catalog.Scripts);
            var invalidFoundationPreview = _worldFeatureHandler.Handle(_history.State,
                new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
                    invalidFoundationCatalog, true));
            if (invalidFoundationPreview.Succeeded ||
                invalidFoundationPreview.Diagnostics.All(item => item.Code != "world.companion.recruitable_invalid") ||
                invalidFoundationPreview.Diagnostics.All(item => item.Code != "world.companion.party_policy_invalid"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R074_COMPANION_FOUNDATION_FAILED invalid_prebuild_validation");
                return false;
            }

            var invalidControlCompanion = validCompanion with
            {
                Control = validCompanion.Control! with
                {
                    Ownership = validCompanion.Control!.Ownership with { ControllerIds = [], Revision = -1 },
                    Movement = validCompanion.Control.Movement with { CommandKinds = ["move"] },
                    Tactical = validCompanion.Control.Tactical with { CommandKinds = ["attack"], Stances = [] },
                    Ai = validCompanion.Control.Ai with { Autonomy = 2, PlanId = Guid.Empty },
                    Equipment = validCompanion.Control.Equipment with
                    {
                        SuggestedLoadout = new Dictionary<string, Guid>(),
                        AllowCompare = false,
                    },
                },
            };
            var invalidControlCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion,
                catalog.Provenance, catalog.Placeables, catalog.Doors, catalog.Triggers, catalog.Encounters,
                catalog.VehicleRoutes, catalog.Merchants, [invalidControlCompanion], catalog.Scripts);
            var invalidControlPreview = _worldFeatureHandler.Handle(_history.State,
                new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
                    invalidControlCatalog, true));
            if (invalidControlPreview.Succeeded ||
                invalidControlPreview.Diagnostics.All(item => item.Code != "world.companion.ownership_invalid") ||
                invalidControlPreview.Diagnostics.All(item =>
                    item.Code != "world.companion.movement_commands_invalid") ||
                invalidControlPreview.Diagnostics.All(item =>
                    item.Code != "world.companion.tactical_commands_invalid") ||
                invalidControlPreview.Diagnostics.All(item => item.Code != "world.companion.ai_control_invalid") ||
                invalidControlPreview.Diagnostics.All(item =>
                    item.Code != "world.companion.equipment_control_invalid"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R075_COMPANION_CONTROL_FAILED invalid_prebuild_validation");
                return false;
            }

            var invalidExperienceCompanion = validCompanion with
            {
                Experience = validCompanion.Experience! with
                {
                    Death = validCompanion.Experience!.Death with
                    { Mode = "resurrection", ResurrectionResourceId = null },
                    Cohesion = validCompanion.Experience.Cohesion with
                    { NavigateThreshold = 30, PortalThreshold = 20, TeleportThreshold = 10, SafeAnchorTags = [] },
                    Hud = validCompanion.Experience.Hud with
                    { Keyboard = false, Gamepad = false, ScreenReaderLabels = false, VisibleResourceIds = [] },
                },
            };
            var invalidExperienceCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion,
                catalog.Provenance, catalog.Placeables, catalog.Doors, catalog.Triggers, catalog.Encounters,
                catalog.VehicleRoutes, catalog.Merchants, [invalidExperienceCompanion], catalog.Scripts);
            var invalidExperiencePreview = _worldFeatureHandler.Handle(_history.State,
                new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
                    invalidExperienceCatalog, true));
            if (invalidExperiencePreview.Succeeded ||
                invalidExperiencePreview.Diagnostics.All(item =>
                    item.Code != "world.companion.death_experience_invalid") ||
                invalidExperiencePreview.Diagnostics.All(item =>
                    item.Code != "world.companion.cohesion_experience_invalid") ||
                invalidExperiencePreview.Diagnostics.All(item =>
                    item.Code != "world.companion.party_hud_inaccessible"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R076_COMPANION_EXPERIENCE_FAILED invalid_prebuild_validation");
                return false;
            }

            var validPlaceable = catalog.Placeables[0];
            var invalidPlaceable = validPlaceable with
            {
                Foundation = validPlaceable.Foundation! with
                {
                    Slug = string.Empty,
                    Interaction = validPlaceable.Foundation!.Interaction! with { MaximumDistance = 0 },
                    Collision = validPlaceable.Foundation.Collision with { Enabled = false, NavigationObstacle = true },
                    Transform = validPlaceable.Foundation.Transform with { Scale = Vector3Value.Zero },
                },
            };
            var invalidPlaceableCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion, catalog.Provenance,
                [invalidPlaceable], catalog.Doors, catalog.Triggers, catalog.Encounters, catalog.VehicleRoutes,
                catalog.Merchants, catalog.Companions, catalog.Scripts);
            var invalidPlaceablePreview = _worldFeatureHandler.Handle(_history.State, new SetWorldFeatureCatalogCommand(
                CommandId.From(Guid.NewGuid()), _history.State.Revision, invalidPlaceableCatalog, true));
            var invalidPlaceableCodes = invalidPlaceablePreview.Diagnostics.Select(item => item.Code).ToHashSet(StringComparer.Ordinal);
            if (invalidPlaceablePreview.Succeeded || !invalidPlaceableCodes.Contains("world.placeable.identity_invalid") ||
                !invalidPlaceableCodes.Contains("world.placeable.collision_invalid") ||
                !invalidPlaceableCodes.Contains("world.placeable.interaction_invalid") ||
                !invalidPlaceableCodes.Contains("world.placeable.transform_invalid"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R046_PLACEABLE_FAILED invalid_prebuild_validation");
                return false;
            }

            var invalidPlaceableRuntime = validPlaceable with
            {
                Foundation = validPlaceable.Foundation! with
                {
                    OnUsed = validPlaceable.Foundation!.OnUsed! with { EventBinding = string.Empty },
                    Validation = new([], [], [], [], [], 0, 0),
                },
            };
            var invalidPlaceableRuntimeCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion, catalog.Provenance,
                [invalidPlaceableRuntime], catalog.Doors, catalog.Triggers, catalog.Encounters, catalog.VehicleRoutes,
                catalog.Merchants, catalog.Companions, catalog.Scripts);
            var invalidPlaceableRuntimePreview = _worldFeatureHandler.Handle(_history.State, new SetWorldFeatureCatalogCommand(
                CommandId.From(Guid.NewGuid()), _history.State.Revision, invalidPlaceableRuntimeCatalog, true));
            var invalidPlaceableRuntimeCodes = invalidPlaceableRuntimePreview.Diagnostics.Select(item => item.Code)
                .ToHashSet(StringComparer.Ordinal);
            if (invalidPlaceableRuntimePreview.Succeeded ||
                !invalidPlaceableRuntimeCodes.Contains("world.placeable.on_used_invalid") ||
                !invalidPlaceableRuntimeCodes.Contains("world.placeable.animation_invalid") ||
                !invalidPlaceableRuntimeCodes.Contains("world.placeable.lock_reference_missing") ||
                !invalidPlaceableRuntimeCodes.Contains("world.placeable.trap_reference_missing") ||
                !invalidPlaceableRuntimeCodes.Contains("world.placeable.inventory_reference_missing") ||
                !invalidPlaceableRuntimeCodes.Contains("world.placeable.physics_profile_missing") ||
                !invalidPlaceableRuntimeCodes.Contains("world.placeable.budget_exceeded"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R047_PLACEABLE_EVENTS_FAILED invalid_prebuild_validation");
                return false;
            }

            var validDoor = catalog.Doors[0];
            var invalidDoor = validDoor with
            {
                Foundation = validDoor.Foundation! with
                {
                    Links = [new(Guid.Parse("d0410000-0000-4000-8000-000000000001"), validDoor.Id, true, string.Empty)],
                    Navigation = validDoor.Foundation.Navigation! with
                    {
                        ObstacleId = string.Empty,
                        BlockingStateIds = ["closed", "open"],
                        TraversableStateIds = ["open"],
                    },
                },
            };
            var invalidDoorCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion, catalog.Provenance,
                catalog.Placeables, [invalidDoor], catalog.Triggers, catalog.Encounters, catalog.VehicleRoutes,
                catalog.Merchants, catalog.Companions, catalog.Scripts);
            var invalidDoorPreview = _worldFeatureHandler.Handle(_history.State, new SetWorldFeatureCatalogCommand(
                CommandId.From(Guid.NewGuid()), _history.State.Revision, invalidDoorCatalog, true));
            var invalidDoorCodes = invalidDoorPreview.Diagnostics.Select(item => item.Code).ToHashSet(StringComparer.Ordinal);
            if (invalidDoorPreview.Succeeded || !invalidDoorCodes.Contains("world.door.navigation_invalid") ||
                !invalidDoorCodes.Contains("world.door.navigation_state_mismatch") ||
                !invalidDoorCodes.Contains("world.door.transition_loop"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R041_DOOR_FAILED invalid_prebuild_validation");
                return false;
            }

            var access = validDoor.AccessEvents!;
            var invalidDoorAccess = validDoor with
            {
                AccessEvents = access with
                {
                    Lock = access.Lock with { CredentialIds = [], Difficulty = -1 },
                    Rejection = access.Rejection with { Texts = [], EventBinding = "open" },
                    Trap = access.Trap with { Sides = [], ResetDelaySeconds = -1 },
                    PortalUse = access.PortalUse with { RequestedBinding = "transition" },
                    StateEvents = access.StateEvents with { OpenedBinding = "opened" },
                    LockStateEvents = access.LockStateEvents! with { LockedBinding = "locked" },
                },
            };
            var invalidDoorAccessCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion,
                catalog.Provenance, catalog.Placeables, [invalidDoorAccess], catalog.Triggers, catalog.Encounters,
                catalog.VehicleRoutes, catalog.Merchants, catalog.Companions, catalog.Scripts);
            var invalidDoorAccessPreview = _worldFeatureHandler.Handle(_history.State,
                new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
                    invalidDoorAccessCatalog, true));
            var invalidDoorAccessCodes = invalidDoorAccessPreview.Diagnostics.Select(item => item.Code)
                .ToHashSet(StringComparer.Ordinal);
            if (invalidDoorAccessPreview.Succeeded ||
                !invalidDoorAccessCodes.Contains("world.door.lock_invalid") ||
                !invalidDoorAccessCodes.Contains("world.door.open_rejected_invalid") ||
                !invalidDoorAccessCodes.Contains("world.door.trap_invalid") ||
                !invalidDoorAccessCodes.Contains("world.door.portal_events_invalid") ||
                !invalidDoorAccessCodes.Contains("world.door.state_events_invalid"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R081_DOOR_ACCESS_EVENTS_FAILED invalid_prebuild_validation");
                return false;
            }
            if (!invalidDoorAccessCodes.Contains("world.door.lock_events_invalid"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R082_DOOR_LOCK_EVENTS_FAILED invalid_prebuild_validation");
                return false;
            }

            var validEncounter = catalog.Encounters[0];
            var invalidEncounter = validEncounter with
            {
                Foundation = validEncounter.Foundation! with
                {
                    Volume = validEncounter.Foundation.Volume with { Points = [], Height = 0 },
                    Candidates = [validEncounter.Foundation.Candidates[0] with { Cost = 0, MaximumAlive = 99 }],
                    SpawnBudget = 0,
                    SpawnAnchorIds = [],
                    RewardIds = [string.Empty],
                    NetworkActorCap = 1,
                },
            };
            var invalidEncounterCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion, catalog.Provenance,
                catalog.Placeables, catalog.Doors, catalog.Triggers, [invalidEncounter], catalog.VehicleRoutes,
                catalog.Merchants, catalog.Companions, catalog.Scripts);
            var invalidEncounterPreview = _worldFeatureHandler.Handle(_history.State, new SetWorldFeatureCatalogCommand(
                CommandId.From(Guid.NewGuid()), _history.State.Revision, invalidEncounterCatalog, true));
            var invalidEncounterCodes = invalidEncounterPreview.Diagnostics.Select(item => item.Code).ToHashSet(StringComparer.Ordinal);
            if (invalidEncounterPreview.Succeeded || !invalidEncounterCodes.Contains("world.encounter.volume_invalid") ||
                !invalidEncounterCodes.Contains("world.encounter.candidate_invalid") ||
                !invalidEncounterCodes.Contains("world.encounter.budget_invalid") ||
                !invalidEncounterCodes.Contains("world.encounter.anchors_invalid") ||
                !invalidEncounterCodes.Contains("world.encounter.rewards_invalid") ||
                !invalidEncounterCodes.Contains("world.encounter.network_cap_invalid"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R042_ENCOUNTER_FAILED invalid_prebuild_validation");
                return false;
            }

            var invalidComposition = validEncounter.Composition! with
            {
                Id = Guid.Empty,
                SchemaVersion = 0,
                Provenance = string.Empty,
                Slug = string.Empty,
                DisplayName = string.Empty,
                Category = string.Empty,
                Theme = string.Empty,
                Difficulty = validEncounter.Composition.Difficulty with
                {
                    Id = Guid.Empty,
                    RulesetId = string.Empty,
                    PartyAssessmentProviderId = string.Empty,
                    ExpectedPartySize = 0,
                    ExpectedPartyPower = -1,
                    BaseBudget = 0,
                    PartySizeMultiplier = 0,
                    DifficultyMultiplier = 0,
                },
                Groups = [validEncounter.Composition.Groups[0] with
                {
                    Id = Guid.Empty,
                    Slug = string.Empty,
                    Minimum = 5,
                    Maximum = 2,
                    Budget = 0,
                    CandidateIds = [Guid.NewGuid()],
                }],
                Selection = validEncounter.Composition.Selection with
                {
                    Id = Guid.Empty,
                    PartyAssessmentProviderId = string.Empty,
                    Strategy = string.Empty,
                },
                Activation = validEncounter.Composition.Activation with
                {
                    Id = Guid.Empty,
                    Mode = EncounterActivationMode.Script,
                    IntervalSeconds = -1,
                    ScriptId = null,
                },
                Overrides = new Dictionary<string, string> { [string.Empty] = string.Empty },
            };
            var invalidCompositionCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion,
                catalog.Provenance, catalog.Placeables, catalog.Doors, catalog.Triggers,
                [validEncounter with { Composition = invalidComposition }], catalog.VehicleRoutes,
                catalog.Merchants, catalog.Companions, catalog.Scripts);
            var invalidCompositionPreview = _worldFeatureHandler.Handle(_history.State,
                new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
                    invalidCompositionCatalog, true));
            var invalidCompositionCodes = invalidCompositionPreview.Diagnostics.Select(item => item.Code)
                .ToHashSet(StringComparer.Ordinal);
            if (invalidCompositionPreview.Succeeded ||
                !invalidCompositionCodes.Contains("world.encounter.composition_identity_invalid") ||
                !invalidCompositionCodes.Contains("world.encounter.composition_uuid_invalid") ||
                !invalidCompositionCodes.Contains("world.encounter.difficulty_invalid") ||
                !invalidCompositionCodes.Contains("world.encounter.count_range_invalid") ||
                !invalidCompositionCodes.Contains("world.encounter.selection_invalid") ||
                !invalidCompositionCodes.Contains("world.encounter.activation_invalid") ||
                !invalidCompositionCodes.Contains("world.encounter.composition_override_invalid"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R083_ENCOUNTER_COMPOSITION_FAILED invalid_prebuild_validation");
                return false;
            }

            var validScenario = validEncounter.Scenario!;
            var invalidScenario = validScenario with
            {
                Id = Guid.Empty,
                SpawnAnchors = [validScenario.SpawnAnchors[0] with
                {
                    Id = Guid.Empty,
                    AnchorId = "anchor:missing",
                    Transform = new(new(1_000_001, 0, 0), QuaternionValue.Identity, Vector3Value.One),
                    Capacity = 0,
                    Navigable = false,
                    IsFallback = false,
                    Weight = -1,
                }],
                Activator = validScenario.Activator with
                {
                    Id = Guid.Empty,
                    RequiredTags = ["party", "party"],
                },
                Lifecycle = validScenario.Lifecycle with
                {
                    ForgeScriptApiVersion = "latest",
                    Entered = validScenario.Lifecycle.Entered with { Binding = "OnEnter" },
                },
                Phases = [validScenario.Phases[0] with
                {
                    Id = Guid.Empty,
                    Order = 0,
                    DisplayName = string.Empty,
                    EntryConditions = [string.Empty],
                    ReinforcementEncounterIds = [validEncounter.Id],
                    Waves = [validScenario.Phases[0].Waves[0] with
                    {
                        Id = Guid.Empty,
                        Order = 0,
                        GroupIds = [Guid.NewGuid()],
                        DelaySeconds = -1,
                        Completion = EncounterWaveCompletionPolicy.Script,
                        CompletionTargetId = null,
                        VoiceCueId = "voice:missing",
                        NextWaveId = Guid.Empty,
                    }],
                }],
                Overrides = new Dictionary<string, string> { [string.Empty] = string.Empty },
            };
            var invalidScenarioCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion,
                catalog.Provenance, catalog.Placeables, catalog.Doors, catalog.Triggers,
                [validEncounter with { Scenario = invalidScenario }], catalog.VehicleRoutes,
                catalog.Merchants, catalog.Companions, catalog.Scripts);
            var invalidScenarioPreview = _worldFeatureHandler.Handle(_history.State,
                new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
                    invalidScenarioCatalog, true));
            var invalidScenarioCodes = invalidScenarioPreview.Diagnostics.Select(item => item.Code)
                .ToHashSet(StringComparer.Ordinal);
            if (invalidScenarioPreview.Succeeded ||
                !invalidScenarioCodes.Contains("world.encounter.scenario_identity_invalid") ||
                !invalidScenarioCodes.Contains("world.encounter.scenario_uuid_invalid") ||
                !invalidScenarioCodes.Contains("world.encounter.spawn_anchor_policy_invalid") ||
                !invalidScenarioCodes.Contains("world.encounter.activator_policy_invalid") ||
                !invalidScenarioCodes.Contains("world.encounter.lifecycle_binding_invalid") ||
                !invalidScenarioCodes.Contains("world.encounter.phase_policy_invalid") ||
                !invalidScenarioCodes.Contains("world.encounter.reinforcement_reference_invalid") ||
                !invalidScenarioCodes.Contains("world.encounter.wave_order_invalid") ||
                !invalidScenarioCodes.Contains("world.encounter.wave_composition_invalid") ||
                !invalidScenarioCodes.Contains("world.encounter.wave_completion_invalid") ||
                !invalidScenarioCodes.Contains("world.encounter.wave_voice_reference_invalid") ||
                !invalidScenarioCodes.Contains("world.encounter.wave_transition_invalid") ||
                !invalidScenarioCodes.Contains("world.encounter.scenario_override_invalid"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R084_ENCOUNTER_SCENARIO_FAILED invalid_prebuild_validation");
                return false;
            }

            var invalidStatistics = validEncounter.Statistics! with
            {
                Id = Guid.Empty,
                Runs = 0,
                DurationEstimatorId = string.Empty,
                SecondsPerActor = 0,
                RewardEstimatorId = string.Empty,
                RewardPerCost = -1,
                Percentiles = [.9, .5, .5, 1],
                Overrides = new Dictionary<string, string> { [string.Empty] = string.Empty },
            };
            var invalidStatisticsCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion,
                catalog.Provenance, catalog.Placeables, catalog.Doors, catalog.Triggers,
                [validEncounter with { Statistics = invalidStatistics }], catalog.VehicleRoutes,
                catalog.Merchants, catalog.Companions, catalog.Scripts);
            var invalidStatisticsPreview = _worldFeatureHandler.Handle(_history.State,
                new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
                    invalidStatisticsCatalog, true));
            var invalidStatisticsCodes = invalidStatisticsPreview.Diagnostics.Select(item => item.Code)
                .ToHashSet(StringComparer.Ordinal);
            if (invalidStatisticsPreview.Succeeded ||
                !invalidStatisticsCodes.Contains("world.encounter.statistics_identity_invalid") ||
                !invalidStatisticsCodes.Contains("world.encounter.statistics_configuration_invalid") ||
                !invalidStatisticsCodes.Contains("world.encounter.statistics_percentiles_invalid") ||
                !invalidStatisticsCodes.Contains("world.encounter.statistics_override_invalid"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R085_ENCOUNTER_STATISTICS_FAILED invalid_prebuild_validation");
                return false;
            }

            var validMerchant = catalog.Merchants[0];
            var invalidMerchant = validMerchant with
            {
                Transactions = validMerchant.Transactions! with
                {
                    VendorInteractionId = string.Empty,
                    Locales = [],
                    BuyerCapacity = 0,
                    ServerAuthoritative = false,
                    Stock = [validMerchant.Transactions!.Stock[0] with { Quantity = -1 }],
                },
            };
            var invalidMerchantCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion, catalog.Provenance,
                catalog.Placeables, catalog.Doors, catalog.Triggers, catalog.Encounters, catalog.VehicleRoutes,
                [invalidMerchant], catalog.Companions, catalog.Scripts);
            var invalidMerchantPreview = _worldFeatureHandler.Handle(_history.State, new SetWorldFeatureCatalogCommand(
                CommandId.From(Guid.NewGuid()), _history.State.Revision, invalidMerchantCatalog, true));
            var invalidMerchantCodes = invalidMerchantPreview.Diagnostics.Select(item => item.Code).ToHashSet(StringComparer.Ordinal);
            if (invalidMerchantPreview.Succeeded || !invalidMerchantCodes.Contains("world.merchant.vendor_binding_invalid") ||
                !invalidMerchantCodes.Contains("world.merchant.localization_invalid") ||
                !invalidMerchantCodes.Contains("world.merchant.capacity_invalid") ||
                !invalidMerchantCodes.Contains("world.merchant.authority_invalid") ||
                !invalidMerchantCodes.Contains("world.merchant.stock_values_invalid"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R045_MERCHANT_FAILED invalid_prebuild_validation");
                return false;
            }

            var merchantFoundation = validMerchant.Foundation!;
            var invalidMerchantFoundation = merchantFoundation with
            {
                Wizard = merchantFoundation.Wizard with { CreateTests = false },
                Pricing = merchantFoundation.Pricing with { MaximumPrice = -1 },
                Restrictions = merchantFoundation.Restrictions with
                {
                    BlacklistTags = merchantFoundation.Restrictions.BlacklistTags
                        .Concat(["merchant-approved"]).ToArray(),
                },
            };
            var invalidMerchantFoundationCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion,
                catalog.Provenance, catalog.Placeables, catalog.Doors, catalog.Triggers, catalog.Encounters,
                catalog.VehicleRoutes, [validMerchant with { Foundation = invalidMerchantFoundation }],
                catalog.Companions, catalog.Scripts);
            var invalidMerchantFoundationPreview = _worldFeatureHandler.Handle(_history.State,
                new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
                    invalidMerchantFoundationCatalog, true));
            var invalidMerchantFoundationCodes = invalidMerchantFoundationPreview.Diagnostics
                .Select(item => item.Code).ToHashSet(StringComparer.Ordinal);
            if (invalidMerchantFoundationPreview.Succeeded ||
                !invalidMerchantFoundationCodes.Contains("world.merchant.wizard_incomplete") ||
                !invalidMerchantFoundationCodes.Contains("world.merchant.foundation_pricing_invalid") ||
                !invalidMerchantFoundationCodes.Contains("world.merchant.foundation_query_invalid"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R091_MERCHANT_FOUNDATION_FAILED invalid_prebuild_validation");
                return false;
            }

            var merchantExperience = validMerchant.Experience!;
            var invalidMerchantExperience = merchantExperience with
            {
                Sessions = merchantExperience.Sessions with
                {
                    Id = merchantExperience.Id,
                    ForgeScriptApiVersion = string.Empty,
                    OpenEventBinding = string.Empty,
                    CloseEventBinding = string.Empty,
                    IncludeTransactionSummary = false,
                },
                Ui = merchantExperience.Ui with
                {
                    Id = merchantExperience.Id,
                    KeyboardNavigation = false,
                    GamepadNavigation = false,
                    Cart = false,
                    Confirmation = false,
                    ScreenReaderLabels = false,
                    HighContrast = false,
                    Columns = [string.Empty, string.Empty],
                },
            };
            var invalidMerchantExperienceCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion,
                catalog.Provenance, catalog.Placeables, catalog.Doors, catalog.Triggers, catalog.Encounters,
                catalog.VehicleRoutes, [validMerchant with { Experience = invalidMerchantExperience }],
                catalog.Companions, catalog.Scripts);
            var invalidMerchantExperiencePreview = _worldFeatureHandler.Handle(_history.State,
                new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
                    invalidMerchantExperienceCatalog, true));
            var invalidMerchantExperienceCodes = invalidMerchantExperiencePreview.Diagnostics
                .Select(item => item.Code).ToHashSet(StringComparer.Ordinal);
            if (invalidMerchantExperiencePreview.Succeeded ||
                !invalidMerchantExperienceCodes.Contains("world.merchant.session_events_invalid") ||
                !invalidMerchantExperienceCodes.Contains("world.merchant.store_ui_invalid") ||
                !invalidMerchantExperienceCodes.Contains("world.merchant.experience_identity_conflict"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R092_MERCHANT_EXPERIENCE_FAILED invalid_prebuild_validation");
                return false;
            }

            var r093Placeable = catalog.Placeables[0];
            var validPlaceableOperations = r093Placeable.Operations!;
            var invalidPlaceableOperations = validPlaceableOperations with
            {
                Container = validPlaceableOperations.Container! with
                {
                    Id = validPlaceableOperations.Id,
                    CapacitySlots = 0,
                    InventoryId = string.Empty,
                },
                StateMachine = validPlaceableOperations.StateMachine! with { States = [] },
                Damageable = validPlaceableOperations.Damageable! with { MaximumHealth = 0 },
                Protection = validPlaceableOperations.Protection! with
                {
                    ProtectedActions = [],
                    Feedback = string.Empty,
                    AuditBinding = string.Empty,
                },
                Lock = validPlaceableOperations.Lock! with
                {
                    CredentialIds = [],
                    SkillId = null,
                    CodeHash = null,
                    Hackable = false,
                    RequiredPermissions = [],
                },
            };
            var invalidPlaceableOperationsCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion,
                catalog.Provenance, [r093Placeable with { Operations = invalidPlaceableOperations }],
                catalog.Doors, catalog.Triggers, catalog.Encounters, catalog.VehicleRoutes, catalog.Merchants,
                catalog.Companions, catalog.Scripts);
            var invalidPlaceableOperationsPreview = _worldFeatureHandler.Handle(_history.State,
                new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
                    invalidPlaceableOperationsCatalog, true));
            var invalidPlaceableOperationsCodes = invalidPlaceableOperationsPreview.Diagnostics
                .Select(item => item.Code).ToHashSet(StringComparer.Ordinal);
            if (invalidPlaceableOperationsPreview.Succeeded ||
                !invalidPlaceableOperationsCodes.Contains("world.placeable.container_invalid") ||
                !invalidPlaceableOperationsCodes.Contains("world.placeable.state_machine_invalid") ||
                !invalidPlaceableOperationsCodes.Contains("world.placeable.damageable_invalid") ||
                !invalidPlaceableOperationsCodes.Contains("world.placeable.protection_invalid") ||
                !invalidPlaceableOperationsCodes.Contains("world.placeable.lock_invalid") ||
                !invalidPlaceableOperationsCodes.Contains("world.placeable.operations_identity_conflict"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R093_PLACEABLE_OPERATIONS_FAILED invalid_prebuild_validation");
                return false;
            }

            var validPlaceableSecurityEvents = r093Placeable.SecurityEvents!;
            var invalidPlaceableSecurityEvents = validPlaceableSecurityEvents with
            {
                Id = r093Placeable.Id,
                Trap = validPlaceableSecurityEvents.Trap! with
                {
                    Id = r093Placeable.Id,
                    TriggerId = string.Empty,
                    PayloadId = string.Empty,
                    DetectionDifficulty = -1,
                },
                Loot = validPlaceableSecurityEvents.Loot! with
                {
                    LootTableId = Guid.Empty,
                    Generation = -1,
                    LootTable = validPlaceableSecurityEvents.Loot!.LootTable with { Entries = [] },
                },
                ContainerEvents = validPlaceableSecurityEvents.ContainerEvents! with
                {
                    OpenedBinding = string.Empty,
                    IncludeActor = false,
                },
                LockEvents = validPlaceableSecurityEvents.LockEvents! with
                {
                    ChangedBinding = string.Empty,
                    ControlledVeto = false,
                },
                ContentsChanged = validPlaceableSecurityEvents.ContentsChanged! with
                {
                    EventBinding = string.Empty,
                    IncludeTransactionalDelta = false,
                },
            };
            var invalidPlaceableSecurityEventsCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion,
                catalog.Provenance, [r093Placeable with { SecurityEvents = invalidPlaceableSecurityEvents }],
                catalog.Doors, catalog.Triggers, catalog.Encounters, catalog.VehicleRoutes, catalog.Merchants,
                catalog.Companions, catalog.Scripts);
            var invalidPlaceableSecurityEventsPreview = _worldFeatureHandler.Handle(_history.State,
                new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
                    invalidPlaceableSecurityEventsCatalog, true));
            var invalidPlaceableSecurityEventsCodes = invalidPlaceableSecurityEventsPreview.Diagnostics
                .Select(item => item.Code).ToHashSet(StringComparer.Ordinal);
            if (invalidPlaceableSecurityEventsPreview.Succeeded ||
                !invalidPlaceableSecurityEventsCodes.Contains("world.placeable.trap_invalid") ||
                !invalidPlaceableSecurityEventsCodes.Contains("world.placeable.loot_spawner_invalid") ||
                !invalidPlaceableSecurityEventsCodes.Contains("world.placeable.container_events_invalid") ||
                !invalidPlaceableSecurityEventsCodes.Contains("world.placeable.lock_events_invalid") ||
                !invalidPlaceableSecurityEventsCodes.Contains("world.placeable.contents_changed_invalid") ||
                !invalidPlaceableSecurityEventsCodes.Contains("world.placeable.security_events_identity_conflict"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R094_PLACEABLE_SECURITY_EVENTS_FAILED invalid_prebuild_validation");
                return false;
            }
            var validPlaceableComposition = r093Placeable.CompositionEvents!;
            var invalidPlaceableComposition = validPlaceableComposition with
            {
                Id = Guid.Empty,
                SchemaVersion = 2,
                Provenance = string.Empty,
                DamageEvents = validPlaceableComposition.DamageEvents with
                {
                    Id = Guid.Empty,
                    DamagedBinding = string.Empty,
                    IncludeSource = false,
                },
                Sockets =
                [
                    validPlaceableComposition.Sockets.Single() with
                    {
                        Id = Guid.Empty,
                        SocketId = string.Empty,
                        Kind = string.Empty,
                        AllowedAttachmentTags = [],
                        MaximumAttachments = 0,
                    },
                ],
                Attachments =
                [
                    validPlaceableComposition.Attachments.Single() with
                    {
                        Id = Guid.Empty,
                        SocketId = "missing",
                        ChildBlueprintId = BlueprintId.From(Guid.NewGuid()),
                        Tags = [],
                    },
                ],
                MultiVerb = validPlaceableComposition.MultiVerb with
                {
                    Id = Guid.Empty,
                    Verbs =
                    [
                        validPlaceableComposition.MultiVerb.Verbs[0] with
                        {
                            Id = Guid.Empty,
                            LabelKey = string.Empty,
                            EventBinding = string.Empty,
                        },
                    ],
                },
            };
            var invalidPlaceableCompositionCatalog = new WorldFeatureCatalog(catalog.Id,
                catalog.SchemaVersion, catalog.Provenance,
                [r093Placeable with { CompositionEvents = invalidPlaceableComposition }], catalog.Doors,
                catalog.Triggers, catalog.Encounters, catalog.VehicleRoutes, catalog.Merchants,
                catalog.Companions, catalog.Scripts);
            var invalidPlaceableCompositionPreview = _worldFeatureHandler.Handle(_history.State,
                new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
                    invalidPlaceableCompositionCatalog, true));
            var invalidPlaceableCompositionCodes = invalidPlaceableCompositionPreview.Diagnostics
                .Select(item => item.Code).ToHashSet(StringComparer.Ordinal);
            if (invalidPlaceableCompositionPreview.Succeeded ||
                !invalidPlaceableCompositionCodes.Contains("world.placeable.damage_events_invalid") ||
                !invalidPlaceableCompositionCodes.Contains("world.placeable.attachment_sockets_invalid") ||
                !invalidPlaceableCompositionCodes.Contains("world.placeable.attachment_invalid") ||
                !invalidPlaceableCompositionCodes.Contains("world.placeable.multi_verb_invalid") ||
                !invalidPlaceableCompositionCodes.Contains(
                    "world.placeable.composition_events_identity_conflict"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R095_PLACEABLE_COMPOSITION_EVENTS_FAILED invalid_prebuild_validation");
                return false;
            }
            var validTriggerAdvanced = catalog.Triggers[0].Advanced!;
            var duplicateTriggerComponentId = validTriggerAdvanced.Id;
            var invalidTriggerAdvanced = validTriggerAdvanced with
            {
                ForgeScriptApiVersion = string.Empty,
                Trap = validTriggerAdvanced.Trap with
                {
                    Id = duplicateTriggerComponentId,
                    TriggerId = string.Empty,
                    DetectionDifficulty = -1,
                    VisualizationColor = "orange",
                    DetectedBinding = string.Empty,
                },
                TargetFilter = validTriggerAdvanced.TargetFilter with
                {
                    Id = duplicateTriggerComponentId,
                    RequiredTags = ["hero"],
                    ExcludedTags = ["hero"],
                    FactionIds = [Guid.Empty],
                    SandboxPredicate = "unsafe",
                },
                GameplayVolumes =
                [
                    validTriggerAdvanced.GameplayVolumes[0] with
                    {
                        Id = duplicateTriggerComponentId,
                        Enabled = false,
                        EnterBinding = "wrong",
                    },
                ],
                OccupancyDebug = validTriggerAdvanced.OccupancyDebug with
                {
                    Id = duplicateTriggerComponentId,
                    Enabled = false,
                    ShowEventTrace = false,
                    MaximumTraceEntries = 0,
                },
            };
            var invalidTriggerAdvancedCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion,
                catalog.Provenance, catalog.Placeables, catalog.Doors,
                [catalog.Triggers[0] with { Advanced = invalidTriggerAdvanced }], catalog.Encounters,
                catalog.VehicleRoutes, catalog.Merchants, catalog.Companions, catalog.Scripts);
            var invalidTriggerAdvancedPreview = _worldFeatureHandler.Handle(_history.State,
                new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
                    invalidTriggerAdvancedCatalog, true));
            var invalidTriggerAdvancedCodes = invalidTriggerAdvancedPreview.Diagnostics
                .Select(item => item.Code).ToHashSet(StringComparer.Ordinal);
            if (invalidTriggerAdvancedPreview.Succeeded ||
                !invalidTriggerAdvancedCodes.Contains("world.trigger.advanced_api_invalid") ||
                !invalidTriggerAdvancedCodes.Contains("world.trigger.trap_invalid") ||
                !invalidTriggerAdvancedCodes.Contains("world.trigger.target_filter_invalid") ||
                !invalidTriggerAdvancedCodes.Contains("world.trigger.gameplay_volumes_invalid") ||
                !invalidTriggerAdvancedCodes.Contains("world.trigger.occupancy_debug_invalid") ||
                !invalidTriggerAdvancedCodes.Contains("world.trigger.advanced_uuid_duplicate"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R096_TRIGGER_ADVANCED_FAILED invalid_prebuild_validation");
                return false;
            }

            var aggregate = ProjectJsonSerializer.Deserialize(ProjectJsonSerializer.Serialize(_history.State.Project));
            var created = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            if (!created.Succeeded || !reopened.Succeeded || aggregate.Module.WorldFeatures.Placeables.Count == 0 ||
                reopened.Session!.Project.Module.WorldFeatures.Companions.Count == 0 ||
                reopened.Session.Project.Module.WorldFeatures.Companions[0].Foundation?.Party.CompanionSlots != 3 ||
                reopened.Session.Project.Module.WorldFeatures.Companions[0].Foundation?.OwnerDialogue.ContextId !=
                "recruited-by-speaker" ||
                !reopened.Session.Project.Module.WorldFeatures.Companions[0].Foundation!.Membership.Atomic ||
                reopened.Session.Project.Module.WorldFeatures.Companions[0].Experience?.Death.Mode !=
                "recoverable-corpse" ||
                reopened.Session.Project.Module.WorldFeatures.Companions[0].Experience?.Hud.HighContrast != true ||
                reopened.Session.Project.Module.WorldFeatures.Merchants[0].Transactions?.Stock.Count != 2 ||
                reopened.Session.Project.Module.WorldFeatures.Merchants[0].Foundation?.Slug !=
                "crypt-vigil-market" ||
                reopened.Session.Project.Module.WorldFeatures.Merchants[0].Foundation?.Wizard.TestFixtureId !=
                "merchant-crypt-vigil-nominal" ||
                reopened.Session.Project.Module.WorldFeatures.Merchants[0].Foundation?.Stock[0].Supply !=
                MerchantSupplyPolicy.Infinite ||
                reopened.Session.Project.Module.WorldFeatures.Merchants[0].Foundation?.Pricing.BuyCurve.Count != 2 ||
                reopened.Session.Project.Module.WorldFeatures.Merchants[0].Foundation?.Restrictions.CustomRuleBinding !=
                "scripts/merchant_rule.forge" ||
                reopened.Session.Project.Module.WorldFeatures.Merchants[0].Experience?.Sessions.OpenEventBinding !=
                "StoreSessionOpened" ||
                reopened.Session.Project.Module.WorldFeatures.Merchants[0].Experience?.Sessions.CloseEventBinding !=
                "StoreSessionClosed" ||
                reopened.Session.Project.Module.WorldFeatures.Merchants[0].Experience?.Ui.Columns.Count != 4 ||
                reopened.Session.Project.Module.WorldFeatures.Merchants[0].Experience?.Ui.ScreenReaderLabels != true ||
                reopened.Session.Project.Module.WorldFeatures.Placeables[0].Foundation?.Transform.SnapIncrement != .25 ||
                reopened.Session.Project.Module.WorldFeatures.Placeables[0].Foundation?.OnUsed?.EventBinding != "InteractionExecuted" ||
                reopened.Session.Project.Module.WorldFeatures.Placeables[0].Operations?.Container?.CapacitySlots != 24 ||
                reopened.Session.Project.Module.WorldFeatures.Placeables[0].Operations?.Container?.Replicated != true ||
                reopened.Session.Project.Module.WorldFeatures.Placeables[0].Operations?.StateMachine?.States.Count != 6 ||
                reopened.Session.Project.Module.WorldFeatures.Placeables[0].Operations?.Damageable?.MaximumHealth != 100 ||
                reopened.Session.Project.Module.WorldFeatures.Placeables[0].Operations?.Protection?.AuditBinding !=
                "PlaceableProtectionOverride" ||
                reopened.Session.Project.Module.WorldFeatures.Placeables[0].Operations?.Lock?.InitialState !=
                PlaceableAuthoredLockState.Locked ||
                reopened.Session.Project.Module.WorldFeatures.Placeables[0].SecurityEvents?.Trap?.PayloadId !=
                "damage:ember-burst" ||
                reopened.Session.Project.Module.WorldFeatures.Placeables[0].SecurityEvents?.Loot?.Timing !=
                PlaceableAuthoredLootSpawnTiming.Open ||
                reopened.Session.Project.Module.WorldFeatures.Placeables[0].SecurityEvents?.Loot?.LootTable.Entries.Count != 1 ||
                reopened.Session.Project.Module.WorldFeatures.Placeables[0].SecurityEvents?.ContainerEvents?.OpenedBinding !=
                "ContainerOpened" ||
                reopened.Session.Project.Module.WorldFeatures.Placeables[0].SecurityEvents?.LockEvents?.ChangingBinding !=
                "LockStateChanging" ||
                reopened.Session.Project.Module.WorldFeatures.Placeables[0].SecurityEvents?.ContentsChanged?.EventBinding !=
                "ContainerContentsChanged" ||
                reopened.Session.Project.Module.WorldFeatures.Placeables[0].CompositionEvents?.DamageEvents.DamagedBinding !=
                "DamageApplied" ||
                reopened.Session.Project.Module.WorldFeatures.Placeables[0].CompositionEvents?.DamageEvents.DestroyedBinding !=
                "EntityDestroyed" ||
                reopened.Session.Project.Module.WorldFeatures.Placeables[0].CompositionEvents?.Sockets.Single().SocketId !=
                "lever" ||
                reopened.Session.Project.Module.WorldFeatures.Placeables[0].CompositionEvents?.Attachments.Single().PrefabId !=
                "prefab:crypt-pillar-lever" ||
                reopened.Session.Project.Module.WorldFeatures.Placeables[0].CompositionEvents?.MultiVerb.Verbs.Count != 8 ||
                reopened.Session.Project.Module.WorldFeatures.Placeables[0].CompositionEvents?.MultiVerb.Verbs
                    .Single(item => item.VerbId == "custom-ring").EventBinding != "RingRequested" ||
                reopened.Session.Project.Module.WorldFeatures.Triggers[0].Advanced?.Trap.PayloadId !=
                "damage:ember-burst" ||
                reopened.Session.Project.Module.WorldFeatures.Triggers[0].Advanced?.Trap.TriggeredBinding !=
                "TrapTriggered" ||
                reopened.Session.Project.Module.WorldFeatures.Triggers[0].Advanced?.TargetFilter.PlayersOnly != true ||
                reopened.Session.Project.Module.WorldFeatures.Triggers[0].Advanced?.GameplayVolumes.Count != 10 ||
                reopened.Session.Project.Module.WorldFeatures.Triggers[0].Advanced?.GameplayVolumes
                    .Single(item => item.Kind == TriggerGameplayVolumeKind.Damage).EnterBinding !=
                "DamageVolumeEntered" ||
                reopened.Session.Project.Module.WorldFeatures.Triggers[0].Advanced?.OccupancyDebug.MaximumTraceEntries !=
                100 ||
                reopened.Session.Project.Module.WorldFeatures.Doors[0].AccessEvents?.Trap.PayloadId !=
                "damage:ember-burst" ||
                reopened.Session.Project.Module.WorldFeatures.Doors[0].AccessEvents?.StateEvents.ClosedBinding !=
                "DoorClosed" ||
                reopened.Session.Project.Module.WorldFeatures.Doors[0].AccessEvents?.LockStateEvents?.UnlockedBinding !=
                "DoorUnlocked" ||
                reopened.Session.Project.Module.WorldFeatures.Encounters[0].Composition?.Difficulty.Band !=
                EncounterDifficultyBand.Hard ||
                reopened.Session.Project.Module.WorldFeatures.Encounters[0].Composition?.Groups[0].Maximum != 4 ||
                reopened.Session.Project.Module.WorldFeatures.Encounters[0].Composition?.Selection.PartyAssessmentProviderId !=
                "party:forge-generic" ||
                reopened.Session.Project.Module.WorldFeatures.Encounters[0].Composition?.Activation.Mode !=
                EncounterActivationMode.PopulationMaintained ||
                reopened.Session.Project.Module.WorldFeatures.Encounters[0].Scenario?.SpawnAnchors[0].Transform.Position !=
                new Vector3Value(2, 0, 4) ||
                reopened.Session.Project.Module.WorldFeatures.Encounters[0].Scenario?.Activator.RequiredAuthority !=
                EncounterNetworkAuthority.Server ||
                reopened.Session.Project.Module.WorldFeatures.Encounters[0].Scenario?.Lifecycle.Exhausted.Binding !=
                "EncounterExhausted" ||
                reopened.Session.Project.Module.WorldFeatures.Encounters[0].Scenario?.Phases[0].Waves.Count != 2 ||
                reopened.Session.Project.Module.WorldFeatures.Encounters[0].Scenario?.Phases[0].Waves[1].VoiceCueId !=
                "audio:encounter-warden-fallen" ||
                reopened.Session.Project.Module.WorldFeatures.Encounters[0].Statistics?.Runs != 64 ||
                reopened.Session.Project.Module.WorldFeatures.Encounters[0].Statistics?.BaseSeed != 840 ||
                reopened.Session.Project.Module.WorldFeatures.Encounters[0].Statistics?.Percentiles.Count != 3 ||
                reopened.Session.Project.Module.WorldFeatures.Encounters[0].Statistics?.DurationEstimatorId !=
                "duration:actors-linear")
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13N_AB_WORLD_FEATURES_FAILED persistence");
                return false;
            }

            var profileId = catalog.Id;
            var undo = _history.Undo();
            var redo = _history.Redo();
            var cleanup = _history.Undo();
            appliedToHistory = false;
            if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
                _history.State.Project.Module.WorldFeatures.Id != original.Id ||
                redo.State.Project.Module.WorldFeatures.Id != profileId)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13N_AB_WORLD_FEATURES_FAILED history");
                return false;
            }

            GD.Print("OPENRPG_FORGE_TOOLSET_G13N_PLACEABLE_SOCIAL_ACCESS_PASS faction=ownership dialogue=bound credential=consume-charge-shared-virtual-audit");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13O_PLACEABLE_REACTIVE_PHYSICS_PASS events=attack-ability-heartbeat-custom physics=rigid-kinematic-replicated-sleep-static");
            GD.Print("OPENRPG_FORGE_TOOLSET_R046_PLACEABLE_FOUNDATION_PASS blueprint=composable-visual-collision-interaction-health-container-lock-trap-physics-scripts static=batching-instancing-occluder-merge interaction=verbs-distance-angle-focus-prompt-duration-authority-conditions-event identity=uuid-slug-localized-visual-icon-portrait-tags-notes transform=position-rotation-scale-pivot-snap-parent-surface-animation-overrides ui=visible-empty-error-overlay-summary transaction=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic");
            GD.Print("OPENRPG_FORGE_TOOLSET_R047_PLACEABLE_EVENTS_VALIDATION_PASS onused=typed-event-verb-actor-target-result-provenance-api-before-after validation=collision-navigation-animation-locks-traps-inventory-scripts-physics-budgets diagnostics=stable-path-uuid ui=visible-empty-error-overlay-summary transaction=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic");
            GD.Print("OPENRPG_FORGE_TOOLSET_R048_TRIGGER_FOUNDATION_PASS volume=2d-extruded-or-3d-vertices-holes-height-snap-transform-overlay generic=filters-conditions-cooldown-fire-policy-actions portal=typed-destination-anchor-orientation-preload-permissions events=entered-entity-point-direction-layers-state-exited-duration-reason-api-scripts ui=visible-empty-error-summary transaction=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic");
            GD.Print("OPENRPG_FORGE_TOOLSET_R049_TRIGGER_VALIDATION_PASS validation=geometry-self-intersection-filters-actions-targets-portal-cooldown-scripts-accessibility diagnostics=stable-path-uuid ui=visible-empty-error-summary transaction=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic");
            GD.Print("OPENRPG_FORGE_TOOLSET_R050_THIRD_PARTY_NOTICES_PASS aggregation=module-blueprints-data-tables-content-packages-sources-assets-dependencies spdx=approved-required-unknown-blocked notice=deterministic-preview-export-recalculated-mismatch-rejected diagnostics=stable-path-uuid ui=visible-empty-error-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip cli=structured mcp=bounded-export build=preflight-before-write");
            GD.Print("OPENRPG_FORGE_TOOLSET_R120_AI_TEST_AUTOMATION_PASS profile=uuid-schema-provenance-bounds-allowlist agent=in-process-gameplay-actions-blockers-coverage-reproduction dialogue=paths-contradictions-tone-outcomes quest=unreachable-loops-rewards-multiplayer-save-resume narrative=citations-suggestions-no-auto-rewrite ui=visible-empty-error-results transaction=dry-run-apply-reject history=undo-redo persistence=roundtrip mcp=bounded-shell-free build=preflight-before-write");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13P_DOOR_DAMAGE_LOADING_PASS damage=health-hardness-breach-navigation-debris-repair loading=portal-fallback events=damage-death-attack");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13Q_DOOR_REACTIVE_COMPLEX_PASS events=ability-heartbeat-custom social=dialogue-faction assembly=double-portcullis-elevator-airlock");
            GD.Print("OPENRPG_FORGE_TOOLSET_R040_DOOR_FOUNDATION_PASS placement=socket-or-free states=open-closed-opening-closing-locked-jammed-broken-disabled-custom interaction=range-side-clearance-animation-collision-authority-queue-feedback portal=typed-anchor-orientation-preload-permissions links=oneway-or-atomic-pair ui=section-summary transaction=dry-run-apply history=undo-redo persistence=roundtrip runtime=resolved");
            GD.Print("OPENRPG_FORGE_TOOLSET_R041_DOOR_NAVIGATION_VALIDATION_PASS navigation=obstacle-link-animation-state-visualization blocking=tested validation=link-destination-animation-collision-nav-scripts-loop diagnostics=stable-path-uuid ui=visible transaction=dry-run-reject persistence=roundtrip runtime=synchronized");
            GD.Print("OPENRPG_FORGE_TOOLSET_R081_DOOR_ACCESS_EVENTS_PASS lock=credentials-skill-relock-consume rejection=reason-localized-sound-animation-script trap=detect-disarm-payload-reset-owner-side-event portal=requested-actor-destination-cause-conditions-veto-completed state=opened-closed-terminal ui=visible-empty-error-overlay-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic-rpgkit-adapter");
            GD.Print("OPENRPG_FORGE_TOOLSET_R082_DOOR_LOCK_EVENTS_PASS events=locked-unlocked payload=method-actor-credential-state-provenance terminal=guarded ui=visible-empty-error-overlay-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-view mcp=filtered-dry-run runtime=optional-safe-required-diagnostic-rpgkit-reused");
            GD.Print("OPENRPG_FORGE_TOOLSET_R083_ENCOUNTER_COMPOSITION_PASS wizard=role-theme-zone-difficulty-groups-budget-activation-spawn difficulty=ruleset-party-estimator-multipliers count=min-max-budget-server-cap selection=provider-challenge-seeded-preview activation=once-repeat-timed-wave-persistent-population-script ui=visible-empty-error-overlay-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic-rpgkit-authoring-composition-reused");
            GD.Print("OPENRPG_FORGE_TOOLSET_R084_ENCOUNTER_SCENARIO_PASS anchors=transform-capacity-navigation-visibility-fallback-distribution activator=players-only-summons-companions-gm-spectators-authority lifecycle=entered-exited-duration-exhausted-full-payload waves=uuid-order-conditions-groups-delay-objective-voice-transition ui=visible-empty-error-overlay-graph-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic-rpgkit-composition-lifecycle-wave-reused");
            GD.Print("OPENRPG_FORGE_TOOLSET_R085_ENCOUNTER_STATISTICS_PASS simulation=n-runs-composition-cost-duration-rewards variances=count-cost-duration-reward seeds=reproducible-consecutive percentiles=50-90-99 estimators=typed-data-driven ui=visible-empty-error-overlay-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic-rpgkit-wave-simulation-reused");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13R_TRIGGER_DISCOVERY_ACCESS_PASS secret=senses-difficulty-scope click=verb-hit permission=credential cursor=outline rest=safe");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13S_TRIGGER_AUTOMATION_NARRATIVE_GM_PASS heartbeat=occupied-budget custom=namespaced clue=journal narrative=priority-cooldown director=manual-audit");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13T_ENCOUNTER_LIFECYCLE_PASS respawn=delay-cycles-persistence-antifarm faction=override heartbeat=budget custom=namespaced");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13U_ENCOUNTER_TACTICAL_PASS reinforcement=alert-route-cap-delay visibility=camera-los cleanup=dead-escaped-abandoned-unload-persistent");
            GD.Print("OPENRPG_FORGE_TOOLSET_R042_ENCOUNTER_FOUNDATION_PASS volume=2d-3d-filters-los-threat-script candidates=blueprint-weight-role-cost-conditions-variants-limits validation=geometry-references-budget-anchors-rewards-network-cap ui=visible transaction=dry-run-reject persistence=roundtrip runtime=nominal-failure");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13V_VEHICLE_ROUTE_PASS route=points-lanes-speed-stops-overtaking network=sync");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13W_MERCHANT_CORE_PASS treasury=balance-credit-debt-caps purchase=bounded stolen=policy restock=world-time-route-persistent");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13X_MERCHANT_ADVANCED_PASS currencies=typed-exchange negotiation=bounded market=supply-demand services=repair audit=ledger-anomaly");
            GD.Print("OPENRPG_FORGE_TOOLSET_R045_MERCHANT_TRANSACTIONS_PASS transaction=buy-sell-barter-aggregate-atomic-rollback validation=vendor-currency-treasury-stock-price-restrictions-localization-scripts ui=visible-sections-summary command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic-authoritative");
            GD.Print("OPENRPG_FORGE_TOOLSET_R091_MERCHANT_FOUNDATION_PASS store=independent-multi-vendor-interface-channel wizard=store-vendor-dialogue-interaction-tests stock=finite-infinite-quantity-variant-supplier-restock-order-hidden pricing=buy-sell-curves-currency-reputation-scarcity-condition-negotiation restrictions=whitelist-blacklist-type-tags-quality-provenance-condition-owner-custom-rule ui=visible-empty-error-sections-summary command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic rpgkit=merchant-services-reused");
            GD.Print("OPENRPG_FORGE_TOOLSET_R092_MERCHANT_EXPERIENCE_PASS stock=infinite-unbounded-quantity-stable sessions=opened-closed-customer-vendor-price-context-transaction-summary-api-1.0 ui=keyboard-gamepad-compare-filters-cart-confirmation-screen-reader-high-contrast-columns command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle-events mcp=filtered-dry-run runtime=optional-safe-required-diagnostic rpgkit=store-session-ui-services-reused");
            GD.Print("OPENRPG_FORGE_TOOLSET_R093_PLACEABLE_OPERATIONS_PASS container=capacity-ownership-access-persistence-loot-ui-replication-inventory state=idle-open-closed-broken-active-custom-transitions-preview damage=health-armor-resistances-stages-destruction-debris-repair protection=permissions-feedback-gm-override-audit lock=states-credentials-skill-code-hacking-relock-jam-permissions ui=visible-empty-error-overlay-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic rpgkit=interaction-security-services-reused");
            GD.Print("OPENRPG_FORGE_TOOLSET_R094_PLACEABLE_SECURITY_EVENTS_PASS trap=trigger-payload-detect-disarm-reset-owner-vfx-friendly-one-shot loot=table-init-open-reset-seed-persisted-state container=opened-closed-actor-result-previous-authority lock=changing-changed-method-credential-actor-veto disturbed=transactional-delta-cause-revision-actor ui=visible-empty-error-overlay-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle-events mcp=filtered-dry-run runtime=optional-safe-required-diagnostic rpgkit=security-event-services-reused");
            GD.Print("OPENRPG_FORGE_TOOLSET_R095_PLACEABLE_COMPOSITION_EVENTS_PASS damage=applied-destroyed-uniform-source-amount-type-health-context sockets=typed-transform-tags-snap-capacity-blueprint-prefab-reversible verbs=inspect-use-open-talk-take-push-climb-custom-context-priority ui=visible-empty-error-overlay-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle-events mcp=filtered-dry-run runtime=optional-safe-required-diagnostic rpgkit=event-attachment-verb-services-reused");
            GD.Print("OPENRPG_FORGE_TOOLSET_R096_TRIGGER_ADVANCED_PASS trap=detection-disarm-payload-reset-ownership-gm-visualization query=players-tags-factions-components-owner-team-sandbox volumes=damage-heal-gravity-water-climb-nobuild-camera-audio-weather-checkpoint debug=occupants-last-fires-cooldowns-filters-event-trace ui=visible-empty-error-overlay-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle-events mcp=filtered-dry-run runtime=optional-safe-required-diagnostic rpgkit=advanced-trigger-service-reused");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13Y_COMPANION_SOCIAL_PROGRESSION_PASS dialogue=permissions formation=slots-obstacles progression=preview-respec relation=loyalty-history banter=priority-cooldown");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13Z_COMPANION_GAMEPLAY_CONTROL_PASS quest=personal traps=task ability=policy control=direct-authorized party=split-regroup-sync");
            GD.Print("OPENRPG_FORGE_TOOLSET_R037_COMPANION_PASS validation=identity-recruitment-ownership-commands-equipment-death-dialogue-contexts-cross-zone ui=sections-preview-empty-error command=dry-run-apply history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic");
            GD.Print("OPENRPG_FORGE_TOOLSET_R074_COMPANION_FOUNDATION_PASS " +
                     "recruitable=component-policy dialogue=not-recruited-owner " +
                     "membership=preview-apply-dismiss-capacity-ownership-inventory-position-persistence-atomic " +
                     "party=companions-summons-pets-allies-roles-server-limit " +
                     "ui=visible-sections-summary-empty-error command=dry-run-apply-reject history=undo-redo " +
                     "persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run " +
                     "runtime=optional-safe-required-diagnostic-engine-nodes");
            GD.Print("OPENRPG_FORGE_TOOLSET_R075_COMPANION_CONTROL_PASS " +
                     "ownership=individual-party-shared-ai-only-gm-transfer-audit-revision " +
                     "movement=move-follow-hold-rally-guard-interact-formation-waypoint-priority-authority " +
                     "tactical=attack-defend-focus-fire-protect-disengage-use-ability-stance-target-priority " +
                     "ai=autonomy-risk-resources-friendly-fire-role-distance-abilities-plan-transaction-authorization " +
                     "equipment=permission-locks-loadout-compare-ownership-semantic-diff " +
                     "ui=visible-sections-summary-empty-error command=dry-run-apply-reject history=undo-redo " +
                     "persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run " +
                     "runtime=optional-safe-required-diagnostic-engine-nodes");
            GD.Print("OPENRPG_FORGE_TOOLSET_R076_COMPANION_EXPERIENCE_PASS " +
                     "death=downed-permanent-corpse-resurrection-replacement-memorial-inventory-events " +
                     "cohesion=navigate-portal-safe-teleport-combat-visible-audit " +
                     "hud=portraits-health-resources-statuses-quick-commands-reorder-keyboard-gamepad-screen-reader-high-contrast " +
                     "ui=visible-sections-summary-empty-error command=dry-run-apply-reject history=undo-redo " +
                     "persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run " +
                     "runtime=optional-safe-required-diagnostic-engine-nodes-visible-hud");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13AA_SCRIPT_CAMERA_TAG_PASS camera=rig-shots-blends-lock-skip-abort tag=explicit-visible-route");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13AB_SCRIPT_SWITCHES_PASS module=typed-documented-scoped-migrated ai=named-schema-preview-authoritative");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13N_AB_WORLD_FEATURES_PASS ui=tabs-8-empty-errors-summary transaction=preview-apply-audit-history mcp=filtered-dry-run persistence=project-workspace-package runtime=resolved");
            return true;
        }
        catch (Exception exception)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13N_AB_WORLD_FEATURES_FAILED " + exception);
            return false;
        }
        finally
        {
            _worldFeatureStudioWindow.Hide();
            if (appliedToHistory) _history.Undo();
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true);
        }
    }


}
#endif
