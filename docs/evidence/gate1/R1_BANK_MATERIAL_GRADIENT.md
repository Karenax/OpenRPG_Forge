# Gate 1 — Transition de couleur des berges

2026-09-05, local Rust, aucun passage de gate ni publication.

## Changement

Le matériau brun uniforme des berges est remplacé par une base blanche opaque et des couleurs
de sommets en espace linéaire : terre sombre côté eau, vert terreux côté terrain, variation bornée
et déterministe liée à la position XZ. Les 1 384 sommets du maillage existant portent chacun une
couleur RGBA (22 144 octets de données additionnelles par copie de cet attribut).
Pas de nouvelle texture, dépendance ou asset externe ; aucun nouveau mesh ni entité de rendu.
Les positions, indices, UV, normales, tracé, terrain et collisions ne sont pas modifiés par cette édition.

Le statut de télémétrie devient `procedural_vertex_wet_to_dry_gradient_no_textures`.
Il ne désigne pas un matériau PBR authoré, une humidité dynamique ou une application du paramètre
`wet_margin_m` : ces capacités restent distinctes et non revendiquées.

## Résultat réellement inspecté

Capture native Full HD sur RX 7800 XT, caméra héroïque et kit corrigé, WorldSketch révision 2
et pont recompilé de `r1-river-valley-20260905/promoted/`.
Racine : `E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-bank-gradient-20260905`.

Options : `--world-sketch-visual-valley`, kit corrigé et aperçu authoré actuel, pont ci-dessus,
capture frame 300, warm-up 180, sortie frame 420.
Run terminé le **18:21:44.010712 UTC**, code 0, `runtime_errors_observed=false`.
Les événements sont conservés dans la conversation, pas dans un fichier JSONL de ce lot.

`hero.png` a été ouverte : le bord externe s'intègre mieux au terrain que la précédente bande
brune. La transition reste régulière et des bandes dues à l'échantillonnage restent
perceptibles ; aucune texture fine, érosion détaillée ou finition artistique n'est revendiquée.
La revue humaine de Gate 1 reste ouverte.

240 frames : moyenne 111,09 FPS, p95 9,5721 ms, 1 % low 47,06 FPS, maximum 41,2809 ms.
Smoke court avec capture, pas un benchmark comparatif ni une preuve de gain de performance.
Les métriques conservent le terrain `c9000f6ab51b7d716511dd718924f5cd6403c749456620bef86bb2120f364930`.

## Vérifications

Un nouveau test couvre déterminisme, nombre de couleurs, RGBA borné/fini, opacité et transition
vert/terre des deux côtés. Le test du codage de profondeur d'eau conserve ses contrôles ; sa
précédente assertion d'absence de couleur sur les berges est remplacée par une assertion de
couverture de tous leurs sommets. Deux remarques Clippy sur le nouveau test ont été corrigées
(`as_chunks`, comparaison des bits flottants), sans désactivation de règle.

- Test ciblé initial : PASS.
- Suite moteur : **102/102 PASS** ; Clippy moteur toutes cibles/features strict : PASS.
- Formatage et `git diff --check` : PASS.
- `cargo check -p forge_engine --target wasm32-unknown-unknown --locked` : PASS.
- Build runtime et lancement/capture natifs : PASS.
- Pas de nouvelle observation Chrome pour cette seule modification de couleur ; le check WASM
  n'est pas présenté comme une preuve de rendu navigateur.

## SHA-256 octets

- `hero.png` : `f5d9389d13bb014969e61824eb6bb3652c2ac9017e2de4edd5b9ca59012759ce`.
- `metrics.json` : `f9e107ffdfe8813194acbaf1e3185f3ce227920126711003ee38e1d03baea469`.
- Binaire natif : `91f41475eacb9be27452c5fd472805162b2aa0bbc5960e4c83a5a1f907f149af`.

Le franchissement reste étayé par le probe de la révision 2 précédente : ni terrain ni géométrie
du pont ne changent ici. Les preuves d'ensemble de Gate 1 ne sont pas remplacées par ce seul lot.
