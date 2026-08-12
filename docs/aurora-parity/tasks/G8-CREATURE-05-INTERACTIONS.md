# G8 CREATURE-05 — Interactions de créature

Les dialogues associés sont désormais des bindings contextuels ordonnés par priorité, interlocuteur,
état et fallback. `InteractionRequested` conduit à `DialogueStarting` ou à un refus codé ; la
politique de protection sépare invulnérabilité, mort narrative, interruption et permissions MJ.

R079 attache ces contrats au blueprint canonique avec UUID, schéma, provenance, AssetId, transform
indépendant du moteur, overrides et binding `creature.interaction_requested` serveur. L’auteur
dispose de cinq sections, d’états vide/erreur, d’un aperçu et d’une simulation de sélection. La même
commande assure dry-run, diff sémantique, application, undo/redo et persistance. ForgeScript lit par
handle sûr, le MCP filtre et prévisualise sans mutation, et le runtime reconstruit factions,
perception, bindings, protection et événement sous forme de nœuds dérivés.

Le différentiel de perception émet `StimulusDetected` et `StimulusLost` avec sens, confiance,
position et mémoire. Les attaques physiques suivent `AttackDeclared` puis `AttackResolved`, avec
attaquant, arme, cible, résultat, dégâts et contexte d'autorité réseau conservé.

R080 attache désormais ces deux cycles au profil événement/IA canonique et à des bindings ForgeScript
serveur distincts. Ils sont authorables, validés, persistés, lisibles par handle et MCP, simulés par
l’adaptateur RPG Kit puis matérialisés en nœuds runtime sans voie de mutation parallèle.

Preuves : `CreatureInteractionCompositionProfileTests`,
`CreatureInteractionCompositionFoundationServiceTests`, `SetCreatureFoundationHandlerTests`,
`ProjectJsonSerializerTests`, `ForgeProjectAgentServiceTests`, capture R079 inspectée et marqueurs
Toolset/runtime R079 et R080.

La tranche Créature se poursuit dans **G8 CREATURE-06 — capacités ciblées, blocage et contrôleurs IA**.
