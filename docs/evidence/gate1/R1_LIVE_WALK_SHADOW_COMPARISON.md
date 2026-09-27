# R1 — Comparaison des ombres pendant la marche, 9 septembre 2026

## Résultat et décision

Quatre parcours natifs **A/B/B/A terminés**, sorties 0, sur RX 7800 XT Vulkan,
pilote AMD 26.8.1 (LLPC), 1920 × 1080. Même EXE, kit courant de 1662 arbres,
commande `1800:1:0.22`, sept domaines High demandés séparément, VSync demandée
désactivée. Les seuls paramètres différents sont la résolution des ombres
(4096 → 2048) et les cascades (4 → 3). Portée 128 m et contact conservés.

| Passage | FPS moyens parcours | p95 ms | 1 % low FPS | FPS dernier tiers | p95 dernier tiers ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| A — témoin 1 | 48,999 | 33,637 | 27,373 | 31,96 | 35,93 |
| B — candidat 1 | 56,726 | 26,677 | 35,203 | 40,38 | 28,02 |
| B — candidat 2 | 56,645 | 27,004 | 34,284 | 40,16 | 28,42 |
| A — témoin 2 | 49,028 | 33,629 | 27,135 | 32,05 | 35,23 |

Le gain est reproductible dans cette paire inversée : environ **+16 % FPS sur le
trajet complet et +26 % sur son dernier tiers forestier**, mais **60 FPS non atteints**.
La plaine initiale ne doit pas masquer la forêt : les trois tiers complets sont
publiés dans `comparison.json`, sans sélection des meilleures images. Le dernier
tiers du candidat reste à 33,47–34,45 FPS en 1 % low.

Le candidat reste disponible comme option Studio, **non promu au profil canonique**.
Ce résultat remplace l'absence de comparatif vivant pour ce trajet, pas le refus
du précédent protocole stationnaire, la revue artistique ou la qualification
RTX 3060/RX 6600. Il ne justifie pas de répéter les essais figés ou cette paire
inchangée. La prochaine optimisation doit traiter le coût restant, pas chercher
un cadrage ou un échantillon favorable. Aucun seuil de gate modifié.

## Protocole et contrôle des trajectoires

Le harnais natif ajoute `--walk-proof-measure-moving`, distinct des modes
stationnaire et figé. Il collecte les deltas réels pendant les commandes de marche,
après disponibilité du kit et 120 ticks au sol. La première durée de rendu observée
en mouvement est exclue car elle peut chevaucher la préparation ; les 120 ticks de
freinage ne sont pas mesurés. Rapport émis uniquement après fin du replay.

Chaque échantillon conserve tick de replay, tick joueur, pose, état au sol,
caméra et temps virtuel. La simulation reste vivante ; aucune modification du
contrôleur, de sa pose, du terrain ou du seuil stationnaire de 2 cm.

- Durées mesurées 29,963–29,994 s ; 1469/1701/1699/1469 images selon le passage.
- 1028 ticks exacts communs aux quatre passages : écart maximal de position
  0,007507 m, de distance caméra 0,004690 m, yaw et pitch identiques.
- Poses finales après freinage : écart maximal 0,003002 m ; joueur au sol.
- Les statistiques recalculées indépendamment en Python concordent avec
  `FrameDistribution` Rust à 1e-9 ; tous les trois tiers fixes de 600 ticks sont inclus.
- Les temps de phase des animations ne sont pas identiques entre lancements :
  comparaison de parcours vivant répété, pas comparaison pixel à pixel à temps figé.
- Projections d'ombres consignées dans les rapports : composants CPU, pas compteurs
  GPU. Aucun temps GPU total ni attribution exclusive CPU/GPU déduit.

## Implémentation et vérification

Ajout limité au harnais `apps/forge_runtime/src/walk_proof/moving_measurement.rs`
et au routage CLI ; aucun nouveau système installé sans le drapeau explicite.
Le mode refuse simulation figée, positions non finies, échantillons invalides/vides
et dépassement de 100000 échantillons. Les modes de mesure sont uniques et exclusifs ;
captures/metrics/resize/close/adaptation restent incompatibles.

90 tests unitaires Runtime et une intégration **PASS**, Clippy all-targets strict,
build natif, fmt et diff-check **PASS**. L'erreur Clippy initiale (let-else et conversion
numérique de fixture) a été corrigée, sans neutralisation de lint. Aucun contrat
partagé, renderer, dépendance, WASM ou Studio modifié ; pas de suite workspace
ou rebuild Studio répété pour cette instrumentation native.

EXE mesuré : `BE7229B698AAD3C4E2AE907B1A46AC80F6AABE0A7E6BFC905491BFCD1ABEEE28`.
Kit build : `0F1304723BAD88F4AB01D0710DC1D2B7F1B0AD073B2745EC39B90FD6943BBD48`.

## Preuves et nettoyage

Racine `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-moving-shadow-pair-20260909/` :
`Run.ps1`, `Analyze.py`, `input-pins.json`, `run.log`, quatre stdout/stderr et rapports.
Ordre ABBA fixé avant exécution, pins contrôlés avant chaque lancement et après la
série ; aucune exécution écartée. Les warnings Vulkan/position sans `@invariant`
restent conservés, aucun événement ERROR. Aucun rendu artistique capturé ici.

| Rapport | SHA-256 |
| --- | --- |
| 0-baseline.report.json | `CB6FDCA08CF3B81AF240E41C5BC0C5A3B81710C12ADC0DB0B4C35AF7694A2D10` |
| 1-balanced.report.json | `95F9091ECAF939172D6E1F1AC64A3082ECE74A07919A9DCA1543C7859DE5668E` |
| 2-balanced.report.json | `1BAFDDEEDB652EB2BCC723EC8987E743157BBDB3488C0F6EE76F6A4A58AC5505` |
| 3-baseline.report.json | `C07EE657CCCD4669C7C22520EEE108C0E246C1D681EB376928705B52279752F4` |
| comparison.json | `5298935B6D2CC1A651CFDE9E9C3FB5A35111D5DA51A2CE21AB123EE6C0484C2A` |

PIDs 30656, 3272, 31540 et 20980 terminés ; absence vérifiée après la série.
Aucun navigateur ouvert, aucun processus d'essai conservé, aucun commit/push.
Gate 1 reste ouverte ; aucune extrapolation au Studio ou aux GPU cibles.
