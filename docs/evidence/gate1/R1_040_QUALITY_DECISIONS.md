# R1-040 — Décisions de qualité et premier adapter pluie

Complément suivant : budgets d'ombres consommés, option native et preuves locales dans
`R1_040_SHADOW_BUDGETS.md`. Le contrôleur global reste non raccordé.

2026-09-05, Rust local non publié. **R1-040 et Gate 1 restent ouverts.**
Le document `examples/visual_valley/quality-profile.v1.json` existait sans consommateur
dans les adapters. Ce lot implémente sa logique de décision pure dans `forge_world::quality`.
Il ne remplace pas l'objectif de quatre profils appliqués, mesurés et accessibles dans le produit.
SHA-256 du document inchangé : `ec4c0db5b3a0287a309a541cb889120143cecd515dec2cea748f527c0c0fd9e3`.

## Implémenté

`QualityAdaptationController::from_applied_tier` valide et hashe le document, conserve une copie
de la politique et exige un niveau initial compatible déclaré comme réellement installé par
l'adapter. Il ne possède ni document modifiable, ni objet Bevy, ni ressource GPU, ni horloge.

Avec la politique courante :

- baisse après 90 images éligibles consécutives strictement au-dessus de 18 ms ;
- hausse après 600 images éligibles consécutives strictement au-dessous de 13,5 ms ;
- bande neutre, échantillon invalide ou interruption explicite : remise à zéro de la fenêtre ;
- 240 images de temporisation après application ou refus, sans accumulation cachée ;
- changements bornés aux niveaux et au pas maximum déclarés dans la politique ;
- mode benchmark verrouillé sur High ; politique désactivée : aucune adaptation.

Une proposition contient séquence monotone, niveau de départ, niveau demandé et raison.
Le niveau appliqué ne change qu'après `acknowledge_applied` de la proposition exacte.
Pendant son application, aucune autre proposition n'est produite. Un refus conserve l'ancien
niveau et impose la temporisation ; un accusé périmé ne consomme pas la proposition actuelle.
Les durées non finies, nulles ou négatives sont refusées. Aucun lissage arbitraire ou minuteur
réel n'est ajouté : l'hystérésis suit les fenêtres consécutives déjà déclarées dans la source.

## Réellement vérifié

Six nouveaux tests couvrent proposition sans fausse application, hash source inchangé,
hystérésis et interruptions, temporisation/remontée, refus/accusés périmés, benchmark/politique
désactivée, pas configurable et saturation aux bornes. Avec les tests existants :

- `cargo test -p forge_world --locked -q` : **73 PASS** ;
- `cargo test --workspace --locked -q` : **337 PASS** ;
- `cargo clippy --workspace --all-targets --all-features --locked -- -D warnings` : PASS ;
- `cargo check -p forge_world --target wasm32-unknown-unknown --locked` : PASS.

Exécuté avec Rust épinglé, `CARGO_INCREMENTAL=0`, `RUSTFLAGS=-C debuginfo=0` et target
`C:/Users/karen/AppData/Local/Temp/openrpg-forge-r1-064-target`.
Preuves de code sur arbre modifié, pas qualification finale en worktree propre ADR-0006.
CI distante **NOT RUN**. Pas de capture GPU : aucun renderer n'a été modifié dans ce lot.

## Complément : budget de pluie réellement projeté

Le lot suivant ajoute `WeatherRuntimeState::request_rain_quality` et
`VisualValleyWeatherPlugin::with_initial_rain_quality`. L'adapter remplace le contenu du mesh
au même AssetId et invalide ses bounds, sans recréer entité ou matériau, sans réinitialiser
horloge/humidité et sans modifier la source. Une erreur conserve le budget précédent avec
diagnostic explicite. Les métriques distinguent `requested_quality` et `quality` projetée CPU ;
elles ne prétendent pas observer la fin du transfert GPU. Aucun accusé du contrôleur global
n'est produit par cet adapter isolé.

Le Runtime accepte `--rain-quality low|medium|high|ultra` dans les deux modes vallée.
Sans option, le niveau authoré reste utilisé. Valeur absente, inconnue, doublon et emploi hors
vallée sont refusés. Ce flag règle seulement la pluie, pas le preset graphique global.

Vérifications du complément, le 2026-09-05 :

- **339 tests workspace PASS**, dont cycle ECS Low → Ultra → Medium → High conservant
  bindings, hash source, horloge à 123 s, humidité, un seul mesh vivant et positions High exactes
  au retour ; tests CLI des quatre niveaux dans les deux modes et des entrées invalides ;
- Clippy workspace/all-targets/all-features avec `-D warnings` : PASS ;
- build natif Runtime et check moteur `--no-default-features --features web`
  pour `wasm32-unknown-unknown` : PASS ; pas de nouvelle exécution Chrome ;
- quatre exécutions natives, Vulkan/RX 7800 XT pilote 26.8.1, Full HD, AutoVsync,
  caméra `human_bridge`, météo rain, capture frame 300, warmup 180, sortie frame 420 :
  code 0, zéro ERROR, 20 instances kit et 36 colliders, source terrain et météo conformes ;
- quatre PNG collectés ; Low et Ultra inspectés, densité distincte visible. Les quatre démarrages
  ne prouvent pas une transition GPU à chaud : celle-ci reste couverte seulement au niveau ECS.

| Budget pluie | Gouttes | Sommets | Triangles | Entités pluie |
| --- | ---: | ---: | ---: | ---: |
| Low | 2048 | 16384 | 8192 | 1 |
| Medium | 4096 | 32768 | 16384 | 1 |
| High | 8192 | 65536 | 32768 | 1 |
| Ultra | 16384 | 131072 | 65536 | 1 |

Artefacts locaux non publiés :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-rain-quality-20260905/{low,medium,high,ultra}.{png,json,log}`.
Runtime utilisé SHA-256 : `47bf256eac163ec8bf7b804ace045460d7c19e63b5607af3931f986c7475885e`.
Reproduction : ce Runtime, `--world-sketch-visual-valley --weather rain --rain-quality <niveau>`,
`--asset-kit-build E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-063-authored-preservation-20260905-b/rebuild-1/asset-kit-build.v1.json`,
`--asset-kit-preview E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-vegetation-normals-after-20260905/human_bridge-clear/preview.json`,
puis `--capture <nouveau.png> --capture-frame 300 --metrics <nouveau.json>
--metrics-warmup-frames 180 --exit-after-frames 420`.

Hashes vérifiés identiques avant/après : WorldSketch fichier
`5b2088952cad01732a53b371462247cf386bb25141c39ff97a60fb14bd2e5fc1`, météo fichier
`b2c69cec24af5ae398d084f20e14e3659a633e60d87806498208d9813e2d4284`, politique qualité
`ec4c0db5b3a0287a309a541cb889120143cecd515dec2cea748f527c0c0fd9e3`, kit
`02566c6ec217084e76fab4d741e62e93db0737e48a5f4ae3e107b924bb877631`, aperçu
`3d47764ff1ceb66903b8a4a1d8b3ac9be475464f157bedc39e2821b8bcf3a784`.

| Niveau | SHA-256 PNG | SHA-256 métriques |
| --- | --- | --- |
| Low | `b66e321c1cfa890a79459535ee09220b59903f0375ed399c08654a34281e2cc8` | `f043d10e527f82bbd751d753c8db987b91eea976679c5ff5120257d836af9e44` |
| Medium | `16c12d10409b9ecd12a98c6d6bdb5f58c5c9fb08d6fd58b89cf5433e3e787a9c` | `faea124e33ac8da15c5ccf45806e7f4deace5b10f00b1e95ea5bc181906de875` |
| High | `529ca10d70bb0072c9fadace6f6f23bc223dfd2a68bfbff1bc4b0aab4554663d` | `5b471cb2f2a73deb20ae012499d5cad51981ca782c85be535c5388ecfa3e09ff` |
| Ultra | `b85076a72e2291c6de76c8adf603fb55a1bd649c88154d934eb0b702a7ffcb48` | `b3e4aad17e4a7b8e1be533d768c67e6c1d93e4496185cc6a9b71e5b1374968b3` |

Mesures exploratoires, 240 échantillons par run (~2,1 s) : moyennes 113,84/114,85/112,85/114,61 FPS,
p95 9,68/9,37/9,63/9,58 ms et 1 % low 42,86/45,47/44,79/46,68 FPS (Low à Ultra).
Chaque run contient un hitch >33,3 ms et une capture PNG dans la fenêtre mesurée. Ce protocole
court n'isole ni coût de la pluie ni coût de capture : **aucun verdict de performance**,
aucune micro-optimisation engagée sur ces chiffres. Silhouettes stylisées, dithering visible
et végétation clairsemée restent des limites artistiques distinctes de ce raccord fonctionnel.

Preuves locales sur arbre modifié, CI **NOT RUN**, revue humaine non obtenue ; R1-040 reste ouvert.

## Raccords encore prévus, non implémentés par ce lot

Le contrôleur n'est pas encore consommé par le Runtime ni le Studio. Il ne suffit donc pas
d'envoyer un accusé artificiel ou de changer une étiquette Low/High pour qualifier R1-040.
Le prochain raccord doit installer et vérifier les budgets réels du preset : terrain/LOD,
végétation, ombres, environnement, eau, pluie et post-traitement. Les caches sont dérivés ;
les documents, placements authorés et bindings physiques ne doivent pas être régénérés ou
réécrits simplement pour changer de qualité.

L'adapter devra filtrer préchauffage, capture, resize et fenêtre non active avant les observations,
attendre la disponibilité des ressources, confirmer uniquement après application complète et
conserver un diagnostic explicite des capacités non prises en charge. Il faudra ensuite exposer
la sélection dans Runtime/Studio et prouver les transitions, le retour exact et le benchmark High
non adaptatif. La couverture de sept domaines et ces preuves restent dues ; aucune gate suivante
n'est ouverte par les seuls tests du contrôleur.
