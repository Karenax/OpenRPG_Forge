# Point 5 — mesures et préchauffage

10 septembre 2026 — **R1-034/037/038 soldés pour le candidat local : natif PASS,
mesures GPU Studio WAIVED sous ADR-0012. R1-039 PASS local natif/Studio.
Point 5 soldé dans ce périmètre ; Gate 1 ouverte.**

## Préchauffage explicitement acquis

Le service partagé prépare les quatre niveaux puis restaure le profil initial.
Trois essais natifs, dont cycles après préparation clair/pluie, et deux essais
Studio par le bouton réel sont acquis. Exports Ready 5/5, reçus GPU courants,
captures inspectées, sources préservées et processus fermés. 696 tests PASS,
un timing ignoré ; Clippy natif/Web et builds PASS. Voir
`R1_PROFILE_PREWARM.md` pour les identités, durées et limites. Pas de promesse de
préchauffage des scènes futures ni de fluidité des transitions (R1-040 séparé).
L'historique suivant conserve les mesures et limites de leurs acquisitions.

## Clôture des capacités de mesure — candidat courant

Une acquisition native bornée (480 frames, pas une qualification FPS) a été exécutée
dans `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-measures-closure-20260910-b`.
Runtime `50991d48b8b00716847f805aa821f72fdd5ded9e9bb52b5d2dfba3f4d69e5e8f`,
huit entrées revérifiées avant/après, forêt/kit/eau/nuages conservés, RX 7800 XT Vulkan.

- R1-034 : capture Full HD réellement inspectée, CPU 0,019 ms et GPU 0,939 ms
  de passe opaque affichés. Export final distinct : CPU 0,0181 ms, GPU 0,91592 ms.
  Ce ne sont pas des temps GPU totaux ni des relevés atomiques avec le PNG.
  Overlay CPU Studio déjà observé ; partie GPU Studio dérogée.
- R1-037 : capture/replay natif intégré déjà acquis, SHA RDC
  `0484634ec6122cd72665fc360d6ef3401d21e076e2409d4aa006d2a9267131c7`,
  analyse `698a36c47f17f55d767c8044baedeaa55b171e8e70afe80cb76b1a9c8cf13a31`.
  1002 draws, 6424 instances et 9325611 triangles soumis, une frame, Vulkan
  non dégradé. Voie capture externe ADR-0004 satisfaite sur le backend natif ;
  couverture GPU Studio dérogée. Aucun compteur global d'overlay revendiqué.
- R1-038 : rapport natif courant à frame 436 : **1403996632 octets alloués,
  1838633268 réservés**, 628 allocations, 8 blocs. Estimation allocateur, pas
  mémoire résidente ni GPU total processus ; mémoire Studio dérogée.
- Sortie 0, arrêt réussi sans ERROR, PID 30128 absent au contrôle final.
  Première commande refusée avant rendu (code 2, garde du profil optique), conservée
  dans le dossier sans suffixe `-b` ; corrigée par le parcours live autorisé
  `--player-camera --walk-proof 1:0:1`, sans affaiblir la validation des arguments.

Pins des nouveaux résultats :

| Fichier | SHA-256 |
| --- | --- |
| capture.png | `e6642e0d396171158950bfeb2476380d6f606e9e11f0332b2f945a58857c7351` |
| metrics.json | `1a2478efcb9d8089f63d24b7e6dafab9b22ba716ba4b98e7fb3abe787bee23a0` |
| receipt.json | `c728c1e6189449784297c07b241619f71c5856a7e1fcdbed41676413dba6d52f` |

`scripts/Verify-Gate1Measurements.py --evidence-root E:/OpenRPG_Forge/.gate1-evidence-runtime`
revérifie les hashes, les huit entrées, l'arrêt sans erreur, les unités/valeurs CPU/GPU,
les totaux allocateur et le replay natif ; résultat PASS. Le résultat garde
`gate1_pass=false` et `warmup_qualified=false`. Cette clôture de capacités ne dispense
pas du dossier final sur révision exacte, des revues humaines ou du préchauffage.
`Test-Gate1Measurements.py` : trois tests PASS, dont dix cas invalides rejetés
(GPU absent/non fini/négatif/booléen, faux draws d'overlay, mémoire incohérente,
indisponibilité, replay dégradé ou vide). `git diff --check` PASS. Pas de build
Rust nécessaire : le renderer et ses sources n'ont pas été modifiés.

## Historique avant clôture des trois capacités

**Décision postérieure aux acquisitions :** sous-parties mesures GPU Studio de
R1-034/037/038 `WAIVED / ACCEPTED` selon
[ADR-0012](../../ADR/ADR-0012-gate1-studio-gpu-measurements-waiver.md).
Les manques Studio du tableau restent factuellement non acquis mais ne bloquent
plus à eux seuls Gate 1. Aucun PASS mesuré substitué ; natif, CPU et préchauffage
restent hors dérogation. Les constats d'acquisition ci-dessous sont conservés.

Complément exécuté : capture/replay du candidat natif intégré (1002 draws, 6424
instances, 9325611 triangles, durées GPU par événement). L'outil conserve désormais
eau et nuages. Voir `R1_CLOSURE_PROGRESS_20260910.md` pour les pins et limites.
Couverture Studio toujours non acquise. **Périmètre** : R1-038 demande une mémoire
estimée ; résidence réelle et total processus sont des limites à déclarer, pas de
nouveaux critères imposés par ce rapport. De même, ne pas imposer un reçu atomique
ou une mesure de chaque passe CPU comme condition supplémentaire de R1-034.

## État effectivement constaté

| Couverture | Implémenté et preuve disponible | Manque pour qualification complète |
| --- | --- | --- |
| CPU/GPU | Overlay courant : durée de frame glissante et statistiques de passe opaque ; valeurs visibles dans les captures du dossier point 7. Studio observé le 10 septembre : CPU 0,100–0,200 ms, GPU indisponible. | Temps GPU Studio non acquis. La passe opaque n'est pas le temps GPU total ; l'exhaustivité de toutes les passes n'est pas un critère ajouté à R1-034. |
| Draws/triangles/instances | Comptages CPU explicitement distincts des statistiques GPU ; ancienne capture RenderDoc documentée dans R1-037 | Capture exhaustive du candidat final sur chaque backend qualifié, notamment Studio WebGPU. Ne pas utiliser les entités ou primitives opaques comme draws. |
| Mémoire | `gpu_memory.rs` utilise le rapport public de l'allocateur wgpu, toutes les 5 secondes ; Studio conserve l'indisponibilité explicite | Estimation Studio non acquise : le backend navigateur ne fournit pas ce rapport. VRAM résidente et total processus restent hors portée, pas des exigences ajoutées. `null` ne signifie pas zéro. |
| Préchauffage | Garde de stabilité native et Studio ; captures fixes attendent les pipelines prêts et au moins 30 observations stables ; les vidéos réutilisées vérifient la préparation avant mouvement | Ce n'est pas une préparation exhaustive anticipée de toutes les variantes ; absence de hitch après changement non démontrée. |

Les acquisitions de ce lot ajoutent **14 captures fixes bornées**, dont 12 retenues,
sans erreur runtime. Elles vérifient les gardes existantes, pas un nouveau système de
préchauffage. Les captures optiques fixes interdisent les mesures de performance :
le collecteur respecte ce contrat, il n'assouplit pas le garde moteur.

## Travail de ce lot

- Collecteur raccordé au candidat eau forward, nuages et forêt en batches.
- Vérification de la stabilité caméra/pipelines, arrêt propre, chargement kit/pont,
  projection terrain High et présence des batches, puis hashes des sorties.
- Tests : collection complète et six corruptions rejetées (absence de demande de
  capture, instabilité, mauvaise caméra, mauvais terrain, batches vides, ERROR).
- Aucun changement moteur, aucun nouveau benchmark ni build Rust dans ce lot.

## Suite nécessaire

R1-037 conserve pour le natif les voies de l'ADR-0004 : capture externe reproductible
ou instrumentation exhaustive. La seule couverture des mesures GPU Studio est
désormais dérogée par ADR-0012 ; l'exception Gate 0 n'a pas été étendue. Aucun fork
moteur introduit. Les compteurs publics restent honnêtement partiels.
Il reste également à qualifier le préchauffage des variantes au-delà de l'attente
de disponibilité de la vue initiale. Ne pas relancer une campagne FPS : point 2 dérogé.

Preuves exactes et limites : [dossier point 7](R1_FINAL_DOSSIER_POINT7.md).
