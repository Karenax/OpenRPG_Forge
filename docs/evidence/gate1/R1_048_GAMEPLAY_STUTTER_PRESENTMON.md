# R1-048 — Diagnostic gameplay continu avec PresentMon

Date : **2026-09-02**
Route : **`R1-048` OUVERTE — FAIL sur les seuils de diagnostic**
Gate : **Gate 1 OUVERTE ; Gate 2 INTERDITE**

## Décision

Le lot de cadence est publié sur `Rust`, validé localement et indépendant des assets Cadle. Le ledger
runtime enregistre une route gameplay continue sans pause de 1 800 ticks en 29,997926 s. Séparément,
l'hôte retient 719 lignes PresentMon pour le PID 2864 à partir d'une borne QPC basse, sans borne haute
issue du runtime. Cette sélection externe échoue nettement aux seuils d'affichage, mais ne qualifie pas
la même fenêtre exacte que le ledger. Le target Cargo partagé, l'absence de rebuild gouverné propre,
la frontière hôte ni vérifiée ni ingérée par le runtime, le GPU cible absent et la revue humaine non
réalisée maintiennent `R1-048` ouverte.

## Identité source et validation

| Élément | Valeur |
|---|---|
| branche distante vérifiée | `Rust` |
| commit | `15635b3f85b623654e498b17ed299674928313e6` |
| arbre | `187ae357e24c47403b9c638c0a0737ed01215e33` |
| sujet | `perf(gate1): bound evidence refresh cadence` |
| runtime SHA-256 | `65f0c24b76c644ddd2a4eef8e772c4b881d7e5f7000cd4465b190edc3ee1dd50` |
| tests moteur Release | **140/140 PASS** |
| tests runtime Release | **52/52 PASS** |
| test d'intégration Release | **1/1 PASS** |
| Clippy strict | `cargo clippy --release --locked -p forge_engine -p forge_runtime --all-targets --all-features -- -D warnings` — **PASS** |
| CI distante | [run `33584774630`](https://github.com/Karenax/OpenRPG_Forge/actions/runs/33584774630), **NOT RUN — infrastructure billing**, dérogation ADR-0006 ; jamais renommée PASS |

Le commit borne le rafraîchissement de l'overlay, audite la visibilité de la végétation à un intervalle
de 100 ms et exige des reçus monotones avant de déclarer les frontières initiale et finale valides.
Ces changements réduisent le coût de la preuve interne sans cadencer le contrôleur, la simulation,
le rendu ou PresentMon.

## Provenance et frontière Cadle

L'audit exact porte le SHA-256
`e36bab2cceeac3a32902d2a920cca95e45a288b7c8c748234b8eb5556dc601c0` et retourne **PASS** :

| Contrôle | Résultat |
|---|---:|
| fichiers Forge suivis | 378 |
| assets gouvernés | 27 |
| modèles | 9 |
| licence | `CC0-1.0` |
| fichiers Cadle hashés | 507 |
| fichiers Forge comparés | 39 |
| correspondances exactes | 0 |
| correspondances de lignes normalisées | 0 |
| payload Cadle suivi | 0 |

La référence Cadle reste `ea0da62549980816a6a27e08b75808b1f56d70e7`, arbre
`f0d52e434454fe302052d22eafde203bc361cc1f`. Aucun asset Cadle n'est copié dans Forge.

## Protocole observé

Artefacts externes non versionnés :

```text
E:\OpenRPG_Forge\.gate1-tools\presentmon\diagnostics\cadence-15635b3-final-20260902T030459251Z
```

La campagne utilise le runtime Release identifié ci-dessus en 1920 × 1080 High, VSync désactivé,
avec une AMD Radeon RX 7800 XT, Vulkan et le pilote `26.8.1 (LLPC)`. PresentMon 2.4.1 est épinglé par
le SHA-256 `d74183e7ae630f72cd3690be0373ecbfdc6cbb86578148aab8fa2a7166068f34`.

### Ledger runtime

| Invariant runtime | Observation |
|---|---:|
| durée murale | 29,997926 s |
| ticks fixes | 1 800, sans catch-up |
| frames main-world couvertes | 3 672 |
| contrôles d'invariants main-world | 3 674 |
| reçu visibilité initial | ticket 1 / complété 1 |
| reçu visibilité final | ticket 2 / complété 2 |
| fenêtre native observée | oui |
| capture sans pause | oui |
| screenshots pendant la fenêtre | non |

Les comptes main-world sont des observations de la boucle applicative, pas des frames présentées. Ils
ne peuvent donc pas être convertis en FPS d'affichage. Cette durée et ces comptes appartiennent au
ledger runtime ; ils ne bornent pas automatiquement la sélection PresentMon hôte.

### Sélection PresentMon hôte

| Frontière hôte | Observation |
|---|---|
| PID ciblé | 2864 |
| fréquence QPC déclarée | 10 000 000 Hz |
| borne basse hôte | `measurement_not_before_qpc = 6416754521443` |
| filtre hôte | `ProcessID == 2864` et `CPUStartQPC >= measurement_not_before_qpc` |
| borne haute issue du runtime | **aucune** |
| lignes retenues | 719 |
| frontière vérifiée par le runtime | non, `runtime_verified_capture=false` |
| métriques ingérées par le runtime | non, `display_metrics_ingested=false` |

La sélection hôte établit une chronologie externe après la borne basse. Sans borne QPC haute fournie
par le runtime, elle ne prouve pas que ses 719 lignes coïncident exactement avec les 29,997926 s du
ledger. Cette frontière hôte n'est ni vérifiée ni ingérée par le runtime.

## Statut non qualifiant

Le fichier de synthèse pose `qualifying=false` pour deux raisons explicites :

- target Cargo partagé réutilisé ;
- rebuild gouverné en target propre non effectué.

Le ledger runtime conserve également `runtime_verified_capture=false` et
`display_metrics_ingested=false`. Seule la borne basse `CPUStartQPC >= measurement_not_before_qpc`
est appliquée par l'hôte ; aucune borne haute runtime n'existe. La frontière hôte n'est donc ni
vérifiée ni ingérée, et aucune qualification « exact QPC » n'est revendiquée.

## Résultats PresentMon

La capture contient 759 lignes CSV. L'hôte en retient 719 pour le PID 2864 avec
`CPUStartQPC >= measurement_not_before_qpc`, sans borne haute runtime ; il s'agit d'une sélection de
diagnostic, pas de la fenêtre runtime exacte. Trente-neuf lignes n'ont pas de DisplayedTime résolu ;
680 présentations sont donc utilisables pour le diagnostic d'affichage.

| Mesure | Nombre | Moyenne | p95 | p99 | Maximum |
|---|---:|---:|---:|---:|---:|
| FrameTime | 719 | 41,8492 ms | 62,7438 ms | 76,7229 ms | non consolidé |
| CPUBusy | 719 | 41,7673 ms | 62,6766 ms | 76,6439 ms | non consolidé |
| GPUTime | 719 | 5,9255 ms | 8,4525 ms | 8,9543 ms | non consolidé |
| DisplayedTime | 680 | 44,2516 ms | 62,4861 ms | 76,4441 ms | 83,3786 ms |

Le 1 % low calculé sur DisplayedTime est **12,5912 FPS**. Avec 680 valeurs résolues, le centile lent
contient `ceil(680 × 1 %) = 7` valeurs ; la formule appliquée est **`1000 / moyenne des 7
DisplayedTime les plus lents`**.

| Seuil de stutter | Dépassements | Séquence maximale | Verdict |
|---|---:|---:|---|
| `>16,7 ms` | 672/680 | 87 | **FAIL** |
| `>33,3 ms` | 600/680 | 64 | **FAIL** |
| p95 `<=16,7 ms` | p95 62,4861 ms | — | **FAIL** |
| 1 % low `>=45 FPS` | 12,5912 FPS | — | **FAIL** |

Ces nombres sont un diagnostic externe utile, pas un benchmark gouverné accepté. Ils ne doivent pas
être remplacés par les 3 672 frames main-world ni décrits comme provenant de la même fenêtre exacte.

## Frontière d'instrumentation

| Signal | Statut |
|---|---|
| diagnostics de rendu Bevy | désactivés pour cette route |
| encodeur de résolution des diagnostics Bevy | désactivé |
| temps CPU internes des passes | `not_observed` |
| temps GPU internes des passes | `not_observed` |
| statistiques de pipeline internes | `not_observed` |
| compteur runtime de frames présentées | `not_observed` |
| timing update applicatif | `observed_non_presentation` |
| chronologie d'affichage | `external_presentmon_displayed_time` |

`GPUTime` est fourni par PresentMon et ne ferme pas `R1-037` : aucun inventaire exhaustif des draw
calls, triangles ou instances n'est acquis. La RX 7800 XT est le GPU de développement ; elle ne ferme
pas `R1-041`, qui exige encore une RTX 3060 ou RX 6600.

## CI distante — exception ADR-0006

Le [run officiel Rust gate `33584774630`](https://github.com/Karenax/OpenRPG_Forge/actions/runs/33584774630)
cible le commit exact `15635b3f85b623654e498b17ed299674928313e6`. Il expose quatre jobs — `wasm`,
`supply-chain`, `native (ubuntu-24.04)` et `native (windows-2025)` — avec **0 étape exécutée** et
aucun runner alloué. La conclusion GitHub `failure` décrit le blocage d'infrastructure/facturation,
pas un échec du code : la qualification reste **NOT RUN — infrastructure billing** sous ADR-0006.
Ce run ne constitue ni une CI verte ni une validation distante du commit.

## Hashes des artefacts externes

| Artefact | SHA-256 |
|---|---|
| `diagnostic-summary.json` | `cde4180dffd374c9f6e4bfa9a3003077287da63385ca34d9b07611fc9e129122` |
| `gate1-gameplay-stutter.json` | `219826f34ceef3bddcba85497684d759d4e1966fb02d351f99022b9bdc314f4e` |
| `gate1-gameplay-stutter.ready.json` | `3eb2d6030cb14b640a711802d49cf3f70ce92bfa4c642274ff25205dfe76dd47` |
| `gate1-gameplay-stutter.start.signal.json` | `89894a4c7572ed9f9abd3836d0b6204e6607508f97b2d376f91096b0abdd7a61` |
| `presentmon.csv` | `3468476ee8e574ff4f901adf63abe23cb7a9c107b2f3d9b7d2838143691b6f97` |
| `presentmon.raw.stdout.log` | `7b166da922a1cbee22d73b074fe7f45845286ac171df9ea47763590aa39b607e` |

Les artefacts bruts restent externes afin de ne pas alourdir ou republier une campagne non
qualifiante. La présente synthèse est la preuve versionnée incluse dans le commit documentaire ; elle
en fixe l'identité, les limites et les résultats sans modifier les preuves historiques.

## État final

- `R1-048` : **OUVERTE — FAIL seuils dans la campagne diagnostique non qualifiante** ;
- `R1-037` : **OUVERTE** ;
- `R1-041` : **OUVERTE** ;
- revue humaine : **non réalisée** ;
- Gate 1 : **OUVERTE** ;
- Gate 2 : **INTERDITE** sans passage formel de Gate 1 ou nouvelle dérogation explicite acceptée ;
- CI distante : **NOT RUN** sous ADR-0006.

La prochaine acquisition qualifiante doit reconstruire dans un target gouverné propre, faire ingérer
et vérifier la frontière QPC exacte par le runtime, conserver la route sans pause, satisfaire les
seuils de présentation, puis être complétée par le benchmark GPU cible et la revue humaine.
