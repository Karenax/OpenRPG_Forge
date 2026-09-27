# Gate 1 — Exclusions WorldSketch appliquées au scatter runtime

Date : 2026-09-05. Branche `Rust`, workspace modifié préservé, aucune publication.

## Correction implémentée

Le cache de carving contenait une exclusion végétale, mais le compilateur retournait encore la
recette de végétation historique sans les splines WorldSketch. Blender consommait le masque ;
le runtime générait donc encore des arbres dans l'eau et sur les chemins.

Le contrat `forge.vegetation` v1 accepte maintenant `polyline_corridor`, composé d'un ID stable,
de 2 à 2 048 points XZ et d'une demi-largeur positive bornée. Les points doivent rester dans la
zone ; l'empreinte peut dépasser ses bords car la génération est elle-même bornée. Les anciens
cercles et segments restent inchangés, avec leurs hashes historiques.

Le compilateur ajoute trois corridors depuis les tracés sculptés route/rivière/ruisseau. Leur
largeur reprend le noyau totalement exclu du masque existant : demi-largeur authorée + 55 % de
la berge. Les IDs sont déterministes, compatibles avec le contrat ASCII, indépendants de l'ordre.
Le filtre s'applique avant terrain/espacement, sans entité supplémentaire ni travail par frame.
Les deux exclusions historiques sont conservées. Le bord adouci DCC n'est pas encore une densité
runtime progressive. Les placements manuels gouvernés ne sont pas supprimés automatiquement.

## Vérifications exécutées

Variables Cargo : target `C:\Users\karen\AppData\Local\Temp\openrpg-forge-r1-064-target`,
`CARGO_INCREMENTAL=0`, `RUSTFLAGS=-C debuginfo=0`.

- `cargo test --workspace --locked -q` : **297 PASS**, code 0 : Assets 14, CLI 7, Core 11,
  Engine 97, Project 45, Runtime 42, Studio 21, World 60.
- `cargo clippy --workspace --all-targets --all-features --locked -- -D warnings` : PASS.
- `cargo check -p forge_world --target wasm32-unknown-unknown --locked` : PASS ; pas un lancement WebGPU.
- Formatage et `git diff --check` : PASS.
- Nouveau test sur la source WorldSketch courante : aucun placement généré dans une exclusion,
  replay identique, plus de 1 000 instances communes conservées, **toutes** les instances communes
  exactement identiques (IDs, poses et autres propriétés), round-trip du document et doublon rejeté.
- Test de contrat : empreinte latérale en bord de carte acceptée ; huit cas invalides rejetés
  (nombre de points, doublon signé zéro, NaN, point hors zone, largeur nulle/infinie/hors budget).

Les premiers replays ont refusé les tirets dans les IDs de végétation ; la normalisation de l'UUID
déterministe en underscores corrige le défaut sans assouplir la validation. Le premier appel CLI
de preuve a refusé le parent de sortie absent ; le parent a été créé puis le CLI a produit un
nouveau dossier, sans écraser de preuve.

## Preuve native

Dossier : `E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-scatter-corridors-20260905`.
Le CLI a recompilé le candidat externe
`r1-river-profile-20260905/world-sketch-candidate.v1.json` dans `compiled/`, avec vérification des
images sous le workspace Rust. Cette preuve utilise le candidat de creusement non promu du rapport
`R1_WORLDSKETCH_WATER_ALIGNMENT.md` ; la source verrouillée par défaut reste inchangée.

Même commande native que le candidat précédent : WorldSketch explicite, même kit Blender corrigé,
même aperçu candidat, pont de `compiled/bridge-placements.v1.json`, capture frame 300, warm-up 180,
sortie frame 420. Seules les sorties de capture/métriques utilisent ce nouveau dossier.

Run du 12:33:27 UTC : code 0, RX 7800 XT/Vulkan/1920 × 1080. Capture inspectée : les arbres
générés visibles dans le cours d'eau ont disparu. **7 275 placements contre 8 026 avant**, dont
627 arbres, 1 023 arbustes, 1 163 fougères et 4 462 plaques d'herbe ; 40 960 tentatives conservées.
Le kit charge toujours **20/20 scènes et 36/36 colliders**. Le terrain, l'eau, le pont et les sources
Blender ne changent pas. La preuve de franchissement précédente garde ces mêmes entrées physiques ;
elle n'a pas été rejouée pour ce changement de scatter visuel.

240 frames : moyenne 141,04 FPS, p95 7,7254 ms, 1 % low 52,78 FPS, un hitch de 36,835 ms.
Smoke court avec capture/VSync demandé, pas un benchmark ni une qualification d'optimisation.
Les avertissements Vulkan AMD et shader `@invariant` restent visibles.

Hashes :

- végétation canonique : `503af9393acc2d5d78e04351a20c540083e11f860abe8d99f1914a7d06273909` ;
- terrain canonique inchangé : `653f14d117a29f628604be76209b13ac572df0c8480d425d690ba55308e88b02` ;
- binaire : `9781db4280bd338662577943885f117d2e6bf7f34d11385f27814ba351347b42` ;
- capture : `c950fb7d2e4e397c24803fca9c8da15e5ff70a46095780d776af354aceb289fe` ;
- métriques : `12b3d674f0a8bcde4e537b0b664ef98d7205deb88f9b837a23b10eb6f1ce16bb`.

## Limites et suite

Le canyon profond, les berges très sombres, la palette jaune/rose, les silhouettes de végétation
et leur transition distante restent non qualifiés artistiquement. Le creusement à 40 m attend
toujours l'accord sur la feature verrouillée. Ni édition Studio, ni revue humaine, ni parité
WebGPU ne sont prouvées par ce lot. Gate 1 reste ouverte ; CI distante `NOT RUN` sous ADR-0006.
