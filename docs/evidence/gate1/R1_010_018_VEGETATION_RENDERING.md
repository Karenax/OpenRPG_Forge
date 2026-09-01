# Gate 1 — Preuve R1-010 à R1-018 et R1-032 — végétation rendue

Date de validation locale : **2026-08-31**

Branche : **`Rust`**

Commit code qualifié : **`1f54b49bef7f086ef72d4f025c5059ec1860780d`**

Tree Git : **`7679000868fb4ae37d3cff1fdcf2a4f6bc1927f9`**

Verdict du lot : **PASS AUTOMATISÉ LOCAL — R1-010 à R1-013, R1-015 à R1-018 et R1-032 ;
R1-014 PARTIEL NATIF**

Gate 1 reste **OUVERTE**. Ce verdict qualifie la projection procédurale de la végétation, ses LOD et
HLOD, l'herbe groupée, le vent shader natif, l'interaction joueur, les règles d'ombre et les métriques
de visibilité prévues par ADR-0007. `R1-014` reste ouvert : le check WASM embarque le WGSL, mais ne
crée pas réellement son pipeline sous WebGPU. Un
[protocole temporel Browser préenregistré](R1_014_BROWSER_TEMPORAL_PROTOCOL.md) existe désormais,
avec le statut `HARNESS READY — ACQUISITION NOT RUN`; il ne change pas cette qualification. Le lot ne
qualifie ni la performance finale, ni les draw calls réels, ni la qualité artistique comparative, ni
les autres lots de Visual Valley.

## Résultat livré

- Le cache déterministe `R1-009` est projeté sans modifier ses documents ou ses deux empreintes
  canoniques. Les **8 282 placements logiques** restent la source de vérité.
- Les arbres, arbustes et fougères créent **3 918 entités de placement** qui partagent quatre meshes
  procéduraux et un matériau. Cette configuration est éligible au batching automatique de Bevy ;
  elle n'est pas présentée comme un nombre observé de lots ou de draw calls GPU.
- Les **704 arbres** possèdent un mesh proche et un mesh moyen. Des `VisibilityRange` complémentaires
  et un dither fragment bornent la transition. Au-delà, **64 meshes HLOD**, un par chunk, regroupent
  les silhouettes ligneuses sans inclure l'herbe.
- Les **5 068 patches d'herbe logiques** deviennent **162 176 brins** fusionnés dans **64 meshes de
  chunk**. Aucun brin ni patch d'herbe ne devient une entité ECS. Chaque base de brin est reprojetée
  sur la surface triangulée du terrain, y compris en pente.
- Un `MaterialExtension` WGSL original Forge déforme les sommets selon leur hauteur, une phase stable,
  le temps courant/précédent et un champ d'interaction local. La base reste ancrée et les amplitudes
  sont bornées par le profil.
- Le champ d'interaction est publié après la pose joueur post-physique et avant la caméra. Le probe
  dynamique vérifie que son centre ne retarde jamais d'une frame et que sa direction suit la vitesse.
- Les tables de visibilité utilisent la caméra 3D active, `VisibleEntities` et `VisibilityRange`.
  Elles séparent candidats caméra, exclusions de portée et exclusions CPU restantes ; elles ne sont
  jamais renommées draw calls, occlusion GPU ou instances effectivement soumises.
- Le soleil réel de Visual Valley reprend la distance d'ombre du profil dans son
  `CascadeShadowConfig`. Les arbres proches projettent des ombres ; les autres représentations portent
  explicitement `NotShadowCaster` selon ADR-0007.

## Qualification item par item

| Item | Preuve observée | Qualification exacte |
|---|---|---|
| `R1-010` | 3 918 entités, 4 handles mesh partagés, 1 matériau, preprocessing GPU actif en mode `culling` | Éligibilité au batching automatique Bevy ; nombre de lots GPU non observé |
| `R1-011` | 3 214 représentations proches et 704 représentations moyennes, meshes distincts et plages testées aux marges | LOD fonctionnel ; popping en déplacement non jugé humainement |
| `R1-012` | 64 meshes HLOD par chunk, dont 21 candidats caméra dans la capture | HLOD fonctionnel ; complémentarité artistique non qualifiée |
| `R1-013` | 162 176 brins dans 64 lots, zéro entité par brin ou patch | Herbe groupée qualifiée |
| `R1-014` | shader natif, temps courant/précédent, deux captures fixes et masque de différence ; code WASM compilé | **PARTIEL** : pipeline WebGPU non lancé, item laissé ouvert |
| `R1-015` | champ borné consommé par le matériau et probe joueur dynamique sans retard | Interaction locale fonctionnelle |
| `R1-016` | 64 lots herbe et 64 lots HLOD avec AABB explicites, plages et partitions caméra cohérentes | Culling observé via les tables CPU/Bevy, pas comme occlusion GPU exhaustive |
| `R1-017` | CSM réel à 128 m, 704 casters et 3 342 opt-out | Distance CSM et politique de casters qualifiées, pas une coupure radiale par caster |
| `R1-018` | schéma métriques v4, instantané final après 241 observations, une caméra et partition exacte sans incohérence | Métriques visibles/cullées qualifiées ; draw calls laissés à `null` |
| `R1-032` | 150 échantillons en marche, 150 directions alignées, erreur de centre maximale 0 m | Interaction joueur-végétation qualifiée fonctionnellement |

## Cardinalités et visibilité runtime

Le ledger final relie les documents canoniques aux nombres dérivés suivants :

| Mesure | Valeur |
|---|---:|
| placements logiques acceptés / tentatives | 8 282 / 40 960 |
| arbres / arbustes / fougères / patches d'herbe | 704 / 1 228 / 1 282 / 5 068 |
| entités de projection totales | 4 046 |
| entités de placement / proches / moyennes | 3 918 / 3 214 / 704 |
| lots herbe / HLOD | 64 / 64 |
| brins d'herbe | 162 176 |
| meshes partagés / meshes de chunk / matériaux | 4 / 128 / 1 |
| casters d'ombre / opt-out | 704 / 3 342 |

L'instantané final suivant a été produit après **241 observations** avec exactement une caméra 3D
active et zéro entité marquée visible hors de sa plage dans cet instantané :

| Famille | Total | Candidats caméra CPU | Hors plage | Exclus CPU après portée |
|---|---:|---:|---:|---:|
| total | 4 046 | 334 | 2 762 | 950 |
| proche | 3 214 | 240 | 2 272 | 702 |
| moyen | 704 | 70 | 426 | 208 |
| HLOD | 64 | 21 | 4 | 39 |
| lots herbe | 64 | 3 | 60 | 1 |

Chaque ligne se referme exactement sur son total. `cpu_visible_for_camera` décrit les candidats
présents dans la table Bevy de la caméra ; `cpu_culled_after_range` est le résidu exclu après la
portée. Ces valeurs ne prouvent ni le nombre de draw calls, ni un test d'occlusion GPU exhaustif.

## Vent shader et preuve temporelle

Le shader utilise l'uniforme matériau pour les temps courant et précédent. Cette décision évite une
liaison aux globals de vue absente du pipeline prepass Bevy 0.19.1. Le temps est replié par
`elapsed_secs_wrapped()` et la valeur précédente est calculée dans la même période pour limiter la
perte de précision pendant une session longue.

Deux exécutions natives 1920 × 1080 ont conservé la caméra et le personnage fixes, désactivé les
entrées clavier/souris automatiques, puis capturé les frames 60 et 180. Après exclusion des 260 pixels
supérieurs contenant l'overlay, FFmpeg mesure un PSNR moyen de **28,837706 dB**. Le masque de
différence amplifié montre les changements sur les herbes, arbustes, cimes et ombres, tandis que le
terrain et la caméra restent stables.

```powershell
$EvidenceRoot = 'E:\OpenRPG_Forge\.gate1-evidence-runtime'
$WindRoot = "$EvidenceRoot\r1_014_wind_final"
New-Item -ItemType Directory -Force -Path $WindRoot | Out-Null

cargo run -j 1 -p forge_runtime --locked -- --visual-valley --player-camera `
  --capture "$WindRoot\wind-frame-0060.png" `
  --capture-frame 60 --exit-after-frames 70 --no-vsync

cargo run -j 1 -p forge_runtime --locked -- --visual-valley --player-camera `
  --capture "$WindRoot\wind-frame-0180.png" `
  --capture-frame 180 --exit-after-frames 190 --no-vsync

ffmpeg -hide_banner `
  -i "$WindRoot\wind-frame-0060.png" `
  -i "$WindRoot\wind-frame-0180.png" `
  -lavfi "[0:v]crop=1920:820:0:260[a];[1:v]crop=1920:820:0:260[b];[a][b]psnr" `
  -f null NUL
```

Cette paire prouve que la déformation varie dans le temps ; elle n'est pas un golden pixel
reproductible, car Gate 1 ne possède pas encore d'horloge de capture fixe.

## Interaction joueur dynamique

Le probe natif `--visual-valley --player-camera-probe` a terminé avec un code de sortie **0** et un
unique événement `player_camera_probe_completed`. Sa marche de 150 ticks traverse la couture centrale
du terrain, de `z = 8,000187 m` à `z = -3,626445 m`, avec 150 ticks au sol.

| Champ interaction | Valeur observée |
|---|---:|
| interaction disponible | oui |
| échantillons pendant la marche | 150 |
| directions alignées sur la vitesse | 150 |
| erreur maximale centre interaction / pose post-physique | **0,0 m** |

Le test unitaire borne aussi centre, rayon, force et direction, puis le système met le même champ dans
l'uniforme WGSL. Le probe démontre le chemin fonctionnel et l'ordre du schedule ; il ne remplace pas
une appréciation humaine de la force ou de l'aspect visuel de la courbure.

## Validation automatisée locale

Environnement : Windows 11 `10.0.26200.0`, AMD Ryzen 5 7600, AMD Radeon RX 7800 XT / Vulkan,
pilote `26.8.1 (LLPC)`, `rustc 1.98.0 (88d9e12ae 2026-08-18)`, Cargo `1.98.0`, hôte
`x86_64-pc-windows-msvc`.

| Contrôle | Résultat observé |
|---|---:|
| `cargo fmt --all -- --check` | PASS |
| `git diff --check 547ad644… 1f54b49b…` | PASS |
| `cargo test -j1 -p forge_engine 'vegetation::tests::' -- --nocapture --test-threads=1` | **14/14 PASS** |
| `cargo test -j1 -p forge_engine -p forge_runtime -- --nocapture --test-threads=1` | **92/92 PASS** : engine 66, runtime 25, intégration 1 |
| `cargo clippy -j1 -p forge_engine -p forge_runtime --all-targets -- -D warnings` | PASS |
| `cargo check -j1 -p forge_engine --target wasm32-unknown-unknown --no-default-features --features web --locked` | PASS en **9 min 43 s** |
| smoke natif GPU final | PASS, code 0, aucune erreur shader ou pipeline |
| probe joueur/caméra/interaction | PASS, code 0, 232 ticks fixes |
| régression runtime Gate 0 sans `--visual-valley` | PASS, sections Visual Valley/ombre/végétation absentes |

Le check WASM prouve la compilation du moteur et l'intégration du fichier WGSL dans cette cible. Il ne
crée pas le pipeline shader sous WebGPU et ne prouve ni lancement du viewport Studio, ni parité
visuelle native/WebGPU. C'est pourquoi `R1-014` reste ouvert.

## Défauts réellement détectés puis corrigés

Les premiers runs n'ont pas été masqués :

1. le premier smoke natif a quitté avec le code 1, car le vertex prepass référençait le binding global
   `group 0 / binding 11`, absent de ce pipeline. Les temps courant/précédent ont été déplacés dans
   l'uniforme matériau ; les builds natif et WASM et le smoke final passent ;
2. le probe a d'abord refusé l'état désactivé exact de l'interaction pendant le warmup. Le validateur
   autorise désormais uniquement cette transition initiale nulle, puis exige un champ actif fini ;
3. le probe a ensuite mesuré un retard d'une frame, soit **0,004624817 m**, entre centre d'interaction
   et pose joueur. L'ordre est maintenant explicite : `Observe` → publication interaction → caméra.
   Le replay final mesure **0,0 m** ;
4. une capture intermédiaire a été rejetée parce que les entrées souris réelles avaient déplacé la
   caméra dans la géométrie. Les captures automatisées bornées désactivent désormais clavier, souris
   et verrouillage du curseur ; le mode interactif normal non borné reste inchangé.

## Artefacts natifs locaux inspectés

Les captures et métriques restent hors du dépôt, sous
`E:\OpenRPG_Forge\.gate1-evidence-runtime\`. Elles ne sont ni des assets produit ni des captures
Cadle. Le rapport conserve leurs empreintes :

```powershell
$EvidenceRoot = 'E:\OpenRPG_Forge\.gate1-evidence-runtime'
$QualifiedRoot = "$EvidenceRoot\r1_010_018_qualified"
New-Item -ItemType Directory -Force -Path $QualifiedRoot | Out-Null

cargo run -j 1 -p forge_runtime --locked -- --visual-valley --player-camera `
  --capture "$QualifiedRoot\visual-valley-vegetation-player-static.png" `
  --capture-frame 180 `
  --metrics "$QualifiedRoot\visual-valley-vegetation-metrics.json" `
  --metrics-warmup-frames 120 --exit-after-frames 240 --no-vsync
```

| Artefact | SHA-256 | Taille |
|---|---|---:|
| `r1_010_018_qualified/visual-valley-vegetation-player-static.png` | `fdd6a6c14211595849a690458ebed599e8201f450bd3f83c79d97991e3924ad7` | 2 003 231 octets |
| `r1_010_018_qualified/visual-valley-vegetation-metrics.json` | `22f0a24ece548556381eda99aab26dc7dca4591ae7efcc90b9548591b3f8e0fd` | 7 587 octets |
| `r1_010_018_final/gate0-regression.png` | `c2b8515ee528320970a9653bf1e54be89093f09b2ea462bfdbda554d79894485` | 1 043 030 octets |
| `r1_010_018_final/gate0-regression-metrics.json` | `51abd890f50fea2e6951d8f65eac25aaeef8267381f2a587a34d5d7118277d9a` | 4 685 octets |
| `r1_014_wind_final/wind-frame-0060.png` | `efd2ee23f94c5cf05953f98ba0302083ec298507b2883aaa3514b4cd94515839` | 1 997 380 octets |
| `r1_014_wind_final/wind-frame-0180.png` | `cefed5b7f1a9bd53d5a34806a54547349741a33165855b817b30bbc1ecbe88be` | 2 002 789 octets |
| `r1_014_wind_final/wind-difference-x12.png` | `56bf2a5f381c003da62e7d8333fc53a3416812d5ae5816b83aba5aafc7f13735` | 388 558 octets |

La capture statique finale a été inspectée à sa résolution originale : arbres proches/moyens, herbe,
arbustes, fougères, HLOD et ombres sont visibles sans géométrie catastrophique. Le masque temporel a
également été inspecté. Le statut exact reste `PASS_BY_CODEX_NOT_HUMAN_COMPARATIVE_REVIEW` : les
formes et couleurs sont celles d'un prototype procédural et ne remplacent pas une revue artistique.

La comparaison technique obligatoire avec la capture joueur précédente
`artifacts/forge-runtime/gate1/r1-027-031-player-camera/player-camera-3p.png` a aussi été faite à
1920 × 1080. Son SHA-256 est
`87eaac2141ce8d253787db5b292e457be2123f96b020894e77593b296f82159f` pour 1 656 111 octets. Les deux
images conservent le même terrain, la même capsule et un cadrage à hauteur humaine comparable. La
capture précédente ne contient aucune végétation ; la nouvelle rend arbres, sous-bois, herbe, HLOD
et leurs ombres. L'overlay passe de 48 486 à 69 869 primitives opaques et de 52 996 à 107 835
invocations vertex. Cette comparaison démontre l'ajout fonctionnel de la tranche ; elle ne juge pas
la supériorité artistique et ne remplace pas la revue humaine Gate 1.

## Smoke métrique — pas un benchmark Gate 1

Le ledger de la capture finale contient 120 échantillons en profil `dev`, sans VSync, sur RX 7800 XT :

| Mesure | Valeur |
|---|---:|
| moyenne / p95 / p99 / maximum | 4,392394 ms / 6,394 ms / 6,9729 ms / 34,3255 ms |
| moyenne FPS / 1 % low | 227,67 / **48,43 FPS** |
| hitches > 16,7 ms / > 33,3 ms | 1 / 1 |
| primitives opaque GPU | 69 869 |
| invocations vertex / fragment opaques | 107 835 / 2 636 043 |
| draw calls GPU | `null` — compteur global indisponible |

Cette fenêtre de 527 ms est seulement un smoke technique. Même si le 1 % low ponctuel dépasse 45 FPS,
la fenêtre est trop courte, un hitch dépasse 33,3 ms, la machine n'est ni une RTX 3060 ni une RX 6600,
et les profils qualité et le préchauffage final n'existent pas encore. Par conséquent, ni `R1-037`,
ni `R1-041`, ni `R1-042`, ni aucune condition de performance Gate 1 ne sont validés par ces nombres.

La régression Gate 0 séparée possède 120 échantillons, un p95 de 6,8443 ms et un 1 % low de 59,70 FPS.
Son ledger ne contient aucune section optionnelle Visual Valley, ombre ou végétation et conserve
`gpu_draw_calls = null`.

## Ombres observées et limite de portée

Le runtime a relu le composant du soleil réel : un soleil unique, shadow maps actives, quatre bornes
CSM `[24,0 ; 41,931866 ; 73,26172 ; 128,00003] m`. Les **704** représentations proches d'arbres sont
les casters prévus ; les **3 342** autres entités de projection désactivent explicitement leurs ombres.

La valeur 128 m qualifie la portée maximale du CSM de la scène, pas une distance radiale calculée par
caster. De plus, les arbres moyens et HLOD ne projettent pas d'ombre selon ADR-0007. Le rapport ne
prétend donc pas que chaque arbre possède une coupure exacte à 128 m.

## Provenance et indépendance Cadle

Le commit contient exactement cinq fichiers Rust et un shader WGSL : deux ajouts et quatre
modifications, **2 932 insertions et 43 suppressions**. Aucun asset, binaire, manifeste Cargo,
lockfile, dépendance déclarée, URL ou média externe n'est ajouté. Le shader est un fichier Forge local
qui importe uniquement `bevy_pbr`.

Un audit lexical du diff et une comparaison exacte des lignes normalisées d'au moins 40 caractères
avec le snapshot Cadle épinglé n'ont trouvé aucun chevauchement, nom propre, lieu, quête, marque ou
lore Cadle identifiable. Un test construit volontairement le terme Cadle par concaténation afin de
vérifier son absence du shader ; le rapport n'emploie donc pas l'affirmation absolue « zéro référence
textuelle ». Cette vérification établit l'absence de copie textuelle évidente et de contenu Cadle
identifiable, pas une expertise juridique exhaustive de provenance algorithmique.

## CI distante — exception ADR-0006

Le push du commit code a déclenché le run GitHub Actions
[`33426239438`](https://github.com/Karenax/OpenRPG_Forge/actions/runs/33426239438), ciblant exactement
`1f54b49bef7f086ef72d4f025c5059ec1860780d`. Les quatre jobs possèdent zéro étape et la même
annotation GitHub : le job n'a pas démarré en raison des paiements récents ou du plafond de dépense.

| Job | ID | Étapes exécutées | Classification Forge |
|---|---:|---:|---|
| `supply-chain` | `99600381596` | 0 | `NOT RUN — infrastructure billing` |
| `native (ubuntu-24.04)` | `99600381823` | 0 | `NOT RUN — infrastructure billing` |
| `wasm` | `99600381883` | 0 | `NOT RUN — infrastructure billing` |
| `native (windows-2025)` | `99600382016` | 0 | `NOT RUN — infrastructure billing` |

Conformément à la dérogation explicite et à ADR-0006, cette absence de CI distante ne bloque pas le
verdict local jusqu'à Gate 4. Elle n'est ni une CI verte, ni un échec produit, ni une preuve de
portabilité distante.

## Limites et suite obligatoire

- Le nombre réel de draw calls reste inconnu ; `R1-037` exige une instrumentation exhaustive ou une
  capture GPU reproductible conforme à ADR-0004.
- L'écart d'origine spatiale entre les entités individuelles et le mesh HLOD de chunk peut produire
  du popping. La complémentarité numérique est testée, mais la transition en mouvement reste à juger.
- La paire de vent est une preuve temporelle, pas une horloge de capture déterministe ni une revue de
  qualité artistique. Le pipeline shader WebGPU n'est pas encore lancé, donc `R1-014` reste ouvert.
- Les ombres lointaines sont volontairement désactivées sur medium, herbe et HLOD ; la capture ne
  mesure pas une coupure radiale par caster.
- Le smoke RX 7800 XT court ne qualifie aucune performance Gate 1. Le benchmark 1080p High sur RTX
  3060 ou RX 6600 reste obligatoire.
- Les revues humaines matériaux/composition, la comparaison avec la base historique, la comparaison
  A/B Cadle, les captures et la vidéo finales restent ouvertes.
- Atmosphère, eau, pluie, wetness, bloom/color grading, profils qualité, viewport Studio et rapport
  final Gate 1 restent à livrer.
