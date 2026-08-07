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
            new(PlaceableWorldProfile.BodyMode.Rigid, 12, ["lock-y"], true, 4, true, "server"));

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
                "open-together", true, "close-and-restore"));

        var triggerProfile = new TriggerWorldProfile(Stable(moduleId, "trigger-exit"), 1, "sample:g13-r-s",
            trigger.Id, area.Id,
            new(["sight", "search"], 10, Stable(moduleId, "secret-reveal"), "feedback.secret", true,
                TriggerWorldProfile.DiscoveryScope.Party),
            new("inspect", "cursor.secret", "outline.gold", "trigger.secret.prompt", "scripts/trigger_click", true, "player"),
            new(2, 30, true, true), "crypt.trigger", new("key:ember", false, "trigger.key.denied"),
            new(true, "danger.nearby", true, true, "rest.allowed", "rest.denied"),
            new("tracking", 8, "ash-tracks", 45, 2, "false-trail", quest),
            new([elyra.Id], 4, true, 60, "party-vote", dialogue), new(true, true, true, true, true));

        var encounter = new EncounterWorldProfile(Stable(moduleId, "encounter-warden"), 1, "sample:g13-t-u",
            area.Id, new(30, 3, "area.occupied", true, true, .5), "ash-hostile",
            new(true, 5, 12, "crypt.encounter"), new(2, "ash-hostile", routeId, 4, 3, "boss.dead"),
            new(EncounterWorldProfile.VisibilityMode.BehindCover, 10, true, "vfx.spawn.ash"),
            new(30, 30, 120, true, true));

        var vehicle = new VehicleRouteProfile(routeId, 1, "sample:g13-v", area.Id,
            [new(start, Vector3Value.Zero, 0, 3), new(end, new(20, 0, 0), 0, 3)],
            [new("service", 0, 8, "forward")], [new(Stable(routeId, "stop"), end, 5, "crypt-lift")],
            8, false, "server-snapshots");

        var merchant = new MerchantWorldProfile(Stable(moduleId, "merchant-elyra"), 1, "sample:g13-w-x", elyra.Id,
            [new("ember-crown", "currency.ember-crown", 1, true, false),
             new("covenant-token", "currency.covenant-token", 2, true, true)],
            [new("ember-crown", 1000, 100, 0, 100, 2000), new("covenant-token", 500, 0, 0, 50, 1000)],
            new(500, 800, new Dictionary<string, decimal> { ["weapon"] = 300 }, -.5, true),
            new(false, ["crypt"], true, true, .25, "crime.stolen-goods"),
            new(24, "market.new-day", routeId, true, true), new("bargain", 12, .2, .2, 60, true),
            new("ashlands", .25, .5, new Dictionary<string, double> { ["warden-defeated"] = -.1 }, true),
            [new(Stable(moduleId, "merchant-service-repair"), "repair", "service.repair", "ember-crown", 25,
                "repair.item", "item.damaged")], new(true, 90, 500, true, true, true));

        var companion = new CompanionWorldProfile(Stable(moduleId, "companion-elyra"), 1, "sample:g13-y-z", elyra.Id,
            new(dialogue, "full", "limited", "greeting", "full", "deny"),
            new("wedge", 1, 1.5, true, true, true),
            new(CompanionWorldProfile.ProgressionMode.PlayerDirected, 4, 20, true, true, [5, 10, 15]),
            new(.5, .6, 0, .75, ["friend"], ["crypt-rescue"]),
            new([elyra.Id], 2, 90, ["crypt"], true, dialogue),
            new(quest, "loyalty>=0.5", "departed", .5, "camp-reaction"),
            new(true, true, 4, "sight", "disable-device", true),
            new(["heal", "shield"], ["ultimate"], .3, "lowest-health", true),
            new(true, true, true, true, "party.owner-or-gm"), new(true, true, "owner-instance", true, 120));

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
