# G8 CREATURE-02 — Assistant, combat et validation

`Forge.RpgKit.Application` constitue désormais la couche de commandes du kit. L'assistant crée une
créature depuis archétype, ruleset, rig, faction, rôle, niveau de menace et kit visuel. Dry-run et
application produisent le même patch ; la révision, le conflit, le doublon et le patch inverse sont
explicites.

Le service de combat applique les hooks `BeforeDamage` dans un domaine borné, commit la ressource
santé puis émet `DamageApplied`. Si la santé atteint zéro, `EntityDied` suit avec killer,
contributeurs dédupliqués, cause, transform, loot et policy.

La validation structurée vérifie données canoniques, rig, apparence, combat, inventaire, faction,
scripts, ressources et budgets de menace, avec code et chemin de document stables.

Preuves : `CreatureCombatAndAuthoringTests`, `CreatureFactoryTests`, suite globale.

La prochaine tranche unique est **G8 CREATURE-03 — espèce, apparence, animations, pose et attributs**.
