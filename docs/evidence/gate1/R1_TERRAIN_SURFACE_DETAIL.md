# Gate 1 — Détail procédural de surface du terrain

2026-09-05, branche Rust locale. R1-049 et Gate 1 restent ouverts.

**Premier candidat ci-dessous remplacé après alerte de coût Web.** Deux relevés Chrome du shader à
bruit par fragment donnent 29,68 puis 30,52 FPS. Le bundle précédent donne 92,36 FPS ;
la seconde paire utilise 2048 × 1101 px, 60 frames de chauffe et 120 échantillons.
Le p95 observé passe de 11,6 à 37,2 ms. La visibilité/focalisation du navigateur n'a toutefois
pas été verrouillée identiquement : ne pas attribuer la totalité de cet écart au shader.
Les captures et bundles sont conservés. Le candidat courant utilise le cache R8 ci-dessous.

## Candidat courant : cache de détail partagé

Le calcul de bruit par fragment est remplacé par une texture R8 de 128 × 128, soit 16 Kio
de texels, produite une fois par un PRNG à seed fixe et partagée par les 64 chunks. Aucun
fichier texture source ou téléchargement. Trois lectures filtrées de ce cache remplacent
les douze évaluations de hash ; le filtrage spatial et les coefficients par matériau sont
conservés. Le motif est maintenant périodique (128 cellules), interpolé linéairement et non
identique au premier candidat. Aucun changement de document, masque ou géométrie.

106 tests moteur passent, dont reproductibilité, dimensions, format, diversité et sampler
du cache. Clippy workspace/all-targets/all-features strict, Clippy Studio WASM/Web strict,
build runtime, Trunk, fmt et diff-check passent. Les tests n'établissent pas la qualité visuelle.

Preuves courantes : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-terrain-surface-cache-20260905/`.
Deux captures natives clair/pluie inspectées, même commande que le candidat historique :
codes 0, zéro ERROR, 20/20 instances et 36/36 colliders du kit. Clair : 103,89 FPS,
p95 10,3211 ms, 1 % low 46,42 FPS. Pluie : 100,45 FPS, p95 10,8568 ms,
1 % low 40,70 FPS. Un hitch >33,3 ms par capture ; le run pluie ne remplit pas le seuil
1 % low Gate 1 et n'est pas qualifié comme benchmark.

Chrome réel : bundle courant chargé sur port 14628, onglet de test 201243137. Premier
relevé à 2048 × 1157 : 24,00 FPS, p95 45,2 ms. Après `Page.bringToFront` puis rechargement,
relevé à 2048 × 1101 (`2026-09-05T20:02:15.689Z`) : 120 échantillons après 60 de chauffe,
64,98 FPS, p95 17,6 ms, 1 % low 50,25 FPS, zéro hitch >33,3 ms. Capture inspectée :
grain visible, caméra 1/1 et hashes alignés. Journal warn/error vide ; onglet et serveur fermés.
Ce résultat ne prouve ni 60 FPS dans toutes les conditions ni l'absence de régression face
au bundle initial. La variance de visibilité, les dimensions et le parcours doivent être
contrôlés dans la qualification longue ; pas de poursuite de micro-réglages dans ce lot.
Le ledger reste une mesure CPU/ECS, pas des timestamps GPU. Pas de kit externe dans Chrome.

| Artefact courant | SHA-256 |
| --- | --- |
| shader | `9354e61112bfb6124eb8ac16b6a1db321088e44eb95a8afdc5d55c9bbd041ebb` |
| runtime | `76bc6667d98f480c6ab82b8589bd0b3757a8d8abadd9fcc87b5b316a63b3bcdb` |
| clear.png | `d91ecb5083cd970715b7c31c5ab71b3724adfe1d7d5e0862fac6cdcc68ae9c9a` |
| rain.png | `f2707b6169b75da40bdeba60d2da463769f7fcedcb09847fa5d3ec85093bfb78` |
| WASM | `069658fbd875c1bc848291ee627f949bc2b3a4493d2b841077fef0227a0ea2f4` |

Statut : candidat visuel implémenté et exécuté, qualité/performance Gate 1 ouvertes.
La matérialisation du cache est un détail de renderer, pas un nouveau format authoré.

## Historique du premier candidat — non courant

Le shader de terrain utilisait seulement trois couleurs/rugosités PBR mélangées par les
masques authorés. Il ajoute maintenant trois échelles de bruit interpolé en coordonnées
monde XZ, exprimées en mètres : 0,18, 3 et 32 cellules/m. Les amplitudes diffèrent par
couche herbe/terre/roche. Le détail module la couleur et légèrement la rugosité avant
les multiplicateurs d'humidité existants ; les poids des masques ne changent pas.

Les fréquences plus fines qu'un pixel s'atténuent vers leur moyenne grâce aux dérivées
spatiales. Pas de temps ni d'origine de chunk dans le calcul : pas de déplacement volontaire
du motif avec la caméra, et même entrée mondiale de part et d'autre des frontières de chunks.
Trois évaluations de bruit par fragment, sans texture, sampler, mesh, dépendance ou asset ajouté.
Terrain, collisions, sources WorldSketch, placements et profils restent inchangés.

Il s'agit de détail d'albédo/rugosité, **pas** de relief géométrique, de normal map ou de textures
PBR photographiques. La revue artistique des trois matériaux et du mouvement reste ouverte.

## Preuves natives

105 tests moteur et Clippy moteur/all-targets/all-features strict passent. Le test du shader
contrôle le raccord des poids, coordonnées monde et filtrage ; ce test textuel n'est pas une
preuve de compilation WGSL. Celle-ci est apportée par les deux exécutions graphiques ci-dessous.
Build runtime, Clippy Studio WASM/Web strict, fmt et diff-check passent également.

Dossier externe : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-terrain-surface-detail-20260905/`.
Même rapport de kit corrigé et même preview que le complément playtest R1-064 :
`r1-063-authored-preservation-20260905-b/rebuild-1/asset-kit-build.v1.json` et
`examples/visual_valley/asset-kits/r1-063/runtime-preview-authored.v1.json`.

```text
forge_runtime --world-sketch-visual-valley --asset-kit-build <rapport>
  --asset-kit-preview <preview> --player-camera --weather clear|rain
  --capture <preuve>/<météo>.png --capture-frame 300
  --metrics <preuve>/<météo>-metrics.json --metrics-warmup-frames 180 --exit-after-frames 420
```

Deux captures Full HD inspectées : le grain est visible dans le sol proche en clair et
reste présent sous la pluie ; le lointain est plus lisse. Les arbres de prototype restent
très visibles. Aucun verdict artistique global ni absence de scintillement en mouvement
n'est déduit de ces images fixes.

| Run Vulkan / RX 7800 XT | Clair | Pluie |
| --- | ---: | ---: |
| FPS moyens | 108,02 | 108,39 |
| p95 ms | 9,9733 | 9,9773 |
| 1 % low FPS | 46,28 | 46,30 |
| hitches >33,3 ms | 1 | 1 |
| instances / colliders du kit | 20 / 36 | 20 / 36 |
| code de sortie / ERROR | 0 / 0 | 0 / 0 |

Arrêts : `2026-09-05T19:49:39.211432Z` et `2026-09-05T19:49:45.896332Z`.
Ces smokes courts mesurent le résultat ; ils ne démontrent ni gain de performance ni coût
isolé du shader. Le run clair précédent est conservé dans `r1-governed-kit-playtest-20260905/`
pour comparaison visuelle, pas comme benchmark A/B contrôlé.

| Artefact | SHA-256 |
| --- | --- |
| shader | `dfb816dda04c1e2acaf856b4fcf1b821071c918b4ffd732f7036fee8dcf748c2` |
| runtime | `ebcc299dfbfd072c148ea8bc5bb4b2c5aae895f68126961bcd4ac78d2ba12b9e` |
| clear.png | `438bcff45bf621315f2f787d5f8af0ec4681e88f79df20d3146fb45f84381be6` |
| rain.png | `0fc21febe59d8048b5b253cd1caf2fee3a25f191f4b9674526b92fbdb1bfc46b` |

WorldSketch source inchangée : `5b2088952cad01732a53b371462247cf386bb25141c39ff97a60fb14bd2e5fc1`.
Terrain observé : `18bd933d37f06a6a08a34b19938a78cb8f6fe8d9667b34bd2dcd6355956fd208`.
CI distante NOT RUN, aucune publication Git, aucun asset Cadle.

## Complément 2026-09-06 — variation de litière dans la couche herbeuse

La couche herbeuse utilise maintenant un mélange borné à 32 % vers la palette de terre déjà
déclarée, piloté par les échantillons broad/medium filtrés existants. Aucun poids canonique de
matériau, masque, document ou mesh de terrain n'est modifié. Aucune texture, lecture texture
ou évaluation de hash supplémentaire ; ce détail de présentation ne crée pas un biome authoré.
Le chemin de mouillure est conservé après le mélange de couleur.

433 tests workspace PASS, un probe timing ignoré ; Clippy strict workspace/all-targets/all-features
et build runtime PASS. Le test textuel de raccord shader est renforcé mais ne prouve pas WGSL.
La compilation/exécution GPU est vérifiée par deux smokes natifs clear/rain, chacun 420 frames,
capture à 300, caméra joueur 1920×1080, RX 7800 XT et kit candidat cohérent révision 2 inchangé.
Les deux sorties valent 0 avec `runtime_errors_observed=false` ; captures ouvertes et inspectées.

Observation : variations terreuses visibles à moyenne distance et présentes sous mouillure,
en remplacement de l'aplat vert uniforme. Le motif reste procédural et le relief de surface
non qualifié ; aucune texture photographique, qualité naturelle acceptée ou gain GPU revendiqué.
Les smokes courts ne valent pas comparaison performance contrôlée. Le bundle Web antérieur
ne contient pas ce changement ; aucun rejeu Web de ce shader revendiqué dans ce lot.

Preuves : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-ground-litter-20260906/`.

- `clear.png` SHA-256 `ed94822a7d78803f3a6790ce9f5b68f0ee6732492d77249e00071cea9fc08c4a`.
- `rain.png` SHA-256 `225d6ed6424cffd10edbe6c4b91781683970988263f69dd651254dbc6a0a1d45`.
- `tests.log` SHA-256 `5091f74f0473fc9dc4ccfdfc3f5aae028c567121da6ccd4cd369509a1cfc8f3f`.
- `clippy.log`, `build.log`, `clear.log`, `rain.log`, métriques correspondantes.

Gate 1 reste ouverte, aucune publication et aucune acceptation humaine inférée.

## 9 septembre — candidat sans plaques chromatiques de grande échelle

La vue globale Studio actualisée (`R1_065_FIXED_VIEWS.md`) montre un motif camouflage.
Le shader appliquait un seuil au bruit broad (0,18 cellule/m) pour mélanger jusqu'à
32 % de terre dans l'herbe. Le candidat conserve le bruit broad pour une modulation
achromatique limitée à ±6 %, commune aux trois couches. La litière chromatique utilise
désormais le détail medium (3 cellules/m), linéairement entre 0 et 16 %, moyenne 8 %.
Quand le filtrage annule ce détail, le mélange rejoint cette moyenne, sans seuil.
Pas de lecture texture supplémentaire ; microrelief, poids authorés, chemins,
relief, collisions, documents et assets ne sont pas modifiés par ce lot.

Implémentation : `crates/forge_engine/src/shaders/visual_valley_terrain.wgsl`,
SHA-256 `7A5CEBEC5F0117A4F55584E9996C77698C8D1F6772CADD56D6A5EB1FBE6C5C72`.
Le test de raccord textuel est adapté ; ce test n'est pas une compilation WGSL.
Dossier : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-ground-chroma-20260909`.
Runtime exécuté : SHA-256
`784BB24C55165EF8F47293734C6CBFDD46EB3FA73B641E84DA248988B11F5BCB`.
10 tests ciblés `cargo test -p forge_engine visual_valley::tests --locked -j2`
PASS ; build runtime, format et diff-check PASS.
Contrôles transversaux du candidat terminés : `cargo test --workspace --locked -j2`
678 PASS / un timing ignoré, Clippy workspace/all-targets/all-features strict PASS,
`cargo check -p forge_studio --lib --no-default-features --features web
--target wasm32-unknown-unknown --locked -j2` PASS. Journaux `workspace-tests.log`,
`clippy.log`, `wasm.log` dans le même dossier. Le check Web ne prouve pas le shader
WebGPU ; seul son lancement natif est effectué pour ce candidat.

Deux exécutions natives réelles, sorties 0, captures 1920×1080 ouvertes/inspectées :
`valley_overview` et `human_bridge`, caméra fixe, clair, eau optique et forêt gouvernée.
Arguments communs : `--world-sketch-visual-valley --prototype-forest-batches
--water-depth-preview --water-reflection-preview --fixed-proof-view <caméra>
--capture <png> --capture-frame 180 --exit-after-frames 300` avec `--asset-kit-build`,
`--asset-kit-preview` et `--prepare-vegetation-catalog` pointant respectivement sur
`build.json`, `preview.json`, `prototype-catalog.json` du kit externe
`r1-atlas-cropped-runtime-kit-20260909`.

- `overview.png` : `E41EACDC20315BF6E2395E32E005D09AF285F2AC5D379C2DF62A60F5BC33C9A7`.
- `near.png` : `5E7574AA11E00F2CA8DCFF79899A6511A3DD877C32C049EEB5236AE86BCCEE2E`.

Le motif de grandes plaques n'apparaît plus sur la vue globale ; les variations du
relief restent présentes. Le grain du sol proche est toujours visible. Sol encore
stylisé, forêt lointaine clairsemée et bord du monde toujours visibles : non résolus.
La référence précédente est Tauri 1408×966, ces vues sont natives Full HD : pas
de comparaison pixel à pixel ni de parité de backend déduite. Zéro entrée ERROR
dans les deux journaux ; warnings Vulkan/invariant conservés. Les valeurs HUD sont
des relevés glissants incluant le démarrage, pas un benchmark (autre compilation
en parallèle). Aucun gain de performance ou qualification GPU cible revendiqué.
Processus terminés après chaque capture ; aucun runtime restant au contrôle.

Intégration du nouveau shader dans le paquet Studio, revue humaine et qualification
finale restent dues. Les anciens paquets Studio restent intacts et ne contiennent
pas ce correctif. Gate 1 ouverte ; CI distante NOT RUN, aucun commit/push.

### Intégration Studio du même candidat — 9 septembre

Limite d'intégration du paragraphe précédent levée : Trunk PASS (50,69 s), paquet
Tauri PASS (14,08 s), puis véritable lancement et exports via Computer Use.
Paquet externe `r1-ground-chroma-studio-20260909`, frontend
`r1-ground-chroma-frontend-20260909` sous le même répertoire de preuves.
EXE SHA-256 `9A4A12F50846A23807BE3C8A42B8010F194A8FE30E14CDEC7F8D2A08128D95E1` ;
WASM `6C1A5D4429FE0B2CF3BC28E63A3C50EA8DAEEBB75B62E9C4A17EDC0A5D421CC2`.
Les pins de tous les fichiers du kit sont identiques au paquet témoin
`r1-overview-current-studio-20260909`. Les six hashes de sources exportés sont
également identiques au diagnostic témoin `forge-diagnostic-1788970350758.json`.

Caméra `valley_overview`, clair/High, temps fixe 0,250 s après un pas ; une seule
initialisation, kit 20/20, 36 colliders, 1662 arbres. Warmup final 87 pipelines,
zéro pending/failed, stable 8 ; pic transitoire d'erreurs shader 18 conservé.
PNG 1408×966 réellement ouvert et inspecté : grandes plaques retirées, autres
limites artistiques inchangées. Ce n'est pas une preuve Full HD, temporelle,
de profil complet ou de performance cible. Aucune acceptation humaine déduite.

Exports copiés depuis Downloads, hashes vérifiés, originaux conservés :

- `forge-diagnostic-1788971604802.json` :
  `1A9802975397D148A435FDC74CE41189D581DED73941D370469599211C8CF74E`.
- `forge-viewport-1788971634774.png` :
  `D2270011EB9856E0F369AB85615F2BF518C346B1D3882C51C145A0751AF8A412`.

Instance PID 32836 fermée par Alt+F4, absence du processus, enfants directs et
WebViews portant ce chemin vérifiée. Tests inchangés non rejoués (678 PASS du
candidat ci-dessus) ; aucun code modifié pendant l'intégration. Gate 1 ouverte.
