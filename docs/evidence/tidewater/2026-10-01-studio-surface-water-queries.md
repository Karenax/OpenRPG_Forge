# Tidewater — hauteurs GPU de la surface dans Studio

Suite du [cache d'éclairage végétal](2026-10-01-studio-plant-light-cache.md),
dans l'objectif complet de l'[ADR-0017](../../ADR/ADR-0017-tidewater-fidelity-integration.md).
Référence figée : `1438b1abfcaee3267092b75573014f4d9b4a983c`.
Cette tranche prépare TW-008/TW-012 ; elle n'active pas encore la nage ni le
post-traitement d'immersion dans Studio.

Suite : [nage raccordée et parcours Studio](2026-10-01-studio-swimming.md).

## Raccordement

`tidewater_surface_queries` utilise le matériau optique réellement préparé
pour l'océan visible, sur le même appareil Bevy/WebGPU. Ses bindings sont
désormais accessibles au calcul. La passe s'exécute après les FFT et le
ressac, avant les caméras. Elle réutilise le solveur eulérien existant : deux
itérations, quatre cascades de déplacement, atténuation selon la profondeur,
vagues côtières et terrain corrigé par les données d'auteur. Aucun plan d'eau
constant ni calcul JavaScript ne remplace ces hauteurs.

Le solveur commun est extrait de la composition du shader d'immersion, dont
la formule reste inchangée. Le paquet borné existant de 2 064 octets réserve
64 points ; seul le point caméra, index 0, est demandé dans ces essais. Les
autres consommateurs pourront allouer leurs points dans le registre partagé.

Le CPU envoie les positions après propagation des transformations. Le GPU
inscrit dans la réponse l'index et l'heure du **uniform réellement consommé**.
La conversion exacte du temps f32 positif normal en en-tête f64 est réalisée
par les bits, pour éviter d'associer une réponse à une horloge CPU plus récente.
Le démarrage à zéro et les réponses périmées restent ignorés par le registre.
Les réponses incompatibles ou non finies sont rejetées sans écraser la dernière
réponse valide.

Le diagnostic Studio des cascades publie `water_surface_queries` : compteur
de travaux encodés, rejets et dernier paquet réellement reçu. Le compteur
d'encodage n'est pas une clôture GPU ; les paquets reçus sont la preuve de
l'exécution du calcul.

## Construction et contrôles

Le [reçu candidat 121](../../../../.tidewater-integration/studio-tidewater-surface-queries-candidate-121/build-proof.json)
reste `BUILD_ONLY`, distinct des preuves d'exécution ci-dessous.

| Élément | Résultat |
| --- | --- |
| WASM release Studio | passe, 6 min 46 s |
| Construction Tauri | passe, 28,87 s |
| `cargo check -p forge_engine --locked --offline`, natif | passe, 3 min 05 s |
| Cinq tests du registre, module de production inclus directement | 5 passent, 0 échec |
| Formatage ciblé et `git diff --check` | passent |
| Clippy moteur `--no-deps -- -D warnings` | échoue, 64 diagnostics |

SHA-256 exécutable :
`8514349f01fbe0830f31517151b1d8b56e34ab4214c2b190d4e251852f84063c`.
SHA-256 WASM :
`ddd500e3fdd5544bc86038df0ecd0aeba1fced830d5534abf03d054052c36c4a`.

Le premier check utilisait `visibility(all)`, qui déclenchait un avertissement
de macro sur la configuration `webgpu`. La version construite utilise
explicitement `visibility(vertex, fragment, compute)`. L'avertissement futur
préexistant de `proc-macro-error2` reste dans le journal de construction.
Clippy relève notamment les conversions, emprunts explicitement libérés et
fonctions longues dans les modules optique/ressac/stand. Aucun diagnostic de
cette exécution ne vise le nouveau module `tidewater_surface_queries` ; cela
ne rend pas le contrôle global vert. Aucun lint global n'est désactivé ici.

Journaux :

- [build WASM](../../../../.tidewater-integration/studio-surface-queries-build-121.log),
  [check natif](../../../../.tidewater-integration/surface-queries-native-check-121.log) ;
- [tests du registre](../../../../.tidewater-integration/surface-query-registry-tests-121.run.log)
  : allocation bornée, positions du paquet terminé, réponses initiales/périmées,
  métadonnées incompatibles et valeurs non finies ;
- [Clippy moteur](../../../../.tidewater-integration/surface-queries-engine-clippy-121.log).

## Parcours forêt 122

Même harness et configuration que la série 119, départ X/Z −120/−340,
canvas 1920 × 1080, douze segments W/D/S/A de cinq secondes. Aucun compilateur
concurrent pendant les mesures. Ouverture, sauvegarde/reouverture, Playtest,
parcours et retour à l'édition passent dans une copie isolée. Les trois cascades,
FFT et ressac restent animés. Le compteur d'éclairage reste à 2, les générations
restent stables et aucune récupération n'est observée.

| Mesure rAF | Série 122 |
| --- | ---: |
| À l'arrêt | 123,45 images/s |
| Onze segments non profilés, moyenne pondérée | 108,76 images/s |
| p95 par segment, plage | 15,10–16,00 ms |
| 1 % low par segment, plage | 34,19–61,10 images/s |

Ces chiffres mesurent rAF, pas la présentation GPU ; le premier segment
instrumenté est exclu de la moyenne. L'écart avec les 105,54 images/s du 119
ne constitue pas une revendication de gain : les trajectoires chronométrées
et états temporels ne sont pas verrouillés au bit.

Les douze fenêtres reçoivent des paquets valides, dont points, étape et temps
progressent, sans rejet. Le temps f64 reçu est exactement représentable comme
le f32 du shader. La hauteur est nulle dans cette portion terrestre, avec des
fonds de 54 à 102 m : cet essai seul ne suffit donc pas à démontrer des vagues.
La capture finale a été inspectée : relief, plantes, ombres et ciel présents.

Preuves : [smoke 122](../../../../.tidewater-integration/studio-tidewater-surface-queries-forest-122/fish-stand-studio-smoke-ledger.json),
[cadence et cache](../../../../.tidewater-integration/studio-tidewater-surface-queries-forest-122/plant-cache-forest-summary.json),
[paquets GPU](../../../../.tidewater-integration/studio-tidewater-surface-queries-forest-122/surface-query-verification.json),
[capture finale](../../../../.tidewater-integration/studio-tidewater-surface-queries-forest-122/fullscreen-forest-end.png).

## Parcours en mer 123 et témoin temporel

Le même harness est repris avec un départ demandé à X/Z 42/40, dans une autre
copie. Ses noms de fichiers contiennent encore « forest », mais **ce parcours
est en mer**. Le smoke et les douze vérifications de paquets passent sans rejet.
Les hauteurs observées aux extrémités des segments vont de −0,354 à +0,543 m.
Les fonds sont négatifs et les normales sont non nulles.

Un contrôle à l'arrêt conserve exactement le même point X/Z et le même fond :

| Valeur | Avant | Après |
| --- | ---: | ---: |
| X/Z | 42,000499725 / 44,378398895 | identique |
| Fond | −4,602630615 m | identique |
| Étape du shader | 2 808 | 3 467 |
| Heure du shader | 34,234348297 s | 41,809200287 s |
| Hauteur de surface | −0,266776770 m | −0,157827809 m |

La hauteur change de **0,108948961 m**, soit 10,895 cm, au même point.
Cela prouve que ce consommateur reçoit la surface animée GPU, plutôt qu'un
niveau horizontal constant. La fenêtre de mesure rAF de cinq secondes n'est
pas la durée entre ces deux snapshots ; les heures réelles figurent ci-dessus.

Preuves : [smoke 123](../../../../.tidewater-integration/studio-tidewater-surface-queries-sea-123/fish-stand-studio-smoke-ledger.json),
[paquets GPU](../../../../.tidewater-integration/studio-tidewater-surface-queries-sea-123/surface-query-verification.json),
[témoin à point fixe](../../../../.tidewater-integration/studio-tidewater-surface-queries-sea-123/surface-query-temporal-witness.json),
[capture sous la surface](../../../../.tidewater-integration/studio-tidewater-surface-queries-sea-123/fullscreen-forest-end.png).

Le joueur reste actuellement piloté par la marche terrestre générique : il
marche sur le fond. La capture montre aussi que la brume/diffusion d'immersion
n'est pas encore active. Ce test ne qualifie donc **ni la nage ni le rendu
sous l'eau**.

## Préservation et suite

Les sources sont intactes dans les deux ledgers et dans la vérification finale.
SHA-256 d'`authoring.json` :
`50bfd655a7fb4f5a61e4593240ea86dc8d13136d82d558d34737a41b4c6c8720`.
Les processus de test possédés sont fermés. Aucun nouvel asset source n'est
importé ; aucune publication ni distribution publique n'est faite.

Prochaine étape : raccorder le `TidewaterPlayerPlugin` existant au registre
partagé dans Studio, puis le post-traitement d'immersion et le masque de la
surface visible. Vérifier entrée dans l'eau, nage, plongée, remontée et retour
plage avec sauvegarde et retour à l'édition. La comparaison directe de ces
paquets au jeu de référence source, la parité temporelle des images, le contrôle
qualité complet et les autres lots du plan restent ouverts. L'objectif global
reste actif.
