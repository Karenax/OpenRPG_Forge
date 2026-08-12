# G8 CREATURE-06 — Capacités, navigation et IA

Les capacités suivent désormais `AbilityTargeted` puis `AbilityImpacted`, avec source, cible, tags,
contexte, résultat et magnitude. `MovementBlocked` conserve obstacle, cause, état navigation,
position et choisit une stratégie explicite : attente, nouveau chemin, évitement, rattrapage ou abandon.

`AIControllerProfile` rend interchangeables machine à états, behavior tree, utility et script. Son
blackboard typé et sa persistance sont inspectables ; les actions autorisées sont bornées et le choix
utility est stable en cas d'égalité.

R080 relie les cycles `AbilityTargeted`/`AbilityImpacted` et `MovementBlocked`, avec ordre de
récupération explicite, au blueprint canonique. Le même profil UUID-bearing contient le contrôleur,
sa définition, ses actions et chaque entrée typée du blackboard. L’atelier à cinq sections montre
les quatre familles d’événements et simule une décision ; dry-run, application, undo/redo et
persistance passent par la commande de fondation existante. ForgeScript et MCP exposent des vues
bornées, tandis que le runtime reconstruit cinq groupes de nœuds dérivés et valide l’adaptateur RPG
Kit.

Preuves : `CreatureEventAiCompositionProfileTests`,
`CreatureEventAiCompositionFoundationServiceTests`, `CreatureAiServiceTests`,
`SetCreatureFoundationHandlerTests`, `ProjectJsonSerializerTests`, `ForgeProjectAgentServiceTests`,
capture R080 inspectée et marqueurs Toolset/runtime R080.

La famille Créature G8 P1 est complète. La prochaine tranche séquentielle est **R081 / G8 DOOR**.
