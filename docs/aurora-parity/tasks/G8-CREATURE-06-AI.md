# G8 CREATURE-06 — Capacités, navigation et IA

Les capacités suivent désormais `AbilityTargeted` puis `AbilityImpacted`, avec source, cible, tags,
contexte, résultat et magnitude. `MovementBlocked` conserve obstacle, cause, état navigation,
position et choisit une stratégie explicite : attente, nouveau chemin, évitement, rattrapage ou abandon.

`AIControllerProfile` rend interchangeables machine à états, behavior tree, utility et script. Son
blackboard typé et sa persistance sont inspectables ; les actions autorisées sont bornées et le choix
utility est stable en cas d'égalité.

Preuves : `CreatureAiServiceTests`, suite RPG Kit et solution globale.

La famille Créature G8 est complète. La prochaine tranche unique est **G8 FACTION-01**.
