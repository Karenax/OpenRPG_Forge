# Copie et pose d'une plante Tidewater — 28 septembre 2026

Le document de zone v11 conserve des copies d'auteur sous des IDs propres,
triés et uniques. Chaque copie référence une couche et une clé du bloc végétal
immuable. Elle réutilise le prototype et ses paramètres, avec un décalage X/Y/Z
propre. Une correction v12 ajoute une rotation Y et une échelle uniforme propres
à chaque copie ; les copies v11 héritent toujours de la rotation et de l'échelle
source, avec les mêmes champs sérialisés. La projection suit
le relief de la destination et garde le dégagement initial au sol. Une copie
peut être déplacée ou retirée, avec Preview, Apply, Undo/Redo et Save/Open.
Le remplacement des blocs source lors d'une régénération est refusé tant
que leurs clés servent à une retouche ou à une copie.

Le panneau Nature utilise la sélection GPU existante : entrer un décalage
différent, examiner la copie, appliquer, puis sauvegarder. Une copie peut ensuite
être sélectionnée pour modifier sa position, sa rotation et sa taille, ou la retirer. Le Studio contrôle
les clés, la collision d'ID avec la base et les limites de carte avant l'aperçu.
Les blocs source et le paquet d'assets ne sont jamais réécrits.

Validation locale :

- Suite `imported_population` : 5/5 PASS. Elle couvre une copie sur archive
  réouverte, déplacement et Undo, retrait, suivi du relief, ID conservé, refus
  de clé absente ou de sortie de carte et conflit de régénération. Elle couvre
  aussi le passage v11→v12, rotation/échelle persistantes, Undo et refus des
  valeurs hors bornes ; l'archive v11 omet les nouveaux champs.
- `cargo test -p forge_project --locked --offline -j 1` : PASS sur la suite
  projet entière, y compris historique, migration et réouverture des zones.
- Probe `tidewater_population_scene_probe` sur le workspace réel
  `workspace-buildings-initial-001` et le paquet épinglé
  `1fec8a063214ad05ceac19f277dbd17e131e04d2714378bae9239efd12224f83` :
  PASS. Les 65 383 identités canoniques deviennent 65 384 avec une copie,
  acceptée par le chargeur de scène et sélectionnée près de sa position.
  Rotation de 90° et échelle ×1,5 sur cette copie : identités inchangées,
  flux de matrices GPU modifié, scène chargée et copie retrouvée.
  Les parcours A/B/A et le picking de la base restent PASS ; l'état source
  du workspace est inchangé.
- Compilation Studio WASM et Clippy Studio WASM : PASS. Clippy
  projet/monde/moteur/probe : PASS, avec l'exemption ciblée
  `needless_pass_by_value` déjà nécessaire au code de session existant ;
  l'exemption `too_many_lines` du Studio reste limitée au module UI antérieur.
  Clippy sur `imported_population` : PASS avec `too_many_lines` écarté pour
  les deux longs scénarios préexistants.
- `scripts/Invoke-StudioTrunk.ps1 -Action build` : PASS. Le bundle
  `forge_studio_bg.wasm` contient 49 032 515 octets, SHA-256
  `d0da29042697a1fd4852295be3ff362406e7f7f94629d271e66f6a06770f6035`.
- `rustfmt --check` sur les fichiers Rust touchés et `git diff --check` sur
  les fichiers suivis de cette tranche : PASS.

Ce probe prouve la projection ECS et la sélection dans cette scène, pas un
geste réalisé dans la fenêtre Tauri ni la parité visuelle avec la référence.
La pose directe par catalogue de couches importées est désormais décrite dans
[la preuve dédiée](2026-09-28-plant-catalogue.md). La manipulation 3D au
pointeur et le parcours GUI/WebGPU restent ouverts. Le lancement du serveur
Studio local pour ce parcours avait été refusé par l'auto-review
(`blocked by policy`) ; aucun contournement n'a été tenté.
