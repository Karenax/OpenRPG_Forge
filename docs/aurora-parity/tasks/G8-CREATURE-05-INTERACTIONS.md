# G8 CREATURE-05 — Interactions de créature

Les dialogues associés sont désormais des bindings contextuels ordonnés par priorité, interlocuteur,
état et fallback. `InteractionRequested` conduit à `DialogueStarting` ou à un refus codé ; la
politique de protection sépare invulnérabilité, mort narrative, interruption et permissions MJ.

Le différentiel de perception émet `StimulusDetected` et `StimulusLost` avec sens, confiance,
position et mémoire. Les attaques physiques suivent `AttackDeclared` puis `AttackResolved`, avec
attaquant, arme, cible, résultat, dégâts et contexte d'autorité réseau conservé.

Preuves : `CreatureInteractionServiceTests`, suite RPG Kit et solution globale.

La prochaine tranche unique est **G8 CREATURE-06 — capacités ciblées, blocage et contrôleurs IA**.
