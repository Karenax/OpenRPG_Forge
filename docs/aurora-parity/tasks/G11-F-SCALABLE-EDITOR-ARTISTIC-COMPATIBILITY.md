# G11-F — Éditeur scalable et compatibilité artistique

## Périmètre

- `ORF-CONT-012` — police TrueType et UI scalable ;
- `ORF-EE-026` — TrueType, DPI et taille de texte ;
- `ORF-EE-003` — plusieurs onglets de zone ;
- `ORF-PAL-009` — favoris et récents ;
- `ORF-PAL-017` — compatibilité artistique.

## Résultat livré

- profil éditeur local versionné v2 avec échelle UI de 75 à 200 %, échelle de texte de 80 à 200 %,
  famille de police système et chaîne de repli ; le thème est appliqué à toute la surface Forge sans
  modifier le module exporté ;
- migration automatique des profils v1 et nettoyage des onglets qui référencent une zone supprimée ;
- jusqu’à huit onglets de zone, zone active persistée et seconde zone de comparaison explicitement en
  lecture seule ; les changements de zone conservent le contexte de sélection partagé ;
- favoris projet exportables, favoris personnels non exportés et historique ordonné des 24 blueprints
  récemment utilisés ;
- `ArtisticStyleProfile` canonique attaché au module : échelle métrique, densité texel, famille de
  shader, époque, réalisme et palette ;
- styles par blueprint et avertissements structurés pour chaque divergence, sans transformer une
  différence artistique en erreur bloquante ; les identités et références invalides restent bloquantes ;
- édition du profil artistique par commande transactionnelle avec dry-run, audit, undo/redo et
  persistance des projets aux formats `0.6.8` et `1.2.0`.

## Preuves

- `EditorWorkspaceProfileTests` et `EditorWorkspaceProfilePlannerTests` couvrent bornes, police,
  onglets, fermeture, réconciliation, favoris et récents ;
- `EditorWorkspaceProfileStoreTests` couvre le round-trip local et la migration v1 vers v2 ;
- `ArtisticStyleProfileTests` couvre les six familles d’avertissements et les références absentes ;
- `SetArtisticStyleProfileHandlerTests` couvre identité, dry-run, mutation et historique ;
- les tests de catalogue couvrent l’union des favoris projet/personnels et l’ordre des récents ;
- les tests de sérialisation couvrent le profil artistique, le round-trip et la migration des anciens
  projets ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_G11F_EXPERIENCE_PASS` : thème scalable, chaîne TrueType,
  deux onglets, comparaison, favoris, récents, commande artistique, persistance et restauration.

## Limites explicites

- le réglage de ce lot concerne l’éditeur Forge ; les interfaces de jeu exposeront leur propre profil
  accessible dans G11-T ;
- la comparaison conserve deux documents ouverts et une cible en lecture seule, sans encore rendre
  deux viewports 3D simultanés ;
- les collections et palettes de tâche avancées restent à généraliser au-dessus du catalogue ;
- le profil signale les incompatibilités déclarées : la mesure automatique des GLB et textures sera
  raccordée aux métriques du pipeline d’assets ;
- la chaîne de repli utilise les polices installées du système et ne distribue aucune police tierce.

## Critère de passage

G11-F est fermé lorsque tests ciblés, smoke Toolset, revue visuelle aux échelles 100/125/150 %,
tracker, graphe et `scripts/Verify.ps1` sont verts sur le même état Git.
