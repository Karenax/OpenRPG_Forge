# R1 — Raccord de l'aperçu forêt au Studio Web

Date : 2026-09-06. Gate 1 ouverte ; ADR-0010, activation expérimentale uniquement.

## Implémenté

`forge_kit=governed&forge_forest=preview` charge le catalogue local `forge-kit/prototype-catalog.json`
avec le transport borné existant. Le constructeur partagé valide révision du kit, hash du document
de végétation et bindings ; les GLB passent le préflight/hachage existant avant installation.
Le plugin utilise la même source mémoire `forge-kit://`, sans duplication de téléchargement,
puis le projecteur natif partagé et le même scatter. Le mode kit manuel seul reste inchangé.

L'état visible distingue chargement refusé, erreur de projection, préparation et compteurs
projetés non qualifiés. Aucun « prêt » du kit manuel n'est pris pour une validation de la forêt.
Le parseur indépendant refuse répétitions, valeurs inconnues et demande de forêt sans kit.

## Réellement vérifié

- `cargo check -p forge_studio --no-default-features --features web --target wasm32-unknown-unknown --locked` : PASS.
- Clippy Studio WASM strict, puis workspace/all-targets/all-features strict : PASS.
- `cargo test --workspace --locked` : **433 PASS**, un probe timing ignoré.
- Le nouveau test couvre modes absent/manuel/forêt et sept requêtes invalides.
- Avertissement Cargo préexistant d'incompatibilité future `proc-macro-error2 2.0.1` encore présent.

Logs : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-forest-wiring-20260906/`.
`tests.log` SHA-256 `e5940a263a38e6bfe8fb16796260e91ec7bb0f311f6aacd6d499d1213eba5fe3`,
`clippy.log`, `clippy-wasm-final.log`. Le refus Clippy initial est conservé et corrigé sans exception.

## Prévu, non vérifié dans ce lot

Build Web complet frais, déploiement du catalogue candidat, lancement navigateur réel et capture.
Le bundle Web antérieur ne constitue pas une preuve de ce raccord. Ni transport réel du catalogue,
ni shader GPU, ni parité visuelle/animation/LOD natif-Web, ni Tauri ne sont qualifiés par ces checks.
Les tests de catalogue, renderer et reprise existants restent des preuves de leur périmètre.
Pas de promotion d'asset, de publication Git ou de passage Gate 2 ; CI distante NOT RUN.

## Complément — build complet et chargement navigateur

Le build Trunk dev complet a ensuite terminé avec sortie 0 (compilation Cargo 48,84 s).
Sortie isolée `web-dist/` dans le même dossier de preuve, sans écraser le déploiement antérieur.
WASM : 148 103 271 octets, SHA-256
`b1f503855c2ed29aa62409e114680bc8ff28f1bac5867d10e3e25eb3ef9016bf`.
Ce poids dev n'est ni un budget release ni une mesure de mémoire utilisée.

Déploiement local des fichiers du candidat `r1-foliage-coherent-runtime-20260906-b`, kit révision 2,
sans modification : build/preview, six GLB et catalogue. Catalogue déployé SHA-256
`7a2c04bb4fa1e53a5958154f60a81dcf6d4069e58ce1ef5a41adf2e807ce2b03`.
Serveur loopback `127.0.0.1:1437`, URL avec
`forge_scene=world-sketch-visual-valley&forge_kit=governed&forge_forest=preview`.

Lecture réelle de l'onglet Chrome par le contrôle navigateur :

- `Kit : 20/20 instances · 36 colliders · prêt` ;
- `Forêt expérimentale : 636 arbres · 7632 primitives · non qualifié` ;
- monde initialisé une seule fois, hashes alignés, une caméra attendue/observée ;
- canvas CSS/rendu observé à 2048×1157, High, eau/environnement partiels ;
- l'UI conserve « présentation GPU non observée » et « collecte 0/120 ».

Le chargement HTTP réel du catalogue et la projection ECS Web sont donc observés. Ce relevé
textuel ne constitue pas une capture GPU, un benchmark, une inspection des shaders ou une
qualification des gestes humains. La capture, les transitions LOD/vent et la comparaison visuelle
restent ouvertes. Les compteurs seuls ne ferment aucun critère artistique de Gate 1.

## Inspection visuelle Web et cycle High → Low → High

Trois captures réelles de l'onglet Chrome ont ensuite été affichées et inspectées dans la
conversation : High initial, Low et High restauré. Elles montrent effectivement terrain,
arbres feuillus avec ombres, végétation basse et capsule joueur. Ce sont des captures navigateur
inline, pas des fichiers PNG normalisés archivés ; aucun hash de capture n'est revendiqué.
Le canvas observé reste 2048×1157, donc pas une comparaison normalisée 1080p avec le natif.

Les boutons de qualité ont été utilisés : Low réduit visiblement l'herbe et la couverture
lointaine ; High les rétablit. Le statut conserve 636 arbres/7 632 primitives, kit 20/20/36,
hashes alignés et une seule initialisation du monde. Le sélecteur de texte suivant le clic Low
a expiré ; le snapshot suivant a confirmé que le clic avait bien été appliqué, sans répétition.
Les lectures de logs navigateur demandées pour erreurs/avertissements ont retourné une liste
vide ; cela ne prouve pas l'absence de toute erreur historique non collectée.

**Verdict visuel agent : non qualifié artistiquement.** La forêt est rendue, mais les silhouettes
restent répétitives, les arbustes très géométriques et la surface verte trop uniforme. Ne pas
assimiler ce succès de raccord à l'objectif de végétation naturelle ou à une acceptation humaine.
Le HUD conserve volontairement « présentation GPU non observée » : il n'est pas un détecteur
automatique de la capture inspectée par l'agent.

Le relevé initial de 120 frames indique moyenne 58,35 ms et p95 40,10 ms : ni 60 FPS démontrés,
ni benchmark recevable (échantillon de démarrage, onglet non contrôlé en continu au premier plan,
résolution différente du protocole). Aucun gain de performance n'est inféré. Restent les captures
normalisées archivées, la comparaison native/Web et le parcours LOD/vent en mouvement.

## Rejeu Web commun — arbustes, fougères et litière

2026-09-06 : nouveau build Trunk dev complet, sortie 0, compilation Cargo 40,64 s.
Il intègre `R1_SHRUB_LEAF_CROWN`, `R1_FERN_FRONDS` et le complément litière de
`R1_TERRAIN_SURFACE_DETAIL`, avec le même kit candidat révision 2 et les mêmes GLB.
Sortie séparée : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-vegetation-ground-web-20260906/`.
WASM de 148 091 069 octets, SHA-256
`57b8eb5fa4fe34acec6f27865f6e1a1ba519f8d691d328d0eab3e702290175af`.
Ce poids dev ne qualifie pas la distribution release. Avertissement de compatibilité future
`proc-macro-error2 2.0.1` toujours présent dans `build.log`.

Lancement Chrome loopback port 1438 avec les options WorldSketch/kit/forêt précédentes.
Captures navigateur inline clair puis pluie réellement inspectées : arbustes ajourés,
fougères découpées et variations terreuses visibles ensemble. Après clic « Aperçu pluie »,
pluie et mouillure visibles ; observation CPU rain/humidité 0,59 lors du relevé, 0,54 lors de
la capture suivante (instants distincts). Kit 20/20 instances, 36 colliders, forêt 636 arbres/
7 632 primitives et une initialisation du monde conservés. Logs demandés erreur/warn : liste vide.
Onglet de preuve fermé après le contrôle ; aucun document authoré ni asset modifié par le parcours.

Ce rejeu lève l'absence de rendu Web frais pour ces trois corrections. Il ne qualifie pas une
parité pixel à pixel (canvas 2048×1157 contre natif 1920×1080), une animation LOD contrôlée,
la performance ou Tauri. Captures inline seulement, pas des PNG normalisés archivés. Aspect
encore stylisé/répétitif, aucune acceptation artistique ou humaine inférée. CI distante NOT RUN.
