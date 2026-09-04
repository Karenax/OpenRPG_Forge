# Gate 2 — Onglets et viewport du projet

Date : 4 septembre 2026. Branche : `Rust`.

## État de qualification

**Candidat en cours de qualification : aucun nouveau PASS de route.** Gate 2 reste à **4/43**.
Les changements sont une projection des zones réelles, pas une nouvelle scène de preuve intégrée.
Les réserves Gate 1, ADR-0010/0011 et la CI distante `NOT RUN — infrastructure billing` restent
inchangées. Aucun asset Cadle, média du site de référence ou nouvelle dépendance n'est ajouté.
`WheelEvent` est activé sur la dépendance web-sys déjà épinglée ; Cargo.lock reste inchangé.

## Contrat du candidat

- Troisième fenêtre Monde dans le bureau doré. Projets conserve son assistant et ses commandes ;
  sélection d'une zone → Ouvrir dans Monde. Le navigateur Monde ouvre aussi les zones du projet.
- Onglets par IDs projet/module/zone, sans doublon ; fermeture purement visuelle, voisin sélectionné,
  réconciliation après changements de sources, fermeture et ouverture de projet.
- Préparation native en lecture seule, liée à la révision observée. Refus des identités étrangères,
  absentes ou périmées ; source copiée sous verrou puis génération sur le pool bloquant Tauri.
  Une génération native au maximum ; la file UI conserve la dernière demande et ignore les réponses
  devenues obsolètes. Transfert dérivé plafonné à 24 Mio, sans écriture de fichier.
- Même générateur et shader terrain que Forge ; 64 chunks adaptés deux par update, révélés ensemble.
  Géométrie antérieure et assets mesh libérés. Les colliders sont encore présents dans le transfert
  du contrat existant, mais ni physique ni personnage ne sont installés dans cet aperçu.
- Un seul canvas et renderer dans le bureau. Diagnostics observe ce viewport ; les routes de preuve
  Gate 0 et Visual Valley restent séparées. Fenêtre masquée : canvas conservé, rendu non suspendu.
- Orbite, déplacement Maj-glisser, zoom molette, vue d'ensemble et cadrage du départ. Caméras par zone
  conservées dans la page, bornées à 256 ; pas de caméra persistée ni de commandes sources implicites.
  Le cube doré est la métadonnée de départ exacte : il peut être sous le terrain, il n'est pas recalé.
- Le ledger sépare démarrage demandé/démarrage ECS observé, source/révision appliquées, chunks,
  caméra et temps de boucle CPU. Présentation et timestamps GPU restent non observés par ce ledger.

## Preuves déjà acquises

- Check natif moteur/Studio PASS.
- Tests ciblés moteur + bibliothèque Studio : **224 PASS** (158 + 66), aucun échec/ignoré.
- Suite complète workspace/all-targets : **440 PASS**, aucun échec/ignoré.
- Clippy workspace/all-targets strict natif PASS ; Clippy Studio WASM strict PASS après correction
  d'un import ambigu et découpage de l'installation du polling. Aucun warning projet supprimé.
- Diff-check PASS. Le warning futur de `proc-macro-error2 2.0.1` préexiste.

Les builds Release, le parcours natif et les captures ne sont pas encore
qualifiés à ce stade du rapport. Les preuves de compilation/CPU ne suffisent pas pour cocher R2-005.

## Parcours de qualification à exécuter

1. Ouvrir le snapshot natif r7 du lot modules/zones, sans le modifier.
2. Ouvrir deux zones aux graines distinctes, vérifier leurs terrains et identités/révisions.
3. Déplacer la caméra A, passer en B puis revenir en A : caméra retrouvée, un seul renderer.
4. Masquer/réouvrir et redimensionner Monde ; conserver canvas, onglets et saisies de Projets.
5. Fermer les onglets puis le projet : aperçu vidé, aucune suppression de source/fichier.
6. Vérifier les erreurs, changements rapides et changements de sources/undo ; aucune réponse périmée
   ne doit être présentée comme la zone courante. Smoke des routes de preuve indépendantes.
7. Capturer le rendu réel et relier artefacts, code et hashes avant un éventuel PASS R2-005.

R2-006 reste partielle (sélection source, pas de picking d'objets), brosses et routes générales R2-F
restent ouvertes. Aucun passage à Gate 3 n'est autorisé par ce lot.
