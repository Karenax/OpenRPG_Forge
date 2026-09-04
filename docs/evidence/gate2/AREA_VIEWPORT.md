# Gate 2 — Onglets et viewport du projet

Date : 4 septembre 2026. Branche : `Rust`.

## État de qualification

**R2-005 PASS — onglets de zones qualifiés dans le Studio autonome.** Gate 2 est à **5/43**.
Code fonctionnel `d22b68c24c85f0aa44b651c8e04e8b02d1690fba`, finition CSS et acquisition
`e99b2ffc676d090fcaf8e5dfc06ef523e78261fd`, arbre `8e6ed47fda2c8916fa6c406c30510e1a1b482fed`.
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

- Trunk Release puis Studio autonome `tauri/custom-protocol` Release PASS à `e99b2ff`.
  Les tests et Clippy portent sur le même Rust à `d22b68c` ; seul `desktop.css` change ensuite.
- `cargo fmt --check` et diff-check PASS au candidat final. Frontière Cadle avec checkout épinglé PASS.
- Essai Windows réel : 1600 × 900 de contenu, capture extérieure 1602 × 932. Une première capture
  à `d22b68c` a révélé des boutons Monde non harmonisés ; corrigés puis reconstruits et recapturés.
  Inspection d'agent, pas un reçu de validation humaine.

## Parcours natif exécuté

1. Ouverture du snapshot r7 du lot modules/zones. Deux modules / trois zones, document enregistré.
2. Clairière du bureau (A, seed 1) puis Lisière profonde (B, seed u64::MAX) : terrains visibles,
   64 chunks, bonne identité et révision 7. Réouvrir B ne crée pas de doublon.
3. Orbite et zoom A ; B ; retour A ; réduction/restauration de Monde puis redimensionnement :
   caméra A retrouvée exactement (distance 596.1264038085938, pitch 0.7899999618530273,
   yaw 4.777787208557129, cible [0,0,0]), hash A identique, `renderer_starts = 1`.
4. Fermeture de l'onglet A actif : B devient actif, A reste dans les sources. Fermeture de B :
   état vide, les trois zones restent proposées. Réouverture B puis Voir le départ : cube visible,
   caméra ciblée sur le départ [12,2,0], sans ajout de personnage ni mutation.
5. Fichier → Fermer le projet enregistré : onglets réconciliés, `area_id = null`, zéro chunk,
   hash vide et compteur de démarrage toujours à 1. Sortie normale du Studio vérifiée.
6. Snapshot r7 SHA-256 avant/après identique :
   `297035827452c15186b9f17840784d575b98f660ddcfb8984102d10c8de61ae4`.
   Aucun enregistrement ni édition des sources pendant ce parcours.

Les captures et ledgers sont locaux sous `artifacts/gate-2/area-viewport-2026-09-04/` :
`01` terrain A, `02` caméra A, `03` ledger A, `04/05` terrain/ledger B, `06` A restauré,
`07` redimensionnement, `08` ledger restauré, `09` onglets fermés, `10` cadrage départ,
`11` menu Fichier et sources r7, `12/13` ledger/capture après fermeture du projet.
`00-native-d22-before-style.png` conserve l'état avant correction CSS, pas le candidat livré.
Tailles et SHA-256 des captures, ledgers et logs : [inventaire](AREA_VIEWPORT_ARTIFACTS.json).

| Projection | ID de zone | Hash source terrain |
| --- | --- | --- |
| A | `5426f526-6ac5-508b-98b3-da80d3df7116` | `ce24227a9c14cc32d0d325a4b7a9b4580064259c81b90a4fbe7db3996e73f3b6` |
| B | `580d7438-73f1-560a-aeda-015967e7540f` | `0a411874b1ef6d864fa84d204eee72b6c5f32e2d353bc34083b4889a335bdeb6` |

### Artefacts Release — SHA-256

- `target/release/forge-studio.exe` : `40682af4cadcbee1b90a09c4f2680ca826b724d512088a32d4441ee99c7ff5d6`.
- `apps/forge_studio/dist/forge_studio_bg.wasm` : `087ac39325f5f9bf3a438b0eeb7cbc10605ffe13c9561ce2e73d5d055c878cab`.
- `apps/forge_studio/dist/desktop.css` : `c12491b1a50ddf99937e4557fb391ce3320c15b7f8d134b2486d058b0226b552`.

## Couverture et limites

Le navigateur sans backend conserve le formulaire saisi après ouverture/masquage/réouverture de
Monde ; Diagnostics reste à zéro renderer et les mutations projet restent désactivées. Aucun projet
fictif n'est substitué. Ce contrôle ne remplace pas le parcours natif ci-dessus.

Smoke séparés dans le navigateur intégré, après fermeture du Studio : `?forge_scene=gate0`
affiche sol/cube et WebGPU prêt ; `?forge_scene=visual-valley` affiche la vallée High, végétation,
personnage de preuve, décor activé, huit hashes alignés et une caméra. Un démarrage chacun,
captures inspectées en session. Ce n'est pas un rejeu des matrices de qualification Gate 0/1,
ni une validation Tauri/CPU/GPU supplémentaire. Les onglets d'essai sont ensuite fermés.

Les identités étrangères/absentes, révisions périmées, topologies invalides, réponse obsolète,
réconciliation des onglets et libération des meshes sont couvertes par les tests Rust. Le scénario
UI natif d'édition/undo pendant une génération, les rafales d'interactions et le déplacement Maj-glisser
ne sont pas qualifiés par cette acquisition ; à reprendre dans la tranche d'édition suivante.
Le temps de boucle affiché est diagnostique, pas un benchmark de cadence ni un timestamp GPU.
Le terrain source initial reste peu accidenté, sans biomes, végétation, rivière ou scène Valley copiée.
Pas de watchdog d'échec d'initialisation GPU ni de caméra persistée dans ce lot.

R2-006 reste partielle (sélection source, pas de picking d'objets), brosses et routes générales R2-F
restent ouvertes. Aucun passage à Gate 3 n'est autorisé par ce lot.
