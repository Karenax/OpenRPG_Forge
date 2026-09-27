# Tidewater — végétation rendue depuis le workspace sauvegardé

Le prototype `tidewater_plant_probe` accepte, après ses paramètres de simulation,
`--workspace-vegetation WORKSPACE_ROOT AREA_ID`. Il ouvre la session workspace
existante, sélectionne une référence de zone unique, vérifie son archive et ses
sources immuables, puis charge végétation et retouches depuis cette autorité.
Le hash du terrain source doit correspondre au terrain de la scène. Les anciens
records canoniques éventuellement présents dans la fixture sont remplacés.
Le verrou workspace reste détenu jusqu'à la fin de cette lecture initiale.

Les plantes visibles et leurs casters utilisent désormais le même jeu de données
en mémoire : le graphe d'ombres ne recharge pas une autre population depuis la
fixture. L'exemple d'ombres autonome conserve son chargement de fixture validée.

## Exécution

Racine des artefacts : `E:/OpenRPG_Forge/.tidewater-integration/`.

- `workspace-source-store-002` : dix blocs de végétation à six paramètres et un
  bloc de terrain, contrôlés par `verify_imported_vegetation`.
- `workspace-adoption-002` : adoption, sauvegarde et réouverture par le parcours
  `FileWorkspaceSession`, 11 blobs et 27238496 octets. Archive de la clairière
  `b97bfba1b12cb39fd961c5879f14debb36be3a028973c1c737c6a6f8593a0603`.
- `workspace-vegetation-scene-001` : 343 fichiers de géométrie, matériaux et
  environnement conservés ; aucune entrée `canonical-*`, aucun des 24 anciens
  buffers d'instances. La fixture ne contient donc aucune population de secours.
- `workspace-render-proof-001/saved-clearing.png` : 64868 plantes visibles,
  515 exclues dans les deux consommateurs. Image inspectée, strictement identique
  à `native-authored-clearing-002.png`, zéro pixel différent en FHD.
- `missing-area` : refus avant capture pour un identifiant absent.
- `corrupt-blob` : refus avant capture après modification d'un octet dans une
  copie jetable du workspace. L'autorité source reste intacte, contrôlée par hash
  de tous ses fichiers avant/après les trois essais.

Rapport : `workspace-render-proof-001/report.json`. Harnais reproductible :
`scripts/tidewater/verify-workspace-vegetation.py ROOT BINARY NEW_PROOF_DIRECTORY`.
Binaire SHA-256 :
`1ae2f367d0d2ec9a29cc4dd9c91dabe3e6f1dcb7f548e46b5b70303dd4b73fe5`.
Compilation du prototype et Clippy strict des exemples de scène et d'ombres
réussis. Aucun commit ni publication.

## Portée

Ce raccordement concerne la végétation et ses exclusions/densité sauvegardées.
Il ne rend pas encore les corrections de relief, les placements manuels promus,
les routes ou les bâtiments modifiés. Le terrain est seulement apparié à la
source par hash ; les autres ressources de scène restent issues de la fixture.
La scène ne constitue pas encore un module autonome. Sélection et édition dans
Studio, rechargement à chaud, catalogue, collisions, navigateur et qualification
complète de la parité restent ouverts.
