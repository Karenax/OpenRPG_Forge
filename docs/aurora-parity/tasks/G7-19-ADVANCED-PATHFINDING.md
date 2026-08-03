# G7-19 — Pathfinding amélioré

Date d’exécution : 3 août 2026.

## Capacité bornée

- `ORF-EE-028` — pathfinding amélioré.

## Résultat livré

Le Core fournit maintenant une requête de chemin versionnée et indépendante du moteur. L’algorithme
A* est déterministe, borné par un budget de cellules visitées, utilise les liens hors-maillage et
retourne chemin, coût, instrumentation, points demandés/résolus et cause d’échec stable.

Une requête peut superposer des obstacles dynamiques et des multiplicateurs de coût sans rebake de
la surface. Les extrémités bloquées peuvent être récupérées vers la cellule disponible la plus
proche selon un ordre stable. `NavigationDynamicUpdatePlanner` calcule les seules cellules affectées
par un changement d’obstacle ou de coût, le nombre de cellules réutilisées et le seuil imposant une
réinitialisation complète.

L’écran Navigation calcule une route représentative pendant l’analyse et affiche longueur, cellules
visitées ou cause d’échec avec les métriques du bake. Le smoke utilise la surface réelle de la
Crypte et contrôle aussi une invalidation dynamique partielle.

## Preuves

- `NavigationPathfinderTests` couvre déterminisme, obstacle dynamique, coûts, récupération,
  inaccessibilité, budget et invalidation partielle ;
- `OPENRPG_FORGE_TOOLSET_G719_PATHFINDING_PASS` couvre route réelle, instrumentation et mise à jour
  dynamique dans le Toolset ;
- les tests G7-14 conservent profils d’agents, portes, vol, liens hors-maillage, persistance et bake
  partiel.

## Limites conservées

- l’adapter runtime de déplacement continu et l’évitement local entre agents restent à brancher ;
- les benchmarks présents mesurent un budget déterministe de cellules ; une campagne chronométrée
  sur cartes extérieures représentatives appartient à G11 ;
- la surface quadrillée actuelle reste l’implémentation Core ; l’évaluation Recast/adapter moteur
  demeure liée au futur terrain générique.

La capacité passe au statut `Implemented` partiel. La prochaine tranche unique est **G7-20 —
productivité de la coque Creator** pour `ORF-SHELL-008`, `009`, `012`, `021` et `023`.
