# ADR-0039 — Profils canoniques de profondeur quête et objet

Date : 6 août 2026
Statut : accepté

## Décision

Les capacités avancées G13-J à G13-M étendent les documents canoniques existants par deux profils
optionnels : `QuestOperationsProfile` sur `QuestDefinition` et `ItemAdvancedProfile` sur les
blueprints de type objet. Aucun document parallèle n’est créé et aucune donnée d’auteur n’est
stockée dans Godot. Les profils absents conservent le comportement historique.

Les resolvers purs restent dans `Forge.Domain`. Ils reçoivent contexte, seed, temps, autorité et
révision comme paramètres et ne lisent ni horloge machine, ni état moteur, ni réseau. Les opérations
qui réservent une unicité ou consomment une recette sont transactionnelles. La mutation auteur passe
par les handlers d’application identifiés, avec dry-run, diff, audit, undo/redo et contrôle de
révision. Toolset, ForgeScript, MCP, runtime et package consomment tous cette même source.

Le format projet passe en `0.6.31` et le workspace en `1.25.0`. `0.6.30` et `1.24.0` restent lisibles
avec profils absents. Les références externes sont des identifiants gouvernés et les textes visibles
restent localisables et accessibles.

## Conséquences

- une quête peut ordonner, répéter, partager, rendre exclusifs et notifier ses changements sans
  contaminer son graphe d’états ;
- un objet possède une extension cohérente de l’économie à l’artisanat sans dupliquer son blueprint ;
- déterminisme et autorité sont testables sans lancer le moteur ;
- l’interface peut être enrichie sans migration du modèle ;
- les habillages 3D, animations, votes et notifications restent des consommateurs de ces décisions,
  pas une seconde source de vérité.
