# R1-064 - Integration runtime du candidat d'assets gouverne

Date de preuve : 2026-09-05  
Statut : **integration native techniquement verifiee ; comparaison humaine ouverte ; R1-064 reste ouvert**

## Complément playtest natif — 2026-09-05, 19:43 UTC

### Disponibilité physique avant les entrées joueur — 20:12 UTC

Le playtest natif avec kit bloque maintenant les entrées clavier/souris tant que Bevy n'a
pas créé toutes les instances et que Rapier n'a pas enregistré tous les colliders configurés.
Un état dérivé `GovernedAssetKitReadiness` observe `WorldInstanceSpawner::instance_is_ready`
et les `RapierColliderHandle`, en plus des contrôles existants de dépendances/LOD/échecs.
Le verrou est appliqué avant `InputSystems`, puis l'échantillonnage joueur habituel ; une perte
de disponibilité referme le verrou. Pas de délai arbitraire ni de changement des sources.
La scène sans kit et les probes spécialisés conservent leur fonctionnement.

Preuve réelle : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-kit-playtest-readiness-20260905/`.
Même paire rapport/preview que ci-dessous, commande joueur avec métriques, 180 frames de chauffe,
sortie à 420 frames, sans capture ni nouvelle revue UI. Code 0, zéro ERROR. L'événement de fichiers
chargés arrive à `20:12:02.237851Z` avec seulement 9 racines de collision configurées ; le nouvel
événement `governed_asset_kit_playtest_ready` arrive à `20:12:02.375907Z` avec **20 instances et
36 colliders enregistrés**. Ce décalage confirme pourquoi le premier événement ne devait pas
autoriser les entrées. Journal validé automatiquement : un seul événement de disponibilité,
compteurs exacts attendus et absence d'ERROR.

107 tests moteur + 48 tests runtime passent, ainsi que Clippy workspace strict, Clippy Studio
WASM/Web strict, build, fmt et diff-check. Les tests vérifient instances partielles, collider
non enregistré, échec de collision, état vide et fermeture/ouverture/refermeture du verrou.
Le run borné neutralise par ailleurs les entrées OS : il prouve la disponibilité physique réelle,
pas une séquence manuelle de touches. Le comportement du verrou est couvert par tests.

Runtime SHA-256 : `4ad1099d5ee48ec080469faa836befa7f34baabbca60c516feaf760a8c0b9a67`.
Journal : `afcaaae0b9ebe0ea752ebff1bcf1869217d962d54322dec7db916941dee3d59c`.
Pas de qualification du kit dans Chrome ni de passage de gate.

Validation d'intégration suivante, sur le workspace Rust modifié préservé :
`cargo test --workspace --locked -q` termine avec code 0 et **324 tests réussis**
(assets 14, CLI 7, core 11, moteur 107, projet 45, runtime 48, Studio 25, monde 67).
Elle couvre ensemble le nouveau matériau de terrain, le départ/orientation joueur et la
disponibilité physique du kit. Aucun test ignoré dans cette exécution. Ce replay complète
les tests ciblés ; il ne remplace ni les preuves graphiques ni la qualification sur arbre
propre requise par ADR-0006 avant passage de gate. Aucun commit ou push n'a été effectué.

Le kit peut désormais être chargé avec `--player-camera`, sans sa caméra de comparaison fixe.
Le départ WorldSketch et l'orientation joueur sont conservés. Le probe du pont et son aperçu
possèdent également leur caméra sans ajout de celle du kit ; le probe caméra spécialisé reste
explicitement refusé. Les validations d'assets, hashes, chemins et collisions ne sont pas relâchées.
Le champ `camera_id` du kit reste une référence du preview, pas le nom de la caméra active quand
`validation_camera_spawned=false`.

Entrées utilisées, intactes avant/après : rapport
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-063-authored-preservation-20260905-b/rebuild-1/asset-kit-build.v1.json`
(SHA-256 `02566c6ec217084e76fab4d741e62e93db0737e48a5f4ae3e107b924bb877631`) et preview
`examples/visual_valley/asset-kits/r1-063/runtime-preview-authored.v1.json`
(`bde50c49ca0e29b9573914a3aa21b9d5e6f9ec34453c5d192145974a0739effa`).
Ce sont les entrées corrigées/préservées, liées au WorldSketch révision 3, pas le candidat historique ci-dessous.

Commande réellement exécutée :

```text
forge_runtime --world-sketch-visual-valley --asset-kit-build <rapport>
  --asset-kit-preview <preview> --player-camera
  --capture <preuve>/player.png --capture-frame 300
  --metrics <preuve>/metrics.json --metrics-warmup-frames 180 --exit-after-frames 420
```

Preuve externe : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-governed-kit-playtest-20260905/`.
Capture inspectée : capsule joueur, vallée et éléments détaillés du kit visibles ; la végétation
procédurale de prototype demeure. Code 0, zéro ERROR, 1920 × 1080 Vulkan/RX 7800 XT.
Métriques : 20/20 instances, 28 nœuds par LOD, 36/36 colliders, zéro échec,
caméras 1/1, `validation_camera_spawned=false`. 240 échantillons : 104,72 FPS moyens,
p95 10,1506 ms, 1 % low 46,03 FPS, un hitch de 40,2683 ms. Smoke court, pas benchmark qualifiant.

Le même exécutable et les mêmes entrées, avec `--bridge-crossing-probe` à la place des options
joueur/capture/métriques, réussissent : 42,5282 m, 518/518 ticks au sol, 435 contacts tablier,
bindings conservés, code 0 et zéro ERROR. Le journal confirme le chargement des dépendances du
kit avant la fin du parcours ; ses compteurs de projection dans l'événement de chargement précoce
ne sont pas les compteurs finaux 36/36 du run avec métriques.

| Artefact | SHA-256 |
| --- | --- |
| forge_runtime.exe | `18d640406943bf0393be4eb8df97d8288f63a9691c26785332f81fe726360087` |
| player.png | `41903cbd4d455738fbe6efa598107ae92d44c09ec455fe68576733844c9f9d2e` |
| metrics.json | `e00d32b16e73ef8e4eda99f30954703c0d0677d3212ea4bd588657e2336f677b` |

47 tests runtime, Clippy runtime/all-targets/all-features strict, build, fmt et diff-check passent.
Changement limité au câblage natif ; pas de nouveau build ou de qualification du kit dans Chrome.
La capture bornée neutralise les entrées OS pour la reproductibilité ; le probe injecte des commandes
dans le vrai contrôleur. Cela ne prouve pas un parcours manuel complet ni le contact joueur avec
chaque collider du kit. Pas d'import Studio, sauvegarde produit ou distribution revendiqués.
R1-063/064 et Gate 1 restent ouverts, sans publication Git ni asset Cadle.

## Reorientation apres relecture des autorites

### Prevu

Le cap produit reste un RPG cree avec l'IA, corrige manuellement avec precision, dont les retouches
survivent aux regenerations, puis sauvegardable et distribuable. Dans la gate active, R1-064 doit
integrer les sorties Blender dans Bevy et les comparer a la vue heroique sans anticiper R1-065 ni
les gates suivantes.

### Toujours pertinent

- le `WorldSketch` versionne et ses identites stables restent la source reconstructible du monde ;
- le kit original R1-063 reste un candidat technique utile pour prouver le chemin Blender vers Bevy ;
- les placements manuels sont separes des GLB, lisibles, versionnes et lies aux hashes exacts ;
- les GLB restent des caches externes regenerables et ne deviennent pas une nouvelle source dans le
  depot.

### A reorienter

- R1-063 ne peut pas etre ferme tant qu'une retopologie ou retouche Blender humaine serait perdue a
  la regeneration ;
- une revision de kit divergente doit echouer explicitement plutot que reappliquer silencieusement
  des placements a une autre geometrie ;
- l'integration automatique ne remplace ni la comparaison artistique humaine, ni la qualification
  performance longue, ni les preuves de sauvegarde et de distribution futures.

## Entrees liees

- rapport Blender externe :
  `E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-063-asset-kit-pbr-run3-20260901-164902\asset-kit-build.v1.json` ;
- kit : `visual-valley-r1-063`, revision `3` ;
- manifeste Blender :
  `c75ebf0183f6a30af6ed9c5f3190079102d76f7bf90887b201358027c8fcb920` ;
- WorldSketch :
  `46f1d766a9dc35af75d172a7d1700af95dda397ba956ceba0e23318df49f3839` ;
- terrain sculpte :
  `f47294def69f86f2d3c7e1fd52c522cab0b1a595e6e292bb7af724742e4ddb16` ;
- preview versionne :
  `examples/visual_valley/asset-kits/r1-063/runtime-preview.v1.json` ;
- camera : `hero_reference_match` ;
- composition : 6 assets declares et 20 placements explicites.

Le preview ne contient aucun mesh, texture ou asset Cadle. La racine d'assets Bevy est le parent
canonique du rapport externe.

## Implemente

1. `forge_assets` expose les contrats v1 du rapport et du preview, avec schemas, identite/revision,
   provenance, licence interne, manifeste, liste exacte d'assets et placements bornes.
2. Le preflight runtime refuse les chemins absolus ou traversants, canonicalise chaque GLB, impose
   son confinement sous la racine externe et compare son SHA-256 aux octets du rapport.
3. `--world-sketch-visual-valley` compile le WorldSketch embarque vers le terrain sculpte et sa
   vegetation, puis charge environnement, eau et meteo existants.
4. `--asset-kit-build` et `--asset-kit-preview` forment une paire obligatoire reservee a ce mode.
5. Bevy projette les 20 placements sur la surface exacte du terrain, instancie les scenes GLB
   externes, applique les plages LOD aux noeuds glTF gouvernes et retire les ombres des primitives
   LOD1/LOD2.
6. Les noeuds `UCX_*` sont caches visuellement et leurs primitives produisent des enveloppes convexes
   Rapier. Le comptage porte sur l'etat ECS courant, pas sur les transitions de chargement.
7. La camera fixe du WorldSketch remplace la camera overview. Son ordonnancement precede la projection
   d'environnement, ce qui conserve HDR, atmosphere, exposition, tonemapping et brouillard.
8. Le schema `forge.runtime-metrics` v7 inclut les compteurs du kit et refuse la preuve si une scene,
   un LOD ou un collider configure manque.

## Reellement verifie

### Validation de code

| Controle | Resultat |
|---|---:|
| `cargo test -p forge_assets` | 12/12 PASS |
| `cargo clippy -p forge_assets --all-targets -- -D warnings` | PASS |
| `cargo test -p forge_engine --lib --all-features` | 96/96 PASS |
| `cargo clippy -p forge_engine --lib --all-features -- -D warnings` | PASS |
| `cargo test -p forge_runtime --all-features` | 39/39 + 1/1 logs PASS |
| `cargo clippy -p forge_runtime --all-targets --all-features -- -D warnings` | PASS |
| formatage cible et `git diff --check` | PASS |

Le volume `E:` etait sature pendant la validation. Aucun fichier n'a ete supprime : les builds ont
ete rejoues avec `CARGO_TARGET_DIR=C:\Users\karen\AppData\Local\Temp\openrpg-forge-r1-064-target`,
`CARGO_INCREMENTAL=0` et `RUSTFLAGS=-C debuginfo=0`.

### Run natif final borne

Commande fonctionnelle :

```powershell
cargo run -p forge_runtime -- `
  --world-sketch-visual-valley `
  --asset-kit-build E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-063-asset-kit-pbr-run3-20260901-164902\asset-kit-build.v1.json `
  --asset-kit-preview E:\OpenRPG_Forge\OpenRPG_Forge-Rust\examples\visual_valley\asset-kits\r1-063\runtime-preview.v1.json `
  --capture C:\Users\karen\AppData\Local\Temp\openrpg-forge-r1-064-proof-run4-20260905\hero-runtime.png `
  --capture-frame 300 `
  --metrics C:\Users\karen\AppData\Local\Temp\openrpg-forge-r1-064-proof-run4-20260905\metrics.v7.json `
  --metrics-warmup-frames 180 `
  --exit-after-frames 420
```

Résultat : code `0`, AMD Radeon RX 7800 XT, Vulkan, 1920 x 1080.

| Observation runtime | Resultat |
|---|---:|
| scenes demandees / chargees / echouees | 20 / 20 / 0 |
| noeuds LOD0 / LOD1 / LOD2 | 28 / 28 / 28 |
| noeuds collision / colliders Rapier / echecs | 36 / 36 / 0 |
| environnement | `observed_approximation` |
| HDR / atmosphere / brouillard / disque solaire | presents |
| echantillons | 240 |
| frame moyenne | 8,159 ms |
| p95 | 9,461 ms |
| 1 % low | 41,91 FPS |
| hitches > 16,7 ms / > 33,3 ms | 1 / 1 |

Artefacts externes finaux :

- capture : 2 158 542 octets, SHA-256
  `00756a28949dcc21d1e2d948bf994d2813a91062d481c3bb932ebaeb24c858f4` ;
- metriques v7 : 20 194 octets, SHA-256
  `1d3356b392eb5ff7a229bdbd19a50a4a109cbc617d3ba89097f8f9f34403be2c`.

Les avertissements observes concernent une couche Vulkan AMD dupliquee dans le registre et
`@invariant` dans un shader Bevy/wgpu ; ils n'ont produit aucun echec de chargement ou de sortie.

Les chiffres de frame sont une observation courte avec VSync demande, pas une qualification de la
condition performance Gate 1. Le 1 % low reste sous 45 FPS et un hitch > 33,3 ms est present : aucune
case performance n'est cochee.

## Inspection visuelle et limites ouvertes

Une seule boucle corrective a ete retenue : la premiere capture montrait que la camera etait creee
apres le systeme d'environnement. L'ordonnancement a ete corrige et la capture finale contient bien
HDR, atmosphere et brouillard. Son rendu reste clairement prototypal : vegetation procedurale tres
lisible, palette et contraste non acceptes, composition heroique non comparee humainement a la
reference.

La troisieme reconstruction Blender du candidat initial a depuis ete verifiee le 2026-09-05
(voir R1-063).

Restent donc ouverts :

- preservation gouvernee des retouches Blender humaines au travers d'une regeneration ;
- revue humaine retopologie, UV/PBR, pivots, echelles, collisions et transitions LOD ;
- comparaison humaine explicite de la capture runtime avec la vue heroique ;
- R1-065 : douze captures normalisees, video, revue artistique et correction ;
- qualification longue des performances, sauvegarde/reouverture du projet et distribution.

## Verdict borne

Le chemin technique **WorldSketch -> terrain sculpte -> rapport Blender externe hashe -> Bevy ->
Rapier** fonctionne localement. Il reste compatible avec le cap produit parce que les placements
manuels sont des donnees stables separees des caches GLB. Il ne resout pas encore la preservation des
retouches de geometrie Blender.

`R1-063` et `R1-064` restent decoches. Gate 1 reste **OUVERTE**. Aucune publication Git n'a ete faite.

## Complement : source Blender corrigee chargee dans le runtime (2026-09-05)

Le contrat Rust accepte maintenant `procedural_original_with_authored_edits` uniquement avec une
ligne de source complete : hashes du descripteur, du Blend et de la recette, IDs corriges uniques
et connus, revue humaine `pending`. L'apercu doit epingler le meme descripteur via
`authored_source_sha256`. Le chargeur conserve le confinement et le SHA-256 de chaque GLB ; les
fichiers Blender sont des sources de reconstruction, pas des dependances du jeu.

Validation executee : `cargo test --workspace`, code 0, **291 tests PASS** (Assets 14, CLI 7,
Core 11, Engine 96, Project 44, Runtime 40 + 1 journalisation, Studio 21, World 57).
Les nouveaux tests couvrent round-trip, source absente/perimee, IDs inconnus/dupliques,
ancien apercu refuse, chargement sans fichier Blender et GLB modifie refuse. Clippy strict cible
`forge_assets` et formatage cible passent egalement. Aucun critere humain n'est converti en test.

Controles finaux du meme code : `cargo clippy --workspace --all-targets --all-features -- -D warnings`,
`cargo check -p forge_assets --target wasm32-unknown-unknown`, `cargo fmt --check` et
`git diff --check` : **PASS**, codes 0. Le check WASM porte sur le contrat modifie ; il ne prouve
pas un lancement WebGPU du kit. Les commandes Cargo utilisent le target C: et les variables
`CARGO_INCREMENTAL=0`, `RUSTFLAGS=-C debuginfo=0` deja documentes plus haut.

Run natif execute avec le binaire produit par ces tests :

```powershell
& 'C:\Users\karen\AppData\Local\Temp\openrpg-forge-r1-064-target\debug\forge_runtime.exe' `
  --world-sketch-visual-valley `
  --asset-kit-build E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-063-authored-preservation-20260905-b\rebuild-1\asset-kit-build.v1.json `
  --asset-kit-preview examples/visual_valley/asset-kits/r1-063/runtime-preview-authored.v1.json `
  --capture E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-064-authored-runtime-20260905\hero.png `
  --capture-frame 300 `
  --metrics E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-064-authored-runtime-20260905\metrics.json `
  --metrics-warmup-frames 180 --exit-after-frames 420
```

Code 0, RX 7800 XT/Vulkan/Full HD : **20/20 scenes**, **28/28/28 noeuds LOD**, **36/36 colliders**,
zero echec de scene ou collision. Capture inspectee : composition presente, mais vegetation
prototypale, palette jaune/rose et zone de riviere noire restent non qualifies artistiquement.
Les avertissements Vulkan de registre/couche AMD et shader `@invariant` restent visibles.
Clippy compilait en parallele : cette observation courte ne qualifie aucune performance.

Le meme binaire avec l'ancien `runtime-preview.v1.json` et `--exit-after-frames 1` refuse le
demarrage en phase `asset_kit_load`, code **2**, avant ouverture du renderer : descripteur non epingle.
Les valeurs des 20 placements et de la recette sont identiques entre les deux apercus.

SHA-256 :

- executable : `a091c2ea3e8f1e5851686dd2bb634a7f2757c4a01c7d545436178fdc4f8f1407` ;
- rapport Blender : `02566c6ec217084e76fab4d741e62e93db0737e48a5f4ae3e107b924bb877631` ;
- apercu authoré : `ce22675d96592aff741f884370e5dd1e5e72fe58377378609d0893697b944918` ;
- capture : `32e00ab46c337f4bbf4f936281bd8dfdb3e6445284c09098670d2a763c838780` ;
- metriques : `744a7faeccf3ddc486271fdab08ed2adc0a24dee3870363a00a00ce94023aa1b`.

Le chemin worker -> GLB corrige -> runtime est techniquement observe. La retouche utilisee reste
synthetique ; la promotion et l'edition Studio, le rebase de conflits, la retopologie humaine,
la qualification WebGPU et la revue artistique restent ouverts. Pas de passage de gate ni de push.

## Contrat de matérialisation des collisions — 7 septembre 2026

Deux tests ECS synthétiques ajoutés au module partagé `governed_asset_kit`, sans modification
du code de production, du kit, des cartes ou de leurs placements :

- le handle de mesh réservé mais non chargé ne produit ni collider ni échec prématuré ;
- une fois les sommets disponibles, l'adaptateur construit le convex hull Rapier ; les
  sommets source restent identiques et une update supplémentaire ne crée pas de doublon ;
- moins de quatre sommets, coordonnées NaN et sommets tous confondus sont refusés ;
- une primitive marquée en échec ne devient pas silencieusement valide après remplacement
  de ses données : une reprise explicite/reconstruction reste nécessaire.

`cargo test -p forge_engine --all-features governed_asset_kit::tests` : **7 PASS**, sortie 0,
dont ces deux nouveaux tests et les cinq tests précédents de liaison/provenance/readiness.
Cette preuve porte sur la création du composant collider et les refus ; elle ne prouve
pas les contacts physiques ni les transformations de chaque GLB réel, leur acceptance
artistique ou une retouche humaine. Aucun nouveau verdict workspace/natif/Web revendiqué.
Gate 1 reste ouverte. Les outils d'édition, undo et sauvegarde générale restent Gate 2.

Logs locaux : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-kit-collider-contract-20260907`.
Clippy moteur all-targets/all-features avec `-D warnings` PASS, sortie 0 ; fmt/diff check PASS.
SHA-256 `tests.log` : `3AEB56D917483FFFAEF68C26E014EAFABDB6F38779A503ED1EFAB282BB13C3AB` ;
`clippy.log` : `B64082F78DAE3982C6347A384184B3D0693FA33D22CD5925B51E6431432EFE19`.

## Requêtes Rapier sur le kit réel — 7 septembre 2026

Implémenté : observation ponctuelle sous feature `qualification`, huit frames après readiness.
Pour chaque primitive chargée, le barycentre des sommets du mesh UCX source est transformé
dans le monde et interrogé dans Rapier avec un filtre sur cette entité précise : départ intérieur,
rayon solide, impact à distance zéro. Un second point au-delà du maximum Y local, transformé
par la même hiérarchie, ne doit pas toucher ce collider en s'éloignant. Chaque résultat conserve
placement et asset ; aucun changement de forme, source, placement ou règle de déplacement.
Le contrôle est réutilisable pour les kits gouvernés, sans identifiant de carte codé en dur.

Réellement vérifié : **36/36 PASS, 20 placements, 6 assets**, événement résumé `complete=true`.
Répartition : arche 6, mur 4, rochers 3+3, chêne 16, pin 4 primitives. Exécution native bornée
à 360 frames, sortie 0, zéro événement ERROR. Les avertissements GPU existants restent visibles.
Le verdict provient des événements, pas seulement de la sortie du processus : cette sonde
diagnostique ne fait pas échouer automatiquement le runtime lorsque `complete=false`.
Ce n'est ni un test de contacts dynamiques ni une acceptation des surfaces complètes ou artistique.
Aucun nouveau verdict physique Web/Tauri, benchmark cible ou passage de Gate 1.

Reproduction depuis le workspace Rust, `RUST_LOG=info`, avec le binaire
`E:/OpenRPG_Forge/.gate1-build-cache/terrain-water-20260906/debug/forge_runtime.exe` :

```text
--world-sketch-visual-valley
--world-sketch-input E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-camera-set-20260906-c/world-sketch.v1.json
--asset-kit-build E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-foliage-coherent-runtime-20260906-b/asset-kit-build.v1.json
--asset-kit-preview E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-twelve-views-20260906/water_detail-clear/preview.json
--exit-after-frames 360
```

Entrées contrôlées avant/après, sans écriture par la sonde. SHA-256 fichiers :

- WorldSketch : `C815E2B9F23071390B69924AB3F4716499B1A8C33FDBB7BCE62E14F7CDA29FBA` ;
- build : `0F321ED0547F09B85A2FBDAB79407B18405CCA28B3749E7297971B0D6CFE3EF9` ;
- preview : `09A1B0A7E926B913E9E582AC7148F0BB0EE33E3FC52DF6FF13C5D201A138A662` ;
- exécutable : `4E7205BE0BB76D0EB9796794ABAFDB584AD80FA9EB30710363CA42DFDB01BC7B`.

Preuves locales : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-kit-physics-query-20260907`.
Les événements JSON sont dans **runtime-stderr.log** (stdout `runtime.jsonl` est vide), hash
`1E6EB0ABCF12651AF5DEFFBB028097E6714B7753307C3A43128713233E5E7CF2`.
Clippy workspace all-targets/all-features strict PASS après correction de deux lints ; premier
échec conservé dans `clippy.log`, succès dans `clippy-pass.log`. `cargo test --workspace` :
**543 PASS, 1 ignored préexistant**, hash `tests.log`
`A9DD90445BEE2EC9049C7CC4666384AAA956BEBBD34BB03B45DED6DF829C26C8`.
Check Studio WASM et build runtime PASS. Le nouveau test synthétique couvre les points sous
translation, rotation et échelle non uniforme. CI distante NOT RUN ; aucune publication Git.
