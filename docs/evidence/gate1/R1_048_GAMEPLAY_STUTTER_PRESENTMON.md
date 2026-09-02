# R1-048 — Diagnostic gameplay continu avec PresentMon

Date : **2026-09-02**
Route : **`R1-048` OUVERTE — FAIL sur les seuils de diagnostic**
Gate : **Gate 1 OUVERTE ; Gate 2 INTERDITE**

## Décision

La baseline Release non instrumentée `6f0e519afd01a701dd4d772d0d3837dd5cbf9814`, arbre
`cfbe45ecd3464c16f47ef1b2da2bf7fd5b78d9c7`, fournit trois runs `AutoNoVsync` valides du point de
vue de l'infrastructure. Ils échouent tous aux seuils : 21,3180 à 21,5263 FPS moyens, p95 de
69,4547 à 69,4584 ms, 1 % low de 6,9036 à 7,0982 FPS, 16 à 27 présentations non affichées, 546 à
556 présentations au-dessus de 33,3 ms et 65 à 116 frames consécutives au-dessus de 16,7 ms. Les
trois variantes FIFO échouent également ; aucune proposition de pacing n'est retenue. L'analyse
`presentation-ab-analysis.json`, SHA-256
`5026e75d1b47824a47381bd05ab8ae273111948b87d6ffbc804f0cecab81dd3e`, porte donc le verdict
**FAIL**, `qualifying=false` et `closes_gate=false`.

Le lot source courant `06b5fd404bb4c3918b4b7ea154c8d7878609fa8d`, arbre
`a195b2b865c45502f3b412a43bfb20f4d53f9491`, ne modifie ni la durée de mesure, ni les seuils, ni
les schémas qualifiants. Il ajoute uniquement un reçu JSON atomique et explicitement
`qualifying=false` lorsque le runtime expire avant l'état prêt. Le smoke DX12/FXC identifie ainsi la
cause exacte : à 1 200 ticks, les huit autres prédicats sont vrais et seul `pipeline_ready=false`, avec
40 pipelines prêts et 29 encore en création. Avec le DXC Microsoft signé du SDK Windows, les 69
pipelines deviennent prêts, mais PresentMon 2.4.1 perd massivement les événements ETW et ne produit
aucune ligne pour le processus. Ces essais DX12 sont des diagnostics d'infrastructure sans métrique de
performance, pas une requalification.

Le diagnostic opt-in historique `2fe48485086e925422c667f49c38a80f96e1a81a` reste conservé, mais
n'est plus la source courante. `R1-037`, `R1-041` et `R1-048` restent ouvertes,
`human_review_performed=false`, Gate 1 reste ouverte et Gate 2 reste interdite.

## Historique — identité source, opt-in et validations `2fe48485`

| Élément | Valeur |
|---|---|
| commit opt-in | `2fe48485086e925422c667f49c38a80f96e1a81a` |
| arbre | `378f13f68e7689a6790aa544f1e5b4a1be30ad16` |
| sujet | `chore(gate1): allow marked route profiling` |
| feature | `render-profile`, opt-in et explicitement non qualifiante |
| runtime Release | 90 245 632 octets ; SHA-256 `e03bd61db8b5f8cf418390a5a78f3b5dc7b4e94dfb123ee6668abcf0d8e755cd` |
| marqueur | schema 1, `qualifying=false` ; SHA-256 `5f365295110fca0a1f47c5419f05e6a1ff1c33d1fe29619a27f4c9f9c34b3e48` |
| provenance | **PASS** ; 915 octets ; SHA-256 `22c1d795f94eb8f247e1b7c173ae06602ff17dd664f0c8d0ef2f4aea4a383892` |

| Validation locale du commit opt-in | Résultat |
|---|---:|
| check `forge_runtime`, configuration normale | **PASS** |
| check `forge_runtime`, feature `render-profile` | **PASS** |
| Clippy strict `forge_runtime --all-targets`, configuration normale | **PASS** |
| Clippy strict `forge_runtime --all-targets --all-features` | **PASS** |
| tests unitaires ciblés | **2/2 PASS** |
| guard d'intégration `render_profile_guard` | **4/4 PASS** |
| formatage et `git diff --check` | **PASS** |

Ces validations bornées prouvent le contrat d'opt-in et son garde-fou. Elles ne transforment pas la
capture instrumentée en benchmark Release qualifiant et ne constituent pas un rejeu workspace complet.
L'audit exact compte 380 fichiers suivis, 27 assets et neuf modèles ; il compare 39 fichiers Forge aux
507 fichiers Cadle et trouve zéro copie exacte comme zéro ligne normalisée commune. Aucun asset Cadle
n'est copié dans Forge.

## Protocole observé `2fe48485`

Artefacts externes non versionnés :

```text
E:\OpenRPG_Forge\.gate1-tools\presentmon\diagnostics\2fe48485-render-profile-20260902T060816358Z
```

La campagne utilise le runtime Release identifié ci-dessus sur une AMD Radeon RX 7800 XT, Vulkan et le
pilote `26.8.1`. Le processus sort avec le code 0, charge 9/9 modèles gouvernés et ses logs runtime
contiennent zéro warning et zéro erreur. La revue humaine reste `false`.

### Ledger runtime `2fe48485`

| Invariant runtime | Observation |
|---|---:|
| durée murale | 29,995543 s |
| ticks fixes | 1 800, dont 2 ticks de catch-up |
| commande gameplay | 900 ticks avant + 900 ticks arrière |
| frames main-world couvertes | 2 608 |
| contrôles d'invariants main-world | 2 609 |
| mouvement réel | oui ; 1 800 ticks grounded, 1 059 avec collision |
| modèles gouvernés chargés | 9/9 |
| fenêtre native observée | oui |
| sortie runtime | code 0 |
| logs runtime | 0 warning ; 0 erreur |
| revue humaine | `false` |

Le ledger conserve `runtime_verified_capture=false`, `display_metrics_ingested=false` et
`gate1_route_qualified=false`. Ses 2 608 frames main-world ne sont pas des présentations et ne doivent
pas être converties en FPS d'affichage.

### Sélection PresentMon hôte `2fe48485`

| Frontière hôte | Observation |
|---|---|
| PID ciblé | 30 780 |
| fréquence QPC déclarée | 10 000 000 Hz |
| borne basse hôte | `measurement_not_before_qpc = 6526742631575` |
| filtre hôte | PID 30 780 après la borne QPC basse |
| borne haute issue du runtime | **aucune** |
| lignes après borne basse | 2 184 |
| `DisplayedTime` non résolus | 738 |
| `DisplayedTime` résolus | 1 446 |
| frontière vérifiée par le runtime | non, `runtime_verified_capture=false` |
| métriques ingérées par le runtime | non, `display_metrics_ingested=false` |
| pertes ETW | 0 événement ; 0 buffer |

Sans borne QPC haute fournie par le runtime, les 2 184 lignes ne sont pas la même fenêtre exacte que
les 29,995543 s du ledger. La frontière hôte n'est ni vérifiée ni ingérée par le runtime.

## Résultats PresentMon `2fe48485`

| Mesure | Nombre | Moyenne | p95 | p99 | Maximum |
|---|---:|---:|---:|---:|---:|
| FrameTime | 2 184 | 13,8155 ms | 22,7558 ms | 50,3844 ms | 620,4266 ms |
| CPUBusy | 2 184 | 13,7076 ms | 22,6474 ms | 50,2758 ms | 620,3147 ms |
| GPUTime | 2 184 | 6,9942 ms | 11,3378 ms | 12,0886 ms | 13,0470 ms |
| DisplayedTime | 1 446 | 20,8579 ms | 34,7319 ms | 83,4934 ms | 625,0145 ms |

Le 1 % low calculé sur les `DisplayedTime` résolus est **4,1709 FPS**.

| Seuil de stutter | Dépassements | Verdict |
|---|---:|---|
| `>16,667 ms` | 680/1 446 | **FAIL** |
| `>33,333 ms` | 111/1 446 | **FAIL** |
| p95 `<=16,7 ms` | p95 34,7319 ms | **FAIL** |
| 1 % low `>=45 FPS` | 4,1709 FPS | **FAIL** |

Ces nombres restent un diagnostic externe non qualifiant, mais le verdict de seuil est sans ambiguïté :
**FAIL**. L'absence de borne haute/runtime ingestion empêche de les attribuer à la fenêtre exacte du
ledger ; elle ne permet pas d'effacer leur échec.

## Trace interne agrégée `2fe48485`

La trace `render-profile` de 6 073 239 151 octets est elle-même **NONQUALIFYING**. Deux passes de
22 604 221 lignes produisent 9 148 749 paires `B/E` complètes, zéro anomalie et huit scopes `B` ouverts
à la coupure, exclus des agrégats. Le parseur sort avec le code 0, sans stderr ; le fichier de trace et
le hash du parseur restent inchangés pendant l'analyse.

| Artefact | Octets | SHA-256 |
|---|---:|---|
| `gate1-gameplay-stutter.json` | 52 803 | `88134d3a80d5bc10de7443947ec3b42a37575e87b02654e751ec129e34386ec5` |
| `gate1-gameplay-stutter.ready.json` | 25 321 | `eab58f51cc22aca297753cdb26e034d3e3a7f08d91430204930a35f73b490682` |
| `gate1-gameplay-stutter.start.signal.json` | 566 | `8cada2b4116e9e7f62938c00d8ff2aa7a67f30416ba043d664404785445b8c6a` |
| `presentmon.csv` | 422 143 | `d78eb048e3688b9a719a0c4370297d17613f99c22092ad8ca792976ed262accd` |
| `presentmon.raw.stdout.log` | 425 117 | `561630f105c9f1c45dc9c6bbf878b84b0aad72736b692d9cc730798ed61b2046` |
| `presentmon.stderr.log` | 303 | `fa8cc09eba927931fa1670be29df1558c531367d416e48cd784acabfa3c77fa6` |
| `presentmon.stdout.log` | 2 974 | `253cbed43cdb12eea553fc45173d44e1bdd74663bea870d5e3417483d6dc90cf` |
| `presentmon.stop.log` | 190 | `c1f3939f2269ef460bbf912d77e38c66779ac26edee7db721201c142f7cb4f0d` |
| `runtime.stderr.log` | 5 445 | `801b20156bdc404fa9e69c44b08e8f2029cef2fc191c0193c93520201fa09e5a` |
| `runtime.stdout.log` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` |
| `trace.json` | 6 073 239 151 | `79763e8306818b7b4f9ba36886a012c7b1cfee10afd830a2f92146b73084fb71` |
| `trace_aggregate_summary.json` | 9 763 | `5a35dff595fa02dee801ea8c99b5d7b857d558d8d9219580c6a7c9c2b044657e` |
| `trace_aggregate_summary.md` | 5 042 | `28287a840f16528833baa4d3466d8c21608b5f93d8ebe221a1ab24508eba73bc` |
| `trace_aggregate_evidence.raw.json` | 123 395 | `9e9ee27a8522e13aa049f79d3c780df99fec832303593db2dc9e51938a2512f7` |
| parseur de référence | 32 179 | `64d3c90f4977c4f4ab7bec86a4af819a370587f59f1b8f4eb4b4591f155ef657` |
| `NONQUALIFYING_RENDER_PROFILE.json` | 327 | `5f365295110fca0a1f47c5419f05e6a1ff1c33d1fe29619a27f4c9f9c34b3e48` |
| `diagnostic-summary.json` | 2 767 | `4f20a58fa6d6a765778b60b38668f7005c2824f2c56d2cccd1b27333e84465b5` |

| Cadence stable et attribution | Valeur |
|---|---:|
| frame interne moyenne | 11,445115 ms, soit 87,3735 FPS équivalents profilés |
| frame interne p95 / p99 / max | 12,621560 / 13,256662 / 27,217700 ms |
| `schedule: name=Render` | 10,845757 ms inclusive ; 6,514969 ms exclusive |
| `Surface::get_current_texture` | 3,539034 ms exclusive |
| `prepare_windows` | 3,649687 ms inclusive |
| `main_render_schedule` | 3,006376 ms inclusive |
| `schedule: name=Core3d` | 1,738853 ms inclusive |
| `PostUpdate` | p95 4,676880 ms exclusive |
| `vkAcquireNextImageKHR` | 0,077400 ms moyenne |
| `vkQueueSubmit` | 0,049984 ms moyenne par appel |
| `vkQueuePresentKHR` | 0,143264 ms moyenne |

La cadence interne `2fe` suit de près la distribution à faible interférence `8b`. Cela aide à cibler
`Surface::get_current_texture` / `prepare_windows`, puis la queue `PostUpdate`, sans démontrer une
performance de production. Les appels Vulkan bruts ne sont pas le goulot récurrent observé. Surtout,
11,445115 ms dans la trace et 20,8579 ms de `DisplayedTime` moyen PresentMon décrivent des frontières et
des observables différents : leur divergence reste **non réconciliée**. Le 87,3735 FPS équivalent est
NONQUALIFYING et ne remplace ni le FAIL PresentMon, ni un benchmark GPU cible, ni la revue humaine.

## Campagne historique publiée `15635b3f` — identité et validation

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

## Provenance et frontière Cadle — historique `15635b3f`

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

## Protocole observé — historique `15635b3f`

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

## Statut non qualifiant — historique `15635b3f`

Le fichier de synthèse pose `qualifying=false` pour deux raisons explicites :

- target Cargo partagé réutilisé ;
- rebuild gouverné en target propre non effectué.

Le ledger runtime conserve également `runtime_verified_capture=false` et
`display_metrics_ingested=false`. Seule la borne basse `CPUStartQPC >= measurement_not_before_qpc`
est appliquée par l'hôte ; aucune borne haute runtime n'existe. La frontière hôte n'est donc ni
vérifiée ni ingérée, et aucune qualification « exact QPC » n'est revendiquée.

## Résultats PresentMon — historique `15635b3f`

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

## Frontière d'instrumentation — historique `15635b3f`

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

## Diagnostics postérieurs à `2fe48485`

### Baseline Release et matrice de pacing `6f0e519a`

Le binaire Release non instrumenté de 82 328 064 octets, SHA-256
`c20c2b727b5103f677aaea0ea04984c214526e0455487f30865200d6974ece0a`, est lié au commit exact
`6f0e519afd01a701dd4d772d0d3837dd5cbf9814`. La campagne contrebalancée `AB/BA/AB` contient trois
runs `AutoNoVsync` et trois runs FIFO. Les six acquisitions sont structurellement valides, leurs
identités scène/GPU/OS/affichage concordent et leurs signatures de présentation sont stables. Les
trois runs de baseline `AutoNoVsync` donnent :

| Mesure, unité par run | Minimum | Médiane | Maximum |
|---|---:|---:|---:|
| FPS affichés moyens | 21,3180 | 21,4731 | 21,5263 |
| p95 affiché | 69,4547 ms | 69,4566 ms | 69,4584 ms |
| 1 % low | 6,9036 FPS | 6,9289 FPS | 7,0982 FPS |
| présentations non affichées | 16 | 19 | 27 |
| présentations au-dessus de 33,3 ms | 546 | 554 | 556 |
| maximum consécutif au-dessus de 16,7 ms | 65 | 88 | 116 |

Les trois runs FIFO échouent eux aussi et ne sont meilleurs à la fois sur le p95 et le 1 % low que
dans une paire sur trois. La matrice refuse donc tout changement de pacing. Elle est diagnostique,
`qualifying=false`, `closes_gate=false`, et son verdict est **FAIL**.

### Reçu de timeout reproductible `06b5fd40`

Le commit `06b5fd404bb4c3918b4b7ea154c8d7878609fa8d`, arbre
`a195b2b865c45502f3b412a43bfb20f4d53f9491`, ajoute uniquement le schéma
`forge.gate1-gameplay-stutter-readiness-timeout` v1. L'écriture est atomique, réservée au chemin
d'échec avant l'état prêt et porte toujours `qualifying=false`. Le binaire Release exact mesure
82 333 696 octets, SHA-256
`b1f0f17bb2499a798445e78d49dfb57db8182a2896dd36f13c48ed44360e60ab`. Les deux tests Release
ciblés `readiness_timeout` passent, Clippy Release strict passe, le formatage et le diff-check passent.

Le smoke réel DX12/FXC
`dx12-fxc-readiness-timeout-06b5fd4-20260902T131450568Z` expire à 1 200 ticks avant le lancement de
PresentMon. Les huit prédicats scène/qualité/GPU/profil joueur/ancrage/caméra/binding sont vrais ; seul
`pipeline_ready=false`. Le cache rapporte 69 pipelines : 40 prêts, 29 en création, zéro en file et
zéro erreur. Le reçu de 1 548 octets porte le SHA-256
`77e96f54e5a4aa8ddfbb5d42cc20a079efcea47abb0851d3a59b2074d01c179b`. Il prouve que le timeout
DX12/FXC est une attente de compilation de pipelines, sans fournir de verdict de performance.

### Routes DXC et invalidation ETW

Le prototype DXC statique local `a59f560c` n'est pas promu : son build atteint l'édition de liens puis
échoue avec `LNK1104` sur `atls.lib`, composant ATL/MFC absent de Visual Studio Build Tools. Aucun
binaire ni aucune métrique n'en résultent. La route dynamique emploie à la place le DXC Microsoft
signé du SDK Windows `10.0.26100.0` : `dxc.exe` v1.8.2502.11, SHA-256
`7c6918a0e2d4e437629fa8549f5ce800970494780f363bbbe1e3d3034f435aee`, `dxcompiler.dll` SHA-256
`78b5e21a91707b4a567ef281a519c52a45b709561202f54c5ad79c5a315847ed` et `dxil.dll` SHA-256
`77e039c905030a641e53658a008b74e90635a5ea9b6b79eabd0f2003bdfca59a`.

Trois pilotes dynamiques atteignent l'état prêt avec 69/69 pipelines entre les ticks 209 et 294. Aucun
ne produit de ligne PresentMon qualifiable, de signal de départ, de ledger ou de synthèse :

| Pilote externe | Reçu prêt SHA-256 | Observation PresentMon |
|---|---|---|
| standard | `1797ba6fd7af11c50f873ac8f903f0f825e83b541d22ad1900a728be11a49e65` | zéro ligne ; 263 726 événements ETW perdus |
| sans suivi input | `87d0f0fe56d4c7cfde0bd8790c2bbb33f9d8b0f84ab13b2952de66622a165952` | zéro ligne ; sortie brute vide |
| affichage seul | `53b4a33bc6695430fa8d044c25b1fe94b13ba7554f9fd4a5c890b3c39295116f` | zéro ligne ; 261 138 événements ETW perdus |

Un probe séparé avec le PresentMon signé livré par AMD reproduit le défaut : 72/72 pipelines prêts,
zéro ligne et 58 122 événements ETW perdus en cinq secondes. Le taux de remplissage publié par
PresentMon décrit le nombre de buffers ETW utilisés, pas leur remplissage en octets ; le binaire 2.4.1
épinglé n'expose aucune option CLI pour la taille ou le nombre de buffers. L'élévation n'apporte que
des privilèges de métadonnées processus et ne constitue pas un correctif démontré. Ces essais sont
donc classés **INVALIDES — acquisition ETW**, sans comparaison de performance.

Un prototype jetable a épinglé l'upstream officiel PresentMon `v2.4.1` au commit vérifié
`43bd033364154bd5eade7c11f6b0364a12380deb`, arbre
`4fbde5ec7fa97676079ac363e0a6f682c26cbb29`, licence MIT. Son patch externe borne le test à des
buffers ETW de 128 KiB, minimum 512 et maximum 1 024 ; `git diff --no-ext-diff --binary` porte le
SHA-256 `5041bead4cb9cbd16bf665dd8ea8c0213191d009f8a6277b05c6cdacfc3b0835`. L'unique build x64
Release échoue avant le link : les dépendances vcpkg `cli11`, `concurrentqueue`, `cereal` et
`boost-asio` ne sont pas restaurées. Aucun exécutable, aucune signature et aucun A/B n'existent ; ce
prototype reste externe, non promu et sans effet sur le verdict.

## CI distante historique — exception ADR-0006

Le [run officiel Rust gate `33584774630`](https://github.com/Karenax/OpenRPG_Forge/actions/runs/33584774630)
reste attaché au commit exact `15635b3f85b623654e498b17ed299674928313e6`, pas aux commits
`6f0e519afd01a701dd4d772d0d3837dd5cbf9814` ou
`06b5fd404bb4c3918b4b7ea154c8d7878609fa8d`. Il expose quatre jobs — `wasm`,
`supply-chain`, `native (ubuntu-24.04)` et `native (windows-2025)` — avec **0 étape exécutée** et
aucun runner alloué. La conclusion GitHub `failure` décrit le blocage d'infrastructure/facturation,
pas un échec du code : la qualification reste **NOT RUN — infrastructure billing** sous ADR-0006.
Ce run ne constitue ni une CI verte ni une validation distante du commit.

## Hashes des artefacts externes — historique `15635b3f`

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

- `R1-048` : **OUVERTE — FAIL sur la baseline `6f0e519a`** ;
- `R1-037` : **OUVERTE** ;
- `R1-041` : **OUVERTE** ;
- baseline Release `6f0e519a` : trois runs `AutoNoVsync` structurellement valides, verdict **FAIL** ;
- reçu de timeout `06b5fd40` : **PASS fonctionnel**, diagnostic d'échec uniquement, sans métrique de
  performance ;
- DX12/FXC : attente de compilation prouvée, 40/69 prêts et 29 en création à 1 200 ticks ;
- DX12/DXC dynamique : 69/69 prêts, acquisition PresentMon **INVALIDE** par pertes ETW massives ;
- DXC statique `a59f560c` : **NON PROMU**, build interrompu par l'absence d'ATL/MFC ;
- campagne opt-in `2fe48485` : **NONQUALIFYING**, trace interne proche de `8b` mais divergence
  trace/PresentMon non réconciliée ;
- campagne historique `15635b3f` et ses hashes : **préservés** ;
- revue humaine : **non réalisée**, `human_review_performed=false` ;
- Gate 1 : **OUVERTE** ;
- Gate 2 : **INTERDITE** sans passage formel de Gate 1 ou nouvelle dérogation explicite acceptée ;
- CI distante `33584774630` attachée à `15635b3f` : **NOT RUN** sous ADR-0006 ;
- asset Cadle copié dans Forge : **aucun**.

La prochaine acquisition qualifiante doit employer un runtime Release normal dans un target gouverné
propre et une capture d'affichage sans perte ETW. Une instrumentation `tracing` interne, bornée et
explicitement non qualifiante peut corréler les appels `Surface::present` et backend
`IDXGISwapchain3::Present`/`vkQueuePresentKHR`, mais ne doit jamais être appelée temps d'affichage ni
remplacer PresentMon. La route doit ensuite satisfaire les seuils de présentation et être complétée par
le benchmark GPU cible et la revue humaine.
