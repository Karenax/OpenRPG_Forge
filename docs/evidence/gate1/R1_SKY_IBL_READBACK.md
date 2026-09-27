# R1 — Pixels HDR réels de la sonde de ciel, 8 septembre 2026

## Diagnostic établi, sans correction lumineuse arbitraire

La capture du candidat d'ombres montre encore des troncs presque noirs. Le ledger
indiquait une sonde raccordée, mais cette observation de composants ne prouvait pas
le contenu GPU. Une lecture réelle de la cubemap brute d'atmosphère confirme :
**393 216 texels RGB finis et de luminance positive**, aucune face noire.
Ne pas augmenter l'intensité du ciel en supposant une texture brute vide.

| Face, ordre brut GPU | Moyenne de luminance linéaire RGB | Minimum | Maximum |
| --- | ---: | ---: | ---: |
| 0 | 2294,291 | 1017,642 | 6207,184 |
| 1 | 2449,077 | 1263,200 | 6437,575 |
| 2 | 1065,794 | 822,970 | 1742,310 |
| 3 | 2319,637 | 2319,637 | 2319,637 |
| 4 | 2280,299 | 1021,289 | 6187,688 |
| 5 | 2397,551 | 1189,844 | 6411,218 |

Calcul `0,2126 R + 0,7152 G + 0,0722 B` sur RGB16F décodés, alpha exclu. Ces nombres
bruts ne sont pas présentés comme des lux mesurés ni comme une qualification de
luminosité perceptuelle. Chaque face a 65 536 pixels, aucun NaN/Inf RGB.
Le shader Bevy écrit l'inscattering de la sky-view LUT et alpha 1 ; la face uniforme
est enregistrée telle quelle, sans inférer ici son orientation physique.

**Limite essentielle :** il s'agit de la cubemap brute, pas de la carte diffuse
filtrée effectivement échantillonnée par les matériaux. Le filtrage, la liaison
finale et les propriétés des matériaux restent à vérifier. Cette preuve écarte
une source brute noire ; elle ne prouve pas que l'éclairage indirect final est correct.
Pas de correction du défaut visuel ni de fermeture de Gate 1 annoncée.

## Implémentation et protocole exécuté

Nouveau harnais natif `sky_ibl_proof.rs`, option explicite
`--sky-ibl-proof-output`, réservée à une capture de vallée bornée et excluant la
mesure de marche. Aucun changement de shader, texture, exposition, intensité,
matériau ou code partagé. Utilisation de `bevy::render::gpu_readback::Readback`,
pas de fork ni de renderer supplémentaire. Requête unique après frame 900, sonde
unique, dimensions/format/usage validés, observer supprimé à la première réponse,
écriture create-new et erreur conservée si elle échoue.

Lecture réussie à frame 900 : 256 × 256 × 6, RGBA16Float, intensité source 1,0.
3 145 728 octets, soit 2048 octets/ligne : déjà alignés sur 256, aucun padding à
retirer dans ce cas. `Analyze.ps1` utilise la conversion Half de .NET, vérifiée
pour 0, 1 et +Inf avant analyse. Lecture et analyse locales, aucun upload.

Racine `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-sky-ibl-readback-20260908`.
`Capture.ps1` reprend le candidat externe 2048 / trois cascades / 128 m / contact
conservé, kit BLEND original épinglé. Marche normale 1800 ticks, capture frame
1700 après événement de fin de marche, sortie frame 1800. Position de fin
(-132,49425 ; 2,5989828 ; -203,5172). PNG inspecté : même défaut sombre ; aucun
gain visuel prétendu. Le relevé GPU se fait pendant le parcours, non à la pose
finale, et ne mesure que l'atmosphère (pas l'occlusion de la forêt).

**84 tests Runtime + une intégration PASS**, Clippy all-targets strict, build
natif, fmt et diff-check PASS. Les deux tests nouveaux couvrent la restriction
CLI/doublon et la requête unique après préchauffage ; les pixels eux-mêmes sont
prouvés par l'exécution GPU, pas par ces fixtures. Aucun test workspace/rebuild
WASM requis pour ce harnais natif seul ; aucune qualification globale déduite.
PID 17740 terminé, sortie 0, aucun ERROR Runtime, readback unique, capture vérifiée.
Processus graphique absent après essai ; script d'analyse également terminé.

| Artefact | SHA-256 |
| --- | --- |
| forge_runtime.exe | `7673A64A403569EA7F5667B858FA48982AA6E479AA961505CC24CFBF55DAB434` |
| sky.bin | `BD5B952A030BB5975E2286A65AF985E7289FCAF1F4AE1714CFB1EE86480C87C2` |
| analysis.json | `D5B4CAFD7E3E38A1200AF2358D105F7F45980FFB6C6AB790A5D1484889271796` |
| candidate.png | `ACAAEE6E90F624490E537CAAFF0BA5C89D32E0B3FF1B10E507A99644DB83AD94` |
| candidate.metrics.json | `E2F97A7899FE0B51D99F79F428865705A7D861067A64E51A308286333349553A` |

Références source locales inspectées : Bevy 0.19.1,
`bevy_pbr/src/atmosphere/environment.rs` et `environment.wgsl`,
`bevy_render/src/gpu_readback.rs`, `bevy_light/src/probe.rs`.
Le gain par défaut 1,0 de `AtmosphereEnvironmentMapLight` est cohérent avec la
documentation du type ; aucun changement de ce gain n'est justifié par cette preuve.
