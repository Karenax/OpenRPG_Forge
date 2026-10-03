# Retouche d'une plante importée — 28 septembre 2026

La population importée reste immuable. Une correction de zone version 10 lie
un décalage X/Y/Z à la couche et à la clé source stable de chaque plante.
La projection rééchantillonne le relief à la nouvelle position, conserve le
dégagement initial par rapport au sol, ainsi que l'identité, la rotation et
l'échelle de la plante. Les retouches restent dans l'archive de zone et passent
par Preview, Apply, Undo/Redo et Save/Open. Un décalage nul rétablit la source.

Le panneau Nature du Studio présente trois champs de décalage après sélection
GPU. Il prépare une commande de zone et contrôle avant l'aperçu les clés et
les limites de la carte pour toutes les plantes retouchées, même masquées.
L'aperçu textuel annonce explicitement que la vue garde la position acceptée
jusqu'à Apply. Les blocs source ne sont jamais réécrits. Une régénération qui
remplace ces blocs est refusée tant que des décalages y font référence.

Vérifications locales :

- `cargo test -p forge_world --test imported_population --locked --offline -j 1` :
  cinq tests PASS, dont archive/réouverture, terrain relevé, Undo/Redo,
  restauration source, identité stable, refus de clé absente et de sortie de carte ;
- `cargo check -p forge_studio --lib --no-default-features --features web
  --target wasm32-unknown-unknown --locked --offline -j 1` : PASS ;
- Clippy projet/monde et Studio WASM : PASS avec les exemptions ciblées
  `needless_pass_by_value` et `too_many_lines` déjà présentes dans l'arbre ;
- `scripts/Invoke-StudioTrunk.ps1 -Action build` : PASS ;
  `forge_studio_bg.wasm` : 48 965 683 octets, SHA-256
  `01a17efddef2d1fb380f38382f82806fee3700869184fb94e60834034c43d463` ;
- `rustfmt --check` sur les quatre fichiers Rust de cette tranche et
  `git diff --check` sur les fichiers suivis touchés : PASS. Le `cargo fmt`
  global reste indisponible tant qu'un autre probe référence le module absent
  `tidewater_plant_probe/terrain_ao.rs`.

La sélection et le déplacement interactifs dans Tauri/WebGPU ne sont pas
qualifiés par ces tests. Le lancement du serveur local Studio pour l'essai GUI
a été refusé par l'auto-review (`blocked by policy`) ; aucun autre serveur n'a
été lancé pour contourner ce refus. Une pose par copie a depuis été ajoutée
([preuve dédiée](2026-09-28-authored-plant-copy.md)) ; le catalogue de pose,
la rotation/l'échelle indépendantes, la manipulation directe dans la vue et la
comparaison visuelle avec la référence Tidewater restent ouverts.
