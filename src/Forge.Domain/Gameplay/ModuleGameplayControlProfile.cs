using Forge.Domain.Modules;
using Forge.Domain.RuntimePolicies;

namespace Forge.Domain.Gameplay;

public sealed record ModuleGameplayControlProfile
{
    public const int CurrentSchemaVersion = 1;

    private static readonly string[] SharedCombatEvents =
        ["combat.started", "combat.action.requested", "combat.action.resolved", "combat.ended"];

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public SwimmingProfile Swimming { get; }
    public ClimbingProfile Climbing { get; }
    public RadialMenuProfile RadialMenu { get; }
    public TimeControlPolicy TimeControl { get; }
    public CombatModeProfile CombatModes { get; }
    public string Provenance { get; }

    public ModuleGameplayControlProfile(Guid id, int schemaVersion, SwimmingProfile swimming,
        ClimbingProfile climbing, RadialMenuProfile radialMenu, TimeControlPolicy timeControl,
        CombatModeProfile combatModes, string provenance)
    {
        ArgumentNullException.ThrowIfNull(swimming);
        ArgumentNullException.ThrowIfNull(climbing);
        ArgumentNullException.ThrowIfNull(radialMenu);
        ArgumentNullException.ThrowIfNull(timeControl);
        ArgumentNullException.ThrowIfNull(combatModes);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Gameplay control profile is invalid.");
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Swimming = swimming;
        Climbing = climbing;
        RadialMenu = radialMenu;
        TimeControl = timeControl;
        CombatModes = combatModes;
        Provenance = provenance.Trim();
    }

    public static ModuleGameplayControlProfile CreateDefault(ModuleId moduleId)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        Guid Stable(string value) => RuntimePolicyIdFactory.Create(moduleId.Value, $"gameplay:{value}");
        var provenance = "migration-default";
        var swimming = new SwimmingProfile(Stable("swimming"), 1, true, false, 8, .75f, 4.5f, 3,
            .65f, 45, ["water", "swimmable"], "swim.surface", "swim.dive", "swim.idle", provenance);
        var climbing = new ClimbingProfile(Stable("climbing"), 1, true, false, 3, 100, 12, 1.5f,
            true, true, true, ["climb", "ladder", "ledge"], "climb.loop", "climb.ledge", provenance);
        var actions = new[]
        {
            new RadialActionDefinition(Stable("radial:interact"), "interact", "Interagir", "Interagir avec la cible",
                RadialActionKind.Verb, "interact", string.Empty, 0, true, ["world"]),
            new RadialActionDefinition(Stable("radial:combat"), "combat", "Combat", "Actions de combat",
                RadialActionKind.Navigation, "open:combat", string.Empty, 1, false, ["combat"]),
            new RadialActionDefinition(Stable("radial:attack"), "attack", "Attaquer", "Attaquer la cible",
                RadialActionKind.Ability, "attack", "combat", 0, true, ["combat"]),
            new RadialActionDefinition(Stable("radial:rest"), "rest", "Se reposer", "Commencer un repos",
                RadialActionKind.Verb, "rest", string.Empty, 2, false, ["recovery"]),
            new RadialActionDefinition(Stable("radial:system"), "system", "Système", "Actions système",
                RadialActionKind.Navigation, "open:system", string.Empty, 3, false, ["system"]),
            new RadialActionDefinition(Stable("radial:save"), "save", "Sauvegarde rapide", "Sauvegarder la partie",
                RadialActionKind.System, "quick_save", "system", 0, false, ["system"]),
        };
        var radial = new RadialMenuProfile(Stable("radial"), 1, true, "action_wheel", .2f, 3,
            true, true, true, true, actions, provenance);
        var time = new TimeControlPolicy(Stable("time"), 1, true, true, .2f, true, 8, true,
            MultiplayerPausePolicy.HostOnly, .5f, ["session.host", "session.moderator"], provenance);
        var providers = new[]
        {
            new CombatModeProviderDefinition(Stable("combat:realtime"), "realtime", "Temps réel",
                CombatModeKind.RealTime, string.Empty, SharedCombatEvents, true),
            new CombatModeProviderDefinition(Stable("combat:rtwp"), "realtime-pause", "Temps réel avec pause",
                CombatModeKind.RealTimeWithPause, string.Empty, SharedCombatEvents, true),
            new CombatModeProviderDefinition(Stable("combat:turns"), "turn-based", "Tour par tour",
                CombatModeKind.TurnBased, string.Empty, SharedCombatEvents, true),
        };
        var combat = new CombatModeProfile(Stable("combat"), 1, "realtime-pause", providers, provenance);
        return new ModuleGameplayControlProfile(moduleId.Value, 1, swimming, climbing, radial, time, combat,
            provenance);
    }
}
