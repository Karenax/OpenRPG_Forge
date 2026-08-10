using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;

namespace Forge.Application.Projects;

public static class CryptWorldFeatureAuthoringService
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
        var routeId = Stable(moduleId, "vehicle-route");
        var start = Stable(routeId, "start");
        var end = Stable(routeId, "end");

        var placeableProfile = new PlaceableWorldProfile(Stable(moduleId, "placeable-pillar"), 1,
            "sample:g13-n-o", placeable.Id, "ash-watch", dialogue,
            new("key:ember", PlaceableWorldProfile.CredentialConsumption.Charge, 3, null, true),
            new([
                new("physical-attack-resolved", "scripts/pillar_attack", false, true),
                new("ability-impacted", "scripts/pillar_ability", false, true),
                new("heartbeat", "scripts/pillar_heartbeat", false, true),
                new("custom:crypt:pillar-pulse", "scripts/pillar_pulse", false, true),
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
                    new Dictionary<string, string> { ["onUsed"] = "scripts/pillar_used" }),
                new(new(1.25f, 0, -2.5f), new(0, 45, 0), Vector3Value.One, new(0, .5f, 0), .25,
                    null, true, "animation:crypt-pillar-rise"),
                new Dictionary<string, string> { ["material"] = "ash-stone", ["variant"] = "lit" },
                new(Stable(moduleId, "placeable-pillar-on-used"), 1, "sample:r047", "1.0.0",
                    "InteractionExecuted", "scripts/pillar_before_used", "scripts/pillar_after_used"),
                new(["animation:crypt-pillar-rise"], ["lock:crypt-pillar"], ["trap:ember"],
                    ["inventory:crypt-pillar"], ["physics:rigid-placeable"], 8, 8), true), true);

        var doorProfile = new DoorWorldProfile(Stable(moduleId, "door-ashes"), 1, "sample:g13-p-q", door.Id,
            new(40, 4, false, "door.damaged", "door.breached", true, "assets/debris/ash-door.glb", "service.repair-door"),
            new("assets/loading/crypt-of-ashes.png", "loading.crypt.title", "loading.crypt.tip", true, .5),
            [new("damage-applied", "scripts/door_damage", false, true),
             new("entity-destroyed", "scripts/door_destroyed", false, true),
             new("physical-attack-resolved", "scripts/door_attack", false, true),
             new("ability-impacted", "scripts/door_ability", false, true),
             new("heartbeat", "scripts/door_heartbeat", false, true),
             new("custom:crypt:door-seal", "scripts/door_seal", false, true)],
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
                [new(Stable(moduleId, "door-link"), Stable(moduleId, "door-destination"), false, "Retour vers la crypte")],
                new Dictionary<string, string> { ["interaction.prompt"] = "door.open" },
                new("nav-obstacle:crypt-door", "nav-link:crypt-door",
                    ["closed", "locked", "jammed", "disabled", "custom"], ["open", "broken"], .75, .25, true)));

        var triggerProfile = new TriggerWorldProfile(Stable(moduleId, "trigger-exit"), 1, "sample:g13-r-s",
            trigger.Id, area.Id,
            new(["sight", "search"], 10, Stable(moduleId, "secret-reveal"), "feedback.secret", true,
                TriggerWorldProfile.DiscoveryScope.Party),
            new("inspect", "cursor.secret", "outline.gold", "trigger.secret.prompt", "scripts/trigger_click", true, "player"),
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
                    null, "scripts/trigger_enter"),
                new(Stable(moduleId, "trigger-exit-on-exit"), 1, "sample:r048", "1.0.0", "VolumeExited",
                    null, "scripts/trigger_exit"),
                new([Stable(moduleId, "trigger-exit-destination")], ["encounter:crypt-warden"],
                    ["scripts/trigger_click", "scripts/trigger_enter", "scripts/trigger_exit"], ["characters"]),
                true), true);

        var encounter = new EncounterWorldProfile(Stable(moduleId, "encounter-warden"), 1, "sample:g13-t-u",
            area.Id, new(30, 3, "area.occupied", true, true, .5), "ash-hostile",
            new(true, 5, 12, "crypt.encounter"), new(2, "ash-hostile", routeId, 4, 3, "boss.dead"),
            new(EncounterWorldProfile.VisibilityMode.BehindCover, 10, true, "vfx.spawn.ash"),
            new(30, 30, 120, true, true),
            new(new(EncounterWorldProfile.VolumeShape.ExtrudedPolygon2D,
                    [new(-4, 0, -4), new(4, 0, -4), new(4, 0, 4), new(-4, 0, 4)], Vector3Value.Zero,
                    4, 8, ["player"], ["party"], true, 18, "scripts/encounter_activate", true),
                [new(Stable(moduleId, "encounter-candidate-elyra"), elyra.Id,
                    EncounterWorldProfile.CandidateRole.Controller, 3, 8, ["area.occupied"], ["ember"], 4)],
                36, ["anchor:crypt-north"], ["reward:ember-shard"], 12));

        var vehicle = new VehicleRouteProfile(routeId, 1, "sample:g13-v", area.Id,
            [new(start, Vector3Value.Zero, 0, 3), new(end, new(20, 0, 0), 0, 3)],
            [new("service", 0, 8, "forward")], [new(Stable(routeId, "stop"), end, 5, "crypt-lift")],
            8, false, "server-snapshots");

        var merchantTransactions = merchantItem is null ? null : new MerchantWorldProfile.TransactionPolicy(
            Stable(moduleId, "merchant-elyra-transactions"), 1, "sample:r045", "interaction:trade", "choice:trade",
            [new(Stable(moduleId, "merchant-elyra-stock"), merchantItem.Id, "standard", 12, 5, "ember-crown",
                ["quest:crypt-open"])], ["fr-FR", "en-US"], ["quest:crypt-open"],
            new Dictionary<string, string> { ["onOpen"] = "scripts/merchant_open" }, 100, 1000, true);
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
            merchantTransactions, merchantTransactions is not null);

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
                new(true, true, "scripts/companion_recruit", "ember-crown", 25, .25),
                new("party", "server", true),
                ["follow", "hold", "attack", "interact"],
                new("companion.inventory", "companion.standard", ["main-hand", "off-hand", "armor"]),
                new("companion.recoverable", "recoverable-corpse", 30),
                [new("recruit", dialogue, "dialogue.recruit"), new("owner", dialogue, "dialogue.owner")],
                new(false, [area.Id], true, true, "party.safe", 15)),
            true);

        var scripts = new ScriptAutomationProfile(moduleId, 1, "sample:g13-aa-ab",
            new([new(Stable(moduleId, "camera-door"), "rig.dialogue", "shot.door", 3, .5, true, true)],
                "restore-camera-and-control", true, true),
            new(true, true, [new("door.ash", "opened", "scripts/door_ash_opened", true)]),
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
}
