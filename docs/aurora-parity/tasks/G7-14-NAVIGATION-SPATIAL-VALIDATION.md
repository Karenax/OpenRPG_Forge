# G7-14 — Navigation et validation spatiale

Date d’exécution : 2 août 2026.

## Capacités bornées

- `ORF-AREA-031` — navigation par zone, agents multiples, liens hors-maillage et rebuild partiel ;
- `ORF-QA-004` — contrôles géométriques, limites et sockets ;
- `ORF-QA-005` — portée depuis les spawns selon portes, profils et liens.

## Résultat livré

Chaque `AreaDefinition` porte désormais une `AreaNavigationDefinition` v1 à UUID stable. Le document
déclare la taille de cellule, quatre profils canoniques Petit/Moyen/Grand/Volant et des liens typés
Saut/Porte/Vol. Le domaine construit une grille navigable déterministe sans dépendance au moteur,
applique le rayon de l’agent aux limites et obstacles, traite trois politiques de portes et expose un
pathfinder avec arêtes hors-maillage. Le fingerprint du bake et `NavigationBakePlanner` distinguent
un bake complet, un résultat réutilisé et les cellules réellement modifiées.

`AreaGeometryValidator` signale les volumes hors limites, overlaps de collisions et sockets de
transition incompatibles. `AreaNavigationValidator` teste chaque profil depuis tous les
`PlayerSpawn` vers les créatures, portes, triggers, objets et objectifs, puis localise liens hors zone,
extrémités bloquées et routes de vol sans profil compatible.

L’écran Aurora **Navigation & géométrie** édite profils et liens, analyse simultanément les états de
portes, présente les diagnostics et affiche les cellules praticables en turquoise et les liens en
violet dans le viewport. L’application passe par `SetAreaNavigationCommand` avec dry-run, patch,
inverse, audit, undo et redo. Le format de zone passe à 1.1, le document agrégé à 0.6.5 et les anciens
documents migrent vers une configuration stable. Les bakes dérivés sont conservés atomiquement dans
`.forge/navigation/`; `forge package spatial <workspace>` valide, bake et publie un bilan JSON.

## Preuves

- `NavigationAndGeometryTests` couvre IDs stables, tailles d’agents, vol, portes, liens, rebuild
  partiel, reachability, limites, overlaps et sockets ;
- `SetAreaNavigationHandlerTests` couvre dry-run, conflit de révision, application et undo/redo ;
- `NavigationPersistenceTests` couvre migration 1.0, round-trip 1.1 et cache de bake versionné ;
- `OPENRPG_FORGE_SPATIAL_VALIDATION_PASS` prouve la validation CLI de la Crypte ;
- `OPENRPG_FORGE_TOOLSET_G714_NAVIGATION_PASS` prouve écran, overlay, commande, historique,
  persistance, portes, liens, vol et rebuild partiel dans le smoke Creator.

## Limites conservées

- la grille Core n’est pas encore un maillage polygonal triangulé ni un système de foule ;
- les polygones de collision arbitraires auto-intersectés attendent un schéma géométrique dédié ;
- le runtime ne consomme pas encore les bakes pour le déplacement autoritaire ;
- la reconstruction partielle identifie précisément les cellules modifiées, mais l’adapter moteur
  futur devra encore traduire ce plan en mises à jour de régions natives ;
- le terrain, l’eau, les routes et le streaming massif restent hors de cet incrément.

Les trois capacités passent au statut `Implemented` partiel.
