# Tidewater — ombres animées et récepteurs du terrain Studio

Suite : [suppression des mises à jour inutiles des matériaux, candidat 107](2026-09-30-studio-cloud-material-churn.md).
Les mesures de cadence ci-dessous décrivent le candidat 105 avant optimisation.

Suite du [balayage d'exposition](2026-09-30-studio-exposure-sweep.md).
Référence immuable : `1438b1abfcaee3267092b75573014f4d9b4a983c`,
Village 1920 × 1080, capture source à 2 s. Le profil Studio conserve +0,10 EV.
Les horloges d'animation ne sont pas appariées ; aucune parité complète acquise.

## Parcours du candidat 099

Le [smoke](../../../../.tidewater-integration/studio-tidewater-live-shadows-smoke-099/fish-stand-studio-smoke-ledger.json)
passe : chargement du workspace isolé, placement de l'échoppe, sauvegarde,
réouverture, application et Playtest. Le personnage se déplace d'environ
3,62 m. La source reste intacte. Les réglages graphiques persistent, la FFT
océan, ShoreSim, rebond lumineux, AO, halo solaire, brume et flou avec eau
restent actifs. SHA-256 de l'exécutable :
`0e68e0438726470df905c315d645c51c6b9901722efbdd3cf2e0066ab3d3c2c7`.

Une télémétrie de diagnostic suit les passes d'ombres effectivement encodées,
le vent et la génération du graphe. Le nouveau contrôle `-RequireLiveShadows`
mesure cinq secondes avant et après mouvement : chaque couche avance, le
vent avance et la génération reste à 1. Aucune récupération constatée.
Il s'agit de compteurs d'encodage, pas de fences de fin GPU.

| Fenêtre 099 | rAF moyen | p95 rAF | 1 % low rAF | Passes encodées/s |
|---|---:|---:|---:|---:|
| Avant déplacement | 26,17 | 48,70 ms | 19,16 | 26,57 |
| Après déplacement | 27,06 | 41,80 ms | 19,19 | 26,27 |

Ce sont des cadences `requestAnimationFrame` du canvas Studio normal, pas des
timestamps GPU, des FPS présentés ni un benchmark complet de l'île. Elles
restent sous la cible de 60 FPS et n'établissent pas la fluidité de la forêt.

## Régression visuelle identifiée

Le verdict fonctionnel précédent ne suffisait pas : la
[capture +0,10 EV](../../../../.tidewater-integration/studio-tidewater-live-shadows-village-099/village-exposure-010-00.png)
présente une grande zone sombre sur le sol. La
[comparaison](../../../../.tidewater-integration/studio-tidewater-live-shadows-village-099/comparison-010-00.json)
donne 43,03/255 d'erreur moyenne de luminance, contre 9,50 pour la série 098.
Le premier plan atteint 81,73/255. Le candidat 099 n'est donc pas retenu comme
validation visuelle ou distribution corrigée.

Les essais isolés 101/102 désactivent séparément les appels GPU des émetteurs
de relief, plantes et bâtiments. Le défaut disparaît avec les seuls émetteurs
de relief coupés ; les bâtiments gardent leurs ombres. Le filtre de plantes
du premier essai 101 avait un libellé erroné et ne constitue pas une exclusion
valide des plantes ; 102 utilise le libellé réel `source plant shadow`.
Les essais 100 (récepteur statique/live) et 103 (décalage de profondeur)
ne corrigent pas le défaut. Ces scripts sont des outils de diagnostic locaux,
pas un contournement JavaScript intégré au produit.

La cause est dans `tidewater_terrain::scene::PreparedTerrain::new` : ce chemin
prépare et installe directement son fragment WGSL, en contournant
`tidewater_terrain::install_shader`. Il avait le buffer résident dans son
matériau, mais ne dérivait pas les lectures `shadowParams` vers ce buffer.
La profondeur suivait la caméra ; le terrain la lisait avec les anciennes
matrices. L'adaptation `receiver::shader_source` est maintenant appliquée
avant les termes sous-marins existants. Soleil, biais, distances et exposition
ne sont pas modifiés pour masquer le défaut.

## Validation de la correction

Le build WASM release du candidat 105 passe (6 min 51 s), puis sa construction
Tauri passe. L'avertissement préexistant `proc-macro-error2` ne constitue pas
une erreur du projet. SHA-256 de l'exécutable :
`fb05e035cc5c0ca4c73e971dd78e1d4651786439c3a9d48a5954128156dd562b`.

Le [parcours complet 105](../../../../.tidewater-integration/studio-tidewater-live-shadows-smoke-105/fish-stand-studio-smoke-ledger.json)
passe avec les mêmes effets et contrôles que 099, y compris persistance des
réglages et déplacement réel du joueur. La source reste intacte. La génération
reste à 1, les trois couches avancent et aucune récupération n'est comptée.
La cadence rAF vaut 27,01 puis 26,63 images/s ; p95 48,70 puis 48,50 ms et
1 % low 19,12 puis 17,99. Cette correction ne résout donc pas la performance.

La [capture 105 à +0,10 EV](../../../../.tidewater-integration/studio-tidewater-live-shadows-village-105/village-exposure-010-00.png)
a été inspectée face à la source : la grande zone sombre disparaît et les
silhouettes des maisons et des feuillages demeurent. Sa
[comparaison](../../../../.tidewater-integration/studio-tidewater-live-shadows-village-105/comparison-010-00.json)
donne une luminance moyenne de 108,50 contre 109,23 pour la source et une
erreur entière de **9,52/255**, proche des 9,50 du balayage 098. Le premier
plan revient à 10,91/255. La répétition à +0,10 EV donne 9,62/255 ; les plantes
et les effets évoluent maintenant entre ces captures. Les écarts restants,
notamment de détails, accessoires et traitement temporel, restent visibles.

| Exposition 105 | Luminance moyenne | Erreur entière |
|---|---:|---:|
| +0,10 EV, début | 108,50 | 9,52 |
| 0,00 EV | 105,21 | 10,32 |
| −0,10 EV | 102,00 | 12,04 |
| −0,15 EV | 100,36 | 13,19 |
| +0,05 EV | 106,87 | 9,87 |
| +0,10 EV, répétition | 108,55 | 9,62 |

Le formatage ciblé, le contrôle des espaces du diff et le SelfTest du smoke
passent. Le reçu de capture reste `gate_qualified=false` : cette revue locale
n'est pas une revue propriétaire. Les preuves 099 restent conservées, sans
réécriture de leurs résultats. Les processus de ces smokes se sont terminés.

Restent ouverts : fidélité animée avec horloge commune, performance Studio,
traversée de forêt et qualification native/WebGPU complète. Aucun passage de
gate, commit ou publication Git revendiqué.
