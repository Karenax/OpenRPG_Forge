# R1 — Durées GPU de replay, 9 septembre 2026

## Complément courant — observations en déplacement, sans nouveau replay

### Séparation des phases CPU pendant la marche

Les cinq frontières de temps réel déjà utilisées par le harnais stationnaire
sont réutilisées pendant la marche, avec leurs valeurs associées à chaque pose.
Aucun profiler supplémentaire ou changement de renderer. Le champ reste null
si la mesure manque ; l'essai réel refuse les poses sans cinq valeurs finies.
95 tests Runtime, Clippy all-targets, build natif et fmt PASS.

Un parcours optique High courant terminé, même kit et protocole, nouveau binaire
dev instrumenté. 1651 poses, 29 observations de passes, trois tiers 680/558/413
poses. Moyennes de phases en ms :

| Intervalle | Premier tiers | Deuxième tiers | Tiers forestier |
| --- | ---: | ---: | ---: |
| Hors First→PostUpdate : Last, rendu, présentation, attentes | 10,041 | 13,577 | 19,827 |
| First→PreUpdate | 0,385 | 0,353 | 0,350 |
| Boucle fixe | 0,791 | 0,864 | 1,036 |
| Update | 0,237 | 0,209 | 0,201 |
| SpawnScene→PostUpdate | 3,226 | 2,919 | 2,756 |

La dégradation forestière de ce parcours se situe principalement dans le premier
intervalle, qui **n'est pas un temps GPU pur** et inclut aussi du travail CPU de
rendu. Les phases ordinaires du monde ne croissent pas de façon comparable.
Décision : ne pas optimiser davantage les petits systèmes de jeu/télémétrie pour
expliquer ce déficit ; examiner extraction/rendu, GPU, présentation et attentes.
Les compteurs de passes partiels ne permettent toujours pas de répartir tout cet
intervalle, ni de supprimer arbitrairement une passe coûteuse.

Exécution `Run-WalkRenderSamples-20260909.ps1 -CpuPhases`, racine
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-walk-cpu-phases-20260909/`.
Entrées épinglées inchangées, trajet terminé au sol, code 0, aucun ERROR,
PID 6920 fermé et absence contrôlée. Aucun navigateur, capture ou build concurrent.
Logs `walk-cpu-phases-*-20260909.log` dans le parent. Rapport :
`3D2986657996BB9720E056C5969ACB3F7BCC3CF708A17D6E187E9F3BC1A115FB`.
EXE : `6FF06DF81B0701865B17FC7EFE51B48EA90EADE1A1FD81E74A47F14C7957C626`.
Pas de nouvelle qualification release/GPU cible/art ni de gain produit annoncé.

### Première collecte des passes existantes

Le harnais natif `--walk-proof-measure-moving` conserve maintenant les diagnostics
Bevy existants `render/*/elapsed_gpu` et `elapsed_cpu` une fois par 60 ticks de
rejeu. Limites : 64 snapshots, 128 chemins par snapshot, troncature signalée,
unités originales, valeurs manquantes/invalides distinctes de zéro. Aucun nouveau
query GPU, changement moteur/shader ou collecte dans le jeu interactif. L'âge
publié est celui de la mesure dans `DiagnosticsStore`, **pas** une datation de
la frame GPU d'origine. Lectures asynchrones, coût de collecte inclus ; ni somme
GPU totale ni attribution atomique à la pose de lecture.

95 tests Runtime PASS (94 unitaires et une intégration), dont dix tests de marche
ciblés ; Clippy all-targets strict, build natif, fmt et diff-check PASS. Tests
nouveaux : fréquence/plafond sans store et conservation unités/zéro/absent/invalide.
Pas de reconstruction Studio ou de suite workspace répétée : adaptation au seul
rapport du harnais natif, sans modification de contrat métier partagé.

Parcours réel terminé, sortie 0, candidat optique complet et forêt courante,
High et ombres candidates 2048/3/128 m, 1920×1080, RX 7800 XT/Vulkan. Binaire
**dev instrumenté**, pas nouvelle mesure release. 29 snapshots aux ticks 60–1740,
24–28 chemins chacun, aucune troncature ; âge maximal dans le store 5,32 ms.
Moyennes des observations GPU exposées, 10/10/9 lectures selon le tiers :

| Passe | Premier tiers ms | Deuxième tiers ms | Tiers forestier ms |
| --- | ---: | ---: | ---: |
| Transparente | 2,450 | 4,282 | 6,169 |
| Opaque | 0,903 | 1,091 | 1,186 |
| Transmissive | 0,043 | 0,179 | 0,878 |

Ce ne sont pas des durées intégrées sur toutes les frames. La passe transparente
domine les passes **exposées**, mais ombres et génération d'éclairage ne sont pas
couvertes complètement. Cela ne démontre ni que les seules feuilles expliquent
tout le ralentissement ni un bottleneck CPU. Suite : identifier les soumissions
et le coût non couvert sur le candidat courant, sans refaire BLEND/MASK ou réduire
encore légèrement les distances. Aucun gain produit ou passage de gate annoncé.

Script externe exécuté : `Run-WalkRenderSamples-20260909.ps1`, racine
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-walk-render-samples-20260909/`.
Rapport, arguments, pins, stdout/stderr conservés. Sources d'entrée inchangées,
trajet complet et arrivée au sol, aucun ERROR. PID 30648 terminé, absence vérifiée.
Pas de capture ni navigateur ouverts. Logs de validation `walk-render-samples-*-20260909.log`
dans le parent. Hash EXE :
`E70C0BBEC7E48CFC6DFAB5B10292C0D90583043E1D22784B3354F2A66CF25020`.
Hash rapport : `86522ED46A0E989EBC4A97C6A14632796C6D4A873D8B9B1C21D1710388346B35`.

## Acquisition terminée, portée historique explicite

Relecture isolée réussie d'une **capture existante du 8 septembre**, sans nouveau
lancement Forge, capture, modification de Bevy ou injection. Outil officiel
RenderDoc 1.46 épinglé par le host. Extraction opt-in via `EventGPUDuration`.
La capture n'est pas le candidat colorimétrique courant, ni le parcours ABBA du
9 septembre : elle utilise le kit rev.4 `97AFCE2D…`, l'EXE historique `281218D7…`
et le WorldSketch externe `C815E2B9…` consignés dans son `inputs.json`.

| Groupe de marqueurs racine | Événements feuilles | Somme GPU de replay ms |
| --- | ---: | ---: |
| main_transparent_pass_3d | 437 | 23,83 |
| quatre cascades directionnelles | 587 | 5,139 |
| lightprobe_radiance_map | 9 | 3,97 |
| main_opaque_pass_3d | 294 | 2,72 |
| early prepass | 295 | 1,48 |
| lightprobe_irradiance_map | 1 | 0,682 |
| ssr | 1 | 0,362 |

2042 résultats feuilles, dont couverture des 1780 feuilles draw/dispatch.
Comptabilité inchangée : 1641 draws, 3065 instances soumises et 5755071 triangles,
un Present. Les parents MultiAction ne sont pas additionnés aux enfants.
Somme de toutes les durées feuilles ~39,52 ms : **pas un frame time ni des FPS**.
Le découpage par événements et les timestamps du replay peuvent perturber
l'ordonnancement ; ne pas assimiler ces sommes à une passe mesurée en jeu vivant.

## Conséquence pour la suite

Les ombres ne sont pas le seul chemin à examiner. La passe transparente domine
**cette relecture** ; elle ne désigne pas à elle seule les feuilles, l'eau ou un
matériau précis. Les cartes de radiance/irradiance forment une piste distincte.
Avant modification du rendu courant, identifier ses soumissions concernées et
la fréquence réelle de régénération. Ne pas refaire les essais BLEND/MASK,
compteurs GPU désactivés ou 128 → 96 m déjà non concluants à entrées inchangées.
Ne pas optimiser les règles de génération ou la télémétrie CPU sans coût établi.

Inspection source Bevy 0.19.1 du 9 septembre : `light_probe/generate.rs` programme
`downsampling_system` puis `filtering_system` à chaque Render, sans condition
de changement. `atmosphere/environment.wgsl` calcule cependant sa cubemap avec
`get_view_position()` et la sky-view LUT. Cacher cette carte au seul hash du
profil/soleil serait incorrect pendant les déplacements. Aucun cache approximatif,
fork Bevy ou désactivation de l'éclairage introduit. Vérifier d'abord le candidat
release courant, les derniers parcours étant compilés en dev opt-level 1.

Cette preuve **ne démontre aucun nouveau gain produit**. Le comparatif vivant
`R1_LIVE_WALK_SHADOW_COMPARISON.md` reste la preuve de cadence du candidat actuel.
Gate 1, art, performances cibles et parité Studio restent ouverts.

## Outillage implémenté et contrôlé

`scripts/renderdoc/gate1_renderdoc_timing.py` extrait le compteur, vérifie sa
représentation Float64 secondes et conserve événement, ancêtres et millisecondes.
Refus des compteurs indisponibles, unités inattendues, résultats manquants,
événements inconnus/dupliqués et durées négatives/non finies. Zéro reste zéro.
Le parcours comptable par défaut ne collecte pas de nouvelles durées.

Le host accepte `-GpuDurations` uniquement avec une capture existante épinglée.
Exécution réelle :

```powershell
./scripts/Test-RenderDocPython.ps1 -ReplayCapture E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-renderdoc-capture-20260908-d/candidate_frame305.rdc -ReplaySha256 B5BAA8D980362DC85D9F5507A9CE8657C6ED98EBB59259ED9904961AA35AB62D -GpuDurations -Output E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-renderdoc-gpu-durations-20260909
```

Host et rapport embarqué PASS, sortie 0. **34 tests synthétiques PASS** avec Python
normal ; ils ne sont pas présentés comme tests GPU. Diff-check PASS. Aucun code
Rust/shared renderer ni dépendance changé, aucun rebuild natif/WASM nécessaire.
API vérifiée dans l'[exemple officiel RenderDoc](https://github.com/baldurk/renderdoc/blob/v1.x/docs/python_api/examples/renderdoc/fetch_counters.rst),
pas de copie de son implémentation.

## Preuves et ressources

Racine `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-renderdoc-gpu-durations-20260909/` :
`analysis.json`, `actions.json`, `duration-root-groups.json`, `smoke.json`,
`host-result.json`, `memory.jsonl`, `replay-input.json` et logs du replay.
`duration-groups.json` conserve un premier groupement au marqueur le plus profond,
qui peut être un conteneur indirect : utiliser **duration-root-groups.json** pour
le tableau ci-dessus. Aucun événement supprimé pour fabriquer le classement.

- Capture source : `B5BAA8D980362DC85D9F5507A9CE8657C6ED98EBB59259ED9904961AA35AB62D`.
- `analysis.json` : `901F197D4443185795CBB3C4DB86CC648586101F9AEE7BC6F9C0645CA0BB20D3`.
- `duration-root-groups.json` : `920CF00A270F03BC9517336ACF877BAE39B83C9026915C9C9030600AF74B1DF0`.
- Extracteur timing : `1673488AF448848265E97539F8B5EBFAE18C14267E14867C6D129D36EA7B11EB`.

Processus QRenderDoc **31628 terminé**, reçu host `process_exited=true`, absence
revérifiée. Dernier relevé mémoire à 84,470 s ; minimum physique libre
13297132 Kio et virtuel libre 29602476 Kio. Le seuil de sécurité 2 GiB reste intact.
Profil jetable archivé, aucune session graphique d'essai conservée, aucun push.
