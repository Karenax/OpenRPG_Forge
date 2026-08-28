using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;

namespace Forge.WorkspaceMigrator;

internal static class CryptWorldFeatureMigration
{
    public static WorldFeatureCatalog Create(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!string.Equals(project.Module.Slug, "crypt-of-ashes", StringComparison.Ordinal))
            throw new ArgumentException("The authored world-feature fixture only applies to crypt-of-ashes.", nameof(project));
        var moduleId = project.Module.Id.Value;
        var area = project.Areas.Single(item => item.Id == project.Module.EntryAreaId);
        var placeable = project.Blueprints.Single(item => item.Slug == "crypt-pillar");
        var door = project.Blueprints.Single(item => item.Slug == "ash-door");
        var trigger = project.Blueprints.Single(item => item.Slug == "crypt-exit");
        var elyra = project.Blueprints.Single(item => item.Slug == "elyra-npc");
        var merchantItem = project.Blueprints.FirstOrDefault(item => item.Kind == BlueprintKind.Item);
        var dialogue = project.Dialogues.Single().Id.Value;
        var quest = project.Quests.Single().Id.Value;
        var ashWatchFaction = project.Module.Factions.Factions.Single(item => item.Slug == "ash-watch");
        var routeId = Stable(moduleId, "vehicle-route");
        var encounterPatrolRouteId = CryptNavigationMigration.EncounterPatrolRouteId(area.Id);
        var start = Stable(routeId, "start");
        var end = Stable(routeId, "end");
        var placeableSecurityEvents = merchantItem is null ? null : new PlaceableSecurityEventsProfile(
            Stable(moduleId, "placeable-pillar-security-events"), 1, "sample:r094",
            new(Stable(moduleId, "placeable-pillar-trap"), "container:open", "damage:ember-burst",
                10, 12, 30, "ash-watch", "vfx:ember-burst", "ash-watch", true, true),
            new(Stable(moduleId, "placeable-pillar-loot-spawner"),
                Stable(moduleId, "placeable-pillar-loot-table"), PlaceableAuthoredLootSpawnTiming.Open,
                42, true, [], 0,
                new(Stable(moduleId, "placeable-pillar-loot-table"), 1, "sample:r094",
                    [new(merchantItem.Id.Value, "crypt-relic", 1, 1, 2, 1, "quest:crypt-open", true)])),
            new(Stable(moduleId, "placeable-pillar-container-events"), "1.0.0", "ContainerOpened",
                "ContainerClosed", true, true, true, true),
            new(Stable(moduleId, "placeable-pillar-lock-events"), "1.0.0", "LockStateChanging",
                "LockStateChanged", true, true, true, true),
            new(Stable(moduleId, "placeable-pillar-contents-event"), "1.0.0",
                "ContainerContentsChanged", true, true, true, true));
        var placeableCompositionEvents = merchantItem is null ? null : new PlaceableCompositionEventsProfile(
            Stable(moduleId, "placeable-pillar-composition-events"), 1, "sample:r095",
            new(Stable(moduleId, "placeable-pillar-damage-events"), "1.0.0", "DamageApplied",
                "EntityDestroyed", true, true, true, true, true, true),
            [
                new(Stable(moduleId, "placeable-pillar-socket-lever"), "lever", "mechanism",
                    new(new(0, 1.2f, 0), Vector3Value.Zero, Vector3Value.One, Vector3Value.Zero, .1,
                        null, false, null), ["lever", "vfx", "prop"], true, 2),
            ],
            [
                new(Stable(moduleId, "placeable-pillar-attachment-lever"), "lever", merchantItem.Id,
                    ["lever", "prop"],
                    new(new(9, 9, 9), Vector3Value.Zero, Vector3Value.One, Vector3Value.Zero, .1,
                        null, false, null), "prefab:crypt-pillar-lever"),
            ],
            new(Stable(moduleId, "placeable-pillar-multi-verb"),
            [
                Verb(moduleId, "inspect", "placeable.inspect", 5, [], [], "InspectRequested"),
                Verb(moduleId, "use", "placeable.use", 5, [], [], "UseRequested"),
                Verb(moduleId, "open", "placeable.open", 5, ["unlocked"], [], "OpenRequested"),
                Verb(moduleId, "talk", "placeable.talk", 5, ["interlocutor"], [], "TalkRequested"),
                Verb(moduleId, "take", "placeable.take", 9, ["portable"], [], "TakeRequested"),
                Verb(moduleId, "push", "placeable.push", 10, [], ["anchored"], "PushRequested"),
                Verb(moduleId, "climb", "placeable.climb", 5, ["climbable"], [], "ClimbRequested"),
                Verb(moduleId, "custom-ring", "placeable.ring", 6, ["ash-rune"], [], "RingRequested"),
            ]));

        var placeableProfile = new PlaceableWorldProfile(Stable(moduleId, "placeable-pillar"), 1,
            "sample:g13-n-o", placeable.Id, "ash-watch", dialogue,
            new("key:ember", PlaceableWorldProfile.CredentialConsumption.Charge, 3, null, true),
            new([
                new("physical-attack-resolved", "scripts/pillar_attack.forge", false, true),
                new("ability-impacted", "scripts/pillar_ability.forge", false, true),
                new("heartbeat", "scripts/pillar_heartbeat.forge", false, true),
                new("custom:crypt:pillar-pulse", "scripts/pillar_pulse.forge", false, true),
            ], new(true, 5, 12, true, "crypt.placeable")),
            new(PlaceableWorldProfile.BodyMode.Rigid, 12, ["lock-y"], true, 4, true, "server"),
            new(Stable(moduleId, "placeable-pillar-foundation"), 1, "sample:r046", "crypt-pillar",
                [new("fr-FR", "Pilier des cendres", "Examiner"), new("en-US", "Ash pillar", "Examine")],
                ["crypt", "ash", "architecture"], "Pilier interactif de la crypte.",
                new("assets/placeables/crypt-pillar.glb", ["mesh:crypt-pillar"], ["material:ash-stone"],
                    "assets/icons/crypt-pillar.png", "assets/portraits/crypt-pillar.png", ["vfx:ember-glow"]),
                new("box", new(1, 1.5f, .5f), true, true, "world"),
                new([new(Stable(moduleId, "placeable-pillar-examine"), "placeable.examine", "placeable-used",
                        "quest:crypt-open")], 2.5, 120, true, "placeable.examine", 1, "server",
                    ["quest:crypt-open"]), null,
                new(true, true, true, true, true,
                    new Dictionary<string, string> { ["onUsed"] = "scripts/pillar_used.forge" }),
                new(new(1.25f, 0, -2.5f), new(0, 45, 0), Vector3Value.One, new(0, .5f, 0), .25,
                    null, true, "animation:crypt-pillar-rise"),
                new Dictionary<string, string> { ["material"] = "ash-stone", ["variant"] = "lit" },
                new(Stable(moduleId, "placeable-pillar-on-used"), 1, "sample:r047", "1.0.0",
                    "InteractionExecuted", "scripts/pillar_before_used.forge", "scripts/pillar_after_used.forge"),
                new(["animation:crypt-pillar-rise"], ["lock:crypt-pillar"], ["trap:ember"],
                    ["inventory:crypt-pillar"], ["physics:rigid-placeable"], 8, 8), true), true,
            new(Stable(moduleId, "placeable-pillar-operations"), 1, "sample:r093",
                new(Stable(moduleId, "placeable-pillar-container"), 24, 160, "party",
                    ["container.open", "container.deposit", "container.withdraw"], true,
                    "loot:crypt-pillar", "ui:placeable-chest", true, "inventory:crypt-pillar"),
                new(Stable(moduleId, "placeable-pillar-state-machine"), "closed",
                    [
                        new(Stable(moduleId, "placeable-pillar-state-idle"), "idle", "animation:idle",
                            "visual:default", true),
                        new(Stable(moduleId, "placeable-pillar-state-open"), "open", "animation:open",
                            "visual:open", true),
                        new(Stable(moduleId, "placeable-pillar-state-closed"), "closed", "animation:closed",
                            "visual:closed", true),
                        new(Stable(moduleId, "placeable-pillar-state-broken"), "broken", "animation:broken",
                            "visual:broken", false),
                        new(Stable(moduleId, "placeable-pillar-state-active"), "active", "animation:active",
                            "visual:embers", true),
                        new(Stable(moduleId, "placeable-pillar-state-custom"), "custom", "animation:custom",
                            "visual:quest", true),
                    ],
                    [
                        new(Stable(moduleId, "placeable-pillar-transition-open"), "closed", "open", "open", 1,
                            "quest:crypt-open"),
                        new(Stable(moduleId, "placeable-pillar-transition-close"), "open", "closed", "close", 1,
                            null),
                        new(Stable(moduleId, "placeable-pillar-transition-break"), "closed", "broken", "destroyed",
                            .25, null),
                    ]),
                new(Stable(moduleId, "placeable-pillar-damageable"), 100, 100, 10,
                    new Dictionary<string, double> { ["fire"] = .25, ["force"] = .5 }, [.75, .5, .25],
                    "vfx:ember-shatter", "debris:crypt-pillar", true, 20),
                new(Stable(moduleId, "placeable-pillar-protection"), ["destroy", "move"],
                    ["placeable.plot.override"], "Cet objet appartient à l'histoire.", true,
                    "PlaceableProtectionOverride"),
                new(Stable(moduleId, "placeable-pillar-lock"), PlaceableAuthoredLockState.Locked,
                    ["key:ember"], "mechanics", 12, "sha256:042", true, true, true,
                    ["placeable.lock.admin"])), true,
            placeableSecurityEvents, placeableSecurityEvents is not null,
            placeableCompositionEvents, placeableCompositionEvents is not null);

        var doorProfile = new DoorWorldProfile(Stable(moduleId, "door-ashes"), 1, "sample:g13-p-q", door.Id,
            new(40, 4, false, "door.damaged", "door.breached", true, "assets/debris/ash-door.glb", "service.repair-door"),
            new("assets/loading/crypt-of-ashes.png", "loading.crypt.title", "loading.crypt.tip", true, .5),
            [new("damage-applied", "scripts/door_damage.forge", false, true),
             new("entity-destroyed", "scripts/door_destroyed.forge", false, true),
             new("physical-attack-resolved", "scripts/door_attack.forge", false, true),
             new("ability-impacted", "scripts/door_ability.forge", false, true),
             new("heartbeat", "scripts/door_heartbeat.forge", false, true),
             new("custom:crypt:door-seal", "scripts/door_seal.forge", false, true)],
            dialogue, "ash-watch",
            new(DoorWorldProfile.AssemblyKind.Double,
                [new(Stable(moduleId, "door-panel-left"), "left", -1, 0, false),
                 new(Stable(moduleId, "door-panel-right"), "right", 1, .1, true)],
                "open-together", true, "close-and-restore"),
            new(DoorWorldProfile.PlacementMode.ArchitectureSocket, "socket:crypt-arch",
                new("kit.crypt", "arched", new Dictionary<string, string> { ["door"] = "material.ash" }, 0, 0, 0),
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
                new(DoorWorldProfile.DestinationKind.Waypoint, Stable(moduleId, "door-destination"),
                    "anchor:crypt-exit", 180, true, ["portal.travel"]),
                [],
                new Dictionary<string, string>
                {
                    ["interaction.prompt"] = "door.open",
                    [DoorWorldProfile.CollisionProfileOverrideKey] = "collision:crypt-door",
                },
                new("nav-obstacle:crypt-door", "nav-link:crypt-door",
                    ["closed", "locked", "jammed", "disabled", "custom"], ["open", "broken"], .75, .25, true)),
            new(Stable(moduleId, "door-ashes-access-events"), 1, "sample:r081",
                "assets/doors/ash-gate.glb", TransformValue.Identity,
                new(Stable(moduleId, "door-ashes-lock"), DoorAccessLockState.Locked, ["key:ember"],
                    "mechanics", 12, null, true, true, true, true, ["door.unlock"]),
                new(Stable(moduleId, "door-ashes-rejection"), "1.0.0", "DoorOpenRejected",
                    [new("fr-FR", "La porte des cendres reste verrouillée."),
                     new("en-US", "The ash gate remains locked.")],
                    "audio:door-locked", "animation:rattle", "scripts/door_open_rejected.forge"),
                new(Stable(moduleId, "door-ashes-trap"), "door:open", "damage:ember-burst", 10, 12, 30,
                    "ash-watch", "vfx:ember-burst", "ash-watch", true, false, ["front", "back"],
                    ["interact", "portal-use-requested"]),
                new(Stable(moduleId, "door-ashes-portal-events"), "1.0.0", "PortalUseRequested",
                    "PortalUseCompleted"),
                new(Stable(moduleId, "door-ashes-state-events"), "1.0.0", "DoorOpened", "DoorClosed"),
                new Dictionary<string, string> { ["failure.priority"] = "lock-before-trap" },
                new(Stable(moduleId, "door-ashes-lock-events"), 1, "sample:r082", "1.0.0", "DoorLocked",
                    "DoorUnlocked"), true), true);

        var triggerProfile = new TriggerWorldProfile(Stable(moduleId, "trigger-exit"), 1, "sample:g13-r-s",
            trigger.Id, area.Id,
            new(["sight", "search"], 10, Stable(moduleId, "secret-reveal"), "feedback.secret", true,
                TriggerWorldProfile.DiscoveryScope.Party),
            new("inspect", "cursor.secret", "outline.gold", "trigger.secret.prompt", "scripts/trigger_click.forge", true, "player"),
            new(2, 30, true, true), "crypt.trigger", new("key:ember", false, "trigger.key.denied"),
            new(true, "danger.nearby", true, true, "rest.allowed", "rest.denied"),
            new("tracking", 8, "ash-tracks", 45, 2, "false-trail", quest),
            new([elyra.Id], 4, true, 60, "party-vote", dialogue), new(true, true, true, true, true),
            new(Stable(moduleId, "trigger-exit-foundation"), 1, "sample:r048", "crypt-exit-threshold",
                new(TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D,
                    [new(-2, 0, -2), new(2, 0, -2), new(2, 0, 2), new(-2, 0, 2)],
                    [[new(-.5f, 0, -.5f), new(.5f, 0, -.5f), new(0, 0, .5f)]],
                    new(4, 3, 4), 3, .25, true),
                new(["creature"], ["player"], ["undead"], ["characters"]), ["crypt.open"], 5,
                TriggerWorldProfile.FirePolicy.OncePerEntity,
                [new(Stable(moduleId, "trigger-exit-action"), "start-encounter", "encounter:crypt-warden",
                    new Dictionary<string, string> { ["source"] = "trigger" })],
                new(TriggerWorldProfile.DestinationKind.Waypoint, Stable(moduleId, "trigger-exit-destination"),
                    "anchor:crypt-exit", new(0, 180, 0), true, ["portal.travel"]),
                new(Vector3Value.Zero, Vector3Value.Zero, Vector3Value.One, null),
                new Dictionary<string, string> { ["editor.overlay"] = "volume" },
                new(Stable(moduleId, "trigger-exit-on-enter"), 1, "sample:r048", "1.0.0", "VolumeEntered",
                    null, "scripts/trigger_enter.forge"),
                new(Stable(moduleId, "trigger-exit-on-exit"), 1, "sample:r048", "1.0.0", "VolumeExited",
                    null, "scripts/trigger_exit.forge"),
                new([Stable(moduleId, "trigger-exit-destination")], ["encounter:crypt-warden"],
                    ["scripts/trigger_click.forge", "scripts/trigger_enter.forge", "scripts/trigger_exit.forge"], ["characters"]),
                true), true,
            new(Stable(moduleId, "trigger-exit-advanced"), 1, "sample:r096", "1.0.0",
                new(Stable(moduleId, "trigger-exit-trap"), "enter", "damage:ember-burst", 10, 12, 30,
                    "ash-watch", "vfx:ember-burst", "ash-watch", true, false, true, "#ff7a45",
                    "TrapDetected", "TrapDisarmed", "TrapTriggered", "TrapReset"),
                new(Stable(moduleId, "trigger-exit-query"), true, ["player"], ["undead"],
                    [ashWatchFaction.Id], ["inventory", "health"], "party", "heroes",
                    "quest==active"),
                Enum.GetValues<TriggerGameplayVolumeKind>()
                    .Select(kind => GameplayVolume(moduleId, kind)).ToArray(),
                new(Stable(moduleId, "trigger-exit-occupancy"), true, true, true, true, true, true, 100)),
            true);

        var encounter = new EncounterWorldProfile(Stable(moduleId, "encounter-warden"), 1, "sample:g13-t-u",
            area.Id, new(30, 3, "area.occupied", true, true, .5), ashWatchFaction.Slug,
            new(true, 5, 12, "crypt.encounter"),
            new(2, ashWatchFaction.Slug, encounterPatrolRouteId, 4, 3, "boss.dead"),
            new(EncounterWorldProfile.VisibilityMode.BehindCover, 10, true, "vfx.spawn.ash"),
            new(30, 30, 120, true, true),
            new(new(EncounterWorldProfile.VolumeShape.ExtrudedPolygon2D,
                    [new(-4, 0, -4), new(4, 0, -4), new(4, 0, 4), new(-4, 0, 4)], Vector3Value.Zero,
                    4, 8, ["player"], ["party"], true, 18, "scripts/encounter_activate.forge", true),
                [new(Stable(moduleId, "encounter-candidate-elyra"), elyra.Id,
                    EncounterWorldProfile.CandidateRole.Controller, 3, 8, ["area.occupied"], ["ember"], 4)],
                36, ["anchor:crypt-north"], ["reward:ember-shard"], 12),
            new(Stable(moduleId, "encounter-warden-composition"), 1, "sample:r083",
                "crypt-warden-ambush", "Gardien des cendres", "ambush", "ash-crypt",
                EncounterWorldProfile.CandidateRole.Controller,
                new(Stable(moduleId, "encounter-warden-difficulty"), "ruleset:forge",
                    "party:forge-generic", EncounterDifficultyBand.Hard, 4, 12, 20, 1.2, 1.5),
                [new(Stable(moduleId, "encounter-warden-group"), "wave:warden", 2, 4, 36,
                    [Stable(moduleId, "encounter-candidate-elyra")])],
                new(Stable(moduleId, "encounter-warden-selection"), "party:forge-generic",
                    "challenge-rating", 42, true),
                new(Stable(moduleId, "encounter-warden-activation"),
                    EncounterActivationMode.PopulationMaintained, 30, null, EncounterSpawnDistribution.Weighted),
                new Dictionary<string, string> { ["selection.fallback"] = "lowest-cost" }), true,
            new(Stable(moduleId, "encounter-warden-scenario"), 1, "sample:r084",
                [new(Stable(moduleId, "encounter-warden-anchor-north"), "anchor:crypt-north",
                    new(new(2, 0, 4), QuaternionValue.Identity, Vector3Value.One), 8, true,
                    EncounterWorldProfile.VisibilityMode.BehindCover, true, 10)],
                EncounterSpawnDistribution.Weighted,
                new(Stable(moduleId, "encounter-warden-activator"), true, true, true, true, true,
                    EncounterNetworkAuthority.Server, ["party"]),
                new(Stable(moduleId, "encounter-warden-lifecycle"), "1.0.0",
                    new(Stable(moduleId, "encounter-warden-entered"), "EncounterEntered",
                        "scripts/encounter_entered.forge"),
                    new(Stable(moduleId, "encounter-warden-exited"), "EncounterExited",
                        "scripts/encounter_exited.forge"),
                    new(Stable(moduleId, "encounter-warden-exhausted"), "EncounterExhausted",
                        "scripts/encounter_exhausted.forge")),
                [new(Stable(moduleId, "encounter-warden-phase"), 1, "Assaut du gardien", ["area.occupied"],
                    [new(Stable(moduleId, "encounter-warden-wave-one"), 1,
                            [Stable(moduleId, "encounter-warden-group")], 0,
                            EncounterWaveCompletionPolicy.AllDefeated, null, null, null,
                            "audio:encounter-warden-warning", Stable(moduleId, "encounter-warden-wave-two")),
                        new(Stable(moduleId, "encounter-warden-wave-two"), 2,
                            [Stable(moduleId, "encounter-warden-group")], 2,
                            EncounterWaveCompletionPolicy.ObjectiveCompleted, null, quest,
                            "crypt-warden-defeated", "audio:encounter-warden-fallen", null)], [] )],
                new Dictionary<string, string> { ["graph.layout"] = "left-to-right" }), true,
            new(Stable(moduleId, "encounter-warden-statistics"), 1, "sample:r085", 64, 840,
                "duration:actors-linear", 5, "reward:cost-linear", .5, [.5, .9, .99], true,
                new Dictionary<string, string> { ["report.format"] = "distribution" }), true);

        var vehicle = new VehicleRouteProfile(routeId, 1, "sample:g13-v", area.Id,
            [new(start, Vector3Value.Zero, 0, 3), new(end, new(20, 0, 0), 0, 3)],
            [new("service", 0, 8, "forward")], [new(Stable(routeId, "stop"), end, 5, "crypt-lift")],
            8, false, "server-snapshots");

        var merchantTransactions = merchantItem is null ? null : new MerchantWorldProfile.TransactionPolicy(
            Stable(moduleId, "merchant-elyra-transactions"), 1, "sample:r045", "interaction:trade", "choice:trade",
            [
                new(Stable(moduleId, "merchant-elyra-stock"), merchantItem.Id, "standard", 12, 5,
                    "ember-crown", ["quest:crypt-open"]),
                new(Stable(moduleId, "merchant-elyra-stock-ritual"), merchantItem.Id, "ritual", 4, 9,
                    "covenant-token", []),
            ], ["fr-FR", "en-US"], ["quest:crypt-open"],
            new Dictionary<string, string> { ["onOpen"] = "scripts/merchant_open.forge" }, 100, 1000, true);
        var merchantFoundation = merchantItem is null ? null : new MerchantFoundationProfile(
            Stable(moduleId, "merchant-elyra-foundation"), 1, "sample:r091", "crypt-vigil-market",
            "merchant.crypt-vigil.name", ["fr-FR", "en-US"],
            [new(Stable(moduleId, "merchant-elyra-vendor"), elyra.Id, dialogue, "choice:trade",
                "interaction:trade", "npc")],
            new(Stable(moduleId, "merchant-elyra-wizard"), 1, "sample:r091", true, true, true, true, true,
                "merchant-crypt-vigil-nominal"),
            [
                new(Stable(moduleId, "merchant-elyra-foundation-stock"), merchantItem.Id, "standard",
                    MerchantSupplyPolicy.Infinite, 12, "supplier:ember-guild", 12, 1,
                    ["quest:market-closed"], 5, "ember-crown"),
                new(Stable(moduleId, "merchant-elyra-foundation-stock-ritual"), merchantItem.Id, "ritual",
                    MerchantSupplyPolicy.Finite, 4, null, 12, 2, [], 9, "covenant-token"),
            ],
            new(Stable(moduleId, "merchant-elyra-pricing"), 1, "sample:r091", 1.2m, .6m,
                [new(0, 1), new(100, 1.1m)], [new(0, 1), new(100, .9m)],
                new Dictionary<string, decimal> { ["ember-crown"] = 1, ["covenant-token"] = 2 },
                .1m, .2m, .2m, .1m, 1, 500),
            new(Stable(moduleId, "merchant-elyra-query"), 1, "sample:r091", ["relic", "consumable"],
                ["merchant-approved"], ["stolen", "cursed"], 1, 5, ["author:forge"], .5, null,
                "scripts/merchant_rule.forge"));
        var merchantExperience = merchantItem is null ? null : new MerchantStoreExperienceProfile(
            Stable(moduleId, "merchant-elyra-experience"), 1, "sample:r092",
            new(Stable(moduleId, "merchant-elyra-sessions"), 1, "sample:r092", "1.0.0",
                "StoreSessionOpened", "StoreSessionClosed", true, true, true, true),
            new(Stable(moduleId, "merchant-elyra-store-ui"), 1, "sample:r092", true, true, true, true, true,
                true, true, true, ["name", "compare", "price", "quantity"]));
        var merchant = new MerchantWorldProfile(Stable(moduleId, "merchant-elyra"), 1, "sample:g13-w-x", elyra.Id,
            [new("ember-crown", "currency.ember-crown", 1, true, false),
             new("covenant-token", "currency.covenant-token", 2, true, true)],
            [new("ember-crown", 1000, 100, 0, 100, 2000), new("covenant-token", 500, 0, 0, 50, 1000)],
            new(500, 800, new Dictionary<string, decimal> { ["weapon"] = 300 }, -.5, true),
            new(false, ["crypt"], true, true, .25, "crime.stolen-goods"),
            new(24, "market.new-day", routeId, true, true), new("bargain", 12, .2, .2, 60, true),
            new("ashlands", .25, .5, new Dictionary<string, double> { ["warden-defeated"] = -.1 }, true),
            [new(Stable(moduleId, "merchant-service-repair"), "repair", "service.repair", "ember-crown", 25,
                "repair.item", "item.damaged")], new(true, 90, 500, true, true, true),
            merchantTransactions, merchantTransactions is not null,
            merchantFoundation, merchantFoundation is not null,
            merchantExperience, merchantExperience is not null);

        var companion = new CompanionWorldProfile(Stable(moduleId, "companion-elyra"), 1, "sample:g13-y-z", elyra.Id,
            new(dialogue, "full", "limited", "greeting", "full", "deny"),
            new("wedge", 1, 1.5, true, true, true),
            new(CompanionWorldProfile.ProgressionMode.PlayerDirected, 4, 20, true, true, [5, 10, 15]),
            new(.5, .6, 0, .75, ["friend"], ["crypt-rescue"]),
            new([elyra.Id], 2, 90, ["crypt"], true, dialogue),
            new(quest, "loyalty>=0.5", "departed", .5, "camp-reaction"),
            new(true, true, 4, "sight", "disable-device", true),
            new(["heal", "shield"], ["ultimate"], .3, "lowest-health", true),
            new(true, true, true, true, "party.owner-or-gm"), new(true, true, "owner-instance", true, 120),
            new(Stable(moduleId, "companion-elyra-validation"), 1, "sample:r037",
                new(true, true, "scripts/companion_recruit.forge", "ember-crown", 25, .25),
                new("party", "server", true),
                ["move", "follow", "hold", "rally", "guard", "interact", "attack", "defend",
                    "focus-fire", "protect", "disengage", "use-ability"],
                new("companion.inventory", "companion.standard", ["main-hand", "off-hand", "armor"]),
                new("companion.recoverable", "recoverable-corpse", 30),
                [new("recruit", dialogue, "dialogue.recruit"), new("owner", dialogue, "dialogue.owner")],
                new(false, [area.Id], true, true, "party.safe", 15)),
            true,
            new(Stable(moduleId, "companion-elyra-foundation"), 1, "sample:r074",
                new(Stable(moduleId, "companion-elyra-recruitable"), 1, "sample:r074", true,
                    ["quest:crypt-open"], 25, "ember-crown", .25, ["relation:elyra-increase"], true, true,
                    "scripts/companion_recruit.forge"),
                new(Stable(moduleId, "companion-elyra-pre-recruitment"), 1, "sample:r074", dialogue,
                    "not-recruited", ["quest:crypt-open"], "offer.join", "offer.refuse",
                    ["relation:elyra-increase"]),
                new(Stable(moduleId, "companion-elyra-owner-dialogue"), 1, "sample:r074", dialogue,
                    "recruited-by-speaker", ["move", "follow", "hold", "rally", "guard", "interact",
                        "attack", "defend", "focus-fire", "protect", "disengage", "use-ability"], true, true,
                    true,
                    "companion.depart"),
                new(Stable(moduleId, "companion-elyra-membership"), 1, "sample:r074", true, true, true,
                    true, CompanionWorldProfile.CompanionPersistence.Campaign, "companion.inventory", "party.safe",
                    true),
                new(Stable(moduleId, "companion-elyra-party"), 1, "sample:r074", 3, 1, 1, 1, 8,
                    new Dictionary<string, int> { ["Support"] = 2 })),
            true,
            new(Stable(moduleId, "companion-elyra-control"), 1, "sample:r075",
                new(Stable(moduleId, "companion-elyra-ownership"), 1, "sample:r075", "individual",
                    ["player:owner"], true, ["party", "shared", "game-master"], 1),
                new(Stable(moduleId, "companion-elyra-movement"), 1, "sample:r075",
                    ["move", "follow", "hold", "rally", "guard", "interact"], "wedge", "party-waypoints",
                    0, 100),
                new(Stable(moduleId, "companion-elyra-tactical"), 1, "sample:r075",
                    ["attack", "defend", "focus-fire", "protect", "disengage", "use-ability"],
                    ["passive", "defensive", "balanced", "aggressive"], ["heal", "shield"], 0, 100),
                new(Stable(moduleId, "companion-elyra-ai"), 1, "sample:r075", .6, .4, .5, false,
                    "Support", 3, ["heal", "shield"], Stable(moduleId, "companion-elyra-ai-plan"),
                    Stable(moduleId, "companion-elyra-ai-transaction"),
                    "replace:/companion/control/ai|role:Support|autonomy:0.6", "owner-or-gm"),
                new(Stable(moduleId, "companion-elyra-equipment-control"), 1, "sample:r075", "full",
                    [Stable(moduleId, "companion-elyra-locked-heirloom")],
                    new Dictionary<string, Guid>
                    {
                        ["main-hand"] = Stable(moduleId, "companion-elyra-suggested-main-hand"),
                    }, true, "player:owner")),
            true,
            new(Stable(moduleId, "companion-elyra-experience"), 1, "sample:r076",
                new(Stable(moduleId, "companion-elyra-death-experience"), 1, "sample:r076",
                    "recoverable-corpse", 30, "resource:ember", "creature:elyra-heir",
                    "memorial:elyra", true),
                new(Stable(moduleId, "companion-elyra-cohesion-experience"), 1, "sample:r076",
                    10, 30, 60, true, true, ["party.safe"]),
                new(Stable(moduleId, "companion-elyra-party-hud"), 1, "sample:r076",
                    true, true, true, true, true, true, true, true, true, true,
                    ["health", "focus"])),
            true);

        var scripts = new ScriptAutomationProfile(moduleId, 1, "sample:g13-aa-ab",
            new([new(Stable(moduleId, "camera-door"), "rig.dialogue", "shot.door", 3, .5, true, true)],
                "restore-camera-and-control", true, true),
            new(true, true, [new("door.ash", "opened", "scripts/door_ash_opened.forge", true)]),
            [new("rest.enabled", ScriptAutomationProfile.SettingKind.Boolean, "true",
                ScriptAutomationProfile.FlagScope.Module, "Allows safe-rest volumes.", 1)],
            [new("combat.aggression", ScriptAutomationProfile.SettingKind.Choice, "balanced",
                ["passive", "balanced", "aggressive"], "Balanced target and risk selection.", true)]);

        return new(moduleId, 1, "sample:g13-n-ab", [placeableProfile], [doorProfile], [triggerProfile],
            [encounter], [vehicle], [merchant], [companion], scripts);
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

    private static PlaceableContextualVerbProfile Verb(Guid moduleId, string id, string label,
        int priority, IReadOnlyList<string> required, IReadOnlyList<string> forbidden, string binding)
        => new(Stable(moduleId, $"placeable-pillar-verb-{id}"), id, label, priority, required,
            forbidden, binding);

    private static TriggerGameplayVolumeProfile GameplayVolume(Guid moduleId, TriggerGameplayVolumeKind kind)
    {
        var parameter = kind switch
        {
            TriggerGameplayVolumeKind.Damage => ("health", -10d),
            TriggerGameplayVolumeKind.Heal => ("health", 10d),
            TriggerGameplayVolumeKind.Gravity => ("gravity", .5d),
            TriggerGameplayVolumeKind.Water => ("movement", .6d),
            TriggerGameplayVolumeKind.Climb => ("climb", 1d),
            TriggerGameplayVolumeKind.NoBuild => ("build", 0d),
            TriggerGameplayVolumeKind.Camera => ("camera", 1d),
            TriggerGameplayVolumeKind.Audio => ("audio", 1d),
            TriggerGameplayVolumeKind.Weather => ("weather", 1d),
            TriggerGameplayVolumeKind.Checkpoint => ("checkpoint", 1d),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        return new(Stable(moduleId, $"trigger-exit-volume-{kind}"), kind,
            new Dictionary<string, double> { [parameter.Item1] = parameter.Item2 },
            new Dictionary<string, string>(), $"{kind}VolumeEntered", $"{kind}VolumeExited", true);
    }
}
