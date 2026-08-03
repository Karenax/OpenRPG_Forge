# G7-18 — Mise à l’échelle dans le Toolset

Date d’exécution : 3 août 2026.

## Capacité bornée

- `ORF-EE-002` — mise à l’échelle dans le Toolset.

## Résultat livré

Le transform canonique portait déjà une échelle tridimensionnelle persistante et le runtime la
reconstruisait. G7-18 complète le workflow auteur : valeurs X/Y/Z précises, verrou uniforme,
incréments ±10 % et Ctrl + molette dans le viewport. L’échelle reste une donnée du domaine, jamais
une mutation directe du nœud de représentation.

`ScaleEntitiesCommand` applique une valeur absolue ou un facteur multiplicatif à une ou plusieurs
entités en une transaction. Le handler impose une plage 0,05–100, refuse sélection vide, doublons et
entités absentes, puis compare les diagnostics géométriques avant/après pour bloquer toute nouvelle
sortie de zone ou collision. Le dry-run produit le même diff sémantique que l’application.

## Preuves

- `ScaleEntitiesHandlerTests` couvre valeurs absolues, dry-run, historique, contraintes et mutation
  atomique multi-entités ;
- `OPENRPG_FORGE_TOOLSET_G718_SCALE_PASS` couvre contrôles précis, uniformité/non-uniformité,
  validation, multi-édition et restauration de l’historique ;
- la sérialisation existante de `TransformValue` couvre la persistance exacte de X/Y/Z.

## Limites conservées

- le gizmo visuel conserve les axes de transform communs ; des poignées de scale sélectionnables par
  axe pourront remplacer les raccourcis lorsque le viewport multi-sélection sera livré ;
- la reconstruction de navigation est déterministe depuis le transform et exécutée au prochain
  bake ; l’invalidation incrémentale temps réel appartient au lot pathfinding suivant.

La capacité passe au statut `Implemented` partiel. La prochaine tranche unique est **G7-19 —
pathfinding amélioré** pour `ORF-EE-028`.
