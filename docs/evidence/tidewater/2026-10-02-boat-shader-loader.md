# Tidewater — lecteur Rust des shaders source du bateau

Suite de l'[acquisition source](2026-10-02-boat-source-shaders.md) et de la
[projection géométrique Engine](2026-10-02-boat-engine-instance.md).
Le résultat qualifie le transport et l'ABI CPU ; le dessin dans Forge reste ouvert.

## Livraison

`forge_assets::source_mesh_shaders` lit le contrat générique versionné
`forge.source-mesh-shaders` v1. Il reçoit les octets par callback synchrone ou
asynchrone et exige le SHA-256 gouverné du manifeste. Le crate ne dépend ni
du clone Tidewater, ni de JavaScript, d'un chemin disque ou d'un device GPU.

Les records sont typés : frame, matériaux, hooks, variantes, passes, buffers
vertex, politiques de profondeur/transparence et layouts de ressources. Les
contrôles refusent les champs inattendus, références échappant au paquet, pins
ou tailles différents, doublons, ABI incohérentes et valeurs hors plage. Les
uniformes f32/u32/i32, vecteurs, mat4 et tableaux supportés sont assemblés en
little endian, avec leur padding source. Un type non supporté échoue.

`declared_requirements()` compte tous les bindings visibles déclarés, avec
l'uniforme Draw du profil source dans les deux stages. Pour les variantes
main/couleur, le fragment demande **12 uniform buffers, 2 storage buffers,
20 textures et 9 samplers**. La réflection Naga ne trouve que neuf textures
accessibles au maximum : cela ne réduit pas les limites exigées par un layout
GPU explicite. Les passes profondeur ont d'autres besoins.

## Entrées et provenance

Source intacte : `1438b1abfcaee3267092b75573014f4d9b4a983c`.

| Entrée locale | SHA-256 |
|---|---|
| capture 344, `capture.json` | `d5924cb51c76fd3323c14e81cce3d448d6ea0dd3bd4bf3e70a4096d2765458a9` |
| paquet 350, `manifest.json` | `0d8da9f42be405c0da3f39247b6e58fe1281fec2bb7c1cc21fb12d45bf8a8872` |
| oracle 347, `oracles.json` | `59d75adadb7b5b7c5a412b6bbeb0559a25e6291ca4cb7ca9ebe2170a906b736e` |

Ces répertoires sont sous `F:/OpenRPG_Forge/.build/`. Le paquet 350 contient
25 fichiers hashés : vingt WGSL originaux et la capture, son reçu, ses pins
d'entrée, la licence et les crédits. Les sept matériaux et huit hooks sont
conservés dans le manifeste ; les vingt WGSL sont byte-identiques à la capture.

`Acquire-UniformPackingOracles.mjs` utilise le véritable `UniformBlock._pack()`
de la source, sans initialiser ou remplacer ses API GPU. Les 18 blocs distincts,
188 sites nommés et 2 336 octets de blocs packés correspondent exactement au
pack Rust. Dix-sept payloads sont distincts par hash. Il s'agit des valeurs
capturées après initialisation, pas des buffers vivants lus sur le GPU.
Les valeurs mutables capturées ne remplacent pas les defaults canoniques 319.

## Validation locale

Sources figées dans `boat-shader-loader-source-348/src`, avec 867 pins contrôlés
après exécution sans écart. Les sources des crates assets/core/project/gameplay,
le lockfile et les manifestes ont été comparés au checkout courant : seul le
manifeste de l'outil de navigation, non compilé par cet essai, a changé ailleurs.

Reçus et logs : `boat-shader-loader-validation-349/`.

| Contrôle | Résultat |
|---|---|
| tests `forge_assets --lib`, Rust 1.98.0 | 51 PASS, 0 FAIL, 3 ignores préexistants |
| build du vérificateur | exit 0 |
| Clippy lib + vérificateur, `-D warnings` | exit 0 |
| check lib `wasm32-unknown-unknown` | exit 0 ; compilation, sans navigateur |
| lecture du paquet réel 350 avec binaire conservé | PASS, 7 matériaux, 20 programmes, 8 hooks |
| comparaison sync/async avec callback immédiat | vingt payloads identiques |
| comparaison packing source/Rust | 18 blocs, 188 sites exacts, 2 336 octets |
| négatifs sémantiques et d'intégrité | 11 refus attendus, avec pins recalculés pour les cas sémantiques |
| copie complète déplacée 351 | même verdict que 350, sans référence au clone source |
| labels oracle renommé, doublon ou affecté à un autre bloc | trois refus attendus, avec pin oracle recalculé |
| CLI sans arguments ou racine absente | deux sorties 1 attendues |

Binaire conservé : `boat-shader-loader-validation-349/artifact/verify_source_mesh_shaders.exe`,
1 635 328 octets, SHA-256
`0096d9649384bf8fd92eb46b8cbd430ee9b4ede0b2204043df3aa860c9d7262f`.
`final-result.json`, `terminal-run-receipt.json` et `source-guard-final.json`
conservent les résultats finaux. Les essais et échecs Clippy intermédiaires
restent conservés ; ils ne sont pas le verdict final.

La revue indépendante vérifie les 25 pins, l'identité des vingt WGSL, les
188 sites et les besoins déclarés. Ses constats sont dans `INDEPENDENT-AUDIT.md`.
Une preuve CPU Naga 29.0.4 séparée (`boat-shader-naga-344/results-abi.json`)
parse/valide vingt programmes et contrôle 180 occurrences de blocs, 654 bindings
et 1 585 membres, ainsi que les streams vertex, Draw, sorties de passe et discard.
Ce harness porte sur les WGSL de la copie 345, identiques à 344/350 ; son ancien
manifeste ne remplace pas le contrat Rust 350. Aucun draw GPU n'y est exécuté.

## Limites et prochaine tranche

Les références de provenance sont vérifiées comme fichiers opaques par le
loader, pas interprétées comme une qualification source. La comparaison async
utilise des callbacks immédiatement prêts ; elle ne qualifie pas une lecture
réseau réellement suspendue. Le check WASM ne qualifie pas une exécution WebGPU.

Les textures et buffers GPU réels ne sont pas encore fournis par ce paquet.
Le prochain raccordement doit employer le même RenderDevice Bevy, vérifier
ses limites, fournir les ressources source avec leurs vrais types et ownership,
composer les passes et leurs politiques de blend/profondeur, puis dessiner le
bateau dans la scène native et Studio. Les providers Forge existants ne couvrent
pas encore tout le contrat ; des ressources neutres ne constituent pas une preuve.

Ce résultat ne qualifie ni l'apparence du bateau, ni l'édition Studio, les
contrôleurs, l'ensemble de l'île ou un paquet Windows destiné au public.
L'objectif complet reste actif.

Suite : [création des programmes sur le GPU Bevy](2026-10-02-boat-bevy-pipelines.md).
Le contrat v2 ajoute les politiques explicites ; l'inventaire source incomplet
et le dessin réel restent distingués de cette preuve de préparation GPU.
