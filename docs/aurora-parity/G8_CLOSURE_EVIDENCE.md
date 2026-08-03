# Clôture G8 — RPG Kit

Date de clôture : 4 août 2026.

## Résultat

Le gate G8 est fermé selon `G8_DELIVERY_PLAN.md`.

1. **Catalogue** — les 184 exigences assignées à G8 sont `Implemented`, sans ligne `Researched` ni
   preuve vide. Chaque statut ajouté conserve le préfixe `Partial:` pour distinguer la tranche
   réellement livrée des extensions futures.
2. **Domaines RPG Kit** — 40 lots bornés couvrent créatures, factions, items, placeables, portes,
   triggers, rencontres, marchands, compagnons, dialogues, quêtes et ForgeScript. Les services
   testent règles, transactions, validation, événements, sérialisation et déterminisme.
3. **Auteur** — `RpgKitAuthoringWindow` présente neuf familles sans édition JSON. Prévisualisation,
   application, undo/redo et diagnostics utilisent `UpsertBlueprintCommand` et
   `ProjectCommandHistory`; le smoke restaure le projet après essai.
4. **Module original** — `OriginalModuleAuthoringService` génère **La Veillée des Éclats** par 17
   commandes et `ProjectSession` (révision 16). L'export `.orpmod` est valide. Le runtime reconstruit
   8 entités, 3 collisions, 2 combattants, un dialogue à deux nœuds et une quête à trois états.
5. **Visuel** — revue nominale et maximisée passée après correction du rafraîchissement initial et
   des ancres de redimensionnement. Preuves locales :
   `artifacts/verification/visual-g8/g8-author-ui-nominal.png` et
   `artifacts/verification/visual-g8/g8-author-ui-maximized.png`.
6. **Portail** — `scripts/Verify.ps1` termine par `OPENRPG_FORGE_VERIFICATION=PASS`; build Release
   sans avertissement, 432 tests .NET, 24 tests tracker/outillage, tracker frais, graphe déterministe,
   branding, Toolset, carte forêt, trois modules, MCP et sauvegarde/reprise sont verts.

## Corrections découvertes par le gate

- le runtime refusait les créatures auteurées sans profil : `SetBlueprintCombatCommand` ferme cette
  voie et les deux profils sont maintenant transactionnels ;
- l'atelier n'occupait pas la fenêtre maximisée et pouvait afficher un état vide avant chargement :
  les ancres, split responsive et rafraîchissement après ouverture de projet sont corrigés ;
- l'exécution du second module est maintenant un gate permanent de `Verify.ps1`.

## Passage

G9 **Network Beta** peut commencer par un audit de preuves des 30 exigences réseau, sans réouvrir
les lots G7/G8 validés.
