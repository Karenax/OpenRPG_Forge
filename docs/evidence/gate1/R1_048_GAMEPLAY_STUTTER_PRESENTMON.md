# R1-048 — Diagnostic gameplay continu avec PresentMon

Date : **2026-09-02**
Route : **`R1-048` OUVERTE — cadence conforme sur GPU de développement ; requalification gouvernée, GPU cible et revue humaine dus**
Gate : **Gate 1 OUVERTE ; Gate 2 INTERDITE**

## Décision

Le lot source courant est `9db35a8e125c6c60cde9de66a7f6aa7585215bfa`, arbre
`656feb155c4a30371210229bf6c5dea5bbf8fcd3`. Son rebuild Release normal, dans un target Cargo propre,
atteint **128,7260 FPS affichés**, un p95 de **13,9112 ms**, un 1 % low de **58,3489 FPS**, zéro
intervalle au-dessus de 33,3 ms et au plus un intervalle consécutif au-dessus de 16,7 ms. La capture
exacte ne perd aucun événement ni buffer ETW. Elle contient toutefois 17 présentations non affichées ;
le validateur schema 7 reste donc `runtime_qpc_display_overlap_thresholds_failed` et
`gate1_qualified=false` sous son ancien prédicat « zéro `NA` ». ADR-0008 clarifie que ce compteur
reste obligatoire et diagnostique, mais qu'il n'est pas à lui seul un stutter lorsque la résidence
d'affichage est continue et que tous les seuils visibles passent. Une nouvelle acquisition schema 8
sur commit exact est donc requise avant de retenir ce résultat automatisé. Le gain de cadence remplace
le défaut de la baseline historique `6f0e519a`, mais ne ferme pas `R1-048`.

Deux répétitions du même binaire comptent respectivement 16 et 21 présentations non affichées. Le
contrôle FIFO en compte 19. Un A/B diagnostique avec le binaire officiel PresentMon 2.5.1 en compte
16 et reproduit pratiquement la même distribution temporelle. Les métriques de cadence sont donc
répétables. Les `DisplayedTime=NA` ne peuvent être effacés ; ADR-0008 les sépare du verdict stutter
uniquement lorsque les invariants de résidence, d'ETW, de fenêtre et de cadence passent tous.

La cause des captures ETW vides et massivement perdues antérieures a été isolée : une session
PresentMon orpheline était encore active. Elle a été arrêtée par son identifiant exact, sans arrêt
générique ni suppression d'une session étrangère, puis son absence a été vérifiée avant la reprise.
Cette correction invalide l'ancienne hypothèse d'un simple défaut de buffers ETW ; elle ne transforme
pas les essais DX12 historiques en mesures de performance.

Le diagnostic opt-in historique `2fe48485086e925422c667f49c38a80f96e1a81a` et la baseline
`6f0e519afd01a701dd4d772d0d3837dd5cbf9814` restent conservés comme historique. `R1-037`, `R1-041`
et `R1-048` restent ouvertes. La RX 7800 XT de développement ne remplace pas la RTX 3060/RX 6600
cible, `human_review_performed=false`, aucune CI distante n'a exécuté le commit courant et aucun
asset Cadle n'est copié dans Forge. Gate 1 reste ouverte et Gate 2 reste interdite.

## Lot courant `2bf5e44f` → `e1e818fc` → `9db35a8e`

### Cause racine ETW et récupération ciblée

La session orpheline
`OpenRPGForge-Gate1-Diagnostic-85a698103aaf4186a480beabd1840099`, issue du diagnostic externe
`cpu-util-vulkan-6f0e519a-20260902T112247353Z`, restait enregistrée après la fin de son processus.
Elle coexistait avec les captures suivantes et expliquait leurs pertes ETW massives ainsi que leurs
zéro lignes cible. L'outil officiel épinglé a reçu uniquement :

```text
--session_name OpenRPGForge-Gate1-Diagnostic-85a698103aaf4186a480beabd1840099
--terminate_existing_session
```

La commande sort avec le code 0. Une interrogation ETW suivante confirme l'absence de cet identifiant.
Aucune autre session n'est arrêtée. Le rejeu propre
`vulkan-presentation-profile-clean-etw-2bf5e44f-20260902T152400224Z` reçoit 11 lignes cible, dont
sept avant le signal, et zéro perte ETW. Il échoue ensuite pour une raison indépendante et explicite :
les noms de systèmes Bevy nécessaires au profil n'étaient pas exposés dans cette configuration.

### Portée des trois commits

| Commit | Changement validé | Limite conservée |
|---|---|---|
| `2bf5e44f5d06529b26208a1ac7c9a4c158d48aad` | ajoute le profil de spans de présentation borné et opt-in `presentation-profile` | instrumentation applicative non qualifiante ; ne remplace jamais `DisplayedTime` ni un FPS écran |
| `e1e818fc1ed8655b851e34007039f4b288941377` | ajoute `bevy/debug` à cette seule feature et teste les vrais noms de systèmes Bevy | le binaire instrumenté et son target partagé restent non qualifiants ; aucun coût n'est attribué sans A/B normal au même commit |
| `9db35a8e125c6c60cde9de66a7f6aa7585215bfa` | ajoute le garde de sessions ETW au harnais gouverné, avec mutex machine `Global\OpenRPGForge-Gate1-PresentMon-Guard`, deux préflights et postflight | refuse une session préexistante au lieu de l'arrêter ; le fallback ne termine que l'identifiant possédé par le run ; ne ferme aucune route à lui seul |

Le validateur schema 7 teste aussi le parsing insensible à la casse des seuls identifiants Gate 1
attendus, borne l'appel `logman query -ets`, vérifie l'absence de la session possédée après arrêt et
interdit explicitement l'arrêt d'une session non possédée. Le profil corrigé `e1e818fc` produit des
spans applicatifs utilisables, mais sa synthèse
`vulkan-presentation-profile-fixed-validator-20260902T161518176Z`, SHA-256
`ba7afea882a5b44095ad1e53bbff87ec053bed2d1c1fe075d30bd2de775ba668`, conserve
`qualifying=false`.

### Run Release normal gouverné `9db35a8e`

Artefacts externes non versionnés :

```text
E:\OpenRPG_Forge\.gate1-tools\presentmon\diagnostics\vulkan-governed-normal-9db35a8-20260902T163730202Z
```

Le rebuild utilise uniquement les features normales `default,native`, sans `presentation-profile`,
`bevy/trace` ni `bevy/debug`. Le target est neuf, le worktree reste propre et le binaire Release exact
mesure 82 333 696 octets, SHA-256
`c3790da4a7a9f46b36f0837144a75f5afdd43a63101093d25f402d09e0e80c4a`.

| Invariant ou mesure | Résultat |
|---|---:|
| durée QPC runtime | 29,9824273 s, intervalle demi-ouvert exact |
| ticks contrôleur | 1 800, 900 avant + 900 arrière, zéro catch-up |
| frames main-world couvertes | 3 878 |
| lignes PresentMon dans l'enveloppe | 3 877 |
| présentations affichées / non affichées | 3 860 / **17** |
| `DisplayedTime` moyen / FPS moyen | 7,7684 ms / **128,7260 FPS** |
| p95 / p99 | **13,9112 / 13,9657 ms** |
| 1 % low | **58,3489 FPS** |
| `>16,7 ms` / `>33,3 ms` | 17 / **0** |
| maximum consécutif `>16,7 ms` | **1** |
| pertes ETW événements / buffers | **0 / 0** |
| fenêtre | handle stable, visible, jamais minimisée et au premier plan sur toute la fenêtre |
| warnings / erreurs / panic runtime | **0 / 0 / 0** |

Le résultat satisfait FPS moyen, p95, 1 % low, absence d'intervalle au-dessus de 33,3 ms et absence de
deux intervalles lents consécutifs. Le prédicat ajouté par le harnais
`bounded_sample_has_no_not_displayed_frames` reste faux à cause des 17 lignes `DisplayedTime=NA`.
La validation de 16 902 octets porte le SHA-256
`61223f1362c85cc7f0990279df2dd77c9ca6a71b8a9b9eb9c834d697d76fd571`; le CSV porte
`12052e2efb218837b1985528b06b68f6e44fe22af1e25f91608c102ba31c9f7b` et le ledger
`acf0a7bd21034335eb090f52d8f9e9c7702724664d0b9f24e71e708c62b1eca1`.

### Répétabilité et distribution des présentations non affichées

| Route | Lignes CPU bornées | `DisplayedTime=NA` | FPS moyen | p95 | 1 % low | `>33,3 ms` | max. consécutif `>16,7 ms` |
|---|---:|---:|---:|---:|---:|---:|---:|
| Auto gouverné | 3 877 | 17 | 128,7260 | 13,9112 ms | 58,3489 FPS | 0 | 1 |
| Auto répétition A1 | 3 896 | 16 | 129,3634 | 13,9130 ms | 53,0763 FPS | 0 | 2 |
| Auto répétition A2 | 3 890 | 21 | 128,9631 | 13,9079 ms | 54,9590 FPS | 0 | 1 |
| FIFO contrôle non qualifiant | 3 907 | 19 | 129,6567 | 13,9083 ms | 57,3937 FPS | 0 | 1 |

Sur les quatre runs, 73 des 15 570 lignes CPU bornées sont non affichées, soit **0,469 %**. Leur
distribution n'est pas uniforme : 37 surviennent entre `+0,0096` et `+0,7417 s`, 32 entre
`+2,8528` et `+3,1949 s`, puis seulement quatre tardivement à `+16,005`, `+23,071`, `+25,619` et
`+25,676 s`. Ainsi, **69/73, soit 94,5 %**, appartiennent à deux transitoires reproductibles.

Les lignes non affichées ont elles-mêmes un `FrameTime` de 7,67 à 11,37 ms et un `GPUTime` d'environ
5,36 à 9,05 ms. Les pipelines sont prêts avant et pendant la mesure, il n'y a aucun catch-up ni perte
ETW, et la fenêtre reste au premier plan. Un intervalle rétrospectif commençant à `+3,5 s` serait donc
trompeur : il réduirait la campagne sous 30 secondes et laisserait encore 1/0/2/1 lignes non affichées.
Une sous-fenêtre commune choisie pour obtenir zéro `NA` ne serait pas une preuve qualifiante. Un futur
settle de premier plan ne serait recevable que s'il précède une nouvelle fenêtre complète et prédéfinie
de 30 secondes ; les données actuelles ne prouvent pas qu'il supprimerait le second transitoire ou les
quatre événements tardifs.

### A/B diagnostique avec PresentMon officiel 2.5.1

Le binaire officiel 2.5.1 mesure 956 768 octets, porte le SHA-256
`9bec3083069f58f911e6a512f4806db51a27bd096103087bc1d05ef54c80a191` et une signature Authenticode
Intel valide. Le run A4 réemploie exactement le binaire runtime Release normal `9db35a8e` :

```text
E:\OpenRPG_Forge\.gate1-tools\presentmon\diagnostics\vulkan-normal-presentmon-2.5.1-a4-9db35a8-20260902T173500000Z
```

| Mesure A4 | Résultat |
|---|---:|
| durée QPC | 29,9871804 s |
| lignes CPU / affichées / non affichées | 3 908 / 3 893 / **16** |
| FPS moyen | **129,7334** |
| p95 / p99 | **13,9102 / 13,9572 ms** |
| 1 % low | **61,4409 FPS** |
| `>33,3 ms` / max. consécutif `>16,7 ms` | **0 / 1** |
| pertes ETW événements / buffers | **0 / 0** |

Ce run est explicitement **NONQUALIFYING**. Le runtime exige encore dans son signal l'identité
épinglée 2.4.1 ; le helper fournit donc un shim d'identité 2.4.1 alors que le processus de capture réel
est 2.5.1. La synthèse énumère cette incompatibilité, le target partagé et l'absence de rebuild
gouverné pour refuser la qualification. Elle porte le SHA-256
`aac662f7177faeef2b26e923061517bf6cf4b80a80543e8de31862fe3de17b28`; le CSV porte
`5053dae9491c5665077d430741258d2cadf1ebcce627506820e137ea8c5db2d2` et le ledger
`718270959d9ccb1bd6711cc6bcae6e1cc3df70b4f987eba5a9ae16a6e646e95f`.

Les distributions 2.4.1 et 2.5.1 sont proches. A4 ne justifie donc ni d'ignorer les présentations non
affichées, ni de changer silencieusement l'outil épinglé, ni de déclarer Gate 1 passée.

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

Un probe séparé avec le PresentMon signé livré par AMD reproduit alors le défaut : 72/72 pipelines
prêts, zéro ligne et 58 122 événements ETW perdus en cinq secondes. L'enquête postérieure identifie
cependant la session orpheline `OpenRPGForge-Gate1-Diagnostic-85a698103aaf4186a480beabd1840099`
comme cause racine sur cette machine. Après son arrêt ciblé, PresentMon 2.4.1 produit de nouveau des
lignes avec zéro perte ETW. Les trois pilotes et le probe restent **INVALIDES — acquisition ETW**,
mais ils ne prouvent ni une régression DX12, ni un besoin d'augmenter les buffers, ni une performance
du produit. L'élévation n'avait apporté que des privilèges de métadonnées processus et n'avait pas
traité la session concurrente.

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
`06b5fd404bb4c3918b4b7ea154c8d7878609fa8d` ou
`9db35a8e125c6c60cde9de66a7f6aa7585215bfa`. Il expose quatre jobs — `wasm`,
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

- `R1-048` : **OUVERTE** — la cadence principale passe localement à `9db35a8e`, mais le rejeu
  gouverné schema 8, le GPU cible et la revue humaine restent dus ;
- `R1-037` : **OUVERTE** ;
- `R1-041` : **OUVERTE** ;
- run Release normal gouverné `9db35a8e` : 128,7260 FPS, p95 13,9112 ms, 1 % low 58,3489 FPS,
  zéro `>33,3 ms`, maximum consécutif 1 et ETW 0/0 ; **17 présentations non affichées**, donc
  `gate1_qualified=false` ;
- garde ETW `9db35a8e` : préflights/postflight et mutex machine validés ; aucune session étrangère
  n'est arrêtée automatiquement ;
- répétitions Auto : 16 et 21 présentations non affichées ; FIFO diagnostique : 19 ;
- diagnostic officiel PresentMon 2.5.1 A4 : métriques concordantes et 16 présentations non affichées,
  mais **NONQUALIFYING** à cause du target partagé, de l'absence de rebuild gouverné et du shim
  d'identité 2.4.1 dans le signal runtime ;
- baseline Release `6f0e519a` : trois runs `AutoNoVsync` structurellement valides, verdict **FAIL** ;
- reçu de timeout `06b5fd40` : **PASS fonctionnel**, diagnostic d'échec uniquement, sans métrique de
  performance ;
- DX12/FXC : attente de compilation prouvée, 40/69 prêts et 29 en création à 1 200 ticks ;
- DX12/DXC dynamique : 69/69 prêts, acquisitions historiques **INVALIDES** ; les pertes ETW ont pour
  cause racine locale la session PresentMon orpheline ensuite arrêtée par son identifiant exact ;
- DXC statique `a59f560c` : **NON PROMU**, build interrompu par l'absence d'ATL/MFC ;
- campagne opt-in `2fe48485` : **NONQUALIFYING**, trace interne proche de `8b` mais divergence
  trace/PresentMon non réconciliée ;
- campagne historique `15635b3f` et ses hashes : **préservés** ;
- revue humaine : **non réalisée**, `human_review_performed=false` ;
- Gate 1 : **OUVERTE** ;
- Gate 2 : **INTERDITE** sans passage formel de Gate 1 ou nouvelle dérogation explicite acceptée ;
- CI distante du commit courant : **NOT RUN** ; le run historique `33584774630` attaché à
  `15635b3f` reste **NOT RUN** sous ADR-0006 ;
- asset Cadle copié dans Forge : **aucun**.

La prochaine acquisition qualifiante doit conserver le runtime Release normal, le target gouverné
propre, les 30 secondes et les 1 800 ticks complets. Elle doit conserver et compter toutes les
présentations non affichées, prouver une résidence continue et appliquer l'intégralité des seuils
d'ADR-0008, sans choisir rétrospectivement une sous-fenêtre favorable. Une éventuelle stabilisation du
premier plan doit précéder une nouvelle fenêtre complète et être enregistrée comme telle. La route doit
ensuite être rejouée sur le GPU cible et complétée par la revue humaine ; l'instrumentation
`presentation-profile` reste un diagnostic interne non qualifiant et ne remplace jamais PresentMon.
